// ============================================================================
// 流浪剑客 - 被动一：剑气纵横（剑路系统）
// 普攻命中恢复 5 MP，叠加剑路（最高 10 层），每层 1% 攻速移速
// 满层时普攻发射穿透剑气 (100% * ATK) 物理伤害
// 脱战 3 秒后剑路迅速衰减
//
// 来源：GameData_Blueprints/05_Hero_Classes_And_Skills.md §流浪剑客
// ============================================================================

using UnityEngine;
using EscapeTheTower.Data;
using EscapeTheTower.Entity;

namespace EscapeTheTower.Combat.Skills.Vagabond
{
    /// <summary>
    /// 剑路被动 —— 自动触发，由普攻命中和战斗状态驱动
    /// 不继承 SkillExecutor（被动无 CD/消耗概念）
    /// </summary>
    public class VagabondSwordPath
    {
        private const int MAX_STACKS = 10;
        private const float DECAY_DELAY = 3.0f;    // 脱战后多久开始衰减
        private const float DECAY_INTERVAL = 0.5f;  // 每层衰减间隔
        private const float MANA_PER_HIT = 5f;      // 普攻回蓝
        private const float RAGE_PER_HIT = 5f;      // 普攻攒怒
        private const float SWORD_BEAM_WIDTH = 0.6f; // 穿透剑气碰撞半宽
        private const float SWORD_BEAM_RANGE = 6f;   // 穿透剑气射程

        private Entity.Hero.HeroController _hero;
        private int _stacks;
        private float _decayTimer;

        /// <summary>当前剑路层数（供 HUD 显示）</summary>
        public int Stacks => _stacks;

        /// <summary>
        /// 初始化被动
        /// </summary>
        public void Initialize(Entity.Hero.HeroController hero)
        {
            _hero = hero;
            _stacks = 0;
            _decayTimer = DECAY_DELAY;
        }

        /// <summary>
        /// 每帧 Tick（处理衰减）
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_stacks <= 0) return;

            // 脱战时衰减
            if (!_hero.IsInBattle)
            {
                _decayTimer -= deltaTime;
                if (_decayTimer <= 0f)
                {
                    _stacks = Mathf.Max(0, _stacks - 1);
                    _decayTimer = DECAY_INTERVAL;
                }
            }
        }

        /// <summary>
        /// 普攻命中回调 —— 回蓝 + 叠层 + 满层发射剑气
        /// </summary>
        public void OnNormalAttackHit()
        {
            // 回蓝
            _hero.RestoreMana(MANA_PER_HIT);

            // 攒怒
            _hero.AddRage(RAGE_PER_HIT);

            // 叠层
            if (_stacks < MAX_STACKS)
            {
                _stacks++;
            }
            _decayTimer = DECAY_DELAY;

            // 满层触发穿透剑气
            if (_stacks >= MAX_STACKS)
            {
                FireSwordBeam();
            }
        }

        /// <summary>
        /// 进入战斗时重置衰减计时器
        /// </summary>
        public void OnEnterBattle()
        {
            _decayTimer = DECAY_DELAY;
        }

        /// <summary>
        /// 发射穿透剑气（100% * ATK 物理伤害）
        /// </summary>
        private void FireSwordBeam()
        {
            float atk = _hero.CurrentStats.Get(StatType.ATK);
            float damage = atk * 1.0f;

            // 获取面朝方向
            var input = _hero.GetComponent<Entity.Hero.HeroInputHandler>();
            Vector2 dir = (input != null && input.IsMoving) ? input.MoveInput : Vector2.right;

            // 线性范围检测
            var targets = SkillTargeting.FindEnemiesOnLine(
                _hero.transform.position, dir, SWORD_BEAM_RANGE, SWORD_BEAM_WIDTH, _hero.Faction);

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (!target.IsAlive) continue;

                var result = DamageCalculator.Calculate(
                    _hero.CurrentStats, target.CurrentStats,
                    baseDamage: damage,
                    damageType: DamageType.Physical);
                target.TakeDamage(result, _hero.EntityID);
            }

            Debug.Log($"[剑客] 剑路满层！穿透剑气 伤害={damage:F1} 命中={targets.Count}");
        }
    }
}
