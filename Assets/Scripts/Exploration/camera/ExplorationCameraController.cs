using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 探索跟随相机。
    /// 视野大小：默认按「角色左右各 visibleCellsPerSide 格」反算，勾选 useViewHeight 后按上下高度 viewHeight 设定；
    /// 横向跟随角色并在到达地图边缘时夹紧；
    /// 纵向按姿势偏移：站立对准角色所在排，匍匐下移看桌底，上桌上移看货架。
    /// </summary>
    public sealed class ExplorationCameraController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("必填。要驱动的相机，留空会自动取本物体上的 Camera")]
        private Camera controlledCamera;

        [SerializeField]
        [Tooltip("必填。指向场景里 Map 物体上的 ExplorationMap，用来取格间距与地图左右边界")]
        private ExplorationMap map;

        [SerializeField]
        [Tooltip("必填。指向 Player 上的 ExplorationPlayerController，用来跟随位置并读取当前姿势")]
        private ExplorationPlayerController player;

        [SerializeField, Min(0)]
        [Tooltip("视野宽度：角色左右各显示几格，一屏格数 = 该值×2+1。只在未勾选 useViewHeight 时生效")]
        private int visibleCellsPerSide = 1;

        [SerializeField]
        [Tooltip("勾选后不再按左右格数反算，改为直接用 viewHeight 指定上下视野高度；此时横向格数会随窗口宽高比变化")]
        private bool useViewHeight;

        [SerializeField, Min(0.01f)]
        [Tooltip("上下可视高度（世界单位），只在勾选 useViewHeight 后生效。调大=上下看到更多内容")]
        private float viewHeight = 2.7f;

        [SerializeField]
        [Tooltip("站立时相机中心的高度 = groundY + 该值。填负值就是整体往下看")]
        private float standingViewHeight = 0.5f;

        [SerializeField]
        [Tooltip("匍匐时在站立高度上再偏移多少。负值=相机下移，看到桌子底下")]
        private float proneViewOffset = -0.9f;

        [SerializeField]
        [Tooltip("上桌时在站立高度上再偏移多少。正值=相机上移，看到顶部货架")]
        private float onTableViewOffset = 0.9f;

        [SerializeField, Min(0f)]
        [Tooltip("横向跟随的平滑时间（秒），越大跟随越滞后。填 0 = 严格居中、无延迟")]
        private float followSmoothTime = 0.1f;

        [SerializeField, Min(0f)]
        [Tooltip("纵向姿势切换的平滑时间（秒），越大上下移动越慢越柔")]
        private float postureSmoothTime = 0.15f;

        private float horizontalVelocity;
        private float verticalVelocity;

        /// <summary>当前实际的半屏宽（世界单位 = 正交 size × 相机宽高比）。</summary>
        public float HalfViewWidth => controlledCamera != null
            ? controlledCamera.orthographicSize * (controlledCamera.aspect > 0f ? controlledCamera.aspect : 1f)
            : 0f;

        private void Awake()
        {
            controlledCamera ??= GetComponent<Camera>();
            if (controlledCamera == null || map == null || player == null)
            {
                Debug.LogError("[Exploration] ExplorationCameraController 缺少 Camera / Map / Player 引用。", this);
                enabled = false;
                return;
            }

            controlledCamera.orthographic = true;
            ApplyViewSize();

            // 开局直接对齐，避免第一帧从默认位置飘过去
            Vector3 start = transform.position;
            start.x = ClampCameraX(player.transform.position.x);
            start.y = ResolveTargetY();
            transform.position = start;
        }

        private void LateUpdate()
        {
            ApplyViewSize();

            float deltaTime = Mathf.Max(0.0001f, Time.deltaTime);
            Vector3 current = transform.position;
            float targetX = ClampCameraX(player.transform.position.x);
            float targetY = ResolveTargetY();

            float x = Mathf.SmoothDamp(current.x, targetX, ref horizontalVelocity,
                followSmoothTime, Mathf.Infinity, deltaTime);
            float y = Mathf.SmoothDamp(current.y, targetY, ref verticalVelocity,
                postureSmoothTime, Mathf.Infinity, deltaTime);
            transform.position = new Vector3(x, y, current.z);
        }

        /// <summary>
        /// 设定正交 size（半高）。
        /// 正交相机下「上下可视高度」与「左右可视宽度」被宽高比绑定，只能定其中一个：
        /// 默认按左右格数反算（保证横向格数），勾选 useViewHeight 后直接按 viewHeight 定上下高度，
        /// 此时横向格数会随宽高比变化。
        /// </summary>
        private void ApplyViewSize()
        {
            if (useViewHeight)
            {
                controlledCamera.orthographicSize = Mathf.Max(0.01f, viewHeight) * 0.5f;
                return;
            }

            float aspect = controlledCamera.aspect > 0f ? controlledCamera.aspect : 1f;
            float halfWidth = (visibleCellsPerSide + 0.5f) * map.CellSpacing;
            controlledCamera.orthographicSize = halfWidth / aspect;
        }

        /// <summary>角色 X 夹紧到「地图边界内缩半个屏宽」的范围；地图比一屏还窄时锁在地图中心。</summary>
        private float ClampCameraX(float playerX)
        {
            float halfWidth = HalfViewWidth;
            float minimum = map.MapMinX + halfWidth;
            float maximum = map.MapMaxX - halfWidth;
            if (minimum > maximum)
            {
                return (map.MapMinX + map.MapMaxX) * 0.5f;
            }

            return Mathf.Clamp(playerX, minimum, maximum);
        }

        /// <summary>纵向目标：地图高度 + 站立观察高度 + 当前姿势的偏移。</summary>
        private float ResolveTargetY()
        {
            float offset = player.Posture switch
            {
                ExplorationPosture.Prone => proneViewOffset,
                ExplorationPosture.OnTable => onTableViewOffset,
                _ => 0f
            };
            return map.GroundY + standingViewHeight + offset;
        }
    }
}
