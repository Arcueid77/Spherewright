# Documentation

[English](README.md) · [简体中文](README.zh-CN.md)

Maintained guides have equivalent English and Simplified Chinese editions. Open the language link at the top of each page. Short compatibility redirects and store templates contain both languages in one file. The original English MIT license is authoritative; [its Chinese translation](../LICENSE.zh-CN.md) is explanatory.

## Start here

| Subject | English | 简体中文 |
| --- | --- | --- |
| Product overview | [README](../README.md) | [说明](../README.zh-CN.md) |
| Current product/candidate status | [Status](project-status.md) | [状态](project-status.zh-CN.md) |
| Installation | [Install](release-installation.md) | [安装](release-installation.zh-CN.md) |
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

- [0.4 validation](validation/v0.4.0.md) / [中文](validation/v0.4.0.zh-CN.md): frozen source/hashes, local acceptance and unverified branches.
- [0.4 release notes](release-notes/v0.4.0.md) / [中文](release-notes/v0.4.0.zh-CN.md): accepted candidate, not a formal release.
- [Lab scenarios/evidence](sample-validation.md) / [中文](sample-validation.zh-CN.md): fixed evidence snapshot, read-only product lock and reproduction.
- [Deferred remote validation](remote-validation.md) / [中文](remote-validation.zh-CN.md): future design, no deployed remote capability.
- [Documentation maintenance](documentation-audit.md) / [中文](documentation-audit.zh-CN.md): reviewed contradictions and verification boundary.
- [Store package template](../packaging/thunderstore/README.md) and [historical package notes](../packaging/thunderstore/changelog/0.3.3.md) are inline bilingual.

Latest published release is v0.3.3; accepted 0.4 candidate source is `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`. Main documentation can change without altering frozen ZIPs or updating installed components.

## Native research

- [29151 compatibility preflight](validation/native-29151-preflight.md) / [中文](validation/native-29151-preflight.zh-CN.md): exact expired-primary direction, completed-history retention, offline evidence and pending live checks.

[Environment](research/environment.md), [M0 historical baseline](research/game-api-m0.md), [Overseer](research/game-api-overseer.md), [Foundry](research/game-api-foundry.md), [29057/29088 history](research/game-api-version-0.10.35.md), [29104 history](research/game-api-version-0.10.35.29104.md), [29145 compatibility](research/game-api-version-0.10.35.29145.md) and [installation startup guard](research/installation-startup-guard.md) each have a complete Chinese companion. Research states its applicable version and direct symbols; historical findings do not certify the current runtime.

## Compatibility paths

The 258 short operational redirects, including [current-status](current-status.md), [evidence index](evidence/README.md), [delivery contract](v0.4-delivery.md) and [roadmap history](../ROADMAP-HISTORY-2026-09-24.md), preserve fixed Lab links. They are compatibility/history entries, not current execution instructions. Do not load whole historical ledgers as the working plan.

Run `pwsh -File scripts/verify-documentation.ps1` from the product root to check the complete Markdown set, language navigation, critical hashes/native versions, relative links/anchors and obvious private-path/credential patterns. CI runs the same check. Semantic translation equivalence and code/evidence interpretation also require review.
