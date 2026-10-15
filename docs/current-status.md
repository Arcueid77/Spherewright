# Spherewright 当前快照

更新：2026-10-15（Asia/Singapore），用户已明确确认本次精确迁移；同owned主档31→45已唯一载入、正常重存并签新票据，J102/四转换及原五对象全部独立核验通过。正在沿现有1209输入/1210输出推进完整三级链。2a7a98d同源码双候选保持冻结；完整三级链、最终联合长窗及候选实机安装/恢复仍开放，持续顺序见 [0.4交付文档](v0.4-delivery.md)。

## 原验收对应表

| 原条款 / 准确引用 | 已有证据及适用版本 | 真实缺口 | 最小下一步 | 通过条件 |
| --- | --- | --- | --- | --- |
| [Overseer 四项验收](../ROADMAP.md#overseer-验收门)：诊断、正常修复、稳定非零、跨恢复窗口语义 | Roadmap 原诊断/恢复证据：三种故障、钛块12/min、healthy Save、Journal49/49，历史候选8c49bcb；不当作最终0.4包。 | 最终源码/安装cohort的相关回归与限制说明。 | 仅复跑受改动影响的诊断、安全与恢复路径。 | 保留原四项标准，不重造故障实验；保留真实carrier stall分支的既有实机限制。 |
| [Foundry 运行模块复制与生命周期](../ROADMAP.md#foundry-验收门) | 原限定六对象模块、取消/恢复/去重及持续石墨输出保持原范围；29131的R1314→R1320五对象另已保存重启续建，R1321独立通过。 | 五对象自身不证明完整三级链。 | 保留所有成功实体和原build，把它纳入完整链计划/产出核验。 | 不重复施工或扣料；旧限定门不重开，后续只核受影响接口。 |
| [Foundry 三级完整计划、正常施工、中途保存重启续建、失效重规划](../ROADMAP.md#foundry-验收门) | 既有1112/1127→1209→1210分段计划和施工可复用；29131 R1334北侧29带输入及末级端口11预检正例已核原SHA；旧27对象候选退役。 | 完整事前设备/外供/电力/物流/精确成本的适用覆盖；末级1209自动输入/1210输出；三级整案中途恢复覆盖仍须逐条核。 | 先核当前模块和既有事前计划，缺什么只补什么；新模块先完整有界规划，再fresh原生预检最小补片。 | 原五项全部有对应证据；不补造旧计划，不以建成后重启冒充中途续建，未来ID建成后读取。 |
| [Governor 五项验收](../ROADMAP.md#governor-验收门) | 原锁定31→62/min、连续37128 ticks及限定升级/保存恢复保留原通过范围。 | 最终候选相关回归和适用范围说明。 | 复用原声明/证据，仅核受改动影响的接口。 | 不重锁基线、不重复两倍实验，不扩大为全厂配平。 |
| [双自动供给、出发清单及整体保存恢复](../ROADMAP.md#跨星系准备与整体交付验收) | 既有取材、来源和施工；R1300的36348 ticks只保留历史结论，不计恢复后新session。R1320/R1321覆盖五对象保存/审计。 | 最终恢复后的燃料/翘曲器与共享来源连续窗，当前准备清单差额，同owned最终保存恢复。 | 施工/必要部署稳定后，按实际tick速度预声明一个联合有限≥36000-tick窗口；只补清单差额。 | 原最低速率、库存趋势、真实自动来源、功率/运输及无缺口同session窗口通过；缓存、搬仓、P=C不能替代来源。 |
| [同源码SHA双候选及完整验证](v0.4-delivery.md#完成定义) | 2a7a98d同clean/pushed/green源码真实手动/ThunderstoreZIP及sidecar已冻结；当前DLL完整Release、准确CI3108测试、全部文件hash/9产品版本、64完整工具/参数/1resource/guide通过，见[双候选报告](evidence/2026-10-15/delivery-candidates-2a7a98d.md)。07菜单安装另有原证据。 | 两包各自适用的安装/运行/恢复、完整游戏门和最终合格Release notes；metadata/菜单不关闭这些门。 | 保留本次ZIP，合法恢复并完成施工/必要代码后安排必要维护边界的适用包验证；内容变化才换候选。 | 两包sourceDirty=false且同SHA，全部适用检查有证据，停在所有者审核发布；后续纯事实证据引用候选SHA不无限重打包。 |

## 源码、安装与 dirty 归属

开始时本地与旧origin/main同为815d8e7ec32bb747194493a728ae6d37ba45baaf，工作区干净。fresh fetch确认它是远端b5cc822c5e372239f0994b7c406e931e8fa786a7的祖先，仅新增交付文档，已安全fast-forward；该SHA的Windows Core CI 38575811624成功。AGENTS按需索引单项更新已推送7742b3c9af6ad5f4e5341c8bb3a135a279b79657，准确SHA CI 38605445392已成功，可以在安全交接采用。

精确恢复政策/进度保全源码、相关测试和指南修复提交为07d1727b76664142615f7eebce2888e3bff654ec；[Windows Core CI 38620035304](https://github.com/AvaloNero/Spherewright/actions/runs/38620035304) headSha完全匹配、completed/success。该同批仍是实际安装执行cohort，完整安装及新菜单/v6披露见[部署记录](evidence/2026-10-15/owned-primary-29145-menu-deployment.md)；旧版本拒绝、原证据和相关CI均保留，不用新报告覆盖旧结果。

候选目标元数据/两语包内README/开发Release notes的6路径修正3bc4cddc191e915fe18ca5f9cd13bc91b1b2c0f4已普通push且[准确CI38627098714](https://github.com/AvaloNero/Spherewright/actions/runs/38627098714)成功。随后纯事实快照提交2a7a98d1932c404e3a7abc2c11233d9c0b8780d9已普通push；本轮打包前维护复核HEAD=origin/main=实时远端main=该完整SHA且clean，[准确CI38627772732](https://github.com/AvaloNero/Spherewright/actions/runs/38627772732) completed/success。本次真实双候选均以2a7a98d冻结，sourceDirty=false；后续本页与日期化候选报告是root的两路径纯事实编辑，由维护仅按这两路径串行commit/push和核准确CI。其最终提交SHA以维护终态为准；不改变已冻结ZIP或07d1727安装，不为事实自引用重打包/部署。其他ignored成果和成功前缀保留，新会话仍fresh核Git/dirty。

候选事实提交3246886ca763aa5e5a94733d70b79be069687901已普通push；维护本轮fresh核HEAD=origin/main=实时main=该SHA、clean，[准确CI38634709236](https://github.com/AvaloNero/Spherewright/actions/runs/38634709236) completed/success。本次随后只有root当前快照和维护获准的新迁移事实文档待提交，其他成果保留；仅事实改动不阻塞无依赖游戏读取。

磁盘安装Bridge.Core/Contracts/Plugin与MCP仍为0.4.0.0 / ProductVersion 0.4.0+07d1727b76664142615f7eebce2888e3bff654ec，实际Native为0.10.35.29145，Assembly-CSharp SHA为B84B60DF5A24947A014801F23B9B85D1E77853F42A331918E2DC85B153E50527。本次caller在唯一原PID35240下fresh核安装cohort后迁移；当前owned主档/新票据均已45，原31票据已按原生路径消费，不手改或复用。4Plugin/224MCP文件及完整工具集合的既有校验保持原范围；候选源码2a7a98d与执行07、最新事实HEAD分别记录，不因事实文档热部署或重打包。

## 已完成和未保存前缀

R1314先建普通带6622与装配机6623（配方78），R1316保存两对象；R1318同owned恢复原部分进度；R1320仅续剩余索引1/3/4（实体6624/6626/6625），普通Save覆盖全部五对象。最后普通保存tick116215326、revision7、durable Journal102，三条版本转换一致。R1321为67页、6626 built/0 prebuild/55 details；除五项预期新增外，静态配置、拓扑和资源节点差异为0。opening lifetime588的fixed20窗口完整封闭、lifetime608；历史整案209 accepted/47 Moves/66 ordinary Saves。完整原件索引见[R1307–R1321](evidence/2026-10-14/foundry-progress-recovery-r1307.md)。

本次发现ignored受保护账本晚于跟踪快照：R1332在独立封窗后开启新fixed20，opening lifetime608，随后1项取料accepted、lifetime609。原action 44dbfb3a-08da-41a9-b43b-6c7960d40232成功终态：物品1005从862原子扣4、玩家0→4。随后源仓多出的1件未归因，不计来源信用。R1337核原action、全背包差量和原intent，当前账本无在途/unknown，但此动作尚未普通保存，窗口仍frozen且保留保存槽位。禁止换键重取。历史整案为210 accepted/47 Moves/66 ordinary Saves。

R1334北侧29带及末级实际端口11的原生正例只证明当时预检可行，29带/新输入分拣器没有建成证据。R1335调用器保留，其context/approval及当前现场仍须fresh核，不凭脚本存在称已施工。完整三级Foundry与自动供给未因此关闭。

## 当前交接与下一安全入口

- 下一唯一目标：为现有三级依赖1112/1127→1209→1210补末级自动输入和持久输出。完整工厂读取已结束：133请求、67页、6626 built、0 prebuild、59详情；root与R1321全部静态配置/拓扑/资源节点逐项比较差异0。Native45运行时完整材料草案确认depth3、目标1210为1/min、四台现有设备合计12.9MW，不需重建机器。下一片先形成前瞻完整有界整合计划，再fresh预检复用R1334的29带输入路线；输出仓/共享来源分配和中途恢复仍是整案开放条件，不给静态读取持续窗口信用。
- writer状态：迁移、纠正性读回、完整工厂和4次运行时配方/建筑/Foundry读取均已结束，writer已停；root正设计当前有限接入片，尚无新的施工commit。离线维护只整理指定迁移事实文档；root0游戏调用，没有并行writer。旧披露token不复用，迁移确认已收到，不再次询问；未决/在途unknown为0。
- DSP：仍为原唯一PID35240/原Steam保留，本轮没有启动/关闭/安装。唯一resume action b3b57506-b00e-48ed-ad55-6a52e7dc3eac终态completed/succeeded，healthy/owned/和平/P104/Native45。首次caller末尾把summary_only进度响应误当完整详情而exit1；保留原件，随后仅带精确buildId的4次只读纠正通过。不是迁移失败或对象丢失，不重放原Load。
- 保存/观察：原候选Save116215326/J102已合法载入；同一次迁移action在第四转换durable后自动正常主档重存116215357并签新45票据，外部Save commit为0，不能再补一次Save或算成第二个accepted。完整工厂immutable tick116224025、结束观察116224382/revision1/J102。退出前116289647及旧4石矿不计恢复信用，当前玩家石矿0；原44dbfb3a…仍历史计数、禁止换键重取。新输出仓可另取现有成品石砖1108，不能把它记成重放旧石矿1005。旧观察较晚不构成拼session窗口。
- 实际窗口：原R1332 fixed20未更改，external2/lifetime610、whole211；本次仅resume新增1 accepted，47 Moves/66外部ordinary Saves不变，另明确本次Native内部主档resave1次。root核完整原终态后knownInFlight清0/unknownfalse；frozen=true、normalSaveReserved=true、剩18槽含保存，不开新窗或补额度。
- 退役：旧27对象Foundry候选、旧不兼容进度、R1327/R1328旧输出候选保持退役；五对象、已完成来源和所有成功前缀禁止重做。
- 预算：旧菜单/披露/打包预算均已结束。本次精确迁移原声明240秒/120请求/最多1accepted，实际约18.8秒/14请求/1accepted；仅摘要读取断言失败。随后独立120秒/4read/0accepted约5.2秒完成。完整工厂240秒/150请求预算以实际19.194秒/133请求结束，配方目录另以120秒/4请求预算实际2.16秒结束，均0accepted；没有运行中的生产长窗、跨session样本或预算续期。
- 离线验证：复用ignored SDK8.0.424，绿CI源码7742b3c的locked restore、Core/Contracts/MCP Release构建与3028测试（73/2707/248，0失败/跳过）通过；95项工具/参数/resource/指南policy与source及安装MCP64工具/1resource/指南一致性通过。旧ref 74AC…保留；在独立干净detached checkout收集5/5当前安装refs，B84B…及manifest逐份匹配，Plugin Release为0警告/错误。此为当前DLL（fresh菜单29145）的离线编译，不能当跨版本恢复/兼容或最终候选实机通过；准确报告见[本轮离线验证](evidence/2026-10-15/delivery-build-validation.md)。
- 版本边界：default仍严格同版本；旧安装拒绝和11项原生研究保留。用户已对披露后的原问题明确回复“确认这次精确迁移”，fresh v6同候选/新digest唯一commit通过，原票据在Load前持久消费；完整J102及前三转换与R1318原Native逐项相同，第四31→45在tick116215327/J102持久化后正常重存。新票据SHA为EC49DBBD4527CFC87AAE486D04C4C0F817E4BDDA53CE2ABD6DF23145C4DC5F5F，45/min116215357/checkpoint102、匹配当前session/原PID且非quarantine/未过期。只证明本次精确迁移，不扩大其他版本/候选授权。
- 进度保全：精确buildId39cc6803-df11-4d9c-8864-37db49dc8ca9读取为completed/submitted5/5、persistenceHealthy/freshWorldReconciled均true、无blockers，五对象6622/6624/6623/6626/6625逐项保全。完整恢复后6626工厂与R1321静态/拓扑差异0。原summary_only响应不能当损坏；没有清空、手改、归档或重建进度。动态供料、完整三级链与最终包实机门不因此关闭。
- 修复回归：MCP258/258、相关Core271/271及初次失败的局部修复见[候选离线验证](evidence/2026-10-15/owned-primary-29145-candidate-validation.md)。准确07d1727 CI已success；clean archive使用SDK8.0.424完成全solution locked restore、当前5份原生refs完整Release构建（0警告/错误）及win-x64 self-contained MCP publish。228文件、产品SourceRevision、完整工具/参数/resource/playbook、真实安装/新菜单/v6准备已独立核验，见[本次部署](evidence/2026-10-15/owned-primary-29145-menu-deployment.md)。validation-only ZIP不是手动/Thunderstore最终候选，原生Load/实体连续性/最终验收仍缺。
- 双候选已生成：ignored `artifacts/v0.4.0-review-2a7a98d/` 的手动ZIP SHA为665FA1B30A86A4D49AC7E69CE174E08348DCA07D159A24AF766ABED89AFD79CB，Thunderstore为81D9D6ED0BAEC843AB22ADE38158102212A5487A43EFEE22769F500B7CF85D91；各有匹配sidecar。正式入口全solution locked restore/完整Release/当前5refs/两包一次完成，0警告/错误；准确源码CI3108/3108、0失败/跳过。root核全部文件map、9产品版本及两布局4Plugin/guide相同；手动目录MCP与TS实际单文件MCP的完整64工具/参数/resource/guide主harness均通过。TS验证fixture不是交付包；补充exit原件-1与JSON0不一致、不计通过。包内说明/Release notes保持开发态与原限制，详见[候选报告](evidence/2026-10-15/delivery-candidates-2a7a98d.md)。
- 剩余包门：2a7a98d两包未实际安装、运行游戏或恢复；07菜单部署不替代。安装脚本包括PreflightOnly/StageOnly也要求DSP退出，本轮不绕过、不为了离线fixture关闭健康菜单。合法迁移后先完成唯一游戏主线，再把实际适用安装/运行/恢复衔接必要维护边界及最终联合长窗；事实提交引用冻结候选SHA，内容变化才重打包。没有正式tag/Release/Thunderstore发布。
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
