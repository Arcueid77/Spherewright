# Native first-rocket observations

[English](game-api-first-rocket.md) · [简体中文](game-api-first-rocket.zh-CN.md)

Source research uses DSP `0.10.35.29151`, `Assembly-CSharp.dll` SHA-256 `EF314425DADE51F529F184F07BE136CD3C2660A6A81515603C7226FBE7AB1D8C`. It does not prove an installed runtime, construction, launch or arrival.

`SiloComponent.InternalUpdate` normally checks `DysonSphere.GetAutoNodeCount`, consumes its configured rocket item, increments the native consume register and adds a rocket targeting the native selected node. The item is1503. `fired` is an animation latch, never a launch receipt. `chargeSpend`, `coldSpend` and `time` remain raw native units. Product inspection does not call these simulation or launch methods.

`DysonRocket` carries a native ID, source planet, target layer/node, flight progress and universal position. The native pool uses `rocketCursor - rocketRecycleCursor - 1` active entries. Native arrival calls the structure construction path; `DysonNode.sp`, `spOrdered` and `spReqOrder` distinguish delivered points, points in flight and remaining orderable work. Inspecting an existing node never creates or orders it.

Detailed factory inspection exposes an optional `silo` observation with exact entity/component/planet/consumer binding, stock/inc, native cycle/target flags, bounded existing rocket/node observations and resident-star silo coverage. All values are copied on the main thread. Missing arrays, inconsistent identities, mismatching native pool counts or exceeded bounds leave coverage incomplete and aggregate counts null. Partial lists remain partial observations. No sphere/factory/layer/node initialization is performed. Existing action hashes exclude these dynamic detail fields.

The observation bounds are8192 rocket/silo pool slots,128 returned rockets,4096 examined node slots and256 returned nodes across at most10 native layers. Exceeding them is unknown coverage, never a claim of zero. This is a bounded first-rocket observation surface, not general structure planning or a launch controller.

Acceptance must independently bind normally manufactured1503 stock, the exact silo and native consumption, complete source isolation, a real matching rocket and its target's delivered-point change. Aggregate growth or animation alone is insufficient. Minimum native target creation, remote construction/configuration, ordinary save/recovery and actual off-world launch remain separate work.

## Minimum receiving-target write research

The pinned Native `DysonSphere.ResetNew` normally creates the default layer through `QueryLayerRadius` and `AddLayer`; inspection must still prove that this existing layer is present rather than invoking initialization. `UIDysonBrush_Node` accepts an explicit input-panel position as well as a drawing-grid snap. It applies node-collision checks, `CheckCondition` (nearby frames/shells) and the current `GameMain.history.dysonNodeLatitude` stress limit before one `layer.NewDysonNode(nodeProtoId, position)` call. An equatorial position has zero latitude, but the actual existing layer, current technology/stress and native brush prototype still need binding.

`NewDysonNode` creates an unbuilt planned node with zero delivered structure points and a30-point requirement; it refreshes native automatic-node selection and rendering. It does not construct the node or inject a rocket. A narrow future adapter can bind one exact existing **empty** layer, a valid native node prototype and one equatorial position, preserving a zero-node baseline and rechecking before a single normal business call. It must not guess the serialized brush prototype, initialize a missing sphere/layer, edit `sp`/`spOrdered`, call simulation or expand to general structural design. This research does not yet provide that public write or prove runtime acceptance.
