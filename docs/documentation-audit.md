# Documentation maintenance review — 2026-10-16

[English](documentation-audit.md) · [简体中文](documentation-audit.zh-CN.md)

## Scope and language coverage

The review covers all 287 original product documentation files: 286 Markdown files and the English MIT license. Current content has been checked against product code, scripts, tests and fixed candidate/Lab evidence. The maintained result consists of 26 complete English/Chinese page pairs, 262 inline bilingual pages (258 compatibility redirects and four store pages), the Chinese explanatory license and the unchanged authoritative English license. Historical source remains linked at exact commits rather than being presented as current guidance.

The [index](README.md) groups installation, recovery, Agent operation, protocol, architecture, safety, release evidence, research, roadmap and contribution rules. English and Chinese carry equivalent operational obligations, identifiers, native versions and hashes; code symbols are not translated.

## Resolved contradictions

| Previous wording or structure | Maintained rule and evidence |
| --- | --- |
| Product split still awaiting merge; temporary untagged-cleanup prohibition | Split ownership is established. Contributor rules require explicit authorization for shared-history rewriting, protected backups, exact mappings/tree checks and exact-ref leases; ordinary changes use normal pushes. |
| Product links call Lab a private Sample repository | Use canonical Spherewright-Lab links. Immutable evidence links remain fixed at `14478a03c64c550e644c559405c8496b647552e7`; historical snapshot wording is not current visibility or authorization. |
| Store README/changelog still say three-stage acceptance, installation and 29145 migration are pending | The locally accepted frozen candidate is documented with its exact scope, hashes and remaining limitations. A separate Mod Manager profile and another computer remain unvalidated; no formal 0.4 publication is implied. |
| Old three-transition limit and incomplete version pair | `OwnedWorldVersionCompatibilityPolicy.MaximumDurableTransitions=4`; active-primary evidence5/6 are exact directional pairs with later confirmation, original Journal continuity and pre-load durable attempt consumption. |
| Research mixes source implementation, old native observations and current live proof | Curated version-scoped research links complete immutable originals at source `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`. Historical limitations are retained without turning them into new current blockers or new live proof. |
| Generic device-upgrade language suggests unsupported families | Executable scope is ordinary manufacturing assemblers and basic `2011 → 2012` sorters. Belt/advanced-sorter research does not enable those upgrades. |
| English-only long guides or short English summaries | Complete paired translations and inline bilingual compatibility/store pages, with language navigation and a preserved authoritative license. |
| Renamed package guides retain source-only relative links | Manual packaging maps guide aliases and sends repository-only links to the exact packaged source SHA. Package smoke checks can require Chinese guides, manifest coverage and working relative links. English playbook remains byte-matched to its MCP resource. |

## Verification

`scripts/verify-documentation.ps1` walks the complete maintained Markdown set, excluding ignored private/build outputs and submodules. It checks pair/inline coverage, language navigation, candidate hashes/native-version parity, relative files/heading anchors and obvious private-path/credential patterns. It emits a per-file hash inventory when `-ReportPath` is supplied. CI runs the same guard; semantic equivalence and source interpretation receive separate review.

Relevant product regressions, exact-SHA CI and isolated preview-package checks verify the changed embedded guide and package documentation. Preview packages are maintenance-test outputs, not replacement 0.4 acceptance candidates. Preserve the original frozen ZIPs and [accepted validation](validation/v0.4.0.md); document-only tests cannot establish new gameplay, installation or recovery.

## Runtime and publication boundary

Accepted runtime source remains `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`, with DSP `0.10.35.29145` and BepInEx `5.4.17.0`. New source documentation does not hot-replace Plugin/MCP or bump Lab's fixed lock. No game, Save/Load, deployment, formal tag, GitHub Release or Thunderstore publication is authorized or performed by this maintenance.
