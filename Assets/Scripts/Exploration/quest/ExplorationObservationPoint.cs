using System;
using Spotlight.Shared;
using UnityEngine;
using UnityEngine.Events;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 观测怪异点击点：复用场景里已有的 RuleClickTarget。
    /// 只负责在任务解锁后完成观测，不加载任何场景；转场通过 Completed / onCompleted 交给外部。
    /// </summary>
    public sealed class ExplorationObservationPoint : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("复用的点击目标。留空会自动取同一个物体上的 RuleClickTarget")]
        private RuleClickTarget target;

        [SerializeField]
        [Tooltip("任务面板。留空会自动在场景里查找 ExplorationTaskPanel")]
        private ExplorationTaskPanel taskPanel;

        [SerializeField, Min(1)]
        [Tooltip("车厢号，观测完成后写入 RunProgress；车厢一填 1")]
        private int carriage = 1;

        [SerializeField]
        [Tooltip("观测完成时触发，供外部接战斗场景转场；这里不会自动加载场景")]
        private UnityEvent onCompleted;

        /// <summary>观测完成事件，参数为车厢号；供代码订阅转场。</summary>
        public event Action<int> Completed;

        private void Awake()
        {
            if (target == null)
            {
                target = GetComponent<RuleClickTarget>();
            }

            if (taskPanel == null)
            {
                taskPanel = FindObjectOfType<ExplorationTaskPanel>();
            }
        }

        private void OnEnable()
        {
            if (target != null)
            {
                target.Clicked += OnTargetClicked;
            }
        }

        private void OnDisable()
        {
            if (target != null)
            {
                target.Clicked -= OnTargetClicked;
            }
        }

        private void OnTargetClicked(string key, Vector3 position)
        {
            if (taskPanel == null || !taskPanel.ObserveTaskUnlocked || taskPanel.ObserveTaskCompleted)
            {
                return;
            }

            taskPanel.CompleteObserveTask();
            RunProgress.CompleteExploration(carriage);
            Completed?.Invoke(carriage);
            onCompleted?.Invoke();
        }
    }
}
