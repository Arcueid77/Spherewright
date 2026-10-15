# Deferred LAN validation-host design

[English](remote-validation.md) · [简体中文](remote-validation.zh-CN.md)

**Status: deferred; no remote host or deployment workflow is implemented or validated by this document.** Local 0.4 acceptance is recorded separately in [validation](validation/v0.4.0.md). It does not establish another-computer use.

## Intended topology

```text
Development computer: source, build, automated tests
    -> authenticated SSH + SCP/SMB artifact deployment
Game computer: interactive DSP + BepInEx + Plugin + MCP
    -> local authenticated Named Pipe -> Plugin main-thread executor
    -> sanitized structured results returned over authenticated SSH
```

The pipe stays local to the game computer, never TCP/SMB exposed. MCP runs there, discovers its descriptor locally and carries only MCP stdio through the authenticated command channel. Tokens and descriptors remain on that computer.

## Prospective workflow

1. Verify exact DSP/BepInEx versions and native assembly hashes; build against that cohort.
2. Refuse deployment while DSP is running; never kill it or replace loaded DLLs.
3. Stage only matching Plugin/MCP artifacts and install the intended version.
4. Start DSP in an explicitly authorized, already logged-in interactive Windows session and wait at the menu. An SSH service session is not assumed to launch a usable GUI.
5. Run a separately approved structured validation suite with fresh native/ownership gates, one writer and finite budgets. Use published MCP/Plugin interfaces, not UI recognition or input macros.
6. Collect only structured results and sanitized logs, separately from save directories. Do not enumerate/copy/read game saves.
7. Return to observation-only `Safety.AllowWrites=false` under the approved configuration lifecycle.

Use key-authenticated OpenSSH and narrowly scoped account/firewall access. Do not add HTTP/SSE/Streamable HTTP to the Plugin, share `%LOCALAPPDATA%/Spherewright/runtime`, or put credentials in scripts/chat. Any future supported imported copy must first satisfy the same explicit import/owned-primary rules as local use; a path/name is never ownership. The reference acceptance scope remains peaceful, non-sandbox, 1× unless another scope is separately declared.

## Activation gate

Future names such as `scripts/remote/prepare-game-host.ps1`, `deploy-game-host.ps1`, `run-validation.ps1` and `collect-evidence.ps1` are design placeholders, not shipped tools.

Begin implementation only after the local release workflow is complete/repeatable, a fresh ordinary-world baseline and safe evidence collection are established, the exact transport/interactive-launch method is chosen, and the owner explicitly resumes this separate work. Local evidence must exclude secrets, save-file access, UI macros, sandbox calls and unexplained item changes. This repository maintenance grants no remote deployment or game authority.
