# 已确认的主档 29131→29145 迁移

2026-10-15，用户对已披露的精确迁移明确回复“确认这次精确迁移”。本报告只记录同一 owned 主档在 P104 上从 Native 0.10.35.29131 到 0.10.35.29145 的这次迁移；Journal origin 仍为0.10.34.28529，不是本次载入源版本。不扩展到其他存档、版本或完整 0.4 验收。此前候选及其未完成门见[当前快照](../../current-status.md)和[双候选报告](delivery-candidates-2a7a98d.md)。

## 迁移终态

唯一 resume action `b3b57506-b00e-48ed-ad55-6a52e7dc3eac` 为 completed/succeeded，accepted 增加 1。fixed20 窗口 external 从 1 增至 2、lifetime 从 609 增至 610；whole accepted 从 210 增至 211。此次没有新的 Move 或外部 Save commit。

原候选为 tick 116215326 / Journal 102。前三项转换及完整 102 条 Journal 与 R1318 原 Native 逐项一致；第四项 31→45 转换已 durable，adopted tick 为 116215327、Journal 仍为 102，`persistencePending=false` 且 error 为空。随后 Native 自动正常 resave 到 116215357 并签发 45 版新票据；这是迁移内的持久化，不是第二次 accepted。旧票据已消费，没有再次 Save 或重放迁移。

新票据 SHA-256 为 `EC49DBBD4527CFC87AAE486D04C4C0F817E4BDDA53CE2ABD6DF23145C4DC5F5F`，版本 45、minimum tick 116215357、checkpoint 102；独立读回确认它匹配同一当前 owned session、PID 35240，未过期且非 quarantine。本文不记录票据内容、token、存档名或原始身份。

## 蓝图进度与当前边界

首个 caller 用时 18.8 秒、发出 14 次请求，但漏传 `buildId`，使 `get_blueprint_builds` 使用默认 `summary_only`：返回 5/5 计数而没有 objects，`fresh=false`，caller 外层 exit 1。原失败结果保留；它不是迁移失败，也不证明蓝图丢失。

随后获批的 120 秒只读读回用精确 build ID `39cc6803-df11-4d9c-8864-37db49dc8ca9`，4 次读取约 5.2 秒完成。freshWorldReconciled 与 persistenceHealthy 均为 true，completed/submitted 为 5/5、无 blockers；对象 6622、6624、6623、6626、6625 均保全，没有重建。该读回不增加 accepted。

fresh 观察为 tick 116217768 / revision 1。先前退出前观察 tick 116289647 不计为恢复后的信用。当前玩家石矿为 0；原石矿动作只保留历史计数，不能称那 4 个石矿已保存或已恢复，也不换键重取。root 独立核验迁移 action 后无在途、`unknown=false`；窗口仍 frozen，剩 18 槽且包含保存槽。

后续完整工厂只读读取已完成：133 次请求、67 页，读到 6626 个 built、0 个 prebuild、59 个 detail，用时 19.194 秒。root 独立与 R1321 比较全部 6626 项的配置、位置、旋转、配方、过滤、仓储、连接及资源节点，差异为 0。该读取属于 Native 45 新 session，不计入任何连续观察窗口或恢复窗口信用。

当前实际安装仍是 07d1727 cohort；本次文档 HEAD 为 `3246886ca763aa5e5a94733d70b79be069687901`，已推送且 clean，[准确 Windows Core CI 38634709236](https://github.com/AvaloNero/Spherewright/actions/runs/38634709236) completed/success。两份候选仍来自源码 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`，本次事实记录未改包或重打包。

唯一后续游戏目标仍是现有 1209 自动输入、1210 输出及完整三级链。上述完整工厂读取为只读，不增加 accepted 或世界写入；完整三级施工、中途恢复覆盖、恢复后新的同 session ≥36000 tick 联合窗，以及两候选的实际安装、运行和恢复门均未关闭。

## 原件索引

| 事实 | 受保护记录引用 | SHA-256 |
| --- | --- | --- |
| 用户确认精确迁移后的 root 批准 | `1092a0f645d44932bf309d8a8f9b7ee3:1` | `8C6F916750A0FBB52978596C7ED4C09979A224760E6DCB125F69028C38B7EE74` |
| 首次迁移 writer 原始结果，含 caller 读回失败 | `089fa0adc37140e490a80faf912415ef:16` | `979E4E7D4FEAB15B7E6735E6FFFA4EB6B9F2AE4C7AE3E33DF4AED49D6C144C51` |
| 精确 build ID 的纠正只读读回 | `a38b386529ac41d5bf16188d67119335:5` | `E765ED541063F24E8273FDC5779A714C97275EB43182FD03AE4D8826E30BCC7A` |
| root 独立核验迁移、自动 resave、Journal 与五对象 | `073d28e37da2434d8de96395ad6ae633:1` | `A0AD497B2E61BAB17ACEC66320564FC48EC3692C2B191E009F91699152F07486` |
| 完整工厂只读采集，133 请求 / 67 页 / 6626 built | `14627977f6d441ed975d5005f8ee48ca:134` | `B859A312AEB123F349C447EEA75F7BA1736876FAA64D9CC5DA5C27B40DB517E7` |
| root 对 R1321 配置与实体完整性独立比较 | `3349ec7fd5d54f37bcb9252632ce9eb4:1` | `AE75674B8E1A22D6D2A8CFE5286BE92DEC0CBFF0DCD290F94F87DFE5DA2FB3A8` |
