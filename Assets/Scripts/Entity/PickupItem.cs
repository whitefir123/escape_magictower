// ============================================================================
// 逃离魔塔 - 拾取物实体 (PickupItem)
// 走廊掉落物（血瓶/法力瓶/钥匙/金币堆）的运行时组件。
// 不使用 GridMovement（静态物品，不参与碰撞注册表）。
//
// 交互方式：HeroController.OnArrivedAtTile → PickupManager.GetItemAt → OnPickedUp
//
// 来源：GameData_Blueprints/09_Consumable_and_Drop_Items.md
// ============================================================================

using UnityEngine;
using EscapeTheTower.Core;
using EscapeTheTower.Map;

namespace EscapeTheTower
{
    /// <summary>
    /// 拾取物实体 —— 走廊掉落物的运行时表示
    /// </summary>
    public class PickupItem : MonoBehaviour
    {
        public PickupType Type { get; private set; }
        public QualityTier Quality { get; private set; }
        public int Value { get; private set; }
        public Vector2Int GridPosition { get; private set; }

        /// <summary>初始化拾取物数据并生成视觉占位</summary>
        public void Initialize(PickupType type, QualityTier quality, int value, Vector2Int pos)
        {
            Type = type;
            Quality = quality;
            Value = value;
            GridPosition = pos;

            EnsureVisual();
        }

        /// <summary>被拾取时调用（由 HeroController 触发）</summary>
        public void OnPickedUp(int pickerEntityID)
        {
            // 广播拾取事件
            EventManager.Publish(new OnItemPickedUpEvent
            {
                Meta = new EventMeta(pickerEntityID),
                ItemType = Type,
                Quality = Quality,
                Position = GridPosition,
                Value = Value,
            });

            string itemName = GetDisplayName();
            string valueStr = Type switch
            {
                PickupType.HealthPotion => $"回复 {Value} HP",
                PickupType.ManaPotion => $"回复 {Value} MP",
                PickupType.GoldPile => $"{Value} 金币",
                _ => "",
            };
            Debug.Log($"[拾取] {itemName} {valueStr}");

            // 从管理器中注销
            PickupManager.Instance?.Unregister(GridPosition);

            // 销毁自身
            Destroy(gameObject);
        }

        /// <summary>获取物品显示名称</summary>
        public string GetDisplayName()
        {
            return Type switch
            {
                PickupType.HealthPotion => GetPotionName(Quality),
                PickupType.ManaPotion => GetManaPotionName(Quality),
                PickupType.KeyBronze => "铜钥匙",
                PickupType.KeySilver => "银钥匙",
                PickupType.KeyGold => "金钥匙",
                PickupType.GoldPile => "金币堆",
                _ => "未知物品",
            };
        }

        // =====================================================================
        //  品质名称映射
        // =====================================================================

        private static string GetPotionName(QualityTier quality)
        {
            return quality switch
            {
                QualityTier.White => "破碎药瓶",
                QualityTier.Green => "小型生命药水",
                QualityTier.Blue => "中型生命药水",
                QualityTier.Purple => "大型生命药水",
                QualityTier.Yellow => "浓缩灵泉",
                QualityTier.Red => "生命精华",
                QualityTier.Rainbow => "永恒圣泉",
                _ => "未知药水",
            };
        }

        private static string GetManaPotionName(QualityTier quality)
        {
            return quality switch
            {
                QualityTier.White => "破碎蓝瓶",
                QualityTier.Green => "小型法力药水",
                QualityTier.Blue => "中型法力药水",
                QualityTier.Purple => "大型法力药水",
                QualityTier.Yellow => "浓缩魔泉",
                QualityTier.Red => "魔力精华",
                QualityTier.Rainbow => "永恒灵泉",
                _ => "未知法力瓶",
            };
        }

        /// <summary>根据品质获取血瓶回复量（固定数值）</summary>
        public static int GetPotionHealAmount(QualityTier quality)
        {
            return quality switch
            {
                QualityTier.White => 20,
                QualityTier.Green => 50,
                QualityTier.Blue => 120,
                QualityTier.Purple => 250,
                QualityTier.Yellow => 500,
                QualityTier.Red => 1000,
                QualityTier.Rainbow => 99999, // 全额回满由使用端 clamp
                _ => 20,
            };
        }

        /// <summary>根据品质获取法力瓶回复量（固定数值）</summary>
        public static int GetPotionManaAmount(QualityTier quality)
        {
            return quality switch
            {
                QualityTier.White => 10,
                QualityTier.Green => 25,
                QualityTier.Blue => 60,
                QualityTier.Purple => 120,
                QualityTier.Yellow => 250,
                QualityTier.Red => 500,
                QualityTier.Rainbow => 99999,
                _ => 10,
            };
        }

        // =====================================================================
        //  视觉占位（不同类型不同颜色的小方块）
        // =====================================================================

        private void EnsureVisual()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();

            // 创建 2x2 像素纹理（比怪物/玩家的 4x4 小，表示可拾取物品）
            var tex = new Texture2D(2, 2);
            Color[] px = new Color[4];
            Color itemColor = GetItemColor();
            for (int i = 0; i < 4; i++) px[i] = itemColor;
            tex.SetPixels(px);
            tex.Apply();
            tex.filterMode = FilterMode.Point;

            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 4f);
            sr.sortingOrder = 5; // 显示在地面之上、实体之下

            // 缩小至 0.6 倍以区分于其他实体
            transform.localScale = Vector3.one * 0.6f;
        }

        private Color GetItemColor()
        {
            return Type switch
            {
                PickupType.HealthPotion => GetQualityColor(Quality, new Color(1f, 0.3f, 0.3f)),
                PickupType.ManaPotion => GetQualityColor(Quality, new Color(0.3f, 0.3f, 1f)),
                PickupType.KeyBronze => new Color(0.72f, 0.45f, 0.20f),  // 铜色
                PickupType.KeySilver => new Color(0.75f, 0.75f, 0.80f),  // 银色
                PickupType.KeyGold => new Color(0.90f, 0.75f, 0.20f),    // 金色
                PickupType.GoldPile => new Color(1.0f, 0.85f, 0.0f),     // 亮金色
                _ => Color.white,
            };
        }

        /// <summary>根据品质微调物品颜色亮度</summary>
        private static Color GetQualityColor(QualityTier quality, Color baseColor)
        {
            float brightness = quality switch
            {
                QualityTier.White => 0.5f,
                QualityTier.Green => 0.65f,
                QualityTier.Blue => 0.8f,
                QualityTier.Purple => 0.9f,
                QualityTier.Yellow => 1.0f,
                QualityTier.Red => 1.0f,
                QualityTier.Rainbow => 1.0f,
                _ => 0.5f,
            };
            return baseColor * brightness;
        }
    }
}
