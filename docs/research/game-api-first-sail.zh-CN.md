# 原生首帆能力

[English](game-api-first-sail.md) · [简体中文](game-api-first-sail.zh-CN.md)

这是所有者2026-11-01指定的0.5首帆窄扩展。原生参考为DSP `0.10.35.29151`，`Assembly-CSharp.dll` SHA-256 `EF314425DADE51F529F184F07BE136CD3C2660A6A81515603C7226FBE7AB1D8C`。原Voyager三项验收及选定基线分别保留；本源码研究不代表实机验收。

## 采用的普通路径

- `FactorySystem.NewEjectorComponent(int entityId, PrefabDesc desc)`绑定正常实体、星球、用电组件及原型帆物品。`chargeSpend`/`coldSpend`是原型帧数乘`10000`；公开周期字段保留原生原始单位，不改称秒或游戏tick。
- `DysonSwarm.ResetNew()`在正常初始化后以`NewOrbit`建立默认轨道。检查只读已存在的球/云，不调用初始化、创建轨道、`AddBullet`、`AddSolarSail`或任何模拟更新。
- `UIEjectorWindow.OnSetOrbit(int)`核验正数轨道存在并启用后，调用`EjectorComponent.SetOrbit(int)`；零清除固定目标。产品在普通prepare/commit/幂等封套内只调用一次相同业务方法。原生可能将充能转为冷却，适配器不覆盖其方向或计时。
- 此固定轨道子集要求原生自动选轨关闭。自动选轨开启时，清除优先轨道不保证停止；对此状态明确拒绝，不冒称已禁用。
- 供料复用正常制造/仓储/分拣路径。配置调用不装填或消耗帆，不改变加速或自动选轨，不主动发射，不续材料或时间额度。

## 读回与证据

详细检查新增`ejector`：精确组件/实体/星球绑定、帆数量/增产点、固定/运行轨道、自动/加速开关、周期原始进度、目标状态，以及本地恒星已存在云的轨道和帆/弹道汇总观察。配置哈希绑定静态身份/姿态和固定设置；所选轨道指纹绑定恒星、ID、启用状态、半径及旋转，排除持续变化的帆数。这些可选详细字段不进入既有工厂哈希域。

恒星弹射器计数覆盖该恒星行星记录指向的全部已驻留工厂，不创建工厂。工厂或组件归属未知/不匹配会使覆盖不完整、计数为null。这是观察覆盖，不是执行证书。弹射器/云为null或覆盖不完整均须保留未知。

`EjectorComponent.InternalUpdate(float power, long tick, DysonSwarm swarm, AstroData[] astroPoses, AnimData[] animPool, int[] consumeRegister)`正常推进充能、核原生角度和遮挡、添加原生`SailBullet`、扣除帆数量/增产点并增加实际消耗记录。`DysonSwarm.AddBullet(SailBullet,int)`分配/复用原生弹道并记录轨道，随后由原生抵达创建轨道帆。这些方法仅作研究依据；产品不调用它们发射或创建内容。

`fired`是发射后仍保留的动画标志，公开为`firedAnimationActive`且`firedAnimationIsLaunchReceipt=false`。配置成功、单次动画观察或云汇总增长均不足以归属发射。首帆验收须有有限原制造/供料记录、精确弹射器库存消耗、真实云弹道/抵达证据及来源隔离/完整覆盖，再正常保存恢复。

## 验证边界

Core回归覆盖生产请求/哈希/轨道绑定、禁用/未知/变更轨道拒绝、固定模式停止语义、模式参数冲突、不受库存波动影响的配置哈希及混合预检回显。Native/solution构建使用上述准确参考。这些测试和原生研究均不证明已安装运行、真实发射或有帆云的恢复；实机结果归Lab。
