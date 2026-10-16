# Foundry and Governor DSP API research

[English](game-api-foundry.md) | [简体中文](game-api-foundry.zh-CN.md)

## Scope and provenance

This is maintained product research for bounded Foundry construction and Governor analysis. The [complete immutable original](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/research/game-api-foundry.md) preserves native signatures, geometry counterexamples, dated offline/live boundaries and retired stage notes. Most initial research used DSP `0.10.34.28529` / native SHA-256 `AE0BA95F75BD879A62AA4CE253B2AB78EAA4FB3C7C595F5E1FEE75EBE0E0EF85`; the elevated-source binding correction used `0.10.35.29131` / `74AC6162CEF8394A6021C8475132CAE4DF4F840871A99E63CC7D2C8297661BC0`. These historical baselines are not current universal compatibility claims.

The accepted local candidate is source `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`, DSP `0.10.35.29145`. [Final validation](../validation/v0.4.0.md) retains the bounded module/Governor proofs, separate three-stage mid-plan and whole-kit recovery, and sustained final supply evidence. Old “not installed/live pending” text stays historical; this rewrite performs no installation, native action or new acceptance.

## Deterministic material compilation

`FoundryPlanCompiler` reuses `RuntimeDependencyGraphBuilder` and current runtime recipe/building catalogs. Explicit recipe/building choices win; deterministic fallback prefers single-output recipes, then recipe ID, building grade and ID. Raw items stop recursion unless explicitly recipe-selected; other external intermediates must be named. Reject cycles/unusable selections. Aggregate shared demand before sizing upstream machines and list coproduct sinks rather than treating them as free input.

Research established `PrefabDesc.assemblerSpeed/labAssembleSpeed`, `workEnergyPerTick` and `RecipeProto.TimeSpend/Items/ItemCounts/Results/ResultCounts`. At 60 game ticks/s, unproliferated full-power capacity is:

```text
items/game minute = 3600 × prefabSpeedRaw × outputCount / (TimeSpend × 10000)
working watts = workEnergyPerTick × 60
```

Unknown speed is not guessed as 1×. Machine recipe types must match. The compiler does not simulate mining, fractionation, proliferator bonuses or actual delivery. Limits are 64 stages, 16 recipe levels and 256 machines with checked finite budgets. Its material hash describes reproducible calculation inputs, not current state, ownership or write authority.

## Read-only data, site and infrastructure checks

`BoundedBlueprintReader` accepts explicit code/current-owned selections, treating text metadata as untrusted data. Limits include 256 KiB code, 128 KiB compressed data, 1 MiB expanded payload, 64 objects and eight areas. The separate `BlueprintSitePolicy` placement limit is 32 objects. Blueprint inspect/export/site DTOs remain `executable=false`; reading them is not construction.

`GameStateReader.Foundry` and `NormalGameActionCoordinator.FoundryPreview` re-use native click/path/inserter/blueprint placement logic without issuing a player build command. `FoundrySitePlanner.CheckPlannedClearance` compares new planned machines with each other; it is not a universal sphere exclusion around old buildings. Existing-object oriented-collider rejection and DSP `CheckBuildConditions` are separate stages. `nativeCheckPerformed=false`, `NotEnoughItem` or a material budget never proves a fully passed site.

`FoundryPowerPlanner` uses copied native node/network/consumer evidence, actual joins and full base-load budgets; unknown coverage and unfinished existing prebuild loads refuse assessment. Native placement and current-power hashes remain distinct. `FoundryTransportPlanner` allocates directed item-compatible flow with belt/sorter capacities and shared boundaries. A theoretical budget is not fair splitting, stable upstream supply or measured throughput.

## Native geometry and ordinary configuration

`BuildTool_Click/Path/Inserter`, `BlueprintUtils.RefreshBuildPreview` and `PlanetFactory.ReadObjectConn` are the native construction/readback basis. Research retains these distinctions:

- Device distance, latitude-dependent `PlanetGrid.CalcSegmentsAcross`, facing, tilt, collider, reach, materials and native placement are independent requirements. A reconstructed span is an offline inference, never an instrumented native approval.
- Inserter attachment considers explicit endpoints/segments with bounded candidates and native offsets. It does not scan neighboring belts or clear `TooSkew`/`ErrorInserterData`. Native collider geometry must not be replaced with an arbitrary expansion margin.
- Source-cover/destination reuse and elevated spans are separate explicit subsets. Source-cover binds the exact old source, unchanged empty/open/unfed cargo path, a free destination, non-removing zero-cost cover and normally charged NEW points. Creation/completion prove actual reciprocal links, ordered membership and preservation of old geometry/edges; there is no arbitrary trimming, routing or destination reuse.
- The historical elevated creation mismatch compared a wrapped source/empty-path binding with an unwrapped base binding. The correction compares complete bindings at the same level before create while retaining original base-binding post-create proof. This explains a supported pre-create failure path, not an observed exception callsite. Quarantined original actions never replay as a “fix.”
- Ordinary storage `bans` prevents normal insertion, not withdrawal. `BuildingParameters.FromFactoryObject` exports settings/filters, not cargo; validate capacity, native modes and IDs before native construction applies them. Sorter filter changes preserve already-held cargo and exact endpoint configuration.

## Finite construction, persistence and upgrades

`BlueprintBuildState`, `BlueprintBuildStore` and `NormalGameActionCoordinator.BlueprintBuild` reuse normal plan/session/revision/identity and idempotency/quarantine stores. Submit bounded dependency-ready work through native paths, pay ordinary items and wait for drones. Project belt outputs only to eligible belt links, sorter endpoints to both ends, and machine creation without sorter-edge overwrite. Every receipt proves new identity, costs and reciprocal/free-end topology.

Protected write-ahead/result evidence binds the exact owned identity and immutable intent/geometry. Partial failure retains successful work; cancellation stops unsubmitted work. Restart restores no token: inspect actual entities/material receipts/topology and obtain fresh prepare. Unknown results stay quarantined. `BlueprintProgressVersionPolicy` retains only proven completed 29131 history on 29145 after the exact continuous Journal transition; it does not resume partial/unknown work or erase the store.

`PrepareUpgradeOnMainThread`/`ExecuteUpgradeOnMainThread` use native `DoUpgradeObject`, not prototype assignment. Supported execution is ordinary manufacturing assemblers and basic `2011 → 2012` sorters. Require a higher-tier item/refund space, native eligibility, exact configuration/cargo/links and post-call conservation; assemblers reset process progress and basic sorters preserve rescaled cycle fraction. Belt path parsing/upgrade proofs are research, not an enabled belt/advanced-sorter action.

## Governor measurement and limits

`GovernorPlanCompiler` compares upgrade, new-build and explicit module-copy alternatives. `GovernorSourceBinding` hashes static identities/recipes/settings/endpoints/network rather than mutable cargo; inventory deltas remain separate. Target demand, actual production/consumption, allocatable local surplus and deficit are distinct quantities.

`GovernorMeasurementSeries` requires fresh independent stable nonzero baseline windows. `GovernorThroughputValidation` locks a prospective target/tolerance/window before execution, counts covered game-time unions and resets on invalid conditions, source changes or gaps. At most eight passive session observers do not form an autonomous planner or durable construction authority; theoretical gain is not measured gain. The approved twofold/ten-game-minute gate cannot be satisfied by module count or relaxed afterward.

The final candidate's accepted limited results do not enable arbitrary blueprint content, all native placement modes, all upgrade families, delivery-distributor actions or actual interstellar travel. Full original method evidence remains linked above; precise scene IDs, ticks and receipts belong in Lab rather than this reusable research.
