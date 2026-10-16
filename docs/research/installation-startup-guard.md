# Installation startup guard research

[English](installation-startup-guard.md) | [简体中文](installation-startup-guard.zh-CN.md)

## Scope and provenance

This note maintains the runtime/installer exclusion contract implemented by `InstallationStartupGuard`, `SpherewrightPlugin.Awake` and `StopHost`. The [original 2026-09-27 research](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/research/installation-startup-guard.md) is historical source/offline evidence; its then-pending deployment text is not the current candidate status. See [final validation](../validation/v0.4.0.md) for accepted local package/runtime evidence and retained limits. No DSP gameplay API or save format is changed by this guard.

## Native/runtime input

The researched `BepInEx.dll` is version `5.4.17.0`, SHA-256 `DC1CB6B58B962BDA5AAA1D6B5F9AE14EC174F61836A1A1F96C1A040C7E8381F7`. Metadata inspection confirms the public static `BepInEx.Paths.BepInExRootPath` getter. `SetExecutablePath` derives that root from its override or the game's `BepInEx` child and `PluginPath` from `plugins`. The guard uses that runtime root rather than guessing a Steam/user path.

## Mutual exclusion and refusal rules

- The installation target is the directory of the actual `typeof(SpherewrightPlugin).Assembly.Location`. Target and BepInEx root, including ancestors, must be existing ordinary directories without reparse points.
- `GetTargetMutexName` matches the manual installer's `Get-SpherewrightInstallMutexName`: `Local\SpherewrightInstallTarget-` plus uppercase SHA-256 hex of the UTF-8 canonical target path after invariant uppercasing. Canonicalization removes a trailing separator except for a filesystem root.
- `Awake` acquires the lease before configuration and `SpherewrightBridgeHost` creation. A busy target refuses startup. An abandoned mutex also refuses: `WaitOne` grants ownership even when it throws, so failure cleanup must release that ownership.
- While holding the lease, any `.spherewright-install-pending.json` at `BepInExRootPath`, including a directory/link or inaccessible/indeterminate result, blocks startup. Only proven absence permits it; marker contents cannot grant authority.
- `StopHost` disposes the pipe/descriptor host before releasing the guard, including startup failure and `OnDestroy`. Acquisition and disposal run on the same Unity thread because Windows mutex ownership is thread-affine.

This excludes concurrent ownership by a guard-aware Bridge and a cooperating installer. It does not prevent CLR assembly loading before `Awake`, retrofit old Plugin binaries, or authorize automatic repair.

## Validation and remaining limits

`InstallationStartupGuardTests` covers canonical naming, marker refusal, busy/abandoned mutexes, lease lifetime and Windows PowerShell/PowerShell 7 child-process contention with the linked .NET guard. Full `net472` Plugin compilation and real package/runtime checks are distinct from those offline tests.

Best-effort withholding of the main DLL after unresolved caught installer failures reduces old-loader exposure, but cannot guarantee safety after a process kill or withholding failure. The accepted candidate does not establish installation transaction-crash recovery, legacy-wide atomic upgrades, Mod Manager/r2modman interoperability or every Unity Mono loader-path/lifecycle branch. Preserve recovery evidence and require an independently authorized recovery path instead of treating a marker or test result as permission to start.
