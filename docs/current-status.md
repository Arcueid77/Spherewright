# Spherewright 当前快照

更新：2026-10-11（Asia/Singapore）。执行 cohort 仍为源码 `369f35c0d7ea352d505ec686cc22a52294d5601a`，Plugin DLL 未变。R845 完成了一次有限西侧移动与普通保存；持续来源条件和整体供需验收仍未通过。

## 当前保存与窗口

- 最新正常保存 tick `107325956`、revision `82`、durable Journal `102`。新固定 20 写窗口从 lifetime `470` 开始，现为 `2/20`、lifetime `472`；本阶段 1 次正常 Move 与 1 次普通 Save 均已保存覆盖，目的独立结束并冻结，无在途或未知动作。窗口尚未封闭，也未重置累计计数。之前从 lifetime `454` 开始的窗口已以 `16/20` 封闭，4 个未用槽退役、不转移。
- R845 完成约 23 m 的正常西侧 Move 并到达，随后普通保存；共 88 请求、约 `32.89 s`。整案累计 `73` 笔 accepted、`18` 次 Move、`19` 次普通 Save。
- 最近完整工厂为 `6492 built / 0 prebuild`、65 页；本阶段记录没有静态变化。本次核验的本地 3 个电网满供，其他星球电网未 fresh 核验。玩家包内 Ti Ore `50`、Ti Ingot `0`、Warp `400`、Rod `5`、Stone `6` 未变。本阶段未取用 Ti Ore、Ti Ingot 或 Warp，也未启动短采样。

## 来源与验收边界

- `sourceConditionsPassed=false`、`wholeSupplyPassed=false`，continuous credit 为 `0`。R783 连续来源长窗仍未通过；R807 短窗仍是有限供料观察，不能替代来源门。所有既定生产、钛供应、材料稳定性和连续 `36,000` ticks 门槛保持。
- 下一项是基于已核验拓扑 fresh 资格化结构不同的北向有限路线，之后才考虑有限取料、保存和短资格采样，再决定正式长窗。本记录不表示路线施工、钛供料或持续生产通过，也不代表存在本地 flight 能力。双自动补给、整合保存恢复和最终同 SHA 双候选仍未通过。
- 先前通过的蓝图/Governor 等门保持有效；失败目标及既有 orthogonal/4 m 候选族继续退役，不重放。

阶段索引：[R845 西侧移动与保存](evidence/2026-10-11/native-west-approach-and-save-r845.md)、[R839 移动停止、保存与流程核验](evidence/2026-10-11/native-route-stop-save-and-process-r839.md)、[R783 完整来源长窗](evidence/2026-10-10/current-full-source-long-r783.md)、[R807 磁铁供料短窗观察](evidence/2026-10-11/magnet-feed-short-r807.md)。
