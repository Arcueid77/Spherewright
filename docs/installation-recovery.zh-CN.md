# 恢复中断的手动安装

[English](installation-recovery.md) · [简体中文](installation-recovery.zh-CN.md)

此明确操作还原**事前捕获的原安装载荷**，不会选择新版本、处理游戏存档，或证明旧安装健康。先停止 DSP 与 MCP host。游戏存档的 protected resume 是另一接口。

保留 pending marker、两侧精确操作目录、进度记录和原备份。使用失败安装的明确 operation ID 与目标目录，不凭最新时间戳选择目标。

## 先预览，再明确还原

包内 `recover-install.ps1` 默认只读预览：

```powershell
./recover-install.ps1 -PluginDestination '<exact Plugin destination>' `
  -McpDestination '<exact MCP destination>' `
  -OperationId '<failed installation operation ID>'
```

在本地用该精确操作的值替换占位符，检查返回 ID 与 fresh `evidenceHash`。明确还原绑定该证据：

```powershell
./recover-install.ps1 -PluginDestination '<same Plugin destination>' `
  -McpDestination '<same MCP destination>' `
  -OperationId '<same operation ID>' `
  -RestoreOriginal -ExpectedEvidenceHash '<fresh preview evidence hash>'
```

每次调用取得两侧目标锁，核验 marker 绑定的 stage/archive、匹配的不可变记录、完整备份、已识别 live hash 及未改变的 `runtime-handoff`。遇到 reparse point 或意外载荷文件会拒绝。顺序写入的进度记录可因中断处于不同阶段，但身份或原快照不同不可接受。

预览 hash 绑定两份实际记录与当前证据。证据变化后须重新预览，不能强行复用旧 hash。

## 还原与失败处理

还原先移开 live 主 Plugin DLL，再还原并核验其他 Plugin 文件及 MCP，最后还原主 DLL。只有最终原载荷及两侧终态记录都通过核验后，才删除本操作 pending marker。原本不存在的文件继续不存在；被移开文件和备份保留为证据，新建的空上级目录可能保留。

失败保留 marker 和证据，并尝试再次移开主 DLL；移开失败也会报告。不要盲目重放失败还原，应调查并取得 fresh 证据。工具不会自动重试、扫描无关备份、修复畸形记录或覆盖未知 hash。截断、不完整备份及无法解释的文件变化需调查。

## 范围与路径预算

仅覆盖受支持手动包目标布局。明确恢复可取得 abandoned 安装 mutex 后重新核验；普通安装及 Plugin 启动仍拒绝 abandoned 锁。新启动门无法阻止旧 Plugin 已经驻留内存。

所有未来 stage/archive、备份及逐次恢复证据路径在写入前计入预算。为兼容受支持 PowerShell，文件路径最多 259 个 UTF-16 code unit，目录最多 247。这是产品限制，不是所有 Windows API 的共同限制。新安装可选更短目录后重新预检；已中断安装须保留精确目标、marker 与证据并寻求审核，禁止移动证据、编辑记录路径或启用扩展路径语法绕过拒绝。

不宣称跨卷原子升级、通用硬件故障保护或部分写入记录的恢复。合成测试、源码构建、包检查及实机部署属于不同证据层；[验收](validation/v0.4.0.zh-CN.md)列出实际通过项。真实安装事务崩溃恢复仍未验证。
