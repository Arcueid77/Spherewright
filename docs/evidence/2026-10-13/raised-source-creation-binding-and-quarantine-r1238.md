# R1238 抬升首段、接缝隔离与建造绑定修复

更新：2026-10-13（Asia/Singapore）。实际安装仍为67ccc62936c0594a62568b46cb4f79c0718ee89c，Native0.10.35.29131。本次原件证明首段施工和第二笔已接受动作的真实差量；不将本地物理核验提升为Native解除隔离或持续供给通过。

## 有限施工与原动作

R1236一次首段prepare成功，13个NEW点与13条材料预算吻合，但本地caller把组合建造hash与player hash比较而拒绝。root独立核全部7帧，0commit/0accepted/无intent；旧once保留。修正的R1237复用同一六段/124条方案，实际pwsh File smoke通过18个守卫，其中包含此前真实Native计划的无token夹具，所有本地绑定均零游戏副作用。后续仍逐段使用实际上一段尾端与fresh Native token，未预分配未来ID。

R1237首prepare在1.94秒发出。首段0→3的原action66cd7404-cdb0-4339-ab92-7030d667245d正常完成，tick113235622→113241848，2001库存177→164。完整13个NEW点逐一对应实际6505→6493，互惠有向空载路径、旧物件和背包count/inc/held均由原件独立核验。第二段3→2的完整Native检查prepared/allowed=true，绑定真实源6493，预算25。原action4a763e9a-89ac-42f5-ba4d-e97e115b2f1a、原key8675142c-bdcb-4c48-afb7-881e5b80a5ab被accepted，tick113242806进入outcome_unknown，启动异常只披露InvalidOperationException。入口立即停止，保留该原intent；没有后四段、分拣器、Move、Save或重放。入口wall143.03秒，子进程145.47秒。

外部计数保留2笔accepted：新窗口5/20、lifetime557，整案158。首段实际13条计入已建累计186条/13段；第二段25为原计划量，不因accepted推成实际材料或已建段。caller顶层unknown=false和捕获摘要的commit/save行计数均不能覆盖原Native unknown和实际两条commit_build帧，root依据82个原请求/响应与唯一意图核清并保留真实边界。

## 原action只读核查

R1238仅77个只读/preview请求，0commit/0accepted，入口24.69秒。精确原action终态仍unknown；Native prepare_quarantine_reconciliation明确拒绝：The retained building-item delta is 0, not -25。该拒绝不能被本地观察替代，也不能靠重复旧Build改变。

66个不可变完整工厂原页为6505 built/0 prebuild。相对R1235独立6492实体基线，只新增首段13条带，无移除、旧静态配置或非互惠连接差异。第二笔原计划的25个NEW点均无实体；玩家2001为164，所有count/inc/held精确保全、Walk静止、无人机返回。旧11对象及首段13条带的同tick完整物理cut保留空载独立有向路径，本地网络满供。Journal保持102个durable条目和完整3条版本转换。

最新session观察tick113267791、revision5，仍owned/和平/非沙盒/1×，writeHealth=quarantined、writesAllowed=false，隔离精确绑定第二原action。最近普通主档仍113054820/R2，未覆盖113241848完成的首段。不能以ownedSaveState=saved、0新增或0扣料清除隔离，也不能载入旧primary丢弃13条已建带。原accepted/历史unknown始终保留。

## 必要窄修复与恢复方案

确定的源码缺陷：raised source准备将基本完整source/path绑定与empty-path证据组合；CreateWithSourceCoverProof却在Native create前拿新捕获的基本hash与该组合hash比较。这两种层次无法相等。修复在create前fresh复读完整empty/open/unfed旧路径，再在同一层次比较；post-create继续核基本完整path、精确旧cargo、cover身份和原生negative prebuild互惠输出。flat源的既有语义不变，无新增工具/DTO、DSP调用、材料放宽、范围扩展或unknown核销旁路。真实异常调用点未由运行日志披露，实际0差量与该pre-create失败路径一致，仍区分代码证据和运行时调用点证明。

新增9项回归验证准备/执行的组合绑定、基本post-create证明、完整路径改变或缺失、double wrapping及flat行为。R1239首次编译的nullable CS8604错误保留；root在已验证非空分支补足类型标记后，R1240复用原Core locked restore，通过279/279相关Core测试。Plugin此前无assets，仅补一次必要的专属locked restore；当前29131五引用下完整Release构建0警告/0错误，验证DLL877568字节，SHA-256 77D7AC8D49161A07E403F495B155FD788BA975AEEA3E292A9B48DA15B10EDB71。root逐原命令和完整流核验，并从真实命令TRX路径读279项结果/9新增用例；最终摘要中的误写TRX路径不当原件。修复未部署/未实机施工，无新工具或协议变动，未重跑无关测试。

恢复仅可准备本次同owned固定LastExit方案：正常退出当前已确认DSP并保留Steam，以原生退出保存取得候选；精确身份、29131、Journal102/3转换及候选tick必须不低于最新已核观察113267791（也覆盖首段113241848），受保护quarantine ticket/单次tombstone/唯一Load仍必需。候选尚未生成，不能声称已可加载；不允许旧primary、另一世界、任意选择、回滚、编辑存档或热换DLL。符合原AGENTS和持续目标授权的排除项，实际隔离恢复要取得本次新的明确确认。新源码先完成相关验证、准确SHA绿CI及同批工件；实际Load前再核候选，失败只核原动作，不改键重放。该次完整计划最多一次正常退出、一次同批事务安装、一次既有桌面broker启动及两笔accepted（唯一Load和其后普通Save），沿用同一20写窗口、不清5/557或旧unknown。恢复后完整工厂/13条带/背包/J连续性核验，随后只施工明确未建余段。

## 受保护索引

| 原件 | 引用 | SHA-256 |
|---|---|---|
| R1236零commit独立审计 | b42f4943395e43e88e839e052a234f3c:213 | 065E1E471FC4541AC6C203FB9C1EE1B7D11A8485671ED27442CBD21D097AA39A |
| R1237实际File smoke | b42f4943395e43e88e839e052a234f3c:215 | 21B72C9409DDE3570A53D123E1C087111054665B5916407D71F05B2F10C174B5 |
| R1237原执行manifest | 308d601cc679459ba0e5cd8b014f7f9b:173 | E2DCD2DB36CD8A1CB7037D4A9D694CB962F79681FD7E3FD63BE2175919432847 |
| R1237首段/第二原unknown独立审计 | b42f4943395e43e88e839e052a234f3c:218 | 4E0807F10C4A4C6CAE4E3EA5F811173275EC690BC128A14AA36A751E4A805DC4 |
| R1238完整只读原件 | 07ef1be5d9c44862b6c33c6863be456a:155 | 984E334A1B7EC17A3E87A7E16DCFDEFECE56707ECEBDB2B85EA026AE101051D7 |
| R1238独立完整差量/拒绝审计 | b42f4943395e43e88e839e052a234f3c:220 | E47A80F1DD88A6E52D079718331FFB6C20915B44FD2C26F927C150837A1D1C5F9 |
| R1240相关验证原件 | 8fd32330b2914db8abee0eac354bf742:1 | 80D67F6CFCA62852DB17D1D403A0EE02815D15315B182927D5226AF022898561 |
| R1240独立原命令/279测试/当前Native审计 | b42f4943395e43e88e839e052a234f3c:221 | EF0B3DBACEC704DCE75D07F8852F90E4FE3563B4DF55874DD8C8B8E0ABFCC770C |

两个执行流完整保留、当前用户ACL与字节hash由root核验。R1238主原件因重复完整页为9,715,286字节，root对这一个已声明精确长度的文件持只读lease核相同字节hash/完整解析，并逐一独立核77个小原件；共享8MiB reader及游戏协议上限未改变。持续来源、wholeSupply和最终双包均false/待验，连续信用0，既有通过门和成功前缀不重做。
