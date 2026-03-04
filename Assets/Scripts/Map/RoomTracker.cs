// ============================================================================
// 逃离魔塔 - 房间怪物追踪器 (RoomTracker)
// 追踪每个房间内的怪物数量，当房间内所有怪物被消灭时广播 OnRoomClearedEvent。
// 用于驱动宝箱通关锁和楼梯解锁。
//
// 来源：DesignDocs/06_Map_and_Modes.md §2.3
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Entity.Monster;

namespace EscapeTheTower.Map
{
    /// <summary>
    /// 房间怪物追踪器 —— 全局单例
    /// </summary>
    public class RoomTracker : MonoBehaviour
    {
        public static RoomTracker Instance { get; private set; }

        // === RoomID → 剩余怪物数 ===
        private readonly Dictionary<int, int> _roomMonsterCounts = new();

        // === EntityID → RoomID 反查表（死亡事件只携带 EntityID） ===
        private readonly Dictionary<int, int> _entityToRoom = new();

        // === 已清除的房间集合 ===
        private readonly HashSet<int> _clearedRooms = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            EventManager.Subscribe<OnEntityDeathEvent>(OnEntityDeath);
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<OnEntityDeathEvent>(OnEntityDeath);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // =====================================================================
        //  注册接口（怪物生成时调用）
        // =====================================================================

        /// <summary>
        /// 注册怪物到指定房间
        /// </summary>
        /// <param name="roomID">房间 ID</param>
        /// <param name="monster">怪物实体</param>
        public void RegisterMonster(int roomID, MonsterBase monster)
        {
            if (roomID <= 0) return; // 走廊怪不追踪

            monster.AssignedRoomID = roomID;

            if (_roomMonsterCounts.ContainsKey(roomID))
                _roomMonsterCounts[roomID]++;
            else
                _roomMonsterCounts[roomID] = 1;

            _entityToRoom[monster.EntityID] = roomID;
        }

        /// <summary>查询指定房间是否已清除</summary>
        public bool IsRoomCleared(int roomID) => _clearedRooms.Contains(roomID);

        /// <summary>查询指定房间的剩余怪物数</summary>
        public int GetRemainingMonsters(int roomID)
        {
            return _roomMonsterCounts.TryGetValue(roomID, out int count) ? count : 0;
        }

        // =====================================================================
        //  事件处理
        // =====================================================================

        private void OnEntityDeath(OnEntityDeathEvent evt)
        {
            // 反查该实体所属房间
            if (!_entityToRoom.TryGetValue(evt.EntityID, out int roomID)) return;

            _entityToRoom.Remove(evt.EntityID);

            if (!_roomMonsterCounts.ContainsKey(roomID)) return;

            _roomMonsterCounts[roomID]--;

            if (_roomMonsterCounts[roomID] <= 0)
            {
                _roomMonsterCounts.Remove(roomID);
                _clearedRooms.Add(roomID);

                // 广播房间清除事件
                EventManager.Publish(new OnRoomClearedEvent
                {
                    Meta = new EventMeta(evt.EntityID),
                    RoomID = roomID,
                });

                Debug.Log($"[RoomTracker] ✅ 房间 {roomID} 已清除！");
            }
        }

        /// <summary>清除所有追踪数据（切换楼层时调用）</summary>
        public void Clear()
        {
            _roomMonsterCounts.Clear();
            _entityToRoom.Clear();
            _clearedRooms.Clear();
        }
    }
}
