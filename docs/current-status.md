# Spherewright 当前快照

更新：2026-10-11（Asia/Singapore）。执行 cohort 仍为源码 `369f35c0d7ea352d505ec686cc22a52294d5601a`，Native DLL 未变（`6C122E5443E6843979B4064050DFCB5E0D75577A0B64F6AE4111290238B33C12`）。R852 完成两次正常移动及一次普通保存；当前目的独立结束并冻结，但固定 20 写窗口仍开放。来源与整体供需验收仍未通过。

## 当前保存与窗口

- 最新正常保存 tick `107417528`、revision `87`、durable Journal `102`。本窗口从 lifetime `470` 开始，目前 `5/20`、lifetime `475`；R852 的 2 次 Move 和 1 次 Save 均已核销并由保存覆盖，无在途或未知动作。窗口仍开放、写入冻结且未重置累计计数。此前从 lifetime `454` 开始的窗口以 `16/20` 封闭，4 个未用槽已退役、不转移。
- R852 共 94 次请求、约 `33.1647 s`，完成两次正常岸内/内陆移动及一次普通保存。整案累计 `76` 笔 accepted、`20` 次 Move、`20` 次普通 Save、`10` 次手工材料动作。
- 最新完整工厂为 `6492 built / 0 prebuild`、65 页；本阶段静态差异、新增、移除和非互惠连接均为 0。已核验的 P104 本地电网满供；其他星球电网未 fresh 核验。玩家包内 Ti Ore `50`、Ti Ingot `0`、Warp `400`、Rod `5`、Stone `6` 未变。本阶段未取用 Ti Ore、Ti Ingot 或 Warp，也未启动短采样。

## 来源与验收边界

- `sourceConditionsPassed=false`、`wholeSupplyPassed=false`，continuous credit 为 `0`。R783 连续来源长窗仍未通过；R807 短窗只是有限供料观察，不能替代来源门。所有既定生产、钛供应、材料稳定性和连续 `36,000` ticks 门槛保持。
- 下一步是 fresh 资格化一条合法的有限路线，进入仓库 `259/531` 的施工范围；之后才执行已界定的 Ti Ore `400`、Ti Ingot `200` 取料、覆盖保存及三个独立 Native `600` 短资格样本。本记录不证明路线已清障、施工范围已到达、钛供料或持续生产通过。
- 西侧/南侧湿地外缘 R849 路线已退役，R849 检出的旧失败目标和 orthogonal/4 m 候选族也继续退役，不重放。双自动补给、整合保存恢复和最终同 SHA 双候选仍未通过。先前通过的蓝图/Governor 等门保持有效。

阶段索引：[R852 内陆接近与保存](evidence/2026-10-11/native-inland-ti-approach-and-save-r852.md)、[R849 湿地外缘预览拒绝](evidence/2026-10-11/zero-accepted-shore-preview-r849.md)、[R845 西侧移动与保存](evidence/2026-10-11/native-west-approach-and-save-r845.md)、[R839 移动停止、保存与流程核验](evidence/2026-10-11/native-route-stop-save-and-process-r839.md)、[R783 完整来源长窗](evidence/2026-10-10/current-full-source-long-r783.md)、[R807 磁铁供料短窗观察](evidence/2026-10-11/magnet-feed-short-r807.md)。
