// ============================================================================
// 逃离魔塔 - 状态效果管理器 (StatusEffectManager)
// 管理所有实体身上的 Buff/Debuff/DOT/CC 状态效果。
// 遵循状态异常核心规则库 (GameData_Blueprints/02_Status_Ailments.md)：
//   - 无限叠加型：中毒/灼烧/流血/破甲/魔碎/虚弱（层数无限，+1 重置计时）
//   - 有上限叠加型：减速（最高 80%）
//   - 不可叠加型：致盲/眩晕/冰封/定身/沉默/魅惑/加速/霸体（刷新时间）
//   - 数值叠加型：护盾（数值累加）
//   - 硬控抗性递减：眩晕/冰封/魅惑（Boss 专用）
//
// 来源：DesignDocs/09_DataFlow_and_Status.md 第二节
//       GameData_Blueprints/02_Status_Ailments.md
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Data;

namespace EscapeTheTower.Combat
{
    /// <summary>
    /// 状态效果实例 —— 运行时存储的单个状态效果数据
    /// </summary>
    public class StatusEffectInstance
    {
        /// <summary>效果类型</summary>
        public StatusEffectType EffectType;

        /// <summary>叠加规则</summary>
        public StackingRule Rule;

        /// <summary>当前叠层数（无限/有上限叠加型使用）</summary>
        public int Stacks;

        /// <summary>剩余持续时间（秒）</summary>
        public float RemainingDuration;

        /// <summary>每层每秒伤害/效果值</summary>
        public float ValuePerStack;

        /// <summary>有上限叠加型的最大层数</summary>
        public int MaxStacks;

        /// <summary>施加者实体 ID（用于事件溯源）</summary>
        public int ApplierID;
    }

    /// <summary>
    /// 状态效果管理器 —— 每个 EntityBase 持有一个
    /// </summary>
    public class StatusEffectManager : MonoBehaviour
    {
        private readonly List<StatusEffectInstance> _activeEffects = new List<StatusEffectInstance>();

        /// <summary>免疫的状态效果类型集合</summary>
        private readonly HashSet<StatusEffectType> _immunities = new HashSet<StatusEffectType>();

        /// <summary>防止 ApplyEffect → CheckAndTrigger → ApplyEffect 无限递归</summary>
        private bool _isProcessingReaction;

        /// <summary>所有当前活跃的状态效果（只读视图）</summary>
        public IReadOnlyList<StatusEffectInstance> ActiveEffects => _activeEffects;

        /// <summary>
        /// 设置免疫列表（由 MonsterBase.Initialize 调用）
        /// </summary>
        public void SetImmunities(StatusEffectType[] immuneTypes)
        {
            _immunities.Clear();
            if (immuneTypes != null)
            {
                foreach (var t in immuneTypes)
                    _immunities.Add(t);
            }
        }

        // =====================================================================
        //  查表：每种效果的叠加规则
        // =====================================================================

        /// <summary>
        /// 获取指定效果类型的叠加规则
        /// </summary>
        public static StackingRule GetStackingRule(StatusEffectType type)
        {
            switch (type)
            {
                // 无限叠加型
                case StatusEffectType.Poison:
                case StatusEffectType.Burn:
                case StatusEffectType.Bleed:
                case StatusEffectType.ArmorBreak:
                case StatusEffectType.MagicShred:
                case StatusEffectType.Weakened:
                    return StackingRule.InfiniteStackable;

                // 有上限叠加型
                case StatusEffectType.Slowed:
                    return StackingRule.CappedStackable;

                // 数值叠加型
                case StatusEffectType.Barrier:
                    return StackingRule.ValueStackable;

                // 不可叠加型（其余全部）
                default:
                    return StackingRule.NonStackable;
            }
        }

        /// <summary>
        /// 获取有上限叠加型的最大层数
        /// </summary>
        private static int GetMaxStacks(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Slowed: return 80; // 每层 1% 减速，最高 80%
                default: return 999;
            }
        }

        // =====================================================================
        //  施加状态效果
        // =====================================================================

        /// <summary>
        /// 施加状态效果
        /// </summary>
        /// <param name="effectType">效果类型</param>
        /// <param name="duration">持续时间（秒）</param>
        /// <param name="valuePerStack">每层每秒效果值</param>
        /// <param name="applierID">施加者实体 ID</param>
        /// <param name="stacks">初始叠层数（默认 1）</param>
        public void ApplyEffect(StatusEffectType effectType, float duration, float valuePerStack, int applierID, int stacks = 1)
        {
            // 霸体检查：霸体状态下免疫控制效果
            var entity = GetComponent<EscapeTheTower.Entity.EntityBase>();
            if (entity != null && entity.HasSuperArmor && IsControlEffect(effectType))
            {
                Debug.Log($"[StatusEffect] {gameObject.name} 霸体中，免疫 {effectType}");
                return;
            }

            // 免疫检查
            if (_immunities.Contains(effectType))
            {
                Debug.Log($"[StatusEffect] 效果 {effectType} 被免疫，施加失败。");
                return;
            }

            var rule = GetStackingRule(effectType);

            // 查找是否已存在同类型效果
            var existing = _activeEffects.Find(e => e.EffectType == effectType);

            if (existing != null)
            {
                switch (rule)
                {
                    case StackingRule.NonStackable:
                        // 刷新持续时间（取最长）
                        existing.RemainingDuration = Mathf.Max(existing.RemainingDuration, duration);
                        existing.ApplierID = applierID;
                        break;

                    case StackingRule.InfiniteStackable:
                        // 层数 +1，重置计时器
                        existing.Stacks += stacks;
                        existing.RemainingDuration = duration;
                        existing.ApplierID = applierID;
                        break;

                    case StackingRule.CappedStackable:
                        // 层数 +1 但不超过上限，重置计时器
                        existing.Stacks = Mathf.Min(existing.Stacks + stacks, existing.MaxStacks);
                        existing.RemainingDuration = duration;
                        existing.ApplierID = applierID;
                        break;

                    case StackingRule.ValueStackable:
                        // 数值累加
                        existing.ValuePerStack += valuePerStack * stacks;
                        existing.RemainingDuration = Mathf.Max(existing.RemainingDuration, duration);
                        existing.ApplierID = applierID;
                        break;
                }
            }
            else
            {
                // 新增效果
                var newEffect = new StatusEffectInstance
                {
                    EffectType = effectType,
                    Rule = rule,
                    Stacks = stacks,
                    RemainingDuration = duration,
                    ValuePerStack = valuePerStack,
                    MaxStacks = GetMaxStacks(effectType),
                    ApplierID = applierID,
                };
                _activeEffects.Add(newEffect);
            }

            // 元素反应检测：当施加的状态属于某种元素时，
            // 检查目标身上是否存在可与之形成反应的前置状态
            // 使用 _isProcessingReaction 防止递归（反应内部可能再次调用 ApplyEffect）
            if (!_isProcessingReaction)
            {
                var element = StatusEffectToElement(effectType);
                if (element != ElementType.None)
                {
                    _isProcessingReaction = true;
                    try
                    {
                        var targetEntity = GetComponent<EscapeTheTower.Entity.EntityBase>();
                        int targetID = targetEntity != null ? targetEntity.EntityID : 0;
                        ElementalReaction.CheckAndTrigger(this, element, targetID, applierID);
                    }
                    finally
                    {
                        _isProcessingReaction = false;
                    }
                }
            }
        }

        // =====================================================================
        //  每帧更新
        // =====================================================================

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = _activeEffects[i];

                // 倒计时
                effect.RemainingDuration -= dt;

                // 到期则移除
                if (effect.RemainingDuration <= 0f)
                {
                    _activeEffects.RemoveAt(i);
                    continue;
                }

                // DOT 类效果每秒结算伤害（由外部战斗系统读取 ActiveEffects 来结算）
                // 此处仅负责生命周期管理，不直接扣血
            }
        }

        // =====================================================================
        //  查询接口
        // =====================================================================

        /// <summary>
        /// 是否存在指定类型的活跃效果
        /// </summary>
        public bool HasEffect(StatusEffectType type)
        {
            return _activeEffects.Exists(e => e.EffectType == type);
        }

        /// <summary>
        /// 获取指定类型效果的当前层数
        /// </summary>
        public int GetStacks(StatusEffectType type)
        {
            var effect = _activeEffects.Find(e => e.EffectType == type);
            return effect?.Stacks ?? 0;
        }

        /// <summary>
        /// 获取指定类型效果的总效果值（层数 * 每层值）
        /// </summary>
        public float GetTotalValue(StatusEffectType type)
        {
            var effect = _activeEffects.Find(e => e.EffectType == type);
            if (effect == null) return 0f;
            return effect.Stacks * effect.ValuePerStack;
        }

        /// <summary>
        /// 是否处于硬控状态（眩晕/冰封/定身/魅惑）
        /// </summary>
        public bool IsHardCCed()
        {
            return HasEffect(StatusEffectType.Stun) ||
                   HasEffect(StatusEffectType.Frozen) ||
                   HasEffect(StatusEffectType.Root) ||
                   HasEffect(StatusEffectType.Charm);
        }

        /// <summary>
        /// 是否处于沉默状态
        /// </summary>
        public bool IsSilenced()
        {
            return HasEffect(StatusEffectType.Silence);
        }

        /// <summary>
        /// 是否拥有霸体（免疫控制）
        /// </summary>
        public bool IsUnstoppable()
        {
            return HasEffect(StatusEffectType.Unstoppable);
        }

        // =====================================================================
        //  清除接口
        // =====================================================================

        /// <summary>
        /// 移除指定类型的效果
        /// </summary>
        public void RemoveEffect(StatusEffectType type)
        {
            _activeEffects.RemoveAll(e => e.EffectType == type);
        }

        /// <summary>
        /// 清除所有 Debuff（霸体激活时调用）
        /// </summary>
        public void ClearAllDebuffs()
        {
            _activeEffects.RemoveAll(e =>
                e.EffectType == StatusEffectType.Stun ||
                e.EffectType == StatusEffectType.Frozen ||
                e.EffectType == StatusEffectType.Root ||
                e.EffectType == StatusEffectType.Silence ||
                e.EffectType == StatusEffectType.Charm ||
                e.EffectType == StatusEffectType.Slowed ||
                e.EffectType == StatusEffectType.Blind ||
                e.EffectType == StatusEffectType.Weakened
            );
        }

        /// <summary>
        /// 清除所有效果（场景切换或死亡重置）
        /// </summary>
        public void ClearAll()
        {
            _activeEffects.Clear();
        }

        /// <summary>
        /// 生成当前所有效果的属性修正 StatBlock（供管线层级6使用）
        /// </summary>
        public StatBlock GenerateBuffStatBlock()
        {
            var block = new StatBlock();

            foreach (var effect in _activeEffects)
            {
                switch (effect.EffectType)
                {
                    case StatusEffectType.ArmorBreak:
                        // 每层扣除固定物防
                        block.Add(StatType.DEF, -effect.Stacks * effect.ValuePerStack);
                        break;

                    case StatusEffectType.MagicShred:
                        // 每层扣除固定魔抗
                        block.Add(StatType.MDEF, -effect.Stacks * effect.ValuePerStack);
                        break;

                    case StatusEffectType.Slowed:
                        // 每层降低 1% 移速和攻速
                        float slowPct = effect.Stacks * 0.01f;
                        block.Add(StatType.MoveSpeed, -slowPct);
                        block.Add(StatType.AttackSpeed, -slowPct);
                        break;

                    case StatusEffectType.Haste:
                        // 加速
                        block.Add(StatType.MoveSpeed, effect.ValuePerStack);
                        break;

                    case StatusEffectType.Wet:
                        // 潮湿：清除闪避率
                        block.Set(StatType.Dodge, 0f);
                        break;

                    case StatusEffectType.Weakened:
                        // 虚弱：每层扣除固定 ATK/MATK（类似破甲/魔碎的降低方式）
                        float weakReduce = effect.Stacks * effect.ValuePerStack;
                        block.Add(StatType.ATK, -weakReduce);
                        block.Add(StatType.MATK, -weakReduce);
                        break;
                }
            }

            return block;
        }

        // =====================================================================
        //  辅助方法
        // =====================================================================

        /// <summary>
        /// 判定效果类型是否属于控制类（霸体期间应被免疫）
        /// </summary>
        private static bool IsControlEffect(StatusEffectType type)
        {
            return type == StatusEffectType.Stun
                || type == StatusEffectType.Frozen
                || type == StatusEffectType.Root
                || type == StatusEffectType.Charm
                || type == StatusEffectType.Slowed;
        }

        /// <summary>
        /// 将状态效果类型映射到对应的元素类型（用于元素反应检测）
        /// 非元素类状态返回 None
        /// </summary>
        private static ElementType StatusEffectToElement(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Burn:     return ElementType.Fire;
                case StatusEffectType.Wet:      return ElementType.Water;
                case StatusEffectType.Frozen:   return ElementType.Ice;
                case StatusEffectType.Shock:    return ElementType.Lightning;
                case StatusEffectType.Poison:   return ElementType.Poison;
                default:                        return ElementType.None;
            }
        }
    }
}
