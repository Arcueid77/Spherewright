# Lab 场景与完整验收

[English](sample-validation.md) · [简体中文](sample-validation.zh-CN.md)

[Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab) 维护场景、运行交接与完整验收。产品构建、测试和打包不拉取或执行 Lab。

不可变迁移快照为 `14478a03c64c550e644c559405c8496b647552e7`，产品子模块固定 `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`。尚无正式 v0.4.0 tag，因此锁保持 `status: provisional` / `releaseTag: null`。导入运行事实来自产品 `e7d5f7d8661092b7c8089710488c448f290638be`，与更早候选源码不同。

## 复现固定证据检查

```powershell
git clone --recurse-submodules https://github.com/Arcueid77/Spherewright-Lab.git
git -C Spherewright-Lab checkout 14478a03c64c550e644c559405c8496b647552e7
git -C Spherewright-Lab submodule update --init --recursive
cd Spherewright-Lab
pwsh -File scripts/bootstrap.ps1
pwsh -File scripts/verify-source-lock.ps1
pwsh -File scripts/verify-evidence.ps1
```

上述是文件、源码与证据检查，不是游戏动作。干净 clone 无法核验不在其中的受保护本地原件或冻结 ZIP；核验器会明确报告边界。

- [v0.4 场景](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7/scenarios/v0.4-final)
- [验收摘要](https://github.com/Arcueid77/Spherewright-Lab/blob/14478a03c64c550e644c559405c8496b647552e7/releases/v0.4.0/validation-summary.md)
- [证据](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7/releases/v0.4.0/evidence)与 [manifest](https://github.com/Arcueid77/Spherewright-Lab/tree/14478a03c64c550e644c559405c8496b647552e7/releases/v0.4.0/manifests)
- [迁移核验](https://github.com/Arcueid77/Spherewright-Lab/blob/14478a03c64c550e644c559405c8496b647552e7/releases/v0.4.0/migration-validation.md)

安全原件在规范化前按字节归档。所有者选择将原始经验账本仅留受保护本地目录，因为其中含历史绝对系统路径；Git 只保存脱敏副本。私有游戏记录、Save、票据、身份及凭据不在仓库中。公开可见性不会让本地证据自动可用，也不授予执行权限。

旧产品运行路径保留短双语固定快照跳转。旧快照措辞属于历史；未来工作阅读 Lab 当前 AGENTS/交接，并 fresh 核验实际 session、native 和所有权。固定产品子模块默认只读；通用修复回到独立产品 clone/worktree，通过准确 SHA 的 CI 后才更新锁。
