# 元素反应系统 — 实装记录 (2026-03-06)

## 变更文件

| 文件 | 变更 |
|------|------|
| `ElementalReaction.cs` | 重写 7 反应规则 + `ExecuteReactionDamage()` + `ApplyAOEDamage()`（含阵营过滤） |
| `StatusEffectManager.cs` | `ApplyEffect()` 自动触发反应检测 + 递归防护 + `StatusEffectToElement()` + 虚弱属性修正 |
| `EntityBase.cs` | `TickDOT()` 新增 Shock 感电 tick + `TickShockChainDamage()` 溅射逻辑 |
| `MonsterBase.cs` | `PerformAttack()` / `OnHitByPlayer()` 致盲 Miss 检查 |
| `HeroCombatHandler.cs` | 移除显式 `CheckAndTrigger` 调用，反应统一走 `ApplyEffect()` 内部检测 |

---

## 待手动测试清单

> 以下项目需在 Unity Editor 中运行时验证。

| # | 测试场景 | 预期结果 | 状态 |
|---|---------|---------|------|
| 1 | 怪物受中毒 3 层 | Console 每秒输出 `[DOT] Poison 伤害`，值 = 3×valuePerStack | 🔲 |
| 2 | 怪物受灼烧 | Console 每秒输出 `[DOT] Burn 伤害`，值 = 层数×MaxHP×5% | 🔲 |
| 3 | 怪物受流血后移动 | 流血伤害 ×2.5 | 🔲 |
| 4 | 先给怪物 Poison，再打火 | 触发毒爆 `VenomBlast`，AOE 仅打怪物不打玩家 | 🔲 |
| 5 | 先给怪物 Wet，再 Ice | 触发冻结 `Freeze`，怪物被冰封 3s | 🔲 |
| 6 | 先给怪物 Wet，再 Lightning | 触发感电 `ElectroCharged`，Wet 不被移除 | 🔲 |
| 7 | 怪物身上有 Shock 状态 | 每秒对自身 + 周围同阵营实体造成雷伤 | 🔲 |
| 8 | 给怪物 Blind | 怪物攻击和反击均 Miss | 🔲 |
| 9 | 超载 AOE 范围 | 仅怪物受伤，玩家不受 AOE | 🔲 |
| 10 | 给怪物 Weakened 后攻击 | 怪物 ATK/MATK 降低，可在属性面板观察 | 🔲 |
| 11 | 灼烧中治疗 | 治疗量被灼烧层数削减 | 🔲 |
