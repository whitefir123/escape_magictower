// ============================================================================
// 逃离魔塔 - 第一层怪物数据注册表 (Floor1MonsterRegistry)
// 程序化创建第一层暗黑地牢全部 9 种普通怪 + 1 Boss 的数据。
// 在编辑器中也可用 Editor Utility 脚本将这些数据导出为 SO 资产。
//
// 来源：GameData_Blueprints/04_1_Floor1_Dungeon.md（精确到单位的数值）
// ============================================================================

using UnityEngine;

namespace EscapeTheTower.Data
{
    /// <summary>
    /// 第一层怪物数据注册表 —— 提供运行时怪物数据的静态工厂
    /// 已迁移至 SO 资产化方案，保留作为降级兜底
    /// 新增/修改怪物请直接编辑 Assets/Data/Monsters/ 下的 .asset 文件
    /// </summary>
    [System.Obsolete("请使用 Assets/Data/Monsters/ 下的 SO 资产。此类仅作为降级兜底保留。")]
    public static class Floor1MonsterRegistry
    {
        // =====================================================================
        //  1. 史莱姆 (Slime)
        //  标签：自然/元素/近战 | 高魔抗低物防的果冻沙包
        // =====================================================================
        public static MonsterData_SO CreateSlime()
        {
            var so = ScriptableObject.CreateInstance<MonsterData_SO>();
            so.entityName = "史莱姆";
            so.entityID = "slime";
            so.tags = MonsterTag.Natural | MonsterTag.Elemental | MonsterTag.Melee;

            // Min 值（基类字段）
            so.baseMaxHP = 40f;    so.maxHP_Max = 120f;
            so.baseATK = 4f;       so.maxATK_Max = 15f;
            so.baseMATK = 0f;      so.maxMATK_Max = 0f;
            so.baseDEF = 1f;       so.maxDEF_Max = 5f;
            so.baseMDEF = 8f;      so.maxMDEF_Max = 20f;
            so.baseDodge = 0f;     so.dodge_Max = 0f;
            so.baseMoveSpeed = 0.4f; // 极慢
            so.attackInterval = 2.0f;
            so.baseExpReward = 4;
            so.goldDropMin = 1; so.goldDropMax = 3;
            so.mechanicDescription = "高魔抗低物防的果冻沙包，近战割草目标";
            so.immuneToEffects = new StatusEffectType[0];
            so.onHitEffects = new[] {
                new OnHitEffect { effectType = StatusEffectType.Burn, chance = 0.3f, duration = 6f, valuePerStack = 0f, stacks = 1 }
            };
            return so;
        }

        // =====================================================================
        //  2. 地牢小蝙蝠 (Cave Bat)
        //  标签：野兽/飞行/微型/刺客 | 极脆高攻速群体冲锋
        // =====================================================================
        public static MonsterData_SO CreateCaveBat()
        {
            var so = ScriptableObject.CreateInstance<MonsterData_SO>();
            so.entityName = "地牢小蝙蝠";
            so.entityID = "cave_bat";
            so.tags = MonsterTag.Beast | MonsterTag.Flying | MonsterTag.Tiny | MonsterTag.Assassin;

            so.baseMaxHP = 15f;    so.maxHP_Max = 45f;
            so.baseATK = 6f;       so.maxATK_Max = 22f;
            so.baseMATK = 0f;      so.maxMATK_Max = 0f;
            so.baseDEF = 0f;       so.maxDEF_Max = 2f;
            so.baseMDEF = 0f;      so.maxMDEF_Max = 2f;
            so.baseDodge = 0.08f;  so.dodge_Max = 0.15f;
            so.baseMoveSpeed = 2.0f; // 极快
            so.attackInterval = 0.8f;
            so.baseExpReward = 3;
            so.goldDropMin = 0; so.goldDropMax = 2;
            so.mechanicDescription = "极脆飞行单位，5~8只成群扇形冲锋，需AOE清场";
            so.immuneToEffects = new StatusEffectType[0];
            return so;
        }

        // =====================================================================
        //  3. 骷髅小兵 (Skeleton Minion)
        //  标签：亡灵/近战 | 标准肉盾，免疫毒素和流血
        // =====================================================================
        public static MonsterData_SO CreateSkeletonMinion()
        {
            var so = ScriptableObject.CreateInstance<MonsterData_SO>();
            so.entityName = "骷髅小兵";
            so.entityID = "skeleton_minion";
            so.tags = MonsterTag.Undead | MonsterTag.Melee;

            so.baseMaxHP = 50f;    so.maxHP_Max = 180f;
            so.baseATK = 10f;      so.maxATK_Max = 30f;
            so.baseMATK = 0f;      so.maxMATK_Max = 0f;
            so.baseDEF = 12f;      so.maxDEF_Max = 35f;
            so.baseMDEF = 3f;      so.maxMDEF_Max = 10f;
            so.baseDodge = 0f;     so.dodge_Max = 0f;
            so.baseMoveSpeed = 1.0f; // 中等
            so.attackInterval = 1.2f;
            so.baseExpReward = 5;
            so.goldDropMin = 2; so.goldDropMax = 5;
            so.mechanicDescription = "标准近战肉盾，免疫毒素和流血";
            so.immuneToEffects = new[] { StatusEffectType.Poison, StatusEffectType.Bleed };
            return so;
        }

        // =====================================================================
        //  4. 骷髅弓箭手 (Skeleton Archer)
        //  标签：亡灵/远程 | 远程后排输出
        // =====================================================================
        public static MonsterData_SO CreateSkeletonArcher()
        {
            var so = ScriptableObject.CreateInstance<MonsterData_SO>();
            so.entityName = "骷髅弓箭手";
            so.entityID = "skeleton_archer";
            so.tags = MonsterTag.Undead | MonsterTag.Ranged;

            so.baseMaxHP = 30f;    so.maxHP_Max = 90f;
            so.baseATK = 15f;      so.maxATK_Max = 35f;
            so.baseMATK = 0f;      so.maxMATK_Max = 0f;
            so.baseDEF = 5f;       so.maxDEF_Max = 15f;
            so.baseMDEF = 5f;      so.maxMDEF_Max = 15f;
            so.baseDodge = 0f;     so.dodge_Max = 0f;
            so.baseMoveSpeed = 0.6f; // 慢（会尽量远离玩家）
            so.attackInterval = 2.5f;
            so.baseExpReward = 5;
            so.goldDropMin = 2; so.goldDropMax = 4;
            so.mechanicDescription = "远处持续射击，需要玩家主动突进清理的后排输出";
            so.immuneToEffects = new[] { StatusEffectType.Poison, StatusEffectType.Bleed };
            return so;
        }

        // =====================================================================
        //  5. 骷髅法师 (Skeleton Mage)
        //  标签：亡灵/法师 | 发射追踪奥术弹
        // =====================================================================
        public static MonsterData_SO CreateSkeletonMage()
        {
            var so = ScriptableObject.CreateInstance<MonsterData_SO>();
            so.entityName = "骷髅法师";
            so.entityID = "skeleton_mage";
            so.tags = MonsterTag.Undead | MonsterTag.Caster;

            so.baseMaxHP = 25f;    so.maxHP_Max = 80f;
            so.baseATK = 0f;       so.maxATK_Max = 0f;
            so.baseMATK = 18f;     so.maxMATK_Max = 40f;
            so.baseDEF = 2f;       so.maxDEF_Max = 8f;
            so.baseMDEF = 15f;     so.maxMDEF_Max = 35f;
            so.baseDodge = 0f;     so.dodge_Max = 0f;
            so.baseMoveSpeed = 0.4f; // 极慢
            so.attackInterval = 3.0f;
            so.baseExpReward = 5;
            so.goldDropMin = 3; so.goldDropMax = 6;
            so.mechanicDescription = "发射缓慢追踪奥术弹，逼迫玩家交走位或防御技能";
            so.immuneToEffects = new[] { StatusEffectType.Poison, StatusEffectType.Bleed };
            return so;
        }

        // =====================================================================
        //  6. 哥布林斥候 (Goblin Scout)
        //  标签：自然/人型/微型/远程 | 扔石头后逃跑
        // =====================================================================
        public static MonsterData_SO CreateGoblinScout()
        {
            var so = ScriptableObject.CreateInstance<MonsterData_SO>();
            so.entityName = "哥布林斥候";
            so.entityID = "goblin_scout";
            so.tags = MonsterTag.Natural | MonsterTag.Humanoid | MonsterTag.Tiny | MonsterTag.Ranged;

            so.baseMaxHP = 20f;    so.maxHP_Max = 60f;
            so.baseATK = 8f;       so.maxATK_Max = 20f;
            so.baseMATK = 0f;      so.maxMATK_Max = 0f;
            so.baseDEF = 1f;       so.maxDEF_Max = 5f;
            so.baseMDEF = 1f;      so.maxMDEF_Max = 5f;
            so.baseDodge = 0.10f;  so.dodge_Max = 0.20f;
            so.baseMoveSpeed = 2.0f; // 极快
            so.attackInterval = 1.0f;
            so.baseExpReward = 3;
            so.goldDropMin = 1; so.goldDropMax = 3;
            so.mechanicDescription = "扔石头后转身逃跑，碰撞体积小极难命中";
            so.immuneToEffects = new StatusEffectType[0];
            return so;
        }

        // =====================================================================
        //  7. 哥布林战士 (Goblin Warrior)
        //  标签：自然/人型/近战 | 成群无脑冲锋
        // =====================================================================
        public static MonsterData_SO CreateGoblinWarrior()
        {
            var so = ScriptableObject.CreateInstance<MonsterData_SO>();
            so.entityName = "哥布林战士";
            so.entityID = "goblin_warrior";
            so.tags = MonsterTag.Natural | MonsterTag.Humanoid | MonsterTag.Melee;

            so.baseMaxHP = 45f;    so.maxHP_Max = 140f;
            so.baseATK = 12f;      so.maxATK_Max = 28f;
            so.baseMATK = 0f;      so.maxMATK_Max = 0f;
            so.baseDEF = 10f;      so.maxDEF_Max = 20f;
            so.baseMDEF = 5f;      so.maxMDEF_Max = 10f;
            so.baseDodge = 0f;     so.dodge_Max = 0f;
            so.baseMoveSpeed = 1.5f; // 快
            so.attackInterval = 1.2f;
            so.baseExpReward = 4;
            so.goldDropMin = 2; so.goldDropMax = 5;
            so.mechanicDescription = "比骷髅灵活的近战单位，成群无脑冲锋";
            so.immuneToEffects = new StatusEffectType[0];
            so.onHitEffects = new[] {
                new OnHitEffect { effectType = StatusEffectType.Bleed, chance = 0.4f, duration = 5f, valuePerStack = 3f, stacks = 1 }
            };
            return so;
        }

        // =====================================================================
        //  8. 哥布林祭司 (Goblin Shaman)
        //  标签：自然/人型/辅助 | 释放加速光环，需优先击杀
        // =====================================================================
        public static MonsterData_SO CreateGoblinShaman()
        {
            var so = ScriptableObject.CreateInstance<MonsterData_SO>();
            so.entityName = "哥布林祭司";
            so.entityID = "goblin_shaman";
            so.tags = MonsterTag.Natural | MonsterTag.Humanoid | MonsterTag.Support;

            so.baseMaxHP = 35f;    so.maxHP_Max = 100f;
            so.baseATK = 0f;       so.maxATK_Max = 0f;
            so.baseMATK = 10f;     so.maxMATK_Max = 25f;
            so.baseDEF = 5f;       so.maxDEF_Max = 10f;
            so.baseMDEF = 15f;     so.maxMDEF_Max = 30f;
            so.baseDodge = 0f;     so.dodge_Max = 0f;
            so.baseMoveSpeed = 1.0f; // 中等
            so.attackInterval = 4.0f;
            so.baseExpReward = 5;
            so.goldDropMin = 3; so.goldDropMax = 6;
            so.mechanicDescription = "释放加速光环图腾，增益周围怪物移速，需优先击杀";
            so.immuneToEffects = new StatusEffectType[0];
            return so;
        }

        // =====================================================================
        //  9. 死神预备役 (Reaper Apprentice)
        //  标签：无实体/亡灵/刺客 | 镰刀扇形AOE，免疫碰撞限制
        // =====================================================================
        public static MonsterData_SO CreateReaperApprentice()
        {
            var so = ScriptableObject.CreateInstance<MonsterData_SO>();
            so.entityName = "死神预备役";
            so.entityID = "reaper_apprentice";
            so.tags = MonsterTag.Spirit | MonsterTag.Undead | MonsterTag.Assassin;

            so.baseMaxHP = 60f;    so.maxHP_Max = 200f;
            so.baseATK = 20f;      so.maxATK_Max = 50f;
            so.baseMATK = 0f;      so.maxMATK_Max = 0f;
            so.baseDEF = 0f;       so.maxDEF_Max = 0f;
            so.baseMDEF = 0f;      so.maxMDEF_Max = 0f;
            so.baseDodge = 0.10f;  so.dodge_Max = 0.20f;
            so.baseMoveSpeed = 1.0f; // 中等（飘忽不定）
            so.attackInterval = 2.0f;
            so.baseExpReward = 8;
            so.goldDropMin = 3; so.goldDropMax = 8;
            so.mechanicDescription = "巨大镰刀扇形AOE物理伤害，无实体标签免疫碰撞限制";
            so.immuneToEffects = new StatusEffectType[0];
            so.onHitEffects = new[] {
                new OnHitEffect { effectType = StatusEffectType.Poison, chance = 0.5f, duration = 8f, valuePerStack = 3f, stacks = 1 }
            };
            return so;
        }

        // =====================================================================
        //  关底 Boss：堕落的人型勇士 (The Fallen Hero)
        //  标签：人型/重装/精英/首领 | 勇者冲锋 + 50%血量处决大风车
        // =====================================================================
        public static MonsterData_SO CreateFallenHero()
        {
            var so = ScriptableObject.CreateInstance<MonsterData_SO>();
            so.entityName = "堕落的人型勇士";
            so.entityID = "fallen_hero";
            so.tags = MonsterTag.Humanoid | MonsterTag.HeavyArmor | MonsterTag.Elite | MonsterTag.Boss;

            so.baseMaxHP = 1200f;  so.maxHP_Max = 2800f;
            so.baseATK = 25f;      so.maxATK_Max = 60f;
            so.baseMATK = 0f;      so.maxMATK_Max = 0f;
            so.baseDEF = 25f;      so.maxDEF_Max = 55f;
            so.baseMDEF = 10f;     so.maxMDEF_Max = 25f;
            so.baseDodge = 0f;     so.dodge_Max = 0f;
            so.baseMoveSpeed = 1.0f; // 中等（冲锋时极快）
            so.attackInterval = 1.5f;
            so.baseExpReward = 150;
            so.goldDropMin = 50; so.goldDropMax = 100;
            so.mechanicDescription = "曾经试图挑战魔塔但被腐化的前辈剑士。勇者冲锋(直线+击退+眩晕) + 50%血量处决大风车(霸体+持续AOE)";
            so.immuneToEffects = new StatusEffectType[0];
            so.hasCCDiminishing = true; // Boss 硬控递减
            return so;
        }

        /// <summary>
        /// 获取第一层所有普通怪物的数据数组
        /// </summary>
        public static MonsterData_SO[] GetAllNormalMonsters()
        {
            return new MonsterData_SO[]
            {
                CreateSlime(),
                CreateCaveBat(),
                CreateSkeletonMinion(),
                CreateSkeletonArcher(),
                CreateSkeletonMage(),
                CreateGoblinScout(),
                CreateGoblinWarrior(),
                CreateGoblinShaman(),
                CreateReaperApprentice(),
            };
        }

        /// <summary>
        /// 获取第一层 Boss 数据
        /// </summary>
        public static MonsterData_SO GetBossData()
        {
            return CreateFallenHero();
        }
    }
}
