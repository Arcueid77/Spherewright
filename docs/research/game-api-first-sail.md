# Native first-sail support

[English](game-api-first-sail.md) · [简体中文](game-api-first-sail.zh-CN.md)

This is the narrow owner-directed 0.5 extension of 2026-11-01. Native references are DSP `0.10.35.29151`, `Assembly-CSharp.dll` SHA-256 `EF314425DADE51F529F184F07BE136CD3C2660A6A81515603C7226FBE7AB1D8C`. The original three Voyager acceptance clauses and selected baseline remain independent. This source research is not live acceptance.

## Adopted ordinary paths

- `FactorySystem.NewEjectorComponent(int entityId, PrefabDesc desc)` binds the normal entity, planet, consumer and prototype sail item. `chargeSpend` and `coldSpend` are prototype frame durations multiplied by `10000`; exposed cycle fields retain raw native units, not seconds or game ticks.
- `DysonSwarm.ResetNew()` creates the ordinary initial orbit with `NewOrbit` after normal initialization. Inspection only reads an already existing sphere/swarm. It never calls initialization, orbit creation, `AddBullet`, `AddSolarSail`, or a simulation update.
- `UIEjectorWindow.OnSetOrbit(int)` validates a positive existing enabled orbit and calls `EjectorComponent.SetOrbit(int)`; zero clears fixed targeting. The product uses that same business method once under the ordinary prepare/commit/idempotency envelope. Native may turn an existing charge into cooldown; the adapter must not overwrite native direction or timing.
- This fixed-orbit subset requires native automatic orbit selection off. With automatic selection on, a cleared preferred orbit is not a stop guarantee; this subset refuses that state rather than reporting it as disabled.
- Actual supply uses ordinary manufacturing/storage/inserter paths. The configuration call does not load or consume sails, alter boost or automatic selection, issue a launch, or extend any resource/time budget.

## Readback and evidence

Detail inspection adds `ejector` with exact component/entity/planet binding, sail count/inc, fixed/runtime orbit, automatic/boost flags, raw cycle progress, target state, and the existing local-star swarm's orbit and aggregate sail/bullet observations. The configuration hash binds static identity/pose and fixed settings. The selected orbit fingerprint binds star, ID, enabled state, radius and rotation; ongoing sail counts are excluded. These optional detail fields do not enter the pre-existing factory hash domains.

The star-ejector count covers every already resident factory indicated by the current star's planet records, without creating a factory. Unknown/mismatched factory or component membership makes coverage incomplete and the count null. It is observational coverage, not an execution certificate. A null ejector/swarm observation or incomplete coverage must remain unknown.

`EjectorComponent.InternalUpdate(float power, long tick, DysonSwarm swarm, AstroData[] astroPoses, AnimData[] animPool, int[] consumeRegister)` normally advances charge, verifies native angle/occlusion, adds a native `SailBullet`, decrements sail count/inc and increments the actual consumption register. `DysonSwarm.AddBullet(SailBullet,int)` allocates/recycles a native bullet and records its orbit; native arrival then creates the orbital sail. Those methods are research references only: product code never calls them to launch or create content.

The `fired` value is an animation latch which remains after the launch. It is exposed as `firedAnimationActive` with `firedAnimationIsLaunchReceipt=false`. Configuration success, a single animation read or aggregate cloud growth is insufficient launch attribution. First-sail acceptance requires a bounded original manufacture/supply record, exact ejector inventory consumption and actual swarm flight/arrival evidence with source isolation/coverage, followed by normal save/recovery.

## Validation boundary

Core regressions exercise production request/hash/orbit binding, disabled/unknown/mutated orbit rejection, fixed-mode stop semantics, conflicting mode fields, stock-independent configuration hashing and mixed prepared echoes. Native/solution compilation uses the exact references above. Neither those tests nor the native source study proves installed operation, a real launch or recovery of a populated swarm; those results belong in Lab.
