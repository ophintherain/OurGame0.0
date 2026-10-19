using TMPro;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 顶部功能提示：获得物品后显示一条说明文本，鼠标左键点击后关闭。
    /// 提示条本身不参与点击判定，不会挡住 HUD 和场景操作。
    /// 注意：本脚本不能挂在 panelRoot 自己或它的子物体上，否则关闭提示会把脚本一起关掉。
    /// </summary>
    public sealed class ExplorationHintPanel : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("提示条根节点（顶部深色底条）。开关时控制它的显隐，建议场景里默认隐藏")]
        private GameObject panelRoot;

        [SerializeField]
        [Tooltip("提示文本（TextMeshPro），运行时显示交互点里填写的功能说明")]
        private TMP_Text hintText;

        private int shownFrame = -1;

        /// <summary>提示是否正在显示。</summary>
        public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            if (panelRoot == null || hintText == null)
            {
                Debug.LogError("[Exploration] ExplorationHintPanel 缺少 panelRoot 或 hintText 引用。", this);
                enabled = false;
                return;
            }

            if (panelRoot == gameObject || transform.IsChildOf(panelRoot.transform))
            {
                Debug.LogError(
                    "[Exploration] ExplorationHintPanel 不能挂在 panelRoot 自己或它的子物体上，否则关闭提示会把脚本一起关掉。",
                    this);
                enabled = false;
                return;
            }

            panelRoot.SetActive(false);
        }

        private void Update()
        {
            if (!IsVisible)
            {
                return;
            }

            // 触发提示的那一次点击不能立刻把提示关掉
            if (Time.frameCount <= shownFrame)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                Hide();
            }
        }

        /// <summary>显示一条提示；重复调用会直接替换文本，并继续等待下一次点击关闭。</summary>
        public void Show(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            hintText.text = text;
            panelRoot.SetActive(true);
            shownFrame = Time.frameCount;
        }

        /// <summary>隐藏提示。</summary>
        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }
    }
}
