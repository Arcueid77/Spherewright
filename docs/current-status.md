# Spherewright 当前快照

更新：2026-10-11（Asia/Singapore）。执行 cohort 仍为源码 `369f35c0d7ea352d505ec686cc22a52294d5601a`，Native DLL 未变。R961 核销了当前阶段保存与原始记录；没有改变执行代码或运行 cohort。

## 当前保存与窗口

- 最近普通保存为 tick `108735104`、revision `133`、durable Journal `102`。R961 独立核实当前动作均由该保存覆盖，无在途或未知动作；写入冻结。当前 fixed-20 窗口从 lifetime `502` 开始，仍开放，已用 `7/20`，当前 lifetime `509`；没有封窗或转移剩余额度。
- 整案累计 `110` accepted、`32` 次 Move、`30` 次普通保存、`20` 次手工材料动作。R953之前的Stone转移只计一次，已由后续保存覆盖；不重放。R954的一个 `position_stalled` Move仍计入accepted，目标不重试。
- 当前玩家背包：Warp `100`、Ti Ingot `500`、Rod `5`、Stone `0`。Rod `400` 尚未取；最低随身 Warp `300` 尚未满足。R960保存前后完整物料、Stone `6`与Oil `261`仓储、Journal均经独立核验。

## 工厂与来源边界

- 最近完整全厂帧仍是R935的 `6492 built / 0 prebuild`；R961另行核对原始记录、当前动作涉及实体和P104本地电网，并未重新采集完整全厂帧。不要把较早完整帧或局部切面写成R961全厂新快照。
- `sourceConditionsPassed=false`、`wholeSupplyPassed=false`、continuous credit `0`。R783长窗中Warper与Rod的速率下界曾通过，但同窗来源条件仍失败；这不构成整体供给通过。完整29物料趋势、钛矿和钛锭持续来源、至少 `36000` ticks稳定供给及其余双自动补给/整合恢复/最终同SHA双候选门仍未通过或需按原声明取得新证据，门槛不变。

## caller与流程核验

- R941/R942/R944的两处caller误停已查明：原生转移的审计记录由commit-intent和唯一Native响应组成，不能误要求总共只有一条；Stone `6`进入Native顺序库存中间，不能假定它落在列表尾部。两者都是本地断言问题，不是Native拒绝。R947核实原Stone动作唯一闭合，R950/R961普通保存覆盖，不重放。
- R953修正了记录计数/落格读回处理；真实 `pwsh -File` 零Game smoke覆盖7个正例、7个拒绝及wrapper提交边界。R954后续另一Move确为Native `position_stalled`，180 ticks内进展不足 `0.75 m`、离目标仍 `20.2328529 m`；它与前两处本地误停分开处理。
- R932对同一批13份原始回执的完整本地审计由 `32272.0965 ms` 降至 `18112.3251 ms`；R954本地准备约 `1.50 s`、到首prepare约 `1.51 s`。R960覆盖保存耗时 `3887.7234 ms`，R961独立复核核心耗时 `376.687 ms`。这些覆盖范围不同，不能据此宣称整体业务流程或游戏物理等待全面提速；精确读取、不可变原件复用、固定20窗口和单写者约束继续生效。

阶段证据：[R953–R961 caller纠正、有限移动与保存核验](evidence/2026-10-11/recent-flow-and-caller-recovery-r953.md)、[R935固定窗口封存与完整工厂核验](evidence/2026-10-11/saved13-window-close-r935.md)、[R931/R932当前来源与审计效率核验](evidence/2026-10-11/current-source-audit-performance-r931.md)。
