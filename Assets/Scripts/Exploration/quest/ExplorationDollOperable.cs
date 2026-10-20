using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 可被 F 键操纵的人偶：场景里未拾取的人偶、以及从背包丢到地上的人偶。
    /// 操纵期间玩家失去本体控制，人偶可用 A/D 或左右方向键移动；再按 F 结束。
    /// </summary>
    public sealed class ExplorationDollOperable : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("第二车厢怪异控制器。留空会自动在场景里查找 ExplorationDollWeirdness")]
        private ExplorationDollWeirdness weirdness;

        [SerializeField]
        [Tooltip("Player 上的 ExplorationPlayerController。留空会自动查找，用于判断距离和控制状态")]
        private ExplorationPlayerController player;

        [SerializeField]
        [Tooltip("场景里的 ExplorationMap，用于限制人偶移动范围。留空会自动查找")]
        private ExplorationMap map;

        [SerializeField, Min(0.1f)]
        [Tooltip("允许按 F 操纵的水平距离（世界单位）。玩家与人偶的 X 距离小于该值才能操作，默认 2")]
        private float interactRange = 2f;

        [SerializeField, Min(0f)]
        [Tooltip("操纵期间人偶的移动速度（世界单位/秒）。数值越大移动越快，0 = 不能移动")]
        private float moveSpeed = 2.5f;

        [SerializeField, Min(0f)]
        [Tooltip("人偶移动时离地图左右边缘的留白（世界单位），防止贴边时半个立绘跑出地图")]
        private float edgePadding = 0.5f;

        private SpriteRenderer spriteRenderer;
        private DroppedItemPickup droppedPickup;
        private bool pickupWasEnabled;

        /// <summary>当前是否正在被操纵。</summary>
        public bool IsOperating { get; private set; }

        /// <summary>人偶物品 ID：2002 / 2003。</summary>
        public static bool IsDollItem(int itemId)
        {
            return itemId == 2002 || itemId == 2003;
        }

        private void Awake()
        {
            if (weirdness == null)
            {
                weirdness = FindObjectOfType<ExplorationDollWeirdness>();
            }

            if (player == null)
            {
                player = FindObjectOfType<ExplorationPlayerController>();
            }

            if (map == null)
            {
                map = FindObjectOfType<ExplorationMap>();
            }

            spriteRenderer = GetComponent<SpriteRenderer>();
            droppedPickup = GetComponent<DroppedItemPickup>();
        }

        private void Update()
        {
            if (weirdness == null || weirdness.IsCompleted)
            {
                return;
            }

            if (IsOperating)
            {
                MoveDoll();

                if (Input.GetKeyDown(KeyCode.F))
                {
                    weirdness.EndOperate();
                }

                return;
            }

            if (!Input.GetKeyDown(KeyCode.F))
            {
                return;
            }

            if (player == null || !player.ControlEnabled || player.IsMoving)
            {
                return;
            }

            if (Mathf.Abs(player.transform.position.x - transform.position.x) > interactRange)
            {
                return;
            }

            weirdness.TryBeginOperate(this);
        }

        /// <summary>由怪异控制器切换操纵状态；开始操纵时禁用点击拾取，结束或失败时恢复。</summary>
        public void SetOperating(bool value)
        {
            if (IsOperating == value)
            {
                return;
            }

            IsOperating = value;

            if (droppedPickup == null)
            {
                return;
            }

            if (value)
            {
                pickupWasEnabled = droppedPickup.enabled;
                droppedPickup.enabled = false;
            }
            else
            {
                droppedPickup.enabled = pickupWasEnabled;
            }
        }

        private void MoveDoll()
        {
            float direction = 0f;

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                direction -= 1f;
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                direction += 1f;
            }

            if (Mathf.Approximately(direction, 0f) || moveSpeed <= 0f)
            {
                return;
            }

            Vector3 position = transform.position;
            position.x += direction * moveSpeed * Time.deltaTime;

            if (map != null)
            {
                float padding = Mathf.Max(0f, edgePadding);
                position.x = Mathf.Clamp(position.x, map.MapMinX + padding, map.MapMaxX - padding);
            }

            transform.position = position;

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = direction < 0f;
            }
        }

        private void OnDisable()
        {
            if (IsOperating)
            {
                IsOperating = false;
                weirdness?.NotifyDollUnavailable(this);
            }

            if (droppedPickup != null)
            {
                droppedPickup.enabled = pickupWasEnabled;
            }
        }
    }
}
