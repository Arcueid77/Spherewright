# Spherewright 当前快照

更新：2026-10-15（Asia/Singapore），29145同批菜单部署及原生v6披露已独立核验；等待本次迁移的后续明确确认。持续收尾顺序见 [0.4 交付文档](v0.4-delivery.md)。0.4 整体验收尚未完成，历史通过、已安装与本次 fresh 核验分开记录。

## 原验收对应表

| 原条款 / 准确引用 | 已有证据及适用版本 | 真实缺口 | 最小下一步 | 通过条件 |
| --- | --- | --- | --- | --- |
| [Overseer 四项验收](../ROADMAP.md#overseer-验收门)：诊断、正常修复、稳定非零、跨恢复窗口语义 | Roadmap 原诊断/恢复证据：三种故障、钛块12/min、healthy Save、Journal49/49，历史候选8c49bcb；不当作最终0.4包。 | 最终源码/安装cohort的相关回归与限制说明。 | 仅复跑受改动影响的诊断、安全与恢复路径。 | 保留原四项标准，不重造故障实验；保留真实carrier stall分支的既有实机限制。 |
| [Foundry 运行模块复制与生命周期](../ROADMAP.md#foundry-验收门) | 原限定六对象模块、取消/恢复/去重及持续石墨输出保持原范围；29131的R1314→R1320五对象另已保存重启续建，R1321独立通过。 | 五对象自身不证明完整三级链。 | 保留所有成功实体和原build，把它纳入完整链计划/产出核验。 | 不重复施工或扣料；旧限定门不重开，后续只核受影响接口。 |
| [Foundry 三级完整计划、正常施工、中途保存重启续建、失效重规划](../ROADMAP.md#foundry-验收门) | 既有1112/1127→1209→1210分段计划和施工可复用；29131 R1334北侧29带输入及末级端口11预检正例已核原SHA；旧27对象候选退役。 | 完整事前设备/外供/电力/物流/精确成本的适用覆盖；末级1209自动输入/1210输出；三级整案中途恢复覆盖仍须逐条核。 | 先核当前模块和既有事前计划，缺什么只补什么；新模块先完整有界规划，再fresh原生预检最小补片。 | 原五项全部有对应证据；不补造旧计划，不以建成后重启冒充中途续建，未来ID建成后读取。 |
| [Governor 五项验收](../ROADMAP.md#governor-验收门) | 原锁定31→62/min、连续37128 ticks及限定升级/保存恢复保留原通过范围。 | 最终候选相关回归和适用范围说明。 | 复用原声明/证据，仅核受改动影响的接口。 | 不重锁基线、不重复两倍实验，不扩大为全厂配平。 |
| [双自动供给、出发清单及整体保存恢复](../ROADMAP.md#跨星系准备与整体交付验收) | 既有取材、来源和施工；R1300的36348 ticks只保留历史结论，不计恢复后新session。R1320/R1321覆盖五对象保存/审计。 | 最终恢复后的燃料/翘曲器与共享来源连续窗，当前准备清单差额，同owned最终保存恢复。 | 施工/必要部署稳定后，按实际tick速度预声明一个联合有限≥36000-tick窗口；只补清单差额。 | 原最低速率、库存趋势、真实自动来源、功率/运输及无缺口同session窗口通过；缓存、搬仓、P=C不能替代来源。 |
| [同源码SHA双候选及完整验证](v0.4-delivery.md#完成定义) | 0b67f85已加强64工具完整集合自检；07d1727准确SHA CI成功，当前DLL完整Release及同批228文件菜单安装/v6准备通过，见[部署记录](evidence/2026-10-15/owned-primary-29145-menu-deployment.md)。 | 最终手动/Thunderstore候选、各自适用的安装运行恢复、校验和和Release notes；菜单安装不关闭这些门。 | 保持游戏主线；代码/施工稳定后冻结干净、已推送、绿CI源码SHA生成两包。 | 两包sourceDirty=false且同SHA，全部适用检查有证据，停在所有者审核发布；后续纯事实证据引用候选SHA不无限重打包。 |

## 源码、安装与 dirty 归属

开始时本地与旧origin/main同为815d8e7ec32bb747194493a728ae6d37ba45baaf，工作区干净。fresh fetch确认它是远端b5cc822c5e372239f0994b7c406e931e8fa786a7的祖先，仅新增交付文档，已安全fast-forward；该SHA的Windows Core CI 38575811624成功。AGENTS按需索引单项更新已推送7742b3c9af6ad5f4e5341c8bb3a135a279b79657，准确SHA CI 38605445392已成功，可以在安全交接采用。

本轮离线验证事实f51cffaa9aebe8040fef8f9fba15463e29ecbfcc的准确CI38609907158成功；原版本blocker613dcf3abc62b0a231bf25aa66588dc7bcf977d4及末次事实88326888a13417205620b34a07a1f0372946e33a的准确CI也成功。20份精确恢复政策/进度保全源码、相关测试和指南修复已单一目的提交为07d1727b76664142615f7eebce2888e3bff654ec；[Windows Core CI 38620035304](https://github.com/AvaloNero/Spherewright/actions/runs/38620035304) headSha完全匹配、completed/success。维护fresh只读确认HEAD=origin/main=实时远端main=该SHA、clean且无其他dirty。此后本页与新部署证据的纯事实编辑由root负责，维护只获准指定文件Git；它们不改变07d1727安装二进制，不因事实提交重打包/重启。准确文档提交终态以维护回执为准。

磁盘安装Bridge.Core/Contracts/Plugin与MCP均为0.4.0.0 / ProductVersion 0.4.0+07d1727b76664142615f7eebce2888e3bff654ec；从clean、已推送、绿CI源码archive构建，4个Plugin文件及224个MCP文件由root逐份核同一map。完整64工具/参数、1resource及内嵌playbook集合已校验；metadata验证没有游戏调用。fresh新进程Native菜单为0.10.35.29145/healthy/未载入；29131仍是原owned主档/票据版本。实际Assembly-CSharp SHA为B84B60DF5A24947A014801F23B9B85D1E77853F42A331918E2DC85B153E50527，旧Updates版本行或refs目录标签不作当前版本证据。root独立审计；维护仅离线/Git；唯一writer按有限阶段执行。其他执行者的ignored成果、原票据/Journal/进度与runtime-handoff保留，未reset/clean/force-push或覆盖。

## 已完成和未保存前缀

R1314先建普通带6622与装配机6623（配方78），R1316保存两对象；R1318同owned恢复原部分进度；R1320仅续剩余索引1/3/4（实体6624/6626/6625），普通Save覆盖全部五对象。最后普通保存tick116215326、revision7、durable Journal102，三条版本转换一致。R1321为67页、6626 built/0 prebuild/55 details；除五项预期新增外，静态配置、拓扑和资源节点差异为0。opening lifetime588的fixed20窗口完整封闭、lifetime608；历史整案209 accepted/47 Moves/66 ordinary Saves。完整原件索引见[R1307–R1321](evidence/2026-10-14/foundry-progress-recovery-r1307.md)。

本次发现ignored受保护账本晚于跟踪快照：R1332在独立封窗后开启新fixed20，opening lifetime608，随后1项取料accepted、lifetime609。原action 44dbfb3a-08da-41a9-b43b-6c7960d40232成功终态：物品1005从862原子扣4、玩家0→4。随后源仓多出的1件未归因，不计来源信用。R1337核原action、全背包差量和原intent，当前账本无在途/unknown，但此动作尚未普通保存，窗口仍frozen且保留保存槽位。禁止换键重取。历史整案为210 accepted/47 Moves/66 ordinary Saves。

R1334北侧29带及末级实际端口11的原生正例只证明当时预检可行，29带/新输入分拣器没有建成证据。R1335调用器保留，其context/approval及当前现场仍须fresh核，不凭脚本存在称已施工。完整三级Foundry与自动供给未因此关闭。

## 当前交接与下一安全入口

- 下一唯一目标：本次精确29131→29145主档候选已完成原生v6披露及root独立核验，等待披露后的明确用户迁移确认。该确认到来后，先核writer已停和fresh现场，再在单一有限阶段fresh prepare同一保存点、核新digest/prompt后只commit一次，核第四转换durable和正常Save/新票据；原披露planToken不存储、不复用。合法恢复后唯一游戏主线仍是现有模块1209自动输入/1210输出及完整三级链。
- writer已停：旧root interrupted/idle，旧writer R1337 completed/notLoaded；当前writer已完成唯一冷部署阶段及随后2-call v6披露阶段并停止。没有恢复commit意图、在途或unknown；root只读原件/安装hash，0root游戏调用。迁移未获后续确认，不执行Load。
- DSP：先前唯一broker启动PID29080记录保留。必要同批冷部署只正常关闭该已确认菜单一次、事务安装一次、Steam桌面调度一次，operation ac8c6cf8ab7142ef944be259950047e8；新唯一DSP PID35240 Responding，原Steam36408及启动时间保留。fresh Native菜单gameLoaded=false/owned=false/29145/bridgeConnected=true/healthy/revision0；实际游戏就绪菜单已证实，owned恢复未证实。阶段结束不Save/退出/Load，不重发任何启动intent。
- 保存/观察：Save仍116215326/J102；退出前R1337观察116289647/revision8/J102。原生v6候选tick=minimumTick=116215326、P104、exact embedded identity=true。该保存点早于观察和未保存4石矿动作，不能声称未保存前缀已恢复；其原action/已用额度保留，恢复后fresh核实际库存/原回执，禁止换键重取。
- 实际窗口：R1332 fixed20，external1/lifetime609、frozen=true、normalSaveReserved=true、knownInFlight=0；不换键重放、不开新窗或转旧额度。剩余19槽含保存，root独立核后再批准当前片。
- 退役：旧27对象Foundry候选、旧不兼容进度、R1327/R1328旧输出候选保持退役；五对象、已完成来源和所有成功前缀禁止重做。
- 预算：原启动/default拒绝阶段保持封闭；原SESSION_NOT_OWNED结论只对应旧安装/default路径，通用quarantine提示不证明隔离。新冷部署有限570秒/1close/1install/1dispatch/1菜单读，v6披露有限180秒/1fresh菜单读/1prepare（约2.8秒）均已完成、0Load/Save/commit/accepted。原fixed20仍1/20/lifetime609/frozen、剩19含保存不变。仅执行cohort冻结为07d1727，最终双候选尚未生成；没有运行中观察窗、后台任务或可续期预算。
- 离线验证：复用ignored SDK8.0.424，绿CI源码7742b3c的locked restore、Core/Contracts/MCP Release构建与3028测试（73/2707/248，0失败/跳过）通过；95项工具/参数/resource/指南policy与source及安装MCP64工具/1resource/指南一致性通过。旧ref 74AC…保留；在独立干净detached checkout收集5/5当前安装refs，B84B…及manifest逐份匹配，Plugin Release为0警告/错误。此为当前DLL（fresh菜单29145）的离线编译，不能当跨版本恢复/兼容或最终候选实机通过；准确报告见[本轮离线验证](evidence/2026-10-15/delivery-build-validation.md)。
- 版本边界：default仍严格同版本；[旧安装拒绝](evidence/2026-10-15/owned-primary-29145-blocker.md)与[11项原生持久化比较](research/game-api-version-0.10.35.29145.md)保留准确范围。07d1727只增加31→45方向、v6专属披露及第四转换，旧v5/Journal连续性/一次消费不变。现已同批部署且fresh Native v6 prepare成功；complete prompt/digest已核、confirmationRequired=true/commitAllowedNow=false，原票据SHA未变。实际迁移Load和恢复后兼容仍未通过，按用户排除项及AGENTS第35行等待后续确认，不能把菜单/prepare当恢复成功。
- 进度保全：fresh只读确认票据SHA未变、非quarantine，Journal origin28529/102条/三转换；原进度schema1、版本29131、唯一build为completed五对象6622/6624/6623/6626/6625，最后证据tick116214800，文件SHA272F16A91F2BAC347587F2CC175E3791C964D726A89EADC26C8CB749125A3587。root候选只允许同owned身份/current session、完整continuous且durable的精确31→45 Journal转换下复用全部completed旧记录，读取不改盘；下次正常durable更新保留全部原计划/对象并stamp45。未完成/unknown/损坏/不匹配仍拒绝，不清空、不手改原件或重建成功对象。离线关联store用替身，实机实体/连接须恢复后fresh核。
- 修复回归：MCP258/258、相关Core271/271及初次失败的局部修复见[候选离线验证](evidence/2026-10-15/owned-primary-29145-candidate-validation.md)。准确07d1727 CI已success；clean archive使用SDK8.0.424完成全solution locked restore、当前5份原生refs完整Release构建（0警告/错误）及win-x64 self-contained MCP publish。228文件、产品SourceRevision、完整工具/参数/resource/playbook、真实安装/新菜单/v6准备已独立核验，见[本次部署](evidence/2026-10-15/owned-primary-29145-menu-deployment.md)。validation-only ZIP不是手动/Thunderstore最终候选，原生Load/实体连续性/最终验收仍缺。
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
