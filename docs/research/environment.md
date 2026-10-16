# Environment research

[English](environment.md) | [简体中文](environment.zh-CN.md)

## Maintained baseline

The accepted local `0.4.0` candidate is source `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`, on Windows x64, DSP `0.10.35.29145` and BepInEx `5.4.17.0`. [Product status](../project-status.md) and [final validation](../validation/v0.4.0.md) distinguish that candidate from the latest published `v0.3.3`. Candidate archives remain frozen; documentation changes do not rebuild them or establish a new installation/native trial.

The earlier per-deployment chronology, machine-specific audit and then-pending results are historical. Their complete immutable source is the [research at the accepted commit](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/research/environment.md). Detailed operational evidence belongs in [Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7), which consumes a fixed read-only product dependency. Product build/test/package has no Lab dependency.

## Reproducible inputs

| Input | Maintained fact |
| --- | --- |
| Platform accepted for this candidate | Windows x64 |
| Native DSP reference | `0.10.35.29145`; `Assembly-CSharp.dll` SHA-256 `B84B60DF5A24947A014801F23B9B85D1E77853F42A331918E2DC85B153E50527` |
| BepInEx reference | `5.4.17.0`; SHA-256 `DC1CB6B58B962BDA5AAA1D6B5F9AE14EC174F61836A1A1F96C1A040C7E8381F7` |
| Plugin target | `net472`, BepInEx 5; game/Unity references are not copied into release output |
| Contracts / Bridge.Core | `netstandard2.0` |
| MCP / tests | `net8.0`; self-contained Windows x64 MCP is a release artifact |
| Dependency locks | `Directory.Packages.props` and project `packages.lock.json` files |
| Local references | Ignored `.local/game-refs`; never commit game assemblies |
| Embedded playbook | `docs/agent-playbook.md`, resource `Spherewright.Mcp.agent-playbook.md` |

Use the repository project files and locks for package versions, rather than old machine SDK/runtime lists. Native hashes identify exact researched inputs; a matching hash alone does not prove a running Plugin or gameplay success.

## Research and verification method

1. Resolve the DSP installation with the product locator using `SPHEREWRIGHT_DSP_DIR`, Steam metadata or an explicit `-DspDir`. Documentation uses `<DSP_ROOT>`; no private absolute path belongs in tracked output.
2. Record native version provenance, file size and SHA-256 before targeted metadata/decompilation with Mono.Cecil or ILSpy. Inspect exact signatures, instruction paths and invariants; do not execute game code as a reflection probe or commit decompiled game source.
3. Use `scripts/sync-game-refs.ps1` only when authorized to refresh compile references; it copies required references and records hashes without modifying the game assemblies. Native-version research remains separate from migration permission.
4. Locked restore, relevant automated tests and a complete supported-reference Plugin build establish source/offline results. They do not establish installation, an MCP handshake, save adoption, production or endurance.
5. For an independently authorized cold deployment, normal save/audit/close precede replacing one complete matching cohort. Compare each build flavor with its own output: RID and non-RID common assemblies need not have identical bytes. No hot DLL replacement or repeated installation is inferred from a later read mismatch.
6. Validate installed hashes, real stdio metadata/playbook, authenticated current Plugin, exact recovery and native readback as separate evidence levels. A clean MCP stdout/exit proves that probe's protocol result, not every action or release gate.

## Retained findings and limits

Portable SDK use, explicit NuGet sources and case-insensitive Steam-root deduplication solved historical environment issues without changing global user settings. The Plugin explicitly disables SDK-dependent implicit framework-reference injection and pins its `net472` reference package. The sandbox `BasicProductionLineCoordinator.cs` remains excluded from compilation; historical injected production is not ordinary-world acceptance.

The final candidate has same-machine package installation, native runtime, playbook and owned-primary recovery evidence. Another computer, a Mod Manager/r2modman profile, installation transaction-crash recovery and broad third-party compatibility remain unestablished. A planned remote-validation host is not an implemented feature. Secrets, raw saves, embedded identities, credentials, runtime descriptors and private paths remain protected local material and never become research fixtures or public evidence.
