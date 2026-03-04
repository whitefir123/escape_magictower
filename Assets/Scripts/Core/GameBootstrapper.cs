// ============================================================================
// 逃离魔塔 - 游戏启动引导器 (GameBootstrapper)
// 场景中的根级 MonoBehaviour，负责在 Awake 中注册所有核心服务。
// 确保此对象挂载在场景最顶层，且 Script Execution Order 最优先。
// ============================================================================

using UnityEngine;
using EscapeTheTower.Combat;
using EscapeTheTower.Data;
using EscapeTheTower.Map;
using EscapeTheTower.Rune;
using EscapeTheTower.UI;
using EscapeTheTower.Entity.Monster;

namespace EscapeTheTower.Core
{
    /// <summary>
    /// 游戏启动引导器 —— 初始化所有核心服务并注册到 ServiceLocator
    /// </summary>
    public class GameBootstrapper : MonoBehaviour
    {
        [Header("=== 核心服务组件引用 ===")]
        [SerializeField] private CommandBuffer _commandBuffer;
        [SerializeField] private FatigueSystem _fatigueSystem;
        [SerializeField] private MapManager _mapManager;
        [SerializeField] private MonsterSpawner _monsterSpawner;
        [SerializeField] private RuneManager _runeManager;
        [SerializeField] private SaveSystem _saveSystem;
        [SerializeField] private FloatingTextManager _floatingTextManager;
        [SerializeField] private HUDManager _hudManager;
        [SerializeField] private VictoryScreen _victoryScreen;
        [SerializeField] private RuneSelectionPanel _runeSelectionPanel;

        private void Awake()
        {
            // 防止场景切换时被销毁
            DontDestroyOnLoad(gameObject);

            InitializeCoreServices();
        }

        private void Start()
        {
            StartNewRun();
        }

        /// <summary>
        /// 初始化并注册所有核心服务
        /// </summary>
        private void InitializeCoreServices()
        {
            Debug.Log("[GameBootstrapper] 正在初始化核心服务...");

            // 1. 清空上一次的残留（场景热重载时）
            ServiceLocator.ClearAll();
            EventManager.ClearAll();

            // 2. 指令缓存队列
            EnsureAndRegister(ref _commandBuffer);

            // 3. 战斗子系统
            EnsureAndRegister(ref _fatigueSystem);

            // 4. 地图系统
            EnsureAndRegister(ref _mapManager);

            // 5. 怪物生成器
            EnsureAndRegister(ref _monsterSpawner);

            // 6. 符文系统
            EnsureAndRegister(ref _runeManager);

            // 7. 存档系统
            EnsureAndRegister(ref _saveSystem);

            // 8. UI 系统
            EnsureAndRegister(ref _floatingTextManager);
            EnsureAndRegister(ref _hudManager);
            EnsureAndRegister(ref _victoryScreen);
            EnsureAndRegister(ref _runeSelectionPanel);

            Debug.Log("[GameBootstrapper] 核心服务初始化完成！共注册 10 个服务。");
        }

        /// <summary>
        /// 确保组件存在并注册到 ServiceLocator
        /// </summary>
        private void EnsureAndRegister<T>(ref T component) where T : MonoBehaviour
        {
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }
            ServiceLocator.Register(component);
        }

        /// <summary>
        /// 开始一局新的轮回
        /// </summary>
        private void StartNewRun()
        {
            Debug.Log("[GameBootstrapper] 开始新轮回...");

            // 创建新存档
            _saveSystem.CreateNewSave("vagabond_swordsman");

            // 初始化符文系统
            _runeManager.Initialize(HeroClass.VagabondSwordsman);

            // 初始化地图（第一层暗黑地牢）
            _mapManager.StartNewRun();

            Debug.Log("[GameBootstrapper] 新轮回启动完毕！");
        }

        private void OnDestroy()
        {
            // 清理所有服务注册，防止静态引用残留
            ServiceLocator.ClearAll();
            EventManager.ClearAll();
        }
    }
}
