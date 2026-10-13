# R1252 raised iron route and R1253 saved prefix

This evidence records a bounded construction prefix and its ordinary-save boundary. It does not establish source admission or continuous supply.

## Verified work and save

R1252 completed four accepted spans and built 86 belts: 31, 30, 15, and 10 across the spans. The independent audit verified 1,292 original request/receipt pairs. The execution wall time was 2,727.0158 seconds. The completed 124-belt route remains empty and unfed; these spans do not count as source delivery.

R1253 then completed one ordinary save at tick `113712512`. The audit verified 84 original request/receipt pairs for the save stage and 67 complete factory pages: `6616 built`, `0 prebuild`, with the same-owned primary header, fresh ticket header, and complete three-version history checked. The saved boundary is revision `14`, durable Journal `102`. The player inventory contains 53 ordinary belts (`2001`).

The fixed 20-action window is `14/20`, lifetime `566`. Whole-case totals are `167 accepted`, `54` ordinary saves, `297` actual belts, and `18` spans. All current actions are saved, with no unresolved in-flight or unknown result. Earlier unknown outcomes remain historical and were not reclassified or replayed.

R1255 ended with 12 read calls and two prepares for the endpoint sorters. Both Native results were `prepared=true` and `commitAllowedNow=true`, with no Native blockers; neither was committed, so the stage added `0 accepted`. The local material cut then stopped because a power node without material buffers had been included in `materialInventoryObjectIds`, producing `material_inventory_buffers_unavailable_or_unsupported`. Root checked all 12 original pairs and the complete stdout/stderr; the original failure and exit code 1 are retained, and the prepares were not repeated. R1256 is doing a read-only follow-up over 135 material objects, 235 geometry objects, and two existing power proxies; this grants no construction admission. `sourceConditionsPassed=false`, `wholeSupplyPassed=false`, and `continuousCreditTicks=0` remain unchanged. The final same-SHA dual-candidate packages are still outstanding.

The repository source commit `e1ff2bf21f3a6beac65586e4ab4b93cb2eef8a4e` has its own successful zero-Game CI evidence; the installed Native cohort remains `6528e1f7101a714826f7364cef2b0ad75c4b6e82`. The source change was not hot-swapped or deployed.

## Protected independent audits

| Scope | Protected record | SHA-256 | Label |
|---|---|---|---|
| R1252 four-span route and original receipts | `23a9b8e84fdc42558679ca803e39b173:5` | `9627B97C650DABD1540C47603B2F5BFEB268FB615A70237AF4DEEF83CA699670` | `root-r1252-four-original86-belts-and-empty124-chain-independent-audit` |
| R1253 covering save and full factory audit | `23a9b8e84fdc42558679ca803e39b173:9` | `B2C742AC7CE543E2E339F6847B23C3E72E3D53F5A82DC4EB641541289DDFE783` | `root-r1253-four-original86-covering-save-full6616-and-empty124-chain-audit` |
| R1255 two prepare-only results and custody-selector stop | `23a9b8e84fdc42558679ca803e39b173:12` | `3C6C8B5980902D1489F24ABA36D2BCF7C88192A14F802C89E2C49948B22C5745` | `root-r1255-two-original-native-positives-zeroaccepted-and-custody-selector-stop-audit` |
