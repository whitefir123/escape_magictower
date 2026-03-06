// ============================================================================
// 逃离魔塔 - 楼层切换管理器 (FloorTransitionManager)
// 管理楼层的生成、销毁、传送循环。
//
// 职责：
//   1. 持有当前楼层状态（编号、FloorGrid、引用）
//   2. TransitionToNextFloor() 执行 14 步清理-生成-传送管线
//   3. 提供 CurrentFloorGrid / CurrentFloorLevel 供外部查询
//
// 来源：DesignDocs/06_Map_and_Modes.md
// ============================================================================

using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Entity;
using EscapeTheTower.Entity.Monster;
using EscapeTheTower.Entity.Hero;
using EscapeTheTower.Data;

namespace EscapeTheTower.Map
{
    /// <summary>
    /// 楼层切换管理器 —— 全局单例
    /// </summary>
    public class FloorTransitionManager : MonoBehaviour
    {
        public static FloorTransitionManager Instance { get; private set; }

        [Header("=== 地图配置 ===")]
        [Tooltip("地图宽度（格）")]
        [SerializeField] private int mapWidth = 55;

        [Tooltip("地图高度（格）")]
        [SerializeField] private int mapHeight = 55;

        [Tooltip("基础随机种子（0 = 随机）")]
        [SerializeField] private int baseSeed = 0;

        [Header("=== 怪物配置 ===")]
        [Tooltip("楼层怪物配置 SO（从 Assets/Data/Monsters/ 拖入）")]
        [SerializeField] private FloorMonsterConfig_SO floorConfig;

        [Tooltip("是否生成 Boss")]
        [SerializeField] private bool spawnBoss = true;

        // === 运行时状态 ===
        /// <summary>当前楼层编号（从 1 开始）</summary>
        public int CurrentFloorLevel { get; private set; } = 0;

        /// <summary>当前楼层数据</summary>
        public FloorGrid CurrentFloorGrid { get; private set; }

        // === 缓存引用 ===
        private FloorRenderer _floorRenderer;
        private int _resolvedBaseSeed;

        // =====================================================================
        //  生命周期
        // =====================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _resolvedBaseSeed = baseSeed != 0 ? baseSeed : System.Environment.TickCount;

            // 自动加载楼层怪物配置（动态 AddComponent 场景无法手动拖拽）
            if (floorConfig == null)
            {
                floorConfig = Resources.Load<FloorMonsterConfig_SO>("Floor1Config");
                if (floorConfig != null)
                    Debug.Log("[FloorTransition] 自动加载 Floor1Config（from Resources）");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // =====================================================================
        //  公共接口
        // =====================================================================

        /// <summary>
        /// 初始化并生成第一层（由 TestSceneSetup 或 GameBootstrap 调用）
        /// </summary>
        public void InitializeFirstFloor()
        {
            CurrentFloorLevel = 0; // TransitionToNextFloor 会递增到 1
            TransitionToNextFloor();
        }

        /// <summary>
        /// 切换到下一层 —— 14 步清理-生成-传送管线
        /// </summary>
        public void TransitionToNextFloor()
        {
            int oldLevel = CurrentFloorLevel;
            CurrentFloorLevel++;

            Debug.Log($"[FloorTransition] ═══════════════════════════════════");
            Debug.Log($"[FloorTransition] 切换到第 {CurrentFloorLevel} 层...");

            // ── 1. 确保基础设施存在 ──
            EnsureInfrastructure();

            // ── 2. 销毁旧层全部动态实体 ──
            DestroyOldEntities();

            // ── 3. 清理旧层渲染 ──
            _floorRenderer.ClearFloor();

            // ── 4. 清理碰撞注册表 ──
            TilemapCollisionProvider.Instance?.ClearManualWalls();

            // ── 5. 清理房间追踪 ──
            RoomTracker.Instance?.Clear();

            // ── 6. 清理拾取物管理器 ──
            PickupManager.Instance?.Clear();

            // ── 7. 生成新楼层地图 ──
            int seed = _resolvedBaseSeed + CurrentFloorLevel;
            var config = BuildFloorConfig(CurrentFloorLevel);
            CurrentFloorGrid = FloorGenerator.Generate(mapWidth, mapHeight, seed, config);

            // ── 8. 渲染新地图 ──
            _floorRenderer.RenderFloor(CurrentFloorGrid);

            // ── 9. 重新绑定门交互 ──
            DoorInteraction.Instance?.Initialize(CurrentFloorGrid, _floorRenderer);

            // ── 10. 生成新层拾取物 ──
            PickupManager.Instance?.SpawnPickups(CurrentFloorGrid);

            // ── 11. 生成新层怪物和宝箱 ──
            SpawnMonstersInRooms();
            SpawnChestsInRooms();

            // ── 12. 传送英雄到新出生点 ──
            TeleportHeroToSpawn();

            // ── 12.5 初始化战争迷雾并揭示出生区域 ──
            var fogMgr = FogOfWarManager.Instance;
            if (fogMgr != null)
            {
                fogMgr.Initialize(CurrentFloorGrid, _floorRenderer);
                fogMgr.OnPlayerMoved(CurrentFloorGrid.SpawnPoint);
            }

            // ── 13. 锁定新楼梯 ──
            LockStaircase();

            // ── 14. 广播楼层切换事件 + 更新摄像机 ──
            UpdateCameraBounds();

            EventManager.Publish(new OnFloorTransitionEvent
            {
                Meta = new EventMeta(0),
                OldFloorLevel = oldLevel,
                NewFloorLevel = CurrentFloorLevel,
            });

            EventManager.Publish(new OnFloorEnterEvent
            {
                Meta = new EventMeta(0),
                FloorNumber = CurrentFloorLevel,
            });

            Debug.Log($"[FloorTransition] 第 {CurrentFloorLevel} 层部署完毕！" +
                      $"房间={CurrentFloorGrid.Rooms.Count}");
            Debug.Log($"[FloorTransition] ═══════════════════════════════════");
        }

        // =====================================================================
        //  生成配置（按楼层深度调整参数）
        //  来源：GameData_Blueprints/09_Consumable_and_Drop_Items.md §2
        // =====================================================================

        private FloorGenConfig BuildFloorConfig(int floorLevel)
        {
            var config = new FloorGenConfig
            {
                FloorLevel = floorLevel,
            };

            // 按楼层深度调整散布品质和数量
            if (floorLevel >= 7)
            {
                config.HealthPotionMin = 5;
                config.HealthPotionMax = 10;
                config.GoldPileMin = 3;
                config.GoldPileMax = 6;
            }
            else if (floorLevel >= 4)
            {
                config.HealthPotionMin = 6;
                config.HealthPotionMax = 12;
                config.GoldPileMin = 4;
                config.GoldPileMax = 8;
            }

            return config;
        }

        // =====================================================================
        //  基础设施确保
        // =====================================================================

        private void EnsureInfrastructure()
        {
            // 碰撞提供器
            if (TilemapCollisionProvider.Instance == null)
            {
                var obj = new GameObject("TilemapCollisionProvider");
                obj.AddComponent<TilemapCollisionProvider>();
            }

            // 楼层渲染器
            _floorRenderer = FindAnyObjectByType<FloorRenderer>();
            if (_floorRenderer == null)
            {
                var obj = new GameObject("FloorRenderer");
                _floorRenderer = obj.AddComponent<FloorRenderer>();
            }

            // 门交互
            if (DoorInteraction.Instance == null)
            {
                var obj = new GameObject("DoorInteraction");
                obj.AddComponent<DoorInteraction>();
            }

            // 房间追踪器
            if (RoomTracker.Instance == null)
            {
                var obj = new GameObject("RoomTracker");
                obj.AddComponent<RoomTracker>();
            }

            // 拾取物管理器
            if (PickupManager.Instance == null)
            {
                var obj = new GameObject("PickupManager");
                obj.AddComponent<PickupManager>();
            }

            // 战争迷雾
            if (FogOfWarManager.Instance == null)
            {
                var obj = new GameObject("FogOfWarManager");
                obj.AddComponent<FogOfWarManager>();
            }
        }

        // =====================================================================
        //  销毁旧层动态实体
        // =====================================================================

        private void DestroyOldEntities()
        {
            // 销毁所有怪物
            var monsters = FindObjectsByType<MonsterBase>(FindObjectsSortMode.None);
            foreach (var m in monsters) Destroy(m.gameObject);

            // 销毁所有宝箱
            var chests = FindObjectsByType<ChestEntity>(FindObjectsSortMode.None);
            foreach (var c in chests) Destroy(c.gameObject);

            // 销毁所有拾取物实体
            var pickups = FindObjectsByType<PickupItem>(FindObjectsSortMode.None);
            foreach (var p in pickups) Destroy(p.gameObject);
        }

        // =====================================================================
        //  英雄传送
        // =====================================================================

        private void TeleportHeroToSpawn()
        {
            var hero = FindAnyObjectByType<HeroController>();
            if (hero == null) return;

            var heroGrid = hero.GetComponent<GridMovement>();
            if (heroGrid != null)
            {
                heroGrid.SetGridPosition(CurrentFloorGrid.SpawnPoint);
            }
            else
            {
                hero.transform.position = new Vector3(
                    CurrentFloorGrid.SpawnPoint.x,
                    CurrentFloorGrid.SpawnPoint.y, 0f);
            }
        }

        // =====================================================================
        //  怪物生成（复用 TestSceneSetup 逻辑）
        // =====================================================================

        private void SpawnMonstersInRooms()
        {
            // 优先从 SO 资产读取，降级兜底到程序化注册
            MonsterData_SO[] registry;
            MonsterData_SO bossData;

            if (floorConfig != null && floorConfig.normalMonsters != null && floorConfig.normalMonsters.Length > 0)
            {
                // 过滤掉可能因资产引用丢失而变为 null 的条目
                var validMonsters = System.Array.FindAll(floorConfig.normalMonsters, m => m != null);
                if (validMonsters.Length > 0)
                {
                    registry = validMonsters;
                    bossData = floorConfig.bossData;
                    Debug.Log($"[FloorTransition] 从 SO 资产加载怪物池（{registry.Length} 种）");
                }
                else
                {
                    // SO 资产存在但所有引用丢失，降级
#pragma warning disable CS0618
                    registry = Floor1MonsterRegistry.GetAllNormalMonsters();
                    bossData = Floor1MonsterRegistry.GetBossData();
#pragma warning restore CS0618
                    Debug.LogWarning("[FloorTransition] floorConfig 怪物引用全部为 null，降级使用 Floor1MonsterRegistry");
                }
            }
            else
            {
                // 降级兜底：使用程序化注册（floorConfig 未配置时）
#pragma warning disable CS0618 // Obsolete 降级兜底，预期行为
                registry = Floor1MonsterRegistry.GetAllNormalMonsters();
                bossData = Floor1MonsterRegistry.GetBossData();
#pragma warning restore CS0618
                Debug.LogWarning("[FloorTransition] floorConfig 未配置，降级使用 Floor1MonsterRegistry");
            }
            var tracker = RoomTracker.Instance;

            // 使用楼层种子驱动的随机数（保证同种子可复现）
            var rng = new System.Random(_resolvedBaseSeed + CurrentFloorLevel * 1000 + 7);

            // 精英突变概率：基础5% + 每层+5%
            // 来源：GameData_Blueprints/04_01_Monster_Spawn_Logic.md §二
            float eliteChance = 0.05f + 0.05f * (CurrentFloorLevel - 1);

            int totalMonstersSpawned = 0;

            foreach (var room in CurrentFloorGrid.Rooms)
            {
                // 出生房间不放怪物
                if (room.Center == CurrentFloorGrid.SpawnPoint) continue;

                // Boss 房：生成 Boss 在楼梯附近
                if (room.Type == RoomType.Boss && spawnBoss && bossData != null)
                {
                    var stairsPos = CurrentFloorGrid.StairsPoint;
                    Vector3 bossPos = new Vector3(stairsPos.x, stairsPos.y, 0f);
                    var bossObj = SpawnMonster(bossData, bossPos, "Boss_堕落勇士", rng, false, 1f);
                    bossObj.transform.localScale = Vector3.one * 1.5f;
                    bossObj.AddComponent<FallenHeroBossAI>();

                    var bossMon = bossObj.GetComponent<MonsterBase>();
                    bossMon.PatrolOrigin = stairsPos;
                    bossMon.PatrolRadius = 4;
                    tracker?.RegisterMonster(room.RoomID, bossMon);
                    continue;
                }

                // 战斗房按面积密度生成怪物
                if (room.Type != RoomType.Combat) continue;

                // 怪物数量 = clamp(floor(面积 / 15), 1, 5)
                int roomArea = room.Bounds.width * room.Bounds.height;
                int monsterCount = Mathf.Clamp(roomArea / 15, 1, 5);

                // 收集房间内可用的 RoomFloor 格子（排除中心 ±1 格避免重叠）
                var availablePositions = new System.Collections.Generic.List<Vector2Int>();
                for (int x = room.Bounds.x; x < room.Bounds.xMax; x++)
                {
                    for (int y = room.Bounds.y; y < room.Bounds.yMax; y++)
                    {
                        if (CurrentFloorGrid.Tiles[x, y] == TileType.RoomFloor)
                        {
                            availablePositions.Add(new Vector2Int(x, y));
                        }
                    }
                }

                // Fisher-Yates 洗牌后取前 N 个位置
                for (int i = availablePositions.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (availablePositions[i], availablePositions[j]) = (availablePositions[j], availablePositions[i]);
                }

                for (int m = 0; m < monsterCount && m < availablePositions.Count; m++)
                {
                    // 随机选择怪物类型
                    var monsterData = registry[rng.Next(registry.Length)];
                    var spawnPos = availablePositions[m];
                    Vector3 pos = new Vector3(spawnPos.x, spawnPos.y, 0f);

                    // 精英突变检定
                    // 来源：04_01_Monster_Spawn_Logic.md §二
                    // 概率=5%+5%*(层数-1)，属性倍率3~5x，体积1.5x
                    bool isElite = rng.NextDouble() < eliteChance;
                    float eliteMult = isElite ? (3f + (float)rng.NextDouble() * 2f) : 1f;

                    string eliteTag = isElite ? "精英" : "";
                    var monsterObj = SpawnMonster(monsterData, pos,
                        $"怪物_{eliteTag}{monsterData.entityName}_{room.RoomID}_{m}",
                        rng, isElite, eliteMult);

                    var monster = monsterObj.GetComponent<MonsterBase>();
                    monster.PatrolOrigin = room.Center;
                    tracker?.RegisterMonster(room.RoomID, monster);
                    totalMonstersSpawned++;
                }
            }

            Debug.Log($"[FloorTransition] 第{CurrentFloorLevel}层 已生成 {totalMonstersSpawned} 只怪物" +
                $"（精英概率={eliteChance:P0}）" +
                (spawnBoss ? " + Boss" : ""));
        }

        /// <summary>生成单只怪物</summary>
        private GameObject SpawnMonster(MonsterData_SO data, Vector3 position, string name,
            System.Random rng, bool isElite, float eliteMult)
        {
            var obj = new GameObject(name);
            obj.transform.position = position;

            var monster = obj.AddComponent<MonsterBase>();

            // 通过反射设置 private SerializeField 字段
            var monsterDataField = typeof(MonsterBase).GetField("monsterData",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            monsterDataField?.SetValue(monster, data);

            var entityDataField = typeof(EntityBase).GetField("entityData",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            entityDataField?.SetValue(monster, data);

            // 同心圆距离系数（来源：04_01_Monster_Spawn_Logic.md §一）
            // 距出生点越远 → distanceFactor 越接近1.0 → 属性越偏向 Max 值
            float distanceFactor = CalculateDistanceFactor(position);
            monster.Initialize(distanceFactor, CurrentFloorLevel, isElite, eliteMult);
            return obj;
        }

        /// <summary>
        /// 计算同心圆距离系数 [0, 1]
        /// 来源：GameData_Blueprints/04_01_Monster_Spawn_Logic.md §一
        /// </summary>
        private float CalculateDistanceFactor(Vector3 monsterPos)
        {
            if (CurrentFloorGrid == null) return 0.5f;

            var spawn = CurrentFloorGrid.SpawnPoint;
            float dist = Mathf.Abs(monsterPos.x - spawn.x) + Mathf.Abs(monsterPos.y - spawn.y);
            float maxDist = CurrentFloorGrid.Width + CurrentFloorGrid.Height; // 理论最大曼哈顿距离
            return Mathf.Clamp01(dist / Mathf.Max(1f, maxDist * 0.5f)); // 归一化到 [0,1]
        }

        // =====================================================================
        //  宝箱生成
        // =====================================================================

        private void SpawnChestsInRooms()
        {
            int chestsSpawned = 0;
            var tracker = RoomTracker.Instance;
            foreach (var room in CurrentFloorGrid.Rooms)
            {
                bool shouldHaveChest = room.Type == RoomType.Combat ||
                                       room.Type == RoomType.Treasure ||
                                       room.Type == RoomType.Boss;
                if (!shouldHaveChest) continue;

                // 宝箱放在房间中心偏移 1 格
                Vector2Int chestPos = new Vector2Int(room.Center.x + 1, room.Center.y);
                if (!CurrentFloorGrid.InBounds(chestPos.x, chestPos.y) ||
                    CurrentFloorGrid.Tiles[chestPos.x, chestPos.y] == TileType.Wall)
                {
                    chestPos = room.Center;
                }

                var chestObj = new GameObject($"Chest_Room{room.RoomID}");
                chestObj.transform.position = new Vector3(chestPos.x, chestPos.y, 0f);
                var chest = chestObj.AddComponent<ChestEntity>();

                // 有怪物守护 → Locked（需清怪解锁）；无怪物 → Unlocked（直接可开）
                int lockRoomID = (tracker != null && tracker.GetRemainingMonsters(room.RoomID) > 0)
                    ? room.RoomID : 0;
                chest.Initialize(lockRoomID, chestPos);
                chestsSpawned++;
            }
            Debug.Log($"[FloorTransition] 已生成 {chestsSpawned} 个房间宝箱");

            // 走廊/死胡同宝箱（无锁，直接可开）
            int corridorChests = 0;
            foreach (var pos in CurrentFloorGrid.CorridorChestSpawns)
            {
                var obj = new GameObject($"Chest_Corridor_{pos.x}_{pos.y}");
                obj.transform.position = new Vector3(pos.x, pos.y, 0f);
                var chest = obj.AddComponent<ChestEntity>();
                chest.Initialize(0, pos); // roomID=0 → 初始 Unlocked
                corridorChests++;
            }
            if (corridorChests > 0)
                Debug.Log($"[FloorTransition] 已生成 {corridorChests} 个走廊宝箱");
        }

        // =====================================================================
        //  楼梯锁定（Boss 击败后解锁）
        // =====================================================================

        private void LockStaircase()
        {
            var collisionProvider = TilemapCollisionProvider.Instance;

            // 楼梯初始注册为碰撞墙壁
            collisionProvider?.RegisterWall(CurrentFloorGrid.StairsPoint);
            CurrentFloorGrid.StairsLocked = true;

            // 订阅 Boss 房间清除事件
            EventManager.Subscribe<OnRoomClearedEvent>(OnBossRoomCleared);

            Debug.Log($"[FloorTransition] 楼梯已锁定=({CurrentFloorGrid.StairsPoint.x}," +
                      $"{CurrentFloorGrid.StairsPoint.y})");
        }

        private void OnBossRoomCleared(OnRoomClearedEvent evt)
        {
            if (CurrentFloorGrid == null) return;

            foreach (var room in CurrentFloorGrid.Rooms)
            {
                if (room.RoomID == evt.RoomID && room.Type == RoomType.Boss)
                {
                    // 解锁楼梯
                    CurrentFloorGrid.StairsLocked = false;
                    TilemapCollisionProvider.Instance?.UnregisterWall(CurrentFloorGrid.StairsPoint);

                    Debug.Log($"[FloorTransition] 🏆 Boss 击败！楼梯已解锁！");

                    EventManager.Unsubscribe<OnRoomClearedEvent>(OnBossRoomCleared);
                    break;
                }
            }
        }

        // =====================================================================
        //  摄像机边界更新
        // =====================================================================

        private void UpdateCameraBounds()
        {
            var mainCam = Camera.main;
            if (mainCam == null) return;

            var follow = mainCam.GetComponent<CameraFollow>();
            if (follow != null && CurrentFloorGrid != null)
            {
                follow.SetBounds(0f, CurrentFloorGrid.Width, 0f, CurrentFloorGrid.Height);
            }
        }
    }
}
