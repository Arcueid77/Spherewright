# 原owned主档续接的29145版本硬边界

更新：2026-10-15（Asia/Singapore）。本阶段仅启动一次原安装DSP并读取菜单/受保护default prepare；没有Load、Save、commit或候选部署。

## 事实和原生结果

旧root/writer已停止。当前受保护原主档票据仍为0.10.35.29131、minimum tick116215326、P104、Journal minimum102；本轮读取时未过期、无quarantineActionId，prepare前后文件SHA保持一致。R1337原取料已经成功核销，但尚未普通保存：窗口仍1/20、lifetime609、frozen、保存槽位保留、无当前在途/unknown；禁止换键重取。

现有桌面broker只dispatch一次，原incident27b4c8bcc8d14e878ba2a410d44390d1。root精确读取原intent，dispatched=true/outcomeUnknown=false/doNotReplay=true，SHA-256为BB5B3DCBEE7A76A4CD1DE599CB86807B983230E37C129C063D6DF95A046F3CB3。新DSP唯一进程29080；Native菜单读取成功，gameLoaded=false、gameVersion=0.10.35.29145、bridgeConnected=true、writeHealth=healthy、revision0。

一次default prepare原生拒绝，code为SESSION_NOT_OWNED，message为“The one-time owned-world resume ticket is consumed, invalid, expired, or belongs to another game version.”。result=null，没有计划、candidateTick、actionId或accepted。恢复建议中的“quarantined”是该失败分支的通用提示，不能据此把本档判为隔离或自行走隔离恢复。

## 必须保持的边界

[当前票据入口](../../../src/Spherewright.Plugin/RuntimeDescriptor/OwnedWorldResumeTicketStore.cs)的TryGetActiveTicket要求票据与实际游戏版本相等。本轮29131与29145不同，足以使default恢复不成立；原错误码同时覆盖其他原因，不能宣称其他分支已全部排除。

[版本兼容政策](../../../src/Spherewright.Bridge.Core/Safety/OwnedWorldVersionCompatibilityPolicy.cs)仅研究到29131，MaximumDurableTransitions=3；原档已保留三条转换。[active-primary迁移政策](../../../src/Spherewright.Bridge.Core/Safety/OwnedWorldPrimaryMigrationPolicy.cs)仅支持29104→29131。现有合法路径没有29131→29145，不允许改票据版本/expiry、清进度、改写原档、跳过Journal、另选存档或盲增第四转换绕过。

这是实际版本/受保护恢复边界，不是阶段结束、CI pending或普通调用超时。正常保存恢复的持续授权不涵盖新的需后续确认迁移；本轮停止游戏写入。DSP与Steam留在实际运行的菜单，writer已结束，未承诺后台执行。

## 已完成成果和下一安全入口

原R1321五对象及所有成功前缀保留，旧27对象候选退役；R1300历史来源窗不算新session。准确验收对应表、实际1/20窗口与原action续接在[当前快照](../../current-status.md)。源码/安装MCP、3028项测试、95项表面回归和当前5/5 DLL的Plugin完整Release构建在[本轮离线验证](delivery-build-validation.md)，均不冒充迁移/安装/最终双包验收。

下一安全入口是针对精确29131→29145进行原生兼容研究，形成可审核的有限兼容路径及Journal/进度保全方案；仅在合法protected prepare披露后取得后续明确迁移确认，才可执行唯一Load。届时fresh重核旧writer停止、当前原action、票据/原档/Journal和1/20窗口，不换键重放。合法恢复后先核未保存4石矿是否实际保全及全部五对象，再回到1209自动供料、1210输出和三级Foundry的唯一游戏主线。

候选尚未冻结：两份同SHA候选、完整三级链、最终恢复后≥36000-tick联合窗、出发清单及实机/安装交付仍未完成。

## 受保护原件

| 原件 | 引用 | SHA-256 |
| --- | --- | --- |
| 本轮root单启动/prepare-only声明 | a7259c455b7d4e5281f82a4a1c432add:1 | FF73E6626D267026C8EE3564176AF95A594989267834AC92019403D79941B771 |
| 唯一Native菜单响应 | 9d554a389ef44eb4922c6f37b85d8582:1 | DEA283CBFA438EF22C8A1AEFAC3BFBED4059CB7F2192902ED1FDC567C5444849 |
| 菜单最终摘要 | 6cdff5e096204b88968e28a422eb3b5c:1 | F67F81593C00C7B3CF2BF6782963AEB0AEE4C91E1A21D670C7AC52EFE892CF3D |
| 唯一default prepare拒绝响应 | dfdbe61513854c14bf2bd2ac3a172ae7:1 | 47B149D58A064FE5F390CA82B88EF84F9A9F34D1706BE690A47649911185AAD9 |
| default prepare阶段摘要 | dfdbe61513854c14bf2bd2ac3a172ae7:2 | F35C6DE5357BE3C5ED843B6331CB9B508A02BD4D1D7E6BF4E6116AB080C4EB72 |
| root原响应/版本/实际窗口独立核验 | f03badcd37ef48fe85202114d5d4f5bd:1 | 3C202474AF33A15CF6584E78142B74FF3047517A6B494D944F0210A6752A4326 |
