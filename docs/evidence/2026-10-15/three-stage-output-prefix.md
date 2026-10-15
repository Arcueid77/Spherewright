# 三级输出段已保存前缀

本记录接续[当前快照](../../current-status.md)、[输入检查点](three-stage-input-checkpoint.md)与[手动候选恢复检查点](manual-candidate-install-resume.md)，只记录输出段中途保存前缀，不代表输出 sorter、完整三级链、持续来源验收或 Thunderstore 候选实装验收完成。

95 取出 4 个成品 `1108`、R86 用 4 铁和 4 砖制作 1 个小仓、Native 无人机正常建成实体 `6657`，三个原 action 均成功；背包净扣 4 铁，没有重取旧 `1005`。此后本段只执行 1 次正常 Save；包含保存及读回的阶段共 80 个请求、31.983 秒，Save 至 `116282657/rev7/J102`。四次历史转换完整保留，完整目录读回计数为 `6657 built / 0 prebuild`，与原 `6656` 静态差异为 0。计数为 external `10`、lifetime `618`、whole `219`，累计 47 Moves / 68 external Saves。

把现有 `6625` 直接接到仓 `6657` 的 sorter 施工收到唯一 Native `BUILD_CONNECTION_INVALID`：48 个候选中 `nativeChecks=1`，最后 Native 拒绝为 `TooFar`，最后前置拒绝为 `TooSkew`；sorter commit 为 0，已建小仓保留。9 个实体及 3 段地形几何的实测支持重规划一个较短的输出接缝，但不构成 sorter 放置正例。此前两项本地参数/guard 错误均为 0 accepted：目录读取漏传 `planetId=104`，以及新 Native 正例的空目的端被错误要求为 `0`；它们不是额外 Native 施工失败。

重规划保留小仓，最多再增加 2 条基础传送带（物品 `2001`）；完整整案新增上限为 31 条基础带、2 个基础 sorter、1 个小仓，共 34 个对象。原四台机器、配方、12.9 MW 目标及外供方案不变。当前 V2 接缝 caller 获批最多 3 accepted，仍在执行且尚无结果；本记录不预写施工成功，也不声称输出或整案完成。

## 受保护原件索引

| 事实 | 原件引用 | SHA-256 |
| --- | --- | --- |
| 原动作终态、Native sorter 拒绝与重规划边界独立核验 | `33bc4e6e08424315bc21245f411e24aa:1` | `51A7DF872B65F8BA942C4B181A75E3AC4B83331423F99C67C53B6536A31215FE` |
| 小仓前缀、正常保存和几何读取 writer | `0fb83268136545a8b537b9b65da6c14f:162` | `4912A29360D95AAA2D9AFB927B19719AE323939F179CC91F2782A476A02DDD27` |
| root 独立核验已保存 `6657` 与端口几何 | `3fa3d5e7988e4cf6a1d65e4c67afe6f3:1` | `856FCA479DDB742AD2E5EBDF463C8EE7EEF0D0E687057ADEC5F468AC25C9EE7F` |

索引只指向受保护原件；本报告不包含真实存档名、身份或私有绝对路径。
