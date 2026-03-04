// ============================================================================
// 逃离魔塔 - 测试场景一键部署 (TestSceneSetup)
// 挂载到场景中任意空物体上，运行时自动部署测试场景。
// 楼层管理已委托给 FloorTransitionManager，此脚本仅负责初始化入口。
// ============================================================================

using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Map;

namespace EscapeTheTower.DevTools
{
    /// <summary>
    /// 测试场景一键部署 —— 创建 FloorTransitionManager 并初始化第一层
    /// </summary>
    public class TestSceneSetup : MonoBehaviour
    {
        [Header("=== 地图配置 ===")]
        [Tooltip("地图宽度（格）")]
        [SerializeField] private int mapWidth = 55;

        [Tooltip("地图高度（格）")]
        [SerializeField] private int mapHeight = 55;

        [Tooltip("随机种子（0 = 随机）")]
        [SerializeField] private int mapSeed = 0;

        [Header("=== 怪物配置 ===")]
        [Tooltip("生成的普通怪物数量")]
        [SerializeField] private int normalMonsterCount = 3;

        [Tooltip("是否生成 Boss")]
        [SerializeField] private bool spawnBoss = true;

        private void Start()
        {
            Invoke(nameof(SetupScene), 0.2f);
        }

        private void SetupScene()
        {
            // 1. 创建 FloorTransitionManager（如果不存在）
            if (FloorTransitionManager.Instance == null)
            {
                var ftmObj = new GameObject("FloorTransitionManager");
                var ftm = ftmObj.AddComponent<FloorTransitionManager>();

                // 通过反射传递配置（FloorTransitionManager 的字段为 SerializeField）
                SetPrivateField(ftm, "mapWidth", mapWidth);
                SetPrivateField(ftm, "mapHeight", mapHeight);
                SetPrivateField(ftm, "baseSeed", mapSeed);
                SetPrivateField(ftm, "normalMonsterCount", normalMonsterCount);
                SetPrivateField(ftm, "spawnBoss", spawnBoss);
            }

            // 2. 设置摄像机跟随
            SetupCameraFollow();

            // 3. 初始化首层
            FloorTransitionManager.Instance.InitializeFirstFloor();

            // 4. 摄像机跟随玩家
            var hero = FindAnyObjectByType<EscapeTheTower.Entity.Hero.HeroController>();
            if (hero != null)
            {
                var mainCam = Camera.main;
                var follow = mainCam?.GetComponent<CameraFollow>();
                follow?.SetTarget(hero.transform);
            }

            // 5. 部署 HUD
            if (FindAnyObjectByType<EscapeTheTower.UI.HUDManager>() == null)
            {
                var hudObj = new GameObject("HUDManager");
                hudObj.AddComponent<EscapeTheTower.UI.HUDManager>();
            }

            Debug.Log("──────────────────────────────────────");
            Debug.Log("[TestSceneSetup] 测试场景部署完毕！（委托 FloorTransitionManager）");
            Debug.Log("[TestSceneSetup] WASD=移动 | 碰怪=攻击 | 碰门=消耗钥匙 | 碰宝箱=开启 | 踩楼梯=下一层");
            Debug.Log("──────────────────────────────────────");
        }

        // =====================================================================
        //  摄像机跟随
        // =====================================================================

        private void SetupCameraFollow()
        {
            var mainCam = Camera.main;
            if (mainCam == null) return;

            var follow = mainCam.GetComponent<CameraFollow>();
            if (follow == null)
            {
                follow = mainCam.gameObject.AddComponent<CameraFollow>();
            }

            // 调整摄像机尺寸以适配格子地图（正交摄像机）
            mainCam.orthographic = true;
            mainCam.orthographicSize = 8f;
        }

        // =====================================================================
        //  工具方法
        // =====================================================================

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
