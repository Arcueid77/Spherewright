# v0.4 离线构建与交付核验

更新：2026-10-15。本文记录离线源码、当前安装文件引用和已安装 MCP 的有限核验，不代表候选包、安装或实时游戏恢复验收。

## 固定源码核验

源码基线为 `7742b3c9af6ad5f4e5341c8bb3a135a279b79657`，对应 CI run `38605445392` 成功。复用了已有便携 .NET SDK `8.0.424`；locked restore、Core/Contracts/MCP Release build 均成功。三个测试程序集共 `3028` 项通过、失败 `0`、跳过 `0`（Contracts 73、Core 2707、MCP 248）。离线 package-surface policy 为 `95/95` 通过；源码 MCP metadata 检查为 64 tools、1 resource、内嵌 playbook 完全匹配、额外 stdout 0 字符。

以上日志均在忽略目录，保留相对索引及 SHA-256：

| 核验 | 忽略日志索引 | SHA-256 |
|---|---|---|
| locked restore | `.local/locked-restore.log` | `0A5293E62D3BB4D8819D8D749F51206D3AA424744E622337BEE96070823F245B` |
| Core/Contracts/MCP Release build | `.local/core-release-build.log` | `B81D4AB8B40FA691DF5188F6CD7CF33033D8C2281984FC9F52197001B0BDB70D` |
| 三程序集测试 | `.local/core-release-test.log` | `88E8DCB8FB669E33ED45F1627A81E337314EA9674338E32A62FA64A81AAFCDEB` |
| package-surface policy | `.local/package-surface-policy.log` | `0A88594447664618D9524F3578E2EDB8C02AFDF374582C7570C446C0E7DFEE7B` |
| 源码 MCP metadata | `.local/source-mcp-metadata.log` | `17436DA804841169D825197B7B3829D460295FF96F7354C2863BEBD4E87CA6C7` |

## 当前安装 DLL 引用构建

在忽略目录中的隔离 detached worktree，以相同源码基线 `7742b3c9af6ad5f4e5341c8bb3a135a279b79657` 收集本轮当前安装的五个 DLL；主工作区原有 refs 未更改。五个源文件均与新 manifest 中的 SHA-256 一致，基于这组 refs 的 Plugin locked restore 与 Release build 成功，build 为 0 warnings、0 errors。隔离目录名中的 `29131` 是最初采集标签，不代表本轮运行版本。该结果只证明本轮文件集合可编译，不证明插件已安装或恢复已通过。

| 当前安装文件 | SHA-256 |
|---|---|
| `Assembly-CSharp.dll` | `B84B60DF5A24947A014801F23B9B85D1E77853F42A331918E2DC85B153E50527` |
| `UnityEngine.dll` | `72CC73EEF0036530ABE21F82971FF06002CEE37EFFDD4DD7D5D4EC8DF3911F8D` |
| `UnityEngine.CoreModule.dll` | `E2B5AE2FD12646D03FC3D04D1A37D522572A3B97022FE1B95BBF2A2F2B04853A` |
| `UnityEngine.PhysicsModule.dll` | `9B8C76635404945AD690B219415151E244C3D6D0A76851FBD40045272F160D38` |
| `BepInEx.dll` | `DC1CB6B58B962BDA5AAA1D6B5F9AE14EC174F61836A1A1F96C1A040C7E8381F7` |

| 核验记录 | 忽略索引 | SHA-256 |
|---|---|---|
| 当前 DLL manifest | `.local/build-worktree-29131/.local/game-refs/manifest.json` | `CFA05CF20919FC33EF37CC38FCAAFA4FD90BB372C092A376113CEDC055B50712` |
| refs 对照及 fresh Native 版本关联报告 | `.local/build-worktree-29131/.local/game-refs/delivery-validation.json` | `3DC5F96FB0D45D8DA8A25401CB3E04BA09233704E5C8D0C0EED9DDBB745F109A` |
| Plugin locked restore | `.local/build-worktree-29131/.local/plugin-current-refs-restore.log` | `492B479BEA3D7882FD5E358E558963592FF45CFB616915F15126C6AED9B7747B` |
| Plugin Release build | `.local/build-worktree-29131/.local/plugin-current-refs-release.log` | `BF8F8093E5BFCA677C80A240EC6087BB746A24FC282492E4EB754ACE73AF325A` |

本轮 writer 通过 healthy 菜单后的 Native `get_session_state` fresh 确认活动游戏版本为 `0.10.35.29145`，状态为未 Load。受保护回复索引为 `9d554a389ef44eb4922c6f37b85d8582:1/bridge-response-get_session_state`，回复 SHA-256 为 `DEA283CBFA438EF22C8A1AEFAC3BFBED4059CB7F2192902ED1FDC567C5444849`。本轮安装 DLL refs 与该 Native 会话属于同一交付核验轮次；这项文件可编译结果不构成历史版本映射或安装/恢复验收。

`Updates/Versions.txt` 中的 `0.10.35.29131` 行不是活动版本证据。29131 的历史原生恢复证据见 [R1311/R1318 恢复记录](2026-10-14/foundry-progress-recovery-r1307.md)；最后 owned 保存和票据仍属于 29131，本轮没有跨版本 Load/迁移。R1337 保存前快照和当时边界见[状态记录](../../current-status.md)，该快照早于本节的 29145 Native 回复。

## 已安装 MCP 磁盘与协议核验

磁盘安装的 MCP apphost 与 managed DLL 均为 FileVersion `0.4.0.0`，ProductVersion source commit 为 `6528e1f7101a714826f7364cef2b0ad75c4b6e82`。文件 SHA-256：

| 文件 | SHA-256 |
|---|---|
| `Spherewright.Mcp.exe` | `A0B3F2520C694B38AD3F0598BA4D609C8C2A0A1E517A455E033C826C1CED99CB` |
| `Spherewright.Mcp.dll` | `C2C268207EDD100418B64A5FEBFEF17696718F409FD6D1BA348BA9138FAB5FAF` |

对该已安装程序完成隔离的 stdio 协议探测：initialize、tools/list、resources/list 和 resources/read 均成功，返回 64 tools、1 resource，嵌入 playbook 匹配，退出码 0；探测没有调用 Bridge 工具。检查快照中专用进程名 `Spherewright.Mcp` 的数量为 0；这不代表其他宿主中的 MCP 进程状态，也不证明当前 DSP/MCP 握手。Healthy 菜单和游戏版本已有上文 Native 证据，但尚无获准的跨版本 Load；恢复验收仍未通过。

本轮没有组装或验证可分发候选包，没有安装候选，也没有通过实时游戏恢复验收。
