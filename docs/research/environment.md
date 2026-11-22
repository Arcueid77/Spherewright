# Environment research

[English](environment.md) | [简体中文](environment.zh-CN.md)

## Maintained baseline

The 0.6.0 release targets Windows x64, DSP `0.10.35.29151` and BepInEx `5.4.17.0`. The release archives identify `78274df5e93ca912a2d4b6670f7eb707d3562e94`. The installed build used for the shell and cold-recovery tests was `e221b263`. [Release notes](../release-notes/v0.6.0.md) · [Lab source lock](https://github.com/Arcueid77/Spherewright-Lab/blob/c012e723d75c35e1d72d82f3bb5f286926259945/source-lock.json)

The 0.4 production trial ran on DSP `0.10.35.29145` at commit `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`. Its native assembly SHA-256 was `B84B60DF5A24947A014801F23B9B85D1E77853F42A331918E2DC85B153E50527`. The recorded BepInEx reference SHA-256 was `DC1CB6B58B962BDA5AAA1D6B5F9AE14EC174F61836A1A1F96C1A040C7E8381F7`. [Historical validation](../validation/v0.4.0.md)

## Reproducible inputs

| Input | Maintained fact |
| --- | --- |
| Supported platform | Windows x64 |
| Native DSP reference | `0.10.35.29151`, `Assembly-CSharp.dll` SHA-256 `EF314425DADE51F529F184F07BE136CD3C2660A6A81515603C7226FBE7AB1D8C` |
| BepInEx reference | `5.4.17.0` (Thunderstore dependency `xiaoye97-BepInEx-5.4.17`) |
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

See the [0.6 live record](https://github.com/Arcueid77/Spherewright-Lab/blob/47580368d44f0d033072be5911bc366009c7c000/scenarios/v0.6-ascension/acceptance-2026-11-20.md) for gameplay and cold-recovery results and the [archive checks](https://github.com/Arcueid77/Spherewright-Lab/blob/c012e723d75c35e1d72d82f3bb5f286926259945/releases/v0.6.0/review-candidate-2026-11-20.md) for the two package layouts. Independent manager profiles, another machine and installation transaction-crash recovery remain unverified. Keep raw saves, credentials and machine-specific descriptors local.
