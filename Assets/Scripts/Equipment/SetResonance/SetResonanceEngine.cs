// ============================================================================
// 逃离魔塔 - 套装共鸣引擎 (SetResonanceEngine)
// 装备变更时的总调度器：词缀匹配 → 激活/停用被动
//
// 来源：GameData_Blueprints/07_Legendary_Equipment_System.md §三
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using EscapeTheTower.Data;
using EscapeTheTower.Entity;

namespace EscapeTheTower.Equipment.SetResonance
{
    /// <summary>
    /// 套装共鸣引擎 —— 管理所有套装被动的激活/停用
    /// 由 HeroEquipmentManager 在装备变更时调用
    /// </summary>
    public class SetResonanceEngine
    {
        /// <summary>已注册的套装定义</summary>
        private readonly List<SetResonanceDefinition_SO> _definitions = new();

        /// <summary>套装定义 → 对应被动实例映射</summary>
        private readonly Dictionary<SetResonanceDefinition_SO, ISetPassive> _passiveInstances = new();

        /// <summary>当前所有激活中的套装名称（供 UI 查询）</summary>
        private readonly List<string> _activeSetNames = new();

        /// <summary>持有者引用</summary>
        private EntityBase _owner;

        // =====================================================================
        //  初始化
        // =====================================================================

        /// <summary>
        /// 初始化引擎，注册所有套装定义
        /// </summary>
        /// <param name="definitions">所有套装 SO 定义</param>
        /// <param name="owner">装备持有者</param>
        public void Initialize(SetResonanceDefinition_SO[] definitions, EntityBase owner)
        {
            _owner = owner;
            _definitions.Clear();
            _passiveInstances.Clear();

            if (definitions == null) return;

            foreach (var def in definitions)
            {
                if (def == null) continue;
                _definitions.Add(def);

                // 根据 passiveClassName 实例化被动
                var passive = CreatePassiveInstance(def.passiveClassName);
                if (passive != null)
                {
                    _passiveInstances[def] = passive;
                }
            }

            Debug.Log($"[共鸣引擎] 初始化完成，注册 {_definitions.Count} 套定义，" +
                      $"实例化 {_passiveInstances.Count} 个被动");
        }

        // =====================================================================
        //  装备变更时重算
        // =====================================================================

        /// <summary>
        /// 重新评估所有套装共鸣状态
        /// 当任何装备槽位变更时调用
        /// </summary>
        /// <param name="equippedItems">当前 6 部位装备数组</param>
        public void Evaluate(EquipmentData[] equippedItems)
        {
            _activeSetNames.Clear();

            foreach (var def in _definitions)
            {
                int matched = def.CountMatchedSlots(equippedItems);
                var tier = MatchCountToTier(matched);

                if (!_passiveInstances.TryGetValue(def, out var passive)) continue;

                if (tier == ResonanceTier.None)
                {
                    // 不满足最低 2pc：停用
                    if (passive.ActiveTier != ResonanceTier.None)
                    {
                        passive.Deactivate();
                        Debug.Log($"[共鸣引擎] {def.setNameCN} 共鸣失效");
                    }
                }
                else
                {
                    // 激活或更新层级
                    if (passive.ActiveTier != tier)
                    {
                        passive.Activate(tier, _owner);
                        Debug.Log($"[共鸣引擎] ✨ {def.setNameCN} 共鸣升级 → {(int)tier}pc！" +
                                  $"（匹配 {matched}/6 部位）");
                    }

                    _activeSetNames.Add($"{def.setNameCN} ({(int)tier}pc)");
                }
            }
        }

        // =====================================================================
        //  属性管线接口
        // =====================================================================

        /// <summary>
        /// 收集所有激活套装提供的属性修正
        /// 供属性管线层级使用
        /// </summary>
        public StatBlock GetCombinedStatModifiers()
        {
            var combined = new StatBlock();

            foreach (var passive in _passiveInstances.Values)
            {
                if (passive.ActiveTier == ResonanceTier.None) continue;
                var mods = passive.GetStatModifiers();
                if (mods != null)
                {
                    combined.MergeAdd(mods);
                }
            }

            return combined;
        }

        /// <summary>
        /// 获取当前所有激活的套装名称列表（供 UI 显示）
        /// </summary>
        public IReadOnlyList<string> ActiveSetNames => _activeSetNames;

        // =====================================================================
        //  辅助
        // =====================================================================

        /// <summary>
        /// 匹配件数 → 共鸣层级
        /// </summary>
        private static ResonanceTier MatchCountToTier(int count)
        {
            if (count >= 6) return ResonanceTier.Six;
            if (count >= 4) return ResonanceTier.Four;
            if (count >= 2) return ResonanceTier.Two;
            return ResonanceTier.None;
        }

        /// <summary>
        /// 根据类名反射创建 ISetPassive 实例
        /// 如果类名为空或找不到，返回 null
        /// </summary>
        private static ISetPassive CreatePassiveInstance(string className)
        {
            if (string.IsNullOrEmpty(className)) return null;

            try
            {
                var type = Type.GetType(className);
                if (type == null)
                {
                    Debug.LogWarning($"[共鸣引擎] 找不到被动类: {className}");
                    return null;
                }

                if (!typeof(ISetPassive).IsAssignableFrom(type))
                {
                    Debug.LogError($"[共鸣引擎] {className} 未实现 ISetPassive 接口");
                    return null;
                }

                return (ISetPassive)Activator.CreateInstance(type);
            }
            catch (Exception e)
            {
                Debug.LogError($"[共鸣引擎] 创建被动实例失败: {className} → {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 清理所有被动（场景切换时调用）
        /// </summary>
        public void Cleanup()
        {
            foreach (var passive in _passiveInstances.Values)
            {
                passive.Deactivate();
            }
            _passiveInstances.Clear();
            _definitions.Clear();
            _activeSetNames.Clear();
        }
    }
}
