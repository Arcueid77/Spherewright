# Spherewright 当前快照

更新：2026-10-11（Asia/Singapore）。执行 cohort 仍为源码 `369f35c0d7ea352d505ec686cc22a52294d5601a`，Native DLL SHA-256 `6C122E5443E6843979B4064050DFCB5E0D75577A0B64F6AE4111290238B33C12`；最近保存业务仍是R1109正常接近Move及覆盖保存，之后R1112/R1114仅有界原生路线预检与只读诊断，无新游戏写入。

## 当前保存与窗口

- 最近普通保存：tick `110593929`、revision `178`、durable Journal `102`。R1109的一次正常接近Move与普通保存共2笔accepted动作已由独立核验确认覆盖保存，无在途或未知动作。
- 当前固定20窗口以 lifetime `535` 开始，`7/20`、lifetime `542`，本阶段已结束且写入冻结；R1088先前窗口在 `16/20` 封闭，未用4槽已退役、不转移。整案累计 `143` accepted、`45` 次Move、`43` 次普通保存、`27` 次手工材料动作。
- 背包现有铁块（`1101`）`50`、传送带 `177`、Rod `5`、Warp `900`、Ti `500`，占用 `34/40` 格；本阶段完整背包及held/inc守恒。29m正常接近Move的到达位置误差为 `0.0362m`；没有取材或新建造。

## 当前来源观察

- R1112的完整铁源路线预检与R1114的独立切面确认：拟议铁路线在第15个原生点遇到既有对象 `1868`，Native返回 `BUILD_LOCATION_INVALID`，创建数为0且同布局不重试。该对象属于硅矿物料路径；切面记录主路径 `42`（194段带）和支路 `44`（39段带），5条完整Native货物流路径及既有功能保留。这证明当前几何接口冲突，不证明它是唯一铁源瓶颈。R1114用7次只读请求核对213个相关实体，没有重采完整工厂；完整帧仍复用R1088。R1106此前通过的 `1501` 仅是短端预览；R1109是正常接近Move与覆盖保存，永久供料线 `6477→1501` 未施工。Warp氘支路保留要求见R1082/R1088。
- 最新来源速率仍来自R1085的单个600-tick样本：铁矿 `1001` 为 `6/min`、铁块 `1101`和磁铁 `1102`为 `0/min`、物品 `1121`为 `30/min`、Warp `1210`和Rod `1802`为 `0/min`。R1109复用了R1088的 `6492 built / 0 prebuild` 完整帧，没有新采全厂或电力；因此不能称本阶段fresh全厂或供电核验。共享氘切面及封窗边界见[R1088阶段证据](evidence/2026-10-11/shared-deuterium-iron-gap-window-close-r1088.md)。

## 验收边界与下一步

`sourceConditionsPassed=false`、`wholeSupplyPassed=false`、continuous credit `0`。当前固定20窗口为 `7/20` 且冻结；共享氘/铁来源、Warp和Rod持续供给、钛源持续供给、完整供需长窗、双自动补给、整合保存恢复及最终同 SHA 双候选门仍未通过。短端资格与旧600-tick读数均不能替代这些验收。

下一步是为既有硅矿线选择结构不同的有限铁源绕行，并重新资格化Native源、路线和汇端；当前没有施工批准。R1112/R1114没有创建对象、没有accepted写入，也没有重试碰撞布局。还需有界打开钛消费者需求并保留Warp所需氘分支。R1114复用旧完整帧，不构成新鲜全厂/电力证据。若后续动作超出当前窗口余量，按规则封窗后再开新固定20窗，不重置或扩展额度。

近期流程审计（R1107）显示，R1109批准到首个Game请求为 `94.150` 秒，调用器记录的首个prepare阶段计时为 `1.276` 秒；R1112分别为 `86.505` 秒和 `1.652` 秒。`firstBusinessPrepareSeconds` 是调用器阶段内部字段，不代表批准/委派到prepare的端到端间隔。R1112的writer manifest到root seal为 `21.149` 秒，原件审核为 `0.624` 秒；主要可见开销仍在首次Game请求之前。单写者、合批、固定窗口、完整帧复用与事实CI并行仍在使用，但Native结果和范围不同，缺少同口径整体历史基线，不能据此宣称整体提速或变慢。此前流程记录见[R1075](evidence/2026-10-11/owned-stock-warp-demand-save-r1075.md)与[R1094](evidence/2026-10-11/finite-iron200-kit-saved-flow-r1094.md)，本次计时及边界见[R1114](evidence/2026-10-11/iron-route-existing-silicon-barrier-r1114.md)。

阶段证据：[正常接近Move与1501短端资格](evidence/2026-10-11/finite-iron-route-approach-saved-r1109.md)、[有限传送带备料与保存](evidence/2026-10-11/finite-belt150-kit-saved-r1099.md)、[有限铁块备料与保存](evidence/2026-10-11/finite-iron200-kit-saved-flow-r1094.md)、[共享氘支路、铁源缺口与R1088封窗](evidence/2026-10-11/shared-deuterium-iron-gap-window-close-r1088.md)、[Rod/Warp转存与来源短窗](evidence/2026-10-11/owned-stock-warp-demand-save-r1075.md)、[完整来源长窗](evidence/2026-10-10/current-full-source-long-r783.md)。
