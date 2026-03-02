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

        // === 已使用的主题（每局不重复） ===
        private List<BiomeConfig_SO> _usedBiomes = new List<BiomeConfig_SO>();

        // === 房间 ID 分配器 ===
        private int _nextRoomID = 1;

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
                Debug.Log("[MapManager] 第 9 层通关！触发轮回。");
                // TODO: 触发轮回/胜利事件
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
        /// 程序化生成一层地图
        /// </summary>
        private void GenerateFloor(BiomeConfig_SO biome)
        {
            CurrentFloorData = new FloorData
            {
                FloorNumber = CurrentFloor,
                BiomeConfig = biome,
            };

            // 计算房间总数
            int totalRooms = Random.Range(biome.minRoomCount, biome.maxRoomCount + 1);

            // 起始房（玩家出生点）
            var startRoom = CreateRoom(RoomType.Combat, new Vector2Int(0, 0));
            startRoom.IsExplored = true; // 起始房自动探索
            CurrentFloorData.StartRoomID = startRoom.RoomID;
            CurrentRoomID = startRoom.RoomID;

            // 按权重分配房间类型
            int combatCount = Mathf.RoundToInt(totalRooms * biome.combatRoomWeight);
            int treasureCount = Mathf.RoundToInt(totalRooms * biome.treasureRoomWeight);
            int eventCount = totalRooms - combatCount - treasureCount - 2; // -2 是 Boss 房和楼梯房
            eventCount = Mathf.Max(0, eventCount);

            // 生成战斗房
            for (int i = 0; i < combatCount; i++)
            {
                Vector2Int pos = GetNextGridPosition(i + 1);
                CreateRoom(RoomType.Combat, pos);
            }

            // 生成奖励房
            for (int i = 0; i < treasureCount; i++)
            {
                Vector2Int pos = GetNextGridPosition(combatCount + i + 1);
                CreateRoom(RoomType.Treasure, pos);
            }

            // 生成事件房（随机选择具体类型）
            RoomType[] eventTypes = { RoomType.Merchant, RoomType.Forge, RoomType.Shrine, RoomType.Campfire };
            for (int i = 0; i < eventCount; i++)
            {
                RoomType eventType = eventTypes[Random.Range(0, eventTypes.Length)];
                Vector2Int pos = GetNextGridPosition(combatCount + treasureCount + i + 1);
                CreateRoom(eventType, pos);
            }

            // Boss 房（地图最远端）
            Vector2Int bossPos = GetNextGridPosition(totalRooms - 1);
            var bossRoom = CreateRoom(RoomType.Boss, bossPos);
            CurrentFloorData.BossRoomID = bossRoom.RoomID;

            // 楼梯房（紧邻 Boss 房后方）
            Vector2Int stairsPos = new Vector2Int(bossPos.x + 1, bossPos.y);
            var stairsRoom = CreateRoom(RoomType.Stairs, stairsPos);
            CurrentFloorData.StairsRoomID = stairsRoom.RoomID;

            // 连接相邻房间（简单线性连接，后续可替换为更复杂的图结构）
            ConnectRoomsLinear();

            Debug.Log($"[MapManager] 生成第 {CurrentFloor} 层地图，共 {CurrentFloorData.Rooms.Count} 个房间。");
        }

        /// <summary>
        /// 创建一个房间节点
        /// </summary>
        private RoomNode CreateRoom(RoomType type, Vector2Int gridPos)
        {
            var room = new RoomNode
            {
                RoomID = _nextRoomID++,
                Type = type,
                GridPosition = gridPos,
                IsExplored = false,
                IsCleared = false,
            };
            CurrentFloorData.Rooms.Add(room);
            return room;
        }

        /// <summary>
        /// 简单的网格位置生成（蛇形排列）
        /// </summary>
        private Vector2Int GetNextGridPosition(int index)
        {
            int row = index / 5;
            int col = (row % 2 == 0) ? (index % 5) : (4 - index % 5);
            return new Vector2Int(col, row);
        }

        /// <summary>
        /// 线性连接所有房间（按生成顺序首尾相连）
        /// </summary>
        private void ConnectRoomsLinear()
        {
            var rooms = CurrentFloorData.Rooms;
            for (int i = 0; i < rooms.Count - 1; i++)
            {
                rooms[i].ConnectedRoomIDs.Add(rooms[i + 1].RoomID);
                rooms[i + 1].ConnectedRoomIDs.Add(rooms[i].RoomID);
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
