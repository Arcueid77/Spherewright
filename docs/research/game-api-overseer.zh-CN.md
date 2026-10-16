# Overseer DSP API 研究

[English](game-api-overseer.md) | [简体中文](game-api-overseer.zh-CN.md)

## 范围与来源

原始检查使用 DSP `0.10.34.28529`、原生 SHA-256 `AE0BA95F75BD879A62AA4CE253B2AB78EAA4FB3C7C595F5E1FEE75EBE0E0EF85`。[完整不可变研究](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/research/game-api-overseer.md)保留公式、反例和带日期本机证据，它们属于历史的版本化观察。当前本机候选支持 DSP `0.10.35.29145`，以[最终验收](../validation/v0.4.0.md)限定；文档维护不是新的原生比较或部署。

## 工厂只读边界

只读取身份自洽、已经存在的 `GameData.factories[0..factoryCount)`。`GetOrCreateFactory` 创建持久游戏状态，`PlanetData.LoadFactory` 改变运行状态，均不属于观察。`factoryLoaded` 表示显示/物理/音频就绪，不代表持久工厂存在性；已创建远端工厂可以在 `factoryDisplayLoaded=false` 时读取。不得扫描存档或为发现未探索世界而创建工厂。全部原生读取在一次 Unity 主线程任务中完成，离开前完成深复制。

## 实际与理论生产

`FactoryProductionStat.GameTick` 更新 `ProductStat.count/cursor/total`。level 0 的 `total[0]`/`total[7]` 表示最近 600 游戏 tick 自动生产/消耗，即 60 tick/s 下十游戏秒；每分钟速率为计数 × 6。lifetime `total[6]`/`total[13]` 含机甲/手搓活动，不是自动产线窗口。数组通过原生 Export/Import 持久化，但仍须判定就绪和声明覆盖。

Governor 显式 3600 tick 选项读取 600 个完整六 tick level-1 桶，窗口末端是原生六 tick 边界，不伪写成当前 capture tick。`NativeMinuteProductionCounters.Read` 核对精确物品、数组长度/游标、非负桶及与 `total[1]`/`total[8]` 一致，不保留原生数组。绑定前历史、暂停、离线和缺口不能成为合格基线。

`OverseerTheoreticalProductionCalculator` 重现复制后的当前组件公式，覆盖装配机、矩阵站、矿/油/水采集、分馏、伽马接收和轨道采集。它不调用共享 UI 计算器，也不信任无时间戳 `refProductSpeed/refConsumeSpeed` 缓存。已接网设计容量不因临时缺料/缺电降低。身份/公式/边界未知即拒绝，不返回伪造零。利用率仅在就绪且分母为正时为实际/理论；离散短窗可超过 1，未知保持 null。

## 电力、物流与科研

实际发电是网络内身份绑定的 `PowerGeneratorComponent.generateCurrentTick` 之和。`PowerNetwork.energyExport` 是防御场导出，不是发电。当前发电/容量/负载属于动态采样，不是静态配置；发电能量缓冲单位为 `joules_per_tick`，不是物品数。满负载规划及持续燃料供应需要各自证据。

物流站读取绑定 `PlanetTransport.stationPool`、实体/组件、`StationStore`、物理槽位及行星/星际身份。区分行星、星际、轨道和矿物采集器类别，分别报告库存、有符号预约、闲置/工作机队、电量和供电。供应站有库存不证明派单或下游缺料根因。

科研是一份全局 `GameMain.history` 状态，不是每星球各一份。当前/排队科技与运行时 `techStates`、`LDB.techs` 核对，复制有序队列、等级、已上传/所需 hash 和矩阵预算。分页快照引用相同采集 tick 和科研副本。

## 诊断与时间证据

`GameStateReader.OverseerDiagnostics` 区分停机满输出的下一批准入阈值和显示容量。例如制造在产出 ≤ 9 批时仍可进入下一周期，精炼/化工路径为 ≤ 19 批。正在工作且已扣输入的机器不一定缺下一批物料。默认窗口短于配方周期不能使长周期满输出机器永久跳过诊断。

沿真实反向 `ReadObjectConn` 遍历受支持带、分拣器、分流器、集装器、喷涂机、仓和罐。每个物品路径受分拣过滤与分流器 `output0/outFilter` 方向约束。同星球其他位置有同物品物流站不算物理证据。跨星球追踪需要精确需求输出槽、匹配供应端及同一供应站真实输入路径；人工填塔或未证明中继不补造上游生产者。递归最多八层/64 生产者，明确循环/预算停止原因。

物流阻塞窗口要求合格精确路线、消费者缺料、正需求预约、匹配供应有库存且有机队。载具阶段/位置/速度/曲速/订单变化、送达或需求缩小均重置停滞。只有连续 600 游戏 tick 无进展才返回**疑似**而非确认阻塞。存档/路线变化、tick 回退、同 tick 突变或超过 3600 tick 缺口破坏连续性。`OverseerLogisticsProgressStore` 先原子持久化一批受保护记录，公共 DTO 再采用时间结果，排除离线墙钟时间。

## 同 tick 组合包、边界与剩余限制

`OverseerDiagnosticBundleComposer` 组合一次主线程工作项捕获的公共生产、电力/物流和科研 DTO；factory/tick 不一致则整包拒绝。版本化公开白名单无凭据、写计划、私有路径或原始存档身份字段。分页绑定 session、精确过滤、页大小和过期，完整首页不占 continuation 槽。

请求保持有界：最多 64 个生产物品、512 个既有工厂、16 星球/页、八个 continuation 槽、60 秒保留。组件/引用扫描超限显式失败，不静默截断总量；finding 披露自己的数量/截断。源码与契约限制为准。

已验收本机证据覆盖受控缺料/电力/物流故障、正常修复、活动运输/交付和保存/恢复窗口语义。真实不变载具连续静止 600 tick 再恢复仍未观察，不得直接改字段伪造。直接缓冲诊断覆盖装配机、矩阵站及受支持矿机；分馏/伽马/轨道直接覆盖明确为 partial。整体候选验收不取消这些限制。全部 finding 是采集时证据，不是永久根因或动作许可。
