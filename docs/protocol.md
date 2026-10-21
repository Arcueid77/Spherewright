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

`prepare_move.surfacePreview` is additive advisory evidence for at most64m of the requested shortest surface arc: at most65 samples with maximum1m spacing and130 total Native downward queries, using the existing8704 ground /16 water masks. It creates no order and changes no player/energy fields or commit admission. Spans beyond64m are unavailable; partial, missing or unavailable evidence stays unknown. Even complete sampling is not Native pathfinding, route clearance or proof of Walk.

A fully observed coastal crossing still requires no unknown samples, the exact reviewed target and an above-water terminal suffix at least4m and at least`2*arrivalTolerance+1` metres. Each suffix sample needs valid ground/water hits and finite `groundBelowWaterMetres<=-0.1`. Keep `arrivalTolerance=1.5`, the original finite budget and original action/key; never widen/replay a failed target. Require actual terminal/Walk/low-speed/energy readback, and retain the existing600 consecutive grounded low-speed ticks for flight shore landing. Water midpoints, a shallow endpoint or a failed action are not landing proof; failure stops that candidate family, without another route or local flight.

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

`spherewright_get_player_state.constructionDrones` preserves existing counts including `total` and adds five nullable observations: `nativeSpeedMetersPerSecond` (m/s), `nativeEnergyPerMeterJoules` (J/m), `nativeEjectEnergyJoules` (J), `nativeRepairEnergyPerTickJoules` (J/native tick) and `nativePendingJumpTicks` (Native ticks). Speed comes from `history.constructionDroneSpeed`, drone energy settings from `Configs.freeMode`, repair from `repairHpPerTick * repairEnergyCostPerHP`, and pending jump from `controller.actionWalk.jumpDelayTick`. Units follow DSP29151's60-Native-tick game-second basis; repair is energy per Native tick, not watts. Missing, unavailable, nonfinite or negative values are null, not zero; older0.4 payloads may omit them. These conditional observations support an initial in-place estimate of necessary construction-tail budget, not execution authority or complete Native geometry evidence. Fresh prepare/commit, original return/core/reserve, single-writer and newer-primary rollback protections remain unchanged; no tool is added and no execution guard is relaxed.

## Native player physics observation

In 0.5 development, `spherewright_get_player_state` adds nullable `nativePhysics`. It copies the current Unity version, physics timestep, Rigidbody settings and bounded player-collider/material facts on the game thread. Units are seconds, metres, metres per second, metres per second squared for both gravity vectors, and kilograms. `gravity` is Unity gravity; `nativeGravity` separately records the controller's actual universal gravity, in universal coordinates. Rigidbody/collider facts use the currently loaded local Unity scene; transform coordinates explicitly before comparing vector directions. Collider dimensions retain their local frame with copied world transform/scale. Interpret `simulationMode` and the named velocity settings against the returned `unityVersion`; no setting is a proof of post-step velocity. `state=unavailable`, a missing field or a missing material means unknown; older payloads can omit this snapshot. `safetyCertified` is always false: observed settings do not prove the total post-contact velocity, a continuous dry region or a safe stopping distance. This observation does not run physics, change the player, replace fresh prepare/commit, enter the existing player hash domains or itself authorize the finite-cost movement branch; the applicable movement, flight and reserve gates still apply.

The detail read may additionally expose `nativePhysics.autoSyncTransforms`, `groundContact`, `resumeGroundContactPreSync` and `resumeGroundContactPostSync`. Ground-contact observations retain `state`/`reasonCode`, capture tick/stage, prescribed layer mask15873, trigger policy, `checkSphereHit`, whether the sphere cast was attempted/hit and `groundDistance`. Cached Walk grounded/distance values are labelled `cachedValuesAreObservationOnly=true`; they are not current-contact proof. A cached Native ground-distance20 after a downcast miss is only the20m search-limit sentinel, not a measured ground distance. The new query reports `groundDistance=0` for an actual sphere hit, the actual finite cast distance for a cast hit, and null for a cast miss while retaining `sphereCastHit=false`. Unobserved/exceptional query results remain unknown, not false or zero. A hit collider exposes only type/layer/trigger/enabled/active/default-scene facts, without names, paths or instance IDs. Pre/post-sync observations apply only to the same adoption/session and clear on mismatch. Missing/unavailable values remain unknown and every contact snapshot has `safetyCertified=false`. This player read queries only: it never calls SyncTransforms, integrates physics or changes player state, and does not enter existing v1 hashes.

Detail-only local terrain lifecycle observations may additionally appear as `nativePhysics.terrain`, `resumeTerrainPreFirstFrame`, `resumeTerrainPostUniverse` and `resumeGroundContactPostUniverse`. Retain their actual stage/session/adoption and unavailable values; a planned stage is not a captured Native pass. These fields and retained old sync diagnostics do not run the first frame, authorize a retry, alter v1 hashes or establish why a previous ground check failed. The new first-frame path performs no SyncTransforms; current query/cached-value and safety-certificate distinctions remain unchanged.

The nullable terrain diagnostics `rootHasParent`, `rootLocalPosition`, `rootLocalRotation`, `rootLocalScale` and `rootLocalToWorldMatrix` copy the current root facts; the matrix contains16 floats in row-major order. Missing/unknown values remain unknown. The exact unit `lossyScale` path is retained. The additional scene branch requires a parentless root, exact local/world zero position/identity rotation, positive uniform scale `S<1` that passes Native `Vector3` approximate-unit comparison, world `lossyScale` exactly matching `S`, and a finite matrix exactly `diag(S,S,S,1)`. Binding rechecks `S` exactly and rejects change. Foot/radius/scene/object references, energy,13-tick and return gates all remain required; contraction is no distance or energy proof. These read-only diagnostics remain outside v1 hashes.

Nullable `voyagerFlightReadback.firstLandingHandoff` retains the first same-tick capture: `capturedAtGameTick`, `proved`, first `blockerCode`, `captureStage`/`captureExceptionType`, `terrain`/`groundContact`, and nullable `coreEnergyJoules`/`loadedEnergyJoules`/`perNativeTickEnergyJoules` (P). Later reads do not overwrite that first capture. Unavailable observations remain unknown; copied terrain/contact snapshots retain `safetyCertified=false`. These diagnostics stay outside v1 hashes, add no tool, change no confirmation/recovery gate and prove neither physical idle nor a successful route.

The terrain snapshot may retain `footHits` (at most257) and nullable `footHitDetailsComplete` from the same bounded overlap query. A mesh with no current-simulator match remains unclassified; that alone does not prove foreign, ocean or other-planet ownership. Detached exact-reference and Unity-equality counts are observations, not interchangeable ownership proofs. `uniqueSurfaceIndex` is zero-based only for a unique exact current-surface match. Bounded `registeredSimulatorCount`, `registeredTerrainOwnersComplete`, `registeredTerrainOwners`, `footCurrentSurfaceHitCount`, `footRegisteredSurfaceHitCount` and `footOverlapsCurrentOrRegisteredSurfaceOnly` may describe registered Native terrain without exposing Native IDs or resources. Unknown, duplicate, ambiguous, unready, wrong-world/star/slot or truncated evidence rejects. A complete diagnostic list or Unity equality is not admission; known ocean/sphere membership alone does not pass the terrain-foot gate. The original query `15873 | (1 << 31)` mask, trigger policy and257-result bound remain; observations stay detached, `safetyCertified=false` and outside v1 hashes. Ordinary layer9 observations do not add this overlap query. Older payloads may omit these observations; absence remains unknown.

The same `spherewright_get_player_state` may return nullable `nativeLocalControlEnergy` without a new opt-in. Its `perNativeTickEnergyUpperJoules` is conditional P in joules per Native tick; retain `basis`, `costInputsProved`, `independentDebitsExcluded`, `independentDebitsBudgeted` and `blockers`. The legacy `independentDebitsExcluded` keeps its original zero-independent-debit meaning and v1 basis. The bounded v2 basis `conditional_native_walk_drift_tick_upper_v2` may instead report `independentDebitsBudgeted=true`: P already includes `independentEnergyPerNativeTickUpperJoules`, with separate `shieldRechargeEnergyPerNativeTickUpperJoules` and `laserRechargeEnergyPerNativeTickUpperJoules` contributions. Do not add them to P again; they are conditional mathematical bounds, not observed payments. `executionPremisesProved` is always false: this read proves no execution permission, and a paused world remains explicitly blocked. Null/unproved is unknown, not zero; this detached observation is outside existing v1 hashes. Generation g remains route-preview `coreGenerationLowerBoundWatts` in watts, so compare rates only after explicit unit conversion. Unknown P/g or nonpositive net generation never guarantees progress. Even a positive estimate permits only a freshly guarded finite attempt, not a charging or flight guarantee.

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

Before an outbound leg reports `flight_succeeded`, it must pass the same complete strict predicate used for the `remote_landed` idle handoff. Landing counters alone are insufficient. Outside the bounded core-only landing-recharge wait below, if a predicate is unproved, keep the original action's failure/unknown path and its exact predicate code in `voyagerFlightReadback.failureReason`; do not replace that detail with a generic arrival result. This requirement applies to new results and does not rewrite recorded historical successes. `GameMain.Pause` proves neither physical idle nor recovery and grants no reload authority; checkpoint recovery remains a separate protected prepare/commit with its applicable confirmation gate.

The existing flight may wait for bounded Native natural recharge before landing handoff only when core-stop reserve is the sole remaining admission deficit and every independent premise and full budget is proved. The 600-tick minimum stability gate and original 7200-tick contact timeout remain; waiting resets neither. Every actual 501 rechecks fully funded 2P, exact scene/ground/cost/work facts, original journey/return budgets and the 30-second original-action lease. Only Native GenerateEnergy may replenish energy, without energy writes or a promise of positive net gain. Flight success and checkpoint success require a current complete original 13P idle admission. Any other change, failed premise or insufficient remaining time follows the original action's stop/recovery path without rekeying or replay. `firstLandingHandoff` remains the first attempt and may retain `proved=false`; eventual success requires fresh complete current validation. A false first diagnostic alone does not establish final failure, and waiting grants no construction admission.

## Voyager local work and durable structured construction

While the original journey is `remote_landed`, its local exception binds the exact owned save, Native version, session, world/player/controller/mecha and actual loaded local factory. Fresh station configuration, package/storage or fleet transfer, refuel and Save retain their native scope. Move/Harvest and the separate structured-build path additionally reserve finite work and stopping time before the original return window, the loaded return-energy floor and native `1+extraWarperReserve`. None resets the journey deadline or grants remote writes from home. Return-flight core recharge is a fresh departure gate, not a requirement to keep the core full during every local action.

Move/Harvest retain their existing ordinary RTS actions and the no-water fast path. The finite-cost branch permits bounded runtime Walk/Drift with a fresh Native pre-player hook and exact owned-world/player/controller/mecha/local-factory binding; queued orders, manual input/inertia, pending jump, navigation, fast travel, warp and Fly remain excluded. Native horizontal, thrust and background costs must cover the finite work window and 13 paid Native game ticks of stopping, including simultaneous mining and RTS deceleration, without spending the original core/loaded return floor, native warpers or return time. The Move/Harvest stop bound is the earlier of those 13 paid game ticks and an independent one-second wall deadline. UTC admission also leaves at least one second for cleanup; that wall bound is a patience deadline, not a physical-stop theorem or 60 paid ticks. Neither bound reopens the original action or journey deadlines. The local UTC/tick deadlines and 30-second original-action lease remain fixed in scope. This replaces the former whole-ball ocean exclusion only in the qualified finite-cost branch; it does not grant path clearance or a continuous dry-region certificate. Normal success still requires actual dry Walk and true Native zero-speed readback; RTS zero, an aborted order or a paused game is insufficient.

After a successful remote landing, the same original journey keeps guarding idle energy and its original return clocks until a budgeted local action, controlled construction or the actual return flight takes over. Loss of idle Walk, cost or original return-clock evidence takes the exact-owned Pause/quarantine path. A previously successful flight or Move remains successful; subsequent idle protection does not rewrite its result. Eligibility is not rebuilt silently if Host unload or default Save reload loses the in-memory landed-controller binding: pause/quarantine only the same owned world and reject local/structured work unless the separately confirmed exact ticket-bound LastExit continuation proves a distinct new idle owner. Retain checkpoint and confirmation gates. The ordinary path covers outbound, local work and normal return within one Host. Arrange logistics save/recovery continuation at home after the journey closes; remote in-journey cold recovery is unverified.

On a guard failure, expired stop/drain bound or lost caller/host observation, the finite-cost fail-safe calls normal GameMain.Pause only while the original owned binding still matches. The current LogicFrame may still finish one whole player-and-drone tick, so that complete tick must remain funded; only subsequent Native gameplay energy/dispatch ticks freeze. Pause proves neither physical idle nor successful drain. Preserve unresolved outcome_unknown, quarantine and the exact original action/build/checkpoint; do not auto-resume, load or replay. Never pause a replacement world. True Host unload pauses the exact owned protected world before removing its hooks. An ordinary MCP disconnect leaves the DSP Plugin resident and its lease checks running. Original wall deadlines do not renew while paused. New-source full Plugin/regression, exact-SHA CI, Lab lock/adoption, cold deployment and live checks remain required before runtime credit; this adds no public tool or protocol version.

Local control retains the legacy zero-independent-debit subset (shield capacity0 or recharge already disabled, and inactive laser), with a proved bounded alternative for shield/laser refill costs only. Both paths still require an empty old ammo chamber, valid empty selection with whole ammo storage item/count/inc/filter0 and actual ROF/cost0, and ground/space `fleetCount=0`: **zero configured fleet slots**, not zero active vessels. Empty default ammo slot1 is allowed; no weapon, recharge or fleet setting is changed to qualify. Native defaults, peaceful mode or currently full shield/laser buffers do not prove zero costs. Replicator/research queues must be idle, with no-input/stability evidence covering generation and recharge-speed inputs throughout the paid envelope; unused native research-item buffers need not be empty.

The bounded path proves a weapon-power upper bound from a fresh safe superset of all loaded item-prototype metadata, the current fuel selection, both Native inc tables over indices0..10 and the no-prototype fallback, so a normal fuel switch cannot escape the bound. Unknown, nonfinite, unstable or unsafe cast/overflow arithmetic rejects. The total per-tick refill bound B covers both shield and laser without credit for full buffers or peaceful mode. It is added after movement/work consumption scaling; B is not multiplied by `energyConsumptionCoef`. This proves neither actual child-world costs nor arbitrary combat support.

The existing finite native construction adapter may explicitly use the same bounded-refill proof only when its background budget already includes the same captured B; it verifies that budget relationship and does not add B twice. Legacy exclusion remains the default. Generation/recharge-speed stability must cover the full paid work and drone tail plus2ticks. Fixed Walk/anchor,16m targets/28m drones, original geometry, core/loaded/return floors and unknown/drain gates remain unchanged; this adds no construction scope or layout capability.

Existing `prepare_build`/`commit_build` can bind one Native-validated batch of1..8 same-item objects within the original journey:2301,2104,2201,2203,2205,2001 or2011. Native miner-node, pose, technology, collision and connection checks remain. The first subset rejects elevated paths and belt-cover reuse, and requires the bounded16m Native target/ejection envelope and fixed anchor. Generic blueprint syntax gains no miner layout format. Native creation happens once for the original action; durable write-ahead, exact prebuild-to-entity/component/topology evidence and one measured whole-batch material debit are retained. Per-object receipts share that debit; never sum it once per object.

Read `constructionControl` in the original action/build: `executionKind=structured_native`, phase, dispatch/physical-idle/binding/anchor/reserve evidence, observation lease, finite tail ticks/energy and stop reason. Normal construction still requires Walk at the fixed held anchor. Drift or anchor loss stops new dispatch immediately and takes the exact-binding Pause/unknown path; it is not proof that drones drained. Initial work remains at most 3600 ticks/60 seconds across fresh resume, with only the 30-second working observation lease renewed by original-action polls. A known bounded cancellation retains its first cleanup clocks: safe idle still requires a real subsequent Native construction pass on the exact factory after disable, a clear original prelaunch cache and actual all-owned-drones-idle readback. Guard/lease/tail failure uses the fail-safe above; neither dispatchDisabled nor Pause supplies physicalIdleVerified.

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

The existing remote-quarantine continuation and its bounded V1–V3 provenance openings keep `recoveryMode=default`; ordinary healthy/default behavior is unchanged. The separately named failed-remote LastExit reauthorization below does not extend those provenance generations. Fresh prepare must return `userConfirmationRequired=true`, the actual exact ticket-bound LastExit `candidateGameTick`, `exactEmbeddedIdentityVerified=true` and a `confirmationDigest`. The executor must have applicable explicit owner authority and check this fresh disclosure before commit. Commit still uses `userConfirmedInConversation` and the matching digest. Existing authority for the same recovery class can apply without another identical question; it does not imply permission for a migration, another world, original-primary rollback or expanded scope.

The existing narrow `default` proposal may add optional flat parameters `recoveryDeadlineUtc`, `recoveryScope=continue_original_finite_task`, `maximumChargeGameTicks` and `maximumChargeWallSeconds`. Charge caps must be positive and bounded, at most18000 ticks/300 wall seconds. Prepared separately discloses `originalJourneyDeadlineUtc` and `effectiveRecoveryDeadlineUtc`, echoes scope/caps and sets `nativeChargeFeasibilityRequiresFreshCapture`; it does not promise charging progress. The proposed effective UTC window cannot exceed the original declared UTC span. Bind one immutable proposal, evidence and confirmation of the fresh preview under applicable explicit authority before credential consumption; that authority must cover the disclosed deadline and charging attempt. Do not renew it by rekeying, another Host or reconstruction. Original expiry, tick maxima, spent ticks, return reserve and energy/warper floors remain unchanged; ordinary healthy/default behavior is unchanged.

The separate `recoveryMode=reauthorize_failed_remote_last_exit` requires fresh explicit owner authority for the disclosed current remote context. `expectedRemoteRecoveryContextFingerprint` must match the commitment recomputed from the eight independently supplied values below; require Prepared `failedRemoteLastExitReauthorizationUsed=true` and the matching `remoteRecoveryContextFingerprint`, otherwise MCP withholds the token. This is not a V4 provenance opening or an old-grant renewal, and `priorFailedResumeProvenanceUsed=false`, `priorFailedResumeV2ProvenanceUsed=false` and `priorFailedResumeV3ProvenanceUsed=false` are required. A known-terminal-failed prior action remains the controller's checked attestation, not a Native durable outcome supplied by consumption records. Only active unconsumed credential replicas and two immediate fixed records qualify, without ancestor traversal. The exact ticket-bound LastExit source tick equals the ticket minimum and last-landed tick plus1; accounted and successful-checkpoint ticks equal the landed tick. Lease the complete checkpoint/primary/Journal evidence and retain no-newer-primary checks; the successful checkpoint is evidence, never the load source. This mode reuses the same four flat window parameters and fixed scope; both charge caps remain within18000 ticks/300 seconds and cannot exceed the immediate prior proposal. The existing UTC-span bound remains, and the disclosed effective recovery UTC neither edits the original journey deadline nor resets maximum/spent/return ticks or energy/warper floors. Applicable explicit authority and the fresh disclosure/digest remain required. No new tool is added; all bootstrap, foot, natural-first-frame, strict13 and separate fresh return-core guards remain. Source-only implementation is not ground-repair or retry permission.

Compute the owner-supplied expected commitment offline with public `VoyagerFailedRemoteReauthorizationPolicy.ContextFingerprint`, domain `voyager-zero-progress-remote-context-v1`. Its eight parameters are ordered as follows:

1. `journeyJson`: exact Native journey-record JSON.
2. `checkpointJson`: exact Native checkpoint-record JSON.
3. `checkpointNativeFingerprint`: complete checkpoint Native-save fingerprint.
4. `primaryFingerprint`: complete protected-primary fingerprint.
5. `sourceTick`: validated source Native-save tick.
6. `primaryTick`: validated protected-primary Native-save tick.
7. `journalCheckpointJson`: exact Native JSON of the ticket's Journal checkpoint.
8. `validatedJournalHash`: `HashToken` of the complete continuity-validated Journal text: SHA-256 of its UTF-8 text, rendered as64 lowercase hex characters without a `sha256:` prefix.

Obtain these values from the owner's independently protected baseline established before the preview. Record JSON strings must preserve the installed Native `PluginJson.Serialize` output, including numeric lexemes; do not desktop-parse/re-render them, normalize floating-point values or substitute only header/metadata evidence for either full-file fingerprint. For the Journal, preserve the entire validated text after the Native BOM-aware text read, including whitespace and line endings; do not parse/re-serialize it before hashing. The public Core function returns a `sha256:`-prefixed commitment and requires no Lab helper. It computes deterministic data binding; it does not verify or issue owner identity, authorization or a Native failure certificate. The coordinator separately verifies leases, identity/version and runtime gates. This preview's computed Plugin echo alone cannot serve as the owner's independent expected baseline; unavailable independent evidence stays unknown and blocks this mode.

Only an original active Move that started under strict grounded admission may retain its own order through a sole ground-contact miss for at most8 actual Native ticks. Fresh checks on every tick must prove all non-ground premises and fund14P: the current tick plus the original13-tick stop tail. Only a real sphere hit clears the gap; exceeding the bound starts the existing stop path. Ground loss during the stop tail makes task failure sticky. A known-failed result still requires fresh grounded Walk and actual zero speed, otherwise the result remains unknown. This does not extend idle, construction, Harvest, flight, deadlines or return reserves.

The separate legacy local-Move-unknown compatibility input is restricted to `recoveryMode=default` and the legacy source cohort `12e6dfcaaae923c844cc3ae0c2b66c185537b93a`. Supply `legacyLocalMoveUnknownEvidenceJson` and `expectedLegacyLocalMoveUnknownEvidenceHash` together, or omit both. The hash is SHA-256 of the exact immutable, strictly encoded UTF-8 JSON bytes,64 lowercase hex characters without a prefix; the complete carrier is at most64KiB. No file path is accepted. Its outer object has exactly eight fields: `sourceProductSha` (the compatibility cohort), `captureAttested` (true), `actionResponseJson`, `sessionResponseJson`, `ticketSha256`, `journeySha256`, `checkpointSha256`, and `journalSha256`. The two response strings preserve the protected original public `success=true`/`result` response text, not a readback projection; necessary fields are type-checked, normal additional DTO fields remain allowed, and duplicate JSON properties reject. The four hashes bind complete original file bytes; ticket replicas must be byte-identical, and the Journal hash includes the entire original file, including any BOM/whitespace/line endings.

The caller attests the old source revision, action kind and failure from those captures: only a terminal `move` in `outcome_unknown` with `voyager_local_native_tail_unproved` is eligible. Neither the hash nor the supplied raw responses authenticate a historical Native action outcome or installed revision. Prepared must echo `legacyLocalMoveUnknownProvenanceUsed=true`, the exact `legacyLocalMoveUnknownEvidenceHash`, `legacyLocalMoveUnknownKindCallerAttested=true`, `nativeOriginalOutcomeProved=false`, and an explicit explanation of this trust boundary. Prior-failed provenance/terminal-failure and failed-remote-reauthorization echoes stay false; this pair cannot mix with their inputs, verified-newer or autosave modes. MCP withholds unsupported/mismatched previews. Commit uses the existing immutable Native plan token and matching confirmation digest; applicable explicit authority must cover the current disclosure.

Native separately verifies the current exact quarantined action/ticket, source session, same owned identity/version, complete fresh ticket-bound LastExit, current primary, successful outbound checkpoint/journey and continuous Journal under leases; the source process must have ended normally, and durable credential consumption precedes load. Commit reopens and rechecks the full binding and carrier hash. The original Move remains unknown, with no fabricated known-failed/settled result, action replay, journey active-action rewrite or successful-checkpoint load. Any bounded recovery-window proposal remains subject to the existing unchanged original tick/spent/return/energy/warper limits and separately disclosed effective UTC, never a renewal of a closed caller window. Existing first-frame/contact/scene/2P/strict13 gates remain mandatory; this provenance input does not guarantee ground admission or recovery success.

A single legacy-provenance compatibility opening may supply `priorConsumedResumeToken`, `priorSourceFingerprint` and nullable `priorActionKnownTerminalFailed` together, or omit all three. It applies only to this narrow `default` branch and the current quarantine. `true` attests that the controller checked the exact original public resume action as terminal, failed and not unknown; consumption records are not a Native durable action outcome, and unknown must never be attested. Both fixed consumption/authorization replicas must agree under read-only leases, each within64KiB, with complete original-version authorization, matching journey/checkpoint/Journal/current-primary provenance and source tick, plus all no-newer-primary checks. The old immutable proposal and prior fingerprint only reopen the legacy v1 evidence fingerprint; they neither execute the old proposal/action nor supply its budget. Require `priorFailedResumeProvenanceUsed=true` and `priorActionTerminalFailureAttested=true` in the fresh Prepared response, retaining all confirmation/digest gates; missing or ignored echoes block compatibility. A fresh proposal needs current applicable authority. New authorization records mark this hop; the three-parameter legacy path still rejects a marked predecessor; original tick/spent/return and energy/warper reserves do not reset. Direct and ordinary v1 paths remain unchanged. Keep token/fingerprint values and record JSON protected.

An explicit V2 predecessor opening additionally requires nonempty `priorLegacySourceFingerprint` with all three prior fields. `priorSourceFingerprint` identifies the immediate predecessor source; the new field identifies its older legacy ancestor. Four fixed bounded records are leased: the immediate-predecessor pair and the older-ancestor pair, byte-identical within each pair and each record within64KiB; the older ancestor must have no compatibility marker. Current quarantine must match the publicly known-terminal-failed predecessor action and the controller attestation. Both provenance layers must reopen the exact route/checkpoint/Journal/full-source/current-primary evidence at the same source tick, with no-newer-primary and all original budgets enforced. Prepared must echo `priorFailedResumeV2ProvenanceUsed=true` as well as the existing provenance/terminal-attestation echoes; MCP withholds the token if a required echo is missing or false. A new fingerprint domain binds all this evidence without changing v1 player hashes. The successor cannot become another V2 predecessor: its ancestor is already marked. This bounded opening is neither an arbitrary chain, old-action replay, tick advance nor budget renewal; omitting the new field preserves the existing three-parameter behavior.

The explicit V3 zero-progress provenance opening additionally requires nonempty `priorIntermediateSourceFingerprint` with every prior field and the known-terminal-failed attestation. `priorSourceFingerprint` is the newest predecessor source, `priorIntermediateSourceFingerprint` the middle source, and `priorLegacySourceFingerprint` the oldest legacy source. Six fixed bounded leased records form three byte-matching replica pairs and three exact evidence openings; the oldest ancestor is unmarked. Current quarantine must match the exact failed predecessor action. All three route/checkpoint/Journal/full-source/current-primary witnesses, zero-progress/source-tick and no-newer-primary checks and original budgets must validate. Prepared must return `priorFailedResumeV3ProvenanceUsed=true` together with the existing provenance, V2 and terminal-attestation echoes true; MCP withholds the token on missing/false V3 acknowledgement. The new domain binds all three witnesses. The successor cannot be another V3 predecessor and a fourth generation rejects. Empty new input preserves v1/V2 behavior. Evidence opening itself advances no tick; any permitted natural first frame is separately guarded and charged. This grants no old-action replay, unknown attestation, arbitrary chain, elapsed/cap reset or extra authority.

A conditional registered-Native-terrain branch retains positive exact-current-surface contact and all original bootstrap premises. It may account for other local-star simulators only through a fresh exact world/star/registration-slot/ready binding, unique exact-reference membership and the proved sphere-disabled Native branch prerequisites, within16 simulators and256 surfaces each; the original257-result query remains bounded. Normal Native registration may precede the real Universe(151) positioning/layer lifecycle; that possibility alone does not classify a captured hit. After the real151 pass, freshly prove the registered nonlocal surfaces and any ocean disabled, their sphere enabled and expected Native positioning, with no residual nonlocal foot obstruction; also prove current layer9, current-only contact and fresh ground. Only then may the real501 checks proceed to original strict13, while the current601 remainder stays fully funded. Reuse the existing first-frame evidence; never call SetLayers, UniverseTick, Simulate or gameplay pose/energy setters directly. Unknown or a missed lifecycle postcondition refuses/Pause/quarantines the exact owned world. Prior retired all-current-query attempts stay retired; this source adaptation grants no additional blind Load. Existing six optional recovery inputs, fresh-mode echoes and the ordered eight-field context commitment are unchanged.

Only an exact-owned, exact ticket-bound LastExit adoption may conditionally permit one naturally scheduled first frame when a real foot probe hits the exact owned terrain collider on layer31 and all other mode/scene/input/order/UI/identity/source-proof premises pass. Original funded2P bootstrap and every elapsed/charge/loaded-return/tick/UTC/warper budget must already hold. This path performs no SyncTransforms, manual Native Update/GameTick or gameplay transform/velocity/energy writes. Observe the real Native post-Universe(151) terrain/contact result, then the real pre-player(501) checks; preflight is not an observed task pass or idle admission. If Pause is requested, the current Native frame can still finish its player(601) remainder, which must remain fully funded and be accounted from actual Native readback. After contact and bootstrap premises hold, only the original bounded, tick-guarded charging may continue until genuine501 proves strict13; the separate fresh return-core gate is unchanged. Any unproved premise keeps exact-owned refusal/Pause/quarantine. The one-frame allowance neither restarts elapsed time/caps nor proves ground repair, physical idle or successful recovery.

Before Native loading, bind full-file evidence for the fixed source and current primary, exact same-version owned/peaceful identity, original Journal and successful-checkpoint/journey provenance; persist any immutable recovery-window authorization before durably consuming the protected credential. The successful checkpoint is evidence only, never the load source, and no old `ActionRecord` is fabricated. The exact new Native GameLoader frame-10 completed/disabled `onLoadComplete` callback must synchronously bind the new Native logic after Begin/InitLogic and before the first player (601) tick, proving ownership, original Journal and the closed bootstrap envelope. Under freshly proved runtime premises, bootstrap requires core energy of at least 2P and loaded energy of at least the protected return floor plus 2P. Queues and possible drone dispatch must be closed; independent shield/laser debits must be excluded or covered by the same proved P. Charging uses only normal Native GenerateEnergy in the newly loaded world, never Resume of the old quarantined world. This preflight neither asserts observed Native-tick availability nor publishes an idle owner. Pending proof blocks automatic primary resave and local/structured writes. Genuine Native pre-player (501) checks must guard every bootstrap tick; only one that proves the complete original strict13 checks (core energy at least13P, loaded energy at least the original protected return floor plus13P, and original tick/wall/warper checks) may admit the idle owner, release leases and permit resave, which needs another strict check immediately beforehand. The unchanged return-core minimum is a separate fresh return-flight departure gate, not an added recovery-admission condition. P/g and fresh budgets determine feasibility; no natural charging, later Move, task or return completion is guaranteed, and no gate or budget is lowered or renewed. A later Update or poll cannot supply missing first-tick proof. Charging stops at either declared tick/wall cap and remains inside the original five-minute total adoption bound; none restarts on charging or Host changes. Failure pauses only the exact owned world and quarantines it, without proving physical idle or granting automatic Resume, Load or replay. Preserve the original UTC record, maximum/spent/return-reserve ticks and core/loaded/warper floors; insufficient remaining original reserves reject.

The recovery modes, evidence versions 1–6, exact candidate/floor/confirmation requirements, leases, consumption and migrations are in [safety](safety-model.md#protected-recovery-and-version-changes). Active migration permits only `29104→29131`/5 or `29131→29145`/6; expired-primary has only researched directional links. Expired-primary adds only the researched `29145→29151` pair; Journal permits ≤5 durable transitions. Default remains exact-version-only. No selector, fallback, ticket edit or post-consumption automatic replay.

Flight commit verifies a separate internal checkpoint first. Reload only accepts the matching failed/interrupted flight; stable arrival/covering primary progress retires it. Transition-time `NO_LOCAL_PLANET` is not permission for another flight/load.

## Action semantics

Mutation is fresh prepare → exact validation → idempotent commit → same-action terminal → reciprocal/material readback. Prepare stays read-only with writes disabled. A read-only assessment is never a commit plan.

Actions may execute/wait/complete/fail/require recovery/become unknown. Check `terminal`/`succeeded`, identity and deltas, not tool-level success. `ACTION_OUTCOME_UNKNOWN` needs original-key/result reconciliation; a new key cannot guess-replay. Single-flight/quarantine survive Pipe reconnects.

Move surface preview is observation, not pathfinding. Its 180/600-tick failure guidance forbids repeating the target. Passive drift retains separate trusted-inspection/time/displacement bounds. Later readiness failure does not undo a successful action; revision is not an accepted-action counter.

Source authority: [Contracts](../src/Spherewright.Contracts), [MCP tools](../src/Spherewright.Mcp/Tools), [Core](../src/Spherewright.Bridge.Core), [native adapter](../src/Spherewright.Plugin/Game). Earlier detail: [accepted protocol](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/protocol.md), [fixed Lab](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7). Offline contracts do not establish installed/live acceptance.
