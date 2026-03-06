// ============================================================================
// 逃离魔塔 - 技能执行器基类 (SkillExecutor)
// 所有技能的通用框架：CD管理、资源检查/消耗、伤害计算、目标检测。
// 每个具体技能继承此类并 override OnExecute() 实现专属行为。
//
// 来源：DesignDocs/03_Combat_System.md §1.2
//       GameData_Blueprints/05_Hero_Classes_And_Skills.md
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EscapeTheTower.Data;
using EscapeTheTower.Entity;
using EscapeTheTower.Entity.Hero;

namespace EscapeTheTower.Combat.Skills
{
    /// <summary>
    /// 技能执行器基类 —— 管理 CD/消耗/通用逻辑，子类实现具体行为
    /// </summary>
    public abstract class SkillExecutor
    {
        /// <summary>技能数据 SO（只读配置）</summary>
        public SkillData_SO Data { get; private set; }

        /// <summary>当前冷却剩余秒数</summary>
        public float CooldownRemaining { get; protected set; }

        /// <summary>冷却是否就绪</summary>
        public bool IsReady => CooldownRemaining <= 0f;

        /// <summary>技能是否正在执行中（多段打击期间）</summary>
        public bool IsExecuting { get; protected set; }

        // === 内部引用 ===
        protected HeroController Hero;
        protected HeroInputHandler Input;

        // =====================================================================
        //  初始化
        // =====================================================================

        /// <summary>
        /// 初始化执行器，绑定技能数据和英雄引用
        /// </summary>
        public virtual void Initialize(SkillData_SO data, HeroController hero, HeroInputHandler input)
        {
            Data = data;
            Hero = hero;
            Input = input;
            CooldownRemaining = 0f;
            IsExecuting = false;
        }

        // =====================================================================
        //  每帧更新
        // =====================================================================

        /// <summary>
        /// 每帧 Tick（由 HeroSkillHandler 调用）
        /// </summary>
        public virtual void Tick(float deltaTime)
        {
            if (CooldownRemaining > 0f)
            {
                CooldownRemaining -= deltaTime;
                if (CooldownRemaining < 0f) CooldownRemaining = 0f;
            }
        }

        // =====================================================================
        //  技能使用（模板方法）
        // =====================================================================

        /// <summary>
        /// 尝试使用技能 —— 检查 CD/资源消耗后调用 OnExecute
        /// </summary>
        /// <returns>是否成功释放</returns>
        public bool TryUse()
        {
            if (IsExecuting) return false;

            // CD 检查
            if (!IsReady)
            {
                Debug.Log($"[技能] {Data.skillName} 冷却中（剩余 {CooldownRemaining:F1}s）");
                return false;
            }

            // 怒气检查（大招专用）
            if (Data.requiresFullRage)
            {
                if (!Hero.IsRageFull())
                {
                    Debug.Log($"[技能] {Data.skillName} 怒气不足！");
                    return false;
                }
            }

            // 法力检查
            if (Data.manaCost > 0f)
            {
                if (!Hero.ConsumeMana(Data.manaCost))
                {
                    Debug.Log($"[技能] {Data.skillName} 法力不足！");
                    return false;
                }
            }

            // 消耗怒气（大招）
            if (Data.requiresFullRage)
            {
                Hero.ConsumeAllRage();
            }

            // 启动冷却
            CooldownRemaining = Data.cooldown;

            // 执行具体技能逻辑
            OnExecute();
            return true;
        }

        // =====================================================================
        //  子类必须实现
        // =====================================================================

        /// <summary>
        /// 技能具体执行逻辑（子类 override）
        /// 此方法在 CD/资源检查通过后调用
        /// </summary>
        protected abstract void OnExecute();

        // =====================================================================
        //  通用工具方法
        // =====================================================================

        /// <summary>
        /// 向 DamageCalculator 传递技能参数并计算伤害结果
        /// </summary>
        protected DamageResult CalculateSkillDamageResult(StatBlock defenderStats)
        {
            return DamageCalculator.Calculate(
                Hero.CurrentStats,
                defenderStats,
                Data.baseDamage,
                Data.atkScaling,
                Data.matkScaling,
                Data.damageType,
                Data.elementType);
        }

        /// <summary>
        /// 对目标列表造成技能伤害（自动走 DamageCalculator 完整结算链）
        /// </summary>
        protected void DealDamageToTargets(List<EntityBase> targets)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (!target.IsAlive) continue;

                var result = CalculateSkillDamageResult(target.CurrentStats);
                target.TakeDamage(result, Hero.EntityID);

                // 吸血
                if (Data.lifeStealPercent > 0f)
                {
                    float healAmount = result.FinalDamage * Data.lifeStealPercent;
                    Hero.Heal(healAmount);
                }

                // 命中回蓝
                if (Data.manaRestoreOnHit > 0f)
                {
                    Hero.RestoreMana(Data.manaRestoreOnHit);
                }

                // 怒气积攒（技能伤害也积攒怒气）
                Hero.AddRage(3f);
            }
        }

        /// <summary>
        /// 获取英雄当前面朝方向（基于最后移动方向，当前按住方向键优先）
        /// </summary>
        protected Vector2 GetFacingDirection()
        {
            // 当前按住方向键优先
            if (Input.IsMoving)
                return Input.MoveInput;

            // 否则使用最后一次移动的方向
            return new Vector2(Input.LastFacingDirection.x, Input.LastFacingDirection.y);
        }

        /// <summary>
        /// 启动协程（需要通过 Hero 的 MonoBehaviour 启动）
        /// </summary>
        protected Coroutine StartCoroutine(IEnumerator routine)
        {
            return Hero.StartCoroutine(routine);
        }
    }
}
