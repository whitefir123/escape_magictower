// ============================================================================
// 逃离魔塔 - 战斗疲劳系统 (FatigueSystem)
// 软狂暴机制：玩家在同一场战斗/同一楼层停留过久后，
// 怪物的攻击力与穿甲持续递增，逼迫玩家推进。
//
// 来源：DesignDocs/03_Combat_System.md 第 1.4 节
//       DesignDocs/06_Map_and_Modes.md（死神机制）
// ============================================================================

using UnityEngine;
using EscapeTheTower.Core;

namespace EscapeTheTower.Combat
{
    /// <summary>
    /// 战斗疲劳系统 —— 全局管理，驱动怪物软狂暴
    /// </summary>
    public class FatigueSystem : MonoBehaviour
    {
        /// <summary>当前疲劳层数</summary>
        public int CurrentStacks { get; private set; }

        /// <summary>是否已达致命阈值</summary>
        public bool IsLethal => CurrentStacks >= GameConstants.FATIGUE_LETHAL_STACKS;

        // === 计时器 ===
        private float _tickTimer;
        private float _floorTimer;
        private bool _isActive;

        [Header("=== 疲劳配置 ===")]
        [Tooltip("疲劳层叠间隔 (秒)")]
        [SerializeField] private float tickInterval = 10f;

        [Tooltip("每层攻击力增幅 (百分比)")]
        [SerializeField] private float atkBonusPerStack = 0.05f;

        [Tooltip("每层穿甲增幅 (百分比)")]
        [SerializeField] private float penBonusPerStack = 0.02f;

        // =====================================================================
        //  接口
        // =====================================================================

        /// <summary>进入新楼层时重置疲劳</summary>
        public void ResetFatigue()
        {
            CurrentStacks = 0;
            _tickTimer = 0f;
            _floorTimer = 0f;
            _isActive = true;

            Debug.Log("[FatigueSystem] 疲劳系统已重置。");
        }

        /// <summary>暂停疲劳累积（如进入商店/休息间）</summary>
        public void PauseFatigue()
        {
            _isActive = false;
        }

        /// <summary>恢复疲劳累积</summary>
        public void ResumeFatigue()
        {
            _isActive = true;
        }

        /// <summary>
        /// 获取当前疲劳对怪物攻击力的乘算系数
        /// </summary>
        public float GetAtkMultiplier()
        {
            return 1f + CurrentStacks * atkBonusPerStack;
        }

        /// <summary>
        /// 获取当前疲劳对怪物穿甲的加算值
        /// </summary>
        public float GetPenBonus()
        {
            return CurrentStacks * penBonusPerStack;
        }

        /// <summary>
        /// 获取当前楼层停留时间
        /// </summary>
        public float GetFloorStayTime()
        {
            return _floorTimer;
        }

        /// <summary>
        /// 是否触发死神（血月警告）
        /// </summary>
        public bool IsReaperTriggered()
        {
            return _floorTimer >= GameConstants.REAPER_TRIGGER_TIME_SECONDS;
        }

        // =====================================================================
        //  生命周期
        // =====================================================================

        private void Awake()
        {
            // 订阅楼层进入事件
            EventManager.Subscribe<OnFloorEnterEvent>(OnFloorEnter);
        }

        private void Update()
        {
            if (!_isActive) return;

            float dt = Time.deltaTime;
            _tickTimer += dt;
            _floorTimer += dt;

            // 每 tick 叠加一层疲劳
            if (_tickTimer >= tickInterval)
            {
                _tickTimer = 0f;
                CurrentStacks++;

                if (CurrentStacks >= GameConstants.FATIGUE_LETHAL_STACKS)
                {
                    Debug.LogWarning($"[FatigueSystem] 疲劳层数 {CurrentStacks} 已达秒杀阈值！");
                }
            }

            // 死神计时（45分钟阈值）
            if (_floorTimer >= GameConstants.REAPER_TRIGGER_TIME_SECONDS && _floorTimer - dt < GameConstants.REAPER_TRIGGER_TIME_SECONDS)
            {
                Debug.LogWarning("[FatigueSystem] ⚠️ 血月死神已降临！");
                // TODO: 触发死神追杀事件
            }
        }

        private void OnFloorEnter(OnFloorEnterEvent evt)
        {
            ResetFatigue();
        }

        private void OnDestroy()
        {
            EventManager.Unsubscribe<OnFloorEnterEvent>(OnFloorEnter);
        }
    }
}
