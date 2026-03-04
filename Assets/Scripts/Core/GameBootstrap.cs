// ============================================================================
// 逃离魔塔 - 游戏启动引导器 (GameBootstrap)
// 在游戏启动时自动初始化全局配置系统。
// 挂载在首场景的一个持久 GameObject 上（DontDestroyOnLoad）。
//
// 职责：
//   1. 加载 BalanceConfig_SO → GameConstants.Initialize
//   2. 后续可扩展：初始化存档、音频管理器、网络等
// ============================================================================

using UnityEngine;
using EscapeTheTower.Data;

namespace EscapeTheTower.Core
{
    /// <summary>
    /// 游戏启动引导器 —— 全局初始化入口
    /// 确保全局配置在任何游戏逻辑之前完成加载
    /// </summary>
    [DefaultExecutionOrder(-1000)] // 确保在所有其他脚本之前执行
    public class GameBootstrap : MonoBehaviour
    {
        [Header("=== 全局配置 ===")]
        [Tooltip("平衡性配置 SO（可选，不填则使用 GameConstants 默认值）")]
        [SerializeField] private BalanceConfig_SO balanceConfig;

        /// <summary>全局单例（跨场景持久化）</summary>
        public static GameBootstrap Instance { get; private set; }

        /// <summary>是否已完成初始化</summary>
        public static bool IsInitialized { get; private set; }

        private void Awake()
        {
            // 单例守护
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeGame();
        }

        /// <summary>
        /// 全局初始化入口
        /// </summary>
        private void InitializeGame()
        {
            // ① 初始化平衡性配置
            if (balanceConfig != null)
            {
                GameConstants.Initialize(balanceConfig);
                Debug.Log("[GameBootstrap] BalanceConfig 已加载。");
            }
            else
            {
                Debug.Log("[GameBootstrap] 未配置 BalanceConfig，使用默认值。");
            }

            // ② 后续初始化扩展点
            // InitializeAudioManager();
            // InitializeSaveSystem();
            // InitializeNetworkManager();

            IsInitialized = true;
            Debug.Log("[GameBootstrap] 游戏初始化完毕。");
        }

        /// <summary>
        /// 运行时热替换配置（调试用）
        /// </summary>
        public void ReloadBalanceConfig(BalanceConfig_SO newConfig)
        {
            if (newConfig == null) return;
            balanceConfig = newConfig;
            GameConstants.Initialize(newConfig);
            Debug.Log("[GameBootstrap] BalanceConfig 已热替换。");
        }
    }
}
