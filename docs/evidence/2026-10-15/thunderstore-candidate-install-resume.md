# Thunderstore 候选的本地安装、运行与恢复

本记录覆盖同一冻结、clean 且 CI 通过的源码候选 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`。它记录 Thunderstore 标准共置载荷在同一本地 DSP 安装根中的实际适用验证；候选包本身的构成见[候选报告](delivery-candidates-2a7a98d.md)，与[来源分配阶段记录](three-stage-source-allocation.md)及[当前状态](../../current-status.md)分开。

## 安装与菜单

整体操作顺序为先执行一次正常 Close，再通过既有配对事务将原始 Thunderstore ZIP（SHA-256 `81D9D6ED0BAEC843AB22ADE38158102212A5487A43EFEE22769F500B7CF85D91`）中的 5 个文件共置于现有本地 DSP 安装根，随后执行一次派发和两次读取。事务保留了原 manual 布局的 224 个 MCP 文件和 606 条 handoff 记录，并以 committed 终态完成；实际事务 marker 已不存在，因此这不证明崩溃恢复。229 个实装产品文件哈希、5 份 Native refs 与产品源码版本核验均通过。派发和菜单读取共 68.688 秒、0 个 accepted 动作；Native `0.10.35.29145` 菜单健康，PID 为 50884。

## 同版本主档恢复与 MCP 运行

同一 owned 主档的同版本 default resume 用 80 个请求、1 个 accepted 动作完成，耗时 37.954 秒；恢复流程内的 Native 正常重存位于 tick `116298956`、revision 1、Journal `J102`，不是额外 external Save；迁移、manual 与 Thunderstore 阶段各有一次 Native 内部重存，外部普通保存仍为 70。四次历史转换保留。完整工厂读回为 6660 built、0 prebuild，静态配置差异和非互反连接均为 0，背包完整保全。原有五个 completed 对象仍为 5/5；旧拓扑标记仍在，所指两条图外边有计划记录，不据此宣称旧建造可重放或标记已清除。当前阶段账目为 fixed-20 external 16、lifetime 624、whole 225。

实装单文件 MCP 通过 7 次 RPC，其中 3 次为游戏只读、写入为 0。检查覆盖全部 64 个精确工具名及既有 schema/annotation、resource 和 guide 断言；进程 exit 0，stdout 仅含协议数据。此结果适用于同一本地安装根中的原始 Thunderstore 共置载荷，不代表 Thunderstore Mod Manager/profile 安装、跨电脑安装或长时生产验收通过。完整游戏联合长窗和出发物资差额仍待验证。

## 原件索引

| 证据 | 保护引用 | SHA-256 |
| --- | --- | --- |
| Thunderstore ZIP 配对事务与菜单的 writer 原件 | `6f8115e3fa8f45d8a01e745cc03f267b:4` | `F53DE59FBE496209627C6F55986FD12B1E8C1FA37971683F705E1494352BAFF6` |
| 安装文件、Native refs、菜单与 handoff 的独立核验 | `a65674b7ae494a4484769d3b33f00f0d:1` | `ABCBD8D9792A951E5A085EF3673EFFC5B6B9D26BEC221DDBC9BBDBD0DA7E385B` |
| 同版恢复与完整工厂读回的独立核验 | `63f4d93e6e514724b6b119f229c2fde4:1` | `8EEF70D61AB50AD607B3DD5F71FACC3E6784FE54BE1B4EC05B2171F4C9B62A64` |
| 已安装 Thunderstore MCP 的协议运行核验 | `1611f1ef4a914527931c5821a9534bf4:1` | `6AC4918A90B7ACC0E9716801FCF832FB7A2245CF13CFF3431A0656D8194D7326` |
