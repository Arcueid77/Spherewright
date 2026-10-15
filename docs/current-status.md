# Spherewright 当前快照

更新：2026-10-15（Asia/Singapore）。完整三级整合34NEW、出发 kit、手动输入已保存/输出未建的中途恢复和最终整案 same45 恢复均已完成并各有独立记录。按原批准 SHA `D811FFEC2E9845842A4EDA072425C8FBA665E9A70B17D6A941EC595770047C96` 运行的 Native-valid 联合窗口覆盖 `36,060` ticks（`116316166→116352225`），79 个有效样本（上限 90）、1,822 对原始请求/响应、22 个库存 cut，无 gap/reset/accepted 写入；独立数据、来源/整案与封窗审核均通过。一次覆盖 Save 于 tick `116353747` 完成；观察 tick `116353967` / revision `2` / Journal `J102`，102 条记录与四次历史转换保留。固定 20 窗口以 opening lifetime `624` 开始，10 个唯一动作核销后封于 lifetime `634` / whole `235`，未扩窗、剩余 10 槽作废；外部普通 Save `72`、Move `47`，Native 内部重存另计。当前 Native `0.10.35.29145` / PID `40884` 仍运行。候选为冻结源码 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9` 的两份原 ZIP，未重打包；本地验收现提交所有者审核，尚未正式发布或获发行批准。详见[最终联合供给与封窗验收](evidence/2026-10-15/final-joint-supply-36060.md)、[最终整案恢复记录](evidence/2026-10-15/final-whole-preparation-recovery.md)和[候选交付审查](evidence/2026-10-15/review-delivery.md)。

## 原验收对应表

| 原条款 / 准确引用 | 已有证据及适用版本 | 真实缺口 | 最小下一步 | 通过条件 |
| --- | --- | --- | --- | --- |
| [Overseer 四项验收](../ROADMAP.md#overseer-验收门)：诊断、正常修复、稳定非零、跨恢复窗口语义 | 历史候选8c49bcb已有三种故障/修复、钛块12/min、healthy Save、Journal49/49；当前2a源码另有准确CI 3108/3108，且手动与TS布局均完成本地实装/同45恢复，见[双候选审查](evidence/2026-10-15/review-delivery.md)。 | carrier连续600 game ticks静止后恢复仍无live正例；旧门不重开。 | 保留该已知限制，交付本轮证据供所有者审核。 | 原四项标准不变；不把全套CI或恢复读回冒充carrier分支通过。 |
| [Foundry 运行模块复制与生命周期](../ROADMAP.md#foundry-验收门) | 原限定六对象模块、取消/恢复/去重及持续石墨输出保持原通过范围；29131的R1314→R1320五对象保存重启续建、R1321独立审计也保留。2a完整CI及两种布局实际同45恢复提供当前候选回归证据；五对象旧完成态和进度未重放，完整34NEW链另有施工/保存读回。 | 无需重做已通过的旧限制门；旧五对象完成态的原拓扑标记继续保留。 | 不重放旧五对象build或扣料；本轮证据交所有者审核。 | 区分旧蓝图生命周期、完整三级链及连续供给；已通过子门不重开。 |
| [Foundry 三级完整计划、正常施工、中途保存重启续建、失效重规划](../ROADMAP.md#foundry-验收门) | 施工前声明depth3/1210≥1每分钟、4现有机器/12.9MW及外供；原29带+2sorter+1仓方案经真实接口拒绝，事前有界修订为31带+2sorter+1仓/34NEW，现全部正常建成并保存。输入已保存、输出未建时的手动候选中途same45恢复通过；之后输出与kit也已保存。最终完整kit同45恢复及本次同session供给、保存、封窗均通过，见[最终联合报告](evidence/2026-10-15/final-joint-supply-36060.md)。 | 本轮本地验收已通过；仍保留 Mod Manager/profile、异机、安装事务崩溃恢复及 Overseer carrier 冻结 600 ticks 后恢复分支等明确边界。 | 将证据交所有者审核；如需发行，另行按授权处理。 | 原五项全部有对应证据；不补造旧计划，不以建成后重启冒充中途续建；最终长窗为同session且无gap/reset。 |
| [Governor 五项验收](../ROADMAP.md#governor-验收门) | 原锁定31→62/min、连续37128 ticks及限定升级/保存恢复保留原通过范围；当前2a准确CI 3108/3108与两种布局实际实装/同45恢复构成候选回归证据。 | 不另行重跑已通过的2× live门；CI没有按 Overseer/Foundry/Governor 场景拆分计数，真实 carrier 冻结恢复仍无 live 正例。 | 保留已通过范围与明确限制，交所有者审核。 | 不重锁基线、不重做旧两倍实验、不扩大为全厂配平。 |
| [双自动供给、出发清单及整体保存恢复](../ROADMAP.md#跨星系准备与整体交付验收) | R1300的36348 ticks只保留其历史范围，不计本次新 session。R443九项清单齐备；item `1210` 的库存900是备料，不是产出速率。最终整案 same45 恢复通过；实际新链来源、速率、库存趋势、功率、≥36000-tick同session窗口、一次覆盖Save与10动作封窗均通过，见[最终联合报告](evidence/2026-10-15/final-joint-supply-36060.md)。 | 本地 0.4 验收已通过；正式发行仍待所有者审核。非本地 profile/异机、事务崩溃恢复及 carrier 冻结恢复仍未验证。 | 所有者审核候选与对外说明；正式发行另行授权。 | 本次现场标准已按 Roadmap 原门核销：来源、最低速率、库存趋势、功率/运输与同session连续供给均有独立证据，并完成覆盖Save与封窗；不以缓存、搬仓或 `P=C` 替代来源。燃料 `≥1/min` 是最终联合长窗事前最低速率，不是 readiness 门槛；三个独立 600-tick readiness 窗口只准入长窗、连续信用为 0。R1300 未设燃料数值最低值，也未套用 Governor 的 10%。 |
| [同源码SHA双候选及完整验证](v0.4-delivery.md#完成定义) | 2a7a98d同clean/pushed/green源码双ZIP、当前Native完整Release/3108测试、逐文件/版本/64完整工具参数/resource/guide通过，见[双候选](evidence/2026-10-15/delivery-candidates-2a7a98d.md)。[手动实装中途恢复](evidence/2026-10-15/manual-candidate-install-resume.md)、[TS本地原始共置载荷实装/运行/恢复](evidence/2026-10-15/thunderstore-candidate-install-resume.md)、最终完整kit同45恢复及联合供给/覆盖Save/封窗均独立通过。冻结包与实际安装IO字节核验见[候选复核](evidence/2026-10-15/review-delivery.md)。 | 本地候选验收已通过并提交所有者审核；Manager/profile、异机、事务崩溃恢复与 Overseer carrier live 冻结恢复仍未验证。尚无正式 tag、GitHub Release 或 Thunderstore 发布。冻结 ZIP 内 README/CHANGELOG 的待迁移/待实装文案是包冻结时点的历史文字，外部报告按后续证据更新，ZIP 未改。 | 所有者审核最终证据与发行说明；发布或重打包均须另行授权。 | 同一 clean/pushed 且准确 CI 通过的候选 SHA；本地范围证据齐全；对外发布停在所有者审核，不因纯事实提交无限重包。 |

## 源码、安装与 dirty 归属

开始时本地与旧origin/main同为815d8e7ec32bb747194493a728ae6d37ba45baaf，工作区干净。fresh fetch确认它是远端b5cc822c5e372239f0994b7c406e931e8fa786a7的祖先，仅新增交付文档，已安全fast-forward；该SHA的Windows Core CI 38575811624成功。AGENTS按需索引单项更新已推送7742b3c9af6ad5f4e5341c8bb3a135a279b79657，准确SHA CI 38605445392已成功，可以在安全交接采用。

精确恢复政策/进度保全源码、相关测试和指南修复提交为07d1727b76664142615f7eebce2888e3bff654ec；[Windows Core CI 38620035304](https://github.com/AvaloNero/Spherewright/actions/runs/38620035304) headSha完全匹配、completed/success。该同批是本次迁移与输入施工使用的历史执行cohort；后续已实装2a候选，旧完整安装及新菜单/v6披露见[部署记录](evidence/2026-10-15/owned-primary-29145-menu-deployment.md)；旧版本拒绝、原证据和相关CI均保留，不用新报告覆盖旧结果。

候选目标元数据/两语包内README/开发Release notes的6路径修正3bc4cddc191e915fe18ca5f9cd13bc91b1b2c0f4已普通push且[准确CI38627098714](https://github.com/AvaloNero/Spherewright/actions/runs/38627098714)成功。冻结源码2a7a98d1932c404e3a7abc2c11233d9c0b8780d9对应[准确CI38627772732](https://github.com/AvaloNero/Spherewright/actions/runs/38627772732) completed/success；同SHA双候选sourceDirty=false，包字节未再生成。正式包、逐文件验证及未关闭发布门见[候选报告](evidence/2026-10-15/delivery-candidates-2a7a98d.md)。后续文档事实提交不改变已冻结ZIP及当前2a安装；07d1727是迁移/输入施工使用的历史执行cohort。没有tag、GitHub Release或Thunderstore发布。

此前kit V3事实核验的基线为 `01c616e04d4c3bedf7bb795da7fea348e670a813`，[准确Windows Core CI 38687342272](https://github.com/AvaloNero/Spherewright/actions/runs/38687342272) completed/success；该轮本地、origin/main、实时main一致且clean。此后新增的最终整案恢复由[独立报告](evidence/2026-10-15/final-whole-preparation-recovery.md)记录。本轮维护以 `7c8da417d5e9fd48159c900453963760c84ddf61` 为clean基线，[准确Windows Core CI 38688895137](https://github.com/AvaloNero/Spherewright/actions/runs/38688895137) completed/success；本页和该恢复报告为本次授权范围。没有产品源码或候选包变化，不热替换运行cohort，也不重打包。

输入施工曾使用07d1727历史cohort，随后冻结2a手动候选已实装并用于完整三级链中途同45恢复和输出续建。当前实际为同一2a7a98d1932c404e3a7abc2c11233d9c0b8780d9 Thunderstore原始5共置文件（4DLL+单文件MCP），另保全原manual224 MCP，合229产品hash逐份通过；Plugin/Core/Contracts为0.4.0.0 / 0.4.0+该完整SHA，实际TS MCP server0.4.0.0。Native0.10.35.29145、Assembly-CSharp SHA B84B60DF5A24947A014801F23B9B85D1E77853F42A331918E2DC85B153E50527，5refs未变。原31迁移票据已消费；本次TS为default同45恢复，不新增迁移。菜单/228或229文件/metadata/恢复/生产各按真实边界区分，当前产品与证据HEAD不同属纯事实差异。

## 已完成和未保存前缀

R1314先建普通带6622与装配机6623（配方78），R1316保存两对象；R1318同owned恢复原部分进度；R1320仅续剩余索引1/3/4（实体6624/6626/6625），普通Save覆盖全部五对象。最后普通保存tick116215326、revision7、durable Journal102，三条版本转换一致。R1321为67页、6626 built/0 prebuild/55 details；除五项预期新增外，静态配置、拓扑和资源节点差异为0。opening lifetime588的fixed20窗口完整封闭、lifetime608；历史整案209 accepted/47 Moves/66 ordinary Saves。完整原件索引见[R1307–R1321](evidence/2026-10-14/foundry-progress-recovery-r1307.md)。

本次发现ignored受保护账本晚于跟踪快照：R1332在独立封窗后开启新fixed20，opening lifetime608，随后1项取料accepted、lifetime609。原action 44dbfb3a-08da-41a9-b43b-6c7960d40232成功终态：物品1005从862原子扣4、玩家0→4。随后源仓多出的1件未归因，不计来源信用。R1337核原action、全背包差量和原intent，当前账本无在途/unknown，但此动作尚未普通保存，窗口仍frozen且保留保存槽位。禁止换键重取。历史整案为210 accepted/47 Moves/66 ordinary Saves。

R1334北侧29带及末级实际端口11的原生正例只证明当时预检可行，29带/新输入分拣器没有建成证据。R1335调用器保留，其context/approval及当前现场仍须fresh核，不凭脚本存在称已施工。完整三级Foundry与自动供给未因此关闭。

上述为旧截面。Native45本轮先声明完整三级整合计划，原地正常施工29带并逐点读取真实ID，随后仅续filter1209的sorter6656和统一Save。原source5942旧连接、全部五对象及全厂旧对象保全；原6622/6624输入保留备用，不声称这两个入口已有供料。当前输入经新tail6655→sorter6656→6623端口11；输出经6625→6658/6659→filter1210 sorter6660→仓6657实际口4。5942及6625的朝向变化均严格匹配各自Native成功后读回和whole_path_native_rotation_v1规则，其他静态差异0。输入见[中途checkpoint](evidence/2026-10-15/three-stage-input-checkpoint.md)，输出及完整6660独立核验见[输出报告](evidence/2026-10-15/three-stage-output-prefix.md)。

## 最终验收与交付边界

- 下一步：本地候选证据交所有者审核；当前阶段不再安排Game/Bridge写入、重启或候选重打包。完整kit同45恢复、输入已存/输出未建时的中途恢复、三级施工和最终联合供给均有各自记录；不重做成功前缀，不重开既有Governor/限定蓝图门。
- 来源readiness独立结果：141请求/280.851秒；239-object cut的四段Native路径、独立lab256与三个独立600-tick观察均完成，短窗不计连续信用。该readiness截面新仓6657从4增至5、旧仓5331保持659；燃料棒item1802为1802→1804、重氢item1121为15→20、氢item1120为9818→9819（实体ID165为氢罐，不是物品数量）。这些是该readiness阶段的截面，不与最终联合窗口的22个cut合并。三个网络比率为1，实测7.27077 ticks/s。翘曲器目标≥1/min；燃料≥1/min是最终联合长窗事前最低速率，不是readiness门槛。R1300未给燃料数值最低值，也不套用Governor的10%。最终来源与产出核验见[联合供给报告](evidence/2026-10-15/final-joint-supply-36060.md)。
- kit V3状态：root独立核对222组原始对及8个唯一action/key的ACK/terminal，kit全项齐备，完整工厂6660 built/0 prebuild、静态/双边连接差异0、全背包守恒。V1未执行并已退役；V2在空delta解析失败，仅2只读、0 prepare/commit；V3只修枚举后完成获批8步，不重放。R443的1210目标为30，现有库存900不是目标且不产生自动供给信用；32条基础带/30目标、3个分拣器/2目标，4批最小生产共12条带。取材标识为Cu1104（物品代码1104），不是1106。明细与原件见[kit阶段报告](evidence/2026-10-15/current-departure-kit.md)；最终fixed20窗口已封于10/20，未用10槽作废、不扩窗。
- DSP：旧PID50884已退出；唯一当前Native PID为40884，仍运行。此前正常Close与Steam派发各仅一次，没有重复启动。候选仍为冻结2a/Native45；TS配对事务committed、marker absent、229产品/606handoff/原票据J保全。本地标准共置载荷实装已过；Manager/profile、跨电脑和事务崩溃恢复未验证。会话/Host结束不触发Save、退出或Load。
- 保存/观察：最终kit同45恢复时Native内部正常重存tick116309367，后续观察tick116309589/rev1/J102，四次历史转换保留；这是此前恢复截面。最终覆盖Save另于tick116353747完成，观察tick116353967/rev2，Journal J102的102条记录和四次历史转换保留。外部普通Save总数72、Moves47；四次Native内部正常重存另计。完整工厂6660/0prebuild、静态/双边连接差异0、全背包与九项kit保全；此前5943物品过滤改为1210后待机、已有持货仍送至5329，以及两条计划图外边/精确Native朝向变化均有原件。历史44dbfb3a…未保存的4件石矿（物品1005）不计当前恢复/保存信用，禁止换键重取。
- 实际窗口：opening608旧fixed20的16个唯一原action/key已独立封闭，lifetime624/whole225、4个空槽不继承；该阶段47 Moves/70 external普通Saves，迁移/manual/TS三次Native内部重存另计。最终fixed20以openingLifetime624起算，10个唯一action/key均有ACK/terminal，封于lifetime634/whole235；20槽上限未扩展，未用10槽作废，不开新窗。联合Native-valid窗口覆盖36060 ticks（116316166→116352225），79个有效样本（上限90），1822原始请求/响应与22个cut；0 accepted、无gap/reset，inFlight=false/unknown=false。sourceConditionsPassed与wholeSupplyPassed均true。原声明SHA D811FFEC2E9845842A4EDA072425C8FBA665E9A70B17D6A941EC595770047C96及7400秒/3600请求/0写入预算未变；执行耗时5020.5297秒，未续期；exec12612 exit0，pwsh43400已退出。
- 退役：旧27对象Foundry候选、旧不兼容进度、R1327/R1328旧输出候选保持退役；五对象、已完成来源和所有成功前缀禁止重做。
- 预算：旧菜单/披露/打包预算均已结束。本次精确迁移原声明240秒/120请求/最多1accepted，实际约18.8秒/14请求/1accepted；仅摘要读取断言失败。随后独立120秒/4read/0accepted约5.2秒完成。完整工厂240秒/150请求预算实际19.194秒/133请求，配方目录另以120秒/4请求预算实际2.16秒结束，均0accepted。来源readiness的720秒/400请求/0写入阶段以141请求/280.851秒独立通过；239-object cut、独立lab256和三个独立600-tick观察不提供连续信用。联合长窗按原批准7400秒/3600请求/0写入预算（helper上限7200秒/3500请求；每450 game ticks取样、Native样本600 ticks、实体每4次、轮询每4次、单样本最多90请求）执行，以79个有效样本（上限90）、36060 ticks、1822请求/5020.5297秒终态通过，无续期或拼接。覆盖Save为80请求/33.913秒/1 accepted；原fixed20累计10个唯一action/key后封闭，20槽上限不变，余10槽作废。
- 输入预算核销：旧两Move方案首prepare因2个shore-risk样本被本地拒绝，5请求/0accepted结束，整个approach退役；新原地700请求/1800秒阶段实际328请求/599.137秒/1accepted，29带原action成功，第二Native正例因旧本地sourceSlot>=0 guard误挡而停止。root逐328对原件核清后，只修caller接受已公开belt_virtual精确-1并用真实物理槽做建成读回；4项actual Native正负fixtures/实际File smoke0Game通过。新续片独立300请求/900秒预算，以132请求/102.332秒/2accepted结束，不能称原阶段续期；没有重复29带。当前手动候选冷安装为独立必要目的600秒/2游戏read/0accepted，终态后即停并独立审计。
- 离线验证：复用ignored SDK8.0.424，绿CI源码7742b3c的locked restore、Core/Contracts/MCP Release构建与3028测试（73/2707/248，0失败/跳过）通过；95项工具/参数/resource/指南policy与source及安装MCP64工具/1resource/指南一致性通过。旧ref 74AC…保留；在独立干净detached checkout收集5/5当前安装refs，B84B…及manifest逐份匹配，Plugin Release为0警告/错误。此为当前DLL（fresh菜单29145）的离线编译，不能当跨版本恢复/兼容或最终候选实机通过；准确报告见[本轮离线验证](evidence/2026-10-15/delivery-build-validation.md)。
- 版本边界：default仍严格同版本；旧安装拒绝和11项原生研究保留。用户已对披露后的原问题明确回复“确认这次精确迁移”，fresh v6同候选/新digest唯一commit通过，原票据在Load前持久消费；完整J102及前三转换与R1318原Native逐项相同，第四31→45在tick116215327/J102持久化后正常重存。新票据SHA为EC49DBBD4527CFC87AAE486D04C4C0F817E4BDDA53CE2ABD6DF23145C4DC5F5F，45/min116215357/checkpoint102、匹配当前session/原PID且非quarantine/未过期。只证明本次精确迁移，不扩大其他版本/候选授权。
- 进度保全：精确buildId39cc6803-df11-4d9c-8864-37db49dc8ca9读取为completed/submitted5/5、persistenceHealthy/freshWorldReconciled均true、无blockers，五对象6622/6624/6623/6626/6625逐项保全。完整恢复后6626工厂与R1321静态/拓扑差异0。原summary_only响应不能当损坏；没有清空、手改、归档或重建进度。动态供料、完整三级链与最终包实机门不因此关闭。
- 修复回归：MCP258/258、相关Core271/271及初次失败的局部修复见[候选离线验证](evidence/2026-10-15/owned-primary-29145-candidate-validation.md)。准确07d1727 CI已success；clean archive使用SDK8.0.424完成全solution locked restore、当前5份原生refs完整Release构建（0警告/错误）及win-x64 self-contained MCP publish。228文件、产品SourceRevision、完整工具/参数/resource/playbook、真实安装/新菜单/v6准备已独立核验，见[本次部署](evidence/2026-10-15/owned-primary-29145-menu-deployment.md)。validation-only ZIP不是手动/Thunderstore最终候选，也不证明它们的实装或恢复；后续2a双候选的本地实装/同45恢复与最终验收见[候选审查](evidence/2026-10-15/review-delivery.md)及[最终联合报告](evidence/2026-10-15/final-joint-supply-36060.md)。
- 双候选已生成：ignored `artifacts/v0.4.0-review-2a7a98d/` 的手动ZIP SHA为665FA1B30A86A4D49AC7E69CE174E08348DCA07D159A24AF766ABED89AFD79CB，Thunderstore为81D9D6ED0BAEC843AB22ADE38158102212A5487A43EFEE22769F500B7CF85D91；各有匹配sidecar。正式入口全solution locked restore/完整Release/当前5refs/两包一次完成，0警告/错误；准确源码CI3108/3108、0失败/跳过。root核全部文件map、9产品版本及两布局4Plugin/guide相同；手动目录MCP与TS实际单文件MCP的完整64工具/参数/resource/guide主harness均通过。TS验证fixture不是交付包；补充exit原件-1与JSON0不一致、不计通过。包内说明/Release notes保持开发态与原限制，详见[候选报告](evidence/2026-10-15/delivery-candidates-2a7a98d.md)。
- 剩余交付边界：同SHA手动和Thunderstore各自适用布局的实装、运行、same45恢复均已通过；候选文件map与实际安装IO字节独立核对见[候选交付审查](evidence/2026-10-15/review-delivery.md)。sourceConditionsPassed/wholeSupplyPassed均true，覆盖Save与封窗核验通过。仍未验证Manager/profile、跨电脑、事务崩溃恢复及Overseer carrier冻结600 ticks后恢复分支；CI列出Contracts/Core/MCP项目计数，但不按Overseer/Foundry/Governor场景拆分。没有正式tag、GitHub Release或Thunderstore发布；0.4候选提交所有者审核。
- 当前安装续接：手动输入已存/输出未建时中途恢复、TS标准共置载荷same45恢复及最终wholekit same45恢复分别有原件；TS实际单文件MCP完整surface通过。原completed5/5 build的`construction_topology_changed_or_unproven`旧标记仍保留，不能重放旧build；最终6660实体快照及联合来源通过是另一验收范围，不清除此蓝图标记，也不改写成功前缀。两次`BRIDGE_NOT_READY` prepare均0commit且票据/J未变，随后仅一次同45 default恢复成功；不把重试计作第二次迁移。
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
| 最终wholekit SAME45恢复writer，80请求/1accepted | ae64950193824e1293955a544fb84be2:162 | 9BBE9C73C2EF83E2D65D7D2125C7FFB91652BB8BE0C62942B73D7A8CF7D96814 |
| root独立核验最终恢复、全6660、九项kit及窗口 | 88d30e970b314c6d81d091b3c98c2a0e:1 | CD6DC40647F35A3BC453B3FF152A94357D2D45CBED9DC8B3C784B7249E70FE91 |
| 最终恢复前第二次BRIDGE_NOT_READY，0commit | 1a0095295af0479bb07b2d89940fb778:5 | F6996E34DF11CA7D69BEA783A3C1F2CF43885207C19DD1C7E0201C796963A68C |
| 最终来源readiness阶段720秒/400请求/0个accepted写入批准；caller SHA-256 8857528E4A12DDEFE5FF7D4D670C88E005AC0410207623C9138044381F767249 | 30ccc4e3ed404486b91c91f194a0d337:1 | 7A5039C19FA1578C2FF15A14FFB37656424F575A7F852171E5BCF69CD54309E6 |
| 最终来源readiness独立审核，141请求/280.851秒 | c7b45177aba043b6b32a09700518cfc1:1 | DF7981BE1F1CD62D48D13CB25DC6FEC0D4614A82AE3B780E08D297C12F51FC55 |
| 最终恢复前启动阶段positive-readiness观察，2 reads/0 commit且plan废弃、未Load | f8f2c453133541f487389f05878969aa:1 | 911C2E1B99E073A31CED4BD62BF2F1917D9B8D665B98BACF6C9FBD2B71EFC760 |
| 最终≥36000-tick联合长窗事前有限批准；预算7400秒/3600请求/0写入 | f4a17b49e36c46be9bf4ebe5d75a0197:1 | D811FFEC2E9845842A4EDA072425C8FBA665E9A70B17D6A941EC595770047C96 |
| R443九项出发清单root独立核对 | c78c15f06d8248bda55bfb0164fd87ca:1 | A4A36AD9E36E692313A212CA0747320762A6E84E6AE2B6EC39BDE7E47920BBDA |
| R1280燃料60件有限取料/保存root独立核对 | 23a9b8e84fdc42558679ca803e39b173:90 | 38F290A62DFC0E3F00B7C7B5661AD155C7C7C7F5CD440AAF4CBF1C28B292D16A |
| V3 kit 8步/完整6660/Save/J102 root独立核验 | 067b144cf90a4e43a4a665777505218e:1 | 9A33750D8369CB69189CF083EDC3F8F79E027599D4B50068890C8A7A6D060D64 |
| Native区间计数边界修正的离线root核验；不改活动caller/imports/产品 | 29976d3ceec04817b91b0921c33e370a:1 | DCA5485DF9CCFEECFA2C54C815536C075F9B32499A229C1862F065A0C2713E88 |
| 联合长窗原writer采样终态；79个有效样本（上限90）、36060 ticks、0 accepted | 1736af59d3454350a94aa84e5ebc799c:3726 | 5B87C66D0BE02240482F0736AD86DD3125D575CC0B848FD829E1CA4692DA0E83 |
| 最终联合长窗原始Data：1822对、79样本、22 cut、无gap/reset/写入 | ed14b0b3c4564a70a88cd2d8dc582a67:1 | 00F3BADC19FF85212D89A792767A7027B421F4925C5E1C5292F985A6E16687BF |
| 覆盖Save原writer：80请求、33.913秒、1 accepted | 0527eaed5eed43eb99ffec9b28d0f2d4:162 | 2173F0EE14497D0952158B403A27609D8FAA5DC44B49DF5A50DCE6430E0B573B |
| 覆盖Save独立核验 | a3e2d9a88a204a0aaa9669ef13d18ad6:1 | E094472A27347FB6513BFC4E125245C1C865E31C509710F0BAC1CFA26D02E46E |
| 最终来源/整案独立审核：sourceConditionsPassed/wholeSupplyPassed=true | 35389bf22f4c4e229ee4444ccf7a4c22:1 | F5C077BAA64A38C4231224FD7E6DBF490897BB82DE5B0427D414E5652A6411AE |
| fixed-20窗口最终封闭：10唯一动作、624→634、whole235、余10槽作废 | fcaf3f36119c4b7e9ffc7be30bc75789:1 | 8CE13D5EFCE49EAE56604A108D35C6E3DE73F7AFC9CF480B05A54DEDBC7C6F29 |
| 冻结候选与实际安装IO独立审核；ZIP未改，安装文件版本/hash一致 | 281413adc4db44b498c7044638564146:1 | 179B270D4C235BE08E51192FA3EE03DAF7B0B66F975EEE6FB8DC5338AB41C67F |
