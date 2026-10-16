# Ordinary-world DSP API research

[English](game-api-m0.md) | [简体中文](game-api-m0.zh-CN.md)

## Scope and provenance

M0 is the historical ordinary-world First Red Matrix capability anchor. The initial native inspection used DSP `0.10.34.28529`, `Assembly-CSharp.dll` SHA-256 `AE0BA95F75BD879A62AA4CE253B2AB78EAA4FB3C7C595F5E1FEE75EBE0E0EF85`, and targeted ILSpy/Mono.Cecil inspection. That is historical provenance, not the currently supported build. The accepted `0.4.0` local candidate uses DSP `0.10.35.29145`; see [environment](environment.md), [exact-version research](game-api-version-0.10.35.29145.md) and [final validation](../validation/v0.4.0.md).

The [complete immutable original](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/research/game-api-m0.md) preserves dated investigations, signatures and old deployment evidence. It includes retired sandbox experiments and then-pending results. Those passages do not grant current action authority or override later accepted evidence. Keep decompiled game source, raw saves and private identities out of the product repository.

## Bootstrap, ownership and observation

`SpherewrightPlugin.Awake` creates the guarded Bridge host; `Update` pumps main-thread work. The authenticated local Named Pipe exposes copied DTOs, never Unity objects. Factory reads and ordinary actions require the exact current-process-created or explicitly adopted `GameData`, session and revision. A player-loaded unrelated world stays restricted until the separate import boundary succeeds. New worlds use DSP's normal `DSPGame.StartGame(GameDesc)` path with peaceful/non-sandbox/1× defaults; missing mode evidence or combat fails closed. Sandbox/resource settings remain reported without granting sandbox tools.

`GameStateReader` binds pool/component/entity identities before reading factory, inventory, recipe, storage, power, research or logistics state. A prototype, object number, filename prefix or position alone is not identity proof. Runtime recipe/build/technology catalogs and dependency graphs supply current requirements; no remembered recipe or guessed unlock substitutes for them.

The per-save gameplay Journal records observed first-event signals and durable checkpoints. Its coverage boundary remains explicit, especially after import. Native technology completion can normally award items: `TechProto.AddItems/AddItemCounts`, `GameHistoryData.UnlockTech` and `GainTechAwards` explain that game-side path. `TechCompletionRewardCatalog` exposes bounded copied metadata without invoking a grant. Metadata alone cannot excuse an unexplained positive inventory delta; reconcile completion, capacity, actual delivery and unrelated inventory.

## Normal action paths and invariants

`NormalGameActionCoordinator` owns fresh prepare, native revalidation, single-flight commit, idempotent result lookup and readback/quarantine. Source modules retain the following researched boundaries:

| Domain | Native evidence and maintained boundary |
| --- | --- |
| Build and links | `BuildTool_Click`, `BuildTool_Path`, `BuildTool_Inserter`, `PlanetFactory.ReadObjectConn`; normal item debit/prebuild creation and drone completion, then exact new-entity and reciprocal-link proof |
| Movement/harvest | Exact owned player order, native travel/range/energy and bounded resource identity; single-flight Move/harvest commits and progress/energy watchdogs stop the exact order without teleportation or input injection |
| Research | Native queue/selection and ordinary mecha research buffers; queue priority only for eligible already-queued prerequisite-complete targets, preserving unrelated order/progress/inventory |
| Refuel and transfer | Already-owned native items, count and proliferator-point conservation, preserved hand/unrelated package state; no energy or inventory grants |
| Storage/filter | Native storage capacity/filter semantics and ordinary sorter configuration; bans limit normal insertion rather than freezing withdrawals, and held sorter cargo remains protected |
| Logistics stations | `PlanetTransport.stationPool`, `StationComponent`, `StationStore` and physical slots; validate local-station sentinel/stellar identity, exact slot settings, energy, idle/working fleets and cargo separately |
| Fleet transfer | Existing `5001`/`5002` items move only between the player and matching idle counter, with prefab capacity and all unrelated station state retained; this never writes station cargo |

Construction excludes pre-existing co-located sorter IDs when attributing a newly built sorter. `stateHash` includes mutable state; `endpointStateHash` binds identity, pose and links while allowing ordinary production to advance. Do not swap those domains or classify a deterministic wrong-domain request as a retryable world change.

## Save, import and flight boundaries

`GameSave.SaveCurrentGame(string)` is the normal saving path for the exact owned world or internally named authorized copy/checkpoint. `OwnedSavePrefixReader` parses only a bounded researched prefix and compares the complete embedded identity without exposing it. Historical source tuple is header `7`, GameData `13/patch22`, GameDesc `9`; researched `0.10.35` targets require `13/patch23`, GameDesc `10`. Account/screenshot fields are skipped under bounds, not disclosed. Prefix proof alone neither grants ownership nor validates the entire save.

`UserSaveImportCoordinator` requires effect-free prepare for the exact restricted current world, a subsequent explicit conversation confirmation and both original-save/Journal acknowledgements before its one normal-save attempt to a new server-generated owned name. It never accepts a save path, opens the original or fabricates earlier Journal history. A save return without exact header proof remains quarantined/restricted.

`OwnedWorldResumeCoordinator` binds protected credentials, exact primary evidence, Journal continuity and native adoption. Default resume requires equal versions; only named researched modes permit disclosed exact directional transitions with later confirmation. No ticket expiry/version editing, arbitrary save selection, LastExit/autosave substitution or replay after consumption is implied.

Same-system flight uses native launch/navigation/landing and a pre-flight checkpoint saved and header-verified before launch. A checkpoint's protected lifecycle binds the exact interrupted flight; success/covering primary save retires it. This is not actual interstellar travel support, which belongs to planned `0.5`.

## Excluded and unproven paths

`BasicProductionLineCoordinator.cs` is explicitly removed from Plugin compilation. Its historical item grants, direct `BuildFinally` and buffer insertion are not normal primitives and must not return through Foundry. Logistics-distributor add-ons and courier primitives are a separate unsupported gap; station fleets do not prove them. Current acceptance remains the bounded local candidate described in final validation, without arbitrary saves, all Mods, another-computer or universal native-API compatibility claims.
