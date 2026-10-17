# Spherewright

[English](README.md) · [简体中文](README.zh-CN.md)

Spherewright connects an external Agent to **Dyson Sphere Program** through a matching MCP server and BepInEx Plugin. Structured observation, planning and bounded native actions retain normal materials, research, power, construction drones and game time. The product embeds no LLM or autonomous expansion loop.

Plugin, MCP, Contracts and Bridge.Core remain one version-coupled monorepo. [Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab) owns scenarios, operational handoffs and detailed evidence; product builds, tests and packaging are independent.

## Versions and scope

The final same-source 0.4.0 pair is built from `b7c0002dbe2e8f5608c5ca140b0c6326a1818021` for DSP `0.10.35.29151`. Frozen production acceptance remains source `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`. Later factual documentation commits do not change package or installed-source identity.

**Downloads (2026-10-17):** [GitHub v0.4.0](https://github.com/Arcueid77/Spherewright/releases/tag/v0.4.0) is published with the manual ZIP, Thunderstore-layout ZIP, checksums and generation manifest. Annotated tag `v0.4.0` identifies source `b7c0002dbe2e8f5608c5ca140b0c6326a1818021`. The [Thunderstore listing](https://thunderstore.io/c/dyson-sphere-program/p/Arcueid_77/Spherewright/) still has `0.3.3`; its `0.4.0` upload is pending. Use the GitHub manual package to install 0.4.0.

- **Overseer:** bounded production, power, logistics and research observations, upstream tracing and diagnosis. Recommendations grant no write authority.
- **Foundry:** multi-level recipe, material, machine, base-power and bounded site/dependency plans with explicit external supplies. Preview and execution remain separate.
- **Finite actions:** native construction, configuration, supported upgrades, procurement and limited blueprints, with per-object progress, cancellation and fresh continuation.
- **Governor:** pre-execution production declarations and game-tick observations. Inventory, rated capacity and short samples do not prove sustained production.
- **Save continuity:** owned, ticket-bound exact-primary resume with durable Journal continuity and separately confirmed disclosed migration. No arbitrary save picker or editor.

The frozen 29145 candidate passed its declared three-stage chain, continuous joint supply, preparation kit, save/recovery and both local package-layout checks. [Validation](docs/validation/v0.4.0.md) records its exact hashes, scope and limits. Those production results retain their original source/session scope. Interstellar flight/logistics remains future work.

## Install and use

Frozen production acceptance used Windows x64, DSP `0.10.35.29145`, BepInEx `5.4.17.0` and one owned peaceful/non-sandbox/1× local world. The final candidate targets the same local environment on DSP `0.10.35.29151`. Older releases retain their own support scope. Use matching Plugin, MCP and shared assemblies.

Both final b7 layouts, manual and standard Thunderstore, completed normal cold installation and same-version `29151→29151` default owned-primary recovery. Each independently passed one complete installed 64-tool matrix: 23 data successes, six prepare successes and 35 expected native guards; zero accepted/unknown matrix actions. Both recoveries retained durable Journal and completed history without another migration. The guards demonstrate callability while retaining their native safety gates; they do not mean every protected write executed. Final source-specific installation, recovery and matrix result audits passed independently. See the [fixed source-specific runtime report](https://github.com/Arcueid77/Spherewright-Lab/blob/74a4ec3d91608467911460eaf0d317800e60309b/operations/formal-release-2026-10-17.md).

The earlier two-layout callability, explicitly confirmed original-world `29145→29151` migration and subsequent same-version recovery remain evidenced for `e018b7f45c70f5fe029418cad987af794b5cb9bf` in the [fixed historical report](https://github.com/Arcueid77/Spherewright-Lab/blob/00f4359eef704bc7cd5c148438c05e0d3121bda6/operations/package-callability-2026-10-17.md); they are not a new b7 migration or production long window. See [native validation](docs/validation/native-29151-preflight.md).

A read-only [Nexus check](https://www.nexusmods.com/dysonsphereprogram/mods/23?tab=files) found its existing 0.3.3 primary file quarantined. The public page does not identify a detection engine or cause. No new 0.4 upload or Nexus/third-party scan approval is claimed; Nexus was inspected only.

- [Installation](docs/release-installation.md) and [interrupted-installation recovery](docs/installation-recovery.md)
- [Agent playbook](docs/agent-playbook.md), also embedded in MCP and included in packages
- [Protocol](docs/protocol.md), [safety model](docs/safety-model.md) and [architecture](docs/architecture.md)
- [Release notes](docs/release-notes/v0.4.0.md), [CHANGELOG](CHANGELOG.md) and [all documentation](docs/README.md)

Writes require fresh native prepare, plan-bound commit, original-action reconciliation and relevant readback. Unknown outcomes are quarantined. Item/technology injection, instant construction, teleportation and direct game-buffer writes are outside the public action surface. Dark Fog/combat, multiplayer/Nebula, broad third-party compatibility and another-computer use are unvalidated.

## Develop

```powershell
dotnet restore Spherewright.Core.slnf --locked-mode
dotnet build Spherewright.Core.slnf -c Release --no-restore
dotnet test Spherewright.Core.slnf -c Release --no-build
```

Full Plugin builds need legally installed DSP/BepInEx references; see [environment setup](docs/research/environment.md). Package scripts record this repository's own source SHA and clean state; they do not fetch or execute Lab.

Start scenario work from Lab; its fixed product submodule is read-only by default. Implement reusable fixes in an independent clone/worktree, test, commit and obtain successful exact-SHA CI before Lab updates its gitlink/lock. Read [contribution policy](CONTRIBUTING.md), [contributor rules](AGENTS.md), [product status](docs/project-status.md) and [Lab setup](docs/sample-validation.md).

Licensed under [MIT](LICENSE), with a [Chinese explanatory translation](LICENSE.zh-CN.md).
