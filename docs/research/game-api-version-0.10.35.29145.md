# DSP 0.10.35.29145 exact-pair persistence research

## Evidence and scope (2026-10-15)

The fresh native menu reports `0.10.35.29145`, with no world loaded and healthy Bridge connectivity. The protected, unconsumed primary credential records `0.10.35.29131`, saved tick `116215326`, planet `104`, and durable Journal sequence `102`. Its Journal already contains the three original directional transitions. A single default prepare refused with `SESSION_NOT_OWNED`; it produced no plan, action, consumption, Load, Save or commit. The version mismatch is a sufficient default-path refusal; the generic recovery hint does not prove quarantine. See the [original blocker and protected references](https://github.com/AvaloNero/Spherewright-Lab/blob/14478a03c64c550e644c559405c8496b647552e7/releases/v0.4.0/evidence/2026-10-15/owned-primary-29145-blocker.md).

This research supports a candidate implementation for only **`0.10.35.29131 → 0.10.35.29145`**. It grants no loading permission, downgrade, wildcard, skipped transition, alternate save, LastExit/autosave substitution or retry after consumption. Migration still requires the user's subsequent explicit confirmation of a fresh native prepared disclosure.

## Native comparison

The retained 29131 `Assembly-CSharp.dll` is SHA-256 `74AC6162CEF8394A6021C8475132CAE4DF4F840871A99E63CC7D2C8297661BC0`; the actual installed 29145 DLL is `B84B60DF5A24947A014801F23B9B85D1E77853F42A331918E2DC85B153E50527`. The old version attribution comes from the exact R1217 capture; the new attribution comes from the native menu, not the stale `Updates/Versions.txt` line.

Both assemblies were read as metadata with Mono.Cecil. No native game method was executed, no Bridge request was sent, and no ticket, save or progress file was changed. For all eleven methods, the full signature, body byte length, instruction count and per-instruction opcode/operand text are identical:

| Native method | Body bytes | Instructions |
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

`GameData.CURRENT_PATCH` remains `23`. The unchanged export/import bodies support retaining the researched bounded prefix tuple: header `7`, GameData `13 / patch 23`, GameDesc `10`, Galaxy runtime marker `1`. This inference covers the compared persistence path; it does not establish that every native method, construction interface or gameplay behavior is unchanged.

The ignored comparison report `.local/native-29131-to-29145-persistence-comparison.json` contains both instruction hashes per method; SHA-256 `7C4D5457CF976FE4E76B994590368CADFFDE267C2E44B82D2BF8D4A9F4534434`. The metadata-only caller `.local/CompareNativePersistence29145.ps1` is `EA8EB034AD8C0B29F511A83BD2C4A7A14035350F7F8386FB8B95672704BDC71D`.

| Version attribution | Protected reference | SHA-256 |
| --- | --- | --- |
| R1217 exact retained 29131 reference capture | b42f4943395e43e88e839e052a234f3c:141 | 5D7C54F4794FA4A155879E948D99CFFF609EC6CF2B780719CE57633C33BE1A6F |
| Fresh 29145 native menu | 9d554a389ef44eb4922c6f37b85d8582:1 | DEA283CBFA438EF22C8A1AEFAC3BFBED4059CB7F2192902ED1FDC567C5444849 |

## Required product boundary

The legacy 29104→29131 active-primary disclosure remains evidence version `5`, with its original third-transition prompt. The new 29131→29145 pair uses evidence version `6` and an exact fourth-transition prompt; a legacy version or prompt must not qualify it. Default resume still requires source/runtime version equality. Prepare leaves confirmation false and tick overrides absent, binds the exact primary tick and complete embedded identity, and neither loads nor consumes.

After subsequent confirmation, commit must fresh-check matching unmodified active ticket replicas, the exact primary, native menu revision and the complete original Journal under read-only leases. The durable attempt/tombstone precedes the one native Load. Only after owned identity, planet and tick adoption may the exact fourth transition append with increasing adoption tick and nondecreasing durable sequence. Persist that transition while preserving Journal origin, entries and all three earlier transitions before normal primary resave and target-version credential issuance. A fifth transition, skipped link, changed file, unknown pair or interrupted consumption must refuse rather than replay.

The original finite-construction store refuses a differing document version. Fresh read-only evidence confirms its schema `1`, version29131 and one completed five-object build: entities `6622/6624/6623/6626/6625`, last evidence tick `116214800`, file SHA-256 `272F16A91F2BAC347587F2CC175E3791C964D726A89EADC26C8CB749125A3587`. The original Journal SHA is `BE2E2FC8EBF319800B790773B9BE6397D3C31E56EBEB20D38F18C5E96932FF42`; its 102 entries and three transitions, unchanged credential hash and non-quarantined state are bound by root protected reference `eb097306510a4fe0b5243180958e2a87:1`, SHA-256 `5601D56E9F729E826D1B2EBAA6D6060BA53D45982FB5AA06C7F9D27FFC49964A`.

The candidate retention path permits only completed29131 history on29145, after the same owned identity/current session's complete continuous Journal proves its exact durable transition and has no pending persistence/error. The loaded tick must cover all recorded progress. Every existing plan hash and object receipt is validated first; partial, unknown, corrupt, other-version or unproven history still refuses. Reading returns clones and does not rewrite the file or restore execution authority. Only a subsequent ordinary durable update stamps the target document version while retaining every historical record; failed persistence leaves the source file intact. Never clear the store or repeat completed construction. Linked-store tests use synthetic files and test-only session/ACL substitutes; native entity/connection readback and Windows installation checks remain separate requirements.

Offline method equality, unit/integration tests and compilation do not establish live migration, saved-header readback, future normal resume, production continuity, package installation or complete 0.4 acceptance. Candidate deployment and a fresh native disclosure are separate steps; actual migration remains gated by later user confirmation.
