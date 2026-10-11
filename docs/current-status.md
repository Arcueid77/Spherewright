# Spherewright 当前快照

更新：2026-10-11（Asia/Singapore）。执行 cohort 仍为源码 `369f35c0d7ea352d505ec686cc22a52294d5601a`，Native DLL 未变（`6C122E5443E6843979B4064050DFCB5E0D75577A0B64F6AE4111290238B33C12`）。本阶段仅更新事实文档。

## 当前保存与窗口

- 最近普通保存为 tick `108894684`、revision `137`、durable Journal `102`。R977 独立核实当前动作均由该保存覆盖，无在途或未知动作；写入冻结。固定20窗口从 lifetime `502` 开始，仍开放，已用 `10/20`，当前 lifetime `512`。
- 整案累计 `113` accepted、`33` 次 Move、`31` 次普通保存、`21` 次手工材料动作。R971 子阶段未使用的5笔额度已退役，不转移；成功前缀不重做，涉水目标不重试。
- 当前玩家背包：Warp `300`、Ti Ingot `500`、Rod `5`、Stone `0`，占用 `29` 格。Rod `400` 尚未取；Stone `6` 与Oil `261`保管均经R977核验。

## 最新有限阶段与来源边界

- R971核销的44条记录含18个Native动作；从仓5331转入Warp `200`并成功完成约5米逃离Move，共2笔accepted。随后约18米Move虽有正向Native prepare，但4个岸线样本显示风险，终点地面低于水面 `1.2934 m`，caller在commit前拒绝；该第三Move未提交。R976正常保存覆盖已成功前缀，R977完成独立核验。
- R977复用R935封存的完整工厂帧 `6492 built / 0 prebuild`，核验9个受影响对象、原配置、保管物料、P104本地供电及完整Journal；这不是新的全厂扫描。`sourceConditionsPassed=false`、`wholeSupplyPassed=false`、continuous credit `0`。Rod `400`携带需求、持续来源、完整29物料稳定性、至少 `36000` ticks供给及其余既定最终验收仍未通过，门槛不变。

## 流程与下一步边界

- R970阶段终止至R976保存终止的跨度为 `16.1908 min`，属于阶段间隔，不能与caller运行时长相减来精确归因或当作前后同口径性能基准。R971三次只读刷新、R976保存与R977独立审计耗时分别为 `786.7148 ms`、`3531.2985 ms`、`467.9608 ms`；既有固定窗口、复用不可变快照与有界读取继续使用，不证明端到端提速。
- 下一业务caller的零Game smoke及5类拒绝检查已通过，但其三次只读刷新尚无实机正例。下阶段仍须fresh核验当前session/player/Journal与移动目标，不能把caller smoke写成运行时通过。

阶段证据：[Warp补给、受限移动与保存核验](evidence/2026-10-11/warp-escape-save-r977.md)、[R935完整工厂基线](evidence/2026-10-11/saved13-window-close-r935.md)。
