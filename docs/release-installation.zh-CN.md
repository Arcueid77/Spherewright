# 安装 Spherewright

[English](release-installation.md) · [简体中文](release-installation.zh-CN.md)

## 选择版本与包布局

最新正式发行版为 v0.3.3。**0.4.0 仍是尚未发布、已通过本机验收的候选。** 冻结源码、两份 ZIP hash 及实际本机验收见[验收](validation/v0.4.0.zh-CN.md)。main 文档可能比冻结包更新。

0.4 已验收环境是 Windows x64、DSP `0.10.35.29145`、BepInEx `5.4.17.0`，一个和平、owned、非沙盒、1× 本机世界。运行兼容允许受支持的和平沙盒或资源倍率设置，不增加沙盒动作，但更广的 0.4 实机覆盖尚未建立。v0.3.3 结果保留原构建的历史范围。

两种布局提供相同游戏和 MCP 能力：

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

将返回的 `Spherewright.Mcp.exe` 注册为 Agent 应用的本地 stdio MCP 服务。命令行不得传入运行时描述文件、认证 token 或存档身份。阅读包内 `AGENT-PLAYBOOK.md` 或 MCP 资源 `spherewright://agent/playbooks/opening-movement-v1`。从当前维护源码生成的新包也包含中文伴页；冻结候选保留原内容。

## Thunderstore 布局

0.4 包已在规定的本机标准布局验证；**正式商店发布以及独立 Mod Manager/r2modman profile 尚未验证**。不要假设商店已经提供尚未发布的 0.4 下载。

获得正式发布授权后，可通过管理器安装选定版本及其声明的 `xiaoye97-BepInEx-5.4.17` 依赖。在该 profile 中找到：

```text
BepInEx/plugins/Arcueid_77-Spherewright/Spherewright.Mcp.exe
```

将其注册为本地 stdio MCP。管理器更新必须同时保持 Plugin/MCP 配套。另一 profile 或计算机需要单独验证。

## 开始会话

Spherewright 默认仅观察。仅在确实准备授权结构化动作时设置 `Safety.AllowWrites=true`，然后正常重启 DSP 使配置生效。

**新档：**让 DSP 停在空闲主菜单，要求 Agent 准备正常新建流程。它会创建和平、非沙盒、1× 的 `Spherewright_New_*` 世界。首次移动前阅读指南，accepted 动作轮询到原终态，只使用有界 fresh 恢复。旧 `Spherewright_M0_*` 名称本身不构成所有权。

**导入玩家已载入存档：**另行启用 `Safety.AllowUserSaveImport=true`，重启 DSP，并手动载入目标受支持和平存档。先 prepare 导入，阅读无副作用披露，再在后续对话中明确确认。之后 commit 才可通过正常保存 API 创建并 Header 核验独立 `Spherewright_Imported_*` 副本，原档不变。不补造此前历史，owned 副本的 Journal 从导入开始。玩家手动游玩后，Agent 后续写入需 fresh 观察及新 prepare。

**恢复 owned 主档：**DSP 停在空闲菜单，只使用该精确受保护票据、嵌入身份及 durable Journal。默认同版本恢复。已披露版本迁移或过期恢复各需后续明确确认，文件名前缀不授予所有权。票据在原生加载前持久消费；失败或中断的 accepted 操作应调查并核销原 action，禁止换键重放。

备份时将受保护 Journal 与票据副本一起保留。Journal 缺失、截断、重建或边界不一致会拒绝恢复或让恢复后的操作保持受限，不会静默建立新时间线。准确原生门见 Agent 指南。

## 核验与恢复

- 包被修改、不完整或存在活动 `DSPGAME` 进程时拒绝安装。
- 若安装留下 `needs_recovery` 或 pending marker，保留两侧操作目录、记录和备份。按[安装恢复](installation-recovery.zh-CN.md)进行只读预览及证据绑定的明确还原，不得删除 marker 或移动证据绕过拒绝。
- 安装事务崩溃恢复、另一台计算机、广泛第三方 Mod、黑雾战斗及多人/Nebula 尚未验证。启动门不能阻止旧 Plugin 已经驻留内存。
- 报告准确 Spherewright/DSP/BepInEx 版本、结构化错误及脱敏证据，不发布 Save、存档名、token、运行时描述文件或私有路径。

另见[发行说明](release-notes/v0.4.0.zh-CN.md)、[验收](validation/v0.4.0.zh-CN.md)与 [Lab 证据](sample-validation.zh-CN.md)。历史安装恢复记录只对其声明构建有效，不是重放命令。
