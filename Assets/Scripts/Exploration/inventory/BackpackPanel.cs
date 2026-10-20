using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 背包面板：B 键或 HUD 图标开关；武器 / 物品两个页签，按固定格子展示已收集的条目。
    /// 打开时锁住角色移动，关闭时恢复。
    /// 注意：本脚本不能挂在 panelRoot 自己或它的子物体上，否则关闭面板会把脚本一起关掉。
    /// </summary>
    public sealed class BackpackPanel : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("背包面板根节点。开关时控制它的显隐，建议场景里默认隐藏")]
        private GameObject panelRoot;

        [SerializeField]
        [Tooltip("武器格子容器（带 GridLayoutGroup）。子物体顺序就是格子顺序，每个格子上挂 InventoryCellView")]
        private Transform weaponGridRoot;

        [SerializeField]
        [Tooltip("物品格子容器，结构同武器页；两个容器互斥显示。留空表示和武器页共用同一个容器")]
        private Transform itemGridRoot;

        [SerializeField]
        [Tooltip("「武器」页签按钮")]
        private Button btnWeaponTab;

        [SerializeField]
        [Tooltip("「物品」页签按钮")]
        private Button btnItemTab;

        [SerializeField]
        [Tooltip("HUD 上打开背包的图标按钮")]
        private Button btnOpen;

        [SerializeField]
        [Tooltip("背包关闭时 HUD 按钮显示的图标。留空则保持按钮原来的图标")]
        private Sprite closedButtonIcon;

        [SerializeField]
        [Tooltip("背包打开时 HUD 按钮显示的图标（例如打开的背包）。留空则打开时不切换图标")]
        private Sprite openedButtonIcon;

        [SerializeField]
        [Tooltip("Player 上的 ExplorationPlayerController。打开背包时锁住移动，关闭时恢复")]
        private ExplorationPlayerController player;

        [SerializeField]
        [Tooltip("背包数据。留空会自动取同一个物体上的 ExplorationInventory（没有则自动添加）")]
        private ExplorationInventory inventory;

        [SerializeField]
        [Tooltip("键盘开关背包的按键，默认 B")]
        private KeyCode toggleKey = KeyCode.B;

        [SerializeField, Min(0f)]
        [Tooltip("右键放下物品的落点：相对地图地面的高度偏移。0.7 = 站立时脚底到角色轴心的距离；数值越大物品放得越低，默认 1.2")]
        private float dropFootOffset = 1.2f;

        [SerializeField, Min(0.05f)]
        [Tooltip("右键放下物品的立绘最大边长（世界单位）。数值越小掉落物越小，点击范围会同步变小")]
        private float dropVisualHeight = 0.7f;

        [SerializeField]
        [Tooltip("放下物品的 Sorting Order。默认 20，与场景里的可探索点一致；数值越大越靠前")]
        private int dropSortingOrder = 20;

        [SerializeField]
        [Tooltip("打开背包时播放的音效。直接把音频文件拖进来；留空则不播")]
        private AudioClip openSound;

        [SerializeField]
        [Tooltip("关闭背包时播放的音效。直接把音频文件拖进来；留空则不播")]
        private AudioClip closeSound;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("背包开关音效的音量 0~1。0 = 静音，1 = 原始音量")]
        private float soundVolume = 1f;

        [SerializeField]
        [Tooltip("播放背包开关音效的 AudioSource。留空会自动取同一个物体上的组件，没有则自动添加；一般不需要手动拖")]
        private AudioSource soundSource;

        /// <summary>面板是否处于打开状态。</summary>
        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        /// <summary>打开 / 关闭状态变化（true = 打开）。供任务面板等做互斥。</summary>
        public event Action<bool> OpenStateChanged;

        private InventoryCellView[] weaponCells;
        private InventoryCellView[] itemCells;
        private InventoryItemKind currentKind = InventoryItemKind.Weapon;
        private Image buttonImage;
        private Sprite defaultButtonIcon;
        private bool interactionEnabled = true;
        private ExplorationMap map;

        private void Awake()
        {
            if (panelRoot == null || weaponGridRoot == null)
            {
                Debug.LogError("[Exploration] BackpackPanel 缺少 panelRoot / weaponGridRoot 引用。", this);
                enabled = false;
                return;
            }

            if (itemGridRoot == null)
            {
                itemGridRoot = weaponGridRoot;
            }

            if (panelRoot == gameObject || transform.IsChildOf(panelRoot.transform))
            {
                Debug.LogError(
                    "[Exploration] BackpackPanel 不能挂在 panelRoot 自己或它的子物体上，否则关闭面板会把脚本一起关掉。请把它挂到 Canvas 或一个常驻物体上。",
                    this);
                enabled = false;
                return;
            }

            if (inventory == null)
            {
                inventory = GetComponent<ExplorationInventory>();
                if (inventory == null)
                {
                    inventory = gameObject.AddComponent<ExplorationInventory>();
                }
            }

            map ??= FindObjectOfType<ExplorationMap>();

            if (soundSource == null)
            {
                soundSource = GetComponent<AudioSource>();
                if (soundSource == null)
                {
                    soundSource = gameObject.AddComponent<AudioSource>();
                }
            }

            soundSource.playOnAwake = false;
            soundSource.spatialBlend = 0f;

            NormalizeGridScale(weaponGridRoot);
            NormalizeGridScale(itemGridRoot);

            weaponCells = CollectCells(weaponGridRoot);
            itemCells = CollectCells(itemGridRoot);
            SubscribeCellClicks(weaponCells);
            SubscribeCellClicks(itemCells);

            if (btnWeaponTab != null)
            {
                btnWeaponTab.onClick.AddListener(() => SwitchTab(InventoryItemKind.Weapon));
            }

            if (btnItemTab != null)
            {
                btnItemTab.onClick.AddListener(() => SwitchTab(InventoryItemKind.Item));
            }

            if (btnOpen != null)
            {
                btnOpen.onClick.AddListener(Toggle);
                buttonImage = btnOpen.GetComponent<Image>();
                defaultButtonIcon = buttonImage != null ? buttonImage.sprite : null;
            }

            inventory.Changed += OnInventoryChanged;
            DisableNonInteractiveRaycasts();
            panelRoot.SetActive(false);
            UpdateButtonIcon(false);
        }

        private void OnDestroy()
        {
            if (inventory != null)
            {
                inventory.Changed -= OnInventoryChanged;
            }

            UnsubscribeCellClicks(weaponCells);
            UnsubscribeCellClicks(itemCells);
        }

        private void Update()
        {
            if (interactionEnabled && Input.GetKeyDown(toggleKey))
            {
                Toggle();
            }
        }

        /// <summary>剧情等界面占用时，临时关闭背包的键盘与 HUD 按钮开关。</summary>
        public void SetInteractionEnabled(bool enabled)
        {
            interactionEnabled = enabled;
            if (btnOpen != null)
            {
                btnOpen.interactable = enabled;
            }
        }

        /// <summary>开关背包（键盘与图标按钮共用）。</summary>
        public void Toggle()
        {
            SetOpen(!panelRoot.activeSelf);
        }

        /// <summary>打开 / 关闭背包。打开时锁住角色移动，并按当前页签刷新。</summary>
        public void SetOpen(bool open)
        {
            bool stateChanged = panelRoot.activeSelf != open;
            panelRoot.SetActive(open);
            UpdateButtonIcon(open);

            if (stateChanged)
            {
                PlayToggleSound(open);
            }

            if (player != null)
            {
                player.SetControlEnabled(!open);
            }

            if (open)
            {
                SwitchTab(currentKind);
            }

            OpenStateChanged?.Invoke(open);
        }

        /// <summary>播放打开 / 关闭背包音效；没有配置对应音频时静默。</summary>
        private void PlayToggleSound(bool open)
        {
            AudioClip clip = open ? openSound : closeSound;
            if (clip == null || soundSource == null)
            {
                return;
            }

            soundSource.PlayOneShot(clip, Mathf.Clamp01(soundVolume));
        }

        /// <summary>切换 HUD 按钮图标：打开用 openedButtonIcon，关闭用 closedButtonIcon（留空则回退到原图标）。</summary>
        private void UpdateButtonIcon(bool open)
        {
            if (buttonImage == null)
            {
                return;
            }

            Sprite target = open
                ? openedButtonIcon
                : (closedButtonIcon != null ? closedButtonIcon : defaultButtonIcon);

            if (target != null)
            {
                buttonImage.sprite = target;
            }
        }

        /// <summary>切换武器 / 物品页签，并刷新该页格子。</summary>
        public void SwitchTab(InventoryItemKind kind)
        {
            currentKind = kind;

            if (weaponGridRoot != itemGridRoot)
            {
                weaponGridRoot.gameObject.SetActive(kind == InventoryItemKind.Weapon);
                itemGridRoot.gameObject.SetActive(kind == InventoryItemKind.Item);
            }

            Refresh(kind);
        }

        private void OnInventoryChanged()
        {
            if (panelRoot.activeSelf)
            {
                Refresh(currentKind);
            }
        }

        private void Refresh(InventoryItemKind kind)
        {
            List<InventoryEntry> entries = inventory.GetEntries(kind);
            InventoryCellView[] cells = kind == InventoryItemKind.Weapon ? weaponCells : itemCells;

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == null)
                {
                    continue;
                }

                if (i < entries.Count)
                {
                    cells[i].SetEntry(entries[i]);
                }
                else
                {
                    cells[i].Clear();
                }
            }
        }

        private void SubscribeCellClicks(InventoryCellView[] cells)
        {
            if (cells == null)
            {
                return;
            }

            for (int index = 0; index < cells.Length; index++)
            {
                if (cells[index] != null)
                {
                    cells[index].RightClicked += OnCellRightClicked;
                }
            }
        }

        private void UnsubscribeCellClicks(InventoryCellView[] cells)
        {
            if (cells == null)
            {
                return;
            }

            for (int index = 0; index < cells.Length; index++)
            {
                if (cells[index] != null)
                {
                    cells[index].RightClicked -= OnCellRightClicked;
                }
            }
        }

        /// <summary>右键格子：物品类（2000~2999）从背包放下；武器不可放。</summary>
        private void OnCellRightClicked(InventoryEntry entry)
        {
            if (entry == null || entry.Kind != InventoryItemKind.Item)
            {
                return;
            }

            if (inventory.Remove(entry.Id))
            {
                SpawnDroppedItem(entry);
            }
        }

        /// <summary>把放下的物品生成在角色站立时的脚部位置；点击可重新拾取。</summary>
        private void SpawnDroppedItem(InventoryEntry entry)
        {
            Vector3 position = player != null ? player.transform.position : transform.position;
            float groundY = map != null ? map.GroundY : position.y;
            position.y = groundY - dropFootOffset;
            position.z = 0f;

            GameObject dropped = new GameObject($"Dropped_{entry.Id}");
            dropped.transform.position = position;

            SpriteRenderer renderer = dropped.AddComponent<SpriteRenderer>();
            renderer.sprite = entry.Icon;
            renderer.sortingOrder = dropSortingOrder;

            DroppedItemPickup pickup = dropped.AddComponent<DroppedItemPickup>();
            pickup.Configure(inventory, entry.Id, entry.Icon, entry.DisplayName, dropVisualHeight);
        }

        /// <summary>
        /// GridLayoutGroup 按未缩放的矩形排布子物体，容器一旦有缩放，格子会被整体放大并推到面板外。
        /// 这里把容器缩放还原为 1；需要的视觉大小应改到 Cell Size 或格子自身的 Scale 上。
        /// </summary>
        private static void NormalizeGridScale(Transform gridRoot)
        {
            Vector3 scale = gridRoot.localScale;
            if (Mathf.Approximately(scale.x, 1f) &&
                Mathf.Approximately(scale.y, 1f) &&
                Mathf.Approximately(scale.z, 1f))
            {
                return;
            }

            Debug.LogWarning(
                $"[Exploration] {gridRoot.name} 的 Scale 是 {scale}，GridLayoutGroup 会按未缩放尺寸排版，导致格子被推到面板外。" +
                "已在运行时按 1 处理；请把它的 Scale 改回 1，再用 Cell Size 或格子自身的 Scale 调整大小。",
                gridRoot);
            gridRoot.localScale = Vector3.one;
        }

        private static InventoryCellView[] CollectCells(Transform gridRoot)
        {
            InventoryCellView[] cells = gridRoot.GetComponentsInChildren<InventoryCellView>(true);
            if (cells.Length == 0)
            {
                Debug.LogWarning($"[Exploration] {gridRoot.name} 下没有找到带 InventoryCellView 的格子。", gridRoot);
            }

            return cells;
        }

        /// <summary>
        /// 关掉面板里装饰性图片的 Raycast，避免背景、格子容器、格子底图盖在页签按钮上方，
        /// 导致背包打开后点不到页签、也点不到被面板遮住的 HUD 按钮。按钮自己的图片保留 Raycast。
        /// </summary>
        private void DisableNonInteractiveRaycasts()
        {
            Image[] images = panelRoot.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];

                // 背包格子自身需要接收右键放下，不能被当成装饰图关掉
                if (image.GetComponent<InventoryCellView>() != null)
                {
                    image.raycastTarget = true;
                    continue;
                }

                if (image.GetComponent<Button>() != null || image.GetComponentInParent<Button>(true) != null)
                {
                    continue;
                }

                image.raycastTarget = false;
            }
        }
    }
}
