# R1190–R1197 原生网格批量读取与路线障碍勘探

本页汇总已安装源码、同批维护保存和两批只读路线勘探的核验边界。没有新增施工或路线写入。

## 已安装源码与维护状态

实际安装源码为 0df97408058badfcb3e93b40f77953b4a0568347，包含 4 个 Plugin 文件与 224 个 MCP 文件；准确源码 Windows Core CI 38207485135 为 completed/success。完整隔离 cohort 执行 2,940 项测试；隔离冷 cohort 与已安装菜单核验覆盖 64 个工具和 1 个资源。R1191–R1195 维护序列包含两次普通保存及一次同 owned 健康加载，共 3 个 accepted；最终普通保存 tick 112145524、revision 2、durable Journal 102。独立保存核验确认全部维护动作已保存，无在途或未知动作。当前固定窗口开于 lifetime 535，15/20、lifetime 550，冻结但未封闭，5 个槽位保留且不转移。

## R1196–R1197 只读勘探

两批各核验 8 组原始请求/响应，共 16 次读取；0 次写入、accepted、保存、加载或移动。已安装并在本轮实机读取的 geometry.nativeBeltSegments 返回 27 个完整原始 Native 带段（16+11）；五组校准段计数为 13/29/25/23/11，合并 97 点且与旧结果一致。R1197 对 76 个对象的 82 个新鲜碰撞框逐项比较，结果与已缓存运行时几何一致。完整静态快照核验为 6,492 个实体、0 个预建对象、65 页，静态与互惠连接均无变化。既有 2,993 个几何对象、3,010 个 Box、452 个地形点是此前记录的几何/地形基线；五条 cargo 路径库存及三厂电力是此前的单点观察，不能当作本轮新鲜动态库存或三厂电力核验。nativeConditionsChecked=false、executable=false；完整 Native 路径可执行性也未证明。

| 候选 | 来源→目标 | 原生生成点数（未施工）/ 明确跨度 | 最低动作模型估算 | 新鲜对象相交 / 范围 |
|---|---|---:|---:|---|
| A | 6478→1501 | 96 / 6 | 9 | 最低估算已超过剩余 5 槽；矿机 1213、风机 1214；72.174 米 |
| B | 6480→1501 | 156 / 9 | 12 | 3050、4686、4936、4937；107.726 米，超 80 米 |
| C | 6489→1501 | 144 / 9 | 12 | 725、814、2411；88.117 米，超 80 米 |

96/156/144 为完整点列去重接缝后的原生生成点数，游戏中新建带对象仍为 0。最低 accepted 估算按当前明确跨度、两个分拣器和至少一次保存计算；它不是精确施工材料账，也不是所有布局的绝对下界。三案均被本地几何守卫拒绝，没有主选或备选施工方案。端点 Native 姿态/端口、精确可执行材料账与完整 Native 条件均未确认。此前 R1187 因请求模式错误被拒，R1188 的点 79 与既有带 5071 重合并在 pre_native_path_guard 停止；当前 survey 的两次全路径预览请求额度已结束，实际 Native 路径/接缝检查计数为 0。

## 流程计时与未通过门

R1196 的 Native 点列生成用时 2,650.7734 ms、独立审计 3,096.1664 ms；R1197 障碍批处理用时 2,110.0922 ms、独立审计 1,593.8132 ms。各阶段内部首次业务计时分别为 1,205.8081 ms 与 898.9253 ms；派发到首个业务请求的计时不可用。两批范围不同，不能据此声称端到端整体提速。

当前 sourceConditionsPassed=false、wholeSupplyPassed=false、连续来源信用为 0；最终同 SHA 双候选包未交付，正式发布未进行。下一步需要一条新的有界结构路线，并取得完整 Native 点列、准确端点姿态/端口、地形与材料/保存预算。当前勘探没有施工授权，也不重开已结束的路线预览声明。

## 独立核验索引

以下均为受保护原件的规范引用；SHA 对应各自记录，不是运行时存档或用户身份：

| 核验 | 受保护引用 | SHA-256 | 标签 |
|---|---|---|---|
| 源码与 CI | b42f4943395e43e88e839e052a234f3c:46 | 16536A7A1D3F8E2C1FAE666D9ED99A13408ACFC57EF9EECD251ACC76B3D0F291 | root-r1190-native-segment-clean-source-ci-independent-audit |
| 冷 cohort | b42f4943395e43e88e839e052a234f3c:49 | 335E3CEF322724030C7BDD454DC59D142E7B0CB49B318D5B557D05010A80FD57 | root-r1190-native-segment-cohort-independent-audit |
| 维护保存 | b42f4943395e43e88e839e052a234f3c:51 | A327BFF32739BC90800CBE3B5AB78765AF6E948094ECEEE1140F4517D8C2EF21 | root-r1191-native-segment-maintenance-save-independent-audit |
| 安装生命周期 | b42f4943395e43e88e839e052a234f3c:55 | 80A6E04BB064FF1590B593B0F4C69E7844CD0457F7844DD5BABBD19EAF88EBE6 | root-r1192-native-segment-cold-lifecycle-independent-audit |
| 安装菜单 | b42f4943395e43e88e839e052a234f3c:60 | D20E94AEFCC9EFD74811937A952ABCE0E3E28B07873C3F22B78958BDE25BB94D | root-r1193-installed-menu-independent-audit |
| 健康恢复 | b42f4943395e43e88e839e052a234f3c:62 | 552CF92E4F6E2997A6966B4F4DD45577BA704688757707DF031AFC99C3D1A208 | root-r1194-same-owned-healthy-resume-independent-audit |
| 恢复后保存 | b42f4943395e43e88e839e052a234f3c:66 | 42AD3753F9448CCD6FEBF358B76C21010403FDDA98768E6B9CA2AFE64EFCE1DB | root-r1195-native-segment-renewed-save-independent-audit |
| Native 段批次 | b42f4943395e43e88e839e052a234f3c:70 | ED24278B023A1BC9F9A96CA064AE8E9B8E97E51B4697F70F12B8BE7409BF85B3 | root-r1196-native-segment-batch-independent-audit |
| 障碍批次 | b42f4943395e43e88e839e052a234f3c:75 | 90FA38C4F3BCD85ED7784CD2F8D1A22FBB483F93EE4917D2ED0EF31442B44C86 | root-r1197-final-native-route-obstacle-batch-independent-audit |
| 聚合事实 | b42f4943395e43e88e839e052a234f3c:76 | 63536B80ACF0850CD96284F4CAE15EE5BB456AEEB96391931CDE5FB5828058E6 | root-r1197-final-native-route-survey-sanitized-facts |
