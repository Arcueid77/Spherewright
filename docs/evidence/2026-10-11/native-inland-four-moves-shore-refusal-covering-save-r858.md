# R855/R858 内陆移动、岸线拒绝与覆盖保存

R855 完成四次正常移动，4 笔 accepted、41 次请求、约 `19.693 s`；前四个动作随后由 R858 的普通 Save 覆盖。第五条候选腿虽有 `nativePrepared=true` 与 `nativeCommitAllowed=true`，但 29 个岸线样本中 17 个检测到 shore risk，精确计划校验拒绝该腿，因此第五腿 accepted 为 0。R855 没有保存，阶段前正常 Save `107417528` 仍在。

R858 只执行普通 Save：1 笔 accepted、80 次请求、约 `32.551 s`，没有移动或材料转移。Save 为 tick `107476141`、revision `96`、durable Journal `102`。opening lifetime `470` 的固定 20 写窗口现为 `10/20`、lifetime `480`，仍开放但冻结；本阶段目的独立结束，全部当前动作均已保存覆盖，无在途或未知动作。早先 opening lifetime `454` 的窗口已在 `16/20` 封闭，剩余 4 槽退役、不转移。

Root 核验确认工厂 `6492 built / 0 prebuild`、65 页；本阶段静态差异、新增、移除及非互惠连接均为 0。P104 本地电网满供，未 fresh 核验其他星球电网。背包 Ti Ore `50`、Ti Ingot `0`、Warp `400`、Rod `5`、Stone `6` 未变；未取用 Ti Ore、Ti Ingot 或 Warp，短采样没有启动。

原件索引：R855 writer `803ed149e0b94d75ab0c1de05be77a00:59`（SHA-256 `03821BC68866D48A673C6A62E25BAF72D4C27A82419CFB91BFD34467FC96BC02`），R855 独立审计 `0299d7dfe49841f0b44eefb14c697743:41`（SHA-256 `D72F84CCACE559E6D1B0873C4F17A09B363AFC37380F1533058F842A06A48D5B`）；R858 writer `b32a3f5889ce4d16963a8076a2ee9aba:86`（SHA-256 `FE7026C2C4EEDAFE343F40DC69990D64F116F937BB7DAE16CE434ED4DF269A0C`），R858 独立审计 `0299d7dfe49841f0b44eefb14c697743:44`（SHA-256 `AF63B6F6A6D771B30C85B34D1D9C4E0BFA8BA902899AFD72D29055B38E39E280`）。

本阶段记录的流程计时为：R855 四次业务移动约 `19.693 s`，R858 Save/覆盖保存与全厂核验请求约 `32.551 s`；两阶段独立审计核心计时约 `0.274 s` 与 `14.844 s`（R858 不含原件加载）。批准至首次 prepare 分别约 `33.354 s` 与 `34.375 s`。相邻动作批处理、原件一次加载并复用、完整静态工厂缓存、单一 writer、固定 20 窗口与事实文档 CI 并行仍在使用；这些阶段计时不能证明端到端提速，当前 `endToEndSpeedupProven=false`。

来源条件和整体供需仍未通过，continuous credit 为 `0`。已观察的湿地路线族退役，不把计划校验拒绝写成 Native 建造拒绝。下一步需 fresh 资格化绕开湿东南湿带的结构不同北侧有限路线，再考虑取料、覆盖保存与短资格样本；本页不证明路线清障、钛供料或连续生产通过。
