// ============================================================================
// 逃离魔塔 - 英雄主控制器 (HeroController)
// 编排器角色：管理英雄生命周期、移动控制、子组件协调。
// 技能/物品/战斗逻辑已拆分至 HeroSkillHandler / HeroInventory / HeroCombatHandler。
//
// 来源：DesignDocs/03_Combat_System.md
//       GameData_Blueprints/05_Hero_Classes_And_Skills.md
// ============================================================================

using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Data;
using EscapeTheTower.Combat;
using EscapeTheTower.Equipment;

namespace EscapeTheTower.Entity.Hero
{
    /// <summary>
    /// 英雄主控制器 —— 编排器，协调各子组件的初始化与生命周期
    /// </summary>
    [RequireComponent(typeof(HeroInputHandler))]
    [RequireComponent(typeof(HeroSkillHandler))]
    [RequireComponent(typeof(HeroInventory))]
    [RequireComponent(typeof(HeroCombatHandler))]
    [RequireComponent(typeof(HeroEquipmentManager))]
    public class HeroController : EntityBase
    {
        [Header("=== 英雄专属数据 ===")]
        [SerializeField] private HeroClassData_SO heroClassData;

        // === 输入处理器 ===
        private HeroInputHandler _input;

        // === 格子移动（使用基类 EntityBase._gridMovement） ===
        // === 子组件引用 ===
        private HeroSkillHandler _skillHandler;
        private HeroInventory _inventory;
        private HeroCombatHandler _combatHandler;
        private HeroEquipmentManager _equipmentMgr;

        // === 移动速度缓存（避免每帧重复调用 SetMoveSpeed） ===
        private float _lastMoveSpeed = -1f;

        // === 战斗状态 ===
        private bool _isInBattle;

        // =====================================================================
        //  门面属性 —— 委托给子组件，保持外部调用方零修改
        // =====================================================================

        /// <summary>当前等级</summary>
        public int CurrentLevel => _inventory != null ? _inventory.CurrentLevel : 1;

        /// <summary>当前经验值</summary>
        public int CurrentExp => _inventory != null ? _inventory.CurrentExp : 0;

        /// <summary>升级所需经验</summary>
        public int ExpToNextLevel => _inventory != null ? _inventory.ExpToNextLevel : 100;

        /// <summary>金币</summary>
        public int Gold
        {
            get => _inventory != null ? _inventory.Gold : 0;
            // 兼容直接赋值（如拾取金币堆时 Gold += value）
            set { if (_inventory != null) _inventory.Gold = value; }
        }

        /// <summary>铜钥匙</summary>
        public int KeyBronze => _inventory != null ? _inventory.KeyBronze : 0;
        /// <summary>银钥匙</summary>
        public int KeySilver => _inventory != null ? _inventory.KeySilver : 0;
        /// <summary>金钥匙</summary>
        public int KeyGold => _inventory != null ? _inventory.KeyGold : 0;

        /// <summary>是否处于战斗状态（供技能系统查询剑路衰减）</summary>
        public bool IsInBattle => _isInBattle;

        // =====================================================================
        //  门面方法 —— 委托给子组件
        // =====================================================================

        /// <summary>增加钥匙</summary>
        public void AddKey(DoorTier tier) => _inventory?.AddKey(tier);

        /// <summary>消耗钥匙（开门时调用），返回是否成功</summary>
        public bool ConsumeKey(DoorTier tier) => _inventory != null && _inventory.ConsumeKey(tier);

        /// <summary>普攻命中回调（由 HeroCombatHandler 触发，委托给技能系统）</summary>
        public void OnNormalAttackHit() => _skillHandler?.OnNormalAttackHit();

        /// <summary>设置战斗状态</summary>
        public void SetBattleState(bool inBattle)
        {
            _isInBattle = inBattle;
            // 脱战时触发击杀后延迟保护（防止连续碰撞）
            if (!inBattle && _combatHandler != null)
            {
                _combatHandler.OnEnemyKilled();
            }
        }

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

            // 格子移动组件（自动添加）
            _gridMovement = GetComponent<GridMovement>();
            if (_gridMovement == null)
            {
                _gridMovement = gameObject.AddComponent<GridMovement>();
            }
            _gridMovement.OnArrivedAtTile += OnArrivedAtTile;

            // 初始化子组件（自动添加缺失的组件）
            _skillHandler = GetComponent<HeroSkillHandler>();
            if (_skillHandler == null) _skillHandler = gameObject.AddComponent<HeroSkillHandler>();

            _inventory = GetComponent<HeroInventory>();
            if (_inventory == null) _inventory = gameObject.AddComponent<HeroInventory>();

            _combatHandler = GetComponent<HeroCombatHandler>();
            if (_combatHandler == null) _combatHandler = gameObject.AddComponent<HeroCombatHandler>();

            _equipmentMgr = GetComponent<HeroEquipmentManager>();
            if (_equipmentMgr == null) _equipmentMgr = gameObject.AddComponent<HeroEquipmentManager>();

            Faction = Faction.Player;
        }

        protected override void Start()
        {
            base.Start();
            EnsureVisualRepresentation();
            InitializeHero();
            EnsureAlive();
        }

        /// <summary>
        /// 确保有可见的视觉表现（无美术素材时自动生成白色方块）
        /// </summary>
        private void EnsureVisualRepresentation()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = gameObject.AddComponent<SpriteRenderer>();
            }

            if (sr.sprite == null)
            {
                Texture2D tex = new Texture2D(4, 4);
                Color[] px = new Color[16];
                for (int i = 0; i < 16; i++) px[i] = Color.white;
                tex.SetPixels(px);
                tex.Apply();
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                sr.color = new Color(0.3f, 0.7f, 1.0f);
            }
        }

        /// <summary>
        /// 确保英雄在初始化后处于存活状态（防止 SO 数据未填导致 IsAlive=false）
        /// </summary>
        private void EnsureAlive()
        {
            if (CurrentStats.Get(StatType.HP) <= 0f)
            {
                CurrentStats.Set(StatType.HP, 100f);
                CurrentStats.Set(StatType.MaxHP, 100f);
                Debug.LogWarning("[HeroController] HP为0，已强制设为100。请检查HeroClassData_SO配置。");
            }

            if (CurrentStats.Get(StatType.MaxHP) <= 0f)
            {
                CurrentStats.Set(StatType.MaxHP, 100f);
                Debug.LogWarning("[HeroController] MaxHP为0，已强制设为100。请检查HeroClassData_SO配置。");
            }

            // 确保实际 HP 不超过 MaxHP
            float hp = CurrentStats.Get(StatType.HP);
            float maxHp = CurrentStats.Get(StatType.MaxHP);
            if (hp > maxHp) CurrentStats.Set(StatType.HP, maxHp);
        }

        protected override void Update()
        {
            base.Update(); // 驱动 EntityBase.DOT 定时器

            if (!IsAlive) return;

            UpdateMovement();
            _skillHandler.Tick();
            _combatHandler.Tick();
        }

        // =====================================================================
        //  初始化
        // =====================================================================

        private void InitializeHero()
        {
            // 初始化子组件（传递依赖引用）
            _skillHandler.Initialize(this, _input);
            _inventory.Initialize(this);
            _combatHandler.Initialize(this, _skillHandler, _gridMovement);

            // 装备穿戴变更时重算属性
            _equipmentMgr.OnEquipmentChanged += RecalculateStats;

            // 订阅符文选取完成事件，将属性符文注入管线L4
            EventManager.Subscribe<OnRuneDraftCompleteEvent>(OnRuneDraftComplete);

            // 对齐到格子
            _gridMovement.SnapToGrid();

            // 绑定技能冷却 HUD
            var skillHUD = FindAnyObjectByType<EscapeTheTower.UI.SkillCooldownHUD>();
            if (skillHUD == null)
            {
                var hudObj = new GameObject("SkillCooldownHUD");
                skillHUD = hudObj.AddComponent<EscapeTheTower.UI.SkillCooldownHUD>();
            }
            skillHUD.SetHeroReference(this);

            Debug.Log($"[HeroController] 英雄初始化完毕 | " +
                      $"职业={heroClassData?.entityName ?? "未配置"} | " +
                      $"HP={CurrentStats.Get(StatType.HP):F0}/{CurrentStats.Get(StatType.MaxHP):F0}");
        }

        // =====================================================================
        //  格子移动
        //  来源：DesignDocs/03_Combat_System.md §1.1.1
        // =====================================================================

        private void UpdateMovement()
        {
            // 移动速度：baseMoveSpeed(1.0) × 8 = 8格/秒 → 单格约125ms
            float speed = Mathf.Max(1f, CurrentStats.Get(StatType.MoveSpeed) * 8f);
            if (!Mathf.Approximately(speed, _lastMoveSpeed))
            {
                _lastMoveSpeed = speed;
                _gridMovement.SetMoveSpeed(speed);
                // 回弹需要比正常移动更快更利落（12f → 约170ms 完成往返）
                _gridMovement.SetBumpSpeed(12f);
            }

            if (_input.IsMoving)
            {
                // 按键栈已输出四方向 Vector2Int，直接传递
                _gridMovement.SetDirection(_input.MoveDirection);
            }
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
        //  拾取物交互（踩到格子时触发）
        // =====================================================================

        private void OnArrivedAtTile(Vector2Int tilePos)
        {
            // === 战争迷雾揭示 ===
            EscapeTheTower.Map.FogOfWarManager.Instance?.OnPlayerMoved(tilePos);

            // === 拾取物交互 ===
            var pickupMgr = EscapeTheTower.Map.PickupManager.Instance;
            if (pickupMgr != null)
            {
                var item = pickupMgr.GetItemAt(tilePos);
                if (item != null)
                {
                    // 根据类型处理效果
                    switch (item.Type)
                    {
                        case PickupType.HealthPotion:
                            Heal(item.Value);
                            break;
                        case PickupType.ManaPotion:
                            RestoreMana(item.Value);
                            break;
                        case PickupType.KeyBronze:
                            AddKey(DoorTier.Bronze);
                            break;
                        case PickupType.KeySilver:
                            AddKey(DoorTier.Silver);
                            break;
                        case PickupType.KeyGold:
                            AddKey(DoorTier.Gold);
                            break;
                        case PickupType.GoldPile:
                            Gold += item.Value;
                            Debug.Log($"[拾取] 金币堆 +{item.Value}，总计={Gold}");
                            break;
                        case PickupType.Equipment:
                            // 直接调用装备管理器处理拾取（与其他拾取物一致的直接调用模式）
                            if (item.EquipData != null && _equipmentMgr != null)
                            {
                                if (!_equipmentMgr.HasEquipped(item.EquipData.slot))
                                {
                                    _equipmentMgr.Equip(item.EquipData);
                                }
                                else
                                {
                                    _equipmentMgr.AddToInventory(item.EquipData);
                                }
                            }
                            break;
                    }

                    item.OnPickedUp(EntityID);
                }
            }

            // === 楼梯交互（踩到解锁楼梯 → 切换楼层） ===
            var transitionMgr = EscapeTheTower.Map.FloorTransitionManager.Instance;
            if (transitionMgr != null && transitionMgr.CurrentFloorGrid != null)
            {
                var grid = transitionMgr.CurrentFloorGrid;
                if (grid.InBounds(tilePos.x, tilePos.y) &&
                    grid.Tiles[tilePos.x, tilePos.y] == TileType.StairsDown &&
                    !grid.StairsLocked)
                {
                    Debug.Log($"[楼梯] 踩到解锁楼梯！准备切换楼层...");
                    transitionMgr.TransitionToNextFloor();
                }
            }
        }

        // =====================================================================
        //  死亡重写
        // =====================================================================

        protected override void OnDeath(int killerID)
        {
            Debug.Log($"[HeroController] 英雄 {heroClassData?.entityName ?? "未知"} 阵亡！");

            // 禁用输入和移动
            _input.enabled = false;
            if (_gridMovement != null)
            {
                _gridMovement.enabled = false;
            }

            // 禁用子组件
            if (_skillHandler != null) _skillHandler.enabled = false;
            if (_combatHandler != null) _combatHandler.enabled = false;

            // 禁用自身 Update（停止一切行为）
            enabled = false;

            // 广播死亡事件
            EventManager.Publish(new OnEntityDeathEvent
            {
                Meta = new EventMeta(EntityID),
                EntityID = EntityID,
            });

            // 弹出死亡结算界面
            var deathScreen = FindAnyObjectByType<EscapeTheTower.UI.DeathScreen>();
            if (deathScreen == null)
            {
                var deathObj = new GameObject("DeathScreen");
                deathScreen = deathObj.AddComponent<EscapeTheTower.UI.DeathScreen>();
            }
            deathScreen.Show(CurrentLevel, Gold, Time.timeSinceLevelLoad);
        }

        // =====================================================================
        //  清理
        // =====================================================================

        private void OnDestroy()
        {
            if (_gridMovement != null)
            {
                _gridMovement.OnArrivedAtTile -= OnArrivedAtTile;
            }
            if (_equipmentMgr != null)
            {
                _equipmentMgr.OnEquipmentChanged -= RecalculateStats;
            }
            EventManager.Unsubscribe<OnRuneDraftCompleteEvent>(OnRuneDraftComplete);
        }

        // =====================================================================
        //  符文选取回调 —— 将属性符文注入管线L4
        // =====================================================================

        /// <summary>
        /// 符文三选一结束后回调：属性符文直接注入管线L4层
        /// 机制符文的效果由各自的机制钩子实现，此处不处理
        /// </summary>
        private void OnRuneDraftComplete(OnRuneDraftCompleteEvent evt)
        {
            if (evt.SelectedRune == null) return;

            // 仅属性符文（KillDrop）类型直接注入数值加成
            if (evt.SelectedRune.acquisitionType == RuneAcquisitionType.KillDrop
                && evt.SelectedRune.statBoostAmount > 0f)
            {
                var bonus = new Data.StatBlock();
                bonus.Set(evt.SelectedRune.statBoostType, evt.SelectedRune.statBoostAmount);
                AddRuneStat(bonus);
                Debug.Log($"[HeroController] 属性符文注入管线L4：" +
                          $"{evt.SelectedRune.statBoostType} +{evt.SelectedRune.statBoostAmount}");
            }
        }

        // =====================================================================
        //  属性重算重写（注入装备 StatBlock）
        // =====================================================================

        /// <summary>
        /// 重写属性重算：从 HeroEquipmentManager 拉取装备 StatBlock 列表
        /// 注入 _equipmentStats，然后调用基类的全量重算
        /// </summary>
        public override void RecalculateStats()
        {
            // 从装备管理器获取当前穿戴装备的属性块
            if (_equipmentMgr != null)
            {
                _equipmentStats = _equipmentMgr.GetEquipmentStatBlocks();
            }

            base.RecalculateStats();
        }
    }
}
