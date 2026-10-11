# Spherewright 当前快照

更新：2026-10-11（Asia/Singapore）。执行 cohort 仍为源码 `369f35c0d7ea352d505ec686cc22a52294d5601a`，Native DLL SHA-256 `6C122E5443E6843979B4064050DFCB5E0D75577A0B64F6AE4111290238B33C12`；R1075 只更新事实记录，未改变运行二进制。

## 当前保存与窗口

- 最近普通保存：tick `109934423`、revision `168`、durable Journal `102`。R1072 本阶段全部动作已被保存覆盖，独立核验确认无在途或未知动作。
- 固定 20 窗口以 lifetime `519` 开始，当前 `16/20`、lifetime `535`；窗口仍开放但冻结，R1072 目的已结束。整案累计 `136` accepted、`43` 次 Move、`40` 次普通保存、`25` 次手工材料动作。
- R1072 没有 Move：仓 `3348` 存有转入的 `400` 根 Rod，背包持有 `5` 根，合计 `405` 根；另从 `5331` 取出 `600` 个 Warp，背包由 `300` 增至 `900`。既有氢过滤出口 `3349` 保持原设置；Warp 仓的即时读数 `2400→2401` 与本次转账分开，作为自然补货观察记录。背包占用由 `40/40` 降至 `33/40`。

## 当前来源观察

- R1075 在 `109950103–109950702` 的单个 `600` tick 窗口中观察到：Warp `1210` 产出 `1` 件，即 `6/min`；Rod `1802` 为 `0/min`；Ti Ore `1004` 与 Ti Ingot `1106` 也均为 `0/min`。Rod 零产出尚无确认发现或唯一根因；Ti 两条观察对应的生产端满载。三座已加载工厂电网在该窗口均满供。
- 本次复用了 R1013 封存的完整工厂帧（`6492 built / 0 prebuild`），没有重新扫描全厂；保管信息最近由 R1068 核验，本次未刷新完整保管切面。该短窗不构成持续来源证据。
- 当前准备库存为 Warp `900`、Ti `500`、Rod `5`；已转存 Rod 共 `405` 件，石料保管记录仍按最近核验值 `6` 记账。完整配方、库存趋势与已知来源门槛见阶段证据。

## 验收边界与下一步

`sourceConditionsPassed=false`、`wholeSupplyPassed=false`、continuous credit `0`。Warp 和 Rod 的短期来源、钛源持续供给、完整供需长窗、双自动补给、整合保存恢复及最终同 SHA 双候选门仍未通过。R1075 的单窗 Warp 读数不能替代这些验收。

下一步定向 fresh 核验 Rod 的氘源、路线和消费者，并通过有界操作打开仍满载的钛消费者。若所需动作超过当前窗口剩余 `4` 个槽位，先按规则封闭当前窗口，再另开固定 20 窗；不重置或扩展当前窗口。

R1076 的流程复盘显示，既有单写者、合批保存、固定窗口、完整帧复用和事实 CI 并行仍在使用，但阶段准备与交接仍有明显开销；不同动作的阶段间隔不证明整体变慢或提速，详见[R1075流程补充](evidence/2026-10-11/owned-stock-warp-demand-save-r1075.md)。

阶段证据：[Rod/Warp 转存与来源短窗](evidence/2026-10-11/owned-stock-warp-demand-save-r1075.md)、[Rod转存与来源短窗](evidence/2026-10-11/upper-bank-rod400-save-source-r1065.md)、[东北两段移动与保存](evidence/2026-10-11/northern-two-move-save-r1055.md)、[东北路线前缀与保存](evidence/2026-10-11/northeast-prefix-save-r1046.md)、[南向转弯停滞与保存](evidence/2026-10-11/south-turn-stall-save-r1038.md)、[完整来源长窗](evidence/2026-10-10/current-full-source-long-r783.md)。
