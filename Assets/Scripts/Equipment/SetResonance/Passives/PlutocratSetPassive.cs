// ============================================================================
// 套装被动 #7 — 财阀的黑心科技 (Plutocrat's Dark Tech)
// 金币轰炸流
//
// 2pc: 每 1000 金币 → 体积 +2%，抗击退提升（上限 10 层）
// 4pc: 受击 20% 概率不掉血 → 金币炸弹 AOE
// 6pc: 闪避抛硬币致盲 + 金币真伤（代价：1% 总金币）
//
// 来源：GameData_Blueprints/07_Legendary_Equipment_System.md §三.7
// ============================================================================

using EscapeTheTower.Data;

namespace EscapeTheTower.Equipment.SetResonance.Passives
{
    public class PlutocratSetPassive : SetPassiveBase
    {
        public override string SetName => "财阀的黑心科技";

        /// <summary>
        /// 2pc 金币层数（供外部系统查询当前体积/抗击退加成）
        /// </summary>
        public int GoldTiers
        {
            get
            {
                if (ActiveTier < ResonanceTier.Two) return 0;
                // 需要从玩家背包系统读取当前金币数
                // 暂时返回 0，待背包系统接入后实现
                return 0;
            }
        }

        public override StatBlock GetStatModifiers()
        {
            // 金币类加成全部通过行为钩子实现，不直接修正属性
            return new StatBlock();
        }

        // TODO: 全部效果需要金币系统接口 + 事件钩子
    }
}
