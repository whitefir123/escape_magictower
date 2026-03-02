// ============================================================================
// 逃离魔塔 - 胜利界面 (VictoryScreen)
// MVP 阶段的胜利结算 UI。
// 击败第一层 Boss 后弹出，显示战斗统计数据。
//
// 来源：DesignDocs/07_UI_and_UX.md
// ============================================================================

using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Entity.Hero;

namespace EscapeTheTower.UI
{
    /// <summary>
    /// 胜利界面管理器
    /// </summary>
    public class VictoryScreen : MonoBehaviour
    {
        [Header("=== 引用 ===")]
        [SerializeField] private HeroController heroReference;

        // === 统计数据 ===
        private int _totalKills;
        private int _totalGold;
        private int _finalLevel;
        private float _totalTime;
        private bool _isVisible;

        // =====================================================================
        //  生命周期
        // =====================================================================

        private void Awake()
        {
            // 默认隐藏
            gameObject.SetActive(false);

            EventManager.Subscribe<OnEntityKillEvent>(OnKill);
        }

        // =====================================================================
        //  事件
        // =====================================================================

        private void OnKill(OnEntityKillEvent evt)
        {
            // 统计击杀数（仅计玩家击杀）
            if (heroReference != null && evt.KillerEntityID == heroReference.EntityID)
            {
                _totalKills++;
            }
        }

        // =====================================================================
        //  显示胜利界面
        // =====================================================================

        /// <summary>
        /// 显示胜利结算界面
        /// </summary>
        public void ShowVictory()
        {
            _isVisible = true;
            gameObject.SetActive(true);
            Time.timeScale = 0f; // 暂停游戏

            if (heroReference != null)
            {
                _finalLevel = heroReference.CurrentLevel;
                _totalGold = heroReference.Gold;
            }

            Debug.Log("===========================================");
            Debug.Log("               🏆 胜利！                   ");
            Debug.Log("===========================================");
            Debug.Log($"  等级: Lv.{_finalLevel}");
            Debug.Log($"  击杀: {_totalKills}");
            Debug.Log($"  金币: {_totalGold}");
            Debug.Log($"  用时: {FormatTime(_totalTime)}");
            Debug.Log("===========================================");

            // TODO: 渲染 Canvas UI 面板
        }

        /// <summary>
        /// 关闭胜利界面并返回主菜单
        /// </summary>
        public void OnContinueClicked()
        {
            Time.timeScale = 1f;
            _isVisible = false;
            gameObject.SetActive(false);

            // TODO: 返回主菜单或进入下一层
        }

        // =====================================================================
        //  工具方法
        // =====================================================================

        private void Update()
        {
            if (!_isVisible)
            {
                _totalTime += Time.deltaTime;
            }
        }

        private string FormatTime(float seconds)
        {
            int minutes = (int)(seconds / 60f);
            int secs = (int)(seconds % 60f);
            return $"{minutes:D2}:{secs:D2}";
        }

        private void OnDestroy()
        {
            EventManager.Unsubscribe<OnEntityKillEvent>(OnKill);
        }
    }
}
