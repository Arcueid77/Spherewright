# Native 29151 compatibility preflight

[English](native-29151-preflight.md) · [简体中文](native-29151-preflight.zh-CN.md)

As of 2026-10-17 this page records the offline compatibility research and subsequent installed-candidate validation. The frozen 0.4.0 production acceptance remains source `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9` on native `0.10.35.29145`. See [product status](../project-status.md) and [frozen validation](v0.4.0.md).

The repair adds only `29145→29151` to existing expired-primary reauthorization and permits its exact fifth durable Journal transition. Default resume remains same-version. Active-primary evidence 5/6 retains its previous two pairs. Prepare, subsequent explicit confirmation, exact identity, full-file leases, reauthorization consumption before loading and original-action reconciliation remain required. No tool or recovery mode is added.

Completed blueprint history can retain the proven `29131→29145→29151` suffix, including a document whose version stayed 29131 after an earlier read-only adoption. Every object must already be completed; every receipt must predate its first adoption and be covered by the loaded tick. Reading preserves original bytes and grants no execution authority. Partial, unknown, missing or corrupt history stays blocked.

| Offline check | Result |
| --- | --- |
| Five native references | Only Assembly-CSharp changed: `B84B60DF5A24947A014801F23B9B85D1E77853F42A331918E2DC85B153E50527` → `EF314425DADE51F529F184F07BE136CD3C2660A6A81515603C7226FBE7AB1D8C`; the other four are byte-identical. |
| Plugin references | All 1026 members (269 methods / 757 fields), 166 types and three separately checked reflection members match both reference sets. |
| Save/load roots | Eleven signatures and normalized method bodies match. Native format remains header 7, GameData 13 / patch 23, GameDesc 10. |
| Serialization call graph | Both graphs contain 1696 methods. The sole body difference is `DefenseSystem.MatchTurretPair`, adding a float multiplication by 100; this is a native behavior change, without an observed serialization-layout change. |
| Build | SDK 8.0.424 locked restore and full Release build of the unchanged baseline against the new references passed with zero warnings/errors. |
| Regression coverage | Exact fifth chain and sixth-link refusal; malformed prefix; read-only expired preparation; unchanged active-primary authority; completed single/two-hop Store reads, cloning, failed persistence and preservation of every original receipt. |

## Installed candidate evidence

Prior compatibility source `e018b7f45c70f5fe029418cad987af794b5cb9bf` completed actual installation and independent full 64-tool calls for both layouts on Windows x64, native `0.10.35.29151`, BepInEx `5.4.17.0` and the original owned peaceful/non-sandbox/1× world. The [fixed independent historical report](https://github.com/Arcueid77/Spherewright-Lab/blob/00f4359eef704bc7cd5c148438c05e0d3121bda6/operations/package-callability-2026-10-17.md) proves the explicitly confirmed original-primary `29145→29151` migration, fifth durable transition before normal resave, complete prior Journal/completed-history preservation and subsequent same-version default recovery. That evidence remains scoped to e018.

Final package source `b7c0002dbe2e8f5608c5ca140b0c6326a1818021` preserves the same game code and safety gates while correcting six bilingual bundled-document inputs. Its SDK 8.0.424 full locked Release build passed without warnings/errors, all 3165 tests (Contracts 73 / Core 2834 / MCP 258), nine workflow checks and [exact-source CI](https://github.com/Arcueid77/Spherewright/actions/runs/39018031556) passed. Both packages passed forced bilingual, embedded-guide and exact 64-name metadata checks.

Both final b7 layouts, manual and standard Thunderstore, completed normal cold installation and same-version `29151→29151` default owned-primary recovery. Each independently passed one complete installed 64-tool matrix: 23 data successes, six prepare successes and 35 expected native guards; zero accepted/unknown matrix actions. Both recoveries retained durable Journal and completed history without another migration. The guards demonstrate callability while retaining their native safety gates; they do not mean every protected write executed. Final source-specific installation, recovery and matrix result audits passed independently. See the [fixed source-specific runtime report](https://github.com/Arcueid77/Spherewright-Lab/blob/74a4ec3d91608467911460eaf0d317800e60309b/operations/formal-release-2026-10-17.md).

Normal Journal-bound default resume consumes its ticket after native adoption and confirmed Journal continuity; reauthorization durably consumes it before native loading. Failed/interrupted accepted operations require investigation and original-action reconciliation, never a new-key replay. This b7 same-version recovery does not repeat the earlier migration.

The offline checks above retain their research scope; frozen 29145 packages and historical production evidence are unchanged and are not inherited by the new source/session. Arbitrary-save safety, another computer, separate manager profiles, installation transaction-crash restoration and a new sustained-production window remain unverified. Later factual documentation commits do not change b7 packages or installed-source identity and do not rebuild them.

**Downloads (2026-10-17):** [GitHub v0.4.0](https://github.com/Arcueid77/Spherewright/releases/tag/v0.4.0) is published with the manual ZIP, Thunderstore-layout ZIP, checksums and generation manifest. Annotated tag `v0.4.0` identifies source `b7c0002dbe2e8f5608c5ca140b0c6326a1818021`. The [Thunderstore listing](https://thunderstore.io/c/dyson-sphere-program/p/Arcueid_77/Spherewright/) still has `0.3.3`; its `0.4.0` upload is pending. Use the GitHub manual package to install 0.4.0.