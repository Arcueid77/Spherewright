$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'SpherewrightActionClient.ps1')
. (Join-Path $PSScriptRoot 'SpherewrightStageTools.ps1')
. (Join-Path $PSScriptRoot 'SpherewrightProductionSampling.ps1')
$script:checks=0
function Assert-Sampling([bool]$Condition,[string]$Name){if(-not $Condition){throw "Sampling regression: $Name"};$script:checks++}
function Reset-Sampling([long[]]$Ends,[string]$Fault='',[int[]]$ItemIds=@(1109)){
    $script:ends=$Ends;$script:sampleIndex=0;$script:fault=$Fault;$script:itemIds=@($ItemIds);$script:entryBoundaryPending=$false;$script:calls=[Collections.Generic.List[string]]::new();$script:payloads=[Collections.Generic.List[object]]::new();$script:records=[Collections.Generic.List[object]]::new();$script:entityTimingWitnesses=[Collections.Generic.List[object]]::new();$script:now=[datetime]'2026-09-30T00:00:00Z';$script:startTime=$script:now
}
function Get-Date{$script:now}
function Get-SpherewrightProductionClockMilliseconds{($script:now-$script:startTime).TotalMilliseconds}
function Start-Sleep([int]$Milliseconds){$script:now=$script:now.AddMilliseconds($Milliseconds)}
function Invoke-SpherewrightBridgeRequest([string]$Method,[string]$SessionId,[hashtable]$Payload){
    $script:calls.Add($Method)
    $script:payloads.Add([pscustomobject]@{method=$Method;payload=$Payload})
    if($Method -like 'prepare_*' -or $Method -like 'commit_*'){throw 'A read-only sampler attempted a write'}
    if($script:fault -in @('source15fps','source20fps')){$script:now=$script:now.AddMilliseconds(130)}
    if($script:fault -ceq 'source60fps'){$script:now=$script:now.AddMilliseconds(120)}
    if($script:fault -in @('jitter15fps','jitter20fps')){
        # Alternate normal native reads and slower responses; do not assume
        # constant transport latency when declaring a full-source experiment.
        $latency=if(($script:calls.Count % 9) -eq 0){650}else{130}
        $script:now=$script:now.AddMilliseconds($latency)
    }
    $index=[math]::Min($script:sampleIndex,$script:ends.Count-1);$tick=$script:ends[$index]
    if($script:fault -in @('7.5fps','15fps','source15fps','source20fps','source60fps','jitter15fps','jitter20fps')){$ticksPerSecond=if($script:fault -ceq '7.5fps'){7.5}elseif($script:fault -ceq 'source60fps'){60}elseif($script:fault -in @('source20fps','jitter20fps')){20}else{15};$tick=[long](($script:now-$script:startTime).TotalSeconds*$ticksPerSecond)}
    $result=switch($Method){
        get_session_state {
            if($script:entryBoundaryPending){if($script:fault -notin @('7.5fps','15fps','source15fps','source20fps','source60fps','jitter15fps','jitter20fps')){$tick=if($script:sampleIndex -eq 0){0}else{$script:ends[$script:sampleIndex-1]}};$script:entryBoundaryPending=$false}
            if($script:fault -ceq 'paused' -and $index -gt 0){$tick=$script:ends[0]}
            [pscustomobject]@{sessionId=$SessionId;localPlanetId=104;gameVersion=$(if($script:fault -ceq 'version'){'other'}else{'fixture'});gameLoaded=$true;ownedBySpherewright=$true;accessRestricted=$false;writeHealth=$(if($script:fault -ceq 'quarantine'){'quarantined'}else{'healthy'});writeBlockers=@();gameTick=$tick;revision=$(if($script:fault -ceq 'revision' -and $script:sampleIndex -gt 0){23}else{22})}
        }
        inspect_factory_entity {
            $detail=[pscustomobject]@{sessionId=$SessionId;planetId=104;objectId=$Payload.objectId;recipeId=17;capturedAtGameTick=$tick;buffers=@([pscustomobject]@{role='input';itemId=1006;count=4})}
            if($Payload.ContainsKey('materialInventoryObjectIds') -and $script:fault -cne 'cutMissing'){
                $cutObjects=@(foreach($selected in $Payload.materialInventoryObjectIds){[pscustomobject]@{sessionId=$SessionId;planetId=104;objectId=$selected;capturedAtGameTick=$tick;buffers=@()}})
                $cut=[pscustomobject]@{state='observed';reasonCode=$null;coverage='explicit_objects_and_complete_native_cargo_paths';sessionId=$SessionId;planetId=104;capturedAtGameTick=$tick;requestedObjectIds=$Payload.materialInventoryObjectIds.Clone();objects=$cutObjects;cargoPaths=@([pscustomobject]@{pathId=7;capturedAtGameTick=$tick;beltObjectIds=@(3405)})}
                switch($script:fault){
                    cutUnavailable {$cut.state='unavailable';$cut.reasonCode='fixture_cut_unavailable'}
                    cutSession {$cut.sessionId='other-session'}
                    cutPlanet {$cut.planetId=105}
                    cutCoverage {$cut.coverage='partial_paths'}
                    cutTick {$cut.capturedAtGameTick=$tick-1}
                    cutSelection {$cut.requestedObjectIds=@(3404)}
                    cutObjects {$cut.objects=@($cutObjects|Select-Object -First 1)}
                    cutObjectTick {$cut.objects[0].capturedAtGameTick=$tick-1}
                    cutPathTick {$cut.cargoPaths[0].capturedAtGameTick=$tick-1}
                }
                $detail|Add-Member -NotePropertyName materialInventoryCut -NotePropertyValue $cut
            }
            $detail
        }
        get_power_summary {[pscustomobject]@{sessionId=$SessionId;planetId=104;networks=@([pscustomobject]@{consumerRatio=$(if($script:fault -ceq 'power' -and $index -eq 1){0.5}else{1.0})})}}
        get_overseer_production {
            if($script:fault -ceq 'deadline'){$script:now=$script:now.AddSeconds(200)}
            $script:sampleIndex++
            $state=if($script:fault -ceq 'warming' -and $index -eq 0){'warming_up'}else{'ready'}
            $returnedItemIds=@($script:itemIds)
            if($script:fault -ceq 'incompleteProduction'){$returnedItemIds=@($returnedItemIds|Select-Object -First ($returnedItemIds.Count-1))}
            $productionRows=@(foreach($itemId in $returnedItemIds){[pscustomobject]@{itemId=$itemId;itemName="fixture item $itemId";producedCount=5;consumedCount=4;actualProductionPerMinute=30.0;actualConsumptionPerMinute=24.0}})
            [pscustomobject]@{sessionId=$SessionId;capturedAtGameTick=$tick;totalFactoryCount=1;returnedFactoryCount=1;requestedItemIds=@($script:itemIds);nextCursor=$(if($script:fault -ceq 'cursor'){'unread-page'}else{$null});window=[pscustomobject]@{state=$state;startGameTick=$tick-599;endGameTick=$tick;elapsedGameTicks=600;crossedSessionBoundary=$false};planets=@(if($script:fault -cne 'localCoverage'){[pscustomobject]@{planetId=104;production=$productionRows}})}
        }
        default{throw "Unexpected read $Method"}
    }
    [pscustomobject]@{success=$true;result=$result}
}
$arguments=@{SessionId='fixture-session';PlanetId=104;GameVersion='fixture';EntityIds=@(3404);ItemIds=@(1109);ValidateObservation={param($s) $s.entities[0].recipeId -eq 17 -and $s.power.networks[0].consumerRatio -eq 1 -and $s.production.planets[0].production[0].actualProductionPerMinute -gt 0};ReceiptMarker={[pscustomobject]@{runId='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';ordinal=$script:calls.Count}};RecordEvidence={param($row) $script:records.Add($row);if($row.event -ceq 'production-experiment-intent'){$script:entryBoundaryPending=$true}}}
$sleepArgs=@{RemainingGameTicks=39;ObservedTicksPerSecond=60;MaximumSleepMilliseconds=4000;RemainingWallMilliseconds=30000}
Reset-Sampling @(600)
Assert-Sampling ((Get-SpherewrightProductionSleepMilliseconds @sleepArgs) -eq 487) '39 remaining ticks at measured60TPS do not trigger a four-second wait'
Assert-Sampling ((Get-SpherewrightProductionSleepMilliseconds 600 15 4000 30000) -eq 4000) 'slow simulation still uses the declared maximum poll bound'
foreach($unavailableRate in @(0.0,[double]::NaN,[double]::PositiveInfinity)){
    Assert-Sampling ((Get-SpherewrightProductionSleepMilliseconds 39 $unavailableRate 4000 30000) -eq 4000) 'unknown or paused rate falls back without inventing ticks'
}
Assert-Sampling ((Get-SpherewrightProductionSleepMilliseconds 39 60 4000 43.9) -eq 43) 'adaptive wait cannot cross the original remaining wall budget'
Assert-Sampling ((Get-SpherewrightProductionSleepMilliseconds 0 60 4000 30000) -eq 0 -and (Get-SpherewrightProductionSleepMilliseconds 1 60 4000 30000) -eq 12 -and (Get-SpherewrightProductionSleepMilliseconds 1 3000 4000 30000) -eq 1) 'due target never sleeps and near-target polling has no coarse minimum wait'
$badSleep=$null;try{Get-SpherewrightProductionSleepMilliseconds -1 60 4000 30000|Out-Null}catch{$badSleep=$_}
Assert-Sampling ($null -ne $badSleep -and $script:calls.Count -eq 0) 'invalid wait input rejects without a native call'
$invalidPoll=@{};foreach($key in $arguments.Keys){$invalidPoll[$key]=$arguments[$key]};$invalidPoll.Mode='continuous';$invalidPoll.EntitySampleEvery=2;$invalidPoll.PollSeconds=11
$invalidPollError=$null;try{Invoke-SpherewrightProductionExperiment @invalidPoll|Out-Null}catch{$invalidPollError=$_}
Assert-Sampling ($null -ne $invalidPollError -and $script:calls.Count -eq 0) 'entry PollSeconds bound rejects values above10 before any read, not mid-experiment'
Reset-Sampling @(600,1206,1812)
$result=Invoke-SpherewrightProductionExperiment @arguments
Assert-Sampling ($result.result -ceq 'sampling_completed' -and $result.qualifyingWindows -eq 3 -and $result.requests -eq 14) 'three fixed independent windows run in one bounded entry'
Assert-Sampling ($result.gameWrites -eq 0 -and $result.modelDecisionsInsideLoop -eq 0 -and -not $result.governorAcceptance) 'no writes or in-loop model decisions, no Governor sign-off'
Assert-Sampling (@($script:records|Where-Object event -EQ 'production-sample').Count -eq 3 -and $script:records[1].firstOrdinal -eq 2 -and $script:records[1].lastOrdinal -eq 5) 'all samples link to original read ranges'
$batchedWindows=@($script:records|Where-Object event -EQ 'production-sample'|ForEach-Object { $_.window|ConvertTo-Json -Compress })

$wideIds=@(1101..1112)
$wide=@{};foreach($key in $arguments.Keys){$wide[$key]=$arguments[$key]};$wide.ItemIds=$wideIds
Reset-Sampling @(600,1206,1812) '' $wideIds
$wideResult=Invoke-SpherewrightProductionExperiment @wide
$wideProductionCalls=@($script:payloads|Where-Object method -CEQ 'get_overseer_production')
$wideQueriesValid=$wideProductionCalls.Count -eq 3 -and @($wideProductionCalls|Where-Object { $_.payload.limit -ne 8 -or (@($_.payload.itemIds|Sort-Object) -join ',') -cne (@($wideIds|Sort-Object) -join ',') }).Count -eq 0
Assert-Sampling ($wideResult.result -ceq 'sampling_completed' -and $wideResult.samples -eq 3 -and $wideResult.qualifyingWindows -eq 3 -and $wideResult.requests -eq 14) 'twelve source items qualify across three native windows with the unchanged request budget'
Assert-Sampling ($wideQueriesValid -and @($script:records|Where-Object event -EQ 'production-sample'|Where-Object { $_.rates.Count -eq 12 }).Count -eq 3) 'all three reads request the complete twelve-item set and retain every returned rate'
Assert-Sampling ($wideResult.gameWrites -eq 0 -and $script:calls.Count -eq 14 -and @($script:calls|Where-Object {$_ -like 'prepare_*' -or $_ -like 'commit_*'}).Count -eq 0) 'twelve-item observation stays read-only within fourteen requests'

$chainIds=@(1101..1115)
$chain=@{};foreach($key in $arguments.Keys){$chain[$key]=$arguments[$key]};$chain.EntityIds=@(1..32);$chain.ItemIds=$chainIds;$chain.RequiredWindows=1;$chain.MaximumSamples=1;$chain.MaximumRequests=40
Reset-Sampling @(600) '' $chainIds
$chainResult=Invoke-SpherewrightProductionExperiment @chain
$chainProductionCalls=@($script:payloads|Where-Object method -CEQ 'get_overseer_production')
$chainEntityCalls=@($script:payloads|Where-Object method -CEQ 'inspect_factory_entity')
$chainIntent=@($script:records|Where-Object event -EQ 'production-experiment-intent')
Assert-Sampling ($chainResult.result -ceq 'sampling_completed' -and $chainResult.samples -eq 1 -and $chainResult.qualifyingWindows -eq 1 -and $chainResult.requests -eq 37 -and $chainIntent.Count -eq 1 -and $chainIntent[0].maximumRequests -eq 40) 'fifteen items and thirty-two entities fit one window within the explicit forty-request budget'
Assert-Sampling ($chainEntityCalls.Count -eq 32 -and $chainProductionCalls.Count -eq 1 -and (@($chainProductionCalls[0].payload.itemIds|Sort-Object) -join ',') -ceq (@($chainIds|Sort-Object) -join ',') -and @($script:records|Where-Object event -EQ 'production-sample'|Where-Object { $_.rates.Count -eq 15 }).Count -eq 1) 'full fixed entity/item scope is covered in one complete fifteen-item response'
Assert-Sampling ($chainResult.gameWrites -eq 0 -and $chainResult.modelDecisionsInsideLoop -eq 0 -and @($script:calls|Where-Object {$_ -like 'prepare_*' -or $_ -like 'commit_*'}).Count -eq 0) 'bounded thirty-seven-request fixture remains read-only with no model loop'

$boundaryIds=@(1101..1116)
$boundary=@{};foreach($key in $arguments.Keys){$boundary[$key]=$arguments[$key]};$boundary.ItemIds=$boundaryIds;$boundary.RequiredWindows=1;$boundary.MaximumSamples=1
Reset-Sampling @(600) '' $boundaryIds
$boundaryResult=Invoke-SpherewrightProductionExperiment @boundary
$boundaryProduction=@($script:payloads|Where-Object method -CEQ 'get_overseer_production')
Assert-Sampling ($boundaryResult.result -ceq 'sampling_completed' -and $boundaryResult.samples -eq 1 -and $boundaryResult.requests -eq 6 -and $boundaryProduction.Count -eq 1 -and @($script:records|Where-Object event -EQ 'production-sample'|Where-Object { $_.rates.Count -eq 16 }).Count -eq 1) 'existing sixteen-item scope still succeeds as one complete source-item response'

# Actual complete-chain observation scope; no writes and no split item windows.
$completeEntities=@(1500,1511,723,724,725,726,727,814,827,883,885,869,871,562,3073,3074,5326,5334,5333,5329,5331,870,861,5187,863,3348,3084,3966,3964,3965,3083,163,784,95,862,753,129,86,752,2802,5171,1496,2440,10,26,1217,1216,1213)
$completeItems=@(1000,1001,1002,1004,1005,1006,1007,1101,1102,1103,1104,1106,1107,1109,1112,1114,1116,1120,1121,1123,1127,1201,1202,1203,1204,1205,1206,1209,1210,1802)
$complete=@{};foreach($key in $arguments.Keys){$complete[$key]=$arguments[$key]};$complete.EntityIds=$completeEntities;$complete.ItemIds=$completeItems;$complete.RequiredWindows=1;$complete.MaximumSamples=1;$complete.MaximumRequests=60
Reset-Sampling @(600) '' $completeItems
$completeResult=Invoke-SpherewrightProductionExperiment @complete
$completeQueries=@($script:payloads|Where-Object method -CEQ 'get_overseer_production')
Assert-Sampling ($completeEntities.Count -eq 48 -and $completeItems.Count -eq 30 -and $completeResult.result -ceq 'sampling_completed' -and $completeResult.requests -eq 53 -and $completeResult.entitySampleEvery -eq 1 -and $completeResult.entitySamples -eq 1 -and @($script:payloads|Where-Object method -CEQ 'inspect_factory_entity').Count -eq 48) 'complete thirty-item chain still fresh-reads all entities in fifty-three requests'
$completeSamples=@($script:records|Where-Object event -EQ 'production-sample')
$completeRates=@($completeSamples|ForEach-Object {$_.rates}|ForEach-Object {$_.itemId})
Assert-Sampling ($completeQueries.Count -eq 1 -and (@($completeQueries[0].payload.itemIds|Sort-Object) -join ',') -ceq (@($completeItems|Sort-Object) -join ',') -and (@($completeRates|Sort-Object) -join ',') -ceq (@($completeItems|Sort-Object) -join ',') -and $completeSamples.Count -eq 1 -and $completeRates.Count -eq 30 -and $completeResult.gameWrites -eq 0) 'the exact thirty requested and returned source item IDs are complete in one native window'

$maximum=@{};foreach($key in $complete.Keys){$maximum[$key]=$complete[$key]};$maximum.EntityIds=@(1..48);$maximum.ItemIds=@(1101..1164);$maximum.MaximumRequests=60
Reset-Sampling @(600) '' $maximum.ItemIds
$maximumResult=Invoke-SpherewrightProductionExperiment @maximum
Assert-Sampling ($maximumResult.result -ceq 'sampling_completed' -and $maximumResult.requests -eq 53 -and @($script:payloads|Where-Object method -CEQ 'inspect_factory_entity').Count -eq 48 -and @($script:payloads|Where-Object method -CEQ 'get_overseer_production').Count -eq 1 -and @($script:records|Where-Object event -EQ 'production-sample'|Where-Object { $_.rates.Count -eq 64 }).Count -eq 1) 'forty-eight-entity and native sixty-four-item hard boundaries succeed without splitting'

$completeBudget=@{};foreach($key in $complete.Keys){$completeBudget[$key]=$complete[$key]};$completeBudget.MaximumRequests=51
Reset-Sampling @(600) '' $completeItems
$completeBudgetError=$null;try{Invoke-SpherewrightProductionExperiment @completeBudget|Out-Null}catch{$completeBudgetError=$_}
Assert-Sampling ($null -ne $completeBudgetError -and $completeBudgetError.Exception.Data['spherewrightSamplingFailureKind'] -ceq 'request_budget_exhausted' -and $script:calls.Count -eq 51 -and $completeBudgetError.Exception.Data['spherewrightSamplingSamples'] -eq 0 -and @($script:payloads|Where-Object method -CEQ 'get_overseer_production').Count -eq 0) 'larger read scope cannot override a tight request budget or invent a completed sample'

# Offline virtual clocks include per-read latency, not just game simulation
# wait. Validate the EXACT larger scope and original long-run budgets first.
foreach($sourceClock in @('source15fps','source20fps')){
    $completeContinuous=@{};foreach($key in $complete.Keys){$completeContinuous[$key]=$complete[$key]}
    $completeContinuous.Mode='continuous';$completeContinuous.MaximumSamples=120;$completeContinuous.MaximumRequests=4090;$completeContinuous.TimeoutSeconds=3290;$completeContinuous.IntervalGameTicks=390;$completeContinuous.PollSeconds=4
    Reset-Sampling @(600) $sourceClock $completeItems
    $completeContinuousResult=Invoke-SpherewrightProductionExperiment @completeContinuous
    Assert-Sampling ($completeContinuousResult.result -ceq 'sampling_completed' -and $completeContinuousResult.coveredGameTicks -ge 36000 -and $completeContinuousResult.resetCount -eq 0 -and $completeContinuousResult.requests -le 4090 -and $completeContinuousResult.samples -le 120 -and $completeContinuousResult.timingMs.total -lt 3290000) "$sourceClock exact complete scope covers36000ticks within initial sample/read/wall caps"
    Assert-Sampling ($completeContinuousResult.gameWrites -eq 0 -and $completeContinuousResult.modelDecisionsInsideLoop -eq 0 -and -not $completeContinuousResult.governorAcceptance -and @($script:records|Where-Object event -EQ 'production-sample'|Where-Object { $_.rates.Count -ne 30 }).Count -eq 0) "$sourceClock complete source coverage grants no mutation or automatic supply sign-off"
}

# An explicitly declared second cadence separates expensive full inventory
# observations from fresh native counters/power. Empty means unobserved, not a
# cached inventory snapshot. Defaults and independent windows are unchanged.
$interleaved=@{};foreach($key in $complete.Keys){$interleaved[$key]=$complete[$key]}
$interleaved.Mode='continuous';$interleaved.EntitySampleEvery=2;$interleaved.IntervalGameTicks=390
$interleaved.MaximumSamples=120;$interleaved.MaximumRequests=4090;$interleaved.TimeoutSeconds=3290;$interleaved.PollSeconds=4
$interleaved.ValidateObservation={param($s)
    if($s.entityObservationPerformed){
        if($s.entities.Count -ne 48 -or @($s.entities|Where-Object recipeId -NE 17).Count){throw 'Full entity scope changed'}
        $script:entityTimingWitnesses.Add([pscustomobject]@{sample=$script:sampleIndex;lastEntityTick=$s.entities[-1].capturedAtGameTick;freshStateTick=$s.state.gameTick;counterTick=$s.production.window.endGameTick})
    }elseif($s.entities.Count -ne 0){throw 'Counter-only sample reused inventory data'}
    [bool]($s.power.networks[0].consumerRatio -eq 1)
}
$jitterBefore=@{};foreach($key in $interleaved.Keys){$jitterBefore[$key]=$interleaved[$key]}
$jitterBefore.EntitySampleEvery=1;$jitterBefore.IntervalGameTicks=390
Reset-Sampling @(600) 'jitter20fps' $completeItems
$jitterBeforeError=$null;try{Invoke-SpherewrightProductionExperiment @jitterBefore|Out-Null}catch{$jitterBeforeError=$_}
$jitterBeforeFailure=@($script:records|Where-Object event -CEQ 'production-experiment-failed')
Assert-Sampling ($null -ne $jitterBeforeError -and $jitterBeforeFailure.Count -eq 1 -and $jitterBeforeFailure[0].failureKind -ceq 'request_budget_exhausted' -and $jitterBeforeFailure[0].requests -eq 4090 -and $jitterBeforeFailure[0].samples -gt 0 -and @($script:records|Where-Object {$_.event -ceq 'production-sample' -and $_.resetReason -ceq 'sample_gap'}).Count -gt 0 -and -not $jitterBeforeFailure[0].automaticRestart) 'latency jitter reproduces the all-entity390-tick caller budget/gap failure without a production verdict'
foreach($jitterClock in @('jitter15fps','jitter20fps')){
    Reset-Sampling @(600) $jitterClock $completeItems
    $interleavedResult=Invoke-SpherewrightProductionExperiment @interleaved
    $interleavedSamples=@($script:records|Where-Object event -CEQ 'production-sample')
    $fullSamples=@($interleavedSamples|Where-Object entityObservationPerformed -EQ $true)
    $counterSamples=@($interleavedSamples|Where-Object entityObservationPerformed -EQ $false)
    Assert-Sampling ($interleavedResult.result -ceq 'sampling_completed' -and $interleavedResult.coveredGameTicks -ge 36000 -and $interleavedResult.resetCount -eq 0 -and $interleavedResult.requests -le 4090 -and $interleavedResult.timingMs.total -lt 3290000) "$jitterClock declared dual cadence completes inside unchanged sample/read/wall limits"
    Assert-Sampling ($fullSamples.Count -eq $interleavedResult.entitySamples -and $fullSamples.Count -eq [math]::Ceiling($interleavedSamples.Count/2.0) -and @($fullSamples|Where-Object {$_.entityCount -ne 48 -or $_.observationKind -cne 'entities_power_production'}).Count -eq 0 -and $counterSamples.Count -gt 0 -and @($counterSamples|Where-Object {$_.entityCount -ne 0 -or $_.observationKind -cne 'power_production_only'}).Count -eq 0) "$jitterClock explicitly marks full versus unobserved inventory scope"
    Assert-Sampling (@($script:payloads|Where-Object method -CEQ 'inspect_factory_entity').Count -eq 48*$fullSamples.Count -and @($script:payloads|Where-Object method -CEQ 'get_power_summary').Count -eq $interleavedSamples.Count -and @($script:payloads|Where-Object method -CEQ 'get_overseer_production').Count -eq $interleavedSamples.Count -and @($interleavedSamples|Where-Object {$_.rates.Count -ne 30}).Count -eq 0) "$jitterClock every sample still has fresh power and the same complete thirty-item native window"
    Assert-Sampling ($script:records[0].entitySampleEvery -eq 2 -and $script:records[0].entityObservationTiming -ceq 'between_native_samples_after_first' -and $interleavedResult.gameWrites -eq 0 -and $interleavedResult.modelDecisionsInsideLoop -eq 0 -and -not $interleavedResult.governorAcceptance) "$jitterClock inventory timing/cadence is declared before execution and grants no production sign-off or writes"
    Assert-Sampling (@($script:entityTimingWitnesses|Where-Object {$_.sample -gt 1 -and $_.lastEntityTick -lt $_.freshStateTick}).Count -gt 0 -and @($script:entityTimingWitnesses|Where-Object {$_.lastEntityTick -gt $_.counterTick}).Count -eq 0) "$jitterClock early entity timestamps are retained and the post-wait session is fresh"
    if($jitterClock -ceq 'jitter20fps'){$jitterAfterResult=$interleavedResult}
}
# Same fixed48-entity scope,390-tick target and120ms native latency, at60TPS.
# Only sleep scheduling differs. The old coarse wait cannot pass; no gaps are
# interpolated, forgiven or joined, and neither run writes to the game.
$adaptiveSleep=(Get-Item Function:Get-SpherewrightProductionSleepMilliseconds).ScriptBlock
try{
    Set-Item Function:Get-SpherewrightProductionSleepMilliseconds -Value {
        param([long]$RemainingGameTicks,[double]$ObservedTicksPerSecond,[int]$MaximumSleepMilliseconds,[double]$RemainingWallMilliseconds)
        [int][math]::Floor([math]::Min($MaximumSleepMilliseconds,$RemainingWallMilliseconds))
    }
    Reset-Sampling @(600) 'source60fps' $completeItems
    $fastBeforeResult=Invoke-SpherewrightProductionExperiment @interleaved
}finally{Set-Item Function:Get-SpherewrightProductionSleepMilliseconds -Value $adaptiveSleep}
Reset-Sampling @(600) 'source60fps' $completeItems
$fastAfterResult=Invoke-SpherewrightProductionExperiment @interleaved
Assert-Sampling ($fastBeforeResult.result -ceq 'not_proven' -and $fastBeforeResult.resetCount -gt 0 -and $fastBeforeResult.samples -eq 120) 'fixed four-second polling reproduces high-rate sample gaps under the same finite declaration'
Assert-Sampling ($fastAfterResult.result -ceq 'sampling_completed' -and $fastAfterResult.coveredGameTicks -ge 36000 -and $fastAfterResult.resetCount -eq 0 -and $fastAfterResult.requests -le 4090 -and $fastAfterResult.timingMs.total -lt 3290000) 'adaptive polling completes the same60TPS fixture without relaxing continuity or budgets'
Assert-Sampling (@($script:calls|Where-Object {$_ -like 'prepare_*' -or $_ -like 'commit_*'}).Count -eq 0 -and $fastAfterResult.gameWrites -eq 0 -and $fastAfterResult.modelDecisionsInsideLoop -eq 0 -and -not $fastAfterResult.governorAcceptance) 'adaptive scheduling remains read-only and is not independent production acceptance'
$interleavedShort=@{};foreach($key in $interleaved.Keys){$interleavedShort[$key]=$interleaved[$key]};$interleavedShort.MaximumSamples=3
foreach($interleavedFault in @('gap','power','revision','budget')){
    Reset-Sampling $(if($interleavedFault -ceq 'gap'){@(600,1300,1900)}else{@(600,1200,1800)}) $(if($interleavedFault -in @('gap','budget')){''}else{$interleavedFault}) $completeItems
    $boundedInterleaved=@{};foreach($key in $interleavedShort.Keys){$boundedInterleaved[$key]=$interleavedShort[$key]}
    if($interleavedFault -ceq 'budget'){$boundedInterleaved.MaximumRequests=56}
    $interleavedFaultError=$null;$interleavedFaultResult=$null
    try{$interleavedFaultResult=Invoke-SpherewrightProductionExperiment @boundedInterleaved}catch{$interleavedFaultError=$_}
    if($interleavedFault -in @('gap','power')){
        $expectedCredit=if($interleavedFault -ceq 'gap'){1200}else{600}
        Assert-Sampling ($null -eq $interleavedFaultError -and $interleavedFaultResult.result -ceq 'not_proven' -and $interleavedFaultResult.resetCount -eq 1 -and $interleavedFaultResult.coveredGameTicks -eq $expectedCredit -and $interleavedFaultResult.entitySamples -eq 2) "$interleavedFault on counter-only sampling still resets continuity with no fabricated inventories"
    }else{
        $expectedSamples=if($interleavedFault -ceq 'revision'){1}else{2}
        $retainedSamples=@($script:records|Where-Object event -CEQ 'production-sample')
        Assert-Sampling ($null -ne $interleavedFaultError -and $interleavedFaultError.Exception.Data['spherewrightSamplingSamples'] -eq $expectedSamples -and $retainedSamples.Count -eq $expectedSamples -and @($script:calls|Where-Object {$_ -like 'prepare_*' -or $_ -like 'commit_*'}).Count -eq 0) "$interleavedFault stops once and preserves only completed native windows"
    }
}
Reset-Sampling @(600,1200,1800) '' $completeItems
$unsafeEntityCallback=@{};foreach($key in $interleavedShort.Keys){$unsafeEntityCallback[$key]=$interleavedShort[$key]}
$unsafeEntityCallback.ValidateObservation={param($s) if($s.entities.Count -ne 48){throw 'Expected fresh entity evidence'};[bool]($s.entities[0].recipeId -eq 17)}
$unsafeEntityCallbackError=$null;try{Invoke-SpherewrightProductionExperiment @unsafeEntityCallback|Out-Null}catch{$unsafeEntityCallbackError=$_}
Assert-Sampling ($null -ne $unsafeEntityCallbackError -and $unsafeEntityCallbackError.Exception.Data['spherewrightSamplingSamples'] -eq 1 -and @($script:payloads|Where-Object method -CEQ 'inspect_factory_entity').Count -eq 48) 'unadapted full-entity validator fails closed instead of receiving cached inventory on a counter-only sample'
Reset-Sampling @(600,1200,1800)
$badInterleaving=@{};foreach($key in $arguments.Keys){$badInterleaving[$key]=$arguments[$key]};$badInterleaving.EntitySampleEvery=2
$badInterleavingError=$null;try{Invoke-SpherewrightProductionExperiment @badInterleaving|Out-Null}catch{$badInterleavingError=$_}
Assert-Sampling ($null -ne $badInterleavingError -and $script:calls.Count -eq 0) 'independent inventory windows cannot silently become counter-only observations'
foreach($badEvery in @(0,5)){
    Reset-Sampling @(600)
    $invalidEvery=@{};foreach($key in $interleaved.Keys){$invalidEvery[$key]=$interleaved[$key]};$invalidEvery.EntitySampleEvery=$badEvery
    $everyError=$null;try{Invoke-SpherewrightProductionExperiment @invalidEvery|Out-Null}catch{$everyError=$_}
    Assert-Sampling ($null -ne $everyError -and $script:calls.Count -eq 0) 'invalid entity cadence rejects before any request'
}


foreach($scopeCase in @(
    [pscustomobject]@{name='sixty-five items';ids=[int[]](1101..1165)},
    [pscustomobject]@{name='duplicate item id';ids=[int[]]@(1109,1109)},
    [pscustomobject]@{name='nonpositive item id';ids=[int[]]@(0)}
)){
    Reset-Sampling @(600,1206,1812) '' $scopeCase.ids
    $badScope=@{};foreach($key in $arguments.Keys){$badScope[$key]=$arguments[$key]};$badScope.ItemIds=$scopeCase.ids
    $scopeError=$null;try{Invoke-SpherewrightProductionExperiment @badScope|Out-Null}catch{$scopeError=$_}
    Assert-Sampling ($null -ne $scopeError -and $script:calls.Count -eq 0) "$($scopeCase.name) rejects before any request"
}

Reset-Sampling @(600) '' @(1109)
$tooManyEntities=@{};foreach($key in $arguments.Keys){$tooManyEntities[$key]=$arguments[$key]};$tooManyEntities.EntityIds=@(1..49)
$entityScopeError=$null;try{Invoke-SpherewrightProductionExperiment @tooManyEntities|Out-Null}catch{$entityScopeError=$_}
Assert-Sampling ($null -ne $entityScopeError -and $script:calls.Count -eq 0) 'forty-nine entity scope rejects before any request'

$incomplete=@{};foreach($key in $arguments.Keys){$incomplete[$key]=$arguments[$key]};$incomplete.ItemIds=$chainIds;$incomplete.RequiredWindows=1;$incomplete.MaximumSamples=1;$incomplete.MaximumRequests=40;$incomplete.ValidateObservation={param($sample) $true}
Reset-Sampling @(600) 'incompleteProduction' $chainIds
$incompleteError=$null;try{Invoke-SpherewrightProductionExperiment @incomplete|Out-Null}catch{$incompleteError=$_}
$incompleteFailures=@($script:records|Where-Object event -EQ 'production-experiment-failed')
Assert-Sampling ($null -ne $incompleteError -and $incompleteError.Exception.Data['spherewrightSamplingSamples'] -eq 0 -and $incompleteError.Exception.Data['spherewrightSamplingRequests'] -eq 5) 'incomplete fifteen-item response fails without sample credit'
Assert-Sampling (@($script:records|Where-Object event -EQ 'production-sample').Count -eq 0 -and $incompleteFailures.Count -eq 1 -and $incompleteFailures[0].samples -eq 0 -and $incompleteFailures[0].qualifyingWindows -eq 0) 'incomplete source-item coverage cannot advance samples or qualification'

# Same three native-window fixtures, split into three scheduler entries. This is
# an offline caller comparison, NOT three measured model calls or token savings.
Reset-Sampling @(600,1206,1812)
$single=@{};foreach($key in $arguments.Keys){$single[$key]=$arguments[$key]};$single.RequiredWindows=1;$single.MaximumSamples=1
for($i=0;$i -lt 3;$i++){ $null=Invoke-SpherewrightProductionExperiment @single }
$splitRequests=$script:calls.Count
$splitWindows=@($script:records|Where-Object event -EQ 'production-sample'|ForEach-Object { $_.window|ConvertTo-Json -Compress })
Assert-Sampling ($splitRequests -eq 18 -and ($splitWindows -join '|') -ceq ($batchedWindows -join '|')) 'three entries versus one retain identical native windows, avoid four duplicate boundary reads'

# Same ten entities, 15 ticks/second, 180 seconds and 90-read cap. A fast
# polling caller burns its cap; changing cadence, not caps, completes offline.
$slow=@{};foreach($key in $arguments.Keys){$slow[$key]=$arguments[$key]};$slow.EntityIds=@(1..10)
Reset-Sampling @(600,1206,1812) '15fps'
$slow.PollSeconds=1
$slowError=$null;try{Invoke-SpherewrightProductionExperiment @slow|Out-Null}catch{$slowError=$_}
Assert-Sampling ($null -ne $slowError -and $slowError.Exception.Data['spherewrightSamplingFailureKind'] -ceq 'request_budget_exhausted' -and $script:calls.Count -eq 90) '15fps one-second polling hits the original hard request cap'
$slowFailure=@($script:records|Where-Object event -EQ 'production-experiment-failed')
Assert-Sampling ($slowFailure.Count -eq 1 -and $slowFailure[0].samples -eq 1 -and $slowFailure[0].requests -eq 90 -and $slowFailure[0].result -ceq 'not_proven' -and -not $slowFailure[0].automaticRestart) 'failure persists the actual cap and one completed sample, not a game verdict or restart'
Assert-Sampling (@($script:records|Where-Object event -EQ 'production-sample').Count -eq 1 -and $slowError.Exception.Data['spherewrightSamplingSamples'] -eq 1) 'a later budget failure cannot erase a completed original window'
$slow.Remove('PollSeconds')
Reset-Sampling @(600,1206,1812) '15fps'
$slowResult=Invoke-SpherewrightProductionExperiment @slow
Assert-Sampling ($slowResult.result -ceq 'sampling_completed' -and $slowResult.samples -eq 3 -and $slowResult.requests -lt 90 -and $slowResult.timingMs.total -le 180000) 'five-second default completes at 15fps within unchanged time/read caps'
Assert-Sampling ($script:records[0].pollSeconds -eq 5 -and $slowResult.gameWrites -eq 0 -and $slowResult.modelDecisionsInsideLoop -eq 0) 'cadence is declared before reads with no model loop or writes'

# Identical15fps virtual-clock fixtures: a30-minute cap cannot cover ten
# game minutes. Declare a finite45-minute task BEFORE it starts, not a retry
# or extension of any live experiment. Existing sample/read caps stay bounded.
$slowContinuous=@{};foreach($key in $arguments.Keys){$slowContinuous[$key]=$arguments[$key]}
$slowContinuous.EntityIds=@(1..15);$slowContinuous.Mode='continuous';$slowContinuous.MaximumSamples=120
$slowContinuous.RequiredContinuousGameTicks=36000;$slowContinuous.IntervalGameTicks=330
$slowContinuous.PollSeconds=2;$slowContinuous.MaximumRequests=4096;$slowContinuous.TimeoutSeconds=1800
Reset-Sampling @(600) '15fps'
$slowContinuousError=$null;try{Invoke-SpherewrightProductionExperiment @slowContinuous|Out-Null}catch{$slowContinuousError=$_}
$slowContinuousFailure=@($script:records|Where-Object event -EQ 'production-experiment-failed')
Assert-Sampling ($null -ne $slowContinuousError -and $slowContinuousError.Exception.Data['spherewrightSamplingFailureKind'] -in @('deadline_exhausted','read_crossed_deadline')) '15fps36000-tick task stops at its original1800-second deadline'
Assert-Sampling ($slowContinuousFailure.Count -eq 1 -and -not $slowContinuousFailure[0].automaticRestart) 'slow continuous deadline failure is retained without extending or replaying it'
$slowContinuous.TimeoutSeconds=2700
Reset-Sampling @(600) '15fps'
$slowContinuousResult=Invoke-SpherewrightProductionExperiment @slowContinuous
Assert-Sampling ($slowContinuousResult.result -ceq 'sampling_completed' -and $slowContinuousResult.coveredGameTicks -ge 36000 -and $slowContinuousResult.resetCount -eq 0) 'predeclared2700-second finite task covers36000 contiguous ticks at15fps'
Assert-Sampling ($slowContinuousResult.timingMs.total -ge 2400000 -and $slowContinuousResult.timingMs.total -le 2700000 -and $slowContinuousResult.requests -le 4096 -and $slowContinuousResult.samples -le 120) 'slow continuous fixture obeys initial wall/read/sample bounds'
Assert-Sampling ($slowContinuousResult.gameWrites -eq 0 -and $slowContinuousResult.modelDecisionsInsideLoop -eq 0 -and -not $slowContinuousResult.governorAcceptance) 'longer initial budget grants no mutations,model loop or production sign-off'
$verySlow=@{};foreach($key in $slowContinuous.Keys){$verySlow[$key]=$slowContinuous[$key]};$verySlow.TimeoutSeconds=3600
Reset-Sampling @(600) '7.5fps'
$verySlowError=$null;try{Invoke-SpherewrightProductionExperiment @verySlow|Out-Null}catch{$verySlowError=$_}
$verySlowFailures=@($script:records|Where-Object event -EQ 'production-experiment-failed')
Assert-Sampling ($null -ne $verySlowError -and $verySlowError.Exception.Data['spherewrightSamplingFailureKind'] -in @('deadline_exhausted','read_crossed_deadline') -and $verySlowFailures.Count -eq 1 -and -not $verySlowFailures[0].automaticRestart) '7.5fps original3600-second declaration stops and persists failure without extension'
$verySlow.TimeoutSeconds=5400
Reset-Sampling @(600) '7.5fps'
$verySlowError=$null;try{Invoke-SpherewrightProductionExperiment @verySlow|Out-Null}catch{$verySlowError=$_}
$verySlowFailures=@($script:records|Where-Object event -EQ 'production-experiment-failed')
Assert-Sampling ($null -ne $verySlowError -and $verySlowError.Exception.Data['spherewrightSamplingFailureKind'] -ceq 'request_budget_exhausted' -and $script:calls.Count -eq 4096 -and $verySlowFailures.Count -eq 1 -and -not $verySlowFailures[0].automaticRestart) '7.5fps two-second polling spends the original4096-request cap before its5400-second deadline'
# A separately declared slower cadence uses the same5400-second/read/sample
# bounds; neither of the two failed fixture tasks is extended or restarted.
$verySlow.PollSeconds=5
Reset-Sampling @(600) '7.5fps'
$verySlowResult=Invoke-SpherewrightProductionExperiment @verySlow
Assert-Sampling ($verySlowResult.result -ceq 'sampling_completed' -and $verySlowResult.coveredGameTicks -ge 36000 -and $verySlowResult.resetCount -eq 0 -and $verySlowResult.timingMs.total -ge 4800000 -and $verySlowResult.timingMs.total -le 5400000 -and $verySlowResult.requests -le 4096 -and $verySlowResult.samples -le 120) 'predeclared5400-second continuous task covers36000 native fixture ticks at7.5fps within unchanged read/sample bounds'
Assert-Sampling ($verySlowResult.gameWrites -eq 0 -and $verySlowResult.modelDecisionsInsideLoop -eq 0 -and -not $verySlowResult.governorAcceptance) 'slow-clock initial budget grants no mutation,model loop or acceptance credit'
Reset-Sampling @(600)
$tooLong=@{};foreach($key in $arguments.Keys){$tooLong[$key]=$arguments[$key]};$tooLong.TimeoutSeconds=7201;$tooLong.Mode='continuous'
$tooLongError=$null;try{Invoke-SpherewrightProductionExperiment @tooLong|Out-Null}catch{$tooLongError=$_}
Assert-Sampling ($null -ne $tooLongError -and $script:calls.Count -eq 0) 'over7200-second declaration rejects before any request'
$tooLong.Mode='independent';$tooLong.TimeoutSeconds=3601
$tooLongError=$null;try{Invoke-SpherewrightProductionExperiment @tooLong|Out-Null}catch{$tooLongError=$_}
Assert-Sampling ($null -ne $tooLongError -and $script:calls.Count -eq 0) 'independent mode retains its3600-second ceiling before any request'

$continuous=@{};foreach($key in $arguments.Keys){$continuous[$key]=$arguments[$key]};$continuous.Mode='continuous';$continuous.IntervalGameTicks=600;$continuous.MaximumSamples=60;$continuous.MaximumRequests=300
Reset-Sampling @(1..60|ForEach-Object {$_*600})
$result=Invoke-SpherewrightProductionExperiment @continuous
Assert-Sampling ($result.coveredGameTicks -eq 36000 -and $result.samples -eq 60 -and $result.requests -eq 242 -and $result.resetCount -eq 0) 'only a contiguous 36000-tick fixture completes continuous sampling'
Assert-Sampling ($result.healthScope -ceq 'sampled_only' -and -not $result.governorAcceptance) 'does not overclaim health/attribution/declaration validation'
$continuous.MaximumSamples=3
Reset-Sampling @(600,1206,1806)
$result=Invoke-SpherewrightProductionExperiment @continuous
Assert-Sampling ($result.result -ceq 'not_proven' -and $result.resetCount -eq 1 -and $result.coveredGameTicks -eq 1200) 'gap resets credit instead of joining separated windows'
Reset-Sampling @(600,1200,1800) power
$result=Invoke-SpherewrightProductionExperiment @continuous
Assert-Sampling ($result.coveredGameTicks -eq 600 -and $result.resetCount -eq 1 -and $result.result -ceq 'not_proven') 'failed predeclared power condition resets continuity'
Reset-Sampling @(600,1206,1812) warming
$result=Invoke-SpherewrightProductionExperiment @arguments
Assert-Sampling ($result.result -ceq 'not_proven' -and $result.qualifyingWindows -eq 2) 'warming window is not an independent production proof'

foreach($fault in @('cursor','version','quarantine','deadline','paused','budget')){
    Reset-Sampling @(600,1206,1812) $fault
    $limited=@{};foreach($key in $arguments.Keys){$limited[$key]=$arguments[$key]}
    if($fault -ceq 'paused'){$limited.TimeoutSeconds=3}
    if($fault -ceq 'budget'){$limited.MaximumRequests=3}
    $errorRecord=$null;try{Invoke-SpherewrightProductionExperiment @limited|Out-Null}catch{$errorRecord=$_}
    Assert-Sampling ($null -ne $errorRecord -and @($script:calls|Where-Object {$_ -like 'prepare_*' -or $_ -like 'commit_*'}).Count -eq 0) "$fault stops without mutations or replay"
    if($fault -ceq 'deadline'){Assert-Sampling ($script:calls.Count -eq 5 -and $errorRecord.Exception.Data['spherewrightSamplingRequests'] -eq 5) 'read crossing original deadline cannot extend observation'}
    if($fault -ceq 'budget'){Assert-Sampling ($script:calls.Count -eq 3 -and $errorRecord.Exception.Data['spherewrightSamplingFailureKind'] -ceq 'request_budget_exhausted') 'hard request cap with exact failure kind'}
    if($fault -in @('version','quarantine')){Assert-Sampling (@($script:records|Where-Object event -EQ 'production-experiment-failed').Count -eq 1) 'initial identity/health failure is retained too'}
}
Reset-Sampling @(600,1206,1812)
$evidenceFailure=@{};foreach($key in $arguments.Keys){$evidenceFailure[$key]=$arguments[$key]};$evidenceFailure.MaximumRequests=3
$evidenceFailure.RecordEvidence={param($row) if($row.event -ceq 'production-experiment-failed'){throw 'fixture evidence write failed'};$script:records.Add($row);if($row.event -ceq 'production-experiment-intent'){$script:entryBoundaryPending=$true}}
$evidenceError=$null;try{Invoke-SpherewrightProductionExperiment @evidenceFailure|Out-Null}catch{$evidenceError=$_}
Assert-Sampling ($evidenceError.Exception.Data['spherewrightSamplingFailureKind'] -ceq 'request_budget_exhausted' -and $evidenceError.Exception.Data['spherewrightFailureEvidenceError'] -ceq 'fixture evidence write failed' -and $script:calls.Count -eq 3) 'failure-record persistence error cannot replace the original sampling failure or trigger new reads'
Reset-Sampling @(600,1206,1812) 'localCoverage'
$coverage=@{};foreach($key in $arguments.Keys){$coverage[$key]=$arguments[$key]};$coverage.ValidateObservation={param($sample) $true}
$coverageError=$null;try{Invoke-SpherewrightProductionExperiment @coverage|Out-Null}catch{$coverageError=$_}
Assert-Sampling ($coverageError.Exception.Message -ceq 'Local item-rate coverage unproved.' -and $coverageError.Exception.Data['spherewrightSamplingSamples'] -eq 0 -and @($script:records|Where-Object event -EQ 'production-sample').Count -eq 0) 'incomplete local rates cannot advance completed-sample or qualification counts'
Reset-Sampling @(600,1206,1812)
$bad=@{};foreach($key in $arguments.Keys){$bad[$key]=$arguments[$key]};$bad.EntityIds=@(3404,3404)
try{Invoke-SpherewrightProductionExperiment @bad|Out-Null}catch{}
Assert-Sampling ($script:calls.Count -eq 0) 'invalid scope rejects before the first request'

# Stock cuts use the same existing inspect requests, not another sampling loop.
$withCut=@{};foreach($key in $arguments.Keys){$withCut[$key]=$arguments[$key]}
$withCut.MaterialInventorySelections=@{3404=@(3404,3405)}
$withCut.ValidateObservation={param($sample) $sample.entities[0].materialInventoryCut.state -ceq 'observed'}
Reset-Sampling @(600,1206,1812)
$cutResult=Invoke-SpherewrightProductionExperiment @withCut
$cutReads=@($script:payloads|Where-Object method -CEQ 'inspect_factory_entity')
Assert-Sampling ($cutResult.result -ceq 'sampling_completed' -and $cutResult.requests -eq 14 -and $cutReads.Count -eq 3) 'explicit material cuts add no requests to the same three-window fixture'
Assert-Sampling (@($cutReads|Where-Object {($_.payload.materialInventoryObjectIds -join ',') -cne '3404,3405'}).Count -eq 0 -and $script:records[0].materialInventorySelections.Count -eq 1) 'fixed cut scope is recorded before reads and forwarded unchanged every observation'
Assert-Sampling ($cutResult.gameWrites -eq 0 -and -not $cutResult.governorAcceptance) 'stock observation grants no writes or production sign-off'

$aliasCut=[hashtable]::new();$aliasCut.Add([int]3404,@(3404));$aliasCut.Add([string]'3404',@(3405))
$invalidCuts=@(
    @{3406=@(3404)},
    @{3404=@()},
    @{3404=@(1..257)},
    @{3404=@(3404,3404)},
    @{3404=@(0)},
    @{3404=@(-1)},
    @{3404=@('not-an-id')},
    @{3404=@(1.5)},
    @{3404=@(2147483648L)},
    $aliasCut
)
foreach($invalidCut in $invalidCuts){
    Reset-Sampling @(600)
    $invalidCutArgs=@{};foreach($key in $withCut.Keys){$invalidCutArgs[$key]=$withCut[$key]};$invalidCutArgs.MaterialInventorySelections=$invalidCut
    $cutError=$null;try{Invoke-SpherewrightProductionExperiment @invalidCutArgs|Out-Null}catch{$cutError=$_}
    Assert-Sampling ($null -ne $cutError -and $script:calls.Count -eq 0) 'invalid cut anchor/selection rejects before any native request'
}
foreach($cutFault in @('cutMissing','cutUnavailable','cutSession','cutPlanet','cutCoverage','cutTick','cutSelection','cutObjects','cutObjectTick','cutPathTick')){
    Reset-Sampling @(600,1206,1812) $cutFault
    $cutError=$null;try{Invoke-SpherewrightProductionExperiment @withCut|Out-Null}catch{$cutError=$_}
    $cutFailures=@($script:records|Where-Object event -CEQ 'production-experiment-failed')
    Assert-Sampling ($null -ne $cutError -and $cutFailures.Count -eq 1 -and $cutFailures[0].samples -eq 0 -and @($script:calls|Where-Object {$_ -ceq 'get_overseer_production'}).Count -eq 0) "$cutFault cannot silently qualify a sample or retry"
    Assert-Sampling (@($script:calls|Where-Object {$_ -ceq 'inspect_factory_entity'}).Count -eq 1 -and @($script:calls|Where-Object {$_ -like 'prepare_*' -or $_ -like 'commit_*'}).Count -eq 0) "$cutFault preserves the single failed read without actions"
    if($cutFault -ceq 'cutUnavailable'){
        Assert-Sampling ($cutError.Exception.Data['spherewrightSamplingFailureKind'] -ceq 'material_inventory_cut_unavailable' -and $cutError.Exception.Data['spherewrightMaterialInventoryReasonCode'] -ceq 'fixture_cut_unavailable' -and $cutFailures[0].materialInventoryReasonCode -ceq 'fixture_cut_unavailable') 'unavailable cut retains structured reason in exception and evidence'
    }
}
Reset-Sampling @(600,1206,1812)
$null=Invoke-SpherewrightProductionExperiment @arguments
Assert-Sampling (@($script:payloads|Where-Object {$_.method -ceq 'inspect_factory_entity' -and $_.payload.ContainsKey('materialInventoryObjectIds')}).Count -eq 0) 'default inspection payload is unchanged when no cut is declared'
[pscustomobject]@{passed=$script:checks;gameCalls=0;independentFixtureRequestsBefore=$splitRequests;independentFixtureRequestsAfter=14;entryInvocationsBefore=3;entryInvocationsAfter=1;slowFixtureRequestsBefore=90;slowFixtureCompletedBefore=$false;slowFixtureRequestsAfter=$slowResult.requests;slowFixtureWallMsAfter=$slowResult.timingMs.total;slowFixtureCompletedAfter=$true;continuousFixtureRequests=242;continuousFixtureTicks=36000;dualCadenceJitterFixture=[pscustomobject]@{ticksPerSecond=20;intervalGameTicks=390;maximumRequests=4090;maximumWallSeconds=3290;beforeRequests=$jitterBeforeFailure[0].requests;beforeCoveredTicks=$jitterBeforeFailure[0].coveredGameTicks;beforeCompleted=$false;beforeVirtualWallMs=$jitterBeforeFailure[0].timingMs.total;afterRequests=$jitterAfterResult.requests;afterCoveredTicks=$jitterAfterResult.coveredGameTicks;afterResets=$jitterAfterResult.resetCount;afterEntitySamples=$jitterAfterResult.entitySamples;afterCompleted=$true;afterVirtualWallMs=$jitterAfterResult.timingMs.total;liveValidation=$false};providerUsage=$null}|ConvertTo-Json -Depth 4 -Compress
