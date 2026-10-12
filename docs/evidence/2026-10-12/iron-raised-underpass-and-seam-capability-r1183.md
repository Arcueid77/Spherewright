# R1182–R1183 高架下穿与接缝能力复核

本次只复核此前冻结的几何与接口证据，没有重新读取游戏：Game、Bridge、accepted 和 Save 均为 0；窗口仍为 12/20、lifetime 547，整条路线 Native 预览仍为 0/2，main/backup 仍为 null，持续来源信用为 0。已安装 cohort `8330b4e` 未变；包检查代码 `8ad32543` 的 CI `38196398320` 为绿色，但本次没有生成最终包或发布。

R1182 对 61 个离线布局进行检查，其中 17 个布局的全部跨度长度符合 `1.5–30 m` 范围；这只是跨度长度筛选，不表示这 17 个布局整体合法或通过 Native 验证。R1183 检查了 8 个离线下穿变体；这些变体不是已验证的接缝布局。两项分别耗时 6.008 秒和 1.801 秒，均为本地批量计算墙钟，没有同范围端到端提速证据。三类 L3 绕行仍受带体 `5066`–`5069` 横挡，最近约 `0.064 m`。L3→L2→L3 两个模型的中段中心线未与已观察 Box 相交，离最近旧 Box 约 `0.541526 m`；但新带的保守包络在 `1214` 附近仍有约 `0.203 m` 与 `0.272 m` 的风险距离。这些是模型几何结果，不是 Native 检查结果；候选还缺内部地形点、外围对象以及 fresh 全路线验证。

现有普通高架施工接口只核验一个明确的自由端到自由端 `2001` 跨度：请求不接受 source、destination 或 resource 绑定；准备与完成后的检查要求跨度内部连接闭合、外端保持空闲。相邻独立跨度不能据此推定能互相自动接缝。只读全路线预览会返回未准备、不可提交且无 token 的结果，不提供施工许可或来源信用。因此“五段+两分拣器+普通保存”的八动作估算不是现有接口可执行的连续施工方案。合法水平复用或单个高架跨度也不能在缺少新几何和接缝资格时当作替代路线。这些结论只适用于当前候选与接口，不推断其他高架布局一概不可行。

此前 R1125 两条短坡道及各自端点的 Native 正例仍保留；它们不证明本次下穿候选或多段接缝通过。下一步需要结构不同的路线和新几何/接缝资格，再核完整 Native 预览与施工预算；当前没有获准施工方案，来源与整案验收门保持不变。

受保护来源索引：R1182 `root-r1182-finite-observed-raised-corridor-batch`，`b42f4943395e43e88e839e052a234f3c:34`，SHA-256 `593178132B91B973B672B38D06D3AA755A0A225630310FF4FF7E8ABA1EB53004`；R1183 `root-r1183-underpass-geometry-and-ordinary-seam-capability-audit`，`b42f4943395e43e88e839e052a234f3c:35`，SHA-256 `7AF9CA13BEEF10A76461E3455984E45497C780912ACCD85F4E9B257712D088F5`。

源码边界依据：[`BeltElevationPolicy.cs`](../../../src/Spherewright.Bridge.Core/Factory/BeltElevationPolicy.cs) 规定单一自由端跨度并拒绝端点绑定；[`NormalGameActionCoordinator.BeltElevation.cs`](../../../src/Spherewright.Plugin/Game/NormalGameActionCoordinator.BeltElevation.cs) 核对跨度内部连接和自由外端；[`NormalGameActionCoordinator.BeltRoutePreview.cs`](../../../src/Spherewright.Plugin/Game/NormalGameActionCoordinator.BeltRoutePreview.cs) 仅生成不可提交的只读预览。
