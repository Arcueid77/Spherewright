# Spherewright 当前快照

更新：2026-10-11（Asia/Singapore）。执行 cohort 仍为源码 `369f35c0d7ea352d505ec686cc22a52294d5601a`，Native DLL SHA-256 `6C122E5443E6843979B4064050DFCB5E0D75577A0B64F6AE4111290238B33C12`；本次只交付事实文档。

## 当前保存与窗口

- 最近普通保存为 tick `109649188`、revision `156`、durable Journal `102`。R1046 独立核验确认本目的动作均由保存覆盖，无在途或未知动作。
- 固定 20 窗口以 lifetime `519` 开始，现为 `7/20`、lifetime `526`；窗口仍开放但冻结，本目的已结束，剩余 3 个本目的预算槽位退役。整案累计 `127` accepted、`40` 次 Move、`37` 次普通保存、`22` 次手工材料动作。
- 本目的第一段短程 Move 成功；第二段 Native prepare 虽 positive，3 个岸线风险样本触发既有本地路线门，未 commit、未 accepted。玩家持有 Warp `300`、Ti `500`、Rod `5`，背包占用 `27/40` 格，核心能量 `1.6 GJ`；Rod `400` 尚未取用。

## 快照与保管

- 本次复用了 R1013 的 `6492 built / 0 prebuild`、65 页完整静态帧，没有重采完整工厂。P104 本地电网本阶段 fresh 满供；另外两座工厂的电网最新 fresh 证据来自 R1013。
- 保管记录为 Oil `261`、原石 `6`、石料仓 `2932`。这些库存不证明持续产量或来源通过。

## 验收边界与流程

`sourceConditionsPassed=false`、`wholeSupplyPassed=false`、continuous credit `0`。Rod `400`、Warp/Rod与钛源持续供给、完整供需长窗、双补给、整合保存恢复和最终同 SHA 双候选门仍未通过。

本次已完成前缀到覆盖保存约 `7.0 min`；Move 调用 `14` 请求、约 `4.719 s`，保存 `15` 请求、约 `4.074 s`。root 前缀与保存审计约 `0.488 s`、`0.399 s`。批准到首个业务 prepare 约 `109.221 s`，该间隔不是精确委派耗时。这些局部耗时不代表端到端提速。

阶段证据：[R1039–R1046东北路线前缀与保存](evidence/2026-10-11/northeast-prefix-save-r1046.md)、[R1030–R1038南向转弯停滞与保存](evidence/2026-10-11/south-turn-stall-save-r1038.md)、[R1013保存封窗](evidence/2026-10-11/saved17-flow-r1013.md)、[R783完整来源长窗](evidence/2026-10-10/current-full-source-long-r783.md)。
