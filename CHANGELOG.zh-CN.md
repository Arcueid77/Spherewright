# 变更记录

[English](CHANGELOG.md) · [简体中文](CHANGELOG.zh-CN.md)

## 0.5.0 — Voyager基线已验收，首帆/首枚火箭扩展开发中

- 在已有产业能力上新增跨恒星飞行、普通翘曲器补给、有界远端建设及活动运输船观察。
- 原Voyager条款已按各自源码范围验收，含真实异星采矿、三轮运输、母星消费及保存恢复后续运。已验收源码已合入main，原交审双包继续在Lab保留。
- 所有者2026-11-01新增正常制造并真实发射首张太阳帆目标。弹射器/云详细检查和固定轨道配置正在开发；源码检查与实装、真实发射证据分别记录。
- 增加显式enable-only原生机甲施工无人机开关，以新鲜玩家状态和同源回显绑定；普通新建在创建待建体前拒绝关闭/不可用的无人机。保留已付料待建体，正常游戏tick负责完成，不重新创建或瞬间建成。
- 既有施工边界仍保留，包括远端单批最多十二个新传送带对象。首枚运载火箭及必要的最小接收结构已纳入0.5；通用戴森结构设计和持续发射网络仍归0.6。本条目不代表创建0.5 tag、Release或平台上传。

另见[当前产品范围](docs/project-status.zh-CN.md)、[原生首帆研究](docs/research/game-api-first-sail.zh-CN.md)及[Lab唯一活动快照](https://github.com/Arcueid77/Spherewright-Lab/blob/main/scenarios/v0.5-voyager/current-status.md)。

## 0.4.0 — 已在GitHub发布；以下保留候选阶段细节

- 有界 Overseer 诊断；Foundry 配方、材料、场地、基础功率规划及原生施工。
- 有限蓝图进度、取消和 fresh 续建；Governor 执行前声明及实际游戏 tick 观察。
- 配套 MCP/Plugin 工具面、嵌入并随包提供的指南、durable Journal 绑定 owned 主档恢复及精确版本迁移披露。
- 冻结手动包和Thunderstore布局候选已通过规定的本机安装、运行、恢复及连续供给验收；其后v0.4.0已在GitHub发布，Thunderstore网站上传核验仍单列。
- `0.10.35.29151` 兼容候选：精确过期主存档 `29145→29151`、第五次持久 Journal 转换及完成历史保留；[离线证据与待完成实机检查](docs/validation/native-29151-preflight.zh-CN.md)与冻结验收分别记录。

另见[发行说明](docs/release-notes/v0.4.0.zh-CN.md)与[验收](docs/validation/v0.4.0.zh-CN.md)。后续文档维护不替换冻结候选字节。

## 已发布版本

- [v0.3.3](https://github.com/Arcueid77/Spherewright/releases/tag/v0.3.3)：和平模式下沙盒与资源倍率兼容；[历史说明](packaging/thunderstore/changelog/0.3.3.md)。
- [v0.3.2](https://github.com/Arcueid77/Spherewright/releases/tag/v0.3.2)：新档命名、开局指南与结构化移动失败；[历史说明](packaging/thunderstore/changelog/0.3.2.md)。
- [v0.3.1](https://github.com/Arcueid77/Spherewright/releases/tag/v0.3.1) 和 [v0.3.0](https://github.com/Arcueid77/Spherewright/releases/tag/v0.3.0) 保留原发行说明及支持范围。

既有发行 tag、包及历史证据继续对应原始产物。
