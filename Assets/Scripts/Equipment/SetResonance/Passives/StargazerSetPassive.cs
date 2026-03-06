// ============================================================================
// 套装被动 #2 — 星体观测者 (Stargazer's Wisdom)
// 法系必爆冷却流
//
// 2pc: 每溢出 10 点蓝 → 1.5% 全局魔法暴伤
// 4pc: 大招后 4s 全法术必暴（内置 10s CD）
// 6pc: 硬控目标无视 50% MDEF + 技能击杀返 80% 蓝
//
// 来源：GameData_Blueprints/07_Legendary_Equipment_System.md §三.2
// ============================================================================

using EscapeTheTower.Data;

namespace EscapeTheTower.Equipment.SetResonance.Passives
{
    public class StargazerSetPassive : SetPassiveBase
    {
        public override string SetName => "星体观测者";

        public override StatBlock GetStatModifiers()
        {
            var block = new StatBlock();
            if (Owner == null || ActiveTier < ResonanceTier.Two) return block;

            // 2pc: 溢出蓝 → 暴伤
            float currentMP = Owner.CurrentStats.Get(StatType.MP);
            float maxMP = Owner.CurrentStats.Get(StatType.MaxMP);
            if (currentMP > maxMP && maxMP > 0f)
            {
                float overflow = currentMP - maxMP;
                float bonusCritDmg = (overflow / 10f) * 0.015f;
                block.Add(StatType.CritMultiplier, bonusCritDmg);
            }

            return block;
        }

        // TODO: 4pc 必暴窗口和 6pc MDEF 穿透/返蓝需事件钩子
    }
}
