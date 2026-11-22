# Installing Spherewright

[English](release-installation.md) · [简体中文](release-installation.zh-CN.md)

## Choose a version and layout

For a direct Windows x64 installation, use [Spherewright v0.6.0 (Ascension)](https://github.com/Arcueid77/Spherewright/releases/tag/v0.6.0). It supports DSP `0.10.35.29151` and BepInEx `5.4.17`.

- [Download the v0.6.0 Windows x64 manual ZIP](https://github.com/Arcueid77/Spherewright/releases/download/v0.6.0/Spherewright-0.6.0-win-x64.zip), then follow the manual package steps below.
- [Download the v0.6.0 checksums](https://github.com/Arcueid77/Spherewright/releases/download/v0.6.0/SHA256SUMS.txt). Verify the ZIP against the SHA-256 from the same release. Do not mix packages, checksums, Plugin or MCP across versions.
- For a managed installation, select `Arcueid_77-Spherewright` version `0.6.0` in Thunderstore Mod Manager or r2modman. The same layout is also offered as a GitHub release asset. [Thunderstore package](https://thunderstore.io/c/dyson-sphere-program/p/Arcueid_77/Spherewright/) · [GitHub layout ZIP](https://github.com/Arcueid77/Spherewright/releases/download/v0.6.0/Spherewright-0.6.0-thunderstore.zip)

The download section of `INSTALL.md` and its Chinese companion inside the original 0.6.0 manual ZIP still describes GitHub 0.4.0 and Thunderstore 0.3.3. Use the 0.6.0 downloads and checksums above for this archive.

Both layouts provide the same gameplay and MCP capabilities.

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

Copy the `mcpExecutable` path from the installer output. Use it in the connection check below.

## Thunderstore layout

Use the standard Thunderstore layout with the Plugin and matching single-file MCP together. The cited local standard-layout checks do not certify a separate Mod Manager/r2modman profile; verify the selected profile's actual components and MCP path.

In your Dyson Sphere Program profile, install version `0.6.0` with its declared `xiaoye97-BepInEx-5.4.17` dependency. Locate the matching executable inside that profile.

```text
BepInEx/plugins/Arcueid_77-Spherewright/Spherewright.Mcp.exe
```

Copy its full path. Launch the modded game from that profile so the Plugin and MCP use the same installation.

## Connect Codex and verify read-only access

With Codex CLI installed, paste the full executable path from your chosen layout when prompted.

```powershell
$mcpExe = Read-Host 'Full path to Spherewright.Mcp.exe'
codex mcp add spherewright -- "$mcpExe"
codex mcp list
```

Other MCP hosts can use the same executable as a local stdio server with no arguments. Keep the runtime descriptor, tokens and save identity out of the command line.

1. Open a Codex session with the server enabled. Read `spherewright://agent/playbooks/opening-movement-v1`. The MCP serves this guide even when DSP is closed.
2. Start modded DSP and wait for the main menu. Ask Codex to call `spherewright_get_status`. A successful reply reports the connected Plugin and game versions. Check for Spherewright `0.6.0` and DSP `0.10.35.29151`. This check works with the default read-only settings.
3. If the guide opens but the status call fails, compare the executable path with the installation or manager profile that launched DSP. Use the error code and recovery message from that call.

## Begin a session

Spherewright starts observation-only. Enable `Safety.AllowWrites=true` only for intended structured actions, then restart DSP normally to load the setting.

**New world:** leave DSP at the idle main menu and ask the Agent to prepare the normal new-game flow. It creates a peaceful, non-sandbox, 1× `Spherewright_New_*` world. Read the playbook before moving; poll accepted actions to their original terminal result and use only bounded fresh recovery. Legacy `Spherewright_M0_*` names do not independently establish ownership.

**Import a player-loaded save:** additionally opt into `Safety.AllowUserSaveImport=true`, restart DSP and manually load the intended supported peaceful save. Prepare import first; review its no-side-effect disclosure and explicitly confirm in a later conversation message. Only then may commit create and Header-verify an independent `Spherewright_Imported_*` copy through the normal save API. The original is unchanged. Its earlier history is not reconstructed; the owned copy's Journal starts at import. Manual play in the copy requires fresh observations and new prepares for later Agent writes.

**Resume an owned primary:** leave DSP at the idle menu and use only its exact protected ticket, embedded identity and durable Journal. Same-version resume is the default. A disclosed version migration or expired recovery requires its own later explicit confirmation. A filename prefix grants no ownership. Reauthorization durably consumes its ticket before native loading; normal Journal-bound default resume consumes it after native adoption and confirmed Journal continuity. Failed/interrupted accepted operations require investigation and original-action reconciliation, never a new-key replay.

Back up the protected Journal together with ticket replicas. Missing, truncated, recreated or mismatched Journal continuity refuses resume or keeps resumed operations restricted; it does not silently create a replacement timeline. Consult the playbook for the exact native gates.

## Verify and recover

- A modified/incomplete package or an active `DSPGAME` process is refused.
- If installation leaves `needs_recovery` or a pending marker, preserve both operation directories, records and backups. Use the read-only preview and evidence-bound explicit restoration in [installation recovery](installation-recovery.md); do not delete the marker or move evidence to bypass refusal.
- Installation transaction-crash recovery, another computer, broad third-party Mods, Dark Fog/combat and multiplayer/Nebula are not validated. A startup guard cannot stop an older Plugin already loaded in memory.
- Report exact Spherewright/DSP/BepInEx versions, the structured error and sanitized evidence. Never publish Saves, save names, tokens, runtime descriptors or private paths.

See the [v0.6.0 release notes](release-notes/v0.6.0.md) and [publication and verification scope](https://github.com/Arcueid77/Spherewright-Lab/blob/c012e723d75c35e1d72d82f3bb5f286926259945/releases/v0.6.0/publication-2026-11-21.md).

## Historical installation and live acceptance records

Choose a version from the [official Releases](https://github.com/Arcueid77/Spherewright/releases) or a reviewed local archive. The package manifest identifies that build's version, source commit and payload hashes; use the matching checksum record. Historical production-acceptance source `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9` and its original ZIP hashes are recorded in [validation](validation/v0.4.0.md). The separate 2026-10-17 package-callability source is `e018b7f45c70f5fe029418cad987af794b5cb9bf`; those historical packages retain their own source identity.

Frozen production acceptance used Windows x64, DSP `0.10.35.29145`, BepInEx `5.4.17.0` and one peaceful owned non-sandbox 1× local world. The cited e018 installation, callability and continuity checks used the same local environment on DSP `0.10.35.29151`. Runtime compatibility permits supported peaceful sandbox/resource-multiplier settings without adding sandbox actions, but broader 0.4 live coverage has not been established. v0.3.3 results retain their historical build scope.

Both cited e018 layouts were actually installed through the official transaction path and independently passed all 64 MCP calls, including successful data/preparation and expected native safety refusals; matrix accepted/unknown counts were zero. The owner-confirmed original-primary migration, normal saving and subsequent same-version recovery preserved durable Journal and completed history. See the [fixed runtime report and package hashes](https://github.com/Arcueid77/Spherewright-Lab/blob/00f4359eef704bc7cd5c148438c05e0d3121bda6/operations/package-callability-2026-10-17.md) and [native validation](validation/native-29151-preflight.md). Evidence applies only to its cited source, packages and source/session scope; no new production long window is claimed. Any future expired 29145 primary still requires the exact prepared `29145→29151` disclosure and subsequent confirmation; active-primary evidence 5/6 keeps its previous scope.

A separate final pair dated 2026-10-17 identifies source `b7c0002dbe2e8f5608c5ca140b0c6326a1818021` without replacing those historical package identities. Both its manual and standard Thunderstore layouts completed normal cold installation, `29151→29151` default owned-primary recovery and an independent complete 64-tool matrix each (23 data / six prepare / 35 expected native guards, zero accepted/unknown matrix actions). They retained durable Journal and completed history without repeating the original-world migration. Final source-specific installation, recovery and matrix result audits passed independently. See the [fixed source-specific report](https://github.com/Arcueid77/Spherewright-Lab/blob/74a4ec3d91608467911460eaf0d317800e60309b/operations/formal-release-2026-10-17.md). Later factual documentation SHAs do not change or rebuild these packages.

**Historical release (2026-10-17):** [GitHub v0.4.0](https://github.com/Arcueid77/Spherewright/releases/tag/v0.4.0) provides its original manual ZIP, Thunderstore-layout ZIP, checksums and generation manifest. Annotated tag `v0.4.0` points to `b7c0002dbe2e8f5608c5ca140b0c6326a1818021`.

See [release notes](release-notes/v0.4.0.md), [validation](validation/v0.4.0.md) and [Lab evidence](sample-validation.md). Historical installation/recovery records remain evidence for their stated builds, not commands to replay.
