# Dot-source AFTER SpherewrightActionClient and the existing protected transport.
# Importing this file makes zero requests. These helpers grant no write authority.
Set-StrictMode -Version Latest

function Get-SpherewrightCommitChecks {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$CommitSha)
    # Reuse gh, fetch once, exact full SHA. An absent run is unknown, not green.
    $json = & gh run list --repo Arcueid77/Spherewright --commit $CommitSha --limit 5 --json databaseId,headSha,status,conclusion,url
    if ($LASTEXITCODE -ne 0) { throw 'CI status collection failed; do not infer success.' }
    $decoded = ($json -join "`n") | ConvertFrom-Json
    $runs = @()
    # Windows PowerShell can emit JSON [] as one empty-array pipeline object.
    # Normalize enumeration before StrictMode property access; absent is unknown.
    foreach ($run in $decoded) { if ($null -ne $run) { $runs += ,$run } }
    if (@($runs | Where-Object { $_.headSha -cne $CommitSha }).Count) { throw 'CI returned a different source commit.' }
    [pscustomobject]@{commit=$CommitSha;observed=($runs.Count -gt 0);runs=$runs;signOff=$false}
}

function Get-SpherewrightCommitImpact {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$CommitSha,
        [Parameter(Mandatory)][string]$RepositoryDirectory
    )
    # Classification only, never CI success or runtime sign-off. No broad *.md
    # exemption: rules, playbooks, roadmap, installation and unknown paths gate
    # changed execution. Deletions/renames/merges also conservatively gate it.
    $rows = @(& git -C $RepositoryDirectory diff-tree --root --no-commit-id --name-status --no-renames -r $CommitSha)
    if ($LASTEXITCODE -ne 0) { throw 'Commit impact could not be collected; keep the execution gate.' }
    $paths = [Collections.Generic.List[string]]::new()
    $deliveryOnly = $rows.Count -gt 0
    foreach ($row in $rows) {
        $parts = $row -split "`t", 2
        if ($parts.Count -ne 2) { throw 'Commit path classification is ambiguous; keep the execution gate.' }
        $paths.Add($parts[1])
        $isFactPath = $parts[1] -cmatch '^docs/(evidence/[0-9]{4}-[0-9]{2}-[0-9]{2}/[A-Za-z0-9_-]+\.md|incidents/[A-Za-z0-9_-]+\.md|current-status\.md|gameplay-timeline\.md)$'
        if ($parts[0] -cnotin @('A','M') -or -not $isFactPath) { $deliveryOnly = $false }
    }
    [pscustomobject]@{commit=$CommitSha;changedPaths=$paths.ToArray();deliveryOnly=$deliveryOnly;executionRelevant=(-not $deliveryOnly);signOff=$false}
}

function Get-SpherewrightStageField($Value, [string]$Name) {
    if ($null -eq $Value -or $null -eq $Value.PSObject.Properties[$Name]) {
        throw "Required response field missing: $Name"
    }
    return $Value.$Name
}

function Assert-SpherewrightStageBudget([int]$AcceptedBefore, [int]$AuditWindowLimit, [int]$RequiredSlots) {
    # This checks capacity, not authority. Pin the approved limit at window open;
    # never enlarge an existing frozen window or reset its accepted count.
    if ($AcceptedBefore -gt $AuditWindowLimit -or $RequiredSlots -gt $AuditWindowLimit - $AcceptedBefore) {
        throw 'The approved audit window has insufficient slots; no reads or writes were issued.'
    }
}

function Get-SpherewrightStorageItemCount {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Snapshot,
        [Parameter(Mandatory)][ValidateRange(1, 2147483647)][int]$ItemId
    )
    if ((Get-SpherewrightStageField $Snapshot 'objectKind') -cne 'entity' -or
        (Get-SpherewrightStageField $Snapshot 'componentKind') -cne 'storage') {
        throw 'An observed built storage entity is required; research points are not items.'
    }
    $null = Get-SpherewrightStageField $Snapshot 'buffers'
    if ($null -eq $Snapshot.buffers) { throw 'Storage buffers were not observed.' }
    [long]$total = 0
    # Storage emits one row per occupied grid, not one row per item type.
    foreach ($buffer in @($Snapshot.buffers)) {
        $observedItemId = Get-SpherewrightStageField $buffer 'itemId'
        if (($observedItemId -isnot [int] -and $observedItemId -isnot [long]) -or
            $observedItemId -le 0 -or $observedItemId -gt [int]::MaxValue) { throw 'Storage item identity is not a positive supported integer.' }
        if ($observedItemId -ne $ItemId) { continue }
        if ((Get-SpherewrightStageField $buffer 'role') -cne 'storage' -or
            (Get-SpherewrightStageField $buffer 'countUnit') -cne 'items' -or
            (Get-SpherewrightStageField $buffer 'unitsPerItem') -ne 1) {
            throw 'Storage item count has an incompatible role or unit.'
        }
        $count = Get-SpherewrightStageField $buffer 'count'
        if (($count -isnot [int] -and $count -isnot [long]) -or $count -lt 0 -or
            $total -gt [long]::MaxValue - $count) { throw 'Storage item count is not a non-negative bounded integer.' }
        $total += $count
    }
    return $total
}

function Read-SpherewrightStageResult([string]$Method, [string]$SessionId, [hashtable]$Payload) {
    Get-SpherewrightBridgeResult -Response (Invoke-SpherewrightBridgeRequest -Method $Method -SessionId $SessionId -Payload $Payload) -Operation $Method
}

function Assert-SpherewrightStageSession($State, [string]$SessionId, [int]$PlanetId, [string]$GameVersion, [switch]$RequireWrites) {
    foreach ($field in @('sessionId','localPlanetId','gameVersion','gameLoaded','ownedBySpherewright','accessRestricted','writeHealth','gameTick','revision','writeBlockers')) {
        $null = Get-SpherewrightStageField $State $field
    }
    if ($State.sessionId -cne $SessionId -or $State.localPlanetId -ne $PlanetId -or $State.gameVersion -cne $GameVersion -or
        $State.gameLoaded -ne $true -or $State.ownedBySpherewright -ne $true -or $State.accessRestricted -ne $false -or
        $State.writeHealth -cne 'healthy' -or @($State.writeBlockers).Count -or $null -eq $State.gameTick -or $State.gameTick -lt 0) {
        throw 'Session identity/version/health boundary changed; stop, do not load or replay.'
    }
    if ($RequireWrites -and ((Get-SpherewrightStageField $State 'writesAllowed') -ne $true -or
        (Get-SpherewrightStageField $State 'peacefulMode') -cne 'confirmed_peaceful')) {
        throw 'Ordinary write boundary is not satisfied.'
    }
}

function Get-SpherewrightDurableJournalBoundary($Journal, [string]$SessionId) {
    foreach ($field in @('sessionId','entries','durableThroughSequence','persistencePending','persistenceError')) {
        $null = Get-SpherewrightStageField $Journal $field
    }
    if ($Journal.sessionId -cne $SessionId -or $null -eq $Journal.entries -or $Journal.persistencePending -ne $false -or $Journal.persistenceError) {
        throw 'Journal identity or durable persistence boundary is unproved.'
    }
    [long]$highest = 0
    foreach ($entry in @($Journal.entries)) {
        $sequence = Get-SpherewrightStageField $entry 'sequence'
        if ($sequence -ne $highest + 1) { throw 'Journal sequence coverage is not contiguous.' }
        $highest = [long]$sequence
    }
    if ($Journal.durableThroughSequence -ne $highest) { throw 'Journal contains entries not proved durable.' }
    return $highest
}

function Invoke-SpherewrightBeltSiteQualification {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$ApprovedPlan,
        [Parameter(Mandatory)][ValidateRange(0, [long]::MaxValue)][long]$ExpectedRevision,
        [Parameter(Mandatory)][ValidateRange(0, 50)][int]$AcceptedBefore,
        [ValidateSet(10, 20, 50)][int]$AuditWindowLimit = 20,
        [Parameter(Mandatory)][ValidateRange(0, [long]::MaxValue)][long]$MinimumDurableSequence,
        [Parameter(Mandatory)][scriptblock]$RecordEvidence
    )

    Assert-SpherewrightStageBudget $AcceptedBefore $AuditWindowLimit 0
    # A fixed, root-approved 3/4/5-span READ-ONLY experiment, not a route planner
    # or construction executor. It cannot dispatch a commit, save or lifecycle call.
    $spans = @(Get-SpherewrightStageField $ApprovedPlan 'spans')
    $maximumRequests = Get-SpherewrightStageField $ApprovedPlan 'maximumRequests'
    $maximumSeconds = Get-SpherewrightStageField $ApprovedPlan 'maximumWallSeconds'
    if ((Get-SpherewrightStageField $ApprovedPlan 'gameCommitAllowed') -ne $false -or
        (Get-SpherewrightStageField $ApprovedPlan 'candidateCountPerInterface') -ne 1 -or
        $spans.Count -notin @(3,4,5) -or $maximumRequests -lt 6 + 2 * $spans.Count -or $maximumRequests -gt 16 -or
        $maximumSeconds -lt 1 -or $maximumSeconds -gt 180) { throw 'A bounded, single-candidate, read-only approved plan is required.' }
    $sessionId = Get-SpherewrightStageField $ApprovedPlan 'expectedSessionId'
    $gameVersion = Get-SpherewrightStageField $ApprovedPlan 'expectedGameVersion'
    $savedTick = Get-SpherewrightStageField $ApprovedPlan 'expectedSaveGameTick'
    $common = Get-SpherewrightStageField $ApprovedPlan 'commonPayload'
    $planetId = Get-SpherewrightStageField $common 'planetId'
    $routingMode = Get-SpherewrightStageField $common 'beltPathMode'
    $elevated = $routingMode -ceq 'native_elevated_grid'
    if ([string]::IsNullOrWhiteSpace($sessionId) -or [string]::IsNullOrWhiteSpace($gameVersion) -or $planetId -le 0 -or
        $common.buildingItemId -ne 2001 -or $routingMode -cnotin @('native_grid','native_elevated_grid') -or $common.stateHashVersion -ne 1 -or
        $ApprovedPlan.sorterItemId -ne 2011 -or $ApprovedPlan.filterItemId -le 0) { throw 'The fixed native2001/ordinary2011 subset is required.' }
    if (@($common.PSObject.Properties.Name | Where-Object {$_ -cnotin @('planetId','buildingItemId','expectedPlayerStateHash','stateHashVersion','beltPathMode')}).Count) { throw 'Unsupported common request fields must not be silently discarded.' }
    $distance = {
        param($a,$b)
        $sum = 0.0
        foreach ($axis in @('x','y','z')) {
            $u = Get-SpherewrightStageField $a $axis; $v = Get-SpherewrightStageField $b $axis
            foreach ($value in @($u,$v)) {
                if (($value -isnot [int] -and $value -isnot [long] -and $value -isnot [single] -and $value -isnot [double] -and $value -isnot [decimal]) -or [double]::IsNaN([double]$value) -or
                    [double]::IsInfinity([double]$value) -or [math]::Abs([double]$value) -gt 10000) { throw 'Invalid fixed/native vector.' }
            }
            $sum += [math]::Pow(([double]$u - [double]$v),2)
        }
        [math]::Sqrt($sum)
    }
    $labels = @(); $roles = @()
    foreach ($span in $spans) {
        $label = Get-SpherewrightStageField $span 'label'; $role = Get-SpherewrightStageField $span 'endpointPreviewRole'
        if ($label -notmatch '^[A-Za-z][A-Za-z0-9_-]{0,60}$' -or $label -in $labels -or
            ($null -ne $role -and $role -cnotin @('source','destination'))) { throw 'Invalid fixed span identity or role.' }
        $startLayer = $span.PSObject.Properties['beltStartAltitudeLevel']
        $endLayer = $span.PSObject.Properties['beltEndAltitudeLevel']
        if ($elevated) {
            if ($null -eq $startLayer -or $null -eq $endLayer -or
                $startLayer.Value -notin @(0,1,2,3) -or $endLayer.Value -notin @(0,1,2,3) -or
                ($startLayer.Value -eq 0 -and $endLayer.Value -eq 0)) { throw 'Explicit supported elevated layers are required.' }
        } elseif (($null -ne $startLayer -and $null -ne $startLayer.Value) -or ($null -ne $endLayer -and $null -ne $endLayer.Value)) {
            # Native ground routing requires absent/null layers, NOT explicit zero.
            throw 'Ground native_grid must not carry altitude levels.'
        }
        $chord = & $distance $span.preferredPosition $span.pathEnd
        if ($chord -lt 1.5 -or $chord -gt 30) { throw 'Fixed endpoint chord is outside the current1.5–30m subset.' }
        $labels += $label; if ($null -ne $role) { $roles += $role }
    }
    if (@($roles | Where-Object {$_ -ceq 'source'}).Count -ne 1 -or
        @($roles | Where-Object {$_ -ceq 'destination'}).Count -ne 1) { throw 'Exactly one fixed source and destination preview are required.' }

    $watch = [Diagnostics.Stopwatch]::StartNew()
    $meter = [pscustomobject]@{requests=0}
    $read = {
        param([string]$method,[hashtable]$payload)
        if ($meter.requests -ge $maximumRequests -or $watch.Elapsed.TotalSeconds -ge $maximumSeconds) { throw 'Read-only request/time budget exhausted; no retry.' }
        $null = & $RecordEvidence ([pscustomobject]@{phase='request';requestNumber=$meter.requests+1;method=$method;payload=$payload})
        $meter.requests++
        Read-SpherewrightStageResult $method $sessionId $payload
    }
    $completed = [Collections.Generic.List[object]]::new()
    $paths = @{}; $entities = @{}; $failure = $null; $closureFailure = $null; $closure = $null
    $baselinePlayer = $null; $ownedBoundary = $false; $phase = 'session'; $firstPrepareMs = $null
    try {
        $initial = & $read 'get_session_state' @{}
        Assert-SpherewrightStageSession $initial $sessionId $planetId $gameVersion
        if ($initial.lastOwnedSaveGameTick -ne $savedTick -or $initial.revision -ne $ExpectedRevision) { throw 'Approved revision/normal-save boundary changed.' }
        $ownedBoundary = $true
        foreach ($role in @('source','destination')) {
            $phase = 'endpoint_' + $role
            $binding = Get-SpherewrightStageField $ApprovedPlan ($role + 'Endpoint')
            if ($binding.existingObjectId -le 0 -or $binding.existingSlot -lt -1 -or $binding.existingSlot -gt 15 -or
                $binding.existingBeltQuarterTurns -notin @(0,1,2,3) -or $binding.plannedBeltQuarterTurns -notin @(0,1,2,3) -or
                ($binding.existingSlot -ne -1 -and $binding.existingBeltQuarterTurns -ne 0)) { throw 'An explicit built endpoint slot/direction is required.' }
            $entity = & $read 'inspect_factory_entity' @{planetId=$planetId;objectId=$binding.existingObjectId}
            $virtualBelt = $binding.existingSlot -eq -1
            if ($virtualBelt) {
                # A belt's four directions all have slot=-1 and unknown occupancy.
                # Never turn those nulls into a free-slot claim: the subsequent
                # mandatory native preview checks the real belt connections.
                $observation = $entity.sorterEndpoints
                $points = @($observation.endpoints)
                if ($entity.itemId -ne 2001 -or $observation.state -cne 'observed' -or $observation.kind -cne 'belt_virtual' -or
                    $points.Count -ne 4 -or @($points | Select-Object -ExpandProperty index -Unique).Count -ne 4 -or
                    @($points | Where-Object {$_.slot -ne -1 -or $_.index -notin @(0,1,2,3) -or
                        $null -ne $_.occupied -or $null -ne $_.otherObjectId -or $null -ne $_.otherSlot}).Count) {
                    throw 'Exact observed2001 virtual belt directions are unproved; unknown occupancy is not free.'
                }
                $slot = @($points | Where-Object {$_.index -eq $binding.existingBeltQuarterTurns})
                if ((& $distance $entity.position $binding.expectedSlotPosition) -gt .02 -or
                    @($points | Where-Object {(& $distance $_.position $entity.position) -gt .02}).Count) {
                    throw 'Virtual belt pose/identity changed.'
                }
            } else {
                $slot = @($entity.sorterEndpoints.endpoints | Where-Object {$_.slot -eq $binding.existingSlot})
            }
            if ($entity.sessionId -cne $sessionId -or $entity.planetId -ne $planetId -or $entity.objectId -ne $binding.existingObjectId -or
                $slot.Count -ne 1 -or (-not $virtualBelt -and ($slot[0].occupied -ne $false -or $slot[0].otherObjectId -ne 0)) -or
                [string]::IsNullOrWhiteSpace($entity.endpointStateHash) -or
                (& $distance $slot[0].position $binding.expectedSlotPosition) -gt .02) { throw 'Exact current endpoint ID/slot/pose/hash is unproved.' }
            $entities[$role] = $entity
        }
        foreach ($span in $spans) {
            $phase = $span.label
            $player = & $read 'get_player_state' @{planetId=$planetId}
            if ($player.sessionId -cne $sessionId -or $player.planetId -ne $planetId -or [string]::IsNullOrWhiteSpace($player.stateHash) -or
                $player.movementState -cne 'Walk' -or $player.speed -gt .1 -or $player.coreEnergy -lt 20000000) { throw 'Fresh settled player boundary failed.' }
            if ($null -eq $baselinePlayer) { $baselinePlayer = $player }
            $payload = @{planetId=$planetId;buildingItemId=2001;stateHashVersion=1;expectedPlayerStateHash=$player.stateHash;
                beltPathMode=$routingMode;preferredPosition=$span.preferredPosition;pathEnd=$span.pathEnd}
            if ($elevated) {
                $payload.beltStartAltitudeLevel = $span.beltStartAltitudeLevel
                $payload.beltEndAltitudeLevel = $span.beltEndAltitudeLevel
            }
            $role = $span.endpointPreviewRole
            if ($null -ne $role) {
                $binding = $ApprovedPlan.($role + 'Endpoint')
                $payload.beltEndpointPreview = @{sorterItemId=2011;filterItemId=$ApprovedPlan.filterItemId}
                $payload.beltEndpointPreview[$role] = @{existingObjectId=$binding.existingObjectId;existingSlot=$binding.existingSlot;
                    existingBeltQuarterTurns=$binding.existingBeltQuarterTurns;plannedBeltQuarterTurns=$binding.plannedBeltQuarterTurns;
                    expectedEndpointStateHash=$entities[$role].endpointStateHash}
            }
            if ($null -eq $firstPrepareMs) { $firstPrepareMs = $watch.Elapsed.TotalMilliseconds }
            $prepared = & $read 'prepare_build' $payload
            $path = @($prepared.plannedPath); $echo = $prepared.plannedBeltPath
            if ($path.Count -lt 4 -or $path.Count -gt 64 -or $echo.newObjectCount -ne $path.Count -or
                $echo.nativeValidationMode -cne 'full_path_stage1' -or $echo.routingMode -cne $routingMode -or
                ($elevated -and ($echo.startAltitudeLevel -ne $span.beltStartAltitudeLevel -or $echo.endAltitudeLevel -ne $span.beltEndAltitudeLevel)) -or
                (-not $elevated -and ($null -ne $echo.startAltitudeLevel -or $null -ne $echo.endAltitudeLevel))) { throw 'Native full path/layer/count echo failed.' }
            $chord = & $distance $path[0] $path[-1]
            if ($chord -lt 1.5 -or $chord -gt 30) { throw 'Native endpoint chord failed; polyline length is not this bound.' }
            $beltBudget = @($prepared.itemBudget | Where-Object {$_.itemId -eq 2001})
            $expectedRows = if ($null -ne $role) {2} else {1}
            if ($beltBudget.Count -ne 1 -or $beltBudget[0].count -ne $path.Count -or @($prepared.itemBudget).Count -ne $expectedRows) { throw 'Native belt material budget mismatch.' }
            if ($null -ne $role) {
                $preview = $prepared.beltEndpointPreview; $attachments = @($preview.attachments)
                if ($preview.nativeCheckPerformed -ne $true -or $preview.nativeCheckPassed -ne $true -or @($preview.blockers).Count) {
                    $failure = [pscustomobject]@{kind='endpoint_rejection';stage=$phase;code=$null;blockers=@($preview.blockers);nativeCheckPerformed=$preview.nativeCheckPerformed}
                    throw 'Exact endpoint preview rejected; no further candidate or prepare.'
                }
                $sorterBudget = @($prepared.itemBudget | Where-Object {$_.itemId -eq 2011})
                if ($prepared.prepared -ne $false -or $prepared.commitAllowedNow -ne $false -or $preview.executable -ne $false -or
                    $attachments.Count -ne 1 -or $attachments[0].role -cne $role -or $attachments[0].existingObjectId -ne $binding.existingObjectId -or
                    $attachments[0].endpointStateHash -cne $entities[$role].endpointStateHash -or $attachments[0].filterItemId -ne $ApprovedPlan.filterItemId -or
                    $attachments[0].sorterItemId -ne 2011 -or $attachments[0].plannedBeltQuarterTurns -ne $binding.plannedBeltQuarterTurns -or
                    ($binding.existingSlot -eq -1 -and ($attachments[0].existingItemId -ne 2001 -or
                        $attachments[0].existingBeltQuarterTurns -ne $binding.existingBeltQuarterTurns)) -or
                    $attachments[0].nativeCondition -cne 'Ok' -or $attachments[0].nativeSpan -ne 2 -or $sorterBudget.Count -ne 1 -or $sorterBudget[0].count -ne 1) { throw 'Native tokenless endpoint/budget binding echo failed.' }
                $actualSlot = if ($role -ceq 'source') {$attachments[0].attachment.sourceSlot} else {$attachments[0].attachment.destinationSlot}
                if ($actualSlot -ne $binding.existingSlot) { throw 'Native exact attachment slot mismatch.' }
            } elseif ($prepared.prepared -ne $true -or $prepared.commitAllowedNow -ne $true -or @($prepared.commitBlockers).Count -or
                [string]::IsNullOrWhiteSpace($prepared.planToken)) { throw 'Ordinary crossing site prepare failed; never commit its token.' }
            $paths[$span.label] = $path
            $completed.Add([pscustomobject]@{label=$span.label;role=$role;newPoints=$path.Count;endpointChordMetres=$chord;nativeValidationMode=$echo.nativeValidationMode})
            $null = & $RecordEvidence ([pscustomobject]@{phase='qualified';span=$completed[$completed.Count-1]})
        }
        $phase = 'contiguous_endpoints'
        $sourceSpan = @($spans | Where-Object {$_.endpointPreviewRole -ceq 'source'})[0]
        $destinationSpan = @($spans | Where-Object {$_.endpointPreviewRole -ceq 'destination'})[0]
        $last = $paths[$sourceSpan.label][-1]
        foreach ($span in @($spans | Where-Object {$null -eq $_.endpointPreviewRole})) {
            if ((& $distance $last $paths[$span.label][0]) -gt .02) { throw 'Native crossing endpoints do not coincide; no path/ID substitution.' }
            $last = $paths[$span.label][-1]
        }
        if ((& $distance $last $paths[$destinationSpan.label][0]) -gt .02) { throw 'Native destination ramp endpoint does not coincide.' }
    } catch {
        if ($null -eq $failure) { $failure = [pscustomobject]@{kind='caller_or_bridge_rejection';stage=$phase;code=$_.Exception.Data['spherewrightBridgeCode'];message=$_.Exception.Message} }
    }
    # Only health-bound reads; no retry, token use, save, reload or action cleanup.
    if ($ownedBoundary) {
        try {
            $state = & $read 'get_session_state' @{}
            Assert-SpherewrightStageSession $state $sessionId $planetId $gameVersion
            if ($state.revision -ne $initial.revision -or $state.lastOwnedSaveGameTick -ne $savedTick) { throw 'Read-only revision/save closure changed.' }
            $player = & $read 'get_player_state' @{planetId=$planetId}
            if ($player.sessionId -cne $sessionId -or $player.planetId -ne $planetId -or [string]::IsNullOrWhiteSpace($player.stateHash)) { throw 'Closing player identity/hash is unproved.' }
            if ($null -ne $baselinePlayer -and ($player.stateHash -cne $baselinePlayer.stateHash -or
                ($player.inventory | ConvertTo-Json -Depth 30 -Compress) -cne ($baselinePlayer.inventory | ConvertTo-Json -Depth 30 -Compress))) { throw 'Player/inventory closure changed.' }
            $journal = & $read 'get_gameplay_journal' @{}
            $durable = Get-SpherewrightDurableJournalBoundary $journal $sessionId
            if ($durable -lt $MinimumDurableSequence) { throw 'Durable Journal floor regressed.' }
            $closure = [pscustomobject]@{observedTick=$state.gameTick;savedTick=$state.lastOwnedSaveGameTick;revision=$state.revision;durableThroughSequence=$durable}
        } catch { $closureFailure = [pscustomobject]@{code=$_.Exception.Data['spherewrightBridgeCode'];message=$_.Exception.Message} }
    }
    $summary = [pscustomobject]@{result=$(if ($null -eq $failure -and $null -ne $closure) {'qualified_sites_only'} else {'stopped'});
        spans=$completed.ToArray();failure=$failure;closure=$closure;closureFailure=$closureFailure;bridgeRequests=$meter.requests;
        acceptedDelta=0;acceptedAfter=$AcceptedBefore;auditWindowLimit=$AuditWindowLimit;doNotReplay=$true;futureActualIdJoinProven=$false;wholePlanExecutable=$false;
        timingMs=[pscustomobject]@{entryToFirstPrepare=$firstPrepareMs;total=$watch.Elapsed.TotalMilliseconds};providerUsage=$null}
    $null = & $RecordEvidence ([pscustomobject]@{phase='summary';summary=$summary})
    return $summary
}

function Invoke-SpherewrightResearchAndSave {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$SessionId,
        [Parameter(Mandatory)][ValidateRange(1, [int]::MaxValue)][int]$PlanetId,
        [Parameter(Mandatory)][string]$GameVersion,
        [Parameter(Mandatory)][ValidateRange(1, [int]::MaxValue)][int]$TechId,
        [Parameter(Mandatory)][ValidateRange(0, 50)][int]$AcceptedBefore,
        [ValidateSet(10, 20, 50)][int]$AuditWindowLimit = 20,
        [Parameter(Mandatory)][scriptblock]$ValidateResearchPlan,
        [Parameter(Mandatory)][scriptblock]$RecordEvidence,
        [bool]$PrioritizeQueued = $false,
        [ValidateRange(1, 1800)][int]$TimeoutSeconds = 180,
        [Nullable[datetimeoffset]]$DispatchedAtUtc
    )

    Assert-SpherewrightStageBudget $AcceptedBefore $AuditWindowLimit 2
    # Caller must already hold the verified single-writer handoff and two write
    # slots. Do not import a historical executor or infer permissions from this count.
    # Research itemBudget is native future research-consumption, not an empty
    # inventory transaction. Validate it against the approved fresh tech requirements.
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $phase = 'fresh_reads'
    $actions = [Collections.Generic.List[object]]::new()
    $acceptedDelta = 0
    try {
        $state = Read-SpherewrightStageResult get_session_state $SessionId @{}
        Assert-SpherewrightStageSession $state $SessionId $PlanetId $GameVersion -RequireWrites
        $player = Read-SpherewrightStageResult get_player_state $SessionId @{planetId=$PlanetId}
        if ((Get-SpherewrightStageField $player 'sessionId') -cne $SessionId -or
            (Get-SpherewrightStageField $player 'planetId') -ne $PlanetId) { throw 'Fresh player identity changed.' }
        $progress = Read-SpherewrightStageResult get_progression_state $SessionId @{planetId=$PlanetId}
        if ((Get-SpherewrightStageField $progress 'sessionId') -cne $SessionId -or
            (Get-SpherewrightStageField $progress 'planetId') -ne $PlanetId) { throw 'Fresh progression identity changed.' }
        $hash = Get-SpherewrightStageField $progress 'selectionStateHash'
        if ([string]::IsNullOrWhiteSpace($hash)) { throw 'selectionStateHash is unavailable.' }
        $hashVersion = Get-SpherewrightStageField $progress 'selectionStateHashVersion'
        $initialJournal = Read-SpherewrightStageResult get_gameplay_journal $SessionId @{}
        $initialDurable = Get-SpherewrightDurableJournalBoundary $initialJournal $SessionId
        $firstPrepareMs = $watch.Elapsed.TotalMilliseconds
        $firstPrepareUtc = [datetimeoffset]::UtcNow
        $phase = 'research'
        $research = Invoke-SpherewrightNormalAction -PrepareMethod prepare_select_research -CommitMethod commit_select_research -SessionId $SessionId -PlanetId $PlanetId -TimeoutSeconds $TimeoutSeconds -PreparePayload @{
            planetId=$PlanetId;techId=$TechId;expectedSelectionStateHash=$hash;stateHashVersion=$hashVersion;prioritizeQueued=$PrioritizeQueued
        } -ValidatePrepared $ValidateResearchPlan
        if (-not $research.committed.idempotentReplay) { $acceptedDelta++ }
        $actions.Add([pscustomobject]@{actionId=$research.committed.actionId;kind='select-research';timingMs=$research.timingMs;observedExecutionGameTicks=$research.observedExecutionGameTicks})
        $phase = 'research_readback'
        $progress = Read-SpherewrightStageResult get_progression_state $SessionId @{planetId=$PlanetId}
        if ($progress.sessionId -cne $SessionId -or $progress.planetId -ne $PlanetId -or
            (@($progress.techQueue) -notcontains $TechId -and @($progress.technologies | Where-Object { $_.techId -eq $TechId -and $_.unlocked -eq $true }).Count -ne 1)) {
            throw 'Selected technology is neither queued nor proved unlocked; retain the successful action.'
        }
        $phase = 'save'
        $state = Read-SpherewrightStageResult get_session_state $SessionId @{}
        Assert-SpherewrightStageSession $state $SessionId $PlanetId $GameVersion -RequireWrites
        $save = Invoke-SpherewrightNormalAction -PrepareMethod prepare_save -CommitMethod commit_save -SessionId $SessionId -PlanetId $PlanetId -TimeoutSeconds $TimeoutSeconds -PreparePayload @{
            planetId=$PlanetId;expectedRevision=$state.revision;stateHashVersion=1
        } -ValidatePrepared { param($plan) $plan.actionKind -ceq 'save' -and @($plan.itemBudget).Count -eq 0 }
        if (-not $save.committed.idempotentReplay) { $acceptedDelta++ }
        $actions.Add([pscustomobject]@{actionId=$save.committed.actionId;kind='save';timingMs=$save.timingMs;observedExecutionGameTicks=$save.observedExecutionGameTicks})
        $phase = 'save_readback'
        $state = Read-SpherewrightStageResult get_session_state $SessionId @{}
        Assert-SpherewrightStageSession $state $SessionId $PlanetId $GameVersion -RequireWrites
        if ((Get-SpherewrightStageField $state 'ownedSaveState') -cne 'saved' -or
            (Get-SpherewrightStageField $state 'lastOwnedSaveGameTick') -ne $save.result.completedAtGameTick -or
            (Get-SpherewrightStageField $state 'restartResumeAvailable') -ne $true) { throw 'Normal save/resume readback is unproved.' }
        $journal = Read-SpherewrightStageResult get_gameplay_journal $SessionId @{}
        $durable = Get-SpherewrightDurableJournalBoundary $journal $SessionId
        if ($durable -lt $initialDurable) { throw 'Journal durable sequence regressed; no replay.' }
        $summary = [pscustomobject]@{
            result='completed';acceptedDelta=$acceptedDelta;acceptedAfter=$AcceptedBefore+$acceptedDelta
            auditWindowLimit=$AuditWindowLimit;frozen=($AcceptedBefore+$acceptedDelta -ge $AuditWindowLimit);inFlightActionIds=@();actions=$actions.ToArray()
            observedTick=$state.gameTick;savedTick=$state.lastOwnedSaveGameTick;revision=$state.revision;durableThroughSequence=$durable
            timingMs=[pscustomobject]@{entryToFirstPrepare=[math]::Round($firstPrepareMs,3);dispatchToFirstPrepare=$(if ($null -eq $DispatchedAtUtc) { $null } else { [math]::Round(($firstPrepareUtc-$DispatchedAtUtc).TotalMilliseconds,3) });total=[math]::Round($watch.Elapsed.TotalMilliseconds,3)}
            unproved=@('save_restart','production_throughput');nextBlocker=$null
        }
        $null = & $RecordEvidence $summary
        return $summary
    } catch {
        # Count a known accepted commit even if terminal/readback failed. An
        # unknown commit remains unknown; a local summary cannot clear the lease.
        $currentAccepted = $_.Exception.Data['spherewrightCommitAccepted']
        if ($phase -in @('research','save') -and $currentAccepted -eq $true -and $_.Exception.Data['spherewrightIdempotentReplay'] -ne $true) { $acceptedDelta++ }
        $_.Exception.Data['spherewrightStagePhase'] = $phase
        $_.Exception.Data['spherewrightStageAcceptedDelta'] = $acceptedDelta
        $_.Exception.Data['spherewrightPriorActionIds'] = @($actions | ForEach-Object actionId)
        $_.Exception.Data['spherewrightDoNotReplayStage'] = $true
        throw
    }
}

function Invoke-SpherewrightMaterialHandcraftAndSave {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$SessionId,
        [Parameter(Mandatory)][ValidateRange(1, 2147483647)][int]$PlanetId,
        [Parameter(Mandatory)][string]$GameVersion,
        [Parameter(Mandatory)][ValidateRange(1, 2147483647)][int]$StorageEntityId,
        [Parameter(Mandatory)][ValidateRange(1, 6000)][int]$MaterialItemId,
        [Parameter(Mandatory)][ValidateRange(1, 1000)][int]$MaterialCount,
        [Parameter(Mandatory)][ValidateRange(1, 2147483647)][int]$RecipeId,
        [Parameter(Mandatory)][ValidateRange(1, 100)][int]$CraftCount,
        [Parameter(Mandatory)][ValidateRange(0, 50)][int]$AcceptedBefore,
        [ValidateSet(10, 20, 50)][int]$AuditWindowLimit = 20,
        [Parameter(Mandatory)][scriptblock]$ValidateCraftPlan,
        [Parameter(Mandatory)][scriptblock]$ValidateCraftReadback,
        [Parameter(Mandatory)][scriptblock]$RecordEvidence,
        [ValidateRange(1, 1800)][int]$TimeoutSeconds = 180
    )
    Assert-SpherewrightStageBudget $AcceptedBefore $AuditWindowLimit 3
    # One explicitly approved ordinary-material transfer, one recipe, one save.
    # Caller supplies the single-writer lease and THREE external audit slots.
    # Matrix/cache transfers are excluded; this does not select goals or recipes.
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $phase = 'fresh_reads'; $acceptedDelta = 0
    $actions = [Collections.Generic.List[object]]::new()
    try {
        $state = Read-SpherewrightStageResult get_session_state $SessionId @{}
        Assert-SpherewrightStageSession $state $SessionId $PlanetId $GameVersion -RequireWrites
        $journal = Read-SpherewrightStageResult get_gameplay_journal $SessionId @{}
        $initialDurable = Get-SpherewrightDurableJournalBoundary $journal $SessionId
        $player = Read-SpherewrightStageResult get_player_state $SessionId @{planetId=$PlanetId}
        if ($player.sessionId -cne $SessionId -or $player.planetId -ne $PlanetId -or
            (Get-SpherewrightStageField $player 'movementState') -cne 'Walk' -or
            (Get-SpherewrightStageField $player 'speed') -gt .1 -or
            (Get-SpherewrightStageField $player 'coreEnergy') -le 0 -or
            @((Get-SpherewrightStageField $player 'handcraftQueue')).Count) { throw 'Settled powered player and empty forge queue required.' }
        $source = Read-SpherewrightStageResult inspect_factory_entity $SessionId @{planetId=$PlanetId;objectId=$StorageEntityId}
        if ($source.sessionId -cne $SessionId -or $source.planetId -ne $PlanetId -or $source.objectId -ne $StorageEntityId -or
            (Get-SpherewrightStorageItemCount $source $MaterialItemId) -lt $MaterialCount) { throw 'Approved ordinary material source is unavailable.' }
        $beforeCount = Get-SpherewrightInventoryCount $player $MaterialItemId
        $firstPrepareMs = $watch.Elapsed.TotalMilliseconds
        $phase = 'transfer'
        $transfer = Invoke-SpherewrightNormalAction -PrepareMethod prepare_transfer -CommitMethod commit_transfer -SessionId $SessionId -PlanetId $PlanetId -TimeoutSeconds $TimeoutSeconds -PreparePayload @{
            planetId=$PlanetId;direction='storage-to-player';storageEntityId=$StorageEntityId;itemId=$MaterialItemId;count=$MaterialCount
            expectedPlayerStateHash=$player.stateHash;expectedStorageStateHash=$source.stateHash;stateHashVersion=1
        } -ValidatePrepared {
            param($plan)
            $budget = @($plan.itemBudget)
            [bool]($plan.actionKind -ceq 'transfer' -and $plan.sourceObjectId -eq $StorageEntityId -and
                $null -eq $plan.destinationObjectId -and $budget.Count -eq 1 -and
                $budget[0].itemId -eq $MaterialItemId -and $budget[0].count -eq $MaterialCount -and
                $budget[0].direction -ceq 'storage-to-player')
        }
        if ($transfer.committed.idempotentReplay) { throw 'Unexpected replay; reconcile original stage.' }
        $acceptedDelta++
        $actions.Add([pscustomobject]@{actionId=$transfer.committed.actionId;kind='transfer';timingMs=$transfer.timingMs})
        $phase = 'transfer_readback'
        $player = Read-SpherewrightStageResult get_player_state $SessionId @{planetId=$PlanetId}
        $afterSource = Read-SpherewrightStageResult inspect_factory_entity $SessionId @{planetId=$PlanetId;objectId=$StorageEntityId}
        if ($player.sessionId -cne $SessionId -or $player.planetId -ne $PlanetId -or
            (Get-SpherewrightInventoryCount $player $MaterialItemId) -ne $beforeCount+$MaterialCount -or
            $transfer.result.beforeTargetAmount -lt $MaterialCount -or
            $transfer.result.afterTargetAmount -ne $transfer.result.beforeTargetAmount-$MaterialCount) { throw 'Accepted ordinary material transfer is not conserved; no replay.' }
        foreach ($field in @('sessionId','planetId','objectId','objectKind','itemId','componentKind','position','rotation','recipeId','connections','storageConfiguration')) {
            if ((ConvertTo-Json -InputObject $source.$field -Depth 12 -Compress) -cne
                (ConvertTo-Json -InputObject $afterSource.$field -Depth 12 -Compress)) { throw "Accepted transfer source changed $field; no replay." }
        }
        $state = Read-SpherewrightStageResult get_session_state $SessionId @{}
        Assert-SpherewrightStageSession $state $SessionId $PlanetId $GameVersion -RequireWrites
        $phase = 'handcraft'
        $craft = Invoke-SpherewrightNormalAction -PrepareMethod prepare_handcraft -CommitMethod commit_handcraft -SessionId $SessionId -PlanetId $PlanetId -TimeoutSeconds $TimeoutSeconds -PreparePayload @{
            planetId=$PlanetId;recipeId=$RecipeId;count=$CraftCount;expectedPlayerStateHash=$player.stateHash;stateHashVersion=1
        } -ValidatePrepared $ValidateCraftPlan
        if ($craft.committed.idempotentReplay) { throw 'Unexpected replay; reconcile original stage.' }
        $acceptedDelta++
        $actions.Add([pscustomobject]@{actionId=$craft.committed.actionId;kind='handcraft';timingMs=$craft.timingMs})
        $phase = 'handcraft_readback'
        $craftedPlayer = Read-SpherewrightStageResult get_player_state $SessionId @{planetId=$PlanetId}
        if ($craftedPlayer.sessionId -cne $SessionId -or $craftedPlayer.planetId -ne $PlanetId -or @($craftedPlayer.handcraftQueue).Count) { throw 'Accepted handcraft identity/terminal queue readback failed; no replay.' }
        $readback = @(& $ValidateCraftReadback $player $craftedPlayer $craft.result)
        if ($readback.Count -ne 1 -or $readback[0] -isnot [bool] -or -not $readback[0]) { throw 'Accepted handcraft exact readback not approved; no replay.' }
        $state = Read-SpherewrightStageResult get_session_state $SessionId @{}
        Assert-SpherewrightStageSession $state $SessionId $PlanetId $GameVersion -RequireWrites
        $phase = 'save'
        $save = Invoke-SpherewrightNormalAction -PrepareMethod prepare_save -CommitMethod commit_save -SessionId $SessionId -PlanetId $PlanetId -TimeoutSeconds $TimeoutSeconds -PreparePayload @{
            planetId=$PlanetId;expectedRevision=$state.revision;stateHashVersion=1
        } -ValidatePrepared {param($plan) $plan.actionKind -ceq 'save' -and @($plan.itemBudget).Count -eq 0}
        if ($save.committed.idempotentReplay) { throw 'Unexpected replay; reconcile original stage.' }
        $acceptedDelta++
        $actions.Add([pscustomobject]@{actionId=$save.committed.actionId;kind='save';timingMs=$save.timingMs})
        $phase = 'save_readback'
        $state = Read-SpherewrightStageResult get_session_state $SessionId @{}
        Assert-SpherewrightStageSession $state $SessionId $PlanetId $GameVersion -RequireWrites
        if ($state.ownedSaveState -cne 'saved' -or $state.lastOwnedSaveGameTick -ne $save.result.completedAtGameTick -or $state.restartResumeAvailable -ne $true) { throw 'Accepted save readback unproved; no replay.' }
        $journal = Read-SpherewrightStageResult get_gameplay_journal $SessionId @{}
        $durable = Get-SpherewrightDurableJournalBoundary $journal $SessionId
        if ($durable -lt $initialDurable) { throw 'Durable Journal regressed; no replay.' }
        $summary = [pscustomobject]@{result='completed';acceptedDelta=$acceptedDelta;acceptedAfter=$AcceptedBefore+$acceptedDelta;auditWindowLimit=$AuditWindowLimit;frozen=($AcceptedBefore+$acceptedDelta -ge $AuditWindowLimit);inFlightActionIds=@();actions=$actions.ToArray();observedTick=$state.gameTick;savedTick=$state.lastOwnedSaveGameTick;revision=$state.revision;durableThroughSequence=$durable;timingMs=[pscustomobject]@{entryToFirstPrepare=$firstPrepareMs;total=$watch.Elapsed.TotalMilliseconds};unproved=@('save_restart','production_throughput')}
        $null = & $RecordEvidence $summary
        return $summary
    } catch {
        if ($phase -in @('transfer','handcraft','save') -and $_.Exception.Data['spherewrightCommitAccepted'] -eq $true -and $_.Exception.Data['spherewrightIdempotentReplay'] -ne $true) { $acceptedDelta++ }
        $_.Exception.Data['spherewrightStagePhase']=$phase
        $_.Exception.Data['spherewrightStageAcceptedDelta']=$acceptedDelta
        $_.Exception.Data['spherewrightPriorActionIds']=@($actions|ForEach-Object actionId)
        $_.Exception.Data['spherewrightDoNotReplayStage']=$true
        throw
    }
}
