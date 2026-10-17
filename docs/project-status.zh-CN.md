# 产品状态

[English](project-status.md) · [简体中文](project-status.zh-CN.md)

**下载（2026-10-17）：**[GitHub v0.4.0](https://github.com/Arcueid77/Spherewright/releases/tag/v0.4.0) 已发布手动 ZIP、Thunderstore 布局 ZIP、校验和及生成清单。附注 tag `v0.4.0` 指向源码 `b7c0002dbe2e8f5608c5ca140b0c6326a1818021`。[Thunderstore 条目](https://thunderstore.io/c/dyson-sphere-program/p/Arcueid_77/Spherewright/)仍为 `0.3.3`，其 `0.4.0` 上传尚未完成；安装 0.4.0 请使用 GitHub 手动包。

冻结生产验收源码为 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`，原生版本为 `0.10.35.29145`。两份 ZIP 保持冻结，hash 见[验收](validation/v0.4.0.zh-CN.md)；链路、生产长窗及准备套件证据保留原源码/session 范围。

最终双包源码为 `b7c0002dbe2e8f5608c5ca140b0c6326a1818021`，原生版本为 `0.10.35.29151`。SDK 8.0.424 完整锁定 Release 构建零警告/错误，3165 项测试全部通过（Contracts 73 / Core 2834 / MCP 258），九项流程及[准确源码 CI](https://github.com/Arcueid77/Spherewright/actions/runs/39018031556)通过。双包强制双语和嵌入指南检查通过，均准确提供 64 工具及一个资源。

最终 b7 手动与标准 Thunderstore 两种布局均已完成正常冷安装及同版本 `29151→29151` 默认 owned 主档恢复。每种布局各自独立通过一轮完整的已安装 64 工具矩阵：23 数据成功、6 预检成功、35 预期原生拒绝，矩阵 accepted/unknown 均为零。两次恢复保留持久 Journal 和已完成历史，没有再次迁移。安全拒绝证明调用时保留原生安全门，不表示执行了每项受保护写操作。对应源码安装、恢复及完整矩阵的最终独立结果审计全部通过。详见[固定对应源码实机报告](https://github.com/Arcueid77/Spherewright-Lab/blob/74a4ec3d91608467911460eaf0d317800e60309b/operations/formal-release-2026-10-17.md)。

原世界明确确认的 `29145→29151` 迁移和先前两布局的调用/同版本恢复证据仍对应 `e018b7f45c70f5fe029418cad987af794b5cb9bf`，见[固定历史报告](https://github.com/Arcueid77/Spherewright-Lab/blob/00f4359eef704bc7cd5c148438c05e0d3121bda6/operations/package-callability-2026-10-17.md)；不能把这次 b7 的同版本恢复写成另一迁移。[原生验证](validation/native-29151-preflight.zh-CN.md)区分这两次源码范围。

后续纯事实文档 SHA 不代表双包或安装源码重新构建；它们保持 b7。独立管理器 profile、另一台计算机、安装事务崩溃恢复及静止运输船 600 tick 正向实机分支仍未验证，不增加生产长窗。

只读[Nexus 核查](https://www.nexusmods.com/dysonsphereprogram/mods/23?tab=files)发现原 0.3.3 主文件处于隔离状态，公开页面未说明检测引擎或具体原因。不声明新 0.4 已上传或获 Nexus/第三方扫描放行；本次仅检查 Nexus。

仓库拆分已经合并。产品保留四个耦合源码项目、通用测试、打包安装脚本、指南、协议、安全模型及双语文档。[Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab) 维护场景、交接和完整证据，不是产品构建依赖。[固定 v0.4 证据快照](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7) 继续作为来源边界。

另见[发行说明](release-notes/v0.4.0.zh-CN.md)、[Lab 设置](sample-validation.zh-CN.md)、[路线图](../ROADMAP.zh-CN.md)和[文档](README.zh-CN.md)。当前指南区分源码行为、历史证据及未验证分支。本页不代表 fresh 游戏观察或游戏授权。
