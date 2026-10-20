using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 剧情表现层：底部对话框、全屏旁白、顶部提示、选项按钮。
    /// 只负责显示和输入，不认识剧情数据与玩家。
    /// 注意：本脚本不能挂在 panelRoot 自己或它的子物体上，否则隐藏 UI 会把脚本一起关掉。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class StoryOverlayView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("剧情 UI 根节点（StoryOverlay），默认隐藏。不能是本脚本所在物体或其父物体")]
        private GameObject panelRoot;

        [SerializeField]
        [Tooltip("底部对话框根节点，显示角色名、正文、立绘")]
        private GameObject dialogueRoot;

        [SerializeField]
        [Tooltip("全屏黑底白字旁白根节点")]
        private GameObject narrationRoot;

        [SerializeField]
        [Tooltip("顶部提示根节点，引导 / 广播 / 警告共用")]
        private GameObject topHintRoot;

        [SerializeField]
        [Tooltip("选项按钮容器根节点")]
        private GameObject choiceRoot;

        [SerializeField]
        [Tooltip("角色名文本（TextMeshPro）")]
        private TMP_Text speakerText;

        [SerializeField]
        [Tooltip("底部对话框正文文本（TextMeshPro）")]
        private TMP_Text bodyText;

        [SerializeField]
        [Tooltip("全屏旁白文本（TextMeshPro）")]
        private TMP_Text narrationText;

        [SerializeField]
        [Tooltip("顶部提示文本（TextMeshPro），引导 / 广播 / 警告共用")]
        private TMP_Text topHintText;

        [SerializeField]
        [Tooltip("立绘图片。留空则不显示立绘。多个立绘同时出现时会以这个节点为模板和基准位置，自动向右复制")]
        private Image portraitImage;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("多个立绘同时出现时，相邻立绘的水平间距（相对单张立绘宽度的比例）。1 = 刚好并排，0.6 = 部分重叠；只有 1 张立绘时不起作用")]
        private float portraitSpacing = 0.85f;

        [SerializeField]
        [Tooltip("「点击继续」提示物体。留空则不显示提示")]
        private GameObject continueHint;

        [SerializeField]
        [Tooltip("选项按钮模板（Button + TextMeshPro 子物体）。模板本身默认隐藏，运行时按选项数量克隆")]
        private Button choiceButtonTemplate;

        [SerializeField]
        [Tooltip("引导 / 广播提示的正文颜色。警告会使用下面的警告色")]
        private Color normalHintColor = Color.white;

        [SerializeField]
        [Tooltip("警告提示的正文颜色，默认偏红。只在 Warning 行生效")]
        private Color warningColor = new Color(1f, 0.35f, 0.35f, 1f);

        private readonly List<GameObject> choiceInstances = new List<GameObject>();
        private readonly List<Image> portraitSlots = new List<Image>();
        private Vector2 portraitBasePosition;
        private bool portraitLayerReady;
        private string playerDisplayName = string.Empty;
        private string censorReplacement = string.Empty;
        private int shownFrame = -1;
        private bool choiceMode;

        /// <summary>继续按钮 / 任意点击 / 空格 / 回车。</summary>
        public event Action ContinueRequested;

        /// <summary>选项被点击（参数为选项索引）。本期所有选项都汇合到同一段后续。</summary>
        public event Action<int> ChoiceSelected;

        /// <summary>剧情 UI 当前是否显示。</summary>
        public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            if (panelRoot == null || bodyText == null)
            {
                Debug.LogError("[Story] StoryOverlayView 缺少 panelRoot 或 bodyText 引用。", this);
                enabled = false;
                return;
            }

            if (panelRoot == gameObject || transform.IsChildOf(panelRoot.transform))
            {
                Debug.LogError(
                    "[Story] StoryOverlayView 不能挂在 panelRoot 自己或它的子物体上，否则隐藏 UI 会把脚本一起关掉。",
                    this);
                enabled = false;
                return;
            }

            if (choiceButtonTemplate != null)
            {
                choiceButtonTemplate.gameObject.SetActive(false);
            }

            if (portraitImage != null)
            {
                portraitSlots.Add(portraitImage);
                portraitBasePosition = portraitImage.rectTransform.anchoredPosition;
            }

            panelRoot.SetActive(false);
        }

        /// <summary>由 StoryDirector 注入占位符替换文本。</summary>
        public void SetTextReplacements(string displayName, string censor)
        {
            playerDisplayName = displayName ?? string.Empty;
            censorReplacement = censor ?? string.Empty;
        }

        /// <summary>显示一行剧情。portraits 为空或全部为 null 时隐藏立绘。</summary>
        public void ShowLine(StoryLine line, Sprite[] portraits)
        {
            if (line == null || panelRoot == null)
            {
                return;
            }

            panelRoot.SetActive(true);
            shownFrame = Time.frameCount;
            choiceMode = false;
            ClearChoices();

            SetActive(dialogueRoot, false);
            SetActive(narrationRoot, false);
            SetActive(topHintRoot, false);
            SetActive(choiceRoot, false);
            SetActive(continueHint, true);

            switch (line.kind)
            {
                case StoryLineKind.Narration:
                    SetActive(narrationRoot, true);
                    SetText(narrationText, line.text);
                    HidePortrait();
                    break;

                case StoryLineKind.Tutorial:
                case StoryLineKind.Broadcast:
                case StoryLineKind.Warning:
                    SetActive(topHintRoot, true);
                    SetText(topHintText, line.text);
                    if (topHintText != null)
                    {
                        topHintText.color = line.kind == StoryLineKind.Warning ? warningColor : normalHintColor;
                    }

                    HidePortrait();
                    break;

                case StoryLineKind.Choice:
                    SetActive(choiceRoot, true);
                    SetActive(continueHint, false);
                    choiceMode = true;
                    BuildChoices(line.choices);
                    HidePortrait();
                    break;

                default:
                    SetActive(dialogueRoot, true);
                    SetText(speakerText, line.speaker);
                    if (speakerText != null)
                    {
                        speakerText.gameObject.SetActive(!string.IsNullOrWhiteSpace(line.speaker));
                    }

                    SetText(bodyText, line.text);
                    ShowPortraits(portraits);
                    break;
            }
        }

        /// <summary>隐藏剧情 UI 并清掉运行时克隆的选项按钮。</summary>
        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            ClearChoices();
            choiceMode = false;
            SetActive(continueHint, true);
        }

        private void Update()
        {
            if (panelRoot == null || !panelRoot.activeSelf || choiceMode)
            {
                return;
            }

            // 触发剧情的那一次点击不能立刻翻页
            if (Time.frameCount <= shownFrame)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0) ||
                Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ContinueRequested?.Invoke();
            }
        }

        private void BuildChoices(string[] choices)
        {
            if (choiceRoot == null || choiceButtonTemplate == null)
            {
                Debug.LogError("[Story] 缺少 choiceRoot 或 choiceButtonTemplate，无法显示选项。", this);
                choiceMode = false;
                return;
            }

            if (choices == null || choices.Length == 0)
            {
                Debug.LogWarning("[Story] Choice 行没有任何选项文字，已跳过。", this);
                choiceMode = false;
                return;
            }

            for (int index = 0; index < choices.Length; index++)
            {
                GameObject instance = Instantiate(choiceButtonTemplate.gameObject, choiceRoot.transform);
                instance.SetActive(true);

                TMP_Text label = instance.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = FormatText(choices[index]);
                }

                Button button = instance.GetComponent<Button>();
                if (button != null)
                {
                    int capturedIndex = index;
                    button.onClick.AddListener(() => OnChoiceClicked(capturedIndex));
                }

                choiceInstances.Add(instance);
            }
        }

        private void OnChoiceClicked(int index)
        {
            if (!choiceMode)
            {
                return;
            }

            choiceMode = false;
            ChoiceSelected?.Invoke(index);
        }

        private void ClearChoices()
        {
            for (int index = 0; index < choiceInstances.Count; index++)
            {
                if (choiceInstances[index] != null)
                {
                    Destroy(choiceInstances[index]);
                }
            }

            choiceInstances.Clear();
        }

        private string FormatText(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            string result = raw;
            result = result.Replace("#master name#", playerDisplayName);
            result = result.Replace("**文明用语**", censorReplacement);
            return result;
        }

        /// <summary>
        /// 显示一行里的所有立绘。以 portraitImage 为模板按需克隆槽位，
        /// 多张时以原始位置为中心横向排开，间距由 portraitSpacing 控制。
        /// </summary>
        private void ShowPortraits(Sprite[] portraits)
        {
            if (portraitImage == null)
            {
                return;
            }

            EnsurePortraitLayer();

            List<Sprite> visible = new List<Sprite>();
            if (portraits != null)
            {
                for (int index = 0; index < portraits.Length; index++)
                {
                    if (portraits[index] != null)
                    {
                        visible.Add(portraits[index]);
                    }
                }
            }

            EnsurePortraitSlots(visible.Count);

            for (int index = 0; index < portraitSlots.Count; index++)
            {
                bool show = index < visible.Count;
                Image slot = portraitSlots[index];
                slot.sprite = show ? visible[index] : null;
                slot.enabled = show;
                slot.gameObject.SetActive(show);
            }

            float spacing = portraitImage.rectTransform.rect.width * portraitSpacing;
            float center = (visible.Count - 1) * 0.5f;

            for (int index = 0; index < visible.Count; index++)
            {
                portraitSlots[index].rectTransform.anchoredPosition =
                    portraitBasePosition + new Vector2((index - center) * spacing, 0f);
            }
        }

        private void EnsurePortraitSlots(int count)
        {
            if (portraitImage == null)
            {
                return;
            }

            if (portraitSlots.Count == 0)
            {
                portraitSlots.Add(portraitImage);
                portraitBasePosition = portraitImage.rectTransform.anchoredPosition;
            }

            while (portraitSlots.Count < count)
            {
                Image clone = Instantiate(portraitImage, portraitImage.transform.parent);
                clone.name = $"PortraitSlot{portraitSlots.Count}";
                clone.sprite = null;
                clone.enabled = false;
                clone.raycastTarget = false;

                if (dialogueRoot != null && clone.transform.parent == dialogueRoot.transform.parent)
                {
                    // 克隆槽位也要排在对话框之前，否则会盖住对话框
                    int dialogueIndex = dialogueRoot.transform.GetSiblingIndex();
                    clone.transform.SetSiblingIndex(Mathf.Max(0, dialogueIndex - 1));
                }

                portraitSlots.Add(clone);
            }
        }

        /// <summary>
        /// 第一次显示立绘时再调整层级：此时 Canvas 布局已完成，换父节点不会把位置算偏。
        /// </summary>
        private void EnsurePortraitLayer()
        {
            if (portraitLayerReady)
            {
                return;
            }

            portraitLayerReady = true;
            MovePortraitBehindDialogue();

            if (portraitImage != null)
            {
                portraitBasePosition = portraitImage.rectTransform.anchoredPosition;
            }
        }

        /// <summary>
        /// 把立绘挂到对话框的同一父级，并排在对话框前面，使立绘位于对话框面板之下、不再遮挡它。
        /// SetParent 保持世界位置，所以 Inspector 里调好的位置不会变。
        /// </summary>
        private void MovePortraitBehindDialogue()
        {
            if (portraitImage == null || dialogueRoot == null)
            {
                return;
            }

            Transform parent = dialogueRoot.transform.parent;
            if (parent == null)
            {
                return;
            }

            if (portraitImage.transform.parent != parent)
            {
                portraitImage.transform.SetParent(parent, true);
            }

            // 注意要插到对话框索引的前一位；直接 SetSiblingIndex(对话框索引) 会排到对话框后面
            int dialogueIndex = dialogueRoot.transform.GetSiblingIndex();
            portraitImage.transform.SetSiblingIndex(Mathf.Max(0, dialogueIndex - 1));
        }

        private void HidePortrait()
        {
            for (int index = 0; index < portraitSlots.Count; index++)
            {
                Image slot = portraitSlots[index];
                if (slot == null)
                {
                    continue;
                }

                slot.sprite = null;
                slot.enabled = false;
                slot.gameObject.SetActive(false);
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }
    }
}
