# Product status

[English](project-status.md) · [简体中文](project-status.zh-CN.md)

**Downloads (2026-10-17):** [GitHub v0.4.0](https://github.com/Arcueid77/Spherewright/releases/tag/v0.4.0) is published with the manual ZIP, Thunderstore-layout ZIP, checksums and generation manifest. Annotated tag `v0.4.0` identifies source `b7c0002dbe2e8f5608c5ca140b0c6326a1818021`. The [Thunderstore listing](https://thunderstore.io/c/dyson-sphere-program/p/Arcueid_77/Spherewright/) still has `0.3.3`; its `0.4.0` upload is pending. Use the GitHub manual package to install 0.4.0.

Frozen production-acceptance source is `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9` on native `0.10.35.29145`. Both ZIPs remain frozen at the hashes in [validation](validation/v0.4.0.md); chain, production-window and preparation-kit evidence retain their original source/session scope.

Final package source is `b7c0002dbe2e8f5608c5ca140b0c6326a1818021`, native `0.10.35.29151`. Its full SDK 8.0.424 locked Release build passed without warnings/errors; all 3165 tests passed (Contracts 73 / Core 2834 / MCP 258), along with nine workflow checks and [exact-source CI](https://github.com/Arcueid77/Spherewright/actions/runs/39018031556). Both packages passed forced bilingual and embedded-guide checks and expose exactly 64 tools and one resource.

Both final b7 layouts, manual and standard Thunderstore, completed normal cold installation and same-version `29151→29151` default owned-primary recovery. Each independently passed one complete installed 64-tool matrix: 23 data successes, six prepare successes and 35 expected native guards; zero accepted/unknown matrix actions. Both recoveries retained durable Journal and completed history without another migration. The guards demonstrate callability while retaining their native safety gates; they do not mean every protected write executed. Final source-specific installation, recovery and matrix result audits passed independently. See the [fixed source-specific runtime report](https://github.com/Arcueid77/Spherewright-Lab/blob/74a4ec3d91608467911460eaf0d317800e60309b/operations/formal-release-2026-10-17.md).

The explicitly confirmed original-world `29145→29151` migration and earlier two-layout callability/same-version recovery remain evidence for `e018b7f45c70f5fe029418cad987af794b5cb9bf` in the [fixed historical report](https://github.com/Arcueid77/Spherewright-Lab/blob/00f4359eef704bc7cd5c148438c05e0d3121bda6/operations/package-callability-2026-10-17.md). This b7 same-version recovery is not another migration. [Native validation](validation/native-29151-preflight.md) separates the two source scopes.

Later factual documentation SHAs do not identify rebuilt packages or installed sources; those remain b7. Separate manager profiles, another computer, installation transaction-crash restoration and the stationary-carrier-600-tick positive live branch remain unverified; no production long window is added.

A read-only [Nexus check](https://www.nexusmods.com/dysonsphereprogram/mods/23?tab=files) found its existing 0.3.3 primary file quarantined. The public page does not identify a detection engine or cause. No new 0.4 upload or Nexus/third-party scan approval is claimed; Nexus was inspected only.

The repository split is merged. The product retains four coupled source projects, general tests, package/install scripts, playbook, protocol, safety and bilingual guides. [Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab) owns scenarios, handoffs and detailed evidence, without becoming a product build dependency. The [fixed v0.4 evidence snapshot](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7) remains the provenance boundary.

Read [release notes](release-notes/v0.4.0.md), [Lab setup](sample-validation.md), [ROADMAP](../ROADMAP.md) and [documentation](README.md). Current guides distinguish source behavior, historical evidence and unverified branches. This page is not a fresh game observation or game authorization.
