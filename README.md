# Spherewright

[English](README.md) · [简体中文](README.zh-CN.md)

Spherewright connects an external Agent to **Dyson Sphere Program** through a matching MCP server and BepInEx Plugin. Structured observation, planning and bounded native actions retain normal materials, research, power, construction drones and game time. The product embeds no LLM or autonomous expansion loop.

Plugin, MCP, Contracts and Bridge.Core remain one version-coupled monorepo. [Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab) owns scenarios, operational handoffs and detailed evidence; product builds, tests and packaging are independent.

## Versions and scope

The latest published release is [v0.3.3](https://github.com/Arcueid77/Spherewright/releases/tag/v0.3.3). **0.4.0 remains unpublished and awaits owner publication review.** The current package-callability candidate is `e018b7f45c70f5fe029418cad987af794b5cb9bf`. The frozen production-acceptance candidate remains `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`; later documentation commits do not change either candidate's package or installed-source identity.

- **Overseer:** bounded production, power, logistics and research observations, upstream tracing and diagnosis. Recommendations grant no write authority.
- **Foundry:** multi-level recipe, material, machine, base-power and bounded site/dependency plans with explicit external supplies. Preview and execution remain separate.
- **Finite actions:** native construction, configuration, supported upgrades, procurement and limited blueprints, with per-object progress, cancellation and fresh continuation.
- **Governor:** pre-execution production declarations and game-tick observations. Inventory, rated capacity and short samples do not prove sustained production.
- **Save continuity:** owned, ticket-bound exact-primary resume with durable Journal continuity and separately confirmed disclosed migration. No arbitrary save picker or editor.

The frozen 29145 candidate passed its declared three-stage chain, continuous joint supply, preparation kit, save/recovery and both local package-layout checks. [Validation](docs/validation/v0.4.0.md) records its exact hashes, scope and limits. Those production results retain their original source/session scope. Interstellar flight/logistics remains future work.

## Install and use

Frozen production acceptance used Windows x64, DSP `0.10.35.29145`, BepInEx `5.4.17.0` and one owned peaceful/non-sandbox/1× local world. The current candidate was tested on the same local environment with DSP `0.10.35.29151`. Older releases retain their own support scope. Use matching Plugin, MCP and shared assemblies.

Both current package layouts independently passed all 64 installed MCP calls, including data/preparation success and expected native safety refusals, with no accepted or unknown matrix action. The owner-confirmed original-world migration and subsequent same-version recovery preserved durable Journal and completed history. See the [fixed runtime report](https://github.com/Arcueid77/Spherewright-Lab/blob/00f4359eef704bc7cd5c148438c05e0d3121bda6/operations/package-callability-2026-10-17.md) and [native validation](docs/validation/native-29151-preflight.md). This phase adds no new sustained-production acceptance.

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
