// ============================================================================
// 逃离魔塔 - 存档系统 (SaveSystem)
// 管理局内进度的手动/自动存档与加载。
// 种子不可逆（加载后无法回退），属性管线加载时全量重算。
//
// 来源：DesignDocs/09_DataFlow_and_Status.md 第三节
// ============================================================================

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using EscapeTheTower.Core;

namespace EscapeTheTower.Data
{
    /// <summary>
    /// 存档数据结构 —— JSON 序列化的全量快照
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>存档版本号（兼容性检测用）</summary>
        public int saveVersion = 1;

        /// <summary>存档时间戳</summary>
        public string saveTimestamp;

        /// <summary>随机种子</summary>
        public int randomSeed;

        // === 英雄状态 ===
        public string heroClassID;
        public int heroLevel;
        public int heroExp;
        public int gold;
        public float currentHP;
        public float currentMP;
        public float currentRage;

        // === 已获取符文 ID 列表 ===
        public List<string> acquiredRuneIDs = new List<string>();

        // === 地图进度 ===
        public int currentFloor;
        public int currentRoomID;
        public List<int> exploredRoomIDs = new List<int>();
        public List<int> clearedRoomIDs = new List<int>();

        // === 游戏时间 ===
        public float totalPlayTime;
        public int totalKills;
    }

    /// <summary>
    /// 存档系统 —— 提供存/读/删三大操作
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        /// <summary>存档文件名</summary>
        private const string SAVE_FILE_NAME = "mota_save.json";

        /// <summary>自动存档间隔（秒）</summary>
        private const float AUTO_SAVE_INTERVAL = 120f;

        /// <summary>当前存档数据（运行时缓存）</summary>
        public SaveData CurrentSave { get; private set; }

        /// <summary>是否存在有效存档</summary>
        public bool HasSave => File.Exists(GetSavePath());

        // === 自动存档计时 ===
        private float _autoSaveTimer;
        private bool _autoSaveEnabled = true;

        // =====================================================================
        //  文件路径
        // =====================================================================

        private string GetSavePath()
        {
            return Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
        }

        // =====================================================================
        //  存档
        // =====================================================================

        /// <summary>
        /// 手动存档
        /// </summary>
        public void Save()
        {
            if (CurrentSave == null)
            {
                CurrentSave = new SaveData();
            }

            CurrentSave.saveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            CurrentSave.randomSeed = UnityEngine.Random.state.GetHashCode();

            // 收集英雄数据
            CollectHeroData();

            // 收集地图数据
            CollectMapData();

            // 序列化为 JSON 并写入文件
            string json = JsonUtility.ToJson(CurrentSave, true);

            try
            {
                File.WriteAllText(GetSavePath(), json);
                Debug.Log($"[SaveSystem] 存档成功：{GetSavePath()}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 存档失败：{e.Message}");
            }
        }

        /// <summary>
        /// 收集当前英雄数据到存档结构
        /// </summary>
        private void CollectHeroData()
        {
            // 通过 ServiceLocator 获取 HeroController
            // MVP 阶段简化实现：由外部主动调用 UpdateSaveData 设置
        }

        /// <summary>
        /// 收集当前地图数据到存档结构
        /// </summary>
        private void CollectMapData()
        {
            // 由 MapManager 提供数据
        }

        /// <summary>
        /// 由外部系统主动更新存档数据
        /// </summary>
        public void UpdateSaveData(Action<SaveData> updater)
        {
            if (CurrentSave == null) CurrentSave = new SaveData();
            updater?.Invoke(CurrentSave);
        }

        // =====================================================================
        //  读档
        // =====================================================================

        /// <summary>
        /// 加载存档（如果存在）
        /// </summary>
        /// <returns>是否加载成功</returns>
        public bool Load()
        {
            string path = GetSavePath();
            if (!File.Exists(path))
            {
                Debug.Log("[SaveSystem] 没有找到存档文件。");
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                CurrentSave = JsonUtility.FromJson<SaveData>(json);

                // 版本兼容性检测
                if (CurrentSave.saveVersion != 1)
                {
                    Debug.LogWarning($"[SaveSystem] 存档版本不匹配：{CurrentSave.saveVersion} vs 1。尝试兼容加载。");
                }

                Debug.Log($"[SaveSystem] 读档成功！楼层={CurrentSave.currentFloor} " +
                          $"等级=Lv.{CurrentSave.heroLevel} 时间={CurrentSave.saveTimestamp}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 读档失败：{e.Message}");
                return false;
            }
        }

        // =====================================================================
        //  删档
        // =====================================================================

        /// <summary>
        /// 删除存档
        /// </summary>
        public void DeleteSave()
        {
            string path = GetSavePath();
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                    CurrentSave = null;
                    Debug.Log("[SaveSystem] 存档已删除。");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveSystem] 删档失败：{e.Message}");
                }
            }
        }

        // =====================================================================
        //  自动存档
        // =====================================================================

        private void Update()
        {
            if (!_autoSaveEnabled) return;

            _autoSaveTimer += Time.deltaTime;
            if (_autoSaveTimer >= AUTO_SAVE_INTERVAL)
            {
                _autoSaveTimer = 0f;
                Save();
                Debug.Log("[SaveSystem] 自动存档触发。");
            }
        }

        /// <summary>
        /// 切换自动存档开关
        /// </summary>
        public void SetAutoSave(bool enabled)
        {
            _autoSaveEnabled = enabled;
        }

        // =====================================================================
        //  新存档
        // =====================================================================

        /// <summary>
        /// 创建一份新存档（新轮回开始时调用）
        /// </summary>
        public SaveData CreateNewSave(string heroClassID, int seed = -1)
        {
            CurrentSave = new SaveData
            {
                saveVersion = 1,
                saveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                randomSeed = seed >= 0 ? seed : UnityEngine.Random.Range(0, int.MaxValue),
                heroClassID = heroClassID,
                heroLevel = 1,
                heroExp = 0,
                gold = 0,
                currentFloor = 1,
                currentRoomID = 1,
                totalPlayTime = 0f,
                totalKills = 0,
            };

            // 初始化随机种子
            UnityEngine.Random.InitState(CurrentSave.randomSeed);

            Debug.Log($"[SaveSystem] 新存档创建！种子={CurrentSave.randomSeed} 职业={heroClassID}");
            return CurrentSave;
        }
    }
}
