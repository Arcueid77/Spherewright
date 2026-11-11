# 变更记录

[English](CHANGELOG.md) · [简体中文](CHANGELOG.zh-CN.md)

## 0.6.0 — Ascension开发

- 增加经既有移动动作显式选择的原生同星球导航，核对当前阈值、完整能耗储备、Fly/抵达/接地停稳回读及自有任务失败隔离。
- 行星导航将仅解锁配方的机甲科研计入能耗，保全矩阵点数、完整队列效果核验及具体准备拒绝前提。
- 行星导航数值绑定支持经典游戏与当前MCP运行时一致校验，补充跨运行时准备DTO序列化回归。
- 原生导航在Walk/Drift/Fly下按地表投影抵达及任务释放完成，回读运动/接地观察并支持继续从水面移动到陆地。
- 任务释放后保留正常原生下降控制及独立回读，抵达结果立即完成。
- 低速漂浮导航、仓储转移和手搓绑定新鲜服务器观察，保留固定位移/时限和准确库存核对。

- 增加无令牌的当前恒星戴森节点/框架/壳层观察，包含有界覆盖、来源绑定设计hash及已交付/在途结构与细胞进度。
- 增加射线接收器详情及当前恒星覆盖，记录原生最近实际发电、容量、光子缓冲与透镜账目。
- 身份、拓扑或扫描覆盖不完整时，汇总值保持可空。
- 增加有界多对象设计/成本只读预览及逐项prepare的原生节点/框架/壳面创建，核对准确几何、科技、分配位置及原动作回读。

## 0.5.0 — 2026-11-10

- 有界跨星探索、航线预算、正常航行/停止、翘曲器补给、外星施工及站点物流。
- 原生首帆/运载火箭检查与配置，记录各自源码范围的当地制造、真实发射、抵达和保存恢复。
- 原生叠放建造、模型/宿主兼容、完整逐层观察及仓储/配送器选择器。
- 准确大小仓分层筛选和禁格、集装机空带路径原生反向及守恒单件放带。
- 行走卡住诊断和无令牌的有界短路线预览。
- 持久原动作核销、owned 主档连续性及原生/材料/能源/返航安全门。

[发行说明](docs/release-notes/v0.5.0.zh-CN.md)

## 0.4.0 — 2026-10-17

- 有界 Overseer 诊断；Foundry 配方、材料、场地、基础功率规划及原生施工。
- 有限蓝图进度、取消和 fresh 续建；Governor 执行前声明及实际游戏 tick 观察。
- 配套 MCP/Plugin 工具面、嵌入并随包提供的指南、durable Journal 绑定 owned 主档恢复及精确版本迁移披露。
- 冻结生产验收和最终同源双包/安装/恢复检查均在记录的本机范围内通过。
- `0.10.35.29151` 兼容包括精确过期主档 `29145→29151` 方向、第五次持久 Journal 转换及完成历史保留。

已从 `b7c0002dbe2e8f5608c5ca140b0c6326a1818021` 发布到 [GitHub](https://github.com/Arcueid77/Spherewright/releases/tag/v0.4.0)。较早的冻结生产验收源码 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9` 保留独立证据。另见[发行说明](docs/release-notes/v0.4.0.zh-CN.md)与[验收](docs/validation/v0.4.0.zh-CN.md)。Thunderstore 网站上传核验单独跟踪。

## 较早的已发布版本

- [v0.3.3](https://github.com/Arcueid77/Spherewright/releases/tag/v0.3.3)：和平模式下沙盒与资源倍率兼容；[历史说明](packaging/thunderstore/changelog/0.3.3.md)。
- [v0.3.2](https://github.com/Arcueid77/Spherewright/releases/tag/v0.3.2)：新档命名、开局指南与结构化移动失败；[历史说明](packaging/thunderstore/changelog/0.3.2.md)。
- [v0.3.1](https://github.com/Arcueid77/Spherewright/releases/tag/v0.3.1) 和 [v0.3.0](https://github.com/Arcueid77/Spherewright/releases/tag/v0.3.0) 保留原发行说明及支持范围。

既有发行 tag、包及历史证据继续对应原始产物。
