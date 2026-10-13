# Spherewright

[中文](#中文) · [English](#english)

## 中文

Spherewright 通过 MCP 把外部 AI 智能体接入《戴森球计划》。它提供结构化观察和有界动作，同时让采集、手搓、科研、施工、能量消耗、飞行、物流与保存继续遵守游戏的正常机制。

此包包含 Windows x64 版 Spherewright `{{VERSION}}`：

- 由《戴森球计划》加载的 BepInEx 5 Plugin；
- 供外部 Agent 应用连接的单文件自包含 `Spherewright.Mcp.exe`；
- MCP 同步提供的开局移动 playbook。

用户不需要源码仓库或 .NET SDK。

### 支持范围

- 《戴森球计划》`0.10.35.29131`
- BepInEx `5.4.17`
- Windows x64
- 单人和平模式（关闭黑雾/战斗）
- 任意沙盒设置和资源倍率；它们会被报告，但不会扩大 Spherewright 的动作能力

v0.3.3 已在本机分别验证：新建非沙盒 1× 世界、导入非沙盒 100× 世界、导入沙盒 1× 世界。三者均通过正常动作完成第一座建筑并保存。Spherewright 不调用沙盒工具，也不注入物品、能量或科技。黑雾/战斗、多人或 Nebula，以及广泛的第三方 Mod 兼容性暂不支持。

### 安装并连接 MCP

1. 通过 Thunderstore Mod Manager 或 r2modman 安装 Spherewright 及其 BepInEx 依赖。
2. 从所选 Mod profile 启动一次《戴森球计划》，让 Plugin 生成配置和受保护的运行时描述文件。
3. 在该 profile 中找到 `BepInEx/plugins/{{TEAM_NAME}}-Spherewright/Spherewright.Mcp.exe`。
4. 将这个 EXE 注册为外部 Agent 应用中的本地 **stdio MCP Server**。不要在命令行中传入运行时描述文件、认证 token 或存档名。
5. Spherewright 默认只读。需要操作游戏时，在该 profile 的 `BepInEx/config/dev.spherewright.bridge.cfg` 中设置 `Safety.AllowWrites=true`，然后重启游戏。

### 直接发给 Agent

> 连接 Spherewright MCP，先读取当前游戏状态和内置说明。有受保护的续档入口就继续，否则创建新档；如果当前是我手动载入的旧档，先准备导入并等我明确确认。只按正常游戏机制推进，遇到卡路或缺电时主动恢复，每完成一条产线就正常保存并汇报。

### 从新档开始

让游戏停在空闲主菜单，启用 `Safety.AllowWrites` 后告诉 Agent 创建一个 Spherewright 新档。Spherewright 会通过正常新建流程创建和平、非沙盒、1× 世界，内部名称为 `Spherewright_New_*`。Agent 第一次行动前应读取随 MCP 发布的 opening-movement playbook。

### 读取旧档继续玩

在配置中同时启用 `Safety.AllowWrites=true` 与 `Safety.AllowUserSaveImport=true`，重启游戏，然后由玩家在游戏菜单中**手动载入**目标和平单人存档。让 Agent 准备导入；它会先展示无副作用预检的说明，并等待你在后续消息中明确确认。确认后，Spherewright 只会通过正常保存 API 创建 `Spherewright_Imported_*` 独立副本，原档不会被覆盖、改名、删除或成为恢复目标。Journal 从导入点开始，不补造此前的首次事件；以后玩家和 Agent 都在副本中继续。

### 下载、证据与许可

- [GitHub Releases：手动安装包、校验和与回滚说明](https://github.com/AvaloNero/Spherewright/releases)
- [v0.3.2：Sol Max 与 Luna Max 双模型黑盒验收对比](https://github.com/AvaloNero/Spherewright/blob/main/docs/v0.3.2-sol-vs-luna-black-box.md)
- [源码与问题反馈](https://github.com/AvaloNero/Spherewright)

Spherewright 使用 MIT License。

## English

Spherewright connects an external AI agent to **Dyson Sphere Program** through MCP. It exposes structured observations and bounded actions while keeping harvesting, handcrafting, research, construction, energy use, travel, logistics, and saving inside normal game mechanics.

This package contains Spherewright `{{VERSION}}` for Windows x64:

- a BepInEx 5 Plugin loaded by Dyson Sphere Program;
- a self-contained single-file `Spherewright.Mcp.exe` for an external Agent application;
- the opening-movement playbook also exposed by the packaged MCP server.

End users do not need the source repository or a .NET SDK.

### Supported scope

- Dyson Sphere Program `0.10.35.29131`
- BepInEx `5.4.17`
- Windows x64
- single-player peaceful mode with Dark Fog/combat disabled
- any sandbox setting or resource multiplier; they are reported but do not expand Spherewright's action surface

v0.3.3 was locally validated in a fresh non-sandbox 1× world, an imported non-sandbox 100× world, and an imported sandbox 1× world. Each used ordinary actions to construct its first building and save normally. Spherewright never calls sandbox tools or injects items, energy, or technologies. Dark Fog/combat, multiplayer or Nebula, and broad third-party Mod compatibility are not supported.

### Install and connect MCP

1. Install Spherewright and its BepInEx dependency through Thunderstore Mod Manager or r2modman.
2. Launch Dyson Sphere Program once from the selected modded profile so the Plugin creates its configuration and protected runtime descriptor.
3. In that profile, locate `BepInEx/plugins/{{TEAM_NAME}}-Spherewright/Spherewright.Mcp.exe`.
4. Register this EXE as a local **stdio MCP server** in the external Agent application. Do not pass a runtime descriptor, authentication token, or save identity on the command line.
5. Spherewright starts observation-only. To allow gameplay actions, set `Safety.AllowWrites=true` in that profile's `BepInEx/config/dev.spherewright.bridge.cfg`, then restart the game.

### Copy-paste prompt for your Agent

> Connect to Spherewright MCP and first read the current game state and bundled guidance. Continue through a protected resume path when available; otherwise create a new world. If I manually loaded an existing save, prepare an import and wait for my explicit confirmation. Use only normal game mechanics, recover proactively from blocked movement or low energy, and save and report after completing each production line.

### Start from a new world

Leave the game at its idle main menu, enable `Safety.AllowWrites`, and ask the Agent to create a Spherewright world. Spherewright uses the normal new-game flow to create a peaceful, non-sandbox, 1× world with an internal `Spherewright_New_*` name. Before its first action, the Agent should read the opening-movement playbook bundled with the MCP server.

### Continue an existing save

Enable both `Safety.AllowWrites=true` and `Safety.AllowUserSaveImport=true`, restart the game, and have the player **manually load** the intended peaceful single-player save from DSP's menu. Ask the Agent to prepare an import. It must show the no-side-effect disclosure and wait for explicit confirmation in a later message. After confirmation, Spherewright uses DSP's normal save API to create a separate `Spherewright_Imported_*` copy. The original is never overwritten, renamed, deleted, or used as a recovery target. The new Journal starts at the import boundary and does not invent earlier first-time events; both player and Agent should continue in the copy.

### Downloads, evidence, and license

- [GitHub Releases: manual installer packages, checksums, and rollback instructions](https://github.com/AvaloNero/Spherewright/releases)
- [v0.3.2 Sol Max versus Luna Max black-box comparison](https://github.com/AvaloNero/Spherewright/blob/main/docs/v0.3.2-sol-vs-luna-black-box.md)
- [Source and issue tracker](https://github.com/AvaloNero/Spherewright)

Spherewright is available under the MIT License.
