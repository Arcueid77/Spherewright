# Spherewright 当前快照

更新：2026-10-15（Asia/Singapore），三级整合34NEW与完整出发kit均已建成并保存；V3核验222组原始对、8个唯一action/key与ACK/terminal、完整6660 built/0 prebuild、静态及双边连接差异0、全背包守恒和R443九项清单无缺。Save为116309335/rev13，观察116309535，J102四次历史转换保留。Warper1210目标30、现有库存900（库存不是目标或自动产出信用）；R1280曾记录燃料库存65。旧opening608窗口16/20已封闭；新opening624窗口为8/20、lifetime632、whole233，余12槽含Save，47 Moves/71外部普通保存，Native内部重存单独核算。V3 writer已停，无在途/unknown，也没有运行中的长窗。下一步是完整kit的必要同45保存恢复，再fresh核来源、速率、库存和功率readiness，并进行最终联合≥36000-tick窗口；kit备齐不代表自动供给通过。两份冻结2a候选不重打包。见[0.4交付顺序](v0.4-delivery.md)。

## 原验收对应表

| 原条款 / 准确引用 | 已有证据及适用版本 | 真实缺口 | 最小下一步 | 通过条件 |
| --- | --- | --- | --- | --- |
| [Overseer 四项验收](../ROADMAP.md#overseer-验收门)：诊断、正常修复、稳定非零、跨恢复窗口语义 | Roadmap 原诊断/恢复证据：三种故障、钛块12/min、healthy Save、Journal49/49，历史候选8c49bcb；不当作最终0.4包。 | 最终源码/安装cohort的相关回归与限制说明。 | 仅复跑受改动影响的诊断、安全与恢复路径。 | 保留原四项标准，不重造故障实验；保留真实carrier stall分支的既有实机限制。 |
| [Foundry 运行模块复制与生命周期](../ROADMAP.md#foundry-验收门) | 原限定六对象模块、取消/恢复/去重及持续石墨输出保持原范围；29131的R1314→R1320五对象另已保存重启续建，R1321独立通过。 | 五对象自身不证明完整三级链。 | 保留所有成功实体和原build，把它纳入完整链计划/产出核验。 | 不重复施工或扣料；旧限定门不重开，后续只核受影响接口。 |
| [Foundry 三级完整计划、正常施工、中途保存重启续建、失效重规划](../ROADMAP.md#foundry-验收门) | 施工前声明depth3/1210≥1每分钟、4现有机器/12.9MW及外供；原29带+2sorter+1仓方案经真实接口拒绝，事前有界修订为31带+2sorter+1仓/34NEW，现全部正常建成并保存116288319。输入后/输出前的手动候选同45中途恢复独立通过，旧五对象全部保全，旧27对象候选退役。 | 完整共享来源分配、必要完整kit恢复后的真实产出及联合长窗仍未通过。 | 以已建成的34NEW和完整kit进行必要same-45保存恢复，随后fresh核来源/速率/库存/功率，再按通过的readiness执行最终联合窗。 | 原五项全部有对应证据；不补造旧计划，不以建成后重启冒充中途续建，未来ID建成后读取。 |
| [Governor 五项验收](../ROADMAP.md#governor-验收门) | 原锁定31→62/min、连续37128 ticks及限定升级/保存恢复保留原通过范围。 | 最终候选相关回归和适用范围说明。 | 复用原声明/证据，仅核受改动影响的接口。 | 不重锁基线、不重复两倍实验，不扩大为全厂配平。 |
| [双自动供给、出发清单及整体保存恢复](../ROADMAP.md#跨星系准备与整体交付验收) | 既有取材、来源和施工；R1300的36348 ticks只保留历史结论，不计恢复后新session。R1320/R1321覆盖五对象保存/审计。R443九项清单已备齐；R1280燃料库存65是历史备料值，不是当前产出速率。 | 完整kit加入整案后的同owned最终保存恢复、自动燃料/翘曲器来源和连续长窗仍未通过。 | 完成完整kit的必要same-45恢复后fresh核库存、速率、电力与运输readiness，再预声明联合有限≥36000-tick窗口。 | 原最低速率、库存趋势、真实自动来源、功率/运输及无缺口同session窗口通过；缓存、搬仓、P=C不能替代来源。 |
| [同源码SHA双候选及完整验证](v0.4-delivery.md#完成定义) | 2a7a98d同clean/pushed/green源码双ZIP、当前Native完整Release/3108测试、逐文件/版本/64完整工具参数/resource/guide通过，见[双候选](evidence/2026-10-15/delivery-candidates-2a7a98d.md)。[手动实装中途恢复](evidence/2026-10-15/manual-candidate-install-resume.md)及[TS本地原始共置载荷实装/运行/恢复](evidence/2026-10-15/thunderstore-candidate-install-resume.md)均独立通过。 | 完整游戏联合来源门和最终Release notes/交付报告仍待；Manager/profile/异机未实测，不扩大本地结论。 | 保持冻结ZIP，仅补缺失游戏门及最终报告；包内容变化才换候选。 | 同一干净已push且准确CI通过的候选SHA，全部适用检查有证据，停在所有者审核发布；纯事实后续提交不无限重包。 |

## 源码、安装与 dirty 归属

开始时本地与旧origin/main同为815d8e7ec32bb747194493a728ae6d37ba45baaf，工作区干净。fresh fetch确认它是远端b5cc822c5e372239f0994b7c406e931e8fa786a7的祖先，仅新增交付文档，已安全fast-forward；该SHA的Windows Core CI 38575811624成功。AGENTS按需索引单项更新已推送7742b3c9af6ad5f4e5341c8bb3a135a279b79657，准确SHA CI 38605445392已成功，可以在安全交接采用。

精确恢复政策/进度保全源码、相关测试和指南修复提交为07d1727b76664142615f7eebce2888e3bff654ec；[Windows Core CI 38620035304](https://github.com/AvaloNero/Spherewright/actions/runs/38620035304) headSha完全匹配、completed/success。该同批是本次迁移与输入施工使用的历史执行cohort；后续已实装2a候选，旧完整安装及新菜单/v6披露见[部署记录](evidence/2026-10-15/owned-primary-29145-menu-deployment.md)；旧版本拒绝、原证据和相关CI均保留，不用新报告覆盖旧结果。

候选目标元数据/两语包内README/开发Release notes的6路径修正3bc4cddc191e915fe18ca5f9cd13bc91b1b2c0f4已普通push且[准确CI38627098714](https://github.com/AvaloNero/Spherewright/actions/runs/38627098714)成功。冻结源码2a7a98d1932c404e3a7abc2c11233d9c0b8780d9对应[准确CI38627772732](https://github.com/AvaloNero/Spherewright/actions/runs/38627772732) completed/success；同SHA双候选sourceDirty=false，包字节未再生成。正式包、逐文件验证及未关闭发布门见[候选报告](evidence/2026-10-15/delivery-candidates-2a7a98d.md)。后续文档事实提交不改变已冻结ZIP或07d1727安装；没有tag、GitHub Release或Thunderstore发布。

本轮事实更新的Git基线为 `01c616e04d4c3bedf7bb795da7fea348e670a813`，[准确Windows Core CI 38687342272](https://github.com/AvaloNero/Spherewright/actions/runs/38687342272) completed/success；维护开始时本地、origin/main、实时main一致且clean。kit V3结果由root独立核验；本页和[kit阶段报告](evidence/2026-10-15/current-departure-kit.md)是本轮事实更新范围。无产品源码或候选包变化，不热替换运行cohort，也不重打包。

输入施工曾使用07d1727历史cohort，随后冻结2a手动候选已实装并用于完整三级链中途同45恢复和输出续建。当前实际为同一2a7a98d1932c404e3a7abc2c11233d9c0b8780d9 Thunderstore原始5共置文件（4DLL+单文件MCP），另保全原manual224 MCP，合229产品hash逐份通过；Plugin/Core/Contracts为0.4.0.0 / 0.4.0+该完整SHA，实际TS MCP server0.4.0.0。Native0.10.35.29145、Assembly-CSharp SHA B84B60DF5A24947A014801F23B9B85D1E77853F42A331918E2DC85B153E50527，5refs未变。原31迁移票据已消费；本次TS为default同45恢复，不新增迁移。菜单/228或229文件/metadata/恢复/生产各按真实边界区分，当前产品与证据HEAD不同属纯事实差异。

## 已完成和未保存前缀

R1314先建普通带6622与装配机6623（配方78），R1316保存两对象；R1318同owned恢复原部分进度；R1320仅续剩余索引1/3/4（实体6624/6626/6625），普通Save覆盖全部五对象。最后普通保存tick116215326、revision7、durable Journal102，三条版本转换一致。R1321为67页、6626 built/0 prebuild/55 details；除五项预期新增外，静态配置、拓扑和资源节点差异为0。opening lifetime588的fixed20窗口完整封闭、lifetime608；历史整案209 accepted/47 Moves/66 ordinary Saves。完整原件索引见[R1307–R1321](evidence/2026-10-14/foundry-progress-recovery-r1307.md)。

本次发现ignored受保护账本晚于跟踪快照：R1332在独立封窗后开启新fixed20，opening lifetime608，随后1项取料accepted、lifetime609。原action 44dbfb3a-08da-41a9-b43b-6c7960d40232成功终态：物品1005从862原子扣4、玩家0→4。随后源仓多出的1件未归因，不计来源信用。R1337核原action、全背包差量和原intent，当前账本无在途/unknown，但此动作尚未普通保存，窗口仍frozen且保留保存槽位。禁止换键重取。历史整案为210 accepted/47 Moves/66 ordinary Saves。

R1334北侧29带及末级实际端口11的原生正例只证明当时预检可行，29带/新输入分拣器没有建成证据。R1335调用器保留，其context/approval及当前现场仍须fresh核，不凭脚本存在称已施工。完整三级Foundry与自动供给未因此关闭。

上述为旧截面。Native45本轮先声明完整三级整合计划，原地正常施工29带并逐点读取真实ID，随后仅续filter1209的sorter6656和统一Save。原source5942旧连接、全部五对象及全厂旧对象保全；原6622/6624输入保留备用，不声称这两个入口已有供料。当前输入经新tail6655→sorter6656→6623端口11；输出经6625→6658/6659→filter1210 sorter6660→仓6657实际口4。5942及6625的朝向变化均严格匹配各自Native成功后读回和whole_path_native_rotation_v1规则，其他静态差异0。输入见[中途checkpoint](evidence/2026-10-15/three-stage-input-checkpoint.md)，输出及完整6660独立核验见[输出报告](evidence/2026-10-15/three-stage-output-prefix.md)。

## 当前交接与下一安全入口

- 下一唯一目标：对完整kit执行必要的同45保存恢复，再fresh核验来源、速率、库存和功率readiness；通过后才进行最终联合连续≥36000-tick窗口。34NEW、输入/输出接缝及kit均已保存；不重做已成功前缀，既有Governor/蓝图限定门不重开。
- kit V3状态：root独立核对222组原始对及8个唯一action/key的ACK/terminal，kit全项齐备，完整工厂6660 built/0 prebuild、静态/双边连接差异0、全背包守恒。V1未执行并已退役；V2在空delta解析失败，仅2只读、0 prepare/commit；V3只修枚举后完成获批8步，不重放。R443的1210目标为30，现有库存900不是目标且不产生自动供给信用；32条基础带/30目标、3个分拣器/2目标，4批最小生产共12条带。取材标识为Cu1104（物品代码1104），不是1106。明细与原件见[kit阶段报告](evidence/2026-10-15/current-departure-kit.md)；当前窗口最多20槽，禁止扩窗。
- DSP：唯一PID50884健康运行，原28156仅为必要TS实装正常Close1次，Steam36408保留且只派发1次；当前actual2a/Native45。TS配对事务committed、实际marker absent、229产品/606handoff/原票据J保全；本地标准共置载荷实装已过，不声称Manager/profile/跨电脑或崩溃恢复。会话/Host结束不触发Save、退出或Load。
- 保存/观察：kit V3统一普通Save为tick116309335/rev13，随后观察tick116309535；durable J102及四次历史转换保留。完整工厂6660/0prebuild、静态/双边连接差异0、全背包守恒；此前TS同45恢复内Native重存116298956/rev1仍是内部重存，不计外部Save。此前5943待机filter1209→1210及两条计划图外边/精确Native朝向变化均有原件。历史44dbfb3a…旧石矿4不计当前恢复信用，禁止换键重取。
- 实际窗口：opening608旧fixed20的16个唯一原action/key已独立封闭，lifetime624/whole225、4个空槽不继承；该阶段47 Moves/70 external普通Saves，迁移/manual/TS三次Native内部重存另计。新fixed20 openingLifetime624已使用8/20，lifetime632/whole233，47 Moves/71 external普通Saves，余12槽含Save；writer已停且无在途/unknown。新窗口内没有运行中的长窗，kit不计自动产出信用。
- 退役：旧27对象Foundry候选、旧不兼容进度、R1327/R1328旧输出候选保持退役；五对象、已完成来源和所有成功前缀禁止重做。
- 预算：旧菜单/披露/打包预算均已结束。本次精确迁移原声明240秒/120请求/最多1accepted，实际约18.8秒/14请求/1accepted；仅摘要读取断言失败。随后独立120秒/4read/0accepted约5.2秒完成。完整工厂240秒/150请求预算以实际19.194秒/133请求结束，配方目录另以120秒/4请求预算实际2.16秒结束，均0accepted；没有运行中的生产长窗、跨session样本或预算续期。
- 输入预算核销：旧两Move方案首prepare因2个shore-risk样本被本地拒绝，5请求/0accepted结束，整个approach退役；新原地700请求/1800秒阶段实际328请求/599.137秒/1accepted，29带原action成功，第二Native正例因旧本地sourceSlot>=0 guard误挡而停止。root逐328对原件核清后，只修caller接受已公开belt_virtual精确-1并用真实物理槽做建成读回；4项actual Native正负fixtures/实际File smoke0Game通过。新续片独立300请求/900秒预算，以132请求/102.332秒/2accepted结束，不能称原阶段续期；没有重复29带。当前手动候选冷安装为独立必要目的600秒/2游戏read/0accepted，终态后即停并独立审计。
- 离线验证：复用ignored SDK8.0.424，绿CI源码7742b3c的locked restore、Core/Contracts/MCP Release构建与3028测试（73/2707/248，0失败/跳过）通过；95项工具/参数/resource/指南policy与source及安装MCP64工具/1resource/指南一致性通过。旧ref 74AC…保留；在独立干净detached checkout收集5/5当前安装refs，B84B…及manifest逐份匹配，Plugin Release为0警告/错误。此为当前DLL（fresh菜单29145）的离线编译，不能当跨版本恢复/兼容或最终候选实机通过；准确报告见[本轮离线验证](evidence/2026-10-15/delivery-build-validation.md)。
- 版本边界：default仍严格同版本；旧安装拒绝和11项原生研究保留。用户已对披露后的原问题明确回复“确认这次精确迁移”，fresh v6同候选/新digest唯一commit通过，原票据在Load前持久消费；完整J102及前三转换与R1318原Native逐项相同，第四31→45在tick116215327/J102持久化后正常重存。新票据SHA为EC49DBBD4527CFC87AAE486D04C4C0F817E4BDDA53CE2ABD6DF23145C4DC5F5F，45/min116215357/checkpoint102、匹配当前session/原PID且非quarantine/未过期。只证明本次精确迁移，不扩大其他版本/候选授权。
- 进度保全：精确buildId39cc6803-df11-4d9c-8864-37db49dc8ca9读取为completed/submitted5/5、persistenceHealthy/freshWorldReconciled均true、无blockers，五对象6622/6624/6623/6626/6625逐项保全。完整恢复后6626工厂与R1321静态/拓扑差异0。原summary_only响应不能当损坏；没有清空、手改、归档或重建进度。动态供料、完整三级链与最终包实机门不因此关闭。
- 修复回归：MCP258/258、相关Core271/271及初次失败的局部修复见[候选离线验证](evidence/2026-10-15/owned-primary-29145-candidate-validation.md)。准确07d1727 CI已success；clean archive使用SDK8.0.424完成全solution locked restore、当前5份原生refs完整Release构建（0警告/错误）及win-x64 self-contained MCP publish。228文件、产品SourceRevision、完整工具/参数/resource/playbook、真实安装/新菜单/v6准备已独立核验，见[本次部署](evidence/2026-10-15/owned-primary-29145-menu-deployment.md)。validation-only ZIP不是手动/Thunderstore最终候选，原生Load/实体连续性/最终验收仍缺。
- 双候选已生成：ignored `artifacts/v0.4.0-review-2a7a98d/` 的手动ZIP SHA为665FA1B30A86A4D49AC7E69CE174E08348DCA07D159A24AF766ABED89AFD79CB，Thunderstore为81D9D6ED0BAEC843AB22ADE38158102212A5487A43EFEE22769F500B7CF85D91；各有匹配sidecar。正式入口全solution locked restore/完整Release/当前5refs/两包一次完成，0警告/错误；准确源码CI3108/3108、0失败/跳过。root核全部文件map、9产品版本及两布局4Plugin/guide相同；手动目录MCP与TS实际单文件MCP的完整64工具/参数/resource/guide主harness均通过。TS验证fixture不是交付包；补充exit原件-1与JSON0不一致、不计通过。包内说明/Release notes保持开发态与原限制，详见[候选报告](evidence/2026-10-15/delivery-candidates-2a7a98d.md)。
- 剩余包门：同SHA手动和TS各自适用实装/运行/同45恢复已独立通过；TS实际单文件MCP精确64必需名+既有schema/annotation/resource/guide断言、3只读Game调用、exit0及纯协议stdout均通过。完整生产联合长窗、完整kit的same45保存恢复和最终报告/Release notes仍开放；07旧菜单不替代最终2a证据。不为纯事实重打包，没有正式tag/Release/Thunderstore发布。
- 当前安装续接：2a TS实际运行，唯一50884；健康时直接续接不Close/Load。原completed5/5 build的construction_topology_changed_or_unproven保持：两条有计划图外边6623:11←6656:0及6625:0→6658:1，内部图/原五对象完整，旧进度不清/不重放。V3 kit已正常保存，不重放；下一入口仅完整kit的同45 owned-save恢复，随后fresh readiness及联合窗。
- 原件按下表精确引用读取；token、真实存档名、私有路径和原始身份不写入此文件。阶段结束与暂停/压缩前继续覆盖本块，不新建任务系统。

| 直接续接原件 | 引用 | SHA-256 |
| --- | --- | --- |
| R1332独立封窗后新fixed20 | 9c9fff04318249758eaed638128587b7:125 | 5477ECE8613EF4398658C9A0299502A53465A20B3105F40484C3AAF02DF6926C |
| R1332原writer，accepted preserved | 983c5bb4c0f14f9893fd4c534b7d3a7a:29 | B8F346CB75460B03354B4DEA811AF697EA91BB02FF2B39734255E33665BD7E16 |
| R1337原action成功/守恒独立核销 | 9c9fff04318249758eaed638128587b7:133 | 7EEF1430B03FDDDECA60D800D4E25B0C1FC154E4E15BDE9D7442980F9710881E |
| R1337原intent核销，窗口继续冻结 | 9c9fff04318249758eaed638128587b7:134 | B8267A5553C83F6FAA916E11CAD80ED707284F2CB88E24BF0D20E0E04C71106D |
| R1334北侧输入/末级端口原生正例 | 9c9fff04318249758eaed638128587b7:124 | FE0EFD18A1948B7D65BDA752E13FC58CDBD8B418C210787FC0744529F776BAD4 |
| 本轮唯一Native菜单，fresh29145 | 9d554a389ef44eb4922c6f37b85d8582:1 | DEA283CBFA438EF22C8A1AEFAC3BFBED4059CB7F2192902ED1FDC567C5444849 |
| 本轮菜单最终摘要，0accepted | 6cdff5e096204b88968e28a422eb3b5c:1 | F67F81593C00C7B3CF2BF6782963AEB0AEE4C91E1A21D670C7AC52EFE892CF3D |
| 本轮唯一default prepare拒绝 | dfdbe61513854c14bf2bd2ac3a172ae7:1 | 47B149D58A064FE5F390CA82B88EF84F9A9F34D1706BE690A47649911185AAD9 |
| root版本/原窗口/原响应独立核验 | f03badcd37ef48fe85202114d5d4f5bd:1 | 3C202474AF33A15CF6584E78142B74FF3047517A6B494D944F0210A6752A4326 |
| root原票据/完整Journal/五对象进度只读保全 | eb097306510a4fe0b5243180958e2a87:1 | 5601D56E9F729E826D1B2EBAA6D6060BA53D45982FB5AA06C7F9D27FFC49964A |
| root clean07同批228文件/当前原生refs独立审核 | 420621ca61274642b600817c8b977593:1 | BDDFD0BBC9B6D4D36BD03434448B202B4076C8FE04B16CEFD238572552848087 |
| 菜单冷部署原writer，0Load/Save | 496641597fe24cb4aac46140b3e43e2f:3 | 0ABC5401D0CF6014D852FE5E84A64F5A1CEB1B0BA557FE29A87101031F424B1D |
| root冷部署intent/228安装hash/新进程独立审核 | 4651c3d0b91c4756bf7759300923b6fb:1 | 0BBCE437FAE73AB54B6FAF21A760602934CF3E0089D0265B45F0EE5AE9CC784C |
| fresh新菜单原生响应 | f8481cbf72e0443b91b80eacc6be4000:1 | 667D6F4C65DB87500349DD7236E8937F7AF1B4AF2715E682499205BF904F1B6D |
| fresh v6 prepare原生响应 | f8481cbf72e0443b91b80eacc6be4000:2 | 50AE1F2544F7F0B372E0E186256048DD4A9F34039D2E6E1207F9A6AD6BC4B6AC |
| v6披露原writer，0Load/commit | f8481cbf72e0443b91b80eacc6be4000:3 | 56B0754ECE8988E88C0C1435B0E94A38ABBCD590E05F2E33FB2682D78C0535BB |
| root v6原响应/原件保全/实际窗口独立审核 | 1b1152237fe54255baf96fd9d7f150c5:1 | 7F502E48BF7309FDC5B01FEAE043EF6D743136BC840D22915596017E10BAE0A9 |
| 本轮fresh菜单原writer，1读0写 | 071544fd22504c2988f978e9e342df91:2 | A33A65DA5D2BFC7BD90C2D6F93606C05869FB8D97F653F2390D3DB71954C8439 |
| root本轮菜单/原票据/窗口独立核验 | 9c37c8c5d4694257b3c2d9079ad113b5:1 | 6E1CD9447F1FEB7B6F77D8D64B266376CD1F9AE66A67CAF202837A946FD60A62 |
| root两包源码/9产品/逐文件独立核验 | 95228048cc7544779a6601d6adc6d6f2:1 | 5CFA51FD70BD5FC20F1AB7BB107F472A98AE28FE4A33A9305B97558D29140C8D |
| root TS实际单文件/主harness/补充exit限制 | 5449a4c92ad6405981ae0359b96d3e23:1 | 12AC1B833DC6EB3452BBBB9C8FB8992B50A4B1590E16C6DFC2AD235768BEB354 |
| 后续明确用户确认后的根批准 | 1092a0f645d44932bf309d8a8f9b7ee3:1 | 8C6F916750A0FBB52978596C7ED4C09979A224760E6DCB125F69028C38B7EE74 |
| 唯一迁移原writer，保留摘要断言失败 | 089fa0adc37140e490a80faf912415ef:16 | 979E4E7D4FEAB15B7E6735E6FFFA4EB6B9F2AE4C7AE3E33DF4AED49D6C144C51 |
| 精确buildId纠正性4读，0accepted | a38b386529ac41d5bf16188d67119335:5 | E765ED541063F24E8273FDC5779A714C97275EB43182FD03AE4D8826E30BCC7A |
| root迁移/正常重存/完整J102/五对象独立验收 | 073d28e37da2434d8de96395ad6ae633:1 | A0AD497B2E61BAB17ACEC66320564FC48EC3692C2B191E009F91699152F07486 |
| 当前完整工厂/1209与1210接口只读批准 | afae33a83ce74470887b38a82ae9e4c4:1 | 0496A2FD4DA0DF5D637B3FEEA811C38BFEE575EAC050E5CCF45879CD12F8FF01 |
| 恢复后完整工厂133请求/67页/59详情原writer | 14627977f6d441ed975d5005f8ee48ca:134 | B859A312AEB123F349C447EEA75F7BA1736876FAA64D9CC5DA5C27B40DB517E7 |
| root全6626静态配置/拓扑与资源节点独立对照 | 3349ec7fd5d54f37bcb9252632ce9eb4:1 | AE75674B8E1A22D6D2A8CFE5286BE92DEC0CBFF0DCD290F94F87DFE5DA2FB3A8 |
| 当前29145目录与depth3/1翘曲器每分钟材料草案 | 18675f15924341478b3c1a52f0d90f3e:5 | C73CBC60819909FDB17D9EF65C0B2673FA26AA9CAF8AE39BBF46AB2C691EFEB9 |
| 施工前三级四机及有界整合全案 | efb3326dfef541d284c358310df3a0f3:1 | 27122A3C3B3B0730A74E7CB05C123DA6328FFADC236C12055C824C237E63B040 |
| 29带原writer，保留随后caller guard拒绝 | fb098b0bf96d4a5caf093eba1ca7970d:658 | 08F4CA4E9A7D5947A9ADB0E80C57F664E7F882D419663A292F74E3684985E449 |
| 原29带成功终态/328对回执/sourceSlot -1独立核销 | fc57d0e265714292bf5195adada78b9e:1 | 7F39A1754A3AF9981495302AFE98CA2C6808255D5293AFE428EFC63245924675 |
| 仅续1输入sorter及1正常Save原writer | 7558e9572cec48b19e1e2f906fa27f93:267 | A5BC7E880DE2D7E8DFC9C16AAC1CF1D1D4C5260863A3090B8DFD1AAEFA7F7724 |
| root460对/6656完整快照/精确Native朝向变化/正常Save独立验收 | 8b7144d139c34181ab0faabd388c2dd8:1 | 7D4968FE98786F0073F32D779E9A0B6BD52B004A1294AD2621A4401F7E65E059 |
| 冻结manual2a在整案中途的600秒0Load冷安装批准 | 63ccd32469fa4e6e90d4bbd11f2d1e56:1 | A16590E902A3916ECD728DBF1C3AB7FB1D903B71A8371B87D6B26B55930F4521 |
| 原cold正常Close后/实际未安装/调用器日志错误终态 | 0d920814958946b0a8de69dce9f2f3a4:6 | 89B4249892829808CC9893609C75B172F5AD66629E0B410961AAB2C976A5F629 |
| 本次StageOnly预览归属/未触live/metadata通过 | 0d920814958946b0a8de69dce9f2f3a4:4 | 91263C4209B1C1877CAA01FA715BAF82702FB9462E3396C68AE411B4EC2ED2B1 |
| 精确owned预览完整归档及唯一actual安装/启动批准 | 8c35ecf5f3d5429fb87d059e9116e3db:1 | 36FF8446E590A46E9784A19A17980995D3289037942646D59A26D4B5D4C9740F |
| 预览完整归档后原生Host错误原件，0启动 | beeb427423b646e0a1f16c8aa9ae30f0:3 | 50D175A844287D3DC64C179B7548DD78B6C02B5B9295AB79D94CB742C66E59A6 |
| root原生子进程双探针/实际安装批准 | 5ccfa5a028cb41e1a3c8a3b6e902161a:1 | A3061ACB7E2B0924443A1FD72F1230852805581D2DC1D250EDDC345CFD3B74B2 |
| 实际手动安装成功/单次启动/首次菜单超时原件 | 42b22c2612e94243ad659be485834f8d:4 | BDAF30120382B0DE8D37625547129019DED078049A5ABA6AE180743FEF206EC6 |
| root228安装/602受保护条目/票据J/原启动独立审核 | 8c2e0db2c29f4041a877b298290a263a:1 | C9DF32888F160201E81C52A8126A61C2273DD4404E28E3675820D7C36408CDF8 |
| 同一已启动进程的fresh健康Native45菜单，1read/0accepted | 3cf0defa8b5548139d6cfea9da5cb80e:2 | EF239B9775ED926FC4A4525604882A2AA344D292A5174203A86989E2711FE951 |
| 同一45主档same-version exact-primary恢复批准，360秒/180请求/最多1accepted | root22ed1d44212c49a48a7a3a230677bea5:1 | B7581273723DD99BED34237D0463B19B1433C5AD23E92D4B734C6B8439B4EC7E |

| 手动候选same45中途恢复原writer，80请求/1accepted | 3aace813db80434f8aa534176331f5a7:162 | F97ADF0EE27C94EDC1D6EC48356A6E8EAD1DCBDF836543E3BC4D94BF4AE557BB |
| root完整6656/Journal102/五对象拓扑标记精确核验 | 5d592fd474974ef0acb667333a9dab0c:1 | 7FCAC4A44023415327D00A32FEBEFE3178E79E1947B186F3CF743AE1E3C2C95E |
| 输出V1目录参数0写入停止独立核验 | ed62d73480394f6e802295063b10a49c:1 | 413185C5043443C5FFFD4E6034F4AE4CB18F7C728D9DDCD0A59E648F61E452F7 |
| 输出V2显式当前星球有限批准 | d1c4d2f4d996483d89bc724e58c5bcc3:1 | 9C512932E69B763E0D517CA147D9C9EBF610EC265BFE3C01571296A2ECFFD1B7 |

| root三成功输出前缀与直连sorter原生拒绝 | 33bc4e6e08424315bc21245f411e24aa:1 | 51A7DF872B65F8BA942C4B181A75E3AC4B83331423F99C67C53B6536A31215FE |
| 仓前缀普通Save/geometry/完整6657原writer | 0fb83268136545a8b537b9b65da6c14f:162 | 4912A29360D95AAA2D9AFB927B19719AE323939F179CC91F2782A476A02DDD27 |
| root saved6657/原6656静态0差异/真实端口geometry | 3fa3d5e7988e4cf6a1d65e4c67afe6f3:1 | 856FCA479DDB742AD2E5EBDF463C8EE7EEF0D0E687057ADEC5F468AC25C9EE7F |
| 事前两基础带短接缝完整成本修订 | 3316ee1eb3ef40319a2240553106d418:1 | 507C45D7565A67BB78441C759BA3D99ABB381FCE6346BF98F7CDC87C51B6C961 |
| 实际Native两带正例/本地null guard零写入核销 | 2a932d0992fc4575ab7be97ff1c5e460:1 | 7A8FFE489F970A4B241F80F98E67EE17B2FB908A6DD527F5CA2E5D8DD92E14BC |
| V2仅修null目的端后的fresh执行批准 | 2a932d0992fc4575ab7be97ff1c5e460:2 | B67DB02B637C7EF20ABB90AE9F709C1BA970D25AEFB7E44C3D16DF0003C9B78A |
| 冻结TS真实12文件/5共置程序payload离线核验，未部署 | 6d93f13fc10c417d9886c6d5e5e3e8aa:1 | BD64E9D746E9F7173427E217481CFF1C3CEB0597BA4D9833734CEF173C0DEB80 |
| 输出短接缝V2三accepted及普通Save原writer | 88bd0a7812464d5ab8913df70b5ef579:496 | 4F7379FFE51FC0D8B101EBEB1E8D287FDE82FB29D3B50E2DC5E00F906ACADAE4 |
| root独立246对/完整6660/静态与扣料/Save核验 | 15012a1bbbcc4e93b36fda09f16ed3b5:1 | 4FB68F5D6A9B00425AAA332A2BF7537F6A9EE75073CF6B2A5C23EABF53828345 |
| 完整三级供给三个独立短窗事前600秒/400请求/0写批准 | d8997e32e7044a8488a0ef0853e34727:1 | 72107016F4FF0550643C8261FCF16260A7BAE8212779476A37EB8380E27383E8 |
| 三个短窗来源/分配独立审核（连续信用0） | 06c8490f17304089b98851eddc78c011:1 | B86A0F9C1FFED499CF557FD8497D94111A517D2244B0C3BFAAA675E75FF12627 |
| 5943最小待机配置/Save/全6660独立审核 | 77b57b52b6de45cbb71f90ae4afd1233:1 | 7961F1CC5BA657FFA2DA21AE38265AFF187B3E9A91CF12EAAF850AD660C9B371 |
| TS实际229产品/606handoff/新健康菜单独立审核 | a65674b7ae494a4484769d3b33f00f0d:1 | ABCBD8D9792A951E5A085EF3673EFFC5B6B9D26BEC221DDBC9BBDBD0DA7E385B |
| TS同45恢复/全6660/完整J102四转换独立审核 | 63f4d93e6e514724b6b119f229c2fde4:1 | 8EEF70D61AB50AD607B3DD5F71FACC3E6784FE54BE1B4EC05B2171F4C9B62A64 |
| TS实际单文件MCP/7RPC/3GameRead/exit0独立审核 | 1611f1ef4a914527931c5821a9534bf4:1 | 6AC4918A90B7ACC0E9716801FCF832FB7A2245CF13CFF3431A0656D8194D7326 |
| 原opening608 fixed20全部16唯一原action/key独立封闭 | ea9936b64c954a7b8cc6e25e547a402b:1 | 0BA373D48A4E444020588B1E397E8DA19D57B54A689E38FF1E5F7F91A46C5869 |
| 独立封闭后新20/open624（4旧空槽不继承） | ea9936b64c954a7b8cc6e25e547a402b:2 | 8C0E862AF13EDFA178B0C54AC4E2FBB6D2E7CBEA044AF3CD67ECD0F24F5B9D34 |
| kit V2空delta解析停止，2read/0prepare/commit独立核销 | 773e0256ea8d47c4aff468a0b5484d57:1 | 9DDE898F09A54D35375BD23FFD1860D4D34AEB221AD10B1C8455ED78DD288045 |
| kit V3保留8步/1200秒/500请求事前有限批准 | e17fe9e0c8c44cb0985e34da2bd95f38:1 | DD8CACE00070D9CF09B0FD7F58E0D9A9BCC09CDB91B6FA3C6ADB698175CD7066 |
| R443九项出发清单root独立核对 | c78c15f06d8248bda55bfb0164fd87ca:1 | A4A36AD9E36E692313A212CA0747320762A6E84E6AE2B6EC39BDE7E47920BBDA |
| R1280燃料60件有限取料/保存root独立核对 | 23a9b8e84fdc42558679ca803e39b173:90 | 38F290A62DFC0E3F00B7C7B5661AD155C7C7C7F5CD440AAF4CBF1C28B292D16A |
| V3 kit 8步/完整6660/Save/J102 root独立核验 | 067b144cf90a4e43a4a665777505218e:1 | 9A33750D8369CB69189CF083EDC3F8F79E027599D4B50068890C8A7A6D060D64 |
