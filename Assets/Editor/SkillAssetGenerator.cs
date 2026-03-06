// ============================================================================
// 逃离魔塔 - 技能 SO 资产导出工具 (SkillAssetGenerator)
// Editor 菜单工具：一键生成流浪剑客的技能 SkillData_SO .asset 文件。
//
// 用法：Unity 菜单栏 → EscapeTheTower → 导出技能数据（流浪剑客）
// ============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using EscapeTheTower.Data;

namespace EscapeTheTower.Editor
{
    public static class SkillAssetGenerator
    {
        private const string OUTPUT_DIR = "Assets/Data/Skills/Vagabond";

        [MenuItem("EscapeTheTower/导出技能数据（流浪剑客）")]
        public static void ExportVagabondSkills()
        {
            // 确保输出目录存在
            if (!AssetDatabase.IsValidFolder("Assets/Data/Skills"))
                AssetDatabase.CreateFolder("Assets/Data", "Skills");
            if (!AssetDatabase.IsValidFolder(OUTPUT_DIR))
                AssetDatabase.CreateFolder("Assets/Data/Skills", "Vagabond");

            // === 闪避：燕返 ===
            CreateSkillAsset("Skill_VagabondEvasion", so =>
            {
                so.skillID = "vagabond_evasion";
                so.skillName = "燕返";
                so.slotType = SkillSlotType.Evasion;
                so.description = "向面朝方向翻滚位移。HP<30%时无敌帧延长至0.5s。";
                so.cooldown = 1.5f;
                so.manaCost = 0f;
                so.hasMovement = true;
                so.movementDistance = 2.5f;
                so.grantsSuperArmor = false;
            });

            // === 技能一：疾风突刺 ===
            CreateSkillAsset("Skill_VagabondDashStrike", so =>
            {
                so.skillID = "vagabond_dash_strike";
                so.skillName = "疾风突刺";
                so.slotType = SkillSlotType.Active1;
                so.description = "向面朝方向突刺，路径上敌人受伤。每命中一个目标恢复10点法力。";
                so.cooldown = 8.0f;
                so.manaCost = 0f;
                so.baseDamage = 40f;
                so.atkScaling = 1.5f;
                so.matkScaling = 0f;
                so.damageType = DamageType.Physical;
                so.hasMovement = true;
                so.movementDistance = 3.0f;
                so.manaRestoreOnHit = 10f;
            });

            // === 技能二：旋风斩 ===
            CreateSkillAsset("Skill_VagabondWhirlwind", so =>
            {
                so.skillID = "vagabond_whirlwind";
                so.skillName = "旋风斩";
                so.slotType = SkillSlotType.Active2;
                so.description = "原地AOE攻击2秒，每0.5s对周围敌人造成伤害。期间移速-30%但免疫击退打断。";
                so.cooldown = 12.0f;
                so.manaCost = 30f;
                so.baseDamage = 15f;
                so.atkScaling = 0.6f;
                so.matkScaling = 0f;
                so.damageType = DamageType.Physical;
                so.isAOE = true;
                so.aoeRadius = 2.0f;
                so.aoeAngle = 360f;
                so.hitCount = 4; // 2s / 0.5s = 4 tick
                so.hitInterval = 0.5f;
                so.grantsSuperArmor = true;
            });

            // === 大招：极刃风暴 ===
            CreateSkillAsset("Skill_VagabondUltimate", so =>
            {
                so.skillID = "vagabond_ultimate";
                so.skillName = "极刃风暴";
                so.slotType = SkillSlotType.Ultimate;
                so.description = "消耗满怒气，8段AOE打击，绝对霸体免疫伤害，15%物理吸血。";
                so.cooldown = 1.0f; // 防连点安全间隔（实际由怒气驱动）
                so.manaCost = 0f;
                so.requiresFullRage = true;
                so.baseDamage = 30f;
                so.atkScaling = 0.8f;
                so.matkScaling = 0f;
                so.damageType = DamageType.Physical;
                so.isAOE = true;
                so.aoeRadius = 2.5f;
                so.aoeAngle = 360f;
                so.hitCount = 8;
                so.hitInterval = 0.15f;
                so.grantsInvincibility = true;
                so.grantsSuperArmor = true;
                so.lifeStealPercent = 0.15f;
            });

            // === 被动一：剑气纵横（数据记录，实际由 VagabondSwordPath 驱动） ===
            CreateSkillAsset("Skill_VagabondSwordPath", so =>
            {
                so.skillID = "vagabond_sword_path";
                so.skillName = "剑气纵横";
                so.slotType = SkillSlotType.Passive1;
                so.description = "普攻命中恢复5MP，叠加剑路(最高10层)。满层时普攻发射穿透剑气。脱战3秒后衰减。";
                so.cooldown = 0f;
                so.manaCost = 0f;
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SkillAssetGenerator] ✅ 导出完成！5 个技能 SO → {OUTPUT_DIR}/");
            EditorUtility.DisplayDialog("导出成功",
                $"已导出流浪剑客 5 个技能 SO 资产到:\n{OUTPUT_DIR}/\n\n" +
                "如需调整数值，直接在 Inspector 中编辑对应 .asset 文件。",
                "确定");
        }

        private static void CreateSkillAsset(string fileName, System.Action<SkillData_SO> configure)
        {
            string path = $"{OUTPUT_DIR}/{fileName}.asset";

            // 如果已存在则跳过
            var existing = AssetDatabase.LoadAssetAtPath<SkillData_SO>(path);
            if (existing != null)
            {
                Debug.Log($"  跳过已存在：{path}");
                return;
            }

            var so = ScriptableObject.CreateInstance<SkillData_SO>();
            configure(so);
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"  已创建：{path}");
        }
    }
}
#endif
