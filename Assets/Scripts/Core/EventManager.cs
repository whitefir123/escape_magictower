// ============================================================================
// 逃离魔塔 - 全局事件总线 (EventManager)
// 基于泛型的 Pub/Sub 模式，所有跨系统通信必须走此中央广播。
// 严禁脚本之间直接引用，严禁 GameObject.Find。
//
// 关键安全机制：
// 1. Generation 熔断器 —— 事件嵌套超过 3 层立即吞噬，防止 A→B→C→A 死循环
// 2. EventMeta 溯源 —— 每条事件携带 SourceID 与 Generation 代数
//
// 来源：DesignDocs/11_Events_and_NPC_Hooks.md
//       DesignDocs/13_Architecture_and_Operations_SLA.md
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeTheTower.Core
{
    /// <summary>
    /// 事件元数据 —— 追踪事件的来源与嵌套深度
    /// </summary>
    public struct EventMeta
    {
        /// <summary>事件发起者的唯一标识</summary>
        public int SourceID;

        /// <summary>当前事件的嵌套代数（首次发布为 1，被动触发链递增）</summary>
        public int Generation;

        public EventMeta(int sourceId, int generation = 1)
        {
            SourceID = sourceId;
            Generation = generation;
        }

        /// <summary>生成下一代元数据（用于被动触发链传递）</summary>
        public EventMeta NextGeneration()
        {
            return new EventMeta(SourceID, Generation + 1);
        }
    }

    /// <summary>
    /// 事件基类 —— 所有游戏事件结构体必须继承此接口
    /// </summary>
    public interface IGameEvent
    {
        EventMeta Meta { get; set; }
    }

    // =========================================================================
    //  预定义核心事件结构体
    //  来源：DesignDocs/11_Events_and_NPC_Hooks.md 第 1.1 节
    // =========================================================================

    /// <summary>战斗开局检测事件</summary>
    public struct OnBattleStartEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
    }

    /// <summary>承伤结算前事件（已算完理论伤害，准备真扣血的刹那）</summary>
    public struct OnEntityTakeDamageBeforeEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
        /// <summary>受击实体 ID</summary>
        public int TargetEntityID;
        /// <summary>攻击方实体 ID</summary>
        public int AttackerEntityID;
        /// <summary>理论伤害值（可被订阅者修改以实现劫持，如免死金牌）</summary>
        public float DamageAmount;
        /// <summary>伤害类型</summary>
        public DamageType DamageType;
        /// <summary>是否被劫持（设为 true 则主流程跳过扣血）</summary>
        public bool IsIntercepted;
    }

    /// <summary>实体成功击杀事件</summary>
    public struct OnEntityKillEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
        /// <summary>击杀者实体 ID</summary>
        public int KillerEntityID;
        /// <summary>被击杀实体 ID</summary>
        public int VictimEntityID;
    }

    /// <summary>进入新楼层事件</summary>
    public struct OnFloorEnterEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
        /// <summary>新楼层编号</summary>
        public int FloorNumber;
    }

    /// <summary>实体属性变更事件（管线重算完成后广播）</summary>
    public struct OnEntityStatChangedEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
        /// <summary>属性变更的实体 ID</summary>
        public int EntityID;
    }

    /// <summary>实体受到伤害后事件（已完成扣血）</summary>
    public struct OnEntityDamagedEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
        public int TargetEntityID;
        public int AttackerEntityID;
        public float FinalDamage;
        public DamageType DamageType;
        public bool WasCritical;
    }

    /// <summary>实体死亡事件（HP ≤ 0 已判定）</summary>
    public struct OnEntityDeathEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
        public int EntityID;
    }

    /// <summary>玩家升级事件</summary>
    public struct OnPlayerLevelUpEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
        public int NewLevel;
    }

    /// <summary>获取经验值事件</summary>
    public struct OnExpGainedEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
        public int Amount;
    }

    /// <summary>获取金币事件</summary>
    public struct OnGoldGainedEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
        public int Amount;
    }

    /// <summary>元素反应触发事件</summary>
    public struct OnElementalReactionEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
        public int TargetEntityID;
        public ElementalReactionType ReactionType;
    }

    /// <summary>符文三选一候选就绪事件（通知 UI 弹窗）</summary>
    public struct OnRuneDraftReadyEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
        /// <summary>触发途径（KillDrop=属性符文, LevelUp=机制符文）</summary>
        public RuneAcquisitionType AcquisitionType;
    }

    /// <summary>符文三选一结束事件（通知系统恢复游戏）</summary>
    public struct OnRuneDraftCompleteEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
        /// <summary>选中的符文数据（放弃选择时为 null）</summary>
        public Data.RuneData_SO SelectedRune;
    }

    /// <summary>门被打开事件</summary>
    public struct OnDoorOpenedEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
        /// <summary>门等级</summary>
        public DoorTier DoorTier;
        /// <summary>门所在格子坐标</summary>
        public Vector2Int Position;
    }

    /// <summary>拾取物被拾取事件</summary>
    public struct OnItemPickedUpEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
        /// <summary>物品类型</summary>
        public PickupType ItemType;
        /// <summary>物品品质（消耗品用）</summary>
        public QualityTier Quality;
        /// <summary>拾取位置</summary>
        public Vector2Int Position;
        /// <summary>效果数值（回复量/金币数等）</summary>
        public int Value;
        /// <summary>装备数据（仅当 ItemType == Equipment 时有值）</summary>
        public Equipment.EquipmentData EquipData;
    }

    /// <summary>宝箱被打开事件</summary>
    public struct OnChestOpenedEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
        /// <summary>所属房间 ID（0 = 路途宝箱）</summary>
        public int RoomID;
        /// <summary>宝箱位置</summary>
        public Vector2Int Position;
    }

    /// <summary>房间清除事件（所有怪物被消灭后广播）</summary>
    public struct OnRoomClearedEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
        /// <summary>被清除的房间 ID</summary>
        public int RoomID;
    }

    /// <summary>楼层切换事件（切层完成后广播）</summary>
    public struct OnFloorTransitionEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
        /// <summary>旧楼层编号（首层为 0）</summary>
        public int OldFloorLevel;
        /// <summary>新楼层编号</summary>
        public int NewFloorLevel;
    }

    /// <summary>怪物掉落钥匙事件（击杀即时获得）</summary>
    public struct OnKeyDroppedEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
        /// <summary>掉落的钥匙类型</summary>
        public DoorTier KeyTier;
    }

    /// <summary>装备面板开关事件（由热键/UI按钮发布，EquipmentPanelUI 订阅）</summary>
    public struct OnEquipmentPanelToggleEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
        /// <summary>true=打开, false=关闭</summary>
        public bool IsOpen;
    }

    /// <summary>装备穿戴/卸除变更事件（穿戴/卸除完成后广播，通知 UI 刷新）</summary>
    public struct OnEquipmentChangedEvent : IGameEvent
    {
        public EventMeta Meta { get; set; }
        /// <summary>发生变更的槽位</summary>
        public EquipmentSlot Slot;
    }

    // =========================================================================
    //  EventManager 核心实现
    // =========================================================================

    /// <summary>
    /// 全局事件总线 —— 中央广播机制
    /// 使用方式：
    ///   订阅：EventManager.Subscribe&lt;OnEntityKillEvent&gt;(OnKill);
    ///   发布：EventManager.Publish(new OnEntityKillEvent { ... });
    ///   退订：EventManager.Unsubscribe&lt;OnEntityKillEvent&gt;(OnKill);
    /// </summary>
    public static class EventManager
    {
        // 以事件类型 Type 为 key，存储该类型的所有订阅者委托
        private static readonly Dictionary<Type, Delegate> _eventTable = new Dictionary<Type, Delegate>();

        // 当前正在处理的事件 Generation 深度（用于熔断检测）
        private static int _currentGeneration = 0;

        /// <summary>
        /// 场景加载前清空静态状态，防止 Domain Reload 关闭时残留已销毁对象的回调
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _eventTable.Clear();
            _currentGeneration = 0;
        }

        /// <summary>
        /// 订阅指定类型的事件
        /// </summary>
        public static void Subscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            var eventType = typeof(T);
            if (_eventTable.TryGetValue(eventType, out var existingDelegate))
            {
                _eventTable[eventType] = Delegate.Combine(existingDelegate, handler);
            }
            else
            {
                _eventTable[eventType] = handler;
            }
        }

        /// <summary>
        /// 退订指定类型的事件
        /// </summary>
        public static void Unsubscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            var eventType = typeof(T);
            if (_eventTable.TryGetValue(eventType, out var existingDelegate))
            {
                var remaining = Delegate.Remove(existingDelegate, handler);
                if (remaining == null)
                {
                    _eventTable.Remove(eventType);
                }
                else
                {
                    _eventTable[eventType] = remaining;
                }
            }
        }

        /// <summary>
        /// 发布事件到所有订阅者
        /// 内置 Generation 熔断器：嵌套深度超过阈值时自动吞噬事件
        /// </summary>
        public static void Publish<T>(T gameEvent) where T : struct, IGameEvent
        {
            // === 熔断器检查 ===
            int eventGeneration = gameEvent.Meta.Generation;
            if (eventGeneration > GameConstants.MAX_EVENT_GENERATION)
            {
                Debug.LogWarning(
                    $"[EventManager 熔断] 事件 {typeof(T).Name} 嵌套深度 {eventGeneration} " +
                    $"超过阈值 {GameConstants.MAX_EVENT_GENERATION}，已被吞噬！" +
                    $"SourceID={gameEvent.Meta.SourceID}");
                return;
            }

            var eventType = typeof(T);
            if (_eventTable.TryGetValue(eventType, out var existingDelegate))
            {
                var callback = existingDelegate as Action<T>;
                if (callback != null)
                {
                    int previousGeneration = _currentGeneration;
                    _currentGeneration = eventGeneration;

                    try
                    {
                        callback.Invoke(gameEvent);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(
                            $"[EventManager 异常] 处理事件 {typeof(T).Name} 时发生异常：{ex.Message}\n{ex.StackTrace}");
                    }
                    finally
                    {
                        _currentGeneration = previousGeneration;
                    }
                }
            }
        }

        /// <summary>
        /// 获取当前事件处理的嵌套代数（供被动触发链内部使用）
        /// </summary>
        public static int CurrentGeneration => _currentGeneration;

        /// <summary>
        /// 清空所有事件订阅（场景切换或测试重置时使用）
        /// </summary>
        public static void ClearAll()
        {
            _eventTable.Clear();
            _currentGeneration = 0;
        }
    }
}
