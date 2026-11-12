# 原生科研暂停与恢复

[English](game-api-research-pause.md) · [简体中文](game-api-research-pause.zh-CN.md)

本次原生核对对应 DSP `0.10.35.29151`，程序集 SHA-256 为 `EF314425DADE51F529F184F07BE136CD3C2660A6A81515603C7226FBE7AB1D8C`。

`UIResearchQueue.OnPauseClick` 调用 `GameHistoryData.PauseTechQueue`。该业务方法将当前科技设为零并保留队列。恢复按钮调用 `ResumeTechQueue`，经 `AlterCurrentTech` 的正常前置检查重新选中保留的队首。

暂停时 `MechaLab.AutoManage` 使用仍为正数的队首科技，继续正常吸收随身所需科研物品。随后 `MechaLab.GameTick` 在当前科技为零时返回，尚未查询科研能源或推进研究。保留队首由此保持科研缓冲的供料分支和分数点。原生历史序列化包含当前科技状态。

正常研究选择动作可选传入 `researchPaused`。准备绑定准确队首、完整队列、当前暂停方向及选择 hash。提交在 Unity 主线程调用对应原生业务方法，同 tick 回读核验完整队列、科技进度、背包/燃料/手持物品及科研缓冲点数。MCP 投影要求准确 `plannedResearchPaused` 回显和 version1 保全能力。

Voyager 空闲证明支持身份已验证、元数据完整且随身所需科研输入为零的暂停研究站科技队列。准备、提交及持续控制均新鲜核对该状态，缓冲点数继续归原生研究组件持有。源码测试、安装及实机验收在 Lab 保持各自证据边界。
