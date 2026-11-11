# Native planetary navigation

[English](game-api-native-surface-navigation.md) · [简体中文](game-api-native-surface-navigation.zh-CN.md)

Research targets DSP0.10.35.29151, Assembly-CSharp SHA-256 `EF314425DADE51F529F184F07BE136CD3C2660A6A81515603C7226FBE7AB1D8C`. The locally installed assembly defines `PlayerNavigation.Navigate(int, VectorLF3)`, `DetermineArrive()`, `Arrive()` and `Abort()`.

## Native control path

For the current planet, `Navigate` computes spherical distance and derives Fly/Sail choices from the player's existing thresholds. `PlayerMove_Walk.GameTick` invokes `DetermineLowVelocity` and its ordinary `SwitchToFly` path when the thruster level and distance permit. `PlayerMove_Fly.GameTick` uses Native navigation velocity, altitude control, energy consumption, gravity and collision. `DetermineArrive` checks the ground-projected target within3 metres. `Arrive` releases the task and invokes the normal landing callback. The adapter observes the resulting actual grounded stop.

Opt-in `useNativeNavigation=true` extends `spherewright_prepare_move` and its existing commit/result family. It requires arrivalTolerance3..5, an owned grounded stationary local player, unlocked thruster, empty navigation/order ownership and a closed journey. Requests bind one current planet and the existing Fly/Sail thresholds. Distance must exceed the Fly threshold and20 metres, stay within the Sail threshold and500 metres. The thresholds remain part of the plan and are checked again during execution.

## Energy and failure handling

The adapter reuses `NativeLandingTickEnergyPolicy`, including current Native thrust, gravity, work, consumption and independent background costs. A60m/s vertical bound funds the complete3600-tick window, with900 ticks reserved for landing. Loaded energy funds the entire declared window, and core energy funds two Native ticks. Actual cost and remaining landing energy are observed throughout. Native research points and item buffers retain their normal game accounting.

An expired work allowance releases only the exact owned navigation task and requests the ordinary Native landing altitude. Completion requires observed Fly, Native arrival, task release, matching world/player/controller/planet references, a grounded stationary Walk and the declared final distance. A known failure settles on the ground before its terminal result. Lost identity, foreign control or unproved bounded landing retain the original action as outcome_unknown and quarantine. Original accepted/unknown actions keep their identities.

The new plan/readback fields are `nativeSurfaceNavigation` and `nativeSurfaceNavigationReadback`. Existing walking, passive-drift and tokenless walking-preview requests retain their existing entry points. Native-build/contracts checks qualify source. Installed movement and subsequent production acceptance follow the source-specific Lab record.
