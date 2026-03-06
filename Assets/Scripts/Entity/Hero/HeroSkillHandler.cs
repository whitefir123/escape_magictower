// ============================================================================
// 逃离魔塔 - 英雄技能处理器 (HeroSkillHandler)
// 数据驱动的技能分发系统。根据 HeroClassData_SO.skills 中配置的技能 SO，
// 自动实例化对应的 SkillExecutor 子类并管理输入分发。
//
// 来源：DesignDocs/03_Combat_System.md §1.2
//       GameData_Blueprints/05_Hero_Classes_And_Skills.md
// ============================================================================

using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Data;
using EscapeTheTower.Combat.Skills;
using EscapeTheTower.Combat.Skills.Vagabond;

namespace EscapeTheTower.Entity.Hero
{
    /// <summary>
    /// 英雄技能处理器 —— 数据驱动的技能管理与输入分发
    /// 挂载在与 HeroController 相同的 GameObject 上
    /// </summary>
    public class HeroSkillHandler : MonoBehaviour
    {
        // === 主控制器引用 ===
        private HeroController _hero;
        private HeroInputHandler _input;

        // === 技能执行器（按槽位分配） ===
        private SkillExecutor _evasionExecutor;     // 闪避
        private SkillExecutor _skill1Executor;      // 技能1 (Q)
        private SkillExecutor _skill2Executor;      // 技能2 (E)
        private SkillExecutor _ultimateExecutor;    // 大招 (R)

        // === 剑客专属被动 ===
        private VagabondSwordPath _swordPath;

        // === 诊断标记 ===
        private bool _diagLogged;

        /// <summary>当前剑路层数（供 HUD 显示用）</summary>
        public int SwordPathStacks => _swordPath?.Stacks ?? 0;

        /// <summary>
        /// 获取指定槽位的技能执行器（供 HUD 读取 CD 状态）
        /// </summary>
        public SkillExecutor GetExecutor(SkillSlotType slot)
        {
            return slot switch
            {
                SkillSlotType.Evasion  => _evasionExecutor,
                SkillSlotType.Active1  => _skill1Executor,
                SkillSlotType.Active2  => _skill2Executor,
                SkillSlotType.Ultimate => _ultimateExecutor,
                _ => null,
            };
        }
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

            // 从 HeroClassData_SO 获取技能列表并分派
            var heroData = hero.EntityData as HeroClassData_SO;
            if (heroData != null && heroData.skills != null && heroData.skills.Count > 0)
            {
                LoadSkillsFromData(heroData);
            }
            else
            {
                // 降级兜底：如果 SO 未配置技能，使用硬编码的剑客默认
                LoadVagabondDefaults();
                Debug.LogWarning("[HeroSkillHandler] HeroClassData_SO 未配置技能列表，降级使用剑客默认配置");
            }

            Debug.Log($"[HeroSkillHandler] 技能系统初始化完毕 | " +
                      $"闪避={_evasionExecutor?.Data?.skillName ?? "无"} | " +
                      $"技能1={_skill1Executor?.Data?.skillName ?? "无"} | " +
                      $"技能2={_skill2Executor?.Data?.skillName ?? "无"} | " +
                      $"大招={_ultimateExecutor?.Data?.skillName ?? "无"}");
        }

        /// <summary>
        /// 从 HeroClassData_SO 的技能列表中加载并分派到对应槽位
        /// </summary>
        private void LoadSkillsFromData(HeroClassData_SO heroData)
        {
            foreach (var skillData in heroData.skills)
            {
                if (skillData == null) continue;

                var executor = CreateExecutorForSkill(skillData);
                if (executor == null) continue;

                executor.Initialize(skillData, _hero, _input);

                switch (skillData.slotType)
                {
                    case SkillSlotType.Evasion:
                        _evasionExecutor = executor;
                        break;
                    case SkillSlotType.Active1:
                        _skill1Executor = executor;
                        break;
                    case SkillSlotType.Active2:
                        _skill2Executor = executor;
                        break;
                    case SkillSlotType.Ultimate:
                        _ultimateExecutor = executor;
                        break;
                    case SkillSlotType.Passive1:
                        // 被动技能不走执行器模式，由专属被动系统处理
                        break;
                }
            }

            // 初始化职业专属被动（根据职业类型）
            InitializePassives(heroData);
        }

        /// <summary>
        /// 根据技能 ID 创建对应的执行器实例
        /// </summary>
        private SkillExecutor CreateExecutorForSkill(SkillData_SO skillData)
        {
            // 根据 skillID 映射到具体的执行器类
            return skillData.skillID switch
            {
                // === 流浪剑客 ===
                "vagabond_evasion"     => new VagabondEvasion(),
                "vagabond_dash_strike" => new VagabondDashStrike(),
                "vagabond_whirlwind"   => new VagabondWhirlwind(),
                "vagabond_ultimate"    => new VagabondUltimate(),

                // === 其他职业预留 ===
                // "arcane_evasion" => new ArcaneEvasion(),
                // ...

                _ => null,
            };
        }

        /// <summary>
        /// 初始化职业专属被动系统
        /// </summary>
        private void InitializePassives(HeroClassData_SO heroData)
        {
            switch (heroData.heroClass)
            {
                case HeroClass.VagabondSwordsman:
                    _swordPath = new VagabondSwordPath();
                    _swordPath.Initialize(_hero);
                    break;

                // 其他职业被动预留
            }
        }

        /// <summary>
        /// 降级兜底：无 SO 配置时使用硬编码的剑客默认
        /// 运行时创建临时 SkillData_SO 并分派执行器
        /// </summary>
        private void LoadVagabondDefaults()
        {
            // 燕返
            var evasionData = CreateTempSkillData("vagabond_evasion", "燕返",
                SkillSlotType.Evasion, cd: 1.5f, manaCost: 0f);
            _evasionExecutor = new VagabondEvasion();
            _evasionExecutor.Initialize(evasionData, _hero, _input);

            // 疾风突刺
            var skill1Data = CreateTempSkillData("vagabond_dash_strike", "疾风突刺",
                SkillSlotType.Active1, cd: 8f, manaCost: 0f,
                baseDmg: 40f, atkScale: 1.5f, manaOnHit: 10f);
            _skill1Executor = new VagabondDashStrike();
            _skill1Executor.Initialize(skill1Data, _hero, _input);

            // 旋风斩
            var skill2Data = CreateTempSkillData("vagabond_whirlwind", "旋风斩",
                SkillSlotType.Active2, cd: 12f, manaCost: 30f,
                baseDmg: 15f, atkScale: 0.6f, isAOE: true, aoeRadius: 2f);
            skill2Data.grantsSuperArmor = true;
            _skill2Executor = new VagabondWhirlwind();
            _skill2Executor.Initialize(skill2Data, _hero, _input);

            // 极刃风暴
            var ultData = CreateTempSkillData("vagabond_ultimate", "极刃风暴",
                SkillSlotType.Ultimate, cd: 1f, manaCost: 0f,
                baseDmg: 30f, atkScale: 0.8f, isAOE: true, aoeRadius: 2.5f);
            ultData.requiresFullRage = true;
            ultData.hitCount = 8;
            ultData.hitInterval = 0.15f;
            ultData.grantsInvincibility = true;
            ultData.grantsSuperArmor = true;
            ultData.lifeStealPercent = 0.15f;
            _ultimateExecutor = new VagabondUltimate();
            _ultimateExecutor.Initialize(ultData, _hero, _input);

            // 被动
            _swordPath = new VagabondSwordPath();
            _swordPath.Initialize(_hero);
        }

        /// <summary>
        /// 创建临时 SkillData_SO（降级兜底用，不序列化）
        /// </summary>
        private SkillData_SO CreateTempSkillData(string id, string name,
            SkillSlotType slot, float cd, float manaCost,
            float baseDmg = 0f, float atkScale = 0f, float matkScale = 0f,
            float manaOnHit = 0f, bool isAOE = false, float aoeRadius = 0f)
        {
            var so = ScriptableObject.CreateInstance<SkillData_SO>();
            so.skillID = id;
            so.skillName = name;
            so.slotType = slot;
            so.cooldown = cd;
            so.manaCost = manaCost;
            so.baseDamage = baseDmg;
            so.atkScaling = atkScale;
            so.matkScaling = matkScale;
            so.damageType = DamageType.Physical;
            so.manaRestoreOnHit = manaOnHit;
            so.isAOE = isAOE;
            so.aoeRadius = aoeRadius;
            return so;
        }

        // =====================================================================
        //  每帧更新（由 HeroController.Update 调用）
        // =====================================================================

        /// <summary>
        /// 技能系统每帧 Tick
        /// </summary>
        public void Tick()
        {
            // 一次性诊断日志（确认 Tick 正在执行）
            if (!_diagLogged)
            {
                _diagLogged = true;
                Debug.Log($"[HeroSkillHandler.Tick] 诊断：_input={(_input != null ? "有效" : "NULL")} " +
                          $"闪避={(_evasionExecutor != null ? _evasionExecutor.Data.skillName : "NULL")} " +
                          $"技能1={(_skill1Executor != null ? _skill1Executor.Data.skillName : "NULL")} " +
                          $"技能2={(_skill2Executor != null ? _skill2Executor.Data.skillName : "NULL")} " +
                          $"大招={(_ultimateExecutor != null ? _ultimateExecutor.Data.skillName : "NULL")}");
            }

            float dt = Time.deltaTime;

            // 更新技能冷却
            _evasionExecutor?.Tick(dt);
            _skill1Executor?.Tick(dt);
            _skill2Executor?.Tick(dt);
            _ultimateExecutor?.Tick(dt);

            // 更新被动系统
            _swordPath?.Tick(dt);

            // 处理技能输入
            HandleSkillInput();
        }

        // =====================================================================
        //  技能输入分发
        // =====================================================================

        private void HandleSkillInput()
        {
            // 闪避（Space）
            if (_input.DodgePressed && _evasionExecutor != null)
            {
                _evasionExecutor.TryUse();
            }

            // 技能1（Q）
            if (_input.Skill1Pressed && _skill1Executor != null)
            {
                _skill1Executor.TryUse();
            }

            // 技能2（E）
            if (_input.Skill2Pressed && _skill2Executor != null)
            {
                _skill2Executor.TryUse();
            }

            // 大招（R）
            if (_input.UltimatePressed && _ultimateExecutor != null)
            {
                _ultimateExecutor.TryUse();
            }
        }

        // =====================================================================
        //  普攻命中回调
        // =====================================================================

        /// <summary>
        /// 普攻命中时调用（由 HeroCombatHandler 触发）
        /// 驱动被动系统
        /// </summary>
        public void OnNormalAttackHit()
        {
            _swordPath?.OnNormalAttackHit();
        }

        /// <summary>
        /// 进入战斗时重置衰减计时器
        /// </summary>
        public void OnEnterBattle()
        {
            _swordPath?.OnEnterBattle();
        }
    }
}
