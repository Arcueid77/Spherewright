# 安装 Spherewright

[English](release-installation.md) · [简体中文](release-installation.zh-CN.md)

## 选择版本与包布局

Windows x64 直接安装推荐 [Spherewright v0.6.0（Ascension）](https://github.com/Arcueid77/Spherewright/releases/tag/v0.6.0)，支持 DSP `0.10.35.29151` 和 BepInEx `5.4.17`。

- [下载 v0.6.0 Windows x64 手动安装包](https://github.com/Arcueid77/Spherewright/releases/download/v0.6.0/Spherewright-0.6.0-win-x64.zip)，然后按下方「手动包」安装。
- [下载 v0.6.0 校验和](https://github.com/Arcueid77/Spherewright/releases/download/v0.6.0/SHA256SUMS.txt)。请用同一版本发行页上的 SHA-256 核对 ZIP；不要混用不同版本的安装包、校验和、Plugin 或 MCP。
- 使用 Thunderstore Mod Manager 或 r2modman 时，选择 `Arcueid_77-Spherewright` 的 `0.6.0` 版本。GitHub 发行页也提供这种布局的压缩包。[Thunderstore 商店](https://thunderstore.io/c/dyson-sphere-program/p/Arcueid_77/Spherewright/) · [GitHub 布局包](https://github.com/Arcueid77/Spherewright/releases/download/v0.6.0/Spherewright-0.6.0-thunderstore.zip)

原版 0.6.0 手动 ZIP 内的 `INSTALL.md` 及中文安装指南沿用了旧下载说明，列的是 GitHub 0.4.0 和 Thunderstore 0.3.3。安装这个包时请使用上面的 0.6.0 下载链接和校验和。

两种布局提供相同的游戏和 MCP 能力。

| 布局 | Plugin | MCP |
| --- | --- | --- |
| `Spherewright-<version>-win-x64.zip` | 手动安装到 `BepInEx/plugins/Spherewright` | 自包含程序，默认位于 `%LOCALAPPDATA%/Spherewright/mcp/<version>` |
| `Spherewright-<version>-thunderstore.zip` | 标准 Thunderstore 布局，标识为 `Arcueid_77-Spherewright` | 单文件程序，与 Plugin 一起安装 |

用户不需要源码或 .NET SDK。Plugin、MCP 与共享程序集必须来自同一构建。安装前退出 DSP 并停止相连 MCP 进程，禁止覆盖已加载 DLL。

## 手动包

另行安装 BepInEx 5。完整解压包并保留到验证成功。在解压目录运行：

```powershell
./install.ps1
```

安装器核验 `manifest.json`，通过 `-DspDir`、`SPHEREWRIGHT_DSP_DIR` 或 Steam 定位 DSP，并以 JSON 返回准确 Plugin 目录和 MCP 程序。多安装或非标准位置应显式指定：

```powershell
$dspDir = Read-Host 'Exact DSP installation directory'
./install.ps1 -DspDir $dspDir
```

重装同版本 MCP 需要 `-Force`，但不会绕过完整性、进程、目标目录或 pending 恢复检查。安装器不会启动 DSP、启用写入、安装 BepInEx 或修改存档。

复制安装器输出的 `mcpExecutable` 完整路径，在下方连接检查中使用。

## Thunderstore 布局

使用标准 Thunderstore 布局，Plugin 与配套单文件 MCP 一同安装。引用的本机标准布局检查不证明独立 Mod Manager/r2modman profile；应核对选定 profile 的实际组件和 MCP 路径。

在《戴森球计划》的目标 profile 中安装 `0.6.0` 和声明的依赖 `xiaoye97-BepInEx-5.4.17`，然后在这个 profile 中找到配套的可执行文件。

```text
BepInEx/plugins/Arcueid_77-Spherewright/Spherewright.Mcp.exe
```

复制该文件的完整路径。通过同一 profile 启动带 Mod 的游戏，使 Plugin 与 MCP 对应同一份安装。

## 连接 Codex 并检查只读访问

已安装 Codex CLI 时，运行以下命令，并在提示时粘贴所选布局的 MCP 程序完整路径。

```powershell
$mcpExe = Read-Host 'Full path to Spherewright.Mcp.exe'
codex mcp add spherewright -- "$mcpExe"
codex mcp list
```

其他 MCP 客户端也可将该程序注册为本地 stdio 服务，参数留空。运行时描述文件、令牌和存档身份请保存在本机受保护的位置。

1. 新开已启用该服务的 Codex 会话，读取 `spherewright://agent/playbooks/opening-movement-v1`。游戏关闭时，MCP 也能提供这份指南。
2. 启动带 Mod 的 DSP 并等到主菜单，让 Codex 调用 `spherewright_get_status`。成功回执会列出已连接的 Plugin 和游戏版本，应分别为 Spherewright `0.6.0` 和 DSP `0.10.35.29151`。这一步使用默认只读设置即可。
3. 如果能读取指南但状态调用失败，核对 MCP 程序路径是否属于启动游戏的安装目录或管理器 profile，再查看该次调用返回的错误码和恢复提示。

## 开始会话

Spherewright 默认仅观察。仅在确实准备授权结构化动作时设置 `Safety.AllowWrites=true`，然后正常重启 DSP 使配置生效。

**新档：**让 DSP 停在空闲主菜单，要求 Agent 准备正常新建流程。它会创建和平、非沙盒、1× 的 `Spherewright_New_*` 世界。首次移动前阅读指南，accepted 动作轮询到原终态，只使用有界 fresh 恢复。旧 `Spherewright_M0_*` 名称本身不构成所有权。

**导入玩家已载入存档：**另行启用 `Safety.AllowUserSaveImport=true`，重启 DSP，并手动载入目标受支持和平存档。先 prepare 导入，阅读无副作用披露，再在后续对话中明确确认。之后 commit 才可通过正常保存 API 创建并 Header 核验独立 `Spherewright_Imported_*` 副本，原档不变。不补造此前历史，owned 副本的 Journal 从导入开始。玩家手动游玩后，Agent 后续写入需 fresh 观察及新 prepare。

**恢复 owned 主档：**DSP 停在空闲菜单，只使用该精确受保护票据、嵌入身份及 durable Journal。默认同版本恢复。已披露版本迁移或过期恢复各需后续明确确认，文件名前缀不授予所有权。重新授权在原生加载前持久消费票据；正常 Journal 绑定的默认恢复在原生接管并确认 Journal 连续性后消费。失败或中断的 accepted 操作应调查并核销原 action，禁止换键重放。

备份时将受保护 Journal 与票据副本一起保留。Journal 缺失、截断、重建或边界不一致会拒绝恢复或让恢复后的操作保持受限，不会静默建立新时间线。准确原生门见 Agent 指南。

## 核验与恢复

- 包被修改、不完整或存在活动 `DSPGAME` 进程时拒绝安装。
- 若安装留下 `needs_recovery` 或 pending marker，保留两侧操作目录、记录和备份。按[安装恢复](installation-recovery.zh-CN.md)进行只读预览及证据绑定的明确还原，不得删除 marker 或移动证据绕过拒绝。
- 安装事务崩溃恢复、另一台计算机、广泛第三方 Mod、黑雾战斗及多人/Nebula 尚未验证。启动门不能阻止旧 Plugin 已经驻留内存。
- 报告准确 Spherewright/DSP/BepInEx 版本、结构化错误及脱敏证据，不发布 Save、存档名、token、运行时描述文件或私有路径。

另见 [v0.6.0 发行说明](release-notes/v0.6.0.zh-CN.md)及[发布和验收范围](https://github.com/Arcueid77/Spherewright-Lab/blob/c012e723d75c35e1d72d82f3bb5f286926259945/releases/v0.6.0/publication-2026-11-21.md)。

## 历史安装与实机验收记录

从[官方发行](https://github.com/Arcueid77/Spherewright/releases)或已审核本地包选择版本。包清单记录该构建的版本、源码提交与 payload hash，并应使用配套校验和记录。历史生产验收源码 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9` 及其原 ZIP hash 见[验收](validation/v0.4.0.zh-CN.md)。另行记录的 2026-10-17 包调用验收源码为 `e018b7f45c70f5fe029418cad987af794b5cb9bf`；这些历史包保留各自源码身份。

冻结生产验收使用 Windows x64、DSP `0.10.35.29145`、BepInEx `5.4.17.0`，一个和平、owned、非沙盒、1× 本机世界。引用的 e018 安装、调用及连续性检查使用同一本机环境及 DSP `0.10.35.29151`。运行兼容允许受支持的和平沙盒或资源倍率设置，不增加沙盒动作，但更广的 0.4 实机覆盖尚未建立。v0.3.3 结果保留原构建的历史范围。

引用的 e018 两种布局已通过官方事务路径实际安装，各自完整通过 64 MCP 工具调用，分别记录数据/预检成功及预期原生安全拒绝，矩阵 accepted/unknown 均为零。所有者明确确认的原主档迁移、正常保存及之后同版本恢复保留持久 Journal 和已完成历史。详见[固定实机报告及包 hash](https://github.com/Arcueid77/Spherewright-Lab/blob/00f4359eef704bc7cd5c148438c05e0d3121bda6/operations/package-callability-2026-10-17.md)及[原生验证](validation/native-29151-preflight.zh-CN.md)。证据只适用于引用的源码、包及源码/session 范围，本次不增加新的生产长窗。以后恢复过期的 29145 主档仍须准备披露精确 `29145→29151` 后再获明确确认；有效主模式证据 5/6 保持原范围。

2026-10-17 最终双包另对应源码 `b7c0002dbe2e8f5608c5ca140b0c6326a1818021`，不替换上述历史包身份。其手动与标准 Thunderstore 两种布局均已完成正常冷安装、`29151→29151` 默认 owned 主档恢复，且各自独立通过一轮完整 64 工具矩阵（23 数据成功 / 6 预检成功 / 35 预期原生拒绝，矩阵 accepted/unknown 为零）。两次恢复保留持久 Journal 和已完成历史，没有重做原世界迁移。对应源码安装、恢复及完整矩阵的最终独立结果审计全部通过。详见[固定对应源码报告](https://github.com/Arcueid77/Spherewright-Lab/blob/74a4ec3d91608467911460eaf0d317800e60309b/operations/formal-release-2026-10-17.md)。后续事实文档 SHA 不改变这些包的源码身份，也不重新打包。

**历史发行（2026-10-17）：** [GitHub v0.4.0](https://github.com/Arcueid77/Spherewright/releases/tag/v0.4.0) 提供当时的手动 ZIP、Thunderstore 布局 ZIP、校验和及生成清单。附注标签 `v0.4.0` 指向 `b7c0002dbe2e8f5608c5ca140b0c6326a1818021`。

另见[发行说明](release-notes/v0.4.0.zh-CN.md)、[验收](validation/v0.4.0.zh-CN.md)与 [Lab 证据](sample-validation.zh-CN.md)。历史安装恢复记录只对其声明构建有效，不是重放命令。
