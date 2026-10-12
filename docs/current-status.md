# Spherewright 当前快照

更新：2026-10-13（Asia/Singapore）。当前实际安装源码仍为 67ccc62936c0594a62568b46cb4f79c0718ee89c，228 文件、64 工具/1资源。R1237首段普通施工完成，第二段accepted后为outcome_unknown；R1238独立完整核验确认第二段0扣料/0新增，但Native隔离仍在。接缝绑定哈希的窄修复已通过279项直接相关Core回归及当前29131引用下Plugin Release构建（0警告/错误），未部署/未实机施工。持续来源、wholeSupply 与最终同 SHA 双候选包仍未通过/交付，无正式发布。

## 当前保存与窗口

最近普通保存仍为 tick 113054820、revision 2、durable Journal 102，游戏版本0.10.35.29131，尚未覆盖首段13条带。最新原生观察tick113267791、revision5，writeHealth=quarantined、writesAllowed=false，绑定R1237第二笔原action；不能将DTO的ownedSaveState=saved当成新施工已保存。当前固定20窗口从lifetime552开始，目前5/20、lifetime557，保持冻结并保留原intent及Save槽。整案累计158 accepted、46次Move、51次显式普通Save、27次手工材料动作；实际建成累计186条带/13段，第二段计划25条与已接受计数分开。上一窗口17笔原动作已核销，未用3槽仍退役。

R1234的同版本 exact-primary healthy Load 与内建重存已由独立原件核验：tick 113040701、revision 1、Journal 102；R1235后续普通保存到tick 113054820、revision 2、Journal 102。

## 抬升尾段与来源边界

R1236首段Native准备成功但caller误比较组合建造hash与玩家hash，独立核7帧为0commit/0accepted；修正后的实际File smoke含18个守卫，未重放旧入口。R1237首段0→3的13条带正常扣料177→164并由无人机建成；第二段3→2的完整Native计划25条通过并accepted，但启动异常后进入隔离，后四段和两端分拣器未执行。抬升source-cover的prepare正例不提升为施工正例。

R1238仅77个只读/原action证明preview请求、0写入。完整66页工厂为6505实体/0预建，相对R1235完整基线仅增加首段13条带，旧静态配置、连接、全背包count/inc/held及Journal102/三条版本记录保全，本地网络满供。Native精确核销preview拒绝：保留动作的材料差量0而非计划-25。原accepted与历史unknown不清零、不改判未提交。最近主档早于已建前缀；后续仅准备同owned固定LastExit受保护恢复，实际隔离恢复仍须新的明确确认。材料现为164条带、2011/2012各1个。

sourceConditionsPassed=false、wholeSupplyPassed=false、连续来源信用0，最终同SHA双候选包未交付。当前原件、代码修复与恢复边界见[首段施工和接缝隔离](evidence/2026-10-13/raised-source-creation-binding-and-quarantine-r1238.md)，此前成功保存恢复见[维护证据](evidence/2026-10-12/raised-tail-maintenance-and-healthy-resume-r1235.md)。
