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

五个SDK步骤耗时合计55.372秒；准备步骤原始独立耗时/日志未捕获，整体耗时不宣称，原汇总错误已单独更正且未重跑验证。上述预提交产物不可部署。源码核验时尚未取得fresh实机prepare；随后准确源码绿CI、同批重建/菜单冷维护与prepare-only披露的实际结果见下一节，主档未加载。

## 同提交安装与实机披露结果（R1219–R1220）

源码提交c488fb72a64f31db4fed722dccbe2a833ec63f0b，父979820bbb6f5ff275ffdbca6b81fbe20f2b9fa3f，tree cd9d2b7fb81de18d2339e7b18589df205c43465e；仅16个批准路径。普通push后本地main、origin/main、远端main一致且工作树干净，准确源码[Windows CI38242390651](https://github.com/AvaloNero/Spherewright/actions/runs/38242390651)及12个实际步骤成功。源交付14对原输出与16个reviewed Git内容独立核验；各Git原exitCode未单独封存，按原输出、准确远端/CI及下一步提交标识archive共同验证。

基于该提交和29131的五项DLL重新locked restore、完整Release build、自包含MCP publish，三项全部成功，完整cohort228文件（4Plugin、224MCP），六个程序集均0.4.0+同一完整SHA，隔离metadata64工具/1资源、playbook匹配、纯stdout。Git archive包含准确提交标识，16个reviewed source与archive/提取源码相比仅允许CRLF/LF及UTF8 BOM表现差异。cohort未重复测试，明确tests=0/sourceRelatedTests=400，绑定前述独立源验证与准确CI。首次cohort入口缺证据reader导入，在0SDK/0Game时退出；补齐导入并实际pwsh-File smoke后有限修正成功。四个冷维护/准备入口pwsh与5.1 inert smoke通过，不冒充5.1实机执行。

R1219仅一次正常关闭未加载菜单DSP42364、保留Steam36408、事务安装228文件、桌面broker dispatch一次。实际耗时47.282秒，1次菜单read；0Save/Load/accepted。原29104主档凭据原hash保留。随后首个get_session_state回执为REQUEST_TIMEOUT、retryable=true，0prepare/写入；该目的立即停止并独立核原件，耗时12.091秒。Root允许一次启动后只读guard续接，复用同一prepare入口，不重复冷安装/启动、不增write额度。R1220成功菜单/metadata/prepare/末菜单共4次调用、6.503秒。超时根因未证明；一次本地80行上限log读取只取得1行，不能据此证明没有异常。

实机Native/MCP evidence5披露：`reauthorize_version_changed_primary`，源0.10.35.29104、目标0.10.35.29131，candidate/minimum tick112747810，P104，embedded identity已验证。`userConfirmationRequired=true`、`commitAllowedNow=false`，唯一blocker USER_CONFIRMATION_REQUIRED。完整主档/原Journal及有效凭据的资格由真实prepare验证；未消费、未Load。准备前后同一健康未加载菜单revision0、票据hash不变，DSP34540与Steam不属当前清理Job。累计本阶段6次Game调用（含先前1失败read及冷维护1read），其中仅1prepare；0accepted/Load/Save/Move/commit。固定20窗仍16/20、lifetime551/opening535，保留来源Save112747810/R3/J102及已有两条迁移。

后续具体执行只允许这个同一主档/版本对/候选：用户在该披露之后明确确认→fresh prepare并核同candidate/digest与所有guards→唯一commit，先durable消费原凭据再原生加载→同action终态、owned身份/P104、原Journal102连续性及准确第三条迁移→正常保存签发29131新凭据并fresh审计。已披露的短期token不跨进程保留或复用，确认后仍须重prepare；candidate或digest变化须重新核范围，不能自动换档。消费后失败/中断只调查同action与原意图，不重放、不改expiry、不改存档/历史Journal、不选LastExit/AutoSave。当前未取得实际Load授权，实际迁移/重存/实体持久性未通过。

独立阶段索引（原件均为受保护记录，DLL、存档、真实名、token和原日志不进Git）：

| 内容 | ref | SHA256 | label |
| --- | --- | --- | --- |
| 源码/准确CI | b42f4943395e43e88e839e052a234f3c:146 | E8DCE7319F48B88F989BC34DA6D1F4C93CAD7E76550FE34C84ED75ACC42E9B19 | root-r1219-active-primary-migration-clean-source-ci-independent-audit |
| 当前Native同提交cohort | b42f4943395e43e88e839e052a234f3c:149 | 6FB428624B3A58D687F4D0B2C51F8C590A67B9E4F47B72EDB4A41AE59F5DCC90 | root-r1219-current-native-exact-migration-cohort-independent-audit |
| 冷维护与首次只读超时 | b42f4943395e43e88e839e052a234f3c:152 | 66AEC889EC60066A26B8B42AF43EBD6B54DB749A9031D239CCF88D0C0CC641E4 | root-r1219-menu-cold-success-and-first-read-timeout-independent-audit |
| 实机v5披露及完整原件 | b42f4943395e43e88e839e052a234f3c:154 | A3654D098FF8777DB196EF6C67BD881A18AC3D6D4D36F3FA7D2A89D5918501F8 | root-r1219-same-primary-migration-menu-cold-and-prepare-independent-audit |

上述结果不增加铁路线原生资格、连续供给/配平信用或0.4最终验收通过项。执行cohort为c488fb7，后续仅事实交付不触发重新冷部署。

## 后续确认、实际迁移与完整工厂审计（R1222–R1223）

本节更新实际结果；上文“尚未授权/加载”保留准备阶段截面。用户在实机v5披露之后明确回复“确认同主档迁移并继续”。Root封存该答复并授权唯一有限Load，执行前fresh prepare的candidate tick112747810、P104、29104→29131版本对及披露digest精确保持，旧短期token未复用。11次请求、11.9秒，唯一accepted Load终态completed；无Move/施工/显式Save commit、未知结果或重放。

恢复仍是同一主档/P104，新会话与已安装进程精确匹配，具体运行标识仅留受保护原件。完整Journal102条、原journalId和已有两条版本记录保持；准确第三条29104→29131在durableThrough102持久后，恢复机制正常重存到112747841/R1并签发目标29131新凭据。Root逐核11个原响应、唯一持久intent/ACK/同action终态、正常保存状态、完整Journal及新凭据文件hash，背包/inc/持物、玩家位置、燃料设置保持，owned/和平/非沙盒/1×/healthy writes通过。凭据日期和存档/历史Journal未编辑，未选择其他恢复候选。外部审计数仅增加实际Load1：17/20、lifetime552，原窗口opening535，仍冻结开放；整案153 accepted/46 Move/49显式普通Save/27手工材料动作。

R1223首个caller因dot-source覆盖Mode只执行了inert smoke、0Game/Bridge，writer停止后root保存入口参数并做真实pwsh-File smoke；新caller仅采74次只读、65页单一完整快照，真实调用方进程墙钟26.55秒。Root按精确索引核74原件并复用旧完整帧作本地比较：6492实体增删/静态配置/配方/站配置/互惠连接全0差异，独立prebuild0，库存/inc/持物、隔离仓286配置与完整货物、原Journal102和3条版本记录保持；全3处loaded工厂的网络覆盖完整且即时供电满足。148个动态对象变化按后台生产处理，不声明连续来源、吞吐或供电长窗通过。

当前几何指纹064E4A5C5488DAA7B14D9F107F6A2522207F925C70078937B14BA60BC17DDE77；源码Fingerprint显式写入sessionId，因此旧session A78指纹退役。源6480和目标1501的原Native geometry/colliders逐字段相同，完整静态帧可继续本地复用。源相关测试/同cohort不重跑，事实文档不触发冷部署。

| 内容 | ref | SHA256 | label |
| --- | --- | --- | --- |
| 实机披露后的实际执行确认 | b42f4943395e43e88e839e052a234f3c:157 | E9FA8EEEA530067D7B630A7D059A626F1C5683F1A35FAD78489087EEC7E771DA | root-user-confirmed-exact-same-primary-migration-after-v5-disclosure |
| 实际迁移及保存独立核验 | b42f4943395e43e88e839e052a234f3c:159 | 89EF2B3B0CAD43EDDB16242B9A65E35CF89B07CADD58751E65DDA146A4F7DD09 | root-r1222-exact-confirmed-primary-migration-independent-audit |
| 完整迁移后工厂核验 | b42f4943395e43e88e839e052a234f3c:163 | 0B1A692F4D858455D5DC5A0AFF83D8703C96384ECDC70B74BDDA2A128EEB4CD4 | root-r1223-complete-migrated-factory-saved-prefix-independent-audit |

本次通过的是准确同主档迁移、正常重存、Journal与完整实体持久性；全0.4最终门、持续供给、准备清单及最终同提交双候选包仍未完成。
