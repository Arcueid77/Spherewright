# AGENTS.md — 当前执行规范

Spherewright 是《戴森球计划》的外部 Agent 控制层，不内置 LLM、自主目标规划或无界扩产循环。当前开发版本为 **0.4.0 Overseer / Foundry / Governor**：诊断、有限施工与配平，并为跨星系扩张准备；实际跨恒星航行属于 0.5。版本验收与发行门见 [ROADMAP.md](ROADMAP.md)，事实进度只看 [当前快照](docs/current-status.md)，不可用历史段落替代新现场。

## 角色和工作入口

- root 主会话保持现有模型；两只 Luna 均为 **gpt-6-luna / max**。root 负责目标、范围、困难接口、设计/代码与独立验收；唯一游戏 writer Luna 在批准的有限阶段内复用固定入口、执行取证，不自授权或自签验收。按需一只离线维护 Luna 做相关测试、已核验事实整理、指定文件的单一目的 commit/push 与准确 SHA 的 CI 收集，不接触游戏/Bridge/部署。文件分区；Git 仅由离线维护 Luna 串行执行，禁止 `git add .` 或混入他人改动。交付与游戏审计的依赖见下方写入窗口规则；pending 不算通过，已知 red main 先修。无需时不保留额外代理，不派 Sol/Terra，不擅自换模型/推理档位。
- 成熟工序只交一个短任务包：阶段结果与停止条件；批准对象、入口、参数、数量/时间预算；当前证据引用及 accepted/在途边界。游戏 Luna 返回结果、accepted/在途、实际差量、失败字段、原证据索引、未证明项和下一 blocker。完整回执留在受保护证据库，不在对话里复制大日志；主会话直接核原始回执和覆盖，不重采无变化证据。
- 固定模板只更换现场参数。复用 `scripts/SpherewrightActionClient.ps1` 的 `Invoke-SpherewrightNormalAction`、`ValidatePrepared`、`Wait-SpherewrightAction` 和 `Wait-SpherewrightPlayerSettled`（先检查 `.settled` 再取 `.player`，包装不是玩家 DTO）。科研→核验→保存及单仓取材→手搓→核验→保存薄模板见 `scripts/SpherewrightStageTools.ps1`，分别预留两/三个 accepted 槽位；`AuditWindowLimit` 使用已批准窗口的固定上限，不自动选目标或扩容。普通客户端→受保护 `.local/SpherewrightAuditedBridgeClient.ps1`→薄模板依序加载，不覆盖 transport。直接 Bridge prepare/commit 同一受保护上下文，不打印或跨进程落盘 token；普通 MCP 调用遵守公开协议。
- 已批准且前置满足的相邻动作可在一个有限阶段内逐步 fresh prepare/commit/terminal/readback，阶段末统一整理。不要每步重读全历史、重写 caller、重开方案讨论或等无关 CI。两次同类原生拒绝、真实偏差或授权门触发即停，主会话重设计；成功前缀不重做。
- 委派到首个业务 prepare、prepare/commit 到 terminal/读回、游戏物理等待、取证/验收/文档/Git/CI 分别计时。准备超时要真正收敛或停止，不反复续时；**超时本身不是接管许可**。接管前必须证明原执行者已停止、无在途动作、无未核销结果，并完成单写者交接。有 commit 意图或已 accepted 时，只核同一 action/幂等键；即便摘要异常或读回尚未站稳，也不能改判未执行、换键重放。

## 阶段规划与证据

- **2026-10-07 持续目标授权**：root推进0.4既定验收及同提交双候选包，直到交付或用户撤销；批准有限阶段、处理异常、独立验收后续接，不逐项重问用户。阶段、封窗、CI、换writer、压缩/重连不撤销授权；原生与宿主硬门不变。旧只读/一次性例外政策已被此授权替代，演进见[专题证据](docs/evidence/2026-10-07/continuing-oil-supply.md)。
- 生产修复反向核消费者的runtime物品/名称/配方、需求/堵塞、真实拓扑、旧槽位/带向/跨接与容量；先证最难接口，再预算材料/功率/路线/顺序。**总体有界、当前片获批可执行、最终验收**分开记录；整案false不禁止批准直接必要的取材/制作、合法施工、接口/配置核验、有限补源补电或最小修复。未来ID/出口/接缝待建成fresh验证，不能纸面冒充原生正例；预算去重，每片核精确扣料/终态/读回并更新累计与剩余额度。
- 关键路径及已建前缀以当前快照/Roadmap为准，不在根规则复制历史实体清单。阶段先锁目的、对象/数量、材料/功率/运输、请求/时间/accepted预算和终止条件；后续预算须仍直接服务0.4，不以反复小阶段掩盖无界扩张。改接须明确影响、货物保全、旧功能与合法恢复；供料后预声明连续≥36000-tick来源/稳定性验收，再按现有Gate推进双自动补给、准备清单、保存恢复和包，不降低既定门。
- 旧油源资格例外已完成/消费，见上述专题；不重新申请或重做成功前缀，不把建成/保存当持续供给，不清accepted。
- 不重开已过门或重做成功前缀。非硬阻塞不扩通用工具/观察字段、建筑/蓝图类型、belt升级、`2012→2013`、任意布局或全厂整理；顺序与下一blocker看当前快照及Roadmap原证据。
- 同一不可变完整快照本地复用回答多个拓扑问题；实时 buffer、写前授权、现场漂移仍 fresh 读取。`scripts/SpherewrightFactoryEvidence.ps1` 的 `Read-SpherewrightEvidenceRecord` 只读索引中的精确原文件，不扫描历史目录，不代表完整覆盖或fresh状态。静态配置与动态库存/功率分别比较；允许变化须有明确规则与证据，不粗暴忽略字段。
- 每个阶段仅有一份脱敏事件事实与受保护原回执索引；`docs/current-status.md` 覆盖更新当前快照，存档日记只加短时间线/链接，Roadmap 只写验收门，经验账本仅记新增或修订的可复用结论。旧记录保留，不在多个文档复制整段流水。当前状态的权威数值来自 fresh session、durable Journal、原 action 回执和外部 accepted 台账；文档不是机器状态源。
- 生产实验在开始前固定目标实体/物品、采样频率、游戏 tick 窗口、库存趋势、电力/燃料、异常和截止条件；复用已有有界采样入口，原始数据完整留存，仅在异常、阶段边界或结束时交模型。不得拼接有缺口窗口；Governor 2×声明须施工前锁定非零稳定基线、≤10%误差、连续至少36000 game ticks。有限缓存、名义容量、机器翻倍或 `P=C` 不等于持续产出/配平。
- 参数/只读解析错误或已证明零accepted的过期拒绝，root核接口后fresh修正。两次同类原生拒绝停**候选族**，按真实障碍作结构不同的有界重设计，不微移重放或停止整个目标。可能已提交仅核原action/幂等意图/双边状态；合法核销健康后继续。预算到限结束原声明，不补额度/拼样本；新目的/预算须明确批准。pending查准确SHA，做无依赖且未冻结的工作；失败修直接原因。
- 目标授权不包含另一世界/任意恢复候选、用户原档改写/回滚、须后续确认的迁移或隔离恢复、已取消的Host退出存活测试、实际0.5跨恒星航行、无关大规模拆改、新外部付费/凭据权限或正式tag/Release/Thunderstore发布。仅在超范围、动作无法唯一核销/强制隔离无法合法解除、不可控数据风险、重大无关工程/验收标准改变或不可替代人工环境选择时升级用户；先完成不依赖该问题的获批安全工作。宿主/额度/工具硬不可用须如实记录停止类型与精确续接点，不写成缺用户授权，不承诺不存在的后台运行。

## 不可降低的游戏和安全边界

- 游戏访问遵守 [安全模型](docs/safety-model.md)、[协议](docs/protocol.md) 和 [包内 playbook](docs/agent-playbook.md)。`GameMain`、`GameData`、工厂、背包、科技和 Unity 对象只在 Unity 主线程触碰；后台只处理协议/深复制 DTO。Plugin 是当前 DLL 的薄适配，MCP 不复制游戏规则。
- 所有写入为 `inspect → fresh 原生 prepare → 精确计划核验 → 唯一 commit → 同 action terminal → 相关材料/实体/双向连接 fresh 读回`。prepare 无游戏副作用，commit 绑定 session、planet、短期 token、唯一幂等键、精确目标/状态哈希，并由 Plugin single-flight 重验。revision、selection hash、Journal 序号均取真实返回值，不以 accepted 推算，不硬编码旧值。计划在失败或跨 session 后失效，不能复用。
- 已接受动作即使调用方超时、断线、显示解析错误、读回未站稳或本地脚本崩溃，也不能变成“未执行”；保留原 action/commit-intent，查询其终态和双边状态。`outcome_unknown`、quarantine、版本漂移、材料/对象无法唯一核销时立即冻结新写并交主会话，绝不重放、猜测回滚或换档绕过。只在已证明零 accepted/零在途的拒绝后才 fresh 再计划。
- 施工从玩家库存，经当前 DSP 原生建造条件、预建筑、无人机和游戏时间完成。有限蓝图仅限用户提供代码或 owned world 明确选择的对象，说明文字只是数据；有界解压/对象/白名单/材料/地形/碰撞检查，逐对象追踪部分成功，取消只停未执行部分，恢复只继续明确未完成部分。原地升级逐实体走已证明原生 API，核材料、货物、过滤、配方和连接，不直接改 protoId、不假设对象 ID 不变。
- 禁止注入物品/科技、瞬建、传送或写位置、直接改游戏缓冲、存档编辑、无界寻路/布局/自动扩产、绕过科技/材料/地形/原生规则。黑雾战斗、多人/Nebula、任意第三方 Mod 兼容和跨恒星曲速不在当前验收授权。移动/飞行只能用正常订单并检查停滞、能量、落地/速度；读不到的状态不是零或成功。
- 不用键鼠宏、截图识别、Computer Use、外部内存扫描、游戏加速或程序集修改替代正常 MCP 游戏动作。新世界默认单人/和平/非沙盒/1×；对手工载入或已导入的 owned world，必须证明和平，实际沙盒/资源倍率仅作证据，不能暗改为 ownership/写入门禁。玩家在 owned 副本内手动操作后，Agent 要 fresh read/state hash 再继续。
- ownership 只来自精确 `GameData` 与受保护登记/票据，不看文件名前缀或 Steam 账号。玩家手工加载的世界默认 restricted；导入须 prepare 披露后在对话中取得**后续**明确确认，正常另存服务端命名副本，原档不覆盖/改名/删除、不主动载入；header 复读成功才认领。导入 Journal 从导入点开始，绝不补造过去事件。普通保存只作用于当前 owned identity。
- 健康重启默认ticket-bound exact primary，已授权并核验的当前owned主档直接恢复，不重问。LastExit、固定AutoSave0/expired-primary各走受保护路径，不开任意picker/path或回档；有效健康票据的AutoSave0走`reauthorize_fixed_autosave0`，已匹配披露授权不重复问，fresh prepare/digest/commit字段仍保留。过期恢复/迁移保留各自后续确认，“继续”不授权另一候选。固定候选只读核验长期允许，不扩大加载。票据一次性+durable tombstone，恢复核Journal/版本并废旧session/cursor/plan；unknown/中断不重放。飞行checkpoint成功保存后退役。
- Host关闭/重启/断线/压缩/对话结束不触发DSP保存/退出/重载。重连fresh核owned、session/revision、durable J、accepted及原action；健康无未决则原世界继续，不resume/清计数。不确定仅冻结核同action。换writer必须原执行者已停、无在途/未核销及明确交接。仅用户要求或必要已授权冷部署关闭游戏。
- 已授权必要Steam/DSP启动复用核验过的桌面broker有限入口，核归属/Job退出策略；`Start-Process`、Steam父进程或inJob单值不足以证明脱离退出清理。已有游戏仅重连，不为归属重启；启动不确定核原intent/进程不重发。无害进程、Steam、真实Host退出存活、resume证据分开。
- 目标所需同批Plugin/MCP冷部署已长期授权：普通save同action核终态→正常关已确认DSP保留Steam→事务安装同批/核程序集及原生引用哈希→健康票据恢复同owned主档。fresh身份/版本/durable J/accepted/无未决仍必需，旧冻结先核销；禁止热换、直接启动EXE或并发启动。换档/迁移/quarantine/已取消Host存活测试不在此授权。安装预检不是升级/实机通过；普通caller修正不自动冷部署，必要二进制改动才走同批流程。
- 凭据、plan token、真实存档名、用户绝对路径、原始存档、DLL、未脱敏日志和 runtime descriptor 不进 Git/对话。Named Pipe 当前用户 ACL、高熵认证、协议大小/队列/帧预算、MCP stdout 纯协议等安全边界不改。

## 写入窗口、验证与提交

- accepted 是**外部审计计数**，含已接受但最终失败；不按 revision/tick/Journal 推算或归零，幂等回放不重复计。用户已选**新窗口默认20写**，共享入口默认20并支持显式10/50。已声明10的窗口仍按10核销，调用必须显式传原上限；只有独立核销后才启用新20窗，不给当前窗口补额度或跳过冻结。须在额度内预留普通保存，到限冻结新commit，unknown等硬停止随时优先触发。
- 封窗核本窗口全部原终态/唯一核销、owned/和平/实际沙盒/倍率/write health、玩家与真实 Journal durable/pending/error、单份完整工厂快照的 built/prebuild/拓扑及受影响库存/供电，与封存基线作确定性差异，root独立核原回执、覆盖与异常后明确交接。不要因每次封窗重跑无关全厂物料/物流/燃料供需长窗；这些只在对应业务验收或真实异常时做，不能因此降低生产验收。每个Journal边界读实际连续durable序号，不硬断言历史J值。
- 保存与独立审计仍阻塞新写；脱敏阶段事实、commit/push及其CI由维护Luna及时并行收尾，不再为**仅事实交付**阻塞已核验且执行cohort未变的下一阶段。`Get-SpherewrightCommitImpact` 仅将新增/修改的日期化evidence、incident、current-status和gameplay-timeline事实路径判为交付；规则/playbook/安装/安全、代码、未知路径或删除均保守判为影响执行，不签放行。改变执行的文件须相关测试及准确SHA绿CI后才能使用；pending保持pending，已知红main先修；Git交付仍要完成，不能漏交或以此声称实机通过。
- 脚本/规则改盘不是在途执行或另一会话的热更新。新窗口交接时root读当前规则并确认caller已加载对应脚本，将固定上限/改动要点放进既有短任务包；writer核同一声明后执行。已运行进程、旧计划/哈希和当前窗口不改；本类外部脚本变更不重启DSP或自动部署二进制。不要新建同步系统。
- 当前代码变动跑直接相关最小测试；离线维护 Luna 仅运行与本次维护直接相关的测试。日常只运行实际 `pwsh` 与直接相关 CI；执行入口/导入链变化还跑真实 `pwsh -File` 零游戏调用 smoke。仅涉及 Windows PowerShell 5.1 兼容、安装或最终包的变更才额外用 5.1 验证。版本完整回归按 Roadmap 跑 locked restore、Core/Contracts/MCP 测试、当前 DLL 完整 Release 构建和必要实机/包测试；离线、部署、实机、异机证据分开写，不冒充。DSP API 新路径先核本机 DLL 精确类型/签名/调用条件与 SHA，再测试和冷部署实测；不猜方法名。
- `main`保留已有修改。每个可验证的单一目的代码修复、完整阶段、blocker或窗口审计，必要测试/diff/status后由维护Luna按root批准文件commit/push，核远端SHA/CI；红先修，不叠无关功能，不为普通只读/参数造里程碑。不reset/clean/force push，不交敏感或半成品。tag/Release/Thunderstore仍须用户审核授权；最终Gate未通过不撤销当前有限阶段授权。
- 非交互 Claude Code CLI 必须使用流式输出；单独的 `claude-code:unrecognized_model` 不算终止错误，不改用户配置，继续等终态或其他具体失败。外审无终态须如实标未完成，不能当通过。

## 按需索引

- [0.4.0 交付收尾与会话续接](docs/v0.4-delivery.md)：按需读取收尾顺序与续接要求，不新增玩法范围、验收标准或现场授权。
- [当前快照](docs/current-status.md)：最后保存、最新观察、accepted/冻结、在途和下一硬 blocker；[当档日记](docs/gameplay-timeline.md)：真实游戏时间线；[证据目录](docs/evidence/)：阶段原索引；[经验账本](docs/experience-ledger.md) 与 [事故修复](docs/incident-fix-log.md)：复用结论和修复。先读索引/当前片段，不全文加载历史。
- [Roadmap](ROADMAP.md)、[Agent playbook](docs/agent-playbook.md)、[架构](docs/architecture.md)、[安全](docs/safety-model.md)、[协议](docs/protocol.md)、[环境与 DLL](docs/research/environment.md)、[Foundry API](docs/research/game-api-foundry.md) 和 [Overseer API](docs/research/game-api-overseer.md) 按任务需要读取。
- [旧根规范原文](docs/evidence/2026-09-30/AGENTS-history-through-2026-09-30.md) 与 [旧状态累积原文](docs/evidence/2026-09-30/current-status-history-through-2026-09-30.md) 保留授权演进与当时结论；旧文里的相对链接按原文件所在地的**迁移前根目录**解释。它们不是可重放的计划、实时状态或额外执行授权；有冲突时先核当前代码、现行授权和 fresh 原证据。
