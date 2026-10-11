# Spherewright 当前快照

更新：2026-10-12（Asia/Singapore）。执行 cohort 仍为源码 `369f35c0d7ea352d505ec686cc22a52294d5601a`，Native DLL SHA-256 `6C122E5443E6843979B4064050DFCB5E0D75577A0B64F6AE4111290238B33C12`；最近保存为R1123正常接近Move及覆盖保存，R1125完成内部铁源坡道端点的Native资格核验但未施工。

## 当前保存与窗口

- 最近普通保存：tick `110924907`、revision `181`、durable Journal `102`。R1123的一次8m正常接近Move与普通保存共2笔accepted动作已由独立核验确认覆盖保存，无在途或未知动作。
- 当前固定20窗口以 lifetime `535` 开始，`9/20`、lifetime `544`；当前阶段已独立结束，窗口仍开放且写入冻结。整案累计 `145` accepted、`46` 次Move、`44` 次普通保存、`27` 次手工材料动作。
- 背包现有铁块（`1101`）`50`、传送带 `177`、Rod `5`、Warp `900`、Ti `500`，占用 `34/40` 格；R1123完整背包及held/inc守恒，Move到达误差 `0.1546m`，无材料差量。

## 当前来源观察

- R1112的完整铁源路线预检与R1114的独立切面确认：拟议铁路线在第15个原生点遇到既有对象 `1868`，Native返回 `BUILD_LOCATION_INVALID`，创建数为0且同布局不重试。该对象属于硅矿物料路径；切面记录主路径 `42`（194段带）和支路 `44`（39段带），5条完整Native货物流路径及既有功能保留。这证明当前几何接口冲突，不证明它是唯一铁源瓶颈。R1114用7次只读请求核对213个相关实体，没有重采完整工厂；完整帧仍复用R1088。R1106此前通过的 `1501` 仅是短端预览；R1109是正常接近Move与覆盖保存，永久供料线 `6477→1501` 未施工。Warp氘支路保留要求见R1082/R1088。
- 后续R1116朝向参数检查、R1117与R1119端点放置检查均在Native调用前被本地护栏拦截（`NativeCheckPerformed=false`），因此它们既不是Native成功，也不是Native拒绝。R1116报告source端点过斜；R1117与R1119分别与既有对象 `1213`、`6492` 重叠，未创建对象、未accepted。末端D尚未尝试。`6477`侧向接入候选族已停止；后续内部坡道资格见下文，施工仍未获准。
- R1123完成正常8m接近Move与覆盖保存。R1125对内部source端 `6480` 与destination端 `1501` 的坡道端点均执行了Native检查，结果为 `Ok`、跨度均为 `2`；源/目的分别规划13和11段新带，两端当前各有13/11个原生点证据。217对象切面保留 `6480` 前后 `6479/6481`、既有 `6477→6492→1220` 与硅矿路径；3张本地电网满供。资格仅覆盖坡道端点，不代表施工或未来新建ID的接缝已通过；实际抬升接合仍待建成后重新资格。source、wholeSupply与连续信用仍未通过。
- 最新来源速率仍来自R1085的单个600-tick样本：铁矿 `1001` 为 `6/min`、铁块 `1101`和磁铁 `1102`为 `0/min`、物品 `1121`为 `30/min`、Warp `1210`和Rod `1802`为 `0/min`。R1109复用了R1088的 `6492 built / 0 prebuild` 完整帧，没有新采全厂或电力；因此不能称本阶段fresh全厂或供电核验。共享氘切面及封窗边界见[R1088阶段证据](evidence/2026-10-11/shared-deuterium-iron-gap-window-close-r1088.md)。

## 验收边界与下一步

`sourceConditionsPassed=false`、`wholeSupplyPassed=false`、continuous credit `0`。当前固定20窗口为 `7/20` 且冻结；共享氘/铁来源、Warp和Rod持续供给、钛源持续供给、完整供需长窗、双自动补给、整合保存恢复及最终同 SHA 双候选门仍未通过。短端资格与旧600-tick读数均不能替代这些验收。

下一提案是在已资格的内部 `6480` source端与 `1501` destination端分别建坡道并覆盖保存，计划需24段新带；该接合后须对实际新建ID重新资格，再考虑连接带或启用分拣器。R1126事实中的总体额外预算上限为9 accepted，Move与保存已用2、提案余7；此方案尚未批准施工。`6477`侧向接入候选族保持停止。还需有界打开钛消费者需求并保留Warp所需氘分支；R1114复用旧完整帧，不构成新鲜全厂/电力证据。若后续动作超出当前窗口余量，按规则封窗后再开新固定20窗，不重置或扩展额度。

近期流程对比仍显示root核验时长有明显波动：R1123正常Move与Save的调用器用时 `7.355` 秒、root独立审计 `0.920` 秒；R1125坡道只读调用器用时 `5.861` 秒、root完整原件审计 `43.321` 秒。此前约 `1003` MB内存清理后仍出现较长审计样本；缓存清理是否改善整体效率及内存压力是否为原因均未证明。单写者、相邻动作与覆盖保存、固定窗口、完整帧复用、预先准备独立root审计及事实CI并行仍在使用，但不能据这两组短样本宣称整体效率已恢复。此前流程记录见[R1075](evidence/2026-10-11/owned-stock-warp-demand-save-r1075.md)、[R1094](evidence/2026-10-11/finite-iron200-kit-saved-flow-r1094.md)与[R1114](evidence/2026-10-11/iron-route-existing-silicon-barrier-r1114.md)，本次计时与坡道资格见[R1125](evidence/2026-10-12/iron-interior-ramp-qualification-r1125.md)。

阶段证据：[正常接近Move与1501短端资格](evidence/2026-10-11/finite-iron-route-approach-saved-r1109.md)、[有限传送带备料与保存](evidence/2026-10-11/finite-belt150-kit-saved-r1099.md)、[有限铁块备料与保存](evidence/2026-10-11/finite-iron200-kit-saved-flow-r1094.md)、[共享氘支路、铁源缺口与R1088封窗](evidence/2026-10-11/shared-deuterium-iron-gap-window-close-r1088.md)、[Rod/Warp转存与来源短窗](evidence/2026-10-11/owned-stock-warp-demand-save-r1075.md)、[完整来源长窗](evidence/2026-10-10/current-full-source-long-r783.md)。
