# R1250 saved prefix and R1251 original-action completion

This note records the independently checked completion and save boundary. It does not claim that the remaining source route or whole-supply gate passed.

## Verified boundary

The R1248 belt-route action was invoked once. Its caller timed out after 600 seconds. R1251 later read the same original action and verified its successful terminal result: 25 belts completed after 9,458 game ticks. The action was not replayed. The completion is bounded by the later read, but the exact wall-clock completion time is unavailable.

R1250 then completed one ordinary save at tick `113665126`. The saved state has revision `5` and durable Journal `102`. The fixed 20-action window opened at lifetime `552` and is now `9/20`, lifetime `561`. Cumulative totals are 162 accepted actions, 53 ordinary saves, 211 built belts, and 14 spans. The verified actions are covered by the save; no unaccounted in-flight action remains.

The independent save audit verified a complete factory snapshot with 6,530 built entities and 0 prebuild entities, 38 existing route belts still empty and unfed, and 139 ordinary belts (`2001`) in the player's inventory. These observations do not establish a supplied source route. The remaining R1252 stage is executing under a limit of four accepted actions, 3,700 requests, and 7,200 seconds, for four spans and 86 empty belts. It has no terminal result or independent acceptance yet; do not treat it as passed or as source-side delivery.

`sourceConditionsPassed=false`, `wholeSupplyPassed=false`, and `continuousCreditTicks=0` remain in force. Final same-SHA dual-candidate delivery is also outstanding.

## Protected evidence

| Check | Protected record | SHA-256 | Label |
|---|---|---|---|
| R1251 original-action terminal and full-factory audit | `b42f4943395e43e88e839e052a234f3c:239` | `A9CEE7F6D0EC7455C5450F5C0D19ED5028A62E0AF2937B6BACCDBF56210FE8C8` | `root-r1251-original25-belts-terminal-complete-factory-and-empty38-chain-audit` |
| R1250 covering save and independent audit | `23a9b8e84fdc42558679ca803e39b173:1` | `5BFAE0A85F5AB0CD8617C1E62C6ACDA91BB956180C75512E6F781AB68D407080` | `root-r1250-original25-covering-save-full6530-factory-and-empty38-chain-audit` |
