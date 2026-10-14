# 29145 同批菜单部署与原生 v6 披露

精确恢复修复已从干净、已推送、绿CI的源码 `07d1727b76664142615f7eebce2888e3bff654ec` 构建并安装；新菜单的原生v6准备通过root独立审核。实际跨版本Load尚未执行，正在等待披露后的用户明确确认。本记录补充[离线修复](owned-primary-29145-candidate-validation.md)，不覆盖原失败，也不将必要部署cohort称作最终双候选。

## 源码与构建

维护fresh只读核本地HEAD、origin/main及实时远端main均为上述完整SHA且clean；[Windows Core CI 38620035304](https://github.com/AvaloNero/Spherewright/actions/runs/38620035304)为匹配headSha的completed/success。源码通过git archive冻结，ZIP内提交标识也相同。使用SDK8.0.424完成全solution locked restore、完整Release构建、win-x64 self-contained MCP publish，三个命令均exit0，Release为0警告/错误。

构建采用5份当前安装原生refs；实际Native菜单版本0.10.35.29145，Assembly-CSharp SHA-256为 `B84B60DF5A24947A014801F23B9B85D1E77853F42A331918E2DC85B153E50527`。旧29131原生研究文件保留，refs目录旧标签不作为版本证据。Plugin、Contracts、Bridge.Core及MCP的FileVersion均0.4.0.0，ProductVersion均 `0.4.0+07d1727b76664142615f7eebce2888e3bff654ec`；Plugin4文件、MCP224文件共用同一逐文件map，无PDB。

既有StagedMcp探针及PackageSurface完整集合断言通过：64工具及必需参数、1resource、内嵌playbook逐字一致，额外stdout字符0。PackageSurface使用validation-only ZIP，只验证metadata集合，不是手动或Thunderstore最终候选，也没有游戏调用。root另核228文件、原生refs、源码archive、构建日志及SourceRevision。

| ignored 构建原件 | SHA-256 |
| --- | --- |
| `cold-cohort-material-cut-07d1727/source.zip` | 5D8148F0D8E5FF5C6748A1B10792F69307335ABF61FE4566320F92F8174CBD4B |
| 同目录`cohort-index.json` | BA190548407A5C7D3A5A1DB41FAE560D3C2B2A79204888952EC060C29FCD2CF5 |
| 同目录`component-file-map.json` | 80CEF0841ADA1E281C94EDA4E77AD98B5FEC110AA1D8EA77AF7CDFDA1109A881 |
| 同目录`solution-locked-restore.log` | 2B6597DC50A7DB8FCE1FED4ABA35C9D50D04D60A10CEE6551CE51BFB4CAE6D97 |
| 同目录`solution-release-build.log` | 7E1AC3589D73AFBB418F883B380A590A87FBA6271290013BC30DED9B501B939E |
| 同目录`mcp-self-contained-publish.log` | A37F5B44F86D6525CBC84A8403942510F11CD3913747E0D7E406768EE03F0238 |
| 同目录`staged-mcp-probe.json` | 1150884012A4D412EAF9699CB8E05138249ED4A1DE5351E692FF6146566C2A82 |
| 同目录`package-surface-validation.json` | F03AE973718D570FD633EC7631853175DB193A714B522322514A12CFC9BE5339 |

这些原件处于既有ignored `.local`证据区；表中相对索引不包含私有绝对路径。cohort-index的“未部署”限制描述的是构建时状态，后续真实安装只由下节新回执证明；原件不改写。

## 后续候选打包准备

菜单部署及披露的纯事实提交2f861429eac19fbdfcd22b04b0c8fd071c86a9f1已普通push且当时clean；[Windows Core CI 38626121629](https://github.com/AvaloNero/Spherewright/actions/runs/38626121629) headSha完全匹配该完整SHA、completed/success。此后核两份正式打包脚本发现`supportedDspVersion`仍为旧29131，root各只改该声明为当前候选目标29145；Thunderstore双语包内README及现有0.4开发changelog同步明确目标、已实现能力、原限定通过范围及仍开放的整体验收。没有生成/安装新候选，不改变当前07d1727运行cohort。

实际pwsh7.6.5及显式启动的Windows PowerShell5.1.26100.9444分别解析两份脚本并读取唯一manifest目标声明，均exit0，0包执行/0游戏调用；说明目标一致。中途通过工具shell选择的请求仍返回7.6.5，不计为5.1证明；后续明确启动5.1子进程的真实返回才计入本结果。这只是静态语法与声明核验，不代替最终ZIP的manifest、文件哈希、完整工具/指南、安装和实机检查。

原件为ignored `package-target-29145-metadata-checks.json`，SHA-256 `4B5F2D1F65567999129129E89EC85811EEC99B0D745AB34BA19E16224E1C747C`；静态入口`CheckPackageTarget29145Metadata.ps1` SHA-256 `5E3452C1BCDC128459275C378D6EF92CB2F80A703279B1DEA19CA10B56009EFF`。被检查的manual脚本SHA-256 `C72C4EBE8B8BA5785AC68B0CBB1DD8D089F825BAD1BAD34DE8D63D6593D37795`、Thunderstore脚本 `269A740986651AE1C3386C993CB59AEF79BF948D9C4EDE59706DAFB305B2718B`。这些后续文件获准指定路径单一目的提交；准确新源码/CI以维护终态为准。最终打包时须采用届时clean、已推送且CI成功的同一SHA，不把07菜单cohort或metadata验证ZIP冒充最终两包。

## 一次必要冷部署

root批准有限570秒、1次正常关闭已确认的unloaded菜单、1次同批事务安装、1次既有Steam桌面broker调度及1次fresh菜单读取；无Save/Load/accepted额度。caller及传递依赖在导入前固定hash，durable CreateNew/WriteThrough/Flush(true)一次性intent阻止重跑。原退出原因未知，未用强关或另启EXE。

唯一writer执行该阶段exit0。旧DSP29080正常关闭，新唯一DSP35240 Responding；原Steam36408及启动时间保留。事务安装Plugin4/MCP224文件并保留runtime-handoff。root核原intent/调度终态和全部228个实际安装hash，原票据、完整Journal及五对象进度文件SHA均未变。此时只证明安装与调度；菜单/Bridge由下一阶段fresh响应证明，owned恢复仍未证明。真实Host退出存活测试没有执行。

## 原生 v6 披露与确认边界

另一次root批准的180秒/2请求/0accepted阶段，只读新菜单并prepare `reauthorize_version_changed_primary`，约2.8秒完成。fresh Native菜单为unloaded、unowned、29145、healthy、revision0、bridgeConnected=true。原生准备返回：source29131→target29145、evidenceVersion6、P104、candidateTick=minimumTick=116215326、exactEmbeddedIdentityVerified=true、userConfirmationRequired=true、commitAllowedNow=false；pair-specific完整prompt/digest与原响应逐字段相符。prepare前后原票据SHA相同，没有消费、Load、Save或恢复commit。

root独立核两个原生响应、durable准备intent、writer原回执、实际228安装文件及原票据/Journal/进度hash。实际窗口仍fixed20、external1/lifetime609、frozen，knownInFlight0，剩19槽包含预留普通保存。原Journal102条、三次转换和五对象completed历史未改盘。writer已停止；两个有限阶段都已结束，没有后台游戏任务。

当前候选是最后普通Save116215326/J102。退出前观察116289647/revision8，成功的4石矿动作尚未普通保存；该未保存前缀不能被称为已持久化或已恢复，原action `44dbfb3a-08da-41a9-b43b-6c7960d40232` 及已用额度保留，不换键重取。五对象已由较早普通Save覆盖，仍须实际恢复后fresh核实体/连接。

实际迁移需要用户在本次披露后明确确认，来源为用户本轮排除项及AGENTS第35行：“过期恢复/迁移保留各自后续确认”。披露内容限定同一原owned主档和完整Journal；原票据在native loading前durably consumed，失败/中断调查而不重放，精确第四转换durable后才正常保存并签发新版本票据。后续获确认时必须fresh准备同一候选，使用新plan/digest；本次planToken已脱敏，不落盘或复用。等待确认期间不Load、不退出DSP，不将普通阶段结束写作整个交付完成。

## 受保护原件索引

| 直接原件 | 引用 | SHA-256 |
| --- | --- | --- |
| root clean源码/同批构建独立审核 | 420621ca61274642b600817c8b977593:1 | BDDFD0BBC9B6D4D36BD03434448B202B4076C8FE04B16CEFD238572552848087 |
| root菜单冷部署批准 | 05e0c43c1e14478ebda34071320505d5:1 | 62936E7AD4AEE504A2EB37600571A08A3231934EEA16D286F2DD314FC6CF407F |
| 冷部署原writer终态 | 496641597fe24cb4aac46140b3e43e2f:3 | 0ABC5401D0CF6014D852FE5E84A64F5A1CEB1B0BA557FE29A87101031F424B1D |
| root冷部署/原件保全独立审核 | 4651c3d0b91c4756bf7759300923b6fb:1 | 0BBCE437FAE73AB54B6FAF21A760602934CF3E0089D0265B45F0EE5AE9CC784C |
| root只prepare有限批准 | 7a1a94172b6e4535b6e886607d87578e:1 | F9ABF8F53AF8D534EC4FAF66FC3E191E4C529B6308A47512FCA9D133754259F1 |
| 新菜单原生响应 | f8481cbf72e0443b91b80eacc6be4000:1 | 667D6F4C65DB87500349DD7236E8937F7AF1B4AF2715E682499205BF904F1B6D |
| v6 prepare原生响应 | f8481cbf72e0443b91b80eacc6be4000:2 | 50AE1F2544F7F0B372E0E186256048DD4A9F34039D2E6E1207F9A6AD6BC4B6AC |
| 披露原writer终态 | f8481cbf72e0443b91b80eacc6be4000:3 | 56B0754ECE8988E88C0C1435B0E94A38ABBCD590E05F2E33FB2682D78C0535BB |
| root v6响应/窗口/原件保全独立审核 | 1b1152237fe54255baf96fd9d7f150c5:1 | 7F502E48BF7309FDC5B01FEAE043EF6D743136BC840D22915596017E10BAE0A9 |

后续唯一游戏主线是合法恢复后核现有1209自动供料/1210输出、逐条复用三级链事前计划及施工/续建证据，再补真实缺口与最终恢复后的联合长窗。最终手动/Thunderstore同SHA双候选、≥36000连续游戏tick及整体实机/恢复门仍未关闭，见[current-status](../../current-status.md)。本次执行SHA可被后续纯事实提交引用，不为证据自引用无限重打包。
