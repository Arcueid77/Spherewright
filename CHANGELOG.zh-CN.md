# 变更记录

[English](CHANGELOG.md) · [简体中文](CHANGELOG.zh-CN.md)

## 0.5.1 — 2026-11-13

- 单座风机原生回收，绑定完整电网拓扑并精确核对建筑物品及增产点。
- 只读电网影响预览，包含发电损失、预计电网、受影响消费者及未满足条件。
- 重建使用新的普通建造操作，保留原动作核销及其他世界对象/电网连接。
- 兼容旧请求默认值，新增预览与回读字段。

[发行说明](docs/release-notes/v0.5.1.zh-CN.md)

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
