# 铁源接入、启动观察与接口核验（R1257–R1273）

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
