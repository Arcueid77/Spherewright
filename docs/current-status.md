# Spherewright 当前快照

更新：2026-10-13（Asia/Singapore）。仓库 `main` 的正式源码提交为 `e1ff2bf21f3a6beac65586e4ab4b93cb2eef8a4e`，用于连续采样任务初始墙钟预算修正；125项直接相关 `pwsh -File` 零Game断言已在本地通过，准确源码提交的独立 Windows Core CI `38337769901` 也已成功。当前 Native 安装cohort仍来自 `6528e1f7101a714826f7364cef2b0ad75c4b6e82`（游戏 `0.10.35.29131`），新源码没有热换或部署。代码CI与已安装运行时证据分开记录；最终同SHA双候选包仍未交付。

## 当前保存与窗口

R1253是最新独立核销边界：普通保存位于tick `113712512`，revision `14`，durable Journal `102`。固定20窗口当前 `14/20`、lifetime `566`；整案累计 `167 accepted`、`54`次普通保存、`297`条实际传送带、`18`段。当前动作均已保存，无未核销在途或未知结果。

该保存覆盖R1252施工的四段、86条新带（各段31/30/15/10）；R1253保存独立核验了84条原始保存回执与67页完整工厂快照。快照为 `6616 built / 0 prebuild`，整条124带路线仍为空且未供料；玩家背包有53条普通传送带（`2001`）。旧unknown继续保留历史状态，不重分类为成功或重放。详见[抬升铁路线与保存前缀](evidence/2026-10-13/raised-iron-route-saved-r1253.md)。

R1255已结束：记录为12次read、2次prepare；两端分拣器各取得一次Native prepare结果。两项均为 `prepared=true`、`commitAllowedNow=true`、无Native blocker，但阶段未commit，`0 accepted`。随后本地库存cut因把无物料缓冲的电力节点列入 `materialInventoryObjectIds` 而不可用，失败原因为 `material_inventory_buffers_unavailable_or_unsupported`；root已核对全部12对原件与完整stdout/stderr，原失败与exit 1均保留，未重做prepare。R1256正在补做135个物料对象、235个几何对象及2个既有电力代理的只读核验，尚无施工准入。来源门与整案供给门仍未通过：`sourceConditionsPassed=false`、`wholeSupplyPassed=false`、`continuousCreditTicks=0`。最终同SHA双候选包未交付。

## 来源与后续范围

既有accepted计数、窗口与来源验收门均不因本次保存而清零或降低。R1252/R1253形成了已保存的建造前缀，但未证明来源准入或持续产出；R1255的prepare结果没有转为commit或accepted。后续状态以新的独立核验为准。
