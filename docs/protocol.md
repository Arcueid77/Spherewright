# Bridge protocol

[English](protocol.md) | [简体中文](protocol.zh-CN.md)

Protocol version is `1`; native compatibility candidate targets `0.10.35.29151` ([offline evidence; live checks pending](validation/native-29151-preflight.md)). This describes maintained source, not an installed-cohort claim. The accepted 0.4.0 candidate is fixed at `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`; latest published release remains v0.3.3. See [product status](project-status.md), [safety](safety-model.md) and the [Agent playbook](agent-playbook.md).

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

Owned save/player/factory reads require active `sessionId`; planet-bound requests also require `planetId`. Normal actions remain local-planet-only. Entity inspection additionally permits an explicitly selected, already-created, identity-verified simulation-resident factory on the current owned star; it never creates/loads one. Player reads still use current `localPlanetId`.

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
