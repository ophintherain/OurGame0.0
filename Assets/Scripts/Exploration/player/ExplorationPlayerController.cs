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
        [Tooltip("上桌时相对站立位置向上抬高的距离（世界单位）。按「桌面高度 − groundY」来填；填 0 = 不抬，外观完全交给动画")]
        private float onTableHeight = 0.6f;

        [SerializeField]
        [Tooltip("站立状态下显示的物体。可直接拖预制体资源（脚本启动时实例化成子物体）或场景里的物体；留空则该姿势用下面的占位方块")]
        private GameObject standingVisual;

        [SerializeField]
        [Tooltip("匍匐状态下显示的物体。可直接拖预制体资源（脚本启动时实例化成子物体）或场景里的物体；留空则该姿势用下面的占位方块")]
        private GameObject proneVisual;

        [SerializeField]
        [Tooltip("占位方块：当前姿势没有配立绘时显示它，配了就隐藏。留空会自动取 Player 自己身上的 SpriteRenderer")]
        private SpriteRenderer placeholderVisual;

        [SerializeField]
        [Tooltip("站立 / 上桌移动时循环播放的序列帧，按数组顺序播放；留空则保持原有静态立绘")]
        private Sprite[] standingMoveFrames;

        [SerializeField]
        [Tooltip("匍匐移动时循环播放的序列帧，按数组顺序播放；留空则保持原有静态立绘")]
        private Sprite[] proneMoveFrames;

        [SerializeField, Min(1f)]
        [Tooltip("移动动画播放速度（帧/秒），数值越大动画越快；8 约为正常步频")]
        private float moveFramesPerSecond = 8f;

        [SerializeField]
        [Tooltip("匍匐立绘相对角色的位置偏移（世界单位），用于把匍匐图对齐到地面；默认 0 = 不偏移")]
        private Vector2 proneVisualOffset;

        private GameObject standingVisualInstance;
        private GameObject proneVisualInstance;
        private SpriteRenderer standingRenderer;
        private SpriteRenderer proneRenderer;
        private Sprite standingOriginalSprite;
        private Sprite proneOriginalSprite;
        private Vector3 proneOriginalLocalPosition;
        private int frameIndex;
        private float frameElapsed;

        private Vector3 moveStartPosition;
        private Vector3 targetPosition;
        private float moveDuration;
        private float moveElapsed;
        private bool hasPendingPosture;
        private ExplorationPosture pendingPosture;
        private int facing = 1; // 1 = 面向右，-1 = 面向左

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

        /// <summary>当前是否响应控制输入（背包等界面打开时为 false），只读。</summary>
        public bool ControlEnabled => controlEnabled;

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
            placeholderVisual ??= GetComponent<SpriteRenderer>();
            standingVisualInstance = ResolveVisual(standingVisual);
            proneVisualInstance = ResolveVisual(proneVisual);
            standingRenderer = FindRenderer(standingVisualInstance);
            proneRenderer = FindRenderer(proneVisualInstance);
            standingOriginalSprite = standingRenderer != null ? standingRenderer.sprite : null;
            proneOriginalSprite = proneRenderer != null ? proneRenderer.sprite : null;
            proneOriginalLocalPosition = proneVisualInstance != null
                ? proneVisualInstance.transform.localPosition
                : Vector3.zero;
            ApplyPose();

            if (placeholderVisual == null && standingVisualInstance == null && proneVisualInstance == null)
            {
                Debug.LogWarning("[Exploration] 角色没有任何可显示的外观：占位方块与立绘都为空（Player 上也没有 SpriteRenderer）。", this);
            }
        }

        /// <summary>
        /// 把「视觉物体」解析成场景里的实例：拖的是预制体资源就实例化成子物体，
        /// 拖的是场景里的物体就直接复用。返回 null 表示该姿势不做显隐控制。
        /// </summary>
        private GameObject ResolveVisual(GameObject source)
        {
            if (source == null)
            {
                return null;
            }

            if (source.scene.IsValid())
            {
                // 已经是场景里的物体，直接用（位置由它自己决定）
                return source;
            }

            // 预制体资源不属于任何场景（scene.IsValid() 为 false），需要实例化才能出现在场景里。
            // 预制体根节点可能自带位置/朝向（比如由摆好位置的场景物体拖成预制体），这里归零，
            // 保证立绘与角色对齐；预制体内部的子物体偏移不受影响。
            GameObject instance = Instantiate(source, transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            return instance;
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

            SetFacing(direction);

            int next = map.ClampColumn(CurrentColumn + Math.Sign(direction));
            if (next == CurrentColumn)
            {
                return false;
            }

            // 上桌状态下移动等于自动下桌并恢复站立；匍匐移动保持匍匐
            if (Posture == ExplorationPosture.OnTable)
            {
                SetPosture(ExplorationPosture.Standing);
            }

            int previous = CurrentColumn;
            CurrentColumn = next;
            targetPosition = map.GetWorldPosition(CurrentColumn);
            moveStartPosition = transform.position;
            moveElapsed = 0f;
            moveDuration = Mathf.Max(0.0001f, map.CellSpacing / Mathf.Max(0.0001f, moveSpeed));
            IsMoving = true;
            frameIndex = 0;
            frameElapsed = 0f;
            ApplyMoveFrame();

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
            // 记录按键之前的姿势。方向键先处理（移动会自动恢复站立），
            // 所以 W/S 的规则必须按「按键那一刻」的姿势判定，否则同帧 D+S 会被误判成「站立→匍匐」。
            ExplorationPosture postureBeforeInput = Posture;

            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                SetFacing(-1);
                TryMove(-1);
            }

            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                SetFacing(1);
                TryMove(1);
            }

            if (Input.GetKeyDown(KeyCode.W))
            {
                RequestPosture(ExplorationPosture.OnTable, postureBeforeInput);
            }

            if (Input.GetKeyDown(KeyCode.S))
            {
                RequestPosture(ExplorationPosture.Prone, postureBeforeInput);
            }
        }

        /// <summary>
        /// W / S 的姿势规则：已在目标姿势（桌上按 W / 匍匐按 S）无操作；
        /// 处于另一种非站立姿势（匍匐按 W / 桌上按 S）恢复站立；站立时切到目标姿势。
        /// 若这一帧刚按了方向键、正在移动，则先把结果记下来，等走到位那一刻再执行。
        /// </summary>
        private void RequestPosture(ExplorationPosture target, ExplorationPosture postureBeforeInput)
        {
            if (postureBeforeInput == target)
            {
                return;
            }

            ExplorationPosture desired = postureBeforeInput == ExplorationPosture.Standing
                ? target
                : ExplorationPosture.Standing;

            if (IsMoving)
            {
                pendingPosture = desired;
                hasPendingPosture = true;
                return;
            }

            TrySetPosture(desired);
        }

        /// <summary>移动到位后执行排队中的姿势请求。目标格不允许时由 TrySetPosture 拒绝，即自动丢弃。</summary>
        private void ApplyPendingPosture()
        {
            if (!hasPendingPosture)
            {
                return;
            }

            hasPendingPosture = false;
            TrySetPosture(pendingPosture);
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

            AdvanceMoveAnimation(deltaTime);

            if (progress >= 1f)
            {
                transform.position = targetPosition;
                IsMoving = false;
                ApplyPendingPosture();
                ApplyIdleFrame();
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

        /// <summary>
        /// 姿势的表现：上桌时整体抬到桌面高度；按姿势切换立绘显隐。
        /// 当前姿势没有配立绘时显示占位方块，配了立绘就把占位方块隐藏 —— 有立绘用立绘，没立绘用方块。
        /// </summary>
        private void ApplyPose()
        {
            float lift = Posture == ExplorationPosture.OnTable ? onTableHeight : 0f;
            Vector3 position = transform.position;
            position.y = map.GroundY + lift;
            transform.position = position;

            bool isProne = Posture == ExplorationPosture.Prone;
            if (standingVisualInstance != null)
            {
                standingVisualInstance.SetActive(!isProne);
            }

            if (proneVisualInstance != null)
            {
                proneVisualInstance.SetActive(isProne);
            }

            if (placeholderVisual != null)
            {
                placeholderVisual.enabled = isProne ? proneVisualInstance == null : standingVisualInstance == null;
            }

            ApplyVisualOffset(proneVisualInstance, proneOriginalLocalPosition, proneVisualOffset);
            ApplyIdleFrame();
            ApplyFacing();
        }

        private static SpriteRenderer FindRenderer(GameObject visual)
        {
            return visual != null ? visual.GetComponentInChildren<SpriteRenderer>(true) : null;
        }

        private static void ApplyVisualOffset(GameObject visual, Vector3 originalLocalPosition, Vector2 offset)
        {
            if (visual != null)
            {
                visual.transform.localPosition = originalLocalPosition + new Vector3(offset.x, offset.y, 0f);
            }
        }

        /// <summary>停下来时显示当前姿势序列帧的第 1 帧；没有序列帧则还原原始立绘。</summary>
        private void ApplyIdleFrame()
        {
            frameIndex = 0;
            frameElapsed = 0f;

            bool isProne = Posture == ExplorationPosture.Prone;
            SpriteRenderer renderer = isProne ? proneRenderer : standingRenderer;
            if (renderer == null)
            {
                return;
            }

            Sprite[] frames = isProne ? proneMoveFrames : standingMoveFrames;
            renderer.sprite = frames != null && frames.Length > 0 && frames[0] != null
                ? frames[0]
                : (isProne ? proneOriginalSprite : standingOriginalSprite);
        }

        /// <summary>移动中按帧率循环推进序列帧。</summary>
        private void AdvanceMoveAnimation(float deltaTime)
        {
            Sprite[] frames = Posture == ExplorationPosture.Prone ? proneMoveFrames : standingMoveFrames;
            if (frames == null || frames.Length == 0)
            {
                return;
            }

            frameElapsed += Mathf.Max(0f, deltaTime);
            float frameDuration = 1f / Mathf.Max(1f, moveFramesPerSecond);
            int steps = Mathf.FloorToInt(frameElapsed / frameDuration);
            if (steps <= 0)
            {
                return;
            }

            frameElapsed -= steps * frameDuration;
            frameIndex = (frameIndex + steps) % frames.Length;
            ApplyMoveFrame();
        }

        /// <summary>把当前帧写给当前姿势的立绘。</summary>
        private void ApplyMoveFrame()
        {
            bool isProne = Posture == ExplorationPosture.Prone;
            SpriteRenderer renderer = isProne ? proneRenderer : standingRenderer;
            Sprite[] frames = isProne ? proneMoveFrames : standingMoveFrames;
            if (renderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            int index = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
            if (frames[index] != null)
            {
                renderer.sprite = frames[index];
            }
        }

        /// <summary>设置朝向：+1 面向右，-1 面向左。朝向变化时立即刷新表现。</summary>
        private void SetFacing(int direction)
        {
            if (direction == 0)
            {
                return;
            }

            int next = direction > 0 ? 1 : -1;
            if (next == facing)
            {
                return;
            }

            facing = next;
            ApplyFacing();
        }

        /// <summary>
        /// 朝向表现：立绘整体水平镜像，占位方块用 SpriteRenderer.flipX。
        /// 只改立绘自己的 localScale.x 符号，不会改动 Player 本体或格子的坐标。
        /// </summary>
        private void ApplyFacing()
        {
            ApplyFacingTo(standingVisualInstance);
            ApplyFacingTo(proneVisualInstance);

            if (placeholderVisual != null)
            {
                placeholderVisual.flipX = facing < 0;
            }
        }

        private void ApplyFacingTo(GameObject visual)
        {
            if (visual == null)
            {
                return;
            }

            Vector3 scale = visual.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * facing;
            visual.transform.localScale = scale;
        }
    }
}
