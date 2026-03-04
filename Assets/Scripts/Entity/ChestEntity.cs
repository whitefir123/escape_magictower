// ============================================================================
// 逃离魔塔 - 宝箱实体 (ChestEntity)
// 房间宝箱（通关锁机制）和路途宝箱的运行时组件。
//
// 房间宝箱：需要清除房间内所有怪物后才能打开（OnRoomClearedEvent）
// 路途宝箱：OwnerRoomID = 0，初始即 Unlocked，碰撞即开
//
// 使用 GridMovement 作为碰撞占位（玩家碰宝箱触发 OnMoveBlocked）
//
// 来源：DesignDocs/06_Map_and_Modes.md §2.3
// ============================================================================

using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Map;

namespace EscapeTheTower.Entity
{
    /// <summary>
    /// 宝箱实体 —— 房间通关锁 / 路途直取
    /// </summary>
    public class ChestEntity : MonoBehaviour
    {
        /// <summary>宝箱当前状态</summary>
        public ChestState State { get; private set; } = ChestState.Locked;

        /// <summary>所属房间 ID（0 = 路途宝箱，无通关锁）</summary>
        public int OwnerRoomID { get; private set; }

        /// <summary>宝箱所在格子坐标</summary>
        public Vector2Int GridPosition { get; private set; }

        // === 格子移动（碰撞占位用） ===
        private GridMovement _gridMovement;

        // =====================================================================
        //  初始化
        // =====================================================================

        /// <summary>
        /// 初始化宝箱
        /// </summary>
        /// <param name="roomID">所属房间 ID（0 = 路途宝箱）</param>
        /// <param name="gridPos">格子坐标</param>
        public void Initialize(int roomID, Vector2Int gridPos)
        {
            OwnerRoomID = roomID;
            GridPosition = gridPos;

            // 路途宝箱初始即可打开
            State = (roomID == 0) ? ChestState.Unlocked : ChestState.Locked;

            // 格子移动组件（碰撞占位）
            _gridMovement = GetComponent<GridMovement>();
            if (_gridMovement == null)
            {
                _gridMovement = gameObject.AddComponent<GridMovement>();
            }
            _gridMovement.SetGridPosition(gridPos);
            _gridMovement.AllowBump = false; // 宝箱不会回弹

            // 订阅房间清除事件
            if (roomID > 0)
            {
                EventManager.Subscribe<OnRoomClearedEvent>(OnRoomCleared);
            }

            EnsureVisual();
        }

        private void OnDestroy()
        {
            if (OwnerRoomID > 0)
            {
                EventManager.Unsubscribe<OnRoomClearedEvent>(OnRoomCleared);
            }
        }

        // =====================================================================
        //  交互逻辑
        // =====================================================================

        /// <summary>
        /// 尝试打开宝箱（由 HeroController.OnGridMoveBlocked 调用）
        /// </summary>
        /// <param name="openerEntityID">开启者 EntityID</param>
        /// <returns>true = 成功打开</returns>
        public bool TryOpen(int openerEntityID)
        {
            if (State == ChestState.Opened)
            {
                Debug.Log("[宝箱] 这个宝箱已经打开过了。");
                return false;
            }

            if (State == ChestState.Locked)
            {
                Debug.Log("[宝箱] 🔒 宝箱被锁定！需要清除房间内所有怪物。");
                return false;
            }

            // 打开宝箱
            State = ChestState.Opened;

            // 广播事件
            EventManager.Publish(new OnChestOpenedEvent
            {
                Meta = new EventMeta(openerEntityID),
                RoomID = OwnerRoomID,
                Position = GridPosition,
            });

            Debug.Log($"[宝箱] 🎁 宝箱已打开！位置=({GridPosition.x},{GridPosition.y}) " +
                      (OwnerRoomID > 0 ? $"房间={OwnerRoomID}" : "路途宝箱"));

            // 更新视觉（变暗表示已开启）
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);

            // TODO: 生成奖励掉落（金币、装备、消耗品等）

            return true;
        }

        // =====================================================================
        //  事件处理
        // =====================================================================

        /// <summary>房间清除事件处理：匹配房间 ID 后解锁</summary>
        private void OnRoomCleared(OnRoomClearedEvent evt)
        {
            if (evt.RoomID != OwnerRoomID) return;
            if (State != ChestState.Locked) return;

            State = ChestState.Unlocked;
            Debug.Log($"[宝箱] 🔓 房间 {OwnerRoomID} 已清除，宝箱解锁！");

            // 视觉提示：颜色变亮
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(1.0f, 0.85f, 0.0f);
        }

        // =====================================================================
        //  视觉占位
        // =====================================================================

        private void EnsureVisual()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();

            var tex = new Texture2D(4, 4);
            Color[] px = new Color[16];
            Color chestColor = State == ChestState.Locked
                ? new Color(0.5f, 0.4f, 0.3f) // 棕灰色表示锁定
                : new Color(1.0f, 0.85f, 0.0f); // 金色表示可开启
            for (int i = 0; i < 16; i++) px[i] = chestColor;
            tex.SetPixels(px);
            tex.Apply();
            tex.filterMode = FilterMode.Point;

            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            sr.sortingOrder = 4;
        }
    }
}
