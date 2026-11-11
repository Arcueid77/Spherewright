# Native planetary navigation

[English](game-api-native-surface-navigation.md) · [简体中文](game-api-native-surface-navigation.zh-CN.md)

Research targets DSP0.10.35.29151, Assembly-CSharp SHA-256 `EF314425DADE51F529F184F07BE136CD3C2660A6A81515603C7226FBE7AB1D8C`. The locally installed assembly defines `PlayerNavigation.Navigate(int, VectorLF3)`, `DetermineArrive()`, `Arrive()` and `Abort()`.

## Native control path

For the current planet, `Navigate` computes spherical distance and derives Fly/Sail choices from the player's existing thresholds. `PlayerMove_Walk.GameTick` and `PlayerMove_Drift.GameTick` invoke `DetermineLowVelocity` and ordinary `SwitchToFly` when the thruster level and distance permit. `PlayerMove_Fly.GameTick` uses Native navigation velocity, altitude control, energy consumption, gravity and collision. `DetermineArrive` checks the ground-projected target within3 metres. `Arrive` releases the task and invokes its normal callback. The adapter observes arrival and task release while ordinary movement continues.

Opt-in `useNativeNavigation=true` extends `spherewright_prepare_move` and its existing commit/result family. It requires arrivalTolerance3..5, an owned stationary grounded local player, unlocked thruster, empty navigation/order ownership and a closed journey. Requests bind one current planet, a target near the current radial height and the existing Fly/Sail thresholds. Distance must exceed the Fly threshold and20 metres, stay within the Sail threshold and500 metres. The thresholds remain part of the plan and are checked again during execution.

## Energy and failure handling

The adapter reuses `NativeLandingTickEnergyPolicy`, including current Native thrust, gravity, work, consumption and independent background costs. A60m/s vertical bound funds the complete3600-tick window, with900 ticks reserved for landing. Loaded energy funds the entire declared window, and core energy funds two Native ticks. Actual cost and remaining landing energy are observed throughout. Native research points and item buffers retain their normal game accounting.

Navigation prices active mecha research when every queued technology has complete Native recipe-only unlocks, zero attribute functions and zero completion-item awards. Nonnegative matrix inventory and buffered points remain available to normal research. Forge tasks and incomplete or cost-changing research effects block preparation. Drone work, handcraft and research-setting commits wait for the owned navigation's terminal task release. Preparation preserves the specific work/recharge blocker so callers can diagnose a refused plan.

The plan hash encodes floating-point scalars as IEEE754 integer bits and normalizes signed zero. Classic game runtimes and the MCP runtime can use different decimal text for the same value. A Windows regression loads the production policy in the classic runtime, serializes the prepared DTO through its compatible Newtonsoft build and verifies the current-runtime funding echo.

An expired work allowance releases only the exact owned navigation task. Completion requires observed Fly, Native arrival, task release, matching world/player/controller/planet references and the declared surface-projected distance in Walk, Drift or Fly. The readback reports both spatial and surface-projected distance, current movement state and observed grounding. A water arrival can continue toward land through a newly prepared ordinary move with `allowPassiveDrift=true` or another eligible navigation request. A released task with unproved arrival is a known failed navigation. Lost identity, foreign control or unproved bounded task release retain the original action as outcome_unknown and quarantine. Original accepted/unknown actions keep their identities.

The new plan/readback fields are `nativeSurfaceNavigation` and `nativeSurfaceNavigationReadback`. Existing walking, passive-drift and tokenless walking-preview requests retain their existing entry points. Native-build/contracts checks qualify source. Installed movement and subsequent production acceptance follow the source-specific Lab record.
