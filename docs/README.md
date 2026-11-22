# Documentation

[English](README.md) · [简体中文](README.zh-CN.md)

Maintained guides have equivalent English and Simplified Chinese editions. Open the language link at the top of each page. Short compatibility redirects and store templates contain both languages in one file. The original English MIT license is authoritative; [its Chinese translation](../LICENSE.zh-CN.md) is explanatory.

## Start here

| Subject | English | 简体中文 |
| --- | --- | --- |
| Product overview | [README](../README.md) | [说明](../README.zh-CN.md) |
| Current product/candidate status | [Status](project-status.md) | [状态](project-status.zh-CN.md) |
| Install and verify the first connection | [Install](release-installation.md) · [Codex read-only check](release-installation.md#connect-codex-and-verify-read-only-access) | [安装](release-installation.zh-CN.md) · [Codex 只读检查](release-installation.zh-CN.md#连接-codex-并检查只读访问) |
| Interrupted manual installation | [Recovery](installation-recovery.md) | [恢复](installation-recovery.zh-CN.md) |
| Agent operation | [Playbook](agent-playbook.md) | [指南](agent-playbook.zh-CN.md) |
| Protocol and tool boundary | [Protocol](protocol.md) | [协议](protocol.zh-CN.md) |
| Architecture | [Architecture](architecture.md) | [架构](architecture.zh-CN.md) |
| Safety | [Safety model](safety-model.md) | [安全模型](safety-model.zh-CN.md) |
| Contributions | [Policy](../CONTRIBUTING.md) | [贡献政策](../CONTRIBUTING.zh-CN.md) |
| Agent contributors | [Rules](../AGENTS.md) | [协作规则](../AGENTS.zh-CN.md) |
| Approved version direction | [Roadmap](../ROADMAP.md) | [路线图](../ROADMAP.zh-CN.md) |
| Changes | [Changelog](../CHANGELOG.md) | [变更记录](../CHANGELOG.zh-CN.md) |

## Release evidence and boundaries

- [0.6 release notes](release-notes/v0.6.0.md) / [中文](release-notes/v0.6.0.zh-CN.md) cover Ascension, package compatibility and the scope of live validation.
- [0.5.1 Wind Recovery](https://github.com/Arcueid77/Spherewright/blob/v0.5.1/docs/release-notes/v0.5.1.md) / [中文](https://github.com/Arcueid77/Spherewright/blob/v0.5.1/docs/release-notes/v0.5.1.zh-CN.md) cover the power-grid impact preview, single-turbine dismantle and independent rebuild on the 0.5 maintenance line.
- [0.5 release notes](release-notes/v0.5.0.md) / [中文](release-notes/v0.5.0.zh-CN.md) summarize Voyager, launch, stacking and package provenance.
- [0.4 validation](validation/v0.4.0.md) / [中文](validation/v0.4.0.zh-CN.md): frozen production-acceptance source, hashes, local evidence and unverified branches.
- [0.4 release notes](release-notes/v0.4.0.md) / [中文](release-notes/v0.4.0.zh-CN.md): the published release, final package/install evidence and support limits.
- [Lab scenarios/evidence](sample-validation.md) / [中文](sample-validation.zh-CN.md): fixed evidence snapshot, current Lab entry points, read-only product lock and reproduction.
- [Deferred remote validation](remote-validation.md) / [中文](remote-validation.zh-CN.md): future design, no deployed remote capability.
- [Documentation maintenance](documentation-audit.md) / [中文](documentation-audit.zh-CN.md): the dated 2026-10-16 review and its verification boundary.
- [Store package template](../packaging/thunderstore/README.md) and [historical package notes](../packaging/thunderstore/changelog/0.3.3.md) are inline bilingual.

The latest formal release is `v0.6.0`, built from `78274df5e93ca912a2d4b6670f7eb707d3562e94`. The manifest records archive checksums and the build used for live validation. [GitHub Release](https://github.com/Arcueid77/Spherewright/releases/tag/v0.6.0)

## Native research

| Topic | English | 简体中文 |
| --- | --- | --- |
| Dyson nodes, shells and receiver power | [Research](research/game-api-dyson-state.md) | [中文](research/game-api-dyson-state.zh-CN.md) |
| First sail | [Research](research/game-api-first-sail.md) | [中文](research/game-api-first-sail.zh-CN.md) |
| First carrier rocket | [Research](research/game-api-first-rocket.md) | [中文](research/game-api-first-rocket.zh-CN.md) |
| Planetary navigation and shore arrival | [Research](research/game-api-native-surface-navigation.md) | [中文](research/game-api-native-surface-navigation.zh-CN.md) |
| Research pause and resume | [Research](research/game-api-research-pause.md) | [中文](research/game-api-research-pause.zh-CN.md) |
| Surface recovery lifecycle | [Research](research/native-surface-recovery-lifecycle.md) | [中文](research/native-surface-recovery-lifecycle.zh-CN.md) |

- [29151 compatibility preflight](validation/native-29151-preflight.md) / [中文](validation/native-29151-preflight.zh-CN.md): exact expired-primary direction, completed-history retention, offline evidence and pending live checks.

[Environment](research/environment.md), [M0 historical baseline](research/game-api-m0.md), [Overseer](research/game-api-overseer.md), [Foundry](research/game-api-foundry.md), [29057/29088 history](research/game-api-version-0.10.35.md), [29104 history](research/game-api-version-0.10.35.29104.md), [29145 compatibility](research/game-api-version-0.10.35.29145.md) and [installation startup guard](research/installation-startup-guard.md) each have a complete Chinese companion. Research states its applicable version and direct symbols; historical findings do not certify the current runtime.

## Compatibility paths

The 258 short operational redirects, including [current-status](current-status.md), [evidence index](evidence/README.md), [delivery contract](v0.4-delivery.md) and [roadmap history](../ROADMAP-HISTORY-2026-09-24.md), preserve fixed Lab links. They are compatibility/history entries, not current execution instructions. Do not load whole historical ledgers as the working plan.

Run `pwsh -File scripts/verify-documentation.ps1` from the product root to check the complete Markdown set, language navigation, critical hashes/native versions, relative links/anchors and obvious private-path/credential patterns. CI runs the same check. Semantic translation equivalence and code/evidence interpretation also require review.
