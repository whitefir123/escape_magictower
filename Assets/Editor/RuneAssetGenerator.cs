// ============================================================================
// 逃离魔塔 - 符文资产自动生成器 (Editor Only)
// 一键根据 GameData_Blueprints/08_Destiny_Rune_System.md 数据蓝图
// 批量创建所有 RuneData_SO 并自动挂载到场景中的 RuneManager。
//
// 使用方式：Unity 顶部菜单 → EscapeTheTower → 生成全部符文资产
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using EscapeTheTower;
using EscapeTheTower.Core;
using EscapeTheTower.Data;
using EscapeTheTower.Rune;

namespace EscapeTheTower.Editor
{
    /// <summary>
    /// 符文资产自动生成器 —— 基于设计蓝图批量创建 RuneData_SO
    /// </summary>
    public static class RuneAssetGenerator
    {
        // 输出目录
        private const string OUTPUT_DIR = "Assets/Data/Runes";
        private const string ATTR_DIR = "Assets/Data/Runes/Attribute";
        private const string MECH_DIR = "Assets/Data/Runes/Mechanism";
        private const string CLASS_DIR = "Assets/Data/Runes/ClassSpecific";

        [MenuItem("EscapeTheTower/生成全部符文资产", priority = 110)]
        public static void GenerateAll()
        {
            // 确保目录存在
            EnsureDirectory(OUTPUT_DIR);
            EnsureDirectory(ATTR_DIR);
            EnsureDirectory(MECH_DIR);
            EnsureDirectory(CLASS_DIR);

            var attributePool = new List<RuneData_SO>();
            var mechanismPool = new List<RuneData_SO>();
            var classSpecificPool = new List<RuneData_SO>();

            // ================================================================
            //  §二.1 属性符文池（怪物掉落限定，KillDrop 类型）
            //  来源：08_Destiny_Rune_System.md §二.1
            //  10 种属性 × 5 档品质 = 50 个 SO
            //  品质概率复用机制符文权重表（55%/30%/11%/3.5%/0.5%）
            // ================================================================

            // 属性符文数值表：[类型ID, 名称, 描述, StatType, 凡品值, 稀有值, 罕见值, 史诗值, 传说值]
            var attrTable = new (string id, string name, string desc, StatType stat, float[] values)[]
            {
                ("attr_max_hp",    "生命",     "直接提升最大生命值",      StatType.MaxHP,       new[] { 2f,    4f,    10f,   30f,   70f }),
                ("attr_max_mp",    "法力",     "直接提升最大法力池上限",  StatType.MaxMP,       new[] { 2f,    4f,    10f,   30f,   70f }),
                ("attr_atk",       "锋利",     "直接提升物理攻击力",      StatType.ATK,         new[] { 1f,    3f,    10f,   20f,   50f }),
                ("attr_matk",      "魔力",     "直接提升魔法攻击力",      StatType.MATK,        new[] { 1f,    3f,    10f,   20f,   50f }),
                ("attr_def",       "铁壁",     "直接提升物理防御",        StatType.DEF,         new[] { 1f,    3f,    10f,   20f,   50f }),
                ("attr_mdef",      "抗魔",     "直接提升魔法抗性",        StatType.MDEF,        new[] { 1f,    3f,    10f,   20f,   50f }),
                ("attr_movespeed", "迅捷",     "提升移动速度基数",        StatType.MoveSpeed,   new[] { 0.05f, 0.08f, 0.12f, 0.25f, 0.5f }),
                ("attr_crit",      "专注",     "提升基础暴击率",          StatType.CritRate,    new[] { 0.005f,0.01f, 0.015f,0.025f,0.08f }),
                ("attr_atkspeed",  "快速打击", "直接提升攻击速度基数",    StatType.AttackSpeed, new[] { 0.02f, 0.04f, 0.10f, 0.20f, 0.5f }),
                ("attr_cdr",       "灵光",     "提升各类技能冷却缩减",    StatType.AttackSpeed, new[] { 0.005f,0.01f, 0.03f, 0.05f, 0.10f }),
            };

            RuneRarity[] rarities = { RuneRarity.Common, RuneRarity.Rare, RuneRarity.Exceptional, RuneRarity.Epic, RuneRarity.Legendary };
            string[] rarityNames = { "凡品", "稀有", "罕见", "史诗", "传说" };
            string[] raritySuffixes = { "c", "r", "e", "ep", "l" };

            foreach (var attr in attrTable)
            {
                for (int r = 0; r < rarities.Length; r++)
                {
                    string id = $"{attr.id}_{raritySuffixes[r]}";
                    string desc = $"{attr.desc} +{attr.values[r]}";
                    attributePool.Add(CreateAttributeRune(
                        id, attr.name, desc,
                        attr.stat, attr.values[r], rarities[r]));
                }
            }

            // ================================================================
            //  §二.2 凡品机制符文 (Common)
            //  来源：08_Destiny_Rune_System.md §二.2
            // ================================================================

            mechanismPool.Add(CreateMechanismRune(
                "mech_c_spring", "回春术", RuneRarity.Common,
                "每次完成专属闪避动作，恢复 2 点生命值。\n[重复获取升级] 每升 1 级，恢复量 +1 点。",
                "闪避回血", "每级 +1 回复量"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_c_static", "静电摩擦", RuneRarity.Common,
                "每次连续移动超过 3 秒，下一次普攻附带 5 点微弱但必中的雷电真实伤害。\n[重复获取升级] 每升 1 级，附加雷伤 +5 点。",
                "移动蓄电", "每级 +5 雷伤"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_c_scavenger", "拾荒者", RuneRarity.Common,
                "拾取任何掉落物或金币时，自身获得持续 1 秒的 5% 移动速度加成。\n[重复获取升级] 每升 1 级，加速时间延长 0.5 秒。",
                "拾取加速", "每级 +0.5s 时长"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_c_hedgehog", "刺猬外壳", RuneRarity.Common,
                "被近战怪物攻击时，立即对前方反击 1 点不可豁免的真实物理防卫伤害。\n[重复获取升级] 每升 1 级，反弹真伤 +2 点。",
                "反伤", "每级 +2 真伤"));

            // ================================================================
            //  §二.2 稀有机制符文 (Rare)
            // ================================================================

            mechanismPool.Add(CreateMechanismRune(
                "mech_r_bloodblade", "饮血之刃", RuneRarity.Rare,
                "每次击杀任意怪物，立即恢复 (1% * MaxHP + 3) 点真实血量。\n[重复获取升级] 每升 1 级，基础恢复量附加 +2 点。",
                "击杀回血", "每级 +2 回复基数"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_r_flame", "烈焰附魔", RuneRarity.Rare,
                "你的每一次普通攻击，附带等同于 (15%ATK + 15%MATK) 的额外火属性真实伤害。\n[重复获取升级] 每升 1 级，加成倍率各提升 +5%。",
                "火焰附魔", "每级 +5% 双攻倍率"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_r_kinetic", "动能护盾", RuneRarity.Rare,
                "进入新房间的开场前 10 秒内，获得一个厚度为 (10%MaxHP + 30) 点的临时白盾。\n[重复获取升级] 每升 1 级，白盾倍率提升 +5%。",
                "开场护盾", "每级 +5% 护盾倍率"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_r_lone", "孤胆英雄", RuneRarity.Rare,
                "当周围 6 个身位内没有敌方单位时，基础暴击率绝对值额外提升 10%。\n[重复获取升级] 每升 1 级，额外附加 5 点固定双攻。",
                "孤立增伤", "每级 +5 双攻"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_r_thorns", "静电荆棘", RuneRarity.Rare,
                "受到近战攻击时，向攻击者发射静电，造成 (50%MDEF + 20%MATK) 点魔法伤害并减速 30% 持续 1.5 秒。\n[重复获取升级] 每升 1 级，倍率各 +10%。",
                "反击减速", "每级 +10% 倍率"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_r_revenge", "复仇刻印", RuneRarity.Rare,
                "每次受到敌方伤害后，下 3 次普攻额外附加 (30%DEF) 真实伤害。\n[重复获取升级] 每升 1 级，护甲收益倍率提升 +10%。",
                "受伤增攻", "每级 +10% 护甲倍率"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_r_momentum", "动量剥夺", RuneRarity.Rare,
                "每次击杀敌人，移动速度提升 0.02，持续 3 秒（最多叠 10 层）。\n[重复获取升级] 每升 1 级，单发偷取量 +0.01，上限 +0.05。",
                "击杀加速", "每级 +0.01 偷取量"));

            // ================================================================
            //  §二.2 罕见机制符文 (Exceptional)
            // ================================================================

            mechanismPool.Add(CreateMechanismRune(
                "mech_e_compound", "复合战防学", RuneRarity.Exceptional,
                "专属闪避成功后 2 秒内，下一次普攻附加 (100%DEF + 100%MDEF) 的物理冲压伤害。\n[重复获取升级] 每升 1 级，双防倍率 +30%。",
                "闪避重击", "每级 +30% 双防倍率"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_e_boilblood", "沸血战意", RuneRarity.Exceptional,
                "当前生命值每缺失 10 点，技能附加 (2%ATK) 真实伤害。\n[重复获取升级] 每升 1 级，ATK 倍率 +1%。",
                "缺血增伤", "每级 +1% ATK倍率"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_e_execute", "处刑宣告", RuneRarity.Exceptional,
                "攻击对生命极低的非 Boss 怪物（斩杀线 100%ATK+100%MATK），直接击杀并 AOE (200%ATK)。\n[重复获取升级] 每升 1 级，斩杀线/爆炸各 +50%。",
                "斩杀AOE", "每级 +50% 倍率"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_e_charge", "蓄力重击", RuneRarity.Exceptional,
                "3 秒未攻击后，下一次攻击引发冲击波 (150%ATK) 额外物理打击。\n[重复获取升级] 每升 1 级，冲击波倍率 +50%。",
                "蓄力爆发", "每级 +50% 倍率"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_e_shatter", "护甲碎裂者", RuneRarity.Exceptional,
                "暴击时削减目标 10 点双防，持续 4 秒。对 Boss 仅半效。\n[重复获取升级] 每升 1 级，单次削弱 +4 点。",
                "暴击破甲", "每级 +4 削甲"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_e_recovery", "复苏之风", RuneRarity.Exceptional,
                "完美闪避后 3 秒内每秒恢复 (2%MaxHP) 生命值。\n[重复获取升级] 每升 1 级，回复倍率 +1%。",
                "闪避回血", "每级 +1% 倍率"));

            // ================================================================
            //  §二.2 史诗机制符文 (Epic)
            // ================================================================

            mechanismPool.Add(CreateMechanismRune(
                "mech_ep_multishot", "多重投射学", RuneRarity.Epic,
                "释放投射物技能时额外分裂 2 发副投射物，造成 (30%MATK+30%ATK) 伤害。\n[重复获取升级] 每升 1 级，副投射物倍率各 +15%。",
                "投射分裂", "每级 +15% 倍率"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_ep_glasscannon", "玻璃大炮", RuneRarity.Epic,
                "双攻永久 +50 点固定加成，但双防永久锁定为 0。\n[重复获取升级] 每升 1 级，双攻再 +30 点。",
                "极端攻击", "每级 +30 双攻"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_ep_giant", "巨人骸骨", RuneRarity.Epic,
                "体积 +20%，抗击退提升，MaxHP +150 点，移速 -0.2。\n[重复获取升级] 每升 1 级，MaxHP +100 点。",
                "体型增大", "每级 +100 MaxHP"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_ep_weakness", "弱点追击", RuneRarity.Epic,
                "对异常状态敌人额外跳出 (50%×状态来源属性攻击力) 元素伤害。\n[重复获取升级] 每升 1 级，额外倍率 +25%。",
                "异常追伤", "每级 +25% 倍率"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_ep_vampire", "吸血鬼的优雅", RuneRarity.Epic,
                "所有治疗效果不再回血，以 100% 转化为白盾，且每次额外赠送 (5%MaxHP) 基础白盾。\n[重复获取升级] 每升 1 级，额外白盾倍率 +2%。",
                "治疗转盾", "每级 +2% 倍率"));

            // ================================================================
            //  §二.2 传说机制符文 (Legendary)
            // ================================================================

            mechanismPool.Add(CreateMechanismRune(
                "mech_l_ragnarok", "诸神黄昏的序曲", RuneRarity.Legendary,
                "释放大招后 6 秒进入神降状态：技能瞬发免蓝，普攻全屏雷击 (200%ATK+200%MATK)。\n[重复获取升级] 每升 1 级，雷击倍率各 +100%。",
                "大招强化", "每级 +100% 倍率"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_l_shield", "坚不可摧的最强之盾", RuneRarity.Legendary,
                "免疫所有负面状态，每 20 秒格挡一次伤害并发射 (300%DEF+300%MDEF) 神圣光束。\n[重复获取升级] 每升 1 级，双抗倍率各 +150%。",
                "绝对防御", "每级 +150% 倍率"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_l_prometheus", "普罗米修斯的火种", RuneRarity.Legendary,
                "每击注入不灭神火，每秒 (10%MATK+10%ATK) 真实灼烧，5 秒可无限刷新叠加。\n[重复获取升级] 每升 1 级，每层倍率各 +5%。",
                "永燃神火", "每级 +5% 倍率"));

            mechanismPool.Add(CreateMechanismRune(
                "mech_l_timelord", "时间领主的怀表", RuneRarity.Legendary,
                "本层死亡时触发：时间逆流至 5 秒前，恢复全部血量。仅触发一次，触发后永久每秒回复 (1%MaxHP)。\n[重复获取升级] 每秒回血倍率 +0.5%。",
                "死亡回溯", "每级 +0.5% 回血倍率"));

            // ================================================================
            //  §三 职业专属符文 (Class-Specific)
            //  来源：08_Destiny_Rune_System.md §三
            // ================================================================

            // --- 流浪剑客 ---
            classSpecificPool.Add(CreateClassRune(
                "class_sword_storm", "暴风眼", RuneRarity.Exceptional,
                HeroClass.VagabondSwordsman,
                "大招[极刃风暴]每跳恢复 (2%MaxHP) 生命值，结束时 2 秒眩晕。\n[重复获取升级] 每级 +1% 回血倍率。",
                "风暴吸血", "每级 +1% 倍率"));

            classSpecificPool.Add(CreateClassRune(
                "class_sword_iai", "居合拔刀术", RuneRarity.Epic,
                HeroClass.VagabondSwordsman,
                "闪避[燕返]距离 +50%，翻滚后 0.5 秒内下一次近战必暴，转化为 (250%ATK) 拔刀剑气。\n[重复获取升级] 每级 +50% 倍率。",
                "居合斩", "每级 +50% 倍率"));

            // --- 奥术学徒 ---
            classSpecificPool.Add(CreateClassRune(
                "class_mage_abyss", "极寒深渊", RuneRarity.Exceptional,
                HeroClass.ArcaneApprentice,
                "[冰霜新星]CD -3 秒，被冰冻敌人受击附加 (20%MATK) 冰霜真伤。\n[重复获取升级] 每级 +10% 倍率。",
                "冰冻追伤", "每级 +10% 倍率"));

            classSpecificPool.Add(CreateClassRune(
                "class_mage_meteor", "陨星阵列", RuneRarity.Epic,
                HeroClass.ArcaneApprentice,
                "大招[陨石天降]变异：砸下 8 颗跟踪小陨石，每颗追加 (40%MATK) 火伤。\n[重复获取升级] 每级 +20% 倍率。",
                "陨石分裂", "每级 +20% 倍率"));

            // --- 暗影刺客 ---
            classSpecificPool.Add(CreateClassRune(
                "class_assassin_venom", "生化毒师", RuneRarity.Exceptional,
                HeroClass.ShadowAssassin,
                "[烟雾弹]范围 +50%，烟雾内敌人每秒 (50%ATK) 毒伤。\n[重复获取升级] 每级 +25% 倍率。",
                "毒雾强化", "每级 +25% 倍率"));

            classSpecificPool.Add(CreateClassRune(
                "class_assassin_shadow", "双生暗影之舞", RuneRarity.Epic,
                HeroClass.ShadowAssassin,
                "大招[影之分身]期间每次攻击召唤 3 个残影夹击，单次 (100%ATK) 断喉伤害。\n[重复获取升级] 每级 +50% 倍率。",
                "分身追击", "每级 +50% 倍率"));

            // --- 虔诚圣骑 ---
            classSpecificPool.Add(CreateClassRune(
                "class_paladin_thorns", "荆棘林地", RuneRarity.Exceptional,
                HeroClass.DevoutPaladin,
                "[荆棘光环]对远程也反噬 (50%DEF+50%MDEF) 神圣光束。\n[重复获取升级] 每级 +25% 双防倍率。",
                "远程反伤", "每级 +25% 倍率"));

            classSpecificPool.Add(CreateClassRune(
                "class_paladin_charge", "无畏战车冲锋", RuneRarity.Epic,
                HeroClass.DevoutPaladin,
                "闪避[举盾突骑]取消 CD，持续架盾冲锋（消耗 1%MaxHP/秒），撞击 (200%DEF) 重砸。\n[重复获取升级] 每级 +100% 倍率。",
                "无限冲锋", "每级 +100% 倍率"));

            // ================================================================
            //  自动挂载到场景中的 RuneManager
            // ================================================================

            AutoAssignToRuneManager(attributePool, mechanismPool, classSpecificPool);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[RuneAssetGenerator] ✅ 生成完毕！" +
                      $"\n  属性符文: {attributePool.Count} 个（{ATTR_DIR}）" +
                      $"\n  机制符文: {mechanismPool.Count} 个（{MECH_DIR}）" +
                      $"\n  职业专属: {classSpecificPool.Count} 个（{CLASS_DIR}）" +
                      $"\n  总计: {attributePool.Count + mechanismPool.Count + classSpecificPool.Count} 个");
        }

        // =====================================================================
        //  辅助方法：创建属性符文 (KillDrop)
        // =====================================================================

        private static RuneData_SO CreateAttributeRune(
            string id, string name, string desc,
            StatType statType, float amount, RuneRarity rarity)
        {
            var rune = ScriptableObject.CreateInstance<RuneData_SO>();
            rune.runeID = id;
            rune.runeName = name;
            rune.description = desc;
            rune.rarity = rarity;
            rune.acquisitionType = RuneAcquisitionType.KillDrop;
            rune.statBoostType = statType;
            rune.statBoostAmount = amount;

            string path = Path.Combine(ATTR_DIR, $"{id}.asset");
            AssetDatabase.CreateAsset(rune, path);
            return rune;
        }

        // =====================================================================
        //  辅助方法：创建机制符文 (LevelUp, 通用池)
        // =====================================================================

        private static RuneData_SO CreateMechanismRune(
            string id, string name, RuneRarity rarity,
            string desc, string effectTag, string upgradeDesc)
        {
            var rune = ScriptableObject.CreateInstance<RuneData_SO>();
            rune.runeID = id;
            rune.runeName = name;
            rune.description = desc;
            rune.rarity = rarity;
            rune.acquisitionType = RuneAcquisitionType.LevelUp;
            rune.isClassSpecific = false;
            rune.effectTag = effectTag;
            rune.upgradeDescription = upgradeDesc;

            string path = Path.Combine(MECH_DIR, $"{id}.asset");
            AssetDatabase.CreateAsset(rune, path);
            return rune;
        }

        // =====================================================================
        //  辅助方法：创建职业专属符文 (LevelUp, 专属池)
        // =====================================================================

        private static RuneData_SO CreateClassRune(
            string id, string name, RuneRarity rarity,
            HeroClass heroClass, string desc,
            string effectTag, string upgradeDesc)
        {
            var rune = ScriptableObject.CreateInstance<RuneData_SO>();
            rune.runeID = id;
            rune.runeName = name;
            rune.description = desc;
            rune.rarity = rarity;
            rune.acquisitionType = RuneAcquisitionType.LevelUp;
            rune.isClassSpecific = true;
            rune.restrictedClass = heroClass;
            rune.effectTag = effectTag;
            rune.upgradeDescription = upgradeDesc;

            string path = Path.Combine(CLASS_DIR, $"{id}.asset");
            AssetDatabase.CreateAsset(rune, path);
            return rune;
        }

        // =====================================================================
        //  自动挂载到 RuneManager（通过 SerializedObject 修改）
        // =====================================================================

        private static void AutoAssignToRuneManager(
            List<RuneData_SO> attrPool,
            List<RuneData_SO> mechPool,
            List<RuneData_SO> classPool)
        {
            // 尝试在场景中找到 RuneManager
            var runeManager = Object.FindAnyObjectByType<RuneManager>();
            if (runeManager == null)
            {
                Debug.LogWarning("[RuneAssetGenerator] 未在场景中找到 RuneManager，请手动拖拽符文到对应池！");
                return;
            }

            var so = new SerializedObject(runeManager);

            // 属性符文池
            var attrProp = so.FindProperty("attributeRunePool");
            attrProp.ClearArray();
            for (int i = 0; i < attrPool.Count; i++)
            {
                attrProp.InsertArrayElementAtIndex(i);
                attrProp.GetArrayElementAtIndex(i).objectReferenceValue = attrPool[i];
            }

            // 机制符文池
            var mechProp = so.FindProperty("mechanismRunePool");
            mechProp.ClearArray();
            for (int i = 0; i < mechPool.Count; i++)
            {
                mechProp.InsertArrayElementAtIndex(i);
                mechProp.GetArrayElementAtIndex(i).objectReferenceValue = mechPool[i];
            }

            // 职业专属池
            var classProp = so.FindProperty("classSpecificRunePool");
            classProp.ClearArray();
            for (int i = 0; i < classPool.Count; i++)
            {
                classProp.InsertArrayElementAtIndex(i);
                classProp.GetArrayElementAtIndex(i).objectReferenceValue = classPool[i];
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(runeManager);

            Debug.Log($"[RuneAssetGenerator] ✅ 已自动挂载到 RuneManager（{runeManager.gameObject.name}）" +
                      $"\n  属性池: {attrPool.Count} | 机制池: {mechPool.Count} | 专属池: {classPool.Count}");

            // 同时自动赋值 GameBootstrapper 的 _runeManager 引用，防止 Inspector 未拖拽导致断链
            var bootstrapper = Object.FindAnyObjectByType<GameBootstrapper>();
            if (bootstrapper != null)
            {
                var bsSO = new SerializedObject(bootstrapper);
                var rmProp = bsSO.FindProperty("_runeManager");
                if (rmProp != null)
                {
                    rmProp.objectReferenceValue = runeManager;
                    bsSO.ApplyModifiedProperties();
                    EditorUtility.SetDirty(bootstrapper);
                    Debug.Log("[RuneAssetGenerator] ✅ 已自动赋值 GameBootstrapper._runeManager 引用");
                }
            }
        }

        // =====================================================================
        //  目录确保存在
        // =====================================================================

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string[] parts = path.Split('/');
                string currentPath = parts[0]; // "Assets"
                for (int i = 1; i < parts.Length; i++)
                {
                    string nextPath = currentPath + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(nextPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, parts[i]);
                    }
                    currentPath = nextPath;
                }
            }
        }
    }
}
#endif
