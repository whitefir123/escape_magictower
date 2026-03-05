// ============================================================================
// 逃离魔塔 - 装备系统运行时引导器 (EquipmentSystemBootstrap)
// 在场景加载时自动查找 AffixDatabase_SO 资产并注入到 LootTableHelper，
// 同步楼层深度和玩家寻宝率。
//
// 挂载方式：放在场景中常驻 GameObject 上（如 GameManager），
//          或由 TestSceneSetup 自动创建。
// ============================================================================

using UnityEngine;
using EscapeTheTower.Data;
using EscapeTheTower.Equipment;
using EscapeTheTower.Core;

namespace EscapeTheTower
{
    /// <summary>
    /// 装备系统运行时引导器 —— 自动注入词缀数据库并同步楼层参数
    /// </summary>
    public class EquipmentSystemBootstrap : MonoBehaviour
    {
        [Header("词缀数据库")]
        [Tooltip("拖入 AffixDatabase SO 资产。留空则自动从 Resources 加载。")]
        [SerializeField] private AffixDatabase_SO _affixDatabase;

        // === 单例 ===
        public static EquipmentSystemBootstrap Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 自动加载词缀数据库
            if (_affixDatabase == null)
            {
                _affixDatabase = TryLoadDatabase();
            }

            if (_affixDatabase != null)
            {
                // 预构建索引
                _affixDatabase.BuildIndex();
                // 注入到 LootTableHelper
                LootTableHelper.AffixDB = _affixDatabase;
                Debug.Log($"[EquipmentBootstrap] ✅ 词缀数据库已注入 ({_affixDatabase.allAffixes.Count} 条词缀)");
            }
            else
            {
                Debug.LogWarning("[EquipmentBootstrap] ⚠️ 未找到 AffixDatabase_SO，装备系统将无法生成词缀！");
            }
        }

        private void OnEnable()
        {
            // 监听楼层切换事件，实时同步楼层深度
            EventManager.Subscribe<OnFloorTransitionEvent>(OnFloorTransition);
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<OnFloorTransitionEvent>(OnFloorTransition);
        }

        /// <summary>
        /// 楼层切换时更新 LootTableHelper 的楼层参数
        /// </summary>
        private void OnFloorTransition(OnFloorTransitionEvent evt)
        {
            LootTableHelper.CurrentFloorLevel = evt.NewFloorLevel;
            Debug.Log($"[EquipmentBootstrap] 楼层深度更新: {evt.NewFloorLevel}");
        }

        /// <summary>
        /// 外部调用：同步玩家寻宝率（由属性管线重算后调用）
        /// </summary>
        public void UpdateMagicFind(float magicFind)
        {
            LootTableHelper.PlayerMagicFind = magicFind;
        }

        /// <summary>
        /// 手动设置楼层深度（初始化或调试用）
        /// </summary>
        public void SetFloorLevel(int level)
        {
            LootTableHelper.CurrentFloorLevel = level;
        }

        // =====================================================================
        //  数据库加载策略
        // =====================================================================

        /// <summary>
        /// 尝试从多个位置加载数据库
        /// 优先级：Resources → 已知固定路径
        /// </summary>
        private static AffixDatabase_SO TryLoadDatabase()
        {
            // 策略1：从 Resources 文件夹加载
            var db = Resources.Load<AffixDatabase_SO>("AffixDatabase");
            if (db != null) return db;

            // 策略2：查找场景中已存在的引用
            // （由 Editor 生成器放在 Assets/Data/Equipment/ 下，需手动拖入或放入 Resources）

            // 策略3：搜索项目中所有 AffixDatabase_SO 资产（仅Editor模式下有效）
            #if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("t:AffixDatabase_SO");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                db = UnityEditor.AssetDatabase.LoadAssetAtPath<AffixDatabase_SO>(path);
                if (db != null)
                {
                    Debug.Log($"[EquipmentBootstrap] 从项目中找到数据库: {path}");
                    return db;
                }
            }
            #endif

            return null;
        }

        // =====================================================================
        //  静态重置（Domain Reload 关闭时防止残留）
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }
    }
}
