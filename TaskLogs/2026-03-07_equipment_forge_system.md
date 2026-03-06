# 装备锻造 + 套装共鸣系统 — 实装记录 (2026-03-07)

## 新增文件

| 文件 | 功能 |
|------|------|
| `ForgeSystem.cs` | 铁砧强化核心（概率曲线 / 金币公式 / 保护卷轴） |
| `ISetPassive.cs` | 套装被动统一接口 |
| `SetPassiveBase.cs` | 被动抽象基类（减少样板代码） |
| `SetResonanceDefinition_SO.cs` | 套装 SO 数据容器（词缀匹配 + 参数外置） |
| `SetResonanceEngine.cs` | 共鸣总调度器（匹配→激活→属性汇总） |
| `Passives/BerserkerSetPassive.cs` | #1 狂战士之怒 |
| `Passives/StargazerSetPassive.cs` | #2 星体观测者 |
| `Passives/AegisSetPassive.cs` | #3 不毁钢屏障 |
| `Passives/SummonerSetPassive.cs` | #4 召唤使仪典（空壳） |
| `Passives/ChronoSetPassive.cs` | #5 时光刺客信条 |
| `Passives/GlacialSetPassive.cs` | #6 极寒暴君 |
| `Passives/PlutocratSetPassive.cs` | #7 财阀黑心科技 |
| `Editor/SetResonanceAssetGenerator.cs` | 批量生成 7 套 SO |

## 修改文件

| 文件 | 变更 |
|------|------|
| `EquipmentData.cs` | 新增 `enhanceLevel` + `ToStatBlock()` 强化乘算 |
| `HeroEquipmentManager.cs` | 集成 `SetResonanceEngine`，`OnEquipmentChanged` 自动重算 |

---

## 使用步骤

1. Unity 菜单 → `EscapeTheTower → 生成套装共鸣 SO`
2. 在 Hero 的 `HeroEquipmentManager` Inspector 中将 7 个 SO 拖入 `_setDefinitions` 数组
3. 运行游戏即可自动检测套装共鸣

## 待手动测试

| # | 项目 | 预期 |
|---|------|------|
| 1 | 强化 +1~+3 | 100% 成功 |
| 2 | 强化概率衰减 | +4 起开始有失败 |
| 3 | 失败降级 | +4 以上失败降 1 级 |
| 4 | 穿戴含指定词缀的 2 件装备 | Console 输出共鸣激活 |
| 5 | 穿戴 4 件 → 6 件 | 共鸣层级递进升级 |
| 6 | 卸除装备后共鸣件数不足 | Console 输出共鸣失效 |
