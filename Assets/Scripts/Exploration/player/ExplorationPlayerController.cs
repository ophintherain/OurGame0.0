using System;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 探索阶段的玩家控制。
    /// 一次按键只移动一格：A/左 向左，D/右 向右，W 上桌/下桌，S 匍匐/起立。
    /// moveSpeed 为世界单位/秒（数值越大越快），实际位移按 moveCurve 缓动逐帧推进。
    /// </summary>
    public sealed class ExplorationPlayerController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("必填。指向场景里 Map 物体上的 ExplorationMap；留空会在 Awake 报错并禁用本脚本")]
        private ExplorationMap map;

        [SerializeField, Min(0.01f)]
        [Tooltip("移动速度（世界单位/秒），数值越大走得越快。走一格耗时 = 格间距 ÷ 该值")]
        private float moveSpeed = 4f;

        [SerializeField]
        [Tooltip("单格移动的缓动曲线。横轴=这一格的进度 0~1，纵轴=位移比例；直线=等速，两端切线压平=起步加速/到位减速")]
        private AnimationCurve moveCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [SerializeField]
        [Tooltip("出生格号，会被自动收敛到 0 ~ 格数-1 之间")]
        private int startingColumn;

        [SerializeField]
        [Tooltip("是否响应 A/D/W/S 键盘输入。取消勾选可临时关掉控制（调试用）")]
        private bool controlEnabled = true;

        [SerializeField]
        [Tooltip("站立时的视觉缩放")]
        private Vector3 standingScale = Vector3.one;

        [SerializeField]
        [Tooltip("匍匐时的视觉缩放（压扁）")]
        private Vector3 proneScale = new Vector3(1.25f, 0.5f, 1f);

        [SerializeField]
        [Tooltip("上桌时相对站立位置向上抬高的距离（世界单位）。按「桌面高度 − groundY」来填，填 0 就是原地不抬")]
        private float onTableHeight = 0.6f;

        private Vector3 moveStartPosition;
        private Vector3 targetPosition;
        private float moveDuration;
        private float moveElapsed;

        /// <summary>开始移动（旧格号, 新格号）。每次按键效果在起步这一帧就派发。</summary>
        public event Action<int, int> MoveStarted;

        /// <summary>移动结束（停下的格号）。</summary>
        public event Action<int> MoveCompleted;

        /// <summary>姿势变化（旧姿势, 新姿势）。</summary>
        public event Action<ExplorationPosture, ExplorationPosture> PostureChanged;

        /// <summary>当前所在格号。</summary>
        public int CurrentColumn { get; private set; }

        /// <summary>是否正在做位移推进。</summary>
        public bool IsMoving { get; private set; }

        /// <summary>当前姿势。</summary>
        public ExplorationPosture Posture { get; private set; } = ExplorationPosture.Standing;

        /// <summary>移动速度（世界单位/秒），只读。</summary>
        public float MoveSpeed => moveSpeed;

        private void Awake()
        {
            if (map == null)
            {
                Debug.LogError("[Exploration] ExplorationPlayerController 没有配置 ExplorationMap。", this);
                enabled = false;
                return;
            }

            CurrentColumn = map.ClampColumn(startingColumn);
            targetPosition = map.GetWorldPosition(CurrentColumn);
            transform.position = targetPosition;
            ApplyPose();
        }

        private void Update()
        {
            AdvanceMovement(Time.deltaTime);

            if (controlEnabled)
            {
                ReadKeyboard();
            }
        }

        /// <summary>开关键盘控制（锁输入用）。</summary>
        public void SetControlEnabled(bool value)
        {
            controlEnabled = value;
        }

        /// <summary>向 direction（+1 向右 / -1 向左）移动一格。边界、移动中、方向为 0 时返回 false。</summary>
        public bool TryMove(int direction)
        {
            if (map == null || direction == 0 || IsMoving)
            {
                return false;
            }

            int next = map.ClampColumn(CurrentColumn + Math.Sign(direction));
            if (next == CurrentColumn)
            {
                return false;
            }

            // 上桌状态下移动等于自动下桌并恢复站立
            SetPosture(ExplorationPosture.Standing);

            int previous = CurrentColumn;
            CurrentColumn = next;
            targetPosition = map.GetWorldPosition(CurrentColumn);
            moveStartPosition = transform.position;
            moveElapsed = 0f;
            moveDuration = Mathf.Max(0.0001f, map.CellSpacing / Mathf.Max(0.0001f, moveSpeed));
            IsMoving = true;

            MoveStarted?.Invoke(previous, CurrentColumn);
            return true;
        }

        /// <summary>切换到指定姿势。上桌只允许在桌子格，否则返回 false。</summary>
        public bool TrySetPosture(ExplorationPosture next)
        {
            if (map == null || next == Posture)
            {
                return false;
            }

            if (next == ExplorationPosture.OnTable && !map.IsTableColumn(CurrentColumn))
            {
                return false;
            }

            SetPosture(next);
            return true;
        }

        private void ReadKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                TryMove(-1);
            }

            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                TryMove(1);
            }

            if (Input.GetKeyDown(KeyCode.W))
            {
                TryChangePostureByKey(ExplorationPosture.OnTable);
            }

            if (Input.GetKeyDown(KeyCode.S))
            {
                TryChangePostureByKey(ExplorationPosture.Prone);
            }
        }

        /// <summary>
        /// W / S 的姿势规则：
        /// 站立时按 W 上桌（需桌子格）、按 S 匍匐；
        /// 匍匐时按 W 恢复站立，按 S 无操作；
        /// 上桌时按 S 下桌恢复站立，按 W 无操作。
        /// </summary>
        private void TryChangePostureByKey(ExplorationPosture target)
        {
            // 已经在目标姿势（桌上按 W / 匍匐按 S）：无操作
            if (Posture == target)
            {
                return;
            }

            // 当前是另一种姿势（匍匐按 W / 桌上按 S）：恢复站立
            if (Posture != ExplorationPosture.Standing)
            {
                TrySetPosture(ExplorationPosture.Standing);
                return;
            }

            // 站立：切到目标姿势
            TrySetPosture(target);
        }

        private void AdvanceMovement(float deltaTime)
        {
            if (!IsMoving)
            {
                return;
            }

            moveElapsed += Mathf.Max(0f, deltaTime);
            float progress = Mathf.Clamp01(moveElapsed / moveDuration);
            float eased = moveCurve != null && moveCurve.length > 0 ? moveCurve.Evaluate(progress) : progress;
            transform.position = Vector3.Lerp(moveStartPosition, targetPosition, eased);

            if (progress >= 1f)
            {
                transform.position = targetPosition;
                IsMoving = false;
                MoveCompleted?.Invoke(CurrentColumn);
            }
        }

        private void SetPosture(ExplorationPosture next)
        {
            if (next == Posture)
            {
                return;
            }

            ExplorationPosture previous = Posture;
            Posture = next;
            ApplyPose();
            PostureChanged?.Invoke(previous, next);
        }

        /// <summary>把姿势应用到表现：匍匐压扁，上桌整体抬高，其余恢复站立外观。</summary>
        private void ApplyPose()
        {
            Vector3 position = transform.position;
            position.y = map.GroundY + (Posture == ExplorationPosture.OnTable ? onTableHeight : 0f);
            transform.position = position;
            transform.localScale = Posture == ExplorationPosture.Prone ? proneScale : standingScale;
        }
    }
}
