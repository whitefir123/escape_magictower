// ============================================================================
// 逃离魔塔 - 英雄装备管理器 (HeroEquipmentManager)
// 管理英雄的 6 个装备槽位和未穿戴装备背包。
// 输出所有已穿戴装备的 StatBlock 列表，供 AttributePipeline 层级3 使用。
//
// 交互链路：
//   OnItemPickedUpEvent (Equipment) → AddToInventory
//   UI/手动穿戴 → Equip → RecalculateStats
//
// 来源：DesignDocs/04_Equipment_and_Forge.md
//       GameData_Blueprints/06_Equipment_Affix_System.md
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Data;
using EscapeTheTower.Equipment.SetResonance;

namespace EscapeTheTower.Equipment
{
    /// <summary>
    /// 英雄装备管理器 —— 管理 6 槽位穿戴和装备背包
    /// 挂载在 Hero 同一 GameObject 上
    /// </summary>
    public class HeroEquipmentManager : MonoBehaviour
    {
        // =====================================================================
        //  数据
        // =====================================================================

        /// <summary>6 槽位已穿戴装备</summary>
        private readonly Dictionary<EquipmentSlot, EquipmentData> _equippedItems
            = new Dictionary<EquipmentSlot, EquipmentData>();

        /// <summary>未穿戴装备背包（暂存）</summary>
        private readonly List<EquipmentData> _inventory = new List<EquipmentData>();

        /// <summary>背包容量上限（5页×30格=150，后续可被天赋/符文扩展）</summary>
        [SerializeField] private int _inventoryCapacity = 150;

        [Header("套装共鸣")]
        [Tooltip("所有套装共鸣定义 SO（在 Inspector 中拖入）")]
        [SerializeField] private SetResonanceDefinition_SO[] _setDefinitions;

        /// <summary>套装共鸣引擎实例</summary>
        private SetResonanceEngine _resonanceEngine;

        // === 事件 ===
        /// <summary>穿戴变更事件：装备穿戴/卸除后触发</summary>
        public event Action OnEquipmentChanged;

        // =====================================================================
        //  生命周期
        // =====================================================================

        private void OnEnable()
        {
            EventManager.Subscribe<OnItemPickedUpEvent>(OnItemPickedUp);

            // 初始化套装共鸣引擎
            _resonanceEngine = new SetResonanceEngine();
            var owner = GetComponent<Entity.EntityBase>();
            if (_setDefinitions != null && owner != null)
            {
                _resonanceEngine.Initialize(_setDefinitions, owner);
            }

            // 订阅自身穿戴变更事件 → 自动重算共鸣
            OnEquipmentChanged += OnEquipChanged_EvaluateResonance;

            Debug.Log("[HeroEquipmentManager] OnEnable → 已订阅事件 + 初始化共鸣引擎");
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<OnItemPickedUpEvent>(OnItemPickedUp);
            OnEquipmentChanged -= OnEquipChanged_EvaluateResonance;
            _resonanceEngine?.Cleanup();
        }

        /// <summary>
        /// 装备变更时自动重算套装共鸣
        /// </summary>
        private void OnEquipChanged_EvaluateResonance()
        {
            _resonanceEngine?.Evaluate(GetEquippedItemsArray());
        }

        // =====================================================================
        //  事件处理
        // =====================================================================

        /// <summary>
        /// 处理装备拾取事件 —— 若对应槽位为空则自动穿戴，否则进入背包
        /// </summary>
        private void OnItemPickedUp(OnItemPickedUpEvent evt)
        {
            // 装备拾取逻辑已由 HeroController.OnArrivedAtTile 直接调用 Equip/AddToInventory 处理
            // 此回调仅用于调试追踪事件是否到达
            if (evt.ItemType == PickupType.Equipment && evt.EquipData != null)
            {
                Debug.Log($"[HeroEquipmentManager] EventBus 收到装备拾取通知（已由 HeroController 直接处理）：{evt.EquipData.GetDisplayName()}");
            }
        }

        // =====================================================================
        //  背包操作
        // =====================================================================

        /// <summary>
        /// 将装备加入背包
        /// </summary>
        public bool AddToInventory(EquipmentData equip)
        {
            if (equip == null) return false;

            if (_inventory.Count >= _inventoryCapacity)
            {
                Debug.LogWarning($"[装备背包] 背包已满({_inventoryCapacity})，无法添加 {equip.GetDisplayName()}");
                return false;
            }

            _inventory.Add(equip);
            Debug.Log($"[装备背包] 添加 [{equip.quality}] {equip.GetDisplayName()} (iPwr={equip.itemPower})");
            return true;
        }

        /// <summary>
        /// 从背包中移除装备
        /// </summary>
        public bool RemoveFromInventory(EquipmentData equip)
        {
            return _inventory.Remove(equip);
        }

        /// <summary>获取背包列表（只读副本）</summary>
        public IReadOnlyList<EquipmentData> GetInventory() => _inventory.AsReadOnly();

        /// <summary>获取背包当前数量</summary>
        public int InventoryCount => _inventory.Count;

        /// <summary>获取背包容量上限</summary>
        public int InventoryCapacity => _inventoryCapacity;

        /// <summary>
        /// 销毁背包中的装备（丢弃操作，不退回背包）
        /// </summary>
        public bool DiscardFromInventory(EquipmentData equip)
        {
            if (equip == null) return false;
            bool removed = _inventory.Remove(equip);
            if (removed)
            {
                Debug.Log($"[装备丢弃] [{equip.quality}] {equip.GetDisplayName()} 已销毁");
            }
            return removed;
        }

        // =====================================================================
        //  穿戴/卸除
        // =====================================================================

        /// <summary>
        /// 穿戴装备到对应槽位
        /// 若槽位已有装备，旧装备自动退回背包
        /// </summary>
        /// <param name="equip">要穿戴的装备</param>
        /// <returns>被替换的旧装备（若有），否则 null</returns>
        public EquipmentData Equip(EquipmentData equip)
        {
            if (equip == null) return null;

            EquipmentData old = null;

            // 如果目标槽位已有装备，先卸除
            if (_equippedItems.TryGetValue(equip.slot, out var existing))
            {
                old = existing;
                _inventory.Add(old); // 旧装备退回背包
            }

            // 从背包中移除（可能不在背包中，如直接从地面穿戴）
            _inventory.Remove(equip);

            // 穿戴到槽位
            _equippedItems[equip.slot] = equip;

            Debug.Log($"[装备穿戴] [{equip.quality}] {equip.GetDisplayName()} → {equip.slot}");

            // 触发穿戴变更（C# event + EventBus 双通道）
            OnEquipmentChanged?.Invoke();
            EventManager.Publish(new OnEquipmentChangedEvent
            {
                Meta = new EventMeta(0),
                Slot = equip.slot,
            });

            return old;
        }

        /// <summary>
        /// 卸除指定槽位的装备，放入背包
        /// </summary>
        /// <returns>卸除的装备（若有），否则 null</returns>
        public EquipmentData Unequip(EquipmentSlot slot)
        {
            if (!_equippedItems.TryGetValue(slot, out var equip)) return null;

            _equippedItems.Remove(slot);
            _inventory.Add(equip);

            Debug.Log($"[装备卸除] {equip.GetDisplayName()} ← {slot}");

            // 触发穿戴变更（C# event + EventBus 双通道）
            OnEquipmentChanged?.Invoke();
            EventManager.Publish(new OnEquipmentChangedEvent
            {
                Meta = new EventMeta(0),
                Slot = slot,
            });

            return equip;
        }

        /// <summary>
        /// 获取指定槽位的已穿戴装备
        /// </summary>
        public EquipmentData GetEquippedItem(EquipmentSlot slot)
        {
            _equippedItems.TryGetValue(slot, out var equip);
            return equip;
        }

        /// <summary>
        /// 检查指定槽位是否已穿戴装备
        /// </summary>
        public bool HasEquipped(EquipmentSlot slot)
        {
            return _equippedItems.ContainsKey(slot);
        }

        // =====================================================================
        //  管线对接
        // =====================================================================

        /// <summary>
        /// 获取所有已穿戴装备的 StatBlock 列表
        /// 直接输入 AttributePipeline.ApplyLayer3_Equipment
        /// </summary>
        public List<StatBlock> GetEquipmentStatBlocks()
        {
            var blocks = new List<StatBlock>(_equippedItems.Count);
            foreach (var kvp in _equippedItems)
            {
                if (kvp.Value != null)
                {
                    blocks.Add(kvp.Value.ToStatBlock());
                }
            }
            return blocks;
        }

        /// <summary>
        /// 获取套装共鸣提供的属性加成 StatBlock
        /// 供属性管线在 Layer3 之后追加使用
        /// </summary>
        public StatBlock GetSetResonanceStatBlock()
        {
            return _resonanceEngine?.GetCombinedStatModifiers() ?? new StatBlock();
        }

        /// <summary>
        /// 获取当前已穿戴装备的数组（6 部位，未穿戴为 null）
        /// </summary>
        public EquipmentData[] GetEquippedItemsArray()
        {
            var items = new EquipmentData[6];
            foreach (var kvp in _equippedItems)
            {
                int idx = (int)kvp.Key;
                if (idx >= 0 && idx < 6)
                    items[idx] = kvp.Value;
            }
            return items;
        }

        /// <summary>
        /// 获取套装共鸣引擎引用（供外部查询激活的被动）
        /// </summary>
        public SetResonanceEngine ResonanceEngine => _resonanceEngine;

        // =====================================================================
        //  调试
        // =====================================================================

        /// <summary>
        /// 输出当前穿戴状态
        /// </summary>
        public void DebugPrintEquipped()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[装备管理器] 当前穿戴状态：");

            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                if (_equippedItems.TryGetValue(slot, out var equip))
                {
                    sb.AppendLine($"  {slot}: [{equip.quality}] {equip.GetDisplayName()} (iPwr={equip.itemPower})");
                }
                else
                {
                    sb.AppendLine($"  {slot}: (空)");
                }
            }

            sb.AppendLine($"  背包: {_inventory.Count}/{_inventoryCapacity} 件");
            Debug.Log(sb.ToString());
        }
    }
}
