# Spherewright

[English](README.md) · [简体中文](README.zh-CN.md)

Spherewright 通过配套 MCP 服务和 BepInEx Plugin，把外部 Agent 接入《戴森球计划》。结构化观察、规划和有界原生动作保留正常材料、科研、供电、施工无人机及游戏时间约束。产品不内置 LLM 或自主扩产循环。

Plugin、MCP、Contracts 和 Bridge.Core 保持为强版本耦合的 monorepo。[Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab) 维护场景、运行交接和完整证据；产品独立构建、测试和打包。

## 版本与范围

最新正式发行版是 [v0.3.3](https://github.com/Arcueid77/Spherewright/releases/tag/v0.3.3)。**0.4.0 是本机验收通过、等待所有者审核发布的候选**，尚无正式 tag、GitHub Release 或 Thunderstore 发布。冻结候选源码为 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`。后续文档更改不替换其候选包或运行证明。

- **Overseer**：有界生产、能源、物流及科研观察，上游追踪与诊断。建议不授予写入权限。
- **Foundry**：多级配方、材料、设备、基础功率及有界场地和依赖计划，明确外部供给。预览与执行保持独立。
- **有限动作**：原生施工、配置、受支持升级、取材与有限蓝图，保留逐对象进度、取消及 fresh 续建。
- **Governor**：执行前生产声明与实际游戏 tick 观察。库存、额定容量和短样本不能证明持续生产。
- **存档连续性**：owned、票据绑定的精确主档恢复，保持 durable Journal 连续性；已披露迁移另需后续确认。不提供任意选档或编辑器。

候选已通过规定的三级链、连续联合供给、准备套件、保存恢复及两种本机包布局检查。[验收](docs/validation/v0.4.0.zh-CN.md)记录准确 hash、范围与限制。实际跨恒星航行和物流仍属后续工作。

## 安装与使用

0.4 已验收配置：Windows x64、DSP `0.10.35.29145`、BepInEx `5.4.17.0`，一个 owned、和平、非沙盒、1× 的本机世界。旧发行版保留各自支持范围。Plugin、MCP 和共享程序集必须配套。

当前维护正在准备 DSP `0.10.35.29151` 兼容候选。[原生预检](docs/validation/native-29151-preflight.zh-CN.md)区分源码研究与冻结的 29145 验收，新的包及实机检查由 Lab 继续记录。

- [安装](docs/release-installation.zh-CN.md)与[中断安装恢复](docs/installation-recovery.zh-CN.md)
- [Agent 指南](docs/agent-playbook.zh-CN.md)，英文版也嵌入 MCP 并随包提供
- [协议](docs/protocol.zh-CN.md)、[安全模型](docs/safety-model.zh-CN.md)和[架构](docs/architecture.zh-CN.md)
- [发行说明](docs/release-notes/v0.4.0.zh-CN.md)、[变更记录](CHANGELOG.zh-CN.md)和[全部文档](docs/README.zh-CN.md)

写入要求 fresh 原生 prepare、计划绑定的 commit、原 action 核销及相关读回。结果未知时进入隔离。物品或科技注入、瞬间施工、传送及直接修改游戏缓冲不属于公开动作面。黑雾战斗、多人/Nebula、广泛第三方兼容及另一台计算机使用尚未验证。

## 开发

```powershell
dotnet restore Spherewright.Core.slnf --locked-mode
dotnet build Spherewright.Core.slnf -c Release --no-restore
dotnet test Spherewright.Core.slnf -c Release --no-build
```

完整 Plugin 构建需要合法安装的 DSP/BepInEx 引用，见[环境说明](docs/research/environment.zh-CN.md)。打包脚本记录本仓自身源码 SHA 与 clean 状态，不拉取或执行 Lab。

场景工作从 Lab 开始；固定产品子模块默认只读。通用修复在独立 clone/worktree 实现、测试、提交并取得准确 SHA 的成功 CI 后，Lab 才更新 gitlink/锁。另见[贡献政策](CONTRIBUTING.zh-CN.md)、[协作规则](AGENTS.zh-CN.md)、[产品状态](docs/project-status.zh-CN.md)和[Lab 设置](docs/sample-validation.zh-CN.md)。

使用 [MIT 许可](LICENSE)，另有[非权威中文译文](LICENSE.zh-CN.md)。
