# Spherewright 当前快照

更新：2026-10-11（Asia/Singapore）。执行 cohort 仍为源码 `369f35c0d7ea352d505ec686cc22a52294d5601a`，Native DLL SHA-256 `6C122E5443E6843979B4064050DFCB5E0D75577A0B64F6AE4111290238B33C12`；本次只交付事实文档。

## 当前保存与窗口

- 最近普通保存为 tick `109172487`、revision `142`、durable Journal `102`。R997 独立核验确认本阶段动作均已保存覆盖，无在途或未知动作；本阶段已结束。
- 固定 20 窗口以 lifetime `502` 开始，当前 `14/20`、lifetime `516`。整案累计 `117` accepted、`34` 次 Move、`33` 次普通保存、`22` 次手工材料动作。R991–R997 本阶段有 2 个 accepted：一次成功 Move 与一次普通保存；原阶段未执行的 7 项动作预算退役，不转移。
- 玩家背包：Warp `300`、Ti Ingot `500`、Rod `5`、Stone Ore `0`、Stone `0`；占用 `27/40` 格，空余 `13` 格。Rod `400` 尚未取用。

## 路线与保管状态

- R991–R997 的第一段普通移动成功。第二段 fresh 向下路线预览观察到 12 个岸线风险样本，未提交第二次移动。没有重放动作或延长原预算。
- 成功前缀通过同一入口内 3 次只读刷新并经运行时边界核验；普通保存覆盖成功 Move。R997 核验了 9 个受影响对象和 P104 本地电网满供；其他星球电网未在本阶段 fresh 核验。
- 保管仓 `286` 的有序缓冲内容保持守恒：Stone Ore `6`、Oil `261`。完整工厂 `6492 built / 0 prebuild` 为已封存静态基线复用，本阶段未重新采集完整全厂帧。

## 未通过的验收门

`sourceConditionsPassed=false`、`wholeSupplyPassed=false`、continuous credit `0`。Rod `400`、Warp/Rod及钛源持续供给、完整物料长窗、双补给、整合恢复和最终同 SHA 双候选验收仍未通过。下一步依据 12 个岸线风险样本设计结构不同的有限路线并重新核验；这不是等待用户授权，也不代表新路线已获 Native 资格。

阶段证据：[R991–R997 北向路线前缀与保存边界](evidence/2026-10-11/northern-prefix-flow-r997.md)、[R982–R988 Rod 容量与保管转存核验](evidence/2026-10-11/rod-capacity-custody-r988.md)、[R783 完整来源长窗](evidence/2026-10-10/current-full-source-long-r783.md)。
