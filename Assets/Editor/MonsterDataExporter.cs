// ============================================================================
// 逃离魔塔 - 怪物数据导出工具 (MonsterDataExporter)
// Editor 菜单工具：一键将 Floor1MonsterRegistry 的程序化数据导出为 .asset 文件。
//
// 用法：Unity 菜单栏 → EscapeTheTower → 导出怪物数据
// 输出：Assets/Data/Monsters/ 目录下的 MonsterData_SO .asset 文件
//       + 一个 FloorMonsterConfig_SO .asset 文件（自动关联所有怪物引用）
// ============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using EscapeTheTower.Data;

#pragma warning disable CS0618 // 导出工具需要调用 Obsolete 的 Floor1MonsterRegistry
namespace EscapeTheTower.Editor
{
    public static class MonsterDataExporter
    {
        private const string OUTPUT_DIR = "Assets/Data/Monsters";
        private const string RESOURCES_DIR = "Assets/Resources";

        [MenuItem("EscapeTheTower/导出怪物数据（Floor1）")]
        public static void ExportFloor1Monsters()
        {
            // 确保输出目录存在
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
                AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(OUTPUT_DIR))
                AssetDatabase.CreateFolder("Assets/Data", "Monsters");

            // 导出每种怪物
            var slime       = ExportSingle(Floor1MonsterRegistry.CreateSlime(),            "Monster_Slime");
            var caveBat     = ExportSingle(Floor1MonsterRegistry.CreateCaveBat(),          "Monster_CaveBat");
            var skelMinion  = ExportSingle(Floor1MonsterRegistry.CreateSkeletonMinion(),   "Monster_SkeletonMinion");
            var skelArcher  = ExportSingle(Floor1MonsterRegistry.CreateSkeletonArcher(),   "Monster_SkeletonArcher");
            var skelMage    = ExportSingle(Floor1MonsterRegistry.CreateSkeletonMage(),     "Monster_SkeletonMage");
            var gobScout    = ExportSingle(Floor1MonsterRegistry.CreateGoblinScout(),      "Monster_GoblinScout");
            var gobWarrior  = ExportSingle(Floor1MonsterRegistry.CreateGoblinWarrior(),    "Monster_GoblinWarrior");
            var gobShaman   = ExportSingle(Floor1MonsterRegistry.CreateGoblinShaman(),     "Monster_GoblinShaman");
            var reaper      = ExportSingle(Floor1MonsterRegistry.CreateReaperApprentice(), "Monster_ReaperApprentice");
            var fallenHero  = ExportSingle(Floor1MonsterRegistry.CreateFallenHero(),       "Monster_FallenHero_Boss");

            // 创建楼层配置 SO（自动关联所有怪物引用）
            var config = ScriptableObject.CreateInstance<FloorMonsterConfig_SO>();
            config.normalMonsters = new MonsterData_SO[]
            {
                slime, caveBat, skelMinion, skelArcher, skelMage,
                gobScout, gobWarrior, gobShaman, reaper
            };
            config.bossData = fallenHero;

            string configPath = $"{OUTPUT_DIR}/Floor1Config.asset";
            AssetDatabase.CreateAsset(config, configPath);

            // 同时在 Resources 目录下放一份引用，供动态创建的 FloorTransitionManager 自动加载
            if (!AssetDatabase.IsValidFolder(RESOURCES_DIR))
                AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CopyAsset(configPath, $"{RESOURCES_DIR}/Floor1Config.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MonsterDataExporter] ✅ 导出完成！10 个怪物 SO + 1 个楼层配置 → {OUTPUT_DIR}/");
            EditorUtility.DisplayDialog("导出成功",
                $"已导出 10 个怪物 SO 资产 + 1 个楼层配置到:\n{OUTPUT_DIR}/\n\n" +
                "Floor1Config 已自动拷贝到 Resources，游戏启动时会自动加载。\n" +
                "如需修改怪物数据，直接编辑 Assets/Data/Monsters/ 下的 .asset 文件。",
                "确定");
        }

        /// <summary>
        /// 导出单个 MonsterData_SO 为 .asset 文件
        /// </summary>
        private static MonsterData_SO ExportSingle(MonsterData_SO so, string fileName)
        {
            string path = $"{OUTPUT_DIR}/{fileName}.asset";

            // 如果已存在则跳过（避免覆盖手动修改）
            var existing = AssetDatabase.LoadAssetAtPath<MonsterData_SO>(path);
            if (existing != null)
            {
                Debug.Log($"  跳过已存在：{path}");
                return existing;
            }

            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"  已创建：{path}");
            return so;
        }
    }
}
#endif
