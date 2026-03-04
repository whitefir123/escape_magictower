// ============================================================================
// 逃离魔塔 - StatBlock 单元测试
// 测试属性值对象的核心操作：Get/Set/Add/Multiply/MergeAdd/Clone/Reset
// ============================================================================

using NUnit.Framework;
using EscapeTheTower.Data;

namespace EscapeTheTower.Tests
{
    [TestFixture]
    public class StatBlockTests
    {
        // =====================================================================
        //  Get / Set
        // =====================================================================

        [Test]
        public void Get_未设置的属性_返回0()
        {
            var block = new StatBlock();
            Assert.AreEqual(0f, block.Get(StatType.ATK));
        }

        [Test]
        public void Set_设置后Get返回正确值()
        {
            var block = new StatBlock();
            block.Set(StatType.ATK, 42f);
            Assert.AreEqual(42f, block.Get(StatType.ATK));
        }

        [Test]
        public void Set_覆盖已有值()
        {
            var block = new StatBlock();
            block.Set(StatType.DEF, 10f);
            block.Set(StatType.DEF, 25f);
            Assert.AreEqual(25f, block.Get(StatType.DEF));
        }

        // =====================================================================
        //  Add
        // =====================================================================

        [Test]
        public void Add_累加到空属性()
        {
            var block = new StatBlock();
            block.Add(StatType.HP, 50f);
            Assert.AreEqual(50f, block.Get(StatType.HP));
        }

        [Test]
        public void Add_累加到已有值()
        {
            var block = new StatBlock();
            block.Set(StatType.HP, 100f);
            block.Add(StatType.HP, 30f);
            Assert.AreEqual(130f, block.Get(StatType.HP));
        }

        [Test]
        public void Add_负值累加()
        {
            var block = new StatBlock();
            block.Set(StatType.ATK, 50f);
            block.Add(StatType.ATK, -20f);
            Assert.AreEqual(30f, block.Get(StatType.ATK));
        }

        // =====================================================================
        //  Multiply
        // =====================================================================

        [Test]
        public void Multiply_正常乘算()
        {
            var block = new StatBlock();
            block.Set(StatType.ATK, 20f);
            block.Multiply(StatType.ATK, 1.5f);
            Assert.AreEqual(30f, block.Get(StatType.ATK), 0.001f);
        }

        [Test]
        public void Multiply_乘以0()
        {
            var block = new StatBlock();
            block.Set(StatType.DEF, 100f);
            block.Multiply(StatType.DEF, 0f);
            Assert.AreEqual(0f, block.Get(StatType.DEF));
        }

        // =====================================================================
        //  MergeAdd
        // =====================================================================

        [Test]
        public void MergeAdd_合并两个StatBlock()
        {
            var a = new StatBlock();
            a.Set(StatType.ATK, 10f);
            a.Set(StatType.DEF, 5f);

            var b = new StatBlock();
            b.Set(StatType.ATK, 3f);
            b.Set(StatType.HP, 100f);

            a.MergeAdd(b);

            Assert.AreEqual(13f, a.Get(StatType.ATK)); // 10 + 3
            Assert.AreEqual(5f, a.Get(StatType.DEF));    // 不变
            Assert.AreEqual(100f, a.Get(StatType.HP));    // 新增
        }

        [Test]
        public void MergeAdd_传入null不崩溃()
        {
            var block = new StatBlock();
            block.Set(StatType.ATK, 10f);
            Assert.DoesNotThrow(() => block.MergeAdd(null));
            Assert.AreEqual(10f, block.Get(StatType.ATK));
        }

        // =====================================================================
        //  Clone
        // =====================================================================

        [Test]
        public void Clone_深拷贝独立()
        {
            var original = new StatBlock();
            original.Set(StatType.ATK, 50f);

            var clone = original.Clone();
            clone.Set(StatType.ATK, 999f);

            Assert.AreEqual(50f, original.Get(StatType.ATK));  // 原始不受影响
            Assert.AreEqual(999f, clone.Get(StatType.ATK));
        }

        // =====================================================================
        //  Reset
        // =====================================================================

        [Test]
        public void Reset_清空所有属性()
        {
            var block = new StatBlock();
            block.Set(StatType.ATK, 50f);
            block.Set(StatType.DEF, 30f);

            block.Reset();

            Assert.AreEqual(0f, block.Get(StatType.ATK));
            Assert.AreEqual(0f, block.Get(StatType.DEF));
        }

        // =====================================================================
        //  Has
        // =====================================================================

        [Test]
        public void Has_未设置返回false()
        {
            var block = new StatBlock();
            Assert.IsFalse(block.Has(StatType.ATK));
        }

        [Test]
        public void Has_设置后返回true()
        {
            var block = new StatBlock();
            block.Set(StatType.ATK, 0f); // 即使设为0也算已设置
            Assert.IsTrue(block.Has(StatType.ATK));
        }

        // =====================================================================
        //  拷贝构造
        // =====================================================================

        [Test]
        public void 拷贝构造_传入null不崩溃()
        {
            Assert.DoesNotThrow(() => new StatBlock(null));
        }
    }
}
