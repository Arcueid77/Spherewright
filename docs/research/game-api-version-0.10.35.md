# DSP 0.10.35.29057 / 29088 persistence research

[English](game-api-version-0.10.35.md) | [简体中文](game-api-version-0.10.35.zh-CN.md)

## Historical scope and exact inputs

This records the 2026-09-24 research for only the directional pairs `0.10.34.28529 → 0.10.35.29057` and `0.10.34.28529 → 0.10.35.29088`. It is historical persistence evidence, not current runtime status or recovery authority. The full method hashes, rationale and then-pending observations remain in the [immutable original](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/research/game-api-version-0.10.35.md). Current candidate support is `0.10.35.29145`; see [environment](environment.md), [later research](game-api-version-0.10.35.29145.md) and [final validation](../validation/v0.4.0.md).

| Native reference | Bytes | Assembly-CSharp.dll SHA-256 |
| --- | ---: | --- |
| `0.10.34.28529` | 7,830,016 | `AE0BA95F75BD879A62AA4CE253B2AB78EAA4FB3C7C595F5E1FEE75EBE0E0EF85` |
| `0.10.35.29057` | 8,019,456 | `E75D3FE4B6A9CA822766189F826BA3A8348DFB7E301AA37FF6779DB29A83FD8D` |
| `0.10.35.29088` | 8,020,480 | `C43A484F6ADF8A9E4B956156047070891B46860D5B5C707BA1377B6A2AF25732` |

Targeted Mono.Cecil/ILSpy inspection read metadata without executing native methods, loading saves, changing installed files or editing tickets/Journals. Version provenance came from the recorded installation metadata and native references; a hash exception is not a general compatibility policy.

## Entry points and real format changes

Across 28529→29057, the signatures, body sizes and normalized instruction streams of `GameSave.SaveCurrentGame(string)`, `GameSave.ReadHeader(string,bool,out GameSaveHeader)` and both `DSPGame.StartGame(GameDesc/string)` overloads matched. That does not mean the whole save stayed unchanged:

- Native header remains `VFSAVE`, header version `7`, followed by version/tick/time and bounded screenshot/account data before GameData.
- `GameData` outer version remains `13`, while `CURRENT_PATCH` changes `22 → 23`; the target importer has a `patch < 23` migration branch.
- `GalaxyData.ExportScannedDatas` marker `0` becomes `ExportRuntimeData` marker `1`, adding vegetation data after the old scanned-record terminator. `ImportRuntimeData` explicitly returns after the old section for marker `<1`.
- `GameDesc` changes `9 → 10` by adding `starNameLCID`; `Import` reads it only for version ≥10 and otherwise uses zero.

Consequently `GameData.Import/Export` and `GameDesc.Import/Export` changed IL. Accept only coherent researched tuples: source header `7` / GameData `13, patch22` / GameDesc `9`, or the exact researched target header `7` / `13, patch23` / `10` (Galaxy runtime marker `1`). Do not independently mix allowed words into an unresearched tuple.

For 29057→29088, all eleven compared persistence methods retained signature/body/instruction equality: the four entry points above, `GameSave.SavePath`, GameData Import/Export, GameDesc Import/Export and GalaxyData Import/ExportRuntimeData. Target serialization tuple stayed unchanged. This supported adding the separate **direct** 28529→29088 pair; it did not establish 29057→29088 as a supported migration chain.

## Adoption and Journal requirements

`OwnedSavePrefixReader` validates only its bounded prefix, exact protected embedded identity, version and tick; it does not import a world. `GameDesc.creationVersion` is not a current source-version proof. Full-file evidence, protected matching credentials, the original Journal and exact native adoption remain separate requirements.

`OwnedWorldVersionCompatibilityPolicy` now lists these two direct pairs and the separately researched later links `29088 → 29104 → 29131 → 29145`, with at most four durable transitions. This current policy is not retroactive proof that every researched pair ran live. Journal `GameVersion` stays its origin; only a validated durable transition changes the effective version. Preserve identity, tracking boundary, historical-coverage flag, entries and sequence, with increasing adoption tick and nondecreasing durable sequence. Persist the transition before normal target-version saving and credential issuance.

The target save must read back exact owned identity, tick floor, current version, researched tuple and healthy Journal before a refreshed credential is trusted. Prepare only discloses; subsequent explicit user confirmation is required for its applicable migration mode. Default resume keeps version equality. Credential attempt/tombstone consumption precedes the one loader call; a failed or interrupted consumed attempt needs reconciliation, never expiry edits, Journal relabeling, another filename, LastExit/autosave substitution or replay.

## Evidence limits

The original 29057 research did not establish a live migration on that build. Later 29088 and subsequent evidence belong to their own exact scope in Lab and final validation. Metadata equality, Plugin recompilation and synthetic tests cannot prove loaded-world integrity, successful target save, future resume, sustained production or whole-version acceptance. The research itself grants none of those actions.
