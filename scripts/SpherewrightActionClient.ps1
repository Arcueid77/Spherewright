Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'SpherewrightBridgeClient.ps1')

function Get-SpherewrightInventoryCount {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$PlayerState,
        [Parameter(Mandatory)][ValidateRange(1, [int]::MaxValue)][int]$ItemId
    )

    # An item absent from a complete inventory is zero. A missing inventory or
    # malformed entry is unknown, never zero. Do not access Measure-Object.Sum
    # on an empty pipeline under StrictMode after an already accepted action.
    if ($null -eq $PlayerState.PSObject.Properties['inventory'] -or $null -eq $PlayerState.inventory) {
        throw 'A complete player inventory snapshot is required.'
    }
    [long]$total = 0
    foreach ($entry in @($PlayerState.inventory)) {
        if ($null -eq $entry -or $null -eq $entry.PSObject.Properties['itemId'] -or $null -eq $entry.PSObject.Properties['count'] -or
            -not ($entry.itemId -is [int] -or $entry.itemId -is [long]) -or $entry.itemId -le 0 -or $entry.itemId -gt [int]::MaxValue -or
            -not ($entry.count -is [int] -or $entry.count -is [long]) -or $entry.count -lt 0 -or $entry.count -gt [int]::MaxValue) {
            throw 'Inventory entries require positive integer item IDs and nonnegative integer counts.'
        }
        if ($entry.itemId -eq $ItemId) { $total += [long]$entry.count }
        if ($total -gt [int]::MaxValue) { throw 'Inventory aggregate exceeds the supported count range.' }
    }
    return $total
}

function Get-SpherewrightOwnedSession {
    [CmdletBinding()]
    param()

    $response = Invoke-SpherewrightBridgeRequest -Method 'get_session_state' -Payload @{}
    $session = Get-SpherewrightBridgeResult -Response $response -Operation 'get_session_state'
    if (-not $session.gameLoaded -or -not $session.ownedBySpherewright) {
        throw 'No Spherewright-owned ordinary game session is active.'
    }

    return $session
}

function Wait-SpherewrightAction {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ActionId,
        [Parameter(Mandatory)][string]$SessionId,
        [ValidateRange(1, 1800)][int]$TimeoutSeconds = 180
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $pollDelayMilliseconds = 250
    do {
        $response = Invoke-SpherewrightBridgeRequest -Method 'get_action_result' -SessionId $SessionId -Payload @{
            actionId = $ActionId
        }
        $action = Get-SpherewrightBridgeResult -Response $response -Operation 'get_action_result'
        if ($null -eq $action.PSObject.Properties['actionId'] -or $action.actionId -cne $ActionId) {
            throw 'Action observation identity mismatch; retain the original action and do not resubmit.'
        }
        if ($action.terminal) {
            if (-not $action.succeeded) {
                $failure = [InvalidOperationException]::new("Action $ActionId ended as $($action.state): $($action.message)")
                $failure.Data['spherewrightTerminal'] = $true
                foreach ($field in @('state','failureKind','stalledGameTicks','remainingDistance','doNotRetrySameTarget','recommendedRecovery')) {
                    if ($null -ne $action.PSObject.Properties[$field]) { $failure.Data['spherewright_' + $field] = $action.$field }
                }
                throw $failure
            }

            return $action
        }

        # Native construction can take minutes. Observe the same action with a
        # bounded backoff, without extending its caller deadline or resubmitting.
        $remainingMilliseconds = ($deadline - (Get-Date)).TotalMilliseconds
        if ($remainingMilliseconds -le 0) { break }
        $sleepMilliseconds = [int][Math]::Min($pollDelayMilliseconds, [Math]::Ceiling($remainingMilliseconds))
        Start-Sleep -Milliseconds $sleepMilliseconds
        $pollDelayMilliseconds = [Math]::Min(2000, $pollDelayMilliseconds * 2)
    } while ((Get-Date) -lt $deadline)

    throw "Action $ActionId did not reach a terminal state within $TimeoutSeconds seconds."
}

function Wait-SpherewrightPlayerSettled {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$SessionId,
        [Parameter(Mandatory)][int]$PlanetId,
        [ValidateRange(0, 100)][double]$MaximumSpeed = 0.1,
        [ValidateRange(1, 60)][int]$TimeoutSeconds = 10
    )

    # Returns { settled, player, observations }, NOT a PlayerState DTO.
    # Check .settled, then use .player; a caller error never undoes the action.
    # A completed Move can leave one immediate player snapshot in Walk with
    # residual speed. This is observation only: never prepare or replay an action.
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $observations = 0
    $lastPlayer = $null
    do {
        $response = Invoke-SpherewrightBridgeRequest -Method 'get_player_state' -SessionId $SessionId -Payload @{ planetId = $PlanetId }
        $lastPlayer = Get-SpherewrightBridgeResult -Response $response -Operation 'get_player_state'
        $observations++
        if ($null -eq $lastPlayer.PSObject.Properties['sessionId'] -or
            $null -eq $lastPlayer.PSObject.Properties['planetId'] -or
            $null -eq $lastPlayer.PSObject.Properties['movementState'] -or
            $null -eq $lastPlayer.PSObject.Properties['speed'] -or
            $lastPlayer.sessionId -cne $SessionId -or
            $lastPlayer.planetId -ne $PlanetId -or
            -not ($lastPlayer.speed -is [double] -or $lastPlayer.speed -is [float] -or $lastPlayer.speed -is [decimal] -or $lastPlayer.speed -is [int] -or $lastPlayer.speed -is [long]) -or
            [double]::IsNaN([double]$lastPlayer.speed) -or
            [double]::IsInfinity([double]$lastPlayer.speed) -or
            $lastPlayer.speed -lt 0) {
            throw 'Player settlement readback is malformed or from a different session/planet; the original action result is unchanged.'
        }
        if ($lastPlayer.movementState -ceq 'Walk' -and $lastPlayer.speed -le $MaximumSpeed) {
            return [pscustomobject]@{ settled = $true; player = $lastPlayer; observations = $observations }
        }
        $remainingMilliseconds = ($deadline - (Get-Date)).TotalMilliseconds
        if ($remainingMilliseconds -le 0) { break }
        Start-Sleep -Milliseconds ([int][Math]::Min(250, [Math]::Ceiling($remainingMilliseconds)))
    } while ((Get-Date) -lt $deadline)

    return [pscustomobject]@{ settled = $false; player = $lastPlayer; observations = $observations }
}

function Invoke-SpherewrightNormalAction {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$PrepareMethod,
        [Parameter(Mandatory)][string]$CommitMethod,
        [Parameter(Mandatory)][hashtable]$PreparePayload,
        [Parameter(Mandatory)][string]$SessionId,
        [Parameter(Mandatory)][int]$PlanetId,
        [ValidateRange(1, 1800)][int]$TimeoutSeconds = 180,
        [guid]$IdempotencyKey = [guid]::NewGuid(),
        [scriptblock]$ValidatePrepared
    )

    $startedTicks = [Diagnostics.Stopwatch]::GetTimestamp()
    $phase = 'prepare'
    $commitMayHaveBeenAccepted = $false
    $commitAccepted = $null
    $definitiveCommitRejectionCode = $null
    $actionId = $null
    try {
        $prepareResponse = Invoke-SpherewrightBridgeRequest -Method $PrepareMethod -SessionId $SessionId -Payload $PreparePayload
        $prepared = Get-SpherewrightBridgeResult -Response $prepareResponse -Operation $PrepareMethod
        if (-not $prepared.prepared -or [string]::IsNullOrWhiteSpace([string]$prepared.planToken)) {
            throw "$PrepareMethod did not issue an executable plan token."
        }
        $planToken = [string]$prepared.planToken

        if (-not $prepared.commitAllowedNow) {
            $codes = @($prepared.commitBlockers | ForEach-Object { $_.code }) -join ', '
            throw "$PrepareMethod is currently blocked: $codes"
        }
        $preparedTicks = [Diagnostics.Stopwatch]::GetTimestamp()

        # A bounded callback checks the exact native path, source and budget.
        # Only one explicit Boolean true permits the original commit.
        $phase = 'plan_validation'
        if ($null -ne $ValidatePrepared) {
            $validation = @(& $ValidatePrepared $prepared)
            if ($validation.Count -ne 1 -or $validation[0] -isnot [bool] -or -not $validation[0]) {
                throw "$PrepareMethod exact-plan validation did not approve the prepared plan."
            }
            if ([string]$prepared.planToken -cne $planToken) {
                throw "$PrepareMethod exact-plan validation changed the prepared plan token."
            }
        }
        $validatedTicks = [Diagnostics.Stopwatch]::GetTimestamp()

        $phase = 'commit'
        # Once the request is attempted, an exception is not proof of rejection.
        $commitMayHaveBeenAccepted = $true
        $commitResponse = Invoke-SpherewrightBridgeRequest -Method $CommitMethod -SessionId $SessionId -Payload @{
            sessionId = $SessionId
            planetId = $PlanetId
            planToken = $planToken
            idempotencyKey = $IdempotencyKey.ToString('D')
        }
        # The Plugin returns STALE_STATE before it creates an action or stores
        # the idempotency key. An exact negative reply closes this attempt;
        # missing replies, malformed replies and transport errors remain uncertain.
        if ($null -ne $commitResponse -and
            $null -ne $commitResponse.PSObject.Properties['success'] -and
            $commitResponse.success -is [bool] -and -not $commitResponse.success -and
            $null -ne $commitResponse.PSObject.Properties['error'] -and
            $null -ne $commitResponse.error -and
            $null -ne $commitResponse.error.PSObject.Properties['code'] -and
            $commitResponse.error.code -ceq 'STALE_STATE' -and
            ($null -eq $commitResponse.PSObject.Properties['result'] -or $null -eq $commitResponse.result)) {
            $commitMayHaveBeenAccepted = $false
            $commitAccepted = $false
            $definitiveCommitRejectionCode = 'STALE_STATE'
        }
        $committed = Get-SpherewrightBridgeResult -Response $commitResponse -Operation $CommitMethod
        $committedTicks = [Diagnostics.Stopwatch]::GetTimestamp()
        if ($null -eq $committed.PSObject.Properties['accepted'] -or $committed.accepted -isnot [bool]) {
            throw 'Commit response has no trustworthy acceptance field; reconcile the original intent, never resubmit.'
        }
        $commitAccepted = $committed.accepted
        if (-not $commitAccepted) {
            $commitMayHaveBeenAccepted = $false
            throw 'Commit response explicitly rejected this intent.'
        }
        $actionId = [string]$committed.actionId
        if ([string]::IsNullOrWhiteSpace($actionId)) { throw 'Accepted commit lacks an action ID; reconcile its original intent.' }

        $phase = 'terminal_observation'
        $terminal = Wait-SpherewrightAction -ActionId $actionId -SessionId $SessionId -TimeoutSeconds $TimeoutSeconds
        $terminalTicks = [Diagnostics.Stopwatch]::GetTimestamp()
        $ticksPerMillisecond = [double][Diagnostics.Stopwatch]::Frequency / 1000.0
        return [pscustomobject]@{
            prepared = $prepared
            committed = $committed
            result = $terminal
            observedExecutionGameTicks = $(if ($null -ne $terminal.PSObject.Properties['startedAtGameTick'] -and
                $null -ne $terminal.PSObject.Properties['completedAtGameTick'] -and $null -ne $terminal.startedAtGameTick -and
                $null -ne $terminal.completedAtGameTick -and $terminal.completedAtGameTick -ge $terminal.startedAtGameTick) {
                [long]$terminal.completedAtGameTick-[long]$terminal.startedAtGameTick
            } else { $null })
            timingMs = [pscustomobject]@{
                prepare = [math]::Round(($preparedTicks - $startedTicks) / $ticksPerMillisecond, 3)
                planValidation = [math]::Round(($validatedTicks - $preparedTicks) / $ticksPerMillisecond, 3)
                commit = [math]::Round(($committedTicks - $validatedTicks) / $ticksPerMillisecond, 3)
                terminalObservation = [math]::Round(($terminalTicks - $committedTicks) / $ticksPerMillisecond, 3)
                total = [math]::Round(($terminalTicks - $startedTicks) / $ticksPerMillisecond, 3)
            }
        }
    } catch {
        # Metadata is advisory; never replace the original exception or turn
        # an accepted/uncertain commit into a caller-side rejection.
        try {
            $_.Exception.Data['spherewrightPhase'] = $phase
            $_.Exception.Data['spherewrightCommitMayHaveBeenAccepted'] = $commitMayHaveBeenAccepted
            $_.Exception.Data['spherewrightCommitAccepted'] = $commitAccepted
            if ($null -ne $definitiveCommitRejectionCode) {
                $_.Exception.Data['spherewrightDefinitiveCommitRejectionCode'] = $definitiveCommitRejectionCode
            }
            if ($commitAccepted -eq $true -and $null -ne $committed.PSObject.Properties['idempotentReplay']) {
                $_.Exception.Data['spherewrightIdempotentReplay'] = $committed.idempotentReplay
            }
            $_.Exception.Data['spherewrightElapsedMs'] = [math]::Round(([Diagnostics.Stopwatch]::GetTimestamp() - $startedTicks) / ([double][Diagnostics.Stopwatch]::Frequency / 1000.0), 3)
            if (-not [string]::IsNullOrWhiteSpace($actionId)) {
                $_.Exception.Data['spherewrightActionId'] = $actionId
            }
        } catch { }
        throw
    }
}
