# Spherewright 当前快照

更新：2026-10-15（Asia/Singapore）。持续收尾顺序见 [0.4 交付文档](v0.4-delivery.md)。0.4 整体验收尚未完成，历史通过、已安装与本次 fresh 核验分开记录。

## 原验收对应表

| 原条款 / 准确引用 | 已有证据及适用版本 | 真实缺口 | 最小下一步 | 通过条件 |
| --- | --- | --- | --- | --- |
| [Overseer 四项验收](../ROADMAP.md#overseer-验收门)：诊断、正常修复、稳定非零、跨恢复窗口语义 | Roadmap 原诊断/恢复证据：三种故障、钛块12/min、healthy Save、Journal49/49，历史候选8c49bcb；不当作最终0.4包。 | 最终源码/安装cohort的相关回归与限制说明。 | 仅复跑受改动影响的诊断、安全与恢复路径。 | 保留原四项标准，不重造故障实验；保留真实carrier stall分支的既有实机限制。 |
| [Foundry 运行模块复制与生命周期](../ROADMAP.md#foundry-验收门) | 原限定六对象模块、取消/恢复/去重及持续石墨输出保持原范围；29131的R1314→R1320五对象另已保存重启续建，R1321独立通过。 | 五对象自身不证明完整三级链。 | 保留所有成功实体和原build，把它纳入完整链计划/产出核验。 | 不重复施工或扣料；旧限定门不重开，后续只核受影响接口。 |
| [Foundry 三级完整计划、正常施工、中途保存重启续建、失效重规划](../ROADMAP.md#foundry-验收门) | 既有1112/1127→1209→1210分段计划和施工可复用；29131 R1334北侧29带输入及末级端口11预检正例已核原SHA；旧27对象候选退役。 | 完整事前设备/外供/电力/物流/精确成本的适用覆盖；末级1209自动输入/1210输出；三级整案中途恢复覆盖仍须逐条核。 | 先核当前模块和既有事前计划，缺什么只补什么；新模块先完整有界规划，再fresh原生预检最小补片。 | 原五项全部有对应证据；不补造旧计划，不以建成后重启冒充中途续建，未来ID建成后读取。 |
| [Governor 五项验收](../ROADMAP.md#governor-验收门) | 原锁定31→62/min、连续37128 ticks及限定升级/保存恢复保留原通过范围。 | 最终候选相关回归和适用范围说明。 | 复用原声明/证据，仅核受改动影响的接口。 | 不重锁基线、不重复两倍实验，不扩大为全厂配平。 |
| [双自动供给、出发清单及整体保存恢复](../ROADMAP.md#跨星系准备与整体交付验收) | 既有取材、来源和施工；R1300的36348 ticks只保留历史结论，不计恢复后新session。R1320/R1321覆盖五对象保存/审计。 | 最终恢复后的燃料/翘曲器与共享来源连续窗，当前准备清单差额，同owned最终保存恢复。 | 施工/必要部署稳定后，按实际tick速度预声明一个联合有限≥36000-tick窗口；只补清单差额。 | 原最低速率、库存趋势、真实自动来源、功率/运输及无缺口同session窗口通过；缓存、搬仓、P=C不能替代来源。 |
| [同源码SHA双候选及完整验证](v0.4-delivery.md#完成定义) | 0b67f85已加强64工具完整集合自检；交付基线b5cc822准确SHA CI成功。 | 手动/Thunderstore候选、完整构建/测试、各自安装运行恢复、校验和、Release notes。 | 离线核版本/指南/工具参数/resource；稳定后冻结干净、已推送、绿CI源码SHA生成两包。 | 两包sourceDirty=false且同SHA，全部适用检查有证据，停在所有者审核发布；后续纯事实证据引用候选SHA不无限重打包。 |

## 源码、安装与 dirty 归属

开始时本地与旧origin/main同为815d8e7ec32bb747194493a728ae6d37ba45baaf，工作区干净。fresh fetch确认它是远端b5cc822c5e372239f0994b7c406e931e8fa786a7的祖先，仅新增交付文档，已安全fast-forward；该SHA的Windows Core CI 38575811624成功。AGENTS按需索引单项更新已推送7742b3c9af6ad5f4e5341c8bb3a135a279b79657，准确SHA CI 38605445392已成功，可以在安全交接采用。

本轮离线验证事实f51cffaa9aebe8040fef8f9fba15463e29ecbfcc的准确CI38609907158成功；三文件版本blocker封存613dcf3abc62b0a231bf25aa66588dc7bcf977d4已推送，准确CI38611805131成功，核验时HEAD=origin/main、工作树clean。这里记录已核验基线；包含本快照最后文字覆盖的事实SHA以维护终态回执为准，不自引用自身提交或混作候选源码SHA。源码执行基线仍7742b3c，安装cohort仍6528e1f；本次最后覆盖只由root负责该页文字，无代码或游戏变动。

磁盘安装Bridge.Core/Contracts/Plugin与安装MCP均为0.4.0.0，构建标识6528e1f7101a714826f7364cef2b0ad75c4b6e82；安装MCP只做initialize/完整64工具/1resource/指南读取后退出，不触发Bridge。fresh Native菜单已确认DSP实际为0.10.35.29145，桥接healthy，游戏未载入；29131是原owned主档/票据及R1337历史版本。当前Assembly-CSharp SHA为B84B60DF5A24947A014801F23B9B85D1E77853F42A331918E2DC85B153E50527，不能用Updates/Versions.txt旧29131行代替当前Native版本。源码文档SHA不等于安装SHA。root负责本快照和原件独立审计；离线维护负责索引、版本/包指南及相关回归、串行Git；唯一新writer按批准有限阶段执行。其他执行者的ignored成果保留，未reset/clean/force-push或覆盖。

## 已完成和未保存前缀

R1314先建普通带6622与装配机6623（配方78），R1316保存两对象；R1318同owned恢复原部分进度；R1320仅续剩余索引1/3/4（实体6624/6626/6625），普通Save覆盖全部五对象。最后普通保存tick116215326、revision7、durable Journal102，三条版本转换一致。R1321为67页、6626 built/0 prebuild/55 details；除五项预期新增外，静态配置、拓扑和资源节点差异为0。opening lifetime588的fixed20窗口完整封闭、lifetime608；历史整案209 accepted/47 Moves/66 ordinary Saves。完整原件索引见[R1307–R1321](evidence/2026-10-14/foundry-progress-recovery-r1307.md)。

本次发现ignored受保护账本晚于跟踪快照：R1332在独立封窗后开启新fixed20，opening lifetime608，随后1项取料accepted、lifetime609。原action 44dbfb3a-08da-41a9-b43b-6c7960d40232成功终态：物品1005从862原子扣4、玩家0→4。随后源仓多出的1件未归因，不计来源信用。R1337核原action、全背包差量和原intent，当前账本无在途/unknown，但此动作尚未普通保存，窗口仍frozen且保留保存槽位。禁止换键重取。历史整案为210 accepted/47 Moves/66 ordinary Saves。

R1334北侧29带及末级实际端口11的原生正例只证明当时预检可行，29带/新输入分拣器没有建成证据。R1335调用器保留，其context/approval及当前现场仍须fresh核，不凭脚本存在称已施工。完整三级Foundry与自动供给未因此关闭。

## 当前交接与下一安全入口

- 下一唯一目标：先解决29131 owned主档与fresh29145运行时的版本恢复门，保全未保存原transfer和全部成功工厂前缀；合法同owned恢复后继续五对象模块1209自动输入/1210输出与完整三级链。迁移须后续确认，本轮不载入跨版本主档。
- 旧writer已停：旧root最新turn interrupted/idle，旧game writerR1337 completed/notLoaded；本轮writer已完成唯一菜单与default prepare两次只读请求后停止，0accepted/0commit/0Load/0Save。root仅核精确原件，没有并发游戏请求或在途调用。
- DSP：首只读阶段DSP0/Steam在，原退出原因不明；现仅一次broker启动，incident27b4c8bcc8d14e878ba2a410d44390d1，原intent dispatched=true/outcomeUnknown=false，root精确核其SHA。唯一DSP PID29080，fresh菜单gameLoaded=false/29145/bridgeConnected=true/healthy/revision0、cap含owned-game.resume；0Load/Save/commit，不重启/不重发。阶段结束不关闭DSP/Steam。
- 保存/观察：Save仍116215326/J102；R1337精确原件观察116289647、revision8/J102、healthy，仅为退出前证据。票据、恢复候选及未保存4石矿前缀须fresh核，不能用旧数值commit。
- 实际窗口：R1332 fixed20，external1/lifetime609、frozen=true、normalSaveReserved=true、knownInFlight=0；不换键重放、不开新窗或转旧额度。剩余19槽含保存，root独立核后再批准当前片。
- 退役：旧27对象Foundry候选、旧不兼容进度、R1327/R1328旧输出候选保持退役；五对象、已完成来源和所有成功前缀禁止重做。
- 预算：启动阶段已封，dispatch1/Bridge0/0写；菜单阶段1请求、default prepare阶段1请求/1.25秒均已封，0accepted。prepare明确SESSION_NOT_OWNED、result=null，未生成计划/action；通用quarantine提示不证明本档隔离。禁止换模式盲试/确认/commit。原fixed20仍1/20/lifetime609/frozen，剩19含保存不变；候选未冻结，writer已停。
- 离线验证：复用ignored SDK8.0.424，绿CI源码7742b3c的locked restore、Core/Contracts/MCP Release构建与3028测试（73/2707/248，0失败/跳过）通过；95项工具/参数/resource/指南policy与source及安装MCP64工具/1resource/指南一致性通过。旧ref 74AC…保留；在独立干净detached checkout收集5/5当前安装refs，B84B…及manifest逐份匹配，Plugin Release为0警告/错误。此为当前DLL（fresh菜单29145）的离线编译，不能当跨版本恢复/兼容或最终候选实机通过；准确报告见[本轮离线验证](evidence/2026-10-15/delivery-build-validation.md)。
- 版本硬边界：default票据入口要求source/current版本相等；已证29131≠29145足以拒绝。当前兼容政策仅研究到29131，MaximumDurableTransitions=3且原Journal已3条；primary migration仅支持29104→29131。无合法29131→29145路径，不能改变票据版本/expiry、跳Journal/清进度/改原档或盲增第四转换。本轮停在真实版本恢复门，完整原证据及可执行续接点见[29145 blocker](evidence/2026-10-15/owned-primary-29145-blocker.md)。
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
