# 三段产线输入段施工检查点

本记录只确认已完成的输入段计划、施工与保存检查点，不代表三段产线、恢复后持续运行窗口或 0.4 候选包验收完成。主档迁移事实见[已确认迁移记录](confirmed-primary-29145-migration.md)。

## 施工前计划与负载边界

完整前瞻三级整合计划在施工前已写入受保护记录。目标是让 1210 达到至少 `1/min`，沿用 5334 / R60 的 diamond `4/min`（360 kW）、5326 / R104 的 strange `1/min`（12 MW）、5333 / R101 的 lens `1/min`（270 kW）和 6623 / R78 的 warper `1/min`（270 kW），合计 12.9 MW。

完整整合方案的新增上限是 29 条基础带、2 个基础 sorter 和 1 个小仓；本次输入段实际新增 29 条带和 1 个 sorter，没有新增小仓，也没有重建现有 4 台机器。完整方案的输入和输出各用 1 个 sorter。N3 现有基础负载计入 station 最大充电为 `1,755,800 J/tick`；两台新 sorter 合计增加 `600 J/tick` 后，对 `1,950,000 J/tick` 容量仍余 `193,600 J/tick`（11.616 MW）。这是容量核算，不是燃料供应或连续运行证据。

原方案包含两次 Move。只有首个 Move 发起 Native prepare；Native 返回 `prepared=true`、`commitAllowed=true`，但 `surfacePreview` 含 2 个浅水风险 sample，随后被本地校验挡住。第二个 Move 未请求，Move commit 数为 0。随后改用原地方案：最大距离 `54.806 m`，低于 `78 m` 门槛；旧 approach 和对应 token 已退役，没有重放旧 Move。

## 输入段施工与独立读回

29 条基础带由 action `8abcd686-ec55-4c14-8fff-36a8d12e44fe` 成功完成。核销覆盖 29 个实际实体 ID、逐点布局、原 source、既有连接，以及材料从 51 到 22 的扣减。之后的 sorter 原生正例曾被本地旧 guard `sourceSlot >= 0` 错误拦截；实际 `belt_virtual` 的 `sourceSlot` 为 `-1`，该阶段没有 commit。局部 caller 修正后，4 项覆盖 Native 正负例的文件 fixture 均通过，`0 Game`。续施工只完成 sorter 和 1 次普通 Save，没有重建这 29 条带。

最终新增 sorter 为 6656：其输入 tail 6655 接到既有 6623 的 port 11，filter 为 1209。原 6622 和 6624 保留备用；本记录不声称原有 5 个对象入口已获供料。

root 独立核验记录报告 460 项原件对照、`6656 built / 0 prebuild`，且原有 6626 个对象全部保全。只有两条精确新增连接：`5942 slot 0 → 新 head 6648 slot 1`，以及 `6623 slot 11 ← sorter 6656 slot 0`。5942 的 rotation 与原 Native 成功后读回完全相同，符合 `whole_path_native_rotation_v1` 和 Native completion 校验；没有未解释的静态差异。

最终普通 Save 为 tick `116274886` / revision `6` / Journal `102`，随后观察 tick `116275170`。窗口记录为 R1332、max 20、opening 608；external 5、lifetime 613、whole 214、Moves 47、外部 ordinary Saves 67，剩余 15 个槽（含保存）。writer 已停止，无 inflight 或 unknown。此检查点不计入新的持续运行窗口。

## 未完成项

输出仓和输出 sorter 尚未施工；完整方案的共享来源分配、中途恢复覆盖和最终长窗口也未验证。后续阶段将在必要的手动候选部署后继续；本记录不把输入段检查点写成完整三级链或验收通过。

## 原件索引

| 事实 | 受保护记录引用 | SHA-256 |
| --- | --- | --- |
| 施工前锁定的完整三级整合计划 | `efb3326dfef541d284c358310df3a0f3:1` | `27122A3C3B3B0730A74E7CB05C123DA6328FFADC236C12055C824C237E63B040` |
| 输入段首段 writer 原始结果 | `fb098b0bf96d4a5caf093eba1ca7970d:658` | `08F4CA4E9A7D5947A9ADB0E80C57F664E7F882D419663A292F74E3684985E449` |
| sorter 续施工与 Save writer 原始结果 | `7558e9572cec48b19e1e2f906fa27f93:267` | `A5BC7E880DE2D7E8DFC9C16AAC1CF1D1D4C5260863A3090B8DFD1AAEFA7F7724` |
| root 对输入段完整度、连接与 rotation 的独立核验 | `8b7144d139c34181ab0faabd388c2dd8:1` | `7D4968FE98786F0073F32D779E9A0B6BD52B004A1294AD2621A4401F7E65E059` |
