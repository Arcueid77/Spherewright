# Architecture

[English](architecture.md) | [简体中文](architecture.zh-CN.md)

Spherewright is a version-coupled product with four source projects. It exposes bounded normal-game operations to an external Agent; the Agent chooses goals and finite plans.

This document describes the maintained source. The accepted 0.4.0 candidate is fixed at `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`, the native baseline is `0.10.35.29145`, and the latest published release remains v0.3.3. Documentation changes do not update an installed Plugin/MCP cohort or alter the accepted candidate archives. See [product status](project-status.md) and [v0.4 validation](validation/v0.4.0.md).

The compatibility candidate targets `0.10.35.29151` with only the exact expired-primary direction and completed-history retention described in [native preflight](validation/native-29151-preflight.md). Its source tests and pending runtime validation do not change the frozen baseline above.

## Four product boundaries

| Project | Responsibility | Boundary |
| --- | --- | --- |
| `Spherewright.Contracts` | Wire DTOs, method names, error codes and versioned evidence fields | No game objects or game assemblies |
| `Spherewright.Bridge.Core` | Framing, authentication, bounded dispatch, snapshots, deterministic planning, fingerprints, idempotency and safety policies | Game-independent inputs and outputs |
| `Spherewright.Plugin` | BepInEx 5 adapter, local Named Pipe, native observation and normal-game execution | All DSP/Unity access stays on Unity's main thread |
| `Spherewright.Mcp` | MCP stdio tools, protected Bridge discovery and the embedded Agent playbook | No reference to game DLLs; no separate gameplay implementation |

The product builds, tests and packages independently of [Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7). Lab owns scenarios, current operational handoffs, game-writer declarations and detailed evidence. Its product submodule is a fixed dependency. Product code has no reverse dependency on Lab.

```text
External Agent chooses a finite goal
  -> read authenticated structured observations
  -> calculate or inspect an explicit bounded plan
  -> prepare one ordinary action or finite native module
  -> validate the exact returned plan and current authority
  -> commit once with the original idempotency identity
  -> poll the same action to terminal
  -> reconcile materials, entities and reciprocal connections
```

## Main-thread adapter and bounded transport

The Pipe worker parses bounded JSON envelopes into owned request DTOs. `SpherewrightBridgeHost.PumpMainThread` updates session identity and drains `BoundedMainThreadDispatcher` within configured item and frame limits. The defaults are four requests and a 2 ms dispatch budget per frame. An individual native operation is still bounded by its own input/scan limits; the frame budget is not preemption of a native call.

Results leave the main thread only as Spherewright-owned deep copies. Background workers never retain live `GameData`, factory, player or Unity objects. Pipe, descriptor and protected persistence use current-user access controls; authentication rotates at Plugin startup. See [protocol](protocol.md) and [safety model](safety-model.md).

## Ownership and durable evidence

Creation provenance or an explicitly confirmed adoption binds the exact owned world. An unrelated loaded session exposes restricted status and import availability without save/player/planet/factory content. Import prepares against the exact current process/session/revision/`GameData`, then requires subsequent conversation confirmation before a normal save creates a new owned copy. The original identity is never a public save selector.

The durable Gameplay Journal records events from its declared tracking boundary. Attached existing worlds keep incomplete historical coverage; missing earlier events are not reconstructed. Resume validates the original Journal checkpoint before and after adoption. Its immutable origin, effective native version and bounded durable version transitions remain distinct.

Protected resume credentials, pre-flight checkpoints, finite-build progress and Governor declarations have different lifecycles. None is an arbitrary save picker or an interchangeable persistence source. A restart restores eligible evidence, never old prepared-plan tokens. See [protected recovery](safety-model.md#protected-recovery-and-version-changes).

## Normal-game execution

`NormalGameActionCoordinator` owns prepare/commit execution, exact target and resource binding, action history, single-flight checks and outcome-unknown quarantine. The adapter uses native movement, harvesting, replicator queues, build tools, drones, configuration paths, transfers, upgrades and normal saves. It does not grant items, complete technology, inject energy, directly complete prebuilds, teleport, edit saves or bypass native costs.

Movement planning remains outside the Plugin. `prepare_move` validates a finite target and optional surface evidence; it does not prove a collision-free corridor. The 180/600-game-tick movement watchdogs stop only the exact owned order and return bounded recovery guidance. Same-star flight uses native Fly/Sail transitions and paid energy after a separately verified internal checkpoint.

The historical sandbox basic-line coordinator is excluded from Plugin compilation and public MCP registration. Its direct grants/completion paths must not be revived as a shortcut.

## Read-only diagnostics and planning

Overseer captures production and power/logistics/research domains as bounded copied snapshots. The diagnostic bundle captures both in one dispatched main-thread work item and admits a joined planet only when factory identity, planet, runtime flags and game tick match exactly. Its public allowlist DTO contains no save identity, credential or filesystem path. Immutable continuation pages do not turn later independent reads into a transaction.

Foundry compiles unlocked native recipes into deterministic material demand, machine counts and explicit byproducts. Optional machine-grid or explicit-blueprint site assessments add native placement, inventory, power and rated transport evidence. These reads remain `executable=false`. A machine-only grid does not price or prove a complete logistics network; rated channel capacity does not prove measured throughput or sustained supply.

Governor compares measured deficits and upgrade/new-machine/module alternatives. It keeps planet production, demand, local surplus, buffer deltas and nameplate gains separate. Its bounded passive observers and immutable pre-execution declarations can measure a qualifying throughput window; Governor never starts another expansion, supplies a new goal or certifies overall balance automatically. See [Governor protocol](protocol.md#governor-proposal-04-development-slice).

## Finite module data and upgrade execution

Blueprint inspection/export accepts explicit bounded data or selected owned entities and remains non-executable. The separate finite executor reuses ordinary session/revision/`GameData` binding, native blueprint geometry, plan/idempotency stores and quarantine.

Core `BlueprintBuildState` holds immutable geometry, directed dependencies and per-object receipts. Plugin submits at most one dependency-ready object per game tick through native build tools, pays normal material costs and waits for drones. `BlueprintBuildStore` atomically flushes write-ahead and result evidence per owned identity. Partial completion is visible. Cancellation stops future submissions and retains existing work.

Restart reconciliation proves known entities, material receipts, configuration and reciprocal/free-end topology before a fresh prepare continues only the unsubmitted suffix. It never replays a whole partially completed module. Foundry-backed composition binds the original material intent, full explicit costs, transport allocations and power to that same executor; it does not create a second planning loop.

Native upgrade execution calls `DoUpgradeObject` for the advertised subset and proves complete new-device cost, old-device refund, identity/configuration, cargo, topology and native timing immediately. It never writes prototype IDs directly. Increased rated speed remains separate from Governor's measured acceptance.

## Validation deployment

Source capability, offline test results, installed cohort and live acceptance are separate facts. Rebuilding MCP is required for an installed binary to receive an updated embedded playbook; editing this file or the source playbook does not hot-reload it. The stable resource URI is `spherewright://agent/playbooks/opening-movement-v1` and its logical resource name is `Spherewright.Mcp.agent-playbook.md`.

Current acceptance evidence is summarized in [validation](validation/v0.4.0.md). The separate same-LAN game-validation-computer design remains a deferred architecture, with the Pipe local to that computer and an authenticated remote command channel for deployment/MCP/evidence. See [the retained proposal](remote-validation.md); it requires its own explicit scope.

Earlier architecture and operational details are available at the [accepted source](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/architecture.md) and the [fixed Lab reference](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7). Historical success is evidence, never a replay plan or new execution authority.
