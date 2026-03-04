// ============================================================================
// 逃离魔塔 - 英雄技能处理器 (HeroSkillHandler)
// 从 HeroController 拆分而来，负责管理 4 个主动技能 + 被动剑路系统。
//
// 来源：GameData_Blueprints/05_Hero_Classes_And_Skills.md
// ============================================================================

using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Data;

namespace EscapeTheTower.Entity.Hero
{
    /// <summary>
    /// 英雄技能处理器 —— 管理技能冷却、输入分发、主动/被动技能逻辑
    /// 挂载在与 HeroController 相同的 GameObject 上
    /// </summary>
    public class HeroSkillHandler : MonoBehaviour
    {
        // === 主控制器引用 ===
        private HeroController _hero;
        private HeroInputHandler _input;

        // === 技能冷却计时器 ===
        private float _skill1CooldownTimer;
        private float _skill2CooldownTimer;
        private float _ultimateCooldownTimer; // CD 记录（大招实际由怒气驱动，CD 仅防连点）
        private float _evasionCooldownTimer;

        // === 剑客被动：剑路层数 ===
        private int _swordPathStacks;
        private float _swordPathDecayTimer;
        private const float SWORD_PATH_DECAY_DELAY = 3.0f;     // 脱战3秒后衰减
        private const int SWORD_PATH_MAX_STACKS = 10;

        /// <summary>当前剑路层数（供 HUD 显示用）</summary>
        public int SwordPathStacks => _swordPathStacks;

        // =====================================================================
        //  初始化
        // =====================================================================

        /// <summary>
        /// 由 HeroController.Awake 调用，传入依赖引用
        /// </summary>
        public void Initialize(HeroController hero, HeroInputHandler input)
        {
            _hero = hero;
            _input = input;
        }

        // =====================================================================
        //  每帧更新（由 HeroController.Update 调用）
        // =====================================================================

        /// <summary>
        /// 技能系统每帧 Tick
        /// </summary>
        public void Tick()
        {
            UpdateSkillCooldowns();
            UpdatePassives();
            HandleSkillInput();
        }

        // =====================================================================
        //  技能冷却
        // =====================================================================

        private void UpdateSkillCooldowns()
        {
            if (_skill1CooldownTimer > 0f) _skill1CooldownTimer -= Time.deltaTime;
            if (_skill2CooldownTimer > 0f) _skill2CooldownTimer -= Time.deltaTime;
            if (_ultimateCooldownTimer > 0f) _ultimateCooldownTimer -= Time.deltaTime;
            if (_evasionCooldownTimer > 0f) _evasionCooldownTimer -= Time.deltaTime;
        }

        // =====================================================================
        //  技能输入处理
        // =====================================================================

        private void HandleSkillInput()
        {
            // 闪避（燕返）
            if (_input.DodgePressed)
            {
                TryUseEvasion();
            }

            // 技能1（疾风突刺）
            if (_input.Skill1Pressed)
            {
                TryUseSkill1();
            }

            // 技能2（旋风斩）
            if (_input.Skill2Pressed)
            {
                TryUseSkill2();
            }

            // 大招（极刃风暴）
            if (_input.UltimatePressed)
            {
                TryUseUltimate();
            }
        }

        // =====================================================================
        //  流浪剑客技能实装
        //  来源：GameData_Blueprints/05_Hero_Classes_And_Skills.md
        // =====================================================================

        /// <summary>
        /// 专属闪避：燕返
        /// CD 1.5秒，无消耗，无敌帧 0.3秒
        /// 被动二「绝境求生」：HP < 30% 时无敌帧延长至 0.5秒
        /// </summary>
        private void TryUseEvasion()
        {
            if (_evasionCooldownTimer > 0f) return;

            var heroData = _hero.EntityData as HeroClassData_SO;
            float cd = heroData != null ? heroData.evasionCooldown : 1.5f;
            _evasionCooldownTimer = cd;

            // 向前翻滚位移
            Vector2 dir = _input.IsMoving ? _input.MoveInput : Vector2.right;
            float rollDistance = 2.5f;
            Vector3 targetPos = _hero.transform.position + (Vector3)(dir * rollDistance);

            // 简化翻滚（直接位移，后续可替换为动画曲线）
            _hero.transform.position = targetPos;

            // 无敌帧持续时间（被动二检测）
            float hpRatio = _hero.CurrentStats.Get(StatType.HP) / Mathf.Max(1f, _hero.CurrentStats.Get(StatType.MaxHP));
            float invDuration = hpRatio < 0.30f ? 0.5f : 0.3f;

            Debug.Log($"[剑客] 燕返！无敌帧 {invDuration}s " +
                      (hpRatio < 0.30f ? "【绝境求生触发：延长至0.5s】" : ""));
        }

        /// <summary>
        /// 技能一：疾风突刺
        /// CD 8秒，无消耗，向摇杆方向突刺，造成 (40 + 1.5*ATK) 物伤
        /// 每命中一个目标恢复 10 点法力
        /// </summary>
        private void TryUseSkill1()
        {
            if (_skill1CooldownTimer > 0f)
            {
                Debug.Log("[剑客] 疾风突刺冷却中...");
                return;
            }

            _skill1CooldownTimer = 8.0f;

            float atk = _hero.CurrentStats.Get(StatType.ATK);
            float damage = 40f + 1.5f * atk;

            // 向摇杆方向突刺位移
            Vector2 dir = _input.IsMoving ? _input.MoveInput : Vector2.right;
            float dashDistance = 3.0f;
            _hero.transform.position += (Vector3)(dir * dashDistance);

            // TODO: 碰撞检测路径上的敌人并造成伤害，每命中一个回复 10 MP
            Debug.Log($"[剑客] 疾风突刺！伤害={damage:F1} 方向={dir}");
        }

        /// <summary>
        /// 技能二：旋风斩
        /// CD 12秒，消耗 30 MP，原地AOE 2秒，每0.5秒造成 (15+0.6*ATK) 物伤
        /// 移速降低30%但免疫击退打断
        /// </summary>
        private void TryUseSkill2()
        {
            if (_skill2CooldownTimer > 0f)
            {
                Debug.Log("[剑客] 旋风斩冷却中...");
                return;
            }

            if (!_hero.ConsumeMana(30f))
            {
                Debug.Log("[剑客] 旋风斩法力不足！");
                return;
            }

            _skill2CooldownTimer = 12.0f;

            float atk = _hero.CurrentStats.Get(StatType.ATK);
            float tickDamage = 15f + 0.6f * atk;

            // TODO: 启动协程，2秒内每0.5秒对周围敌人造成伤害，期间移速-30%但免疫控制
            Debug.Log($"[剑客] 旋风斩！每跳伤害={tickDamage:F1} 持续2秒");
        }

        /// <summary>
        /// 终极必杀技：极刃风暴
        /// CD 45秒（实际由怒气驱动），消耗满怒气
        /// 8段打击，每段 (30+0.8*ATK)，绝对霸体免疫伤害，15%物理吸血
        /// </summary>
        private void TryUseUltimate()
        {
            if (_ultimateCooldownTimer > 0f)
            {
                Debug.Log("[剑客] 极刃风暴防连点冷却中...");
                return;
            }

            if (!_hero.IsRageFull())
            {
                Debug.Log($"[剑客] 极刃风暴怒气不足！当前怒气: " +
                          $"{_hero.CurrentStats.Get(StatType.Rage):F0}/{_hero.CurrentStats.Get(StatType.MaxRage):F0}");
                return;
            }

            // 消耗全部怒气
            _hero.ConsumeAllRage();
            _ultimateCooldownTimer = 1.0f; // 防连点安全间隔

            float atk = _hero.CurrentStats.Get(StatType.ATK);
            float hitDamage = 30f + 0.8f * atk;
            int hitCount = 8;
            float lifeSteal = 0.15f;

            // TODO: 启动协程，8段打击，期间霸体免疫伤害，每段附带15%物理吸血
            Debug.Log($"[剑客] 极刃风暴！{hitCount}段 x {hitDamage:F1}伤害 吸血{lifeSteal:P0}");
        }

        // =====================================================================
        //  被动技能
        //  来源：GameData_Blueprints/05_Hero_Classes_And_Skills.md
        // =====================================================================

        /// <summary>
        /// 更新被动技能逻辑
        /// </summary>
        private void UpdatePassives()
        {
            UpdatePassive1_SwordPath();
            // 被动二（绝境求生）在 TryUseEvasion 和 HeroController.TakeDamage 中内联检测
        }

        /// <summary>
        /// 被动一：剑气纵横
        /// 普攻命中恢复 5 MP，叠加剑路（最高10层），每层1%攻速移速
        /// 满层普攻发射穿透剑气(100%*ATK)
        /// 脱战3秒后剑路衰减
        /// </summary>
        private void UpdatePassive1_SwordPath()
        {
            if (_swordPathStacks > 0 && !_hero.IsInBattle)
            {
                _swordPathDecayTimer -= Time.deltaTime;
                if (_swordPathDecayTimer <= 0f)
                {
                    _swordPathStacks = Mathf.Max(0, _swordPathStacks - 1);
                    _swordPathDecayTimer = 0.5f; // 每 0.5 秒衰减一层
                }
            }
        }

        // =====================================================================
        //  普攻命中回调
        // =====================================================================

        /// <summary>
        /// 普攻命中时调用（由 HeroCombatHandler 触发）
        /// 处理被动一的回蓝和剑路叠层
        /// </summary>
        public void OnNormalAttackHit()
        {
            // 被动一：剑气纵横 —— 普攻回蓝
            _hero.RestoreMana(5f);

            // 叠加剑路
            if (_swordPathStacks < SWORD_PATH_MAX_STACKS)
            {
                _swordPathStacks++;
            }
            _swordPathDecayTimer = SWORD_PATH_DECAY_DELAY;

            // 满层触发穿透剑气
            if (_swordPathStacks >= SWORD_PATH_MAX_STACKS)
            {
                float atk = _hero.CurrentStats.Get(StatType.ATK);
                float swordBeamDamage = atk * 1.0f; // 100% * ATK

                // TODO: 生成穿透剑气投射物，对路径敌人造成物理伤害
                Debug.Log($"[剑客] 剑路满层！发射穿透剑气 伤害={swordBeamDamage:F1}");
            }

            // 普攻造成伤害积攒怒气
            float rageGain = 5f; // 普攻每次积攒固定怒气
            _hero.AddRage(rageGain);
        }

        /// <summary>
        /// 进入战斗时重置剑路衰减计时器
        /// </summary>
        public void OnEnterBattle()
        {
            _swordPathDecayTimer = SWORD_PATH_DECAY_DELAY;
        }
    }
}
