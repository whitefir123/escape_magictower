// ============================================================================
// 逃离魔塔 - 元素反应系统 (ElementalReaction)
// 处理元素异常状态之间的复合反应。
// 七大复合反应 = 特定元素A + 元素B → 额外爆发效果
//
// 反应查找表：
//   灼烧+水 = 蒸发    (清除灼烧，水伤×2.0)
//   灼烧+冰 = 融化    (清除灼烧，冰伤×2.5)
//   灼烧+雷 = 超载    (清除灼烧+感电，3×3 AOE 真伤 = ATK×1.5)
//   潮湿+冰 = 冻结    (清除潮湿，无视CC抗性强行冰封 3s)
//   潮湿+雷 = 感电连营(不消耗潮湿，持续 5s 链式雷伤)
//   冰封+火 = 碎冰    (清除冰封，目标 MaxHP×15% 物理真伤)
//   中毒+火 = 毒爆术  (清空毒层，层数×20 的 AOE 火伤)
//
// 来源：DesignDocs/03_Combat_System.md §2.2
//       GameData_Blueprints/02_Status_Ailments.md
// ============================================================================

using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Data;
using EscapeTheTower.Entity;

namespace EscapeTheTower.Combat
{
    /// <summary>
    /// 元素反应处理器 —— 静态工具类
    /// </summary>
    public static class ElementalReaction
    {
        // === 反应伤害常量 ===
        private const float OVERLOAD_ATK_SCALING = 1.5f;         // 超载 AOE = ATK × 1.5
        private const float SHATTER_MAX_HP_RATIO = 0.15f;        // 碎冰 = MaxHP × 15%
        private const float VENOM_BLAST_PER_STACK = 20f;         // 毒爆每层 = 20 伤害
        private const float ELECTRO_CHAIN_ATK_SCALING = 0.3f;    // 感电链伤 = ATK × 0.3
        private const int   ELECTRO_CHAIN_MAX_TARGETS = 3;       // 感电最多溅射 3 个目标
        private const float ELECTRO_CHAIN_RANGE = 3f;            // 感电溅射范围（格）

        /// <summary>
        /// 检查并触发元素反应。
        /// 当一个新的元素效果施加到已有元素状态的目标上时调用。
        /// </summary>
        /// <param name="target">目标的状态效果管理器</param>
        /// <param name="incomingElement">新施加的元素类型</param>
        /// <param name="targetEntityID">目标实体 ID</param>
        /// <param name="attackerID">攻击方实体 ID</param>
        /// <param name="attackerATK">攻击方 ATK 值（用于部分反应伤害计算）</param>
        /// <returns>触发的反应类型，None 表示无反应</returns>
        public static ElementalReactionType CheckAndTrigger(
            StatusEffectManager target,
            ElementType incomingElement,
            int targetEntityID,
            int attackerID,
            float attackerATK = 0f)
        {
            if (target == null) return ElementalReactionType.None;

            var reaction = ElementalReactionType.None;

            // =================================================================
            //  火系组合
            // =================================================================
            if (incomingElement == ElementType.Fire)
            {
                // 碎冰 = 冰封(冰) + 火 → 巨额物理真伤
                if (target.HasEffect(StatusEffectType.Frozen))
                {
                    reaction = ElementalReactionType.Shatter;
                    target.RemoveEffect(StatusEffectType.Frozen);
                }
                // 超载 = 灼烧(火) + 雷（感电标记） + 火 → 真实 AOE
                // 注意：超载的设计是 灼烧+雷。但感电挂在目标身上也可被火引爆
                else if (target.HasEffect(StatusEffectType.Shock))
                {
                    reaction = ElementalReactionType.Overload;
                    target.RemoveEffect(StatusEffectType.Shock);
                    target.RemoveEffect(StatusEffectType.Burn);
                }
                // 毒爆术 = 中毒(毒) + 火 → 清空毒层 AOE 绿火
                else if (target.HasEffect(StatusEffectType.Poison))
                {
                    reaction = ElementalReactionType.VenomBlast;
                    // 毒爆在 ExecuteReactionDamage 中清空毒层
                }
            }
            // =================================================================
            //  水系组合
            // =================================================================
            else if (incomingElement == ElementType.Water)
            {
                // 先施加潮湿状态
                target.ApplyEffect(StatusEffectType.Wet, 8f, 0f, attackerID);

                // 蒸发 = 灼烧(火) + 水 → 水伤翻倍
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
                // 冻结 = 潮湿(水) + 冰 → 无视抗性强行冰封
                if (target.HasEffect(StatusEffectType.Wet))
                {
                    reaction = ElementalReactionType.Freeze;
                    target.RemoveEffect(StatusEffectType.Wet);
                    // 无视 CC 抗性递减，强行施加 3 秒冰封
                    target.ApplyEffect(StatusEffectType.Frozen, 3f, 0f, attackerID);
                }
                // 融化 = 灼烧(火) + 冰 → 超高乘区
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
                // 感电连营 = 潮湿(水) + 雷 → 持续链式雷伤（不消耗潮湿）
                if (target.HasEffect(StatusEffectType.Wet))
                {
                    reaction = ElementalReactionType.ElectroCharged;
                    // 【关键】不消耗潮湿状态，允许持续触发
                    target.ApplyEffect(StatusEffectType.Shock, 5f, 0f, attackerID);
                }
                // 超载 = 灼烧(火) + 雷 → AOE 真实爆炸
                else if (target.HasEffect(StatusEffectType.Burn))
                {
                    reaction = ElementalReactionType.Overload;
                    target.RemoveEffect(StatusEffectType.Burn);
                }
            }

            // 如果触发了反应，广播事件并执行反应伤害
            if (reaction != ElementalReactionType.None)
            {
                EventManager.Publish(new OnElementalReactionEvent
                {
                    Meta = new EventMeta(attackerID),
                    TargetEntityID = targetEntityID,
                    ReactionType = reaction,
                });

                // 执行反应附带的伤害效果
                var targetEntity = target.GetComponent<EntityBase>();
                ExecuteReactionDamage(reaction, targetEntity, target, attackerID, attackerATK);

                Debug.Log($"[元素反应] 💥 {reaction} 在实体 {targetEntityID} 上触发！");
            }

            return reaction;
        }

        // =====================================================================
        //  反应伤害执行
        // =====================================================================

        /// <summary>
        /// 根据反应类型执行附带的伤害/控制效果
        /// </summary>
        private static void ExecuteReactionDamage(
            ElementalReactionType reaction,
            EntityBase targetEntity,
            StatusEffectManager targetStatus,
            int attackerID,
            float attackerATK)
        {
            if (targetEntity == null) return;

            switch (reaction)
            {
                case ElementalReactionType.Overload:
                    // 超载：以目标为中心 3×3 AOE 真实伤害 = ATK × 1.5
                    float overloadDmg = attackerATK * OVERLOAD_ATK_SCALING;
                    ApplyAOEDamage(targetEntity.transform.position, overloadDmg,
                                   DamageType.True, attackerID, 1.5f);
                    Debug.Log($"[元素反应] 超载爆炸！AOE 真伤={overloadDmg:F1}");
                    break;

                case ElementalReactionType.Shatter:
                    // 碎冰：目标 MaxHP × 15% 的物理真伤
                    float shatterDmg = targetEntity.CurrentStats.Get(StatType.MaxHP)
                                       * SHATTER_MAX_HP_RATIO;
                    var shatterResult = new DamageResult
                    {
                        FinalDamage = shatterDmg,
                        DamageType = DamageType.True,
                        IsCritical = false,
                    };
                    targetEntity.TakeDamage(shatterResult, attackerID);
                    Debug.Log($"[元素反应] 碎冰！真伤={shatterDmg:F1}");
                    break;

                case ElementalReactionType.VenomBlast:
                    // 毒爆术：清空毒层，层数 × 20 的 AOE 火伤
                    float venomDmg = CalculateVenomBlastDamage(targetStatus);
                    ApplyAOEDamage(targetEntity.transform.position, venomDmg,
                                   DamageType.Magical, attackerID, 1.5f);
                    Debug.Log($"[元素反应] 毒爆术！AOE 火伤={venomDmg:F1}");
                    break;

                // 蒸发/融化：伤害倍率已在 GetReactionDamageMultiplier 中提供
                // 由攻击方在伤害计算时乘算，此处无需额外执行
                case ElementalReactionType.Vaporize:
                case ElementalReactionType.Melt:
                    break;

                // 冻结：已在 CheckAndTrigger 中施加冰封状态
                case ElementalReactionType.Freeze:
                    break;

                // 感电连营：多目标链式雷伤由 Shock 状态的持续 tick 处理
                // 此处无额外即时伤害
                case ElementalReactionType.ElectroCharged:
                    break;
            }
        }

        /// <summary>
        /// 获取元素反应的伤害倍率加成（供攻击方乘算使用）
        /// </summary>
        public static float GetReactionDamageMultiplier(ElementalReactionType reaction)
        {
            switch (reaction)
            {
                case ElementalReactionType.Vaporize:        return 2.0f;    // 蒸发：水伤翻倍
                case ElementalReactionType.Melt:            return 2.5f;    // 融化：超高乘区
                case ElementalReactionType.Overload:        return 1.0f;    // 超载：伤害在 AOE 中直接结算
                case ElementalReactionType.Freeze:          return 1.0f;    // 冻结：无额外伤害，靠控制
                case ElementalReactionType.ElectroCharged:  return 1.0f;    // 感电：链伤由 Shock tick 处理
                case ElementalReactionType.Shatter:         return 1.0f;    // 碎冰：伤害在 Execute 中直接结算
                case ElementalReactionType.VenomBlast:      return 1.0f;    // 毒爆：伤害在 Execute 中直接结算
                default:                                    return 1.0f;
            }
        }

        /// <summary>
        /// 计算毒爆术的毒层伤害
        /// 清空所有毒层 → 层数 × 20 = 总 AOE 伤害
        /// </summary>
        public static float CalculateVenomBlastDamage(StatusEffectManager target)
        {
            int poisonStacks = target.GetStacks(StatusEffectType.Poison);

            // 清空毒层
            target.RemoveEffect(StatusEffectType.Poison);

            // 毒爆伤害 = 层数 × 20
            return poisonStacks * VENOM_BLAST_PER_STACK;
        }

        // =====================================================================
        //  AOE 伤害工具
        // =====================================================================

        /// <summary>
        /// 以指定位置为中心，对范围内的所有敌方实体造成伤害
        /// </summary>
        /// <param name="center">爆炸中心（世界坐标）</param>
        /// <param name="damage">伤害值</param>
        /// <param name="damageType">伤害类型</param>
        /// <param name="attackerID">攻击方实体 ID</param>
        /// <param name="radius">AOE 半径（格数）</param>
        private static void ApplyAOEDamage(Vector3 center, float damage,
            DamageType damageType, int attackerID, float radius)
        {
            // 查找范围内的所有实体
            var allEntities = Object.FindObjectsByType<EntityBase>(FindObjectsSortMode.None);
            float sqrRadius = radius * radius;

            // 确定攻击方阵营 → AOE 只打对立阵营
            Faction attackerFaction = Faction.Neutral;
            foreach (var e in allEntities)
            {
                if (e.EntityID == attackerID)
                {
                    attackerFaction = e.Faction;
                    break;
                }
            }

            foreach (var entity in allEntities)
            {
                if (!entity.IsAlive) continue;

                // 阵营过滤：只打对立阵营（玩家的 AOE 打怪物，怪物的 AOE 打玩家）
                if (attackerFaction == Faction.Player && entity.Faction != Faction.Enemy) continue;
                if (attackerFaction == Faction.Enemy && entity.Faction != Faction.Player) continue;

                // 计算距离（格子坐标系 = 单位坐标系）
                float sqrDist = (entity.transform.position - center).sqrMagnitude;
                if (sqrDist > sqrRadius) continue;

                var result = new DamageResult
                {
                    FinalDamage = damage,
                    DamageType = damageType,
                    IsCritical = false,
                };
                entity.TakeDamage(result, attackerID);
            }
        }
    }
}
