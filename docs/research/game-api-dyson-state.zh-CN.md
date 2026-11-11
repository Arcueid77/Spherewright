# 原生戴森结构与射线接收器观察

[English](game-api-dyson-state.md) · [简体中文](game-api-dyson-state.zh-CN.md)

0.6 Ascension观察切片面向DSP `0.10.35.29151`，Assembly-CSharp SHA-256为 `EF314425DADE51F529F184F07BE136CD3C2660A6A81515603C7226FBE7AB1D8C`。研究依据为本机合法安装的原生程序集及完整Native Plugin构建。

## 结构与进度

`spherewright_get_dyson_state`绑定当前owned本地星球、所在恒星及已有 `GameData.dysonSpheres` 条目。`layerId=0`选择全部已有层，1..10选择准确层。读取器在游戏主线程从已有层、节点、框架和壳层池复制标量数据。

节点 `sp`、`spMax`分别表示已交付及计划结构点。每个框架以 `spA + spB`计数一次，最大值取自身 `spMax`。在途结构点来自节点订单。壳层进度取 `nodecps` 最后一个累计值，最大细胞点按 `vertexCount * cpPerVertex`计算。复制边界节点/框架ID并核对闭环，汇总时每条框架及每个壳层各计一次。

完整设计hash绑定owned会话、恒星、所选层范围、半径、轨道旋转、节点位置/原型、框架端点及壳层边界。交付进度和最近发电量独立采样。复用既有有界发射井火箭观察器，返回准确火箭ID及目标。太阳帆计数保留原生分配器边界核验。

单次观察最多返回10层、256节点、512框架、128壳层、4096个壳层边界节点和256接收器，并限制池扫描。覆盖不完整或不一致时，返回明确问题及可空汇总值。较大设计可选择单层缩小观察范围。

## 接收器功率

原生 `PowerSystem.GameTick` 将实际有用的电能分配给联网发电设备，并写入各设备的 `generateCurrentTick`。该值复制为 `generatedEnergyPerTick`，按每秒60个原生tick换算瓦特。

Gamma的 `capacityCurrentTick`来自原生日照、预热、透镜及戴森响应计算。光子模式使用该容量推进产品，其电力 `generateCurrentTick`为0。因此响应分别提供容量、最近实际发电、产品模式/缓冲、透镜数量/增产点及剩余透镜点数。

每个接收器绑定实体、原生Gamma预制体、发电组件池、恒星、星球及电网成员关系。当前恒星覆盖已有驻留工厂，工厂详情检查通过可空 `rayReceiver`返回同一数据。

## 验证流程

持续输出验证使用预声明游戏tick区间，并保持接收器身份、模式和电网成员关系。记录各样本的游戏tick、实际交付电能、缓冲变化及拓扑。观察接口提供只读数据，`executable=false`、`commitAllowedNow=false`。原生设计写入、产线供给及保存恢复验收按各自正常执行路径推进。

源码 `ee810a699b71db44bfe11ae1e4e71b9370f79353`通过6389项源码回归和准确CI。同源0.6开发程序完成owned世界的正常冷恢复，观察接口返回当前恒星初始空结构和空接收器集合的完整覆盖。正向接收发电及非空设计验证按后续Ascension阶段推进。[本源码原生记录](https://github.com/Arcueid77/Spherewright-Lab/blob/e0023fabe84cb989f6498e99636cd83bfb64d03b/scenarios/v0.6-ascension/current-status.md)
