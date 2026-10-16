# 环境研究

[English](environment.md) | [简体中文](environment.zh-CN.md)

## 维护基线

已验收的本机 `0.4.0` 候选源码为 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`，环境为 Windows x64、DSP `0.10.35.29145`、BepInEx `5.4.17.0`。[产品状态](../project-status.md)和[最终验收](../validation/v0.4.0.md)区分此候选与最新正式 `v0.3.3`。候选包保持冻结；文档变更不重建工件，也不构成新安装或实机验证。

早期逐次部署流水、特定机器审计和当时待验结果属于历史，完整不可变来源见[已验收提交中的研究](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/research/environment.md)。详细运行证据属于 [Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7)，Lab 消费固定、只读产品依赖；产品构建/测试/打包不依赖 Lab。

## 可复现输入

| 输入 | 维护事实 |
| --- | --- |
| 此候选已验收平台 | Windows x64 |
| 原生 DSP 引用 | `0.10.35.29145`；`Assembly-CSharp.dll` SHA-256 `B84B60DF5A24947A014801F23B9B85D1E77853F42A331918E2DC85B153E50527` |
| BepInEx 引用 | `5.4.17.0`；SHA-256 `DC1CB6B58B962BDA5AAA1D6B5F9AE14EC174F61836A1A1F96C1A040C7E8381F7` |
| Plugin 目标 | `net472`、BepInEx 5；游戏/Unity 引用不复制到发行输出 |
| Contracts / Bridge.Core | `netstandard2.0` |
| MCP / 测试 | `net8.0`；自包含 Windows x64 MCP 是发行工件 |
| 依赖锁定 | `Directory.Packages.props` 和项目 `packages.lock.json` |
| 本地引用 | 忽略的 `.local/game-refs`；不得提交游戏程序集 |
| 嵌入指南 | `docs/agent-playbook.md`，资源名 `Spherewright.Mcp.agent-playbook.md` |

包版本以仓库项目文件与锁文件为准，不沿用旧机器的 SDK/运行时清单。原生哈希标识精确研究输入，仅哈希匹配不能证明 Plugin 正在运行或玩法成功。

## 研究与验证方法

1. 通过产品定位脚本解析 DSP 安装，使用 `SPHEREWRIGHT_DSP_DIR`、Steam 元数据或显式 `-DspDir`。文档使用 `<DSP_ROOT>`，跟踪输出不得含私有绝对路径。
2. 针对性使用 Mono.Cecil 或 ILSpy 元数据/反编译前，记录原生版本来源、文件长度及 SHA-256。检查精确签名、指令路径和不变量；不以反射探针执行游戏代码，不提交反编译游戏源码。
3. 仅在获准刷新编译引用时使用 `scripts/sync-game-refs.ps1`；它复制必要引用并记录哈希，不修改游戏程序集。原生版本研究与迁移权限分离。
4. 锁定恢复依赖、相关自动测试和完整受支持引用 Plugin 构建只证明源码/离线结果，不证明安装、MCP 握手、存档认领、产量或长跑。
5. 独立获准冷部署时，先正常保存/审计/关闭，再替换完整同批工件。每种构建形式与其自身输出比较：RID 与非 RID 公共程序集不要求字节相同。后续读回不同不自动授权热替换 DLL 或重复安装。
6. 分别验证已安装哈希、真实 stdio 元数据/指南、当前 Plugin 认证、精确恢复和原生读回。MCP stdout 纯净/正常退出证明该探针协议结果，不证明全部动作或发行门。

## 保留结论与限制

便携 SDK、显式 NuGet 源和大小写不敏感的 Steam 根目录去重解决了历史环境问题，未修改全局用户设置。Plugin 显式禁用依赖 SDK 环境的隐式框架引用注入，并锁定 `net472` 引用包。沙盒 `BasicProductionLineCoordinator.cs` 仍被排除编译；历史注入生产不算普通世界验收。

最终候选具有同机包安装、原生运行、指南和 owned-primary 恢复证据。异机、Mod Manager/r2modman profile、安装事务崩溃恢复和广泛第三方兼容仍未建立。规划中的远程验证机器不是已实现功能。秘密、原始存档、嵌入身份、凭据、运行时描述符和私有路径始终是受保护本地材料，不成为研究 fixture 或公开证据。
