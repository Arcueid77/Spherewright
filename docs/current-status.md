# Spherewright 当前快照

更新：2026-10-13（Asia/Singapore）。当前安装源码为6528e1f7101a714826f7364cef2b0ad75c4b6e82（版本0.4.0+该提交SHA，游戏0.10.35.29131）。源码CI38278446205成功，279项相关验证已绑定；同源隔离构件和安装读回已独立核验。它们不代表最终双候选包交付或持续来源通过。

## 当前保存与窗口

R1250是当前最新独立核销边界：普通Save位于tick 113665126，完整Journal为102；固定20窗口opening lifetime 552，当前9/20、lifetime 561。整案累计162 accepted、53次普通Save、211条实际带、14段。保存覆盖全部已核动作，无未核销在途；38条既有路线仍为空且未供料，完整工厂为6530 built、0 prebuild；玩家背包有139条普通传送带（2001）。详见[R1250保存前缀与R1251原动作完成核验](evidence/2026-10-13/fixed-quarantine-recovery-and-saved-prefix-r1251.md)。

R1248的原带路动作只调用一次，caller在600秒后超时。R1251只读核验确认同一原action最终成功完成25条带，经过9458个游戏tick；未重放。可界定完成发生在后续只读核验之前，但没有精确的完成墙钟时间。R1250随后普通保存覆盖该成功前缀。旧unknown仍保留历史状态，不重分类为成功。

R1252正在执行余下四段、86条空带的有限阶段，最多4 accepted、3700 requests、7200秒；当前尚无终态或独立验收，不写作通过，也不向源端放料。sourceConditionsPassed=false、wholeSupplyPassed=false、continuousCreditTicks=0；最终同SHA双候选包也尚未交付。

## 来源与后续范围

sourceConditionsPassed=false、wholeSupplyPassed=false、continuousCreditTicks=0，最终同SHA双候选包尚未交付。R1252当前阶段的终态及独立核验仍待原回执；在此之前不把剩余空带写作已建成或已供料。既有accepted、窗口与来源验收门均不因本次恢复而清零或降低。
