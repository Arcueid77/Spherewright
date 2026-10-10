# R845：有限西侧移动与保存

更新：2026-10-11（Asia/Singapore）。本页记录一次有限正常移动及保存，不改变来源验收门，也不把接近位置写成路线或施工通过。

## 当前阶段结果

R845 完成约 23 m 的西侧正常 Move 并到达，随后完成普通 Save。该固定 20 写窗口从 lifetime `470` 开始，本阶段新增 2 笔 accepted（Move、Save），当前为 `2/20`、lifetime `472`；两笔均由本次 Save 覆盖。目的独立结束，阶段冻结且无在途或未知动作；窗口仍开着，计数未重置。此前从 lifetime `454` 开始的窗口已在 `16/20` 封闭，4 个未用槽退役。

当前 Save tick `107325956`、revision `82`、durable Journal `102`。累计计数为 `73` 笔 accepted、`18` 次 Move、`19` 次普通 Save。原始阶段共 88 请求、writer 墙钟 `32893.6567 ms`。批准至首个 prepare 为 `37.1974988 s`；root 审计核心耗时 `14426.7734 ms`，不含原件预加载。不同阶段计时口径不应混用为整体提速比例。

## 状态与边界

完整工厂为 `6492 built / 0 prebuild`，65 页；审计记录无静态变化、新增、移除或非互惠连接。本次核验的本地 3 个电网满供，其他星球电网未 fresh 核验。玩家包中的 Ti Ore `50`、Ti Ingot `0`、Warp `400`、Rod `5`、Stone `6` 未变；本阶段没有取用 Ti Ore、Ti Ingot 或 Warp，也没有启动短采样。

root 独立核验引用：`0299d7dfe49841f0b44eefb14c697743:32`，SHA256 `F0DD764966429710D5B7F3EFF8D1C69B2F6D22753A94F46B9C43DA930502A7E5`。原阶段引用：`336560424cf143a5a25c78b7e54e947b:98`，SHA256 `C13A5AF163EF4AE29C668220C32AA51F7BFB21B75978398C3461B7BBEBA46710`。批准事实摘要 `.local/r845-approved-facts.json` 的 SHA256 为 `55FB446C8F46B5AACC0E74F5A68EE2BE96B718710CA91FCB8276DF8A306DD398`。

`sourceConditionsPassed=false`、`wholeSupplyPassed=false`，continuous credit 为 `0`。R783 连续来源长窗失败保持，R807 短窗仅作有限诊断。西侧到达不是来源接入或持续供料证据。下一项需 fresh 资格化结构不同的有限北向路线，再考虑有限取料、保存与短资格采样；本页不批准施工、不表示本地 flight 能力存在。双自动补给、整合保存恢复、Warper/Rod 连续门与最终同 SHA 双候选仍未通过。
