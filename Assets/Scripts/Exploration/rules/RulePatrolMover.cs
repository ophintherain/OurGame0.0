using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 规则用的简单巡逻移动：在左右两个 X 边界之间往返，只改 X，方向和贴图翻转可选。
    /// 不依赖 Rigidbody / Collider，直接改 Transform。
    /// </summary>
    public sealed class RulePatrolMover : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("巡逻左边界（世界 X）。物体在这里转向向右走")]
        private float leftX = -1.96f;

        [SerializeField]
        [Tooltip("巡逻右边界（世界 X）。物体在这里转向向左走")]
        private float rightX = 1.92f;

        [SerializeField, Min(0f)]
        [Tooltip("移动速度，单位世界单位/秒。0 = 不移动。数值越大巡逻越快")]
        private float speed = 0.8f;

        [SerializeField]
        [Tooltip("是否按移动方向翻转 SpriteRenderer 的 X。勾选后向左走会镜像贴图")]
        private bool flipSprite = true;

        private SpriteRenderer spriteRenderer;
        private int direction = 1;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (speed <= 0f)
            {
                return;
            }

            float left = Mathf.Min(leftX, rightX);
            float right = Mathf.Max(leftX, rightX);

            Vector3 position = transform.position;
            position.x += direction * speed * Time.deltaTime;

            if (position.x >= right)
            {
                position.x = right;
                direction = -1;
            }
            else if (position.x <= left)
            {
                position.x = left;
                direction = 1;
            }

            transform.position = position;

            if (flipSprite && spriteRenderer != null)
            {
                spriteRenderer.flipX = direction < 0;
            }
        }

        private void OnValidate()
        {
            speed = Mathf.Max(0f, speed);
        }

        private void OnDrawGizmosSelected()
        {
            float left = Mathf.Min(leftX, rightX);
            float right = Mathf.Max(leftX, rightX);
            Vector3 from = new Vector3(left, transform.position.y, 0f);
            Vector3 to = new Vector3(right, transform.position.y, 0f);

            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.9f);
            Gizmos.DrawLine(from, to);
            Gizmos.DrawWireSphere(from, 0.08f);
            Gizmos.DrawWireSphere(to, 0.08f);
        }
    }
}
