// ============================================================================
// 逃离魔塔 - 怪物 SO 资产批量导出工具 (MonsterAssetGenerator)
// Editor 菜单工具：一键生成 2-9 层全部怪物 MonsterData_SO 与 BiomeConfig_SO。
// 严格对照 GameData_Blueprints/04_2 ~ 04_9 蓝图数值。
//
// 用法：Unity 菜单栏 → EscapeTheTower → 批量导出怪物数据（全楼层）
// ============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using EscapeTheTower.Data;
using System.Collections.Generic;

namespace EscapeTheTower.Editor
{
    public static class MonsterAssetGenerator
    {
        private const string MONSTER_ROOT = "Assets/Data/Monsters";
        private const string BIOME_ROOT = "Assets/Resources/Biomes";

        [MenuItem("EscapeTheTower/批量导出怪物数据（全楼层）")]
        public static void ExportAllFloors()
        {
            EnsureFolder(MONSTER_ROOT);
            EnsureFolder(BIOME_ROOT);

            ExportFloor2();
            ExportFloor3();
            ExportFloor4();
            ExportFloor5();
            ExportFloor6();
            ExportFloor7();
            ExportFloor8();
            ExportFloor9();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MonsterAssetGenerator] ✅ 全部楼层怪物导出完成！");
            EditorUtility.DisplayDialog("导出成功", "已导出 2-9 层全部怪物 SO 与 BiomeConfig SO。", "确定");
        }

        // =============================================================
        //  第 2 层 花园
        // =============================================================
        private static void ExportFloor2()
        {
            string dir = EnsureFloorDir(2);
            var pool = new List<MonsterData_SO>();

            pool.Add(M(dir, "Monster_PhantomButterfly", "蝶妖", "phantom_butterfly",
                MonsterTag.Natural | MonsterTag.Flying | MonsterTag.Caster,
                120,240, 0,0, 25,55, 5,15, 15,35, 0.10f,0.20f, 0f,
                2.0f, 0.8f, 15, 5,12,
                "远端法系干扰者，命中附加中毒或致盲",
                new OnHitEffect{effectType=StatusEffectType.Poison,chance=0.4f,duration=4f,valuePerStack=3f,stacks=1}));

            pool.Add(M(dir, "Monster_FloraFairy", "花仙子", "flora_fairy",
                MonsterTag.Natural | MonsterTag.Flying | MonsterTag.Support,
                100,180, 0,0, 15,30, 0,10, 25,45, 0.05f,0.15f, 0f,
                3.5f, 0.7f, 18, 6,14,
                "优先辅助施法者，为血量最低的友军回血或加盾", null));

            pool.Add(M(dir, "Monster_DeepRootTreant", "树精", "deep_root_treant",
                MonsterTag.Natural | MonsterTag.Plant | MonsterTag.HeavyArmor,
                450,800, 30,70, 0,0, 30,60, 0,10, 0f,0f, 0.15f,
                3.0f, 0.3f, 25, 10,22,
                "超巨型肉盾，地刺攻击附带定身，极度惧怕灼烧",
                new OnHitEffect{effectType=StatusEffectType.Stun,chance=0.3f,duration=1.5f,valuePerStack=0f,stacks=1}));

            pool.Add(M(dir, "Monster_WorkerBee", "工蜂", "worker_bee",
                MonsterTag.Insect | MonsterTag.Flying | MonsterTag.Tiny | MonsterTag.Melee,
                70,150, 15,40, 0,0, 0,5, 0,5, 0.10f,0.20f, 0f,
                1.0f, 1.2f, 8, 2,6,
                "成群冲锋的近战杂鱼，Boss蜂后的召唤物", null));

            pool.Add(M(dir, "Monster_ArmoredDroneWasp", "守卫雄蜂", "armored_drone_wasp",
                MonsterTag.Insect | MonsterTag.Flying | MonsterTag.Ranged,
                180,320, 25,55, 0,0, 15,35, 5,15, 0.05f,0.15f, 0f,
                2.0f, 0.7f, 16, 5,12,
                "重装飞行昆虫，远端毒刺弹幕",
                new OnHitEffect{effectType=StatusEffectType.Poison,chance=0.25f,duration=3f,valuePerStack=2f,stacks=1}));

            pool.Add(M(dir, "Monster_ScytheMantis", "鬼螳螂", "scythe_mantis",
                MonsterTag.Insect | MonsterTag.Assassin,
                150,280, 30,65, 0,0, 10,20, 10,20, 0.10f,0.20f, 0f,
                1.2f, 0.9f, 20, 6,15,
                "致命近战刺客，20%概率双倍暴击伤害", null));

            pool.Add(M(dir, "Monster_PsychedelicShroom", "致幻孢子菇", "psychedelic_shroom",
                MonsterTag.Natural | MonsterTag.Plant,
                200,380, 10,25, 0,0, 15,30, 15,30, 0f,0f, 0f,
                2.5f, 0.2f, 14, 4,10,
                "防守反击，受击喷射孢子附加沉默",
                new OnHitEffect{effectType=StatusEffectType.Silence,chance=0.5f,duration=2f,valuePerStack=0f,stacks=1}));

            pool.Add(M(dir, "Monster_RhinoBeetleWarrior", "独角兜虫勇士", "rhino_beetle_warrior",
                MonsterTag.Insect | MonsterTag.HeavyArmor | MonsterTag.Melee,
                300,550, 25,50, 0,0, 30,65, 10,20, 0f,0f, 0.20f,
                2.0f, 0.6f, 22, 8,18,
                "虫群先锋，正面物理减免30%，偶尔冲锋击飞", null));

            pool.Add(M(dir, "Monster_BlightwoodBerserker", "枯木狂战士", "blightwood_berserker",
                MonsterTag.Natural | MonsterTag.Plant | MonsterTag.Melee,
                250,450, 35,80, 0,0, 15,25, 5,15, 0f,0f, 0f,
                1.5f, 0.6f, 18, 5,14,
                "血越低攻越高（最高1.5倍），需高爆发秒杀", null));

            // Boss: 蜂后
            var boss2 = M(dir, "Monster_Boss_QueenBee", "提泰妮娅·蜂后", "boss_queen_bee",
                MonsterTag.Natural | MonsterTag.Insect | MonsterTag.Summoner | MonsterTag.Boss,
                6000,9500, 40,80, 40,80, 35,60, 35,60, 0.10f,0.10f, 0.10f,
                2.5f, 0.6f, 200, 50,120,
                "召唤流母体，每15秒召唤蜂群，费洛蒙标记锁定追杀", null);
            boss2.hasCCDiminishing = true;
            boss2.immuneToEffects = new[] { StatusEffectType.Stun };
            EditorUtility.SetDirty(boss2);

            CreateBiomeConfig(2, "花园", "garden", pool, boss2);
        }

        // =============================================================
        //  第 3 层 齿轮工坊
        // =============================================================
        private static void ExportFloor3()
        {
            string dir = EnsureFloorDir(3);
            var pool = new List<MonsterData_SO>();

            pool.Add(M(dir, "Monster_Gearling", "齿轮仔", "gearling",
                MonsterTag.Construct | MonsterTag.Tiny | MonsterTag.Support,
                180,350, 25,50, 0,0, 25,45, 5,15, 0.05f,0.05f, 0f,
                1.5f, 0.9f, 10, 3,8,
                "依附装配其他构装体提升10%攻速攻击力", null,
                StatusEffectType.Bleed, StatusEffectType.Poison));

            pool.Add(M(dir, "Monster_ClockworkKnight", "发条骑士", "clockwork_knight",
                MonsterTag.Construct | MonsterTag.HeavyArmor | MonsterTag.Melee,
                500,950, 50,110, 0,0, 50,90, 10,25, 0f,0f, 0.30f,
                2.5f, 0.5f, 30, 12,28,
                "精英保安，持盾格挡正面物理，冲锋击飞", null,
                StatusEffectType.Bleed, StatusEffectType.Poison));

            pool.Add(M(dir, "Monster_ClockworkHound", "发条机械犬", "clockwork_hound",
                MonsterTag.Construct | MonsterTag.Beast,
                250,450, 40,75, 0,0, 15,30, 5,15, 0.15f,0.15f, 0f,
                1.2f, 1.2f, 16, 5,12,
                "成群游荡的机械猎犬，攻速极快", null,
                StatusEffectType.Bleed, StatusEffectType.Poison));

            pool.Add(M(dir, "Monster_ClockworkGunner", "发条机炮手", "clockwork_gunner",
                MonsterTag.Construct | MonsterTag.Ranged,
                220,380, 18,35, 0,0, 25,45, 15,25, 0.05f,0.05f, 0f,
                0.5f, 0.3f, 14, 4,10,
                "站桩加特林扫射，射击时无法移动", null,
                StatusEffectType.Bleed, StatusEffectType.Poison));

            pool.Add(M(dir, "Monster_ClockworkTinker", "修理工匠", "clockwork_tinker",
                MonsterTag.Construct | MonsterTag.Support,
                180,300, 10,25, 0,0, 10,20, 10,20, 0.10f,0.10f, 0f,
                3.0f, 0.4f, 20, 6,14,
                "回收残骸满血复活或改造为自走地雷", null,
                StatusEffectType.Bleed, StatusEffectType.Poison));

            pool.Add(M(dir, "Monster_WalkingMine", "自走地雷", "walking_mine",
                MonsterTag.Construct | MonsterTag.Tiny,
                100,200, 0,0, 0,0, 5,15, 5,15, 0f,0f, 0f,
                1.0f, 0.9f, 12, 3,8,
                "触碰自爆，免疫任何控制", null,
                StatusEffectType.Bleed, StatusEffectType.Poison));

            pool.Add(M(dir, "Monster_ClockworkMusicBox", "八音盒", "clockwork_music_box",
                MonsterTag.Construct,
                280,500, 0,0, 20,40, 20,40, 30,60, 0f,0f, 0f,
                3.0f, 0.1f, 18, 5,12,
                "催眠音波，叠满3层困意后强制睡眠2.5秒",
                new OnHitEffect{effectType=StatusEffectType.Stun,chance=0.3f,duration=2.5f,valuePerStack=0f,stacks=1},
                StatusEffectType.Bleed, StatusEffectType.Poison));

            pool.Add(M(dir, "Monster_GrandfatherClock", "肃穆落地钟", "grandfather_clock",
                MonsterTag.Construct,
                400,700, 0,0, 40,70, 20,40, 30,50, 0f,0f, 0f,
                4.0f, 0.05f, 22, 8,18,
                "时间迟缓减速光环，12秒后强制眩晕",
                new OnHitEffect{effectType=StatusEffectType.Slowed,chance=1f,duration=12f,valuePerStack=0.15f,stacks=1},
                StatusEffectType.Bleed, StatusEffectType.Poison));

            pool.Add(M(dir, "Monster_AntiqueFlashCamera", "镁光留影机", "antique_flash_camera",
                MonsterTag.Construct,
                200,350, 0,0, 10,20, 15,30, 15,30, 0.05f,0.05f, 0f,
                6.0f, 0.6f, 14, 4,10,
                "闪光致盲+深度眩晕2.5秒（需面对），背对可免疫", null,
                StatusEffectType.Bleed, StatusEffectType.Poison));

            var boss3 = M(dir, "Monster_Boss_DoomLocomotive", "残破的蒸汽末日火车", "boss_doom_locomotive",
                MonsterTag.Construct | MonsterTag.HeavyArmor | MonsterTag.Boss,
                8000,13000, 100,180, 80,150, 80,130, 40,80, 0f,0f, 0.50f,
                3.0f, 0f, 500, 100,250,
                "组装倒计时Boss，5阶段渐进强化，45分钟后主动猎杀", null,
                StatusEffectType.Bleed, StatusEffectType.Poison);
            boss3.hasCCDiminishing = true;
            boss3.hasPermUnstoppable = true;
            EditorUtility.SetDirty(boss3);

            CreateBiomeConfig(3, "齿轮工坊", "clockwork", pool, boss3);
        }

        // =============================================================
        //  第 4 层 倒悬之海
        // =============================================================
        private static void ExportFloor4()
        {
            string dir = EnsureFloorDir(4);
            var pool = new List<MonsterData_SO>();

            pool.Add(M(dir,"Monster_Clownfish","小丑鱼","clownfish",
                MonsterTag.Beast|MonsterTag.Aquatic|MonsterTag.Tiny|MonsterTag.Melee,
                80,150,15,30,0,0,15,25,10,20,0.15f,0.20f,0f,1.0f,1.2f,8,2,5,"杂鱼冲锋，刷连击数的肉靶",null));
            pool.Add(M(dir,"Monster_ShrimpSoldier","虾兵","shrimp_soldier",
                MonsterTag.Aquatic|MonsterTag.Melee,
                200,380,30,55,0,0,40,70,15,30,0.05f,0.05f,0.10f,1.8f,0.6f,14,4,10,"前线炮灰，水枪突刺击退",null));
            pool.Add(M(dir,"Monster_CrabGeneral","蟹将","crab_general",
                MonsterTag.Aquatic|MonsterTag.HeavyArmor|MonsterTag.Melee,
                500,850,50,90,0,0,80,120,30,50,0f,0f,0.40f,3.0f,0.3f,28,10,25,
                "重装坦克，正面免疫物理飞行物，巨钳夹击附流血",
                new OnHitEffect{effectType=StatusEffectType.Bleed,chance=0.4f,duration=4f,valuePerStack=5f,stacks=1}));
            pool.Add(M(dir,"Monster_DeepAngler","钓鱼佬","deep_angler",
                MonsterTag.Aquatic|MonsterTag.Beast,
                250,450,25,50,0,0,45,75,45,75,0.05f,0.05f,0f,4.5f,0.3f,20,6,15,
                "后排钩索，命中强制拉拽+减速",
                new OnHitEffect{effectType=StatusEffectType.Slowed,chance=1f,duration=2f,valuePerStack=0.3f,stacks=1}));
            pool.Add(M(dir,"Monster_Siren","塞壬","siren",
                MonsterTag.Aquatic|MonsterTag.Caster,
                150,280,0,0,40,75,20,35,60,90,0.15f,0.15f,0f,2.5f,0.6f,22,7,16,
                "海妖控制法师，2秒咏唱后魅惑玩家",null));
            pool.Add(M(dir,"Monster_TurtleButler","龟管家","turtle_butler",
                MonsterTag.Aquatic|MonsterTag.Beast|MonsterTag.Support,
                400,700,0,0,15,30,70,100,70,100,0f,0f,0.30f,4.0f,0.1f,24,8,20,
                "极品纯辅助，全屏深海水盾+回血，30%血以下缩壳极大减伤",null));
            pool.Add(M(dir,"Monster_AnchorRevenant","水手亡魂","anchor_revenant",
                MonsterTag.Undead|MonsterTag.Aquatic|MonsterTag.HeavyArmor|MonsterTag.Melee,
                450,750,60,110,0,0,40,70,30,50,0f,0f,0.20f,2.5f,0.4f,26,10,22,
                "亡灵重装，普攻附加潮湿状态",
                new OnHitEffect{effectType=StatusEffectType.Slowed,chance=0.6f,duration=3f,valuePerStack=0.2f,stacks=1}));
            pool.Add(M(dir,"Monster_ToxicUrchin","深海巨毒海胆","toxic_urchin",
                MonsterTag.Aquatic|MonsterTag.Beast,
                200,450,0,0,0,0,60,90,60,90,0f,0f,0.50f,5.0f,0f,16,5,12,
                "固定毒雾+近战反伤附加中毒",
                new OnHitEffect{effectType=StatusEffectType.Poison,chance=1f,duration=5f,valuePerStack=4f,stacks=2}));

            var boss4 = M(dir,"Monster_Boss_StarJelly","巨型水母","boss_star_jelly",
                MonsterTag.Aquatic|MonsterTag.Flying|MonsterTag.Boss,
                3500,6500,40,70,80,140,70,110,120,180,0f,0f,0.20f,4.0f,0.1f,350,80,180,
                "九须护体75%免伤，断肢喷麻痹毒雾",null);
            boss4.hasCCDiminishing=true; boss4.hasPermUnstoppable=true;
            EditorUtility.SetDirty(boss4);
            CreateBiomeConfig(4,"倒悬之海","inverted_sea",pool,boss4);
        }

        // =============================================================
        //  第 5 层 马戏团
        // =============================================================
        private static void ExportFloor5()
        {
            string dir = EnsureFloorDir(5);
            var pool = new List<MonsterData_SO>();

            pool.Add(M(dir,"Monster_JugglingClown","掷球小丑","juggling_clown",
                MonsterTag.Humanoid|MonsterTag.Ranged,
                180,320,30,55,0,0,15,30,15,30,0.15f,0.15f,0f,1.5f,0.9f,14,4,10,
                "弹跳彩球远程，反弹3次",null));
            pool.Add(M(dir,"Monster_StiltManiac","高跷疯子","stilt_maniac",
                MonsterTag.Humanoid|MonsterTag.Melee,
                450,750,60,100,0,0,25,45,25,45,0f,0f,0f,2.5f,0.6f,22,8,18,
                "免疫地面陷阱，极长拐杖戳击",null));
            pool.Add(M(dir,"Monster_JackInTheBox","惊吓魔盒","jack_in_the_box",
                MonsterTag.Construct,
                300,500,0,0,0,0,60,90,60,90,0f,0f,0.50f,10f,0f,18,6,14,
                "伪装道具，弹出恐惧1.5秒",null));
            pool.Add(M(dir,"Monster_GrandIllusionist","大魔术师","grand_illusionist",
                MonsterTag.Humanoid|MonsterTag.Caster|MonsterTag.Summoner,
                250,450,0,0,45,85,20,35,40,70,0.10f,0.10f,0f,4.0f,0.6f,24,8,18,
                "白鸽戏法+烟雾闪现远距风筝",null));
            pool.Add(M(dir,"Monster_FunhouseMirror","哈哈镜","funhouse_mirror",
                MonsterTag.Construct,
                400,600,0,0,0,0,10,20,100,150,0f,0f,0f,10f,0f,20,6,14,
                "100%反弹远程法术，复制镜像幻影",null));
            pool.Add(M(dir,"Monster_BalancingLion","独轮狮子","balancing_lion",
                MonsterTag.Beast|MonsterTag.HeavyArmor,
                800,1300,80,140,0,0,60,95,20,40,0f,0f,0.20f,3.0f,0.4f,35,15,30,
                "直线加速冲撞碾压，撞墙长硬直",null));
            pool.Add(M(dir,"Monster_AnimalTamer","黑心驯兽师","animal_tamer",
                MonsterTag.Humanoid|MonsterTag.Support,
                350,600,25,50,0,0,25,45,30,50,0f,0f,0f,2.5f,0.6f,20,7,16,
                "鞭策友军获得狂化增益",null));
            pool.Add(M(dir,"Monster_KnifeThrower","蒙眼飞刀女郎","knife_thrower",
                MonsterTag.Humanoid|MonsterTag.Assassin,
                200,380,45,80,0,0,15,30,20,40,0.18f,0.18f,0f,1.2f,1.1f,18,5,14,
                "高频后空翻位移+扇形飞刀，命中必定流血",
                new OnHitEffect{effectType=StatusEffectType.Bleed,chance=1f,duration=4f,valuePerStack=4f,stacks=1}));
            pool.Add(M(dir,"Monster_BalloonCluster","气球簇","balloon_cluster",
                MonsterTag.Flying,
                50,100,0,0,20,40,0,0,0,0,0f,0f,0f,99f,0.1f,6,2,5,
                "无仇恨飘浮，触碰爆炸产生火/冰/致盲",null));

            var boss5 = M(dir,"Monster_Boss_Ringmaster","诡宴团长·伽罗瓦","boss_ringmaster",
                MonsterTag.Humanoid|MonsterTag.Demon|MonsterTag.Caster|MonsterTag.Boss,
                4500,7500,60,100,120,200,40,70,60,90,0.10f,0.10f,0.30f,3.0f,0.6f,400,80,200,
                "礼帽传送+命运轮盘+马戏大巡游",null);
            boss5.hasCCDiminishing=true; boss5.hasPermUnstoppable=true;
            EditorUtility.SetDirty(boss5);
            CreateBiomeConfig(5,"马戏团","carnival",pool,boss5);
        }

        // =============================================================
        //  第 6 层 水晶矿洞
        // =============================================================
        private static void ExportFloor6()
        {
            string dir = EnsureFloorDir(6);
            var pool = new List<MonsterData_SO>();

            pool.Add(M(dir,"Monster_CrystalHermitCrab","冰晶寄居蟹","crystal_hermit_crab",
                MonsterTag.Beast|MonsterTag.Melee,
                250,380,35,60,0,0,60,90,60,90,0f,0f,0.50f,1.5f,0.4f,14,4,10,
                "30%概率缩壳减伤50%，拖延输出节奏",null));
            pool.Add(M(dir,"Monster_CrystalLungInfected","肺痨感染者","crystal_lung_infected",
                MonsterTag.Undead,
                300,450,0,0,40,65,25,40,15,30,0f,0f,0f,3.0f,0.15f,18,5,12,
                "受击喷射晶化粉尘，附加易碎(受伤+20%)，死亡粉尘爆炸",null));
            pool.Add(M(dir,"Monster_CrystalArmorGuard","水晶铠甲守卫","crystal_armor_guard",
                MonsterTag.Humanoid|MonsterTag.HeavyArmor|MonsterTag.Melee,
                500,750,60,90,0,0,70,100,70,100,0f,0f,0.30f,2.5f,0.15f,28,10,24,
                "折光晶甲反射正面弹道50%伤害，需绕背攻击",null));
            pool.Add(M(dir,"Monster_BlindEchoFiend","盲眼妖","blind_echo_fiend",
                MonsterTag.Humanoid,
                200,350,0,0,45,75,20,35,20,35,0.10f,0.10f,0f,2.0f,1.0f,16,5,12,
                "靠听觉辨位，玩家跑动/技能触发落石暴击",null));
            pool.Add(M(dir,"Monster_ResonanceCrystal","共振晶簇","resonance_crystal",
                MonsterTag.Elemental,
                180,280,0,0,0,0,10,25,150,150,0f,0f,1f,99f,0f,10,3,8,
                "存在时每次用技能法力消耗+5%，需物理平A踹碎",null));
            pool.Add(M(dir,"Monster_HeadlampMineRat","探照灯矿鼠","headlamp_mine_rat",
                MonsterTag.Beast,
                180,300,30,50,0,0,15,30,15,30,0.20f,0.20f,0f,1.2f,1.2f,12,4,10,
                "探照灯扫射致盲，高频闪白屏幕",null));
            pool.Add(M(dir,"Monster_TreasureGoblin","探宝地精","treasure_goblin",
                MonsterTag.Humanoid,
                600,850,0,0,0,0,35,60,35,60,0.15f,0.15f,0f,1.0f,1.3f,50,30,80,
                "稀有福利怪，15秒内击杀爆极品道具，否则逃跑消失",null));

            var boss6 = M(dir,"Monster_Boss_PrismaCore","棱核","boss_prisma_core",
                MonsterTag.Elemental|MonsterTag.Boss,
                2800,4000,0,0,100,160,80,120,80,120,0f,0f,0.30f,5.0f,0f,500,100,250,
                "水晶Boss，8000护盾需反光机制破除，七彩光波洗地",null);
            boss6.hasCCDiminishing=true; boss6.hasPermUnstoppable=true;
            EditorUtility.SetDirty(boss6);
            CreateBiomeConfig(6,"水晶矿洞","crystal_caves",pool,boss6);
        }

        // =============================================================
        //  第 7 层 沙海
        // =============================================================
        private static void ExportFloor7()
        {
            string dir = EnsureFloorDir(7);
            var pool = new List<MonsterData_SO>();

            pool.Add(M(dir,"Monster_DesertRattlesnake","沙漠响尾蛇","desert_rattlesnake",
                MonsterTag.Beast|MonsterTag.Assassin,
                100,180,25,45,0,0,10,20,10,20,0.15f,0.15f,0f,1.2f,0.9f,10,3,8,
                "命中必中毒，体型细小难命中",
                new OnHitEffect{effectType=StatusEffectType.Poison,chance=1f,duration=4f,valuePerStack=3f,stacks=1}));
            pool.Add(M(dir,"Monster_Mummy","木乃伊","mummy",
                MonsterTag.Undead|MonsterTag.Melee,
                350,600,45,80,0,0,25,45,15,30,0f,0f,0f,2.0f,0.3f,18,6,14,
                "受击绷带断裂喷尸毒粉尘，怕火",null));
            pool.Add(M(dir,"Monster_AbyssWorm","巨口虫","abyss_worm",
                MonsterTag.Insect,
                450,750,60,110,0,0,40,70,20,40,0f,0f,0f,3.5f,1.0f,26,10,22,
                "潜地伏击，流沙旋涡+破土吞噬击飞",null));
            pool.Add(M(dir,"Monster_Mirage","海市蜃楼","mirage",
                MonsterTag.Spirit,
                1,1,0,0,0,0,0,0,0,0,0f,0f,0f,99f,0f,5,1,3,
                "1HP幻象，在场时全怪隐身，触碰即破",null));
            pool.Add(M(dir,"Monster_SunCultist","烈阳祭司","sun_cultist",
                MonsterTag.Humanoid|MonsterTag.Caster,
                250,450,0,0,50,90,15,30,45,75,0.05f,0.05f,0f,4.0f,0.3f,22,7,16,
                "烈阳聚焦跟踪光圈，爆发光柱+致盲",null));
            pool.Add(M(dir,"Monster_QuicksandScorpion","流沙蝎","quicksand_scorpion",
                MonsterTag.Beast|MonsterTag.HeavyArmor|MonsterTag.Melee,
                600,950,55,100,30,60,70,110,30,50,0f,0f,0.20f,1.8f,0.6f,28,10,24,
                "重装蝎子，神经毒尾扇形喷射麻痹",
                new OnHitEffect{effectType=StatusEffectType.Slowed,chance=0.5f,duration=3f,valuePerStack=0.4f,stacks=1}));
            pool.Add(M(dir,"Monster_SandstormTotem","风沙图腾","sandstorm_totem",
                MonsterTag.Construct,
                300,400,0,0,0,0,60,90,60,90,0f,0f,0.30f,8.0f,0f,16,5,12,
                "沙域屏障吹飞飞行弹道，需近战强拆",null));

            var boss7 = M(dir,"Monster_Boss_Sphinx","狮身人面像","boss_sphinx",
                MonsterTag.Construct|MonsterTag.Boss,
                3000,4500,0,0,80,130,100,150,80,120,0f,0f,0.50f,6.0f,0f,500,100,250,
                "谜语Boss，答对反噬破防，答错法老天罚巨雷",null);
            boss7.hasCCDiminishing=true; boss7.hasPermUnstoppable=true;
            EditorUtility.SetDirty(boss7);
            CreateBiomeConfig(7,"沙海","desert",pool,boss7);
        }

        // =============================================================
        //  第 8 层 雪镇
        // =============================================================
        private static void ExportFloor8()
        {
            string dir = EnsureFloorDir(8);
            var pool = new List<MonsterData_SO>();

            pool.Add(M(dir,"Monster_MacabreSnowman","诡面雪人","macabre_snowman",
                MonsterTag.Elemental|MonsterTag.Ranged,
                350,500,40,75,0,0,25,45,25,45,0f,0f,1f,2.0f,0f,16,5,12,
                "背对时疯狂砸冰球附加冰霜，需踩碎树枝防重生",
                new OnHitEffect{effectType=StatusEffectType.Frozen,chance=0.5f,duration=1f,valuePerStack=0f,stacks=1}));
            pool.Add(M(dir,"Monster_FrostWolf","饥寒霜狼","frost_wolf",
                MonsterTag.Beast|MonsterTag.Assassin,
                180,300,35,60,0,0,15,25,15,25,0.20f,0.20f,0f,1.5f,1.1f,12,4,10,
                "成群包抄，咬击附加冰霜减速",
                new OnHitEffect{effectType=StatusEffectType.Frozen,chance=0.4f,duration=1f,valuePerStack=0f,stacks=1}));
            pool.Add(M(dir,"Monster_FrozenWatchman","守夜冰雕","frozen_watchman",
                MonsterTag.Construct|MonsterTag.HeavyArmor|MonsterTag.Melee,
                500,750,60,95,0,0,80,130,70,110,0f,0f,0.30f,2.5f,0.3f,26,10,22,
                "万载玄冰铠甲，轻武器弹刀，需重锤或火法融化",null));
            pool.Add(M(dir,"Monster_StormYeti","风暴雪怪","storm_yeti",
                MonsterTag.Beast|MonsterTag.Melee,
                750,1100,80,140,40,70,50,80,50,80,0f,0f,0f,2.2f,0.6f,35,15,30,
                "15秒一次极地暴风雪压缩视野+叠冰霜",null));
            pool.Add(M(dir,"Monster_FrostBanshee","霜怨灵","frost_banshee",
                MonsterTag.Undead|MonsterTag.Spirit|MonsterTag.Caster,
                200,350,0,0,45,85,0,5,60,100,0.20f,0.20f,0f,3.0f,0.4f,20,6,14,
                "体温剥夺光环压缩耐力上限50%，音波无视格挡",null,
                StatusEffectType.Bleed, StatusEffectType.Poison));
            pool.Add(M(dir,"Monster_ShatterIceRaven","裂冰松鸦","shatter_ice_raven",
                MonsterTag.Beast|MonsterTag.Flying|MonsterTag.Assassin,
                120,200,25,50,0,0,15,30,15,30,0.30f,0.30f,0f,2.0f,1.2f,10,3,8,
                "高空冰羽空袭，30%闪避飞行单位",null));
            pool.Add(M(dir,"Monster_FrostbittenZombie","霜冻丧尸","frostbitten_zombie",
                MonsterTag.Undead|MonsterTag.Melee,
                350,550,40,75,0,0,25,50,15,30,0f,0f,0f,2.5f,0.15f,14,4,10,
                "极慢丧尸海，触碰附加冰霜，叠满3层冰封2秒",
                new OnHitEffect{effectType=StatusEffectType.Frozen,chance=1f,duration=1f,valuePerStack=0f,stacks=1}));
            pool.Add(M(dir,"Monster_RabidSnowElk","狂乱雪地麋鹿","rabid_snow_elk",
                MonsterTag.Beast|MonsterTag.Melee,
                500,750,70,125,0,0,45,70,30,50,0.10f,0.10f,0f,3.0f,0.9f,24,8,20,
                "蓄力1.5秒+超长距离冲锋，必定击飞",null));

            var boss8 = M(dir,"Monster_Boss_CorruptedSanta","堕落的圣诞老人","boss_corrupted_santa",
                MonsterTag.Humanoid|MonsterTag.Boss,
                4000,6000,90,160,80,130,80,120,80,120,0f,0f,0.30f,2.0f,0.6f,450,100,250,
                "夺命盲盒+坏孩子烟囱砸+骸骨雪橇Z字冲锋",null);
            boss8.hasCCDiminishing=true; boss8.hasPermUnstoppable=true;
            EditorUtility.SetDirty(boss8);
            CreateBiomeConfig(8,"雪镇","snow_town",pool,boss8);
        }

        // =============================================================
        //  第 9 层 神圣所
        // =============================================================
        private static void ExportFloor9()
        {
            string dir = EnsureFloorDir(9);
            var pool = new List<MonsterData_SO>();

            pool.Add(M(dir,"Monster_Flagellant","苦行僧","flagellant",
                MonsterTag.Humanoid|MonsterTag.Melee,
                180,350,35,65,0,0,10,20,40,70,0f,0f,0f,1.0f,1.2f,16,5,12,
                "殉道者激愤，血越低攻速移速几何倍增",null));
            pool.Add(M(dir,"Monster_AtonementSupplicant","赎罪者","atonement_supplicant",
                MonsterTag.Humanoid|MonsterTag.Support,
                450,750,0,0,0,0,10,20,10,20,0f,0f,0f,3.0f,0.2f,24,8,18,
                "苦痛链接，被链怪物锁血1HP，致死伤害2倍转移给自己",null));
            pool.Add(M(dir,"Monster_Nun","修女","nun",
                MonsterTag.Humanoid|MonsterTag.Support,
                280,450,0,0,40,70,15,30,50,80,0.10f,0.10f,0f,3.0f,0.5f,20,6,14,
                "圣典庇护，全场友军套等离子光盾+反弹近战30%",null));
            pool.Add(M(dir,"Monster_Priest","神父","priest",
                MonsterTag.Humanoid|MonsterTag.Caster,
                350,600,0,0,75,130,25,45,70,110,0f,0f,0.15f,4.5f,0.3f,28,10,24,
                "异端审判巨雷+致盲3秒，劈友军反而给巨力祝福",null));
            pool.Add(M(dir,"Monster_FacelessAngel","盲目天使雕像","faceless_angel",
                MonsterTag.Construct,
                450,750,0,0,80,140,80,130,80,130,0f,0f,0.50f,5.0f,0f,30,12,28,
                "神罚死光真实伤害贯穿全图",null));
            pool.Add(M(dir,"Monster_EyeOfAngel","天使之眼","eye_of_angel",
                MonsterTag.Spirit|MonsterTag.Flying|MonsterTag.Support,
                250,450,0,0,30,60,0,0,100,150,0.15f,0.15f,0f,2.5f,1.0f,18,5,14,
                "真视窥探破隐身+扒取Buff+挂灼烧",
                new OnHitEffect{effectType=StatusEffectType.Burn,chance=0.5f,duration=3f,valuePerStack=5f,stacks=1},
                StatusEffectType.Bleed, StatusEffectType.Poison));
            pool.Add(M(dir,"Monster_PegasusTemplar","神恩骑士","pegasus_templar",
                MonsterTag.HeavyArmor|MonsterTag.Melee,
                700,1100,90,160,0,0,70,110,50,80,0.10f,0.10f,0f,3.5f,1.1f,35,15,30,
                "超长距离龙枪冲锋，命中必暴+按墙僵直",null));

            var boss9 = M(dir,"Monster_Boss_DevouringCathedral","大教堂","boss_devouring_cathedral",
                MonsterTag.Construct|MonsterTag.Boss,
                6000,9500,150,250,120,200,120,180,120,180,0f,0f,0.50f,3.0f,0f,800,200,500,
                "活体圣所，墙壁挤压+十字射线+强酸胃液沼泽",null);
            boss9.hasCCDiminishing=true; boss9.hasPermUnstoppable=true;
            EditorUtility.SetDirty(boss9);
            CreateBiomeConfig(9,"神圣所","sanctuary",pool,boss9);
        }

        // =============================================================
        //  工具方法
        // =============================================================

        /// <summary>
        /// 创建怪物 SO 资产（核心工厂方法）
        /// </summary>
        private static MonsterData_SO M(string dir, string fileName, string name, string id,
            MonsterTag tags,
            float hpMin, float hpMax, float atkMin, float atkMax,
            float matkMin, float matkMax, float defMin, float defMax,
            float mdefMin, float mdefMax,
            float dodgeMin, float dodgeMax, float critResist,
            float atkInterval, float moveSpeed,
            int exp, int goldMin, int goldMax,
            string mechDesc, OnHitEffect? onHit,
            params StatusEffectType[] immunities)
        {
            string path = $"{dir}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<MonsterData_SO>(path);
            if (existing != null) { Debug.Log($"  跳过已存在：{path}"); return existing; }

            var so = ScriptableObject.CreateInstance<MonsterData_SO>();

            // 基类字段（EntityData_SO）= Min 值
            so.entityName = name;
            so.entityID = id;
            so.baseMaxHP = hpMin;
            so.baseATK = atkMin;
            so.baseMATK = matkMin;
            so.baseDEF = defMin;
            so.baseMDEF = mdefMin;
            so.baseDodge = dodgeMin;
            so.baseMoveSpeed = moveSpeed;
            so.baseCritRate = 0.05f;
            so.baseCritMultiplier = 1.5f;

            // MonsterData_SO 特有字段 = Max 值
            so.tags = tags;
            so.maxHP_Max = hpMax;
            so.maxATK_Max = atkMax;
            so.maxMATK_Max = matkMax;
            so.maxDEF_Max = defMax;
            so.maxMDEF_Max = mdefMax;
            so.dodge_Max = dodgeMax;
            so.critResist = critResist;
            so.attackInterval = atkInterval;
            so.baseExpReward = exp;
            so.goldDropMin = goldMin;
            so.goldDropMax = goldMax;
            so.mechanicDescription = mechDesc;

            if (onHit.HasValue)
                so.onHitEffects = new[] { onHit.Value };

            if (immunities != null && immunities.Length > 0)
                so.immuneToEffects = immunities;

            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"  已创建：{path}");
            return so;
        }

        /// <summary>
        /// 创建 BiomeConfig SO 并挂载怪物池
        /// </summary>
        private static void CreateBiomeConfig(int floor, string name, string id,
            List<MonsterData_SO> pool, MonsterData_SO boss)
        {
            string path = $"{BIOME_ROOT}/Biome_Floor{floor}_{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<BiomeConfig_SO>(path);
            if (existing != null) { Debug.Log($"  跳过已存在BiomeConfig：{path}"); return; }

            var biome = ScriptableObject.CreateInstance<BiomeConfig_SO>();
            biome.biomeName = name;
            biome.biomeID = id;
            biome.biomeIndex = floor;
            biome.biomeDescription = $"第{floor}层 - {name}";
            biome.normalMonsterPool = pool;
            biome.bossData = boss;
            biome.floorScalingConstant = 0.15f;
            biome.expScalingConstant = 0.10f;

            AssetDatabase.CreateAsset(biome, path);
            Debug.Log($"  已创建BiomeConfig：{path}");
        }

        private static string EnsureFloorDir(int floor)
        {
            string dir = $"{MONSTER_ROOT}/Floor{floor}";
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder(MONSTER_ROOT, $"Floor{floor}");
            return dir;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
#endif
