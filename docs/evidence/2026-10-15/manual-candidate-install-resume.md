# 手动候选安装与同版本恢复：输入检查点

本记录补充[当前快照](../../current-status.md)、[冻结候选报告](delivery-candidates-2a7a98d.md)和[前一输入施工检查点](three-stage-input-checkpoint.md)。冻结手动候选的源码为 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`；该 SHA 是候选来源，本文属于后续事实记录，不改变候选 ZIP，也不把事实文档的仓库提交当作候选来源。

## 安装与菜单

冻结候选完成实际手动事务安装，独立核对的 228 个产品文件、原恢复票据、Journal `J102` 和 602 条受保护 handoff 目录/文件/hash 条目均保全。第一次菜单请求超时的原件仍保留；同一 PID 的后续 fresh 菜单读取确认 Native `0.10.35.29145` 健康。此前三个调用问题也保留原记录（[07d1727部署阶段背景](owned-primary-29145-menu-deployment.md)，不是本次2a安装依据）：预览守卫阻止首次实际安装、PowerShell 子进程缺少 `Get-FileHash` 导致安装前退出、首次菜单读取 `REQUEST_TIMEOUT`；这些失败不被后续读回改写。

## 同版本恢复与输入读回

唯一恢复 action `2fc392d1-f440-4636-b797-eb284be806e3` 完成；四次历史转换完整保留，本次同45恢复未增加转换；Native 正常重存至 tick `116274917`、revision `1`、Journal `J102`。阶段共 80 个请求、39.841 秒、1 次 accepted、0 次外部 Save。完整目录读回覆盖 67 页，6656 项 built、0 项 prebuild；与原静态拓扑比较差异为 0。原有五对象和本段新增的 30 个输入项全部保全。读回计数为 external `6`、lifetime `614`、whole `215`。

此前 completed `5/5` 的蓝图构建仍带有唯一 blocker `construction_topology_changed_or_unproven`。实际连接校验只认可当前保存的五对象蓝图图内边；本段计划新增的 `6623:11 <- 6656:0` 不在该图中，因此仍未被该构建证明。[校验代码](../../../src/Spherewright.Plugin/Game/NormalGameActionCoordinator.BlueprintNative.cs#L203-L225)显示意外连接会使校验失败；原图内连接均保全。该新增边由[事前方案与输入施工独立核验](three-stage-input-checkpoint.md)及 fresh Native 批准施工；这不表示原五对象 build 可重放或再次执行。

这证明完整三级方案的输入部分在一次保存后的中途恢复检查点得到保全；输出仓和输出 sorter 尚未施工。整案共享来源分配、输出链、持续生产窗口及 Thunderstore 候选的实际安装/恢复均未由本检查点证明。输出部分由另一获批的有限阶段处理，本记录不预写其结果。

## 受保护原件索引

| 事实 | 原件引用 | SHA-256 |
| --- | --- | --- |
| 228 文件手动安装、票据/Journal 与 602 条 handoff 独立核验 | `8c2e0db2c29f4041a877b298290a263a:1` | `C9DF32888F160201E81C52A8126A61C2273DD4404E28E3675820D7C36408CDF8` |
| 同一已启动进程的 fresh Native 45 菜单 | `3cf0defa8b5548139d6cfea9da5cb80e:2` | `EF239B9775ED926FC4A4525604882A2AA344D292A5174203A86989E2711FE951` |
| 同版本恢复 writer 结果 | `3aace813db80434f8aa534176331f5a7:162` | `F97ADF0EE27C94EDC1D6EC48356A6E8EAD1DCBDF836543E3BC4D94BF4AE557BB` |
| 独立核验恢复、完整目录与输入对象保全 | `5d592fd474974ef0acb667333a9dab0c:1` | `7FCAC4A44023415327D00A32FEBEFE3178E79E1947B186F3CF743AE1E3C2C95E` |

本记录不包含 token、真实存档名、原始身份或私有绝对路径；索引只指向受保护原件，不替代对原件的授权读取。
