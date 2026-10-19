# Safety model

[English](safety-model.md) | [简体中文](safety-model.zh-CN.md)

Spherewright exposes bounded normal-game capabilities. A capability, a positive preview, an accepted local candidate and permission to execute are separate facts. This document grants no gameplay, Save/Load, recovery/migration, installation, deployment or publication authority. Game work starts in the current [Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab) checkout after reading its current [AGENTS.md](https://github.com/Arcueid77/Spherewright-Lab/blob/main/AGENTS.md) and [active handoff](https://github.com/Arcueid77/Spherewright-Lab/blob/main/operations/active-handoff.md).

The maintained compatibility candidate targets `0.10.35.29151`; see [offline native evidence and pending live checks](validation/native-29151-preflight.md). The accepted 0.4.0 candidate remains fixed at `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`; latest published release is v0.3.3. A documentation update does not install a new cohort or republish that candidate. See [product status](project-status.md).

## Native execution and transport

- All DSP/Unity reads and writes run on Unity's main thread. Workers receive only owned deep-copy DTOs. Native object references never cross that boundary.
- The local Named Pipe and runtime descriptor require current-user ACL protection. The startup credential rotates, authentication is required before requests, frame size and queues are bounded, and invalid pre-authentication traffic closes the connection.
- `AllowWrites` defaults to `false`. Enabling it does not grant a goal or bypass prepare, native validation or an operational finite budget.
- Gameplay execution and acceptance use authenticated structured state. Computer Use, screenshots-as-state, visual recognition and keyboard/mouse macros are forbidden.
- No item grants, inventory/storage/buffer/energy injection, technology-progress writes, direct prebuild completion, teleportation, game-speed changes, arbitrary save edits or safety bypasses are allowed. Real ingredients, range, inventory/refund capacity, energy, technology, construction drones, power and elapsed game ticks remain game costs.

Native configuration may use an explicitly verified UI-equivalent assignment path, such as a sorter filter or station charge setting. This narrow path is not permission to assign arbitrary game fields. Refuelling transfers owned native fuel and proves count/inc conservation. Fleet transfer moves only owned `5001`/`5002` to/from the matching idle fleet counter, preserving working craft, capacity, station stock/energy/warpers, other fleets and player inventory. It never creates craft.

## Prepare, commit and one writer

One session has one designated game writer. Before handoff the original executor must be stopped, with no in-flight action or unreconciled result. A Pipe reconnect, model turn, context compaction or host restart does not reset accepted counts or authorize a second writer.

Every mutation requires a fresh prepare, validation of its exact plan, one idempotent commit and terminal/readback reconciliation. Plans bind the bridge/session/revision/planet, exact native identity, action kind, parameters, target/resource hashes and budget. A plan is short-lived and process-local; restart never restores it.

Use the hash domain required by the action: the player action hash, stable `endpointStateHash` for build endpoints, root `configurationStateHash` for sorter-filter, full `stateHash` for storage-capacity, station-specific configuration hash, or `selectionStateHash` for research selection. Do not rewrite hashes or substitute a convenient one.

Commit rechecks preconditions on the main thread. Active player-order actions cannot replace one another; active finite construction excludes competing normal actions. Idempotency is scoped to `(sessionId, idempotencyKey)`: during the configured retention window the same request/key returns the original result across Pipe reconnects; a changed request with that key conflicts. Defaults are 30 minutes and 1024 entries per session, not an indefinite durable replay guarantee.

## Unknown results and reconciliation

A timeout, lost response or caller exception after possible acceptance is not proof that zero writes occurred. Freeze new writes; retain the original request/key/action identity, reconcile its result and never guess-replay with a new key. A successful terminal remains successful when a later observer or readiness assertion fails.

If a started mutation cannot be proved as before or expected-after, write health becomes `quarantined`. In-process quarantine reconciliation is limited to the exact retained unknown build and requires conserved cost, uniquely attributable entities/components and complete directed topology. It repeats no mutation. Unavailable proof does not permit clearing a flag, fabricating a receipt, resetting the session or loading an older save to conceal work.

Finite blueprints retain per-object states and write-ahead receipts. Only unsubmitted objects may continue after fresh identity/configuration/material/topology reconciliation. Cancellation retains submitted prebuilds and entities; it does not roll back, refund or prove atomic whole-module success. An unknown object/result blocks replay and quarantines writes.

Native upgrades use only the advertised assembler/basic-sorter subset, consume a complete higher-grade device and refund the old device through `DoUpgradeObject`. Immediate cargo/inc, configuration, links, timing and inventory proof is mandatory. The implementation never directly writes a prototype ID; an uncertain result is not automatically rolled back or retried.

## Observation is bounded evidence

A missing/null/unavailable value is unknown, never zero, free capacity, a free port or permission to execute. Complete the relevant immutable pages before declaring absence. Do not combine different ticks or factories as a single atomic observation.

Belt `buffers=[]` is not empty-cargo proof. Detail-only segment cargo can overlap adjacent segments; a material cut counts each full path once. Research matrix points, fuel energy and electrical J/t are not transferable item counts. A tank scalar and its fluid buffer describe the same stock.

Endpoint/configuration/geometry hashes have distinct scopes. Dynamic fuel, cargo or output changes are not automatically topology changes. Conversely a positive geometry preview does not prove inventory, power, a free endpoint, automatic supply, actual future entity IDs or sustained throughput.

Read-only Foundry, Governor, blueprint, endpoint and route previews stay non-executable. In particular, tokenless build previews require `prepared=false`, `commitAllowedNow=false` and `executable=false`. MCP withholds missing/downgraded echoes; callers must not turn an old-cohort ordinary token into a preview or submit the preview to a commit.

## Movement and finite recovery

Move validates a finite surface target, not a collision-free path. Powered movement fails after less than 0.75 m displacement over 180 game ticks or less than 1 m improvement over 600 ticks. Continuous power starvation has a separate 600-tick grace window. Harvest approach uses the same bounded progress discipline; stationary productive mining is not a movement stall.

Only the exact owned native order is aborted. Recovery uses fresh structured obstacles, one bounded short escape or at most four orthogonal probes, each with its own fresh prepare/commit/terminal checks. No unchanged-target replay, internal pathfinder, teleport or injected recovery fuel is allowed. Arrival must be proved by the required native movement state, not proximity alone.

Optional `allowPassiveDrift` remains Move-only and default-false. It uses the latest trusted external inspection, idle alive/on-planet Drift, speed ≤0.15 m/s, unchanged exact core capacity and existing hashed state, ≤0.05 m displacement within two monotonic seconds. It never renews the baseline; another external player read invalidates it. Native surface, energy, order, stall and dry-Walk proof remain required. This admission option is separate from the bounded runtime Walk/Drift branch below.

Voyager local Move/Harvest reuse ordinary RTS and keep the no-water fast path. Their finite-cost branch admits bounded runtime Walk/Drift, with no manual input/inertia, queued order, pending jump, navigation, fast travel, warp or Fly. Exact owned binding and the Native pre-player hook remain mandatory. Horizontal, thrust and background energy cover finite work plus 13 paid Native game ticks of stopping; original core, loaded-return, native-warper, UTC/tick and observation-lease reserves remain protected. The Move/Harvest stop bound is the earlier of 13 paid game ticks and an independent one-second wall deadline. UTC admission leaves at least one second for cleanup; the wall bound is a patience deadline, not a physical-stop theorem or 60 paid ticks. Original action/journey deadlines never reopen. Normal success requires actual dry Walk and true Native zero-speed, not RTS zero or Pause. This is a finite-cost replacement for the ocean exclusion in that branch, not a sampled/global dry-geometry certificate. `nativePhysics` remains read-only with safetyCertified=false and does not select or authorize the branch.

After a successful remote landing, the same original journey keeps guarding idle energy and its original return clocks until a budgeted local action, controlled construction or the actual return flight takes over. Loss of idle Walk, cost or original return-clock evidence takes the exact-owned Pause/quarantine path. A previously successful flight or Move remains successful; subsequent idle protection does not rewrite its result. Eligibility is not rebuilt silently if Host unload or default Save reload loses the in-memory landed-controller binding: pause/quarantine only the same owned world and reject local/structured work, retaining checkpoint and confirmation gates. This subset covers outbound, local work and normal return within one Host. Arrange logistics save/recovery continuation at home after the journey closes; remote in-journey cold recovery is unverified.

On a guard failure, expired stop/drain bound or lost caller/host observation, the finite-cost fail-safe calls normal GameMain.Pause only while the original owned binding still matches. The current LogicFrame may still finish one whole player-and-drone tick, so that complete tick must remain funded; only subsequent Native gameplay energy/dispatch ticks freeze. Pause proves neither physical idle nor successful drain. Preserve unresolved outcome_unknown, quarantine and the exact original action/build/checkpoint; do not auto-resume, load or replay. Never pause a replacement world. True Host unload pauses the exact owned protected world before removing its hooks. An ordinary MCP disconnect leaves the DSP Plugin resident and its lease checks running. Original wall deadlines do not renew while paused. New-source full Plugin/regression, exact-SHA CI, Lab lock/adoption, cold deployment and live checks remain required before runtime credit; this adds no public tool or protocol version.

Controlled construction still holds Walk at its fixed anchor and retains its separate Native drone-tail budget. Drift/anchor loss stops new dispatch and takes Pause/unknown, without claiming drone drain. Known normal drain still requires the exact subsequent Native factory pass, empty original prelaunch cache and all-owned-drones-idle evidence. This maintained-source adaptation still needs full Plugin/regressions, exact-SHA CI, Lab adoption, cold installation and live qualification; historical 2/3 acceptance and the 72-tool/1-resource surface do not change.

## Ownership, creation, import and Journal

Factory/save reads and ordinary writes require the exact world created or explicitly adopted by Spherewright. An unrelated loaded session reveals only opaque restricted session metadata and import availability.

New-game creation uses DSP's official path with peaceful mode, 1× resources and sandbox disabled. Missing mode evidence or a non-peaceful world fails closed. Runtime combat/sandbox/resource settings stay explicit; sandbox and resource multiplier do not independently gate ordinary commits or expand capabilities.

Import is only for the player's already loaded unowned world. Prepare captures the exact process/session/revision/`GameData` without exposing world content. A subsequent explicit reply to the disclosure is required before commit attests to confirmation and both original-save/Journal boundaries. Native save creates a new internal copy, exact header tick is proved, then ownership is adopted. No public request accepts, enumerates, actively loads, overwrites, renames or deletes the original save. A true save return without exact header proof quarantines that loaded session.

The current-user-protected Gameplay Journal has a save-derived internal key, a declared tracking boundary, continuous sequences, durable checkpoint and explicit pending/error state. `attached_existing_save` keeps `historicalCoverageComplete=false`; absence of an earlier event is not evidence it never happened. Preserve the original entry prefix; never backfill timestamps or force a new event for every action. Resume must prove the same original Journal and minimum durable sequence before loading and after adoption. Legacy credentials remain compatible only through their supported path and acquire the stronger checkpoint on a later healthy save.

<a id="protected-recovery-and-version-changes"></a>
## Protected recovery and version changes

A protected capability is not a save picker. Never edit a ticket, expiry, identity, Journal or source file to qualify it. A stale primary cannot silently discard known newer accepted work. Select only the explicitly authorized flow and exact current candidate:

| Mode | Required boundary |
| --- | --- |
| `default` | Valid protected credential; exact version/identity/Journal and internally selected eligible primary or quarantine source; no arbitrary source or caller tick override |
| `verified_newer_lastexit` | Explicit authorized exact newer candidate and known progress floor; healthy Journal-bearing provenance; evidence 1 and exact identity/tick echoes |
| `reauthorize_expired_primary` | Expired unconsumed healthy provenance; exact recorded primary tick, original Journal, evidence 2 and a subsequent explicit reply to the prepared disclosure/digest |
| `reauthorize_expired_autosave0` | Expired healthy provenance; only fixed native AutoSave0, approved floor/exact tick, same supported source/runtime/candidate version, evidence 3 and subsequent confirmation |
| `reauthorize_fixed_autosave0` | Unchanged active healthy provenance; the same fixed-source/tick/version bounds, evidence 4 and an exact matching explicit conversation grant |
| `reauthorize_version_changed_primary` | Unchanged active healthy primary; only `29104→29131` (evidence 5) or `29131→29145` (evidence 6), exact recorded tick and subsequent pair-specific confirmation |

AutoSave0 modes require a candidate strictly newer than the credential and any observed primary. There is no fallback to another autosave, primary or LastExit. Prepare in reauthorization modes does not accept pre-asserted confirmation. Its exact mode, evidence version, identity, candidate, source/target, prompt/digest and disallowed-commit echoes must match. An earlier generic “continue” does not authorize a different candidate.

Expired-primary reauthorization permits exact same-version adoption or these researched directional native migrations: `0.10.34.28529→0.10.35.29057`, `0.10.34.28529→0.10.35.29088`, `0.10.35.29088→0.10.35.29104`, `0.10.35.29104→0.10.35.29131` `0.10.35.29131→0.10.35.29145` and `0.10.35.29145→0.10.35.29151`. Only the expired-primary mode adds the last pair; active-primary evidence 5/6 stays unchanged. Default resume still requires matching versions. Skipped links, unknown pairs and downgrades reject.

Journal origin/history remain unchanged. At most five durable researched transitions are permitted; each must continue the effective source version with monotonic adoption tick and durable sequence. For the full researched patch chain, each intermediate link must exist. Ticket, primary and Journal effective version must agree with the allowed source/runtime pair.

Default resume binds the protected ticket and menu revision. Source selection checks the eligible internal primary or fixed LastExit header tick and file-modification freshness, and commit rechecks the ticket and source selection. When present, its protected Journal checkpoint must validate before loading and after adoption; the existing legacy-ticket compatibility remains limited to its supported path. Adoption must still prove embedded owned identity, expected planet, peaceful mode and the minimum tick. Default resume does not compute a complete source-file fingerprint or hold a source-file read-only lease through adoption.

Only `verified_newer_lastexit` and the reauthorization modes bind bounded full-file evidence and hold source-file read-only leases through adoption; fixed-AutoSave0 reauthorization also leases the original primary. Reauthorization additionally opens a separate read-only lease on the original Journal. Commit revalidates those mode-specific proofs, and reauthorization durably writes attempt/consumption evidence before native loading. A crash after consumption is fenced for investigation, not automatic replay. Native adoption plus original-Journal validation and any durable version append precede normal primary resave/new credentials. Persistence failure, drift, unsupported header tuple or failed identity proof blocks continuation. The applicable file-proof limits and adoption bounds remain enforced in code.

A flight checkpoint is a separate internally generated pre-launch save bound to one owned flight, exact tick and origin/destination. Its lifecycle-bounded token can reload only the matching failed/interrupted flight, expires after 24 hours and retires on stable success or a covering newer primary save. It is never revived or substituted for a primary recovery credential.

## Finite operational budgets and lifecycle

Before execution, a current authorized declaration fixes the result, exact scope, unchanged successful prefix, write/request/time limits, completion proof and stop conditions. Caller limits may be narrower than the underlying tool. Do not expand them from tool discovery.

New audit windows default to 20 accepted writes where the active Lab workflow uses `AuditWindowLimit`; supported explicit limits are 10/20/50. Existing declarations keep their original limit and count, including ten-write windows. Reserve any required normal save within the same budget. At the limit, freeze commits, independently reconcile/audit, and reopen only through the designated handoff. Two similar native failures stop the candidate family for redesign; they do not justify a shifted replay.

Production experiments keep predeclared sampling/request/deadline bounds, original failed windows and gaps. Overlapping native windows or wall time do not earn extra coverage. Governor's immutable baseline/target/tolerance and contiguous ≥36000-game-tick proof remain separate from resource conservation, source attribution and save/resume acceptance.

Host shutdown/reconnect/compaction is independent of DSP lifecycle. It must not automatically save, exit, relaunch or reload DSP. A healthy still-running world continues after fresh identity/Journal/action reconciliation. Explicit game close or an already authorized necessary cold deployment retains normal-save, terminal, close and protected-recovery gates.

## Privacy and validation

Never publish save names/files, tickets, credentials, real identities, raw requests/responses, runtime descriptors, absolute local paths, DLLs or crash dumps. Keep protected local evidence and publish only sanitized conclusions. Public diagnostic bundles use their explicit allowlist; that does not sanitize arbitrary other DTOs automatically.

Tests, source review, native prepare positives, installed readback and live sustained acceptance prove different things. Pending checks are not success. Formal tag/Release/Thunderstore publication requires its own authority. Earlier detailed evidence is retained in the [accepted safety document](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/safety-model.md) and [historical Lab snapshot](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7).
