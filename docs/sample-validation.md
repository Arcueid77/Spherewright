# Lab scenarios and detailed validation

[English](sample-validation.md) · [简体中文](sample-validation.zh-CN.md)

[Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab) owns scenarios, operational handoffs and detailed acceptance. The product builds, tests and packages without fetching or executing Lab.

## Current work and historical evidence

For current work, start from Lab's [contributor rules](https://github.com/Arcueid77/Spherewright-Lab/blob/main/AGENTS.md), [active handoff](https://github.com/Arcueid77/Spherewright-Lab/blob/main/operations/active-handoff.md), [source lock](https://github.com/Arcueid77/Spherewright-Lab/blob/main/source-lock.json) and [Voyager snapshot](https://github.com/Arcueid77/Spherewright-Lab/blob/main/scenarios/v0.5-voyager/current-status.md). These entries change as reviewed facts advance; they do not reuse historical authorization.

The immutable transfer snapshot below is `14478a03c64c550e644c559405c8496b647552e7`, with product submodule `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`. At that historical checkpoint its lock was `status: provisional` / `releaseTag: null`; imported operational facts came from product `e7d5f7d8661092b7c8089710488c448f290638be`. The later formal [`v0.4.0`](https://github.com/Arcueid77/Spherewright/releases/tag/v0.4.0) release is separately bound to `b7c0002dbe2e8f5608c5ca140b0c6326a1818021` and does not rewrite this fixed snapshot.

## Reproduce the fixed evidence check

```powershell
git clone --recurse-submodules https://github.com/Arcueid77/Spherewright-Lab.git
git -C Spherewright-Lab checkout 14478a03c64c550e644c559405c8496b647552e7
git -C Spherewright-Lab submodule update --init --recursive
cd Spherewright-Lab
pwsh -File scripts/bootstrap.ps1
pwsh -File scripts/verify-source-lock.ps1
pwsh -File scripts/verify-evidence.ps1
```

These are file/source/evidence checks, not game actions. A clean clone cannot verify protected local originals or frozen ZIPs absent from that clone; verifiers explicitly report those boundaries.

- [v0.4 scenario](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7/scenarios/v0.4-final)
- [validation summary](https://github.com/Arcueid77/Spherewright-Lab/blob/14478a03c64c550e644c559405c8496b647552e7/releases/v0.4.0/validation-summary.md)
- [evidence](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7/releases/v0.4.0/evidence) and [manifests](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7/releases/v0.4.0/manifests)
- [migration validation](https://github.com/Arcueid77/Spherewright-Lab/blob/14478a03c64c550e644c559405c8496b647552e7/releases/v0.4.0/migration-validation.md)

Safe originals were archived byte-for-byte before normalization. By owner choice, the original experience ledger remains protected-local because of a historical absolute system path; only its sanitized derivative enters Git. Private game records, Saves, tickets, identities and credentials are excluded. Public visibility never makes local-only evidence available or grants execution authority.

Old product operational paths retain short bilingual fixed-snapshot redirects. Older snapshot wording is historical; use Lab's current AGENTS/handoff for future work and fresh-check actual session/native/ownership state. The fixed product submodule is read-only by default; reusable fixes return to an independent product clone/worktree and exact-SHA CI before any lock bump.
