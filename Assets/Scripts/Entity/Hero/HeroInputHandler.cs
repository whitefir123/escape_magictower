// ============================================================================
// 逃离魔塔 - 玩家输入处理器 (HeroInputHandler)
// 负责处理所有玩家的键鼠输入映射。
// 使用 Unity New Input System，所有非移动按键支持自定义。
//
// 移动输入采用「按键栈」机制（参考 Magic Tower Wars 的 inputStack 设计）：
//   - keyDown → 方向入栈，keyUp → 方向出栈
//   - 栈顶 = 当前有效移动方向（最后按下的键优先）
//   - 松开当前方向键后自动回退到上一个仍按住的方向
//
// 来源：DesignDocs/07_UI_and_UX.md 第三节
//       NewProject_AIGuide/Core_Features/01_MVP_Implementation_Guide.md
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EscapeTheTower.Entity.Hero
{
    /// <summary>
    /// 玩家输入处理器 —— 将原始输入转化为抽象的意图驱动信号
    /// 执行顺序优先（-100），确保输入标记在其他脚本的 Update 之前被设置
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class HeroInputHandler : MonoBehaviour
    {
        // === 移动输入（按键栈机制） ===

        /// <summary>
        /// 当前有效移动方向（四方向之一或 zero）。
        /// 基于按键栈，最后按下的方向键拥有最高优先级。
        /// </summary>
        public Vector2Int MoveDirection { get; private set; }

        /// <summary>
        /// 最后一次移动的面朝方向（不移动时保持上一次方向）
        /// 用于技能方向判定。初始默认朝下。
        /// </summary>
        public Vector2Int LastFacingDirection { get; private set; } = Vector2Int.down;

        /// <summary>是否正在移动（有方向键按下）</summary>
        public bool IsMoving => MoveDirection != Vector2Int.zero;

        /// <summary>
        /// [兼容旧接口] WASD 移动方向（已归一化的 Vector2）。
        /// 新代码请使用 MoveDirection。
        /// </summary>
        public Vector2 MoveInput => new Vector2(MoveDirection.x, MoveDirection.y);

        /// <summary>鼠标在世界空间中的位置</summary>
        public Vector3 MouseWorldPosition { get; private set; }

        /// <summary>鼠标右键单击（本帧按下）—— 点击移动 / 远程攻击</summary>
        public bool RightClickPressed { get; private set; }

        /// <summary>鼠标右键持续按住</summary>
        public bool RightClickHeld { get; private set; }

        /// <summary>交互键按下（F 键 / 鼠标左键）</summary>
        public bool InteractPressed { get; private set; }

        /// <summary>技能 1 按下</summary>
        public bool Skill1Pressed { get; private set; }

        /// <summary>技能 2 按下</summary>
        public bool Skill2Pressed { get; private set; }

        /// <summary>大招按下</summary>
        public bool UltimatePressed { get; private set; }

        /// <summary>闪避按下</summary>
        public bool DodgePressed { get; private set; }

        // === 按键栈内部数据 ===
        // 记录当前按住的所有方向键，后入栈的在列表末尾（优先级最高）
        private readonly List<Vector2Int> _inputStack = new();

        // WASD 对应的四个方向
        private static readonly Vector2Int DirUp    = Vector2Int.up;
        private static readonly Vector2Int DirDown  = Vector2Int.down;
        private static readonly Vector2Int DirLeft  = Vector2Int.left;
        private static readonly Vector2Int DirRight = Vector2Int.right;

        // === 内部引用 ===
        private Camera _mainCamera;

        // =====================================================================
        //  生命周期
        // =====================================================================

        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            UpdateMovementInput();
            UpdateActionInput();
            UpdateMouseInput();
        }

        /// <summary>
        /// 每帧末尾清除一次性按键标记
        /// </summary>
        private void LateUpdate()
        {
            RightClickPressed = false;
            InteractPressed = false;
            Skill1Pressed = false;
            Skill2Pressed = false;
            UltimatePressed = false;
            DodgePressed = false;
        }

        // =====================================================================
        //  移动输入（按键栈）
        // =====================================================================

        /// <summary>
        /// 按键栈式移动输入处理：
        /// - wasPressedThisFrame → 方向入栈（确保不重复）
        /// - wasReleasedThisFrame → 方向出栈
        /// - 栈顶元素 = MoveDirection
        /// </summary>
        private void UpdateMovementInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // W 键
            if (kb.wKey.wasPressedThisFrame)   PushDirection(DirUp);
            if (kb.wKey.wasReleasedThisFrame)  PopDirection(DirUp);

            // S 键
            if (kb.sKey.wasPressedThisFrame)   PushDirection(DirDown);
            if (kb.sKey.wasReleasedThisFrame)  PopDirection(DirDown);

            // A 键
            if (kb.aKey.wasPressedThisFrame)   PushDirection(DirLeft);
            if (kb.aKey.wasReleasedThisFrame)  PopDirection(DirLeft);

            // D 键
            if (kb.dKey.wasPressedThisFrame)   PushDirection(DirRight);
            if (kb.dKey.wasReleasedThisFrame)  PopDirection(DirRight);

            // 同时支持方向键
            if (kb.upArrowKey.wasPressedThisFrame)    PushDirection(DirUp);
            if (kb.upArrowKey.wasReleasedThisFrame)   PopDirection(DirUp);
            if (kb.downArrowKey.wasPressedThisFrame)   PushDirection(DirDown);
            if (kb.downArrowKey.wasReleasedThisFrame)  PopDirection(DirDown);
            if (kb.leftArrowKey.wasPressedThisFrame)   PushDirection(DirLeft);
            if (kb.leftArrowKey.wasReleasedThisFrame)  PopDirection(DirLeft);
            if (kb.rightArrowKey.wasPressedThisFrame)  PushDirection(DirRight);
            if (kb.rightArrowKey.wasReleasedThisFrame) PopDirection(DirRight);

            // 从栈顶读取当前有效方向
            MoveDirection = _inputStack.Count > 0 ? _inputStack[^1] : Vector2Int.zero;

            // 记住最后移动方向（用于技能面朝判定）
            if (MoveDirection != Vector2Int.zero)
            {
                LastFacingDirection = MoveDirection;
            }
        }

        /// <summary>方向入栈（防重复）</summary>
        private void PushDirection(Vector2Int dir)
        {
            // 先移除防止重复（WASD 和方向键可能映射到同一 dir）
            _inputStack.Remove(dir);
            _inputStack.Add(dir);
        }

        /// <summary>方向出栈</summary>
        private void PopDirection(Vector2Int dir)
        {
            _inputStack.Remove(dir);
        }

        // =====================================================================
        //  动作输入（技能、交互、闪避）
        // =====================================================================

        private void UpdateActionInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // F 键 交互
            if (kb.fKey.wasPressedThisFrame)
                InteractPressed = true;

            // Q 键 技能1（默认键位，可自定义）
            if (kb.qKey.wasPressedThisFrame)
                Skill1Pressed = true;

            // E 键 技能2
            if (kb.eKey.wasPressedThisFrame)
                Skill2Pressed = true;

            // R 键 大招
            if (kb.rKey.wasPressedThisFrame)
                UltimatePressed = true;

            // Space 键 闪避
            if (kb.spaceKey.wasPressedThisFrame)
                DodgePressed = true;
        }

        // =====================================================================
        //  鼠标输入
        // =====================================================================

        private void UpdateMouseInput()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            // 鼠标世界位置（2D 场景用 z=0 平面投射）
            Vector3 mouseScreenPos = Mouse.current.position.ReadValue();
            mouseScreenPos.z = -_mainCamera.transform.position.z;
            MouseWorldPosition = _mainCamera.ScreenToWorldPoint(mouseScreenPos);

            // 鼠标左键 = 交互（与 F 键等价）
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                InteractPressed = true;
            }

            // 鼠标右键
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                RightClickPressed = true;
            }
            RightClickHeld = Mouse.current.rightButton.isPressed;
        }
    }
}
