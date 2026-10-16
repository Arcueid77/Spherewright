# Foundry 与 Governor DSP API 研究

[English](game-api-foundry.md) | [简体中文](game-api-foundry.zh-CN.md)

## 范围与来源

本文维护有界 Foundry 施工和 Governor 分析的产品研究。[完整不可变原文](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/research/game-api-foundry.md)保留原生签名、几何反例、带日期离线/实机边界和退役阶段记录。多数初期研究使用 DSP `0.10.34.28529` / 原生 SHA-256 `AE0BA95F75BD879A62AA4CE253B2AB78EAA4FB3C7C595F5E1FEE75EBE0E0EF85`；高架源绑定修正使用 `0.10.35.29131` / `74AC6162CEF8394A6021C8475132CAE4DF4F840871A99E63CC7D2C8297661BC0`。这些历史基线不是当前通用兼容声明。

已验收本机候选为源码 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`、DSP `0.10.35.29145`。[最终验收](../validation/v0.4.0.md)保留有界模块/Governor 证明、独立三级链中途与整体清单恢复，以及最终持续供给证据。旧“未安装/待实机”文字保留历史语义，本次重写不安装、不执行原生动作、不新增验收。

## 确定性物料编译

`FoundryPlanCompiler` 复用 `RuntimeDependencyGraphBuilder` 和当前运行时配方/建筑目录。显式配方/建筑选择优先；确定性候选依次按单输出配方、配方 ID、建筑等级、建筑 ID 选择。原料在未显式选配方时终止递归，其他外供中间物须明确列出。循环/不可用选择拒绝；先汇总共享需求再计算上游设备，并列出副产物出口，不把它们当免费输入。

研究确认 `PrefabDesc.assemblerSpeed/labAssembleSpeed`、`workEnergyPerTick` 与 `RecipeProto.TimeSpend/Items/ItemCounts/Results/ResultCounts`。在 60 游戏 tick/s 下，无增产满电容量为：

```text
件/游戏分钟 = 3600 × prefabSpeedRaw × outputCount / (TimeSpend × 10000)
工作瓦数 = workEnergyPerTick × 60
```

未知速度不猜为 1×，设备配方类型必须匹配。编译器不模拟采矿、分馏、增产奖励或实际交付。限制为 64 阶段、16 层配方、256 设备，预算有界且检查溢出。material hash 描述可复现计算输入，不是当前状态、所有权或写权限。

## 只读数据、场地与基础设施检查

`BoundedBlueprintReader` 接受明确代码/当前 owned 选区，将文字元数据视为不可信数据。限制包括 256 KiB 代码、128 KiB 压缩、1 MiB 展开、64 对象和八区域。独立 `BlueprintSitePolicy` 放置限制为 32 对象。蓝图 inspect/export/site DTO 仍为 `executable=false`，读取不等于施工。

`GameStateReader.Foundry` 与 `NormalGameActionCoordinator.FoundryPreview` 复用原生 click/path/inserter/blueprint 放置逻辑，不发玩家建造命令。`FoundrySitePlanner.CheckPlannedClearance` 比较新规划设备之间，不是给所有旧建筑施加球形排斥。已有对象有向碰撞体拒绝与 DSP `CheckBuildConditions` 是不同阶段。`nativeCheckPerformed=false`、`NotEnoughItem` 或材料预算均不能证明场地完整通过。

`FoundryPowerPlanner` 使用复制后的原生节点/网络/消费者、真实合网和满基础负载预算，覆盖未知或既有未完工预建筑负载不明时拒绝。原生放置哈希与当前供电哈希分离。`FoundryTransportPlanner` 按带/分拣器容量、共享边界分配有向物料兼容流。理论预算不是公平分流、上游稳定供给或实测吞吐。

## 原生几何与普通配置

`BuildTool_Click/Path/Inserter`、`BlueprintUtils.RefreshBuildPreview` 和 `PlanetFactory.ReadObjectConn` 是原生施工/读回基础。研究保留以下区分：

- 设备距离、随纬度变化的 `PlanetGrid.CalcSegmentsAcross`、朝向、倾斜、碰撞、距离、材料和原生放置是独立要求。重建跨度是离线推断，不是原生仪表读数或批准。
- 分拣连接仅考虑显式端点/段、有限候选和原生 offset，不扫描邻带，不清除 `TooSkew`/`ErrorInserterData`。不能以任意扩边替代原生碰撞体。
- 源覆盖/目的复用和高架跨度是不同显式子集。源覆盖绑定精确旧源、未变且空/开放/无供料的货路、空目的、非移除零成本源覆盖与正常收费 NEW 点。创建/完成证明实际双向连接、有序成员和旧几何/边保持，不做任意裁带、路由或目的复用。
- 历史高架创建错误将包装后的源/空路绑定与未包装基础绑定比较。修正在创建前以同层完整绑定比较，同时保留原基础绑定的创建后证明。这解释一个受支持的创建前失败路径，不声称已观察异常抛出行。原隔离动作不得借“修复”重放。
- 普通仓 `bans` 阻止正常输入，不阻止提取。`BuildingParameters.FromFactoryObject` 导出设置/过滤，不导出货物；原生建造应用前验证容量、模式和 ID。分拣过滤变更保护持货及精确端点配置。

## 有限施工、持久化与升级

`BlueprintBuildState`、`BlueprintBuildStore` 和 `NormalGameActionCoordinator.BlueprintBuild` 复用正常 plan/session/revision/identity 及幂等/隔离存储。通过原生路径提交有界、依赖就绪工作，正常付料并等待无人机。带输出只投影到合格带连接，分拣器投影双端，机器创建不让分拣边覆盖带输出。每份收据证明新身份、成本及双向/自由端拓扑。

受保护 write-ahead/结果证据绑定精确 owned 身份和不可变意图/几何。部分失败保留成功前缀，取消只停未提交工作。重启不恢复 token：先检查真实实体/材料收据/拓扑，再 fresh prepare。未知结果继续隔离。`BlueprintProgressVersionPolicy` 仅在精确连续 Journal 转换后保留 29131→29145 的已证明完成历史，不续建部分/未知工作，也不清空存储。

`PrepareUpgradeOnMainThread`/`ExecuteUpgradeOnMainThread` 调用原生 `DoUpgradeObject`，不赋原型。可执行子集为普通制造装配机与基础 `2011 → 2012` 分拣器。要求高阶物品/返还空位、原生资格、精确配置/货物/连接及调用后守恒；装配机重置加工进度，基础分拣器保留缩放周期比例。带路径解析/升级证明属于研究，不开放带/高级分拣器动作。

## Governor 测量与限制

`GovernorPlanCompiler` 比较升级、新建和显式模块复制。`GovernorSourceBinding` 哈希静态身份/配方/设置/端点/网络，不哈希动态货物，库存差独立测量。目标需求、实际产消、当地可分配余量和缺口是不同量。

`GovernorMeasurementSeries` 要求 fresh、独立、稳定非零基线窗。`GovernorThroughputValidation` 在执行前锁定目标/误差/窗口，按覆盖游戏时间并集计时，失效条件、来源变化或缺口重置。最多八个被动 session 观察器不构成自主规划或持久施工权限；理论收益不是实测收益。批准的两倍/十游戏分钟门不能靠模块数或事后放宽误差通过。

最终候选的限定验收不开放任意蓝图、全部原生放置模式、全部升级族、配送器动作或实际跨恒星飞行。完整原生方法证据保留在上方链接，精确场景 ID、tick、收据属于 Lab，不进入此可复用研究。
