# 固定隔离恢复与保存前缀（R1249）

日期：2026-10-13。本文记录同一已安装源码上的隔离恢复、普通保存和独立核验。安装、源码 CI、游戏状态与持续来源门分别核算。

## 源码、冷构件与安装边界

R1241 源提交为 6528e1f7101a714826f7364cef2b0ad75c4b6e82，准确 Windows Core CI 38278446205 成功；源码相关的279项验证已另行核实。R1242 同源隔离构件含228个文件（Plugin 4、MCP 224）、64个工具和1个资源，六个产品程序集的版本均为0.4.0+该提交SHA，playbook匹配。该cohort没有重跑279项测试，也不是最终发布包。

R1244 安装后本地metadata guard曾停止；独立核验随后逐项确认228个安装文件和六个程序集版本与6528e1f一致。第一次只读菜单读取超时后，同一进程的后续只读核对成功。上述本地停止和超时不代表Native拒绝或安装内容损坏。

## 恢复、保存与当前边界

R1243–R1249案件累计在同一受保护主档上完成Close、Install、Dispatch、Load和普通Save各一次；R1249本轮只做原Load完成后的读回核对与普通Save。新案accepted增加2，分别对应唯一完成的Load和普通Save；原Load terminal已完成，不重放。R1246的消费核对过早、R1247对可空字段的假设是调用器本地检查问题，不改写为Native失败。

普通保存完成于tick 113467495，revision 2，durable Journal 102。独立核验确认79对原始请求/响应、66页完整工厂数据、6505个实体和0个预建；13条已有空带、库存count/inc/held、Journal 102及三次版本转换均保全。当前session健康、无在途或未决unknown，保存覆盖当前已核销效果。旧unknown仍作为历史记录保留，没有被改判成功或重放。

在R1249最新独立核销边界，固定20窗口从opening lifetime 552起算，为7/20、lifetime 559。整案累计160 accepted、46次Move、52次普通Save、27次手工材料动作；实际186条带、13段。最近保存后的读取tick为113467501。sourceConditionsPassed=false、wholeSupplyPassed=false、continuousCreditTicks=0；最终同SHA双候选包仍未交付。R1248已按批准入口启动，仍以五段、111条空带为上限；当前尚无终态或独立验收，不向源端放料，新增差量待原回执核销。

## 流程计时口径

R1249 writer记录79条请求、wallMs 46,655.5004；独立root核验另记originalCaseRequests 83、originalCaseExecutionWallMs 49,495.2568，以及independentAuditMs 27,065.8302。它们属于不同记录边界，不相加作为全阶段耗时，也不能据此宣称整体提速。

## 受保护证据索引

| 内容 | 受保护引用 | SHA-256 |
|---|---|---|
| R1241源码提交、远端与准确CI独立核验 | b42f4943395e43e88e839e052a234f3c:223 | 783E6E815F63A4640C32E5353E3C787D472D7DA3C60D974958206377647AEB06 |
| R1242同源冷构件原记录 | faea6c280ea3483eb25af14b924e9a0d:4 | 275EE0FDFA0DACA90D9B6D9C7447CC41B21389758DB84E1891A41AB666125EE6 |
| R1242同源构件独立审计 | b42f4943395e43e88e839e052a234f3c:225 | 04598A50F35CC13527966081500F287FC07A7CA34E8FB08E674C6B905A41CBA9 |
| R1244安装文件与候选状态独立核验 | b42f4943395e43e88e839e052a234f3c:229 | 26B01F78FD42C401DD64EC1BAE469D4A37A3A55E6F74505D73AEE8B7963251EB |
| 首次菜单读取超时后同进程只读核验 | b42f4943395e43e88e839e052a234f3c:231 | 5913E87912D36350CFF5232A3DCC69143B6BE277B1101F552A514AE4DAEBABAF |
| R1249原始恢复与保存writer | d8544b4810bd49dfa1f3c21e7f13f447:161 | 7186E40B5CA99194D60B817961E40B3E4C1047D34AF43F437449B6B348551CCE |
| R1249独立恢复、完整工厂与Journal核验 | b42f4943395e43e88e839e052a234f3c:235 | A0F2FC39555B4FF0B949BE94F65A529AEA996BD5C60695534B059DF6FD9A1BF4 |
| R1249未用Save额度批准 | b42f4943395e43e88e839e052a234f3c:234 | 0036249B1F271E61438AB8AC2457708E54DC1AE2C5A6B468088AE68DB8D24BA2 |
