# 普通世界 DSP API 研究

[English](game-api-m0.md) | [简体中文](game-api-m0.zh-CN.md)

## 范围与来源

M0 是历史普通世界 First Red Matrix 能力锚点。初期原生检查使用 DSP `0.10.34.28529`、`Assembly-CSharp.dll` SHA-256 `AE0BA95F75BD879A62AA4CE253B2AB78EAA4FB3C7C595F5E1FEE75EBE0E0EF85`，以及针对性 ILSpy/Mono.Cecil 检查。这是历史来源，不是当前支持版本。已验收 `0.4.0` 本机候选使用 DSP `0.10.35.29145`；见[环境](environment.md)、[精确版本研究](game-api-version-0.10.35.29145.md)及[最终验收](../validation/v0.4.0.md)。

[完整不可变原文](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/research/game-api-m0.md)保留带日期调查、签名与旧部署证据，含退役沙盒实验和当时待验结果。这些段落不授予当前动作权限，也不覆盖后续验收证据。反编译游戏源码、原始存档和私有身份不得进入产品仓库。

## 启动、所有权与观察

`SpherewrightPlugin.Awake` 创建受守卫保护的 Bridge host，`Update` 推进主线程工作。认证本地 Named Pipe 只暴露复制后的 DTO，不传递 Unity 对象。工厂读取和普通动作需要当前进程创建或明确认领的精确 `GameData`、session 和 revision。玩家载入的无关世界在独立导入边界成功前保持受限。新世界通过 DSP 正常 `DSPGame.StartGame(GameDesc)` 路径创建，默认和平/非沙盒/1×；模式证据缺失或战斗世界拒绝。沙盒/资源设置继续报告，不授予沙盒工具。

`GameStateReader` 读取工厂、库存、配方、仓储、电力、科研或物流前，绑定池/组件/实体身份。原型、对象号、文件名前缀或位置均不能单独证明身份。运行时配方/建筑/科技目录及依赖图提供当前前置，不能用记忆配方或猜测解锁替代。

per-save 玩法 Journal 记录已观察首次事件和持久检查点，覆盖起点始终明确，导入后尤其如此。原生科技完成可能正常奖励物品：`TechProto.AddItems/AddItemCounts`、`GameHistoryData.UnlockTech` 和 `GainTechAwards` 解释该游戏侧路径。`TechCompletionRewardCatalog` 只暴露有界复制元数据，不调用授予。仅元数据不能核销无法解释的库存增加；须核对完成事件、容量、实际交付及无关库存。

## 正常动作路径与不变量

`NormalGameActionCoordinator` 负责 fresh prepare、原生重验证、单飞 commit、幂等结果查询和读回/隔离。源码模块保留以下研究边界：

| 域 | 原生证据与维护边界 |
| --- | --- |
| 建造与连接 | `BuildTool_Click`、`BuildTool_Path`、`BuildTool_Inserter`、`PlanetFactory.ReadObjectConn`；正常扣料/创建预建筑与无人机完工，再证明精确新实体及双向连接 |
| 移动/采集 | 精确 owned 玩家指令、原生移动/距离/能量及有界资源身份；Move/采集提交单飞，进度/能源 watchdog 只停止精确指令，不传送或注入输入 |
| 科研 | 原生队列/选择和普通机甲研究缓冲；仅可优先排列已入队、前置完成且合格的目标，保留其他顺序/进度/库存 |
| 补燃料与转移 | 已拥有的原生物品、数量和增产点守恒，保留手持及无关背包；不授予能量或物品 |
| 仓储/过滤 | 原生仓储容量/过滤语义与普通分拣器配置；bans 限制普通输入而非冻结提取，保护已有分拣器持货 |
| 物流站 | `PlanetTransport.stationPool`、`StationComponent`、`StationStore` 及物理槽位；分别核对行星站 sentinel/星际身份、精确槽设置、电量、闲置/工作机队和货物 |
| 机队转移 | 已有 `5001`/`5002` 物品仅在玩家与对应闲置计数间转移，受 prefab 容量约束并保留所有无关站状态；不写站内货物 |

新分拣器施工归因排除已有同位分拣器 ID。`stateHash` 包含动态状态；`endpointStateHash` 绑定身份、位姿、连接，允许普通生产推进。不得混用哈希域，或把确定的错误域请求当成可重试现场变化。

## 保存、导入与飞行边界

`GameSave.SaveCurrentGame(string)` 是精确 owned world、内部命名授权副本/检查点的正常保存路径。`OwnedSavePrefixReader` 只解析受研究约束的有界前缀，比较完整嵌入身份但不暴露身份。历史源格式为 header `7`、GameData `13/patch22`、GameDesc `9`；研究中的 `0.10.35` 目标要求 `13/patch23`、GameDesc `10`。账号/截图字段有界跳过，不披露。前缀证明本身不授予所有权，也不验证整个存档。

`UserSaveImportCoordinator` 要求对精确受限当前世界进行无副作用 prepare，后续对话明确确认及原档/Journal 两项确认后，才向新服务端 owned 名称进行一次正常保存。不接受存档路径、不打开原档、不补造旧 Journal。保存返回成功却无精确 header 证明时，仍隔离/受限。

`OwnedWorldResumeCoordinator` 绑定受保护凭据、精确主档证据、Journal 连续和原生认领。默认恢复要求版本相同；只有明确研究模式才可在披露与后续确认后允许精确有向转换。不允许编辑票据过期/版本、任意选档、替换 LastExit/自动保存或消费后重放。

同星系飞行使用原生起飞/导航/着陆，起飞前正常保存并验证检查点 header。受保护生命周期将检查点绑定精确中断飞行；成功/主档覆盖保存即退役。这不是实际跨恒星支持，后者属于规划 `0.5`。

## 排除与未证明路径

`BasicProductionLineCoordinator.cs` 被显式排除 Plugin 编译，其历史物品授予、直接 `BuildFinally` 和缓冲插入不属于正常原语，也不得借 Foundry 恢复。物流配送器 add-on 与运输机原语仍是独立未支持缺口，物流站机队不证明它们。当前验收限于最终摘要中的有界本机候选，不承诺任意存档、全部 Mod、异机或通用原生 API 兼容。
