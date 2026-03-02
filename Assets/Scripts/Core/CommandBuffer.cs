// ============================================================================
// 逃离魔塔 - 指令缓存队列 (CommandBuffer)
// 战斗中实体的死亡/新增/Buff施加等操作统一写入队列，
// 在 LateUpdate 中批处理执行，防止遍历中修改集合导致的 
// "Collection was modified" 异常。
//
// 来源：DesignDocs/13_Architecture_and_Operations_SLA.md 第三节
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace EscapeTheTower.Core
{
    /// <summary>
    /// 指令接口 —— 所有延迟执行的操作必须实现此接口
    /// </summary>
    public interface ICommand
    {
        /// <summary>执行指令</summary>
        void Execute();
    }

    // =========================================================================
    //  预定义指令类型
    // =========================================================================

    /// <summary>
    /// 销毁实体指令（怪物死亡、投射物消失等）
    /// </summary>
    public class DestroyEntityCommand : ICommand
    {
        private readonly GameObject _target;

        public DestroyEntityCommand(GameObject target)
        {
            _target = target;
        }

        public void Execute()
        {
            if (_target != null)
            {
                Object.Destroy(_target);
            }
        }
    }

    /// <summary>
    /// 生成实体指令（怪物刷新、掉落物实例化等）
    /// </summary>
    public class SpawnEntityCommand : ICommand
    {
        private readonly GameObject _prefab;
        private readonly Vector3 _position;
        private readonly Transform _parent;

        /// <summary>生成后的实例引用（Execute 后可用）</summary>
        public GameObject SpawnedInstance { get; private set; }

        public SpawnEntityCommand(GameObject prefab, Vector3 position, Transform parent = null)
        {
            _prefab = prefab;
            _position = position;
            _parent = parent;
        }

        public void Execute()
        {
            if (_prefab != null)
            {
                SpawnedInstance = Object.Instantiate(_prefab, _position, Quaternion.identity, _parent);
            }
        }
    }

    /// <summary>
    /// 通用委托指令（轻量级一次性操作，无需单独定义类）
    /// </summary>
    public class ActionCommand : ICommand
    {
        private readonly System.Action _action;

        public ActionCommand(System.Action action)
        {
            _action = action;
        }

        public void Execute()
        {
            _action?.Invoke();
        }
    }

    // =========================================================================
    //  CommandBuffer 核心实现
    // =========================================================================

    /// <summary>
    /// 指令缓存队列管理器 —— 挂载到场景中的持久 GameObject 上
    /// 在 LateUpdate 中统一处理所有延迟指令
    /// </summary>
    public class CommandBuffer : MonoBehaviour
    {
        private readonly Queue<ICommand> _commandQueue = new Queue<ICommand>();

        /// <summary>当前队列中待处理的指令数量</summary>
        public int PendingCount => _commandQueue.Count;

        /// <summary>
        /// 将指令加入缓存队列（可在 Update/FixedUpdate 中任意时刻调用）
        /// </summary>
        public void Enqueue(ICommand command)
        {
            if (command == null)
            {
                Debug.LogWarning("[CommandBuffer] 尝试入队一个 null 指令，已跳过。");
                return;
            }
            _commandQueue.Enqueue(command);
        }

        /// <summary>
        /// LateUpdate 统一执行所有缓存的指令
        /// 采用安全边界：单帧最多处理一定数量的指令，防止极端情况下卡帧
        /// </summary>
        private void LateUpdate()
        {
            // 安全阀：单帧最多处理 256 条指令，防止意外的无限入队
            int safetyCounter = 256;
            while (_commandQueue.Count > 0 && safetyCounter > 0)
            {
                var command = _commandQueue.Dequeue();
                try
                {
                    command.Execute();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError(
                        $"[CommandBuffer 异常] 执行指令 {command.GetType().Name} 时发生异常：" +
                        $"{ex.Message}\n{ex.StackTrace}");
                }
                safetyCounter--;
            }

            if (_commandQueue.Count > 0)
            {
                Debug.LogWarning(
                    $"[CommandBuffer 安全阀] 单帧指令处理已达上限，剩余 {_commandQueue.Count} 条指令将在下一帧继续执行。");
            }
        }

        /// <summary>
        /// 清空所有待处理的指令（场景切换或紧急重置时使用）
        /// </summary>
        public void ClearAll()
        {
            _commandQueue.Clear();
            Debug.Log("[CommandBuffer] 指令队列已清空。");
        }
    }
}
