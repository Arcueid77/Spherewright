# Spherewright

[English](#english) · [中文](#中文)

## English

Spherewright connects an external Agent to **Dyson Sphere Program** through MCP. This Windows x64 package contains Spherewright `{{VERSION}}`: a BepInEx Plugin, matching self-contained `Spherewright.Mcp.exe`, and the Agent playbook. End users need no source checkout or .NET SDK.

### Version and support

This 0.4 package targets DSP `0.10.35.29151` and BepInEx `5.4.17`. Its exact source commit and payload hashes are recorded in `spherewright-manifest.json`; keep every component from that build together. Historical production-acceptance source `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9` passed its declared 29145 local manual/Thunderstore-layout installation, runtime, same-version recovery and continuous-supply acceptance. See the [validation summary](https://github.com/Arcueid77/Spherewright/blob/main/docs/validation/v0.4.0.md) and [native compatibility reports](https://github.com/Arcueid77/Spherewright/blob/main/docs/validation/native-29151-preflight.md). Evidence applies only to its stated source, native version and scope.

Supported gameplay is single-player peaceful mode. Sandbox/resource-multiplier settings are reported without adding sandbox actions. Actual 0.4 live evidence is limited to one owned non-sandbox 1× local world; v0.3.3's broader local import examples retain their historical scope. A separate Mod Manager/r2modman profile, another computer, installation transaction-crash recovery, Dark Fog/combat, multiplayer/Nebula and broad third-party Mod compatibility are unvalidated.

### Install and connect

1. Install the selected Spherewright package and its declared BepInEx dependency in your chosen profile; use a trusted release or reviewed local archive.
2. Launch the selected modded profile once to create configuration and the protected runtime descriptor.
3. Locate `BepInEx/plugins/{{TEAM_NAME}}-Spherewright/Spherewright.Mcp.exe` in that profile and register it as a **local stdio MCP server**.
4. Never pass a descriptor, token or save identity on the command line. Keep Plugin and MCP from the same build.
5. Default mode is observation-only. Enable `Safety.AllowWrites=true` for intended structured actions, then restart DSP normally. Never overwrite loaded DLLs.

### New, imported and owned worlds

For a new world, leave DSP at the idle menu and prepare the ordinary new-game flow: peaceful, non-sandbox, 1× `Spherewright_New_*`. Read the opening playbook before moving.

To import a player-loaded supported peaceful save, additionally enable `Safety.AllowUserSaveImport=true`, restart and manually load it. Prepare import, review the disclosure, then explicitly confirm in a later message. Commit creates and verifies an independent `Spherewright_Imported_*` copy through normal saving; the original is unchanged and Journal history begins at import.

To resume an owned primary, use its exact protected ticket, identity and durable Journal from the idle menu. Same-version resume is the default. Disclosed migration/expired recovery needs later explicit confirmation. Reauthorization durably consumes its ticket before native loading; normal Journal-bound default resume consumes it after native adoption and confirmed Journal continuity. Reconcile failed/interrupted accepted actions without rekeying or replaying.

### Agent use and limits

Read `spherewright://agent/playbooks/opening-movement-v1` before acting. A useful request is: “Read the current state and playbook, explain the next bounded phase, and execute only actions already authorized for that phase. Reconcile every accepted action and save according to the declared rules.”

Observation and planning do not grant write authority. No item/energy/technology injection, instant construction, teleportation, direct buffer editing, arbitrary save picker or autonomous expansion loop is provided. Interstellar flight/logistics remains future work.

[Installation](https://github.com/Arcueid77/Spherewright/blob/main/docs/release-installation.md) · [Releases](https://github.com/Arcueid77/Spherewright/releases) · [Source/issues](https://github.com/Arcueid77/Spherewright) · [Fixed Lab evidence](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7). MIT licensed.

## 中文

Spherewright 通过 MCP 将外部 Agent 接入**《戴森球计划》**。此 Windows x64 包包含 Spherewright `{{VERSION}}`：BepInEx Plugin、配套自包含 `Spherewright.Mcp.exe` 及 Agent 指南。用户不需要源码或 .NET SDK。

### 版本与支持范围

此 0.4 包面向 DSP `0.10.35.29151` 和 BepInEx `5.4.17`。准确源码提交与 payload hash 记录在 `spherewright-manifest.json`，全部组件须来自该同一构建。历史生产验收源码 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9` 已通过规定的 29145 本机手动/Thunderstore 布局安装、运行、同版本恢复和连续供给验收，见[验收摘要](https://github.com/Arcueid77/Spherewright/blob/main/docs/validation/v0.4.0.zh-CN.md)及[原生兼容报告](https://github.com/Arcueid77/Spherewright/blob/main/docs/validation/native-29151-preflight.zh-CN.md)。证据只适用于声明的源码、原生版本和范围。

支持单人和平模式。沙盒和资源倍率会被报告，不增加沙盒动作。0.4 实机证据限于一个 owned、非沙盒、1× 本机世界；v0.3.3 较广的本机导入示例保留历史范围。独立 Mod Manager/r2modman profile、另一台计算机、安装事务崩溃恢复、黑雾战斗、多人/Nebula 和广泛第三方 Mod 兼容尚未验证。

### 安装与连接

1. 在选定 profile 安装对应 Spherewright 包及其声明的 BepInEx 依赖，使用可信发行或已审核本地包。
2. 从选定 modded profile 启动一次游戏，生成配置与受保护运行时描述文件。
3. 在该 profile 找到 `BepInEx/plugins/{{TEAM_NAME}}-Spherewright/Spherewright.Mcp.exe`，注册为**本地 stdio MCP 服务**。
4. 不在命令行传入描述文件、token 或存档身份。Plugin 与 MCP 必须来自同一构建。
5. 默认仅观察。准备授权结构化动作时启用 `Safety.AllowWrites=true`，再正常重启 DSP。不要覆盖已加载 DLL。

### 新档、导入与 owned 世界

新档时让 DSP 停在空闲菜单，准备正常新建流程：和平、非沙盒、1× 的 `Spherewright_New_*` 世界。首次移动前阅读开局指南。

导入玩家已载入的受支持和平存档时，另行启用 `Safety.AllowUserSaveImport=true`，重启并手动载入。prepare 后阅读披露，并在后续消息明确确认。commit 通过正常保存创建并核验独立 `Spherewright_Imported_*` 副本，原档不变，Journal 从导入开始。

恢复 owned 主档时，在空闲菜单使用精确受保护票据、身份和 durable Journal。默认同版本恢复。已披露迁移或过期恢复须后续明确确认。重新授权在原生加载前持久消费票据；正常 Journal 绑定的默认恢复在原生接管并确认 Journal 连续性后消费。失败或中断的 accepted action 核原结果，不换键或重放。

### Agent 使用与限制

操作前阅读 `spherewright://agent/playbooks/opening-movement-v1`。可向 Agent 要求：“读取当前状态和指南，说明下一个有限阶段，只执行该阶段已授权动作，核销每个 accepted action，并按声明规则保存。”

观察和规划不授予写入权限。不提供物品/能量/科技注入、瞬间施工、传送、直接缓冲编辑、任意选档或自主扩产。实际跨恒星航行与物流属于后续工作。

[安装](https://github.com/Arcueid77/Spherewright/blob/main/docs/release-installation.zh-CN.md) · [发行](https://github.com/Arcueid77/Spherewright/releases) · [源码与反馈](https://github.com/Arcueid77/Spherewright) · [固定 Lab 证据](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7)。使用 MIT 许可。
