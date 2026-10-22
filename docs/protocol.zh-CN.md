<a id="bridge-protocol"></a>
# Bridge 协议

[English](protocol.md) | [简体中文](protocol.zh-CN.md)

协议版本保持 `1`；`0.5.0` Voyager 开发接口面向 DSP `0.10.35.29151`，保留 `0.4` 协议接口。本文描述当前维护的源码，不宣称已安装组件或 Voyager 实机验收。正式 `v0.4.0` tag 指向 `b7c0002dbe2e8f5608c5ca140b0c6326a1818021`，旧生产验收仍固定于 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`。参见[产品状态](project-status.zh-CN.md)、[原生验证](validation/native-29151-preflight.zh-CN.md)、[安全模型](safety-model.zh-CN.md)和 [Agent 指南](agent-playbook.zh-CN.md)。

<a id="framing-and-authentication"></a>
## 帧与认证

每条 Pipe 消息由四字节小端有符号长度加 UTF-8 JSON 组成。负长度及超过 1 MiB 的负载在分配内存前拒绝；零长度帧进入 JSON 校验后拒绝。

每次连接先进行认证 `handshake`，携带当前用户保护的运行描述文件中的有效 `bridgeInstanceId` 与启动 token。凭据错误或认证前请求会关闭连接。Pipe 与描述文件凭据属于受保护传输细节，不是公共证据。

请求包含 `protocolVersion`、`messageType=request`、UUID `requestId`、可选 `sessionId`、`method` 与 `payload`。响应回显请求身份，带 `success` 和 `result` 或结构化 `error`。传输成功不等于原生校验通过、commit 接受或操作完成。须保留明确错误码、可重试性和失败阶段。

<a id="current-public-mcp-surface"></a>
## 当前公共 MCP 接口

显式 `prepare_resume_owned_game(recoveryMode=authorize_remote_primary_recovery)` 接受用户新授予的精确远端主档有限恢复窗口，不依赖旧自动操作凭据继续有效。回显 `remotePrimaryRecoveryAuthorizationUsed` 及准确新期限/范围/充能上限、候选与舍弃 tick 数；仍须独立 digest 确认、原 Journey/Journal/成功去程证明、持久消费及完整 Native 健康接管。舍弃进度不返还旅程时间，不改变普通 `open_saved_primary` 或宣称旧动作结果。见[手册](agent-playbook.zh-CN.md#远端主档的新有限恢复授权)。

公共 MCP 的 `prepare_resume_owned_game` 参数现默认 `open_saved_primary`。显式使用旧 `default` 模式的既有 wire 请求保留原行为；普通 MCP 读档请求不会被推断为继续旧任务。

普通读档使用 `prepare_resume_owned_game(recoveryMode=open_saved_primary)`：即使旧自动操作许可已过期，也核验精确完整的同 Native 主档、受保护归属双副本及原 Journal；原生加载完成后停在准确存档 tick，不续跑旧任务，不自动保存/充能/飞行。Prepare 回显 `opensSavedWorldOnly`、`previousAutomationRemainsStopped`、`newChargingProhibited`、`discardedUnstoredProgressGameTicks`。可选 `acceptUnstoredProgressLoss` 默认 false，须同时绑定准确已知 `minimumRecoveryGameTick` 和获准主档 `expectedRecoveryGameTick`。保留 fresh prepare/digest/确认、持久单次消费、完整文件租约及禁止回退来源；新签发的仅读档凭据可供以后再次打开存档，不能充当自动操作许可。见[手册](agent-playbook.zh-CN.md#打开存档并保持旧自动任务停止)。

`prepare_resume_owned_game` 新增显式 `continue_completed_remote_last_exit` 和可选 `priorActionKnownTerminalCompleted`，要求独立当前上下文域及立即前驱消费双副本，继承不变的授权窗口/起点，禁止新充能，拒绝新期限/上限参数。成功终态是调用方声明，不是Native认证，不得替换失败/未知结果；须核独立prepare回显并确认准确digest。见[手册](agent-playbook.zh-CN.md#在原窗口内续接已成功的远端接管)。

Owned Save 准备新增可选 `preserveQuarantinedProgress`，默认 false。序列化请求要求匹配的 `preservesQuarantine`/`preservationGameTick` 回显及 fresh 接管后暂停保存证明；普通 commit/幂等和其他阻塞均保留，不清隔离，不授予 Resume/Load。可选的 `BridgeError.nativeFirstFailure` 和动作结果 `nativeFirstFailure` 是有界、独立复制的首次判断证据，不是安全认证；未知值保留 null/unknown，未执行检查为 `not_reached`。见[手册](agent-playbook.zh-CN.md#接管后闲置隔离的进度保全)。

```text
spherewright_get_status
spherewright_get_session_state
spherewright_get_player_state
spherewright_get_progression_state
spherewright_get_gameplay_journal
spherewright_get_local_star_system
spherewright_get_voyager_star_catalog
spherewright_preview_voyager_route
spherewright_get_recipe_catalog
spherewright_get_build_catalog
spherewright_get_foundry_plan
spherewright_get_governor_plan
spherewright_inspect_blueprint
spherewright_export_blueprint
spherewright_get_blueprint_builds
spherewright_prepare_blueprint_build
spherewright_commit_blueprint_build
spherewright_prepare_cancel_blueprint
spherewright_commit_cancel_blueprint
spherewright_get_power_summary
spherewright_get_overseer_diagnostic_bundle
spherewright_get_overseer_production
spherewright_get_overseer_summary
spherewright_get_action_result
spherewright_list_resource_nodes
spherewright_inspect_resource_node
spherewright_list_factory_entities
spherewright_inspect_factory_entity
spherewright_list_assemblers
spherewright_inspect_assembler
spherewright_prepare_new_game
spherewright_commit_new_game
spherewright_prepare_save_import
spherewright_commit_save_import
spherewright_prepare_move
spherewright_commit_move
spherewright_prepare_interplanetary_flight
spherewright_commit_interplanetary_flight
spherewright_prepare_voyager_flight
spherewright_commit_voyager_flight
spherewright_prepare_cancel_voyager_flight
spherewright_commit_cancel_voyager_flight
spherewright_prepare_harvest
spherewright_commit_harvest
spherewright_prepare_handcraft
spherewright_commit_handcraft
spherewright_prepare_select_research
spherewright_commit_select_research
spherewright_prepare_build
spherewright_commit_build
spherewright_prepare_dismantle
spherewright_commit_dismantle
spherewright_prepare_upgrade
spherewright_commit_upgrade
spherewright_prepare_configure_building
spherewright_commit_configure_building
spherewright_prepare_transfer
spherewright_commit_transfer
spherewright_prepare_logistics_station_fleet_transfer
spherewright_commit_logistics_station_fleet_transfer
spherewright_prepare_refuel
spherewright_commit_refuel
spherewright_prepare_supply_mecha_warpers
spherewright_commit_supply_mecha_warpers
spherewright_prepare_save
spherewright_commit_save
spherewright_prepare_quarantine_reconciliation
spherewright_commit_quarantine_reconciliation
spherewright_prepare_resume_owned_game
spherewright_commit_resume_owned_game
spherewright_prepare_reload_flight_checkpoint
spherewright_commit_reload_flight_checkpoint
```

无需活动 Bridge 即可读取文本资源 `spherewright://agent/playbooks/opening-movement-v1`，游戏操作前必须阅读。逻辑内嵌名保持 `Spherewright.Mcp.agent-playbook.md`。发现工具不授予执行权限；历史沙盒基础产线方法不注册。

<a id="request-rules"></a>
## 请求规则

受管存档、玩家、工厂读取要求活动 `sessionId`，绑定星球的请求还要求 `planetId`。普通地面操作限当前本地星球；飞行及停止飞行请求在太空过渡中保留各自约定的出发地绑定。实体检查额外允许明确选择当前受管恒星中已创建、身份验证的模拟驻留工厂，但从不创建或加载工厂。玩家读取仍使用当前 `localPlanetId`。

检查使用 `objectId`，正数为已建实体，负数为预建造。零、缺失及 `Int32.MinValue` 拒绝；其他工具的 `entityId` 或 `storageEntityId` 不能替代。未完成或过期分页不证明不存在。

修改 prepare 绑定当前会话、revision、`GameData`、操作、身份、参数、预算和要求的哈希域。Commit 使用短期 `planToken`、UUID `idempotencyKey` 及规定的会话、星球绑定。菜单创建与恢复有独立 schema。

`prepare_move.surfacePreview` 是请求最短表面弧最多64m的附加建议证据：最多65样本、最大1m间距及总计130次原生向下查询，沿用8704地面/16水面掩码。它不创建订单，不改玩家/能量字段或commit准入；超过64m为unavailable，partial、缺失或unavailable保持未知。即使采样完整，也不等于原生寻路、路线净空或Walk证明。

完整观察的岸线过水仍须无未知样本、精确已审目标，以及至少4m且至少`2*arrivalTolerance+1`米的末尾干地后缀；后缀每点要求有效地面/水面命中及有限 `groundBelowWaterMetres<=-0.1`。保持 `arrivalTolerance=1.5`、原有限预算及原action/key，不扩大失败目标容差重放。要求真实终态/Walk/低速/能量读回，并保留飞行岸线落地原有的连续600 tick地面低速门。水中间点、浅水终点或失败操作不是落地证明；失败停止该候选族，不另开路线或本地飞行。

| 证据 | 范围 |
| --- | --- |
| 玩家操作 `stateHash` | 新的正常操作玩家证据；可选 Move 漂移有独立限制 |
| 工厂 `stateHash` | 完整可变状态；storage-capacity 绑定此值 |
| `endpointStateHash` | 建造、升级、导出的身份、姿态、静态配置与连接；普通货物和进度可以变化 |
| 根 `configurationStateHash` | Sorter-filter 配置、拓扑及手持货物，不是通用静态拓扑 |
| 嵌套物流站配置哈希 | 仅用于规定的物流站模式 |
| `selectionStateHash` | 研究队列、当前科技、解锁、前置条件，排除自然上传进度 |
| 材料、评估、几何、提案哈希 | 各自声明的只读证据，不能替代状态哈希或令牌 |

`STALE_SESSION`、`SESSION_NOT_OWNED`、`STALE_STATE` 区分过期会话、所有权和状态。请求或哈希域错误可为 `INVALID_REQUEST`。模式证据缺失或战斗模式阻止游戏 commit；沙盒、资源设置保持显式，但不扩大能力。

列表使用有界不可变分页及不透明、绑定会话、星球、过滤器、分页大小的 cursor。保留采集 tick，读完一个快照；独立调用不是事务。禁止发布能力字段、原始请求、描述文件或私有路径。

`spherewright_get_player_state.constructionDrones` 保留既有计数（含 `total`），并新增五项可空观察：`nativeSpeedMetersPerSecond`（m/s）、`nativeEnergyPerMeterJoules`（J/m）、`nativeEjectEnergyJoules`（J）、`nativeRepairEnergyPerTickJoules`（J/Native tick）及 `nativePendingJumpTicks`（Native tick）。速度来自 `history.constructionDroneSpeed`，无人机能量设置来自 `Configs.freeMode`，修复项来自 `repairHpPerTick * repairEnergyCostPerHP`，待跳跃计数来自 `controller.actionWalk.jumpDelayTick`。单位沿 DSP29151 每游戏秒60个 Native tick 的体系；修复项是每 Native tick 能量，不是瓦。缺失、不可用、非有限或负值为 null，不是0；旧0.4载荷可缺少这些字段。这些条件观察只支持原地初估施工停止尾部必要预算，不是执行授权或完整 Native 几何证据；fresh prepare/commit、原返程/core/reserve、单 writer 及较新 primary 回滚保护均不变，不新增工具或放松执行守卫。

## 原生玩家物理只读观察

0.5 开发版的 `spherewright_get_player_state` 新增可空 `nativePhysics`，在游戏主线程复制当前 Unity 版本、物理步长、Rigidbody 设置及有界玩家碰撞体/材质参数。单位为秒、米、米/秒、两个重力向量的米/秒²及千克。`gravity` 是 Unity 重力，`nativeGravity` 另行记录控制器实际的 universal gravity（universal 坐标）；刚体及碰撞体事实使用当前已加载的本地 Unity 场景，比较向量方向前须显式变换坐标。碰撞体尺寸保留局部坐标系，并附复制的世界变换及缩放。结合读回的 `unityVersion` 解释 `simulationMode` 和各速度设置；设置值不证明模拟后的实际速度。`state=unavailable`、缺失字段或缺失材质均表示未知，旧载荷可以不含此快照。`safetyCertified` 始终为 false：观察到设置不等于证明接触后的总速度、连续干地区域或安全停止距离。此观察不推进物理模拟、不改变玩家、不替代 fresh prepare/commit、不进入现有玩家哈希域，也不自行授权有限能耗移动分支；适用的移动、飞行及储备门继续生效。

详细读取可另返回 `nativePhysics.autoSyncTransforms`、`groundContact`、`resumeGroundContactPreSync` 与 `resumeGroundContactPostSync`。地面接触观察保留 `state`/`reasonCode`、采集 tick/阶段、规定的 layer mask15873、trigger 策略、`checkSphereHit`、是否尝试/命中 sphere cast 及 `groundDistance`。缓存 Walk 着地/距离值明确标记 `cachedValuesAreObservationOnly=true`，不作为当前接触证明。 Native downcast 未命中后的缓存地距20只是20m搜索上界哨兵，不是实测地距；新 query 在实际 sphere hit 时返回 `groundDistance=0`、cast hit 时返回真实有限距离，cast miss 时距离为 null 并保留 `sphereCastHit=false`。未观测或异常仍为未知，不填 false 或零。命中碰撞体只披露类型/layer/trigger/enabled/active/default-scene 事实，不含名称、路径或 instance ID。同步前后观察仅属于同一次接管/session，失配即清空；缺失/unavailable 保持未知，每份接触快照均为 `safetyCertified=false`。此玩家读取只做 query，绝不调用 SyncTransforms、不积分物理或改变玩家状态，也不进入既有 v1 哈希。

详细本地地形生命周期观察可另返回 `nativePhysics.terrain`、`resumeTerrainPreFirstFrame`、`resumeTerrainPostUniverse` 及 `resumeGroundContactPostUniverse`。须保留真实阶段/session/接管归属和 unavailable；计划阶段不是已采集 Native 轮次。这些字段与保留的旧同步诊断不运行首帧、不授权重试、不改变 v1 哈希，也不确定此前地面检查失败的原因。新首帧路径不调用 SyncTransforms；现有 query/缓存值及安全证书的区分不变。

可选的 `nativePhysics.resumeNearSupportPreFirstFrame`、`resumeNearSupportPostUniverse` 及 `resumeNearSupportLatest` 是脱离原生引用的只读观察，描述精确当前 Native near-collider Vein/Vegetable 池支撑。保留采集阶段、查询完整性及未知值；`safetyCertified=false` 不变。cast 命中或足部 overlap 为0均不证明 Native sphere 接地或严格物理空闲；这些诊断不授执行权限，也不改变 v1 哈希。

每份近支撑快照还可含可空 `identity` 诊断；其中 `queriedHit` 单独描述实际 query 命中，与保留的支撑物区分。`sameManagedReference`、`sameUnityObject` 和 `sameNativeInstance` 是不同观察；最后一项只比较非零实例 ID 是否相等，不输出其值。几何相同不证明身份相同。保留 record/collider/pool cell/object pool/chain 的比较、`planetColliderDirty` 和 `observationReasonCode` 可在外层 `state=unavailable` 时保留。缺失观察仍为未知，明确 false 仍为 false；即使全部为 true，也不自行证明归属、接地、重建或失败原因，不授权重新绑定、重试或准入。`safetyCertified=false`、全部既有守卫及 v1 哈希保持；这些诊断不新增物理查询。

可空地形诊断 `rootHasParent`、`rootLocalPosition`、`rootLocalRotation`、`rootLocalScale` 与 `rootLocalToWorldMatrix` 复制当前根变换事实；矩阵为16个 float，按 row-major 排列。缺失/未知仍为未知。保留精确单位 `lossyScale` 旧路径；新增场景分支仅接受无父节点的根、精确本地/world 零位置/identity 旋转、通过 Native `Vector3` 近似单位比较的正均匀缩放 `S<1`、精确等于 `S` 的 world `lossyScale`，以及有限且精确为 `diag(S,S,S,1)` 的矩阵。绑定期间精确重核 `S`，变化即拒绝。足部/半径/场景/对象引用、能量、13tick及返程门仍全部必需；收缩不是距离或能量证明。这些只读诊断不进入 v1 哈希。

可空 `voyagerFlightReadback.firstLandingHandoff` 保留第一次同 tick 采集：`capturedAtGameTick`、`proved`、首个 `blockerCode`、`captureStage`/`captureExceptionType`、`terrain`/`groundContact`，以及可空 `coreEnergyJoules`/`loadedEnergyJoules`/`perNativeTickEnergyJoules`（P）。晚到读取不得覆盖首次采集。不可用观察仍为未知，复制的地形/接触快照保持 `safetyCertified=false`。这些诊断不进入 v1 哈希，不新增工具、不改变确认/恢复门，也不证明物理空闲或路线成功。

地形快照可保留原同次有界 overlap 查询的 `footHits`（至多257条）及可空 `footHitDetailsComplete`。未匹配当前 simulator 的 mesh 仍未分类，不能据此证明 foreign、ocean 或另一星球归属。深拷贝的精确引用及 Unity 相等计数只是观察，不能互换作归属证明；`uniqueSurfaceIndex` 仅在唯一精确当前 surface 匹配时从0开始。可另以有界 `registeredSimulatorCount`、`registeredTerrainOwnersComplete`、`registeredTerrainOwners`、`footCurrentSurfaceHitCount`、`footRegisteredSurfaceHitCount` 及 `footOverlapsCurrentOrRegisteredSurfaceOnly` 描述已注册 Native 地形，不暴露 Native ID 或资源。未知、重复、歧义、未就绪、错 world/star/slot 或截断证据仍拒绝。诊断列表完整或 Unity 相等不等于准入，已知 ocean/sphere 归属本身也不通过 terrain 足部门。原查询 `15873 | (1 << 31)` mask、trigger 策略及257结果上限保持；观察仍为深拷贝、`safetyCertified=false` 且不进入 v1 哈希，普通 layer9 观察不新增该 overlap 查询。旧 payload 可缺少这些观察，缺失仍为未知。

同一 `spherewright_get_player_state` 可返回可空 `nativeLocalControlEnergy`，无新 opt-in。`perNativeTickEnergyUpperJoules` 是每 Native tick 的条件能量上界 P（焦耳），须保留 `basis`、`costInputsProved`、`independentDebitsExcluded`、`independentDebitsBudgeted` 及 `blockers`。旧 `independentDebitsExcluded` 保持原零独立扣费排除含义及 v1 basis；有界 v2 basis `conditional_native_walk_drift_tick_upper_v2` 可改为报告 `independentDebitsBudgeted=true`：P 已包含 `independentEnergyPerNativeTickUpperJoules`，并分别披露 `shieldRechargeEnergyPerNativeTickUpperJoules` 与 `laserRechargeEnergyPerNativeTickUpperJoules` 贡献。不得再把这些含项加到 P；它们是条件数学上界，不是实际已支付量。`executionPremisesProved` 始终 false：此读取不证明执行权限，暂停世界仍明确带 blocker。缺失/未证保持未知，不能当零；该脱离原生引用的观察不进入既有 v1 哈希。生成下界 g 仍来自航线预览 `coreGenerationLowerBoundWatts`（瓦），比较净值前须显式统一单位。P/g 未知或净值非正均不保证进度；即使估算为正，也仅可披露 fresh 守卫下的有限尝试，不保证充能或飞行完成。

<a id="voyager-visibility-and-route-previews"></a>
## Voyager 可见信息与航线预览

原生补给方法 `spherewright_prepare_supply_mecha_warpers` / `spherewright_commit_supply_mecha_warpers` 映射到 `prepare_supply_mecha_warpers` / `commit_supply_mecha_warpers`。Prepare 使用 `PrepareSupplyMechaWarpersRequest`：当前受管 `sessionId` / `planetId`、完整玩家快照的新鲜 `expectedPlayerStateHash`、`count`（默认20）和 `stateHashVersion=1`。Count 必须等于 `min(20, fresh 包内 item1210 数量)>0`。受管玩家须存活、着地且空闲；原生舱须为单个空 `Filtered` 格，filter1210 / stack20，count/inc 为零且无禁用格。Prepare 绑定精确玩家/背包/舱身份、count/inc 与当前自动设置，不移动库存。

补给 commit 复用 `CommitNormalActionRequest` 与既有协调器，使用精确 `planToken` 和 UUID `idempotencyKey`；没有公开通用 `commit_normal_action`。它只调用一次 `Mecha.AutoReplenishWarper`，保持 `autoReplenishWarper`，不调用 `UseWarper`。查询原 action 的 `mechaWarperSupplyReadback`：背包/舱 count/inc、自动设置、可空 after 字段 / `actualStored` / `nativeReturnedSuccess`、`readbackComplete` 和 `conservationVerified`。这是后续正常消耗前的原生同步调用边界；缺失证明保持未知，已接受、超时或结果未知的调用不得换键重做。

`0.5` 只读工具 `spherewright_get_voyager_star_catalog` 和 `spherewright_preview_voyager_route` 分别映射 Bridge 方法 `get_voyager_star_catalog`、`preview_voyager_route`，均要求当前受管 session 和出发行星。它们观察原生探索可见性并计算有限航线预览；对许可视图，目录可沿正常星图路径请求 `StarData.RunScanningThread` / `PlanetData.RunScanThread`，可能生成合法可见扫描缓存，扫描未就绪仍保持未知。它不调用任意生成器，不创建远端工厂。这些读取不起飞、进入曲速、消耗燃料/翘曲器、保存、创建 checkpoint、读档或创建游戏 action。预览无 token，保持 `executable=false`；预算通过不代表飞行 commit 或实机验收。

目录输入为 `sessionId`、`planetId`、可选 `limit`（1–32 颗恒星，默认 16）及 `stateHashVersion`（1）。航线额外要求 `destinationPlanetId`、`expectedPlayerStateHash` 和 `expectedCatalogStateHash`；默认 `maximumFlightGameTicks=36000`、`returnReserveGameTicks=18000`、`minimumCoreEnergyRatio=0.8`、`extraWarperReserve=1`。最大窗口是整个往返（7200–108000 游戏 tick），返程窗从中预留，去返程各须至少 3600 tick。核心比例须为 0.8–1.0 的有限值；额外翘曲器为 0–18 枚，在原生往返两枚之外预留。能量字段使用焦耳，功率使用瓦，距离明确分别给出米和光年；这些是保守预算边界，不是实测航时或消耗。

合法可见恒星标记可使用 `destinationPlanetId=0` 和明确 `destinationStarId`，其行星/资源仍保持未知。`maximumWarpGameTicksPerLeg` 默认1800，至少180且不能超过任一单程预留窗口；`maximumArrivalOffsetMeters` 须为100000–3000000有限值，默认3000000。仅恒星距离预算包含该偏移，明确可见行星使用实际已知距离。`budgetBasis=finite_native_warp_duration_and_explicit_return_recharge_v2` 将有限曲速时长与总飞行窗口分开预算。`requiresReturnCoreRecharge=true` 要求返程出发前 fresh 读回核心达到 `returnCoreEnergyMinimumJoules`，同时保留已装燃料/翘曲器储备；单程缓冲不证明之后能充电或落地。

预览可选非空 `journeyId` 选择精确受保护航程的回家单腿，并返回 `isReturnLeg=true`；目的地必须朝向原出发地。从原储备校核剩余单腿，包括一次原生进入曲速及 `extraWarperReserve`，不得要求再预留一轮母星至远端的新往返。不传 `journeyId` 则请求去程的往返预算；返程 prepare 同样须指定原 `journeyId`。

两结果均保持 `prepared=false`、`executable=false`、`commitAllowedNow=false`，不含 `planToken`；MCP 拒绝声称执行权限的响应。`resourceVisibility=unknown`、null `resources` 或 null `planets` 不能转成已观察空集合/零；`resourceSummaryComplete` 和 `scanReady` 限定证据范围，显式观察到零仍保留零。`budgetComputed=false` 表示数值预算不可用，不是零成本航线；`budgetBasis` 标明保守计算依据，`budgetSufficient` 与执行权限分开。须保留独立能量/科技/可见性/翘曲器检查及结构化 `blockers`。

已观察恒星/行星汇总声明 `resourceSummaryScope=remaining_veins_and_native_gas_oceans`，范围包括剩余矿脉及合法可见的原生气体/海洋来源。某矿脉行归零不能证明该物品在整个星系缺失；须检查所有相关行星的完整可见汇总及全部原生来源类型，该星系覆盖未知、不完整或截断都不能证明全系缺失。无关恒星目录条目被截断，不使已完整呈现的目标星系证据失效。`expectedPlayerStateHash` 绑定完整玩家快照的 `stateHash`，不是更窄的玩家 action 哈希域。

资源可见性及预算字段的未知必须保留。隐藏或未观察资源不等于缺失，主题潜在资源不是已勘探矿脉；“出发星系缺失资源”须有合法完整探索证据。使用 fresh 目录/玩家绑定及当前原生科技、单位、距离、核心/燃料、翘曲器消耗和去返程储备。不可用证据应报阻塞或未知，不能作为零消耗或剩余容量；航线预览不能证明目的地可以安全落地。

既有 flight/checkpoint 安全规则继续适用：取消、超时或断线后核销原 action，结果未知不得换键重放。飞行 checkpoint 只属于精确且符合条件的中断航程，成功到达或 primary 覆盖进度即退役，不得回滚较新主档已覆盖的进度。这些读取不授予恢复权限，也不执行跨星 commit；有限执行使用下述独立原生方法及 fresh prepare/commit/readback 门。

飞行 checkpoint ticket v2 复用受保护 `OwnedSaveRecoveryLease`，记录 `CheckpointFileFingerprint`，绑定完整文件及精确内嵌 owned 身份、原生版本、和平模式和保存 tick。Prepare 与 commit 均 fresh 重验，tick/mtime 相同不足以放行。Commit 持只读 lease 贯穿原生异步认领，最多五分钟，成功、失败、取消或 dispose 均释放。缺少此强证据的旧 v1 flight 票据拒绝使用，不会提升为新恢复权限；protected-primary 的确认/迁移门不变，也不声明已通过原生失败飞行验收。

原生 dispatch 前先登记原 action/key，并持久记录 `ReloadPending` / `ReloadActionId`。进入原生加载后抛异常时，保留原 accepted action 为 `outcome_unknown` 及其 source lease/expectation，只核销原 action，不得换键。持久 pending 阻止新 reload 和普通写入。成功须证明精确 checkpoint 已在不同于 dispatch 来源的新 session 中认领，旧 checkpoint 标志不足以证明完成。核销后票据标记 `recovered` 并保留原完成结果；再次 reload 须有新一次绑定航程失败。

<a id="bounded-voyager-execution-and-stop"></a>
## 有限 Voyager 执行与停止

开发方法 `prepare_voyager_flight` / `commit_voyager_flight` 复用既有行星间飞行协调器/checkpoint。Prepare 通过 `PrepareVoyagerFlightRequest`（`planetId`、`expectedRouteStateHash`、完整玩家快照的新鲜 `expectedPlayerStateHash`、`stateHashVersion=1`、可选 `journeyId`）绑定当前已审阅的一条航线及原生储备检查。空 `journeyId` 开始有限去程，返程必须指定同一受保护航程。Commit 使用 `CommitNormalActionRequest` 并明确要求 Voyager 模式；普通行星间 commit 明确要求普通模式且保留同星系守卫，两路由不能使用对方计划。

prepare/commit 时原生控制钩子必须在过去两秒内观察到 tick。以短于 30 秒控制租约的间隔查询原飞行 action；只有绑定原 action 的读取才续此租约。租约到期或原生 tick 中断会锁定停止请求。原生退出曲速与普通制动须在原定 1,800 tick／30 秒清理上限内收敛，暂停不延长墙钟期限，停止未获证明仍是未知。同进程检查点恢复计入加载前捕获的 source tick 之前的实际耗时；真实进程退出后若 source tick 未知，精确恢复会保守核销原航程全部剩余时间、标明保守计账，并禁止据此再开返程。原绝对期限与物料底线不变。

查询原 action 的 `voyagerFlightReadback`：`journeyId`、`phase`、起终点、可空所选/实际行星及实际恒星、原生曲速状态/命令、`entryAttempted`、`nativeExitObserved`、舱内/包内/已消耗翘曲器、焦耳储备、`flightDeadlineGameTick` 和 `journeyDeadlineUtc`。根 action 的 `planetId` 始终保留出发地，包括无本地行星时。到达可见恒星范围不证明落地；单程成功不等于完整往返/资源/物流/故障验收。跨 action/session 保留持久返程储备与绝对航程期限；Host 中断不使它们重置。

`prepare_cancel_voyager_flight` 使用 `PrepareCancelVoyagerFlightRequest`，绑定原飞行出发地 `planetId`、精确 `actionId` 和 `stateHashVersion=1`；`commit_cancel_voyager_flight` 使用同一提交合约。它在原航程锁存 `stopRequested`，不创建第二个移动 writer。查询原飞行，核原生退出曲速、刹停及真实终态；停止回执本身不证明飞行稳定或恢复完成。未知结果保留原 key/action；checkpoint reload 为独立受保护操作，不得回滚较新主档。

<a id="voyager-local-work-and-durable-structured-construction"></a>
去程报告 `flight_succeeded` 前，须通过与 `remote_landed` 闲置交接共用的完整严格谓词，仅有落地计数不足。除下述有界的核心单项落地补能等待外，任一条件未证时保留原 action 的失败/unknown 路径，并在 `voyagerFlightReadback.failureReason` 保留精确 predicate code，不以笼统到达结果覆盖细因。该要求约束新结果，不追改已记录的历史成功。`GameMain.Pause` 既不证明物理空闲或恢复完成，也不授予重载权限；checkpoint 恢复仍须独立受保护的 prepare/commit 及其适用确认门。

既有飞行仅在核心停止储备是唯一尚不足的准入项、所有独立前提与完整预算均已证明时，才可在落地交接前有限等待 Native 自然补能。600 tick 最低稳定门及原 7200 tick 接触上限保持，等待不重置任何一个时钟。每个真实 501 重核已核足的 2P 储备、精确场景/地面/成本/工作事实、原 journey/返程预算及 30 秒原 action 租约。只允许 Native GenerateEnergy 自然补能，不写能量、不承诺净增为正。飞行成功及 checkpoint 成功须经当前完整原 13P 闲置准入。其它变化、前提失败或原剩余时间不足均走原 action 停止/恢复路径，不换键或重放。`firstLandingHandoff` 仍保留首次尝试，可保持 `proved=false`；最终成功必须取得 fresh 当前完整验证。仅凭首次诊断为 false 不能认定最终失败，等待状态不授施工准入。

## Voyager 本地作业与持久结构化施工

原航程处于 `remote_landed` 时，本地例外绑定精确 owned 存档、Native 版本、session、world/player/controller/mecha 及实际已加载本地工厂。Fresh 站点配置、包/仓储或机队转移、补燃料及 Save 保留原生范围；Move/Harvest 与独立结构化施工路径还须在原返程窗口之前预留有限工作和停止时间，保留 loaded 返程能量底线及原生 `1+extraWarperReserve`。这些动作不重置航程期限，也不允许在母星写远端。返程核心充能是 fresh 起飞门，不要求每个本地动作始终满核心。

Move/Harvest 复用既有普通 RTS 动作并保留无水快路。有限能耗分支允许运行中有界 Walk/Drift，要求 fresh Native pre-player 钩子及精确 owned world/player/controller/mecha/本地工厂绑定；排队订单、手动输入/惯性、待跳跃、导航、快速旅行、曲速和 Fly 仍不支持。原生水平、推力及后台成本须覆盖有限工作窗与13个付费 Native 游戏停止 tick，含采矿与 RTS 减速同 tick 发生，且不侵入原核心/loaded 返程底线、原生翘曲器或返程时间。Move/Harvest 的停止上限为这13个付费游戏 tick 或独立1秒墙钟，先到为止；UTC 准入另留至少1秒清理余量。墙钟上限只是等待期限，不是物理停稳定理，也不能折成60个付费 tick；任一上限均不重开原 action 或 journey 期限。本地 UTC/tick 期限及30秒原 action 租约的范围保持不变。此分支仅在有限能耗资格通过时替代原整球海洋排除门，不授予路线净空或连续干地区域证书。正常成功仍须实际干地 Walk 与真实 Native 零速度读回；RTS 归零、abort 订单或游戏暂停均不足。

成功远端落地后，同一原 journey 继续守护闲置能耗与原返程时钟，直至已预算的本地动作、受控施工或真实返航接管。失去闲置 Walk、成本或原返程时钟证据时，走精确 owned 绑定的 Pause/quarantine 分支；已成功的飞行或 Move 仍保留成功，后续闲置保护不改写其原结果。 Host 卸载或默认 Save 重载若丢失内存中的 landed-controller 绑定，不得静默重建资格；除非独立确认的精确票据绑定的 LastExit 续接证明新的独立 idle owner，否则只对同一 owned 世界 Pause/quarantine 并拒绝本地/结构化施工。保留 checkpoint 与确认门；普通路径支持同一 Host 内出航、本地作业和正常返航；物流保存恢复续运安排在回母星且 journey 闭合后，远端在途冷恢复尚未验证。

守卫失败、停止/尾返上限到期或调用者/Host 观察丢失时，有限能耗的失败保护仅在原 owned 绑定仍精确匹配时调用正常 GameMain.Pause。当前 LogicFrame 仍可能跑完 player 与无人机的一整个 tick，因此必须支付并保留该整 tick 的储备；只有后续 Native 游戏能耗/派发 tick 冻结。Pause 不证明物理空闲或尾返成功。保留未核销的 outcome_unknown、quarantine 及精确原 action/build/checkpoint，不自动恢复、Load 或重放；绝不暂停替换后的另一世界。Host 真正卸载时先暂停精确 owned 的受保护世界，再拆除钩子；普通 MCP 断线不卸载 DSP 内常驻 Plugin，租约检查继续。暂停不续原墙钟期限。新源码仍须完整 Plugin/回归、准确 SHA CI、Lab 锁/采用、冷部署及实机检查后才计运行信用；不新增公开工具或协议版本。

本地控制保留旧零独立扣费子集（护盾容量0或补能原已关闭，且激光未启用），并仅为护盾/激光补能费用提供已证明有界的替代路径。两条路径均仍要求旧弹仓为空、有效空选择且整个弹药仓 item/count/inc/filter0、真实 ROF/费用0，以及地面/太空 `fleetCount=0`：即**零配置机队槽位**，不是零活动船只。默认空弹药槽1可以通过；不会为符合条件而修改武器、补能或机队设置。Native 默认值、和平模式或当前满盾/满激光缓冲不证明零费用。复制器/科研队列须空闲，并以无输入及稳定性证据覆盖整个已预算包络内的生成与补能速度参数；未用完的原生科研物品 buffer 不必清空。

有界路径以全部已加载物品原型元数据的 fresh 安全超集、当前燃料选择、两张 Native inc 表的0..10索引及无原型回退值证明武器功率上界，防止正常换燃料逃出上界。未知、非有限、不稳定或不安全的转换/溢出算术均拒绝。每 tick 补能总上界 B 同时覆盖护盾与激光，不因缓冲当前已满或和平模式而减免；它在移动/工作消耗系数结算后相加，B 本身不乘 `energyConsumptionCoef`。这不证明本次子世界的实际费用，也不扩展到任意战斗支持。

既有有限原生施工适配器仅在后台预算已包含同一组捕获事实下的 B 时，才可显式采用相同有界补能证明；它核验预算覆盖关系，不重复计入 B。旧排除门仍为默认。生成/补能速度稳定性须覆盖完整已预算工作与无人机尾部加2tick；固定 Walk/锚点、16m目标/28m无人机、原几何、核心/loaded/返程底线及未知/尾返门均不变，不新增施工范围或布局能力。

既有 `prepare_build`/`commit_build` 可在原航程内绑定一个原生已验批次，含1..8个同物品对象：2301、2104、2201、2203、2205、2001或2011。保留原生矿机节点、姿态、科技、碰撞和连接校验。首版拒绝高架路径及带覆盖复用，要求有限16m原生目标/弹射包络与固定 anchor；通用蓝图语法不增加矿机布局格式。原 action 只创建一次，持久保留写前记录、精确预建造至实体/组件/拓扑证据及一次全批实测材料扣除。各对象回执共享这次扣除，不能逐对象重复求和。

读取原 action/build 的 `constructionControl`：`executionKind=structured_native`、phase、禁发/物理空闲/绑定/anchor/储备证据、观察租约、有限尾部 tick/能量与停止原因。正常施工仍要求固定 hold anchor 处的 Walk；Drift 或锚损失立即停止新派发并走精确绑定的 Pause/unknown 分支，不证明无人机已尾返。fresh resume 保持原最多3600 tick/60秒工作窗，原 action 查询只续30秒 working 观察租约。已知的有限取消保留第一次清理时钟：安全空闲仍须关闭后跨过精确工厂真实 Native 施工轮次、原预发缓存清空及全部 owned 无人机真实空闲读回。守卫/租约/尾期失败使用上述失败保护，dispatchDisabled 或 Pause 均不能提供 physicalIdleVerified。

只有精确 pending 对象可通过既有 `prepare_blueprint_build`，以 `resumeBuildId` 和新进度/玩家哈希续接。不能替换批次或再次原生创建；原工作窗过期不得刷新。未知结果只核原 action/build，不启用无人机、不编造尾返/储备证明。已知完成仅在精确 owned hold 仍存在且用户未暂停/接管时，才能恢复原本启用的无人机设置。Host 退出记录 pending/interrupted，不虚构空闲或重载权限。

<a id="detail-only-belt-cargo-observation-04-development-slice"></a>
<a id="factory-cargo-material-and-fuel-observations"></a>
## 工厂、货物、材料与燃料观察

矩阵配方使用研究站，`list_assemblers` 不查询全部生产者。仅详情的 `sorterEndpoints` 给出最多 16 个原生槽位，或四个带虚拟朝向（`slot=-1`，占用未知）。已建连接、时序证据与计划放置几何是不同证明。

`beltCargo` 仅在详情出现，独立于 `buffers` 与既有操作哈希。`state=observed` 报告该 tick 接触所选段的唯一货物栈。相邻段可能重复同一栈，不能求和作为路径库存。读取最多验证 512 格及两侧各九格保护区，失败不返回部分总数。空段不证明整路空或零流量。

可选 `rearPickup` 观察开放路径尾部一个对齐包。`no_aligned_packet` 不等于空；`not_applicable` 表示闭路或非尾段。按物品排序的总量不是队列顺序。物流站不能跳过不需要的输入对齐包；输入 `storageIndex` 记录最后接收槽，不是固定过滤器。

`materialInventoryObjectIds` 在同一 tick、一个工厂中选择最多 256 个正数已建对象：带、仓储、储液罐、分拣器、制造台、采矿机、物流站及有燃料证据的普通火力或微型核聚变电站。上限为 64 路、总 32768 格、4096 路径成员；每路仍限 8192 格、512 带。每路只出现一次，包括选择范围外成员。订单、机队货物、已加载热量和电力缓冲不是物品库存。不支持研究站、电力节点或其他发电机。缺失或 unavailable 是未知，不是零。

`tankFluidCount` 是原生总液体物品数，包含显式零。Null 是未知，空 `buffers` 不是零；正数量液体缓冲标识同一库存，不是额外物资。

`fuelPowerState` 将缓冲物品、增产点和已加载燃料能量分开，二者物品 ID 可不同，零缓冲也可有余热。电力发电量单位为 J/t，绝非燃料库存；`isWorking=false` 不足以证明发电机停止。可选读取不改操作哈希，也不调用会修改能量的原生方法。

配方元数据增加热值、类型和可空 `acceptedAsMechaFuel`，要求原型与原生燃料表同时有证据。`fuelPowerProfile` 只提供有效普通 `2204/2211` 基础额定值，包括发电、耗燃料 J/t 与掩码，不表示实发电、燃料库存或持续供应。缺失是未知；未建或未供燃料的发电机不获建造电力信用。

<a id="research-and-journal"></a>
## 研究与 Journal

`autoManageResearchItems` 与 `mechaResearchItemBuffer` 用原生 3600 点等价值描述库存，与背包栈分开。正常研究管理可能保留矩阵或退回整件；余点永远不能向上取整。

`completionItemRewards` 是完整有序原型元数据（最多 32 条），不是交付证明。Null 为未知，`[]` 为观察到无奖励。保留重复条目，对账实际解锁 tick、背包和增产点、缓冲及容量。奖励不是制造或生产里程碑。

研究 prepare 使用 `expectedSelectionStateHash`。默认入队查原生前置条件。`prioritizeQueued=true` 仅通过 `SortTechQueue` 重排一个合格的非队首科技，保留其他槽位、进度、库存并返回即时 `researchQueueReadback`。终态表示重排，不是研究完成。

普通入队的 prepared 计划现在声明 `researchQueueReadbackVersion=1`；只有在同一游戏 tick 证明精确队尾追加/当前队首、科技进度、背包数量/inc/格占用、手持及燃料物品和原生机甲科研缓冲均保全后，才返回即时 `researchQueueReadback`。保全证明失败沿既有 unknown/隔离路径处理。能力标记缺失或为零表示旧版/未证明，要求此证据的调用方必须在 commit 前拒绝。动作 `expectedStateHash` 为 `CanonicalStateHash.Combine("select-research", sessionId, planetId, selectionStateHash, techId, false)`，不等于选择状态 hash。`itemBudget` 的 `research-consumption` 表示该等级原生总需求，不是入队扣料或剩余需求；`int.MaxValue` 可能是截断投影。入队后仍由正常游戏规则消耗材料和时间。commit 要求 UUID 幂等键，调用方自加的字符串前缀不构成 UUID。

受管 Journal 分别报告首次手动、产线产出、科技与升级选择、跟踪边界、持久序号及持久化状态，不暴露原存档身份或路径。`attached_existing_save` 保留历史覆盖不完整；起始、有效版本与转换分开呈现。

<a id="overseer"></a>
## Overseer

生产读取接收 1–64 个唯一运行时物品及 1–16 分页大小，采集已创建受管工厂，使用自动原生 level-0 `total[0]/total[7]` 滚动 600 tick 计数，排除玩家与机甲累计输出。理论速率来自有界原生组件，不用 UI 缓存。

诊断保留直接生产者覆盖、总数、截断和有界真实上游追踪。短窗零产出一般要求完整周期。停止且有电的 `output_blocked` 例外使用下一批准入阈值，不是物理仓储容量：熔炼 `101-q`（`q≤100`）、制造 `9*q+1`、其他 `19*q+1`。速率、电力、容量未知时不合格，无诊断也不证明持续供给。

汇总区分发电、容量、需求、外送，并限制电力、物流、研究扫描：512 工厂、4096 电网、65536 发电机引用、4096 站槽、每站 64 存储槽、12000 科技。不创建或加载远端工厂，超限失败，不静默截断。

诊断包在一个主线程任务中采集生产和汇总，仅合并精确工厂、星球、标志、tick；schema `1`、`public_allowlist_v1` 不含凭据、存档身份或路径。专用不可变分页保留 60 秒。

`inspect_factory_entity.ownedRemoteLogisticsStation=true` 只选择一个已知正数物流站实体，要求精确、已存在、归属受管世界且驻留模拟的工厂，可位于另一恒星。读取不创建或加载工厂，不赋予远端写入范围；远端几何/材料选择、负数/预建造 ID 均拒绝。默认 false 保留现有读取范围，prepare/commit 仍要求玩家实际当前本地星球。

仅详情的 `logisticsStation.activeVessels` 最多复制32条原生 `workShipDatas`，带 `coverage=native_active_work_ship_data`、`complete`、`truncated`、可空原生工作船计数及明确的 `observed`/`partial`/`unavailable`/`not_applicable` 状态。缺失/不可用是未知，完整观察到零须单独区分。`shipIndex` 是真实船字段，不是会压缩的活动数组索引；`ownerGId/shipIndex/gene` 仅区分有限窗口的派遣观察，可能复用或回绕，不能作为跨保存的永久航次身份。`direction` 表示离开/返回归属站，不等于供货/取货；`t` 是阶段参数，不是全程进度或 ETA。白名单仅有货物/增产点、阶段/方向/t/速度/曲速/翘曲器标量，无运输船坐标或私有 `StateToken`。船移动、指纹变化、货物归零或一次到达均不证明原生交付、完整周期或持续开采供应；须结合精确端点、双边库存变化、真实采矿及消费，观察多个完整周期。这些观察不进入 action/configuration/fleet 哈希。

<a id="stocked-logistics-causal-boundary-04-development-slice"></a>
`stocked_logistics_boundary` 表示 `configured_route_supply_total` 范围有库存，不是所示源未预留库存或发货证明。真实物品路径、needs、订单和运载器进度仍必需。疑似物流阻塞要求连续 600 tick 无进展；变化、回滚、拓扑漂移或超过 3600 tick 缺口使基线失效。时间证据持久化成功后才能返回公共诊断。

<a id="foundry-material-draft-and-machine-site-preview"></a>
<a id="foundry-material-and-site-plans"></a>
## Foundry 材料与选址计划

Foundry 接收目标、速率、可选外部供给和显式配方、建筑选择，聚合需求为依赖阶段、机器成本、工作功率和副产物，基于满电且无增产。上限 64 阶段、16 层、256 机器。`material_plan` 始终 `executable=false`；机器成本不含物流、电力设施，确定性 `planHash` 不是状态哈希或令牌。

可选单个显式网格最多 32 机器、1–8 列、4–32 m 间距，原点距表面 10 m 内，放置在 64 m 切平面范围。原生吸附、保守碰撞两两间隙 0.5 m、原生放置和完整背包预算均适用。首先阻止的条件不是全部缺陷。评估哈希绑定计划、会话、revision、玩家、姿态、原生条件、预算，单独 tick 不参与。清晰网格不证明完整物流、持续供给或吞吐；电力针对返回吸附位置。

显式 `blueprint`、自由 `boundaryPorts` 则绑定精确阶段数量、有向物品流、全部显式成本与依赖。组合支持单输出 `2302–2305` 阶段、默认未禁格 `2101` 与非混合通道。不支持的副产物、仓储、混输布局拒绝或阻塞。读取保持不可执行。

<a id="foundry-native-transport-budget"></a>
### Foundry 原生运输预算

`transportBudget` 对每条内部及边界通道采用满电、单物品、无背压的额定带容量与基础 `2011/2012` 分拣器时序、跨度容量。未知额定值或堆叠 `2013` 阻止组合，不缩小普通蓝图数据支持。分配流可能低于需求，必须看 `internalFlowsRouted`、`canPrepare` 和 blockers。`transportCapacityVerified=false`，额定值不证明实交付或公平分流。

独立选址电力采集最多 32 计划对象、512 节点、256 电网、8192 消费者、2048 未分配消费者，预算既有 max(work,idle,current)、充电、外送、新覆盖消费者和计划负载；新增发电只计风机。歧义、边界几何或已有未完成预建造阻止可用性。未知不是零需求；当前满基础负载充足不证明持续燃料，也不是令牌。

<a id="governor-proposal-04-development-slice"></a>
<a id="governor-proposal"></a>
## Governor 提案

`governor_proposal` 为 `executable=false`、`balanced=false`。选择 1–32 本地精确对象，附端点、配方证据、目标速率、`0<tolerance≤0.5` 和 36000–216000 验证 tick。原生速率是星球计数，归属要求完整相关直接生产者。

`governor-source-v2` 绑定静态身份、姿态、拓扑、配方、加速、过滤、设置、电网，排除活货物、进度和供电比例。最多八个序列各保留三个独立非零窗口。周期为默认 600 或 3600；长周期读取已验证、六 tick 对齐的原生 level-1 环，排除不完整桶。源、会话、物品范围、周期变化或回滚、缺口使历史无效。`governor-measurement-binding-v3` 排除仅规划目标速率：不变来源重新定价保留历史，但新提案仍须写入前核对锁定。

需求、实产实耗、本地余量、缺口和所选缓冲变化分别保留。饥饿消耗是下限，产减耗不是库存变化。备选明确完整设备、仅机器、所选模块或全部显式对象成本；额定增益不是实测增益。

<a id="governor-explicit-parallel-construction-comparison"></a>
### Governor 显式并行建造比较

`parallelExpansionBlueprint` 在就绪稳定非零基线下，以目标减基线使用同一显式 Foundry 布局，绑定同采集会话、星球、tick、revision、代码、选址、玩家、意图、建造哈希和电力。`bounded_construction_ready_at_capture` 仅表示可新 prepare，绝不表示 executable 或 balanced。外部源与汇设施不计入，相同意图、哈希使用既有有限执行器。

<a id="governor-pre-execution-throughput-declaration"></a>
### Governor 执行前吞吐声明

`validationBaselineProposalHash` 在任何接受写入或源变化前、同 revision、3600 tick 内锁定服务端采集的就绪、健康、可归属提案。基线、目标、容差、时长、周期、配方规模变为不可变。每受管身份最多八个锁，受保护归档最多 64 KiB，发布要求刷盘原子持久化与 `declarationDurable=true`。不接受编辑归档或客户自填历史。

健康计划重启只有在覆盖锁时刻、相同受管身份、版本、Journal 连续，且已消耗 ticket 下限覆盖锁时才能恢复声明。隔离、检查点或锁前恢复不能制造资格，采样始终从零开始。

`throughputValidation` 对连续原生覆盖仅计一次，重叠、重复、墙上时间不加信用。一 tick 缺口、零、超容差、不可用输出、健康或归属失败、回滚清进度。≥36000 tick 证明请求的十分钟；`doubleThroughputTargetObserved` 还要求精确两倍目标、容差 ≤10%。`balanced` 保持 false，守恒、持续供给、运输和保存恢复仍是独立门槛。

<a id="blueprint-data-and-native-upgrades-04-development-slices"></a>
<a id="blueprint-data-finite-construction-and-upgrades"></a>
## 蓝图数据、有限建造与升级

检查接收明确代码；导出选择距首对象 64 m 内最多 64 个正数对象及新端点、配方证据。不扫描文件，不复制货物。原生签名与逐字节导入导出往返必须通过。上限：代码 256 KiB、压缩 128 KiB、解压 1 MiB、64 对象、8 区域、256 格尺寸、每对象 128 参数，仅限支持格式。

数据子集：`2302–2305` 机器、无标签 `2001–2003` 带、受支持 `2011–2013` 分拣器、受支持未堆叠 `2101`、`2201`、`2203`。未知类型、设置、内容、地形改造或错误引用拒绝整个输入。`untrusted*` 元数据是数据，不是指令。

检查保持 `blueprint_inspection`、`executable=false`。可选选址最多 32 对象、64 m 半径、闭合内部分拣器端、无环带依赖；完整库存、解锁、距离、地形、回归线、占用、原生容量、条件都必须满足，不隐式外连、覆盖、升级或拆除。

有限 prepare 绑定精确代码、选址、评估，或 `resumeBuildId`、新进度，不替换代码和选址，附新玩家哈希和 `maximumObjectsToSubmit=1..32`。`foundryIntent`、`expectedFoundryPlanHash` 进一步重查完整图、成本、运输、电力。Commit 返回 action/build ID，每 tick 最多提交一个依赖满足预建造，使用正常成本与无人机。

逐对象状态：`not_submitted`、`submitting`、`pending_construction`、`completed`、`blocked`、`outcome_unknown`。批次成功暂停不等于全完成。受保护进度最多 32 计划、4 MiB，创建前刷盘预写，之后持久化精确成本、身份、拓扑。汇总不提供新哈希；精确 build 读取先对账持久化新增完成才暴露。重启恢复回执而非令牌，只继续未提交工作。取消只停未来提交，保留已有工作；未知隔离写入。

升级通过 `DoUpgradeObject` 仅支持制造台更高档或 `2011→2012`，绑定分拣器两真实连接、跨度、姿态、新配置和玩家状态。消耗完整新设备、返还旧设备，证明即时货物、增产点、拓扑、时序。制造台重置原生加工进度；基础分拣器按周期比例缩放。`upgradeReadback` 是即时证据，不是吞吐验收。

<a id="new-belt-centre-occupancy-containment-04-development-slice"></a>
<a id="native-belt-routing-mode-04-development-slice"></a>
<a id="native-belts-and-endpoints"></a>
## 原生传送带与端点

保留完整原生点、占用和端点绑定。NEW 点不能静默覆盖已有对象。`belt_path_existing_overlap` 是拒绝，不允许省 ID。声明覆盖分别证明不移除、零成本锚点，保留在原生检查与创建中。

自由地面路线支持 `native_grid` 或显式 `native_geodesic`。Geodesic 允许自由 `2001–2003` 端点或精确 `2001` 双覆盖子集，不是自动寻路。要求所选路由、完整路径回显；路径不证明两个分拣器连接。

地面 `2001` 开路拼接绑定精确端点身份、哈希、不同完整空开路及终点空输入。源 `whole_path_native_rotation_v1`、终点 `empty_open_path_native_geometry_v1`、至少两个 NEW 和精确成本必须匹配。唯一带供源子集单独证明一个无电、无缓冲 `2301` 钛矿机及唯一双向输出；泛化的有电、有货、侧馈、预建造供给拒绝。完成保留旧几何、成员和边，仅声明接头例外。

<a id="explicit-free-to-free-elevated-grid-span-04-development-slice"></a>
### 显式自由端高架网格跨度

`native_elevated_grid`：普通 `2001`、显式 0–3 层且至少一端升高、每层 1.3333333 m、吸附端点三维直线距离 1.5–30 m、4–64 完整点、两端平坦点对、坡度 ≤0.5。30 m 不是折线长。其他模式带高度字段拒绝，缺省不等于显式零。

唯一绑定子集为精确空独立高架 `2001` 尾、新源 ID、哈希、起点、自由终点及完整旧路证明。保留精确不移除覆盖，3–63 NEW 仍查成本、占用。不允许终点复用、有供给或带货源、任意拼接、垂直抬升、删点或旧组件拍平回退。后续段使用实际终态读回 ID。

<a id="native-bounded-belt-inserter-attachments-04-development-slice"></a>
<a id="initial-sorter-filter-during-normal-construction-04-development-slice"></a>
### 分拣器连接

候选筛选先于原生放置：双设备朝向误差 <14°，触带 <11°。带与设备包括 5.5 m、3.499 网格单位上限；带与带为 5 m、3.2。最小跨度、碰撞、地形、距离、材料仍适用。公布的有界单带、双带插值保留 quarter-turn、折叠 <40° 种子门槛，不保证连接。

`nativeChecks` 是真实放置调用数，不同于候选检查。分开保留 `lastNativeRejection` 和 `lastPreNativeRejection`。零次表示放置未知。初始过滤要求受支持建造与回显，已有过滤使用配置。

<a id="explicit-local-geometry-cut-and-complete-read-only-route-04-development-slice"></a>
<a id="geometry-cuts-and-read-only-routes"></a>
### 几何切片与只读路线

显式几何最多 256 带符号 ID、64 地形点、1024 碰撞体，绑定完整 `factoryGeometryHash`。已建带要求 `native_runtime_belt_collider_world_bounds`，缺失为未知。须纳入扩展分拣器边界、预建造和走廊风险，不能只看中心。

`geometry.nativeBeltSegments` 读取最多 16 显式自由跨度，每段最多 64 点、总 1024、1.5–30 m、支持的显式层。要求完整有序 `read_only_native_belt_segment_points_v1` 回显及 `nativeConditionsChecked=false`、`executable=false`。失败无部分点，不检查占用或放置，不提供估算回退。

`beltEndpointPreview` 资格检查一个自由 NEW `2001` 跨度、最多两真实端点及普通 `2011/2012` 分拣器、过滤、quarter-turn。`beltRoutePreview` 绑定两真实端点、冻结几何、最多 12 显式段、弦长总 240 m、192 个不同 NEW 点，要求完整路径 stage 1 及全部带与两个分拣器联合检查。不隐式寻路、覆盖、裁剪或使用旧参数。

二者要求 `prepared=false`、`commitAllowedNow=false`、`executable=false`、精确请求、点、绑定、哈希、预算回显且无令牌。成功响应也可有 guard 或原生 blocker；未检查不是通过，检查失败是拒绝。MCP 不放行替换普通令牌或不完整旧组件回显。通过也仅证明捕获时有界几何、材料、原生条件，不证明未来 ID、建造、电力、持续供给或许可。

<a id="warehouse-capacity-and-reservation-configuration-04-development-slice"></a>
<a id="configuration-transfer-and-dismantle"></a>
## 配置、转移与拆除

Storage-capacity 在未堆叠 `2101` 绑定完整状态，使用 `set-bans`、`lock-occupied`、`filter-empty-or-matching`、`clear-filters`。尾部禁格不冻结取出或 `TakeItem`。须证明投影过滤、数量、增产点、拓扑；紧凑缓冲不是原生格号。

Sorter-filter 绑定根配置及手持货物，支持无货 Returning 或普通 `2011/2012` Inserting。只改过滤、标记，已持货仍去相同目的地。已确认 inserter 的 null 读回表示无过滤，请求 `filterItemId=0` 清过滤；这不是清堵。

物流站 storage 只通过 `SetStationStorage` 配置无订单空槽或同物品槽；belt 选择未选的已连空输出；charge 采用精确原生范围、3 MW 步进。机队转移保留全部其他状态。转移移动真实库存，补燃料用 `AutoReplenishFuel` 和数量、增产点守恒，保存用内部主身份、`SaveCurrentGame`。

现有机队转移支持无人机5001、运输船5002和原生站点翘曲器1210，数量1–100。翘曲器要求普通 ILS、原生运输船曲速科技、真实 `warperCount`/`warperCapacity`、存入零增产点物品及精确玩家/机队哈希；采集站、PLS、未知或已满容量均拒绝。同一原 action 证明背包/计数器双边变化及其他机队/存储/能量保留；不扩大远端写范围、不创造翘曲器，未知转移不得换键重放。

拆除限公布的已建采矿机、普通 `2011/2012` 分拣器、孤立空默认 `2101` 或空 `2001` 链头。仓储要求全部 30 格为空、默认、无堆叠、连接、缓存引用及完整有界扫描；带要求 ≤16 带、512 格、空开独立双向路、无外部、预建造、缓存引用，中段、相连尾、有货、高档拒绝。一次 `DoDismantleObject` 后必须证明消失、精确返还与增产点、幸存对象和拓扑保留。不提供任意删除、瞬拆、自动重建或直接修连接。

<a id="protected-resume-and-import"></a>
## 受保护恢复与导入

远端 LastExit 续接的 Prepared 可选字段 `successfulFlightWitnessRetiredAtGameTick` 披露被主档进度覆盖后退役的成功出航 checkpoint。此字段只作历史来源证明：原成功 action/checkpoint/Journey/Journal 仍精确绑定，矛盾的失败/重载/退役事实即拒绝，完整固定 LastExit 仍须覆盖当前主档及票据最低 tick。退役记录包含在 fresh 证据承诺及确认 digest 中；不恢复 checkpoint 加载能力、不续授权或预算。

导入 prepare 绑定受限当前会话、revision 而不披露内容。只有之后明确回复才允许确认、边界声明与正常保存副本、接管；原始存档不能通过请求寻址。

恢复使用当前受保护 token。菜单 `gameLoaded=false` 正常，新 prepare 检查原生预加载、菜单、无 loader、精确源及 Journal，不加载。可用性本身不证明有效期或最终就绪。

既有远端隔离续接及其有界 V1–V3 来源开口仍使用 `recoveryMode=default`，普通 healthy/default 行为不变。下述独立命名的远端失败 LastExit 再授权不扩展这些来源代际。Fresh prepare 必须返回 `userConfirmationRequired=true`、精确票据绑定的 LastExit 的真实 `candidateGameTick`、`exactEmbeddedIdentityVerified=true` 及 `confirmationDigest`。执行方须有适用的明确所有者授权，并在 commit 前核本次 fresh 披露；commit 仍使用 `userConfirmedInConversation` 与匹配 digest。同类恢复的既有明确授权可以适用，无需重复相同询问；不能由此推导迁移、另一世界、原主档回滚或扩大范围的许可。

既有 `default` 窄提案可追加四个可选扁平参数：`recoveryDeadlineUtc`、固定 `recoveryScope=continue_original_finite_task`、`maximumChargeGameTicks`、`maximumChargeWallSeconds`。充能上限须为正且有界，最多18000 tick/300墙钟秒。Prepared 单列 `originalJourneyDeadlineUtc` 与 `effectiveRecoveryDeadlineUtc`，回显 scope/上限，并声明 `nativeChargeFeasibilityRequiresFreshCapture`，不承诺充能进度。所提有效 UTC 窗口不得超过原声明 UTC 跨度；凭据消费前绑定单一不可变提案、证据及适用明确授权下对本次新预览的确认；该授权须涵盖披露的期限与充能尝试。不得靠重键、更换 Host 或重建续期；原有效期、tick 最大值/已花值、返程预留及能量/翘曲器底线不变，普通 healthy/default 行为不变。

独立的 `recoveryMode=reauthorize_failed_remote_last_exit` 要求对本次披露的当前远端上下文有 fresh 明确所有者授权。`expectedRemoteRecoveryContextFingerprint` 须匹配下述八个独立提供值重新计算的期望承诺；Prepared 必须回显 `failedRemoteLastExitReauthorizationUsed=true` 及匹配的 `remoteRecoveryContextFingerprint`，否则 MCP 不发令牌。这不是 V4 来源开口或旧 grant 续期，必须保持 `priorFailedResumeProvenanceUsed=false`、`priorFailedResumeV2ProvenanceUsed=false` 及 `priorFailedResumeV3ProvenanceUsed=false`。原 action 已知终态失败仍是控制方核实后的事实断言，不是消费记录提供的 Native 持久结果。只接受 active 且未消耗的双凭据及直接两份固定记录，不追溯祖先。精确票据绑定 LastExit 的源 tick 须等于当前票据下界且严格晚于最近落地 tick，既包含原加1边界，也允许其后的有限本地 elapsed；已核销 tick 与成功 checkpoint tick 仍均等于该落地 tick。每个源减落地 tick 都须计入原预算；这不核销旧 unknown action，也不证明整个 journey 零推进。完整 checkpoint/primary/Journal 证据持只读租约，禁止较新主档回滚检查保持；成功 checkpoint 仅作证据，不作加载源。该模式复用相同四个扁平窗口参数及固定 scope；两项充能上限仍不超过18000 tick/300秒且不得超过直接前次提案。既有 UTC 跨度上限保持，披露的有效恢复 UTC 不修改原旅程 deadline，也不重置最大/已耗/返程 tick 或能量/翘曲器底线。适用明确授权及本次 fresh 披露/digest 仍必需。不新增工具，原 bootstrap、足部、自然首帧、strict13 及独立 fresh 返航核心门全部保持；仅源码实现不代表地面修复或重试许可。

可在离线调用公开 `VoyagerFailedRemoteReauthorizationPolicy.ContextFingerprint` 计算所有者独立提供的期望承诺，domain 为 `voyager-zero-progress-remote-context-v1`。八个参数顺序固定如下：

1. `journeyJson`：精确 Native journey 记录 JSON。
2. `checkpointJson`：精确 Native checkpoint 记录 JSON。
3. `checkpointNativeFingerprint`：完整 checkpoint Native 存档指纹。
4. `primaryFingerprint`：完整受保护 primary 指纹。
5. `sourceTick`：经验证的源 Native 存档 tick。
6. `primaryTick`：经验证的受保护 primary Native 存档 tick。
7. `journalCheckpointJson`：票据 Journal checkpoint 的精确 Native JSON。
8. `validatedJournalHash`：完整连续性验证 Journal 文本的 `HashToken`，即其 UTF-8 文本的 SHA-256，表示为64个小写十六进制字符，不带 `sha256:` 前缀。

这些值须来自所有者在预览前独立保护的基线。记录 JSON 字符串须保留已安装 Native `PluginJson.Serialize` 的原输出及数值词元；不得在桌面解析/重渲染、归一化浮点数，或以只有 header/metadata 的证据替代任一完整文件指纹。Journal 须保留 Native 识别 BOM 后读取的全部已验证文本及空白/行尾，不得解析后重新序列化再哈希。公开 Core 函数返回带 `sha256:` 前缀的承诺，不依赖 Lab helper；它只计算确定性数据绑定，不验证或签发所有者身份、授权或 Native 失败证书。租约、身份/版本及实机门仍由 coordinator 独立验证。本次预览的 Plugin 计算回显本身不能充当所有者独立 expected 基线；独立证据缺失仍为未知并阻止该模式。

仅在严格接地准入下起步的原活动 Move，才可在单独接触缺失时最多保留原订单8个真实 Native tick。每 tick 须 fresh 证明全部非接地前提及14P储备：当前 tick 加原13 tick停止尾部。只有真实 sphere 命中才清除 gap，越界即走原停止路径；停止尾部缺地使任务失败保持。已知失败仍须 fresh 接地 Walk 与实际零速度，否则结果保持 unknown。此分支不扩 idle、施工、Harvest、飞行、期限或返程储备。

独立的旧本地 Move 未知结果兼容输入仅用于 `recoveryMode=default` 及旧源码组件组 `12e6dfcaaae923c844cc3ae0c2b66c185537b93a`。`legacyLocalMoveUnknownEvidenceJson` 与 `expectedLegacyLocalMoveUnknownEvidenceHash` 须同时提供或同时省略。哈希为精确不可变、严格 UTF-8 编码 JSON 字节的 SHA-256，以无前缀64位小写 hex 表示；整个 carrier 最多64KiB，不接受文件路径。外层对象精确八字段：`sourceProductSha`（该兼容组件组）、`captureAttested`（true）、`actionResponseJson`、`sessionResponseJson`、`ticketSha256`、`journeySha256`、`checkpointSha256`、`journalSha256`。两个响应字符串保留受保护原公开 `success=true`/`result` 响应文本，不用 readback 投影；必要字段核类型，正常 DTO 附加字段仍可保留，重复 JSON 属性拒绝。四项哈希绑定完整原文件字节；票据双副本须字节相同，Journal 哈希包含原文件全部 BOM、空白及行尾。

旧源码版本、动作种类与失败原因由调用方根据原采集作事实断言：仅终态 `move`、`outcome_unknown` 且 `voyager_local_native_tail_unproved` 可用。哈希或附带原响应都不认证历史 Native 动作结果或已安装版本。Prepared 须回显 `legacyLocalMoveUnknownProvenanceUsed=true`、精确 `legacyLocalMoveUnknownEvidenceHash`、`legacyLocalMoveUnknownKindCallerAttested=true`、`nativeOriginalOutcomeProved=false`，并明确披露这一信任边界。此前失败来源/终态失败及远端再授权回显仍为 false；本输入不得与这些来源输入、verified-newer 或 autosave 模式混用。MCP 不向不支持或不匹配的预览发令牌。Commit 复用原不可变 Native plan token 及匹配 confirmation digest；适用明确授权须涵盖当前披露。

Native 独立核当前精确隔离 action/票据、源 session、同 owned 身份/版本、完整 fresh 票据绑定 LastExit、当前 primary、成功去程 checkpoint/journey 及连续 Journal，并持只读租约；源进程须已正常结束，加载前先持久消费凭据。Commit 重新打开并核全绑定及 carrier 哈希。原 Move 仍为 unknown，不伪造 known-failed/settled 结果、不重放、不改 journey 活动 action、不加载成功 checkpoint。任何有限恢复窗口提案仍保原 tick/已花/返程/能量/翘曲器上限及单独披露的有效 UTC，不续已关闭调用方窗口。既有首帧/接触/scene/2P/strict13 门仍全部必需；此来源输入不保证地面准入或恢复成功。

单次旧来源兼容开口可同时提供 `priorConsumedResumeToken`、`priorSourceFingerprint` 及可空 `priorActionKnownTerminalFailed`，或三者全不提供；仅适用于此 `default` 窄分支与当前隔离。`true` 表示控制方已核精确原公开恢复 action 为终态失败且非未知；消费记录不是 Native 持久动作终态，未知绝不能作此断言。两份固定消费/授权副本须在只读租约下相符、每份不超过64KiB，具完整原版本授权、匹配的 journey/checkpoint/Journal/当前主档来源与同一 source tick，并保留全部禁止较新主档回滚检查。旧不可变提案与 prior 指纹只用于打开旧 v1 证据指纹，不执行旧提案/action，也不复用其预算。Fresh Prepared 须回显 `priorFailedResumeProvenanceUsed=true` 和 `priorActionTerminalFailureAttested=true`，原确认/digest 门保持；回显缺失或被忽略则阻止兼容。新提案须有当前适用授权；新授权记录标记本次 hop；原三参数路径仍拒绝已标记前驱。原最大/已花/返程 tick 与能量/翘曲器储备不重置，直接路径及普通 v1 路径不变。token/指纹实际值与记录 JSON 留在保护区。

显式 V2 前驱开口还要求非空 `priorLegacySourceFingerprint` 与原三个字段齐备。`priorSourceFingerprint` 指向直接前驱源，新字段指向它更旧的 legacy 祖先。四份固定有界记录均持租约：直接前驱一对与旧祖先一对，各对内字节相同、每份不超过64KiB，旧祖先不得有兼容标记。当前隔离须匹配公开已知终态失败的前驱 action 及控制方断言；两层来源均须在同一 source tick 打开精确 route/checkpoint/Journal/完整源/当前主档证据，并保留禁止较新主档回滚及全部原预算检查。Prepared 除原来源/终态断言回显外，还须返回 `priorFailedResumeV2ProvenanceUsed=true`；必需回显缺失或 false 时，MCP 不发令牌。新指纹域绑定全部证据，不改变 v1 玩家哈希。后继不能再作 V2 前驱，因为其祖先已带标记；此有限开口不是任意链、旧动作重放、推进 tick 或续预算。不提供新字段时，原三参数行为不变。

显式 V3 零进度来源开口还要求非空 `priorIntermediateSourceFingerprint`、全部原 prior 字段及已知终态失败断言。`priorSourceFingerprint` 是最新前驱源，`priorIntermediateSourceFingerprint` 是中间源，`priorLegacySourceFingerprint` 是最旧 legacy 源。六份固定有界记录持租约，组成三对字节相同副本及三个精确证据开口，最旧祖先未带标记。当前隔离须匹配精确失败前驱 action；三层 route/checkpoint/Journal/完整源/当前主档 witness、零进度/source-tick、禁止较新主档回滚检查及原预算均须成立。Prepared 必须返回 `priorFailedResumeV3ProvenanceUsed=true`，原来源、V2 及终态断言回显也均须 true；V3 acknowledgement 缺失/false 时 MCP 不发令牌。新域绑定全部三层 witness；后继不能再作 V3 前驱，第四代拒绝。新输入为空时保留 v1/V2 行为。证据开口本身不推进 tick，获准自然首帧须另受守卫并计费；不授予旧动作重放、未知断言、任意链、重开已耗时/上限或额外授权。

受限的已注册 Native 地形分支保留精确当前 surface 的正接触及全部原 bootstrap 前提。只有鲜活精确 world/star/注册 slot/ready 绑定、唯一精确引用归属与已证 sphere-disabled 原生分支前提齐备，才可计入本地星的其他 simulator；上限16个 simulator、各256个 surface，原257结果查询仍有界。正常 Native 注册可能早于真实 Universe(151) 的定位/层生命周期，这种可能性本身不为已采集 hit 定归属。真实151完成后，须 fresh 证明已注册非当前 surface 及其存在的 ocean 已禁用、sphere 已启用并完成预期 Native 定位，且无残留非当前足部占用；同时核当前 layer9、仅当前接触与 fresh ground。随后才可经真实501继续原 strict13，当前601余部仍须足额资金覆盖。复用既有首帧证据，不直接调用 SetLayers、UniverseTick、Simulate 或 gameplay 姿态/能量 setter。未知或生命周期后置条件未满足时，只对精确 owned 世界拒绝/Pause/隔离。已退役的 all-current-query 尝试仍退役，本源码适配不授额外盲 Load；既有六项可选恢复输入、fresh 模式回显及固定八字段上下文承诺不变。

加载完成后，初始化最多等待 8 次 host pump / 2 秒墙钟时间；准确 owned 世界保持 Native 暂停，source tick、actor、物料/能量及证据租约均保持不变。集合为空或明确仍待碰撞体刷新时，等待普通 Native Update/LateUpdate 稳定后才首次绑定支撑。就绪要求集合非空且明确无待刷新；刷新状态未知即拒绝。fresh resume 时若再次待刷新也拒绝，后续观察或重新绑定不能消除此拒绝。不手工调用 Update/Init/LateUpdate、不替换保留引用，也不把 pending 当作 idle。可空 `registeredColliderCount` 只是有限范围只读计数，不证明归属或接地；未观测仍为 null。fresh 初始化证明后至多一次 NativeResume，再走既有 151 到 501 / 至多 8 个实际 601 / 2 秒 / 14P 接触尝试；不预设实际计数或恢复成功。

既有首帧失败 code 保持不变。Core 内部 `FailureDetail` 随首次失败锁存固定标签的首个失效前提；既有 action `Message` 保留 `[原 code]` 并附 `First failed premise: ...`。细因来自同次真实 151/501 对保留 player 精确不变、scene/terrain、support 及阶段顺序的实际判断，不放松任何检查。缺失或尚未到达的观察仍未证明，不能推断为 false 测量或已通过。不新增 DTO、工具或 schema；既有公开 bool API 保持兼容，细因不改变 canonical hash 或授权，也不授自动重试。使用 Native 替身、链接生产谓词的回归不证明 Native 实机健康、已安装或恢复成功。

Native Import 可将 double 宇宙坐标逆投影为 float 本地位置；普通 LateUpdate 以 `relativePos + Maths.QRotateLF(relativeRot, p.position)` 回算，因此不保证与存档 double 逐位相等。仅精确加载的首帧路径在 Arm 时冻结同一 Native frame 及其精确投影端点。首次真实 151 只接受保留原值或该端点；其余本地姿态/刚体/input/能量/world/scene 及 source+1 门不变。同一次 151 完整判断通过后才锁定实际观察到的选中值，首次 501 必须精确匹配该锁定值。禁止 151 后换 frame、引入容差、任意重绑状态或写 Native 位置；普通 strict 检查及 paused hold 仍为原精确检查。接地、严格 idle、原能量/返程及正常 Save 门仍必需；此源码改动不新增公开工具、DTO 或授权，也不证明实机恢复成功。

精确恢复加载后，可在原预算内作一次有界接触尝试：至多8个实际 Native 601 tick（包含失败 Pause 当帧）及2秒墙钟时间。每步先经当 tick 真实151核验，再由同 tick 真实501消费；普通 Update 不消费步数。在该501 fresh 核同一 world/scene/当前支撑、全部非接地前提与成本、14P储备（当前 tick 加原13 tick停止尾部），以及原 elapsed/spent/max、返程/能量/翘曲器及 UTC 限制。只有实际 Native sphere 接地并且严格物理空闲，才可接入既有补能/Save链；守卫失败或尝试耗尽则 NativePause 并隔离。不写 gameplay 姿态、速度或缓存接地状态，不重放旧 Move 或失败 Load，不新增工具或参数。原已注册地形、确认及较新主档保护保持；源码实现不代表实机验收。

仅精确 owned、精确票据绑定的 LastExit 接管，在真实足下探测命中精确 owned 地形碰撞体 layer31，且其余模式/scene/输入/命令/UI/身份/原源文件前提全过时，才可有条件放行一次自然调度的首帧。已核足原2P储备的 bootstrap 及全部已耗时/充能/loaded返程/tick/UTC/翘曲器预算须已成立。此路径不调用 SyncTransforms、不手动调用 Native Update/GameTick，不写 gameplay 变换/速度/能量。须观察真实 Native post-Universe(151) 地形/接触结果，再经真实 pre-player(501) 检查；预检不冒充已观察任务轮次或 idle 准入。即使已请求 Pause，当前 Native 帧仍可能跑完 player(601) 余部，其费用仍须有足额储备并计入 Native 实际读回。接触及 bootstrap 前提成立后，仅原有有界、逐 tick 守护的充能可继续，直到真实501证明 strict13；独立 fresh 返航核心门不变。仍有未证前提就保持精确 owned 拒绝/Pause/quarantine。一次首帧不重开已耗时/上限，也不证明地面已修复、物理空闲或恢复成功。

原生加载前绑定固定源与当前主档的完整文件证据、精确同版本 owned/和平身份、原 Journal 及已成功 checkpoint/journey 来源；任何不可变恢复窗口授权须先持久化，再持久消费受保护凭据。成功 checkpoint 只作证据，绝不是加载源，也不伪造旧 `ActionRecord`。精确新 Native GameLoader 在 frame10 已完成且禁用的 `onLoadComplete` 同步回调，须在 Begin/InitLogic 后、首个 player(601) tick 前绑定新 Native logic，证明 owned 身份、原 Journal 与闭合 bootstrap 包络。在实时证明的运行前提下，bootstrap 要求核心至少2P、loaded 能量至少为受保护返程底线加2P；队列及可能无人机派发须闭合，独立护盾/激光扣费须被排除或由同一已证明的 P 覆盖。充能只使用新加载世界中自然运行的 Native GenerateEnergy，绝不 Resume 原隔离世界。该预检不伪称 Native tick 可用，也不发布 idle owner；证明未决禁止自动主档重存及本地/结构化写入。真实 Native pre-player(501) 须逐 tick 守护 bootstrap，只有某次证明原完整 strict13（核心至少13P、loaded 至少为原受保护返程底线加13P，以及原 tick/墙钟/翘曲器门）后，才可准入 idle owner、释放租约并允许重存，紧邻重存前仍须再核严格条件。原返航核心最低值仍是独立 fresh 返航起飞门，不追加为恢复接管条件。可行性须依 P/g 及 fresh 预算判定，不保证自然充能、后续 Move、任务或返程完成，也不降门或续预算。事后 Update 或轮询不能补首 tick 证明。充能在声明的 tick/墙钟任一上限先到时停止，且仍受原五分钟总接管上限约束；进入充能或更换 Host 均不重开。失败只暂停精确 owned 世界并隔离，不证明物理空闲，也不授予自动 Resume、Load 或重放。保留原 UTC 原件、最大/已花/返程预留 tick 及核心/loaded/翘曲器底线；原剩余储备不足则拒绝。

恢复模式、证据 1–6、精确候选、下限、确认、租约、消耗、迁移见[安全模型](safety-model.zh-CN.md#protected-recovery-and-version-changes)。有效迁移只允许 `29104→29131`/5 或 `29131→29145`/6，过期主模式只允许已研究有向链，包括 `29145→29151` 和经过证明的直达 `29088→29151`。Journal 最多五次持久转换，只记录真实接管并保留全部旧链，默认仍限同版本。不增加选择器、回退、ticket 编辑或消耗后自动重放。

飞行 commit 先验证独立内部检查点。只恢复匹配失败或中断飞行，稳定到达或覆盖主进度会使能力退役。过渡时 `NO_LOCAL_PLANET` 不允许再飞行或加载。

<a id="action-semantics"></a>
## 操作语义

修改为新 prepare → 精确校验 → 幂等 commit → 同 action 终态 → 双向、材料读回。写关闭时 prepare 仍只读，只读评估绝不是 commit 计划。

操作可能执行、等待、完成、失败、要求恢复、未知。检查 `terminal`、`succeeded`、身份、delta，而不是工具级成功。`ACTION_OUTCOME_UNKNOWN` 需要原 key 与结果对账，新 key 不能猜重放。单飞、隔离跨 Pipe 重连保留。

Move 地表预览是观察，不是寻路，180/600 tick 失败指导禁止重复目标。被动漂移保留独立可信检查、时间、位移限制。之后就绪失败不撤销成功，revision 不是接受操作计数。

源码依据：[Contracts](../src/Spherewright.Contracts)、[MCP 工具](../src/Spherewright.Mcp/Tools)、[Core](../src/Spherewright.Bridge.Core)、[原生适配器](../src/Spherewright.Plugin/Game)。早期细节：[已验收协议](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/protocol.md)、[Lab 固定参考](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7)。离线契约不证明已安装或实机验收。
