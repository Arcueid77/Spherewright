# 文档索引

[English](README.md) · [简体中文](README.zh-CN.md)

维护指南提供等价英文和简体中文版本，可通过页首语言链接切换。短兼容跳转页及商店模板在同一文件中包含完整双语。原始英文 MIT 许可具有权威性，[中文译文](../LICENSE.zh-CN.md)仅便于理解。

## 从这里开始

| 主题 | English | 简体中文 |
| --- | --- | --- |
| 产品概览 | [README](../README.md) | [说明](../README.zh-CN.md) |
| 当前产品与候选状态 | [Status](project-status.md) | [状态](project-status.zh-CN.md) |
| 安装 | [Install](release-installation.md) | [安装](release-installation.zh-CN.md) |
| 手动安装中断 | [Recovery](installation-recovery.md) | [恢复](installation-recovery.zh-CN.md) |
| Agent 操作 | [Playbook](agent-playbook.md) | [指南](agent-playbook.zh-CN.md) |
| 协议与工具边界 | [Protocol](protocol.md) | [协议](protocol.zh-CN.md) |
| 架构 | [Architecture](architecture.md) | [架构](architecture.zh-CN.md) |
| 安全 | [Safety model](safety-model.md) | [安全模型](safety-model.zh-CN.md) |
| 贡献 | [Policy](../CONTRIBUTING.md) | [贡献政策](../CONTRIBUTING.zh-CN.md) |
| Agent 贡献者 | [Rules](../AGENTS.md) | [协作规则](../AGENTS.zh-CN.md) |
| 已批准的版本方向 | [Roadmap](../ROADMAP.md) | [路线图](../ROADMAP.zh-CN.md) |
| 变更 | [Changelog](../CHANGELOG.md) | [变更记录](../CHANGELOG.zh-CN.md) |

## 发行证据及边界

- [0.4 验收](validation/v0.4.0.md) / [中文](validation/v0.4.0.zh-CN.md)：冻结源码/hash、本机验收与未验证分支。
- [0.4 发行说明](release-notes/v0.4.0.md) / [中文](release-notes/v0.4.0.zh-CN.md)：已验收候选，不是正式发行。
- [Lab 场景与证据](sample-validation.md) / [中文](sample-validation.zh-CN.md)：固定证据快照、只读产品锁与复现。
- [暂缓远程验收](remote-validation.md) / [中文](remote-validation.zh-CN.md)：后续设计，不代表已部署远程能力。
- [文档维护](documentation-audit.md) / [中文](documentation-audit.zh-CN.md)：审过的矛盾与核验边界。
- [商店包模板](../packaging/thunderstore/README.md)及[历史包说明](../packaging/thunderstore/changelog/0.3.3.md)为页内双语。

最新正式版本是 v0.3.3；已验收 0.4 候选源码为 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`。main 文档可更新，不改变冻结 ZIP 或已安装组件。

## 原生研究

- [29151 兼容预检](validation/native-29151-preflight.md) / [中文](validation/native-29151-preflight.zh-CN.md)：精确过期主存档方向、完成历史保留、离线证据及待完成实机检查。

[环境](research/environment.zh-CN.md)、[M0 历史基线](research/game-api-m0.zh-CN.md)、[Overseer](research/game-api-overseer.zh-CN.md)、[Foundry](research/game-api-foundry.zh-CN.md)、[29057/29088 历史](research/game-api-version-0.10.35.zh-CN.md)、[29104 历史](research/game-api-version-0.10.35.29104.zh-CN.md)、[29145 兼容](research/game-api-version-0.10.35.29145.zh-CN.md)及[安装启动门](research/installation-startup-guard.zh-CN.md)均有完整双语。研究注明适用版本及直接符号；历史发现不验收当前运行时。

## 兼容路径

258 个短运行跳转页，包括[当前状态](current-status.md)、[证据索引](evidence/README.md)、[交付条款](v0.4-delivery.md)及[路线图历史](../ROADMAP-HISTORY-2026-09-24.md)，保留固定 Lab 链接。它们属于兼容或历史入口，不是当前执行指令。不要把完整历史账本当作工作计划加载。

在产品根目录运行 `pwsh -File scripts/verify-documentation.ps1`，检查全部 Markdown、语言导航、关键 hash/native 版本、相对链接/锚点及明显私有路径或凭据模式。CI 执行同样检查。翻译语义等价以及代码/证据解释还需要审阅。
