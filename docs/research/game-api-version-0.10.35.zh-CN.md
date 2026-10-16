# DSP 0.10.35.29057 / 29088 持久化研究

[English](game-api-version-0.10.35.md) | [简体中文](game-api-version-0.10.35.zh-CN.md)

## 历史范围与精确输入

本文记录 2026-09-24 仅针对有向版本对 `0.10.34.28529 → 0.10.35.29057` 和 `0.10.34.28529 → 0.10.35.29088` 的研究，属于历史持久化证据，不是当前运行状态或恢复权限。完整方法哈希、理由与当时待验观察见[不可变原文](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/research/game-api-version-0.10.35.md)。当前候选支持 `0.10.35.29145`；见[环境](environment.md)、[后续研究](game-api-version-0.10.35.29145.md)和[最终验收](../validation/v0.4.0.md)。

| 原生引用 | 字节数 | Assembly-CSharp.dll SHA-256 |
| --- | ---: | --- |
| `0.10.34.28529` | 7,830,016 | `AE0BA95F75BD879A62AA4CE253B2AB78EAA4FB3C7C595F5E1FEE75EBE0E0EF85` |
| `0.10.35.29057` | 8,019,456 | `E75D3FE4B6A9CA822766189F826BA3A8348DFB7E301AA37FF6779DB29A83FD8D` |
| `0.10.35.29088` | 8,020,480 | `C43A484F6ADF8A9E4B956156047070891B46860D5B5C707BA1377B6A2AF25732` |

针对性 Mono.Cecil/ILSpy 检查只读元数据，不执行原生方法、不加载存档、不改已安装文件或票据/Journal。版本来源是记录的安装元数据和原生引用；哈希例外不等于通用兼容策略。

## 入口与真实格式变化

28529→29057 比较中，`GameSave.SaveCurrentGame(string)`、`GameSave.ReadHeader(string,bool,out GameSaveHeader)` 和 `DSPGame.StartGame(GameDesc/string)` 两重载的签名、方法体大小、规范指令流一致，但不代表整个存档未变：

- 原生 header 仍为 `VFSAVE`、版本 `7`，其后为版本/tick/时间和有界截图/账号数据，再进入 GameData。
- `GameData` 外层仍为 `13`，`CURRENT_PATCH` 从 `22 → 23`，目标导入器具有 `patch < 23` 迁移分支。
- `GalaxyData.ExportScannedDatas` marker `0` 变为 `ExportRuntimeData` marker `1`，在旧扫描记录终止符后加入植被数据。`ImportRuntimeData` 在 marker `<1` 时明确于旧段后返回。
- `GameDesc` 从 `9 → 10`，新增 `starNameLCID`；`Import` 仅在版本 ≥10 读取，否则置零。

因此 `GameData.Import/Export` 与 `GameDesc.Import/Export` 的 IL 已变化。只接受一致的研究元组：源 header `7` / GameData `13, patch22` / GameDesc `9`，或精确研究目标 header `7` / `13, patch23` / `10`（Galaxy runtime marker `1`）。不能把各自允许的字段任意混成未研究元组。

29057→29088 中，全部十一项持久化方法的签名/体/指令一致：上述四入口、`GameSave.SavePath`、GameData Import/Export、GameDesc Import/Export、GalaxyData Import/ExportRuntimeData。目标序列化元组未变，支持加入独立的**直接** 28529→29088 对，不证明 29057→29088 是受支持链。

## 认领与 Journal 要求

`OwnedSavePrefixReader` 只验证有界前缀、精确受保护嵌入身份、版本和 tick，不导入世界。`GameDesc.creationVersion` 不是当前源版本证明。完整文件证据、受保护一致凭据、原 Journal 和精确原生认领仍各自必需。

`OwnedWorldVersionCompatibilityPolicy` 现列出两组直接对及另行研究的后续 `29088 → 29104 → 29131 → 29145`，最多四个持久转换。当前策略不追认每组研究都已有实机。Journal 的 `GameVersion` 保持来源版本，仅验证后的持久转换推进有效版本。保留身份、跟踪起点、历史覆盖标记、条目和序列，认领 tick 严格递增、持久序列不递减。转换先持久化，之后才能正常保存为目标版本并签发凭据。

信任新凭据前，目标保存须读回精确 owned 身份、tick 下限、当前版本、研究元组和 healthy Journal。prepare 只披露；适用迁移模式必须取得后续用户明确确认，默认恢复仍要求版本相等。凭据 attempt/tombstone 消费先于一次加载调用；已消费后失败/中断需要核销，不能改过期时间、重标 Journal、换文件名、替代 LastExit/自动保存或重放。

## 证据限制

原始 29057 研究未证明该版本上的实机迁移。后续 29088 及更晚证据以 Lab 和最终验收各自范围为准。元数据一致、Plugin 重编译和合成测试不能证明载入世界完整、目标保存成功、未来恢复、持续生产或整体版本验收。研究本身不授权这些动作。
