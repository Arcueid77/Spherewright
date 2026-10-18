Set-StrictMode -Version Latest

# Protocol-only checks. No Bridge request or gameplay action is sent by this policy.
function Assert-SpherewrightPackageSurface {
    param(
        [Parameter(Mandatory)][version]$Version,
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Tools,
        [AllowEmptyCollection()][object[]]$Resources = @(),
        [Parameter(Mandatory)][string]$PackagedPlaybook,
        [Parameter(Mandatory)][string]$ResourcePlaybook
    )

    $normalize = { param([string]$value) $value.Replace("`r`n", "`n").TrimStart([char]0xFEFF) }
    if (-not [string]::Equals((& $normalize $PackagedPlaybook), (& $normalize $ResourcePlaybook), [StringComparison]::Ordinal)) {
        throw 'The packaged playbook differs from the MCP embedded resource; rebuild the same source cohort.'
    }
    if ($Version -lt [version]'0.4.0') { return }

    $readOnlyTools = @(
        'spherewright_get_overseer_production',
        'spherewright_get_overseer_summary',
        'spherewright_get_overseer_diagnostic_bundle',
        'spherewright_get_foundry_plan',
        'spherewright_inspect_blueprint',
        'spherewright_export_blueprint',
        'spherewright_get_blueprint_builds',
        'spherewright_get_governor_plan',
        'spherewright_inspect_factory_entity'
    )
    $writeTools = @(
        'spherewright_prepare_upgrade', 'spherewright_commit_upgrade',
        'spherewright_prepare_blueprint_build', 'spherewright_commit_blueprint_build',
        'spherewright_prepare_cancel_blueprint', 'spherewright_commit_cancel_blueprint',
        'spherewright_prepare_select_research', 'spherewright_prepare_dismantle', 'spherewright_commit_dismantle',
        'spherewright_prepare_build'
    )
    foreach ($name in ($readOnlyTools + $writeTools)) {
        $matchingTools = @($Tools | Where-Object { $_.name -ceq $name })
        if ($matchingTools.Count -ne 1) { throw "The merged 0.4 package requires exactly one tool named $name." }
        $annotations = $matchingTools[0].PSObject.Properties['annotations']
        $readOnly = if ($annotations) { $annotations.Value.PSObject.Properties['readOnlyHint'] } else { $null }
        if (-not $readOnly -or $readOnly.Value -isnot [bool] -or $readOnly.Value -ne ($readOnlyTools -ccontains $name)) {
            throw "The package has missing/incorrect read-only metadata for $name."
        }
    }
    $research = @($Tools | Where-Object { $_.name -ceq 'spherewright_prepare_select_research' })[0]
    $schema = $research.PSObject.Properties['inputSchema']
    $properties = if ($schema) { $schema.Value.PSObject.Properties['properties'] } else { $null }
    $priority = if ($properties) { $properties.Value.PSObject.Properties['prioritizeQueued'] } else { $null }
    if (-not $priority -or $priority.Value.type -cne 'boolean') {
        throw 'The package lacks the explicit native research-priority option.'
    }
    $governor = @($Tools | Where-Object { $_.name -ceq 'spherewright_get_governor_plan' })[0]
    $governorSchema = $governor.PSObject.Properties['inputSchema']
    $governorProperties = if ($governorSchema) { $governorSchema.Value.PSObject.Properties['properties'] } else { $null }
    $declaration = if ($governorProperties) { $governorProperties.Value.PSObject.Properties['validationBaselineProposalHash'] } else { $null }
    $declarationType = if ($declaration) { $declaration.Value.PSObject.Properties['type'] } else { $null }
    $declarationTypes = @()
    if ($declarationType) { $declarationTypes = @($declarationType.Value) }
    $invalidDeclarationTypes = @($declarationTypes | Where-Object { $_ -cne 'string' -and $_ -cne 'null' }).Count -gt 0
    $duplicateDeclarationTypes = @($declarationTypes | Select-Object -Unique).Count -ne $declarationTypes.Count
    if (-not $declaration -or $declarationTypes.Count -lt 1 -or $declarationTypes.Count -gt 2 -or $declarationTypes -cnotcontains 'string' -or $invalidDeclarationTypes -or $duplicateDeclarationTypes) {
        throw 'The package lacks the immutable pre-execution Governor declaration option.'
    }
    foreach ($requirement in @(
        @('spherewright_get_foundry_plan', 'blueprint'),
        @('spherewright_prepare_blueprint_build', 'foundryIntent'),
        @('spherewright_prepare_blueprint_build', 'expectedFoundryPlanHash')
    )) {
        $tool = @($Tools | Where-Object { $_.name -ceq $requirement[0] })[0]
        $inputSchema = $tool.PSObject.Properties['inputSchema']
        $inputProperties = if ($inputSchema) { $inputSchema.Value.PSObject.Properties['properties'] } else { $null }
        if (-not $inputProperties -or -not $inputProperties.Value.PSObject.Properties[$requirement[1]]) {
            throw "The package lacks the Foundry finite-composition option: $($requirement[1])."
        }
    }
    # The tool names did not change when geometry/route qualification was added.
    # A same-count older MCP must not qualify as the current package surface.
    function Get-RequiredPackageSchemaNode($Schema, [string]$Path, [string]$ExpectedType, [string]$Context='read-only geometry/route') {
        $node = $Schema
        foreach ($part in $Path.Split('.')) {
            $container = if ($part -ceq 'items') { $node } else {
                $property = $node.PSObject.Properties['properties']
                if ($property) { $property.Value } else { $null }
            }
            $member = if ($null -ne $container) { $container.PSObject.Properties[$part] } else { $null }
            if (-not $member -or $null -eq $member.Value) {
                throw "The package lacks the $Context schema: $Path."
            }
            $node = $member.Value
        }
        $type = $node.PSObject.Properties['type']
        $types = @()
        if ($type) { $types = @($type.Value) }
        $invalidTypes = @($types | Where-Object {
            $_ -isnot [string] -or ($_ -cne $ExpectedType -and
                ($_ -cne 'null' -or $ExpectedType -cnotin @('object', 'array', 'string')))
        }).Count -gt 0
        if ($types.Count -lt 1 -or $types.Count -gt 2 -or $types -cnotcontains $ExpectedType -or
            $invalidTypes -or @($types | Select-Object -Unique).Count -ne $types.Count) {
            throw "The package has an incompatible $Context schema type: $Path."
        }
        return $node
    }
    foreach ($requirement in @(
        @('spherewright_inspect_factory_entity', 'geometry'),
        @('spherewright_prepare_build', 'beltRoutePreview')
    )) {
        $tool = @($Tools | Where-Object { $_.name -ceq $requirement[0] })[0]
        $schemaProperty = $tool.PSObject.Properties['inputSchema']
        if (-not $schemaProperty) { throw 'The package lacks the read-only geometry/route input schema.' }
        $schema = $schemaProperty.Value
        $field = $requirement[1]
        $node = Get-RequiredPackageSchemaNode $schema $field 'object'
        $required = $schema.PSObject.Properties['required']
        if ($required -and @($required.Value) -ccontains $field) {
            throw "The package makes the optional read-only geometry/route parameter mandatory: $field."
        }
        $description = $node.PSObject.Properties['description']
        if (-not $description -or ([string]$description.Value).IndexOf('READ-ONLY', [StringComparison]::Ordinal) -lt 0) {
            throw "The package lacks the explicit read-only geometry/route contract: $field."
        }
        $shape = if ($field -ceq 'geometry') {
            @(@('objectIds','array'), @('objectIds.items','integer'),
                @('terrainPoints','array'), @('terrainPoints.items','object'),
                @('terrainPoints.items.x','number'), @('terrainPoints.items.y','number'), @('terrainPoints.items.z','number'),
                @('expectedFactoryGeometryHash','string'), @('nativeBeltSegments','array'), @('nativeBeltSegments.items','object'),
                @('nativeBeltSegments.items.routingMode','string'), @('nativeBeltSegments.items.start','object'), @('nativeBeltSegments.items.end','object'),
                @('nativeBeltSegments.items.startAltitudeLevel','integer'), @('nativeBeltSegments.items.endAltitudeLevel','integer'))
        } else {
            @(@('expectedFactoryGeometryHash','string'), @('filterItemId','integer'),
                @('sourceSorterItemId','integer'), @('destinationSorterItemId','integer'),
                @('source','object'), @('destination','object'), @('segments','array'), @('segments.items','object'),
                @('segments.items.routingMode','string'), @('segments.items.start','object'), @('segments.items.end','object'),
                @('segments.items.startAltitudeLevel','integer'), @('segments.items.endAltitudeLevel','integer'))
        }
        foreach ($entry in $shape) {
            $null = Get-RequiredPackageSchemaNode $schema "$field.$($entry[0])" $entry[1]
        }
        if ($field -ceq 'geometry') {
            foreach ($endpoint in @('start','end')) {
                foreach ($coordinate in @('x','y','z')) {
                    $null = Get-RequiredPackageSchemaNode $schema "$field.nativeBeltSegments.items.$endpoint.$coordinate" 'number'
                }
            }
        }
        if ($field -ceq 'beltRoutePreview') {
            foreach ($endpoint in @('source','destination')) {
                foreach ($entry in @(@('existingObjectId','integer'), @('expectedEndpointStateHash','string'),
                        @('existingSlot','integer'), @('existingBeltQuarterTurns','integer'), @('plannedBeltQuarterTurns','integer'))) {
                    $null = Get-RequiredPackageSchemaNode $schema "$field.$endpoint.$($entry[0])" $entry[1]
                }
            }
            foreach ($endpoint in @('start','end')) {
                foreach ($coordinate in @('x','y','z')) {
                    $null = Get-RequiredPackageSchemaNode $schema "$field.segments.items.$endpoint.$coordinate" 'number'
                }
            }
            foreach ($rule in @('tokenless','executable=false')) {
                if (([string]$description.Value).IndexOf($rule, [StringComparison]::Ordinal) -lt 0) {
                    throw "The package lacks the non-executable whole-route contract: $rule."
                }
            }
        }
    }
    $dismantle = @($Tools | Where-Object { $_.name -ceq 'spherewright_prepare_dismantle' })[0]
    $dismantleDescription = $dismantle.PSObject.Properties['description']
    if (-not $dismantleDescription -or ([string]$dismantleDescription.Value).IndexOf('2011/2012', [StringComparison]::Ordinal) -lt 0) {
        throw 'The package lacks the guarded basic-sorter dismantle contract.'
    }
    $foundry = @($Tools | Where-Object { $_.name -ceq 'spherewright_get_foundry_plan' })[0]
    $foundryDescription = $foundry.PSObject.Properties['description']
    if (-not $foundryDescription -or ([string]$foundryDescription.Value).IndexOf('transportBudget', [StringComparison]::Ordinal) -lt 0) {
        throw 'The package lacks the native Foundry transport budget contract.'
    }
    foreach ($rule in @('maximumObjectsToSubmit', 'resumeBuildId', 'never replay the whole blueprint',
            'spherewright_get_governor_plan', 'nonzero measured baseline', 'ten game minutes', 'researchQueueReadback',
            'validationBaselineProposalHash', 'inputEndpointFacingDot/outputEndpointFacingDot',
            'foundryIntent', 'expectedFoundryPlanHash', 'boundaryPorts', '2011/2012', 'transportBudget',
            'inspect_factory_entity.geometry', 'beltRoutePreview', 'native_runtime_belt_collider_world_bounds',
            'ALL-belts-plus-two-sorters', 'tokenless', 'nativeBeltSegments', 'nativeConditionsChecked=false')) {
        if ($ResourcePlaybook.IndexOf($rule, [StringComparison]::Ordinal) -lt 0) {
            throw "The merged 0.4 playbook lacks required finite-construction/Governor rule: $rule."
        }
    }
    if ($Version -ge [version]'0.5.0') {
        $toolNames = @($Tools | ForEach-Object { $_.name })
        if (@($toolNames | Where-Object { $_ -isnot [string] -or [string]::IsNullOrWhiteSpace($_) }).Count -gt 0 -or
            @($toolNames | Sort-Object -Unique).Count -ne $Tools.Count) {
            throw 'The Voyager package requires nonempty distinct tool names.'
        }
        foreach ($name in @('spherewright_get_voyager_star_catalog', 'spherewright_preview_voyager_route')) {
            $matchingTools = @($Tools | Where-Object { $_.name -ceq $name })
            if ($matchingTools.Count -ne 1) { throw "The Voyager package requires exactly one tool named $name." }
            $tool = $matchingTools[0]
            $annotations = $tool.PSObject.Properties['annotations']
            foreach ($annotation in @(@('readOnlyHint',$true), @('destructiveHint',$false), @('idempotentHint',$true), @('openWorldHint',$false))) {
                $member = if ($annotations) { $annotations.Value.PSObject.Properties[$annotation[0]] } else { $null }
                if (-not $member -or $member.Value -isnot [bool] -or $member.Value -ne $annotation[1]) {
                    throw "The Voyager package has missing/incorrect read-only metadata for $name`: $($annotation[0])."
                }
            }
            $description = $tool.PSObject.Properties['description']
            foreach ($rule in @('READ-ONLY', 'tokenless', 'executable=false', 'commitAllowedNow=false')) {
                if (-not $description -or ([string]$description.Value).IndexOf($rule,[StringComparison]::Ordinal) -lt 0) {
                    throw "The Voyager package lacks the non-executable read contract for $name`: $rule."
                }
            }
            $schemaProperty = $tool.PSObject.Properties['inputSchema']
            if (-not $schemaProperty) { throw "The Voyager package lacks the read-only Voyager input schema for $name." }
            $schema = $schemaProperty.Value
            $shape = @(@('sessionId','string'), @('planetId','integer'), @('stateHashVersion','integer'))
            $requiredFields = @('sessionId','planetId')
            if ($name -ceq 'spherewright_get_voyager_star_catalog') {
                $shape += ,@('limit','integer')
            } else {
                $shape += @(@('destinationPlanetId','integer'), @('expectedPlayerStateHash','string'), @('expectedCatalogStateHash','string'),
                    @('destinationStarId','integer'), @('maximumFlightGameTicks','integer'), @('returnReserveGameTicks','integer'),
                    @('maximumWarpGameTicksPerLeg','integer'), @('maximumArrivalOffsetMeters','number'),
                    @('minimumCoreEnergyRatio','number'), @('extraWarperReserve','integer'), @('journeyId','string'))
                $requiredFields += @('destinationPlanetId','expectedPlayerStateHash','expectedCatalogStateHash')
            }
            foreach ($entry in $shape) {
                $null = Get-RequiredPackageSchemaNode $schema $entry[0] $entry[1] 'read-only Voyager'
            }
            $required = $schema.PSObject.Properties['required']
            foreach ($field in $requiredFields) {
                if (-not $required -or @($required.Value) -cnotcontains $field) {
                    throw "The Voyager package makes a bound read parameter optional: $name.$field."
                }
            }
        }
        foreach ($suffix in @('supply_mecha_warpers','voyager_flight','cancel_voyager_flight')) {
            foreach ($verb in @('prepare','commit')) {
                $name = 'spherewright_' + $verb + '_' + $suffix
                $actionTools = @($Tools | Where-Object { $_.name -ceq $name })
                if ($actionTools.Count -ne 1) { throw "The Voyager package requires exactly one action tool named $name." }
                $tool = $actionTools[0]
                $annotations = $tool.PSObject.Properties['annotations']
                foreach ($annotation in @(@('readOnlyHint',$false), @('destructiveHint',($verb -ceq 'commit')),
                        @('idempotentHint',($verb -ceq 'commit')), @('openWorldHint',$false))) {
                    $member = if ($annotations) { $annotations.Value.PSObject.Properties[$annotation[0]] } else { $null }
                    if (-not $member -or $member.Value -isnot [bool] -or $member.Value -ne $annotation[1]) {
                        throw "The Voyager action has missing/incorrect metadata for $name`: $($annotation[0])."
                    }
                }
                $schemaProperty = $tool.PSObject.Properties['inputSchema']
                if (-not $schemaProperty) { throw "The Voyager package lacks the native action schema for $name." }
                $schema = $schemaProperty.Value
                $shape = @(@('sessionId','string'), @('planetId','integer'))
                $requiredFields = @('sessionId','planetId')
                if ($verb -ceq 'commit') {
                    $shape += @(@('planToken','string'), @('idempotencyKey','string'))
                    $requiredFields += @('planToken','idempotencyKey')
                } else {
                    $shape += ,@('stateHashVersion','integer')
                    if ($suffix -ceq 'cancel_voyager_flight') {
                        $shape += ,@('actionId','string'); $requiredFields += 'actionId'
                    } else {
                        $shape += ,@('expectedPlayerStateHash','string'); $requiredFields += 'expectedPlayerStateHash'
                        if ($suffix -ceq 'supply_mecha_warpers') { $shape += ,@('count','integer') }
                        else {
                            $shape += @(@('expectedRouteStateHash','string'), @('journeyId','string'))
                            $requiredFields += 'expectedRouteStateHash'
                        }
                    }
                }
                foreach ($entry in $shape) { $null = Get-RequiredPackageSchemaNode $schema $entry[0] $entry[1] 'Voyager native action' }
                $required = $schema.PSObject.Properties['required']
                foreach ($field in $requiredFields) {
                    if (-not $required -or @($required.Value) -cnotcontains $field) {
                        throw "The Voyager action makes a bound parameter optional: $name.$field."
                    }
                }
                foreach ($field in @('autoReplenishWarper','itemId','userConfirmedInConversation','recoveryMode')) {
                    if ($schema.properties.PSObject.Properties[$field]) {
                        throw "The Voyager action exposes an unsupported native override: $name.$field."
                    }
                }
                $description = $tool.PSObject.Properties['description']
                $rules = if ($verb -ceq 'commit') {
                    if ($suffix -ceq 'supply_mecha_warpers') { @('Mecha.AutoReplenishWarper','mechaWarperSupplyReadback','nativeReturnedSuccess','readbackComplete','original action') }
                    elseif ($suffix -ceq 'voyager_flight') { @('voyagerFlightReadback','nativeExitObserved','original action','return reserves') }
                    else { @('stopRequested','original flight','never repeat') }
                } elseif ($suffix -ceq 'supply_mecha_warpers') { @('min(20','Filtered','autoReplenishWarper','original action') }
                elseif ($suffix -ceq 'voyager_flight') { @('routeStateHash','journeyId','return reserves') }
                else { @('original active Voyager','origin planetId') }
                foreach ($rule in $rules) {
                    if (-not $description -or ([string]$description.Value).IndexOf($rule,[StringComparison]::Ordinal) -lt 0) {
                        throw "The Voyager action lacks its native/original-action contract for $name`: $rule."
                    }
                }
            }
        }
        $playbookResources = @($Resources | Where-Object { $_ -and $_.PSObject.Properties['uri'] -and $_.uri -ceq 'spherewright://agent/playbooks/opening-movement-v1' })
        if ($playbookResources.Count -ne 1 -or -not $playbookResources[0].PSObject.Properties['mimeType'] -or $playbookResources[0].mimeType -cne 'text/markdown') {
            throw 'The Voyager package requires the stable Markdown Agent playbook resource exactly once.'
        }
        foreach ($rule in @('spherewright_get_voyager_star_catalog', 'spherewright_preview_voyager_route',
                'maximumFlightGameTicks', 'returnReserveGameTicks', 'maximumWarpGameTicksPerLeg', 'maximumArrivalOffsetMeters',
                'destinationStarId', 'returnCoreEnergyMinimumJoules', 'journeyId', 'isReturnLeg', 'tokenless', 'executable=false', 'newer primary',
                'spherewright_prepare_supply_mecha_warpers', 'spherewright_commit_supply_mecha_warpers',
                'mechaWarperSupplyReadback', 'nativeReturnedSuccess', 'readbackComplete',
                'spherewright_prepare_voyager_flight', 'spherewright_commit_voyager_flight',
                'spherewright_prepare_cancel_voyager_flight', 'spherewright_commit_cancel_voyager_flight', 'nativeExitObserved')) {
            if ($ResourcePlaybook.IndexOf($rule,[StringComparison]::Ordinal) -lt 0) {
                throw "The Voyager playbook lacks the visibility/budget/recovery rule: $rule."
            }
        }
    }
    if ($Version.Major -eq 0 -and $Version.Minor -eq 4) {
        $toolNames = @($Tools | ForEach-Object { $_.name })
        if ($Tools.Count -ne 64 -or @($toolNames | Where-Object { $_ -isnot [string] -or [string]::IsNullOrWhiteSpace($_) }).Count -gt 0 -or
            @($toolNames | Sort-Object -Unique).Count -ne 64) {
            throw 'The current 0.4 package requires exactly 64 distinct tools.'
        }
        $resourceUri = if ($Resources.Count -eq 1 -and $null -ne $Resources[0]) { $Resources[0].PSObject.Properties['uri'] } else { $null }
        $resourceMime = if ($Resources.Count -eq 1 -and $null -ne $Resources[0]) { $Resources[0].PSObject.Properties['mimeType'] } else { $null }
        if (-not $resourceUri -or $resourceUri.Value -cne 'spherewright://agent/playbooks/opening-movement-v1' -or
            -not $resourceMime -or $resourceMime.Value -cne 'text/markdown') {
            throw 'The current 0.4 package requires exactly one Markdown Agent playbook resource.'
        }
    }
}
