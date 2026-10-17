# 产品状态

[English](project-status.md) · [简体中文](project-status.zh-CN.md)

截至 2026-10-17，**0.4.0 等待所有者审核发布**。最新正式版本为 v0.3.3，尚无正式 v0.4.0 tag、GitHub Release 或 Thunderstore 发布。

冻结生产验收源码为 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`，原生版本为 `0.10.35.29145`。两份 ZIP 保持冻结，hash 见[验收](validation/v0.4.0.zh-CN.md)。其链路、长窗及准备套件证据保留原源码和 session 范围。

当前包调用验收候选为 `e018b7f45c70f5fe029418cad987af794b5cb9bf`，原生版本为 `0.10.35.29151`。完整锁定 Release 构建零警告/错误，3165 项测试全部通过（Contracts 73 / Core 2834 / MCP 258），九项流程及[准确源码 CI](https://github.com/Arcueid77/Spherewright/actions/runs/38951284927)通过。

官方手动布局和标准 Thunderstore 布局各自完整通过已安装的 64 工具矩阵；每轮分别记录 23 数据成功、6 预检成功、35 预期原生拒绝，accepted/unknown 均为零。明确确认的原世界迁移、正常保存及之后的同版本恢复保留持久 Journal 和已完成历史。[固定 Lab 报告](https://github.com/Arcueid77/Spherewright-Lab/blob/00f4359eef704bc7cd5c148438c05e0d3121bda6/operations/package-callability-2026-10-17.md)记录包 hash、独立审计和有界范围；[原生验证](validation/native-29151-preflight.zh-CN.md)衔接兼容修复与这些证据。

后续这些事实文档更改保留 e018 候选源码、安装源码及两份原包，不代表另一构建或新的持续生产长窗。独立管理器 profile、另一台计算机及安装中断恢复仍未验证。

仓库拆分已经合并。产品保留四个耦合源码项目、通用测试、打包安装脚本、指南、协议、安全模型及双语文档。[Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab) 维护场景、交接和完整证据，不是产品构建依赖。[固定 v0.4 证据快照](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7) 继续作为来源边界。

另见[发行说明](release-notes/v0.4.0.zh-CN.md)、[Lab 设置](sample-validation.zh-CN.md)、[路线图](../ROADMAP.zh-CN.md)和[文档](README.zh-CN.md)。当前指南区分源码行为、历史证据及未验证分支。本页不代表 fresh 游戏观察或游戏授权。
