// ============================================================================
// 逃离魔塔 - 套装共鸣 SO 批量生成器 (Editor Only)
// 根据 07_Legendary_Equipment_System.md 的定义批量创建 7 套 SO 资产
//
// 使用方式：Unity 菜单栏 → EscapeTheTower → 生成套装共鸣 SO
// ============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using EscapeTheTower.Equipment;
using EscapeTheTower.Equipment.SetResonance;
using EscapeTheTower.Data;

namespace EscapeTheTower.Editor
{
    public static class SetResonanceAssetGenerator
    {
        private const string OUTPUT_PATH = "Assets/Data/SetResonance";

        [MenuItem("EscapeTheTower/生成套装共鸣 SO")]
        public static void GenerateAll()
        {
            // 确保输出目录存在
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
                AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(OUTPUT_PATH))
                AssetDatabase.CreateFolder("Assets/Data", "SetResonance");

            // === 1. 狂战士之怒 ===
            CreateSet("Berserker", "狂战士之怒", "Fury of the Berserker",
                "物理系极速吸血流",
                "EscapeTheTower.Equipment.SetResonance.Passives.BerserkerSetPassive",
                new[] {
                    Req(EquipmentSlot.Weapon,    "重刃的",    "…之吸血"),
                    Req(EquipmentSlot.Helmet,    "壮容的",    "…之自愈"),
                    Req(EquipmentSlot.Armor,     "坚韧的",    "…之反伤"),
                    Req(EquipmentSlot.Gloves,    "坚固的",    "…之减CD"),
                    Req(EquipmentSlot.Boots,     "闪避的",    "…之移速"),
                    Req(EquipmentSlot.Accessory, "必爆的",    "…之狂力"),
                });

            // === 2. 星体观测者 ===
            CreateSet("Stargazer", "星体观测者", "Stargazer's Wisdom",
                "法系必爆冷却流",
                "EscapeTheTower.Equipment.SetResonance.Passives.StargazerSetPassive",
                new[] {
                    Req(EquipmentSlot.Weapon,    "会心的",    "…之回蓝"),
                    Req(EquipmentSlot.Helmet,    "抗魔的",    "…之减CD"),
                    Req(EquipmentSlot.Armor,     "坚固的",    "…之守护"),
                    Req(EquipmentSlot.Gloves,    "坚韧的",    "…之贪婪"),
                    Req(EquipmentSlot.Boots,     "抗魔的",    "…之自愈"),
                    Req(EquipmentSlot.Accessory, "扩蓝的",    "极速的"),
                });

            // === 3. 不毁之钢屏障 ===
            CreateSet("Aegis", "不毁之钢屏障", "Aegis of Indestructible Steel",
                "重装反伤流",
                "EscapeTheTower.Equipment.SetResonance.Passives.AegisSetPassive",
                new[] {
                    Req(EquipmentSlot.Weapon,    "破甲的",    "…之连击"),
                    Req(EquipmentSlot.Helmet,    "减伤的",    "…之守护"),
                    Req(EquipmentSlot.Armor,     "减伤的",    "…之反伤"),
                    Req(EquipmentSlot.Gloves,    "坚固的",    "…之减CD"),
                    Req(EquipmentSlot.Boots,     "坚固的",    "…之贪婪"),
                    Req(EquipmentSlot.Accessory, "极速的",    "…之护盾"),
                });

            // === 4. 召唤使的仪典 ===
            CreateSet("Summoner", "召唤使的仪典", "Ritual of the Summoner",
                "万灵长河统御套（待召唤物系统）",
                "EscapeTheTower.Equipment.SetResonance.Passives.SummonerSetPassive",
                new[] {
                    Req(EquipmentSlot.Weapon,    "致命的",    "…之斩杀"),
                    Req(EquipmentSlot.Helmet,    "壮容的",    "…之减CD"),
                    Req(EquipmentSlot.Armor,     "抗魔的",    "…之自愈"),
                    Req(EquipmentSlot.Gloves,    "闪避的",    "…之贪婪"),
                    Req(EquipmentSlot.Boots,     "闪避的",    "…之移速"),
                    Req(EquipmentSlot.Accessory, "必爆的",    "…之处决"),
                });

            // === 5. 时光刺客信条 ===
            CreateSet("Chrono", "时光刺客信条", "Creed of the Chrono-Assassin",
                "极致减CD技能刷新流",
                "EscapeTheTower.Equipment.SetResonance.Passives.ChronoSetPassive",
                new[] {
                    Req(EquipmentSlot.Weapon,    "致命的",    "…之急速"),
                    Req(EquipmentSlot.Helmet,    "减伤的",    "…之减CD"),
                    Req(EquipmentSlot.Armor,     "壮容的",    "…之守护"),
                    Req(EquipmentSlot.Gloves,    "闪避的",    "…之减CD"),
                    Req(EquipmentSlot.Boots,     "抗魔的",    "…之移速"),
                    Req(EquipmentSlot.Accessory, "极速的",    "…之幻影"),
                });

            // === 6. 极寒暴君之怒 ===
            CreateSet("Glacial", "极寒暴君之怒", "Wrath of the Glacial Tyrant",
                "绝对冰封控制流",
                "EscapeTheTower.Equipment.SetResonance.Passives.GlacialSetPassive",
                new[] {
                    Req(EquipmentSlot.Weapon,    "强攻的",    "…之冰缓"),
                    Req(EquipmentSlot.Helmet,    "坚固的",    "…之自愈"),
                    Req(EquipmentSlot.Armor,     "抗魔的",    "…之反伤"),
                    Req(EquipmentSlot.Gloves,    "坚韧的",    "…之贪婪"),
                    Req(EquipmentSlot.Boots,     "坚固的",    "…之移速"),
                    Req(EquipmentSlot.Accessory, "元素的",    "…之护盾"),
                });

            // === 7. 财阀的黑心科技 ===
            CreateSet("Plutocrat", "财阀的黑心科技", "Plutocrat's Dark Tech",
                "金币轰炸流",
                "EscapeTheTower.Equipment.SetResonance.Passives.PlutocratSetPassive",
                new[] {
                    Req(EquipmentSlot.Weapon,    "会心的",    "…之连击"),
                    Req(EquipmentSlot.Helmet,    "壮容的",    "…之守护"),
                    Req(EquipmentSlot.Armor,     "坚韧的",    "…之守护"),
                    Req(EquipmentSlot.Gloves,    "抗魔的",    "…之贪婪"),
                    Req(EquipmentSlot.Boots,     "闪避的",    "…之贪婪"),
                    Req(EquipmentSlot.Accessory, "扩蓝的",    "…之狂力"),
                });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // === 自动挂载到 HeroEquipmentManager ===
            AutoAssignToHero();

            Debug.Log($"[套装共鸣] ✅ 7 套 SO 资产已生成至 {OUTPUT_PATH} 并自动挂载");
        }

        /// <summary>
        /// 自动将生成的 SO 挂载到场景中/预制体上的 HeroEquipmentManager
        /// </summary>
        private static void AutoAssignToHero()
        {
            // 加载所有生成的 SO
            string[] guids = AssetDatabase.FindAssets("t:SetResonanceDefinition_SO", new[] { OUTPUT_PATH });
            var definitions = new SetResonanceDefinition_SO[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                definitions[i] = AssetDatabase.LoadAssetAtPath<SetResonanceDefinition_SO>(path);
            }

            if (definitions.Length == 0)
            {
                Debug.LogWarning("[套装共鸣] 未找到生成的 SO 资产");
                return;
            }

            // 方式1：查找场景中的 HeroEquipmentManager
            var manager = Object.FindFirstObjectByType<HeroEquipmentManager>();
            if (manager != null)
            {
                var so = new SerializedObject(manager);
                var prop = so.FindProperty("_setDefinitions");
                prop.arraySize = definitions.Length;
                for (int i = 0; i < definitions.Length; i++)
                {
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(manager);
                Debug.Log($"[套装共鸣] ✅ 已自动挂载 {definitions.Length} 个 SO 到场景中的 HeroEquipmentManager");
                return;
            }

            // 方式2：查找预制体
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab Hero");
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var prefabManager = prefab?.GetComponent<HeroEquipmentManager>();
                if (prefabManager != null)
                {
                    var so = new SerializedObject(prefabManager);
                    var prop = so.FindProperty("_setDefinitions");
                    prop.arraySize = definitions.Length;
                    for (int i = 0; i < definitions.Length; i++)
                    {
                        prop.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
                    }
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(prefab);
                    Debug.Log($"[套装共鸣] ✅ 已自动挂载到预制体: {path}");
                    return;
                }
            }

            Debug.LogWarning("[套装共鸣] ⚠ 未找到 HeroEquipmentManager（场景未加载 / 预制体不存在），请手动拖入");
        }

        private static SlotAffixRequirement Req(EquipmentSlot slot, string opt1, string opt2)
        {
            return new SlotAffixRequirement
            {
                slot = slot,
                affixOption1 = opt1,
                affixOption2 = opt2,
            };
        }

        private static void CreateSet(string filePrefix, string nameCN, string nameEN,
            string desc, string passiveClass, SlotAffixRequirement[] reqs)
        {
            var so = ScriptableObject.CreateInstance<SetResonanceDefinition_SO>();
            so.setNameCN = nameCN;
            so.setNameEN = nameEN;
            so.description = desc;
            so.passiveClassName = passiveClass;
            so.slotRequirements = reqs;

            string path = $"{OUTPUT_PATH}/SetResonance_{filePrefix}.asset";
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[套装共鸣] 生成: {nameCN} → {path}");
        }
    }
}
#endif
