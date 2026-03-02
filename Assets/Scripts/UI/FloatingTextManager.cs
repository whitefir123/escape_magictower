// ============================================================================
// 逃离魔塔 - 飘字系统 (FloatingTextManager)
// 战斗中的视觉反馈：伤害数字、治疗、经验获取等。
// 颜色/大小/动画根据伤害类型差异化显示。
//
// 来源：DesignDocs/07_UI_and_UX.md 第三节
// ============================================================================

using UnityEngine;
using EscapeTheTower.Core;

namespace EscapeTheTower.UI
{
    /// <summary>
    /// 飘字类型枚举
    /// </summary>
    public enum FloatingTextType
    {
        NormalDamage,       // 普通物理伤害（白色小字）
        CritDamage,         // 暴击伤害（红色放大 + 震动）
        MagicDamage,        // 魔法伤害（蓝色小字）
        ElementalReaction,  // 元素反应（特殊艺术字）
        Heal,               // 治疗（绿色）
        ExpGain,            // 经验获取（金色）
        GoldGain,           // 金币获取（黄色）
        Miss,               // 未命中 / 闪避
        Immune,             // 免疫
        LevelUp,            // 升级
    }

    /// <summary>
    /// 飘字管理器 —— 管理所有战斗飘字的生成与回收
    /// MVP 阶段使用简化实现（Debug.Log 输出），后续接入对象池 + UI Canvas
    /// </summary>
    public class FloatingTextManager : MonoBehaviour
    {
        // === 单例快捷访问（通过 ServiceLocator 注册） ===

        // =====================================================================
        //  公共接口
        // =====================================================================

        /// <summary>
        /// 在指定世界位置显示飘字
        /// </summary>
        /// <param name="worldPosition">世界坐标位置</param>
        /// <param name="text">显示文本</param>
        /// <param name="type">飘字类型</param>
        public void Show(Vector3 worldPosition, string text, FloatingTextType type)
        {
            // MVP 阶段简化实现：在控制台输出并标注类型
            // 后续将替换为对象池 + TextMeshPro + 动画曲线
            Color color = GetColorForType(type);
            float scale = GetScaleForType(type);

            Debug.Log($"[飘字 {type}] '{text}' at {worldPosition} color={color} scale={scale}");

            // TODO: 从对象池获取飘字实例
            // TODO: 设置颜色、大小、动画
            // TODO: 自动回收
        }

        /// <summary>
        /// 快捷方法：显示伤害数字
        /// </summary>
        public void ShowDamage(Vector3 worldPosition, float damage, bool isCrit, DamageType damageType)
        {
            string text = Mathf.CeilToInt(damage).ToString();
            FloatingTextType type;

            if (isCrit)
            {
                type = FloatingTextType.CritDamage;
                text = text + "!"; // 暴击加感叹号
            }
            else if (damageType == DamageType.Magical)
            {
                type = FloatingTextType.MagicDamage;
            }
            else
            {
                type = FloatingTextType.NormalDamage;
            }

            Show(worldPosition, text, type);
        }

        /// <summary>
        /// 快捷方法：显示治疗
        /// </summary>
        public void ShowHeal(Vector3 worldPosition, float amount)
        {
            Show(worldPosition, $"+{Mathf.CeilToInt(amount)}", FloatingTextType.Heal);
        }

        /// <summary>
        /// 快捷方法：显示闪避
        /// </summary>
        public void ShowMiss(Vector3 worldPosition)
        {
            Show(worldPosition, "MISS", FloatingTextType.Miss);
        }

        /// <summary>
        /// 快捷方法：显示经验获取
        /// </summary>
        public void ShowExpGain(Vector3 worldPosition, int amount)
        {
            Show(worldPosition, $"+{amount} EXP", FloatingTextType.ExpGain);
        }

        /// <summary>
        /// 快捷方法：显示金币获取
        /// </summary>
        public void ShowGoldGain(Vector3 worldPosition, int amount)
        {
            Show(worldPosition, $"+{amount} G", FloatingTextType.GoldGain);
        }

        /// <summary>
        /// 快捷方法：显示元素反应名称
        /// </summary>
        public void ShowReaction(Vector3 worldPosition, ElementalReactionType reaction)
        {
            string name = GetReactionName(reaction);
            Show(worldPosition, name, FloatingTextType.ElementalReaction);
        }

        // =====================================================================
        //  内部工具
        // =====================================================================

        private Color GetColorForType(FloatingTextType type)
        {
            switch (type)
            {
                case FloatingTextType.NormalDamage:      return Color.white;
                case FloatingTextType.CritDamage:        return Color.red;
                case FloatingTextType.MagicDamage:       return new Color(0.4f, 0.6f, 1f);
                case FloatingTextType.ElementalReaction: return new Color(1f, 0.6f, 0f);
                case FloatingTextType.Heal:              return Color.green;
                case FloatingTextType.ExpGain:           return new Color(1f, 0.85f, 0f);
                case FloatingTextType.GoldGain:          return Color.yellow;
                case FloatingTextType.Miss:              return Color.gray;
                case FloatingTextType.Immune:            return Color.cyan;
                case FloatingTextType.LevelUp:           return new Color(1f, 0.9f, 0.3f);
                default:                                 return Color.white;
            }
        }

        private float GetScaleForType(FloatingTextType type)
        {
            switch (type)
            {
                case FloatingTextType.CritDamage:        return 1.8f;
                case FloatingTextType.ElementalReaction: return 1.5f;
                case FloatingTextType.LevelUp:           return 2.0f;
                default:                                 return 1.0f;
            }
        }

        private string GetReactionName(ElementalReactionType reaction)
        {
            switch (reaction)
            {
                case ElementalReactionType.Vaporize:        return "蒸发";
                case ElementalReactionType.Melt:            return "融化";
                case ElementalReactionType.Overload:        return "超载";
                case ElementalReactionType.Freeze:          return "冻结";
                case ElementalReactionType.ElectroCharged:  return "感电";
                case ElementalReactionType.Shatter:         return "碎冰";
                case ElementalReactionType.VenomBlast:      return "毒爆";
                default:                                    return "";
            }
        }
    }
}
