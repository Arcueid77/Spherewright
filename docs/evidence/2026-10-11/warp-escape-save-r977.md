# R963–R977：Warp补给、受限移动与覆盖保存

## 有界前缀与未提交目标

R971 writer记录 `79b3d98b08a64656a327e77e84fc2a14:45` / SHA-256 `3E8CA7F8698940B84DA19CCDB2669754E553BFD307E5EE9E412ED94230F25CEA`，root独立核验索引 `0299d7dfe49841f0b44eefb14c697743:137` / SHA-256 `F3FD11FB12FD6721C44EA1323B3FB1B90CC4F3076ACE3D4453C49D3D2EA7E13D`。核验覆盖44条原始记录和18个Native动作：从仓5331转出Warp `200`并成功完成约5米逃离Move，两笔accepted均保留。后续约18米Move虽获正向Native prepare，4个岸线样本显示涉水风险，目标地面低于水面 `1.2934169769 m`，caller在commit前拒绝；没有第三笔commit。该子阶段未用的5笔额度退役，不转移；不重做成功前缀或重试涉水目标。

## 保存与独立核验

R976普通保存writer记录 `6ceff3ca49d942acb62da3a2716055e7:21` / SHA-256 `FA3F7B3D15EDA27E80CAFEC4A68473EC9A30B03DE95BBBC2B2522094D25D60FE`，15个RPC，`3531.2985 ms`。R977独立核验索引 `0299d7dfe49841f0b44eefb14c697743:141` / SHA-256 `AFF828EC3406569A672B33857C446185572DFAC42517906D0B4D4F9D381D8216`，核心审计耗时 `467.9608 ms`；核对唯一原动作、保存覆盖、affected custody、9个受影响对象与配置、本地供电和完整Journal，无在途或未知动作。

当前普通保存为tick `108894684`、revision `137`、durable Journal `102`。固定20窗口从lifetime `502`开始，当前 `10/20`、lifetime `512`，仍开放且冻结；整案累计 `113` accepted、`33`次Move、`31`次普通保存、`21`次手工材料动作。玩家背包为Warp `300`、Ti Ingot `500`、Rod `5`、Stone `0`，占用 `29`格；Rod `400`仍待携带。Stone `6`及Oil `261`保管、当前受影响物料和全部动作保存覆盖均已核验。

R977复用R935完整工厂基线 `0299d7dfe49841f0b44eefb14c697743:112` / SHA-256 `A2993E2CFD206F0D7A8AC8E62D6ED9B55F96C7F9B01DBAE28F0A0283401F81E7`（`6492 built / 0 prebuild`），没有重扫完整工厂；此次仅核9个受影响对象、原配置、P104本地供电与Journal。不能将该基线写成R977新全厂扫描。

## 流程耗时与验收边界

R970阶段终止至R976保存终止相隔 `16.1907937183 min`。这是阶段间隔，不是caller执行时长，也不能与其他阶段作不匹配的性能比较。R971 writer耗时 `5765.0089 ms`，三次只读刷新 `786.7148 ms`；R976保存 `3531.2985 ms`；R977独立审计 `467.9608 ms`。窗口复用与有限读取继续使用，但这些局部数字不证明端到端整体提速。

source与整案仍未通过：`sourceConditionsPassed=false`、`wholeSupplyPassed=false`、continuous credit `0`；Rod `400`、持续来源与既定完整供给验收仍待证明。后续业务caller `EB8CCD0211A576400034BD3CA1106FD1AE15371E6CD7911C66BEC55E809EEC00`的零Game smoke及五类拒绝检查已通过，但其中三次只读刷新尚无实机正例，不构成运行时验证。
