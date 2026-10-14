# 29145 精确恢复候选：离线修复与验证

本阶段直接解决[菜单版本拒绝](owned-primary-29145-blocker.md)，[原生比较](../../research/game-api-version-0.10.35.29145.md)只支持候选实现。没有部署、Load、Save、commit游戏动作或消耗凭据；五对象成功前缀和原取料action保留。

## 变更与保留边界

- 兼容政策只新增29131→29145方向和已研究29145前缀tuple；原路径不变，default仍必须同版本，未知/跳链/降级拒绝。完整Journal最多4转换，候选第四转换必须满足原起点/每个历史链接、递增adoption tick、非递减durable sequence和有效时间；第五条拒绝。
- 活跃主档恢复增加pair-specific v6披露。原29104→29131的v5和原prompt不变；新pair不接受旧v5/旧prompt、prepare时确认或tick覆盖。原票据副本、exact primary、完整Journal、revision/digest、后续确认、durable consume-before-load和禁止中断重放仍保留。
- 进度库只允许精确31→45、同owned identity/current session、完整durable Journal和覆盖tick下保留全部completed历史。读取不改盘/不恢复写权限，正常durable更新才保留每份计划/回执并stamp45；partial/unknown、损坏或未经证明的版本继续拒绝，不清库、不重建成功对象。
- 新关联store回归发现旧Json.NET复制会追加默认Limitations列表。只为进度库增加替换已初始化集合的反序列化入口，共享协议Settings和原Deserialize行为不变；保留文件中已有内容，不做历史列表整理。

## 原件保全

root只读核票据hash未变且非quarantine、原Journal102条/三转换及唯一五对象completed进度。原Journal SHA-256 `BE2E2FC8EBF319800B790773B9BE6397D3C31E56EBEB20D38F18C5E96932FF42`；进度库 `272F16A91F2BAC347587F2CC175E3791C964D726A89EADC26C8CB749125A3587`，最后证据tick116214800，实体6622/6624/6623/6626/6625。保护引用 `eb097306510a4fe0b5243180958e2a87:1` / SHA-256 `5601D56E9F729E826D1B2EBAA6D6060BA53D45982FB5AA06C7F9D27FFC49964A`。原fixed20仍external1/lifetime609/frozen，0在途，预留保存；不重开窗口/换键重取。

## 离线结果

复用ignored SDK8.0.424。Core/Contracts/MCP及Core/MCP测试项目五个locked restore均exit0。第一轮Core2760项中2759通过，唯一失败为新测试对BeforeLoad抛错前后的替身计数时机写错；root删除该错误计数断言，保留票据/tombstone/lease安全断言。MCP258/258通过、0失败/跳过、exit0。

加入进度保全后第二轮Core2777项中2775通过、2项失败、0跳过；两项完整记录比较揭示默认限制集合追加问题，已按上述局部方式修复。最后仅重跑直接相关六类，271/271通过、0失败/跳过/NotExecuted、exit0：进度store17、版本兼容56、primary migration56、票据store43、实际coordinator+native替身60、save prefix39。16份相关源码/测试输入前后SHA一致。其余已通过用例未因该局部修复反复重跑；准确提交的完整CI仍是采用代码前的独立门。

| 原始离线结果 | ignored索引 / 文件 | SHA-256 |
| --- | --- | --- |
| 五项目locked restore及首轮Core/MCP | `.local/offline-regression-29131-29145-20261015-r01/test-results.json` | 5A6A2C4866BCBACE1BAEF7FC06BFBD100B63F7829C307BEA2CDADE1DE5749B6F |
| 首轮MCP258通过TRX | `.local/offline-regression-29131-29145-20261015-r01/trx/Mcp.Tests.trx` | 558C00978CD2A8C5FAC5EB8795DAD8EA070446D5083ED28DCCB56F50B66D870E |
| 最后271项日志 | `.local/offline-blueprint-progress-regression-29131-29145-20261015-r02/`，索引中的test log | 8ABC4446801E2389150D8D2A9ABC52C73A0E292F62854C7FF7737882DDF2973E |
| 最后271项TRX | 同索引中的TRX | AA7BC1CDCE9BCB4EBE281019E76493CCC17F7E924C1CAB27FA3D85D463704C1C |
| 最后计数/输入哈希摘要 | 同目录`result-summary.json` | 956DFA89999DFACB38E267C146058B223AC7904F58D7ADAF4608EF30A0F01868 |

关联测试使用GUID临时文件、session/native/ACL替身，不能证明Unity加载/实体连续性或真实Windows ACL。当前DLL的Plugin Release、同批候选部署、安装MCP完整工具/参数/resource/playbook检查和fresh native v6 prepare是后续独立步骤；实际跨版本Load仍须披露后用户明确确认。最后普通Save116215326/J102与退出前观察116289647/revision8必须分开；成功但未保存的4石矿动作只追原action，不能假定已持久化或换键重取。
