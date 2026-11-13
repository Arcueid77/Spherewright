# Spherewright

[English](README.md) · [简体中文](README.zh-CN.md)

Spherewright 通过配套 MCP 服务和 BepInEx Plugin，把外部 Agent 接入《戴森球计划》。结构化观察、确定性规划 helper 和有界原生动作保留正常材料、科研、供电、施工无人机及游戏时间约束。产品不内置 LLM 或自主扩产循环。

Plugin、MCP、Contracts 和 Bridge.Core 构成一个版本强耦合的 monorepo。[Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab) 维护场景、运行交接和完整证据；产品构建、测试和打包不依赖 Lab。

## 当前状态

| 项目 | 当前状态 |
| --- | --- |
| 维护发行线 | [`v0.5.1`](https://github.com/Arcueid77/Spherewright/releases/tag/v0.5.1)，面向 DSP `0.10.35.29151` / BepInEx `5.4.17.0` 的风机原生回收及电网影响预览 |
| Voyager 基线 | 原三项条款已在各自源码范围内验收，同源交审双包保留于 Lab |
| 首帆/首枚运载火箭扩展 | 2026-11-06 记录有界实机验收：外星当地制造、4 帆真实入云、1 枚火箭抵达并增加 1 个原生结构点、辅料送达及普通保存/冷恢复续接 |
| 选定扩展实装/交审源码 | `d3b7ac897bb44d62c8a7ed54c51a55ff2b8de7b0`；准确源码 CI、Native 构建、同源安装和包载荷检查在记录范围内通过 |
| 风机维护 | 单座风机准备/提交/回读、只读影响预览及独立普通重建。[0.5.1说明](docs/release-notes/v0.5.1.zh-CN.md) |

发行物包含手动 ZIP、Thunderstore 布局 ZIP、校验和及生成清单。直接安装使用手动包，并遵循同包配套的安装与 Agent 指南。

开发源码、Lab 固定采用、实际安装、最终候选和历史验收是不同状态。后续源码或文档提交不改变已发布包身份，也不自动继承历史实机信用。各自范围见[产品状态](docs/project-status.zh-CN.md)、[ROADMAP](ROADMAP.zh-CN.md)和 [Lab 活动快照](https://github.com/Arcueid77/Spherewright-Lab/blob/main/scenarios/v0.5-voyager/current-status.md)。

## 能力与边界

- **Overseer**：有界生产、供电、物流及科研观察、上游追踪与诊断；建议不授予写入权限。
- **Foundry**：配方、材料、设备、功率、场地和依赖计划，明确外部供给；预览与执行保持独立。
- **有限原生动作**：施工、配置、受支持升级、取材和有界蓝图，保留逐对象进度、取消及 fresh 续建。
- **Governor**：执行前生产声明和实际游戏 tick 测量；库存、额定容量和短样本不能证明持续生产。
- **存档连续性**：票据绑定的精确 owned 主档恢复，保持 durable Journal 连续性；已披露迁移另需后续确认。不提供任意选档或编辑器。
- **Voyager 与发射扩展**：原生可见探索、有限飞行/恢复、当地制造/作业和物流原语；已验收基线与首帆/首枚火箭交审保留各自范围，不代表通用覆盖。

所有写入都要求 fresh 原生 prepare、计划绑定的 commit、原 action/幂等核销及相关读回；结果未知时进入隔离。物品或科技注入、瞬间施工、传送和直接修改游戏缓冲均被排除。黑雾战斗、多人/Nebula、广泛第三方 Mod 兼容及另一台计算机使用尚未验证。

## 安装与使用

1. 阅读[安装指南](docs/release-installation.zh-CN.md)，使用配套的 Plugin、MCP 和共享程序集。
2. 替换现有安装前，先了解[中断安装恢复](docs/installation-recovery.zh-CN.md)。
3. 遵循 [Agent 指南](docs/agent-playbook.zh-CN.md)；英文指南同时嵌入 MCP 并随包提供。
4. 集成或审阅行为时，查阅[协议](docs/protocol.zh-CN.md)、[安全模型](docs/safety-model.zh-CN.md)和[架构](docs/architecture.zh-CN.md)。

发行证据按源码和用途区分：[0.4 发行说明](docs/release-notes/v0.4.0.zh-CN.md)、[0.4 验收](docs/validation/v0.4.0.zh-CN.md)、[29151 兼容证据](docs/validation/native-29151-preflight.zh-CN.md)和[固定对应源码实机报告](https://github.com/Arcueid77/Spherewright-Lab/blob/74a4ec3d91608467911460eaf0d317800e60309b/operations/formal-release-2026-10-17.md)。完整导航见[文档索引](docs/README.zh-CN.md)。

## 开发

```powershell
dotnet restore Spherewright.Core.slnf --locked-mode
dotnet build Spherewright.Core.slnf -c Release --no-restore
dotnet test Spherewright.Core.slnf -c Release --no-build
pwsh -File scripts/verify-documentation.ps1
```

完整 Plugin 构建需要合法安装的 DSP/BepInEx 引用，见[环境说明](docs/research/environment.zh-CN.md)。打包脚本记录本仓自身源码 SHA 与 clean 状态，不拉取或执行 Lab。

场景工作从 Lab 开始，固定产品子模块保持只读。通用修改在独立产品 clone/worktree 中实现、测试并提交，取得准确 SHA 的成功 CI 后，Lab 才更新 gitlink/锁。另见[贡献政策](CONTRIBUTING.zh-CN.md)、[协作规则](AGENTS.zh-CN.md)和 [Lab 设置](docs/sample-validation.zh-CN.md)。

使用 [MIT 许可](LICENSE)，另有[非权威中文译文](LICENSE.zh-CN.md)。
