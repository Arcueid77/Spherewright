# Load StageTools and the existing protected transport first. Import is inert.
# Scheduling of existing read-only native windows, not a Governor/sign-off engine.
Set-StrictMode -Version Latest

function Get-SpherewrightProductionClockMilliseconds {
    # Monotonic receive-time clock; fixtures replace only this inert clock.
    [Diagnostics.Stopwatch]::GetTimestamp()*1000.0/[Diagnostics.Stopwatch]::Frequency
}

function Get-SpherewrightProductionSleepMilliseconds {
    param(
        [ValidateRange(0,[long]::MaxValue)][long]$RemainingGameTicks,
        [double]$ObservedTicksPerSecond,
        [ValidateRange(1,10000)][int]$MaximumSleepMilliseconds,
        [ValidateScript({-not [double]::IsNaN($_) -and -not [double]::IsInfinity($_) -and $_ -ge 0})][double]$RemainingWallMilliseconds
    )
    if ($RemainingGameTicks -eq 0) { return 0 }
    $wait=[double]$MaximumSleepMilliseconds
    if ($ObservedTicksPerSecond -gt 0 -and -not [double]::IsInfinity($ObservedTicksPerSecond)) {
        # Poll before the predicted target. This is scheduling, not tick credit:
        # slow/paused/variable simulation still needs fresh native responses.
        $wait=[math]::Min($wait,[math]::Max(1.0,$RemainingGameTicks*1000.0/$ObservedTicksPerSecond*0.75))
    }
    [int][math]::Floor([math]::Min($wait,$RemainingWallMilliseconds))
}

function Invoke-SpherewrightProductionExperiment {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$SessionId,
        [Parameter(Mandatory)][ValidateRange(1,[int]::MaxValue)][int]$PlanetId,
        [Parameter(Mandatory)][string]$GameVersion,
        # Include raw sources, upstream stores and consumers in one declared
        # experiment. These read-only bounds do not change construction limits,
        # native600-tick windows, request caps or the original wall deadline.
        [Parameter(Mandatory)][ValidateCount(1,48)][int[]]$EntityIds,
        # Match the native query's64-ID bound; every sample still requires the
        # complete requested set in the SAME response, without split credit.
        [Parameter(Mandatory)][ValidateCount(1,64)][int[]]$ItemIds,
        [Parameter(Mandatory)][scriptblock]$ValidateObservation,
        [Parameter(Mandatory)][scriptblock]$ReceiptMarker,
        [Parameter(Mandatory)][scriptblock]$RecordEvidence,
        [ValidateSet('independent','continuous')][string]$Mode='independent',
        [ValidateRange(1,120)][int]$RequiredWindows=3,
        [ValidateRange(1,120)][int]$MaximumSamples=3,
        [ValidateRange(1,3600)][int]$IntervalGameTicks=606,
        [ValidateSet(600)][int]$WindowGameTicks=600,
        [ValidateRange(36000,360000)][int]$RequiredContinuousGameTicks=36000,
        # Slow native simulation may need more than an hour for36000 ticks.
        # Only the initial finite declaration may use this larger bound; a
        # running experiment's original deadline is never extended or replayed.
        [ValidateRange(1,7200)][int]$TimeoutSeconds=180,
        [ValidateRange(1,4096)][int]$MaximumRequests=90,
        [ValidateRange(1,10)][int]$PollSeconds=5,
        # Explicit continuous-mode observation cadence, not cached entity data.
        # The default still fresh-reads every declared entity in every sample.
        [ValidateRange(1,4)][int]$EntitySampleEvery=1,
        # Optional explicit stock cuts piggyback on existing entity reads. They
        # retain their own native timestamps, never the production-window time.
        [ValidateNotNull()][hashtable]$MaterialInventorySelections=@{}
    )
    if (@($EntityIds|Where-Object {$_ -le 0}).Count -or @($ItemIds|Where-Object {$_ -le 0}).Count -or
        @($EntityIds|Sort-Object -Unique).Count -ne $EntityIds.Count -or @($ItemIds|Sort-Object -Unique).Count -ne $ItemIds.Count -or
        ($Mode -ceq 'independent' -and $MaximumSamples -lt $RequiredWindows) -or
        ($Mode -ceq 'independent' -and $EntitySampleEvery -ne 1) -or
        ($Mode -ceq 'continuous' -and $IntervalGameTicks -gt $WindowGameTicks) -or
        ($Mode -cne 'continuous' -and $TimeoutSeconds -gt 3600)) { throw 'Invalid fixed scope/cadence/budget; no request sent.' }
    $materialSelections=@{}
    foreach($key in $MaterialInventorySelections.Keys){
        $anchor=0
        if(-not [int]::TryParse([string]$key,[ref]$anchor) -or $anchor -notin $EntityIds -or $materialSelections.ContainsKey($anchor)){
            throw 'Material cut anchor must be a unique declared entity; no request sent.'
        }
        $selection=@($MaterialInventorySelections[$key])
        if($selection.Count -lt 1 -or $selection.Count -gt 256){throw 'Material cut must select 1..256 explicit objects; no request sent.'}
        $ids=[Collections.Generic.List[int]]::new()
        foreach($value in $selection){
            $id=0
            if(-not [int]::TryParse([string]$value,[ref]$id) -or $id -le 0 -or $ids.Contains($id)){
                throw 'Material cut object IDs must be positive unique integers; no request sent.'
            }
            $ids.Add($id)
        }
        $materialSelections[$anchor]=$ids.ToArray()
    }
    $started=Get-Date; $deadline=$started.AddSeconds($TimeoutSeconds)
    $experiment=[pscustomobject]@{requests=0;lastMethod=$null;readWallMs=0.0;pollWaitMs=0.0;samples=0;entitySamples=0;qualifying=0;resets=0;covered=0;startTick=$null;endTick=$null;lastWindowEnd=$null;lastSessionTick=$null;nextTick=0;firstTick=$null;lastTick=$null;lastRates=@();rateClockTick=$null;rateClockMs=$null;observedTicksPerSecond=0.0}
    $read = {
        param([string]$Method,[hashtable]$Payload)
        $experiment.lastMethod=$Method
        if ((Get-Date) -ge $deadline -or $experiment.requests -ge $MaximumRequests) {
            $failure=[InvalidOperationException]::new('Finite sampling deadline/request budget reached; do not extend or restart.')
            $failure.Data['spherewrightSamplingFailureKind']=$(if ((Get-Date) -ge $deadline) {'deadline_exhausted'} else {'request_budget_exhausted'})
            throw $failure
        }
        if ($Method -notin @('get_session_state','inspect_factory_entity','get_power_summary','get_overseer_production')) { throw 'Sampling is read-only.' }
        $experiment.requests++
        $clock=[Diagnostics.Stopwatch]::StartNew()
        try { $value=Read-SpherewrightStageResult $Method $SessionId $Payload }
        finally { $clock.Stop(); $experiment.readWallMs+=$clock.Elapsed.TotalMilliseconds }
        if ((Get-Date) -ge $deadline) {
            $failure=[InvalidOperationException]::new('Read crossed the original sampling deadline; evidence retained, no new request.')
            $failure.Data['spherewrightSamplingFailureKind']='read_crossed_deadline'
            throw $failure
        }
        if ($Mode -ceq 'continuous' -and $EntitySampleEvery -gt 1 -and $Method -ceq 'get_session_state') {
            $receivedMs=Get-SpherewrightProductionClockMilliseconds
            if ($null -ne $experiment.rateClockTick -and $receivedMs -gt $experiment.rateClockMs -and $value.gameTick -gt $experiment.rateClockTick) {
                $rate=([long]$value.gameTick-$experiment.rateClockTick)*1000.0/($receivedMs-$experiment.rateClockMs)
                # Fastest measured positive rate is conservative for sleeping;
                # Estimation reuses these reads; shorter waits may add bounded polls.
                if (-not [double]::IsInfinity($rate)) { $experiment.observedTicksPerSecond=[math]::Max($experiment.observedTicksPerSecond,$rate) }
            }
            $experiment.rateClockTick=[long]$value.gameTick
            $experiment.rateClockMs=$receivedMs
        }
        return $value
    }
    $readEntities={
        foreach($id in $EntityIds){
            $payload=@{planetId=$PlanetId;objectId=$id}
            if($materialSelections.ContainsKey($id)){$payload.materialInventoryObjectIds=$materialSelections[$id].Clone()}
            $detail=& $read inspect_factory_entity $payload
            if ($detail.sessionId -cne $SessionId -or $detail.planetId -ne $PlanetId -or $detail.objectId -ne $id) { throw 'Selected entity identity changed.' }
            if($materialSelections.ContainsKey($id)){
                $cut=Get-SpherewrightStageField $detail 'materialInventoryCut'
                if($null -eq $cut -or $cut.state -cne 'observed'){
                    $failure=[InvalidOperationException]::new('Declared material inventory cut is unavailable; retain the original receipt, do not retry.')
                    $failure.Data['spherewrightSamplingFailureKind']='material_inventory_cut_unavailable'
                    if($null -ne $cut -and $null -ne $cut.PSObject.Properties['reasonCode']){$failure.Data['spherewrightMaterialInventoryReasonCode']=$cut.reasonCode}
                    throw $failure
                }
                $expected=@($materialSelections[$id]|Sort-Object)
                if($cut.sessionId -cne $SessionId -or $cut.planetId -ne $PlanetId -or
                    $cut.capturedAtGameTick -ne $detail.capturedAtGameTick -or
                    $cut.coverage -cne 'explicit_objects_and_complete_native_cargo_paths' -or
                    (@($cut.requestedObjectIds|Sort-Object)-join ',') -cne ($expected-join ',') -or
                    (@($cut.objects.objectId|Sort-Object)-join ',') -cne ($expected-join ',') -or
                    @($cut.objects|Where-Object {$_.sessionId -cne $SessionId -or $_.planetId -ne $PlanetId -or $_.capturedAtGameTick -ne $cut.capturedAtGameTick}).Count -or
                    @($cut.cargoPaths|Where-Object {$_.capturedAtGameTick -ne $cut.capturedAtGameTick}).Count){
                    throw 'Declared material inventory identity/selection/same-tick coverage changed.'
                }
            }
            $detail
        }
    }
    $entityObservationTiming=if($EntitySampleEvery -eq 1){'at_native_sample'}else{'between_native_samples_after_first'}
    $null = & $RecordEvidence ([pscustomobject]@{event='production-experiment-intent';mode=$Mode;entityIds=$EntityIds;itemIds=$ItemIds;materialInventorySelections=$materialSelections;entitySampleEvery=$EntitySampleEvery;entityObservationTiming=$entityObservationTiming;intervalGameTicks=$IntervalGameTicks;windowGameTicks=$WindowGameTicks;pollSeconds=$PollSeconds;requiredWindows=$RequiredWindows;requiredContinuousGameTicks=$RequiredContinuousGameTicks;maximumSamples=$MaximumSamples;maximumRequests=$MaximumRequests;deadlineUtc=$deadline.ToUniversalTime().ToString('o');gameWrites=0})
    try {
        $initial=& $read get_session_state @{}
        Assert-SpherewrightStageSession $initial $SessionId $PlanetId $GameVersion
        # Exclude the rolling window that predates the declared experiment. Wait in
        # game ticks, within the original wall/request budget, not via a model loop.
        $experiment.nextTick=[long]$initial.gameTick+$WindowGameTicks
        while ($experiment.samples -lt $MaximumSamples) {
            $beginMarker=& $ReceiptMarker
            $state=& $read get_session_state @{}
            Assert-SpherewrightStageSession $state $SessionId $PlanetId $GameVersion
            if ($state.revision -ne $initial.revision -or ($null -ne $experiment.lastSessionTick -and $state.gameTick -lt $experiment.lastSessionTick)) { throw 'Read-only experiment revision/tick changed; stop for review.' }
            $experiment.lastSessionTick=[long]$state.gameTick
            $entityObservationPerformed=($experiment.samples % $EntitySampleEvery) -eq 0
            $earlyEntityObservation=$EntitySampleEvery -gt 1 -and $experiment.samples -gt 0 -and $entityObservationPerformed
            $details=@()
            # Use the waiting interval, rather than adding48 serial reads AFTER
            # the next counter is due. The first full observation still waits
            # for a wholly post-start native window. Entity timestamps remain
            # their actual earlier read times, never the counter timestamp.
            if ($earlyEntityObservation) {
                $details=@(& $readEntities)
                $state=& $read get_session_state @{}
                Assert-SpherewrightStageSession $state $SessionId $PlanetId $GameVersion
                if ($state.revision -ne $initial.revision -or $state.gameTick -lt $experiment.lastSessionTick) { throw 'Read-only experiment revision/tick changed; stop for review.' }
                $experiment.lastSessionTick=[long]$state.gameTick
            }
            while ($state.gameTick -lt $experiment.nextTick) {
                $remaining=($deadline-(Get-Date)).TotalMilliseconds
                if ($remaining -le 0) { throw 'Finite sampling deadline reached.' }
                $sleepMs=[int][math]::Min($PollSeconds*1000,$remaining)
                if ($Mode -ceq 'continuous' -and $EntitySampleEvery -gt 1) {
                    $sleepMs=Get-SpherewrightProductionSleepMilliseconds -RemainingGameTicks ($experiment.nextTick-[long]$state.gameTick) -ObservedTicksPerSecond $experiment.observedTicksPerSecond -MaximumSleepMilliseconds ($PollSeconds*1000) -RemainingWallMilliseconds $remaining
                }
                Start-Sleep -Milliseconds $sleepMs; $experiment.pollWaitMs+=$sleepMs
                if ($EntitySampleEvery -eq 1) { break }
                $state=& $read get_session_state @{}
                Assert-SpherewrightStageSession $state $SessionId $PlanetId $GameVersion
                if ($state.revision -ne $initial.revision -or $state.gameTick -lt $experiment.lastSessionTick) { throw 'Read-only experiment revision/tick changed; stop for review.' }
                $experiment.lastSessionTick=[long]$state.gameTick
            }
            if ($EntitySampleEvery -eq 1 -and $state.gameTick -lt $experiment.nextTick) { continue }
            if ($entityObservationPerformed -and -not $earlyEntityObservation) { $details=@(& $readEntities) }
            $power=& $read get_power_summary @{planetId=$PlanetId}
            if ($power.sessionId -cne $SessionId -or $power.planetId -ne $PlanetId) { throw 'Power observation identity changed.' }
            $production=& $read get_overseer_production @{itemIds=$ItemIds;limit=8}
            if ($production.sessionId -cne $SessionId -or $production.nextCursor -or
                $production.totalFactoryCount -ne $production.returnedFactoryCount -or
                (@($production.requestedItemIds|Sort-Object) -join ',') -cne (@($ItemIds|Sort-Object) -join ',')) { throw 'Production identity/item/complete-page coverage unproved.' }
            $window=Get-SpherewrightStageField $production 'window'
            foreach($field in @('state','startGameTick','endGameTick','elapsedGameTicks','crossedSessionBoundary')){$null=Get-SpherewrightStageField $window $field}
            $endMarker=& $ReceiptMarker
            if ($beginMarker.runId -notmatch '^[0-9a-f]{32}$' -or $endMarker.runId -cne $beginMarker.runId -or $endMarker.ordinal -le $beginMarker.ordinal) { throw 'Original sample receipt range unproved.' }
            # Never give the validator the previous sample's buffers/config as
            # fresh observations. A counter-only sample has an explicit empty
            # entity list; its callback must deliberately handle that scope.
            $sample=[pscustomobject]@{state=$state;entities=$details;entityObservationPerformed=$entityObservationPerformed;entitySampleEvery=$EntitySampleEvery;entityObservationTiming=$entityObservationTiming;power=$power;production=$production}
            $approval=@(& $ValidateObservation $sample)
            if ($approval.Count -ne 1 -or $approval[0] -isnot [bool]) { throw 'Observation validator must return exactly one Boolean; retain evidence.' }
            $valid=$approval[0] -and $window.state -ceq 'ready' -and $window.crossedSessionBoundary -eq $false
            $resetReason=$null
            if ($window.state -ceq 'ready' -and ($null -eq $window.startGameTick -or $window.elapsedGameTicks -ne $WindowGameTicks -or
                $window.endGameTick-$window.startGameTick+1 -ne $WindowGameTicks -or $production.capturedAtGameTick -lt $window.endGameTick)) { throw 'Native window fields do not match the declared period.' }
            if ($window.state -ceq 'ready' -and $window.startGameTick -le $initial.gameTick) { throw 'Native window predates the declared experiment; do not count it.' }
            if ($window.state -ceq 'ready' -and $null -ne $experiment.lastWindowEnd -and $window.endGameTick -le $experiment.lastWindowEnd) { throw 'Stale/regressed native window; do not count or replay it.' }
            $local=@($production.planets|Where-Object planetId -EQ $PlanetId)
            if ($local.Count -ne 1 -or @($local[0].production).Count -ne $ItemIds.Count -or
                (@($local[0].production.itemId|Sort-Object) -join ',') -cne (@($ItemIds|Sort-Object) -join ',')) { throw 'Local item-rate coverage unproved.' }
            $experiment.lastRates=@($local[0].production|Select-Object itemId,itemName,producedCount,consumedCount,actualProductionPerMinute,actualConsumptionPerMinute)
            if ($Mode -ceq 'independent') {
                if ($valid -and $null -ne $experiment.lastWindowEnd -and $window.startGameTick -le $experiment.lastWindowEnd) { throw 'Independent windows overlap; evidence cannot be combined.' }
                if ($valid) { $experiment.qualifying++ }
            } elseif ($valid) {
                if ($null -eq $experiment.endTick -or $window.startGameTick -gt $experiment.endTick+1) {
                    if ($null -ne $experiment.endTick) { $experiment.resets++;$resetReason='sample_gap' }
                    $experiment.startTick=[long]$window.startGameTick
                }
                $experiment.endTick=[long]$window.endGameTick
                $experiment.covered=$experiment.endTick-$experiment.startTick+1
            }
            if (-not $valid) {
                $experiment.resets++;$resetReason='observation_not_qualifying'
                $experiment.covered=0;$experiment.startTick=$null;$experiment.endTick=$null
            }
            $experiment.samples++
            if ($entityObservationPerformed) { $experiment.entitySamples++ }
            $experiment.lastWindowEnd=[long]$window.endGameTick
            $experiment.nextTick=[long]$window.endGameTick+$IntervalGameTicks
            if ($null -eq $experiment.firstTick) { $experiment.firstTick=$production.capturedAtGameTick }
            $experiment.lastTick=$production.capturedAtGameTick
            $null=& $RecordEvidence ([pscustomobject]@{event='production-sample';index=$experiment.samples;runId=$beginMarker.runId;firstOrdinal=$beginMarker.ordinal+1;lastOrdinal=$endMarker.ordinal;entityObservationPerformed=$entityObservationPerformed;entityCount=$details.Count;entityObservationTiming=$entityObservationTiming;observationKind=$(if($entityObservationPerformed){'entities_power_production'}else{'power_production_only'});window=$window;valid=$valid;resetReason=$resetReason;coveredGameTicks=$experiment.covered;rates=$experiment.lastRates;healthScope='sampled_only'})
            if (($Mode -ceq 'independent' -and $experiment.qualifying -ge $RequiredWindows) -or ($Mode -ceq 'continuous' -and $experiment.covered -ge $RequiredContinuousGameTicks)) { break }
        }
        $final=& $read get_session_state @{}
        Assert-SpherewrightStageSession $final $SessionId $PlanetId $GameVersion
        if ($final.revision -ne $initial.revision -or $final.gameTick -lt $experiment.lastTick) { throw 'Final experiment identity/revision/tick boundary changed.' }
        $completed=($Mode -ceq 'independent' -and $experiment.qualifying -ge $RequiredWindows) -or ($Mode -ceq 'continuous' -and $experiment.covered -ge $RequiredContinuousGameTicks)
        $summary=[pscustomobject]@{event='production-experiment-result';result=$(if($completed){'sampling_completed'}else{'not_proven'});samples=$experiment.samples;entitySamples=$experiment.entitySamples;entitySampleEvery=$EntitySampleEvery;qualifyingWindows=$experiment.qualifying;coveredGameTicks=$experiment.covered;resetCount=$experiment.resets;requests=$experiment.requests;firstObservedTick=$experiment.firstTick;lastObservedTick=$experiment.lastTick;lastRates=$experiment.lastRates;gameWrites=0;modelDecisionsInsideLoop=0;governorAcceptance=$false;healthScope='sampled_only';timingMs=@{reads=[math]::Round($experiment.readWallMs,3);scheduledWait=$experiment.pollWaitMs;total=((Get-Date)-$started).TotalMilliseconds}}
        $null=& $RecordEvidence $summary
        return $summary
    } catch {
        $samplingError=$_
        $failureKind=$samplingError.Exception.Data['spherewrightSamplingFailureKind']
        if (-not $failureKind) { $failureKind='observation_failed' }
        $samplingError.Exception.Data['spherewrightSamplingFailureKind']=$failureKind
        $samplingError.Exception.Data['spherewrightSamplingRequests']=$experiment.requests
        $samplingError.Exception.Data['spherewrightSamplingSamples']=$experiment.samples
        $samplingError.Exception.Data['spherewrightSamplingDeadlineUtc']=$deadline.ToUniversalTime().ToString('o')
        $samplingError.Exception.Data['spherewrightGameWrites']=0
        # Keep completed sample receipts even when a later read fails. Failure is
        # not a production verdict, and never starts another experiment/worker.
        try {
            $null=& $RecordEvidence ([pscustomobject]@{event='production-experiment-failed';result='not_proven';failureKind=$failureKind;method=$experiment.lastMethod;materialInventoryReasonCode=$samplingError.Exception.Data['spherewrightMaterialInventoryReasonCode'];message=$samplingError.Exception.Message;samples=$experiment.samples;entitySamples=$experiment.entitySamples;entitySampleEvery=$EntitySampleEvery;qualifyingWindows=$experiment.qualifying;requests=$experiment.requests;maximumRequests=$MaximumRequests;lastObservedTick=$experiment.lastTick;coveredGameTicks=$experiment.covered;deadlineUtc=$deadline.ToUniversalTime().ToString('o');gameWrites=0;governorAcceptance=$false;automaticRestart=$false;timingMs=@{reads=[math]::Round($experiment.readWallMs,3);scheduledWait=$experiment.pollWaitMs;total=((Get-Date)-$started).TotalMilliseconds}})
        } catch {
            $samplingError.Exception.Data['spherewrightFailureEvidenceError']=$_.Exception.Message
        }
        throw $samplingError
    }
}
