# R887–R893：移动停滞、覆盖保存与窗口封存

R887 的普通移动被接受，但 Native 终态为 `position_stalled`，玩家未到达目标；该动作之后只按原 action 唯一核销，没有重放。writer 原件 `932614c74c354b4a8df08810706102e1:16`（SHA-256 `AA357B2E7D0EED2FE490BA35C5FC2C7FE1620716FF6BEFE646F78EFC52B9AEEB`）。R888 的四份只读回执 `86dcbacecfcb4fbd97dbcb883ea8c61a:5`（SHA-256 `EB311E5C735F4FAA52F320583ABEA47B0888FC8FAC2D628ED9E590FA7FDAF581`）与 R889 唯一核销 `0299d7dfe49841f0b44eefb14c697743:69`（SHA-256 `C689A82D637ED8FF9EFCE437CD19E3D9C519C2E5E949C448CC244A9DC1670855`）闭合原动作；没有将停滞改判为成功。

R891 普通保存与完整工厂读取 writer `b7a5a99173c24c0ea2391510395b9ccd:86`（SHA-256 `E0589CA5AEAA9FF50B1152C88045AEBB446B29F63B37F75BD7E2A5D6BDD490A7`）记录 80 请求、`29167.1012 ms`、revision `110`、保存 tick `107773180`、durable Journal `102`，完整读取 6492 个实体、65 页、0 预建筑。R892 保存核验 `0299d7dfe49841f0b44eefb14c697743:72`（SHA-256 `F1FA913ABD8CFE497BCE601F545B7DAB5907E4A6C81456B82899658DC4BCCAAD`）确认覆盖。R893 窗口审计 `0299d7dfe49841f0b44eefb14c697743:73`（SHA-256 `F9DE7E0BAD5B5F1E7892CF2E8F96F8B325B62E65F4FC43DA2357E44B005A594F`）核验 19 笔唯一动作及其保存覆盖：13 次 Move（12 次到达、1 次停滞）和 6 次普通保存。窗口从 lifetime `470` 开始，以 `19/20`、lifetime `489` 封闭；未用的 1 个槽退役，不补额、不转移。整案累计 90 笔 accepted、30 次 Move、24 次普通保存、10 次手工材料动作。当前无在途或未知动作，新窗口尚未打开。

R893 读得玩家到 P104 钛仓259、531的距离分别为 `77.6397 m`、`75.1654 m`，均在原生建造范围 `80 m` 内。这仅提供无移动候选的静态依据；下一阶段仍需 fresh 检查玩家、仓储和实体身份，并通过 Native prepare 后才能决定是否取料。本阶段没有取钛或启动短采样，`sourceConditionsPassed=false`、`wholeSupplyPassed=false`，continuous credit 为 `0`。

### 流程耗时对照

较窄覆盖的 R865 与 R884 保存阶段各为 15 请求，writer 时间分别为 `3595.2224 ms` 和 `3646.4052 ms`；两者不能与完整工厂扫描直接比较。R858 与 R891 都是 80 请求的完整工厂保存场景，writer 时间分别为 `32550.8408 ms` 与 `29167.1012 ms`。这只显示单次完整 writer 时段少约 `3.38 s`，不包含路线准备或独立审核耗时，不能证明端到端提速。固定窗口、单写者、有界批处理、静态快照复用与事实 CI 并行仍在使用；路线和审核准备仍是流程耗时重点。
