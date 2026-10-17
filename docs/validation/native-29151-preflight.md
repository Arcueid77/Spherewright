# Native 29151 compatibility preflight

[English](native-29151-preflight.md) · [简体中文](native-29151-preflight.zh-CN.md)

As of 2026-10-17 this page records the offline compatibility research and subsequent installed-candidate validation. The frozen 0.4.0 production acceptance remains source `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9` on native `0.10.35.29145`. See [product status](../project-status.md) and [frozen validation](v0.4.0.md).

The repair adds only `29145→29151` to existing expired-primary reauthorization and permits its exact fifth durable Journal transition. Default resume remains same-version. Active-primary evidence 5/6 retains its previous two pairs. Prepare, subsequent explicit confirmation, exact identity, full-file leases, consumption before loading and original-action reconciliation remain required. No tool or recovery mode is added.

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

Candidate source `e018b7f45c70f5fe029418cad987af794b5cb9bf` passed its full locked Release build with zero warnings/errors, all 3165 tests (Contracts 73 / Core 2834 / MCP 258), nine workflow checks and [exact-source CI](https://github.com/Arcueid77/Spherewright/actions/runs/38951284927). Its same-source manual and Thunderstore packages were then installed and tested locally on Windows x64, native `0.10.35.29151` and BepInEx `5.4.17.0` in the original owned peaceful/non-sandbox/1× world.

The [fixed independent runtime report](https://github.com/Arcueid77/Spherewright-Lab/blob/00f4359eef704bc7cd5c148438c05e0d3121bda6/operations/package-callability-2026-10-17.md) proves the explicitly confirmed original-primary migration, the fifth durable transition before normal resave, complete prior Journal/completed-history preservation and subsequent same-version default recovery. Each installed layout independently called all 64 tools in one complete matrix: 23 data successes, six prepare successes and 35 expected native guards; zero accepted/unknown matrix actions. Guarded calls preserve their native safety gates.

The offline checks above retain their narrower research scope. This runtime phase establishes the declared installation, callability and continuity checks; arbitrary-save safety, another computer, separate manager profiles, installation crash-restoration and a new sustained-production window remain unverified. Frozen 29145 packages and historical production evidence are unchanged and are not inherited by the new source/session. Later documentation commits do not change the e018 candidate or installed-source identity and do not rebuild its packages. Formal publication remains pending owner review.
