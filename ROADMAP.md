# Spherewright 0.3–1.0 Roadmap

[English](ROADMAP.md) | [简体中文](ROADMAP.zh-CN.md)

This roadmap defines the approved capability sequence, acceptance gates and release rules from the historical Post-M0 baseline to `1.0.0`. It does not grant gameplay or publication authority. Product contribution rules are in [AGENTS.md](AGENTS.md); scenario execution begins in Spherewright-Lab under its current scope.

## Version anchors and current status

The accepted local `0.4.0` candidate is source `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`, validated on Windows x64 with DSP `0.10.35.29145` and BepInEx `5.4.17.0`. See [product status](docs/project-status.md) and the [final validation summary](docs/validation/v0.4.0.md) for the exact evidence and limits. The latest published version remains `v0.3.3`; no `v0.4.0` tag, GitHub Release or Thunderstore publication exists from this work. The accepted candidate archives are frozen. This documentation maintenance creates no replacement package, installation or live acceptance.

- `0.1`: M0 First Red Matrix; safe Bridge, basic MCP primitives and the first automated energy-matrix line.
- `0.2`: historical Post-M0 expansion; structure-matrix automation, same-system checkpoint flight, recovery tickets, Journal and write-proof hardening. These capability anchors do not invent unpublished tags or Releases.
- `v0.3.0` is the first formal release under this roadmap. Every subsequent `0.x.0` must deliver an independently installable and accepted capability slice.
- `0.x.y` is for fixes, compatibility, diagnostics, documentation and release corrections, without a new major gameplay action domain. Owner-approved `v0.3.1` is the narrow authorized-import backport; `v0.3.2` fixes naming and packaging; `v0.3.3` removes sandbox/resource-multiplier runtime gates without enabling sandbox actions.

The owner's 2026-09-05 decision combined all former Foundry `0.5` and Governor `0.6` scope and gates into `0.4.0` with Overseer, preparing for interstellar expansion. Voyager, Ascension and Release Candidate moved forward; published `0.3.x` and the `1.0.0` promotion point did not change.

| Version | Approved scope | Original position |
| --- | --- | --- |
| `0.4.0` | Overseer, Foundry & Governor: diagnostics, construction, balancing, expansion and departure preparation | Former `0.4` + `0.5` + `0.6` |
| `0.5.0` | Voyager: actual interstellar travel and logistics | Former `0.7` |
| `0.6.0` | Ascension: Dyson design, launch and power | Former `0.8` |
| `0.7.0` | Release Candidate: public-surface freeze, release engineering and end-to-end acceptance | Former `0.9` |
| `1.0.0` | Promote the same artifacts as the final qualifying `0.7.x` | Unchanged |

This is the current numbering. Old version numbers and pending statements remain historical in the [immutable previous roadmap](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/ROADMAP.md) and [Lab roadmap archive](https://github.com/Arcueid77/Spherewright-Lab/blob/14478a03c64c550e644c559405c8496b647552e7/archive/roadmap-history/ROADMAP-HISTORY-2026-09-24.md). They are not additional releases, current blockers or replay instructions.

## Development and release rules

- The approved development model uses reviewable, reversible, single-purpose commits on `main`, without long-lived release branches. Temporary independent worktrees/review branches follow current contributor rules. Pushes, merges and publication require their applicable authorization; old operational permissions are not standing release authority.
- Before a new `0.x.0` gameplay phase begins, update the current Lab phase rules in a separate reviewed change, naming permitted write families and retained prohibitions. This roadmap alone never authorizes those actions. The [imported historical rules](https://github.com/Arcueid77/Spherewright-Lab/blob/14478a03c64c550e644c559405c8496b647552e7/operations/agent-rules-imported.md) preserve provenance, not current execution scope.
- New DSP fields, methods and call paths require the exact supported `Assembly-CSharp.dll` as evidence. Record signatures, hashes, reasons and readback invariants in [research](docs/research/game-api-m0.md).
- All gameplay writes retain Unity-main-thread access, `inspect → prepare → commit → readback`, short plans, identity/session binding, idempotency, conservation and outcome-unknown quarantine.
- Every complete release supplies self-contained artifacts requiring neither source nor the .NET SDK. From `0.3`, the minimum is a versioned ZIP, manifest and installation instructions/script; checksums, changelog, upgrades and rollback continue to mature.
- Acceptance includes automated tests, native evidence on the supported DSP version, ordinary save/recovery, experience-ledger review and updated user documentation.
- After all gates pass and the final commit is pushed to `main`, present the candidate commit, tests/native evidence, artifact hashes and release notes to the owner. Only explicit approval permits an annotated `v0.x.0` tag and matching GitHub Release. Never release a dirty/unpushed tree or reuse another version's publication permission.
- Release records include the commit, DSP/BepInEx support, protocol/tool changes, install/upgrade steps, limits, tests and sanitized native evidence. Thunderstore and other registries require separate explicit authorization.

## Constraints throughout this roadmap

The `0.3–1.0` sequence excludes built-in LLMs, autonomous goal planners/long-running loops, Computer Use, screenshot recognition, input macros, external-memory control, item/energy/technology/buffer injection, teleportation, instant construction, save editing, game-speed changes, combat/Dark Fog, multiplayer/Nebula, non-Windows support promises and broad third-party Mod compatibility promises.

Arbitrary user saves are never enumerated, selected or actively loaded. The narrow import boundary is a world the player already loaded manually: effect-free prepare, subsequent explicit conversation consent, then normal saving to a new internally named owned copy. Unbounded construction and native-rule bypass remain excluded; bounded, individually verifiable blueprint reuse is allowed from `0.4`.

The external Agent chooses goals and actions. Spherewright supplies structured observations, deterministic helpers, safe primitives and verifiable outcomes.

## 0.3.0 — Logistics Towers

Status: **released**, 2026-09-03, tag/Release `v0.3.0`, commit `a52ff44`.

### Goal and scope

Use normal technology, items, power and transport to supply remote titanium and silicon to the home structure-matrix chain, ending manual interplanetary hauling and temporary stone-to-silicon dependence.

- Research the native `PlanetTransport`, station and carrier paths; observe slots, caps, local/remote supply and demand, inventories, drones/vessels, charging, transport and errors.
- Configure legitimate slots, modes, limits and transport/power parameters through separate two-phase actions.
- Load existing items and fleets only through supported normal transfers, belts and sorters, without direct station-cargo/buffer writes.
- Limit stellar stations to planets of the same star system; observing warper fields does not enable interstellar warp routes.
- Complete the prepared particle-container, logistics-drone and planetary-station milestones, then a two-planet transport loop.
- Supply the first self-contained versioned ZIP, manifest and installation path.

### Acceptance gates

- Normally constructed, powered stellar stations on two planets; vessels actually carry titanium and silicon from currently observed corresponding veins.
- Sustained structure-matrix production without manual hauling; safely retire the temporary silicon line or retain it as backup.
- Prepare/commit, idempotency, paired inventory/configuration readback and conservation for every station write.
- Configuration and transport survive normal save, close and recovery.
- Core/Contracts/MCP tests, full solution build and supported-DSP native regression pass; the release package starts and completes an MCP handshake in a clean supported environment.

## 0.3.1 — Authorized Save Import

Status: **released**, first published 2026-09-04 and promoted from prerelease on 2026-09-05; tag `v0.3.1`, commit `33a733f`.

This owner-approved narrow patch backports import from immutable `v0.3.0` without unfinished Overseer work. Historical package/test details remain in the immutable roadmap; later local `v0.3.3` import evidence does not become an independent other-computer `v0.3.1` claim.

### Scope and gates

- The player loads the world manually. Prepare exposes no world content or gameplay side effects, binds the restricted process/session/revision/exact `GameData`, and discloses preservation of the original, a new owned copy and Journal coverage starting at import.
- A later explicit user message permits commit with `userConfirmedInConversation=true` and both boundary acknowledgements. This is the caller's declaration, not a shortcut code or claimed access to chat history.
- The short one-use plan saves normally to a server-generated high-entropy name; no input save name/path, enumeration, original load/overwrite/rename/delete is accepted.
- World/revision changes, expiry or a save attempt invalidate reuse. Ownership requires save success and exact header/tick readback; uncertain results remain restricted and fail closed.
- All owned-world write, recovery, quarantine and privacy rules remain. Do not fabricate pre-import first events.
- Automated coverage includes confirmation, boundaries, single use/expiry, session/object binding, idempotency, failed adoption and privacy. Native validation proves the original header unchanged, the copy save/recovery and restriction when returning to the original.

## 0.3.2 — New World Naming and Opening Recovery

Status: **released**, first published 2026-09-04 and promoted on 2026-09-05; tag `v0.3.2`, commit `da11e44`.

This fix separates Agent-created and imported-copy names, preserves exact legacy `Spherewright_M0_*` recovery, and packages bounded movement recovery around the landing base/local obstacles. The authorized GitHub and Thunderstore artifacts share one tag. Two isolated package black-box new-world trials completed automated electromagnetic matrices and normal saves; their [comparison](https://github.com/Arcueid77/Spherewright-Lab/blob/14478a03c64c550e644c559405c8496b647552e7/scenarios/v0.3.2-black-box/comparison.md) retains Agent paths, timing and API-equivalent cost.

## 0.3.3 — Gameplay Mode Compatibility

Status: **released**, 2026-09-05, tag/Release `v0.3.3`; latest formal release.

New worlds remain peaceful/non-sandbox/1×. Existing or imported peaceful worlds report sandbox/resource settings, which no longer gate ordinary actions, import, planned-restart resume or flight-checkpoint recovery. Sandbox tools and injection remain absent. Local release-Plugin trials covered a new non-sandbox/1× world and imported non-sandbox/100× and sandbox/1× worlds, using normal gathering, crafting, research rewards, drone construction and saving while preserving both external originals. This is local compatibility evidence, not independent other-computer validation.

## 0.4.0 — Overseer, Foundry & Governor

Status: **accepted local candidate; publication pending**. The [final validation summary](docs/validation/v0.4.0.md) records the accepted source, frozen package hashes and evidence limits. Earlier dated pending/deployment notes in the immutable roadmap are superseded only within the scope of later evidence. Do not repeat accepted construction or convert a narrow positive into universal coverage.

### Goal

Create a sustainable, diagnosable, expandable and recoverable industrial base in the departure system. Overseer identifies problems; Foundry compiles item/rate intent into inspectable finite construction; Governor balances and expands existing lines, power and sources. All three form one release. Complete key technology/mecha upgrades, warper/fuel replenishment, transport and expedition supplies. Actual interstellar travel, warp routes and cross-star dispatch stay in `0.5.0`.

### Overseer scope and gates

- Observe multi-planet production, power, logistics and research; define measured/theoretical rates, utilization and game-time windows.
- Distinguish input shortage, blocked output, insufficient power, logistics blockage and exhausted veins; trace supported physical upstream equipment and edges.
- Produce diagnostic bundles with existing privacy rules, excluding credentials, plan tokens, private paths and raw save identity. Protect persistent window state using per-save/Journal patterns.
- Retain the explicit player-loaded import/onboarding boundary with `v0.3.1` safety semantics.
- Under controlled conditions distinguish material, power and logistics faults; use diagnostics to repair at least one, then clear all three. Restore stable nonzero production and finish with healthy normal saving.
- Preserve window semantics across save/recovery without counting offline time, pause or observation gaps as throughput. The stationary-carrier 600-tick live positive remains an explicit coverage limit, not an excuse to synthesize game state.

### Foundry scope and gates

- Parameterized line plans, candidate sites, native terrain/collision/power/logistics checks and complete material budgets, using runtime recipes/dependency graphs and actual machine speeds. Name external supply and coproduct sinks explicitly.
- An immutable finite action graph reuses existing safety and researched native blueprint/construction paths. Bounded batch prepare/commit and per-frame work are permitted; the model need not choose every belt.
- Accept explicit user blueprint codes or explicitly selected current-owned buildings; support generation/export, bound input/decompression/object counts, and parse recipes, links, I/O and costs. Reject unsupported content; metadata is data. No arbitrary-file scan.
- Check full materials, technology, native placement and external connections at the chosen pose. Aggregate/per-object outcomes distinguish unsubmitted, pending, completed, blocked and unknown, binding blueprint hash, identity, site, budget and idempotency.
- Partial success never replays the whole graph. Cancel stops unexecuted work. Protected owned-bound progress survives restart; fresh inspection/prepare and actual-entity receipt reconciliation replace old tokens.
- Plan generation, external selection and execution remain distinct. Replan on site changes. Belt/sorter lines can use existing primitives; delivery-based plans additionally require independent distributor add-on/configuration and conserved-courier primitives, rather than making these a prerequisite for every Foundry plan.
- Generate a complete equipment/power/logistics/cost plan for at least one three-stage chain, then build with normal materials and drones. Save/restart mid-plan and continue without duplicate entities, charges or unexplained topology.
- Demonstrate a running-module copy with native costs, settings, links and sustained output. Cover illegal/unsupported inputs, shortage, occupancy, stale plans, idempotency, partial success, cancel and deduplicated restart continuation. Preview/offline evidence alone cannot pass these gates.
- Regress Overseer, owned save/import/resume, Journal and prior actions before release. Retain the original separation between implementation and independent long-lived-world construction/continuation validation.

### Governor scope and gates

- Analyze steady state and bottlenecks with the shared runtime recipe graph; calculate machine counts, transport, power and theoretical/measured rates. Propose expansion, power, logistics and source replacement.
- Prioritize native same-family in-place upgrades of production equipment, belts and sorters, one entity per prepare/commit and only when that family is implemented and proven. Never assign `protoId`; verify debit/refund, recipe/filter, cargo and reciprocal links without assuming ID stability. The accepted executable subset is ordinary manufacturing assemblers and basic `2011 → 2012` sorters; belt/advanced-sorter upgrade research does not enable those families.
- Compare upgrade, new-build and module-copy costs/benefits. Separate target demand, actual production/consumption, allocatable supply, stock delta and deficit; equal shortage-limited production/consumption is not balance.
- Foundry plans new ground; Governor replans existing production using the same sizing helpers. External Agents select proposals and execute through finite Foundry/primitives, without an autonomous expansion loop.
- Declare baseline, target, tolerance and steady-state window before action. Raise a chosen line to twice its fresh stable nonzero measured baseline and sustain it within the declared tolerance for at least ten game minutes.
- Throughout that window require no grid instability, logistics deadlock, unexplained resource change or write quarantine. Changed conditions must yield an explainable new proposal.
- Native-upgrade at least one existing device, preserve materials/cargo/connections and prove save/recovery. Recheck upstream, power and logistics after copying; doubling modules is not doubling throughput. Theoretical capacity cannot replace measurement or justify loosening tolerance afterward.

### Departure preparation and whole-version gates

Use genuine expedition demand for three-stage construction and expansion. One line may supply several proofs, but costs, actual rate, mid-plan recovery and departure readiness each need evidence.

- Complete native research/mecha/logistics prerequisites from the current runtime catalog, without assumed recipes or direct unlocks.
- Sustain automatic warper and chosen conventional-fuel replenishment; normally produce stations, vessels, remote mining, power and transport materials. Handcrafting may bootstrap or fill gaps, never replace the sustained key-consumable lines.
- The external Agent selects a preparation kit and target quantities/rates. Record source, stock, measured replenishment, power/transport margin and deficit; close each against fresh readback and predeclared game-time windows.
- Pass Overseer, Foundry, Governor and kit gates together, normally save the same owned world, and prove recovery, Journal continuity and healthy writes. Build complete same-source packages for owner review; earlier diagnostic packages or material drafts do not substitute.
- Preparation covers the departure system only, without unseen-system resource disclosure. Actual round trips, target-system extraction and interstellar routes belong to `0.5.0`.

The accepted candidate retains separate mid-plan three-stage and whole-kit recovery proofs plus a final contiguous 36060-game-tick joint-supply window. Those retained proofs satisfy the accepted local candidate within their recorded scope; they do not establish all Mods, machines, worlds or computers.

## 0.5.0 — Voyager

Status: **planned**, moved intact from former `0.7.0`.

### Goal and scope

Complete interstellar travel/logistics under normal exploration, energy and warper rules. Catalogs obey native exploration visibility without leaking unseen veins. Plan routes, distance, energy, warpers and return budgets; expose launch, flight, warp, braking, landing, replenishment and recovery stages. A pre-flight checkpoint binds exactly one flight and loses rollback authority after success/covering primary save. Observe/configure cross-star stations, vessels and warpers. Reuse accepted finite construction, upgrades and balancing for remote modules.

### Acceptance gates

- Normally travel to another star and return safely; bring back a resource missing from the departure system.
- Establish and prove at least one sustained interstellar logistics route.
- Cause one recoverable flight failure and recover only its exact checkpoint; never roll back a world covered by a newer primary save.

Reuse the `0.3` station and existing checkpoint primitives while separately implementing/researching current-assembly interstellar routes, budgets and warp behavior.

## 0.6.0 — Ascension

Status: **planned**, moved intact from former `0.8.0`.

### Goal and scope

Build a Dyson system with sustainable launches and power through normal industry and structured design. Observe/configure late technology, solar sails, rockets, ejectors, silos and ray receivers; inspect swarm/orbits/nodes/frames/shells. New native-proven orbit/node/frame design writes require prepare/commit, idempotency, readback and safety-matrix validation. Deterministic legality/cost/progress helpers support external design decisions and long-window launch, shortage, power and progress observation.

### Acceptance gates

- Normally manufacture and launch the first sail and rocket; prove real node/frame/shell progress by readback.
- Sustain nonzero ray-receiver power under normal conditions.
- Preserve design, launch state and progress across save, restart and long operation.

## 0.7.0 — Release Candidate

Status: **planned**, moved intact from former `0.9.0`.

### Goal and scope

Freeze the public surface for `1.0.0` and complete reproducible distribution, upgrade, rollback and a new-world-to-Dyson-power proof. No new major action domain. Freeze MCP/protocol/errors/compatibility, version handshake, deprecation and migration rules, and fail-closed unsupported-DSP handling. Supply Thunderstore-compatible layout and GitHub installation/upgrade/uninstall/rollback artifacts; atomically deploy the same-batch Plugin/Core/Contracts set, addressing partial replacement and `TypeLoadException`. Retain self-contained MCP, manifest, checksums, changelog, diagnostics, clear user/developer dependencies, normal/abnormal exit, offline recovery, performance and endurance regressions.

### Acceptance gates

- Use the exact final artifacts intended for `1.0.0` from a fresh owned world to sustained Dyson power, retaining complete sanitized evidence.
- Install, upgrade, save and recover in a second independent owned world; cover repeated normal exits, one abnormal exit, long offline recovery and multi-day operation with one coherent evidence chain.
- Automatically prove no MCP/protocol/error-code diff between the final `0.7.x` and `1.0.0`.
- No known P0/P1 remains in the public defect list; state this in release notes. README, AGENTS, CHANGELOG, LICENSE, installation and metadata agree.
- End users need neither source nor SDK; installation, upgrade, uninstall and failure rollback are reproducible.

## 1.0.0 promotion rule

Promote only the same artifacts that pass every `0.7` gate. During promotion, fix only release blockers. Other compatibility fixes belong in `1.0.x`; new capabilities require a subsequent roadmap.
