# 铁源接入、启动观察与容量重分配核验（R1257–R1291）

R1257完成两台分拣器的原生建造并由普通保存覆盖。root独立审计覆盖151对原件、67页完整工厂快照及保存边界；建造后为`6618 built / 0 prebuild`，没有未核销在途或未知结果。

| 实体 | 配方 | 过滤物品 | 原生连接 | 材料变化 |
| --- | --- | --- | --- | --- |
| `6617` | 建筑物品`2012` | `1001` | `6616 → 1501` | 对应分拣器材料扣除1件 |
| `6618` | 建筑物品`2011` | `1001` | `6480 → 6505` | 对应分拣器材料扣除1件 |

原端口均保留，N3供电完整。保存边界为tick `113747912`、revision `19`、durable Journal `102`。固定20窗口为`17/20`、lifetime `569`；整案累计`170 accepted`、`5`台分拣器、`55`次普通保存、`297`条实际传送带、`18`段、`46`次Move及`27`次手工材料动作，动作均已保存。

在tick `113748186`的同次读回中，124带Native货物路径显示13件铁矿，通往既有`1501`的旧路径为空。该观察表明有货物进入新路径，但没有证明货物抵达`1501`、设备启动或持续供给通过，不计来源或连续供给信用。

R1256此前只读闭合覆盖231个节点、235个几何对象和135个物料对象；当时尚未建成R1257的两台分拣器，按原生目录核算两台满负荷合计为`900 J/t`，完整基础负载预算后的余量为`47905 J/t`。这是原生目录负荷预算，不是两台分拣器建成后的负载实测。

## R1258–R1260只读延续

R1258完成三个相互独立、各600-tick的窗口，范围含同tick的221对象物料cut（其中16个燃料发电机状态）、8个直接对象与33类物料。区间为`113759795–113760394`、`113760421–113761020`、`113761045–113761644`，合计1800个被采样tick；窗口之间存在间隔，没有拼接或延长既有声明。观察到铁矿沿已接入路径移动，末端分拣器读到持有铁矿；三段启动输出有记录。窗口累计产出`1206=4`、`1127=2`、`1209=2`、`1210=3`，其中每个窗口均观察到1个`1210`。然而，保存/重载后的重复输出检查为false，故只记短时运行输出观察，不记启动恢复门通过。

R1259独立核验了12组原请求/响应，协议闭合通过，写入计数不变；调用器没有保留数值退出码，按原记录保留该限制且不重放。该截面报告`6618 built / 0 prebuild`，Save仍为tick `113747912` / revision `19` / Journal `102`。电力摘要中的三个网络均为满服务，但这只是一个采样截面。

R1260再核验26组原件，独立审核通过、调用器退出码为0；当前状态tick `113802032`、revision `19`，未产生accepted或新保存。后续本地生产读回中电动机`1203`仍为0产出、消耗4；电动机`724`未工作，配方输入`1101`仅1件（该配方记录需求2件），输出电动机为0。结合三短窗内电动机总产出0、消耗18，这些是缺料/需求相关的观察，尚不足以证明唯一根因。氘核燃料棒装配机`3403`未工作、输出缓冲20（目录容量19）；燃料仓`3955`的30个格位各存30件，合计900且已满。带段`1582`和`2348`各自快照未见对齐货包，分拣器`2317`处于`Returning`；这些瞬时字段不能证明持续堵塞或唯一供需原因。

R1258–R1260均为只读，没有新增accepted；当时保存边界仍为R1257。其后的普通保存、同版本健康恢复与输出核验见下一节；短窗本身不替代≥36000-tick连续门。

## R1261–R1268恢复、封窗与新窗口

R1262的一次菜单只读请求以`REQUEST_TIMEOUT`结束，保留该原始失败，不重放。R1263后续Native只读读取到健康菜单；其原始调用文件位置没有独立证明，因此不扩大该次结果的证明范围。随后R1264同一owned主档Load恢复、R1265普通保存完成。R1265保存边界为tick `113839396`、revision `2`、durable Journal `102`，三条版本记录保留；完整工厂快照为`6618 built / 0 prebuild`，静态配置未变。恢复流程没有重新分类或重放更早的历史unknown。

R1266完成三个独立、每个600-tick的窗口，共1800个采样tick，未拼接或延长声明。产出合计：`1206=2`、`1127=1`、`1209=1`、`1210=0`；电动机`1203`在三窗均为零产出、零消耗，燃料棒`1802`也无产出。该次切片未捕获目的端`6617`持有铁矿；这不推翻此前到货证据，也不能记作源头分类失败。三层启动输出与保存/重载后的重复输出检查仍未通过，因此不授予启动恢复或持续供给信用。

R1260接口读回中，电动机`724`未工作，铁块输入`1101`为1（低于该配方记录的2件需求）；R1266三窗电动机无产出。燃料棒装配机`3403`未工作，输出缓冲20（目录容量19）；燃料仓`3955`的30格各存30件，共900件且已满。带段`1582`和`2348`的采样时刻未见对齐货包，分拣器`2317`显示`Returning`。这些瞬时观察不能证明持续堵塞或唯一根因；氢、重氢和精炼油共享供给的因果关系也未据此定论。

R1267封闭opening lifetime `552`的旧固定20窗口：20个accepted动作均被唯一核销并保存，其中19个为成功终态，1个历史unknown继续保留，不重分类或重放。封窗后无当前在途或未决unknown；当时整案累计为`173 accepted / 5`台分拣器、`57`次普通保存、`297`条带、`18`段、`46`次Move与`27`次手工材料动作。R1268随后以opening external `0`、opening lifetime `572`开启新固定20窗口；当时边界为`0/20`，旧窗口未用额度不转移。该窗口的后续动作和当前状态见下节。

## R1269–R1273读取、材料阶段与保存

R1269完成33项消费者读取；一次因32页参数上限被拒绝，原stage未完成。R1270仅补做6项后缀读取，保留前段详情而不重读；合计39项读取成功及一次参数拒绝。其600-tick切片中电动机`1203`产出2、消耗2，不能据此称其永久停机。

R1271的两个邻接仓候选分别与实体`3079`、`3074`碰撞，候选族已退役；一次正常电路板取料只到prepare，未commit。R1272的5请求材料尝试以`STALE_STATE`结束，0 commit、0 intent、0 accepted；精确漂移字段未公开。两项结果均未重放或改写历史。

R1273在28个请求中闭合三个唯一完成终态：正常提取物品`1301`两件、配方`85`消耗铁两件和电路两件制成基础分拣器`2011`两台，以及普通Save。材料制作不等于建设；本阶段没有新增建筑、带或分拣器实物。Save为tick `114117844`、revision `6`、durable Journal `102`，三条版本记录保留。当前固定20窗口为`3/20`、lifetime `575`；整案为`176 accepted / 58`次普通Save、`297`条带、`18`段、`5`台分拣器、`46`次Move与`29`次手工材料动作，未决或在途为0。历史unknown继续保留，不重分类或重放。

R1274只读核验仍在进行，尚无独立结论。下一目标是把现有仓`3348`中的燃料备用库存接入既有路线；不增加第三个相邻仓微移。当前`sourceConditionsPassed=false`、`wholeSupplyPassed=false`、`continuousCreditTicks=0`，最终同SHA双候选仍未交付。短窗、库存截面或满供快照不替代连续来源验收。

截至R1273保存核验，`sourceConditionsPassed=false`、`wholeSupplyPassed=false`、`continuousCreditTicks=0`，最终同SHA双候选仍未交付。后续仍需有限核验电动机铁料支路、燃料出口满仓及氢/重氢/精炼油共享供给关系；不得将短窗、库存截面或满供快照提升为唯一根因或连续供给通过。

受保护原件索引：R1257 writer `7d393909d32b46b19ce0afd8b066451f:316`，SHA-256 `D8A2D9C4C3CF008ABBCE7227E7EA1B49F907303D1B403BA9FA5F60148C02CD31`；R1257独立审计 `23a9b8e84fdc42558679ca803e39b173:20`，SHA-256 `5C362843F380770BF03D182766216EDF025680E78C8051A37FA30D1A0B9093BE`。

R1258原件独立审计 `23a9b8e84fdc42558679ca803e39b173:23`，SHA-256 `ACCD0192094C29E349A8DD00E8C1FC195EE518743996917CF24C6F7C935ED5F0`，label `root-r1258-original-three-native-windows-and-full221-atomic-supply-fuels-audit`。R1259接口/路线审计 `23a9b8e84fdc42558679ca803e39b173:27`，SHA-256 `7FF47DA9FD33E311DB72A7B2FD8E262E04395F18CA8C84F88AE366A8087DC8D5`，label `root-r1259-twelve-original-route-closure-readbacks-and-power-audit`。R1260接口细节审计 `23a9b8e84fdc42558679ca803e39b173:30`，SHA-256 `371168F27DCA5B2A097DED440F76D2D7EBFBF2B6CB53D86AFD57A5F315E1B16A`，label `root-r1260-original-interface-detail-and-atomic-brackets-audit`.

R1265启动恢复与保存审计 `23a9b8e84fdc42558679ca803e39b173:46`，SHA-256 `A9FEA771435DDC0865D428AB7F2F6B9CD9997565B72E1205FCFD0E0576D6C754`，label `root-r1265-startup-save-original-terminal-full-factory-and-fresh-primary-audit`。R1266三窗来源/启动审计 `23a9b8e84fdc42558679ca803e39b173:50`，SHA-256 `958E2825974C26CC9CD474F0A4CFE9A534E067FC386FFB7E171335F5C979D90E`，label `root-r1266-original-three-native-windows-and-full221-atomic-supply-fuels-audit`。R1267旧窗完整封闭审计 `23a9b8e84fdc42558679ca803e39b173:49`，SHA-256 `08A28A6E6ABDB3FCA15361F5EE2DF01A092C9483AA72ABB4FC1B464A5E10AE1E`，label `root-r1267-independent-all20-original-acks-terminals-and-complete-factory-window-close`。R1268新固定20窗口交接审计 `23a9b8e84fdc42558679ca803e39b173:51`，SHA-256 `59E73E8AF429C2C994CD34BBDA8439F211F0C79C8438CC0002556469EBE37203`，label `root-r1268-new-fixed20-after-complete20-closure-for-bounded-shared-source-repair`。

R1269消费者读取前缀审计 `23a9b8e84fdc42558679ca803e39b173:54`，SHA-256 `E061180623E6709796F262A001FF28047FAB6CFB67B8B729861EB5D4A4B4949D`，label `root-r1269-original33-successful-read-prefix-and-one-page-limit-rejection-audit`。R1270读取闭合审计 `23a9b8e84fdc42558679ca803e39b173:57`，SHA-256 `8F204AF6CE8FFA0844800D63C8177CBB07EB42F2C96FFC505309660764B920AB`，label `root-r1270-original-six-read-suffix-and-preserved33-detail-prefix-complete-audit`。R1271材料与邻接候选审计 `23a9b8e84fdc42558679ca803e39b173:61`，SHA-256 `7A271215C4F692BAECD0610B848FCA259E9DD8C1435DBC68FF9FD01819065224`，label `root-r1271-original-two-storage-collision-denials-retired-and-normal-material-positive-audit`。R1272零commit材料拒绝审计 `23a9b8e84fdc42558679ca803e39b173:64`，SHA-256 `A2C126611E661F9E2492E70C9F2159FBDCC37B4959A5E8B0FD7367ECD3F2C648`，label `root-r1272-original-zero-commit-stale-material-refusal-audit`。R1273材料保存审计 `23a9b8e84fdc42558679ca803e39b173:67`，SHA-256 `932EE1683D4164CD95E5802666DC5A2F18DA42940565AD85B75DFB6A3DBAC1B4`，label `root-r1273-original-two-circuit-two-basic-sorter-and-covering-save-audit`。


## R1274–R1280燃料方向、候选筛选与有限备料

R1274只读确认燃料方向应为`3955`自动出料→`3348`现有接收空间；截面分别读到`3955=900`、`3348=400`。这只是端点和库存核验，没有建成或资格化完整出料路线。

R1275–R1278为只读候选筛选：四轮矩阵都没有保留可施工候选，未进入完整整线Native预览，`constructionApproved=false`，没有accepted。候选分别因与已观察实体包络重叠、Native点列/接缝条件或53条带携带材料上限被排除；这些是局部筛选结果，不构成完整Native路线拒绝或来源通过。

R1279共核7组原始请求/响应。新仓的Native prepare因建造体积与实体`3060`重叠而返回`BUILD_LOCATION_INVALID`；`warehouseBuilt=false`，0 commit、0 intent、0 accepted、没有Save。随后本地收尾逻辑访问空的通用页面列表，遮住了主要prepare拒绝；并非Build或Save之后失败。该候选不重放。

R1280的17组原始请求/响应由root独立核验，唯一完成的两项accepted为从`3955`取料和普通Save。燃料棒60件的原生终态扣料为`900→840`，玩家库存为`5→65`；`3348`不是本次取料源，原有400件保留。Save为tick `114476839`、revision `8`、durable Journal `102`，三条版本记录保留；全部当前动作已保存，无在途或未决unknown。root核定本阶段实际增量为2；调用器报出的5来自窗口计数，不按5累计。调用器数值退出码未保存，保留这一限制而不重跑。

本阶段没有建成自动供料路线，也没有新施工或生产采样。仓库库存只支持有限备料，不计自动生产、来源信用或连续供给。当前窗口为opening lifetime `572`的`5/20`、lifetime `577`；整案为`178 accepted / 59`次普通Save / `30`次手工材料动作，既有`5`台分拣器、`297`条实际带、`18`段及`46`次Move不变。来源、整案和连续供给门仍分别为false、false、0。

### 受保护原件索引

以下SHA绑定单个受保护root审计记录；未将原始日志、存档名或运行时凭据写入仓库。

| 阶段 | 受保护记录 | SHA-256 | label |
| --- | --- | --- | --- |
| R1274 端点与库存 | `23a9b8e84fdc42558679ca803e39b173:70` | `5CB7313D9142963CDBC2FCFF4C44B05AD970B874947A5D7310719CEB4A1C786E` | `root-r1274-original-existing-fuel-warehouse-slots-stock-and206-collider-audit` |
| R1275 路线筛选 | `23a9b8e84fdc42558679ca803e39b173:74` | `05D456780AC491A3CB35BB8C913433071E40FD535E62FBD26EDA5584F5980D21` | `root-r1275-original-three-fuel-route-local-matrix-and-native-batch-audit` |
| R1276 路线筛选 | `23a9b8e84fdc42558679ca803e39b173:77` | `E5ED6C9EAE63EE8D26A6405D96EA58F6211CBA5DF6A87E6CC250E32D6D1243BD` | `root-r1276-original-three-fuel-route-local-matrix-and-native-batch-audit` |
| R1277 路线筛选 | `23a9b8e84fdc42558679ca803e39b173:80` | `1CE5B07A89C71D3438CFB147730AB0AE9034995DA04A1003AA8A445D1080801D` | `root-r1277-original-three-fuel-route-local-matrix-and-native-batch-audit` |
| R1278 路线筛选 | `23a9b8e84fdc42558679ca803e39b173:84` | `7F70129C8F9CAE5589F5E31BC216B63AC7591E5AA60D57B58A3292C6CA48205F` | `root-r1278-original-three-fuel-route-local-matrix-and-native-batch-audit` |
| R1279 零提交拒绝 | `23a9b8e84fdc42558679ca803e39b173:87` | `EAEC97F57D040B4CC7507C97AADB682F2FED4A5EE9940E24C6B0DFEB0A86EDEF` | `root-r1279-all14-originals-zero-commit-native3060-denial-and-local-finalizer-failure-audit` |
| R1280 取料与保存 | `23a9b8e84fdc42558679ca803e39b173:90` | `38F290A62DFC0E3F00B7C7B5661AD155C7C7C7F5CD440AAF4CBF1C28B292D16A` | `root-r1280-original-two-accepted-finite-fuel60-kit-and-covering-save-audit` |

## R1281–R1285短窗、Persist修复与保存前缀

R1281完成三个、R1282完成六个相互独立的600-tick窗口，窗口之间有间隔，不能拼成连续样本。R1282中四类目标物出现非零输出；这些短时读数没有通过保存/重载后的重复输出门，也不产生连续来源信用，`continuousCreditTicks=0`。来源条件与整案持续供给仍未通过。

R1283只读检查显示燃料仓`3955`的30个格位各有30件，合计900并达到仓容量。该库存截面没有证明自动出料或稳定来源，且没有发生施工。

R1284中Native prepare给出允许结果，但调用器导入的Persist回调因ledger路径为空而失败。commit没有发生，accepted增量为0。commit-intent在回调失败前已写入；独立核验确认无commit后才退休该原意图，未重放。故障位于本地持久化路径，不是Native拒绝，也不能表述成intent未写入。

R1285将ledger路径显式绑定，并通过17项隔离的Persist回归。随后实际建成一座新仓`6619`和一台筛选物品`1210`的分拣器`6620`，在`5331`增加一条互返连接且保留原输入`5365`。同tick观察到一次自动到货，普通Save覆盖本阶段动作。独立审计核对125组原件、67页完整工厂快照、`6620 built / 0 prebuild`、库存和拓扑；本阶段新增3个accepted，全部已保存，无在途或unknown。保存位于tick `114881508`，当前窗口`8/20`、lifetime `580`；整案为`181 accepted / 60`次普通Save / `6`台分拣器 / `297`条带 / `18`段 / `46`次Move / `30`次手工材料动作。

R1285的一次到货只证明这条有限前缀有货物到达，不证明完整Foundry建造链、双source稳定或连续供给。完整Foundry设备、电力、物流与精确成本计划，以及中途普通保存/重启续建去重和失效计划重规划，仍缺完整证据。R1285当时仍有燃料容量问题；来源门、整案门与≥36000-tick连续门均未通过，`sourceConditionsPassed=false`、`wholeSupplyPassed=false`、`continuousCreditTicks=0`。历史unknown仍保留，不重分类或重放。

### R1281–R1285受保护原件索引

| 阶段 | 受保护记录 | SHA-256 | label |
| --- | --- | --- | --- |
| R1281 三个共享来源短窗 | `23a9b8e84fdc42558679ca803e39b173:93` | `DA6024A9B419BECC078ACA2B5F37FF1CFCD5057713A516555FEFAC01423893B9` | `root-r1281-original-three-native-readiness-windows-full221-stock-fuels-audit` |
| R1282 六个共享来源短窗 | `23a9b8e84fdc42558679ca803e39b173:97` | `914524A6F215E2EF314EF3B09C116630EDF79E2597B5F684746281B35BF8F1A5` | `root-r1282-original-six-native-hydrogen-deuterium-windows-full233-stock-fuels-audit` |
| R1283 燃料仓容量 | `23a9b8e84fdc42558679ca803e39b173:100` | `7FD845039823F45A2D2914A1F59E95F61439BBD8802694DBC46F0A80105AB05A` | `root-r1283-original-output-capacity-six-warehouses-four-port-details253-colliders-audit` |
| R1284 零commit调用器路径故障 | `23a9b8e84fdc42558679ca803e39b173:104` | `174DAEC22FCDAF69283E53636523BD69F7FFF9BC6D45A93C281E5C3D3DB1E230` | `root-r1284-seven-original-reads-positive-native-prepare-local-persist-failure-zero-commit` |
| R1285 自动到货与保存审计 | `23a9b8e84fdc42558679ca803e39b173:107` | `DCDFF678269FA9B2336723E1B27BE9DE3C97FBDF760476791665CD1521ED8D8F` | `root-r1285-one-real-warehouse-sorter-full-factory-automatic-arrival-and-save-audit` |

## R1288–R1291自动到货、只读核验与容量重分配

R1288原件中，建筑物品`2011`分拣器`6621`的建造与配置两项accepted，且记录一次燃料自动到货。第三项配置在Native提交前收到`STALE_STATE`，所以该有限阶段留下两个未保存成功动作；root独立核验它们没有重复提交。R1289以只读五对象截面确认29个燃料过滤格已满、输入与输出分拣器等待，并观察到融合机燃料能量缓冲下降和发电运行。这是能量与发电状态读数，不代表燃料棒物品计数下降，也不能据此推断长期供给状态。

R1290进行了9个只读请求的Native容量预检，0 accepted。R1291完成六项唯一accepted并普通保存：暂停并恢复原有分拣器过滤，从仓`3074`取出210根燃料棒，经玩家库存存入仓`3348`；`3074`按23个燃料格、1个氢格、6个氘格重新配置。`3348`最终持有610根燃料棒。root独立审计确认玩家库存守恒、拓扑未变、相关电力摘要平衡、全部动作已保存；保存边界为tick `115498158`、revision `26`、durable Journal `102`，三条版本转换保留。窗口为`16/20`、lifetime `588`，当前没有在途或未决unknown，历史unknown不重分类、不重放。

本阶段完成的是有限库存与过滤容量调整，不是自动补给或持续来源通过。根验收的完整Foundry设备/电力/物流/精确成本计划，以及普通保存重启后的逐对象续建去重和失效计划重规划仍缺证据。R1291只核对受影响对象，没有新做完整全厂普查；最近完整普查仍是R1285的`6620 built / 0 prebuild`，其后R1288增加分拣器`6621`。来源、整案与≥36000-tick连续来源门仍未通过。

### R1288–R1291受保护原件索引

| 阶段 | 受保护记录 | SHA-256 | label |
| --- | --- | --- | --- |
| R1288 writer：两项accepted及到货 | `7812923727eb4314ab15c323c53e8c1c:77` | `7092B1DDE21C850C7B3A63CC937A20EFE0AF9F8141531D15709C811C88BFFE73` | `r1288-original-fuel-belt-warehouse-fusion-auto-arrival-and-save` |
| R1288 root：部分前缀与明确拒绝 | `23a9b8e84fdc42558679ca803e39b173:118` | `1F8A87829DF0DF7A766E437C1A217C4DAC0A192FA568086B87F2761E5149B04B` | `root-r1288-two-original-accepted-and-third-definitive-stale-before-action` |
| R1289 writer：只读五对象读取 | `67430e679868479b941d4eeb9276efed:19` | `2FED01B7E60113F7645A63FDE1B11315B4F14112CC577B9F6181D9B594C59C94` | `r1289-original-current-five-object-fuel-feed-and-unsaved-health-readonly` |
| R1289 root：容量与融合机截面 | `23a9b8e84fdc42558679ca803e39b173:123` | `12A9FD3B30425BF2F5C4EF20914EC5389DB087713842FF5187791363314689DF` | `root-r1289-full-fuel-warehouse-capacity-and-live-fusion-audit` |
| R1290 writer：零写容量Native预检 | `1321abdf705a431194f74ca8c839a0cc:19` | `075235F7458A58FC392F75B759B65F4FFF6D273EA5689CA0BE5412F77DE97BA6` | `r1290-original-fuel-capacity-native-readonly-preflight` |
| R1290 root：预检批准与边界 | `23a9b8e84fdc42558679ca803e39b173:125` | `0C7D0AEAEF4BB076BA863F937CB925EE9C10BDEB1439C8511CF0CC850C42177A` | `root-r1290-exact-native-zero-write-preflight-approval` |
| R1291 writer：六项动作与普通保存 | `48ee9b6359c14f0ea8b7341979a36229:111` | `62274263874CB8D6594BAAA19109F047203548E666F09B3E83F2E4DCE8736F5C` | `r1291-original-hd-capacity-fuel-reserve-and-covering-save` |
| R1291 root：独立保存与容量核销 | `23a9b8e84fdc42558679ca803e39b173:130` | `57E84F79C306FD5505CDC4E615A26C76715BF8F93B45DC5F4C8A1D97F8B51FA4` | `root-r1291-six-original-native-actions-save-and-capacity-independent-audit` |

## R1292–R1293氢/氘/燃料因果读取与分离短窗

R1292以26组只读原请求和R1293以74个只读请求完成因果读取及六个彼此独立的Native 600-tick窗口，观察耗时98.3秒，0 accepted、0写入。窗口有间隔，以下为分离窗口读数合计，不是连续速率或拼接样本：氢产出/消耗`158/157`，氘`30/40`，燃料棒物品`1802`为`2/3`，翘曲器物品`1210`为`2/0`。所选截面中氘节点为`18→16`，`3955`燃料格为`35→35`，仓`3074=690`与`3348=610`六次均未变；相关电网六次均满供。短窗中的燃料产消是间歇观察，不建立自动稳定来源信用。

两轮均为只读，最新独立核验仍是同一owned保存边界：tick `115498158`、revision `26`、durable Journal `102`；固定20窗口`16/20`、lifetime `588`，无在途或未决unknown。R1289的融合机读数是燃料能量缓冲下降并处于发电运行，不是燃料棒物品计数下降；以J/tick表示的能量读数不当作离散物品消耗。`sourceConditionsPassed=false`、`wholeSupplyPassed=false`、`continuousCreditTicks=0`；六个分离短窗未满足窗口连续性或≥36000-tick验收门。

### R1292–R1293受保护原件索引

| 阶段 | 受保护记录 | SHA-256 | label |
| --- | --- | --- | --- |
| R1292 writer：因果只读读取 | `135616dbb0754502a714db262d0b5f78:53` | `F46677AD104EDD12B991C89101A67BF77EA9C1256C744F2B3B23722D2E18F9E1` | `r1292-fresh-hd-fuel-causal-readonly` |
| R1292 root：独立原件审计 | `23a9b8e84fdc42558679ca803e39b173:134` | `AB901F2586EFC057010E793D0BC02C28024CA5F0D24695E8B3860E3F3C0797BA` | `root-r1292-all26-original-current-hd-fuel-causal-readonly-audit` |
| R1293 writer：六窗采样 | `f7e1177a231b45db84857e556db0f513:83` | `CD1C0220786D07966E9294EF571F440FA7F0A68F4356E90FFD1A9613FA01AB73` | `r1293-independent-hd-fuel-causal-sampling` |
| R1293 root：独立原件审计 | `23a9b8e84fdc42558679ca803e39b173:138` | `8D1DE10D638C1FD0D720576474D4BAD34A7D4EAA9D7D2A76C399F32911A30A8C` | `root-r1293-all83-original-six-native-hd-fuel-causal-sampling-independent-audit` |

## R1294–R1295 Foundry范围与共享供需只读核验

R1294对27个对象的三段Foundry组合通过内部路线组合检查，但未执行场地Native检查：全部27个对象均超出当前玩家施工范围。候选还缺7个建筑物品`2011`、1个`2302`和2个`2303`，供电覆盖未核验；既有透镜`5333`没有奇异物质。因此这不是可施工方案，也没有提供完整设备、供电、物流与精确成本计划的通过证据。

R1295完成28组只读读取。独立审计核对56对原始请求/响应及两个各25对象的同tick截面，分别在tick `115705858`与`115706076`；新增accepted为0。`3074`氘仓为空、氢为20、燃料为690；`3073`氘输出`1→0`；`5326`有5氘但未工作；`3403`氘`11→12`且燃料输出0；氘路径`209`有7，奇异物质路径`213`有1但`5333`透镜仍为0；`3083`氢输出59、接近满仓。融合机`3079`的载入燃料能量下降、缓冲燃料棒读数为10，相关网络均满供。这些只读短时库存和能量截面不能证明燃料棒物品被消耗、持续自动补给或稳定来源；R1293彼此分开的六个短窗也不能合并为连续信用。

保存边界未变化：同一owned P104、Save tick `115498158`、revision `26`、durable Journal `102`；固定20窗口`16/20`、lifetime `588`，无在途或未决unknown。`sourceConditionsPassed=false`、`wholeSupplyPassed=false`、`continuousCreditTicks=0`。

### R1294–R1295受保护原件索引

| 阶段 | 受保护记录 | SHA-256 | label |
| --- | --- | --- | --- |
| R1294 root：Foundry组合与范围审计 | `23a9b8e84fdc42558679ca803e39b173:143` | `4FF835F82011992E5469EF2DE53566F7E5A0D77662C4A701B5672C0E7F7082F4` | `root-r1294-all77-original-warper-foundry-readonly-independent-audit` |
| R1295 writer：共享氢/氘/燃料只读读取 | `270cb9f07409434ca09aada8ddefd5cb:57` | `884C36029010A032E6DEF6750D7EAE8307059176005269A1DB89F1B3B76605D7` | `r1295-shared-deuterium-fuel-consumer-causal-readonly` |
| R1295 root：56对原件独立审计 | `23a9b8e84fdc42558679ca803e39b173:148` | `02557A2D78C2C35E08B92327B04785315C581052A8A570143624BDBB51A88D49` | `root-r1295-all57-original-two-cuts-consumer-causal-independent-audit` |
| R1295 root：只读范围封闭 | `23a9b8e84fdc42558679ca803e39b173:149` | `CA5D0E04621AD0AC8CD0488F90B26D2755D81C0761019C1DFD7FF0E75B149868` | `root-r1295-independent-readonly-scope-closure` |
