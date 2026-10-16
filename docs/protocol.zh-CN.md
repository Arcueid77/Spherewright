<a id="bridge-protocol"></a>
# Bridge 协议

[English](protocol.md) | [简体中文](protocol.zh-CN.md)

协议版本为 `1`，原生兼容候选面向 `0.10.35.29151`（[离线证据；实机检查待完成](validation/native-29151-preflight.zh-CN.md)）。本文描述当前维护的源码，不宣称已安装组件状态。已验收 0.4.0 候选固定于 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`，最新正式发布仍是 v0.3.3。参见[产品状态](project-status.zh-CN.md)、[安全模型](safety-model.zh-CN.md)和 [Agent 指南](agent-playbook.zh-CN.md)。

<a id="framing-and-authentication"></a>
## 帧与认证

每条 Pipe 消息由四字节小端有符号长度加 UTF-8 JSON 组成。负长度及超过 1 MiB 的负载在分配内存前拒绝；零长度帧进入 JSON 校验后拒绝。

每次连接先进行认证 `handshake`，携带当前用户保护的运行描述文件中的有效 `bridgeInstanceId` 与启动 token。凭据错误或认证前请求会关闭连接。Pipe 与描述文件凭据属于受保护传输细节，不是公共证据。

请求包含 `protocolVersion`、`messageType=request`、UUID `requestId`、可选 `sessionId`、`method` 与 `payload`。响应回显请求身份，带 `success` 和 `result` 或结构化 `error`。传输成功不等于原生校验通过、commit 接受或操作完成。须保留明确错误码、可重试性和失败阶段。

<a id="current-public-mcp-surface"></a>
## 当前公共 MCP 接口

```text
spherewright_get_status
spherewright_get_session_state
spherewright_get_player_state
spherewright_get_progression_state
spherewright_get_gameplay_journal
spherewright_get_local_star_system
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

受管存档、玩家、工厂读取要求活动 `sessionId`，绑定星球的请求还要求 `planetId`。正常操作始终限当前本地星球。实体检查额外允许明确选择当前受管恒星中已创建、身份验证的模拟驻留工厂，但从不创建或加载工厂。玩家读取仍使用当前 `localPlanetId`。

检查使用 `objectId`，正数为已建实体，负数为预建造。零、缺失及 `Int32.MinValue` 拒绝；其他工具的 `entityId` 或 `storageEntityId` 不能替代。未完成或过期分页不证明不存在。

修改 prepare 绑定当前会话、revision、`GameData`、操作、身份、参数、预算和要求的哈希域。Commit 使用短期 `planToken`、UUID `idempotencyKey` 及规定的会话、星球绑定。菜单创建与恢复有独立 schema。

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

受管 Journal 分别报告首次手动、产线产出、科技与升级选择、跟踪边界、持久序号及持久化状态，不暴露原存档身份或路径。`attached_existing_save` 保留历史覆盖不完整；起始、有效版本与转换分开呈现。

<a id="overseer"></a>
## Overseer

生产读取接收 1–64 个唯一运行时物品及 1–16 分页大小，采集已创建受管工厂，使用自动原生 level-0 `total[0]/total[7]` 滚动 600 tick 计数，排除玩家与机甲累计输出。理论速率来自有界原生组件，不用 UI 缓存。

诊断保留直接生产者覆盖、总数、截断和有界真实上游追踪。短窗零产出一般要求完整周期。停止且有电的 `output_blocked` 例外使用下一批准入阈值，不是物理仓储容量：熔炼 `101-q`（`q≤100`）、制造 `9*q+1`、其他 `19*q+1`。速率、电力、容量未知时不合格，无诊断也不证明持续供给。

汇总区分发电、容量、需求、外送，并限制电力、物流、研究扫描：512 工厂、4096 电网、65536 发电机引用、4096 站槽、每站 64 存储槽、12000 科技。不创建或加载远端工厂，超限失败，不静默截断。

诊断包在一个主线程任务中采集生产和汇总，仅合并精确工厂、星球、标志、tick；schema `1`、`public_allowlist_v1` 不含凭据、存档身份或路径。专用不可变分页保留 60 秒。

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

拆除限公布的已建采矿机、普通 `2011/2012` 分拣器、孤立空默认 `2101` 或空 `2001` 链头。仓储要求全部 30 格为空、默认、无堆叠、连接、缓存引用及完整有界扫描；带要求 ≤16 带、512 格、空开独立双向路、无外部、预建造、缓存引用，中段、相连尾、有货、高档拒绝。一次 `DoDismantleObject` 后必须证明消失、精确返还与增产点、幸存对象和拓扑保留。不提供任意删除、瞬拆、自动重建或直接修连接。

<a id="protected-resume-and-import"></a>
## 受保护恢复与导入

导入 prepare 绑定受限当前会话、revision 而不披露内容。只有之后明确回复才允许确认、边界声明与正常保存副本、接管；原始存档不能通过请求寻址。

恢复使用当前受保护 token。菜单 `gameLoaded=false` 正常，新 prepare 检查原生预加载、菜单、无 loader、精确源及 Journal，不加载。可用性本身不证明有效期或最终就绪。

六模式、证据 1–6、精确候选、下限、确认、租约、消耗、迁移见[安全模型](safety-model.zh-CN.md#protected-recovery-and-version-changes)。有效迁移只允许 `29104→29131`/5 或 `29131→29145`/6，过期主模式只允许已研究有向链。过期主模式仅新增已研究的 `29145→29151`，Journal 最多五次持久转换，默认仍限同版本。不增加选择器、回退、ticket 编辑或消耗后自动重放。

飞行 commit 先验证独立内部检查点。只恢复匹配失败或中断飞行，稳定到达或覆盖主进度会使能力退役。过渡时 `NO_LOCAL_PLANET` 不允许再飞行或加载。

<a id="action-semantics"></a>
## 操作语义

修改为新 prepare → 精确校验 → 幂等 commit → 同 action 终态 → 双向、材料读回。写关闭时 prepare 仍只读，只读评估绝不是 commit 计划。

操作可能执行、等待、完成、失败、要求恢复、未知。检查 `terminal`、`succeeded`、身份、delta，而不是工具级成功。`ACTION_OUTCOME_UNKNOWN` 需要原 key 与结果对账，新 key 不能猜重放。单飞、隔离跨 Pipe 重连保留。

Move 地表预览是观察，不是寻路，180/600 tick 失败指导禁止重复目标。被动漂移保留独立可信检查、时间、位移限制。之后就绪失败不撤销成功，revision 不是接受操作计数。

源码依据：[Contracts](../src/Spherewright.Contracts)、[MCP 工具](../src/Spherewright.Mcp/Tools)、[Core](../src/Spherewright.Bridge.Core)、[原生适配器](../src/Spherewright.Plugin/Game)。早期细节：[已验收协议](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/protocol.md)、[Lab 固定参考](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7)。离线契约不证明已安装或实机验收。
