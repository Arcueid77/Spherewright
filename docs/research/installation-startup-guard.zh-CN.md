# 安装启动守卫研究

[English](installation-startup-guard.md) | [简体中文](installation-startup-guard.zh-CN.md)

## 范围与来源

本文维护 `InstallationStartupGuard`、`SpherewrightPlugin.Awake` 和 `StopHost` 实现的运行时/安装器互斥契约。[2026-09-27 原始研究](https://github.com/Arcueid77/Spherewright/blob/2a7a98d1932c404e3a7abc2c11233d9c0b8780d9/docs/research/installation-startup-guard.md)属于历史源码/离线证据，其中当时待部署文字不是当前候选状态。已验收的本机包/运行证据及限制见[最终验收](../validation/v0.4.0.md)。此守卫不改变 DSP 游戏 API 或存档格式。

## 原生/运行时输入

研究中的 `BepInEx.dll` 为 `5.4.17.0`，SHA-256 `DC1CB6B58B962BDA5AAA1D6B5F9AE14EC174F61836A1A1F96C1A040C7E8381F7`。元数据确认公开静态 getter `BepInEx.Paths.BepInExRootPath`。`SetExecutablePath` 从显式覆盖或游戏的 `BepInEx` 子目录导出根，再从 `plugins` 导出 `PluginPath`。守卫使用该运行时根，不猜测 Steam/用户路径。

## 互斥与拒绝规则

- 安装目标为实际 `typeof(SpherewrightPlugin).Assembly.Location` 所在目录。目标、BepInEx 根及全部祖先必须是已存在、无 reparse point 的普通目录。
- `GetTargetMutexName` 与手动安装器的 `Get-SpherewrightInstallMutexName` 一致：`Local\SpherewrightInstallTarget-` 加规范目标路径经 invariant 大写、UTF-8 编码后的 SHA-256 大写十六进制。规范化去掉末尾分隔符，但保留文件系统根。
- `Awake` 在读配置和创建 `SpherewrightBridgeHost` 前取得租约。忙目标拒绝启动；遗弃 mutex 也拒绝。`WaitOne` 抛错时仍授予所有权，失败清理必须释放。
- 持有租约时，`BepInExRootPath` 的任何 `.spherewright-install-pending.json` 都阻止启动，包括同名目录/链接、不可访问或不能确定的结果。只有证明不存在才放行；标记内容不能授予权限。
- `StopHost` 先清理 pipe/descriptor host，再释放守卫，启动失败和 `OnDestroy` 同样如此。取得与释放在同一 Unity 线程执行，因为 Windows mutex 具有线程所有权。

这防止识别守卫的 Bridge 和协作安装器同时拥有目标，不阻止 `Awake` 前 CLR 装载程序集，不能给旧 Plugin 补上锁，也不授权自动修复。

## 验证与剩余限制

`InstallationStartupGuardTests` 覆盖规范名称、标记拒绝、忙/遗弃 mutex、租约生命周期，以及 Windows PowerShell/PowerShell 7 子进程与链接 .NET 守卫的争用。完整 `net472` Plugin 编译、真实包/运行检查与这些离线测试是不同证据。

安装器捕获失败且未解决时尽力扣留主 DLL，可降低旧加载器暴露，但不保证进程被杀或扣留失败后的安全。已验收候选不证明安装事务崩溃恢复、全部旧版原子升级、Mod Manager/r2modman 互操作或所有 Unity Mono 加载路径/生命周期分支。应保留恢复证据，使用独立获准的恢复路径，不能把标记或测试结果当启动许可。
