# Foundry进度归档与29131健康恢复：R1307–R1316

更新：2026-10-14（Asia/Singapore）。本记录汇总旧 Foundry 进度读取失败、授权保存与归档、单次启动、菜单复读及同一 owned 主档健康恢复。详细原始回执留在受保护证据库；下表仅提供精确索引。

## 已核实事实

- R1307 的 `get_blueprint_builds` 返回 `blueprint_progress_store_unavailable` 后停止，新增 accepted 为0。旧版本 `0.10.34.28529` 列出12项 build，覆盖的40个对象全部完成且0在途；进度哈希与当前状态不兼容，不能直接续用该计划。
- 用户授权后，R1308 普通保存位于 tick `116183243`。随后正常退出并按原字节及 SHA 归档旧文件；没有改写旧档或重放旧计划。R1309 独立核验完成的前缀与归档边界。
- R1310 仅启动一次桌面 broker。首个菜单读取超时后保留该启动；root :57 核实启动与关闭/归档前缀，但菜单仍未证明（`REQUEST_TIMEOUT`）。R1312 root :59 随后通过实际菜单与 Job 只读审计，`acceptedDelta=0`、`loads=0`。
- R1311 的唯一 Load 将同一 owned 主档恢复到 `0.10.35.29131`，并在 tick `116183275` 自动重存，revision `1`、durable Journal `102`；这不是额外的一次普通 Save。三条版本转换记录保留。root 独立审计确认新进度 `builds[]` 为空、两份新票据字节一致、旧票据有两条 tombstone，旧档归档 SHA 未变。
- R1313 恢复后完整工厂审计已独立通过：67页、6621 built、0 prebuild、50项详情；相对R1296封存基线的静态配置/连接差异与资源矿点差异均为0，玩家库存与持物、Journal及3条版本记录保全。采集123请求44.69秒，root核原件未重复实机采样。
- R1314 有一项 Blueprint 被 accepted 并以 `paused` 成功终态完成前两个对象：普通传送带6622（物品2001）与装配机6623（物品2303、配方78），仅各扣料1件；剩余3项未提交、未扣料。调用器随后因一架无人机为 working（pending为0）但要求全部 idle 而停止；这是调用器条件过严，不是建造失败或 unknown。阶段83请求、158.2秒。更早的坐标与动作哈希读取偏差也都在提交前停止、0 accepted，原失败保留；实际比较依据是 Native DTO 与已安装 Core 函数。
- R1316 新增1笔普通 Save accepted 并覆盖上述两对象前缀；root 对10条原始记录、库存与完整 Journal 独立核验通过，三架无人机 idle、库存与持物保持、三条版本记录一致。保存为 tick `116205141`、revision `6`、Journal `102`。当前固定20窗口 `17/20`、lifetime `605`，整案 `206 accepted / 47 Moves / 65 ordinary Saves`，无当前在途或 unknown。R1317 完整6623基线审计待 root 独立核验；下一步是保存重启后精确续建剩余3项并核验运行供料。

## 未证明的边界

R1300 的 `36348` 连续 tick 来源证据来自本次恢复之前，不能替代恢复后的复产验证。完整 Foundry 设备、电力、物流和精确成本计划，以及中途保存/重启后的剩余续建去重和失效计划重规划仍未闭环。最终同 SHA 手动包与 Thunderstore 包的安装和实机运行也没有通过证据；源码 CI 不替代这些验收。

## 受保护原件索引

| 阶段 | 原件引用 | SHA-256 | label |
| --- | --- | --- | --- |
| R1307 writer：旧进度构建尝试 | `eda5a8b8ed4e4937bd852262891c4b57:13` | `7483EBD593CF950EE65A465BD4E2A3FC602ACEB9B7E2DF48CDF79D11E6AC1796` | `r1307-five-object-foundry-build-save-writer` |
| R1309 root：保存前缀与归档边界 | `9c9fff04318249758eaed638128587b7:52` | `F33ECA185DD3F2DA22368B0A5A349EF5EF029C01B832E9083A322088B91FF111` | `root-r1309-normal-close-exact-archive-prefix-audit` |
| R1310 writer：单次启动 | `ead63a759a2c418b8d02d4ba3b57edd7:3` | `DDC6FF2AB66BC0018834C9366FBA8082FB4C6C5B639BDD920F0F2FC3B618CC6D` | `r1310-original-start-only-healthy-menu` |
| R1310 root：启动/归档前缀审计（菜单超时） | `9c9fff04318249758eaed638128587b7:57` | `74CDD45FD56770B678DE457D67318D40A24CEF46081CEAC2B1A55F9BA0C71668` | `root-r1310-original-single-start-menu-timeout-prefix-audit` |
| R1312 writer：延迟菜单只读快照 | `b6cfb1ef9f0e4fe6ab334fb1a4a3f635:2` | `8F561C0F5E212143630A8D3C1E53E5C74600A7FBE293F577AE8C87A260CCFEFF` | `r1312-original-dispatched-dsp-late-healthy-menu-snapshot` |
| R1312 root：实际菜单与 Job 只读审计 | `9c9fff04318249758eaed638128587b7:59` | `AC208C8D23858103B9E73D3E9A4D0260746654FAEABFD9AC589276C2A63330E1` | `root-r1312-original-late-menu-and-job-independent-audit` |
| R1311 writer：健康恢复摘要 | `a4dae04d33d341028901e1d7b8b2a09c:17` | `AA13231A3C6848DA3A06F531FD98E0CCC1A86239E5BB8959C4A2FDC10ABB9192` | `r1311-maintenance-resume-summary` |
| R1311 root：唯一恢复、Journal与票据审计 | `9c9fff04318249758eaed638128587b7:63` | `F4B32378E2CB2E0BF2C733490A7365F6F9C047C1F1F1F51E0C1A96B3DCD2485A` | `root-r1311-unique-healthy-primary-journal-ticket-independent-audit` |
| R1313 root：恢复后完整工厂、保管与静态差异审计 | `9c9fff04318249758eaed638128587b7:65` | `C8475A2DAD5074C5A59E2077657D3C78930180AEE389B5805994B7D753F922D5` | `root-r1313-restored-full6621-static-custody-independent-audit` |
| R1314 root：两对象原生前缀与材料核销 | `9c9fff04318249758eaed638128587b7:78` | `2ACDD6C14416CB11156A3249C4D2E7258E8288C437E1E20FADCA9FF9F4E994D2` | `root-r1314-original-one-accepted-two-objects-material-prefix-audit` |
| R1316 root：保存覆盖、两对象前缀与 Journal 核验 | `9c9fff04318249758eaed638128587b7:81` | `F8B65511EE31522528980158913C1BBDB859D352704A546871588C0A5CC545F7` | `root-r1316-one-save-covering-two-object-prefix-journal-audit` |
