# 原生行星表面导航

[English](game-api-native-surface-navigation.md) · [简体中文](game-api-native-surface-navigation.zh-CN.md)

研究面向DSP0.10.35.29151，Assembly-CSharp SHA-256为 `EF314425DADE51F529F184F07BE136CD3C2660A6A81515603C7226FBE7AB1D8C`。本机合法安装程序集提供 `PlayerNavigation.Navigate(int, VectorLF3)`、`DetermineArrive()`、`Arrive()`及 `Abort()`。

## 原生控制路径

对当前星球，`Navigate`计算球面距离并依据角色现有阈值决定Fly/Sail。推进器等级和距离满足时，`PlayerMove_Walk.GameTick`通过 `DetermineLowVelocity`及普通 `SwitchToFly`路径起飞。`PlayerMove_Fly.GameTick`负责原生导航速度、高度、能源、重力及碰撞。`DetermineArrive`核对地面投影目标3米范围，`Arrive`释放任务并触发普通落地回调，适配器继续观察实际接地停稳。

`useNativeNavigation=true`以显式选择方式扩展 `spherewright_prepare_move`及既有提交/结果动作族。要求arrivalTolerance3..5、owned当前星球上的接地停稳角色、已解锁推进器、导航/移动任务为空及已关闭旅程。方案绑定当前星球与现有Fly/Sail阈值。距离须超过Fly阈值及20米，并保持在Sail阈值和500米以内。阈值参与方案绑定并在执行中重新核验。

## 能耗和失败处理

复用 `NativeLandingTickEnergyPolicy`，纳入当前原生推力、重力、工作、消耗倍率及独立背景开销。按60m/s垂直速度上界资助完整3600tick窗口，其中900tick保留给落地。已装载能源覆盖整窗，核心能源覆盖两个原生tick，执行中持续核对实际成本及剩余落地能源。科研点数和物品缓冲沿游戏正常账目保全。

队列中每项科技均有完整原生证明、仅解锁配方、属性函数及完成物品奖励均为0时，导航可将机甲科研正常计入能耗。非负矩阵库存及已缓冲点数继续供正常科研使用。制造队列、科研效果不完整或会改变成本时，准备阶段拒绝。无人机施工、手搓和科研设置提交等待原导航落地。准备拒绝保留具体工作或充能前提，供调用方诊断。

方案hash将浮点标量编码为IEEE754整数位，并规范化正负零。经典游戏运行时与MCP运行时可能把相同数值写为不同十进制文本。Windows回归在经典运行时加载正式策略，以兼容的Newtonsoft构建序列化准备DTO，并核验当前运行时的有预算方案回读。

工作额度结束时，仅释放准确的自有导航任务，并请求普通原生落地高度。完成要求已观察Fly、原生抵达、任务释放、世界/角色/控制器/星球引用一致、接地停稳Walk及声明终点距离。已知失败在接地后终结。身份丢失、外来控制或有限落地无法证明时，保留原动作为outcome_unknown并隔离，已接受/unknown动作保持原身份。

新方案/回读字段为 `nativeSurfaceNavigation`及 `nativeSurfaceNavigationReadback`。既有步行、被动漂移和无令牌步行预览保留原入口。原生构建及协议检查证明源码资格，实装移动和后续生产按对应源码的Lab记录验收。
