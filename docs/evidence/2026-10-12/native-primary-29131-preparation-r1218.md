# 29104→29131同主档迁移准备

用户明确选择“以29131继续，准备同主档迁移方案”，实际Load仍须后续具体计划确认。该准备授权封存在b42f4943395e43e88e839e052a234f3c:140；来源为当前对话的明确答复，不推断旧“继续”已授权版本迁移。

此前版本变化与保存事实见[R1214阶段证据](readonly-fingerprint-maintenance-and-native-drift-r1214.md)：源普通保存112747810/R3/J102，20窗16/20/lifetime551，无在途；游戏29131处于未加载菜单，29104原主档凭据未消费。本文仅记迁移能力准备，不宣布加载、原生线路、持续供给或保存恢复通过。

## 当前DLL离线依据

本机29131 Assembly-CSharp SHA74AC6162CEF8394A6021C8475132CAE4DF4F840871A99E63CC7D2C8297661BC0。五个编译DLL复制到隔离本地目录且逐字节核hash，原29104编译引用保留。所有944项Plugin Native成员引用匹配：934直接匹配与10项展开的ProtoSet泛型匹配。11个选定GameSave、GameData、GameDesc、GameConfig保存/路径/导入方法的规范化IL与旧DLL一致；原生v7/v13/patch23/GameDesc10布局保持同一研究边界。尚无29131实际加载或正常重存档证明，接口/IL相同不等于完整实机兼容。

独立索引b42f4943395e43e88e839e052a234f3c:142，SHA A2ED5190F675BAEF9B75FF0747FF1FE76B2ADC352AF560C028C81F40683C2DD7，label root-r1218-current-native-reference-and-complete-plugin-abi-independent-audit；原944引用/11方法内容在:136，SHA B08550899EDBD72EDA403EB2EFC939672F7899671B886675DA0B2D8572A6F4A3。DLL及原件不进Git。

## 最小适配范围

现有owned-resume工具新增显式mode `reauthorize_version_changed_primary`，evidence5。仅29104→29131方向、未到期的健康原凭据、同一ticket-bound primary及原Journal可准备；不接受confirmation或caller tick覆盖。准备只披露，不消费、不Load，MCP须核完整mode/证据版本/身份/保存tick/来源目标版本/prompt/digest回声才暴露token。

实际commit仍要求prepared disclosure之后的明确回复与精确digest，fresh重验凭据双副本、菜单revision、完整主档/Journal并持只读lease，先durable consumption再调用原生loader。默认恢复继续严格要求同版本；expired-primary与AutoSave0保留各自时间、版本和指纹域，不能互换凭据。只允许准确的新第三29104→29131迁移接在已有两条之后，保留原origin/entries/transitions；跳级、降级、重复和第四条拒绝。凭据日期、游戏程序集、历史Journal、存档身份均不编辑。

相关策略、实际票据仓储与Coordinator的合成文件/原生替身、MCP回声/拒绝测试已在隔离源码中通过：Core366/366、MCP34/34，共400项，无失败/跳过。基于29131五项引用的完整Plugin Release（net472）编译通过，0警告/0错误。16个批准文件的隔离覆盖与当前源码hash一致；原13份stdout/stderr/TRX/产物独立核hash并读取两份TRX。独立索引b42f4943395e43e88e839e052a234f3c:144，SHA 3243E7D158B18EA7002FCD5C502F2F4E3390A3C4CB81C49783ADB93E58886E8B，label root-r1218-active-primary-migration-related-validation-independent-audit。

五个SDK步骤耗时合计55.372秒；准备步骤原始独立耗时/日志未捕获，整体耗时不宣称，原汇总错误已单独更正且未重跑验证。上述预提交产物不可部署。后续按准确源码绿CI→从该提交重建同批Plugin/MCP→菜单冷维护→一次限定prepare-only披露推进；尚未取得fresh实机prepare，不在这些步骤里加载主档。
