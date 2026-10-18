using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 背包里的一个固定格子：底框由格子自身常显，图标按"是否拥有"显隐。
    /// 图标优先取子物体 Icon 上的 Image；没有 Icon 子物体时运行时自动创建，不需要手动拖引用。
    /// </summary>
    public sealed class InventoryCellView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("物品图标。留空会自动找子物体 Icon 上的 Image，找不到会自动创建一个")]
        private Image icon;

        private void Awake()
        {
            if (icon != null)
            {
                return;
            }

            Transform child = transform.Find("Icon");
            if (child == null)
            {
                icon = CreateIcon();
                return;
            }

            icon = child.GetComponent<Image>();
            if (icon == null)
            {
                Debug.LogWarning($"[Exploration] 格子 {name} 的子物体 Icon 上没有 Image 组件。", this);
            }
        }

        /// <summary>没有 Icon 子物体时，按格子大小创建一个铺满的图标层。</summary>
        private Image CreateIcon()
        {
            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform iconRect = (RectTransform)iconObject.transform;
            iconRect.SetParent(transform, false);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            Image image = iconObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        /// <summary>显示一个条目：有图标就显示，没有就只留底框。</summary>
        public void SetEntry(InventoryEntry entry)
        {
            if (icon == null)
            {
                return;
            }

            icon.sprite = entry.Icon;
            icon.enabled = entry.Icon != null;
        }

        /// <summary>清空为"未收集"状态：保留底框，只隐藏图标。</summary>
        public void Clear()
        {
            if (icon == null)
            {
                return;
            }

            icon.sprite = null;
            icon.enabled = false;
        }
    }
}
