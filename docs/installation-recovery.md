# Recover an interrupted manual installation

[English](installation-recovery.md) · [简体中文](installation-recovery.zh-CN.md)

This explicit operation restores the **captured original installation payloads**. It does not select a newer release, handle game saves or prove the old installation was healthy. Stop DSP and the MCP host first. Game-save protected resume is a separate interface.

Preserve the pending marker, both exact operation directories, progress records and original backups. Use the failed installation's explicit operation ID and destinations; a newest timestamp is not target selection.

## Preview, then explicitly restore

The packaged `recover-install.ps1` defaults to read-only preview:

```powershell
./recover-install.ps1 -PluginDestination '<exact Plugin destination>' `
  -McpDestination '<exact MCP destination>' `
  -OperationId '<failed installation operation ID>'
```

Replace placeholders locally with that exact operation's values. Inspect the returned ID and fresh `evidenceHash`. Explicit restoration binds that evidence:

```powershell
./recover-install.ps1 -PluginDestination '<same Plugin destination>' `
  -McpDestination '<same MCP destination>' `
  -OperationId '<same operation ID>' `
  -RestoreOriginal -ExpectedEvidenceHash '<fresh preview evidence hash>'
```

Each call acquires both target locks and checks the marker-bound stage/archive, matching immutable records, complete backups, recognized live hashes and unchanged `runtime-handoff`. Reparse points and unexpected payload files are refused. Sequential progress records can differ by an interruption phase; different identities or original snapshots are not acceptable.

The preview hash binds both actual records and current evidence. If evidence changes, obtain a fresh preview; never force reuse of an old hash.

## Restoration and failure handling

Restoration withholds the live main Plugin DLL, restores and verifies other Plugin files and MCP, then restores the main DLL last. Only verified final original payloads and both terminal records permit removal of this operation's pending marker. Previously absent files remain absent. Displaced files/backups remain evidence; newly created empty ancestor directories may remain.

Failure retains the marker/evidence and attempts to withhold the main DLL again, reporting any withholding failure. Do not blindly replay a failed restore. Investigate and obtain fresh evidence. The tool does not automatically retry, scan unrelated backups, repair malformed records or overwrite unknown hashes. Truncation, incomplete backups and unexplained file changes require investigation.

## Scope and path budget

Only the supported manual-package target layout is covered. Explicit recovery may acquire an abandoned installation mutex and then revalidate; ordinary install and Plugin startup still refuse an abandoned lock. A new startup gate cannot stop an older Plugin already loaded in memory.

The complete future stage/archive, backup and per-attempt evidence paths are budgeted before writes. File paths are limited to 259 UTF-16 code units and directories to 247 for supported PowerShell compatibility. These are product limits, not universal Windows limits. New installations can choose shorter destinations and rerun preflight. For interrupted installs, preserve exact targets/marker/evidence and seek review; do not move evidence, edit recorded paths or enable extended-path syntax to bypass refusal.

Atomic cross-volume upgrades, universal hardware-failure protection and partially written-record recovery are not claimed. Synthetic tests, source builds, package checks and live deployment are different evidence levels; [validation](validation/v0.4.0.md) states what was actually accepted. Real installation transaction-crash recovery remains unvalidated.
