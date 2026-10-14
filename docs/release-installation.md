# Installing a Spherewright release

The current unreleased 0.4 candidate target is Windows x64, Dyson Sphere Program `0.10.35.29145`, and BepInEx `5.4.17.0`. Its exact Spherewright version is recorded in `manifest.json` and reported by the installer. The matching development Plugin/MCP cohort has local menu deployment and native v6 preparation evidence; actual `29131 → 29145` migration requires a later explicit confirmation and has not run. Earlier R1311/R1318 protected recovery evidence is limited to 29131. The final same-commit manual and Thunderstore packages still require their own installation and runtime/recovery validation. Released 0.3.x local evidence remains scoped to `0.10.34.28529` and does not validate this candidate target. The package includes the Plugin and a self-contained MCP executable; end users do not need the repository, source code, or a .NET SDK. See the [current state](current-status.md) and [29145 deployment record](evidence/2026-10-15/owned-primary-29145-menu-deployment.md).

The supported gameplay scope is single-player peaceful mode. Sandbox state and resource multiplier are reported but do not block import, normal actions, saving, or protected recovery; they never enable extra Spherewright actions or sandbox-tool calls. The validated reference world remains non-sandbox with 1× resources. Released v0.3.3 historical local validation covered a fresh 1× non-sandbox world and imported peaceful 100× and sandbox 1× worlds; it does not validate the current unreleased 0.4 package, DSP `0.10.35.29145`, or cross-computer use. Dark Fog/combat, multiplayer or Nebula, broad third-party Mod compatibility, an arbitrary save picker, and arbitrary save-name loads are not guaranteed.

Spherewright is distributed in two layouts with the same gameplay and MCP capabilities:

- `Spherewright-<version>-win-x64.zip` is the complete manual installer package. It installs the Plugin into DSP and keeps the self-contained MCP server under `%LOCALAPPDATA%\Spherewright`.
- `Spherewright-<version>-thunderstore.zip` is the Mod Manager package published as `Arcueid_77-Spherewright`. It lets Thunderstore Mod Manager or r2modman install and update BepInEx plus the Plugin, and keeps a single-file MCP executable alongside the Plugin.

## Unreleased 0.4 development capabilities

Current source also connects an explicitly chosen finite blueprint layout to Foundry's material intent and the same protected construction executor. It reports complete object costs, per-item routes/free boundary ports and immutable per-object steps; only fresh prepare/commit grants action authority. Native full-power single-item belt/basic-sorter capacity constrains routing, including boundary belts and sorter span. Unknown or advanced stacking capacity blocks this composition; this does not change ordinary blueprint support. Rated budget success is not fair splitting or certified transport throughput. Ordinary2011/2012 sorter removal has a guarded normal-recovery path for missing-end repair. Empty2011 and one-item-carrying2011 removal, separate native rebuilds and normal saves passed on the local736 cohort;2012 removal, nonzero-inc cargo, post-repair resume and the newer transport budget still need matching-build live evidence. The six-object blueprint lifecycle at about `29.989` graphite/min and one durable Governor `31→62/min` declaration over `37128` effective ticks are closed historical gates only; they do not validate a complete three-stage factory chain, departure readiness, or the latest same-batch cold deployment. Existing September5 artifacts are older development packages, not the current merged completion candidate.

The bounded blueprint subset includes ordinary unstacked storage2101 with exact bans, mode and grid filters, but never contents. Physical connection ports are not inventory capacity. This bounded type/configuration subset and the six-object module result do not validate every allowed storage setting or layout combination in game; tests and tool descriptions alone are not game-run evidence.

The development source includes read-only Foundry/Governor proposals, bounded blueprint inspection/export, a separate finite blueprint prepare/commit/progress/cancel executor, and native single-building upgrades (manufacturing assemblers and Mk.I→Mk.II sorters only). Preview/proposal responses remain **non-executable data**, not write tokens. Finite execution preserves ordinary materials/drones and requires fresh per-object evidence after restart. The six-object lifecycle and one Governor `31→62/min` declaration above are bounded historical closures; complete three-stage factory work, departure preparation, final-candidate recovery and sustained automatic supply acceptance remain unfinished. The current matching menu cohort is installed, with actual owned-world migration still pending. Belt and advanced stacking sorter upgrades are not supported. Released 0.3.x packages do not gain these tools from documentation. Read the packaged Agent playbook and actual `tools/list`; unsupported types/settings reject, never silently disappear. Do not replace DLLs while DSP is running.

The 0.4 package smoke check requires the merged tool surface and its read-only annotations, native research-priority parameter, finite-build/Governor instructions, and agreement between the packaged playbook and MCP's embedded resource. It also checks normal protocol-only startup/exit. Passing these offline checks does **not** establish local gameplay, the ten-game-minute throughput target or cross-computer validation; those are recorded separately in the Roadmap and save diary.

Current development site previews include separate advisory full base-load power budgets; unavailable, uncertain geometry or ambiguous coverage must not be treated as spare capacity. Governor retains an explicitly locked pre-execution throughput declaration durably, with target-chain findings separated from unattributed planet warnings; narrow proof that restored declaration data matches its original hash remains open. These source additions are not capabilities of an older installed package. Move completion also does not certify dry-land Walk state: the updated playbook permits only bounded, fresh-energy shore recovery to a recently verified Walk waypoint, not indefinite energy-draining Drift waits or blind path search.

## Prerequisites

- Install Dyson Sphere Program and BepInEx 5.
- Exit DSP before installing or upgrading Spherewright.
- Keep the extracted release package until installation has completed and its integrity check succeeds.

## Install

### Thunderstore Mod Manager or r2modman

Install `Arcueid_77-Spherewright` for Dyson Sphere Program and launch the selected modded profile once. The declared `xiaoye97-BepInEx-5.4.17` dependency is installed by the manager. In the profile directory, locate:

```text
BepInEx\plugins\Arcueid_77-Spherewright\Spherewright.Mcp.exe
```

Register that executable as a local stdio MCP server in the external Agent host. Do not pass a runtime descriptor, authentication token, or save identity on the command line. Updating Spherewright through the manager updates both the Plugin and that version's MCP executable together.

The Thunderstore archive uses the store's standard package layout and static integrity checks. Its first runtime check should be performed in a separate Mod Manager profile or clean computer before it is treated as cross-computer validated.

### Manual release package

Extract the zip, open PowerShell in the extracted `Spherewright-<version>` directory, and run:

```powershell
.\install.ps1
```

The installer verifies every packaged file against `manifest.json`, locates DSP through the explicit `-DspDir` argument, `SPHEREWRIGHT_DSP_DIR`, or Steam, and then installs the Plugin under `BepInEx\plugins\Spherewright`. The self-contained MCP server is installed under `%LOCALAPPDATA%\Spherewright\mcp\<version>` by default.

For a nonstandard DSP location:

```powershell
.\install.ps1 -DspDir 'D:\Games\Dyson Sphere Program'
```

The command returns JSON containing the exact installed Plugin directory and MCP executable. Register that `Spherewright.Mcp.exe` path as a stdio MCP server in the external Agent host; do not pass runtime descriptors, authentication tokens, or save identities as arguments. The package also includes the concise `AGENT-PLAYBOOK.md`; ordinary MCP hosts can discover and read the same content at `spherewright://agent/playbooks/opening-movement-v1`.

Reinstalling the same MCP version requires `-Force`. The installer never starts DSP, changes a save, enables writes, or installs BepInEx. Spherewright's generated BepInEx configuration remains observation-only by default; enable `Safety.AllowWrites` only when you intend to authorize structured commits. Importing a world that you loaded manually also requires the separate `Safety.AllowUserSaveImport=true` opt-in, followed at runtime by a fresh no-side-effect prepare and your subsequent explicit confirmation in the Agent conversation.

If installation reports `needs_recovery` or leaves a pending marker, do not delete or overwrite it. Start with the preview and use only the evidence-hash-bound explicit restore described in the [repository recovery guide](https://github.com/AvaloNero/Spherewright/blob/main/docs/installation-recovery.md) and packaged `RECOVERY.md`. This is manual, bounded recovery evidence; it does not make upgrades atomic or close the legacy loaded-Plugin startup race.

## Quick start

### Start a new world

Leave DSP at the idle main menu, set `Safety.AllowWrites=true`, restart DSP, and ask the Agent to create a new world. The normal new-game flow creates a peaceful, non-sandbox, 1× save named `Spherewright_New_*`. Before moving from the landing site, have the Agent read MCP resource `spherewright://agent/playbooks/opening-movement-v1` and follow its terminal-polling and bounded four-direction recovery rules. Legacy `Spherewright_M0_*` saves are not migrated or renamed and can still be resumed only through their exact protected tickets.

### Continue an existing save

Set `Safety.AllowWrites=true` and `Safety.AllowUserSaveImport=true`, restart DSP, then manually load the intended supported save. Ask the Agent to prepare import; review its disclosure and reply with a later explicit confirmation. Only then may commit create and Header-verify an independent `Spherewright_Imported_*` copy. The original save remains unchanged. Continue manual and Agent play in the copy, and after a restart leave DSP at the main menu and use protected resume. Whenever you operate that copy manually, the Agent must fresh-read it and prepare later writes with the current state hashes instead of reusing stale observations or plans.

A Spherewright-looking filename never grants ownership. Import does not provide arbitrary save selection or loading, and the imported copy's journal begins at the import point rather than reconstructing earlier history.

Every newly issued protected-resume ticket records the owned copy's current durable journal checkpoint. Keep the protected runtime journal data together with the resume-ticket replicas during backup or isolated package testing. If that journal is missing, recreated, truncated, or belongs to another tracking boundary, Spherewright refuses resume before gameplay and keeps the one-time ticket available for an exact repair and retry; it does not silently create a replacement timeline.

The repository records offline, local-live, and cross-computer-live evidence separately. Released v0.3.3 has historical local import evidence for the sandbox and non-1× cases above; that evidence does not validate the current unreleased 0.4 package or cross-computer import use.

## Verify or troubleshoot

- A modified or incomplete package is rejected before installation.
- The installer refuses to run while `DSPGAME` is active so it cannot overwrite loaded assemblies.
- If more than one DSP installation is found, pass the intended directory explicitly with `-DspDir`.
- Report the Spherewright version, supported DSP/BepInEx versions, structured error code, and sanitized state evidence. Never publish tokens, plan tokens, runtime descriptors, save names, or save files.
