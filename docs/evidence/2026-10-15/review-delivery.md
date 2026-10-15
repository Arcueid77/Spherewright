# Spherewright 0.4.0 候选交付审查（草稿）

状态：本地验收事实汇总，现供所有者审核；这不是发布批准。候选 ZIP 固定来自源码 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`；后续证据文档不改变候选来源。

## 冻结候选与构建

| 候选文件 | 大小 | SHA-256 |
| --- | ---: | --- |
| `Spherewright-0.4.0-win-x64.zip` | 36,759,398 bytes | `665FA1B30A86A4D49AC7E69CE174E08348DCA07D159A24AF766ABED89AFD79CB` |
| `Spherewright-0.4.0-thunderstore.zip` | 32,492,607 bytes | `81D9D6ED0BAEC843AB22ADE38158102212A5487A43EFEE22769F500B7CF85D91` |

准确源码 SHA 的 [Windows Core CI 38627772732](https://github.com/AvaloNero/Spherewright/actions/runs/38627772732) completed/success，测试为 Contracts 73/73、Bridge.Core 2,777/2,777、MCP 258/258，合计 3,108，失败和跳过均为 0。正式打包入口在 5/5 当前原生引用上完成 locked restore、完整 Release 构建、两种包检查和 self-contained MCP 发布；构建无警告/错误。fresh Native 菜单为 DSP `0.10.35.29145`。实际 BepInEx `FileVersion` 与 `ProductVersion` 均为 `5.4.17.0`，文件 SHA-256 `DC1CB6B58B962BDA5AAA1D6B5F9AE14EC174F61836A1A1F96C1A040C7E8381F7`。逐文件构建与包证据见[候选构建报告](delivery-candidates-2a7a98d.md)及[当前 DLL 验证](delivery-build-validation.md)。

两种布局的 MCP 接口检查均覆盖 64 个必需工具、参数/schema、annotations、1 个 resource 和 playbook 一致性。手动包的完整 self-contained MCP 由正式 harness 启动，退出码 0；Thunderstore 包中的实际单文件 EXE 另经完整 surface harness 验证，退出码 0、stdout 为协议数据。后者另有 7 次 MCP RPC，其中 3 次只读游戏调用、0 次游戏写入。没有用验证 fixture 代替交付 ZIP；fixture 仅用于复用检查 harness，原包文件逐项比对一致。

## 本地实装与恢复证据

手动候选完成本地事务安装，228 个产品文件、原恢复票据、Journal `J102` 和 602 条受保护 handoff 条目经独立核验保全。同一 Native `0.10.35.29145` owned 主档的恢复 action 完成；恢复时保存的输入段是在输出段尚未施工时形成的检查点。恢复后原五对象与新增 30 个输入对象均保全，未重建已完成前缀；随后另行完成输出段。具体范围见[手动候选恢复记录](manual-candidate-install-resume.md)、[输入检查点](three-stage-input-checkpoint.md)和[输出记录](three-stage-output-prefix.md)。

Thunderstore 原始候选以标准共置载荷方式安装在同一本地 DSP 根目录：4 个 Plugin 文件与单文件 MCP；配对事务保留既有 manual MCP 文件和 handoff 条目，229 个产品文件、5 份 Native 引用与源码版本核验通过。Native 菜单、同版本恢复与实际 MCP 只读协议调用均有记录。该结果证明本地标准载荷布局，不证明 Thunderstore Mod Manager/r2modman profile 部署。细节见[Thunderstore 安装、运行与恢复记录](thunderstore-candidate-install-resume.md)。

有限施工和恢复证据还包括：原五对象蓝图的限定范围内重启续建；输入已保存、输出尚未施工时的中途恢复；后续完成 34 个新增对象及完整工厂读回。原五对象的旧拓扑标记仍保留，不能据此重放旧 blueprint build。kit施工时的保存截面为 tick `116309335` / revision `13` / Journal `J102`，观察 tick `116309535`，窗口当时为 opening lifetime `624`、`8/20`、lifetime `632`、whole `233`；这是最终整案恢复前的历史状态。随后完整 kit 在 Native `0.10.35.29145` 同版本恢复独立通过：Native 内部正常重存 tick `116309367`，观察 tick `116309589` / revision `1` / Journal `J102`，四次历史转换保留；窗口更新为 `9/20`、lifetime `633`、whole `234`，余 11 槽含 Save。全厂 `6,660 built / 0 prebuild`、全背包及 R443 九项 kit 保全，外部普通 Save 仍为 71。最终恢复细节见[最终整案恢复记录](final-whole-preparation-recovery.md)。Warper 目标为 30，实际库存 900 是库存而非产出速率证明。来源 5943 的物品过滤改为 1210 后待机；已有持货仍送往 5329；参见[来源分配报告](three-stage-source-allocation.md)和[kit 报告](current-departure-kit.md)。

最终来源 readiness 随后独立通过：239-object cut 的四段 Native 路径、另采 lab256 和三个互相独立的 600-tick 观察共 141 请求、280.851 秒；这些短窗不拼接，也不产生连续生产信用。新输出仓 6657 从 4 增至 5，旧仓 5331 保持 659；燃料棒（物品 1802）全 cut 库存从 1,802 增至 1,804，重氢（物品 1121）从 15 增至 20；氢罐实体 ID 165 中的氢（物品 1120）库存从 9,818 增至 9,819。三个网络比率为 1，实测 `7.27077 ticks/s`。翘曲器保留既定 `≥1/min` 目标；燃料 `≥1/min` 是最终联合长窗事前声明的最低速率，不是 readiness 门槛；三个独立 600-tick readiness 窗口仅允许进入长窗，连续信用为 0。旧 R1300 没有燃料数值最低值，也没有套用 Governor 的 10%。readiness 只作为进入联合长窗的准入证据；最终联合长窗随后按[最终联合供给与封窗报告](final-joint-supply-36060.md)独立通过。

## 原验收门与本候选回归

ROADMAP 明确此前已通过的限定蓝图生命周期与 Governor 两倍观察门不重复施工。Overseer 三类故障/修复、非零钛块产量、healthy Save 与 Journal 连续性的历史证据继续保留其原适用范围；Governor `31→62/min`、连续 `37,128` ticks 及限定升级/保存恢复也不重开。精确 2a 源码 SHA 的完整 Windows Core CI 为 `3,108/3,108`，且报告列出 Contracts/Core/MCP 项目计数，但没有按 Overseer/Foundry/Governor 场景拆分；手动和 Thunderstore 原始布局都已有本地实装、运行与同版本恢复证据，但不表示每个历史 live 场景都在 2a 安装态重演。

旧五对象 blueprint 的完成态与拓扑标记保留，不重放 build；其保存后恢复与完整 34NEW 三级整合链分别有独立证据。Overseer 的真实 carrier 连续 `600` game ticks 静止后恢复分支仍无 live 正例，是明确保留的限制；不把全套 CI、一般恢复或本次联合长窗替代该分支证据。

## 支持范围与未关闭门

候选目标为 Windows x64、DSP `0.10.35.29145`、BepInEx `5.4.17.0`。本轮实际恢复和生产证据限于本地 owned、单人和平、非沙盒、资源倍率 1× 的环境。历史报告证明一次精确的 owned-primary `0.10.35.29131 → 0.10.35.29145` 迁移；它不允许其他版本跳转或任意存档载入，见[迁移记录](confirmed-primary-29145-migration.md)。0.4.0 只准备出发星系一侧，不包括实际跨恒星飞行、曲速调度或跨恒星物流路线。

最终联合连续 `≥36,000` game-tick 窗口按原事前批准 SHA `D811FFEC2E9845842A4EDA072425C8FBA665E9A70B17D6A941EC595770047C96` 完成：79 个 Native-valid 样本（上限 90）覆盖 `36,060` ticks（`116316166→116352225`），开闭观察 `116316041→116352283` / revision `1`；1,822 对原始请求/响应、`5,020.5297` 秒、22 个实际库存 cut，0 accepted、无 gap/reset，inFlight=false、unknown=false。独立 Data 与来源/整案审核均通过（sourceConditionsPassed=true、wholeSupplyPassed=true）。一次覆盖 Save 用 80 请求 / 33.913 秒 / 1 accepted 完成，独立 Save 审核通过；最终保存 tick `116353747`，观察 tick `116353967` / revision `2` / `J102`，全 102 条 Journal 记录和四次历史版本转换保留。最终固定窗口以 opening lifetime `624` 开始，10 个唯一 action/key 均有 ACK/terminal，封于 lifetime `634` / whole `235`；20 槽上限未扩展，未用 10 槽作废。全厂为 `6,660 built / 0 prebuild`，静态及双边连接差异为 0，九项 kit 保全。完整库存/速率/电网边界与原件索引见[最终联合供给与封窗报告](final-joint-supply-36060.md)。

没有验证 Mod Manager/profile 安装、跨电脑使用或安装事务崩溃恢复；不把本地标准载荷安装外推到这些场景，也不承诺非 Windows 或广泛第三方 Mod 兼容。Overseer carrier 连续静止 600 game ticks 后恢复仍无 live 正例；准确源码 CI 列出 Contracts/Core/MCP 项目计数，但不按 Overseer/Foundry/Governor 场景拆分，也不声称在 2a cohort 重演所有历史 live 场景。没有创建 tag、GitHub Release 或 Thunderstore 发布。冻结 ZIP 内的 README/CHANGELOG 保留了构建时“迁移和包实装仍待验证”的文案；这是冻结时点的历史状态，不是当前证据，本报告按后续原件更新外部事实，不改 ZIP 或重打包。候选包与实际安装 IO 的独立字节核验为 ref `281413adc4db44b498c7044638564146:1` / SHA-256 `179B270D4C235BE08E51192FA3EE03DAF7B0B66F975EEE6FB8DC5338AB41C67F`。仓库 README 与 `docs/release-installation.md` 本轮同步最终验收状态；0.4.0 候选现提交所有者审核，正式发布仍须另行批准。
