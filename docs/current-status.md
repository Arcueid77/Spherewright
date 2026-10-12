# Spherewright 当前快照

更新：2026-10-12（Asia/Singapore）。当前安装的 Plugin/MCP 源码来自提交 0df97408058badfcb3e93b40f77953b4a0568347：228 个文件（4 个 Plugin、224 个 MCP）；准确源码 Windows Core CI 38207485135 成功。完整隔离 cohort 执行了 2,940 项测试，元数据为 64 个工具和 1 个资源。辅助证据解析器修正提交 9b5e0d0aedebed529d14e2079e1a3b2500d446d4 没有更换已安装二进制。最终双候选包尚未交付，也未正式发布。

## 当前保存与窗口

最近正常保存为 tick 112145524、revision 2、durable Journal 102。R1190–R1195 维护阶段新增 3 笔 accepted 操作：两次普通保存和一次同 owned 健康加载；独立核验确认保存闭合，无在途或未知动作。

固定 20 窗口从 lifetime 535 开始，目前 15/20、lifetime 550，冻结但仍开放，剩余 5 个槽位不转移。整案累计 151 accepted、46 次 Move、48 次普通保存、27 次手工材料动作。最近完整静态快照含 6,492 个实体、0 个预建对象、65 页；静态实体与互惠连接无变化。

## 当前只读主候选与实现修复

R1198–R1205共75次只读请求，新增21段完整原生点列、3次tokenless短端点预览和一次新有限目的整线预览；0游戏写入/accepted/Save/Load/Move/部署。原survey两次预览额度已结束，不重开。保留同一完整静态页与成功源13点/目标11点，批量淘汰西源6479（planned sorter重叠矿机1213）、宽西南225（旧带5424/5037/5385）及超出原生跨度的270走廊。建筑包围框筛选不替代原生带条件；对象1501现场是普通传送带2001。

当前“延长下穿东135”主候选6480→1501，6跨度、生成124点，旧带中心重合守卫清除，最远74.643米。完整只读请求返回INTERNAL_ERROR而没有Native assessment；不能据空值记原生调用0或称路线通过。对应日志和精确安装DLL离线复现已证明：只读评估指纹误调用只接受施工模式的BeltPathRoutingPolicy.BindGeometry，抛出ArgumentException“Unknown belt routing mode”。最小只读指纹修复已通过247项相关测试和完整Plugin Release编译（零警告/错误），尚未安装，准确提交CI仍待收集；普通施工模式限制保留。

没有可写主/备用候选；124点不是精确材料账。6跨度+2分拣器+至少一次Save的9动作模型超过剩5槽，真实ID/层间接缝/精确预算仍未证明。下一步完成相关测试及准确SHA绿CI后，受保护同批维护并限定一次修复验证；不自动扩原窗口、不重放旧预览。新鲜18个关键对象与互惠连接守恒；旧完整帧/2993几何对象/3010框/452地形点及五条货流库存是缓存，三厂长期供电未刷新；最新当地三网只作单点背景。

sourceConditionsPassed=false、wholeSupplyPassed=false、连续来源信用0，最终同SHA双候选包未交付、无正式发布。持续目标仍active。详见[批量下穿勘探与指纹异常证据](evidence/2026-10-12/iron-readonly-underpass-and-fingerprint-r1206.md)；此前安装维护和原A/B/C批次见[前一阶段证据](evidence/2026-10-12/iron-native-grid-batch-live-and-maintenance-r1197.md)。
