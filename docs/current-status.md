# Spherewright 当前快照

更新：2026-10-14（Asia/Singapore）。

## 源码与运行时

当前源码提交为 0b67f85a1b6c1bd82e39f19c277bc16f10937631，Windows Core CI 38502520017 成功；该提交只加强手动发行包的 64 工具自检，不代表候选包已构建或验收。当前游戏为 0.10.35.29131，已安装 Native cohort 仍为 6528e1f7101a714826f7364cef2b0ad75c4b6e82。最终同 SHA 手动包与 Thunderstore 包均未完成实机验收。

## 当前拥有档与已保存边界

R1320 普通 Save 覆盖了五对象模块，保存于 tick `116215326`、revision `7`、durable Journal `102`；三条版本转换记录保持一致。R1321 已封闭 opening lifetime `588` 的固定20窗口，20个唯一动作终态均已核销、lifetime为`608`；整案累计`209 accepted / 47 Moves / 66 ordinary Saves`，无在途或unknown。R1318 的唯一 Load 恢复同一 owned 主档并自动重存于 tick `116205174`、revision `1`、Journal `102`；这不是额外普通 Save。

## R1307–R1321 Foundry进度恢复与有限模块续建

旧版本 `0.10.34.28529` 的进度列出12项 build，覆盖的40个对象均已完成且0在途；其进度哈希与当前状态不兼容。R1307 的 `get_blueprint_builds` 返回 `blueprint_progress_store_unavailable` 后停止，未产生 accepted。经用户授权，R1308 保存于 tick `116183243`；正常退出后按原字节与 SHA 归档旧文件，没有编辑或重放旧计划。R1310 只启动一次桌面 broker；首次菜单读取超时，root :57 仅核实启动及关闭/归档前缀，菜单仍未证明。R1312 root :59 随后通过实际菜单与 Job 只读审计。

R1311 的唯一 Load 恢复同一 owned 主档，并在 tick `116183275` 自动重存，revision `1`、Journal `102`；这不是额外的一次普通 Save。三条版本转换记录保留。R1313 已独立通过恢复后完整工厂审计：67页、6621 built、0 prebuild、50项详情；相对R1296封存基线的静态配置/连接差异与资源矿点差异均为0，玩家库存、持物与落地位置、Journal及3条版本记录保全。采集123请求44.69秒，root核原件未重复实机采样。

R1314 有一项 Blueprint 被 accepted 并以 `paused` 成功终态完成前两个对象：新增普通传送带6622（物品2001）与装配机6623（物品2303、配方78），仅各扣料1件；剩余3项未提交且未扣料。调用器随后因一架无人机仍为 working（pending为0）但要求全部 idle 而停止；这是调用器条件过严，不是建造失败或 unknown。83请求、158.2秒。更早的坐标与动作哈希读取偏差也均在提交前停止、0 accepted，原失败保留；比较依据已改为实际 Native DTO 与已安装 Core 函数。

R1316 新增1笔普通 Save accepted 并覆盖上述两对象前缀；root 对10条原始记录、库存与完整 Journal 独立核验通过，确认三架无人机 idle、库存与持物保持、三条版本记录一致。R1317 独立核验完整 `6623 built / 0 prebuild` 基线。R1315 只执行一次正常退出与同 cohort 启动，保留部分进度且未归档；R1318 随后的唯一 Load 恢复同一 owned 主档并自动重存，R1319 复核恢复后 `6623 built / 0 prebuild`，原进度仍在。

R1320 完成剩余三个 Blueprint 对象（索引1/3/4，对应实体6624/6626/6625；原索引0/2实体6622/6623保留），并普通 Save 覆盖全部五对象。R1321 完整工厂审计为67页、6626 built、0 prebuild、55项详情；相对6621基线，除这5个预期新增对象外，静态配置、拓扑和资源节点差异为0；玩家库存、Journal、三条版本记录及三处电网满供保持。root 对8条阶段原件链的20个唯一动作终态完成独立核销；票据与保存绑定、历史票据tombstone及旧档归档SHA均核对一致。当前固定20窗口已封闭，lifetime `608`，整案 `209 accepted / 47 Moves / 66 ordinary Saves`，无在途或unknown。

## 尚未通过的 0.4 门

R1300 的 36,348 tick 来源窗口发生在本次恢复之前，只证明重启前来源就绪。已完成的五对象模块不等于完整三级 Foundry：完整计划及设备、电力、物流和精确成本仍缺；模块外部`1209`供料与`1210`持久输出、恢复后的双自动供给/来源、出发准备清单及最终同 SHA 双候选包均未通过。旧27对象候选保持退役。此前已通过的限定蓝图生命周期及 Governor `31→62/min`、37,128连续tick门保持关闭，不因本次恢复重验；见[历史快照](../ROADMAP-HISTORY-2026-09-24.md)与[时间线](gameplay-timeline.md)。详见[R1307–R1321恢复事实与原件索引](evidence/2026-10-14/foundry-progress-recovery-r1307.md)及[铁源与Foundry准备阶段证据](evidence/2026-10-13/iron-source-connected-r1257.md)。

0.4 整体验收仍未完成。
