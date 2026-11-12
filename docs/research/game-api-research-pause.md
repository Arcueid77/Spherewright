# Native research pause and resume

[English](game-api-research-pause.md) · [简体中文](game-api-research-pause.zh-CN.md)

The Native reading targets DSP `0.10.35.29151`, assembly SHA-256 `EF314425DADE51F529F184F07BE136CD3C2660A6A81515603C7226FBE7AB1D8C`.

`UIResearchQueue.OnPauseClick` calls `GameHistoryData.PauseTechQueue`. That business method sets the active technology to zero and retains the queue. `OnResumeClick` calls `ResumeTechQueue`, which selects the retained head through the ordinary prerequisite checks in `AlterCurrentTech`.

`MechaLab.AutoManage` selects the positive retained head while paused. Its automatic-supply branch continues to admit carried required items. `MechaLab.GameTick` then returns when the active technology is zero, before querying research energy or advancing research. Keeping the queue head therefore preserves the research-buffer supply branch and fractional points. Native history serialization includes the active technology state.

The normal selection action optionally accepts `researchPaused`. Its prepare binds the exact head, complete queue, current pause direction and selection hash. Native commit calls the corresponding business method on Unity's main thread. Immediate same-tick readback verifies the complete queue, technology progress, package/fuel/held items and research-buffer points. The MCP projection requires a matching `plannedResearchPaused` and version1 preservation capability.

Voyager quiescence recognizes an identity-verified paused laboratory queue with complete copied metadata and zero carried required research inputs. Prepare, commit and continuous guards observe this state afresh. Buffered points remain owned by the Native lab. Source tests, installation and gameplay acceptance retain their individual evidence boundaries in Lab.
