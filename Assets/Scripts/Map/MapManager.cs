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
        /// 从 FloorGrid 的物理连通性构建 RoomNode 间的邻接关系
        /// 两个房间共享走廊连接则视为相邻
        /// </summary>
        private void BuildRoomConnections()
        {
            if (CurrentGrid == null) return;

            // 扫描所有走廊格，检查是否连接两个不同房间
            var connectedPairs = new HashSet<(int, int)>();

            for (int x = 0; x < CurrentGrid.Width; x++)
            {
                for (int y = 0; y < CurrentGrid.Height; y++)
                {
                    int roomId = CurrentGrid.RoomMap[x, y];
                    if (roomId == 0) continue; // 走廊或墙壁

                    // 检查四邻格是否属于不同房间
                    CheckConnection(x + 1, y, roomId, connectedPairs);
                    CheckConnection(x - 1, y, roomId, connectedPairs);
                    CheckConnection(x, y + 1, roomId, connectedPairs);
                    CheckConnection(x, y - 1, roomId, connectedPairs);
                }
            }

            // 将连接关系写入 RoomNode
            foreach (var (a, b) in connectedPairs)
            {
                var roomA = CurrentFloorData.Rooms.Find(r => r.RoomID == a);
                var roomB = CurrentFloorData.Rooms.Find(r => r.RoomID == b);
                if (roomA != null && !roomA.ConnectedRoomIDs.Contains(b))
                    roomA.ConnectedRoomIDs.Add(b);
                if (roomB != null && !roomB.ConnectedRoomIDs.Contains(a))
                    roomB.ConnectedRoomIDs.Add(a);
            }
        }

        /// <summary>检查相邻格是否为不同房间，记录连接对</summary>
        private void CheckConnection(int nx, int ny, int currentRoomId, HashSet<(int, int)> pairs)
        {
            if (!CurrentGrid.InBounds(nx, ny)) return;
            int neighborRoomId = CurrentGrid.RoomMap[nx, ny];
            if (neighborRoomId > 0 && neighborRoomId != currentRoomId)
            {
                // 规范化为 (小ID, 大ID) 避免重复
                int a = Mathf.Min(currentRoomId, neighborRoomId);
                int b = Mathf.Max(currentRoomId, neighborRoomId);
                pairs.Add((a, b));
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
            var available = randomBiomePool.FindAll(b => !_usedBiomes.Contains(b));
            if (available.Count == 0) return null;

            var picked = available[Random.Range(0, available.Count)];
            _usedBiomes.Add(picked);
            return picked;
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
