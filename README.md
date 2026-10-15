# Spherewright

Spherewright 是《戴森球计划》的外部 Agent 控制层。Plugin 在游戏内提供受保护的原生适配，MCP 为外部 Agent 提供观察、规划和有限行动接口；产品不内置 LLM、无界扩产循环或任意存档编辑器。

Plugin、MCP、Contracts 和 Bridge.Core 作为一个强版本耦合的 monorepo 开发、测试和打包。详细场景、运行交接和完整验收证据由私有 [Spherewright-Lab](https://github.com/AvaloNero/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7) 消费仓维护；产品构建不依赖它。

## 版本与能力

已发布版本为 [v0.3.3](https://github.com/AvaloNero/Spherewright/releases/tag/v0.3.3)。**0.4.0 是验收完成、待所有者审核发布的候选**，没有正式 v0.4.0 tag、GitHub Release 或 Thunderstore 发布。

- **观察与诊断**：读取生产、能源、物流、科研、库存和建筑状态；Overseer 归因与建议不会自动获得写入权限。
- **Foundry**：从明确目标速率计算多级配方、物料、设备与基础功率，提供有界场地与依赖预算。预览和有限施工执行保持独立。
- **有限行动**：正常材料、科技、原生地形/碰撞、无人机和游戏时间约束下的建造、配置、升级、取材与有限蓝图；保留逐对象进度、取消与安全续建。
- **Governor**：预先声明非零基线、目标、误差和观测边界，按有效游戏 tick 判断实际变化；库存、名义容量和短窗口不代替持续生产证据。
- **保存与恢复**：只处理明确 owned、票据绑定的精确主档，维护 durable Journal、一次性票据和版本披露；不提供任意存档选择或跨版本绕过。

0.4 候选已完成限定三段供应链、连续供给、准备清单、保存恢复及两种本地安装布局验收；[最终摘要](docs/validation/v0.4.0.md)列明候选 SHA、包校验和、适用范围和未证明项。实际跨恒星航行及物流属于后续版本。

## 安装与使用

已验证的 0.4 配置为 Windows x64、DSP `0.10.35.29145`、BepInEx `5.4.17.0`。已发布 0.3.x 的历史支持边界以对应发行说明为准。请使用同一构建的 Plugin、MCP 和共享程序集。

- [安装指南](docs/release-installation.md)与[安装事务恢复](docs/installation-recovery.md)
- [Agent playbook](docs/agent-playbook.md)：源码中嵌入 MCP、并随包提供的通用指南
- [协议](docs/protocol.md)、[安全模型](docs/safety-model.md)、[架构](docs/architecture.md)
- [Release notes](docs/release-notes/v0.4.0.md)与[CHANGELOG](CHANGELOG.md)

所有游戏写入都必须经过 fresh 原生 prepare、绑定精确计划的唯一 commit、原 action 终态与相关状态核验。未知结果进入隔离，不换键重放。没有物品/科技注入、瞬建、传送、后台直接改游戏对象或任意存档改写。黑雾战斗、多人/Nebula、广泛第三方兼容和跨恒星曲速不在当前已验证范围。

## 产品开发

```powershell
dotnet restore Spherewright.Core.slnf --locked-mode
dotnet build Spherewright.Core.slnf -c Release --no-restore
dotnet test Spherewright.Core.slnf -c Release --no-build
```

完整 Plugin 构建另需本机合法 DSP/BepInEx 引用，按 [环境说明](docs/research/environment.md)准备。通用测试、打包、安装、协议与安全实现均在本仓。打包入口 `scripts/package-release.ps1` 从本仓自身 HEAD 和 clean 状态记录源码，并调用 `scripts/package-thunderstore.ps1`；它们不读取或依赖 Lab 仓。

场景运行从 Lab 根目录开始。子模块默认只读；产品修改在独立 clone/worktree 完成，提交并通过准确 SHA 的产品 CI 后，Lab 才更新 gitlink 与锁文件。[场景验证说明](docs/sample-validation.md)提供固定提交入口；[产品状态](docs/project-status.md)记录简洁的产品交付状态。

## English

Spherewright exposes bounded Dyson Sphere Program observation, planning, and native actions to an external Agent through MCP and a matching Plugin. It does not embed an LLM or autonomous expansion loop. The four source projects remain one product monorepo.

Released v0.3.3 and the unpublished v0.4.0 candidate have separate support scopes. The 0.4 candidate targets Windows x64, DSP 0.10.35.29145 and BepInEx 5.4.17.0. See the [validation summary](docs/validation/v0.4.0.md) for exact provenance, local installation/recovery evidence, continuous-supply acceptance and limits. Detailed operational evidence lives in the pinned private Lab repository, which is not a product build dependency.
