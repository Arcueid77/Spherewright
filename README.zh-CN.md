# Spherewright

[English](README.md) · [简体中文](README.zh-CN.md)

Spherewright 通过配套 MCP 服务和 BepInEx Plugin，把外部 Agent 接入《戴森球计划》。结构化观察、规划和有界原生动作保留正常材料、科研、供电、施工无人机及游戏时间约束。产品不内置 LLM 或自主扩产循环。

Plugin、MCP、Contracts 和 Bridge.Core 保持为强版本耦合的 monorepo。[Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab) 维护场景、运行交接和完整证据；产品独立构建、测试和打包。

## 版本与范围

最终同源 0.4.0 双包的源码为 `b7c0002dbe2e8f5608c5ca140b0c6326a1818021`，面向 DSP `0.10.35.29151`。冻结生产验收源码仍为 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`。后续纯事实文档提交不改变双包或安装源码身份。

**下载（2026-10-17）：**[GitHub v0.4.0](https://github.com/Arcueid77/Spherewright/releases/tag/v0.4.0) 已发布手动 ZIP、Thunderstore 布局 ZIP、校验和及生成清单。附注 tag `v0.4.0` 指向源码 `b7c0002dbe2e8f5608c5ca140b0c6326a1818021`。[Thunderstore 条目](https://thunderstore.io/c/dyson-sphere-program/p/Arcueid_77/Spherewright/)仍为 `0.3.3`，其 `0.4.0` 上传尚未完成；安装 0.4.0 请使用 GitHub 手动包。

- **Overseer**：有界生产、能源、物流及科研观察，上游追踪与诊断。建议不授予写入权限。
- **Foundry**：多级配方、材料、设备、基础功率及有界场地和依赖计划，明确外部供给。预览与执行保持独立。
- **有限动作**：原生施工、配置、受支持升级、取材与有限蓝图，保留逐对象进度、取消及 fresh 续建。
- **Governor**：执行前生产声明与实际游戏 tick 观察。库存、额定容量和短样本不能证明持续生产。
- **存档连续性**：owned、票据绑定的精确主档恢复，保持 durable Journal 连续性；已披露迁移另需后续确认。不提供任意选档或编辑器。

冻结的 29145 候选已通过规定的三级链、连续联合供给、准备套件、保存恢复及两种本机包布局检查。[验收](docs/validation/v0.4.0.zh-CN.md)记录其准确 hash、范围与限制。这些生产结果保留原源码及 session 范围。实际跨恒星航行和物流仍属后续工作。

## 安装与使用

冻结生产验收使用 Windows x64、DSP `0.10.35.29145`、BepInEx `5.4.17.0`，一个 owned、和平、非沙盒、1× 本机世界。最终候选面向同一本机环境及 DSP `0.10.35.29151`。旧发行版保留各自支持范围，Plugin、MCP 和共享程序集必须配套。

最终 b7 手动与标准 Thunderstore 两种布局均已完成正常冷安装及同版本 `29151→29151` 默认 owned 主档恢复。每种布局各自独立通过一轮完整的已安装 64 工具矩阵：23 数据成功、6 预检成功、35 预期原生拒绝，矩阵 accepted/unknown 均为零。两次恢复保留持久 Journal 和已完成历史，没有再次迁移。安全拒绝证明调用时保留原生安全门，不表示执行了每项受保护写操作。对应源码安装、恢复及完整矩阵的最终独立结果审计全部通过。详见[固定对应源码实机报告](https://github.com/Arcueid77/Spherewright-Lab/blob/74a4ec3d91608467911460eaf0d317800e60309b/operations/formal-release-2026-10-17.md)。

此前 `e018b7f45c70f5fe029418cad987af794b5cb9bf` 的两布局调用、明确确认的原世界 `29145→29151` 迁移及之后同版本恢复，仍由[固定历史报告](https://github.com/Arcueid77/Spherewright-Lab/blob/00f4359eef704bc7cd5c148438c05e0d3121bda6/operations/package-callability-2026-10-17.md)证明，不转记为 b7 的新迁移或生产长窗。另见[原生验证](docs/validation/native-29151-preflight.zh-CN.md)。

只读[Nexus 核查](https://www.nexusmods.com/dysonsphereprogram/mods/23?tab=files)发现原 0.3.3 主文件处于隔离状态，公开页面未说明检测引擎或具体原因。不声明新 0.4 已上传或获 Nexus/第三方扫描放行；本次仅检查 Nexus。

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
