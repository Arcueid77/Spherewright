# Offline regression; no descriptor discovery or game request is made.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SpherewrightActionClient.ps1')
$script:checks = 0
function Assert-Count($State, [int]$Id, [long]$Expected) {
    $actual = Get-SpherewrightInventoryCount -PlayerState $State -ItemId $Id
    if ($actual -ne $Expected) { throw "Inventory count expected $Expected, got $actual." }
    $script:checks++
}
function Assert-Rejected([scriptblock]$Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'Malformed or unknown inventory was silently accepted.' }
    $script:checks++
}
Assert-Count ([pscustomobject]@{inventory=@()}) 1104 0
$before = '{"inventory":[{"itemId":2012,"count":2}]}' | ConvertFrom-Json
$after = '{"inventory":[{"itemId":1104,"count":1},{"itemId":2012,"count":3}]}' | ConvertFrom-Json
Assert-Count $before 2012 2
Assert-Count $before 1104 0
Assert-Count $after 2012 3
Assert-Count $after 1104 1
Assert-Count ('{"inventory":[{"itemId":1104,"count":2},{"itemId":1104,"count":3}]}'|ConvertFrom-Json) 1104 5
Assert-Rejected { Get-SpherewrightInventoryCount -PlayerState ([pscustomobject]@{}) -ItemId 1104 }
Assert-Rejected { Get-SpherewrightInventoryCount -PlayerState ([pscustomobject]@{inventory=$null}) -ItemId 1104 }
Assert-Rejected { Get-SpherewrightInventoryCount -PlayerState ([pscustomobject]@{inventory=@($null)}) -ItemId 1104 }
foreach ($json in @(
    '{"inventory":[{"itemId":1104}]}',
    '{"inventory":[{"itemId":1104,"count":null}]}',
    '{"inventory":[{"itemId":1104,"count":-1}]}',
    '{"inventory":[{"itemId":1104,"count":1.5}]}',
    '{"inventory":[{"itemId":1104,"count":"1"}]}',
    '{"inventory":[{"itemId":1104,"count":true}]}',
    '{"inventory":[{"count":1}]}',
    '{"inventory":[{"itemId":0,"count":1}]}',
    '{"inventory":[{"itemId":"1104","count":1}]}',
    '{"inventory":[{"itemId":1104,"count":2147483647},{"itemId":1104,"count":1}]}',
    '{"inventory":[{"itemId":9999,"count":null}]}'
)) {
    $state = $json | ConvertFrom-Json
    Assert-Rejected { Get-SpherewrightInventoryCount -PlayerState $state -ItemId 1104 }
}
Assert-Rejected { Get-SpherewrightInventoryCount -PlayerState $before -ItemId 0 }

# Deterministic clock/transport stubs exercise the existing caller, not a second
# action executor. No runtime descriptor, pipe, game process or real sleep.
function Assert-Action([bool]$Condition, [string]$Label) {
    if (-not $Condition) { throw "Action-client regression: $Label" }
    $script:checks++
}
function Reset-ActionStub([object[]]$States, [int]$ReadMilliseconds = 0, [object[]]$PlayerStates = @()) {
    $script:actionStubStates = [Collections.Generic.Queue[object]]::new()
    foreach ($state in $States) { $script:actionStubStates.Enqueue($state) }
    $script:playerStubStates = [Collections.Generic.Queue[object]]::new()
    foreach ($state in $PlayerStates) { $script:playerStubStates.Enqueue($state) }
    $script:actionStubCalls = [Collections.Generic.List[object]]::new()
    $script:actionStubSleeps = [Collections.Generic.List[int]]::new()
    $script:actionStubTime = [datetime]'2026-01-01T00:00:00Z'
    $script:actionStubReadMilliseconds = $ReadMilliseconds
    $script:actionStubTransportFailure = $false
    $script:commitStubTransportFailure = $false
    $script:commitStubResult = $null
    $script:commitStubError = $null
    $script:actionStubBridgeError = $null
}
function Get-Date { $script:actionStubTime }
function Start-Sleep([int]$Milliseconds) {
    $script:actionStubSleeps.Add($Milliseconds)
    $script:actionStubTime = $script:actionStubTime.AddMilliseconds($Milliseconds)
}
function Invoke-SpherewrightBridgeRequest([string]$Method, [string]$SessionId, [hashtable]$Payload) {
    $script:actionStubCalls.Add([pscustomobject]@{method=$Method;sessionId=$SessionId;payload=$Payload})
    $result = switch ($Method) {
        'prepare_build' { [pscustomobject]@{prepared=$true;commitAllowedNow=$true;planToken='offline-test-placeholder'} }
        'commit_build' {
            if ($script:commitStubTransportFailure) { throw 'offline commit transport failure' }
            if ($null -ne $script:commitStubError) { return [pscustomobject]@{success=$false;result=$null;error=$script:commitStubError} }
            if ($null -ne $script:commitStubResult) { return [pscustomobject]@{success=$true;result=$script:commitStubResult} }
            [pscustomobject]@{accepted=$true;actionId='offline-action'}
        }
        'get_action_result' {
            if ($script:actionStubTransportFailure) { throw 'offline transport failure' }
            if ($null -ne $script:actionStubBridgeError) {
                return [pscustomobject]@{success=$false;error=$script:actionStubBridgeError}
            }
            $script:actionStubTime = $script:actionStubTime.AddMilliseconds($script:actionStubReadMilliseconds)
            if ($script:actionStubStates.Count) { $script:actionStubStates.Dequeue() }
            else { [pscustomobject]@{actionId='offline-action';terminal=$false;succeeded=$false;state='waiting_for_game'} }
        }
        'get_player_state' {
            if ($script:playerStubStates.Count) { $script:playerStubStates.Dequeue() }
            else { [pscustomobject]@{sessionId='offline-session';planetId=104;movementState='Walk';speed=1.6} }
        }
        default { throw "Unexpected offline method: $Method" }
    }
    [pscustomobject]@{success=$true;result=$result}
}
function Assert-SameActionReads {
    $reads = @($script:actionStubCalls | Where-Object method -EQ 'get_action_result')
    Assert-Action ($reads.Count -gt 0) 'at least one action observation'
    Assert-Action (@($reads | Where-Object { $_.sessionId -cne 'offline-session' -or $_.payload.actionId -cne 'offline-action' }).Count -eq 0) 'same action and session on every observation'
}
$pending = [pscustomobject]@{actionId='offline-action';terminal=$false;succeeded=$false;state='waiting_for_game'}
$success = [pscustomobject]@{actionId='offline-action';terminal=$true;succeeded=$true;state='completed';startedAtGameTick=100;completedAtGameTick=160}
Reset-ActionStub @($success)
$result = Wait-SpherewrightAction -ActionId offline-action -SessionId offline-session
Assert-Action ($result.terminal -and $result.succeeded) 'immediate terminal returned unchanged'
Assert-Action ($script:actionStubCalls.Count -eq 1 -and $script:actionStubSleeps.Count -eq 0) 'no sleep after terminal'
Assert-SameActionReads

Reset-ActionStub @($success)
$validated = Invoke-SpherewrightNormalAction -PrepareMethod prepare_build -CommitMethod commit_build -PreparePayload @{buildingItemId=2001} -SessionId offline-session -PlanetId 104 -ValidatePrepared {
    param($plan)
    return ($plan.prepared -and $plan.planToken -ceq 'offline-test-placeholder')
}
Assert-Action ($validated.result.succeeded) 'exact-plan callback allows one original commit'
Assert-Action (($script:actionStubCalls.method -join ',') -ceq 'prepare_build,commit_build,get_action_result') 'validated plan uses existing single commit and terminal path'
Assert-Action ($null -ne $validated.timingMs -and $validated.timingMs.prepare -ge 0 -and
    $validated.timingMs.planValidation -ge 0 -and $validated.timingMs.commit -ge 0 -and
    $validated.timingMs.terminalObservation -ge 0 -and $validated.timingMs.total -ge 0) 'timing splits added without a second game request'
Assert-Action ($validated.observedExecutionGameTicks -eq 60) 'native execution game ticks remain separate from observation wall time'

Reset-ActionStub @()
Assert-Rejected {
    Invoke-SpherewrightNormalAction -PrepareMethod prepare_build -CommitMethod commit_build -PreparePayload @{buildingItemId=2001} -SessionId offline-session -PlanetId 104 -ValidatePrepared { $false }
}
Assert-Action (($script:actionStubCalls.method -join ',') -ceq 'prepare_build') 'rejected exact plan never commits or polls'

Reset-ActionStub @()
Assert-Rejected {
    Invoke-SpherewrightNormalAction -PrepareMethod prepare_build -CommitMethod commit_build -PreparePayload @{buildingItemId=2001} -SessionId offline-session -PlanetId 104 -ValidatePrepared { throw 'native plan changed' }
}
Assert-Action (($script:actionStubCalls.method -join ',') -ceq 'prepare_build') 'throwing exact-plan callback never commits or polls'

Reset-ActionStub @()
Assert-Rejected {
    Invoke-SpherewrightNormalAction -PrepareMethod prepare_build -CommitMethod commit_build -PreparePayload @{buildingItemId=2001} -SessionId offline-session -PlanetId 104 -ValidatePrepared {
        param($plan)
        $plan.planToken = 'different-token'
        return $true
    }
}
Assert-Action (($script:actionStubCalls.method -join ',') -ceq 'prepare_build') 'mutated prepared token never commits or polls'

Reset-ActionStub @()
$planFailure = $null
try {
    Invoke-SpherewrightNormalAction -PrepareMethod prepare_build -CommitMethod commit_build -PreparePayload @{buildingItemId=2001} -SessionId offline-session -PlanetId 104 -ValidatePrepared { $false } | Out-Null
} catch { $planFailure = $_.Exception }
Assert-Action ($planFailure.Data['spherewrightPhase'] -ceq 'plan_validation' -and
    $planFailure.Data['spherewrightCommitMayHaveBeenAccepted'] -eq $false -and
    @($script:actionStubCalls | Where-Object method -EQ 'commit_build').Count -eq 0) 'precommit validation failure is classified without a commit'

Reset-ActionStub @()
$script:commitStubTransportFailure = $true
$commitFailure = $null
try {
    Invoke-SpherewrightNormalAction -PrepareMethod prepare_build -CommitMethod commit_build -PreparePayload @{buildingItemId=2001} -SessionId offline-session -PlanetId 104 | Out-Null
} catch { $commitFailure = $_.Exception }
Assert-Action ($commitFailure.Message -ceq 'offline commit transport failure' -and
    $commitFailure.Data['spherewrightPhase'] -ceq 'commit' -and
    $commitFailure.Data['spherewrightCommitMayHaveBeenAccepted'] -eq $true -and
    ($script:actionStubCalls.method -join ',') -ceq 'prepare_build,commit_build') 'commit transport failure stays uncertain and is never replayed'

# Exact R1288-shaped native rejection happens before the Plugin accepts an
# action. Keep the negative response distinct from a missing ACK.
Reset-ActionStub @()
$script:commitStubError = [pscustomobject]@{code='STALE_STATE';message='The exact warehouse, ordered grids, inventory, or automation configuration changed.';retryable=$true;recovery='Inspect current state and prepare a fresh action.'}
$staleFailure = $null
try {
    Invoke-SpherewrightNormalAction -PrepareMethod prepare_build -CommitMethod commit_build -PreparePayload @{buildingItemId=2001} -SessionId offline-session -PlanetId 104 | Out-Null
} catch { $staleFailure = $_.Exception }
Assert-Action ($staleFailure.Data['spherewrightPhase'] -ceq 'commit' -and
    $staleFailure.Data['spherewrightCommitMayHaveBeenAccepted'] -eq $false -and
    $staleFailure.Data['spherewrightCommitAccepted'] -eq $false -and
    $staleFailure.Data['spherewrightDefinitiveCommitRejectionCode'] -ceq 'STALE_STATE' -and
    ($script:actionStubCalls.method -join ',') -ceq 'prepare_build,commit_build') 'exact preaccept STALE_STATE is rejected without polling or replay'

Reset-ActionStub @()
$script:commitStubError = [pscustomobject]@{code='REQUEST_TIMEOUT';message='Commit response unavailable.';retryable=$true}
$timeoutFailure = $null
try {
    Invoke-SpherewrightNormalAction -PrepareMethod prepare_build -CommitMethod commit_build -PreparePayload @{buildingItemId=2001} -SessionId offline-session -PlanetId 104 | Out-Null
} catch { $timeoutFailure = $_.Exception }
Assert-Action ($timeoutFailure.Data['spherewrightCommitMayHaveBeenAccepted'] -eq $true -and
    $null -eq $timeoutFailure.Data['spherewrightDefinitiveCommitRejectionCode'] -and
    ($script:actionStubCalls.method -join ',') -ceq 'prepare_build,commit_build') 'timeout-shaped response never becomes definitive rejection'

Reset-ActionStub @() 0
$terminalFailure = $null
try {
    Invoke-SpherewrightNormalAction -PrepareMethod prepare_build -CommitMethod commit_build -PreparePayload @{buildingItemId=2001} -SessionId offline-session -PlanetId 104 -TimeoutSeconds 1 | Out-Null
} catch { $terminalFailure = $_.Exception }
Assert-Action ($terminalFailure.Data['spherewrightPhase'] -ceq 'terminal_observation' -and
    $terminalFailure.Data['spherewrightActionId'] -ceq 'offline-action' -and
    $terminalFailure.Data['spherewrightCommitMayHaveBeenAccepted'] -eq $true -and
    @($script:actionStubCalls | Where-Object method -EQ 'commit_build').Count -eq 1) 'caller timeout preserves original action and does not submit again'

Reset-ActionStub @($pending, $success)
$intent = [guid]'00000000-0000-0000-0000-000000000001'
$result = Invoke-SpherewrightNormalAction -PrepareMethod prepare_build -CommitMethod commit_build -PreparePayload @{buildingItemId=2001} -SessionId offline-session -PlanetId 104 -IdempotencyKey $intent
Assert-Action ($result.result.succeeded) 'short action completed'
Assert-Action (($script:actionStubCalls.method -join ',') -ceq 'prepare_build,commit_build,get_action_result,get_action_result') 'prepare and commit occur exactly once and in order'
Assert-Action (($script:actionStubSleeps -join ',') -ceq '250') 'first pending observation retains quick cadence'
Assert-Action ($script:actionStubCalls[1].payload.idempotencyKey -ceq $intent.ToString('D')) 'original idempotency key preserved'
Assert-Action ($script:actionStubCalls[1].payload.planToken -ceq 'offline-test-placeholder') 'only the original prepared plan committed'
Assert-SameActionReads

Reset-ActionStub @($pending, $pending, $pending, $pending, $pending, $pending, $success)
$result = Wait-SpherewrightAction -ActionId offline-action -SessionId offline-session -TimeoutSeconds 30
Assert-Action ($result.succeeded) 'long action completed'
Assert-Action (($script:actionStubSleeps -join ',') -ceq '250,500,1000,2000,2000,2000') 'exponential observation backoff capped at two seconds'
Assert-Action ($script:actionStubCalls.Count -eq 7) 'bounded long-action observation count'
Assert-Action (@($script:actionStubCalls | Where-Object method -NE 'get_action_result').Count -eq 0) 'wait never prepares or commits'
Assert-SameActionReads

Reset-ActionStub @($pending, [pscustomobject]@{actionId='offline-action';terminal=$true;succeeded=$false;state='failed';message='offline blocked';failureKind='position_stalled';stalledGameTicks=180;doNotRetrySameTarget=$true})
$failure = $null
try { Wait-SpherewrightAction -ActionId offline-action -SessionId offline-session | Out-Null } catch { $failure=$_.Exception.Message; $terminalError=$_.Exception }
Assert-Action ($failure -ceq 'Action offline-action ended as failed: offline blocked') 'terminal failure is propagated'
Assert-Action ($terminalError.Data['spherewrightTerminal'] -eq $true -and $terminalError.Data['spherewright_failureKind'] -ceq 'position_stalled' -and $terminalError.Data['spherewright_stalledGameTicks'] -eq 180 -and $terminalError.Data['spherewright_doNotRetrySameTarget'] -eq $true) 'structured native failure fields retained'
Assert-Action ($script:actionStubCalls.Count -eq 2 -and ($script:actionStubSleeps -join ',') -ceq '250') 'no further observation or sleep after failure'
Assert-SameActionReads

Reset-ActionStub @() 100
$failure = $null
try { Wait-SpherewrightAction -ActionId offline-action -SessionId offline-session -TimeoutSeconds 1 | Out-Null } catch { $failure=$_.Exception.Message }
Assert-Action ($failure -ceq 'Action offline-action did not reach a terminal state within 1 seconds.') 'original caller timeout and handle retained'
Assert-Action (($script:actionStubSleeps -join ',') -ceq '250,500') 'sleep never extends remaining deadline'
Assert-Action ($script:actionStubCalls.Count -eq 3) 'response crossing deadline stops without another sleep or request'
Assert-SameActionReads

Reset-ActionStub @()
$failure = $null
try { Wait-SpherewrightAction -ActionId offline-action -SessionId offline-session -TimeoutSeconds 1 | Out-Null } catch { $failure=$_.Exception.Message }
Assert-Action ($failure -ceq 'Action offline-action did not reach a terminal state within 1 seconds.') 'pending action expires without replay'
Assert-Action (($script:actionStubSleeps -join ',') -ceq '250,500,250') 'last sleep clipped to remaining deadline'
Assert-Action ($script:actionStubCalls.Count -eq 3) 'no poll after expired deadline'
Assert-SameActionReads

Reset-ActionStub @()
$script:actionStubTransportFailure = $true
$failure = $null
try { Wait-SpherewrightAction -ActionId offline-action -SessionId offline-session | Out-Null } catch { $failure=$_.Exception.Message }
Assert-Action ($failure -ceq 'offline transport failure') 'transport uncertainty propagated without retry'
Assert-Action ($script:actionStubCalls.Count -eq 1 -and $script:actionStubSleeps.Count -eq 0) 'transport error never resubmits or sleeps'

$moving = [pscustomobject]@{sessionId='offline-session';planetId=104;movementState='Walk';speed=1.61493}
$settled = [pscustomobject]@{sessionId='offline-session';planetId=104;movementState='Walk';speed=0.0}
Reset-ActionStub @() 0 @($moving, $settled)
$observed = Wait-SpherewrightPlayerSettled -SessionId offline-session -PlanetId 104
Assert-Action ($observed.settled -and $observed.observations -eq 2 -and $observed.player.speed -eq 0) 'transient post-terminal speed settles on bounded reread'
Assert-Action (($script:actionStubCalls.method -join ',') -ceq 'get_player_state,get_player_state' -and ($script:actionStubSleeps -join ',') -ceq '250') 'settlement performs reads only and never replays action'

Reset-ActionStub @() 0 @($moving)
$observed = Wait-SpherewrightPlayerSettled -SessionId offline-session -PlanetId 104 -TimeoutSeconds 1
Assert-Action (-not $observed.settled -and $observed.player.speed -eq 1.6) 'unsettled timeout remains an observation result, not action failure'
Assert-Action (@($script:actionStubCalls | Where-Object method -NE 'get_player_state').Count -eq 0) 'unsettled timeout cannot prepare or commit'

Reset-ActionStub @() 0 @([pscustomobject]@{sessionId='other-session';planetId=104;movementState='Walk';speed=0.0})
$failure = $null
try { Wait-SpherewrightPlayerSettled -SessionId offline-session -PlanetId 104 | Out-Null } catch { $failure=$_.Exception.Message }
Assert-Action ($failure -like '*different session/planet*' -and $script:actionStubCalls.Count -eq 1) 'cross-session settlement readback rejects without replay'

Reset-ActionStub @([pscustomobject]@{actionId='other-action';terminal=$true;succeeded=$true})
$failure = $null
try { Invoke-SpherewrightNormalAction -PrepareMethod prepare_build -CommitMethod commit_build -PreparePayload @{} -SessionId offline-session -PlanetId 104 | Out-Null } catch { $failure=$_.Exception }
Assert-Action ($failure.Data['spherewrightCommitAccepted'] -eq $true -and $failure.Data['spherewrightActionId'] -ceq 'offline-action' -and $failure.Message -like '*identity mismatch*') 'wrong returned action cannot resolve an accepted intent'
Assert-Action (@($script:actionStubCalls | Where-Object method -EQ 'commit_build').Count -eq 1) 'identity mismatch never replays'
foreach ($accepted in @($false, 'true')) {
    Reset-ActionStub @()
    $script:commitStubResult = [pscustomobject]@{accepted=$accepted;actionId=''}
    $failure = $null
    try { Invoke-SpherewrightNormalAction -PrepareMethod prepare_build -CommitMethod commit_build -PreparePayload @{} -SessionId offline-session -PlanetId 104 | Out-Null } catch { $failure=$_.Exception }
    Assert-Action (@($script:actionStubCalls | Where-Object method -EQ 'get_action_result').Count -eq 0 -and @($script:actionStubCalls | Where-Object method -EQ 'commit_build').Count -eq 1) 'invalid/rejected commit is not polled or retried'
    Assert-Action ($failure.Data['spherewrightCommitMayHaveBeenAccepted'] -eq ($accepted -is [string])) 'explicit boolean rejection differs from malformed uncertainty'
}
# Use the real response helper: native read timeouts remain distinguishable
# from descriptor discovery and preserve accepted-action reconciliation.
$timeoutError = [pscustomobject]@{code='REQUEST_TIMEOUT';message='The Unity main-thread read request timed out.';retryable=$true;recovery='Inspect the original receipt; do not infer no execution.';details=@{planToken='private-error-token';authToken='private-error-auth'}}
$failure = $null
try { Get-SpherewrightBridgeResult -Response ([pscustomobject]@{success=$false;error=$timeoutError}) -Operation get_session_state | Out-Null } catch { $failure=$_.Exception }
Assert-Action ($null -ne $failure -and $failure.Data['spherewrightBridgeOperation'] -ceq 'get_session_state' -and $failure.Data['spherewrightBridgeCode'] -ceq 'REQUEST_TIMEOUT') 'read timeout retains actual operation/code instead of descriptor failure'
Assert-Action ($failure.Data['spherewrightBridgeMessage'] -ceq $timeoutError.message -and $failure.Data['spherewrightBridgeRetryable'] -eq $true -and $failure.Data['spherewrightBridgeRecovery'] -ceq $timeoutError.recovery) 'native message retryability and recovery retained without reinterpretation'
Assert-Action (($failure.Data | ConvertTo-Json -Depth 5) -notmatch 'private-error-token|private-error-auth|planToken|authToken') 'raw error details and credentials are not copied'
$failure=$null
try { Get-SpherewrightBridgeResult -Response ([pscustomobject]@{success=$false;error=[pscustomobject]@{code='INVENTORY_INSUFFICIENT';message='Insufficient inventory.'}}) -Operation prepare_build | Out-Null } catch { $failure=$_.Exception }
Assert-Action ($failure.Data['spherewrightBridgeCode'] -ceq 'INVENTORY_INSUFFICIENT' -and -not $failure.Data.Contains('spherewrightBridgeRetryable') -and -not $failure.Data.Contains('spherewrightBridgeRecovery')) 'absent optional failure fields stay unknown'
$failure=$null
try { Get-SpherewrightBridgeResult -Response @{success=$false;error=@{code='INVALID_REQUEST';message='Wrong request.';retryable=$false;recovery='Fresh inspect.'}} -Operation prepare_build | Out-Null } catch { $failure=$_.Exception }
Assert-Action ($failure.Data['spherewrightBridgeRetryable'] -eq $false -and $failure.Data['spherewrightBridgeRecovery'] -ceq 'Fresh inspect.') 'dictionary error and false retryability preserved'
$menu=[pscustomobject]@{gameLoaded=$false;sessionId=$null;gameTick=$null;bridgeConnected=$true}
$sameMenu=Get-SpherewrightBridgeResult -Response ([pscustomobject]@{success=$true;result=$menu}) -Operation get_session_state
Assert-Action ([object]::ReferenceEquals($menu,$sameMenu) -and $null -eq $sameMenu.PSObject.Properties['menuReady']) 'successful menu DTO unchanged and native readiness not invented'
Reset-ActionStub @()
$script:actionStubBridgeError=$timeoutError
$failure=$null
try { Invoke-SpherewrightNormalAction -PrepareMethod prepare_build -CommitMethod commit_build -PreparePayload @{} -SessionId offline-session -PlanetId 104 | Out-Null } catch { $failure=$_.Exception }
Assert-Action ($failure.Data['spherewrightCommitAccepted'] -eq $true -and $failure.Data['spherewrightActionId'] -ceq 'offline-action' -and $failure.Data['spherewrightBridgeCode'] -ceq 'REQUEST_TIMEOUT') 'accepted write remains accepted after structured observation timeout'
Assert-Action (($script:actionStubCalls.method -join ',') -ceq 'prepare_build,commit_build,get_action_result') 'timeout cannot trigger automatic reread or resubmission'
[pscustomobject]@{passed=$script:checks;gameCalls=0} | ConvertTo-Json -Compress
