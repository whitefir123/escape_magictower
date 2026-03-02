// ============================================================================
// 逃离魔塔 - 英雄主控制器 (HeroController)
// 管理英雄的移动、攻击、技能释放、经验升级等核心逻辑。
// MVP 阶段首发职业：流浪剑客 (Vagabond Swordsman)
//
// 来源：GameData_Blueprints/05_Hero_Classes_And_Skills.md
//       DesignDocs/03_Combat_System.md
//       DesignDocs/07_UI_and_UX.md
// ============================================================================

using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Data;
using EscapeTheTower.Combat;

namespace EscapeTheTower.Entity.Hero
{
    /// <summary>
    /// 英雄主控制器 —— 驱动英雄的全部行为逻辑
    /// </summary>
    [RequireComponent(typeof(HeroInputHandler))]
    public class HeroController : EntityBase
    {
        [Header("=== 英雄专属数据 ===")]
        [SerializeField] private HeroClassData_SO heroClassData;

        // === 输入处理器 ===
        private HeroInputHandler _input;

        // === 经验与等级 ===
        public int CurrentLevel { get; private set; } = 1;
        public int CurrentExp { get; private set; } = 0;
        public int ExpToNextLevel { get; private set; }

        // === 金币 ===
        public int Gold { get; private set; } = 0;

        // === 移动 ===
        private Rigidbody2D _rb;
        private Vector3 _clickMoveTarget;
        private bool _isClickMoving;

        // === 技能冷却计时器 ===
        private float _skill1CooldownTimer;
        private float _skill2CooldownTimer;
        private float _ultimateCooldownTimer; // CD 记录（大招实际由怒气驱动，CD 仅防连点）
        private float _evasionCooldownTimer;

        // === 战斗状态 ===
        private bool _isInBattle;

        // === 剑客被动：剑路层数 ===
        private int _swordPathStacks;
        private float _swordPathDecayTimer;
        private const float SWORD_PATH_DECAY_DELAY = 3.0f;     // 脱战3秒后衰减
        private const int SWORD_PATH_MAX_STACKS = 10;

        // =====================================================================
        //  生命周期
        // =====================================================================

        protected override void Awake()
        {
            base.Awake();

            // 将英雄职业数据赋给基类的 entityData
            if (heroClassData != null)
            {
                entityData = heroClassData;
            }

            _input = GetComponent<HeroInputHandler>();
            _rb = GetComponent<Rigidbody2D>();

            Faction = Faction.Player;
        }

        protected override void Start()
        {
            base.Start();
            InitializeHero();
        }

        private void Update()
        {
            if (!IsAlive) return;

            UpdateMovement();
            UpdateSkillCooldowns();
            UpdatePassives();
            HandleSkillInput();
        }

        // =====================================================================
        //  初始化
        // =====================================================================

        private void InitializeHero()
        {
            CurrentLevel = heroClassData != null ? heroClassData.startingLevel : 1;
            CurrentExp = heroClassData != null ? heroClassData.startingExp : 0;
            ExpToNextLevel = DamageCalculator.GetExpRequiredForLevel(CurrentLevel);

            // 订阅事件
            EventManager.Subscribe<OnEntityKillEvent>(OnEnemyKilled);
            EventManager.Subscribe<OnExpGainedEvent>(OnExpGained);
            EventManager.Subscribe<OnGoldGainedEvent>(OnGoldGained);

            Debug.Log($"[HeroController] 英雄 {heroClassData?.entityName ?? "未知"} 初始化完成。" +
                      $" Lv.{CurrentLevel} EXP:{CurrentExp}/{ExpToNextLevel}");
        }

        // =====================================================================
        //  移动
        // =====================================================================

        private void UpdateMovement()
        {
            float moveSpeed = CurrentStats.Get(StatType.MoveSpeed) * 3f; // 基准 1.0 → 3 单位/秒

            if (_input.IsMoving)
            {
                // WASD 移动优先，取消点击移动
                _isClickMoving = false;
                Vector2 velocity = _input.MoveInput * moveSpeed;

                if (_rb != null)
                {
                    _rb.linearVelocity = velocity;
                }
                else
                {
                    transform.Translate(velocity * Time.deltaTime);
                }
            }
            else if (_input.RightClickPressed)
            {
                // 鼠标右键点击地板移动
                _clickMoveTarget = _input.MouseWorldPosition;
                _clickMoveTarget.z = 0f;
                _isClickMoving = true;
            }
            else if (_isClickMoving)
            {
                // 执行点击移动
                Vector3 direction = (_clickMoveTarget - transform.position);
                if (direction.magnitude < 0.1f)
                {
                    _isClickMoving = false;
                    if (_rb != null) _rb.linearVelocity = Vector2.zero;
                }
                else
                {
                    Vector2 velocity = direction.normalized * moveSpeed;
                    if (_rb != null)
                    {
                        _rb.linearVelocity = velocity;
                    }
                    else
                    {
                        transform.Translate(velocity * Time.deltaTime);
                    }
                }
            }
            else
            {
                // 静止
                if (_rb != null) _rb.linearVelocity = Vector2.zero;
            }
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

            float cd = heroClassData != null ? heroClassData.evasionCooldown : 1.5f;
            _evasionCooldownTimer = cd;

            // 向前翻滚位移
            Vector2 dir = _input.IsMoving ? _input.MoveInput : Vector2.right;
            float rollDistance = 2.5f;
            Vector3 targetPos = transform.position + (Vector3)(dir * rollDistance);

            // 简化翻滚（直接位移，后续可替换为动画曲线）
            transform.position = targetPos;

            // 无敌帧持续时间（被动二检测）
            float hpRatio = CurrentStats.Get(StatType.HP) / Mathf.Max(1f, CurrentStats.Get(StatType.MaxHP));
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

            float atk = CurrentStats.Get(StatType.ATK);
            float damage = 40f + 1.5f * atk;

            // 向摇杆方向突刺位移
            Vector2 dir = _input.IsMoving ? _input.MoveInput : Vector2.right;
            float dashDistance = 3.0f;
            transform.position += (Vector3)(dir * dashDistance);

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

            if (!ConsumeMana(30f))
            {
                Debug.Log("[剑客] 旋风斩法力不足！");
                return;
            }

            _skill2CooldownTimer = 12.0f;

            float atk = CurrentStats.Get(StatType.ATK);
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

            if (!IsRageFull())
            {
                Debug.Log($"[剑客] 极刃风暴怒气不足！当前怒气: " +
                          $"{CurrentStats.Get(StatType.Rage):F0}/{CurrentStats.Get(StatType.MaxRage):F0}");
                return;
            }

            // 消耗全部怒气
            ConsumeAllRage();
            _ultimateCooldownTimer = 1.0f; // 防连点安全间隔

            float atk = CurrentStats.Get(StatType.ATK);
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
            // 被动二（绝境求生）在 TryUseEvasion 和 TakeDamage 中内联检测
        }

        /// <summary>
        /// 被动一：剑气纵横
        /// 普攻命中恢复 5 MP，叠加剑路（最高10层），每层1%攻速移速
        /// 满层普攻发射穿透剑气(100%*ATK)
        /// 脱战3秒后剑路衰减
        /// </summary>
        private void UpdatePassive1_SwordPath()
        {
            if (_swordPathStacks > 0 && !_isInBattle)
            {
                _swordPathDecayTimer -= Time.deltaTime;
                if (_swordPathDecayTimer <= 0f)
                {
                    _swordPathStacks = Mathf.Max(0, _swordPathStacks - 1);
                    _swordPathDecayTimer = 0.5f; // 每 0.5 秒衰减一层
                }
            }
        }

        /// <summary>
        /// 普攻命中时调用（由战斗系统外部触发）
        /// 处理被动一的回蓝和剑路叠层
        /// </summary>
        public void OnNormalAttackHit()
        {
            // 被动一：剑气纵横 —— 普攻回蓝
            RestoreMana(5f);

            // 叠加剑路
            if (_swordPathStacks < SWORD_PATH_MAX_STACKS)
            {
                _swordPathStacks++;
            }
            _swordPathDecayTimer = SWORD_PATH_DECAY_DELAY;

            // 满层触发穿透剑气
            if (_swordPathStacks >= SWORD_PATH_MAX_STACKS)
            {
                float atk = CurrentStats.Get(StatType.ATK);
                float swordBeamDamage = atk * 1.0f; // 100% * ATK

                // TODO: 生成穿透剑气投射物，对路径敌人造成物理伤害
                Debug.Log($"[剑客] 剑路满层！发射穿透剑气 伤害={swordBeamDamage:F1}");
            }

            // 普攻造成伤害积攒怒气
            float rageGain = 5f; // 普攻每次积攒固定怒气
            AddRage(rageGain);
        }

        // =====================================================================
        //  受伤重写（被动二：绝境求生）
        // =====================================================================

        public override void TakeDamage(DamageResult damage, int attackerID)
        {
            // 被动二检测：HP < 30% 时获得 20% 最终伤害减免
            float hpRatio = CurrentStats.Get(StatType.HP) / Mathf.Max(1f, CurrentStats.Get(StatType.MaxHP));
            if (hpRatio < 0.30f)
            {
                damage.FinalDamage *= 0.80f; // 减免 20%
            }

            // 受击回蓝（如果是受击回蓝型职业）
            if (heroClassData != null && heroClassData.manaRegenMode == ManaRegenMode.OnHitRegen)
            {
                RestoreMana(heroClassData.manaOnHit);
            }

            base.TakeDamage(damage, attackerID);
        }

        // =====================================================================
        //  经验与升级
        // =====================================================================

        private void OnExpGained(OnExpGainedEvent evt)
        {
            CurrentExp += evt.Amount;

            // 检查升级
            while (CurrentExp >= ExpToNextLevel)
            {
                CurrentExp -= ExpToNextLevel;
                LevelUp();
            }
        }

        private void LevelUp()
        {
            CurrentLevel++;
            ExpToNextLevel = DamageCalculator.GetExpRequiredForLevel(CurrentLevel);

            // 升级奖励：全基础属性 +1
            // 通过符文层级实现，创建一个升级加成 StatBlock
            var levelUpBonus = new StatBlock();
            levelUpBonus.Set(StatType.MaxHP, GameConstants.LEVEL_UP_STAT_BONUS);
            levelUpBonus.Set(StatType.ATK, GameConstants.LEVEL_UP_STAT_BONUS);
            levelUpBonus.Set(StatType.MATK, GameConstants.LEVEL_UP_STAT_BONUS);
            levelUpBonus.Set(StatType.DEF, GameConstants.LEVEL_UP_STAT_BONUS);
            levelUpBonus.Set(StatType.MDEF, GameConstants.LEVEL_UP_STAT_BONUS);
            AddRuneStat(levelUpBonus); // 走管线层级4叠加

            // 广播升级事件（触发机制符文三选一 UI）
            EventManager.Publish(new OnPlayerLevelUpEvent
            {
                Meta = new EventMeta(EntityID),
                NewLevel = CurrentLevel,
            });

            Debug.Log($"[HeroController] 升级！Lv.{CurrentLevel} 全属性+1" +
                      $" 下一级需要 {ExpToNextLevel} EXP");
        }

        // =====================================================================
        //  击杀与金币
        // =====================================================================

        private void OnEnemyKilled(OnEntityKillEvent evt)
        {
            if (evt.KillerEntityID != EntityID) return;
            _isInBattle = false; // 击杀后暂时视为脱战（后续由战斗状态机管理）
        }

        private void OnGoldGained(OnGoldGainedEvent evt)
        {
            Gold += evt.Amount;
        }

        /// <summary>进入战斗标记</summary>
        public void EnterBattle()
        {
            _isInBattle = true;
            _swordPathDecayTimer = SWORD_PATH_DECAY_DELAY;
        }

        // =====================================================================
        //  死亡重写
        // =====================================================================

        protected override void OnDeath(int killerID)
        {
            Debug.Log($"[HeroController] 英雄 {heroClassData?.entityName ?? "未知"} 阵亡！");
            // TODO: 弹出死亡 UI / 结算界面
            // 英雄不走 CommandBuffer 销毁，而是禁用控制
            _input.enabled = false;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;

            // 仍然广播死亡/击杀事件
            EventManager.Publish(new OnEntityDeathEvent
            {
                Meta = new EventMeta(EntityID),
                EntityID = EntityID,
            });
        }

        // =====================================================================
        //  清理
        // =====================================================================

        private void OnDestroy()
        {
            EventManager.Unsubscribe<OnEntityKillEvent>(OnEnemyKilled);
            EventManager.Unsubscribe<OnExpGainedEvent>(OnExpGained);
            EventManager.Unsubscribe<OnGoldGainedEvent>(OnGoldGained);
        }
    }
}
