# Bridge protocol

[English](protocol.md) | [简体中文](protocol.zh-CN.md)

Protocol version remains `1`; the `0.5.0` Voyager development interface targets DSP `0.10.35.29151` and retains the `0.4` protocol surface. This describes maintained source, not an installed-cohort or Voyager live-acceptance claim. Formal `v0.4.0` is tagged at `b7c0002dbe2e8f5608c5ca140b0c6326a1818021`; older production acceptance remains fixed at `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`. See [product status](project-status.md), [native validation](validation/native-29151-preflight.md), [safety](safety-model.md) and the [Agent playbook](agent-playbook.md).

## Framing and authentication

Each Pipe message is a four-byte little-endian signed length followed by UTF-8 JSON. Negative lengths and payloads over 1 MiB reject before allocation. Zero-length frames reach and fail JSON validation.

Every connection begins with authenticated `handshake` carrying the active `bridgeInstanceId` and startup token from the current-user-protected runtime descriptor. Invalid credentials or pre-authentication requests close it. Pipe/descriptor credentials are protected transport details, never public evidence.

Requests contain `protocolVersion`, `messageType=request`, UUID `requestId`, optional `sessionId`, `method` and `payload`. Responses echo request identity, with `success` and `result` or structured `error`. Transport success does not mean a native check passed, a commit was accepted or an action finished. Retain explicit error code, retryability and failure phase.

## Current public MCP surface

```text
spherewright_get_status
spherewright_get_session_state
spherewright_get_player_state
spherewright_get_progression_state
spherewright_get_gameplay_journal
spherewright_get_local_star_system
spherewright_get_voyager_star_catalog
spherewright_preview_voyager_route
spherewright_get_recipe_catalog
spherewright_get_build_catalog
spherewright_get_foundry_plan
spherewright_get_governor_plan
spherewright_inspect_blueprint
spherewright_export_blueprint
spherewright_get_blueprint_builds
spherewright_prepare_blueprint_build
spherewright_commit_blueprint_build
spherewright_prepare_cancel_blueprint
spherewright_commit_cancel_blueprint
spherewright_get_power_summary
spherewright_get_overseer_diagnostic_bundle
spherewright_get_overseer_production
spherewright_get_overseer_summary
spherewright_get_action_result
spherewright_list_resource_nodes
spherewright_inspect_resource_node
spherewright_list_factory_entities
spherewright_inspect_factory_entity
spherewright_list_assemblers
spherewright_inspect_assembler
spherewright_prepare_new_game
spherewright_commit_new_game
spherewright_prepare_save_import
spherewright_commit_save_import
spherewright_prepare_move
spherewright_commit_move
spherewright_prepare_interplanetary_flight
spherewright_commit_interplanetary_flight
spherewright_prepare_voyager_flight
spherewright_commit_voyager_flight
spherewright_prepare_cancel_voyager_flight
spherewright_commit_cancel_voyager_flight
spherewright_prepare_harvest
spherewright_commit_harvest
spherewright_prepare_handcraft
spherewright_commit_handcraft
spherewright_prepare_select_research
spherewright_commit_select_research
spherewright_prepare_build
spherewright_commit_build
spherewright_prepare_dismantle
spherewright_commit_dismantle
spherewright_prepare_upgrade
spherewright_commit_upgrade
spherewright_prepare_configure_building
spherewright_commit_configure_building
spherewright_prepare_transfer
spherewright_commit_transfer
spherewright_prepare_logistics_station_fleet_transfer
spherewright_commit_logistics_station_fleet_transfer
spherewright_prepare_refuel
spherewright_commit_refuel
spherewright_prepare_supply_mecha_warpers
spherewright_commit_supply_mecha_warpers
spherewright_prepare_save
spherewright_commit_save
spherewright_prepare_quarantine_reconciliation
spherewright_commit_quarantine_reconciliation
spherewright_prepare_resume_owned_game
spherewright_commit_resume_owned_game
spherewright_prepare_reload_flight_checkpoint
spherewright_commit_reload_flight_checkpoint
```

The text resource `spherewright://agent/playbooks/opening-movement-v1` is available without a live Bridge and must be read before gameplay. Its logical embedded name remains `Spherewright.Mcp.agent-playbook.md`. Discovery grants no execution authority. Historical sandbox basic-line methods are not registered.

## Request rules

Owned save/player/factory reads require active `sessionId`; planet-bound requests also require `planetId`. Ordinary ground actions remain local-planet-only; flight and flight-stop requests retain their documented origin binding during space transit. Entity inspection additionally permits an explicitly selected, already-created, identity-verified simulation-resident factory on the current owned star; it never creates/loads one. Player reads still use current `localPlanetId`.

Inspection uses `objectId`, positive for a built entity and negative for a prebuild. Zero/missing and `Int32.MinValue` reject; another tool's `entityId` or `storageEntityId` is not interchangeable. Incomplete/expired pagination cannot prove absence.

Mutable prepare binds current session/revision/`GameData`, action, identity, parameters, budget and required hash domain. Commit uses a short-lived `planToken`, UUID `idempotencyKey` and documented session/planet binding. Menu creation/resume have separate schemas.

| Evidence | Scope |
| --- | --- |
| Player action `stateHash` | Fresh normal-action player evidence; optional Move drift has separate bounds |
| Factory `stateHash` | Complete mutable state; storage-capacity binds this |
| `endpointStateHash` | Identity/pose/static configuration/connections for build, upgrade and export; ordinary cargo/progress may advance |
| Root `configurationStateHash` | Sorter-filter configuration/topology/held cargo; not universal static topology |
| Nested station configuration hash | Documented station modes only |
| `selectionStateHash` | Research queue/current tech/unlocks/prerequisites, excluding naturally uploaded progress |
| Material/assessment/geometry/proposal hashes | Their stated read-only evidence, never interchangeable state hashes or tokens |

`STALE_SESSION`, `SESSION_NOT_OWNED` and `STALE_STATE` distinguish stale session, ownership and state. Wrong request/hash domains may be `INVALID_REQUEST`. Missing mode evidence or combat mode blocks gameplay commits; sandbox/resource settings remain explicit without expanding capabilities.

Lists use bounded immutable pages and opaque session/planet/filter/page-size cursors. Retain capture ticks and finish one snapshot; independent calls are not a transaction. Never publish capability fields, raw requests, descriptors or private paths.

## Voyager visibility and route previews

The native supply methods `spherewright_prepare_supply_mecha_warpers` / `spherewright_commit_supply_mecha_warpers` map to `prepare_supply_mecha_warpers` / `commit_supply_mecha_warpers`. Prepare uses `PrepareSupplyMechaWarpersRequest`: current owned `sessionId` / `planetId`, fresh complete-player `expectedPlayerStateHash`, `count` (default20) and `stateHashVersion=1`. Count must equal `min(20, fresh package count of item1210)>0`. The owned player must be alive, grounded and idle; the native chamber must be one empty `Filtered` slot, filter1210 / stack20, zero count/inc and no banned grids. Preparation binds exact player/package/chamber identities, count/inc and the current automatic setting without moving stock.

Supply commit uses existing `CommitNormalActionRequest` and the shared coordinator, with exact `planToken` and UUID `idempotencyKey`; there is no public generic `commit_normal_action`. It calls `Mecha.AutoReplenishWarper` once, preserves `autoReplenishWarper`, and never calls `UseWarper`. Poll the original action for `mechaWarperSupplyReadback`: bilateral count/inc, automatic setting, nullable after fields / `actualStored` / `nativeReturnedSuccess`, `readbackComplete` and `conservationVerified`. This is the synchronous native-call boundary before later ordinary consumption. Missing proof stays unknown; accepted, timed-out or outcome-unknown calls must not be repeated under another key.

The `0.5` read-only tools `spherewright_get_voyager_star_catalog` and `spherewright_preview_voyager_route` map to Bridge methods `get_voyager_star_catalog` and `preview_voyager_route`. Both require the current owned session and origin planet. They observe native exploration visibility and calculate a finite route preview. For permitted views, the catalog may request `StarData.RunScanningThread` / `PlanetData.RunScanThread` through the normal star-map path; this can populate legitimate visible scan caches, while pending scans stay unknown. It never invokes arbitrary generators or creates a remote factory. These reads do not launch, enter warp, consume fuel/warpers, save, create a checkpoint, load a save or create a gameplay action. The preview is tokenless and `executable=false`. A positive budget is not a flight commit or live acceptance.

Catalog input is `sessionId`, `planetId`, optional `limit` (1–32 stars, default 16) and `stateHashVersion` (1). Route input additionally requires `destinationPlanetId`, `expectedPlayerStateHash` and `expectedCatalogStateHash`. Defaults are `maximumFlightGameTicks=36000`, `returnReserveGameTicks=18000`, `minimumCoreEnergyRatio=0.8` and `extraWarperReserve=1`. The maximum is the whole round trip (7200–108000 game ticks); the return window is reserved within it and must leave at least 3600 ticks for each leg. The finite core ratio is 0.8–1.0; extra warpers are 0–18, above the two native round-trip warpers. Energy fields use joules, power fields watts, and distances explicitly provide metres and light years. These are conservative budget bounds, not measured flight duration or consumption.

A legitimately visible star marker may use `destinationPlanetId=0` with explicit `destinationStarId` while its planets/resources remain unknown. `maximumWarpGameTicksPerLeg` defaults to1800, is at least180 and cannot exceed either leg's reserved window. Finite `maximumArrivalOffsetMeters` is100000–3000000, default3000000. Star-only distance budgets include this offset; a selected visible planet uses its actual known distance. `budgetBasis=finite_native_warp_duration_and_explicit_return_recharge_v2` budgets finite warp duration separately from total flight windows. `requiresReturnCoreRecharge=true` requires a fresh return-departure core read at or above `returnCoreEnergyMinimumJoules`, while preserving loaded-fuel/warper reserves. A single-leg buffer does not prove future charging or landing.

An optional nonempty preview `journeyId` selects the homebound leg of that exact protected journey and returns `isReturnLeg=true`. Its destination must point home. Validate the remaining single leg against the original reserve, including one native entry plus `extraWarperReserve`; do not require a second new home-to-remote round trip. Omitting `journeyId` requests the outbound round-trip budget. Return preparation also names the same original `journeyId`.

Both results keep `prepared=false`, `executable=false` and `commitAllowedNow=false`, with no `planToken`; MCP rejects a response that advertises execution authority. `resourceVisibility=unknown`, null `resources` or null `planets` must not become observed empty/zero. `resourceSummaryComplete` and `scanReady` delimit the evidence, and explicit observed zero remains zero. `budgetComputed=false` means the numeric budget is unavailable, not a zero-cost route. `budgetBasis` names the conservative calculation; `budgetSufficient` is separate from execution permission. Retain the individual energy/technology/visibility/warper checks and structured `blockers`.

Observed star/planet summaries declare `resourceSummaryScope=remaining_veins_and_native_gas_oceans`: remaining veins plus legitimately visible native gas/ocean sources. A zero vein row does not prove that its item is absent from the whole system. Check every required planet's complete, visible summary and all native source kinds; unknown, incomplete or truncated coverage of that system cannot establish system-wide absence. Truncating unrelated galaxy entries does not invalidate a completely represented system. `expectedPlayerStateHash` binds the complete player snapshot `stateHash`, not the narrower player action hash domain.

Preserve unknown resource visibility and budget fields. A hidden or unobserved resource is not absent; theme-based potential is not a surveyed vein. A departure-system-missing-resource claim requires legitimate complete exploration evidence. Use fresh catalog/player bindings and current native technology, units, distance, core/fuel, warper consumption and outbound/return reserves. Report a blocker or unknown instead of treating unavailable evidence as zero consumption or spare capacity. Do not infer that a destination is safe to land from a route preview.

Existing flight/checkpoint safety still applies: reconcile the original action after cancellation, timeout or disconnect; a result-unknown action must not be repeated under another key. A flight checkpoint belongs to its exact eligible interrupted flight, retires after successful arrival or covering primary progress, and cannot roll back progress covered by a newer primary save. These reads never grant recovery authority or perform a cross-star commit. Bounded execution uses the separate native methods below with fresh prepare/commit/readback gates.

Flight-checkpoint ticket v2 records `CheckpointFileFingerprint` from the existing protected `OwnedSaveRecoveryLease`, binding the entire file plus its exact embedded owned identity, native version, peaceful mode and saved tick. Prepare and commit freshly revalidate this evidence; matching tick/mtime alone is insufficient. Commit keeps a read-only lease through asynchronous native adoption, with a five-minute upper bound and release on success, failure, cancellation or disposal. A legacy v1 flight ticket without this strong evidence is rejected; it is never promoted into new recovery authority. This does not change protected-primary confirmation/migration gates or claim a native failed-flight acceptance.

Before native dispatch, reload registers the original action/key and durably records `ReloadPending` / `ReloadActionId`. An exception after entering native loading retains that original accepted action as `outcome_unknown`, with its source lease/expectation; reconcile only that action, never rekey. Durable pending state blocks fresh reloads and ordinary writes. Success requires exact checkpoint adoption in a new session distinct from the dispatch source; old checkpoint flags are insufficient. Reconciliation marks the ticket `recovered` and preserves the original completion result; another reload requires a newly failed bound flight attempt.

## Bounded Voyager execution and stop

Development methods `prepare_voyager_flight` / `commit_voyager_flight` reuse the existing interplanetary-flight coordinator/checkpoint. Prepare binds `PrepareVoyagerFlightRequest` (`planetId`, `expectedRouteStateHash`, fresh complete-player `expectedPlayerStateHash`, `stateHashVersion=1`, optional `journeyId`) to one inspected current route and native reserve checks. Empty `journeyId` starts a bounded outbound journey; return must name that same protected journey. Commit uses `CommitNormalActionRequest` with Voyager mode explicitly required. Ordinary interplanetary commit explicitly requires ordinary mode and keeps its same-star guard; neither route consumes the other's plans.

The native control hook must have observed a tick within two seconds at prepare and commit. Poll the original flight at intervals shorter than its 30-second control lease; only a bound original-action read renews that lease. Lease expiry or an interrupted native tick requests a sticky stop. Native warp exit and ordinary braking must settle within the original 1,800-tick / 30-second cleanup bound; pausing does not extend its wall-clock deadline, and an unproved stop remains unknown. A same-process checkpoint recovery charges elapsed ticks through the captured pre-load source tick. If a real process exit leaves that source tick unknown, exact recovery conservatively forfeits all remaining original journey time, marks the accounting as conservative and cannot authorize another return leg. The original deadline and material floors remain unchanged.

Poll the original action's `voyagerFlightReadback`: `journeyId`, `phase`, origin/destination, nullable selected/actual planet and actual star, native warp state/command, `entryAttempted`, `nativeExitObserved`, native/package/consumed warpers, joule reserves, `flightDeadlineGameTick` and `journeyDeadlineUtc`. The root action `planetId` remains its origin, including without a local planet. A visible-star arrival envelope does not prove landing; one successful leg is not complete round-trip/resource/logistics/failure acceptance. Preserve persistent return reserves and the absolute journey deadline across actions/sessions; Host interruption does not reset them.

`prepare_cancel_voyager_flight` uses `PrepareCancelVoyagerFlightRequest` with the original flight's origin `planetId`, exact `actionId` and `stateHashVersion=1`; `commit_cancel_voyager_flight` uses the same shared commit contract. It latches `stopRequested` on that original flight without creating another movement writer. Poll the original flight through native warp exit, braking and its actual terminal outcome. Stop acknowledgement alone proves neither settled flight nor recovery completion. Unknown results retain the original key/action; checkpoint reload is separately protected and cannot roll back a newer primary.

## Voyager local work and durable structured construction

While the original journey is `remote_landed`, its local exception binds the exact owned save, Native version, session, world/player/controller/mecha and actual loaded local factory. Fresh station configuration, package/storage or fleet transfer, refuel and Save retain their native scope. Move/Harvest and the separate structured-build path additionally reserve finite work and stopping time before the original return window, the loaded return-energy floor and native `1+extraWarperReserve`. None resets the journey deadline or grants remote writes from home. Return-flight core recharge is a fresh departure gate, not a requirement to keep the core full during every local action.

Move/Harvest support only the proved ordinary RTS Walk/Mine subset: no queued order, manual inertia/input, pending jump, navigation, fast travel, warp, Fly or Drift. The Native pre-player hook rechecks exact loaded-planet/default-physics references and a whole-ball layer4-water absence certificate each tick; an Ice name, `!inWater` or one point sample is insufficient. The stop margin covers13 paid ticks including simultaneous mining and RTS deceleration; aborting an order is not zero velocity. Core and loaded energy must cover that margin without spending the return floor. The original local UTC/tick window and30-second original-action lease cannot be renewed into return time; terminal requires actual Native RTS/manual velocity settled. Unknown scene/mode/tail evidence stays unknown.

This first local-control subset excludes independent Native combat debits: shield capacity0 or an already-disabled recharge setting, inactive laser, empty old ammo chamber, valid empty selection with whole ammo storage item/count/inc/filter0 and actual ROF/cost0, and ground/space `fleetCount=0`. That is **zero configured fleet slots**, not zero active vessels. Empty default ammo slot1 is allowed; no weapon, recharge or fleet setting is changed to qualify. Replicator/research queues must be idle; unused native research-item buffers need not be empty.

Existing `prepare_build`/`commit_build` can bind one Native-validated batch of1..8 same-item objects within the original journey:2301,2104,2201,2203,2205,2001 or2011. Native miner-node, pose, technology, collision and connection checks remain. The first subset rejects elevated paths and belt-cover reuse, and requires the bounded16m Native target/ejection envelope and fixed anchor. Generic blueprint syntax gains no miner layout format. Native creation happens once for the original action; durable write-ahead, exact prebuild-to-entity/component/topology evidence and one measured whole-batch material debit are retained. Per-object receipts share that debit; never sum it once per object.

Read `constructionControl` in the original action/build: `executionKind=structured_native`, phase, dispatch/physical-idle/binding/anchor/reserve evidence, observation lease, finite tail ticks/energy and stop reason. Initial work is at most3600 ticks/60seconds, with the original absolute window retained across fresh resume; original-action polling renews only its30-second observation lease while working. Cancel, stale lease, user takeover/pause or lost binding stop new dispatch and retain the first cleanup clocks. Disabling Native drones is not physical idle: the exact factory must finish a real Native construction pass after disable, the original cached prelaunch must be clear, and pool/module readback must prove all owned drones idle before a safe terminal.

Only exact pending objects may continue through the existing `prepare_blueprint_build` with `resumeBuildId` and fresh progress/player hashes. Never create a replacement batch or repeat Native creation; expired original work cannot be refreshed. Unknown outcomes reconcile only the original action/build, without enabling drones or inventing drained/reserve proof. Known completion may restore an originally enabled drone setting only while the exact owned hold remains and the user has not paused or taken control. Host exit records pending/interrupted evidence, not fictional idle or permission to reload.

<a id="detail-only-belt-cargo-observation-04-development-slice"></a>
## Factory, cargo, material and fuel observations

Matrix recipes use labs; `list_assemblers` is not an all-producer query. Detail-only `sorterEndpoints` reports at most 16 native slots or four belt virtual orientations (`slot=-1`, occupancy unknown). Completed-link/timing proof differs from prospective placement geometry.

`beltCargo` is detail-only, separate from `buffers` and existing action hashes. `state=observed` reports unique stacks touching the selected segment at its tick. Adjacent segments can share stacks; do not sum them as path inventory. The reader validates at most 512 cells plus nine guards per side and returns no partial totals on failure. An empty segment proves neither an empty whole path nor zero flow.

Optional `rearPickup` observes one aligned packet at an open path's rear. `no_aligned_packet` is not empty; `not_applicable` means closed/non-tail. Item-sorted totals are not queue order. A station cannot skip an unneeded aligned input packet; input `storageIndex` records its last received slot, not a fixed filter.

`materialInventoryObjectIds` selects ≤256 positive built objects in one same-tick factory cut: belt, storage, tank, inserter, assembler, miner, station and ordinary thermal/fusion generator with fuel evidence. Bounds: 64 paths/32768 total cells/4096 path members; each path ≤8192 cells/512 belts. Each path appears once, including outside-selection members. Station orders/fleet cargo, loaded heat and electrical buffers are not item stock. Labs, power nodes and other generators are unsupported. Missing/unavailable is unknown, never zero.

`tankFluidCount` is native total fluid items including explicit zero. Null is unknown; empty `buffers` is not zero. Its positive fluid buffer identifies the same stock, not extra inventory.

`fuelPowerState` separates buffered items/inc from loaded fuel energy; their IDs may differ and zero buffer can coexist with residual heat. Electrical generation is J/t, never fuel stock; `isWorking=false` is not authoritative stopped-generator evidence. These optional reads leave action hashes unchanged and invoke no mutating energy methods.

Recipe metadata adds heat/type and nullable `acceptedAsMechaFuel`, requiring prototype plus native fuel-table evidence. `fuelPowerProfile` covers only valid ordinary `2204/2211` base ratings, including generation/fuel-use J/t and fuel mask. These are not live generation, fuel stock or sustainable supply. Missing is unknown; unbuilt/unfuelled generators receive no construction-power credit.

## Research and Journal

`autoManageResearchItems` and `mechaResearchItemBuffer` describe native 3600-point equivalents separately from package stacks. Normal research management may reserve matrices or return whole items; never round up remainder points.

`completionItemRewards` is complete ordered prototype metadata (≤32 entries), not delivery proof. Null means unknown; `[]` means observed no rewards. Preserve duplicates and reconcile actual unlock tick, package/inc, buffers and capacity. Rewards are not crafting/production milestones.

Research prepare uses `expectedSelectionStateHash`. Default enqueue checks native prerequisites. `prioritizeQueued=true` only reorders an eligible non-head queued technology through `SortTechQueue`, preserving other slots/progress/inventory and returning immediate `researchQueueReadback`. Terminal means reordered, not researched.

The owned Journal reports first manual/line outputs independently, technology/upgrade selections, tracking boundary, durable sequence and persistence state without raw save identity/path. `attached_existing_save` preserves incomplete historical coverage. Origin, effective version and transitions remain separate.

## Overseer

Production reads accept 1–64 unique runtime items and page size 1–16, capture already-created owned factories and use automatic native level-0 `total[0]/total[7]` rolling 600-tick counters, excluding player/mecha lifetime output. Theory comes from bounded native components, not UI caches.

Findings retain direct-producer coverage, totals/truncation and bounded physical upstream traversal. Short zero-output diagnosis generally needs a full cycle. A stopped, powered `output_blocked` exception uses next-batch admission thresholds, not physical storage capacity: smelting `101-q` (`q≤100`), assembly `9*q+1`, others `19*q+1`. Unknown rate/power/capacity never qualifies; an absent finding is not sustainable-supply proof.

Summary preserves generation/capacity/demand/export distinctions and bounded power/logistics/research scans: 512 factories, 4096 networks, 65536 generator references, 4096 station slots, 64 storage slots/station, 12000 technologies. It creates/loads no remote factory; over-bound scans fail, not silently truncate.

The diagnostic bundle captures production and summary in one main-thread work item, joins exact factory/planet/flags/tick, and uses schema `1`/`public_allowlist_v1` with no credential, save identity or path. Dedicated immutable pages last 60 seconds.

`inspect_factory_entity.ownedRemoteLogisticsStation=true` opts into one already-known positive station entity in an exact existing owned simulation-resident factory, including another star. It never creates/loads a factory or grants a remote write scope. Remote geometry/material selections and negative/prebuild IDs reject. Default false preserves the existing read scope. Prepare/commit still require the actual current local planet.

Detail-only `logisticsStation.activeVessels` reports at most32 copied Native `workShipDatas` records with `coverage=native_active_work_ship_data`, `complete`, `truncated`, nullable native working count and explicit `observed`/`partial`/`unavailable`/`not_applicable` state. Missing/unavailable is unknown; complete observed zero is distinct. `shipIndex` is the carrier's real field, not its compressed active-array index. `ownerGId/shipIndex/gene` distinguish dispatch observations only within a finite window, can be reused/wrap, and are not durable trip identities across saves. `direction` means away from/back to owner, not supply/demand; `t` is stage-local, not total progress or ETA. The allowlist contains cargo/inc, stage/direction/t/speed/warp/warper scalars, no vessel coordinates or private `StateToken`. Motion, a changed fingerprint, cargo0 or one arrival does not prove Native delivery, complete cycles or sustained mined supply; correlate exact endpoints, bilateral stock changes, source mining and real consumption over several complete cycles. These observations do not enter action/configuration/fleet hashes.

<a id="stocked-logistics-causal-boundary-04-development-slice"></a>
`stocked_logistics_boundary` means inventory exists in `configured_route_supply_total` scope, not unreserved displayed-source stock or proven dispatch. Physical item paths, needs/orders and carrier progress remain necessary. Suspected logistics blockage needs 600 continuous stagnant ticks; changes, rollback, topology drift or gaps >3600 ticks invalidate that baseline. Temporal persistence must succeed before public findings.

<a id="foundry-material-draft-and-machine-site-preview"></a>
## Foundry material and site plans

Foundry accepts target/rate, optional external supplies and explicit recipe/building choices. It aggregates demand into dependency stages, machine costs, working power and byproducts at full power without proliferation. Limits: 64 stages/16 levels/256 machines. `material_plan` remains `executable=false`; machine cost excludes logistics/power infrastructure and deterministic `planHash` is not a state hash/token.

One optional explicit grid has ≤32 machines, columns 1–8, spacing 4–32 m, origin within 10 m of surface and placements within 64 tangent metres. Native snapping, conservative pairwise collider clearance (0.5 m), native placement and whole package budget all apply. A first-condition failure is not an exhaustive defect list. Assessment hashes bind plan/session/revision/player/poses/native conditions/budget, not capture tick alone. A clear grid proves no complete logistics, ongoing supply or throughput; power assesses returned snapped positions.

Explicit `blueprint`/free `boundaryPorts` instead binds exact stage counts, directed item flows, ALL explicit costs and dependencies. Composition supports single-output `2302–2305` stages, default unbanned `2101` and unmixed channels. Unsupported coproduct/storage/mixed layouts reject/block. Reads remain non-executable.

<a id="foundry-native-transport-budget"></a>
### Foundry native transport budget

`transportBudget` uses rated full-power single-item no-backpressure belt/basic `2011/2012` timing/span capacity for every internal/boundary channel. Missing ratings or stacking `2013` block composition without narrowing ordinary blueprint data. Allocated flow can be below demand: inspect `internalFlowsRouted`, `canPrepare` and blockers. `transportCapacityVerified=false`; ratings prove neither measured delivery nor fair splitting.

Separate site power captures ≤32 planned objects, 512 nodes, 256 networks, 8192 consumers and 2048 unassigned consumers. It budgets existing max(work,idle,current), chargers, exports, newly covered consumers and planned loads; only new wind gets generation credit. Ambiguous/boundary geometry or unfinished existing prebuilds block availability. Unknown is not zero demand; current full-base-load sufficiency is not sustained fuel or a token.

<a id="governor-proposal-04-development-slice"></a>
## Governor proposal

`governor_proposal` is `executable=false`/`balanced=false`. Select 1–32 exact local objects with endpoint/recipe evidence, target rate, `0<tolerance≤0.5` and 36000–216000 validation ticks. Native rates are planet counters; attribution requires complete relevant direct producers.

`governor-source-v2` binds static identity/pose/topology/recipe/acceleration/filter/settings/network, excluding live cargo/progress/power ratio. At most eight series keep three independent nonzero windows. Period is 600/default or 3600; longer readings use validated six-tick-aligned native level-1 rings, excluding the partial bucket. Changed source/session/item scope/period, rollback or gaps invalidate history. `governor-measurement-binding-v3` excludes planning-only target rate: unchanged-source repricing preserves history, but the NEW proposal must be checked/locked before writes.

Demand, actual production/consumption, local surplus, shortfall and selected-buffer deltas remain separate. Starved consumption is a lower bound; production minus consumption is not inventory delta. Alternatives label whole-device, machine-only, selected-module or all-explicit-object cost scope. Nameplate gain is not actual gain.

<a id="governor-explicit-parallel-construction-comparison"></a>
### Governor explicit parallel-construction comparison

`parallelExpansionBlueprint` uses the same explicit Foundry layout at target minus a ready stable nonzero baseline. It binds same-capture session/planet/tick/revision, code/site/player, intent, construction hash and power. `bounded_construction_ready_at_capture` means fresh-prepare eligible, never executable/balanced. External source/sink infrastructure is excluded; identical intent/hash uses the existing finite executor.

<a id="governor-pre-execution-throughput-declaration"></a>
### Governor pre-execution throughput declaration

`validationBaselineProposalHash` locks a server-captured ready healthy attributable proposal before any accepted write/source change, at the same revision within 3600 ticks. Baseline/target/tolerance/duration/period/recipe scale become immutable. ≤8 locks per owned identity fit a protected 64 KiB archive; publication needs flushed atomic persistence and `declarationDurable=true`. No edited archive/client history is accepted.

A covering healthy planned restart restores only the same owned/version/Journal-bound declaration when the consumed-ticket floor covers the lock. Quarantine/checkpoint/pre-lock recovery cannot create eligibility. Samples always restart at zero.

`throughputValidation` counts contiguous native coverage once: overlap/duplicates/wall time add no credit. A one-tick gap, zero/out-of-tolerance/unavailable output, failed health/attribution or rollback resets progress. ≥36000 ticks proves the requested ten-minute window; `doubleThroughputTargetObserved` also requires exactly 2× target and tolerance ≤10%. `balanced` stays false; conservation, sustained supply, transport and save/resume are separate gates.

<a id="blueprint-data-and-native-upgrades-04-development-slices"></a>
## Blueprint data, finite construction and upgrades

Inspection takes explicit code; export takes ≤64 positive selected objects within 64 m with fresh endpoint/recipe evidence. No file scan/cargo copy. Native signature and byte-exact import/export roundtrip must pass. Bounds: 256 KiB code, 128 KiB compressed, 1 MiB decompressed, 64 objects, 8 areas, 256-grid dimensions, 128 parameters/object and supported formats only.

Data subset: `2302–2305` machines, unlabeled `2001–2003` belts, supported `2011–2013` sorters, supported unstacked `2101`, `2201` and `2203`. Unknown types/settings/content, terrain reform or malformed references reject the whole input. `untrusted*` metadata is data, never instructions.

Inspection remains `blueprint_inspection`/`executable=false`. Optional site: ≤32 objects, 64 m radius, closed internal sorter ends, acyclic belt dependencies; full inventory/unlocks/range/terrain/tropic/occupancy/native capacity/conditions. No implicit external matching, covers, upgrades or demolition.

Finite prepare binds exact code/site/assessment or `resumeBuildId`/fresh progress, never replacement code/site, plus fresh player hash and `maximumObjectsToSubmit=1..32`. `foundryIntent`/`expectedFoundryPlanHash` additionally revalidate the complete graph/cost/transport/power. Commit returns action/build IDs; at most one dependency-ready prebuild per tick uses normal costs/drones.

Per-object states: `not_submitted`, `submitting`, `pending_construction`, `completed`, `blocked`, `outcome_unknown`. Successful batch pause is not whole completion. Protected progress (32 plans/4 MiB) flushes write-ahead before creation and exact cost/identity/topology evidence afterward. Summary reads provide no fresh hash; exact-build reads reconcile/persist new completion before exposure. Restart restores receipts, not tokens, and continues only unsubmitted work. Cancel stops future submissions and retains existing work. Unknown quarantines writes.

Upgrade supports higher manufacturing-assembler grades or `2011→2012` through `DoUpgradeObject`, with both actual sorter links, span/pose and fresh config/player binding. It consumes a complete new device/refunds old and proves immediate cargo/inc/topology/timing. Assemblers reset native processing; basic sorters rescale cycle fraction. `upgradeReadback` is immediate evidence, not throughput acceptance.

<a id="new-belt-centre-occupancy-containment-04-development-slice"></a>
<a id="native-belt-routing-mode-04-development-slice"></a>
## Native belts and endpoints

Retain full native generated points, occupancy and endpoint bindings. NEW points cannot silently cover existing objects. `belt_path_existing_overlap` is refusal, not permission to omit IDs. Declared covers are separately proved non-removing zero-cost anchors retained in native checks/creation.

Free ground routes support `native_grid` or explicit `native_geodesic`. Geodesic allows free `2001–2003` endpoints or its exact `2001` dual-cover subset, not auto-routing. Require requested routing/full-path echoes; a path does not prove both sorter attachments.

Ground `2001` open-path joins bind exact endpoint identities/hashes, distinct complete empty open paths and destination free inputs. Source `whole_path_native_rotation_v1`/destination `empty_open_path_native_geometry_v1`, ≥2 NEW and exact cost must match. The sole fed-source subset separately proves one unpowered/unbuffered `2301` titanium miner with sole reciprocal output. General powered/stocked/side/prebuild feeds reject. Completion preserves old geometry/members/edges except the declared join.

<a id="explicit-free-to-free-elevated-grid-span-04-development-slice"></a>
### Explicit free-to-free elevated grid span

`native_elevated_grid`: ordinary `2001`, explicit levels 0–3 with ≥1 raised, 1.3333333 m/layer, 1.5–30 m native-snapped straight 3D endpoint distance, 4–64 complete points, flat end pairs, slope ≤0.5. The 30 m bound is not polyline length. Altitude outside this mode rejects; absent is not explicit zero.

Its sole bound subset is an exact empty independent raised `2001` tail, fresh source ID/hash/start position, free destination and full old-path proof. Preserve its exact non-removing cover; 3–63 NEW points retain costs/occupancy. No destination reuse, fed/loaded source, arbitrary stitching, lift, deleted points or flattened legacy fallback. Later spans use actual terminal/readback IDs.

<a id="native-bounded-belt-inserter-attachments-04-development-slice"></a>
<a id="initial-sorter-filter-during-normal-construction-04-development-slice"></a>
### Sorter attachments

Candidate selection precedes native placement: facing error <14° device/device or <11° touching belt. Belt/device limits include 5.5 m/3.499 grid units; belt/belt 5 m/3.2. Minimum span, collision, terrain, range/materials still apply. Advertised bounded one/two-belt interpolation retains quarter-turn/folded <40° seed gates and promises no connection.

`nativeChecks` counts actual placement calls, unlike candidate checks. Keep `lastNativeRejection` and `lastPreNativeRejection` separate. Zero checks means placement unknown. Initial sorter filters need supported construction/echo; existing filters use configuration.

<a id="explicit-local-geometry-cut-and-complete-read-only-route-04-development-slice"></a>
### Geometry cuts and read-only routes

Explicit geometry allows ≤256 signed IDs/64 terrain points/1024 colliders bound to whole `factoryGeometryHash`. Built belts require `native_runtime_belt_collider_world_bounds`; missing collider is unknown. Include expanded sorter bounds, prebuilds and corridor risks, not only centres.

`geometry.nativeBeltSegments` reads ≤16 explicit free spans, ≤64 points each/1024 total, 1.5–30 m and supported explicit levels. Require complete ordered `read_only_native_belt_segment_points_v1` echoes, `nativeConditionsChecked=false`/`executable=false`. Failed generation returns no partial points. No occupancy/placement check or estimated fallback is provided.

`beltEndpointPreview` qualifies one free NEW `2001` span with ≤2 real endpoints and ordinary `2011/2012` sorters/filter/quarter-turns. `beltRoutePreview` binds both real endpoints, frozen geometry and ≤12 explicit segments/240 m summed chords/192 distinct NEW points, with full-path stage 1 and ALL-belts-plus-two-sorters joint checks. No implicit routing, covering, clipping or legacy arguments.

Both require `prepared=false`, `commitAllowedNow=false`, `executable=false`, exact request/point/binding/hash/budget echoes and no token. Successful responses may still have guard/native blockers. Not performed is not positive; performed/failed is refusal. MCP withholds substituted ordinary tokens/incomplete old-cohort echoes. A positive proves bounded captured geometry/material/native conditions only, not future IDs, construction, power, sustained supply or permission.

<a id="warehouse-capacity-and-reservation-configuration-04-development-slice"></a>
## Configuration, transfer and dismantle

Storage-capacity binds full state on unstacked `2101` using `set-bans`, `lock-occupied`, `filter-empty-or-matching` or `clear-filters`. Disabled tail grids do not freeze withdrawals/`TakeItem`. Prove projected filters, counts/inc/topology; compact buffers are not native indices.

Sorter-filter binds root config/held cargo, with supported empty Returning or ordinary `2011/2012` Inserting. Only filter/sign changes; held cargo still goes to the same destination. Confirmed inserter null readback is unfiltered; request `filterItemId=0` clears. This is not jam clearance.

Station storage only configures empty/same-item no-order slots via `SetStationStorage`; belt selects an unselected connected empty output; charge uses exact native range/3 MW steps. Fleet transfer preserves all unrelated state. Transfer moves real stock, refuel uses `AutoReplenishFuel` and count/inc conservation, save uses internal primary/`SaveCurrentGame`.

Existing fleet transfer accepts drones5001, vessels5002 and native station warpers1210, count1–100. Warpers require an ordinary ILS, native vessel warp-drive unlock, actual `warperCount`/`warperCapacity`, zero-inc deposit items and exact player/fleet hashes; collectors/PLS or unknown/full capacity reject. The same original action proves bilateral package/counter changes and unrelated fleet/storage/energy preservation. This changes no remote write scope, does not create warpers and never retries an unknown transfer under a new key.

Dismantle is limited to advertised completed miners, ordinary `2011/2012` sorters, isolated empty default `2101` or empty `2001` chain head. Storage requires all 30 empty/default grids, no stacks/links/cached references and complete bounded scans. Belt needs ≤16 belts/512 cells, empty open independent reciprocal path and no external/prebuild/cached references; middle/connected-tail/loaded/higher-grade reject. One `DoDismantleObject` call must prove disappearance, exact refund/inc and preserved survivors/topology. No arbitrary delete, instant dismantle, auto-rebuild or direct link repair.

## Protected resume and import

Import prepare binds restricted current session/revision without content. Only a subsequent explicit reply permits confirmation/boundary attestations and normal-save copy/adoption. The original is not request-addressable.

Resume uses the current protected token. At the menu `gameLoaded=false` is expected: fresh prepare checks native preload/menu/no-loader, exact source and Journal without loading. Availability alone proves neither valid expiry nor final readiness.

The six modes, evidence versions 1–6, exact candidate/floor/confirmation requirements, leases, consumption and migrations are in [safety](safety-model.md#protected-recovery-and-version-changes). Active migration permits only `29104→29131`/5 or `29131→29145`/6; expired-primary has only researched directional links. Expired-primary adds only the researched `29145→29151` pair; Journal permits ≤5 durable transitions. Default remains exact-version-only. No selector, fallback, ticket edit or post-consumption automatic replay.

Flight commit verifies a separate internal checkpoint first. Reload only accepts the matching failed/interrupted flight; stable arrival/covering primary progress retires it. Transition-time `NO_LOCAL_PLANET` is not permission for another flight/load.

## Action semantics

Mutation is fresh prepare → exact validation → idempotent commit → same-action terminal → reciprocal/material readback. Prepare stays read-only with writes disabled. A read-only assessment is never a commit plan.

Actions may execute/wait/complete/fail/require recovery/become unknown. Check `terminal`/`succeeded`, identity and deltas, not tool-level success. `ACTION_OUTCOME_UNKNOWN` needs original-key/result reconciliation; a new key cannot guess-replay. Single-flight/quarantine survive Pipe reconnects.

Move surface preview is observation, not pathfinding. Its 180/600-tick failure guidance forbids repeating the target. Passive drift retains separate trusted-inspection/time/displacement bounds. Later readiness failure does not undo a successful action; revision is not an accepted-action counter.

Source authority: [Contracts](../src/Spherewright.Contracts), [MCP tools](../src/Spherewright.Mcp/Tools), [Core](../src/Spherewright.Bridge.Core), [native adapter](../src/Spherewright.Plugin/Game). Earlier detail: [accepted protocol](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/protocol.md), [fixed Lab](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7). Offline contracts do not establish installed/live acceptance.
