# Overseer DSP API research

[English](game-api-overseer.md) | [简体中文](game-api-overseer.zh-CN.md)

## Scope and provenance

The original inspections used DSP `0.10.34.28529` and native SHA-256 `AE0BA95F75BD879A62AA4CE253B2AB78EAA4FB3C7C595F5E1FEE75EBE0E0EF85`. Their [complete immutable research](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/research/game-api-overseer.md) retains formulas, counterexamples and dated local evidence. Those are historical versioned observations. Current local candidate support is DSP `0.10.35.29145`, as bounded by [final validation](../validation/v0.4.0.md); documentation maintenance is not a new native comparison or deployment.

## Read-only factory boundary

Read only identity-consistent existing `GameData.factories[0..factoryCount)`. `GetOrCreateFactory` creates persistent game state; `PlanetData.LoadFactory` changes runtime state. Neither belongs in an observation. `factoryLoaded` describes display/physics/audio readiness, not persistent-factory existence: an already-created remote factory can be read while `factoryDisplayLoaded=false`. Never scan saves or create factories to discover unseen worlds. All native reads run in one Unity-main-thread task and complete deep copies before leaving it.

## Actual and theoretical production

`FactoryProductionStat.GameTick` updates `ProductStat.count/cursor/total`. Level 0 `total[0]`/`total[7]` gives the last 600 game ticks of automatic production/consumption, or ten game seconds at 60 ticks/s; items/minute is count × 6. Lifetime `total[6]`/`total[13]` includes mecha/handcraft activity and is not an automatic-line window. Arrays persist through native Export/Import, but readiness and declared coverage remain necessary.

Governor's explicit 3600-tick option reads 600 completed six-tick level-1 buckets. Its end is the native six-tick boundary, not an invented current capture tick. `NativeMinuteProductionCounters.Read` validates exact item, array lengths/cursors, nonnegative buckets and agreement with `total[1]`/`total[8]`; it retains no native arrays. Pre-binding history, pause, offline time and gaps cannot become an accepted baseline.

`OverseerTheoreticalProductionCalculator` reconstructs copied current-component formulas for assemblers, matrix labs, vein/oil/water extraction, fractionators, gamma receivers and orbital collectors. It does not call the shared UI calculator or trust untimestamped `refProductSpeed/refConsumeSpeed` caches. Connected design capacity does not fall merely because inputs/power are temporarily short. Unknown identities/formulas/bounds fail closed instead of returning a fabricated zero. Utilization is actual/theoretical only when ready and denominator-positive; discrete short windows may exceed 1, and unknown stays null.

## Power, logistics and research

Actual generation is the sum of identity-bound `PowerGeneratorComponent.generateCurrentTick` in the network. `PowerNetwork.energyExport` is defense-field export, not generation. Current generation/capacity/load are dynamic samples, not static configuration; generator energy buffers use `joules_per_tick`, not item counts. Full-load planning and sustained fuel supply require their own evidence.

Station reads bind `PlanetTransport.stationPool`, entity/component, `StationStore`, physical slot and local/stellar identity. Distinguish planetary, stellar, orbital and vein-collector classes; separately report inventory, signed reservations, idle/working fleets, energy and power. A stocked supply station does not prove dispatch or the root cause of a downstream shortage.

Research is one global `GameMain.history` state, not a separate copy per planet. Validate current/queued technologies against runtime `techStates` and `LDB.techs`; copy ordered queue, levels, uploaded/required hashes and matrix budgets. A paginated snapshot refers to the same captured tick and copied research.

## Diagnosis and temporal evidence

`GameStateReader.OverseerDiagnostics` distinguishes stopped full-output admission thresholds from display capacity. For example, assembly admits another cycle while produced ≤ 9 batches; refinery/chemical paths use ≤ 19 batches. A working machine with inputs already consumed is not necessarily short of the next batch. Long-cycle blocked machines cannot be skipped forever because the default observation window is shorter than a cycle.

Trace actual reverse `ReadObjectConn` links through supported belts, sorters, splitters, pilers, spray coaters, storage and tanks. Sorter filters and splitter `output0/outFilter` direction constrain each item-specific path. A same-item station elsewhere on the planet is not physical evidence. Cross-planet tracing needs the exact demand output slot, matching supply endpoint and that same supply station's actual input path; manual stocking or unproved relay chains do not invent upstream producers. Recursion is bounded to eight levels/64 producers with explicit cycle/budget stop reasons.

A logistics-blockage window requires a qualifying exact route, missing consumer input, positive demand reservation, stocked matching supply and existing fleet. Carrier stage/position/speed/warp/order changes, deliveries or smaller demand reset stagnation. Only 600 continuous game ticks without progress yields **suspected**, not confirmed, blockage. Save/route changes, tick regression, same-tick mutation or a gap above 3600 ticks invalidate continuity. `OverseerLogisticsProgressStore` atomically persists one protected batch before public DTOs use the temporal result; offline wall time is excluded.

## Same-tick bundles, bounds and remaining limits

`OverseerDiagnosticBundleComposer` combines public production, power/logistics and research DTOs captured in one main-thread work item. Factory/tick mismatches refuse the whole bundle. Its versioned public allowlist has no credential, write-plan, private-path or raw-save-identity fields. Pagination binds session, exact filter, page size and expiry; complete first pages do not consume continuation slots.

Requests remain bounded: at most 64 production items, 512 existing factories, 16 planets/page, eight continuation slots and 60-second retention. Component/reference scan budgets fail explicitly rather than silently truncating totals; findings expose their own counts/truncation. Source and contract limits remain authoritative.

Accepted local evidence covers controlled shortage/power/logistics faults, normal repair, active shipment/delivery and save/recovery window semantics. A real unchanged carrier stalled for 600 ticks and then recovered remains unobserved; direct field mutation must never fabricate it. Direct-buffer diagnosis covers assemblers, matrix labs and supported miners; fractionator/gamma/orbital direct coverage is explicitly partial. These limits survive whole-candidate acceptance. All findings are captured-time evidence, not permanent causes or action permissions.
