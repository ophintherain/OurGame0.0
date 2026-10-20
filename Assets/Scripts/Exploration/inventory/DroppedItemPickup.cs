using UnityEngine;
using UnityEngine.EventSystems;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 从背包右键放下的物品：落在角色站立时的脚部位置，点击后重新加入背包。
    /// 不需要 Collider，用世界坐标矩形判定点击。
    /// </summary>
    public sealed class DroppedItemPickup : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("点击矩形的宽高（世界单位）。留空时由图标按 visualHeight 缩放后自动填")]
        private Vector2 clickSize = new Vector2(0.8f, 0.8f);

        [SerializeField, Min(0.05f)]
        [Tooltip("放下后图标在世界里的最大边长（世界单位）。由 BackpackPanel 在创建时注入，一般不需要手动改这里")]
        private float visualHeight = 0.7f;

        private ExplorationInventory inventory;
        private int itemId;
        private Sprite icon;
        private string displayName;
        private string description;
        private bool collected;

        /// <summary>由 BackpackPanel 创建时注入数据。</summary>
        public void Configure(ExplorationInventory targetInventory, int id, Sprite itemIcon, string name,
            string itemDescription, float height)
        {
            inventory = targetInventory;
            itemId = id;
            icon = itemIcon;
            displayName = name;
            description = itemDescription;
            visualHeight = Mathf.Max(0.05f, height);
        }

        private void Start()
        {
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null)
            {
                return;
            }

            Vector2 spriteSize = renderer.sprite.bounds.size;
            float maxSide = Mathf.Max(spriteSize.x, spriteSize.y);
            float scale = maxSide > 0.001f ? visualHeight / maxSide : 1f;
            transform.localScale = Vector3.one * scale;
            clickSize = spriteSize * scale;

            // 让图标底部落在投放点（脚部）上，而不是中心
            transform.position += Vector3.up * (clickSize.y * 0.5f);
        }

        private void Update()
        {
            if (collected || inventory == null || !Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Camera worldCamera = Camera.main;
            if (worldCamera == null)
            {
                return;
            }

            Vector3 world = worldCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 center = transform.position;
            float halfWidth = Mathf.Abs(clickSize.x) * 0.5f;
            float halfHeight = Mathf.Abs(clickSize.y) * 0.5f;

            if (Mathf.Abs(world.x - center.x) > halfWidth ||
                Mathf.Abs(world.y - center.y) > halfHeight)
            {
                return;
            }

            if (inventory.Add(itemId, icon, displayName, description))
            {
                collected = true;
                Destroy(gameObject);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.9f);
            Gizmos.DrawWireCube(transform.position,
                new Vector3(Mathf.Abs(clickSize.x), Mathf.Abs(clickSize.y), 0f));
        }
    }
}
