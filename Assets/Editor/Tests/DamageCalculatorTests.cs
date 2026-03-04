// ============================================================================
// 逃离魔塔 - DamageCalculator 单元测试
// 测试伤害结算链路、经验值曲线计算
// ============================================================================

using NUnit.Framework;
using EscapeTheTower.Data;
using EscapeTheTower.Combat;

namespace EscapeTheTower.Tests
{
    [TestFixture]
    public class DamageCalculatorTests
    {
        // =====================================================================
        //  辅助方法
        // =====================================================================

        /// <summary>创建一个基础攻击者属性块</summary>
        private StatBlock CreateAttacker(float atk = 50f, float matk = 0f, float critRate = 0f)
        {
            var stats = new StatBlock();
            stats.Set(StatType.ATK, atk);
            stats.Set(StatType.MATK, matk);
            stats.Set(StatType.CritRate, critRate);
            stats.Set(StatType.CritMultiplier, 1.5f); // 默认暴击倍率
            return stats;
        }

        /// <summary>创建一个基础防御者属性块</summary>
        private StatBlock CreateDefender(float def = 10f, float mdef = 10f, float hp = 100f)
        {
            var stats = new StatBlock();
            stats.Set(StatType.DEF, def);
            stats.Set(StatType.MDEF, mdef);
            stats.Set(StatType.HP, hp);
            stats.Set(StatType.MaxHP, hp);
            stats.Set(StatType.Dodge, 0f); // 默认不闪避
            return stats;
        }

        // =====================================================================
        //  Calculate 基础测试
        // =====================================================================

        [Test]
        public void Calculate_物理伤害_基本结算()
        {
            var attacker = CreateAttacker(atk: 50f);
            var defender = CreateDefender(def: 10f);

            var result = DamageCalculator.Calculate(
                attacker, defender,
                baseDamage: 0f,
                atkScaling: 1.0f,
                damageType: DamageType.Physical
            );

            // 伤害应为正值（具体数值取决于减伤公式）
            Assert.Greater(result.FinalDamage, 0f, "物理伤害应大于0");
            Assert.AreEqual(DamageType.Physical, result.DamageType);
        }

        [Test]
        public void Calculate_魔法伤害_基本结算()
        {
            var attacker = CreateAttacker(matk: 40f);
            var defender = CreateDefender(mdef: 10f);

            var result = DamageCalculator.Calculate(
                attacker, defender,
                baseDamage: 0f,
                atkScaling: 1.0f,
                damageType: DamageType.Magical
            );

            Assert.Greater(result.FinalDamage, 0f, "魔法伤害应大于0");
            Assert.AreEqual(DamageType.Magical, result.DamageType);
        }

        [Test]
        public void Calculate_有baseDamage时伤害更高()
        {
            var attacker = CreateAttacker(atk: 50f);
            var defender = CreateDefender(def: 10f);

            var resultBase = DamageCalculator.Calculate(
                attacker, defender, baseDamage: 0f, atkScaling: 1.0f, damageType: DamageType.Physical);
            var resultWithExtra = DamageCalculator.Calculate(
                attacker, defender, baseDamage: 100f, atkScaling: 1.0f, damageType: DamageType.Physical);

            Assert.Greater(resultWithExtra.FinalDamage, resultBase.FinalDamage,
                "带 baseDamage 的伤害应更高");
        }

        [Test]
        public void Calculate_防御越高伤害越低()
        {
            var attacker = CreateAttacker(atk: 50f);
            var defenderLow = CreateDefender(def: 5f);
            var defenderHigh = CreateDefender(def: 50f);

            var resultLow = DamageCalculator.Calculate(
                attacker, defenderLow, baseDamage: 0f, atkScaling: 1.0f, damageType: DamageType.Physical);
            var resultHigh = DamageCalculator.Calculate(
                attacker, defenderHigh, baseDamage: 0f, atkScaling: 1.0f, damageType: DamageType.Physical);

            Assert.Greater(resultLow.FinalDamage, resultHigh.FinalDamage,
                "高防御应减少伤害");
        }

        [Test]
        public void Calculate_伤害不为负()
        {
            var attacker = CreateAttacker(atk: 1f);
            var defender = CreateDefender(def: 9999f);

            var result = DamageCalculator.Calculate(
                attacker, defender, baseDamage: 0f, atkScaling: 1.0f, damageType: DamageType.Physical);

            Assert.GreaterOrEqual(result.FinalDamage, 0f, "伤害不应为负数");
        }

        // =====================================================================
        //  GetExpRequiredForLevel
        // =====================================================================

        [Test]
        public void GetExpRequiredForLevel_等级1()
        {
            int exp = DamageCalculator.GetExpRequiredForLevel(1);
            Assert.AreEqual(GameConstants.BASE_EXP_REQUIRED, exp,
                "1级升级经验应等于基础值");
        }

        [Test]
        public void GetExpRequiredForLevel_递增()
        {
            int exp1 = DamageCalculator.GetExpRequiredForLevel(1);
            int exp2 = DamageCalculator.GetExpRequiredForLevel(2);
            int exp5 = DamageCalculator.GetExpRequiredForLevel(5);

            Assert.Greater(exp2, exp1, "2级经验应高于1级");
            Assert.Greater(exp5, exp2, "5级经验应高于2级");
        }

        [Test]
        public void GetExpRequiredForLevel_高等级不溢出()
        {
            int exp = DamageCalculator.GetExpRequiredForLevel(99);
            Assert.Greater(exp, 0, "高等级经验应为正值");
        }
    }
}
