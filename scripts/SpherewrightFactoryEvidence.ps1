# Derived views of explicit immutable pages, plus exact local receipt reads.
# Importing this file makes zero requests and performs no file access.
Set-StrictMode -Version Latest
$script:swFactoryStatic = @('objectId','itemId','objectKind','componentKind','position','rotation','connections','recipeId','forceAccelerationMode','filterItemId','storageConfiguration','powerNetworkId','resourceNodeIds','pickTargetObjectId','insertTargetObjectId')
$script:swFactoryDynamic = @('isWorking','progress','progressRequired','powerDemandPerTick','powerServeRatio','buffers','tankFluidCount','beltCargo','inserterStage','inserterStackCount','requiredBuildItemCount','constructionProgress','sorterEndpoints')
$script:swFactoryMetadata = @('sessionId','planetId','name','recipeName','filterItemName','capturedAtGameTick','stateHash','stateHashVersion','configurationStateHash','configurationStateHashVersion','endpointStateHash','endpointStateHashVersion','materialInventoryCut','fuelPowerState','geometryCut')
$script:swStationStatic = @('planetId','entityId','stationId','galacticStationId','buildingItemId','position','isInterstellar','isCollector','isVeinCollector','powerNetworkId','energyCapacity','maximumChargeEnergyPerTick','maximumChargePowerWatts','warperCapacity','droneCapacity','vesselCapacity','droneTripRangeRaw','vesselTripRangeRaw','includeOrbitCollectors','warpEnableDistanceRaw','warpersRequired','droneDeliverySetting','vesselDeliverySetting','pilerCount','droneAutoReplenish','vesselAutoReplenish','remoteGroupMask','remoteRoutePriority')
$script:swStationDynamic = @('powerServeRatio','energy','requestedChargeEnergyPerTick','requestedChargePowerWatts','warperCount','idleDroneCount','workingDroneCount','idleVesselCount','workingVesselCount','neededItemIds')
$script:swStationMetadata = @('sessionId','buildingName','capturedAtGameTick','stateHash','stateHashVersion','configurationStateHash','configurationStateHashVersion','fleetStateHash','fleetStateHashVersion')

function Read-SpherewrightEvidenceRecord {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$EvidenceDirectory,
        [Parameter(Mandatory)][ValidatePattern('^(action|resume)-[0-9a-f]{32}-[0-9]{4,}-[A-Za-z0-9_-]+\.json$')][string]$RecordName,
        [ValidatePattern('^[0-9a-fA-F]{64}$')][string]$ExpectedSha256
    )
    # Read ONE named original, not the directory history. Keep this raw object
    # in local/protected comparisons; do not emit it into chat or commit it.
    # This is not complete action coverage, independent sign-off or fresh state.
    if ($RecordName -cnotmatch '^(action|resume)-([0-9a-f]{32})-([0-9]{4,})-([A-Za-z0-9_-]+)\.json$') { throw 'Invalid exact evidence record name.' }
    $family = $Matches[1]; $runId = $Matches[2]; $recordType = $Matches[4]
    $directory = Get-Item -LiteralPath $EvidenceDirectory -ErrorAction Stop
    if ($directory -isnot [IO.DirectoryInfo]) { throw 'An explicit local evidence directory is required.' }
    $file = Get-Item -LiteralPath (Join-Path $directory.FullName $RecordName) -ErrorAction Stop
    if ($file -isnot [IO.FileInfo] -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'A regular evidence file is required.' }
    if ($file.Length -gt 8388608) { throw 'The named evidence record exceeds the 8 MiB read budget.' }
    # Hash and parse the same bounded bytes; do not reopen after a hash check.
    $stream = [IO.File]::Open($file.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        if ($stream.Length -gt 8388608) { throw 'The named evidence record exceeds the 8 MiB read budget.' }
        $bytes = [byte[]]::new([int]$stream.Length)
        $offset = 0
        while ($offset -lt $bytes.Length) {
            $read = $stream.Read($bytes, $offset, $bytes.Length - $offset)
            if ($read -eq 0) { throw 'The named evidence record is incomplete.' }
            $offset += $read
        }
    } finally { $stream.Dispose() }
    if ($ExpectedSha256) {
        $hasher = [Security.Cryptography.SHA256]::Create()
        try { $hash = ([BitConverter]::ToString($hasher.ComputeHash($bytes))).Replace('-', '') }
        finally { $hasher.Dispose() }
        if ($hash -ine $ExpectedSha256) { throw 'The named evidence record hash does not match.' }
    }
    $encoding = [Text.UTF8Encoding]::new($false, $true)
    $record = $encoding.GetString($bytes).TrimStart([char]0xFEFF) | ConvertFrom-Json -ErrorAction Stop
    foreach ($field in @('runId','ordinal','recordType','payload')) {
        if ($null -eq $record -or $null -eq $record.PSObject.Properties[$field] -or $null -eq $record.$field) { throw "Evidence record field missing: $field" }
    }
    if (($record.ordinal -isnot [int] -and $record.ordinal -isnot [long]) -or $record.ordinal -lt 0 -or $record.ordinal -gt [int]::MaxValue -or
        $record.runId -cne $runId -or $record.recordType -cne $recordType -or
        $RecordName -cne ('{0}-{1}-{2:D4}-{3}.json' -f $family, $runId, [int]$record.ordinal, $record.recordType)) {
        throw 'The named evidence record identity does not match its filename.'
    }
    return $record
}

function ConvertTo-SpherewrightEvidenceJson($Value) {
    ConvertTo-Json -InputObject $Value -Depth 30 -Compress
}
function Select-SpherewrightEvidenceFields($Value, [string[]]$Fields) {
    $selected = [ordered]@{}
    foreach ($field in $Fields) {
        if ($null -eq $Value.PSObject.Properties[$field]) { throw "Snapshot field missing: $field" }
        $selected[$field] = $Value.$field
    }
    return [pscustomobject]$selected
}
function Assert-SpherewrightEvidenceShape($Value, [string[]]$KnownFields) {
    foreach ($property in $Value.PSObject.Properties) {
        if ($property.Name -notin $KnownFields) { throw "Unclassified nested field: $($property.Name)" }
    }
}
function New-SpherewrightFactoryEvidenceView {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateCount(1, 512)][object[]]$Pages,
        [Parameter(Mandatory)][ValidateRange(0, 100000)][int]$ExpectedEntityCount
    )
    $first = $Pages[0]
    $null = Select-SpherewrightEvidenceFields $first @('sessionId','planetId','snapshotId','capturedAtGameTick','entities','nextCursor')
    if (-not $first.sessionId -or -not $first.snapshotId) { throw 'Snapshot identity unavailable.' }
    $entities = @{}; $cursors = @{}
    for ($i=0; $i -lt $Pages.Count; $i++) {
        $page = $Pages[$i]
        if ($page.sessionId -cne $first.sessionId -or $page.planetId -ne $first.planetId -or
            $page.snapshotId -cne $first.snapshotId -or $page.capturedAtGameTick -ne $first.capturedAtGameTick -or $null -eq $page.entities -or
            ($i -lt $Pages.Count-1 -and -not $page.nextCursor) -or ($i -eq $Pages.Count-1 -and $page.nextCursor)) { throw 'Mixed or incomplete immutable snapshot pages.' }
        if ($page.nextCursor) {
            if ($cursors.ContainsKey([string]$page.nextCursor)) { throw 'Repeated snapshot cursor.' }
            $cursors[[string]$page.nextCursor]=$true
        }
        foreach ($entity in @($page.entities)) {
            if ($entity.objectId -le 0 -or $entities.ContainsKey([int]$entity.objectId) -or
                $entity.sessionId -cne $first.sessionId -or $entity.planetId -ne $first.planetId -or
                $entity.capturedAtGameTick -ne $first.capturedAtGameTick) { throw 'Duplicate/mixed entity identity.' }
            foreach ($field in @('position','rotation','connections','resourceNodeIds','buffers')) {
                if ($null -eq $entity.PSObject.Properties[$field] -or $null -eq $entity.$field) { throw "Required snapshot observation unknown: $field" }
            }
            # The shared native DTO carries this optional wrapper on ordinary rows.
            # Only a null placeholder is row metadata: never silently discard a
            # populated selected-stock/cargo cut or mistake it for a factory page.
            $materialCut = $entity.PSObject.Properties['materialInventoryCut']
            if ($null -ne $materialCut -and $null -ne $materialCut.Value) {
                throw 'Populated materialInventoryCut is not an immutable factory entity row.'
            }
            # Current native list rows carry a null detail-only fuel placeholder.
            # A populated detail observation requires its own evidence scope;
            # it must never disappear from a factory comparison silently.
            $fuelState = $entity.PSObject.Properties['fuelPowerState']
            if ($null -ne $fuelState -and $null -ne $fuelState.Value) {
                throw 'Populated fuelPowerState is not an immutable factory entity row.'
            }
            # The geometry detail endpoint shares the native entity DTO. Only
            # its null placeholder belongs to an ordinary immutable list row.
            # A populated cut has separate hash/coverage/terrain semantics and
            # must not disappear from a factory projection silently.
            $geometryCut = $entity.PSObject.Properties['geometryCut']
            if ($null -ne $geometryCut -and $null -ne $geometryCut.Value) {
                throw 'Populated geometryCut is not an immutable factory entity row.'
            }
            $known = $script:swFactoryStatic + $script:swFactoryDynamic + $script:swFactoryMetadata + @('logisticsStation')
            foreach ($property in $entity.PSObject.Properties) {
                if ($property.Name -notin $known) { throw "Unclassified snapshot field: $($property.Name)" }
            }
            $static = Select-SpherewrightEvidenceFields $entity $script:swFactoryStatic
            $static.connections = @($entity.connections | Sort-Object slot | ForEach-Object {
                Assert-SpherewrightEvidenceShape $_ @('slot','isOutput','otherObjectId','otherSlot')
                Select-SpherewrightEvidenceFields $_ @('slot','isOutput','otherObjectId','otherSlot')
            })
            # Preserve the existing audit's static projection; station buffers,
            # energy/fleet and input-selector observations are separate evidence.
            $stationStatic = $null
            $stationDynamic = $null
            if ($entity.logisticsStation) {
                if ($null -eq $entity.logisticsStation.storageSlots -or $null -eq $entity.logisticsStation.beltSlots) { throw 'Station slot coverage unknown.' }
                foreach ($property in $entity.logisticsStation.PSObject.Properties) {
                    if ($property.Name -notin ($script:swStationStatic+$script:swStationDynamic+$script:swStationMetadata+@('storageSlots','beltSlots'))) { throw "Unclassified station field: $($property.Name)" }
                }
                $stationStatic = Select-SpherewrightEvidenceFields $entity.logisticsStation $script:swStationStatic
                foreach ($slot in $entity.logisticsStation.storageSlots) { Assert-SpherewrightEvidenceShape $slot @('index','itemId','itemName','count','inc','maximumCount','localOrder','remoteOrder','totalOrdered','localSupplyCount','localDemandCount','remoteSupplyCount','remoteDemandCount','localLogic','remoteLogic','keepMode','keepIncRatio') }
                foreach ($slot in $entity.logisticsStation.beltSlots) { Assert-SpherewrightEvidenceShape $slot @('index','direction','beltComponentId','beltEntityId','storageIndex','counter') }
                if (@($entity.logisticsStation.storageSlots|ForEach-Object index|Sort-Object -Unique).Count -ne @($entity.logisticsStation.storageSlots).Count -or
                    @($entity.logisticsStation.beltSlots|ForEach-Object index|Sort-Object -Unique).Count -ne @($entity.logisticsStation.beltSlots).Count) { throw 'Duplicate station slot identity.' }
                $storage = @($entity.logisticsStation.storageSlots | Sort-Object index | ForEach-Object { Select-SpherewrightEvidenceFields $_ @('index','itemId','maximumCount','localLogic','remoteLogic','keepMode','keepIncRatio') })
                $belts = @($entity.logisticsStation.beltSlots | Sort-Object index | ForEach-Object {
                    $slot = Select-SpherewrightEvidenceFields $_ @('index','direction','beltEntityId','beltComponentId')
                    if ($_.direction -eq 'Output') { $slot | Add-Member -NotePropertyName storageIndex -NotePropertyValue $_.storageIndex }
                    $slot
                })
                $stationStatic | Add-Member -NotePropertyName storageSlots -NotePropertyValue $storage
                $stationStatic | Add-Member -NotePropertyName beltSlots -NotePropertyValue $belts
                $stationDynamic = Select-SpherewrightEvidenceFields $entity.logisticsStation $script:swStationDynamic
                $stationDynamic | Add-Member -NotePropertyName storageSlots -NotePropertyValue @($entity.logisticsStation.storageSlots | Sort-Object index | ForEach-Object { Select-SpherewrightEvidenceFields $_ @('index','count','inc','localOrder','remoteOrder','totalOrdered','localSupplyCount','localDemandCount','remoteSupplyCount','remoteDemandCount') })
                $stationDynamic | Add-Member -NotePropertyName beltSlots -NotePropertyValue @($entity.logisticsStation.beltSlots | Sort-Object index | ForEach-Object { Select-SpherewrightEvidenceFields $_ @('index','direction','counter','storageIndex') })
            }
            $static | Add-Member -NotePropertyName logisticsStation -NotePropertyValue $stationStatic
            $serialized = [ordered]@{}
            foreach ($field in $static.PSObject.Properties) { $serialized[$field.Name]=ConvertTo-SpherewrightEvidenceJson $field.Value }
            $dynamic = Select-SpherewrightEvidenceFields $entity $script:swFactoryDynamic
            $dynamic | Add-Member -NotePropertyName logisticsStation -NotePropertyValue $stationDynamic
            $entities[[int]$entity.objectId] = [pscustomobject]@{static=$serialized;dynamic=(ConvertTo-SpherewrightEvidenceJson $dynamic);connections=$entity.connections}
            if ($entities.Count -gt 100000) { throw 'Snapshot entity budget exceeded.' }
        }
    }
    if ($entities.Count -ne $ExpectedEntityCount) { throw 'Snapshot count differs from sealed coverage evidence.' }
    [pscustomobject]@{sessionId=$first.sessionId;planetId=$first.planetId;snapshotId=$first.snapshotId;capturedAtGameTick=$first.capturedAtGameTick;entityCount=$entities.Count;entities=$entities;coverage='supplied_pages_and_sealed_count';notFreshPreflight=$true}
}

function Compare-SpherewrightFactoryEvidence {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Before, [Parameter(Mandatory)]$After, [object[]]$AllowedStaticChanges = @())
    if ($Before.planetId -ne $After.planetId -or $After.capturedAtGameTick -lt $Before.capturedAtGameTick) { throw 'Snapshot planet/tick continuity unproved.' }
    # Across sessions, owned identity continuity must be proved separately. This
    # projection never adopts a world or waives an original-receipt audit.
    $changes = [Collections.Generic.List[object]]::new(); $dynamic = [Collections.Generic.List[int]]::new()
    $usedRules = @{}
    foreach ($id in @($Before.entities.Keys | Sort-Object)) {
        if (-not $After.entities.ContainsKey($id)) { continue }
        foreach ($field in $Before.entities[$id].static.Keys) {
            $old = $Before.entities[$id].static[$field]; $now = $After.entities[$id].static[$field]
            if ($old -ceq $now) { continue }
            $rules = @($AllowedStaticChanges | Where-Object { $_.objectId -eq $id -and $_.field -ceq $field })
            $allowed = $false
            if ($rules.Count) {
                if ($rules.Count -ne 1 -or (ConvertTo-SpherewrightEvidenceJson $rules[0].before) -cne $old -or
                    (ConvertTo-SpherewrightEvidenceJson $rules[0].after) -cne $now -or
                    $rules[0].evidenceRef -notmatch '^[0-9a-f]{32}/[0-9]{4}$' -or -not $rules[0].reason) { throw 'Static allowance requires exact before/after and original evidence.' }
                $allowed = $true; $usedRules["$id/$field"]=$true
            }
            $changes.Add([pscustomobject]@{objectId=$id;field=$field;before=$old;after=$now;allowed=$allowed;evidenceRef=$(if($allowed){$rules[0].evidenceRef}else{$null})})
        }
        if ($Before.entities[$id].dynamic -cne $After.entities[$id].dynamic) { $dynamic.Add($id) }
    }
    if ($usedRules.Count -ne $AllowedStaticChanges.Count) { throw 'Unused/duplicate allowance; do not waive unrelated changes.' }
    $edges = [Collections.Generic.List[object]]::new()
    foreach ($id in $After.entities.Keys) {
        foreach ($edge in $After.entities[$id].connections) {
            if (-not $After.entities.ContainsKey([int]$edge.otherObjectId) -or @($After.entities[[int]$edge.otherObjectId].connections | Where-Object {
                $_.slot -eq $edge.otherSlot -and $_.otherObjectId -eq $id -and $_.otherSlot -eq $edge.slot -and $_.isOutput -ne $edge.isOutput
            }).Count -ne 1) { $edges.Add([pscustomobject]@{objectId=$id;slot=$edge.slot}) }
        }
    }
    [pscustomobject]@{beforeSnapshot=$Before.snapshotId;afterSnapshot=$After.snapshotId;addedObjectIds=@($After.entities.Keys | Where-Object {-not $Before.entities.ContainsKey($_)} | Sort-Object);removedObjectIds=@($Before.entities.Keys | Where-Object {-not $After.entities.ContainsKey($_)} | Sort-Object);staticChanges=$changes.ToArray();dynamicChangedObjectIds=$dynamic.ToArray();nonreciprocalEdges=$edges.ToArray();auditCoverage='factory_projection_only';independentAcceptance=$false}
}
