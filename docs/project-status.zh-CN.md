# 产品状态

[English](project-status.md) · [简体中文](project-status.zh-CN.md)

**活动阶段（2026-10-19）：`0.5.0` Voyager 开发。** 所有者阶段启动指令保留且仅保留三项验收：正常跨恒星安全往返，带回出发星系有合法证据缺失的资源；至少一条持续跨恒星物流路线；一次可恢复飞行失败及其精确 checkpoint 恢复，且不回滚较新 primary 进度。实机验收仍为 **0/3**。较早的可见信息/预算及 checkpoint 保护开发预览已安装，较新的飞行/取消控制器尚未安装，最终手动/Thunderstore 双候选仍缺失。详见[范围与能力/缺口表](../ROADMAP.zh-CN.md#050--voyager)及 [Lab 活动快照](https://github.com/Arcueid77/Spherewright-Lab/blob/main/scenarios/v0.5-voyager/current-status.md)。

以正式 0.4 源码 `b7c0002dbe2e8f5608c5ca140b0c6326a1818021` 及其 DSP `0.10.35.29151` 证据为出发基线，保留全部已发布 tag/包/哈希、历史锁和验收，不重做已验收的 0.4 建厂、两倍扩产或长窗。Thunderstore 网站上传核验仍是单列的 0.4 发布事项，不阻塞无依赖的 0.5 工作，也不授权重新发布。新公开 Voyager 能力归入 0.5 开发版本，保留 0.4 协议兼容；历史 64 工具数量不是能力上限。

当前源码复用既有 flight/checkpoint/station，已实现两个服从探索可见性的星图/航线预算只读预览、机甲空仓的原生翘曲器补给，以及有限 Voyager 飞行/取消路径。执行切片的离线验证已通过：完整 Plugin Release 构建零警告/错误，3,704 项测试全过且零跳过，九项工作流辅助检查及内嵌指南/源码 MCP 核验通过。部署前仍须准确已推送 SHA 的 CI 与 Lab 固定采用；新控制器尚未安装。较早已安装的只读开发预览不代表控制器验收，普通部署保存/恢复也不是 Voyager 飞行故障证明。

历史同星系阶段限制不禁止新阶段；原生、安全、隐私和明确确认门全部保留。产品修改使用本独立仓或已批准 worktree，完整 Plugin 构建、相关检查及准确 SHA 绿 CI 后，Lab 才推进固定只读依赖。实际安装、候选哈希及实机结果须与活动源码锁分开记录，缺失工件保持缺失。本阶段不授权 0.5 tag、Release 或注册表上传。

**下载（2026-10-17）：**[GitHub v0.4.0](https://github.com/Arcueid77/Spherewright/releases/tag/v0.4.0) 已发布手动 ZIP、Thunderstore 布局 ZIP、校验和及生成清单。附注 tag `v0.4.0` 指向源码 `b7c0002dbe2e8f5608c5ca140b0c6326a1818021`。2026-10-17 最后一次有记录的 [Thunderstore 官方条目](https://thunderstore.io/c/dyson-sphere-program/p/Arcueid_77/Spherewright/)核查显示 `0.3.3`；所有者网站自助上传的实际版本和结果仍未核验，与 0.5 分开跟踪。安装 0.4.0 请使用 GitHub 手动包。

冻结生产验收源码为 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`，原生版本为 `0.10.35.29145`。两份 ZIP 保持冻结，hash 见[验收](validation/v0.4.0.zh-CN.md)；链路、生产长窗及准备套件证据保留原源码/session 范围。

0.4 最终双包源码为 `b7c0002dbe2e8f5608c5ca140b0c6326a1818021`，原生版本为 `0.10.35.29151`。SDK 8.0.424 完整锁定 Release 构建零警告/错误，3165 项测试全部通过（Contracts 73 / Core 2834 / MCP 258），九项流程及[准确源码 CI](https://github.com/Arcueid77/Spherewright/actions/runs/39018031556)通过。双包强制双语和嵌入指南检查通过，均准确提供 64 工具及一个资源。

最终 b7 手动与标准 Thunderstore 两种布局均已完成正常冷安装及同版本 `29151→29151` 默认 owned 主档恢复。每种布局各自独立通过一轮完整的已安装 64 工具矩阵：23 数据成功、6 预检成功、35 预期原生拒绝，矩阵 accepted/unknown 均为零。两次恢复保留持久 Journal 和已完成历史，没有再次迁移。安全拒绝证明调用时保留原生安全门，不表示执行了每项受保护写操作。对应源码安装、恢复及完整矩阵的最终独立结果审计全部通过。详见[固定对应源码实机报告](https://github.com/Arcueid77/Spherewright-Lab/blob/74a4ec3d91608467911460eaf0d317800e60309b/operations/formal-release-2026-10-17.md)。

原世界明确确认的 `29145→29151` 迁移和先前两布局的调用/同版本恢复证据仍对应 `e018b7f45c70f5fe029418cad987af794b5cb9bf`，见[固定历史报告](https://github.com/Arcueid77/Spherewright-Lab/blob/00f4359eef704bc7cd5c148438c05e0d3121bda6/operations/package-callability-2026-10-17.md)；不能把这次 b7 的同版本恢复写成另一迁移。[原生验证](validation/native-29151-preflight.zh-CN.md)区分这两次源码范围。

后续纯事实文档 SHA 不会重新构建 0.4 双包，也不改变其历史安装证据；这些记录仍绑定 b7，与已安装的 0.5 开发预览分开。独立管理器 profile、另一台计算机、安装事务崩溃恢复及静止运输船 600 tick 正向实机分支仍未验证，不增加生产长窗。

只读[Nexus 核查](https://www.nexusmods.com/dysonsphereprogram/mods/23?tab=files)发现原 0.3.3 主文件处于隔离状态，公开页面未说明检测引擎或具体原因。不声明新 0.4 已上传或获 Nexus/第三方扫描放行；本次仅检查 Nexus。

仓库拆分已经合并。产品保留四个耦合源码项目、通用测试、打包安装脚本、指南、协议、安全模型及双语文档。[Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab) 维护场景、交接和完整证据，不是产品构建依赖。[固定 v0.4 证据快照](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7) 继续作为来源边界。

另见[发行说明](release-notes/v0.4.0.zh-CN.md)、[Lab 设置](sample-validation.zh-CN.md)、[路线图](../ROADMAP.zh-CN.md)和[文档](README.zh-CN.md)。当前指南区分源码行为、历史证据及未验证分支。本页不代表 fresh 游戏观察或游戏授权。
