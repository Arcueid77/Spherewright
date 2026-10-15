# 暂缓的局域网验收主机设计

[English](remote-validation.md) · [简体中文](remote-validation.zh-CN.md)

**状态：暂缓；本文不代表已实现或验证远程主机与部署流程。** 本机 0.4 验收见[验收摘要](validation/v0.4.0.zh-CN.md)，不能据此证明另一台计算机可用。

## 预期拓扑

```text
开发计算机：源码、构建、自动测试
    -> 认证 SSH + SCP/SMB 产物部署
游戏计算机：交互式 DSP + BepInEx + Plugin + MCP
    -> 本机认证 Named Pipe -> Plugin 主线程执行器
    -> 通过认证 SSH 返回脱敏结构化结果
```

Named Pipe 只留在游戏计算机，不通过 TCP 或 SMB 暴露。MCP 在该机运行、本地发现描述文件，仅通过认证命令通道传输 MCP stdio。token 与描述文件保留在游戏计算机。

## 后续预期流程

1. 核准确 DSP/BepInEx 版本及原生程序集 hash，按该 cohort 构建。
2. DSP 运行时拒绝部署，不杀进程或替换已加载 DLL。
3. 仅 stage 配套 Plugin/MCP 产物并安装目标版本。
4. 在明确授权、已经登录的交互式 Windows session 启动 DSP 并停在菜单；不假设 SSH 服务 session 能启动可用 GUI。
5. 运行另行批准的结构化验收，保持 fresh 原生/所有权门、单 writer 和有限预算。使用公开 MCP/Plugin 接口，不用界面识别或输入宏。
6. 仅收集结构化结果与脱敏日志，与存档目录分离，不枚举、复制或读取 Save。
7. 按已批准配置生命周期恢复 `Safety.AllowWrites=false` 的仅观察模式。

优先使用密钥认证 OpenSSH，并收窄账号与防火墙权限。不向 Plugin 添加 HTTP/SSE/Streamable HTTP，不共享 `%LOCALAPPDATA%/Spherewright/runtime`，不在脚本或聊天放凭据。未来若支持导入副本，仍先满足本机同样的明确导入和 owned 主档规则；路径或名字不代表所有权。除非另行声明范围，参考验收仍为和平、非沙盒、1×。

## 启动门

`scripts/remote/prepare-game-host.ps1`、`deploy-game-host.ps1`、`run-validation.ps1` 与 `collect-evidence.ps1` 等仅是设计占位名，不是已交付工具。

只有本机发行流程完成且可重复、新建普通世界基线及安全取证建立、准确运输和交互启动方法选定，且所有者明确恢复此独立工作后，才开始实现。本机证据应排除秘密、存档文件访问、界面宏、沙盒调用和无法解释的物品变化。本轮仓库维护不授予远程部署或游戏权限。
