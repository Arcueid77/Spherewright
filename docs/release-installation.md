# Installing Spherewright

[English](release-installation.md) · [简体中文](release-installation.zh-CN.md)

## Choose a version and layout

The latest published release is v0.3.3. **0.4.0 remains an unpublished, locally accepted candidate.** Its frozen source, both ZIP hashes and actual local acceptance are recorded in [validation](validation/v0.4.0.md). Documentation on main may be newer than those frozen packages.

The accepted 0.4 environment is Windows x64, DSP `0.10.35.29145`, BepInEx `5.4.17.0`, one peaceful owned non-sandbox 1× local world. Runtime compatibility permits supported peaceful sandbox/resource-multiplier settings without adding sandbox actions, but broader 0.4 live coverage has not been established. v0.3.3 results retain their historical build scope.

The current compatibility candidate packages declare DSP `0.10.35.29151`. Its [offline preflight](validation/native-29151-preflight.md) and subsequent exact-source Lab runtime report must be read alongside the frozen 29145 acceptance. A package target version does not certify that candidate's installation or protected recovery. An expired 29145 primary requires the exact prepared `29145→29151` disclosure and subsequent confirmation; active-primary evidence 5/6 keeps its previous scope.

Two layouts provide the same gameplay/MCP capabilities:

| Layout | Plugin | MCP |
| --- | --- | --- |
| `Spherewright-<version>-win-x64.zip` | manual installation into `BepInEx/plugins/Spherewright` | self-contained executable under `%LOCALAPPDATA%/Spherewright/mcp/<version>` by default |
| `Spherewright-<version>-thunderstore.zip` | standard Thunderstore layout, identifier `Arcueid_77-Spherewright` | single-file executable alongside the Plugin |

End users need no source checkout or .NET SDK. Keep Plugin, MCP and shared assemblies from one build. Exit DSP and stop the connected MCP process before installation; never overwrite loaded DLLs.

## Manual package

Install BepInEx 5 separately. Extract the complete package and keep it until verification succeeds. In the extracted directory:

```powershell
./install.ps1
```

The installer verifies `manifest.json`, discovers DSP through `-DspDir`, `SPHEREWRIGHT_DSP_DIR` or Steam, and reports the exact Plugin directory and MCP executable in JSON. With multiple/nonstandard installations, supply the intended directory explicitly:

```powershell
$dspDir = Read-Host 'Exact DSP installation directory'
./install.ps1 -DspDir $dspDir
```

Reinstalling the same MCP version requires `-Force`; this does not bypass integrity, process, target-tree or pending-recovery checks. The installer does not start DSP, enable writes, install BepInEx or modify a save.

Register the returned `Spherewright.Mcp.exe` as a local stdio MCP server in your Agent host. Never pass a runtime descriptor, authentication token or save identity on its command line. Read the packaged `AGENT-PLAYBOOK.md` or MCP resource `spherewright://agent/playbooks/opening-movement-v1`. New packages from the maintained source also include Chinese companion guides; the frozen candidates retain their original contents.

## Thunderstore layout

The 0.4 archive has been validated in its declared local standard layout; **formal store publication and a separate Mod Manager/r2modman profile remain unvalidated**. Do not assume an unpublished 0.4 package is already downloadable from the store.

After an authorized publication, install the selected version and its declared `xiaoye97-BepInEx-5.4.17` dependency through your manager. In that selected profile, locate:

```text
BepInEx/plugins/Arcueid_77-Spherewright/Spherewright.Mcp.exe
```

Register it as local stdio MCP. Updating through the manager must keep Plugin and MCP together. Another profile/computer requires its own verification.

## Begin a session

Spherewright starts observation-only. Enable `Safety.AllowWrites=true` only for intended structured actions, then restart DSP normally to load the setting.

**New world:** leave DSP at the idle main menu and ask the Agent to prepare the normal new-game flow. It creates a peaceful, non-sandbox, 1× `Spherewright_New_*` world. Read the playbook before moving; poll accepted actions to their original terminal result and use only bounded fresh recovery. Legacy `Spherewright_M0_*` names do not independently establish ownership.

**Import a player-loaded save:** additionally opt into `Safety.AllowUserSaveImport=true`, restart DSP and manually load the intended supported peaceful save. Prepare import first; review its no-side-effect disclosure and explicitly confirm in a later conversation message. Only then may commit create and Header-verify an independent `Spherewright_Imported_*` copy through the normal save API. The original is unchanged. Its earlier history is not reconstructed; the owned copy's Journal starts at import. Manual play in the copy requires fresh observations and new prepares for later Agent writes.

**Resume an owned primary:** leave DSP at the idle menu and use only its exact protected ticket, embedded identity and durable Journal. Same-version resume is the default. A disclosed version migration or expired recovery requires its own later explicit confirmation. A filename prefix grants no ownership. Tickets are durably consumed before native loading; failed/interrupted accepted operations require investigation and original-action reconciliation, never a new-key replay.

Back up the protected Journal together with ticket replicas. Missing, truncated, recreated or mismatched Journal continuity refuses resume or keeps resumed operations restricted; it does not silently create a replacement timeline. Consult the playbook for the exact native gates.

## Verify and recover

- A modified/incomplete package or an active `DSPGAME` process is refused.
- If installation leaves `needs_recovery` or a pending marker, preserve both operation directories, records and backups. Use the read-only preview and evidence-bound explicit restoration in [installation recovery](installation-recovery.md); do not delete the marker or move evidence to bypass refusal.
- Installation transaction-crash recovery, another computer, broad third-party Mods, Dark Fog/combat and multiplayer/Nebula are not validated. A startup guard cannot stop an older Plugin already loaded in memory.
- Report exact Spherewright/DSP/BepInEx versions, the structured error and sanitized evidence. Never publish Saves, save names, tokens, runtime descriptors or private paths.

See [release notes](release-notes/v0.4.0.md), [validation](validation/v0.4.0.md) and [Lab evidence](sample-validation.md). Historical installation/recovery records remain evidence for their stated builds, not commands to replay.
