# Spherewright 当前快照

更新：2026-10-12（Asia/Singapore）。当前安装源码为 c488fb72a64f31db4fed722dccbe2a833ec63f0b，准确源码 Windows CI [38242390651](https://github.com/AvaloNero/Spherewright/actions/runs/38242390651) 成功。该提交包含既有只读整线指纹修复及仅29104→29131的有效凭据同主档迁移准备。400项相关测试、基于当前29131引用的完整Plugin Release编译、自包含MCP publish、228安装文件（4个Plugin、224个MCP）、实机菜单及64工具/1资源已独立核验；一次fresh迁移准备成功。主档尚未Load，迁移后的保存/Journal与整线实测未通过。旧批次测试仅保留为历史证据。最终双候选包未交付，也未正式发布。

## 当前保存与窗口

最近正常保存为 tick 112747810、revision 3、durable Journal 102，源游戏版本0.10.35.29104。R1209一次普通保存已独立核验全部原回执、精确库存/持物/增产点、完整Journal与已保存前缀；无在途或未知写动作。R1219正常关闭未加载菜单中的DSP、保留Steam并一次事务安装/桌面启动完成，当前DSP34540仍在健康未加载29131菜单。R1220实机prepare前后菜单revision0、凭据hash不变；原健康主档凭据未消费、未编辑。

固定20窗口从lifetime535开始，目前16/20、lifetime551，冻结但仍开放，剩4槽不转移。整案累计152 accepted、46次Move、49次普通保存、27次手工材料动作。最近完整静态基线含6,492实体、0预建对象、65页，属于旧已保存session的缓存；本轮没有载入新session，不能声称新版本实体持久性已通过。

## 当前29131同主档迁移准备与确认边界

此前冷启动出现Assembly-CSharp哈希变化，R1214只读状态确认游戏为0.10.35.29131；与启动期间Steam更新相符，更新操作者未证明。用户随后明确选择“以29131继续，准备同主档迁移方案”，仅授权准备，实际Load保留具体披露之后的确认门。

当前代码新增显式 `reauthorize_version_changed_primary` / evidence5，仅支持有效健康原凭据、准确29104→29131、同一ticket-bound primary及原完整Journal。已有两条Journal迁移保留，只允许这个准确第三条，第四条/跳级/降级仍拒绝；默认健康恢复仍要求同版本。实机披露candidate/minimum tick均112747810、P104、embedded identity已验证，`userConfirmationRequired=true`、`commitAllowedNow=false`，唯一blocker为 `USER_CONFIRMATION_REQUIRED`。实际Load尚未授权；下一步须用户确认这个具体披露，再fresh prepare并核同一candidate/digest，不能复用过期token。游戏程序集、凭据日期、存档及历史Journal未编辑。

当前29131程序集SHA74AC6162CEF8394A6021C8475132CAE4DF4F840871A99E63CC7D2C8297661BC0。944项Plugin Native成员引用全部匹配（含10项展开泛型），11个选定保存/导入/路径方法规范化IL一致；这些仍不代表实际迁移加载或重存已通过。首次安装后菜单读取出现可重试的Unity主线程超时，0prepare/accepted；原件独立核销后一次启动后重读与prepare成功，超时原因未证明。详见[同主档迁移准备证据](evidence/2026-10-12/native-primary-29131-preparation-r1218.md)。

## 当前只读主候选与实现修复

R1198–R1205共75次只读请求，新增21段完整原生点列、3次tokenless短端点预览和一次新有限目的整线预览；0游戏写入/accepted/Save/Load/Move/部署。原survey两次预览额度已结束，不重开。保留同一完整静态页与成功源13点/目标11点，批量淘汰西源6479（planned sorter重叠矿机1213）、宽西南225（旧带5424/5037/5385）及超出原生跨度的270走廊。建筑包围框筛选不替代原生带条件；对象1501现场是普通传送带2001。

当前“延长下穿东135”主候选6480→1501，6跨度、生成124点，旧带中心重合守卫清除，最远74.643米。完整只读请求返回INTERNAL_ERROR而没有Native assessment；不能据空值记原生调用0或称路线通过。对应日志和精确安装DLL离线复现证明：只读评估指纹误调用只接受施工模式的BeltPathRoutingPolicy.BindGeometry，抛出ArgumentException“Unknown belt routing mode”。最小修复已完成相关测试、准确源码绿CI、同批构建和冷安装，普通施工模式限制保留；受实际游戏版本变化影响，修复后的整线实测尚未执行。旧原生片段来自29104，不能冒充29131的新资格。

没有可写主/备用候选；124点不是精确材料账。6跨度+2分拣器+至少一次Save的9动作模型超过剩4槽，真实ID/层间接缝/精确预算仍未证明。下一步先处理版本恢复边界，再按新的真实session、Native哈希和现场状态批准有限修复验证；不扩原窗口、不重放旧预览。旧18个关键对象与互惠连接守恒证据、完整帧/2993几何对象/3010框/452地形点及五条货流库存均为缓存，三厂长期供电未刷新；当地三网旧点读只作背景。

## 流程效率的实际结论

完整静态快照与成功路段复用、同批多候选筛选、相关最小测试和阶段统一事实交付继续执行；本阶段400项定向测试仅在本地跑一次，绿CI后同提交cohort不重跑。隔离cohort构建37.991秒，菜单冷维护47.282秒；首个只读超时12.091秒，成功菜单/prepare6.503秒。此前75次只读内部执行19.915秒，R1205准备等待66.491秒/执行2.775秒。这些均未覆盖完整设计、入口准备、审计、Git/CI、失败与交接，不能计算端到端提速比例。版本适配与实机准备取得推进，生产/保存恢复验收门仍未新增通过项。

sourceConditionsPassed=false、wholeSupplyPassed=false、连续来源信用0，最终同SHA双候选包未交付、无正式发布。持续目标仍active。详见[批量下穿勘探与指纹异常证据](evidence/2026-10-12/iron-readonly-underpass-and-fingerprint-r1206.md)；此前安装维护和原A/B/C批次见[前一阶段证据](evidence/2026-10-12/iron-native-grid-batch-live-and-maintenance-r1197.md)。
