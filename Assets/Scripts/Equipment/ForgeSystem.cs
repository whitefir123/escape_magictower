// ============================================================================
// 逃离魔塔 - 锻造系统 (ForgeSystem)
// 铁匠铺核心后端逻辑：装备强化、金币消耗、概率曲线。
//
// 强化规则（来源：DesignDocs/04_Equipment_and_Forge.md §2.1）：
//   +1~+3  = 100% 成功，新手保护
//   +4~+7  = 80%→50% 平缓衰减
//   +8~+15 = 40%→10% 深水区
//   +16+   = 5% 保底阈值
//   失败惩罚：降 1 级（+1~+3 区间免疫降级）
//   保护卷轴：失败时消耗卷轴，装备原级保留
// ============================================================================

using UnityEngine;

namespace EscapeTheTower.Equipment
{
    /// <summary>
    /// 强化结果枚举
    /// </summary>
    public enum EnhanceResult
    {
        Success,            // 成功升级
        FailedDowngrade,    // 失败并降级
        FailedProtected,    // 失败但卷轴保护（不降级）
        FailedSafe,         // 失败但处于安全区（+1~+3 不降级）
    }

    /// <summary>
    /// 锻造系统 —— 静态工具类
    /// </summary>
    public static class ForgeSystem
    {
        // === 强化常量 ===
        private const float ENHANCE_STAT_PER_LEVEL = 0.05f;     // 每级底座 +5%
        private const int   SAFE_ZONE_MAX = 3;                   // +1~+3 安全区（100% 成功，失败不降级）
        private const float FLOOR_SUCCESS_RATE = 0.05f;          // +16 及以上保底成功率
        private const int   BASE_ENHANCE_COST = 100;             // 基础强化金币
        private const float COST_EXPONENT = 1.35f;               // 金币指数增长系数

        // =====================================================================
        //  强化
        // =====================================================================

        /// <summary>
        /// 尝试强化装备
        /// </summary>
        /// <param name="equipment">待强化装备</param>
        /// <param name="useProtection">是否使用保护卷轴</param>
        /// <param name="rng">随机数生成器（支持种子定式，防止 S/L 刷结果）</param>
        /// <returns>强化结果</returns>
        public static EnhanceResult TryEnhance(EquipmentData equipment, bool useProtection, System.Random rng)
        {
            if (equipment == null) return EnhanceResult.FailedSafe;

            int currentLevel = equipment.enhanceLevel;
            float successRate = GetSuccessRate(currentLevel);

            // 投骰
            float roll = (float)rng.NextDouble();

            if (roll < successRate)
            {
                // 成功：等级 +1
                equipment.enhanceLevel++;
                Debug.Log($"[锻造] ✅ 强化成功！{equipment.GetDisplayName()} → +{equipment.enhanceLevel}" +
                          $"（成功率={successRate:P0}）");
                return EnhanceResult.Success;
            }
            else
            {
                // 失败
                if (currentLevel <= SAFE_ZONE_MAX)
                {
                    // 安全区：不降级
                    Debug.Log($"[锻造] ❌ 强化失败（安全区，不降级）{equipment.GetDisplayName()} +{currentLevel}");
                    return EnhanceResult.FailedSafe;
                }
                else if (useProtection)
                {
                    // 保护卷轴生效：不降级
                    Debug.Log($"[锻造] ❌ 强化失败（保护卷轴生效）{equipment.GetDisplayName()} +{currentLevel}");
                    return EnhanceResult.FailedProtected;
                }
                else
                {
                    // 降级：等级 -1
                    equipment.enhanceLevel = Mathf.Max(0, currentLevel - 1);
                    Debug.Log($"[锻造] ❌ 强化失败！降级 {equipment.GetDisplayName()} +{currentLevel} → +{equipment.enhanceLevel}");
                    return EnhanceResult.FailedDowngrade;
                }
            }
        }

        // =====================================================================
        //  概率曲线
        // =====================================================================

        /// <summary>
        /// 获取指定强化等级的成功率
        /// 分段插值：安全区 100%，中段线性衰减，高段保底 5%
        /// </summary>
        public static float GetSuccessRate(int currentLevel)
        {
            // 目标等级 = currentLevel + 1
            int targetLevel = currentLevel + 1;

            if (targetLevel <= SAFE_ZONE_MAX)
                return 1.0f;                            // +1~+3 = 100%

            if (targetLevel <= 7)
            {
                // +4~+7：80% → 50%（线性插值）
                float t = (targetLevel - 4) / 3f;       // 0 → 1
                return Mathf.Lerp(0.80f, 0.50f, t);
            }

            if (targetLevel <= 15)
            {
                // +8~+15：40% → 10%（线性插值）
                float t = (targetLevel - 8) / 7f;       // 0 → 1
                return Mathf.Lerp(0.40f, 0.10f, t);
            }

            // +16 及以上：保底 5%
            return FLOOR_SUCCESS_RATE;
        }

        // =====================================================================
        //  金币消耗
        // =====================================================================

        /// <summary>
        /// 计算强化到下一级所需的金币
        /// 公式：base * (level + 1) ^ exponent
        /// </summary>
        public static int GetEnhanceCost(int currentLevel)
        {
            int targetLevel = currentLevel + 1;
            return Mathf.CeilToInt(BASE_ENHANCE_COST * Mathf.Pow(targetLevel, COST_EXPONENT));
        }

        /// <summary>
        /// 获取强化提供的底座属性乘算倍率
        /// 供 UI 预览用
        /// </summary>
        public static float GetEnhanceMultiplier(int enhanceLevel)
        {
            return 1f + enhanceLevel * ENHANCE_STAT_PER_LEVEL;
        }
    }
}
