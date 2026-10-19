using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 任务面板：左上角按钮或 J 键开关；收集武器进度实时读取背包，完成后才显示「观测怪异」。
    /// 打开时锁住角色移动，并与背包面板互斥。
    /// </summary>
    public sealed class ExplorationTaskPanel : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("任务面板根节点（Panel），开关时控制它的显隐")]
        private GameObject panelRoot;

        [SerializeField]
        [Tooltip("任务行模板：面板下的 Image（自带 Text (TMP) 子物体）。第二行会在运行时复制它生成")]
        private Transform rowTemplate;

        [SerializeField]
        [Tooltip("第一行任务的 TMP 文本（rowTemplate 下的 Text (TMP)）")]
        private TMP_Text firstTaskText;

        [SerializeField]
        [Tooltip("HUD 左上角打开任务列表的按钮")]
        private Button btnOpen;

        [SerializeField]
        [Tooltip("任务面板关闭时按钮显示的图标。留空则保持按钮原来的图标")]
        private Sprite closedButtonIcon;

        [SerializeField]
        [Tooltip("任务面板打开时按钮显示的图标。留空则打开时不切换图标")]
        private Sprite openedButtonIcon;

        [SerializeField]
        [Tooltip("背包数据，用于统计已拥有的武器数量")]
        private ExplorationInventory inventory;

        [SerializeField]
        [Tooltip("Player 上的 ExplorationPlayerController。打开任务面板时锁住移动")]
        private ExplorationPlayerController player;

        [SerializeField]
        [Tooltip("背包面板。打开任务时自动关闭背包，打开背包时自动关闭任务")]
        private BackpackPanel backpackPanel;

        [SerializeField]
        [Tooltip("键盘开关任务列表的按键，默认 J")]
        private KeyCode toggleKey = KeyCode.J;

        [SerializeField, Min(1)]
        [Tooltip("「收集武器」任务的目标数量，默认 3。背包里武器数量达到该值即完成")]
        private int weaponTargetCount = 3;

        [SerializeField]
        [Tooltip("第二行相对第一行向下偏移的 UI 距离（面板单位），默认 64")]
        private float rowSpacing = 64f;

        /// <summary>任务完成事件：0 = 收集武器，1 = 观测怪异。</summary>
        public event Action<int> TaskCompleted;

        /// <summary>收集武器是否已完成。</summary>
        public bool WeaponTaskCompleted { get; private set; }

        /// <summary>观测怪异是否已完成。</summary>
        public bool ObserveTaskCompleted { get; private set; }

        private GameObject secondRow;
        private TMP_Text secondTaskText;
        private Image buttonImage;
        private Sprite defaultButtonIcon;
        private bool interactionEnabled = true;

        private void Awake()
        {
            if (panelRoot == null || rowTemplate == null || firstTaskText == null)
            {
                Debug.LogError("[Exploration] ExplorationTaskPanel 缺少 panelRoot / rowTemplate / firstTaskText 引用。", this);
                enabled = false;
                return;
            }

            if (inventory == null || player == null)
            {
                Debug.LogError("[Exploration] ExplorationTaskPanel 缺少 inventory 或 player 引用。", this);
                enabled = false;
                return;
            }

            if (backpackPanel == null)
            {
                backpackPanel = FindObjectOfType<BackpackPanel>();
            }

            if (btnOpen != null)
            {
                btnOpen.onClick.AddListener(Toggle);
                buttonImage = btnOpen.GetComponent<Image>();
                defaultButtonIcon = buttonImage != null ? buttonImage.sprite : null;
            }

            CreateSecondRow();

            inventory.Changed += RefreshTasks;

            if (backpackPanel != null)
            {
                backpackPanel.OpenStateChanged += OnBackpackOpenStateChanged;
            }

            panelRoot.SetActive(false);
            UpdateButtonIcon(false);
            RefreshTasks();
        }

        private void OnDestroy()
        {
            if (inventory != null)
            {
                inventory.Changed -= RefreshTasks;
            }

            if (backpackPanel != null)
            {
                backpackPanel.OpenStateChanged -= OnBackpackOpenStateChanged;
            }
        }

        private void Update()
        {
            if (interactionEnabled && Input.GetKeyDown(toggleKey))
            {
                Toggle();
            }
        }

        /// <summary>剧情等界面占用时，临时关闭任务面板的键盘与 HUD 按钮开关。</summary>
        public void SetInteractionEnabled(bool enabled)
        {
            interactionEnabled = enabled;
            if (btnOpen != null)
            {
                btnOpen.interactable = enabled;
            }
        }

        /// <summary>开关任务列表（按钮与 J 键共用）。</summary>
        public void Toggle()
        {
            SetOpen(!panelRoot.activeSelf);
        }

        /// <summary>打开 / 关闭任务面板。打开时锁住角色移动，并与背包互斥。</summary>
        public void SetOpen(bool open)
        {
            if (open && backpackPanel != null && backpackPanel.IsOpen)
            {
                backpackPanel.SetOpen(false);
            }

            panelRoot.SetActive(open);
            UpdateButtonIcon(open);

            if (player != null)
            {
                player.SetControlEnabled(!open);
            }

            if (open)
            {
                RefreshTasks();
            }
        }

        /// <summary>后续剧情 / 交互系统调用，完成「观测怪异」任务。</summary>
        public void CompleteObserveTask()
        {
            if (!WeaponTaskCompleted)
            {
                Debug.LogWarning("[Exploration] 收集武器任务尚未完成，观测怪异任务还未解锁。", this);
                return;
            }

            if (ObserveTaskCompleted)
            {
                return;
            }

            ObserveTaskCompleted = true;
            RefreshTasks();
            TaskCompleted?.Invoke(1);
        }

        /// <summary>用第一行模板复制出第二行，放在下方，初始隐藏。</summary>
        private void CreateSecondRow()
        {
            GameObject clone = Instantiate(rowTemplate.gameObject, rowTemplate.parent);
            clone.name = $"{rowTemplate.name}_2";

            RectTransform sourceRect = rowTemplate as RectTransform;
            RectTransform cloneRect = clone.transform as RectTransform;
            if (sourceRect != null && cloneRect != null)
            {
                cloneRect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(0f, -rowSpacing);
            }

            secondTaskText = clone.GetComponentInChildren<TMP_Text>(true);
            if (secondTaskText == null)
            {
                Debug.LogWarning("[Exploration] 第二行任务模板里没有找到 Text (TMP)。", this);
            }

            secondRow = clone;
            secondRow.SetActive(false);
        }

        /// <summary>刷新两条任务的文本与第二行的显隐；收集武器首次达标时派发完成事件。</summary>
        private void RefreshTasks()
        {
            int weaponCount = inventory.GetEntries(InventoryItemKind.Weapon).Count;

            if (!WeaponTaskCompleted && weaponCount >= weaponTargetCount)
            {
                WeaponTaskCompleted = true;
                TaskCompleted?.Invoke(0);
            }

            if (firstTaskText != null)
            {
                int shown = WeaponTaskCompleted ? weaponTargetCount : Mathf.Min(weaponCount, weaponTargetCount);
                firstTaskText.text = $"收集武器（{shown}/{weaponTargetCount}）";
            }

            if (secondRow != null)
            {
                secondRow.SetActive(WeaponTaskCompleted);
            }

            if (secondTaskText != null)
            {
                secondTaskText.text = ObserveTaskCompleted ? "观测怪异（1/1）" : "观测怪异（0/1）";
            }
        }

        private void OnBackpackOpenStateChanged(bool open)
        {
            // 背包打开时静默关闭任务面板，玩家锁定交给背包，避免互相解锁
            if (open && panelRoot.activeSelf)
            {
                panelRoot.SetActive(false);
                UpdateButtonIcon(false);
            }
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
    }
}
