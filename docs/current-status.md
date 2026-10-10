# Spherewright 当前快照

更新：2026-10-11（Asia/Singapore）。执行 cohort 仍为源码 `369f35c0d7ea352d505ec686cc22a52294d5601a`，Native DLL 未变（`6C122E5443E6843979B4064050DFCB5E0D75577A0B64F6AE4111290238B33C12`）。R881–R885 有界移动阶段已保存并独立核验；石料来源及持续供需验收仍未通过。

## 当前保存与窗口

- 最新普通保存 tick `107723060`、revision `108`、durable Journal `102`。固定 20 写窗口从 lifetime `470` 开始，目前 `17/20`、lifetime `487`；本阶段三次成功移动和一次普通保存均已核销并由保存覆盖，无在途或未知动作。本阶段目的已独立结束并冻结，窗口仍开放；未使用的额度不转移。
- 整案累计 `88` 笔 accepted、`29` 次移动、`23` 次普通保存、`10` 次手工材料动作。玩家位于 P104，Walk 速度 `0`；背包 Ti Ore `50`、Ti Ingot `0`、Warp `400`、Rod `5`、Stone `6`，本阶段无转移或短采样。
- R885 fresh 检查了 17 个受影响对象，静态差异为 `0`，并核验 P104 本地电网满供。完整 `6492 built / 0 prebuild`、65 页数据来自 R858 封存快照复用，不是本阶段 fresh 全厂重扫；其他星球电网也未 fresh 核验。后续封窗仍需 fresh 完整工厂与预建筑扫描。

## 路线与验收边界

- R881 的前三次普通移动完成。第四腿原生 prepare 与 `commitAllowed=true`，但 28 个岸线样本中 26 个有岸线风险，精确计划校验拒绝提交，`0` commit / accepted；目标处于水下 `1.7679 m`。该湿终点候选退役，不重试。先前湿地与 orthogonal/4 m 候选族继续保持退役，`routeClearanceProven=false`、`waterFreeRouteProven=false`。
- `sourceConditionsPassed=false`、`wholeSupplyPassed=false`，continuous credit 为 `0`。Ti Ore/Ti Ingot 未取用，短采样未开始；R783 连续来源长窗未通过的结果保持。Warper/Rod 产能及稳定库存、全部来源、双自动补给、整合保存恢复、最终同 SHA 双候选等既定门仍未通过。
- 下一步从当前已停稳位置做一次预声明的正常接近；只有 fresh 确认实际地表停稳且距取材目标不超过 `76 m` 后，才进入后续有限取材/接线核验。不得扫候选点、重试退役目标或把水面停靠当作通过。后续仍须执行原有渡水与连续供需验收，不降低门槛。

阶段索引：[R881–R885 南侧三次移动、湿终点拒绝与覆盖保存](evidence/2026-10-11/known-walk-south-three-moves-and-bounded-save-process-r885.md)、[R858 内陆四次移动与覆盖保存](evidence/2026-10-11/native-inland-four-moves-shore-refusal-covering-save-r858.md)、[R783 完整来源长窗](evidence/2026-10-10/current-full-source-long-r783.md)、[R807 磁铁供料短窗观察](evidence/2026-10-11/magnet-feed-short-r807.md)。
