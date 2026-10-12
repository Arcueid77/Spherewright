# Spherewright 当前快照

更新：2026-10-13（Asia/Singapore）。当前安装源码为6528e1f7101a714826f7364cef2b0ad75c4b6e82（版本0.4.0+该提交SHA，游戏0.10.35.29131）。源码CI38278446205成功，279项相关验证已绑定；同源隔离构件和安装读回已独立核验。它们不代表最终双候选包交付或持续来源通过。

## 当前保存与窗口

R1243–R1249案件累计完成Close、Install、Dispatch、Load及普通Save各一次；R1249本轮只包含原Load完成后的读回核对与普通Save。新案accepted增加2，即原Load和普通Save。最近普通保存为tick 113467495、revision 2、durable Journal 102；保存后状态读取tick 113467501。截至R1249最新独立核销边界，会话健康、无在途或未决unknown，已核销效果均由保存覆盖。

截至R1249最新独立核销边界，固定20窗口opening lifetime 552，为7/20、lifetime 559。整案累计160 accepted、46次Move、52次普通Save、27次手工材料动作；实际建成186条带、13段。历史unknown仍保留原状，不重分类为成功，也不重放。R1248已按批准入口启动；该阶段仍限五段、111条空带，尚无终态或独立验收，不向源端放料；新增差量待原回执核销。

R1249独立核对79对原始请求/响应、66页完整工厂数据、6505个实体、0个预建；13条已有空带、count/inc/held库存、Journal 102和三次版本转换均保全。源码安装和菜单核验过程中的本地metadata/字段检查停止不代表Native拒绝，详见[R1249隔离恢复与保存前缀](evidence/2026-10-13/fixed-quarantine-recovery-and-saved-prefix-r1249.md)。

## 来源与后续范围

sourceConditionsPassed=false、wholeSupplyPassed=false、continuousCreditTicks=0，最终同SHA双候选包尚未交付。R1248的原回执核销完成后，再按当前已批准五段、111条空带上限确认新边界。既有accepted、窗口与来源验收门均不因本次恢复而清零或降低。
