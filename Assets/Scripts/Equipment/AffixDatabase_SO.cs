// ============================================================================
// 逃离魔塔 - 词缀注册表 (AffixDatabase_SO)
// 汇总所有 AffixDefinition_SO 资产引用，提供按部位和类型的快速查询接口。
// 运行时首次查询时自动构建索引缓存。
//
// 来源：GameData_Blueprints/06_Equipment_Affix_System.md
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace EscapeTheTower.Equipment
{
    /// <summary>
    /// 词缀注册表 —— 持有全部词缀定义 SO 的引用列表
    /// 通过 Unity Inspector 拖入所有词缀资产
    /// </summary>
    [CreateAssetMenu(fileName = "AffixDatabase", menuName = "EscapeTheTower/Affix Database")]
    public class AffixDatabase_SO : ScriptableObject
    {
        [Header("全部词缀定义（在 Inspector 中拖入）")]
        [Tooltip("所有已创建的 AffixDefinition_SO 资产")]
        public List<AffixDefinition_SO> allAffixes = new List<AffixDefinition_SO>();

        // === 运行时索引缓存 ===
        private Dictionary<EquipmentSlot, List<AffixDefinition_SO>> _prefixBySlot;
        private Dictionary<EquipmentSlot, List<AffixDefinition_SO>> _suffixBySlot;
        private bool _isIndexBuilt = false;

        // =====================================================================
        //  索引构建
        // =====================================================================

        /// <summary>
        /// 构建按部位分类的词缀索引（首次查询时自动调用）
        /// </summary>
        public void BuildIndex()
        {
            _prefixBySlot = new Dictionary<EquipmentSlot, List<AffixDefinition_SO>>();
            _suffixBySlot = new Dictionary<EquipmentSlot, List<AffixDefinition_SO>>();

            // 初始化所有槽位的空列表
            var allSlots = System.Enum.GetValues(typeof(EquipmentSlot));
            foreach (EquipmentSlot slot in allSlots)
            {
                _prefixBySlot[slot] = new List<AffixDefinition_SO>();
                _suffixBySlot[slot] = new List<AffixDefinition_SO>();
            }

            // 分类注册
            foreach (var affix in allAffixes)
            {
                if (affix == null)
                {
                    Debug.LogWarning("[AffixDatabase] 发现 null 词缀引用，已跳过");
                    continue;
                }

                var targetDict = affix.slotType == AffixSlotType.Prefix
                    ? _prefixBySlot
                    : _suffixBySlot;

                foreach (var slot in affix.validSlots)
                {
                    if (targetDict.ContainsKey(slot))
                    {
                        targetDict[slot].Add(affix);
                    }
                }
            }

            _isIndexBuilt = true;
            Debug.Log($"[AffixDatabase] 索引构建完成，共 {allAffixes.Count} 条词缀");
        }

        // =====================================================================
        //  查询接口
        // =====================================================================

        /// <summary>
        /// 获取指定部位和类型（前缀/后缀）的所有可用词缀
        /// </summary>
        public List<AffixDefinition_SO> GetAffixesForSlot(
            EquipmentSlot slot, AffixSlotType slotType)
        {
            EnsureIndex();

            var dict = slotType == AffixSlotType.Prefix ? _prefixBySlot : _suffixBySlot;

            if (dict.TryGetValue(slot, out var list))
            {
                return list;
            }

            return new List<AffixDefinition_SO>();
        }

        /// <summary>
        /// 获取指定部位的全部可用词缀（前缀+后缀合并）
        /// </summary>
        public List<AffixDefinition_SO> GetAllAffixesForSlot(EquipmentSlot slot)
        {
            EnsureIndex();

            var result = new List<AffixDefinition_SO>();

            if (_prefixBySlot.TryGetValue(slot, out var prefixes))
                result.AddRange(prefixes);

            if (_suffixBySlot.TryGetValue(slot, out var suffixes))
                result.AddRange(suffixes);

            return result;
        }

        /// <summary>
        /// 按 affixID 查找词缀定义
        /// </summary>
        public AffixDefinition_SO FindByID(string affixID)
        {
            foreach (var affix in allAffixes)
            {
                if (affix != null && affix.affixID == affixID)
                    return affix;
            }
            return null;
        }

        /// <summary>
        /// 确保索引已构建
        /// </summary>
        private void EnsureIndex()
        {
            if (!_isIndexBuilt)
            {
                BuildIndex();
            }
        }

        // =====================================================================
        //  调试
        // =====================================================================

        /// <summary>
        /// 输出索引统计信息
        /// </summary>
        public void DebugPrintStats()
        {
            EnsureIndex();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[AffixDatabase] 索引统计：");

            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                int prefixCount = _prefixBySlot.ContainsKey(slot)
                    ? _prefixBySlot[slot].Count : 0;
                int suffixCount = _suffixBySlot.ContainsKey(slot)
                    ? _suffixBySlot[slot].Count : 0;
                sb.AppendLine($"  {slot}: 前缀={prefixCount} 后缀={suffixCount}");
            }

            Debug.Log(sb.ToString());
        }
    }
}
