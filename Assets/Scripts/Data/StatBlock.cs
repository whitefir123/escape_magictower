// ============================================================================
// 逃离魔塔 - 属性值对象 (StatBlock)
// 承载所有实体的数值属性，支持快照、克隆与对比。
// 内部使用 Dictionary<StatType, float> 存储，确保 O(1) 查表。
//
// 来源：DesignDocs/09_DataFlow_and_Status.md
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace EscapeTheTower.Data
{
    /// <summary>
    /// 属性块 —— 存储一个实体的全部属性数值
    /// 作为值对象在管线各层级之间传递和叠加
    /// </summary>
    [System.Serializable]
    public class StatBlock
    {
        private readonly Dictionary<StatType, float> _stats = new Dictionary<StatType, float>();

        public StatBlock() { }

        /// <summary>
        /// 拷贝构造（深拷贝）
        /// </summary>
        public StatBlock(StatBlock other)
        {
            if (other != null)
            {
                foreach (var kvp in other._stats)
                {
                    _stats[kvp.Key] = kvp.Value;
                }
            }
        }

        /// <summary>
        /// 获取指定属性值，不存在则返回 0
        /// </summary>
        public float Get(StatType type)
        {
            return _stats.TryGetValue(type, out var value) ? value : 0f;
        }

        /// <summary>
        /// 设置指定属性值
        /// </summary>
        public void Set(StatType type, float value)
        {
            _stats[type] = value;
        }

        /// <summary>
        /// 增加指定属性值（累加）
        /// </summary>
        public void Add(StatType type, float amount)
        {
            _stats[type] = Get(type) + amount;
        }

        /// <summary>
        /// 乘算指定属性值
        /// </summary>
        public void Multiply(StatType type, float multiplier)
        {
            _stats[type] = Get(type) * multiplier;
        }

        /// <summary>
        /// 将另一个 StatBlock 的所有属性累加到自身（逐项相加）
        /// </summary>
        public void MergeAdd(StatBlock other)
        {
            if (other == null) return;
            foreach (var kvp in other._stats)
            {
                Add(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// 深拷贝克隆
        /// </summary>
        public StatBlock Clone()
        {
            return new StatBlock(this);
        }

        /// <summary>
        /// 清空所有属性归零
        /// </summary>
        public void Reset()
        {
            _stats.Clear();
        }

        /// <summary>
        /// 获取所有已设置的属性类型（用于遍历）
        /// </summary>
        public IEnumerable<KeyValuePair<StatType, float>> GetAll()
        {
            return _stats;
        }

        /// <summary>
        /// 属性是否已被设置
        /// </summary>
        public bool Has(StatType type)
        {
            return _stats.ContainsKey(type);
        }

        /// <summary>
        /// 调试输出：打印所有非零属性
        /// </summary>
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("[StatBlock] ");
            foreach (var kvp in _stats)
            {
                if (Mathf.Abs(kvp.Value) > 0.001f)
                {
                    sb.Append($"{kvp.Key}={kvp.Value:F2} ");
                }
            }
            return sb.ToString();
        }
    }
}
