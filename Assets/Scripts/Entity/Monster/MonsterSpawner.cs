// ============================================================================
// 逃离魔塔 - 怪物生成器 (MonsterSpawner)
// 负责在战斗房中根据 BiomeConfig 和同心圆距离法则生成怪物。
// 包含精英突变检定、波次生成、难度曲线控制。
//
// 来源：GameData_Blueprints/04_01_Monster_Spawn_Logic.md
//       DesignDocs/06_Map_and_Modes.md
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Data;
using EscapeTheTower.Map;

namespace EscapeTheTower.Entity.Monster
{
    /// <summary>
    /// 怪物生成器 —— 战斗房的怪物填充系统
    /// </summary>
    public class MonsterSpawner : MonoBehaviour
    {
        [Header("=== 生成配置 ===")]
        [Tooltip("怪物预制体模板（运行时创建的通用实例）")]
        [SerializeField] private GameObject monsterPrefab;

        [Tooltip("Boss 预制体模板")]
        [SerializeField] private GameObject bossPrefab;

        /// <summary>当前房间的活跃怪物列表</summary>
        public List<MonsterBase> ActiveMonsters { get; private set; } = new List<MonsterBase>();

        /// <summary>房间是否已清空</summary>
        public bool IsRoomCleared => ActiveMonsters.TrueForAll(m => !m.IsAlive);

        // =====================================================================
        //  主接口
        // =====================================================================

        /// <summary>
        /// 为指定房间生成怪物
        /// </summary>
        /// <param name="room">房间节点</param>
        /// <param name="floorNumber">当前楼层</param>
        /// <param name="totalRoomCount">楼层总房间数（用于计算同心圆距离因子）</param>
        /// <param name="monsterPool">怪物数据池（从 Floor1MonsterRegistry 获取）</param>
        public void SpawnRoom(RoomNode room, int floorNumber, int totalRoomCount, MonsterData_SO[] monsterPool)
        {
            ClearRoom();

            if (room.Type == RoomType.Boss)
            {
                SpawnBoss(room, floorNumber, totalRoomCount);
                return;
            }

            if (room.Type != RoomType.Combat)
            {
                return; // 非战斗房不生成怪物
            }

            // 同心圆距离因子：roomIndex / totalRooms（越远越强）
            float distanceFactor = Mathf.Clamp01((float)room.RoomID / totalRoomCount);

            // 怪物数量（内环少外环多）
            int baseCount = Mathf.RoundToInt(Mathf.Lerp(2f, 6f, distanceFactor));
            int monsterCount = baseCount + Random.Range(-1, 2); // ±1 随机波动
            monsterCount = Mathf.Clamp(monsterCount, 1, 8);

            for (int i = 0; i < monsterCount; i++)
            {
                // 从池中随机选择怪物类型
                MonsterData_SO data = monsterPool[Random.Range(0, monsterPool.Length)];

                // 精英突变检定
                bool isElite = Random.value < GameConstants.ELITE_MUTATION_CHANCE;
                float eliteMult = isElite
                    ? Random.Range(GameConstants.ELITE_STAT_MULTIPLIER_MIN, GameConstants.ELITE_STAT_MULTIPLIER_MAX)
                    : 1f;

                // 计算生成位置（围绕房间中心散布）
                Vector3 spawnPos = GetSpawnPosition(room.GridPosition, i, monsterCount);

                // 创建怪物实例
                SpawnMonster(data, spawnPos, distanceFactor, floorNumber, isElite, eliteMult);
            }

            Debug.Log($"[MonsterSpawner] 房间 {room.RoomID} 生成 {monsterCount} 只怪物" +
                      $"（距离因子={distanceFactor:F2}）");
        }

        /// <summary>
        /// 生成 Boss
        /// </summary>
        private void SpawnBoss(RoomNode room, int floorNumber, int totalRoomCount)
        {
            // Boss 吃满额距离乘区
            float distanceFactor = 1.0f;

            MonsterData_SO bossData = Floor1MonsterRegistry.CreateFallenHero();
            Vector3 spawnPos = new Vector3(room.GridPosition.x * 5f, room.GridPosition.y * 5f, 0f);

            GameObject bossObj;
            if (bossPrefab != null)
            {
                bossObj = Instantiate(bossPrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                // MVP 阶段简化：无预制体时创建空物体
                bossObj = new GameObject($"Boss_{bossData.entityName}");
                bossObj.transform.position = spawnPos;
            }

            var monster = bossObj.GetComponent<MonsterBase>();
            if (monster == null) monster = bossObj.AddComponent<MonsterBase>();

            // 确保 Boss AI 组件挂载
            if (bossObj.GetComponent<FallenHeroBossAI>() == null)
            {
                bossObj.AddComponent<FallenHeroBossAI>();
            }

            // 确保有状态效果管理器
            if (bossObj.GetComponent<Combat.StatusEffectManager>() == null)
            {
                bossObj.AddComponent<Combat.StatusEffectManager>();
            }

            monster.Initialize(distanceFactor, floorNumber, false, 1f);
            ActiveMonsters.Add(monster);

            Debug.Log($"[MonsterSpawner] Boss '{bossData.entityName}' 已生成！");
        }

        /// <summary>
        /// 创建单个怪物
        /// </summary>
        private void SpawnMonster(MonsterData_SO data, Vector3 position, float distanceFactor, int floor, bool isElite, float eliteMult)
        {
            GameObject monsterObj;
            if (monsterPrefab != null)
            {
                monsterObj = Instantiate(monsterPrefab, position, Quaternion.identity);
            }
            else
            {
                monsterObj = new GameObject($"Monster_{data.entityName}");
                monsterObj.transform.position = position;
            }

            var monster = monsterObj.GetComponent<MonsterBase>();
            if (monster == null) monster = monsterObj.AddComponent<MonsterBase>();

            // 确保有状态效果管理器
            if (monsterObj.GetComponent<Combat.StatusEffectManager>() == null)
            {
                monsterObj.AddComponent<Combat.StatusEffectManager>();
            }

            monster.Initialize(distanceFactor, floor, isElite, eliteMult);
            ActiveMonsters.Add(monster);
        }

        // =====================================================================
        //  清理
        // =====================================================================

        /// <summary>
        /// 清空当前房间所有怪物
        /// </summary>
        public void ClearRoom()
        {
            foreach (var monster in ActiveMonsters)
            {
                if (monster != null && monster.gameObject != null)
                {
                    Destroy(monster.gameObject);
                }
            }
            ActiveMonsters.Clear();
        }

        // =====================================================================
        //  工具
        // =====================================================================

        /// <summary>
        /// 计算怪物生成位置（围绕房间中心散布）
        /// </summary>
        private Vector3 GetSpawnPosition(Vector2Int roomGridPos, int index, int total)
        {
            float roomCenterX = roomGridPos.x * 5f;
            float roomCenterY = roomGridPos.y * 5f;

            // 环形均匀分布
            float angle = (2f * Mathf.PI / total) * index;
            float radius = 1.5f + Random.Range(0f, 1f);

            return new Vector3(
                roomCenterX + Mathf.Cos(angle) * radius,
                roomCenterY + Mathf.Sin(angle) * radius,
                0f
            );
        }
    }
}
