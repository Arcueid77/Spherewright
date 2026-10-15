# Spherewright 0.4.0 — 发布说明草稿

**未发布草稿。** 这份后续说明按已审核的候选和现场证据整理；正式发布资格仍未关闭。源码候选为 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`，对应的两份冻结包及审查范围见[候选交付审查](review-delivery.md)。

## 本版本内容

- 增加有界的 Foundry 物料、机器和功率方案，以及生产、能源、物流和研究状态读取；诊断和方案建议本身不会生成写入授权。
- 增加有限蓝图施工、对象进度读取和安全续建，按正常材料消耗、建造无人机和逐对象原生检查执行。
- 增加 Governor 持续生产声明与观测入口；不会启动无界扩产循环。
- 扩展 MCP 工具集，并随包提供与 MCP 同步的开局 playbook。
- 增加受约束的 owned-primary 版本变更恢复披露。证据支持一次精确的 `0.10.35.29131 → 0.10.35.29145` 恢复；每次仍要求匹配的票据、完整 Journal、明确披露和后续确认。它不支持任意跨版本载入或进度回写。

## 验证配置与证据边界

候选面向 Windows x64、Dyson Sphere Program `0.10.35.29145` 与 BepInEx `5.4.17.0`。已验证的实际 owned 世界为单人和平、非沙盒、资源倍率 1×。同一源码 SHA 的 Windows Core CI 完成 3,108 项测试，0 失败、0 跳过；两份候选均通过各自的打包检查、完整 MCP surface 与 playbook 检查。

此前通过的 Overseer 受控诊断/修复、Governor `31→62/min` 连续 `37,128` ticks，以及限定蓝图生命周期继续按原范围成立，不为本次候选重复施工。准确 2a 源码有完整 3,108 项 CI；两种实际本地布局也完成实装、运行和同版本恢复。CI 报告列出 Contracts/Core/MCP 项目计数，但没有按 Overseer/Foundry/Governor 场景拆分；这些证据也不声称在每种布局上重新演练所有旧 live 场景。Overseer 的真实 carrier 连续 `600` game ticks 静止后恢复仍是已知 live 覆盖限制。

冻结手动包和 Thunderstore 标准共置载荷均在本地完成适用实装、运行及同版本恢复验证。Thunderstore 证据限于把原始共置文件安装到同一本地 DSP 根目录，并运行其单文件 MCP；没有 Thunderstore Mod Manager/r2modman profile 安装、跨电脑验证或安装事务崩溃恢复证据。非 Windows、广泛第三方 Mod 兼容，以及实际跨恒星飞行/曲速调度/跨恒星物流路线不在本版本范围内。较早的五对象重启续建、输入保存后的中途恢复，以及其后完成的 34 个新增施工对象和 kit，均按各自的窄范围保留原证据，不替代持续生产验收。完整 kit 后来在 Native `0.10.35.29145` 上完成一次独立同版本恢复：Native 内部正常重存 tick `116309367`，观察 tick `116309589` / revision `1` / Journal `J102`，四次历史转换保留；固定窗口为 `9/20`、lifetime `633`、whole `234`，余 11 槽含 Save。详见[最终整案恢复记录](final-whole-preparation-recovery.md)。

最终来源 readiness 独立通过：239-object cut 四段 Native 路径、另采 lab256 和三个独立 600-tick 观察共 141 请求 / 280.851 秒；短观察不拼接，也不计连续产出信用。新仓 6657 从 4 增至 5、旧仓 5331 保持 659；燃料棒（物品 1802）全 cut 库存为 1,802→1,804，重氢（物品 1121）为 15→20；氢罐实体 ID 165 中的氢（物品 1120）库存为 9,818→9,819。三个网络比率为 1，观察到 7.27077 ticks/s。翘曲器门槛维持既定 `≥1/min`；燃料 `≥1/min` 是最终联合长窗事前声明的最低速率，不是 readiness 门槛；三个独立 600-tick readiness 窗口仅允许进入长窗、连续信用为 0。R1300 没有燃料数值最低值，也不套用 Governor 的 10%。

## 最终验收状态与未发布边界

最终 kit 的同版本恢复、来源 readiness 与联合连续供给均已按各自范围通过。联合窗口按原批准 SHA `D811FFEC2E9845842A4EDA072425C8FBA665E9A70B17D6A941EC595770047C96` 取得 79 个 Native-valid 样本（上限 90）、覆盖 `36,060` ticks（`116316166→116352225`）；1,822 对原始请求/响应、22 个库存 cut、`5,020.5297` 秒，0 accepted、无 gap/reset。独立 Data 与来源/整案审核均确认 sourceConditionsPassed=true、wholeSupplyPassed=true。一次覆盖 Save 为 80 请求 / 33.913 秒 / 1 accepted，保存于 tick `116353747`；观察 `116353967` / revision `2` / `J102` 保留 102 条 Journal 记录和四次历史版本转换。最终 fixed-20 窗口以 opening lifetime `624` 开始，10 个唯一动作均有 ACK/terminal，封闭于 lifetime `634` / whole `235`；20 槽上限未扩展，未用 10 槽作废。细节与五项原件索引见[最终联合供给与封窗报告](final-joint-supply-36060.md)。

0.4.0 仍是未发布候选，现提交所有者审核；没有创建正式 tag、GitHub Release 或 Thunderstore 发布。本次证明限于本地 owned、单人和平、非沙盒、资源倍率 1× 的 Native 29145 cohort。尚未验证 Mod Manager/profile、跨电脑使用、安装事务崩溃恢复及 Overseer carrier 连续静止 600 game ticks 后恢复分支；CI给出Contracts/Core/MCP项目计数，没有按Overseer/Foundry/Governor场景拆分，也不声称在两种布局重演所有旧 live 场景。冻结 ZIP 内 README/CHANGELOG 中待迁移、待实装的句子是构建时的历史文案；后续外部原件已补足本地迁移、实装、恢复和联合供给事实，ZIP 字节保持不变。本轮仓库 README 与 `docs/release-installation.md` 同步了最终验收状态。详见[候选交付审查](review-delivery.md)及[最终联合供给与封窗报告](final-joint-supply-36060.md)。