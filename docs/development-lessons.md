# Development lessons

[English](development-lessons.md) · [简体中文](development-lessons.zh-CN.md)

These engineering lessons come from Voyager 0.5. They guide implementation and validation; they add no approval gate, execution authority, resource allowance or requirement to rebuild unchanged binaries. Current repository instructions and the active Lab authority remain controlling. Scenario facts and raw evidence belong in Lab, not this guide.

## Priorities

| Priority | Failure pattern | Better approach |
| --- | --- | --- |
| P0 | Individual actions work, but their dependencies are discovered only after deployment. | Plan the complete useful result: real source, connection, power, transport, consumption, Save and continued operation after recovery. Mark geometry, material estimates and runtime premises as verified, unknown or blocked. Refine the next stage from actual observations. |
| P0 | Recovery accepts an empty site, then needs another exception after each supported construction or production step. | Describe legal states and transitions of supported components, including operating inventories and Native caches. Validate provenance and actual state together; avoid assuming every legitimate imported state is empty or equal to its startup value. Preserve rejection of unproved components and bindings. |
| P0 | A caller interprets a nullable field or a valid tool result incorrectly. | Use component-specific semantics and the actual public DTO. A pure power node need not expose a consumer's network field; an input port is not an output selector; arrival within tolerance does not prove useful route progress. Test conditional request fields for each mode. |
| P1 | Thousands of isolated tests pass while the same live lifecycle still fails. | Add realistic captured-state and cross-layer regressions for the affected transition. Synthetic Native facts must be labelled as substitutes, not runtime proof. Preserve source, transport, installation and gameplay evidence as separate layers. |
| P1 | Every local caller error triggers another world exit and recovery. | Classify the failure before choosing recovery. Stop the failed write batch and retain its receipt, then determine whether a corrected fresh batch can use the current healthy binding. World reload is a technical operation with its own costs, not a universal error handler. |
| P1 | Old instructions, old snapshots or procedural counters override the latest real state. | Refresh the current checkout, lock, handoff and authority after interruption or handoff. Keep current facts concise and history explicitly historical. Separate authorization, Native executability, observation coverage and cumulative resource accounting. |
| P1 | Package, test or metadata success is reported as complete delivery. | Tie each claim to the evidence that proves it. Payload equality, a menu probe, a real gameplay cycle and a cold installation test prove different things. Record any accepted replacement of an acceptance requirement explicitly. |

## Validate the whole transition

Use an affected-state matrix rather than adding only another isolated negative predicate:

| State or transition | Important observations |
| --- | --- |
| Empty site to paid construction | Exact whole-batch debit, original action identity, Native entities and reciprocal connections. Do not count a shared debit once per object. |
| Pending construction to physical idle | Real drone drain, original switch restoration, retained completed/pending objects and unchanged budgets. |
| Completed site to unpowered connection | Known component membership, valid Native network topology and actual energy state. A connection is not evidence of generation. |
| Mining and station input | Real resource decrease, belt cargo, station stock and legal input-cache transitions. Startup zero stock is a provenance baseline, not a perpetual invariant. |
| Save, menu and load | A complete covering owned save, released conflicting file leases, correct import/scene readiness and fresh automation admission. Save-file validity, loaded-world readiness and automation health are distinct facts. |
| Recovery to continued delivery | New real shipment and consumption after recovery, conserving prior spending and original outcomes. A loaded world alone is not this result. |
| Caller and bridge boundary | Actual DTO variants, missing/not-applicable values, required and forbidden mode-specific flags, parser/name errors and original-action polling. |

Native lifecycle details need direct investigation. A retired prebuild cursor can legally reset to its empty value; a collider collection can still be initializing; a recomputed floating-point transform is not necessarily bitwise stable. Research the pinned Native implementation and retain its real same-cut observations before changing a predicate. Unknown remains unknown until proved.

## Keep callers maintainable

- Keep reusable Lab driver code and its tests in version control. Private configuration, credentials, save files and raw receipts remain protected and ignored. Immutable execution packets should pin the driver revision and inputs instead of becoming independent hand-edited implementations.
- Exercise the complete caller flow against realistic, sanitized public responses. Syntax checks do not catch all missing names, wrong-mode flags or semantic assumptions; schema validation does not establish runtime meaning.
- Check meaningful macro progress for multi-step movement. Relate waypoint spacing to arrival tolerance and current obstruction evidence. Inspect and use an existing supported harvest/cancellation path where applicable; do not repeatedly reissue an unchanged failed target.
- Treat prepared tokens, acceptance and terminal success separately. Retain the original accepted/unknown action and idempotency key; never make a new key a substitute for reconciliation.

## Choose the response to the actual failure

| Observed failure | Response within existing authority |
| --- | --- |
| Local preparation or caller validation, no game action accepted | Preserve the error and correct the caller. Inspect current state before starting a fresh batch; do not infer game corruption. |
| Native prepare refusal | Keep the refusal and inspect the actual predicate. Diagnose a product gap, caller misuse, missing observation or a real budget shortage separately. Permission cannot make a false technical predicate true. |
| Known terminal failure with proved physical stop | Retain that outcome. A different corrected action requires its own fresh proof and applicable authority; it does not rewrite the failed action. |
| Unknown outcome or lost binding | Preserve quarantine and original evidence. Reconcile through the supported path; do not replay, assume idle or overwrite history. |
| Proposed source replacement or process exit | Prove complete current progress coverage and preserve required inputs before the operation. Use ordinary menu/Save/Load where supported; close the process when the authorized update or another concrete prerequisite needs it. |

## Order work around accepted results

1. Establish the whole-chain dependencies and the next measurable stage result. Do not freeze every distant action before its prerequisites are known.
2. Reproduce and fix the related source/caller gaps with focused tests and cross-layer replay. Group known related fixes before the next runtime deployment.
3. Qualify the exact source under the existing build/CI rules, then install a coherent cohort when live validation needs it. Observe the real stage result and update only affected plans.
4. Generate final distribution archives after functional evidence closes. A documentation-only revision does not change the selected runtime source or require another installation/package pair.

Report the result achieved, its first failed or successful boundary, the next unique action and whether a real owner decision is needed. Test totals, commits, backups and packages support that report; they are not substitutes for gameplay acceptance.

## Evidence and acceptance boundaries

Retain raw observations even when they differ slightly from nominal estimates. Floating-point or Native integer behavior must be explained by measured evidence, not hidden by modifying counters or inventing capacity. Fixed user limits and return reserves remain limits.

If an acceptance clause requires separate package installations and recovery tests, exact-byte parity and packaged menu probes do not by themselves establish those installations. Perform the specified verification or record the accepted scope adjustment and remaining coverage. A retrospective does not silently rewrite an earlier acceptance result.

The [fixed 0.5 Lab outcome](https://github.com/Arcueid77/Spherewright-Lab/blob/7baf7277b09a8e4f632d8ff5fef151bfb4b12a0c/scenarios/v0.5-voyager/current-status.md) and [review evidence](https://github.com/Arcueid77/Spherewright-Lab/blob/7baf7277b09a8e4f632d8ff5fef151bfb4b12a0c/releases/v0.5.0/review-delivery.md) preserve the scenario-specific results and evidence boundaries. They are historical observations, not authority for a new run.
