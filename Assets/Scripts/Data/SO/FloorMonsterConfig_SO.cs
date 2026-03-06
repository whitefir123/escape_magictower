// ============================================================================
// 逃离魔塔 - 楼层怪物配置 SO (FloorMonsterConfig_SO)
// 持有某一楼层的全部怪物 SO 引用，供 FloorTransitionManager 使用。
// 替代原先 Floor1MonsterRegistry 的程序化创建方式，实现 Inspector 可视化编辑。
//
// 用法：Assets/Data/Monsters/ 下创建资产，拖入对应楼层的怪物 SO 引用。
// ============================================================================

using UnityEngine;

namespace EscapeTheTower.Data
{
    /// <summary>
    /// 楼层怪物配置 —— 持有该楼层全部怪物 SO 引用
    /// </summary>
    [CreateAssetMenu(fileName = "NewFloorMonsterConfig", menuName = "EscapeTheTower/Data/FloorMonsterConfig")]
    public class FloorMonsterConfig_SO : ScriptableObject
    {
        [Header("=== 普通怪物池 ===")]
        [Tooltip("该楼层可用的普通怪物数据（Inspector 拖拽配置）")]
        public MonsterData_SO[] normalMonsters;

        [Header("=== Boss ===")]
        [Tooltip("该楼层的 Boss 数据")]
        public MonsterData_SO bossData;
    }
}
