<a id="spherewright-agent-playbook-opening-and-core-operation"></a>
# Spherewright Agent 指南：开局与核心操作

[English](https://github.com/Arcueid77/Spherewright/blob/main/docs/agent-playbook.md) | [简体中文](agent-playbook.zh-CN.md)

每个会话首次游戏操作前必须阅读本指南。它也是内嵌 MCP 资源 `spherewright://agent/playbooks/opening-movement-v1`，不需要活动 Bridge 即可读取。

本指南描述 DSP `0.10.35.29151` 的 0.5.0 开发接口，保留 0.4 协议接口；逐源码的离线及实机证据见[原生兼容验证](https://github.com/Arcueid77/Spherewright/blob/main/docs/validation/native-29151-preflight.zh-CN.md)。历史 29145 生产验收仍固定于 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`。准确构建身份应读取已安装组件版本与包清单，正式版本见[发行](https://github.com/Arcueid77/Spherewright/releases)。修改本指南不会更新已安装 MCP 或冻结包。能力、当前授权、原生可执行性和最终验收是不同事实。本指南不授予游戏、加载、恢复、安装、部署或发布权限。

<a id="session-entry-and-ownership"></a>
## 会话入口与所有权

1. 读取状态、本资源及新的会话、玩家快照。核对 `ownedBySpherewright`、精确会话、星球、revision、和平证据、`writeHealth`、持久 Journal、已接受和进行中的操作及已安装组件组合。明确指出失败字段，未知不能替换为零。
2. 在闲置菜单只使用授权且公布的受保护恢复，或正常新世界流程。不能猜测、枚举或传入存档名。新世界通过原生流程固定和平、1 倍、非沙盒。
3. 玩家加载世界是受限世界。导入要求精确会话 prepare、披露、之后明确用户回复，再正常保存独立受管副本。`attached_existing_save` 不声明接管前历史。
4. 写入关闭、非受管或不健康时保持只读，直到规定流程成功。原生支持或早期预览通过不等于授权。
5. Host 重启不是游戏重启，遵守[重连规则](#restart-and-interplanetary-flight)。新证据替换旧观察，不清零接受计数或未解决操作。

<a id="active-primary-after-a-researched-version-change"></a>
## 已研究版本变化后的有效主存档

`reauthorize_version_changed_primary` 仅支持未变化、有效、健康的 `29104→29131` 来源与证据 5，或 `29131→29145` 与证据 6。默认恢复要求同版本。Prepare 在闲置菜单使用当前受保护 token，确认 false，不覆盖 tick。要求精确版本对、嵌入身份、候选等于记录 tick、该版本对专用 prompt/digest、要求确认且禁止立即 commit。旧证据 5 的提示不能用于新版本对。

展示披露并获得之后明确回复后才 commit。Commit 在只读租约下重查副本、主存档、原 Journal、菜单 revision，加载前持久消耗，再证明接管及精确第三或第四次转换，之后正常重存、发新凭据。本有效凭据模式仍拒绝未知对、跳链、第五次转换、旧证据和重放。消耗后失败需要对账。

旧版本已完成蓝图历史只有在同身份、完整持久的 `29131→29145→29151` Journal 后缀（或其支持的单跳）得到证明，且所有回执不晚于首次接管、并由加载 tick 覆盖时才能保留。读取不重写文件；下次正常进度更新保留记录并标记支持版本。部分、未知或未证明进度仍拒绝。不能清空进度或重建成功对象。

<a id="expired-planned-restart-credential"></a>
## 过期的计划重启凭据

`restartResumeAvailable` 表示来源存在，不表示未过期授权。不能改有效期、离线续期、换源或导入副本伪造连续性。`reauthorize_expired_primary` 只预览精确健康过期 ticket 绑定的主存档记录 tick 和原持久 Journal。Prepare 不携带确认、tick 覆盖，要求证据 2 及精确模式、身份、候选、源与目标、prompt/digest 回显。

展示候选与提示，等待之后明确回复，再以 `userConfirmedInConversation=true` 和该次新 `confirmationDigest` commit。Digest 是协议回显，不是让用户输入的验证码。`commitAllowedNow=false`、`USER_CONFIRMATION_REQUIRED` 不豁免其他 blocker。计划变化或过期必须重新披露，批准不能搬到变化证据上。

过期主存档仅支持已研究有向对：`0.10.34.28529→0.10.35.29057`、`0.10.34.28529→0.10.35.29088`、`0.10.35.29088→0.10.35.29104`、`0.10.35.29104→0.10.35.29131`、`0.10.35.29131→0.10.35.29145`、`0.10.35.29145→0.10.35.29151`。保留 Journal 起始版本、条目、旧转换，要求有效版本一致，最多五次连续持久转换。第五次转换仅支持本过期主模式，有效主模式证据 5/6 的范围不变。不允许未知对、降级或普通默认跨版本恢复。

Plugin 租约绑定完整主文件和 Journal，加载前持久化尝试与消耗记录。中断、失败消耗会受阻隔离，不自动重试。一次接受后轮询终态，证明受管、已保存、健康的身份、tick、原持久 Journal 和新凭据。离线测试不是实机恢复证据。

<a id="active-credential-explicitly-authorized-fixed-autosave0"></a>
## 有效凭据：明确授权的固定 AutoSave0

`reauthorize_fixed_autosave0` 只使用未变化的有效健康来源、相同受保护副本、原 Journal、已知进度下限及精确授权候选，预览固定原生 AutoSave0。要求证据版本 **4**，源、运行时、候选为同一支持版本，精确嵌入身份，`candidateGameTick=minimumGameTick=expectedRecoveryGameTick`，专用 prompt/digest。主存档仍须匹配身份和 ticket tick；两个完整文件均有指纹、租约。不能迁移或回退。

已有明确会话授权精确匹配披露候选时不用重复询问，泛化“继续”不授权另一个候选。Prepare 仍未确认且只读，commit 在匹配授权下声明并带新 digest；其他 blocker 保留。轮询一个 action，验证正常主重存、Journal、新凭据，对账已有工作，中断或未知时冻结。

<a id="expired-fixed-autosave0-credential"></a>
## 过期的固定 AutoSave0 凭据

`reauthorize_expired_autosave0` 仅使用固定 AutoSave0、过期健康来源、批准下限与精确 tick、未变化原主存档和 Journal。要求 `recoveryEvidenceVersion=3`、prepared 精确身份与候选、相同支持版本、专用 prompt/digest、要求确认且禁止立即 commit。候选必须新于 ticket 和已观察主存档；任意槽、LastExit、主存档作为加载源、迁移和回退均拒绝。

Prepare 不接受确认。展示具体披露并获得之后明确回复，再 commit 一次。持久消耗先于加载，完整文件租约和 Journal 连续性先于正常主保存、新凭据。保留并对账更新接受回执，不能重放。

<a id="commit-discipline"></a>
## Commit 纪律

- 普通操作只 prepare 下一个已批准动作，要求 `prepared=true`、新 token、`commitAllowedNow=true`、无 blocker。只读预览是独立无 token 契约，不能囤令牌或扫候选。
- 重新授权 prepare 有意返回 `commitAllowedNow=false` 与 `USER_CONFIRMATION_REQUIRED`。仅在没有其他 blocker，且精确披露模式要求的明确会话授权已满足时，才可用确认标志和精确 digest commit 同一个仍有效的计划。该模式要求之后明确回复时必须先取得回复；只有规定的有效固定 AutoSave0 模式可使用已有匹配授权。不能等待另一次 prepare 返回 true，也不能伪造 true 预检。确认只满足已披露的确认要求，绝不绕过其他 blocker。
- 直连 Bridge 脚本在同一受保护调用上下文保留 prepare/commit，不能打印、持久化 `planToken` 用于跨进程。普通 MCP prepare/commit 可按协议传递计划。只有证明零接受、零进行中操作，才能丢弃孤儿计划；可能接受就必须对账原 action。
- 使用正确哈希域。研究采用 `selectionStateHash` 的 `expectedSelectionStateHash`，不是通用 progression `stateHash`。Sorter-filter 使用根 `configurationStateHash`，storage-capacity 使用完整 `stateHash`。诊断过期前先检查实际 payload 和哈希来源。
- 现有 Lab 调用器如 `Invoke-SpherewrightNormalAction` 必须同时绑定 commit 的 `sessionId`、`planetId`，不能临时手写新 payload。同阶段两次拒绝 commit 要停止调用器诊断；格式错误、响应丢失不证明拒绝。
- 每次 `spherewright_commit_*` 后，用 `spherewright_get_action_result` 轮询返回的每个原 `actionId`，直到 `terminal=true`。接受 commit 或 Host 超时均不等于完成。同步 commit 没有 `actionId` 时使用其规定终态再新读。核对 terminal/succeeded、身份、真实 ID、材料与增产点、双向边。可能接受后的丢响应、观察错误、格式断言、超时都冻结新写，不能换 key 重放。
- 普通 configure 计划的 `targetObjectId` 可选，返回绑定不是请求哈希回显。保留明确的模式专用检查。分拣器配置返回请求绑定的 `expectedStateHash`；`plannedSorterFilterItemId`、可空 target 不是配置必需字段，配置终态 `targetObjectIds` 可为空。独立验证时复用当前 Core 规范哈希，不编造回显，也不因调用器断言失败重放接受操作。
- 一个游戏写入者持有当前有限声明。交接要求原写入者停止、无尚未终态的操作或未对账结果及明确单写者交接。保留完整受保护意图、回执覆盖，包括未匹配 commit 意图。
- 格式化可选观察前，先保留 `actionId` 及其终态。普通动作回执可按已配置保留窗从观察到完成时起过期；活动、未知/恢复证据及检查点绑定飞行受保护。保留证据达到容量时，在接受前拒绝新的普通 commit，不能驱逐旧证据腾位。缺少旧回执不证明未执行。不能假定全部 DTO 都有 `capturedAtGameTick` 或编造字段；报告/脚本错误不是动作失败，应对账而不重放。保存后新查 `lastOwnedSaveGameTick`、`ownedSaveState`、`writeHealth` 和受保护恢复可用性。
- 执行前固定结果、范围、成功前缀、请求、时间、写预算、完成和停止条件。新 Lab 窗口按配置默认 20，既有十写声明保留上限和计数。所需正常保存在同预算内预留。到限冻结 commit、独立对账审计后明确重开。两次相似原生失败停止候选家族，做结构重设计。
- `wholePlanExecutable=false` 的只读结果不可执行。既有有限授权可覆盖另行批准的必要工作，但每操作仍需原生 prepare 和精确预算；不授权未批准恢复、另一个世界或正式发布。
- 采样保留原游戏 tick 截止、墙时截止及请求上限。之后零产出窗不证明此前无产出。保留失败窗、缺口、调用器时间和原证据，不自动扩预算、重启或追认通过。

直连检查字段是 **objectId，不是 entityId**（正实体、负预建造）；玩家用新 localPlanetId，配置用 entityId，转移用 storageEntityId。仍有 nextCursor 时，前 100 条缺少 ID 不证明消失。先修 schema 错误，不能重放成功建造。

<a id="research-buffers-and-inventory-accounting"></a>
## 研究缓冲与库存对账

`attached_existing_save`、`historicalCoverageComplete=false` 下，缺少首事件不证明从未制造。保留完整旧条目前缀、`durableThroughSequence`、无 pending、无 persistence error。操作不必新增 Journal，不能强求每次 +1。不能重放完成制造或补造跟踪前历史满足断言。

保留 `mechaResearchItemBuffer`、`autoManageResearchItems` 和前后 progression。每物品 **3600 点** 不是额外背包栈。正常管理可退回整件，余点不能向上取整；这不允许接受任意库存增加。数量、触发、操作边界不明就停止新写。

研究完成前采集 `completionItemRewards`（最多 32 条）。它不是交付回执；null 或缺失是未知，空列表是已观察无奖励。保留顺序和重复条目，对账 `unlockTick`、完整库存与增产点、缓冲和容量。交付未知、溢出或多个可能来源使增量未解决。绝不重放完成操作。奖励不是手工或生产，不得创建、补记首次生产 Journal 事件。

所需施工科技排在无关研究之后时，先检查完整原生队列与前置条件。公布支持时，`prepare_select_research(prioritizeQueued=true)` 可经原生队列业务路径，把已排队、前置完成且不在队首的指定科技移至前端。其余顺序、已上传进度和库存保持；暂停、重复或不合格队列拒绝。只 commit 一次，轮询原 action 到终态，保留即时 `researchQueueReadback`，再新读研究及材料要求。重排不完成研究、不供应物品，不能清队列或写科技字段绕过阻塞。

核对运行时物品身份：请求 `itemId`、返回 `itemName`、`get_recipe_catalog` 输入输出必须一致；否则丢弃诊断，不能为错误物料 commit 供给修改。

核对单位：研究站缓冲为点数、每物品 3600，普通库存为 items/1。电力 `power-generation-current-tick` 绝非燃料库存；旧 Plugin 即使错报 items/1 也必须排除。`unitsPerItem=0` 无转换含义，禁止除零。整数有封顶，能量总数用完整宽度电力汇总。

原生生产是滚动 600 tick 窗口，不是区间账本。不能累加重叠窗口，或对有缺口窗口套完整区间不等式。重叠、缺口和库存观察区间分别保留。流入下限低于目标是无法下结论，不是上限失败；零 `P=C` 或满库存都不证明持续供给。修正标准不能追认旧窗，固定目标、容差、缺口清零规则不变。

<a id="rear-pickup-on-mixed-input-belts"></a>
## 混合输入带的尾部拾取

`beltCargo.rearPickup` 观察所选开放尾部的原生对齐包。排序段总量不是队列顺序，无对齐包不是空，unavailable 是未知。新读取双向站输入、needs、容量：原生输入不能跳过不需要的包，storageIndex 是上次接收槽，不是输入过滤器。

以重复包、needs 证据诊断持续阻塞。一个水源不能使共享带变纯。容量增加只是有限排空，必须证明持续源与汇；不能清库存制造吞吐，也不能重放部分路线。

<a id="energy-and-approach"></a>
## 能量与接近

移动、采集、建造、飞行前检查核心、燃料舱、可用燃料和充电路线，使用原生补燃料 prepare/commit 和守恒证明。`acceptedAsMechaFuel` 是原生证据，不靠记忆燃料棒名称；null 是未知。热值不证明范围、燃耗。不能反复探测耗尽核心。已稳定 Walk 就取消恢复意图。有限即时新读、prepare 重试只适用于确定未接受请求，不适用于未知 commit。

原生翘曲器舱为空时，以完整玩家快照的新鲜 `stateHash` 和精确 `count=min(20, fresh 包内1210数量)>0` 调用 `spherewright_prepare_supply_mecha_warpers`，再用原 UUID 调用一次 `spherewright_commit_supply_mecha_warpers`。原生舱须为单个空 `Filtered` 格，filter1210 / stack20，count/inc 为零且无禁用格，玩家须存活、着地且空闲。一次 `Mecha.AutoReplenishWarper` 保持现有 `autoReplenishWarper` 设置，不消耗翘曲器。查询原 action 的 `mechaWarperSupplyReadback`，核背包/舱 count/inc 与设置守恒。after 字段、`actualStored`、`nativeReturnedSuccess` 未证明时保留可空未知，`readbackComplete` 标明即时同步边界。超时、断线或结果未知时只核销原 action，不得再次转移或换键。

`prepare_move` 校验目标，不证明通道；`prepare_harvest` 也不证明接近通道。采集前以有界切平面路径点把观察到的障碍留在玩家身后。

采集接近在原生到达或产出、节点减少前使用相同 **180/600 游戏 tick** 进度窗。原地正常采矿不是停滞；缺电时暂停，恢复后重置。真实终态停滞保留原 action，不能重放、传送或内置寻路。

`prepare_move.surfacePreview` 是最短弧建议证据（最多 64 m、65 样本、130 射线），不是原生寻路或路线净空。Partial、unavailable、缺失是未知，不是干地。通过也不预测控制器或样本间净空。终态后要求实际稳定 Walk、低速和足够能量。

较长或不完整观察的过水，只能由外部选择一次连续原生 Move，目标必须是在当前受管世界重新观察、此前验证的 Walk 落点，带障碍证据、有限过水和恢复储备。建筑存在本身不是 Walk 落点。禁止建立中间水上目标；这不会把 unavailable 变干地证据，也不是未知岸线的通用例外。轮询同 action 至终态并要求 `movementState=Walk`；失败或非 Walk 到达立即停，不重复同目标。

完整观察的短过水可在途中有水。要求 `state=observed`、`unknownSampleCount=0`、`arcLengthMetres<=64`、完整间距 `arcLengthMetres/(samples.Count-1)<=1`，末尾干地后缀至少 4 m。后缀每点需 `groundHit=true`、`waterHit=true`、有限非 null `groundBelowWaterMetres<=-0.1`，且无岸线风险。采用 `arrivalTolerance=1.5`，不要强制 0.5 m；半径归一、三维距离会占用容差。容差要高于径向差并留停止余量，干地后缀还须 ≥`2*arrivalTolerance+1` 米。不能扩大失败操作容差重放。这仍不证明 Walk 或净空。浅水终点、缺样、partial、unavailable 均不合格。对精确审阅目标重 prepare、重核证据、commit 一次、轮询终态。失败或未就绪落点即停，保留 action；不能扩成候选扫描或自动寻路。

这是 Move 接近规则，不是建筑放置门槛。移动预览通过不批准建筑占地。已在当前原生建造范围内就不要额外 Move。全占地原生地面、碰撞、科技、材料仍必查，不能用路径样本覆盖 `NeedGround` 拒绝。检查真实 `plannedPosition` 的电力覆盖。

成功移动后原生减速可能需要有界只读稳定等待，例如最多八次、间隔 250 ms，而非额外或重复 Move。会话 revision 不是接受操作计数。已接受命令要继续轮询，不能竞争发命令。

<a id="stalled-move-recovery"></a>
## Move 停滞恢复

- `position_stalled`、`route_stalled` 终态后不重复同目标。
- 检查附近对象，包括带、分拣器。重合位置也保留不同 ID。用当前目录、检查确认物品，粗半径不是水平行走占地。不完整、过期分页不能排除障碍。逃出后留在障碍簇外，再穿同障碍不算新路线。无独立授权不能拆工作产线。
- 单个可识别障碍：切平面投影远离方向，prepare 一个表面目标，约 **5 m**。
- 多个或未知障碍：两个正交切平面轴，最多四目标 `+u,-u,+v,-v`，约 **4 m**，每方向一次。每次轮询终态，首次成功即停，四次全失败停止搜索。
- 登陆舱属于植被，不保证是工厂实体，保留四向回退。
- 原业务已可 prepare 时执行业务，不继续驱动至中心。

所有恢复复用 `prepare_move`、`commit_move`。禁止传送、输入注入、写位置或无界寻路。

<a id="construction-and-production-lines"></a>
## 建造与产线

安装的 `prepare_build` 公布 `initialSorterFilterItemId` 时，在新普通 2011/2012 分拣器连接混料源**之前**设置它。要求 `plannedSorterFilterItemId` 精确等于请求的非零过滤；缺失或不符不是获准计划，应安装匹配 Plugin/MCP 后新 prepare，不能 commit。零明确表示无过滤。原生预建造在第一次拾取前已携带过滤，完成时验证过滤/标记及两端。后配置不能撤销已送入的污染。旧服务没有该参数时，必须在独立过滤动作完成期间证明源为空或隔离；不能假定能赢过活跃混料带的时序。过滤也不能消除上游其他物品的队首阻塞，应诊断或建设真实专用供应后测下游交付。

合法仓储位置不是完整模块方案。优先完整有限模块原生预检；普通增量施工也须在消耗建筑前声明进出路线、过滤、电力和完整材料预算。新端点一旦可读取，就验证剩余连接。路线失败时先对账、复用已建对象，再提另一仓；不能隐藏对象、清库存或把更多模块当作吞吐。

建造终态成功与无人机空闲是不同事实。Working 计所有存活非空闲无人机。先核对返回对象、材料、连接；下一步需空闲时使用有界读取，例如最多 20 次、间隔 500 ms。仅以新哈希继续未提交部分，不能重放完成路线或模块。拆除后 ID 可复用，必须重查物品、姿态、端点。

不能对每次普通建造或保存都强加所有无人机空闲。已有成功终态读回、玩家静止健康且待建、待修目标为零时，返航中能否 commit 仍由原生校验决定。保留初始空闲边界与独立阶段就绪审计。这不允许操作重叠、未知结果或省略哈希。

`INVENTORY_INSUFFICIENT`、`NotEnoughItem` 后不能库存不变重试。通过正常制造或转移取得材料，按相同显式端点重查完整路径。材料检查可能先于其他失败，不证明放置。旧 Plugin 可能记为位置无效；混合、未知、占用、覆盖错误保留各自诊断。

新分拣器由原生成本、终态、身份、过滤和连接证明建造。`sorterEndpoints` 是未来放置几何，不是运行时序。即使 `sorterEndpoints=unavailable`，也查 `pickTargetObjectId`、`insertTargetObjectId` 和双向连接，不能把成功终态改成失败；也不能豁免原生 `prepare_build` 拒绝。电力和实际货物流另证。

建立 pending-readback，绑定原 action、新成功终态、唯一返回新 ID、计划源与终点。仅允许该证明对象在两端新增预期双向边，保留全部旧边和其他配置。两物理端点槽位均证明后才完成本地步骤。不能宽泛略连接、放行未证 ID，或未计新增边就做零变化断言。后检查异常不授权重放，新对账后只继续未提交后缀。

有条件时使用原生槽位姿态与外向方向；带虚拟姿态不知道物理占用。追踪所有允许物品通过的双向路径，完成相关分页。矩阵用研究站，不是制造台。当前 `buffers` 不观察带货物，空列表是货物未知，不是空带。不能清库存制造需求并声称输出。

详情 `beltCargo` 要求 observed；缺失、unavailable 是未知。带保护区的 512 格段可能与邻段重复，不能相加，也不是整路升级货物保留证明。

`materialInventoryObjectIds` 最多选 256 对象、64 路、总 32768 格、4096 成员，每路最多 8192 格、512 带。每路只计一次，包括选择外成员，不能按选择比例摊分。要求相同会话、星球、tick，无缺失或 unavailable。支持带、仓储、罐、分拣器、制造台、矿机、物流站及普通火力、核聚变燃料状态；订单不是库存，电力缓冲必须排除。电力节点（`power-node`）、其他发电机、研究站不支持。切片不证明流量，不同 tick 不能拼成同 tick 库存。

`tankFluidCount` 显式零证明空，null、缺失是未知，空 `buffers` 本身不是零。液体缓冲和标量是同份库存；单次排空不是持续氢汇证明。

Storage-capacity 绑定完整状态：`set-bans` 禁尾部输入，`lock-occupied` 保留已占格，`filter-empty-or-matching` 保留真空或同物品格，`clear-filters` 清预留而非物品。混合输入开供前先预留容量：filter0 不证明格子为空，lock-occupied 不预留空格。两次成功配置之间的到货可占据预期空间；`storage_configuration_unchanged` 不证明目标预留已存在。新查投影过滤，不能重放相同请求或改用仅配置哈希。公共缓冲不是原生格号，保留精确数量、增产点、拓扑、投影过滤。

若批准有限容量调整，声明移动精确物品数及要恢复的原 bans。Bans 不是全局冻结：TakeItem 向前搜索，全部禁格仍可被普通分拣器取出。转移后重查，若声明恰一保留输出格就证明恰一个匹配投影过滤，保留其他格，仅规划未完成步骤。不能反复移库存或加有限仓储假装持续需求。

已有 sorter-filter 保留持货、运动，只改未来拾取。确认 `componentKind=inserter` 且字段存在的 `filterItemId=null` 是无过滤，请求 `filterItemId=0` 清过滤，两种表示不同。字段缺失、检查失败或非 inserter 仍未知。保留接受操作，绝不重放。改过滤不需要重做建造路径、放置预览。

`storageConfigurationReadback` 是同步原生配置边界，不是之后轮询时刻，不要求跨 tick 库存相等。例如终态转移 `600 -> 580`、之后 581 可能是后续原生流动；保留原转移终态并单独调查，不能作为持续供给基线。转移使用 `storageEntityId`。

不能仅因建筑存在宣告产线完成。证明已解锁配置配方、真实供电、输入运输或消耗、目标非零输出在有界 tick 增加。持久 Journal 可支持但不替代。批准里程碑后正常保存并验证保存 tick、健康。

<a id="power-and-fuel"></a>
## 电力与燃料

`configurationStateHash` 不是通用静态拓扑指纹。保留完整 before、probe、after 快照，对比身份、位置、旋转、电网、连接及其他静态字段。只有已识别发电缓冲的非负整数数量可变化，角色、燃料物品名、单位、增产点仍保留。无法解释的哈希继续阻止计划，不能重写返回哈希，每个实际操作仍新原生 prepare/commit。

`power-generation-current-tick` 不是燃料库存，零不证明燃料空。`isWorking=false` 不权威证明发电机停止，无独立燃料观察时数量未知。原生 `energyCapacity` 是动态观察，不是成员拓扑。独立比较受影响消费者、发电机成员，保留批准容量下限和供电比例及变化；这些值都不证明燃料库存、燃耗、连续性，变化本身不证明原因。Null 不证明断网。

公布时，详情 `inspect_factory_entity.fuelPowerState` 的 bufferedFuelCount 是物品，loadedFuelEnergyJoules 是能量，不是额外燃料。缓冲与已加载 ID 可不同，null、unavailable 是未知，零缓冲不代表无余热。材料切片需切片 tick 的 fuelPowerState。操作哈希不变，容量、当前发电为 J/t；额定计算不是真燃耗或持续分配。禁止为观察而调用修改能量、发电的原生方法。

采用当前原生 `fuelPowerProfile`，不能套记忆额定值。`fuelEnergyUsePerTick * 3600 / fuelHeatValueJoules` 是额定基础消耗，不是实发电、燃料库存或持续供给。这些火力、核聚变额定值不能给未建或未供燃料发电机信用。没有有效负载时增加发电机不会成倍增加燃耗，先声明有用需求与独立测量窗。

预算既有完整基础负载加全部剩余计划需求，包含闲置机器、充电、外送和新覆盖设备。不能等有利容量尖峰或忽略缺口。Power `energyCapacity` 是当前 tick 可用发电，不是不可变装机拓扑。保留解释变化，实际发电、容量、燃料分别成立。

Foundry 机器电力采用吸附 `site.machines[].position`，不是 `site.origin`，也不是另一设备的精确位置探针。不能把正面或负面结果跨偏移套用。检查真实 `plannedPosition`、明确覆盖余量及真实计划消费者，不能用代理机器替代；完成后核对消费者真实网络、供电。几何缺失是未知，不允许建造。

纯节点 null 详情不证明无网，读取完整汇总和成员。规划风机时两两至少 10.5 m，检查所有计划对。原生放置、电力门槛保留，不授权移动、重建成功对象。

有限额外仓储不证明持续供给，低于声明需求限流也不证明目标。保留失败窗、采样缺口。瞬时电力或燃料库存不等于长窗验收。

发电单位单独用 J/t。isWorking=false 不权威证明停机。纯节点可无网络详情，新塔可能合网改 ID；先对账完整汇总、实际成员数、覆盖、拓扑。缺证据或无法解释变化仍须停止。

<a id="bounded-blueprints-and-upgrades-development-subset"></a>
## 有限蓝图与升级（开发子集）

当前服务公布 `spherewright_get_foundry_plan` 时，用它检查目标速率、共用上游需求及机器规模。读取 `phase`、`executable`、`remainingChecks`：材料草案不批准选址，也不是可执行施工计划。电力/成本排除运输与供电设施，有限库存不证明持续外供。`planHash` 不能作写 token，材料哈希未变化也不能跳过新 prepare。

工具公布可选 `site` 时，只选一个明确地表原点和有界网格（最多 32 台机器），读取全部吸附位置、原生放置条件、计划内重叠及全案库存缺口。`machine_previews_clear` 只是一 tick 的机器评估，不是已有物流供电的工厂。`assessmentHash` 不能当状态哈希或 commit token。玩家介入、重启后重新评估，每次普通施工新 prepare；不能囤 token 或无界轮询替代网格。

Foundry 或蓝图预检返回 `site.power` 时，读取覆盖、分量预算及全部 blocker。它保留现有完整工作/待机/当前基础需求、充电、新覆盖但无电的消费者及外送，并计计划负载；新增发电仅给原生风机信用。`unavailable` 或边界/歧义是未知，不是剩余电力。独立捕获哈希及 `base_load_budget_ready_at_capture` 仅供判断，不是施工 token 或持续燃料/输出证明；仍须原生检查及建成对象的实际电力读回。

`get_blueprint_builds` 不给 buildId 时仅返回简短汇总。续建前指定一个精确 buildId，取得新世界对账与 stateHash。普通保存/重启丢失旧 plan token；使用 `prepare_blueprint_build(resumeBuildId=...)` 和新进度/玩家哈希，不替换代码或选址。完成对象绝不重提；实体、连接、回执变化或缺失，游戏 tick 过旧或提交结果未知均阻塞续建。不能重放整张蓝图，也不能从外观相似推断安全。

蓝图检查、导出仅为数据，`executable=false`。导出最多 64 个、64 m 内明确完成对象；检查限代码 256 KiB、压缩 128 KiB、负载 1 MiB、64 对象、8 区域。未知内容、设置拒绝，元数据不可信。复制 storageConfiguration，不复制货物。检查 `inputEndpointFacingDot/outputEndpointFacingDot`，负值表示背向，不允许绕过 `TooSkew`。

可选选址最多 32 显式对象、闭合内部端、无环依赖，检查完整原生成本、解锁、范围、地形、碰撞、电力。原生 `SnapTropic` 可调整跨回归线网格，返回逐对象姿态需重新批准，对齐不豁免检查。不能重放完成对象，也不能连续循环附近锚点应对重复拒绝。

有限 `prepare_blueprint_build` 绑定完全相同的代码、选址、评估或精确 `resumeBuildId`、新进度、新玩家哈希，以及显式 1..32 的 `maximumObjectsToSubmit`。使用一个 UUID 键及时 commit，轮询原 `actionId` 至终态，保留 `buildId` 和逐对象回执。每 tick 最多提交一个依赖已就绪对象，使用正常材料、无人机。批次暂停不是全完成；只续建未完成部分，绝不能重放整张蓝图。取消只停未来提交。受保护预写进度在恢复前对账身份、材料、配置、双向和自由端拓扑；重启不恢复 token，也不允许重提完成对象。未知隔离写入。

Foundry 材料读取不可执行。机器网格通过不含物流、电力设施和持续供应。显式蓝图组合绑定原 `foundryIntent`、其带物品/方向/对象索引/原生槽的 `boundaryPorts`、精确阶段数、全成本依赖、逐物品流和自由边界。读取 `construction.canPrepare`、全部 blocker、`blueprintSite.power` 中的独立原生电力及额定 `transportBudget`。`transportCapacityVerified` 仍 false，阻塞分配可低于需求。基础 2011/2012 跨度额定不是实测吞吐，堆叠 2013 阻塞此组合。将相同代码、选址、`blueprintSite.assessmentHash`、意图，以及作为 `expectedFoundryPlanHash` 的 `construction.planHash` 用于既有有限 prepare/commit；完整图、成本、电力、运输规则重新检查。恢复只提供 `resumeBuildId` 与新的进度/玩家哈希，不能替换代码、选址或意图，剩余电力不重复计完成对象。

升级仅公布的制造台更高档或普通 2011→2012，需一整台目标设备和返还空间。分拣器两条真实连接必须双向，不能只看缓存目标。原生 STT 不是游戏 tick：`max(10000,inserterSttRaw*span)`；基础理想周期 `max(4,ceil(2*STT/10000))` 是额定，不是实测。制造台进度重置，分拣器比例缩放。保留即时 `upgradeReadback` 和物品 delta，新读取实际结果 ID、产线、电力、流量。不能重放成功建造修调用器断言。

分拣器时序按档位区分：`inserter` 的 `progressRequired` 是原生 STT，**不是已过游戏 tick**。与同物品目录的 `inserterSttRaw`、`inserterGrade` 对照；支持跨度 1–3 的原生 STT 为 `max(10000, inserterSttRaw * span)`。已验证目录中，普通 2011 基础值 200000、跨度 3 得 600000；高速 2012 基础值 100000、跨度 3 得 300000。不能把高速枚举套普通分拣器，也不能仅以此值诊断停滞。基本单物品档满电理想周期为 `max(4, ceil(2 * STT / 10000))` 游戏 tick（普通跨度 3 为 120），不是实测吞吐。缺料、输出阻塞、电力均可降低实际速率；仍须核对原生终态、材料、精确双向端点、过滤、实际功率和流量。调用器后检在原生成功后失败时，保留原 action 并唯一读回完成对象；**绝不能重放成功建造**来修复断言。

拆除仅公布子集。空默认 2101 要全部 30 原生格为空、默认，无堆叠、连接、预建造、缓存引用，不自动重建。空 2001 链头最多 16 带、512 格，完整空独立路且无外部引用。先正常断开外部分拣器，不能移中段或相连尾。原生返还、幸存对象证明仍必需。

<a id="governor-measurement-and-acceptance"></a>
## Governor 测量与验收

只读 `spherewright_get_governor_plan` 对 1–32 个明确的新读来源对象比较实测需求、缺口和成本排除项。星球速率要求相关生产者完整归属。采样前选定周期：默认 600 或 3600、原生六 tick 对齐计数；要求匹配回显及三个不同、独立且非零的窗口。验收要求稳定非零实测基线、预声明目标/容差及至少十游戏分钟（≥36000 合格游戏 tick）；仅返回提案不验收该长窗。源、会话、观察物品范围、周期变化清历史。

仅规划目标变化可请求实测两倍。锁前只重新定价目标速率保留不变来源历史，但须重编译、新核对新 proposalHash，在任何扩建写入前锁定。基线不稳定仍不稳定，不扩大容差或重试至好看样本。已锁目标、容差、规模不可改，原目标、容差、规模保持不可变。

写入前要求持久服务端 `validationBaselineProposalHash`，同源、同 revision、3600 tick 内。禁止客户历史速率或编辑归档。

Governor 公布可选 `parallelExpansionBlueprint` 时，提供一个明确蓝图代码、选址与自由边界端口，以比较新增产线。仅就绪非零基线可比较：速率为**目标减实测基线**，绝不是目标减缺料瞬时速率。读取 `parallelExpansion.intent`、完整 Foundry `plan`、所有施工/电力/运输 blocker 及各方案 `costScope`。全部显式模块对象均计成本；未规划外部供给/输出连接及额外发电仍排除在成本外，并非免费。就绪比较仍只读：扩建前锁 Governor 声明，再用相同代码、选址、返回意图与施工哈希走既有新鲜 `prepare_blueprint_build`。部分完成、取消、重启保留同一有限 buildId，不重放模块。扩建后吞吐观察省略可选布局，保留锁定声明哈希，新读完整扩建来源；重启清空全部观察信用，持久锁定声明只能经覆盖它的受保护计划恢复来恢复。

执行、采样保留同一锁。只有健康计划受保护恢复、相同受管身份、版本、Journal、原 ticket 下限覆盖锁才可恢复。重启总将连续观察清零；`durable=false` 描述采样，不否定持久声明。存储缺失、损坏或锁前证据是阻塞，不允许扩建后重新锁。

计锁定原生窗口连续并集，重复、墙时不加信用。缺口、无效速率、源变更、采样健康未知清进度。Governor throughputValidation.resetReason 是上次重置历史，不是当前失败标志。一起读当前速率、区间、容差、归属、健康、连续 tick；重复文本不是不同独立失败窗口。采样缺口不能恢复旧信用。

`targetChainFindings` 具备目标诊断、所选对象或上游路径证据；`unattributedPlanetFindingCount` 保留其他星球发现，不能假装它们已解释选定产线，也不能丢弃警告或声称矿已修好。目标链发现及真实供给速率缺口仍须诊断。`currentWindow` 是生产区间，所选缓冲变化有独立库存观察区间，不能改称生产窗。600 tick 计数每物品对应每分钟 6，3600 tick 对应每分钟 1。诊断仍按原 600 tick 保持新鲜，实测电力、目标链发现、归属与写健康仍是验收门。

长周期改善分辨率，不允许放松 10%、改进行中实验或重解释失败。连续 ≥36000 合格 tick，加要求时精确两倍、≤10%，仅为吞吐证据，不是整体平衡或逐 tick 健康。保留失败窗，源守恒、自动供给、真实运输、保存恢复另证。

`output_blocked` 的 `output_buffer_capacity` 是下一批准入阈值，不是物理仓储容量：`101-q`、`9*q+1`、`19*q+1`。短窗例外要求停止、满电；速率、电力、容量未知不合格，其他情形仍需完整周期。无诊断不证明持续供应，不能清库存抹诊断。

`stocked_logistics_boundary` 的 `configured_route_supply_total` 不是可用未预留库存，`logistics_dispatch_state=unproven` 不证明派送或原因。改物流、扩矿前检查原生订单、机队、路线进展。

读取另一星系中已知受管物流站时，复用 `spherewright_inspect_factory_entity`，传 `ownedRemoteLogisticsStation=true`、精确正数 `objectId` 及已存在驻留工厂的星球。不自动选择、不加载或创建工厂、不读取远端几何/材料切片，也不因此取得远端写入权。须核匹配的物流站详情回显；缺失或旧 Plugin 覆盖为未知。所有站点 prepare/commit 仍要求玩家实际本地星球及精确距离。

用有界 `logisticsStation.activeVessels`（`coverage=native_active_work_ship_data`，最多32条）保留原有限窗运输观察，区分完整、截断与未知。读取真实 `shipIndex`，不能用会压缩的数组索引冒充。`ownerGId/shipIndex/gene` 可重复，不是跨保存/恢复的永久航次 ID；`direction` 是离开/返回归属站，`t` 是阶段参数，不是 ETA。不公开运输船坐标或私有 `StateToken`。船移动、货物归零、一次到货或配置正确均不能关闭持续路线门。事前声明数量/速率、库存/能量/翘曲器趋势及覆盖多个原生完整运输周期的窗口；证明真实采矿、双边交货、母星实际消费和正常保存恢复后继续运输。运输船货物、订单不能加入物流站库存。

现有物流站机队 prepare/commit 也支持原生翘曲器 `itemId=1210`。须核新鲜的原生运输船曲速科技、真实 `warperCount`/容量、背包零增产点物品、完整玩家/机队哈希、本地距离及双边数量/增产点守恒。这是正常计数器转移，不注入物品，不新增远端写工具；转移结果未知只核原 action/key。

<a id="native-free-ground-belt-route-choice"></a>
### 原生自由地面传送带选择

默认 `native_grid` 适合网格对齐直线末段。显式 `beltPathMode=native_geodesic` 选择一个原生地面路径，不是自动寻路。要求 `plannedBeltPath.routingMode` 和完整原生路径检查，不修改玩家命令。本身不启用已有带覆盖，不证明两个分拣器连接。不能省端点 ID 绕 `belt_path_existing_overlap`，原生覆盖复用不自动修既有世界。

DSP **经度网格间距随纬度变化**。固定赤道行列公式不是权威 `native_grid` 路径；原生吸附、转折、去重会改变点位及数量。将有界纸面候选与完整新鲜 `plannedPath` 和 NEW 预算对照；不匹配必须拒绝，不能忽略意外点。占用拒绝不证明完整原生放置检查已执行。`OutOfReach` 需要单独检查的短距离接近和新建造 prepare，不能扩大距离或重用 token。即使 `shoreRisk=not_detected` 仍可含浅水样本；选择步行目标前核实际地面/水面证据。合法带路或纸面三格间隙均不证明最终分拣器连接。

**自由端点在路径生成前先吸附，包括 `native_geodesic`。** 请求坐标的无碰撞球面插值不是原生路径。纸面占用、点数、材料、连接估算前先考虑两端吸附，再核完整新鲜 `plannedPath` 与预算。被拒 prepare 没有返回路径时，不能编造吸附坐标或假定报错点索引对应纸面点。退役被拒候选；不能重试未变化目标、移除端点绑定或改已有产线绕过占用。

仅源覆盖的原生 2001–2003 子集要求同档、零倾角、自由输出、开放路径、最多一条输入带、新 ID 与哈希、唯一中心、有界旧货物证明及至少两 NEW。零成本、不移除锚点留在原生检查、创建中。此仅源路径拒绝终点覆盖、占用输入合并、替换或升级锚点、高架及闭路。创建须同帧保留完整旧货物、路径，只允许原自由输出变为与新预建造双向边。地面 2001 空终点复用另需不同完整空开路、自由输入、精确 `destinationBindingMode=non_removing_belt_cover` 和 `empty_open_path_native_geometry_v1` 回显；两完整旧路、所有旧边仍绑定。无人机完成后有序成员必须恰为源 + NEW + 终点，无额外实体或旧边丢失。唯一带供源例外是单独证明无电空钛矿机，不允许泛化带供给、带货拼接。源保留要求整路原生旋转和碰撞体证明，不是任意四元数容忍。证明未知隔离写入，绝不允许重放。

花费路线预算前检查源、桥、消费端。合法带不证明分拣器，球面对角虚拟朝向可能拒绝网格设备，优先直线末段。不要求建造前整链通过时，在最小批准接口测试前缀后先 prepare 关键分拣器，再建剩余路线。该前缀不是整路通过，预测基方向不是原生批准。

分拣器米距离、原生格跨度独立：带与设备 5.5 m、3.499 格；带与带 5 m、3.2 格。`TooFar` 不是 `OutOfReach`。加中继前检查明确已有内部带段、方向、货物、物理空槽、全部旧双向边。几何不是原生放置批准。保留整条成功路线，每新端点对只 prepare 一次，拒绝即停；不任意搜邻居、自动改路或重放。

精确、回退诊断分别保留 `nativeChecks`（真正调用 DSP 放置验证数）、`lastNativeRejection`、`lastPreNativeRejection`。零次表示放置未知，旧 Plugin 可能只报最后精确槽位错。候选数不是原生放置次数。读取 `candidate_validation_rejected`、`bestFacingDegrees=unknown` 等阶段原因。不能重放不变端点对、放宽门槛。回退不含火力电站，旧版本还拒绝双带插值，新版本可公布 `native_two_belt_segments`，要求两几何绑定、最多 64 原生路径区间，闭路拒绝。原生 quarter-turn、两次曲线投影前折叠 <40° 种子门槛不能由后续拟合救回。`TooClose` 是最小格跨度，不是玩家距离。支持不保证任意一对可连，几何变化需新 prepare。

<a id="frozen-geometry-and-whole-route-read-only-qualification"></a>
### 冻结几何与整路只读资格

使用 `inspect_factory_entity.geometry` 冻结最多 256 个明确有符号对象/64 地形点，绑定完整 `factoryGeometryHash`，包含预建造及扩张包围。建成带要求 `native_runtime_belt_collider_world_bounds`。可选 `geometry.nativeBeltSegments` 接收最多 16 个明确自由端到自由端的原生网格/高架跨度，长 1.5–30m、显式层级 0..3，每段最多 64 点/共 1024 点。要求完整有序端点/段回显、`read_only_native_belt_segment_points_v1`、`nativeConditionsChecked=false` 与 `executable=false`。这些无 token 读取不检查占用或原生放置；生成失败不能返回部分点，旧组件缺少回显也不允许用估算测地线替代。

端点预览绑定一 NEW2001 跨度、最多两真实端点、普通 2011/2012、过滤、quarter-turn。`plannedBeltQuarterTurns` 是向外槽位姿态：source 朝已有源反向，destination 朝已有终点。适配器已将终点转 180°，不能再转一次。预建造与完成带参考系不同，不能复制 quarter-turn 数；仍必须新查原生角度、跨度、占用、材料。

整路 `beltRoutePreview` 绑定两真实端点、冻结几何、最多 12 显式段、240 m 弦长总、192 NEW，要求完整 stage1、全部带与两分拣器联合原生检查及完整请求、哈希、预算回显。两预览必须 `prepared=false`、`commitAllowedNow=false`、`executable=false`、精确玩家哈希、无 token。不能交 commit 或普通执行器，旧组件缺回显为不可用，不回退建造。

区分原生前 guard、真实原生拒绝、通过。响应成功但 performed=false 或有 blocker 不是原生批准。通过仅证明采集时有界几何、材料、原生条件，不证明未来真实 ID、电力、持续供给或整计划许可。遵守固定候选、预览、请求预算，退役失败家族，不为每候选做审计读取循环。

<a id="one-explicit-elevated-native-belt-span"></a>
### 单个明确高架原生跨度

`beltPathMode=native_elevated_grid` 允许普通 2001、显式 `beltStartAltitudeLevel`、`beltEndAltitudeLevel` 0..3 且一端升高、每层 1.3333333 m、4..64 完整点、平坦两端、坡度 ≤0.5。边界为原生吸附端点 1.5–30 m 三维直线距离，不是网格路线折线总长。保留独立 64 点、每段、高度及全原生检查，不抬升、裁剪、任意寻路或静默拍平。

唯一高架绑定源为精确空独立高架 2001 尾，带新 ID、哈希、起点、自由终点、完整旧路证明及保留零成本覆盖；3..63 NEW 仍查占用、成本。不允许有货、供给源或终点复用；下一段须用真实终态读回 ID，不能预测拼接。

本地调用器断言不是原生拒绝。再 prepare 前独立核对原精确字段；成功不允许执行未批准整链。合法坡道不证明两个分拣器。仅轮询原操作至终态，保留成功坡道，只为独立授权后续新 prepare。

## 原生玩家物理读回

研究移动边界前，可用 `spherewright_get_player_state` 中可选的 `nativePhysics` 检查实际引擎及物理设置。保留采集 tick，缺失值保持未知；即使所有设置均已观察，`safetyCertified=false` 也不变。步长、脱离穿透设置或采样干地点都不能证明完整停止区域。此只读快照不授予执行权限：继续保留已安装移动守卫、原航程储备、检查点及 fresh prepare/commit 门。

<a id="voyager-visibility-and-route-previews"></a>
## Voyager 可见信息与航线预览

已安装 0.5 组件提供 `spherewright_get_voyager_star_catalog` 和 `spherewright_preview_voyager_route` 时，使用 fresh 受管 session/出发地观察及原生可见目标。这些是只读、无 token、`executable=false` 的观察，不起飞、曲速、耗料、保存、创建 checkpoint 或游戏 action。目录只对许可视图请求正常原生星图扫描，可能生成合法可见扫描缓存；扫描未就绪仍保持未知，不调用任意生成器或创建远端工厂。航线预览不开放跨星 commit，也不证明安全落地或原生验收。历史 0.4 同星系阶段范围不禁止 Voyager 开发；所有原生执行与确认门继续保留。

目录 `limit` 限定 1–32 颗恒星。航线 `maximumFlightGameTicks` 是整个往返，默认 36000，范围 7200–108000；`returnReserveGameTicks` 默认从总窗内预留 18000，去返程各至少 3600 tick。`minimumCoreEnergyRatio` 须为 0.8–1.0 有限值；`extraWarperReserve` 在原生往返两枚之外额外预留 0–18 枚。保留 fresh `expectedPlayerStateHash`/`expectedCatalogStateHash`、明确米/光年与焦耳/瓦单位、独立预算检查及 `blockers`。即使 `budgetSufficient=true`，仍保持 `prepared=false`、`executable=false`、`commitAllowedNow=false` 且无 `planToken`；MCP 拒绝相反执行标志。

已观察恒星标记可使用 `destinationPlanetId=0` 和明确 `destinationStarId`，行星/资源仍保持未知，不猜隐藏行星。`maximumWarpGameTicksPerLeg` 默认1800，至少180且不超过任一单程窗口；`maximumArrivalOffsetMeters` 为100000–3000000有限值，默认3000000，仅恒星预算包含该偏移。v2 的 `budgetBasis=finite_native_warp_duration_and_explicit_return_recharge_v2` 将有限曲速能量与总窗口分开。`requiresReturnCoreRecharge=true` 要求返程前 fresh 读回核心达到 `returnCoreEnergyMinimumJoules`，同时保留已装燃料/翘曲器储备并安排真实正常充能；单程缓冲不证明之后能充电或落地。

在远端用同一非空受保护 `journeyId` 预览回家航线，要求 `isReturnLeg=true` 且朝向原出发地。预算只从原储备核剩余返程单腿：一次原生进入曲速加 `extraWarperReserve`，不能再要求一轮母星至远端的新往返。返程 prepare 同样保留原 `journeyId`；不传 ID 表示去程的往返预览。

资源可见性与预算的未知必须保持未知。未勘探不等于缺失，主题潜在资源不是已勘探矿脉；缺失资源验收要求合法完整的出发星系探索证据，不得要求抵达或正常探测前读取隐藏目的地矿脉。核对原生单位、距离/科技/核心/燃料、真实翘曲器消耗、去返程储备、落地、补给与撤回条件。`budgetComputed=false`、阻塞或不可用预算不代表零成本，也不许可消耗受保护返程储备，须保留 `budgetBasis`；目录/玩家绑定过期应刷新，不能把预览作为 plan token 复用。

`resourceSummaryScope=remaining_veins_and_native_gas_oceans` 表示剩余矿脉及合法可见的原生气体/海洋来源。某矿脉行归零不等于全系物品缺失；须完整覆盖所有相关行星和来源类型，该星系不能有未知、不完整或截断覆盖。无关恒星条目截断不使完整星系证据失效。`expectedPlayerStateHash` 使用完整玩家快照 `stateHash`，不是更窄的玩家 action 哈希。

预览成功不证明 checkpoint 可用或可恢复。取消、超时或断线后核销原 action，未知 commit 不得换键重放。只有精确且符合条件的失败/中断航程可使用其公布 checkpoint；成功或 primary 覆盖进度即退役，较新主档进度不得回滚。原生飞行扩展和安全可控失败/恢复演示仍是独立验收门；普通往返与离线检查不能单独关闭故障恢复门。

Flight ticket v2 复用现有受保护 `OwnedSaveRecoveryLease`，绑定 `CheckpointFileFingerprint`、精确内嵌 owned 身份、原生版本、和平模式及保存 tick。Prepare 和 commit fresh 重验完整文件证据，tick/mtime 相同也不能放行已变化内容。Commit 持只读 lease 贯穿原生异步认领，最多五分钟，成功、失败、取消或 dispose 均释放。拒绝缺少强文件证据的旧 v1 flight 票据，不得编辑或提升为新恢复权限；protected-primary 的确认及迁移要求保持不变。

Reload 在原生 dispatch 前登记原 action/key，持久记录 `ReloadPending` / `ReloadActionId`。进入原生加载后抛异常时，原 accepted action 保持 `outcome_unknown` 并保留 source lease/expectation；只查询原 action，不得换键或重放。Pending 阻止新 reload 及普通写入；成功须证明精确认领发生在不同于 dispatch 来源的新 session，旧 checkpoint 标志不能证明成功。核销后票据成为 `recovered`，保留原完成回执，再次 reload 须有新一次绑定航程失败。

<a id="bounded-voyager-execution-and-stop"></a>
## 有限 Voyager 执行与停止

已安装组件公布对应能力时，以精确已审阅 `expectedRouteStateHash`、完整玩家快照的新鲜 `expectedPlayerStateHash` 及受管出发地调用 `spherewright_prepare_voyager_flight`。空 `journeyId` 开始有限去程，返程使用同一受保护 `journeyId`。检查原生储备/阻塞读回后，以 fresh 计划和原 UUID 调用一次 `spherewright_commit_voyager_flight`。它复用现有 flight/checkpoint 协调器；普通同星飞行保留原守卫及独立提交模式绑定，不能使用 Voyager 计划。

prepare/commit 前要求 fresh 前进的原生 tick，并以短于 30 秒控制租约的间隔查询绑定的原 action。调用者过期或原生 tick 中断会锁定停止请求；须在原定 1,800 tick／30 秒清理上限内核实真实退出曲速与制动。暂停不延长墙钟期限。同进程恢复计入加载前捕获的 source tick 之前的耗时；真实进程退出后若 source tick 未知，只能精确受保护恢复，并保守核销原航程全部剩余时间，不获得新返程预算。保留原绝对期限、物料底线与保守计账标记。

查询同一 action 的 `voyagerFlightReadback`，核真实 `phase`、可空恒星/行星身份、能量、舱内/包内翘曲器、`nativeExitObserved`、`flightDeadlineGameTick` 和绝对 `journeyDeadlineUtc`。太空过渡中根 action 的 `planetId` 保留原出发地。跨 action/session 保留返程储备与航程期限；Host 中断不增加时间或库存。到达恒星标记范围不等于稳定落地，单程不等于完整 Voyager 验收。

有限停止使用 `spherewright_prepare_cancel_voyager_flight` 绑定原飞行 `actionId` 与出发地 `planetId`，`spherewright_commit_cancel_voyager_flight` 在同一 writer 锁存 `stopRequested`。持续查询原航程，核原生退出曲速、刹停及真实终态；停止回执不证明恢复完成。未知结果保留原 action/key；checkpoint reload 仍为独立受保护操作，不得回滚较新主档。安全可控的原生失败与精确符合条件的恢复仍是独立实机验收门。

<a id="voyager-local-work-and-structured-construction"></a>
## Voyager 本地作业与结构化施工

所有远端动作保留在原 `remote_landed` 航程及精确 owned 存档/Native/world 绑定内；保留原返程 UTC/tick 窗、loaded 返程能量底线与原生 `1+extraWarperReserve`。返程核心充能在起飞前重验，不是每个本地动作都要满核心。原生站点配置/转移仍要求玩家实际本地、精确站点及 fresh 哈希；owned-remote 站点只读观察不授权在母星写远端。

Move/Harvest 复用既有普通 RTS 动作并保留无水快路。有限能耗分支允许运行中有界 Walk/Drift，要求 fresh Native pre-player 钩子及精确 owned world/player/controller/mecha/本地工厂绑定；排队订单、手动输入/惯性、待跳跃、导航、快速旅行、曲速和 Fly 仍不支持。原生水平、推力及后台成本须覆盖有限工作窗与13个付费 Native 游戏停止 tick，含采矿与 RTS 减速同 tick 发生，且不侵入原核心/loaded 返程底线、原生翘曲器或返程时间。Move/Harvest 的停止上限为这13个付费游戏 tick 或独立1秒墙钟，先到为止；UTC 准入另留至少1秒清理余量。墙钟上限只是等待期限，不是物理停稳定理，也不能折成60个付费 tick；任一上限均不重开原 action 或 journey 期限。本地 UTC/tick 期限及30秒原 action 租约的范围保持不变。此分支仅在有限能耗资格通过时替代原整球海洋排除门，不授予路线净空或连续干地区域证书。正常成功仍须实际干地 Walk 与真实 Native 零速度读回；RTS 归零、abort 订单或游戏暂停均不足。

成功远端落地后，同一原 journey 继续守护闲置能耗与原返程时钟，直至已预算的本地动作、受控施工或真实返航接管。失去闲置 Walk、成本或原返程时钟证据时，走精确 owned 绑定的 Pause/quarantine 分支；已成功的飞行或 Move 仍保留成功，后续闲置保护不改写其原结果。 Host 卸载或默认 Save 重载若丢失内存中的 landed-controller 绑定，不得静默重建资格；只对同一 owned 世界 Pause/quarantine，并拒绝本地/结构化施工，保留 checkpoint 与确认门。本子集支持同一 Host 内出航、本地作业和正常返航；物流保存恢复续运安排在回母星且 journey 闭合后，远端在途冷恢复尚未验证。

守卫失败、停止/尾返上限到期或调用者/Host 观察丢失时，有限能耗的失败保护仅在原 owned 绑定仍精确匹配时调用正常 GameMain.Pause。当前 LogicFrame 仍可能跑完 player 与无人机的一整个 tick，因此必须支付并保留该整 tick 的储备；只有后续 Native 游戏能耗/派发 tick 冻结。Pause 不证明物理空闲或尾返成功。保留未核销的 outcome_unknown、quarantine 及精确原 action/build/checkpoint，不自动恢复、Load 或重放；绝不暂停替换后的另一世界。Host 真正卸载时先暂停精确 owned 的受保护世界，再拆除钩子；普通 MCP 断线不卸载 DSP 内常驻 Plugin，租约检查继续。暂停不续原墙钟期限。新源码仍须完整 Plugin/回归、准确 SHA CI、Lab 锁/采用、冷部署及实机检查后才计运行信用；不新增公开工具或协议版本。

首版本地控制子集还要求独立原生战斗扣费为零：护盾容量0或补能原已关闭、激光未启用、旧弹仓0、有效空弹药选择且整仓 item/count/inc/filter0及真实 ROF/费用0，地面/太空模块均为**零配置机队槽位**。这不是活动船只数；默认空弹药槽1可以通过。不能为强行通过修改玩家设置，应报告具体不支持条件。复制器/科研队列须空闲，剩余科研物品 buffer 可以保留。

需要矿机、站点、电力杆、地面带或分拣器时，只用既有原生 `prepare_build`/`commit_build`，每批为2301/2104/2201/2203/2205/2001/2011之一的1..8个同物品对象。保留精确原生节点/姿态/连接校验，以及固定 hold anchor 周围16m目标/弹射包络；高架路径及带覆盖复用不支持。通用蓝图文本不增加矿机布局能力。保留原 `actionId`、`buildId` 和 `constructionControl`；逐对象回执共享一次全批材料实测扣除，不能将每行当成独立扣除。完成前须核精确预建造/实体/组件及双向连接。

正常施工保持固定 hold anchor 处的 Walk；Drift 或锚损失立即停止新派发并走精确绑定的 Pause/unknown 失败保护，不能声称无人机已尾返。工作仍自原开始最多3600 tick/60秒，fresh resume 不增加一分钟，working 原 action 查询只续30秒观察租约。已知的有限取消保留第一次尾部时钟。dispatchDisabled 不等于安全空闲：须关闭后跨过精确工厂真实 Native 施工轮次、原预发缓存清空、全部 owned 无人机实际空闲及 physicalIdleVerified，并保留原绑定/anchor/储备证据。守卫、租约或尾期失败走失败保护，不能写成空闲成功。保留未知的原 action/build/checkpoint，不启用无人机、不重放创建；已证 pending 对象仅通过 fresh prepare_blueprint_build，以精确 resumeBuildId 及进度/玩家哈希续接。过期工作仍过期，不覆盖用户暂停/输入、不自动恢复，也不从 Host 退出推断无人机已返。

<a id="restart-and-interplanetary-flight"></a>
## 重启与行星间飞行

Codex、MCP Host 关闭、重启、断连、上下文压缩、回合结束，均不得触发 DSP 保存、退出、重启、加载。重连时新读受管身份、会话、revision、持久 Journal、外部接受计数和原操作账本。健康且已对账则继续当前世界、不受保护重载；当前是十写声明就保留该计数。未解决则冻结新写、对账同 action，绝不重放。换写入者要求原写入者停止、无尚未终态的操作或未对账结果、明确单写者交接。

只在用户明确关闭游戏，或必要且已授权冷部署时关闭 DSP，保留正常保存、终态、关闭、受保护恢复。Host 重启不授予加载权限，也不证明存档新鲜。

菜单 `gameLoaded=false` 正常，不能等 `gameLoaded=true` 才 prepare。受保护可用不表示原生就绪；prepare 查预加载、菜单、无 loader、精确源和 Journal，不消耗、不加载。只有真实未就绪拒绝才有界等待。接受终态后要求受管、已存、健康 tick 与 Journal。

主存档早于已知接受工作时先停，不加载。只有精确批准 `verified_newer_lastexit` 才可用健康含 Journal 来源、已知下限与批准候选、证据 1、嵌入身份、完整文件与租约。不推断确认、不伪造隔离、不任意选源、不覆盖主文件。对账保存实体、材料、连接，不能重放成功调用器或伪造旧会话审计。

主恢复、AutoSave0、飞行能力独立，不是选择器，新进度后不复活旧能力。Governor 附着失败保留原声明，不能改归档或替基线。修复后的冷组件可恢复同声明并清观察，不恢复旧信用。

飞行要求新核对正常核心、燃料、专用检查点，使用原生 Fly/Sail 并付能量，轮询到稳定落地。过渡中可选读取 `NO_LOCAL_PLANET` 不是终态，继续轮询同 actionId、延后本地读取，不能重启飞行调用器。

有界岸线恢复中，瞬时 Walk 不算到达，精确未完成命令必须持续进度、能量检查直到目标。不能发竞争 Move；要求连续 600 tick 地面低速。终态 `recovery_required` 不能从之后快照改称成功，只有仍公布的匹配检查点可恢复。

冷启动可能因精确主进度更新而使失败飞行检查点退役。不能改 ticket 或复活 token。公布的主受保护恢复是进入更新时间线的独立流程，检查后再新飞行、新检查点。失败重试在资格仍有效时可重复加载同匹配点。稳定到达后确认能力消失，并正常保存主世界使退役持久。

当前场景执行从当前 [Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab) 检出目录开始，先读取其当前 [AGENTS.md](https://github.com/Arcueid77/Spherewright-Lab/blob/main/AGENTS.md) 与[活动交接](https://github.com/Arcueid77/Spherewright-Lab/blob/main/operations/active-handoff.md)；本内嵌指南不授予权限。[Lab 固定快照](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7)与[已验收指南](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/agent-playbook.md)仅保留历史证据、示例。历史成功绝不授予重放权限。
