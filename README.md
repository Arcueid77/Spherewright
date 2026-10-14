# Spherewright

DSP version adaptation is narrow: exact `0.10.34.28529 → 0.10.35.29057`, `0.10.34.28529 → 0.10.35.29088`, `0.10.35.29088 → 0.10.35.29104`, and `0.10.35.29104 → 0.10.35.29131` owned-primary transitions preserve Journal origin/history and record a separate durable transition. One local live `28529 → 29088` recovery and the `29104 → 29131` active-primary version-change recovery have completed; R1318 later confirmed a healthy recovery of the same primary. The `29088 → 29104` extension still has native comparison and offline-test evidence but no successful live load. These results do not allow arbitrary cross-version save loading or prove production continuity. See the [current status](docs/current-status.md), [earlier native evidence](docs/research/game-api-version-0.10.35.md), [29104 evidence](docs/research/game-api-version-0.10.35.29104.md), and [R1311/R1318 recovery evidence](docs/evidence/2026-10-14/foundry-progress-recovery-r1307.md).

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)
[![Platform: Windows](https://img.shields.io/badge/platform-Windows-0078D4)](#requirements)
[![Windows Core CI](https://github.com/AvaloNero/Spherewright/actions/workflows/windows-core-ci.yml/badge.svg)](https://github.com/AvaloNero/Spherewright/actions/workflows/windows-core-ci.yml)

[中文](#中文) · [English](#english)

## 中文

Spherewright 是《戴森球计划》的 MCP 控制桥。它让外部 AI 智能体读取结构化游戏状态，并通过游戏自身的移动、采集、手搓、科研、施工、物流、飞行和保存机制操作伊卡洛斯。项目不在游戏里内置大模型，也不靠截图、键鼠宏、改档、物品注入或瞬间建造完成任务。

当前正式版为 **v0.3.3 — Gameplay Mode Compatibility**。本机实测已覆盖三种和平单人世界：Spherewright 从主菜单创建的非沙盒 1× 新档、导入的非沙盒 100× 资源档、导入的沙盒 1× 档；三者都完成了正常采集、手搓、研究和第一座建筑施工并正常保存。沙盒状态和资源倍率会如实显示，但不再阻止普通 MCP 动作；Spherewright 仍不提供任何沙盒工具或资源、能量、科技注入能力。

当前开发版本为 **v0.4.0 — Overseer, Foundry & Governor**：合并诊断、确定性建厂/续建和存量产线配平/扩产，整体目标是**为跨星系扩张做准备**，补齐关键科技、翘曲器/燃料供应、运输能力和远征备料。建厂与扩产部分仍在开发，合并版尚未进入发布审核。后续依次为 **0.5 跨星系（Voyager）→ 0.6 戴森系统（Ascension）→ 0.7 发布候选 → 1.0 正式版**；实际跨恒星飞行和曲速物流留到 0.5。

main 已增加只读 `spherewright_get_foundry_plan`：从目标产量计算多级配方、共享原料需求、设备数量和基础功率，明确外供与副产物。可选 `site` 为最多 32 台机器生成球面网格候选，返回吸附后净空、原生建造条件和整份机器库存缺口；不提供写 token。现场另有独立的原生电网覆盖/满基础负载预算，计入既有设备、充电塔和新接通负载；这是当次容量证据，不证明燃料持续供应。两种结果均不可执行，完整物流/电力施工计划和三级链验收仍在开发。有限蓝图另走独立的准备/提交/逐对象进度/取消/恢复接口，预览不冒充该执行能力；当前实机边界见[状态入口](docs/current-status.md)。

源码另有只读 `spherewright_get_governor_plan`，复用 Foundry 和 Overseer，对明确选定的存量设备比较升级、增建和复制模块，分别列出实际产消、目标需求、库存变化及供给缺口。可选 `parallelExpansionBlueprint` 在实测非零基线成立后，按“目标减基线”复用完整 Foundry 建材、原生场地、供电和输送预算；明确区分模块内全部对象与未规划的外部基础设施成本。三个独立非零窗口形成候选基线，须在施工前锁定目标/误差/时长，后续只累计有效游戏时间窗口的并集；源码现将已锁声明独立保护落盘（`declarationDurable=true`）。已锁的 Governor `31→62/min` 声明已在受保护实机窗口累计通过 **≥37128** 个有效游戏 tick；这只关闭该声明的吞吐门，不证明全厂配平、0.4 完成或持续供给。正常同档计划重启仍核对身份、游戏版本、Journal 连续性及原票据保存时间点；未持久声明、旧档或客户端自报历史不能补证。目标链告警与尚未归因的全星球告警分开保留；它不签发施工权限、不内置自主扩产，也不把预测容量或缺料时“产出=消耗”当作配平。专用供电/物流扩建、换源方案和其余版本门仍待验，见[当前状态](docs/current-status.md)。

0.4 开发源码为单个传送带详情新增有界 `beltCargo` 只读观察，明确区分“读到空段”和“未观测”。它不扫描整条带路，不能把相邻段的重叠货物相加或用一个快照推断流量；现有升级范围不因此扩大。本切片的离线、部署与实机状态见[API 证据](./docs/research/game-api-foundry.md)，不包含于已发布的0.3.3。

### 支持范围

- Windows x64
- 《戴森球计划》未发布 0.4 包目标 `0.10.35.29131`（R1311/R1318 已有同一 owned 主档的本机保护恢复证据；最终同提交手动包与 Thunderstore 包仍须分别安装和运行验证）；已发布 0.3.x 的历史本机验证为 `0.10.34.28529`
- BepInEx `5.4.17`
- 单人、和平模式（关闭黑雾/战斗）
- 任意沙盒设置和资源倍率；它们只作为运行证据，不会扩展可调用能力

黑雾/战斗、多人或 Nebula、跨恒星曲速自动化，以及与任意第三方 Mod 的兼容性暂不保证。

### 0.4 开发中的蓝图与升级

0.4源码还为错误空载布局补充窄范围正常回收：现有 `prepare_dismantle` / `commit_dismantle` 可处理默认、全空、无叠层/配送器/连接或缓存引用的一级仓2101，返还一个仓并核验其他实体及库存不变。不会自动重建或搬迁；有货/过滤/堆叠仓不支持。离线、冷部署、本机单个空仓回收及单独正常重新放置已通过；实际接线生产与覆盖保存/恢复另验，详见[IFX-101](./docs/incident-fix-log.md#ifx-101--合法但接线过近的空仓缺少正常回收纠正入口)，不在已发布0.3.3中。

`prepare_move` 的新增可选 `surfacePreview` 提供最长32m短路径上的有界地表采样，帮助Agent在提交前识别水面/岸边风险；缺失证据不代表陆地，未发现风险也不保证无障碍或能保持步行。它不自动寻路、不改变正常Move准入，抵达后仍须复读落地、速度和能量。此为未发布0.4开发能力，离线、部署与实机状态见[API证据](./docs/research/game-api-foundry.md)。

普通传送带新增可选 `beltPathMode=native_geodesic`：让游戏在两个明确空地点之间生成球面直线路径，默认仍为网格路径。当前只支持贴地、1.5–30m短路径，不含旧带覆盖、合流或自动寻路；必须收到匹配的计划模式、通过完整原生预检，并另验两端分拣器与真实供料。源码/离线、部署及实机结果分别记录在[API证据](./docs/research/game-api-foundry.md)和存档日记中；这不是已发布0.3.3的功能。

接线拒绝现在区分几何读取、初始候选、接点投影、朝向、倾斜和候选校验阶段，并报告已测最小角度与尝试计数；未测不填0。Agent可据此重新规划，只有明确超出施工范围时才需要移近，不能盲试同一端点。该诊断切片已离线验证，安装/实机状态见[API证据](./docs/research/game-api-foundry.md)。

普通分拣器预检新增有界单传送带接点微调：只处理Agent明确指定的带与仓储箱2101/生产设备/研究站，返回计划槽位、接点和偏移量，不搜索邻带或移动建筑。原生角度、碰撞、材料、无人机施工与逐端核验仍保留；几何变化后必须重新预检。该切片已通过离线回归，尚待同批实机接线验证，不包含在0.3.3中；见[API证据](./docs/research/game-api-foundry.md)。

现有建筑详情的 `sorterEndpoints` 可提供有界的原生槽位位置、朝向和占用信息，帮助 Agent 按实际端口规划接线，而非反复猜建筑中心距离。传送带虚拟端口不代表真实槽位空闲，仍须正常预检；本切片目前仅源码/离线验证，实机状态见 [IFX-041](./docs/incident-fix-log.md#ifx-041--多次接线拒绝时缺少端口几何并把共享带误称为纯水带)。

普通2011/2012分拣器建造可指定 `initialSorterFilterItemId`，让过滤随原生预建筑在第一次取货前生效；预检返回计划过滤，建成后核对过滤标记及两端连接。默认0仍为无过滤，事后配置不能消除已经进仓的混料。该修复目前为0.4源码/离线验证，安装与实机验收状态见[IFX-040](./docs/incident-fix-log.md#ifx-040--普通建造缺少初始过滤事后配置前已发生混料)，不包含在已发布0.3.3中。

新增的显式蓝图布局可与Foundry物料意图组合为含全部建材、内部流向和逐对象步骤的有限计划，并复用原来的施工/取消/续建入口。输送预算使用原生带速与普通2011/2012分拣器的跨格往返时间约束流量；未知速率和高级堆叠分拣器阻止此组合计划通过，但不缩减普通蓝图的独立支持范围。它不是任意自动布局，也不把满电单件理论预算当作公平分流或实测吞吐。普通2011/2012分拣器另有受控正常拆除，允许缺端修复但拒绝错配的存在边，核对货物count/inc及其他连接。空载和携1件金刚石的2011拆除、分别重建及普通保存已在本机验证；2012拆除、非零inc货物、修复后恢复、新输送预算和完整模块施工仍待同批实机验收。

蓝图支持普通未堆叠仓储箱2101，完整保留禁用格数、默认/过滤模式和逐格过滤；**不复制库存**。实体详情区分仓储格子与建筑连接口，设置变化会使旧选择哈希失效。该仓储施工扩展仍待本机复制验收，不代表已发布包具有该能力。

本地开发版新增 `spherewright_inspect_blueprint` / `spherewright_export_blueprint`：只解析用户明确提供的代码，或导出当前 owned world 明确选定的最多 64 个建筑；不扫描文件。仅支持已列明的基础设备/配置，超限或不支持内容整份拒绝。两个读取工具始终是**只读数据/预览，`executable=false`**。可选位置和方向进行最多32对象的原生现场、科技和整图材料预检。

源码另已加入 `prepare/commit_blueprint_build`、`get_blueprint_builds` 与 `prepare/commit_cancel_blueprint`：执行已批准的有限模块，保留正常材料、无人机和耗时，逐对象记录部分成功；取消不拆已建对象，重启后用新预检只继续未提交部分。同一六对象模块的施工、部分取消、保存恢复后仅续未提交项、外部接入及持续产出已有本机实机验收；净入约29.989石墨/分钟。当前只支持闭合内部输送端点，不自动猜外部接线；该限定正例不等于任意布局、全厂持续供给或0.4完成。详见[当前状态](docs/current-status.md)与[包内Agent playbook](./docs/agent-playbook.md)。

`spherewright_prepare_upgrade` / `spherewright_commit_upgrade` 已实现普通制造台 Mk.I/II/III 同族高阶升级，以及普通分拣器2011→高速分拣器2012：需要已解锁的完整高阶设备和一个退款空格，正常扣新设备、返还旧设备，核对配置、货物与连接。制造台重置原生加工进度；基本分拣器保留周期比例并按原生规则改变速度。传送带/高阶堆叠分拣器升级仍未开放，不混同于已通过的限定蓝图模块与 Governor 两倍观察门。以上是未发布的 0.4 切片，不代表现有 0.3.x 包已有这些工具。

科研选择新增可选 `prioritizeQueued=true`，仅通过原生研究队列排序把指定的、已排队且前置已完成的科技前移；保留其他顺序、已投入研究和库存，并读回核对。它不增加科技进度，也不替代研究材料和耗时。默认仍为追加排队。

### 安装与连接

使用 Thunderstore Mod Manager 或 r2modman 时，安装 `Arcueid_77-Spherewright` 及其 BepInEx 依赖，并从对应 Mod profile 启动一次游戏。MCP 可执行文件位于：

```text
BepInEx/plugins/Arcueid_77-Spherewright/Spherewright.Mcp.exe
```

把这个 EXE 注册为外部 Agent 应用中的本地 stdio MCP Server；命令行不需要也不应附带运行时描述文件、认证 token 或存档名。手动安装请下载 [GitHub Releases](https://github.com/AvaloNero/Spherewright/releases) 中同版本的 `Spherewright-<version>-win-x64.zip`，并按[发行版安装说明](./docs/release-installation.md)操作。

Spherewright 默认只能观察。需要实际操作时，在所用 profile 的 `BepInEx/config/dev.spherewright.bridge.cfg` 中设置：

```ini
[Safety]
AllowWrites = true
```

然后重启游戏和 MCP 连接。所有写动作仍会执行 `fresh read → prepare → commit → terminal/readback`，不能绕过游戏成本和耗时。

### 从新档开始

1. 让游戏停在空闲主菜单，不要先载入其他世界。
2. 确认 `Safety.AllowWrites=true` 并重启游戏。
3. 告诉 Agent“创建一个 Spherewright 新档并从零开始”。
4. Spherewright 会通过游戏的新建流程创建和平、非沙盒、1× 世界，并使用内部生成的 `Spherewright_New_*` 名称保存。
5. Agent 在第一次行动前应读取随 MCP 一起发布的 opening-movement playbook，避免在出生舱旁反复撞同一路径。

### 读取旧档继续玩

1. 除 `Safety.AllowWrites=true` 外，再设置 `Safety.AllowUserSaveImport=true`，然后重启游戏。
2. **由玩家在《戴森球计划》菜单中手动选择并载入目标和平单人存档。** Spherewright 不提供任意存档选择器，也不会枚举本机存档。
3. 告诉 Agent“准备接管当前存档”。Agent 只能先做无副作用预检，并把返回的说明展示给你。
4. 在预检完成后的另一条消息中明确确认。Spherewright 才会用游戏正常保存 API 创建一个 `Spherewright_Imported_*` 独立副本，并复读存档头证明成功。
5. 原档不会被覆盖、改名或删除；之后玩家和 Agent 都在新副本中继续。该副本的 Journal 从导入时开始，不伪造导入前的首次产物、科技或升级记录。
6. 后续重启时把游戏留在主菜单，让 Agent 使用受保护的精确恢复票据继续同一个副本。若玩家中途手动操作过，Agent 会丢弃旧快照并重新读取状态。

导入后的沙盒档或非 1× 档可以使用现有普通动作；这只代表兼容，不代表 Spherewright 会调用沙盒作弊能力，也不应与非沙盒 1× 基准档的耗时和资源消耗直接比较。

### 证据、开发与许可

- [v0.3.2：Sol Max 与 Luna Max 双模型黑盒验收对比](./docs/v0.3.2-sol-vs-luna-black-box.md)
- [版本路线与验收门](./ROADMAP.md)
- [协议](./docs/protocol.md) · [安全模型](./docs/safety-model.md) · [经验账本](./docs/experience-ledger.md)
- [构建、打包与贡献说明](./CONTRIBUTING.md)

无游戏 DLL 的核心回归使用 `Spherewright.Core.slnf`；完整 Plugin 构建需先执行 `./scripts/sync-game-refs.ps1`。`./scripts/package-release.ps1 -Version <version>` 会从同一提交同时生成 GitHub 手动安装包和 Thunderstore 包。项目使用 [MIT License](./LICENSE)。

## English

Unreleased v0.4 source adds narrowly scoped native recovery of one empty default2101 warehouse through the existing two-phase dismantle tools: no layers, add-on, connections or cached references; exactly one building item is returned, with surviving entities and inventory preserved. It does not automatically move/rebuild anything or remove occupied/filtered/stacked storage. Offline validation, cold deployment, one local live recovery and a separate normally built replacement have passed; connected production and a covering save/resume remain pending checks (IFX-101).

Spherewright is a structured, safety-first control bridge for **Dyson Sphere Program**. It lets an external MCP-capable Agent observe the live game and perform bounded actions through normal DSP systems—without embedding an LLM, editing saves, injecting items, or driving the UI with screenshots and keyboard/mouse macros.

The project is experimental and under active development. The original **M0 — First Red Matrix** milestone is complete; the current development save has also validated automatic power-engine, plastic, titanium-ingot, titanium-alloy, diamond, gear, electric-motor, water, organic-crystal, titanium-crystal, structure-matrix, particle-container, logistics-drone, and planetary-logistics-station production plus same-star interplanetary flight. Two normally built, powered, and complementary planetary logistics stations completed a real 100-titanium local drone shipment, and two normally built interstellar stations completed real vessel delivery of both titanium ore and silicon ore from planet `102` to the home planet. The home station's titanium and silicon outputs are physically connected to production, the temporary stone-to-silicon input is safely disabled, and a sustained structure-matrix run consumed locally automated plastic, refined oil, and water without Icarus cargo. These capabilities shipped in [Spherewright v0.3.0](https://github.com/AvaloNero/Spherewright/releases/tag/v0.3.0). [v0.3.1](https://github.com/AvaloNero/Spherewright/releases/tag/v0.3.1) added the explicit conversation-confirmed import of a manually loaded save as a new owned copy, and v0.3.2 corrected fresh-world naming and packaged the opening recovery playbook. **v0.3.3** removes sandbox/resource-multiplier authorization gates without enabling sandbox operations. Local packaged-Plugin validation covered a fresh 1× non-sandbox world, an imported peaceful 100× world, and an imported peaceful sandbox 1× world; each performed ordinary actions, built a first structure, and saved normally. The current development target is **v0.4.0 — Overseer, Foundry & Governor**, a combined update preparing the departure star system for interstellar expansion. On 2026-09-05 the owner merged the original v0.5 Foundry and v0.6 Governor scopes into this unreleased version: multi-planet diagnostics, deterministic factory plans, individual restartable construction steps, and measured balancing/expansion of existing lines. Readiness includes research/upgrades, warper/fuel supply, transport capacity and expedition materials; actual interstellar flight and logistics move to v0.5 Voyager. Construction, expansion and overall readiness are still in development.

The last verified runtime evidence includes the exact owned-primary `0.10.34.28529 → 0.10.35.29088` migration in single-player peaceful mode. The reference progression world remains non-sandbox at 1× resources; v0.3.3 additionally has local live evidence for sandbox and 100× saves. This is a last-verified boundary, not a claim about a current running game; see [current status](docs/current-status.md).

## What it provides

- An authenticated, current-user-only Named Pipe between the game Plugin and the local MCP server.
- Structured reads for the player, progression, recipes, build catalog, resources, factory entities—including detailed logistics-station state—power, the local star system, actions, the per-save gameplay journal, a bounded v0.4 multi-planet native production window with independently recomputed theoretical capacity/utilization, cursor-stable per-planet power/logistics plus global-research summaries, and a versioned same-tick diagnostic bundle that joins those public domains without save identities, paths, or write credentials.
- A directly discoverable MCP Agent playbook resource for session entry, terminal polling, energy and harvest approach, construction/production proof, saves, flight recovery, and bounded escape from landing-capsule or factory collisions; the same concise file is included in release packages as `AGENT-PLAYBOOK.md`.
- Two-phase `prepare → commit` actions for movement, harvesting, handcrafting, research, construction, building configuration—including no-inventory-mutation logistics-station storage and output-belt selection—player/storage and conservation-checked station-fleet transfers, refuelling, saving, and recovery.
- Native-tick same-star flight with a separately saved, expiring pre-flight checkpoint that remains reusable only while that exact flight needs recovery, then loses its capability on success and retires after the covering primary save.
- Exact owned-world restart handoff: healthy planned restarts default to the ticket-bound primary; real quarantine may use a qualifying fixed LastExit. Explicitly user-confirmed recovery can instead bind one newer fixed LastExit to its embedded owned identity, approved tick, known progress floor and unchanged full-file evidence, without fallback. Tickets bind the per-save journal identity, tracking boundary and durable sequence; missing/recreated/truncated journals block loading. The exact expired-primary `28529 → 29088` reauthorization has one cold-deployed local live result; it is not evidence of a later restart, fresh factory audit, or production continuity. No save picker or enumeration of unrelated saves is exposed; see [current status](docs/current-status.md).
- Explicit handoff for a player-loaded save: Spherewright first prepares an exact-session, no-game-side-effect plan and the Agent then asks for confirmation in the conversation. Only a subsequent clear approval may create a separately named owned copy; the original is never overwritten, renamed, deleted, or exposed, and journaling starts at the import boundary.
- Per-save first-event journaling for manual output, production-line output, technology selection, and upgrade selection, including wall-clock/in-save time plus the durable-through sequence, pending-write flag, and persistence error.
- Readback, state hashes, short-lived plans, idempotency, single-flight execution, and write quarantine when a result cannot be proved.

Spherewright is a control layer, not an autonomous planner. The external Agent decides what to do; Spherewright supplies typed state, legal primitives, and evidence-backed results.

Unreleased v0.4 development includes bounded native blueprint inspection/export/site assessment and a separate prepare/commit executor with per-object material receipts, cancellation and fresh restart reconciliation. One six-object module has local live acceptance for that lifecycle, external connections and sustained output (approximately `29.989` graphite/min); data/site reads remain `executable=false`. The read-only `spherewright_get_governor_plan` compares supported upgrades, added machines and module copies while separating measured production/consumption, target demand, selected-buffer changes and supply shortfalls. It neither executes an expansion nor certifies balance. The locked `31 → 62/min` declaration passed its bounded live observation gate of `37128` effective game ticks. These results do not close broader Foundry, sustained-supply or whole-v0.4 gates. See [current status](docs/current-status.md). These tools are not present in released v0.3.x packages.

Development site previews also expose an independent advisory native-coverage/full-base-load power assessment, including existing peak loads and newly energized consumers; it is not sustainable fuel proof or permission to build. Governor retains a pre-execution declaration and measures only the union of valid game-tick windows, keeping target-chain findings separate from unattributed planet warnings and never turning an inventory observation interval into a production window. The remaining acceptance boundaries are summarized in [current status](docs/current-status.md).

An optional Governor `parallelExpansionBlueprint` reuses the existing full Foundry budget for the additional rate (target minus measured nonzero baseline), including every explicit module object, native site, full-base-load power and rated transport. Cost scopes disclose unplanned external infrastructure rather than treating it as free. The returned intent/construction hash goes through the existing fresh finite-build protocol; the comparison remains read-only, and post-expansion observation retains the original locked declaration without resubmitting the layout. This new comparison is source/offline evidence, not completed live expansion or a new package.

Unreleased 0.4 ordinary sorter construction also accepts `initialSorterFilterItemId`, installed through the native prebuild before the first pickup. A nonzero request requires an exact filter echo before MCP exposes its plan; a mixed/older Plugin cannot silently produce an unfiltered build. Default0 remains unfiltered. Offline tests pass; matching-build deployment and live acceptance remain pending in [IFX-040](./docs/incident-fix-log.md#ifx-040--普通建造缺少初始过滤事后配置前已发生混料). This does not clean existing stock or guarantee delivery through a mixed belt.

Unreleased 0.4 also adds cargo-preserving filter changes for ordinary inserting sorters and explicit single-warehouse `storage-capacity` configuration: native automation limits, existing-item reservations, empty/same-item filters and clearing reservations. No stock is deleted or moved, held cargo still goes to the same destination, and a successful configuration does not imply a recovered production line. The new slice has 1,066 passing offline tests and a clean full Release build; installation/live recovery remain pending. See the [configuration protocol](./docs/protocol.md#warehouse-capacity-and-reservation-configuration-04-development-slice).

## Architecture

```text
External Agent / MCP host
        │ stdio MCP
        ▼
Spherewright.Mcp                 .NET 8
        │ authenticated local Named Pipe
        ▼
Spherewright.Plugin              .NET Framework 4.7.2 / BepInEx 5
        │ bounded Unity-main-thread work
        ▼
DSP native gameplay systems
```

- `Spherewright.Contracts` contains the public DTOs and protocol contracts.
- `Spherewright.Bridge.Core` contains game-independent framing, safety, plan, fingerprint, and idempotency logic.
- `Spherewright.Plugin` is the thin adapter to the current DSP/Unity runtime.
- `Spherewright.Mcp` exposes the Bridge as MCP tools over stdio.

## Safety model

Writes are disabled by default. When enabled, every gameplay mutation is bound to a current owned session and follows a fresh read, a non-mutating prepare, one idempotent commit, and terminal/readback verification.

Spherewright deliberately does not use:

- sandbox-tool calls, item injection, direct buffer writes, instant construction, technology injection, or game-speed changes;
- save editing, save enumeration, or loading an arbitrary save name;
- external memory scanning or modifications to `Assembly-CSharp.dll`;
- Computer Use, visual recognition, or keyboard/mouse macros for game operations.

All DSP and Unity access runs on Unity's main thread. Only deep-copied DTOs leave that thread. Ambiguous write outcomes quarantine further commits until the exact retained action can be proved or the same owned world is safely restarted from protected evidence.

See [current status](./docs/current-status.md), [ROADMAP.md](./ROADMAP.md), [docs/protocol.md](./docs/protocol.md), the [save diary index](./docs/save-diaries/README.md), the [incident/fix log](./docs/incident-fix-log.md), and the [experience ledger](./docs/experience-ledger.md) for the current boundary, approved 0.3–1.0 plan, protocol, per-save history, engineering fixes, and accumulated operational evidence. The independent [v0.3.2 Sol Max versus Luna Max black-box comparison](./docs/v0.3.2-sol-vs-luna-black-box.md) shows two external Agents completing the same automatic Electromagnetic Matrix goal from clean worlds.

## Requirements

The currently supported runtime scope is deliberately narrow:

- Windows x64
- Dyson Sphere Program (unreleased v0.4 package target: `0.10.35.29131`; the same owned primary has local protected-recovery evidence from R1311/R1318, while final same-commit manual and Thunderstore packages still need separate installation/runtime validation; released v0.3.x historical local validation: `0.10.34.28529`)
- BepInEx `5.4.17.0`
- single-player
- peaceful mode
- any sandbox setting or resource multiplier; both are reported in session evidence and do not authorize additional actions

The validated reference world remains non-sandbox with 1× resources. v0.3.3 locally validated both a peaceful sandbox 1× save and a peaceful non-sandbox 100× save through import, ordinary gameplay, construction, and normal saving. Spherewright does not currently guarantee Dark Fog/combat, multiplayer or Nebula, broad third-party Mod compatibility, an arbitrary save picker, or loading an arbitrary caller-supplied save name.

The versioned Windows release package includes a self-contained MCP server; using it does not require the repository, source code, or a .NET SDK. See [release installation](./docs/release-installation.md).

Source builds additionally need:

- .NET 8 SDK
- PowerShell 7 recommended for the helper scripts

No game assemblies are committed to this repository. They remain local and are copied only into the ignored `.local/game-refs` build directory.

## Build and test

Core contracts, Bridge logic, and MCP tests do not require game assemblies:

```powershell
dotnet restore Spherewright.Core.slnf --locked-mode
dotnet build Spherewright.Core.slnf --no-restore
dotnet test Spherewright.Core.slnf --no-build
```

To build the BepInEx Plugin, first sync the minimal compile references from your local DSP/BepInEx installation, then build the full solution:

```powershell
./scripts/sync-game-refs.ps1
dotnet build Spherewright.sln --no-restore
```

If DSP is installed somewhere the locator cannot find automatically:

```powershell
./scripts/sync-game-refs.ps1 -DspDir 'D:\Games\Dyson Sphere Program'
```

The Plugin output is `src/Spherewright.Plugin/bin/Debug/net472/Spherewright.Plugin.dll` by default, or the corresponding `Release` directory when built with `--configuration Release`.

To produce both supported distribution formats and their SHA-256 sidecars from a clean worktree:

```powershell
./scripts/package-release.ps1 -Version 0.4.0
```

The command creates `Spherewright-<version>-win-x64.zip` for manual installation and `Spherewright-<version>-thunderstore.zip` for Thunderstore/r2modman. The manual package contains the full self-contained MCP directory and installer; the Mod package uses a single-file MCP executable so BepInEx does not scan its runtime dependencies as Plugins. Both archives carry Spherewright integrity metadata and are written under the ignored `artifacts/` directory. The Thunderstore namespace is `Arcueid_77-Spherewright`.

The manual package keeps its extracted-file and MCP handshake smoke test. The Thunderstore package is checked for its required root files, manifest/dependency metadata, 256×256 icon, exact payload hashes, and forbidden bundled game/loader assemblies; runtime installation is then validated in a separate Mod Manager profile or another computer. Creating artifacts does not create a tag, GitHub Release, or Thunderstore version; publication remains gated by [ROADMAP.md](./ROADMAP.md).

To repeat the package integrity and self-contained MCP `initialize`/`tools/list` smoke test independently:

```powershell
./scripts/test-release-package.ps1 -PackagePath ./artifacts/Spherewright-0.4.0-win-x64.zip
./scripts/test-thunderstore-package.ps1 -PackagePath ./artifacts/Spherewright-0.4.0-thunderstore.zip -ExpectedVersion 0.4.0
```

## Local setup

1. Copy `Spherewright.Plugin.dll` into a dedicated folder under DSP's `BepInEx/plugins` directory.
2. Launch DSP once so BepInEx creates `BepInEx/config/dev.spherewright.bridge.cfg`.
3. Keep `Safety.AllowWrites=false` for observation-only use. Set it to `true` only when you intend to authorize structured gameplay commits. To hand a manually loaded save to the Agent, also set `Safety.AllowUserSaveImport=true`; the default is `false`, and import still requires a fresh prepare followed by your explicit confirmation in the conversation.
4. Start the MCP server from the repository:

   ```powershell
   dotnet run --project src/Spherewright.Mcp/Spherewright.Mcp.csproj
   ```

5. Register that stdio command with your MCP host.

Runtime descriptors and credentials are protected for the current Windows user and rotate when the Plugin starts. Do not copy them into logs, issues, or configuration files.

## Quick start

### Start a new world

Leave DSP at its idle main menu, set `Safety.AllowWrites=true`, restart DSP, and ask the Agent to create a new world. Spherewright uses DSP's normal peaceful, non-sandbox, 1× new-game flow and saves it as `Spherewright_New_*`. Before the first gameplay action, the Agent should read MCP resource `spherewright://agent/playbooks/opening-movement-v1`; it explains how to leave the landing capsule without replaying a stalled target. Existing `Spherewright_M0_*` worlds keep their original names and remain eligible for their exact protected resume tickets; Spherewright does not migrate or rename them.

### Continue an existing save

For a **previously owned** world whose 24-hour restart credential has expired, development 0.4 adds `reauthorize_expired_primary` to the existing resume flow: preview the exact primary and original Journal, show the disclosure, then wait for a new explicit confirmation before commit. It does not import a new copy, extend the old credential, select arbitrary saves or reconstruct history. Evidence drift refuses loading; interrupted attempts require manual reconciliation. The exact expired-primary `28529 → 29088` path has one cold-deployed local live result; the exact `29088 → 29104` extension is still awaiting live recovery. Separately, the active-primary `29104 → 29131` version-change flow completed protected recovery at R1311 and a subsequent healthy recovery at R1318. See [current status](docs/current-status.md), [the expired-primary playbook](docs/agent-playbook.md#expired-planned-restart-credential), and [the 29131 version-change playbook](docs/agent-playbook.md#active-primary-after-the-exact29131-version-change).

Set both `Safety.AllowWrites=true` and `Safety.AllowUserSaveImport=true`, restart DSP, and manually load the intended peaceful single-player save. Ask the Agent to prepare an import. It must show the returned disclosure and wait for a later explicit confirmation from you before commit creates a separate `Spherewright_Imported_*` copy. The original save is not overwritten, renamed, deleted, or selected by the import API. Sandbox state and resource multiplier are reported but do not block the import or later normal actions. From then on, both you and the Agent should continue in that copy; after restart, leave DSP at the main menu and use protected resume. After any manual play in the owned copy, the Agent must discard stale observations and plans, read the live state again, and prepare later writes against the current state hashes.

The prefixes are labels, not ownership proofs. A manually loaded save is restricted even if its name looks like a Spherewright name; ownership requires the exact armed new-game transition, a confirmed imported-copy Header proof, or an exact protected resume ticket. An imported save receives a new journal whose coverage begins at the import point and does not invent earlier first-time events.

Repository evidence distinguishes offline build/test and package checks from local live and cross-computer live validation. v0.3.3 has local end-to-end import evidence for both sandbox and non-1× fixtures; this does not claim a separate cross-computer run.

## Development status

Local blueprint/Governor throughput results do not by themselves verify declaration restoration after restart, 2012 sorter removal, nonzero-inc cargo preservation, post-repair resume or the complete composed transport budget. Their narrower historical evidence must be reconciled independently. Cross-computer and final-package recovery validation also remain separate from local-live recovery.

The development source composes an explicitly chosen blueprint layout with Foundry intent, complete object costs, directed flow allocations and finite dependency steps through the protected build/cancel/resume executor. It is not arbitrary auto-layout, fair splitting or measured throughput. Current operational state, completed bounded stages and remaining live gates are kept in [docs/current-status.md](docs/current-status.md); the [save diary](docs/gameplay-timeline.md) retains the detailed historical record.

The release gates live in [ROADMAP.md](./ROADMAP.md). The current save's complete decision, research, upgrade, and first-output chronology lives in its [save diary](./docs/gameplay-timeline.md), indexed with every owned save in [docs/save-diaries/](./docs/save-diaries/README.md). The short version:

- secure local Bridge and MCP surface: complete;
- ordinary owned-world observation and action primitives: complete for the validated peaceful reference world; v0.3.3 locally validates both sandbox and non-1× compatibility while keeping sandbox operations outside the tool surface;
- first automatic red matrix: complete;
- automatic power engine, plastic, titanium ingot, diamond, gear, electric motor, water, organic crystal, titanium crystal, structure matrix, electromagnetic turbine, high-purity silicon, microcrystalline component, sulfuric acid, processor, graphene, thruster, particle container, logistics drone, and planetary logistics station production: complete;
- native same-star checkpointed flight: complete for the validated route;
- planetary/interstellar logistics: released in v0.3.0 after clean-install, protected-resume, live-Bridge, installed-MCP, and same-save regression;
- Overseer multi-planet diagnostics: the diagnostic slice has completed candidate validation, including a journal-continuity guard discovered during isolated validation. The owner has since merged Foundry and Governor into v0.4.0 to prepare for interstellar expansion; the combined version is in development and the older diagnostic-only candidate is not the final release. The live implementation pages every already-created owned factory, combines native 600-tick production/consumption rates, runtime-derived theoretical capacity/utilization, per-planet power/logistics, global research, bounded direct and recursive root causes, and a `public_allowlist_v1` same-tick diagnostic bundle. It follows exact item-admitting belt/sorter/splitter and logistics-station routes across unloaded factories, persists logistics progress per protected owned save, and exposes no raw save identity, path, auth token, or write credential. Real shipments covered dispatch, 2,100+ moving ticks without a false stall, pickup, delivery, restored `12 min⁻¹` titanium-ingot production, and two active-route save/normal-exit/exact-resume cycles that excluded offline wall time. Reversible live trials distinguished and repaired `logistics_blocked`, `material_shortage`, and `insufficient_power`; the power trial reached about 58.15% service before normal restoration to ratio 1. The Journal refresh passed a checkpoint-bearing `49/49` normal resume plus protected missing and sequence-48 truncation negatives: both stopped at prepare without consuming the token or starting the loader, and restoring the exact document allowed that same ticket to resume normally. Clean commit `8c49bcb` passed 262 tests, Windows CI, both package checks, exact installed-file verification, protected same-save resume, installed MCP/playbook, and a three-factory public diagnostic bundle. No `v0.4.0` tag or Release is created until the owner approves the final evidence and notes. A true 600-game-tick frozen-carrier trial remains an explicit live-coverage limitation: the validated DSP build has no safe normal-game control that freezes an already-dispatched carrier while preserving the same order and route, so Spherewright does not fabricate it through direct runtime-field writes.

Combined v0.4.0 acceptance requires Foundry's complete three-stage factory plan and mid-build save/restart/resume without duplicate entities or material charges, plus Governor's doubling of an existing line's throughput for at least ten game minutes within its declared tolerance. The departure-side readiness checklist must also prove the required research/upgrades, automatic warper/fuel replenishment, transport capacity and expedition supplies. External Agents choose the module, site and goal; an approved finite executor may advance bounded native blueprint construction with per-object evidence, normal materials/drones/time, cancellation of unexecuted work and fresh restart reconciliation. It must not run an autonomous expansion loop or replay a partially completed module. Native same-family upgrades are another permitted per-entity primitive. The original v0.5 Foundry and v0.6 Governor scopes now belong to v0.4; Voyager moves from v0.7 to v0.5, Ascension from v0.8 to v0.6, and Release Candidate from v0.9 to v0.7. v1.0.0 remains the promotion target. This is the development plan, not a claim that those gates are complete.

There are no stability or compatibility guarantees yet. Before reporting a bug, include the DSP version, BepInEx version, Spherewright commit, the structured error code, and sanitized action/state evidence—never auth tokens, plan tokens, raw save identities, or save files.

## Contributing

Spherewright is currently a personal project and does not accept pull requests before `1.0.0`. Hands-on testers are welcome to open Issues for reproducible problems. See [CONTRIBUTING.md](./CONTRIBUTING.md) for the evidence and privacy guidelines.

## License

Spherewright is available under the [MIT License](./LICENSE).
