# 0.4.0 同源码双候选构建与包检查

2026-10-15。两份真实候选已经生成并冻结，构建和包接口检查通过；完整0.4验收仍未完成。本文只证明下列范围，不把菜单部署、metadata探测或验证专用fixture称作实际安装/游戏恢复通过。原条款与剩余工作仍见 [current-status](../../current-status.md) 和 [交付要求](../../v0.4-delivery.md)。

## 源码与产物

候选源码为 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`。打包前维护核本地HEAD、origin/main和实时远端main相同且clean；[Windows Core CI 38627772732](https://github.com/AvaloNero/Spherewright/actions/runs/38627772732) 的headSha完全匹配、completed/success。保留已有ignored成果，将原有干净detached构建worktree正常切至该祖先链上的提交；没有reset、clean、force push、AllowDirty、覆盖其他执行者文件或游戏操作。

工件位于仓库ignored `artifacts/v0.4.0-review-2a7a98d/`。后续只记录事实的提交引用该源码SHA，不改变ZIP或为自引用反复打包；二进制或随包内容真正变化才重新生成候选并补相关验证。

| 候选 | 字节数 | ZIP SHA-256 |
| --- | ---: | --- |
| `Spherewright-0.4.0-win-x64.zip` | 36759398 | `665FA1B30A86A4D49AC7E69CE174E08348DCA07D159A24AF766ABED89AFD79CB` |
| `Spherewright-0.4.0-thunderstore.zip` | 32492607 | `81D9D6ED0BAEC843AB22ADE38158102212A5487A43EFEE22769F500B7CF85D91` |

各自同名 `.zip.sha256` 内容均与实际ZIP及文件名匹配。sidecar文件本身SHA依次为 `AEBD0EDF14F47BE030CEF54F7E89C94D11BBA92A7615E5CADFFB8622C8D5D96A`、`1484EB637B73EB59D34F70ACE72370C2A215C4247D3BB7591E42A0CF1FE8042B`。这两个ZIP是当前可审核工件，尚未具备正式发布资格。

## 构建与准确SHA测试

现有 `scripts/package-release.ps1 -Version 0.4.0` 入口单次完成，使用便携SDK `8.0.424`，约77.7秒、exit0。全solution locked restore、当前原生引用的完整Release构建、win-x64 self-contained MCP发布和两种包检查均完成，Release为0警告/0错误；构建worktree随后仍clean。本轮没有另跑重复全量测试，直接读取上述准确候选SHA的CI原日志：

| 程序集 | 通过 / 总数 | 失败 | 跳过 |
| --- | ---: | ---: | ---: |
| Contracts | 73 / 73 | 0 | 0 |
| Bridge.Core | 2777 / 2777 | 0 | 0 |
| MCP | 258 / 258 | 0 | 0 |
| 合计 | 3108 / 3108 | 0 | 0 |

构建使用5/5当前安装原生引用，逐份对安装文件的bytes/SHA，manifest SHA为 `CFA05CF20919FC33EF37CC38FCAAFA4FD90BB372C092A376113CEDC055B50712`。实际Assembly-CSharp SHA为 `B84B60DF5A24947A014801F23B9B85D1E77853F42A331918E2DC85B153E50527`，对应本轮fresh Native菜单 `0.10.35.29145`；旧refs和目录旧29131标签保留，不能用标签替代版本证据。其他四份引用的完整hash见 [当前DLL报告](delivery-build-validation.md#当前安装-dll-引用构建)。

root独立打开两份原ZIP，复核同一sourceCommit、`sourceDirty=false`、版本0.4.0及目标DSP/BepInEx声明；逐文件核大小/SHA，拒绝重复或不安全路径、未列文件及PDB。手动包有238个payload文件加1个自描述manifest，共239个文件（含目录项的ZIP条目数241）；Thunderstore有11个payload文件加1个自描述manifest，共12个文件。两布局中的4个Plugin依赖文件逐字节相同；两个AGENT-PLAYBOOK.md相同，SHA为 `2E354DB900B7822F47092B023EA971BEEA688DEF97798D666D8C12C3FC16AB70`。共9个抽取的产品DLL/EXE独立核FileVersion `0.4.0.0` 和ProductVersion `0.4.0+2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`。

## 两种MCP程序的接口检查

手动包的self-contained目录程序由正式打包入口内的 `test-release-package.ps1` 实际启动并通过完整0.4工具/必需参数/annotations断言、1个resource和包内/内嵌playbook一致性；退出码0。判定依据是原入口的强制断言和exit0，原wrapper未单独输出完整probe JSON，不能把这一限制写成有原JSON。

正式Thunderstore脚本原本只做静态包检查；随后独立对**该ZIP中的实际单文件EXE**补足metadata启动检查。`plugins/Spherewright.Mcp.exe` 为36624930字节，SHA `3EF7636A31366A953782348C76351C3A08A382BA208F07F5937C2B780E3FB80A`。仅将原EXE、4个原Plugin依赖及原guide共6个成员复制到验证专用手动布局fixture，以复用现有正式harness；root逐份比对原ZIP与fixture，均完全相同。fixture SHA `72BFC141342C1959A83D36D1B155AD9558B4E0E31319C90B46215D0012740FEE`，不是另一份交付候选，也未修改正式两包。

该主harness原stdout与exit文件同时证明：64工具/1resource、protocol `2025-06-18`、server `0.4.0.0`、完整能力/必需参数/annotations及guide匹配、实际MCP exit0。只请求initialize、tools/list、resources/list、resources/read，没有tools/call、Bridge、安装或游戏动作。外层stderr为0字节；MCP自己的stderr记录为2645字符，不能笼统声称所有stderr为空。这项启动检查仍不证明Thunderstore Mod Manager/profile安装、Plugin加载、实际游戏运行或恢复。

维护为补充原始流记录，在root“不再启动MCP”消息到达前已启动第二个metadata进程；该记录的JSON/index宣称exit0，但原 `mcp-process.exit-code.txt` 为-1。root发现后保留全部原件，**不采用该补充退出码或将其重写成0**；本次合格结果只依据前述主harness及原字节核验。未再启动MCP或为补日志重跑测试。只读调查发现未保存补充caller源码，因此不能确定写入-1的具体函数、行号或成因；仅标记该补充退出码未证明。原件引用与准确不一致均包含在root审计中。

## 实机、安装与恢复的适用边界

实际安装仍为 `07d1727b76664142615f7eebce2888e3bff654ec` 的同批Plugin/MCP，FileVersion0.4.0.0，Native29145。此前228文件事务安装、新菜单及原生v6 prepare的证据见 [菜单部署记录](owned-primary-29145-menu-deployment.md)；不是本次2a7a98d两包的安装报告。

本轮唯一writer只获准60秒/1个get_session_state/0accepted，已完成并停止。fresh菜单为唯一DSP35240、healthy、unloaded/unowned、revision0、Native29145；root核该原件、唯一进程和原票据未变，未Load、Save、commit或重启。最后普通Save仍116215326/revision7/J102，退出前观察116289647/revision8/J102。未保存4石矿的原action、fixed20已用1/lifetime609/frozen、保留保存槽及剩19不变；没有新在途/unknown，不换键重取。

手动包的合成安装/回滚fixture、真实手动安装，以及Thunderstore适用安装/运行检查均未执行。本次只读检查确认安装入口在PreflightOnly/StageOnly之前也要求所有DSP进程退出（`scripts/install-release.ps1` 及共享事务入口）；因此不绕过门，也不为了离线fixture关闭健康菜单。后续在施工/必要代码稳定后的必要维护边界，分别完成原要求适用检查，不把两种方式合并掉。

当前真实阻塞是同owned原主档 `29131→29145` 的后续明确迁移确认。Native v6已披露保存tick116215326、P104、完整Journal及一次消费风险，`confirmationRequired=true`、`commitAllowedNow=false`。用户本轮明确排除需后续确认的迁移；[AGENTS](../../../AGENTS.md) 的恢复规则为“过期恢复/迁移保留各自后续确认”。自动goal续接和普通阶段授权不替代该确认。原票据仍未消费，没有迁移commit意图。

确认到来后的唯一安全入口：先证明旧writer已停并fresh核菜单/原票据/实际窗口；fresh prepare同一保存点，核新的完整prompt/digest，再唯一commit一次。原计划token不保存或复用；票据在原生Load前durably consumed，失败/中断只调查原意图，不换键重放。精确第四转换完整durable后，正常保存/新票据再独立验收。恢复后fresh核全部成功实体、库存及原4石矿动作；不假定较晚观察或未保存前缀已恢复。随后继续唯一游戏主线1209自动输入/1210输出及完整三级Foundry，按原计划/材料/续建证据补真实缺口，再预声明并完成同session连续≥36000游戏tick的最终联合窗及出发清单差额。

## 支持版本、限制与Release notes

两包声明Windows x64、DSP `0.10.35.29145`、BepInEx `5.4.17.0`；这是候选构建目标，不是完成跨版本恢复/所有Mod兼容的承诺。default恢复仍严格同版本；已研究的精确迁移对、v6后续确认、Journal连续性、票据一次消费及完成蓝图历史复用条件见原报告。未完成/unknown/损坏/不匹配的跨版本历史仍拒绝，不能清空进度文件。

原限定六对象蓝图生命周期、五对象中途保存续建和Governor31→62/min/37128ticks保持原通过范围，禁止重做。完整三级链、中途恢复覆盖、最终恢复后的自动来源/速率/库存/功率、出发套件、两种候选实机安装和恢复仍开放。R1300历史长窗不计新session。真实carrier stall仍是声明的实机覆盖限制；黑雾/战斗、多人/Nebula、广泛第三方Mod兼容、高级堆叠分拣器/传送带升级及实际跨恒星航行不扩大范围。

现有 [0.4.0开发Release notes](../../../packaging/thunderstore/changelog/0.4.0.md) 已随源码冻结，列出有限Foundry/升级/Governor及精确迁移边界，并明确上述开放项。没有tag、GitHub Release或Thunderstore发布；全部原必需门关闭后才交所有者发布审核。

## 直接原件索引

原日志和fixture保留在ignored区；受保护游戏回执只按以下精确引用/hash读取，不扫描历史。仓库报告及可审核索引不含token、真实存档名、私有绝对路径或原始身份。

| 原件 | 相对索引或引用 | SHA-256 |
| --- | --- | --- |
| 两包维护构建/逐文件map | `.local/candidate-package-2a7a98d-audit/candidate-package-index.json` | `521709987727CF3A77618C724C5605891955FA3AD4290D08BF3A494FEFCC7D9F` |
| 正式构建stdout | 同目录 `package-release.stdout.log` | `F62D4A6764AE45827422CF4C960A18790D2A7723C632622755C45F494A2A06A7` |
| 正式构建stderr，0字节 | 同目录 `package-release.stderr.log` | `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855` |
| 准确候选SHA的CI原日志 | `.local/ts-mcp-surface-audit-2a7a98d-64f8fc655ef7456ea9de52d728e6f5de/source-ci-38627772732.log` | `D2C4B229630C5123A084EEAB7B915F6FBA458CB34D6A7D6E1AEAD129CFAA548A` |
| TS维护索引，补充exit主张未获root采用 | 同目录 `ts-mcp-surface-index.json` | `2E7DEA18174E76F559F0C394BA574C5688BE72DDEDFE2ECDF9BDE4904F9AC962` |
| TS主harness结果 | 同目录 `probe.result.json` | `162A2647F7D20DD573EDC9A27A1D41C0B0ED6DE414190232708023CEC77BCA49` |
| TS主harness原stdout | 同目录 `probe.stdout.log` | `1F3DE2D30898540E2550EC06283B15FDD774DFE3F397D853F3E7DA3FA6935AFA` |
| TS主harness exit0 | 同目录 `probe.exit-code.txt` | `5FECEB66FFC86F38D952786C6D696C79C2DBC239DD4E91B46729D73A27FB57E9` |
| 补充exit原件-1，未计通过 | 同目录 `mcp-process.exit-code.txt` | `1BAD6B8CF97131FCEAB8543E81F7757195FBB1D36B376EE994AD1CF17699C464` |
| 本轮writer唯一fresh菜单/0写 | `071544fd22504c2988f978e9e342df91:2` | `A33A65DA5D2BFC7BD90C2D6F93606C05869FB8D97F653F2390D3DB71954C8439` |
| root菜单/原票据/窗口保全 | `9c37c8c5d4694257b3c2d9079ad113b5:1` | `6E1CD9447F1FEB7B6F77D8D64B266376CD1F9AE66A67CAF202837A946FD60A62` |
| root两份正式ZIP/9产品/源码审计 | `95228048cc7544779a6601d6adc6d6f2:1` | `5CFA51FD70BD5FC20F1AB7BB107F472A98AE28FE4A33A9305B97558D29140C8D` |
| root实际TS成员/主harness/exit不一致审计 | `5449a4c92ad6405981ae0359b96d3e23:1` | `12AC1B833DC6EB3452BBBB9C8FB8992B50A4B1590E16C6DFC2AD235768BEB354` |

开始前的引用属性预检曾错把manifest的`path`读成不存在的`relativePath`，在SDK/checkout之前停止；root核实际安装路径/5个文件后只修ignored caller再执行正式单次打包。首次root ZIP只读caller的数组拼接错误及本次补充exit不一致也保留原痕迹；均未导致重建正式包、改写原件或游戏写入。
