# 最终整案 kit 的同版本恢复记录

本报告记录已完成的最终整案恢复：对已建成并保存的 34 个新增对象及完整出发 kit，在 Native `0.10.35.29145` 上恢复同一 owned 主档。它与此前“输入已保存、输出尚未建成”的手动候选中途恢复检查点不同；后者仍见[手动候选恢复记录](manual-candidate-install-resume.md)，两种阶段不得合并叙述。

## 本次恢复结果

整案恢复前只发生一次正常 Close 和一次 Steam 派发；旧 PID `50884` 已退出，随后唯一运行 PID 为 `40884`。更早在 `50884` 上的两次 fresh default prepare 返回 `BRIDGE_NOT_READY`，均为 0 commit/0 accepted，票据和 Journal 未变；这不表示迁移失败、票据失效或已消费。随后在 `40884` 的启动阶段，Native positive-readiness 观察仅执行 2 次读取、0 commit，plan 已废弃且未发生 Load；它只记录启动readiness，不是恢复动作。之后仅执行一次 fresh default、同版本 `0.10.35.29145` resume，accepted action 成功终结；这不是新的跨版本迁移，也没有重做或换键重放施工动作。writer 共 80 个请求、38.577 秒、1 个 accepted。Native 在恢复流程内正常重存到 tick `116309367`；这是 Native 内部重存，外部普通 Save 总数仍为 71。后续观察为 tick `116309589`、revision `1`、Journal `J102`，四次历史版本转换完整保留。

root 独立核对 full factory 为 `6660 built / 0 prebuild`，静态配置及双边连接差异均为 0，全背包和九项 kit 清单完整保全。原五对象蓝图仍为 `5/5`；保留的两条计划图外边是 `6623:11 <- 6656:0` 与 `6625:0 -> 6658:1`，有明确的事前计划和 Native 几何解释。不清除旧标记、不重建前缀，也不据此宣称旧 build 可重放。恢复成功证明的是这个已保存整案在同一 Native 版本和 owned 时间线上被恢复，不证明来源速率或持续生产。

恢复后 fixed-20 窗口为 opening lifetime `624`、`9/20`、lifetime `633`、whole `234`，剩余 11 槽含 Save；无在途或 unknown action。

## 尚未通过的验收门

完整 kit 恢复已通过，但自动来源、速率、库存、功率和运输 readiness 尚未完成。下一有限阶段获批读取 239-object cut（当前上限 256）及独立的 lab 256 采样，并计划三个相隔 606 tick 的独立 600-tick 观察；这些窗口不拼接，也不计为连续生产信用。该阶段批准预算为 720 秒、400 请求、0 个 accepted 写入；阶段结果尚未回报。caller SHA-256 `8857528E4A12DDEFE5FF7D4D670C88E005AC0410207623C9138044381F767249`；批准原件 `30ccc4e3ed404486b91c91f194a0d337:1`，SHA-256 `7A5039C19FA1578C2FF15A14FFB37656424F575A7F852171E5BCF69CD54309E6`。目前最终联合连续 `≥36,000` game-tick 窗口尚未开始，不能把库存、施工或三个短观察窗当成该门的通过。

## 原件索引

| 事实 | 受保护引用 | SHA-256 |
| --- | --- | --- |
| 最终 same-45 resume writer，80 请求 / 1 accepted | `ae64950193824e1293955a544fb84be2:162` | `9BBE9C73C2EF83E2D65D7D2125C7FFB91652BB8BE0C62942B73D7A8CF7D96814` |
| 独立核验恢复、全厂读回及 kit 保全 | `88d30e970b314c6d81d091b3c98c2a0e:1` | `CD6DC40647F35A3BC453B3FF152A94357D2D45CBED9DC8B3C784B7249E70FE91` |
| 先前 `BRIDGE_NOT_READY` 第二次响应；未 commit | `1a0095295af0479bb07b2d89940fb778:5` | `F6996E34DF11CA7D69BEA783A3C1F2CF43885207C19DD1C7E0201C796963A68C` |
| 恢复前启动阶段 positive-readiness 观察；2 reads、0 commit，plan 已废弃 | `f8f2c453133541f487389f05878969aa:1` | `911C2E1B99E073A31CED4BD62BF2F1917D9B8D665B98BACF6C9FBD2B71EFC760` |

恢复结果与此前的中途输入检查点分别保留；本记录没有 Load/Save 凭据、真实存档名、原始身份或私有绝对路径。
