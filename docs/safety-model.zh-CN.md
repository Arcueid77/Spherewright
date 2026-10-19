<a id="safety-model"></a>
# 安全模型

[English](safety-model.md) | [简体中文](safety-model.zh-CN.md)

Spherewright 提供有界的正常游戏能力。具备能力、预览通过、候选本地验收通过与获准执行是不同事实。本文不授予游戏、Save/Load、恢复或迁移、安装、部署、发布权限。游戏工作从当前 [Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab) 检出目录开始，先读取其当前 [AGENTS.md](https://github.com/Arcueid77/Spherewright-Lab/blob/main/AGENTS.md) 与[活动交接](https://github.com/Arcueid77/Spherewright-Lab/blob/main/operations/active-handoff.md)。

当前维护的兼容候选面向 `0.10.35.29151`，见[离线原生证据及待完成实机检查](validation/native-29151-preflight.zh-CN.md)。已验收 0.4.0 候选仍固定于 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`，最新正式版本为 v0.3.3。更新文档不会安装新组件组合，也不会重新发布候选。参见[产品状态](project-status.zh-CN.md)。

<a id="native-execution-and-transport"></a>
## 原生执行与传输

- 所有 DSP/Unity 读写都在 Unity 主线程执行。工作线程只接收自有深拷贝 DTO，原生对象引用不能跨越此边界。
- 本地 Named Pipe 与运行描述文件必须有当前用户 ACL 保护。启动凭据轮换，请求前必须认证，帧大小与队列有界；认证前非法流量会关闭连接。
- `AllowWrites` 默认 `false`。启用它不等于授予目标，也不能绕过 prepare、原生校验或操作预算。
- 游戏执行和验收使用经过认证的结构化状态。禁止 Computer Use、截图作为状态、视觉识别及键鼠宏。
- 禁止发放物品，注入库存、仓储、缓冲或能量，写科技进度，直接完成预建造，传送，更改游戏速度，任意修改存档或绕过安全限制。真实材料、距离、库存与返还空间、能量、科技、建造无人机、电力和游戏时间仍必须支付。

原生配置可以使用经过明确验证的 UI 等效赋值路径，例如分拣器过滤或物流站充电设置。这种窄范围路径不允许任意赋值游戏字段。补充燃料只能转移已有原生燃料，并证明数量与增产点守恒。机队转移仅将已有 `5001`/`5002` 与对应空闲机队计数互转，保留工作中运载器、容量、站内物资、能量、翘曲器、其他机队和玩家库存；绝不创建运载器。

<a id="prepare-commit-and-one-writer"></a>
## Prepare、commit 与单写入者

每个会话只有一个指定游戏写入者。交接前必须停止原执行器，且没有进行中的操作或未对账结果。Pipe 重连、模型回合、上下文压缩或 Host 重启不清零已接受计数，也不允许第二个写入者。

每次修改都必须新 prepare、核对精确计划、幂等 commit 一次，并以终态和读回对账。计划绑定 bridge、会话、revision、星球、精确原生身份、操作类型、参数、目标与资源哈希和预算。计划短期且仅属当前进程；重启不恢复旧计划。

必须使用操作要求的哈希域：玩家操作哈希、建造端点的稳定 `endpointStateHash`、sorter-filter 的根 `configurationStateHash`、storage-capacity 的完整 `stateHash`、物流站专用配置哈希，或研究选择的 `selectionStateHash`。不得重写哈希或替换成方便的另一种哈希。

Commit 在主线程重查前置条件。活动玩家命令不能互相覆盖；活动有限建造排斥竞争的普通操作。幂等域为 `(sessionId, idempotencyKey)`：在配置保留期内，同一请求与 key 跨 Pipe 重连返回原结果，同 key 不同请求冲突。默认每会话 30 分钟、1024 条，不是永久持久重放保证。

<a id="unknown-results-and-reconciliation"></a>
## 未知结果与对账

可能已接受后的超时、丢响应或调用器异常，不证明零写入。冻结新写入，保留原请求、key、action 身份，核对原结果；绝不换 key 猜测重放。后续观察器或就绪断言失败，不会使已成功终态变成失败。

已开始修改若既不能证明修改前，也不能证明预期修改后，写入健康变为 `quarantined`。进程内解除隔离仅限精确保留的未知 build，必须证明成本守恒、唯一归属的新实体与组件及完整有向拓扑，且不重复修改。证据不足不允许清标记、伪造回执、重置会话或加载旧存档隐藏工作。

有限蓝图保留逐对象状态和预写回执。只有新核对身份、配置、材料和拓扑后，才能继续未提交对象。取消保留已提交预建造与实体，不回滚、不退款，也不证明整个模块原子成功。未知对象或结果阻止重放并隔离写入。

原生升级只使用公布的制造台与基础分拣器子集，通过 `DoUpgradeObject` 消耗一整台高阶设备并返还旧设备。必须立即证明货物与增产点、配置、连接、时序和库存。实现不直接写原型 ID；不确定结果不能自动回滚或重试。

<a id="observation-is-bounded-evidence"></a>
## 观察只证明有界事实

缺失、null、unavailable 都是未知，不是零、免费容量、空闲接口或执行许可。宣告不存在前必须读完相关不可变分页。不能将不同 tick 或工厂的数据拼成原子观察。

传送带 `buffers=[]` 不证明无货。详细段货物可能与邻段重复；材料切片对每条完整路径只计一次。研究矩阵点数、燃料能量和电力 J/t 不是可转移物品数。储液罐总量字段与液体缓冲描述同一份库存。

端点、配置和几何哈希有不同范围。动态燃料、货物或输出变化不自动等于拓扑改变；几何预览通过也不证明库存、电力、空闲端点、自动供给、未来真实实体 ID 或持续吞吐。

只读 Foundry、Governor、蓝图、端点与路线预览保持不可执行。无令牌 build 预览必须 `prepared=false`、`commitAllowedNow=false`、`executable=false`。MCP 对缺失或降级回显不放行；调用方不能把旧组件普通令牌当作预览，也不能将预览提交 commit。

<a id="movement-and-finite-recovery"></a>
## 移动与有限恢复

Move 校验有限地表目标，不证明路径无碰撞。有电移动在 180 游戏 tick 内位移不足 0.75 m，或 600 tick 内最佳剩余距离改善不足 1 m 时失败。持续缺电另有 600 tick 宽限。采集接近阶段使用相同有界进度纪律；原地正常产出的采矿不是移动停滞。

只中止自己拥有的精确原生命令。恢复使用新结构化障碍证据、一次有界短逃逸或最多四个正交探针，每次都独立 prepare/commit/终态校验。禁止原目标重放、内部寻路、传送或注入恢复燃料。到达必须证明要求的原生移动状态，不能只靠距离。

`allowPassiveDrift` 仅用于 Move，默认 false。它使用最近可信外部检查，要求闲置、存活、在星球上的 Drift，速度 ≤0.15 m/s，核心容量与既有哈希状态完全不变，在两单调秒内总位移 ≤0.05 m。它不刷新基线；另一次外部玩家读取使绑定失效。原生地表、能量、命令、停滞及干地 Walk 证明仍必须满足。此准入选项与下述运行中有界 Walk/Drift 分支分开。

Voyager 本地 Move/Harvest 复用普通 RTS 并保留无水快路；有限能耗分支允许运行中有界 Walk/Drift，仍拒绝手动输入/惯性、排队订单、待跳跃、导航、快速旅行、曲速及 Fly。精确 owned 绑定与 Native pre-player 钩子继续必需。水平、推力及后台能量覆盖有限工作与13个付费 Native 游戏停止 tick，保留原核心、loaded 返程、原生翘曲器、UTC/tick 和观察租约储备。Move/Harvest 停止上限为13个付费游戏 tick 或独立1秒墙钟，先到为止；UTC 准入留至少1秒清理余量。墙钟上限只是等待期限，不是物理停稳定理，也不折算成60个付费 tick；原 action/journey 期限绝不重开。正常成功要求实际干地 Walk 和真实 Native 零速度，RTS 归零或 Pause 不足。这是在该分支内以有限能耗替代海洋排除门，不是采样或全域干地几何证书；`nativePhysics` 仍为 safetyCertified=false 的只读观察，不选择或授权该分支。

成功远端落地后，同一原 journey 继续守护闲置能耗与原返程时钟，直至已预算的本地动作、受控施工或真实返航接管。失去闲置 Walk、成本或原返程时钟证据时，走精确 owned 绑定的 Pause/quarantine 分支；已成功的飞行或 Move 仍保留成功，后续闲置保护不改写其原结果。 Host 卸载或默认 Save 重载若丢失内存中的 landed-controller 绑定，不得静默重建资格；除非独立确认的固定 LastExit 续接证明新的独立 idle owner，否则只对同一 owned 世界 Pause/quarantine 并拒绝本地/结构化施工。保留 checkpoint 与确认门；普通路径支持同一 Host 内出航、本地作业和正常返航；物流保存恢复续运安排在回母星且 journey 闭合后，远端在途冷恢复尚未验证。

守卫失败、停止/尾返上限到期或调用者/Host 观察丢失时，有限能耗的失败保护仅在原 owned 绑定仍精确匹配时调用正常 GameMain.Pause。当前 LogicFrame 仍可能跑完 player 与无人机的一整个 tick，因此必须支付并保留该整 tick 的储备；只有后续 Native 游戏能耗/派发 tick 冻结。Pause 不证明物理空闲或尾返成功。保留未核销的 outcome_unknown、quarantine 及精确原 action/build/checkpoint，不自动恢复、Load 或重放；绝不暂停替换后的另一世界。Host 真正卸载时先暂停精确 owned 的受保护世界，再拆除钩子；普通 MCP 断线不卸载 DSP 内常驻 Plugin，租约检查继续。暂停不续原墙钟期限。新源码仍须完整 Plugin/回归、准确 SHA CI、Lab 锁/采用、冷部署及实机检查后才计运行信用；不新增公开工具或协议版本。

受控施工仍在固定锚点保持 Walk，并保留独立原生无人机尾部预算；Drift/锚损失停派发并走 Pause/unknown，不声称尾返成功。已知的正常尾返仍须精确工厂后续 Native 轮次、原预发缓存为空及全部 owned 无人机空闲证据。本维护源码适配仍待完整 Plugin/回归、准确 SHA CI、Lab 采用、冷安装与实机资格，历史2/3验收及72工具/1资源接口不变。

<a id="ownership-creation-import-and-journal"></a>
## 所有权、创建、导入与 Journal

工厂、存档读取及普通写入要求精确世界由 Spherewright 创建或明确接管。无关已加载会话只显示不透明受限会话元数据和导入可用性。

创建新游戏走 DSP 官方路径，固定和平、1 倍资源、关闭沙盒。模式证据缺失或非和平世界失败关闭。运行时战斗、沙盒与资源设置保持显式；沙盒和资源倍率本身不单独阻止普通 commit，也不扩大能力。

导入只用于玩家已经加载的非受管世界。Prepare 绑定精确进程、会话、revision、`GameData`，不暴露世界内容。之后必须获得对披露的明确回复，commit 才可声明确认及原存档、Journal 两项边界。原生保存创建新内部副本，证明精确头部 tick 后才接管。公共请求不能接收、枚举、主动加载、覆盖、重命名或删除原存档。保存返回 true 但缺少精确头部证明时，隔离当前已加载会话。

当前用户保护的 Gameplay Journal 使用由存档身份推导的内部 key，并记录声明的跟踪边界、连续序列、持久检查点及显式 pending/error 状态。`attached_existing_save` 保留 `historicalCoverageComplete=false`；缺失早期事件不证明事件从未发生。保留原条目前缀，不能补造时间戳或强制每个操作新增事件。恢复必须在加载前及接管后证明同一原 Journal 与最低持久序号。旧凭据仅沿支持的兼容路径使用，并在之后健康保存时获得更强检查点。

<a id="protected-recovery-and-version-changes"></a>
## 受保护恢复与版本变化

受保护能力不是存档选择器。禁止修改 ticket、有效期、身份、Journal 或源文件使其合格。过旧主存档不能静默丢弃已知更新的接受工作。只使用明确授权的流程和精确当前候选：

| 模式 | 必要边界 |
| --- | --- |
| `default` | 有效受保护凭据；精确版本、身份与 Journal，由内部选择符合条件的主存档或隔离源；不得任意选源或由调用方覆盖 tick |
| `verified_newer_lastexit` | 明确授权的精确更新候选及已知进度下限；健康且含 Journal 的来源；证据 1 与精确身份、tick 回显 |
| `reauthorize_expired_primary` | 过期、未消耗、健康来源；精确记录的主存档 tick、原 Journal、证据 2，以及 prepare 披露与 digest 后的明确回复 |
| `reauthorize_expired_autosave0` | 过期健康来源；仅固定原生 AutoSave0、已批准下限与精确 tick，源、运行时、候选为同一支持版本；证据 3 与之后确认 |
| `reauthorize_fixed_autosave0` | 未变化的有效健康来源；相同固定来源、tick 和版本边界；证据 4 及精确匹配的明确会话授权 |
| `reauthorize_version_changed_primary` | 未变化的有效健康主存档；仅 `29104→29131`（证据 5）或 `29131→29145`（证据 6），精确记录 tick 及之后针对该版本对的确认 |

AutoSave0 模式要求候选严格新于凭据与任何已观察主存档，不能回退到其他自动存档、主存档或 LastExit。重新授权 prepare 不接受预先声明确认，其模式、证据版本、身份、候选、源与目标、prompt/digest 和不允许 commit 的回显必须精确匹配。早期泛化的“继续”不授权不同候选。

过期主存档重新授权允许精确同版本接管，或以下已研究的有向原生迁移：`0.10.34.28529→0.10.35.29057`、`0.10.34.28529→0.10.35.29088`、`0.10.35.29088→0.10.35.29104`、`0.10.35.29104→0.10.35.29131`、`0.10.35.29131→0.10.35.29145`、`0.10.35.29145→0.10.35.29151`。最后一个版本对只加入过期主模式，有效主模式证据 5/6 的范围不变。默认恢复仍要求版本相等，跳链、未知版本对和降级均拒绝。

Journal 起始版本和历史保持不变，最多允许五次已研究的持久转换。每次必须承接有效源版本，接管 tick 与持久序号单调。完整已研究补丁链的各中间环节必须存在。Ticket、主存档与 Journal 有效版本必须与允许的源和运行时版本对一致。

远端隔离的窄续接仍使用 `recoveryMode=default`，不新增工具或恢复模式，普通 healthy/default 行为不变。Fresh prepare 必须返回 `userConfirmationRequired=true`、固定 LastExit 的真实 `candidateGameTick`、`exactEmbeddedIdentityVerified=true` 及 `confirmationDigest`。所有者须在审阅这次精确披露后明确同意；commit 使用既有 `userConfirmedInConversation` 与匹配 digest，早先授权或不同候选均不能满足本次确认。

仅此 `default` 窄提案可追加四个可选扁平参数：`recoveryDeadlineUtc`、固定 `recoveryScope=continue_original_finite_task`、`maximumChargeGameTicks`、`maximumChargeWallSeconds`。充能上限须为正且有界，最多18000 tick/300墙钟秒。Prepared 单列 `originalJourneyDeadlineUtc` 与 `effectiveRecoveryDeadlineUtc`，回显 scope/上限，并声明 `nativeChargeFeasibilityRequiresFreshCapture`，不承诺充能进度。所提有效 UTC 窗口不得超过原声明 UTC 跨度；凭据消费前绑定单一不可变提案、证据及本次新预览后的明确确认，旧同意不涵盖该期限或充能尝试。不得靠重键、更换 Host 或重建续期；原有效期、tick 最大值/已花值、返程预留及能量/翘曲器底线不变，普通 healthy/default 行为不变。

原生加载前绑定固定源与当前主档的完整文件证据、精确同版本 owned/和平身份、原 Journal 及已成功 checkpoint/journey 来源；任何不可变恢复窗口授权须先持久化，再持久消费受保护凭据。成功 checkpoint 只作证据，绝不是加载源，也不伪造旧 `ActionRecord`。精确新 Native GameLoader 在 frame10 已完成且禁用的 `onLoadComplete` 同步回调，须在 Begin/InitLogic 后、首个 player(601) tick 前绑定新 Native logic，证明 owned 身份、原 Journal 与闭合 bootstrap 包络。在实时证明的运行前提下，bootstrap 要求核心至少2P、loaded 能量至少为受保护返程底线加2P；队列、独立扣费及可能无人机派发均须闭合。充能只使用新加载世界中自然运行的 Native GenerateEnergy，绝不 Resume 原隔离世界。该预检不伪称 Native tick 可用，也不发布 idle owner；证明未决禁止自动主档重存及本地/结构化写入。真实 Native pre-player(501) 须逐 tick 守护 bootstrap，只有某次证明原完整 strict13（核心至少13P、loaded 至少为原受保护返程底线加13P，以及原 tick/墙钟/翘曲器门）后，才可准入 idle owner、释放租约并允许重存，紧邻重存前仍须再核严格条件。原返航核心最低值仍是独立 fresh 返航起飞门，不追加为恢复接管条件。可行性须依 P/g 及 fresh 预算判定，不保证自然充能、后续 Move、任务或返程完成，也不降门或续预算。事后 Update 或轮询不能补首 tick 证明。充能在声明的 tick/墙钟任一上限先到时停止，且仍受原五分钟总接管上限约束；进入充能或更换 Host 均不重开。失败只暂停精确 owned 世界并隔离，不证明物理空闲，也不授予自动 Resume、Load 或重放。保留原 UTC 原件、最大/已花/返程预留 tick 及核心/loaded/翘曲器底线；原剩余储备不足则拒绝。

默认恢复绑定受保护 ticket 与菜单 revision。来源选择检查合格内部主存档或固定 LastExit 的头部 tick 和文件修改时间新鲜度，commit 重查 ticket 与来源选择。存在受保护 Journal 检查点时，加载前和接管后都必须验证；既有旧 ticket 兼容性仍限规定路径。接管仍须证明嵌入受管身份、预期星球、和平模式和最低 tick。除上述远端隔离窄续接外，普通默认恢复不计算完整源文件指纹，也不在接管期间持有源文件只读租约。

除远端隔离 default 窄续接外，`verified_newer_lastexit` 和重新授权模式也绑定有界完整文件证据，并在接管期间持有源文件只读租约；固定 AutoSave0 重新授权还租约保护原主存档。重新授权另对原 Journal 打开独立只读租约。Commit 重查对应模式证据，重新授权在原生加载前持久写入尝试与消耗证据。消耗后崩溃需要调查，不能自动重放。原生接管、原 Journal 校验以及所需的持久版本追加完成后，才允许正常主存档重存并发新凭据。持久化失败、证据漂移、不支持的头部元组或身份失败会阻止继续。代码中适用的文件证明上限和接管范围继续生效。

飞行检查点是独立的内部起飞前存档，绑定一个受管飞行、精确 tick 与起终星球。生命周期受限令牌仅能恢复匹配的失败或中断飞行，24 小时过期，并在稳定成功或更新主存档覆盖后退役。不得复活，也不能代替主存档恢复凭据。

<a id="finite-operational-budgets-and-lifecycle"></a>
## 有限操作预算与生命周期

执行前，当前授权声明固定结果、精确范围、保留的成功前缀、写入与请求与时间上限、完成证明及停止条件。调用器限制可能比底层工具更窄，不能因工具存在而扩大。

当前 Lab 流程使用 `AuditWindowLimit` 时，新审计窗口默认 20 次接受写入，可显式选择 10/20/50。既有声明保留原上限和计数，包括十写窗口。所需正常保存必须纳入同一预算。到达上限时冻结 commit，独立对账审计，只能按指定交接重新开启。两次相似原生失败停止该候选家族并重新设计，不能平移后重放。

生产实验保留预先声明的采样、请求、截止时间、失败窗口与缺口。重叠原生窗口或墙上时间不能增加覆盖。Governor 不可变基线、目标、容差及连续 ≥36000 游戏 tick 证明，与资源守恒、来源归属、保存恢复验收分别成立。

Host 关闭、重连、压缩独立于 DSP 生命周期，不得自动保存、退出、重启或重新加载 DSP。仍健康运行的世界在新核对身份、Journal 和操作后继续。明确关闭游戏或已授权的必要冷部署，仍保留正常保存、终态、关闭及受保护恢复门槛。

<a id="privacy-and-validation"></a>
## 隐私与验证

不得发布存档名与文件、ticket、凭据、真实身份、原始请求响应、运行描述文件、本机绝对路径、DLL 或崩溃转储。证据保留于受保护本地，仅发布脱敏结论。公共诊断包使用明确白名单，这不自动使其他任意 DTO 脱敏。

测试、源码审查、原生 prepare 通过、已安装读回和实机持续验收证明不同事实。待检查不算成功。正式 tag、Release、Thunderstore 发布需要独立授权。早期详细证据保留于[已验收安全文档](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/safety-model.md)与 [Lab 历史快照](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7)。
