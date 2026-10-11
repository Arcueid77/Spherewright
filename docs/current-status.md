# Spherewright 当前快照

更新：2026-10-11（Asia/Singapore）。执行 cohort 仍为源码 `369f35c0d7ea352d505ec686cc22a52294d5601a`，Native DLL SHA-256 `6C122E5443E6843979B4064050DFCB5E0D75577A0B64F6AE4111290238B33C12`；本次只交付事实文档。

## 当前保存与窗口

- 最近普通保存为 tick `109463292`、revision `151`、durable Journal `102`。R1027 独立核验确认本目的动作均已保存覆盖，无在途或未知动作；本目的结束。
- 新固定 20 窗口以 lifetime `519` 开始，当前 `3/20`、lifetime `522`；窗口仍开放但冻结。整案累计 `123` accepted、`38` 次 Move、`35` 次普通保存、`22` 次手工材料动作。当前目的包含两次成功 Move 与一次普通保存；第三段 prepare 因 5 个岸线风险样本被精确策略拒绝，未 commit、未 accepted。剩余 3 项目的预算退役。
- 玩家持有 Warp `300`、Ti `500`、Rod `5`；占用 `27/40` 格，核心能量 `1.6 GJ`。Rod `400` 尚未取用。

## 快照与电力边界

- 本次封存复用了 R1013 的 `6492 built / 0 prebuild` 完整静态帧，没有重采完整工厂。P104 本地电网本阶段 fresh 满供；另外两座工厂的电网最新 fresh 证据来自 R1013。
- 保管记录为 Oil `261`、原石 `6`、石料仓 `2932`。这些库存不代表持续产量或来源通过。

## 未通过的验收门与流程

`sourceConditionsPassed=false`、`wholeSupplyPassed=false`、continuous credit `0`。Rod `400`、Warp/Rod与钛源持续供给、完整供需长窗、双补给、整合保存恢复和最终同 SHA 双候选门仍未通过。

本次 prefix-to-save 约 `8.770 min`，R1013约 `11.31 min`、R997约 `6.36 min`，样本情境不同、不能直接比较。移动请求共25次约13.204秒；保存15次约3.556秒，独立本地前缀/保存审计约0.821/0.394秒。这些局部耗时不能代表端到端提速。

阶段证据：[R1019–R1027裙边路线前缀与保存](evidence/2026-10-11/row-skirt-prefix-r1027.md)、[R1013保存封窗](evidence/2026-10-11/saved17-flow-r1013.md)、[R991–R997北向路线前缀](evidence/2026-10-11/northern-prefix-flow-r997.md)、[R783完整来源长窗](evidence/2026-10-10/current-full-source-long-r783.md)。
