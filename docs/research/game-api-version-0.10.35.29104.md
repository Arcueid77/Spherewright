# DSP 0.10.35.29104 exact-pair persistence research

[English](game-api-version-0.10.35.29104.md) | [简体中文](game-api-version-0.10.35.29104.zh-CN.md)

## Historical scope

The 2026-09-29 investigation supports only `0.10.35.29088 → 0.10.35.29104` on the researched protected expired-primary path. It is evidence, not loading permission. The [complete immutable original](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/research/game-api-version-0.10.35.29104.md) retains the then-current blocker and protected operational context. Save identity, candidate ticks and ticket contents belong in Lab, not this maintained product note.

Current accepted local candidate support is DSP `0.10.35.29145`; see [later research](game-api-version-0.10.35.29145.md) and [final validation](../validation/v0.4.0.md). Historical refusal/pending statements do not describe the current installation.

## Metadata comparison

The retained 29088 native reference is 8,020,480 bytes, SHA-256 `C43A484F6ADF8A9E4B956156047070891B46860D5B5C707BA1377B6A2AF25732`. The 29104 reference is 8,022,016 bytes, SHA-256 `6C122E5443E6843979B4064050DFCB5E0D75577A0B64F6AE4111290238B33C12`; recorded Steam build ID was `25599610`.

Mono.Cecil metadata inspection, without native execution, found equal full signatures, body sizes, instruction counts and per-instruction opcode/operand text for:

| Method | Body bytes | Instructions |
| --- | ---: | ---: |
| `GameSave.SaveCurrentGame(string)` | 846 | 290 |
| `GameSave.SavePath(string)` | 42 | 14 |
| `GameSave.ReadHeader(string,bool,out GameSaveHeader)` | 604 | 263 |
| `DSPGame.StartGame(GameDesc)` | 87 | 26 |
| `DSPGame.StartGame(string)` | 86 | 28 |
| `GameData.Export(BinaryWriter)` | 616 | 223 |
| `GameData.Import(BinaryReader)` | 2146 | 780 |
| `GameDesc.Export(BinaryWriter)` | 268 | 97 |
| `GameDesc.Import(BinaryReader)` | 560 | 223 |
| `GalaxyData.ExportRuntimeData(BinaryWriter)` | 284 | 122 |
| `GalaxyData.ImportRuntimeData(BinaryReader)` | 224 | 101 |

`GameData.CURRENT_PATCH` remains `23`. Unchanged compared import/export bodies support the narrow tuple inference: header `7`, GameData `13/patch23`, GameDesc `10`, Galaxy runtime marker `1`. This is a persistence-path comparison, not proof that all game methods or behavior are identical. Exact-reference Plugin compilation and native validation remain distinct evidence.

## Maintained recovery boundary

`OwnedWorldVersionCompatibilityPolicy` permits this exact link only after a matching source. It does not permit direct `28529 → 29104`, `29057 → 29104`, downgrade, wildcard or a skipped link. The current maximum is four durable transitions across the separately researched chain ending at 29145; that later extension does not weaken this pair's validation.

Validate the original complete Journal and source credential checkpoint before adoption. After native adoption, append only `29088 → 29104`, persist it before normal primary saving, then read back the target header/identity/tick and issue a target-version credential. Preserve historical origin, existing entries/transition, identity and credential dates; never relabel past history to make it qualify.

Expired-primary prepare discloses the exact protected primary and requires a subsequent explicit conversation confirmation plus matching plan evidence at commit. Default resume does not gain cross-version authority. Consume durable attempt/tombstone before the single native load; changed evidence or interrupted consumed attempts refuse rather than replay. No LastExit/autosave substitution, arbitrary saves or import copy is implied.

## Limits

Offline equality, compilation and tests do not establish live migration, target save/readback, future resume, production continuity or complete `0.4` acceptance. The original research's lack of those proofs remains historical; later acceptance is established only by its separately indexed exact evidence. This page grants no Save/Load, installation or game authority.
