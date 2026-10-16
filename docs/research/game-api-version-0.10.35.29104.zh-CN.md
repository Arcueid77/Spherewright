# DSP 0.10.35.29104 精确版本对持久化研究

[English](game-api-version-0.10.35.29104.md) | [简体中文](game-api-version-0.10.35.29104.zh-CN.md)

## 历史范围

2026-09-29 调查只为已研究受保护 expired-primary 路径上的 `0.10.35.29088 → 0.10.35.29104` 提供证据，不授予加载权限。[完整不可变原文](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/research/game-api-version-0.10.35.29104.md)保留当时阻塞及受保护运行上下文。存档身份、候选 tick 和票据内容属于 Lab，不进入此维护产品说明。

当前已验收本机候选支持 DSP `0.10.35.29145`；见[后续研究](game-api-version-0.10.35.29145.md)及[最终验收](../validation/v0.4.0.md)。历史拒绝/待验声明不是当前安装描述。

## 元数据比较

保留的 29088 原生引用为 8,020,480 字节，SHA-256 `C43A484F6ADF8A9E4B956156047070891B46860D5B5C707BA1377B6A2AF25732`。29104 引用为 8,022,016 字节，SHA-256 `6C122E5443E6843979B4064050DFCB5E0D75577A0B64F6AE4111290238B33C12`；记录的 Steam build ID 为 `25599610`。

Mono.Cecil 仅检查元数据、不执行原生代码，发现以下完整签名、方法体大小、指令数和逐条 opcode/operand 文本相同：

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

`GameData.CURRENT_PATCH` 仍为 `23`。所比较导入/导出体未变，支持窄范围元组推断：header `7`、GameData `13/patch23`、GameDesc `10`、Galaxy runtime marker `1`。这是持久化路径比较，不证明所有游戏方法/行为相同。精确引用 Plugin 编译与实机验证仍是不同证据。

## 维护中的恢复边界

`OwnedWorldVersionCompatibilityPolicy` 仅允许匹配源后的此精确链路，不允许直接 `28529 → 29104`、`29057 → 29104`、降级、通配或跳链。当前上限为另行研究、终止于 29145 的链上四个持久转换；后续扩展不减弱本对的验证。

认领前验证完整原 Journal 和源凭据检查点；原生认领后只追加 `29088 → 29104`，先持久化，再正常保存主档，读回目标 header/身份/tick 后签发目标版本凭据。保留历史来源、已有条目/转换、身份与凭据日期，不得重标过去历史来满足资格。

expired-primary prepare 披露精确受保护主档；commit 需要后续对话明确确认和匹配计划证据。默认恢复不获得跨版本权限。持久 attempt/tombstone 消费先于一次原生加载；证据变化或已消费尝试中断应拒绝，不重放。不允许 LastExit/自动保存替代、任意存档或导入副本。

## 限制

离线一致、编译和测试不证明实机迁移、目标保存/读回、未来恢复、生产连续或完整 `0.4` 验收。原研究缺少这些证明的事实保留历史语义；后续验收只能由独立索引的精确证据建立。本文不授予 Save/Load、安装或游戏权限。
