# Spherewright 当前快照

更新：2026-10-11（Asia/Singapore）。执行 cohort 仍为源码 `369f35c0d7ea352d505ec686cc22a52294d5601a`，Native DLL 未变（`6C122E5443E6843979B4064050DFCB5E0D75577A0B64F6AE4111290238B33C12`）。R862 的两次移动已成功，第三腿因岸线风险被精确计划校验拒绝；R865 普通保存已覆盖本阶段动作。持续来源和整体供需验收仍未通过。

## 当前保存与窗口

- 最新正常保存 tick `107522697`、revision `101`、durable Journal `102`。固定 20 写窗口从 lifetime `470` 开始，目前 `13/20`、lifetime `483`；R862 两次 Move 与 R865 一次普通 Save 均已核销并由 Save 覆盖，无在途或未知动作。本阶段目的独立结束并冻结，窗口仍开放、未重置累计计数。此前从 lifetime `454` 开始的窗口以 `16/20` 封闭，4 个未用槽已退役、不转移。
- 整案累计 `84` 笔 accepted、`26` 次 Move、`22` 次普通 Save、`10` 次手工材料动作。R865 复用 R858 的完整静态快照 `6492 built / 0 prebuild`、65 页；本阶段没有 fresh 全厂重扫。fresh 受影响对象切片为 17 个对象、静态差异为 0；P104 本地电网满供，其他星球电网未 fresh 核验。封闭窗口时仍须 fresh 完整工厂和预建筑扫描。
- 玩家包内 Ti Ore `50`、Ti Ingot `0`、Warp `400`、Rod `5`、Stone `6` 未变；本阶段未转移物料、未取用钛、未启动短采样。此前 R855/R858、R852、R849 阶段链接见下方索引。

## 来源与验收边界

- `sourceConditionsPassed=false`、`wholeSupplyPassed=false`，continuous credit 为 `0`。R783 连续来源长窗仍未通过；R807 短窗仍是有限供料观察，不能替代来源门。所有既定生产、钛供应、材料稳定性和连续 `36,000` ticks 门槛保持。
- R862 的前两次 Move 成功；第三腿虽有 Native prepare 与 commitAllowed 正结果，但 28 个岸线样本中 26 个触发 shore risk，精确计划校验拒绝该腿，没有第三腿 accepted。湿地外缘、湿东南、湿东北路线族、旧失败目标及 orthogonal/4 m 候选族均已退役，不重试。`routeClearanceProven=false`、`waterFreeRouteProven=false`。
- 下一步仅做一个预先声明的 Native 目标地表预览，零 commit，用于为下一次正常接近选择有证据的目标；不扫候选点、不在水面中途停靠、不盲目移动。此后仍须按既有渡水规则和范围执行，才能考虑 Ti Ore `400`、Ti Ingot `200` 取料、覆盖保存及三个独立 Native `600` 短资格样本。双自动补给、整合保存恢复和最终同 SHA 双候选仍未通过；先前通过的蓝图/Governor 等门保持有效。

阶段索引：[R862/R865 两次移动、第三腿拒绝与覆盖保存](evidence/2026-10-11/native-north-two-moves-bounded-save-process-r865.md)、[R855/R858 四次移动、第五腿拒绝与保存](evidence/2026-10-11/native-inland-four-moves-shore-refusal-covering-save-r858.md)、[R852 内陆接近与保存](evidence/2026-10-11/native-inland-ti-approach-and-save-r852.md)、[R849 湿地外缘预览](evidence/2026-10-11/zero-accepted-shore-preview-r849.md)、[R845 西侧移动与保存](evidence/2026-10-11/native-west-approach-and-save-r845.md)、[R839 移动停止、保存与流程核验](evidence/2026-10-11/native-route-stop-save-and-process-r839.md)、[R783 完整来源长窗](evidence/2026-10-10/current-full-source-long-r783.md)、[R807 磁铁供料短窗观察](evidence/2026-10-11/magnet-feed-short-r807.md)。
