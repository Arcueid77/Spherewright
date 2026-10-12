# R1198–R1206 多候选只读勘探与整线预览异常

本阶段沿用同一已保存布局，按批读取原生点列、离线筛选结构不同的走廊，复用成功路段，统一在本页汇总。Game writes、accepted、施工、Move、Save、Load、部署均为 0。原 survey 的两次完整预览额度已结束并保持 2；R1205 为单独批准的新有限目的，只消费一次整线只读预览，没有自动重试。

## 边界与保护对象

安装源码仍为 0df97408058badfcb3e93b40f77953b4a0568347。最后可核正常保存为 tick 112145524、revision 2、durable J102；固定20窗开于 lifetime535，仍15/20、lifetime550、剩5且冻结，无在途/unknown。65页完整静态快照的6492实体/0预建对象复用；关键实体与互惠连接逐项无变化。原铁源6476→6491…6477→6492→1220…1223→1216及硅矿主path42/194带、支path44/39带、共享氘/粒子容器支路均未改接或损坏。对象1501现场类型为普通传送带2001；旧描述中的“分拣器1501”已纠正。

R1198新读64个地形探针、每点4层原生坐标与36个对象；随后共21段原生点列完整返回。R1205前后各读18个关键对象，其运行时碰撞框、静态配置与端口连接同缓存一致；最后一次当地三网电力为新鲜单点背景。2993几何对象、3010碰撞框、旧452地形点、源端13点/目标11点及旧成功端点预览为缓存复用。五条既有cargo path的动态库存及三厂长期电力未刷新，不能计入本轮持续供给/功率验收。自然游戏时间和生产可继续，不把零Agent写入描述为全部动态缓存冻结。

## 候选及批量淘汰

| 候选族 | 源→目标 | 生成点/跨度 | 原因与结论 |
|---|---|---:|---|
| 原A北侧 | 6478→1501 | 96/6 | 旧批局部包围框相交，端点/整线资格未得；保留原筛选结论，不重送 |
| 原B外圈 | 6480→1501 | 156/9 | 最大107.726米超80米，旧批相交对象；不重送 |
| 原C换入口 | 6489→1501 | 144/9 | 最大88.117米超80米；新源端逃逸片段仍85.718/99.706米，淘汰 |
| 西侧换出口 | 6479→1501 | 源片25或15 | 一次错误quarter-turn得到TooSkew；按原生球面坐标约定修正后，planned sorter与采矿机1213重叠，候选族关闭 |
| 宽东135全高层 | 6480→1501 | 124/6 | 原生点80与旧带5056中心重合；降低中段后仍提前上升至同一旧带，淘汰该全高/早升尾段 |
| 宽西南225 | 6480→1501 | 97/6 | 原生点列与旧带5424、5037重合；降层增加旧带5385冲突，族关闭 |
| 宽西270 | 6480→1501 | 未生成完整路线 | 明确跨度38.513米超过30米；未请求、未截断 |
| 延长下穿东135 | 6480→1501 | 124/6 | source13→26→32→31→16→target11，去重接缝后124点；中段维持层2，跨旧线后才升层3；旧带中心≤0.25米守卫无冲突，最远74.643米、点列长度156.646米；整线只读请求因已定位实现异常没有返回assessment |

普通建筑包围框相交只是筛选提示，不能自动当作原生传送带拒绝。只有实际既有带中心重合是此处确定的预检障碍。所有新生成片段仍为nativeConditionsChecked=false/executable=false。短片正例只可按已安装代码的phase分支推断短带路径检查经过，不能升级成整线或端点连接通过。

当前唯一值得继续验证的主选为“延长下穿东135”；没有合格备用或可写候选。124是生成点数，不是可执行施工材料账。源2011/目标2012分拣器各1、filter1001；按6跨度+2分拣器+至少1Save估算9动作，已经超过当前剩5槽。真实新ID、层间接缝、精确扣料/动作/保存顺序仍须正常API fresh资格；因此施工顺序和Save时机不获批准，也不自动补窗口额度。

## R1205整线预览及根因

固定六段请求通过实际pwsh -File零RPC smoke和同安装Contracts/Core typed wire验证。13对受保护原始请求/返回、前后关键几何、玩家库存/持物、端点hash及J102均独立核验守恒。唯一prepare-only整线请求返回INTERNAL_ERROR：A main-thread game-state read failed；没有Native assessment、失败点或路径/joint标志。不能由空返回推断原生调用数为0，也不能称作几何拒绝或路线通过。

R1206单次本地日志尾读取得52行、一个对应异常标记，日志写入在预览执行开始后2000ms、处于2775.2731ms执行区间；前后日志大小/时间一致。异常为System.ArgumentException：Unknown belt routing mode，调用链为BeltPathRoutingPolicy.BindGeometry→BuildPlanFingerprint→PrepareBeltEndpointPreviewOnMainThread→PrepareBoundedBeltRoutePreviewOnMainThread。日志中的Core/Plugin模块ID与精确已安装副本一致；直接离线调用同Core复现只读模式异常，三个普通施工模式成功。

根因是只读整线评估指纹误入施工模式绑定入口。当前最小修复为只读模式独立的指纹域，并复用完整build-step hash；普通施工模式入口继续拒绝preview mode，不扩大commit、路由或工具权限。修复在源码验证中，尚未安装；须相关测试、准确SHA绿CI及受保护同批维护后，另行明确一次修复验证目的，再核固定路线。到达端点评估分支只能推断先前路径检查分支经过；原生点条件与joint最终结果仍未知。

## 计时与复用

八个游戏只读阶段合计75请求，入口内部执行计时累计19915.3001ms；成功独立验收计时累计13651.4434ms。R1205执行2775.2731ms、派发至首次业务prepare66490.8338ms、独立验收2531.8653ms；R1206日志文件读取内部685.1252ms。这些计时不含全部caller准备、本地模型计算、失败脚本、日志委派准备或Git/CI，不能合并冒充端到端提速。已执行的优化是复用完整静态页、源13/目标11与成功中段，批量比较候选，统一一次文档交付；实现异常优先修，不以改线重放代替。

sourceConditionsPassed=false、wholeSupplyPassed=false、连续来源信用0；最终同SHA双候选包和正式发布均未完成。持续目标仍active，本次整线异常结束其有限声明，不停止整个目标。

## 原件与临时分析清单

下表引用受保护原件；临时caller、离线分析、实际smoke和点列清单保存在本地，不提交原日志、DLL、运行时描述或私有路径。完整临时文件名称及SHA另存本地r1206-temporary-analysis-inventory.json，聚合事实记录含同一清单。

| 核验 | 引用 | SHA-256 | 标签 |
|---|---|---|---|
| r1198 | b42f4943395e43e88e839e052a234f3c:86 | 246E0291A6449E57D0CF49FFD8114CDB44AA949A43608D2163925D7AEE77E43F | root-r1198-current-owned-anchor-grid-probe-independent-audit |
| r1199 | b42f4943395e43e88e839e052a234f3c:91 | C49FA8D9572776BD2FA66F9CF37AECEAC4A80E836387E3A73149811491A05303 | root-r1199-native-escape-fragments-independent-audit |
| r1200 | b42f4943395e43e88e839e052a234f3c:95 | EEDE4AA8EB47C4FE04A3C943786B22A392A285EE6352675F49D1DEFDD82B0663 | root-r1200-short-west-source-native-endpoint-independent-audit |
| r1201 | b42f4943395e43e88e839e052a234f3c:100 | A3F42A7C60E76E16454C56F62EB44AF2D03F2363F2EDAA3136B63B18EAED6BB1 | root-r1201-short-west-source-native-endpoint-independent-audit |
| r1202 | b42f4943395e43e88e839e052a234f3c:104 | 5AAFF8518645E0DA37D80F406DC7C85B87F06D4E165E541B8E093FA958E8587C | root-r1202-two-high-middle-native-generation-independent-audit |
| r1203 | b42f4943395e43e88e839e052a234f3c:108 | D222502064FD41CDEA02EC16D5C303D8113A60235AE1DB93FD7D013E0552E14D | root-r1203-two-high-middle-native-generation-independent-audit |
| r1204 | b42f4943395e43e88e839e052a234f3c:112 | DA73750B530C00D0034EF094392A85215875292D396F0777EF3C212CF994DE96 | root-r1204-extended-main-underpass-native-generation-independent-audit |
| r1205 | b42f4943395e43e88e839e052a234f3c:115 | 5D33DC7CBF0E92E170B6C00C576B7821F2275033D15150427CC9B8151FE3899A | root-r1205-whole-route-native-independent-audit |
| 异常根因 | b42f4943395e43e88e839e052a234f3c:116 | 4BBE73B74064EDA4CD51B50B055529988FEBF48A15F5DEA5AD9D01298807BF1F | root-r1206-readonly-route-fingerprint-root-cause-independent-audit |
| 聚合事实与临时清单 | b42f4943395e43e88e839e052a234f3c:117 | 0C3DDF58E82094020412D0BF1A3EDCCB9CE978A442CDA431646F3EEF456E94A6 | root-r1198-r1206-readonly-phase-sanitized-facts |

## R1207 最小修复相关验证

Core的四个相关测试类共247/247通过，0失败/跳过；Core/Plugin锁定restore各exit0，Plugin完整Release编译exit0、0warning/0error，原TRX、8个stdout/stderr流、6个本地引用哈希和4个源码文件/当前规则均由root独立核原件。普通施工模式指纹语义保留，preview mode仍被施工入口拒绝。新二进制尚未按准确源码提交重建/安装，CI与实机整线结果仍待后续。root核验：b42f4943395e43e88e839e052a234f3c:118，SHA B2D9ABA209DD6362FEF3064C9596C16DE306215387346AF8256BFC5BE8556F7E，label root-r1207-related-fingerprint-source-validation-independent-audit。
