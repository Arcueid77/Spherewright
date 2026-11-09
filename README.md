# Spherewright

[English](README.md) · [简体中文](README.zh-CN.md)

Spherewright connects an external Agent to **Dyson Sphere Program** through a matching MCP server and BepInEx Plugin. Structured observation, deterministic planning helpers and bounded native actions preserve ordinary materials, research, power, construction drones and game time. The product embeds no LLM or autonomous expansion loop.

Plugin, MCP, Contracts and Bridge.Core form one version-coupled monorepo. [Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab) owns scenarios, operational handoffs and detailed evidence; product builds, tests and packages do not depend on Lab.

## Current status

| Item | Current state |
| --- | --- |
| Latest formal release | [`v0.4.0`](https://github.com/Arcueid77/Spherewright/releases/tag/v0.4.0), published 2026-10-17 from `b7c0002dbe2e8f5608c5ca140b0c6326a1818021` for DSP `0.10.35.29151` and BepInEx `5.4.17.0` |
| Voyager baseline | All three original clauses are accepted within their recorded source scopes; the same-source review pair is retained in Lab |
| First-sail/carrier-rocket extension | Bounded live acceptance recorded 2026-11-06: local off-world manufacture, four sails launched into the swarm, one carrier rocket arrival with one added Native structure point, auxiliary delivery and normal save/cold continuation |
| Selected extension runtime/review source | `d3b7ac897bb44d62c8a7ed54c51a55ff2b8de7b0`; exact-source CI, Native builds, coherent installation and package payload checks passed within the recorded scope |
| 0.5 publication | No tag, Release or platform upload; later development source remains separate from the selected runtime and review archives |

The GitHub release provides the manual ZIP, Thunderstore-layout ZIP, checksums and generation manifest. Use the manual package for a direct installation. The last recorded Thunderstore check on 2026-10-17 showed `0.3.3`; the owner's later website-upload result remains separately tracked and unverified here.

Development source, fixed Lab adoption, actual installation, final candidates and historical acceptance are different states. Later source or documentation commits do not change published package identity or inherit earlier live credit. See [product status](docs/project-status.md), [ROADMAP](ROADMAP.md) and the [Lab active snapshot](https://github.com/Arcueid77/Spherewright-Lab/blob/main/scenarios/v0.5-voyager/current-status.md) for their respective scopes.

## Capabilities and boundaries

- **Overseer:** bounded production, power, logistics and research observations, upstream tracing and diagnosis. Recommendations grant no write authority.
- **Foundry:** recipe, material, machine, power, site and dependency plans with explicit external supplies. Preview and execution remain separate.
- **Finite native actions:** construction, configuration, supported upgrades, procurement and bounded blueprints, with per-object progress, cancellation and fresh continuation.
- **Governor:** pre-execution production declarations and game-tick measurements. Inventory, rated capacity and short samples do not prove sustained production.
- **Save continuity:** ticket-bound exact-owned-primary resume with durable Journal continuity and separately confirmed disclosed migration. No arbitrary save picker or editor.
- **Voyager and launch expansion:** native-visible exploration, finite flight/recovery, local manufacture/work and logistics primitives. The accepted baseline and first-sail/carrier-rocket review retain their recorded scopes and do not establish universal coverage.

Every write requires fresh native prepare, plan-bound commit, original-action/idempotency reconciliation and relevant readback. Unknown outcomes are quarantined. Item/technology injection, instant construction, teleportation and direct game-buffer writes are excluded. Dark Fog/combat, multiplayer/Nebula, broad third-party Mod compatibility and another-computer use remain unvalidated.

## Install and learn

1. Read [installation](docs/release-installation.md) and use a matching Plugin/MCP/shared-assembly set.
2. Keep [interrupted-installation recovery](docs/installation-recovery.md) available before replacing an existing installation.
3. Follow the [Agent playbook](docs/agent-playbook.md); it is also embedded in MCP and included in packages.
4. Check the [protocol](docs/protocol.md), [safety model](docs/safety-model.md) and [architecture](docs/architecture.md) when integrating or reviewing behavior.

Release evidence is separated by source and purpose: [0.4 release notes](docs/release-notes/v0.4.0.md), [0.4 validation](docs/validation/v0.4.0.md), [29151 compatibility evidence](docs/validation/native-29151-preflight.md) and the [fixed source-specific runtime report](https://github.com/Arcueid77/Spherewright-Lab/blob/74a4ec3d91608467911460eaf0d317800e60309b/operations/formal-release-2026-10-17.md). The complete documentation index is in [docs/README.md](docs/README.md).

## Develop

```powershell
dotnet restore Spherewright.Core.slnf --locked-mode
dotnet build Spherewright.Core.slnf -c Release --no-restore
dotnet test Spherewright.Core.slnf -c Release --no-build
pwsh -File scripts/verify-documentation.ps1
```

Full Plugin builds require legally installed DSP/BepInEx references; see [environment setup](docs/research/environment.md). Package scripts record this repository's own source SHA and clean state; they do not fetch or execute Lab.

Start scenario work from Lab and treat its fixed product submodule as read-only. Implement reusable changes in an independent product clone/worktree, test and commit them, and obtain successful exact-SHA CI before Lab updates its gitlink/lock. Read [contribution policy](CONTRIBUTING.md), [contributor rules](AGENTS.md) and [Lab setup](docs/sample-validation.md).

Licensed under [MIT](LICENSE), with a [Chinese explanatory translation](LICENSE.zh-CN.md).
