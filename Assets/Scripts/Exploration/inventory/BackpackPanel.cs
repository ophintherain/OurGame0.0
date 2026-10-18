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

        /// <summary>面板是否处于打开状态。</summary>
        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        /// <summary>打开 / 关闭状态变化（true = 打开）。供任务面板等做互斥。</summary>
        public event Action<bool> OpenStateChanged;

        private InventoryCellView[] weaponCells;
        private InventoryCellView[] itemCells;
        private InventoryItemKind currentKind = InventoryItemKind.Weapon;
        private Image buttonImage;
        private Sprite defaultButtonIcon;

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

            weaponCells = CollectCells(weaponGridRoot);
            itemCells = CollectCells(itemGridRoot);

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
            panelRoot.SetActive(false);
            UpdateButtonIcon(false);
        }

        private void OnDestroy()
        {
            if (inventory != null)
            {
                inventory.Changed -= OnInventoryChanged;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                Toggle();
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
            panelRoot.SetActive(open);
            UpdateButtonIcon(open);

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

        private static InventoryCellView[] CollectCells(Transform gridRoot)
        {
            InventoryCellView[] cells = gridRoot.GetComponentsInChildren<InventoryCellView>(true);
            if (cells.Length == 0)
            {
                Debug.LogWarning($"[Exploration] {gridRoot.name} 下没有找到带 InventoryCellView 的格子。", gridRoot);
            }

            return cells;
        }
    }
}
