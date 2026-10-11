# Spherewright 当前快照

更新：2026-10-11（Asia/Singapore）。执行 cohort 仍为源码 `369f35c0d7ea352d505ec686cc22a52294d5601a`，Native DLL SHA-256 `6C122E5443E6843979B4064050DFCB5E0D75577A0B64F6AE4111290238B33C12`；本次只交付事实文档。

## 当前保存与窗口

- 最近普通保存为 tick `109303045`、revision `146`、durable Journal `102`。R1013 独立封窗确认所有当前动作均由保存覆盖，无在途或未知动作。
- 固定 20 窗口从 lifetime `502` 开始，现以 `17/20`、lifetime `519` 提前封窗；未用 3 槽退役、不转移。整案累计 `120` accepted、`36` 次 Move、`34` 次普通保存、`22` 次手工材料动作。accepted 计数包含已接受但终态失败的动作。
- 玩家持有 Warp `300`、Ti `500`、Rod `5`，背包占用 `27/40` 格；核心能量 `1.6 GJ`。Rod `400`尚未取用。

## 封窗与保管状态

- 本阶段请求的三次移动中，两次 accepted；一段成功到达，另一次以 `action_failed` / `native_move_stalled` 结束，181 ticks 内进展小于 `0.75 m`、距目标 `22.44 m`。原 action 已核销，没有重放或恢复，具体碰撞原因未证明。
- R1013完整帧为 `6492 built / 0 prebuild`、65页，相对已核基线无静态变化；三座已加载工厂均满供。保管仓有序缓冲中 Oil `261`、Stone Ore `6`，Stone物料仓存 `2932`。

## 来源与最终验收边界

`sourceConditionsPassed=false`、`wholeSupplyPassed=false`、continuous credit `0`。Rod `400`、Warp/Rod与钛源持续供给、完整供需长窗、双补给、整合保存恢复和最终同 SHA 双候选门仍未通过；库存保全、短窗和封窗均不替代这些验收。

流程数据仅作不同阶段样本：本次移动前缀到覆盖保存约 `11.31 min`，前一成功前缀约 `6.36 min`；两者情境不同，不能作直接性能比较。115次游戏请求调用者约 `49.07 s`，完整本地审计约 `49.40 s`，不涵盖整段协调等待。

阶段证据：[R991–R1013有限路线、保存与封窗](evidence/2026-10-11/saved17-flow-r1013.md)、[R991–R997北向路线前缀](evidence/2026-10-11/northern-prefix-flow-r997.md)、[R783完整来源长窗](evidence/2026-10-10/current-full-source-long-r783.md)。
