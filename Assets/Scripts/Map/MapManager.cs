// ============================================================================
// 逃离魔塔 - 地图管理器 (MapManager)
// 管理地图的程序化生成、房间连接、战争迷雾与楼层切换。
// 第一层暗黑地牢为固定起始层，后续层从主题池随机不重复抽取。
//
// 来源：DesignDocs/06_Map_and_Modes.md
//       GameData_Blueprints/03_World_Themes.md
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Data;

namespace EscapeTheTower.Map
{
    /// <summary>
    /// 房间节点 —— 地图中的单个房间数据
    /// </summary>
    [System.Serializable]
    public class RoomNode
    {
        /// <summary>房间唯一 ID</summary>
        public int RoomID;
        /// <summary>房间类型</summary>
        public RoomType Type;
        /// <summary>网格坐标</summary>
        public Vector2Int GridPosition;
        /// <summary>是否已探索（战争迷雾）</summary>
        public bool IsExplored;
        /// <summary>是否已通关（战斗房已清怪）</summary>
        public bool IsCleared;
        /// <summary>连接的相邻房间 ID 列表</summary>
        public List<int> ConnectedRoomIDs = new List<int>();
    }

    /// <summary>
    /// 楼层数据 —— 一层地图的完整信息
    /// </summary>
    [System.Serializable]
    public class FloorData
    {
        /// <summary>楼层编号 (1-9)</summary>
        public int FloorNumber;
        /// <summary>所用群系配置</summary>
        public BiomeConfig_SO BiomeConfig;
        /// <summary>该楼层的所有房间</summary>
        public List<RoomNode> Rooms = new List<RoomNode>();
        /// <summary>起始房间 ID</summary>
        public int StartRoomID;
        /// <summary>Boss 房间 ID</summary>
        public int BossRoomID;
        /// <summary>楼梯房间 ID</summary>
        public int StairsRoomID;
    }

    /// <summary>
    /// 地图管理器 —— 核心地图系统
    /// </summary>
    public class MapManager : MonoBehaviour
    {
        [Header("=== 群系配置池 ===")]
        [Tooltip("暗黑地牢（固定第一层）")]
        [SerializeField] private BiomeConfig_SO darkDungeonBiome;

        [Tooltip("随机主题池（8 个不同主题）")]
        [SerializeField] private List<BiomeConfig_SO> randomBiomePool = new List<BiomeConfig_SO>();

        /// <summary>当前楼层编号</summary>
        public int CurrentFloor { get; private set; } = 1;

        /// <summary>当前楼层数据</summary>
        public FloorData CurrentFloorData { get; private set; }

        /// <summary>当前玩家所在房间 ID</summary>
        public int CurrentRoomID { get; private set; }

        /// <summary>当前低层瓦片地图（供渲染器/碰撞系统使用）</summary>
        public FloorGrid CurrentGrid { get; private set; }

        // === 已使用的主题（每局不重复） ===
        private List<BiomeConfig_SO> _usedBiomes = new List<BiomeConfig_SO>();

        // === 房间 ID 分配器 ===
        private int _nextRoomID = 1;

        // === 默认地图尺寸 ===
        private const int DEFAULT_MAP_WIDTH = 51;
        private const int DEFAULT_MAP_HEIGHT = 51;

        // =====================================================================
        //  初始化
        // =====================================================================

        /// <summary>
        /// 开始新的轮回（初始化第一层）
        /// </summary>
        public void StartNewRun()
        {
            CurrentFloor = 1;
            _usedBiomes.Clear();
            _nextRoomID = 1;

            // 如果 Inspector 未手动拖入随机池，自动从 Resources 加载
            AutoLoadBiomePoolIfEmpty();

            GenerateFloor(darkDungeonBiome);

            // 广播进入新楼层事件
            EventManager.Publish(new OnFloorEnterEvent
            {
                Meta = new EventMeta(0),
                FloorNumber = CurrentFloor,
            });

            Debug.Log($"[MapManager] 新轮回开始！第 {CurrentFloor} 层：{darkDungeonBiome?.biomeName ?? "暗黑地牢"}");
        }

        /// <summary>
        /// 进入下一层（通过楼梯）
        /// </summary>
        public void AdvanceFloor()
        {
            CurrentFloor++;

            if (CurrentFloor > 9)
            {
                Debug.Log("[MapManager] 第 9 层通关！触发胜利事件。");
                EventManager.Publish(new OnVictoryEvent { Meta = new EventMeta(0) });
                return;
            }

            // 从随机池中不重复抽取
            BiomeConfig_SO nextBiome = PickRandomBiome();
            if (nextBiome == null)
            {
                Debug.LogError("[MapManager] 无法抽取下一层群系！随机池耗尽。");
                return;
            }

            GenerateFloor(nextBiome);

            EventManager.Publish(new OnFloorEnterEvent
            {
                Meta = new EventMeta(0),
                FloorNumber = CurrentFloor,
            });

            Debug.Log($"[MapManager] 进入第 {CurrentFloor} 层：{nextBiome.biomeName}");
        }

        // =====================================================================
        //  楼层生成
        // =====================================================================

        /// <summary>
        /// 程序化生成一层地图 —— 委托 FloorGenerator 生成实际瓦片，再构建高层 FloorData
        /// </summary>
        private void GenerateFloor(BiomeConfig_SO biome)
        {
            CurrentFloorData = new FloorData
            {
                FloorNumber = CurrentFloor,
                BiomeConfig = biome,
            };

            // 地图尺寸和种子
            int mapWidth = DEFAULT_MAP_WIDTH;
            int mapHeight = DEFAULT_MAP_HEIGHT;
            int seed = Random.Range(int.MinValue, int.MaxValue);

            // 从 BiomeConfig 构建 FloorGenConfig
            FloorGenConfig genConfig = null;
            if (biome != null)
            {
                genConfig = new FloorGenConfig
                {
                    TargetRoomCount = Random.Range(biome.minRoomCount, biome.maxRoomCount + 1),
                    FloorLevel = CurrentFloor,
                };
            }

            // 委托 FloorGenerator 生成完整瓦片地图
            CurrentGrid = FloorGenerator.Generate(mapWidth, mapHeight, seed, genConfig);

            // 从 FloorGrid 构建高层 FloorData（RoomData → RoomNode 映射）
            _nextRoomID = 1;
            foreach (var roomData in CurrentGrid.Rooms)
            {
                var roomNode = new RoomNode
                {
                    RoomID = roomData.RoomID,
                    Type = roomData.Type,
                    GridPosition = roomData.Center,
                    IsExplored = false,
                    IsCleared = false,
                };
                CurrentFloorData.Rooms.Add(roomNode);
                _nextRoomID = Mathf.Max(_nextRoomID, roomData.RoomID + 1);
            }

            // 从 FloorGrid 设置特殊房间 ID
            var spawnRoom = CurrentGrid.GetRoomAt(CurrentGrid.SpawnPoint.x, CurrentGrid.SpawnPoint.y);
            if (spawnRoom != null)
            {
                CurrentFloorData.StartRoomID = spawnRoom.RoomID;
                CurrentRoomID = spawnRoom.RoomID;
                // 起始房自动探索
                var startNode = CurrentFloorData.Rooms.Find(r => r.RoomID == spawnRoom.RoomID);
                if (startNode != null) startNode.IsExplored = true;
            }

            // 查找 Boss 房和楼梯房
            foreach (var roomData in CurrentGrid.Rooms)
            {
                if (roomData.Type == RoomType.Boss)
                    CurrentFloorData.BossRoomID = roomData.RoomID;
                if (roomData.Type == RoomType.Stairs)
                    CurrentFloorData.StairsRoomID = roomData.RoomID;
            }

            // 构建房间连接图（基于 FloorGrid 中房间入口的物理连通性）
            BuildRoomConnections();

            Debug.Log($"[MapManager] 生成第 {CurrentFloor} 层地图，" +
                      $"共 {CurrentFloorData.Rooms.Count} 个房间，" +
                      $"种子={seed}");
        }

        /// <summary>
        /// 接收外部生成的 FloorGrid，用它重建 CurrentFloorData。
        /// FloorTransitionManager 生成实际使用的地图后调用此方法，
        /// 确保 MapManager 的房间数据与玩家实际行走的地图一致。
        /// </summary>
        /// <param name="externalGrid">FloorTransitionManager 生成的实际地图</param>
        /// <param name="floorNumber">楼层编号</param>
        public void SyncWithGrid(FloorGrid externalGrid, int floorNumber)
        {
            if (externalGrid == null)
            {
                Debug.LogError("[MapManager] SyncWithGrid: 传入的 FloorGrid 为 null！");
                return;
            }

            // 替换当前地图引用
            CurrentGrid = externalGrid;
            CurrentFloor = floorNumber;

            // 重建 FloorData
            CurrentFloorData = new FloorData
            {
                FloorNumber = floorNumber,
            };

            _nextRoomID = 1;
            foreach (var roomData in externalGrid.Rooms)
            {
                var roomNode = new RoomNode
                {
                    RoomID = roomData.RoomID,
                    Type = roomData.Type,
                    GridPosition = roomData.Center,
                    IsExplored = false,
                    IsCleared = false,
                };
                CurrentFloorData.Rooms.Add(roomNode);
                _nextRoomID = Mathf.Max(_nextRoomID, roomData.RoomID + 1);
            }

            // 设置出生房
            var spawnRoom = externalGrid.GetRoomAt(externalGrid.SpawnPoint.x, externalGrid.SpawnPoint.y);
            if (spawnRoom != null)
            {
                CurrentFloorData.StartRoomID = spawnRoom.RoomID;
                CurrentRoomID = spawnRoom.RoomID;
                var startNode = CurrentFloorData.Rooms.Find(r => r.RoomID == spawnRoom.RoomID);
                if (startNode != null) startNode.IsExplored = true;
            }

            // 设置 Boss 房和楼梯房
            foreach (var roomData in externalGrid.Rooms)
            {
                if (roomData.Type == RoomType.Boss)
                    CurrentFloorData.BossRoomID = roomData.RoomID;
                if (roomData.Type == RoomType.Stairs)
                    CurrentFloorData.StairsRoomID = roomData.RoomID;
            }

            // 构建房间连接图
            BuildRoomConnections();

            Debug.Log($"[MapManager] SyncWithGrid 完成：第 {floorNumber} 层，" +
                      $"共 {CurrentFloorData.Rooms.Count} 个房间，" +
                      $"出生点=({externalGrid.SpawnPoint.x},{externalGrid.SpawnPoint.y})");
        }

        /// <summary>
        /// 从 FloorGrid 的物理连通性构建 RoomNode 间的邻接关系。
        /// 策略：从每个房间的入口(Entrance)出发 BFS 沿走廊格探索，
        /// 遇到属于不同房间的格子即记录两个房间相连。
        /// 这样可以正确检测通过走廊桥接的房间连接关系。
        /// </summary>
        private void BuildRoomConnections()
        {
            if (CurrentGrid == null) return;

            var connectedPairs = new HashSet<(int, int)>();
            var grid = CurrentGrid;

            // 四方向偏移
            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };

            foreach (var roomData in grid.Rooms)
            {
                int srcRoomID = roomData.RoomID;

                // 收集 BFS 起点：入口 + 房间边界相邻的可通行格
                var startTiles = new List<Vector2Int>();

                // 1) 从 RoomData.Entrances 出发
                foreach (var entrance in roomData.Entrances)
                    startTiles.Add(entrance);

                // 2) 补充：扫描房间地板的四邻，如果是可通行的非房间格也作为起点
                var b = roomData.Bounds;
                for (int x = b.x; x < b.xMax; x++)
                {
                    for (int y = b.y; y < b.yMax; y++)
                    {
                        if (grid.RoomMap[x, y] != srcRoomID) continue;
                        for (int d = 0; d < 4; d++)
                        {
                            int nx = x + dx[d], ny = y + dy[d];
                            if (!grid.InBounds(nx, ny)) continue;
                            if (grid.RoomMap[nx, ny] == 0 && grid.Tiles[nx, ny] != TileType.Wall)
                                startTiles.Add(new Vector2Int(nx, ny));
                        }
                    }
                }

                if (startTiles.Count == 0) continue;

                // BFS 沿走廊格探索，遇到不同房间即记录连接
                var visited = new HashSet<(int, int)>();
                var queue = new Queue<Vector2Int>();

                // 将房间自身格子全部标记为已访问（不回头搜索房间内部）
                for (int x = b.x; x < b.xMax; x++)
                    for (int y = b.y; y < b.yMax; y++)
                        visited.Add((x, y));

                foreach (var start in startTiles)
                {
                    if (!visited.Add((start.x, start.y))) continue;
                    queue.Enqueue(start);
                }

                while (queue.Count > 0)
                {
                    var cur = queue.Dequeue();

                    for (int d = 0; d < 4; d++)
                    {
                        int nx = cur.x + dx[d], ny = cur.y + dy[d];
                        if (!grid.InBounds(nx, ny)) continue;
                        if (!visited.Add((nx, ny))) continue;
                        if (grid.Tiles[nx, ny] == TileType.Wall) continue;

                        int targetRoom = grid.RoomMap[nx, ny];
                        if (targetRoom > 0 && targetRoom != srcRoomID)
                        {
                            // 找到了通过走廊连接的另一个房间
                            int a = Mathf.Min(srcRoomID, targetRoom);
                            int bId = Mathf.Max(srcRoomID, targetRoom);
                            connectedPairs.Add((a, bId));
                            // 不继续深入该房间内部，但边界格保留在已访问中
                            continue;
                        }

                        // 走廊格（RoomMap==0）→ 继续探索
                        if (targetRoom == 0)
                        {
                            queue.Enqueue(new Vector2Int(nx, ny));
                        }
                    }
                }
            }

            // 将连接关系写入 RoomNode
            foreach (var (a, bId) in connectedPairs)
            {
                var roomA = CurrentFloorData.Rooms.Find(r => r.RoomID == a);
                var roomB = CurrentFloorData.Rooms.Find(r => r.RoomID == bId);
                if (roomA != null && !roomA.ConnectedRoomIDs.Contains(bId))
                    roomA.ConnectedRoomIDs.Add(bId);
                if (roomB != null && !roomB.ConnectedRoomIDs.Contains(a))
                    roomB.ConnectedRoomIDs.Add(a);
            }

            // 调试日志
            int totalConnections = connectedPairs.Count;
            Debug.Log($"[MapManager] 房间连接图构建完毕：{totalConnections} 个连接对");
            foreach (var node in CurrentFloorData.Rooms)
            {
                Debug.Log($"  房间 {node.RoomID}({node.Type}) @ ({node.GridPosition.x},{node.GridPosition.y}) → 连接:[{string.Join(",", node.ConnectedRoomIDs)}]");
            }
        }

        // =====================================================================
        //  房间导航
        // =====================================================================

        /// <summary>
        /// 玩家移动到指定房间
        /// </summary>
        public bool MoveToRoom(int targetRoomID)
        {
            var currentRoom = CurrentFloorData.Rooms.Find(r => r.RoomID == CurrentRoomID);
            if (currentRoom == null) return false;

            // 检查是否相邻
            if (!currentRoom.ConnectedRoomIDs.Contains(targetRoomID))
            {
                Debug.Log("[MapManager] 目标房间不相邻！");
                return false;
            }

            CurrentRoomID = targetRoomID;
            var newRoom = CurrentFloorData.Rooms.Find(r => r.RoomID == targetRoomID);
            if (newRoom != null)
            {
                newRoom.IsExplored = true;
            }

            Debug.Log($"[MapManager] 移动到房间 {targetRoomID}（类型={newRoom?.Type}）");
            return true;
        }

        // =====================================================================
        //  群系随机抽取
        // =====================================================================

        /// <summary>
        /// 从随机池中不重复抽取一个群系
        /// </summary>
        private BiomeConfig_SO PickRandomBiome()
        {
            // 兜底：如果池子为空，尝试运行时加载
            AutoLoadBiomePoolIfEmpty();

            var available = randomBiomePool.FindAll(b => b != null && !_usedBiomes.Contains(b));
            if (available.Count == 0) return null;

            var picked = available[Random.Range(0, available.Count)];
            _usedBiomes.Add(picked);
            return picked;
        }

        /// <summary>
        /// 如果 Inspector 的 randomBiomePool 为空，自动从 Resources 目录加载全部 BiomeConfig SO
        /// </summary>
        private void AutoLoadBiomePoolIfEmpty()
        {
            if (randomBiomePool != null && randomBiomePool.Count > 0) return;

            var loaded = Resources.LoadAll<BiomeConfig_SO>("");
            if (loaded == null || loaded.Length == 0)
            {
                Debug.LogWarning("[MapManager] Resources 中未找到 BiomeConfig SO，请将 BiomeConfig 放入 Resources 目录。");
                return;
            }

            randomBiomePool = new List<BiomeConfig_SO>();
            foreach (var biome in loaded)
            {
                // 排除第一层（darkDungeonBiome 由专门字段管理）
                if (darkDungeonBiome != null && biome == darkDungeonBiome) continue;
                // 排除 biomeIndex == 1 的（即暗黑地牢）
                if (biome.biomeIndex <= 1) continue;
                randomBiomePool.Add(biome);
            }

            Debug.Log($"[MapManager] ✅ 自动加载 {randomBiomePool.Count} 个 BiomeConfig SO 到随机池。");
        }

        // =====================================================================
        //  查询接口
        // =====================================================================

        /// <summary>
        /// 获取指定房间
        /// </summary>
        public RoomNode GetRoom(int roomID)
        {
            return CurrentFloorData?.Rooms.Find(r => r.RoomID == roomID);
        }

        /// <summary>
        /// 获取当前房间
        /// </summary>
        public RoomNode GetCurrentRoom()
        {
            return GetRoom(CurrentRoomID);
        }

        /// <summary>
        /// 外部通知玩家进入了指定房间（由 FogOfWarManager 在物理移动时调用）
        /// 更新 CurrentRoomID 并标记为已探索
        /// </summary>
        public void SetPlayerRoom(int roomID)
        {
            if (roomID <= 0 || roomID == CurrentRoomID) return;

            CurrentRoomID = roomID;

            var node = GetRoom(roomID);
            if (node != null && !node.IsExplored)
            {
                node.IsExplored = true;
                Debug.Log($"[MapManager] 玩家进入房间 {roomID}（类型={node.Type}），标记为已探索");
            }
        }

        /// <summary>
        /// 标记当前房间已通关
        /// </summary>
        public void ClearCurrentRoom()
        {
            var room = GetCurrentRoom();
            if (room != null)
            {
                room.IsCleared = true;
                Debug.Log($"[MapManager] 房间 {room.RoomID} 已通关。");
            }
        }
    }
}
