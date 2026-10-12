# 只读整线指纹修复、同批冷安装及实际Native版本变化

R1207–R1215阶段只完成最小指纹修复、相关验证、同提交构建、维护保存、冷安装和版本变化诊断。修复后的整线预览、加载、施工、持续来源和最终双候选包均未通过；持续目标保留，版本变化处冻结新写。

## 修复与安装

干净源码128ec5fee8ec536d8f84971e110a1405983b7a7c的准确Windows CI 38230443288 completed/success。只读预览使用独立指纹域并保留完整几何绑定；普通施工入口继续拒绝preview mode。相关四类Core测试247/247、锁定restore、完整Plugin Release编译及MCP自包含publish成功，同源Native引用和64 tools/1 resource/playbook核验通过。

隔离cohort为228文件（Plugin4/MCP224），所有程序集版本绑定该源码。R1209唯一普通Save原ack、terminal、commit-intent、全部玩家库存/持物/增产点、J102和已保存前缀独立通过。最新普通保存tick112747810/R3/J102，source version0.10.35.29104；原20窗16/20/lifetime551，累计152 accepted/46 Move/49普通Save，无在途或unknown。

R1210原3个preclose响应、安装intent、installed metadata和最后lifecycle共6份原件独立核验。正常关闭旧DSP14608，Steam36408保留，一次事务安装228文件及一次桌面broker dispatch成功，耗时59299.0592ms；没有Load、Host退出测试或accepted。未写或替换游戏本体程序集。

## 当前版本变化

R1211菜单入口在Game读取与MCP RPC前失败：current Native assembly differs。原stopped记录证明gameReads0/rpcCalls0/accepted0，不重跑该入口。R1212同版本恢复caller仅完成零Game实际File smoke，尚未批准或执行，现已因版本变化退役。

R1214新诊断目的最多1次状态读取、1次1000行日志尾读、60秒，实际3476.5553ms，返回27行及一次原状态。新DSP42364在未加载菜单，actual gameVersion0.10.35.29131/writeHealth healthy；0写/Load/Save/部署/启动。原29104主档凭据与冷安装前hash相同、未消费，源主档仍由112747810/R3/J102保存证据覆盖。未声称完成29131加载或实体保持。

Native hash由6C122E5443E6843979B4064050DFCB5E0D75577A0B64F6AE4111290238B33C12变为74AC6162CEF8394A6021C8475132CAE4DF4F840871A99E63CC7D2C8297661BC0。当前文件mtime为2026-10-12T10:03:34.0858899Z，新DSP启动为10:03:40.9500517Z；Steam manifest buildid25878145并报告本次321936下载字节。与启动期间Steam更新相符只是推断，更新操作者未证明。

## 只读接口分析与迁移边界

R1215只读Cecil对比Plugin的944项Native成员引用。934项直接签名匹配；10项ProtoSet泛型引用在简化匹配器中对旧/新均未展开，无匹配计数变化，不能声明944项全解析通过。选定11个GameSave、GameData、GameDesc、GameConfig的保存/加载/路径/导入方法规范化IL相同；没有运行原生方法，也没有完成全保存格式或实机兼容验证。

实际安装Core的IsSupportedMigration(29104,29131)=false、IsResearchedTarget(29131)=false、MaximumDurableTransitions=2。原Journal已有2条durable transition，新的迁移会是第3条，不能靠放宽上限代替研究和授权。凭据仍有效至2026-10-13T09:53:47.4425296Z；现有expired-primary保护路径只接受已经到期的凭据，不能修改expiry或提前借用。

后续可供评审的范围只有同一ticket-bound owned primary的29104→29131方向：先核当前Native、必要接口与原生格式，设计保留完整旧Journal和身份的受保护恢复支持，再由fresh prepare披露版本/候选tick/Journal/消耗与失败边界，获得后续明确确认后才Load。未知版本对、降级、其他存档、修改凭据有效期或游戏程序集、绕过Journal上限和无确认加载均不获批准。本阶段没有实施迁移代码或加载。

## 流程优化与实际速度

R1198–R1205八个只读目的共75请求，内部执行累计19915.3001ms，成功独立核验累计13651.4434ms。R1205 root批准至首次业务prepare66490.8338ms、执行2775.2731ms。R1208准确源码cohort43.04秒，R1210冷安装59.299秒，R1209普通保存4485.6ms。定义各不相同，不拼成端到端提速比例。

已执行完整快照/成功源13点和目标11点及中段缓存复用、批量淘汰候选、直接相关测试、一次阶段事实交付。仍耗时的是设计、caller与原证据准备、审计和交接，另有已定位指纹实现异常及这次真实版本变化。本轮未增加实机验收通过项；sourceConditionsPassed=false、wholeSupplyPassed=false、continuousCreditTicks=0。

## 受保护原件索引

| 内容 | ref | SHA-256 | label |
|---|---|---|---|
| 准确干净源码/CI | b42f4943395e43e88e839e052a234f3c:120 | 461F1AA0DA1D124C808DAA734C1FC560E1B09A473AF0999025F06C3C8C346512 | root-r1208-readonly-fingerprint-clean-source-ci-independent-audit |
| 同提交cohort核验 | b42f4943395e43e88e839e052a234f3c:123 | DCF8AE7550151DB4380F53EC78B003E37588E08D590AF3E824F5EAA77BE57CE5 | root-r1208-native-segment-cohort-independent-audit |
| 普通Save独立核验 | b42f4943395e43e88e839e052a234f3c:125 | 962420BE17DAC56436889ADFC1E2CDCBF6478D069D23BBD283C9FDEA2E1E9CBC | root-r1209-native-segment-maintenance-save-independent-audit |
| 冷安装独立核验 | b42f4943395e43e88e839e052a234f3c:129 | DE8B6413CD86428DFF82EFB7294BEFE564C542C9020DEC81F48EF4720AC43502 | root-r1210-readonly-fingerprint-cold-lifecycle-independent-audit |
| 菜单零Game拒绝核验 | b42f4943395e43e88e839e052a234f3c:133 | 51212A3891BD133BB2A86A519D5B5CF6CC9B1DEDECD616504B65C4812D3393B5 | root-r1211-native-hash-rejection-independent-audit |
| 当前版本独立核验 | b42f4943395e43e88e839e052a234f3c:135 | 676C2AC677A200C6C5C6F992FACAB09C5E6B0CA0DA04A0AFD623A6DAF6514277 | root-r1214-current-native-version-drift-independent-audit |
| Native离线接口与实际策略 | b42f4943395e43e88e839e052a234f3c:136 | B08550899EDBD72EDA403EB2EFC939672F7899671B886675DA0B2D8572A6F4A3 | root-r1215-current-native-offline-contract-and-policy-analysis |

前阶段布局与候选完整矩阵见[批量下穿勘探与指纹异常](iron-readonly-underpass-and-fingerprint-r1206.md)。原始日志、DLL、实际存档身份、token及本地分析脚本均留在受保护本地，不进Git。
