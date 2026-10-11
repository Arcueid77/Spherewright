# R1030–R1038：南向转弯停滞与保存

本阶段唯一 Move 已 accepted，Native 终态为 `position_stalled`：180 ticks 后距目标约 `3.3696 m`。原 action 已核销并保留，不重放；具体碰撞原因没有证据。随后普通保存覆盖该 Move 与本目的状态。

| 证据 | 受保护索引 | SHA-256 |
|---|---|---|
| 移动阶段原件 | `cc7d833fbfb14e6e92a33a8df084aa69:30` | `E072DD3725535C406C49AFD879E0765FB0F2C00B5747E83661594280F5A14789` |
| 原 terminal 独立核验 | `0299d7dfe49841f0b44eefb14c697743:180` | `F15194448C72530146907D3E742BABD65EE7971A1AF05DCDF89B5A8D0E5BAE1D` |
| 四读调用者 | `6f7a9dc5bba54a0e94c3cd76de613672:9` | `4445B7181A3968AB8B52CDB62D0F04F7847469EE4AB190AB35949A5E5C62A8C8` |
| 健康读回独立核验 | `0299d7dfe49841f0b44eefb14c697743:182` | `9055F3F756EF6A535086CAD80F3932D3C75C4D3B57A510C451141D7EA7FD3D16` |
| 普通保存原件 | `07666fc4f9444cbb93041f20207c04f9:21` | `923902DE7D057BFA63F8BD24FA6261896A65EAC57DC6FD6F2AF06B88CEBF6B0B` |
| 保存与窗口独立核验 | `0299d7dfe49841f0b44eefb14c697743:185` | `2233DBBFF889A3A7F89B944252D856FD6130825186A35FC5FF9CE74661ACB604` |
| R1013完整静态帧 | `0299d7dfe49841f0b44eefb14c697743:165` | `FE71CB8DFE7A147317565C0724F2A7D9323BD382B44CF810D6FF0CE616A25822` |

保存为 tick `109570338`、revision `153`、durable Journal `102`。固定 20 窗口以 lifetime `519` 开始，现为 `5/20`、lifetime `524`，仍开放但冻结；本目的已独立结束，余下 3 个本目的预算槽位退役。整案累计 `125` accepted、`39` 次 Move、`36` 次普通保存、`22` 次手工材料动作。全部当前动作已保存覆盖，无在途或未知动作。玩家持有 Warp `300`、Ti `500`、Rod `5`，占用 `27/40` 格，核心能量 `1.6 GJ`；Rod `400` 尚未取用。

原件核验复用了 R1013 的 `6492 built / 0 prebuild`、65 页静态帧，本阶段没有重采完整工厂。P104 本地电网在本阶段 fresh 满供；另外两座工厂的电网证据只在 R1013 fresh。保管记录为 Oil `261`、原石 `6`、石料仓 `2932`。这些截面不证明持续供料。

同一只读边界调用者现从 approval 的 opening lifetime 读取声明，并要求与真实固定窗口 ledger 完全一致；它保留 4 个精确只读方法、60 秒上限、原 action/key/intent 核验门。零 Game smoke 的 SHA-256 为 `B79316D309812B403E156AA7BF3012C81E9E3EE044C7F08423DA9480FFF1B2F6`：3 个声明窗口正例通过、7 个变更窗口拒绝、8 个越界方法拒绝，最多 4 请求且 `acceptedDelta=0`。源码执行 cohort 和 Native DLL 未变化。

本次 Move 使用 `13` 请求、约 `13.300 s`，首次 prepare 约 `1.911 s`；四次只读约 `1.267 s`，保存使用 `15` 请求、约 `3.519 s`。root 对原终态、四读和保存的独立审计分别约 `0.580 s`、`0.256 s` 和 `0.355 s`。停滞 Move 到覆盖保存约 `11.471 min`；这些局部计时不代表端到端提速。

`sourceConditionsPassed=false`、`wholeSupplyPassed=false`、continuous credit `0`。Rod `400`、Warp/Rod与钛源持续供给、完整供需长窗、双补给、整合保存恢复和最终同 SHA 双候选门仍未通过。
