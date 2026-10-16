# DSP 0.10.35.29145 精确版本对持久化研究

[English](game-api-version-0.10.35.29145.md) | [简体中文](game-api-version-0.10.35.29145.zh-CN.md)

## 研究范围与后续验收

2026-10-15 研究仅支持 `0.10.35.29131 → 0.10.35.29145`，保留精确 owned-primary 与 Journal 来源。[完整不可变原文](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/research/game-api-version-0.10.35.29145.md)保留初始默认路径拒绝、元数据报告、受保护引用及当时待实现/部署边界。[Lab 阻塞记录](https://github.com/Arcueid77/Spherewright-Lab/blob/14478a03c64c550e644c559405c8496b647552e7/releases/v0.4.0/evidence/2026-10-15/owned-primary-29145-blocker.md)保留该历史阶段。通用 `SESSION_NOT_OWNED` 提示本身不证明隔离。

后续[最终验收](../validation/v0.4.0.md)在 Windows x64 / DSP `0.10.35.29145` / BepInEx `5.4.17.0` 上验收源码 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`，原生、包和恢复证据有独立索引。不得将原“候选未部署”当当前状态，也不得把元数据一致单独升格为实机证明。冻结候选包不变；本次文档工作不授予加载、安装或发布权限。

## 精确原生比较

| 输入 | Assembly-CSharp.dll SHA-256 |
| --- | --- |
| 保留的 `0.10.35.29131` 引用 | `74AC6162CEF8394A6021C8475132CAE4DF4F840871A99E63CC7D2C8297661BC0` |
| 由原生菜单归属为 `0.10.35.29145` 的已安装引用 | `B84B60DF5A24947A014801F23B9B85D1E77853F42A331918E2DC85B153E50527` |

旧版本归属来自精确保留采集，新版本来自原生菜单而非过时 `Updates/Versions.txt`。Mono.Cecil 只读元数据，比较不调用原生方法或 Bridge，不修改存档、票据或进度文件。

十一项完整签名、方法体大小、指令数和逐条 opcode/operand 文本均相同：

| 方法 | 方法体字节 | 指令数 |
| --- | ---: | ---: |
| `GameSave.SaveCurrentGame(string)` | 846 | 290 |
| `GameSave.SavePath(string)` | 42 | 14 |
| `GameSave.ReadHeader(string,bool,out GameSaveHeader)` | 604 | 263 |
| `DSPGame.StartGame(GameDesc)` | 87 | 26 |
| `DSPGame.StartGame(string)` | 86 | 28 |
| `GameData.Export(BinaryWriter)` | 616 | 223 |
| `GameData.Import(BinaryReader)` | 2146 | 780 |
| `GameDesc.Export(BinaryWriter)` | 268 | 97 |
| `GameDesc.Import(BinaryReader)` | 560 | 223 |
| `GalaxyData.ExportRuntimeData(BinaryWriter)` | 284 | 122 |
| `GalaxyData.ImportRuntimeData(BinaryReader)` | 224 | 101 |

`GameData.CURRENT_PATCH=23` 和所比较体未变支持有界元组推断：原生 header `7`、GameData `13/patch23`、GameDesc `10`、Galaxy runtime marker `1`。这不普遍证明施工接口或全部游戏行为。原比较报告 SHA-256 为 `7C4D5457CF976FE4E76B994590368CADFFDE267C2E44B82D2BF8D4A9F4534434`，仅元数据调用方 SHA-256 为 `EA8EB034AD8C0B29F511A83BD2C4A7A14035350F7F8386FB8B95672704BDC71D`。详细指令哈希属于受保护证据，不打包游戏代码。

## 精确版本对恢复与 Journal 策略

`OwnedWorldPrimaryMigrationPolicy` 保留 `29104 → 29131` evidence version `5`，新增 `29131 → 29145` evidence version `6`，各有精确提示。旧版本/提示不能满足新版本对。默认恢复要求源/运行版本相等。prepare 拒绝预先确认/tick 覆盖，仅披露绑定完整身份/tick 的精确主档，不消费或加载。

commit 要求后续明确确认与匹配 digest，fresh 核对未变活动票据双副本、主档、菜单 revision 和原 Journal，并持有只读租约。持久 attempt/tombstone 先于一次原生加载。精确身份/星球/tick 认领后只追加下一个转换，保留来源、全部条目及前三转换。`OwnedWorldVersionCompatibilityPolicy.MaximumDurableTransitions=4`；认领 tick 递增，持久序列不递减。精确第四转换先持久化，再正常重存主档/证明 header 并签发目标版本凭据。

不允许第五转换、跳链、降级、未知对、LastExit/自动保存替代、其他存档或已消费尝试重放。特定模式迁移不是默认恢复，也不允许重写凭据日期/历史。

## 已完成有限施工历史

`BlueprintProgressVersionPolicy.CanRetainCompletedHistory` 只允许相同 session/Journal 身份的 stored29131→runtime29145 完成历史，要求 Journal 完整连续且持久、无待持久化/错误，并证明精确转换。加载 tick 覆盖每份收据，每个 build/object 都已完成。`BlueprintBuildStore` 返回克隆前验证计划哈希/收据。部分、未知、损坏、其他版本或未证明历史拒绝。

读取不重写文件，也不恢复 plan token 或执行权。之后普通持久更新可在保留历史的同时标记目标文档版本；持久化失败保留源证据。不得为解决版本不匹配而清空存储或重做已完工施工。合成 linked-store 测试不能替代原生实体/拓扑读回或 Windows 安装证据。

## 剩余限制

方法一致和单元/集成测试不证明任意存档兼容、未来正常恢复、生产连续、全部原生建造路径或异机运行。已验收同版本恢复与持续供给各有最终证据，整体候选结果不消除这些限制。后续任何游戏或迁移动作仍需新的适用授权和原生门禁。
