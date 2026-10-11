# R953–R961：caller误停纠正与有限移动阶段

## 阶段边界

本页记录R940 Stone动作读回后的caller纠正，以及随后一段有限移动/需求作业。它不改变source、whole-supply或最终发行门，也不把本地审计耗时当作游戏整体提速证据。

Root R953复核索引 `0299d7dfe49841f0b44eefb14c697743:125` / SHA-256 `4CEDB41734F9BDA28841FF2FF9D42C279C99D5B82301091AA90E5BC478F524D6E`。R955独立审计 `0299d7dfe49841f0b44eefb14c697743:126` / SHA-256 `3EA73D45A8E0E3C77934057AA9A1A6CA19B4F73A12A7490ED922DB1203EB4FEAF` 核对了原始请求/响应顺序、Native结果、accepted计数和终态，未发现重复Native响应。

## 两处caller误停

第一处，Stone转移已获Native accepted，但旧caller把整组审计记录误要求为一条；实际是提交意图和唯一Native响应各一条。R942根核计为一次accepted（external `1`、lifetime `503`），没有重放。R941中序号15为commit-intent、16为唯一 `commit_transfer` 响应。

第二处，caller把Stone6假定为Storage列表末项；Native DTO省略空缓冲的物理ID，Stone出现在缓冲项顺序的中间，因而本地“最后一格”断言失败。R944停止文本为 `Exact disconnected final custody grid Stone6 required.`，这不是Native拒绝。R945/R947随后只读核原终态、Stone与既有29个非空 buffer 及配置、Oil261保全，不重放原动作。

## caller修复与后续阶段

R953 caller改为允许提交意图加唯一响应两条证据，先按正向ACK记数并持久化，再做哈希和读回；Stone插入缓冲项的位置通过完整既有buffers比较确定，不再假定在数组尾端。真实 `pwsh -File` 零Game smoke包括7个正例、7个拒绝、两种wrapper ACK/提交后I/O错误情形，以及Stone中间插入fixture和原物料变化拒绝；未写入合成Native状态。

R954在原fixed-20窗口（opening lifetime `502`）继续：37个请求、`24.7983756 s`；本地准备 `1.5003485 s`，首个业务prepare前 `1.5056065 s`。Warp与Ti Ingot有限转入、第一段西侧Move成功；下一Move由Native以 `position_stalled` 结束（180 ticks内进展不足0.75 m，距目标尚有 `20.2328529 m`）。该失败目标不重放。R955核对了原始记录；R956四次只读通过、零accepted，用于覆盖保存阶段前核验。

## 流程核验边界

R932将同一批13份原始回执的完整本地审计由 `32272.0965 ms` 降至 `18112.3251 ms`，精确逐件读取、8 MiB单件上限、JsonNode全属性比较、解析后不可变缓存及退役旧运行号枚举入口仍在使用。这只是同一批原件的本地复核，不代表全业务流程或游戏等待提速。R954从准备计时到首prepare约 `1.51 s`；近期误停说明caller与Native DTO/ACK语义对齐仍是实际耗时来源，不能因本地审计缩短就称端到端已提速。

R960普通保存原件 `8f28425b9c0c4eacbd7a5f9dfe405972:21` / SHA-256 `F37697EB4426FF200051B31CDB7083E815B09AFEDC1BB41317A851BA452A9531` 为15个请求、`3887.7234 ms`、新增accepted 1。Root R961独立索引 `0299d7dfe49841f0b44eefb14c697743:130` / SHA-256 `F33C406BEFB7273C00580B8118A49272816D1B3B597885785B8D4B456AC9C488`，核对15份原始Native记录、唯一Save意图/ACK/终态、Stone6与Oil261保管、受影响实体、P104本地电网和完整Journal。最终普通保存为tick `108735104`、revision `133`、durable Journal `102`；R961确认当前动作均已保存覆盖，无在途或未知。窗口仍开放且冻结：opening lifetime `502`，当前 `7/20`、lifetime `509`；不封窗、不转移未用额度。

## 端到端阶段间隔

R962时间索引 `0299d7dfe49841f0b44eefb14c697743:131` / SHA-256 `2B6BE08FE3D3FDF5C88D3D9EE20294A69B34AD0C9B4FF89DBB51F5FA01AA5393`汇总了先前独立核验的阶段终止时间。R941误停至R950保存终止间隔为 `18.622195685 min`；R954 Native Move失败至R960覆盖保存终止间隔为 `8.296573515 min`；R941至R960总跨度为 `38.06350352 min`。六个列出的caller运行耗时合计 `36.3701882 s`，但不是这段期间的全部Game/RPC调用，不能与总跨度相减后精确归因给root或模型，也不是可直接比较的旧/新完整流程基线。它显示异常处理和分段交接仍消耗壁钟；既有读取/缓存、fixed-20和事实CI并行优化仍在使用，但局部32.27→18.11秒的审计时间下降不能外推为整体端到端已经提速。
