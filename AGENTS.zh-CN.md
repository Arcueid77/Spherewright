# Spherewright 产品协作规则

[English](AGENTS.md) · [简体中文](AGENTS.zh-CN.md)

Spherewright 是 Plugin / MCP / Contracts / Bridge.Core 强版本耦合 monorepo。按需阅读[产品状态](docs/project-status.zh-CN.md)、[文档](docs/README.zh-CN.md)、[路线图](ROADMAP.zh-CN.md)、[架构](docs/architecture.zh-CN.md)、[协议](docs/protocol.zh-CN.md)和[安全](docs/safety-model.zh-CN.md)。不要把完整历史运行账本加载进模型上下文。

- 通用能力、通用测试、打包安装脚本、协议、安全、用户指南及简明逐版本验收留在本仓。四个源码项目保持一起。
- 场景、writer 范围、状态交接、时间线、事故与完整证据归 [Spherewright-Lab](https://github.com/AvaloNero/Spherewright-Lab)。历史证明使用[固定 v0.4 快照](https://github.com/AvaloNero/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7)。不向产品仓复制存档特定实体 ID、tick、R 编号或运行流水。
- Lab 消费固定只读产品子模块。通用变更在独立 clone/worktree 实现，经过相关测试、可审核提交及准确 SHA 的成功 CI 后，Lab 才更新 gitlink/锁。产品构建、测试和打包不能依赖 Lab。
- `docs/agent-playbook.md` 是 EmbeddedResource 和包输入，资源名/URI 保持稳定。变更需要核验嵌入资源与包内内容，不会更新冻结候选或已安装 MCP。维护完整中文伴页和包内语言导航。
- 保留原生安全：Unity 对象只在主线程访问，线程外使用深复制 DTO，fresh prepare，精确计划/session/身份绑定，single-flight commit，原 action/幂等键核销及 unknown 隔离。禁止物品或科技注入、瞬间施工、传送、任意存档编辑或直接写游戏缓冲。
- 文档不授予游戏、Save/Load、部署、迁移恢复或正式发布权限。游戏工作从 Lab 的明确范围及 fresh 原生门开始；历史证据不授权重放。
- 不提交 Save、票据、凭据、私有身份或路径、私有原始请求、运行时描述文件、DLL 或崩溃转储。受保护本地存储必须被忽略。
- root 负责设计与独立审查。指定离线维护者串行负责所有 Git/gh、显式路径暂存、提交推送及准确 SHA 的 CI 收集。并行文档编辑者使用互斥文件并保留他人成果。禁止 `git add .`、reset 或 clean。
- 优先按分开的线性提交合入，不新增合并提交。改写共享历史须有所有者明确授权、受保护本地备份、精确新旧映射及 tree 检查，并使用精确 ref 的 `--force-with-lease`；禁止普通 force。保留旧 tag 及无关成果。后续普通变更正常推送。
- 文档保持等价英文与简体中文，可用 `.md` / `.zh-CN.md` 配对，短跳转或商店模板可用完整双语段落。代码、符号、标识符及 hash 保持一致。区分当前指南、历史研究、离线检查和实机验收。
- 运行相关检查。产品 CI 负责产品构建测试，Lab CI 负责锁、manifest 及证据。pending 不等于成功。公开仓审查涵盖可达历史、公开元数据及日志，不只检查最新 tree。
- Claude Code 模型名及 API/base URL 是用户有意管理的配置，不要修改。非交互调用必须流式输出；单独出现 `claude-code:unrecognized_model` 不代表终态，不应据此重试或改配置。

另见[Lab 验证](docs/sample-validation.zh-CN.md)、[v0.4 摘要](docs/validation/v0.4.0.zh-CN.md)、[安装](docs/release-installation.zh-CN.md)和 [Agent 指南](docs/agent-playbook.zh-CN.md)。
