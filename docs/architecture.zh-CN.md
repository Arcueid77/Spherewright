<a id="architecture"></a>
# 架构

[English](architecture.md) | [简体中文](architecture.zh-CN.md)

Spherewright 由四个版本耦合的源码项目组成。产品向外部 Agent 提供有界的正常游戏操作；目标与有限计划由 Agent 选择。

本文描述当前维护的源码。已验收的 0.4.0 候选固定于 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`，原生游戏基准为 `0.10.35.29145`，最新正式发布仍为 v0.3.3。修改文档不会更新已安装的 Plugin/MCP 组合，也不会改变已验收候选归档。参见[产品状态](project-status.zh-CN.md)与 [v0.4 验证](validation/v0.4.0.zh-CN.md)。

兼容候选面向 `0.10.35.29151`，只加入[原生预检](validation/native-29151-preflight.zh-CN.md)所述的精确过期主档方向及完成历史保留。其源码测试与待完成实机验证不改变上述冻结基准。

<a id="four-product-boundaries"></a>
## 四个产品边界

| 项目 | 职责 | 边界 |
| --- | --- | --- |
| `Spherewright.Contracts` | 传输 DTO、方法名、错误码与版本化证据字段 | 不包含游戏对象或游戏程序集 |
| `Spherewright.Bridge.Core` | 帧处理、认证、有界调度、快照、确定性规划、指纹、幂等及安全策略 | 输入与输出独立于游戏 |
| `Spherewright.Plugin` | BepInEx 5 适配器、本地 Named Pipe、原生观察与正常游戏执行 | 所有 DSP/Unity 访问都在 Unity 主线程 |
| `Spherewright.Mcp` | MCP stdio 工具、受保护的 Bridge 发现与内嵌 Agent 指南 | 不引用游戏 DLL，也不另做一套游戏执行实现 |

产品可独立于 [Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7) 构建、测试和打包。Lab 维护场景、当前操作交接、游戏写入者声明与详细证据，其产品子模块是固定依赖。产品代码不反向依赖 Lab。

```text
外部 Agent 选择有限目标
  -> 读取经过认证的结构化观察
  -> 计算或检查明确且有界的计划
  -> prepare 单个正常操作或有限原生模块
  -> 核对精确返回计划与当前授权
  -> 使用原幂等身份仅 commit 一次
  -> 轮询同一 action 至终态
  -> 对账材料、实体与双向连接
```

<a id="main-thread-adapter-and-bounded-transport"></a>
## 主线程适配器与有界传输

Pipe 工作线程将有界 JSON 信封解析为自有请求 DTO。`SpherewrightBridgeHost.PumpMainThread` 更新会话身份，并在配置的数量与帧预算内处理 `BoundedMainThreadDispatcher`。默认每帧最多四个请求、2 ms 调度预算。单次原生操作仍受自身输入与扫描上限约束；帧预算不能抢占正在执行的原生调用。

离开主线程的结果只能是 Spherewright 自有的深拷贝。后台线程不保留活的 `GameData`、工厂、玩家或 Unity 对象。Pipe、描述文件与受保护持久化使用当前用户访问控制；Plugin 启动时轮换认证凭据。参见[协议](protocol.zh-CN.md)与[安全模型](safety-model.zh-CN.md)。

<a id="ownership-and-durable-evidence"></a>
## 所有权与持久证据

创建来源或明确确认的接管绑定精确的受管世界。无关的已加载会话仅暴露受限状态与导入可用性，不暴露存档、玩家、星球或工厂内容。导入 prepare 绑定当前进程、会话、revision 与 `GameData`；之后必须获得会话中的明确确认，才能通过正常保存建立新的受管副本。原始身份从不成为公共存档选择参数。

持久 Gameplay Journal 从声明的跟踪边界记录事件。接管已有世界时保留历史覆盖不完整这一事实，不补造缺失的早期事件。恢复在接管前后校验原 Journal 检查点；不可变的起始版本、有效原生版本与有界的持久版本转换分别保留。

受保护恢复凭据、飞行前检查点、有限建造进度和 Governor 声明有各自独立的生命周期，不能充当任意存档选择器，也不能相互替代。重启恢复符合条件的证据，不恢复旧 prepare 令牌。参见[受保护恢复](safety-model.zh-CN.md#protected-recovery-and-version-changes)。

<a id="normal-game-execution"></a>
## 正常游戏执行

`NormalGameActionCoordinator` 负责 prepare/commit、精确目标与资源绑定、操作历史、单飞检查及结果未知隔离。适配器使用原生移动、采集、制造队列、建造工具、无人机、配置路径、转移、升级和正常保存。不发放物品、不完成科技、不注入能量、不直接完成预建造、不传送、不修改存档，也不绕过原生成本。

移动规划留在 Plugin 外部。`prepare_move` 校验有限目标与可选的地表证据，不证明通道无碰撞。180/600 游戏 tick 的移动看门狗只停止自己拥有的精确命令，并返回有界恢复建议。同恒星飞行在独立验证内部检查点后使用原生 Fly/Sail 状态转换并支付能量。

历史沙盒基础产线协调器不参与 Plugin 编译，也不注册为公共 MCP 工具。不得重新启用其中的直接发放与完成路径作为捷径。

<a id="read-only-diagnostics-and-planning"></a>
## 只读诊断与规划

Overseer 将生产与电力、物流、研究领域采集为有界深拷贝快照。诊断包在同一个主线程任务中采集两部分，仅在工厂身份、星球、运行标志和游戏 tick 完全一致时合并星球。公共白名单 DTO 不含存档身份、凭据或文件系统路径。不可变的分页结果不能把之后的独立读取变成事务。

Foundry 根据已解锁原生配方，确定性计算材料需求、机器数量和明确的副产物。可选机器网格或显式蓝图选址评估增加原生放置、库存、电力与额定运输证据。这些读取始终 `executable=false`。仅含机器的网格不会计价或证明完整物流网络；额定通道容量也不证明实测吞吐或持续供应。

Governor 对比实测缺口与升级、新机器、模块复制方案，将星球产量、需求、本地余量、缓冲变化和额定增益分别呈现。有界被动观察器与不可变的执行前声明可以测量合格吞吐窗口；Governor 不自动开启下一轮扩建、不选择新目标，也不自动认证整体平衡。参见 [Governor 协议](protocol.zh-CN.md#governor-proposal-04-development-slice)。

<a id="finite-module-data-and-upgrade-execution"></a>
## 有限模块数据与升级执行

蓝图检查、导出只接收明确的有界数据或选定受管实体，始终不可执行。独立的有限执行器复用普通会话、revision、`GameData` 绑定、原生蓝图几何、计划与幂等存储及隔离机制。

Core 的 `BlueprintBuildState` 保存不可变几何、有向依赖与逐对象回执。Plugin 每游戏 tick 最多通过原生建造工具提交一个依赖已满足的对象，支付正常材料并等待无人机。`BlueprintBuildStore` 按受管身份原子持久化预写与结果证据。部分完成状态可见；取消只停止后续提交，保留已有工作。

重启对账先证明已知实体、材料回执、配置及双向连接和自由端拓扑，再通过新 prepare 仅继续尚未提交的后缀；绝不重放整个部分完成模块。Foundry 蓝图组合把原材料意图、显式完整成本、运输分配与电力绑定到同一个执行器，不建立第二套规划循环。

原生升级对已公布子集调用 `DoUpgradeObject`，立即证明完整新设备成本、旧设备返还、身份与配置、货物、拓扑及原生时序，不直接写原型 ID。额定速度提升与 Governor 实测验收仍是不同证据。

<a id="validation-deployment"></a>
## 验证部署

源码能力、离线测试、已安装组件组合与实机验收是不同事实。已安装 MCP 必须重新构建后才能获得更新的内嵌指南；编辑本文或源指南不会热更新二进制。稳定资源 URI 为 `spherewright://agent/playbooks/opening-movement-v1`，逻辑资源名为 `Spherewright.Mcp.agent-playbook.md`。

当前验收证据概述见[验证记录](validation/v0.4.0.zh-CN.md)。同局域网独立游戏验证机仍是延期架构：Pipe 留在游戏机本地，部署、远程 MCP 与证据收集走经过认证的远程命令通道。参见[保留的方案](remote-validation.zh-CN.md)，该架构需要独立明确的任务范围。

早期架构与操作细节保留于[已验收源码](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/architecture.md)与 [Lab 固定参考](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7)。历史成功是证据，不是重放计划或新执行授权。
