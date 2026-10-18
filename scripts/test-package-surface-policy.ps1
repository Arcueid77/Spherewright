[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SpherewrightPackageSurface.ps1')

$playbook = 'maximumObjectsToSubmit resumeBuildId never replay the whole blueprint spherewright_get_governor_plan nonzero measured baseline ten game minutes researchQueueReadback validationBaselineProposalHash inputEndpointFacingDot/outputEndpointFacingDot foundryIntent expectedFoundryPlanHash boundaryPorts 2011/2012 transportBudget inspect_factory_entity.geometry beltRoutePreview native_runtime_belt_collider_world_bounds ALL-belts-plus-two-sorters tokenless nativeBeltSegments nativeConditionsChecked=false'
$readNames = @('spherewright_get_overseer_production', 'spherewright_get_overseer_summary',
    'spherewright_get_overseer_diagnostic_bundle', 'spherewright_get_foundry_plan',
    'spherewright_inspect_blueprint', 'spherewright_export_blueprint',
    'spherewright_get_blueprint_builds', 'spherewright_get_governor_plan', 'spherewright_inspect_factory_entity')
$writeNames = @('spherewright_prepare_upgrade', 'spherewright_commit_upgrade',
    'spherewright_prepare_blueprint_build', 'spherewright_commit_blueprint_build',
    'spherewright_prepare_cancel_blueprint', 'spherewright_commit_cancel_blueprint',
    'spherewright_prepare_select_research', 'spherewright_prepare_dismantle', 'spherewright_commit_dismantle', 'spherewright_prepare_build')
$otherNames = @('spherewright_prepare_handcraft','spherewright_commit_configure_building','spherewright_get_local_star_system',
    'spherewright_prepare_new_game','spherewright_prepare_interplanetary_flight','spherewright_prepare_refuel',
    'spherewright_commit_logistics_station_fleet_transfer','spherewright_commit_reload_flight_checkpoint','spherewright_prepare_quarantine_reconciliation',
    'spherewright_commit_move','spherewright_commit_transfer','spherewright_prepare_harvest','spherewright_prepare_reload_flight_checkpoint',
    'spherewright_commit_resume_owned_game','spherewright_inspect_assembler','spherewright_commit_select_research','spherewright_inspect_resource_node',
    'spherewright_commit_new_game','spherewright_get_player_state','spherewright_get_progression_state','spherewright_commit_refuel',
    'spherewright_get_session_state','spherewright_prepare_save','spherewright_commit_harvest','spherewright_get_power_summary',
    'spherewright_list_factory_entities','spherewright_get_build_catalog','spherewright_commit_save_import','spherewright_commit_handcraft',
    'spherewright_commit_save','spherewright_get_gameplay_journal','spherewright_prepare_move','spherewright_list_resource_nodes',
    'spherewright_commit_interplanetary_flight','spherewright_prepare_save_import','spherewright_get_status',
    'spherewright_prepare_logistics_station_fleet_transfer','spherewright_get_action_result','spherewright_prepare_configure_building',
    'spherewright_prepare_transfer','spherewright_commit_build','spherewright_list_assemblers','spherewright_commit_quarantine_reconciliation',
    'spherewright_get_recipe_catalog','spherewright_prepare_resume_owned_game')
$resources = @([pscustomobject]@{uri='spherewright://agent/playbooks/opening-movement-v1';mimeType='text/markdown'})
function New-TestVectorSchema {
    [pscustomobject]@{type=@('object','null');properties=[pscustomobject]@{
        x=[pscustomobject]@{type='number'};y=[pscustomobject]@{type='number'};z=[pscustomobject]@{type='number'}
    }}
}
function New-TestEndpointSchema {
    [pscustomobject]@{type=@('object','null');properties=[pscustomobject]@{
        existingObjectId=[pscustomobject]@{type='integer'};expectedEndpointStateHash=[pscustomobject]@{type=@('string','null')}
        existingSlot=[pscustomobject]@{type='integer'};existingBeltQuarterTurns=[pscustomobject]@{type='integer'};plannedBeltQuarterTurns=[pscustomobject]@{type='integer'}
    }}
}
function New-TestSurface {
    foreach ($name in ($readNames + $writeNames + $otherNames)) {
        [pscustomobject]@{ name=$name; description='ordinary2011/2012 reciprocal recovery transportBudget'; annotations=[pscustomobject]@{readOnlyHint=($readNames -contains $name)}
            inputSchema=[pscustomobject]@{properties=[pscustomobject]@{
                prioritizeQueued=[pscustomobject]@{type='boolean'}; validationBaselineProposalHash=[pscustomobject]@{type='string'}
                blueprint=[pscustomobject]@{type='object'}; foundryIntent=[pscustomobject]@{type='object'}; expectedFoundryPlanHash=[pscustomobject]@{type='string'}
                geometry=[pscustomobject]@{type=@('object','null');description='READ-ONLY geometry';properties=[pscustomobject]@{
                    objectIds=[pscustomobject]@{type=@('array','null');items=[pscustomobject]@{type='integer'}}
                    terrainPoints=[pscustomobject]@{type=@('array','null');items=(New-TestVectorSchema)}
                    expectedFactoryGeometryHash=[pscustomobject]@{type=@('string','null')}
                    nativeBeltSegments=[pscustomobject]@{type=@('array','null');items=[pscustomobject]@{type=@('object','null');properties=[pscustomobject]@{
                        routingMode=[pscustomobject]@{type=@('string','null')};start=(New-TestVectorSchema);end=(New-TestVectorSchema)
                        startAltitudeLevel=[pscustomobject]@{type='integer'};endAltitudeLevel=[pscustomobject]@{type='integer'}
                    }}}
                }}
                beltRoutePreview=[pscustomobject]@{type=@('object','null');description='READ-ONLY tokenless executable=false';properties=[pscustomobject]@{
                    expectedFactoryGeometryHash=[pscustomobject]@{type=@('string','null')};filterItemId=[pscustomobject]@{type='integer'}
                    sourceSorterItemId=[pscustomobject]@{type='integer'};destinationSorterItemId=[pscustomobject]@{type='integer'}
                    source=(New-TestEndpointSchema);destination=(New-TestEndpointSchema)
                    segments=[pscustomobject]@{type=@('array','null');items=[pscustomobject]@{type=@('object','null');properties=[pscustomobject]@{
                        routingMode=[pscustomobject]@{type=@('string','null')};start=(New-TestVectorSchema);end=(New-TestVectorSchema)
                        startAltitudeLevel=[pscustomobject]@{type='integer'};endAltitudeLevel=[pscustomobject]@{type='integer'}
                    }}}
                }}
            }} }
    }
}
function Assert-Rejects([scriptblock]$Action, [string]$ExpectedMessage) {
    try { & $Action } catch {
        if ($_.Exception.Message.Contains($ExpectedMessage)) { return }
        throw
    }
    throw "Expected rejection containing: $ExpectedMessage"
}
function Test-Surface([object[]]$Surface, [string]$Text=$playbook) {
    Assert-SpherewrightPackageSurface -Version '0.4.0.0' -Tools $Surface -Resources $resources -PackagedPlaybook $Text -ResourcePlaybook $Text
}

$voyagerPlaybook = $playbook + ' spherewright_get_voyager_star_catalog spherewright_preview_voyager_route maximumFlightGameTicks returnReserveGameTicks maximumWarpGameTicksPerLeg maximumArrivalOffsetMeters destinationStarId returnCoreEnergyMinimumJoules journeyId isReturnLeg tokenless executable=false newer primary spherewright_prepare_supply_mecha_warpers spherewright_commit_supply_mecha_warpers mechaWarperSupplyReadback nativeReturnedSuccess readbackComplete spherewright_prepare_voyager_flight spherewright_commit_voyager_flight spherewright_prepare_cancel_voyager_flight spherewright_commit_cancel_voyager_flight nativeExitObserved'
function New-TestVoyagerSurface {
    New-TestSurface
    foreach ($name in @('spherewright_get_voyager_star_catalog','spherewright_preview_voyager_route')) {
        $properties = [ordered]@{sessionId=[pscustomobject]@{type='string'};planetId=[pscustomobject]@{type='integer'};stateHashVersion=[pscustomobject]@{type='integer'}}
        $required = @('sessionId','planetId')
        if ($name -ceq 'spherewright_get_voyager_star_catalog') {
            $properties['limit'] = [pscustomobject]@{type='integer'}
        } else {
            foreach ($field in @('destinationPlanetId','destinationStarId','maximumFlightGameTicks','returnReserveGameTicks','maximumWarpGameTicksPerLeg','extraWarperReserve')) { $properties[$field]=[pscustomobject]@{type='integer'} }
            foreach ($field in @('expectedPlayerStateHash','expectedCatalogStateHash')) { $properties[$field]=[pscustomobject]@{type='string'} }
            $properties['minimumCoreEnergyRatio'] = [pscustomobject]@{type='number'}
            $properties['maximumArrivalOffsetMeters'] = [pscustomobject]@{type='number'}
            $properties['journeyId'] = [pscustomobject]@{type=@('string','null')}
            $required += @('destinationPlanetId','expectedPlayerStateHash','expectedCatalogStateHash')
        }
        [pscustomobject]@{name=$name;description='READ-ONLY tokenless executable=false commitAllowedNow=false';
            annotations=[pscustomobject]@{readOnlyHint=$true;destructiveHint=$false;idempotentHint=$true;openWorldHint=$false};
            inputSchema=[pscustomobject]@{type='object';properties=[pscustomobject]$properties;required=$required}}
    }
    foreach ($suffix in @('supply_mecha_warpers','voyager_flight','cancel_voyager_flight')) {
        foreach ($verb in @('prepare','commit')) {
            $properties = [ordered]@{sessionId=[pscustomobject]@{type='string'};planetId=[pscustomobject]@{type='integer'}}
            $required = @('sessionId','planetId')
            if ($verb -ceq 'commit') {
                foreach ($field in @('planToken','idempotencyKey')) { $properties[$field]=[pscustomobject]@{type='string'} }
                $required += @('planToken','idempotencyKey')
            } else {
                $properties['stateHashVersion']=[pscustomobject]@{type='integer'}
                if ($suffix -ceq 'cancel_voyager_flight') { $properties['actionId']=[pscustomobject]@{type='string'}; $required += 'actionId' }
                else {
                    $properties['expectedPlayerStateHash']=[pscustomobject]@{type='string'}; $required += 'expectedPlayerStateHash'
                    if ($suffix -ceq 'supply_mecha_warpers') { $properties['count']=[pscustomobject]@{type='integer'} }
                    else {
                        $properties['expectedRouteStateHash']=[pscustomobject]@{type='string'}; $required += 'expectedRouteStateHash'
                        $properties['journeyId']=[pscustomobject]@{type=@('string','null')}
                    }
                }
            }
            [pscustomobject]@{name=('spherewright_'+$verb+'_'+$suffix);
                description='min(20 Filtered autoReplenishWarper original action Mecha.AutoReplenishWarper mechaWarperSupplyReadback nativeReturnedSuccess readbackComplete voyagerFlightReadback nativeExitObserved return reserves stopRequested original flight never repeat routeStateHash journeyId original active Voyager origin planetId';
                annotations=[pscustomobject]@{readOnlyHint=$false;destructiveHint=($verb -ceq 'commit');idempotentHint=($verb -ceq 'commit');openWorldHint=$false};
                inputSchema=[pscustomobject]@{type='object';properties=[pscustomobject]$properties;required=$required}}
        }
    }
}
function Test-VoyagerSurface([object[]]$Surface, [string]$Text=$voyagerPlaybook, [object[]]$ResourceList=$resources) {
    Assert-SpherewrightPackageSurface -Version '0.5.0' -Tools $Surface -Resources $ResourceList -PackagedPlaybook $Text -ResourcePlaybook $Text
}

$cases = 0
Test-Surface @(New-TestSurface); $cases++
Assert-SpherewrightPackageSurface -Version '0.3.3.0' -Tools @() -PackagedPlaybook 'legacy' -ResourcePlaybook 'legacy'; $cases++
Assert-SpherewrightPackageSurface -Version '0.3.3.0' -Tools @() -PackagedPlaybook "a`r`nb" -ResourcePlaybook "a`nb"; $cases++
Assert-Rejects { Assert-SpherewrightPackageSurface -Version '0.4.0' -Tools @(New-TestSurface) -PackagedPlaybook $playbook -ResourcePlaybook 'old' } 'differs'; $cases++
foreach ($name in ($readNames + $writeNames)) {
    $caseTools = @(New-TestSurface | Where-Object name -ne $name)
    Assert-Rejects { Test-Surface $caseTools } 'requires exactly one tool'; $cases++
}
$caseTools = @(New-TestSurface); $caseTools += $caseTools[0]
Assert-Rejects { Test-Surface $caseTools } 'requires exactly one tool'; $cases++
$caseTools = @(New-TestSurface); $caseTools[0].annotations.readOnlyHint = $false
Assert-Rejects { Test-Surface $caseTools } 'read-only metadata'; $cases++
$caseTools = @(New-TestSurface); ($caseTools | Where-Object name -eq 'spherewright_prepare_build').annotations.readOnlyHint = $true
Assert-Rejects { Test-Surface $caseTools } 'read-only metadata'; $cases++
$caseTools = @(New-TestSurface); $caseTools[0].PSObject.Properties.Remove('annotations')
Assert-Rejects { Test-Surface $caseTools } 'read-only metadata'; $cases++
$caseTools = @(New-TestSurface); ($caseTools | Where-Object name -eq 'spherewright_prepare_select_research').inputSchema.properties.prioritizeQueued.type = 'string'
Assert-Rejects { Test-Surface $caseTools } 'research-priority option'; $cases++
$caseTools = @(New-TestSurface); ($caseTools | Where-Object name -eq 'spherewright_prepare_select_research').inputSchema.properties.PSObject.Properties.Remove('prioritizeQueued')
Assert-Rejects { Test-Surface $caseTools } 'research-priority option'; $cases++
Assert-Rejects { Test-Surface @(New-TestSurface) 'old playbook' } 'lacks required'; $cases++
$caseTools = @(New-TestSurface)
($caseTools | Where-Object name -eq 'spherewright_get_governor_plan').inputSchema.properties.PSObject.Properties.Remove('validationBaselineProposalHash')
Assert-Rejects { Test-Surface $caseTools } 'Governor declaration option'; $cases++
$caseTools = @(New-TestSurface)
($caseTools | Where-Object name -eq 'spherewright_get_governor_plan').inputSchema.properties.validationBaselineProposalHash.type = @('string', 'null')
Test-Surface $caseTools; $cases++
$caseTools = @(New-TestSurface)
($caseTools | Where-Object name -eq 'spherewright_get_governor_plan').inputSchema.properties.validationBaselineProposalHash.type = 'number'
Assert-Rejects { Test-Surface $caseTools } 'Governor declaration option'; $cases++
foreach ($requirement in @(@('spherewright_get_foundry_plan','blueprint'), @('spherewright_prepare_blueprint_build','foundryIntent'), @('spherewright_prepare_blueprint_build','expectedFoundryPlanHash'))) {
    $caseTools = @(New-TestSurface)
    ($caseTools | Where-Object name -eq $requirement[0]).inputSchema.properties.PSObject.Properties.Remove($requirement[1])
    Assert-Rejects { Test-Surface $caseTools } 'Foundry finite-composition option'; $cases++
}
$caseTools = @(New-TestSurface)
($caseTools | Where-Object name -eq 'spherewright_prepare_dismantle').description = 'miners only'
Assert-Rejects { Test-Surface $caseTools } 'basic-sorter dismantle contract'; $cases++
$caseTools = @(New-TestSurface)
($caseTools | Where-Object name -eq 'spherewright_get_foundry_plan').description = 'old unconstrained graph'
Assert-Rejects { Test-Surface $caseTools } 'Foundry transport budget contract'; $cases++
Assert-Rejects { Test-Surface @(New-TestSurface) ($playbook.Replace('transportBudget', '')) } 'lacks required'; $cases++
foreach ($requirement in @(@('spherewright_inspect_factory_entity','geometry'), @('spherewright_prepare_build','beltRoutePreview'))) {
    $caseTools = @(New-TestSurface)
    $tool = @($caseTools | Where-Object name -eq $requirement[0])[0]
    $tool.inputSchema.properties.PSObject.Properties.Remove($requirement[1])
    Assert-Rejects { Test-Surface $caseTools } 'lacks the read-only geometry/route schema'; $cases++
    $caseTools = @(New-TestSurface)
    $tool = @($caseTools | Where-Object name -eq $requirement[0])[0]
    $tool.inputSchema | Add-Member required @($requirement[1])
    Assert-Rejects { Test-Surface $caseTools } 'parameter mandatory'; $cases++
    $caseTools = @(New-TestSurface)
    $tool = @($caseTools | Where-Object name -eq $requirement[0])[0]
    $tool.inputSchema.properties.($requirement[1]).description = 'ordinary construction'
    Assert-Rejects { Test-Surface $caseTools } 'explicit read-only'; $cases++
}
foreach ($field in @('source','destination')) {
    foreach ($member in @('existingObjectId','expectedEndpointStateHash','existingSlot','existingBeltQuarterTurns','plannedBeltQuarterTurns')) {
        $caseTools = @(New-TestSurface)
        $route = @($caseTools | Where-Object name -eq 'spherewright_prepare_build')[0].inputSchema.properties.beltRoutePreview
        $route.properties.$field.properties.PSObject.Properties.Remove($member)
        Assert-Rejects { Test-Surface $caseTools } 'lacks the read-only geometry/route schema'; $cases++
    }
}
foreach ($member in @('expectedFactoryGeometryHash','filterItemId','sourceSorterItemId','destinationSorterItemId','segments')) {
    $caseTools = @(New-TestSurface)
    $route = @($caseTools | Where-Object name -eq 'spherewright_prepare_build')[0].inputSchema.properties.beltRoutePreview
    $route.properties.PSObject.Properties.Remove($member)
    Assert-Rejects { Test-Surface $caseTools } 'lacks the read-only geometry/route schema'; $cases++
}
foreach ($member in @('routingMode','start','end','startAltitudeLevel','endAltitudeLevel')) {
    $caseTools = @(New-TestSurface)
    $route = @($caseTools | Where-Object name -eq 'spherewright_prepare_build')[0].inputSchema.properties.beltRoutePreview
    $route.properties.segments.items.properties.PSObject.Properties.Remove($member)
    Assert-Rejects { Test-Surface $caseTools } 'lacks the read-only geometry/route schema'; $cases++
}
foreach ($member in @('routingMode','start','end','startAltitudeLevel','endAltitudeLevel')) {
    $caseTools = @(New-TestSurface)
    $geometry = @($caseTools | Where-Object name -eq 'spherewright_inspect_factory_entity')[0].inputSchema.properties.geometry
    $geometry.properties.nativeBeltSegments.items.properties.PSObject.Properties.Remove($member)
    Assert-Rejects { Test-Surface $caseTools } 'lacks the read-only geometry/route schema'; $cases++
}
$caseTools = @(New-TestSurface)
@($caseTools | Where-Object name -eq 'spherewright_inspect_factory_entity')[0].inputSchema.properties.geometry.properties.PSObject.Properties.Remove('nativeBeltSegments')
Assert-Rejects { Test-Surface $caseTools } 'lacks the read-only geometry/route schema'; $cases++
$caseTools = @(New-TestSurface)
@($caseTools | Where-Object name -eq 'spherewright_inspect_factory_entity')[0].inputSchema.properties.geometry.properties.nativeBeltSegments.items.properties.end.properties.z.type = 'string'
Assert-Rejects { Test-Surface $caseTools } 'incompatible read-only geometry/route schema type'; $cases++
$caseTools = @(New-TestSurface)
@($caseTools | Where-Object name -eq 'spherewright_inspect_factory_entity')[0].inputSchema.properties.geometry.properties.objectIds.items.type = 'number'
Assert-Rejects { Test-Surface $caseTools } 'incompatible read-only geometry/route schema type'; $cases++
$caseTools = @(New-TestSurface)
@($caseTools | Where-Object name -eq 'spherewright_inspect_factory_entity')[0].inputSchema.properties.geometry.properties.terrainPoints.items.properties.z.type = 'string'
Assert-Rejects { Test-Surface $caseTools } 'incompatible read-only geometry/route schema type'; $cases++
$caseTools = @(New-TestSurface)
@($caseTools | Where-Object name -eq 'spherewright_prepare_build')[0].inputSchema.properties.beltRoutePreview.properties.segments.items.properties.end.properties.PSObject.Properties.Remove('z')
Assert-Rejects { Test-Surface $caseTools } 'lacks the read-only geometry/route schema'; $cases++
$caseTools = @(New-TestSurface)
@($caseTools | Where-Object name -eq 'spherewright_prepare_build')[0].inputSchema.properties.beltRoutePreview.type = @('object','object')
Assert-Rejects { Test-Surface $caseTools } 'incompatible read-only geometry/route schema type'; $cases++
$caseTools = @(New-TestSurface)
@($caseTools | Where-Object name -eq 'spherewright_prepare_build')[0].inputSchema.properties.beltRoutePreview.PSObject.Properties.Remove('type')
Assert-Rejects { Test-Surface $caseTools } 'incompatible read-only geometry/route schema type'; $cases++
$caseTools = @(New-TestSurface)
@($caseTools | Where-Object name -eq 'spherewright_prepare_build')[0].inputSchema.properties.beltRoutePreview.properties.source.properties.existingObjectId.type = @('integer','null')
Assert-Rejects { Test-Surface $caseTools } 'incompatible read-only geometry/route schema type'; $cases++
foreach ($rule in @('tokenless','executable=false')) {
    $caseTools = @(New-TestSurface)
    $route = @($caseTools | Where-Object name -eq 'spherewright_prepare_build')[0].inputSchema.properties.beltRoutePreview
    $route.description = $route.description.Replace($rule,'')
    Assert-Rejects { Test-Surface $caseTools } 'non-executable whole-route'; $cases++
}
foreach ($rule in @('inspect_factory_entity.geometry','beltRoutePreview','native_runtime_belt_collider_world_bounds','ALL-belts-plus-two-sorters','tokenless')) {
    Assert-Rejects { Test-Surface @(New-TestSurface) ($playbook.Replace($rule,'')) } 'lacks required'; $cases++
}
$caseTools = @(New-TestSurface | Where-Object name -ne $otherNames[0])
Assert-Rejects { Test-Surface $caseTools } 'exactly 64 distinct tools'; $cases++
$caseTools = @(New-TestSurface); $caseTools += [pscustomobject]@{name='unexpected_extra_tool'}
Assert-Rejects { Test-Surface $caseTools } 'exactly 64 distinct tools'; $cases++
$caseTools = @(New-TestSurface)
($caseTools | Where-Object name -eq $otherNames[0]).name = $otherNames[1]
Assert-Rejects { Test-Surface $caseTools } 'exactly 64 distinct tools'; $cases++
foreach ($invalidName in @('',42)) {
    $caseTools = @(New-TestSurface)
    ($caseTools | Where-Object name -eq $otherNames[0]).name = $invalidName
    Assert-Rejects { Test-Surface $caseTools } 'exactly 64 distinct tools'; $cases++
}
foreach ($invalidResources in @(
    @(), @($resources[0],$resources[0]), @([pscustomobject]@{uri='unexpected';mimeType='text/markdown'}),
    @([pscustomobject]@{uri=$resources[0].uri;mimeType='text/plain'}), @([pscustomobject]@{mimeType='text/markdown'})
)) {
    Assert-Rejects {
        Assert-SpherewrightPackageSurface -Version '0.4.0' -Tools @(New-TestSurface) -Resources $invalidResources -PackagedPlaybook $playbook -ResourcePlaybook $playbook
    } 'exactly one Markdown Agent playbook resource'; $cases++
}
Test-VoyagerSurface @(New-TestVoyagerSurface); $cases++
$caseTools = @(New-TestVoyagerSurface)
$caseTools += [pscustomobject]@{name='spherewright_future_read_only_tool'}
Test-VoyagerSurface $caseTools; $cases++ # Future capabilities remain permitted; no tool-count ceiling.
Assert-Rejects { Test-VoyagerSurface @(New-TestSurface) } 'Voyager package requires exactly one tool'; $cases++
foreach ($name in @('spherewright_get_voyager_star_catalog','spherewright_preview_voyager_route')) {
    Assert-Rejects { Test-VoyagerSurface @(New-TestVoyagerSurface | Where-Object name -cne $name) } 'Voyager package requires exactly one tool'; $cases++
    foreach ($field in @('readOnlyHint','destructiveHint','idempotentHint','openWorldHint')) {
        $caseTools = @(New-TestVoyagerSurface)
        $tool = @($caseTools | Where-Object name -ceq $name)[0]
        $tool.annotations.$field = -not $tool.annotations.$field
        Assert-Rejects { Test-VoyagerSurface $caseTools } 'Voyager package has missing/incorrect read-only metadata'; $cases++
    }
    foreach ($rule in @('READ-ONLY','tokenless','executable=false','commitAllowedNow=false')) {
        $caseTools = @(New-TestVoyagerSurface)
        $tool = @($caseTools | Where-Object name -ceq $name)[0]
        $tool.description = $tool.description.Replace($rule,'')
        Assert-Rejects { Test-VoyagerSurface $caseTools } 'non-executable read contract'; $cases++
    }
    $caseTools = @(New-TestVoyagerSurface)
    $tool = @($caseTools | Where-Object name -ceq $name)[0]
    $tool.inputSchema.required = @('sessionId')
    Assert-Rejects { Test-VoyagerSurface $caseTools } 'makes a bound read parameter optional'; $cases++
    foreach ($field in @('sessionId','planetId','stateHashVersion')) {
        $caseTools = @(New-TestVoyagerSurface)
        $tool = @($caseTools | Where-Object name -ceq $name)[0]
        $tool.inputSchema.properties.PSObject.Properties.Remove($field)
        Assert-Rejects { Test-VoyagerSurface $caseTools } 'read-only Voyager schema'; $cases++
    }
}
foreach ($field in @('destinationPlanetId','destinationStarId','expectedPlayerStateHash','expectedCatalogStateHash','maximumFlightGameTicks','returnReserveGameTicks','maximumWarpGameTicksPerLeg','maximumArrivalOffsetMeters','minimumCoreEnergyRatio','extraWarperReserve','journeyId')) {
    $caseTools = @(New-TestVoyagerSurface)
    $tool = @($caseTools | Where-Object name -ceq 'spherewright_preview_voyager_route')[0]
    $tool.inputSchema.properties.PSObject.Properties.Remove($field)
    Assert-Rejects { Test-VoyagerSurface $caseTools } 'read-only Voyager schema'; $cases++
}
foreach ($field in @('maximumFlightGameTicks','returnReserveGameTicks','maximumWarpGameTicksPerLeg','maximumArrivalOffsetMeters','minimumCoreEnergyRatio')) {
    $caseTools = @(New-TestVoyagerSurface)
    $tool = @($caseTools | Where-Object name -ceq 'spherewright_preview_voyager_route')[0]
    $tool.inputSchema.properties.$field.type = 'string'
    Assert-Rejects { Test-VoyagerSurface $caseTools } 'incompatible read-only Voyager schema type'; $cases++
}
$caseTools = @(New-TestVoyagerSurface)
($caseTools | Where-Object name -ceq 'spherewright_get_voyager_star_catalog').inputSchema.properties.PSObject.Properties.Remove('limit')
Assert-Rejects { Test-VoyagerSurface $caseTools } 'read-only Voyager schema'; $cases++
$caseTools = @(New-TestVoyagerSurface); $caseTools += $caseTools[-1]
Assert-Rejects { Test-VoyagerSurface $caseTools } 'nonempty distinct tool names'; $cases++
foreach ($rule in @('spherewright_get_voyager_star_catalog','spherewright_preview_voyager_route','maximumFlightGameTicks','returnReserveGameTicks','newer primary')) {
    Assert-Rejects { Test-VoyagerSurface @(New-TestVoyagerSurface) ($voyagerPlaybook.Replace($rule,'')) } 'Voyager playbook lacks'; $cases++
}
Assert-Rejects { Test-VoyagerSurface @(New-TestVoyagerSurface) $voyagerPlaybook @() } 'stable Markdown Agent playbook'; $cases++
foreach ($suffix in @('supply_mecha_warpers','voyager_flight','cancel_voyager_flight')) {
    foreach ($verb in @('prepare','commit')) {
        $name='spherewright_'+$verb+'_'+$suffix
        Assert-Rejects { Test-VoyagerSurface @(New-TestVoyagerSurface | Where-Object name -cne $name) } 'requires exactly one action tool'; $cases++
        foreach ($field in @('readOnlyHint','destructiveHint','idempotentHint','openWorldHint')) {
            $caseTools=@(New-TestVoyagerSurface);$tool=@($caseTools|Where-Object name -ceq $name)[0];$tool.annotations.$field=-not$tool.annotations.$field
            Assert-Rejects { Test-VoyagerSurface $caseTools } 'action has missing/incorrect metadata'; $cases++
        }
        $caseTools=@(New-TestVoyagerSurface);$tool=@($caseTools|Where-Object name -ceq $name)[0];$tool.inputSchema.required=@('sessionId')
        Assert-Rejects { Test-VoyagerSurface $caseTools } 'makes a bound parameter optional'; $cases++
        $caseTools=@(New-TestVoyagerSurface);$tool=@($caseTools|Where-Object name -ceq $name)[0];$tool.description='omitted native proof'
        Assert-Rejects { Test-VoyagerSurface $caseTools } 'lacks its native/original-action contract'; $cases++
        foreach ($field in @('autoReplenishWarper','itemId','userConfirmedInConversation','recoveryMode')) {
            $caseTools=@(New-TestVoyagerSurface);$tool=@($caseTools|Where-Object name -ceq $name)[0]
            $tool.inputSchema.properties|Add-Member -NotePropertyName $field -NotePropertyValue ([pscustomobject]@{type='boolean'})
            Assert-Rejects { Test-VoyagerSurface $caseTools } 'unsupported native override'; $cases++
        }
        $fields=if($verb -ceq 'commit'){@('sessionId','planetId','planToken','idempotencyKey')}
            elseif($suffix -ceq 'supply_mecha_warpers'){@('sessionId','planetId','stateHashVersion','expectedPlayerStateHash','count')}
            elseif($suffix -ceq 'voyager_flight'){@('sessionId','planetId','stateHashVersion','expectedPlayerStateHash','expectedRouteStateHash','journeyId')}
            else{@('sessionId','planetId','stateHashVersion','actionId')}
        foreach($field in $fields){
            $caseTools=@(New-TestVoyagerSurface);$tool=@($caseTools|Where-Object name -ceq $name)[0];$tool.inputSchema.properties.PSObject.Properties.Remove($field)
            Assert-Rejects { Test-VoyagerSurface $caseTools } 'Voyager native action schema';$cases++
        }
    }
}
foreach($rule in @('spherewright_prepare_supply_mecha_warpers','mechaWarperSupplyReadback','nativeReturnedSuccess','readbackComplete','spherewright_prepare_voyager_flight','spherewright_commit_voyager_flight','spherewright_prepare_cancel_voyager_flight','spherewright_commit_cancel_voyager_flight','returnCoreEnergyMinimumJoules','maximumWarpGameTicksPerLeg')){
    Assert-Rejects { Test-VoyagerSurface @(New-TestVoyagerSurface) ($voyagerPlaybook.Replace($rule,'')) } 'Voyager playbook lacks';$cases++
}
[pscustomobject]@{passed=$cases;failed=0;scope='offline 0.4/0.5 package surface policy only; no game or ZIP validation'} | ConvertTo-Json
