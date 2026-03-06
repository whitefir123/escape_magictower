// ============================================================================
// 套装被动 #1 — 狂战士之怒 (Fury of the Berserker)
// 物理系极速吸血流
//
// 2pc: 物理技能命中时回血 = 已损失HP × 5%
// 4pc: 每低于满血 10%，ATK + (4%×基础ATK)，攻速 +3%
// 6pc: HP<30% 时获得绝对霸体 + 普攻附加真伤(5%×已损失HP)
//
// 来源：GameData_Blueprints/07_Legendary_Equipment_System.md §三.1
// ============================================================================

using EscapeTheTower.Data;
using EscapeTheTower.Entity;
using UnityEngine;

namespace EscapeTheTower.Equipment.SetResonance.Passives
{
    public class BerserkerSetPassive : SetPassiveBase
    {
        public override string SetName => "狂战士之怒";

        // SO parameters[] 索引约定：
        // [0] = 2pc 回血比例（已损失HP%）默认 0.05
        // [1] = 4pc ATK 加成比例（每 10% 缺血） 默认 0.04
        // [2] = 4pc 攻速加成（每 10% 缺血） 默认 0.03
        // [3] = 6pc 霸体触发阈值（HP%）默认 0.30
        // [4] = 6pc 真伤比例（已损失HP%）默认 0.05

        public override StatBlock GetStatModifiers()
        {
            var block = new StatBlock();
            if (Owner == null || ActiveTier < ResonanceTier.Four) return block;

            // 4pc: 动态 ATK/攻速加成（基于当前缺血比例）
            float currentHP = Owner.CurrentStats.Get(StatType.HP);
            float maxHP = Owner.CurrentStats.Get(StatType.MaxHP);
            if (maxHP <= 0f) return block;

            float missingPct = Mathf.Clamp01(1f - currentHP / maxHP);
            int tiers = Mathf.FloorToInt(missingPct / 0.10f); // 每 10% 一档

            if (tiers > 0)
            {
                float baseATK = Owner.CurrentStats.Get(StatType.ATK);
                block.Add(StatType.ATK, tiers * 0.04f * baseATK);
                block.Add(StatType.AttackSpeed, tiers * 0.03f);
            }

            return block;
        }

        // TODO: 2pc 技能回血和 6pc 霸体/真伤附加需要通过事件钩子实现
        // 将在 EventManager 中订阅 OnDamageDealt 事件
    }
}
