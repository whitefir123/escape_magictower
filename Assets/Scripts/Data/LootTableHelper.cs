// ============================================================================
// 逃离魔塔 - 掉落表辅助工具 (LootTableHelper)
// 集中管理宝箱和怪物的掉落计算逻辑。
// 所有概率数据严格来自 GameData_Blueprints/09_Consumable_and_Drop_Items.md
//
// 职责：
//   - 按品质权重表 Roll 消耗品品质
//   - 按门等级/怪物类型生成奖励掉落物
//   - 在指定位置附近空格生成 PickupItem 实体
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Map;
using EscapeTheTower.Equipment;

namespace EscapeTheTower.Data
{
    /// <summary>
    /// 掉落表辅助工具 —— 宝箱/怪物掉落的统一入口
    /// </summary>
    public static class LootTableHelper
    {
        // === 外部注入配置（由 GameBootstrap 或场景初始化器设置） ===
        /// <summary>词缀数据库引用（必须在掉落前设置）</summary>
        public static AffixDatabase_SO AffixDB { get; set; }
        /// <summary>当前楼层深度（影响装备 iPwr）</summary>
        public static int CurrentFloorLevel { get; set; } = 1;
        /// <summary>玩家当前寻宝率（影响装备品质判定）</summary>
        public static float PlayerMagicFind { get; set; } = 0f;
        // =====================================================================
        //  宝箱奖励生成（来源：09_Consumable_and_Drop_Items.md §三）
        // =====================================================================

        /// <summary>
        /// 生成宝箱奖励并在指定位置附近创建 PickupItem 实体
        /// </summary>
        /// <param name="chestPos">宝箱位置</param>
        /// <param name="doorTier">房间门等级（None=无门房间）</param>
        /// <param name="isBossRoom">是否 Boss 房</param>
        /// <param name="isCorridorChest">是否路途宝箱</param>
        /// <param name="rng">随机数生成器</param>
        public static void GenerateChestReward(
            Vector2Int chestPos,
            DoorTier doorTier,
            bool isBossRoom,
            bool isCorridorChest,
            System.Random rng)
        {
            // === 路途宝箱：简化奖励 ===
            if (isCorridorChest)
            {
                GenerateCorridorChestReward(chestPos, rng);
                return;
            }

            // === 房间宝箱（按门等级查表） ===
            int consumableMin, consumableMax, goldMin, goldMax;
            float equipDropRate;
            float[] consumableWeights;

            if (isBossRoom)
            {
                // Boss 房：必出高品消耗品3~4个 + 金币(80~150) + 装备100%(1~2件)
                consumableMin = 3; consumableMax = 4;
                goldMin = 80; goldMax = 150;
                equipDropRate = 1.0f;
                consumableWeights = new[] { 0f, 0f, 0f, 0.25f, 0.40f, 0.25f, 0.10f };
            }
            else
            {
                switch (doorTier)
                {
                    case DoorTier.Gold:
                        // 金门：3~4消耗品 + 金币(40~80) + 装备100%
                        consumableMin = 3; consumableMax = 4;
                        goldMin = 40; goldMax = 80;
                        equipDropRate = 1.0f;
                        consumableWeights = new[] { 0f, 0f, 0.15f, 0.40f, 0.30f, 0.15f, 0f };
                        break;
                    case DoorTier.Silver:
                        // 银门：2~3消耗品 + 金币(20~40) + 装备100%
                        consumableMin = 2; consumableMax = 3;
                        goldMin = 20; goldMax = 40;
                        equipDropRate = 1.0f;
                        consumableWeights = new[] { 0f, 0.20f, 0.40f, 0.30f, 0.10f, 0f, 0f };
                        break;
                    case DoorTier.Bronze:
                        // 铜门：1~2消耗品 + 金币(10~25) + 装备65%
                        consumableMin = 1; consumableMax = 2;
                        goldMin = 10; goldMax = 25;
                        equipDropRate = 0.65f;
                        consumableWeights = new[] { 0.20f, 0.45f, 0.30f, 0.05f, 0f, 0f, 0f };
                        break;
                    default: // 无门
                        // 无门：1~2消耗品 + 金币(5~10) + 装备45%
                        consumableMin = 1; consumableMax = 2;
                        goldMin = 5; goldMax = 10;
                        equipDropRate = 0.45f;
                        consumableWeights = new[] { 0.55f, 0.30f, 0.15f, 0f, 0f, 0f, 0f };
                        break;
                }
            }

            var drops = new List<PickupSpawnData>();

            // 消耗品（血瓶或法力瓶，50/50）
            int consumableCount = rng.Next(consumableMin, consumableMax + 1);
            for (int i = 0; i < consumableCount; i++)
            {
                bool isHealth = rng.NextDouble() < 0.5;
                var quality = RollQuality(consumableWeights, rng);
                int value = isHealth
                    ? PickupItem.GetPotionHealAmount(quality)
                    : PickupItem.GetPotionManaAmount(quality);

                drops.Add(new PickupSpawnData
                {
                    Type = isHealth ? PickupType.HealthPotion : PickupType.ManaPotion,
                    Quality = quality,
                    Value = value,
                });
            }

            // 金币
            int gold = rng.Next(goldMin, goldMax + 1);
            drops.Add(new PickupSpawnData
            {
                Type = PickupType.GoldPile,
                Quality = QualityTier.White,
                Value = gold,
            });

            // 装备掉落（接入 EquipmentGenerator）
            if (rng.NextDouble() < equipDropRate)
            {
                var equipWeights = GetChestEquipWeights(doorTier, isBossRoom);
                var equipData = TryGenerateEquipment(
                    EquipmentGenerator.MonsterTierForEquip.Chest, equipWeights, rng);
                if (equipData != null)
                {
                    drops.Add(new PickupSpawnData
                    {
                        Type = PickupType.Equipment,
                        Quality = equipData.quality,
                        Value = equipData.itemPower,
                        EquipData = equipData,
                    });
                }
            }

            // Boss 房可能掉 2 件装备
            if (isBossRoom && rng.NextDouble() < 0.5)
            {
                var equipData = TryGenerateEquipment(
                    EquipmentGenerator.MonsterTierForEquip.Boss,
                    EquipmentGenerator.BossChestEquipWeights, rng);
                if (equipData != null)
                {
                    drops.Add(new PickupSpawnData
                    {
                        Type = PickupType.Equipment,
                        Quality = equipData.quality,
                        Value = equipData.itemPower,
                        EquipData = equipData,
                    });
                }
            }

            // 在宝箱周围空位生成掉落物
            SpawnDropsNearPosition(chestPos, drops, rng);
        }

        /// <summary>路途宝箱奖励（品质偏低，无风险）</summary>
        private static void GenerateCorridorChestReward(Vector2Int chestPos, System.Random rng)
        {
            var drops = new List<PickupSpawnData>();

            // 1 个蓝品及以下消耗品
            var corridorWeights = new[] { 0.40f, 0.40f, 0.20f, 0f, 0f, 0f, 0f };
            bool isHealth = rng.NextDouble() < 0.5;
            var quality = RollQuality(corridorWeights, rng);
            int value = isHealth
                ? PickupItem.GetPotionHealAmount(quality)
                : PickupItem.GetPotionManaAmount(quality);

            drops.Add(new PickupSpawnData
            {
                Type = isHealth ? PickupType.HealthPotion : PickupType.ManaPotion,
                Quality = quality,
                Value = value,
            });

            // 少量金币
            int gold = rng.Next(3, 11);
            drops.Add(new PickupSpawnData
            {
                Type = PickupType.GoldPile,
                Quality = QualityTier.White,
                Value = gold,
            });

            // 20% 装备掉落
            if (rng.NextDouble() < 0.20)
            {
                var equipData = TryGenerateEquipment(
                    EquipmentGenerator.MonsterTierForEquip.Chest,
                    EquipmentGenerator.CorridorChestEquipWeights, rng);
                if (equipData != null)
                {
                    drops.Add(new PickupSpawnData
                    {
                        Type = PickupType.Equipment,
                        Quality = equipData.quality,
                        Value = equipData.itemPower,
                        EquipData = equipData,
                    });
                }
            }

            SpawnDropsNearPosition(chestPos, drops, rng);
        }

        // =====================================================================
        //  怪物掉落生成（来源：09_Consumable_and_Drop_Items.md §四）
        // =====================================================================

        /// <summary>怪物类型枚举（用于掉落计算）</summary>
        public enum MonsterDropTier
        {
            Normal,
            Elite,
            Boss,
        }

        /// <summary>
        /// 生成怪物死亡掉落
        /// </summary>
        public static void GenerateMonsterDrop(
            Vector2Int deathPos,
            MonsterDropTier tier,
            System.Random rng)
        {
            var drops = new List<PickupSpawnData>();

            // === 消耗品掉落 ===
            float consumableRate;
            float[] consumableWeights;
            switch (tier)
            {
                case MonsterDropTier.Boss:
                    consumableRate = 1.0f;
                    consumableWeights = new[] { 0f, 0f, 0f, 0.30f, 0.40f, 0.20f, 0.10f };
                    break;
                case MonsterDropTier.Elite:
                    consumableRate = 0.50f;
                    consumableWeights = new[] { 0f, 0.30f, 0.40f, 0.25f, 0.05f, 0f, 0f };
                    break;
                default: // Normal
                    consumableRate = 0.35f;
                    consumableWeights = new[] { 0.55f, 0.40f, 0.05f, 0f, 0f, 0f, 0f };
                    break;
            }

            if (rng.NextDouble() < consumableRate)
            {
                bool isHealth = rng.NextDouble() < 0.6; // 血瓶概率略高
                var quality = RollQuality(consumableWeights, rng);
                int value = isHealth
                    ? PickupItem.GetPotionHealAmount(quality)
                    : PickupItem.GetPotionManaAmount(quality);

                drops.Add(new PickupSpawnData
                {
                    Type = isHealth ? PickupType.HealthPotion : PickupType.ManaPotion,
                    Quality = quality,
                    Value = value,
                });
            }

            // === 钥匙掉落 ===
            float keyRate;
            switch (tier)
            {
                case MonsterDropTier.Boss:
                    keyRate = 1.0f;
                    break;
                case MonsterDropTier.Elite:
                    keyRate = 0.70f;
                    break;
                default:
                    keyRate = 0.35f;
                    break;
            }

            if (rng.NextDouble() < keyRate)
            {
                PickupType keyType;
                if (tier == MonsterDropTier.Boss)
                {
                    // Boss 必掉钥匙（铜/银/金随机，偏向铜）
                    float kr = (float)rng.NextDouble();
                    keyType = kr < 0.5f ? PickupType.KeyBronze
                            : kr < 0.85f ? PickupType.KeySilver
                            : PickupType.KeyGold;
                }
                else if (tier == MonsterDropTier.Elite)
                {
                    // 精英怪钥匙：铜50% 银40% 金10%
                    float kr = (float)rng.NextDouble();
                    keyType = kr < 0.5f ? PickupType.KeyBronze
                            : kr < 0.9f ? PickupType.KeySilver
                            : PickupType.KeyGold;
                }
                else
                {
                    // 普通怪仅掉铜钥匙
                    keyType = PickupType.KeyBronze;
                }

                drops.Add(new PickupSpawnData
                {
                    Type = keyType,
                    Quality = QualityTier.White,
                    Value = 1,
                });
            }

            // === 装备掉落 ===
            float equipRate;
            float[] equipWeights;
            switch (tier)
            {
                case MonsterDropTier.Boss:
                    equipRate = 1.0f;
                    equipWeights = EquipmentGenerator.BossEquipWeights;
                    break;
                case MonsterDropTier.Elite:
                    equipRate = 1.0f;
                    equipWeights = EquipmentGenerator.EliteMonsterEquipWeights;
                    break;
                default:
                    equipRate = 0.30f;
                    equipWeights = EquipmentGenerator.NormalMonsterEquipWeights;
                    break;
            }

            var monsterEquipTier = tier switch
            {
                MonsterDropTier.Boss => EquipmentGenerator.MonsterTierForEquip.Boss,
                MonsterDropTier.Elite => EquipmentGenerator.MonsterTierForEquip.Elite,
                _ => EquipmentGenerator.MonsterTierForEquip.Normal,
            };

            if (rng.NextDouble() < equipRate)
            {
                var equipData = TryGenerateEquipment(monsterEquipTier, equipWeights, rng);
                if (equipData != null)
                {
                    drops.Add(new PickupSpawnData
                    {
                        Type = PickupType.Equipment,
                        Quality = equipData.quality,
                        Value = equipData.itemPower,
                        EquipData = equipData,
                    });
                }
            }

            // Boss 可能掉 2 件
            if (tier == MonsterDropTier.Boss && rng.NextDouble() < 0.5)
            {
                var equipData = TryGenerateEquipment(
                    EquipmentGenerator.MonsterTierForEquip.Boss,
                    EquipmentGenerator.BossEquipWeights, rng);
                if (equipData != null)
                {
                    drops.Add(new PickupSpawnData
                    {
                        Type = PickupType.Equipment,
                        Quality = equipData.quality,
                        Value = equipData.itemPower,
                        EquipData = equipData,
                    });
                }
            }

            if (drops.Count > 0)
            {
                SpawnDropsNearPosition(deathPos, drops, rng);
            }
        }

        // =====================================================================
        //  装备生成辅助
        // =====================================================================

        /// <summary>
        /// 尝试生成一件装备（统一入口，各处掉落调用此方法）
        /// 若 AffixDB 未设置则记录警告并返回 null
        /// </summary>
        private static EquipmentData TryGenerateEquipment(
            EquipmentGenerator.MonsterTierForEquip tier,
            float[] qualityWeights,
            System.Random rng)
        {
            if (AffixDB == null)
            {
                Debug.LogWarning("[LootTable] AffixDatabase_SO 未设置，装备生成跳过");
                return null;
            }

            return EquipmentGenerator.Generate(
                CurrentFloorLevel,
                tier,
                qualityWeights,
                PlayerMagicFind,
                AffixDB,
                rng);
        }

        /// <summary>
        /// 根据宝箱门等级和是否Boss房获取装备品质权重表
        /// </summary>
        private static float[] GetChestEquipWeights(DoorTier doorTier, bool isBossRoom)
        {
            if (isBossRoom) return EquipmentGenerator.BossChestEquipWeights;
            return doorTier switch
            {
                DoorTier.Gold => EquipmentGenerator.GoldChestEquipWeights,
                DoorTier.Silver => EquipmentGenerator.SilverChestEquipWeights,
                DoorTier.Bronze => EquipmentGenerator.BronzeChestEquipWeights,
                _ => EquipmentGenerator.NoDoorChestEquipWeights,
            };
        }

        // =====================================================================
        //  通用工具
        // =====================================================================
        private static QualityTier RollQuality(float[] weights, System.Random rng)
        {
            float roll = (float)rng.NextDouble();
            float cumulative = 0f;

            QualityTier[] tiers =
            {
                QualityTier.White,
                QualityTier.Green,
                QualityTier.Blue,
                QualityTier.Purple,
                QualityTier.Yellow,
                QualityTier.Red,
                QualityTier.Rainbow,
            };

            for (int i = 0; i < weights.Length && i < tiers.Length; i++)
            {
                cumulative += weights[i];
                if (roll < cumulative) return tiers[i];
            }

            // 兜底：返回最后一个有权重的品质
            return tiers[Mathf.Min(weights.Length - 1, tiers.Length - 1)];
        }

        /// <summary>
        /// 在指定位置附近的空格生成 PickupItem 实体
        /// </summary>
        private static void SpawnDropsNearPosition(
            Vector2Int center,
            List<PickupSpawnData> drops,
            System.Random rng)
        {
            // 获取楼层数据用于判断可行走格子
            var transitionMgr = FloorTransitionManager.Instance;
            var grid = transitionMgr?.CurrentFloorGrid;

            // 收集 center 附近 3×3 范围内的可行走空格
            var emptySlots = new List<Vector2Int>();
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue; // 跳过中心位置
                    var pos = new Vector2Int(center.x + dx, center.y + dy);

                    // 检查是否可行走（非墙壁、非门）
                    if (grid != null && grid.InBounds(pos.x, pos.y))
                    {
                        var tileType = grid.Tiles[pos.x, pos.y];
                        if (tileType == TileType.Wall ||
                            DoorInteraction.IsDoorTile(tileType))
                            continue; // 跳过墙壁和门
                    }

                    emptySlots.Add(pos);
                }
            }

            // 如果没有足够空位，使用中心位置（怪物/宝箱原位必定可行走）
            if (emptySlots.Count == 0) emptySlots.Add(center);

            // 洗牌
            for (int i = emptySlots.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (emptySlots[i], emptySlots[j]) = (emptySlots[j], emptySlots[i]);
            }

            var pickupMgr = PickupManager.Instance;
            if (pickupMgr == null)
            {
                Debug.LogWarning("[LootTable] PickupManager 不存在，跳过掉落生成");
                return;
            }

            for (int i = 0; i < drops.Count; i++)
            {
                var data = drops[i];
                var pos = emptySlots[i % emptySlots.Count]; // 循环使用空位

                // 如果该位置已有拾取物，尝试中心位置
                if (pickupMgr.GetItemAt(pos) != null)
                    pos = center;

                data.Position = pos;

                // 创建 PickupItem 实体
                var obj = new GameObject($"Drop_{data.Type}_{pos.x}_{pos.y}");
                obj.transform.position = new Vector3(pos.x, pos.y, 0f);

                var pickup = obj.AddComponent<PickupItem>();

                // 装备类型使用带 EquipmentData 的初始化重载
                if (data.Type == PickupType.Equipment && data.EquipData != null)
                {
                    pickup.InitializeEquipment(data.EquipData, pos);
                }
                else
                {
                    pickup.Initialize(data.Type, data.Quality, data.Value, pos);
                }

                pickupMgr.Register(pos, pickup);
            }

            Debug.Log($"[掉落] 在 ({center.x},{center.y}) 附近生成 {drops.Count} 个掉落物");
        }
    }
}
