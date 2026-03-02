// ============================================================================
// 逃离魔塔 - 符文数据 ScriptableObject
// 符文的数据配置模板，涵盖属性符文与机制符文两大类。
//
// 来源：DesignDocs/05_Runes_and_MetaProgression.md
//       GameData_Blueprints/08_Destiny_Rune_System.md
// ============================================================================

using UnityEngine;

namespace EscapeTheTower.Data
{
    /// <summary>
    /// 符文数据 SO —— 定义单个符文的全部配置参数
    /// </summary>
    [CreateAssetMenu(fileName = "NewRuneData", menuName = "EscapeTheTower/Data/RuneData")]
    public class RuneData_SO : ScriptableObject
    {
        [Header("=== 基础信息 ===")]
        [Tooltip("符文名称")]
        public string runeName;

        [Tooltip("符文英文标识")]
        public string runeID;

        [Tooltip("符文描述")]
        [TextArea(2, 5)]
        public string description;

        [Tooltip("符文稀有度")]
        public RuneRarity rarity;

        [Tooltip("获取途径")]
        public RuneAcquisitionType acquisitionType;

        [Header("=== 属性符文配置（KillDrop 类型）===")]
        [Tooltip("属性符文加成的属性类型")]
        public StatType statBoostType;

        [Tooltip("属性符文单次加成数值")]
        public float statBoostAmount;

        [Header("=== 机制符文配置（LevelUp 类型）===")]
        [Tooltip("是否属于职业专属池")]
        public bool isClassSpecific;

        [Tooltip("限定的职业类型（仅 isClassSpecific=true 时有效）")]
        public HeroClass restrictedClass;

        [Tooltip("机制符文的效果标签描述")]
        public string effectTag;

        [Header("=== 重复获取升级 ===")]
        [Tooltip("当前等级（运行时叠加，初始为 1）")]
        public int currentLevel = 1;

        [Tooltip("每次重复获取时的升级描述（策划填入增量规则文本）")]
        [TextArea(1, 3)]
        public string upgradeDescription;
    }
}
