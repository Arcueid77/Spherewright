# Spherewright 当前快照

更新：2026-10-11（Asia/Singapore）。执行 cohort 仍为源码 `369f35c0d7ea352d505ec686cc22a52294d5601a`，Native DLL SHA-256 `6C122E5443E6843979B4064050DFCB5E0D75577A0B64F6AE4111290238B33C12`；本次只交付事实文档。

## 当前保存与窗口

- 最近普通保存为 tick `109570338`、revision `153`、durable Journal `102`。R1038 独立核验确认当前目的动作均由保存覆盖，无在途或未知动作。
- 固定 20 窗口以 lifetime `519` 开始，现为 `5/20`、lifetime `524`；窗口仍开放但冻结，本目的已结束，剩余 3 个本目的预算槽位退役。整案累计 `125` accepted、`39` 次 Move、`36` 次普通保存、`22` 次手工材料动作。accepted 包含一笔已接受但终态失败的 Move。
- 本目的的一次 Move 在 `180` ticks 后以 `position_stalled` 结束，距目标约 `3.3696 m`；原动作已核销，没有重放，具体碰撞原因未证明。随后普通保存覆盖当前动作。玩家持有 Warp `300`、Ti `500`、Rod `5`，背包占用 `27/40` 格，核心能量 `1.6 GJ`；Rod `400` 尚未取用。

## 快照与保管

- 本次复用了 R1013 的 `6492 built / 0 prebuild`、65 页完整静态帧，没有重采完整工厂。P104 本地电网本阶段 fresh 满供；另外两座工厂的电网最新 fresh 证据来自 R1013。
- 保管记录为 Oil `261`、原石 `6`、石料仓 `2932`。这些库存不证明持续产量或来源通过。

## 验收边界与流程

`sourceConditionsPassed=false`、`wholeSupplyPassed=false`、continuous credit `0`。Rod `400`、Warp/Rod与钛源持续供给、完整供需长窗、双补给、整合保存恢复和最终同 SHA 双候选门仍未通过。

本目的从已停滞 Move 到覆盖保存约 `11.471 min`；Move 调用 `13` 请求、约 `13.300 s`，保存 `15` 请求、约 `3.519 s`。四次只读调用约 `1.267 s`；独立前缀、四读与保存审计约 `0.580 s`、`0.256 s` 和 `0.355 s`。这些局部耗时不代表端到端提速。

阶段证据：[R1030–R1038南向转弯停滞与保存](evidence/2026-10-11/south-turn-stall-save-r1038.md)、[R1019–R1027裙边路线前缀与保存](evidence/2026-10-11/row-skirt-prefix-r1027.md)、[R1013保存封窗](evidence/2026-10-11/saved17-flow-r1013.md)、[R783完整来源长窗](evidence/2026-10-10/current-full-source-long-r783.md)。
