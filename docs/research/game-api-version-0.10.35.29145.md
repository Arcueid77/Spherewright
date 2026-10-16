# DSP 0.10.35.29145 exact-pair persistence research

[English](game-api-version-0.10.35.29145.md) | [简体中文](game-api-version-0.10.35.29145.zh-CN.md)

## Research scope and later acceptance

The 2026-10-15 research supports only `0.10.35.29131 → 0.10.35.29145`, preserving exact owned-primary and Journal provenance. The [complete immutable original](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/research/game-api-version-0.10.35.29145.md) retains initial default-path refusal, metadata reports, protected references and the then-pending implementation/deployment boundaries. The [Lab blocker record](https://github.com/Arcueid77/Spherewright-Lab/blob/14478a03c64c550e644c559405c8496b647552e7/releases/v0.4.0/evidence/2026-10-15/owned-primary-29145-blocker.md) preserves that historical stage. A generic `SESSION_NOT_OWNED` hint did not itself prove quarantine.

Later [final validation](../validation/v0.4.0.md) accepts source `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9` locally on Windows x64 / DSP `0.10.35.29145` / BepInEx `5.4.17.0`, with its indexed native, package and recovery evidence. Do not present the original “candidate not deployed” wording as current status, or promote metadata equality alone into live proof. Frozen candidate archives are unchanged; this documentation pass grants no loading, installation or publication authority.

## Exact native comparison

| Input | Assembly-CSharp.dll SHA-256 |
| --- | --- |
| Retained `0.10.35.29131` reference | `74AC6162CEF8394A6021C8475132CAE4DF4F840871A99E63CC7D2C8297661BC0` |
| Installed reference attributed by native menu to `0.10.35.29145` | `B84B60DF5A24947A014801F23B9B85D1E77853F42A331918E2DC85B153E50527` |

The old attribution comes from its exact retained capture; the new version came from the native menu rather than a stale `Updates/Versions.txt`. Mono.Cecil read metadata only. The comparison invoked no native method or Bridge request and changed no save, ticket or progress file.

All eleven full signatures, method-body sizes, instruction counts and per-instruction opcode/operand text matched:

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

`GameData.CURRENT_PATCH=23` and unchanged compared bodies support the bounded tuple inference: native header `7`, GameData `13/patch23`, GameDesc `10`, Galaxy runtime marker `1`. This says nothing universal about construction interfaces or all gameplay behavior. The original comparison report SHA-256 is `7C4D5457CF976FE4E76B994590368CADFFDE267C2E44B82D2BF8D4A9F4534434`; the metadata-only caller SHA-256 is `EA8EB034AD8C0B29F511A83BD2C4A7A14035350F7F8386FB8B95672704BDC71D`. Detailed instruction hashes remain protected evidence, not bundled game code.

## Pair-specific recovery and Journal policy

`OwnedWorldPrimaryMigrationPolicy` preserves `29104 → 29131` evidence version `5` and adds `29131 → 29145` evidence version `6`, each with its own exact prompt. Legacy version/prompt cannot qualify the new pair. Default resume requires source/runtime equality. Prepare refuses confirmation/tick overrides and discloses the exact primary with complete identity/tick binding without consuming or loading.

Commit requires a subsequent explicit confirmation and matching digest, fresh unchanged active ticket replicas, primary, menu revision and original Journal, held under read-only leases. The durable attempt/tombstone precedes the single native load. After exact identity/planet/tick adoption, append only the next transition; preserve origin, all entries and the previous three transitions. `OwnedWorldVersionCompatibilityPolicy.MaximumDurableTransitions=4`; adoption ticks increase and durable sequences do not decrease. Persist the exact fourth transition before normal primary resave/header proof and new target-version credentials.

No fifth transition, skipped link, downgrade, unknown pair, LastExit/autosave substitution, alternate save or replay of a consumed attempt is allowed. A mode-specific migration is not a default resume or permission to rewrite credential dates/history.

## Completed finite-construction history

`BlueprintProgressVersionPolicy.CanRetainCompletedHistory` permits only stored29131→runtime29145 completed history for the same session/Journal identity, with a complete continuous durable Journal, no pending persistence/error and a proven exact transition. The loaded tick must cover every recorded receipt; every build and object must already be completed. `BlueprintBuildStore` validates plan hashes/receipts before returning clones. Partial, unknown, corrupt, other-version or unproven history refuses.

Reading neither rewrites the file nor restores a plan token or execution authority. A later ordinary durable update may stamp the target document version while preserving history; failed persistence leaves source evidence intact. Never clear a store or repeat completed construction to resolve a version mismatch. Synthetic linked-store tests do not substitute for native entity/topology readback or Windows installation evidence.

## Remaining limits

Method equality and unit/integration tests cannot establish arbitrary-save compatibility, future normal resume, production continuity, all native build paths or cross-computer operation. Accepted same-version recovery and sustained supply have their own final evidence; the whole-candidate result does not erase those limits. Fresh authorization and native gates remain necessary for any later game or migration action.
