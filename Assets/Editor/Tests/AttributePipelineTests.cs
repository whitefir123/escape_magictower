// ============================================================================
// 逃离魔塔 - AttributePipeline 单元测试
// 测试六层属性管线的重算逻辑：基础值 → 天赋 → 装备 → 符文 → 跨界协同 → Buff
// ============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using EscapeTheTower.Data;
using EscapeTheTower.Combat;

namespace EscapeTheTower.Tests
{
    [TestFixture]
    public class AttributePipelineTests
    {
        // =====================================================================
        //  辅助方法
        // =====================================================================

        /// <summary>创建一个测试用的基础 EntityData_SO</summary>
        private EntityData_SO CreateTestEntityData()
        {
            var data = UnityEngine.ScriptableObject.CreateInstance<EntityData_SO>();
            data.baseATK = 20f;
            data.baseDEF = 10f;
            data.baseMATK = 15f;
            data.baseMDEF = 8f;
            data.baseMaxHP = 100f;
            data.baseMaxMP = 50f;
            data.baseMoveSpeed = 1f;
            data.baseAttackSpeed = 1f;
            return data;
        }

        // =====================================================================
        //  RecalculateAll 基础测试
        // =====================================================================

        [Test]
        public void RecalculateAll_仅基础值_输出正确()
        {
            var pipeline = new AttributePipeline();
            var entityData = CreateTestEntityData();

            var result = pipeline.RecalculateAll(
                entityData,
                talentBonuses: new StatBlock(),
                equipmentStats: new List<StatBlock>(),
                runeStats: new List<StatBlock>(),
                buffStats: new List<StatBlock>()
            );

            Assert.AreEqual(20f, result.Get(StatType.ATK), 0.01f, "ATK 应等于基础白值");
            Assert.AreEqual(10f, result.Get(StatType.DEF), 0.01f, "DEF 应等于基础白值");
            Assert.AreEqual(100f, result.Get(StatType.MaxHP), 0.01f, "MaxHP 应等于基础白值");
        }

        [Test]
        public void RecalculateAll_符文叠加_属性正确增加()
        {
            var pipeline = new AttributePipeline();
            var entityData = CreateTestEntityData();

            var runeBonus = new StatBlock();
            runeBonus.Set(StatType.ATK, 5f);
            runeBonus.Set(StatType.DEF, 3f);

            var result = pipeline.RecalculateAll(
                entityData,
                talentBonuses: new StatBlock(),
                equipmentStats: new List<StatBlock>(),
                runeStats: new List<StatBlock> { runeBonus },
                buffStats: new List<StatBlock>()
            );

            Assert.AreEqual(25f, result.Get(StatType.ATK), 0.01f, "ATK 应 = 20 + 5");
            Assert.AreEqual(13f, result.Get(StatType.DEF), 0.01f, "DEF 应 = 10 + 3");
        }

        [Test]
        public void RecalculateAll_多枚符文累加()
        {
            var pipeline = new AttributePipeline();
            var entityData = CreateTestEntityData();

            var rune1 = new StatBlock();
            rune1.Set(StatType.ATK, 5f);

            var rune2 = new StatBlock();
            rune2.Set(StatType.ATK, 8f);

            var result = pipeline.RecalculateAll(
                entityData,
                talentBonuses: new StatBlock(),
                equipmentStats: new List<StatBlock>(),
                runeStats: new List<StatBlock> { rune1, rune2 },
                buffStats: new List<StatBlock>()
            );

            Assert.AreEqual(33f, result.Get(StatType.ATK), 0.01f, "ATK 应 = 20 + 5 + 8");
        }

        [Test]
        public void RecalculateAll_Buff叠加()
        {
            var pipeline = new AttributePipeline();
            var entityData = CreateTestEntityData();

            var buff = new StatBlock();
            buff.Set(StatType.ATK, 10f);
            buff.Set(StatType.DEF, -5f); // Debuff

            var result = pipeline.RecalculateAll(
                entityData,
                talentBonuses: new StatBlock(),
                equipmentStats: new List<StatBlock>(),
                runeStats: new List<StatBlock>(),
                buffStats: new List<StatBlock> { buff }
            );

            Assert.AreEqual(30f, result.Get(StatType.ATK), 0.01f, "ATK 应 = 20 + 10");
            Assert.AreEqual(5f, result.Get(StatType.DEF), 0.01f, "DEF 应 = 10 - 5");
        }

        // =====================================================================
        //  跨界协同测试
        // =====================================================================

        [Test]
        public void RecalculateAll_高ATK产生魔穿()
        {
            var pipeline = new AttributePipeline();
            var entityData = CreateTestEntityData();
            // 提高 ATK 到一个能产生可见魔穿的值
            entityData.baseATK = 200f;

            var result = pipeline.RecalculateAll(
                entityData,
                talentBonuses: new StatBlock(),
                equipmentStats: new List<StatBlock>(),
                runeStats: new List<StatBlock>(),
                buffStats: new List<StatBlock>()
            );

            float magicPen = result.Get(StatType.MagicPen);
            Assert.Greater(magicPen, 0f, "高 ATK 应产生跨界魔穿");
            Assert.LessOrEqual(magicPen, GameConstants.MAX_CROSS_MAGIC_PEN,
                "魔穿不应超过硬上限");
        }

        // =====================================================================
        //  钳制测试
        // =====================================================================

        [Test]
        public void RecalculateAll_属性不为负()
        {
            var pipeline = new AttributePipeline();
            var entityData = CreateTestEntityData();

            // 施加一个巨大的 Debuff
            var debuff = new StatBlock();
            debuff.Set(StatType.ATK, -9999f);

            var result = pipeline.RecalculateAll(
                entityData,
                talentBonuses: new StatBlock(),
                equipmentStats: new List<StatBlock>(),
                runeStats: new List<StatBlock>(),
                buffStats: new List<StatBlock> { debuff }
            );

            Assert.GreaterOrEqual(result.Get(StatType.ATK), 0f,
                "ATK 应被钳制为非负值");
        }

        // =====================================================================
        //  空输入安全性
        // =====================================================================

        [Test]
        public void RecalculateAll_空列表不崩溃()
        {
            var pipeline = new AttributePipeline();
            var entityData = CreateTestEntityData();

            Assert.DoesNotThrow(() =>
            {
                pipeline.RecalculateAll(
                    entityData,
                    talentBonuses: null,
                    equipmentStats: null,
                    runeStats: null,
                    buffStats: null
                );
            }, "传入 null 列表不应崩溃");
        }
    }
}
