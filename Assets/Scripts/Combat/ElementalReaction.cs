// ============================================================================
// 逃离魔塔 - 元素反应系统 (ElementalReaction)
// 处理元素异常状态之间的复合反应。
// 七大复合反应 = 特定元素A + 元素B → 额外爆发效果
//
// 来源：DesignDocs/03_Combat_System.md 第 1.3.4 节
//       GameData_Blueprints/02_Status_Ailments.md
// ============================================================================

using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Data;

namespace EscapeTheTower.Combat
{
    /// <summary>
    /// 元素反应处理器 —— 静态工具类
    /// </summary>
    public static class ElementalReaction
    {
        /// <summary>
        /// 检查并触发元素反应
        /// 当一个新的元素效果施加到已有元素状态的目标上时调用
        /// </summary>
        /// <param name="target">目标的状态效果管理器</param>
        /// <param name="incomingElement">新施加的元素类型</param>
        /// <param name="targetEntityID">目标实体 ID</param>
        /// <param name="attackerID">攻击方实体 ID</param>
        /// <returns>触发的反应类型，None 表示无反应</returns>
        public static ElementalReactionType CheckAndTrigger(
            StatusEffectManager target,
            ElementType incomingElement,
            int targetEntityID,
            int attackerID)
        {
            if (target == null) return ElementalReactionType.None;

            var reaction = ElementalReactionType.None;

            // =================================================================
            //  火系组合
            // =================================================================
            if (incomingElement == ElementType.Fire)
            {
                // 蒸发 = 潮湿 + 火 → 水伤翻倍
                if (target.HasEffect(StatusEffectType.Wet))
                {
                    reaction = ElementalReactionType.Vaporize;
                    target.RemoveEffect(StatusEffectType.Wet);
                }
                // 融化 = 冰封 + 火 → 超高乘区一击（碎冰变体）
                else if (target.HasEffect(StatusEffectType.Frozen))
                {
                    reaction = ElementalReactionType.Shatter;
                    target.RemoveEffect(StatusEffectType.Frozen);
                }
                // 超载 = 感电 + 火 → 无视防御 AOE 真实伤害
                else if (target.HasEffect(StatusEffectType.Shock))
                {
                    reaction = ElementalReactionType.Overload;
                    target.RemoveEffect(StatusEffectType.Shock);
                }
                // 毒爆术 = 中毒 + 火 → 清空毒层 AOE 绿火
                else if (target.HasEffect(StatusEffectType.Poison))
                {
                    reaction = ElementalReactionType.VenomBlast;
                    // 毒爆在执行层清空毒层
                }
            }
            // =================================================================
            //  水系组合
            // =================================================================
            else if (incomingElement == ElementType.Water)
            {
                // 施加潮湿状态
                target.ApplyEffect(StatusEffectType.Wet, 8f, 0f, attackerID);

                // 蒸发 = 火 + 水 → 水伤翻倍
                if (target.HasEffect(StatusEffectType.Burn))
                {
                    reaction = ElementalReactionType.Vaporize;
                    target.RemoveEffect(StatusEffectType.Burn);
                }
            }
            // =================================================================
            //  冰系组合
            // =================================================================
            else if (incomingElement == ElementType.Ice)
            {
                // 冻结 = 潮湿 + 冰 → 无视抗性强行冰封
                if (target.HasEffect(StatusEffectType.Wet))
                {
                    reaction = ElementalReactionType.Freeze;
                    target.RemoveEffect(StatusEffectType.Wet);
                    // 施加冰封
                    target.ApplyEffect(StatusEffectType.Frozen, 3f, 0f, attackerID);
                }
                // 融化 = 火 + 冰 → 超高乘区
                else if (target.HasEffect(StatusEffectType.Burn))
                {
                    reaction = ElementalReactionType.Melt;
                    target.RemoveEffect(StatusEffectType.Burn);
                }
            }
            // =================================================================
            //  雷系组合
            // =================================================================
            else if (incomingElement == ElementType.Lightning)
            {
                // 感电连营 = 潮湿 + 雷 → 持续链式雷伤
                if (target.HasEffect(StatusEffectType.Wet))
                {
                    reaction = ElementalReactionType.ElectroCharged;
                    target.RemoveEffect(StatusEffectType.Wet);
                    target.ApplyEffect(StatusEffectType.Shock, 5f, 0f, attackerID);
                }
                // 超载 = 火 + 雷 → AOE 真实爆炸
                else if (target.HasEffect(StatusEffectType.Burn))
                {
                    reaction = ElementalReactionType.Overload;
                    target.RemoveEffect(StatusEffectType.Burn);
                }
            }

            // 如果触发了反应，广播事件
            if (reaction != ElementalReactionType.None)
            {
                EventManager.Publish(new OnElementalReactionEvent
                {
                    Meta = new EventMeta(attackerID),
                    TargetEntityID = targetEntityID,
                    ReactionType = reaction,
                });

                Debug.Log($"[元素反应] {reaction} 在实体 {targetEntityID} 上触发！");
            }

            return reaction;
        }

        /// <summary>
        /// 获取元素反应的伤害倍率加成
        /// </summary>
        public static float GetReactionDamageMultiplier(ElementalReactionType reaction)
        {
            switch (reaction)
            {
                case ElementalReactionType.Vaporize:        return 2.0f;    // 蒸发：水伤翻倍
                case ElementalReactionType.Melt:            return 2.5f;    // 融化：超高乘区
                case ElementalReactionType.Overload:        return 1.5f;    // 超载：AOE 基础倍率
                case ElementalReactionType.Freeze:          return 1.0f;    // 冻结：无额外伤害，靠控制
                case ElementalReactionType.ElectroCharged:  return 1.2f;    // 感电：持续链伤
                case ElementalReactionType.Shatter:         return 3.0f;    // 碎冰：巨额物理
                case ElementalReactionType.VenomBlast:      return 1.0f;    // 毒爆：伤害由毒层决定
                default:                                    return 1.0f;
            }
        }

        /// <summary>
        /// 获取毒爆术的毒层伤害
        /// 清空所有毒层 → 层数 * 每层值 * 3 = 总AOE伤害
        /// </summary>
        public static float CalculateVenomBlastDamage(StatusEffectManager target)
        {
            int poisonStacks = target.GetStacks(StatusEffectType.Poison);
            float totalValue = target.GetTotalValue(StatusEffectType.Poison);

            // 清空毒层
            target.RemoveEffect(StatusEffectType.Poison);

            // 毒爆伤害 = 总蓄积毒伤 × 3
            return totalValue * 3f;
        }
    }
}
