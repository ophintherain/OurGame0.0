using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 死亡后的重新挑战面板：显示 Retry 面板，
    /// 「是」回满血并重载当前场景，「否」只触发返回开始页面的接口。
    /// </summary>
    public sealed class ExplorationRetryPanel : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Retry 面板根节点。死亡时显示，平时隐藏")]
        private GameObject panelRoot;

        [SerializeField]
        [Tooltip("「是」按钮，点击后回满血并重载当前场景")]
        private Button yesButton;

        [SerializeField]
        [Tooltip("「否」按钮，点击后触发 onReturnToStart，不自动跳转")]
        private Button noButton;

        [SerializeField]
        [Tooltip("Player 上的 ExplorationHealth，用于监听死亡并恢复满血。留空会自动查找")]
        private ExplorationHealth health;

        [SerializeField]
        [Tooltip("Player 上的 ExplorationPlayerController，死亡后锁住移动")]
        private ExplorationPlayerController player;

        [SerializeField]
        [Tooltip("选择「否」时触发，留给外部接返回开始页面的转场；这里不会自动加载场景")]
        private UnityEvent onReturnToStart;

        /// <summary>选择「否」时触发，供代码订阅返回开始页面的逻辑。</summary>
        public event Action ReturnToStartRequested;

        private void Awake()
        {
            if (health == null)
            {
                health = FindObjectOfType<ExplorationHealth>();
            }

            if (player == null)
            {
                player = FindObjectOfType<ExplorationPlayerController>();
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            if (yesButton != null)
            {
                yesButton.onClick.AddListener(ChooseRetry);
            }

            if (noButton != null)
            {
                noButton.onClick.AddListener(ChooseReturnToStart);
            }
        }

        private void Start()
        {
            // 如果进场时血量已经是 0（例如静态血量没恢复），直接显示面板
            if (health != null && health.IsDead)
            {
                ShowPanel();
            }
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= OnDied;
            }
        }

        private void OnDestroy()
        {
            if (yesButton != null)
            {
                yesButton.onClick.RemoveListener(ChooseRetry);
            }

            if (noButton != null)
            {
                noButton.onClick.RemoveListener(ChooseReturnToStart);
            }
        }

        private void OnDied()
        {
            ShowPanel();
        }

        private void ShowPanel()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            if (player != null)
            {
                player.SetControlEnabled(false);
            }

            if (EventSystem.current != null && yesButton != null)
            {
                EventSystem.current.SetSelectedGameObject(yesButton.gameObject);
            }
        }

        /// <summary>选择「是」：回满血并重载当前场景。</summary>
        public void ChooseRetry()
        {
            if (health != null)
            {
                health.ResetToFull();
            }

            Scene active = SceneManager.GetActiveScene();
            if (active.buildIndex >= 0)
            {
                SceneManager.LoadScene(active.buildIndex);
            }
            else
            {
                SceneManager.LoadScene(active.name);
            }
        }

        /// <summary>选择「否」：只触发返回开始页面的接口，不自动跳转。</summary>
        public void ChooseReturnToStart()
        {
            ReturnToStartRequested?.Invoke();
            onReturnToStart?.Invoke();

            Debug.Log("[Retry] 选择返回开始页面；请在 onReturnToStart 中接转场。", this);
        }
    }
}
