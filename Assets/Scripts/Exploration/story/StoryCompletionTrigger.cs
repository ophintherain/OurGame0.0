using System;
using System.Collections;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>一条「某段剧情完成后 → 延迟 → 播放下一段」的规则。</summary>
    [Serializable]
    public sealed class StoryCompletionRule
    {
        [Tooltip("前置剧情 ID。该段剧情结束（StoryFinished）后触发本规则")]
        public string finishedStoryId;

        [SerializeField, Min(0f)]
        [Tooltip("前置剧情结束后延迟多少秒再播放下一段，0 = 立即")]
        public float delaySeconds;

        [Tooltip("延迟结束后按顺序播放的剧情 ID 列表。只播一段就填一个元素")]
        public string[] nextStoryIds = Array.Empty<string>();
    }

    /// <summary>
    /// 剧情完成链：监听 StoryFinished，按列表里的规则延迟播放下一段。
    /// 一个场景可以挂多个组件，也可以在一个组件里配多条规则。
    /// 注意：StoryFinished 也会在 HideCurrent() 收起广播 / 引导时派发。
    /// </summary>
    public sealed class StoryCompletionTrigger : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("剧情调度器。留空会自动查找场景里的 StoryDirector")]
        private StoryDirector director;

        [SerializeField]
        [Tooltip("完成规则列表：每条规则指定「哪段剧情完成后」延迟多久播放哪些剧情")]
        private StoryCompletionRule[] rules = Array.Empty<StoryCompletionRule>();

        private bool subscribed;

        private void Awake()
        {
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            director ??= FindObjectOfType<StoryDirector>();
            if (director == null)
            {
                return;
            }

            director.StoryFinished += OnStoryFinished;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (director != null)
            {
                director.StoryFinished -= OnStoryFinished;
            }

            subscribed = false;
        }

        private void OnStoryFinished(string storyId)
        {
            if (string.IsNullOrWhiteSpace(storyId) || rules == null)
            {
                return;
            }

            StoryCompletionRule match = null;
            int matchCount = 0;

            for (int index = 0; index < rules.Length; index++)
            {
                StoryCompletionRule rule = rules[index];
                if (rule == null || string.IsNullOrWhiteSpace(rule.finishedStoryId))
                {
                    continue;
                }

                if (!string.Equals(rule.finishedStoryId.Trim(), storyId.Trim(), StringComparison.Ordinal))
                {
                    continue;
                }

                matchCount++;
                if (match == null)
                {
                    match = rule;
                }
            }

            if (match == null)
            {
                return;
            }

            if (matchCount > 1)
            {
                Debug.LogWarning(
                    $"[Story] 剧情 {storyId} 匹配到 {matchCount} 条完成规则，只执行第一条。请把后续剧情合并到同一条规则的 nextStoryIds。",
                    this);
            }

            if (match.nextStoryIds == null || match.nextStoryIds.Length == 0)
            {
                return;
            }

            StartCoroutine(PlayAfterDelay(match));
        }

        private IEnumerator PlayAfterDelay(StoryCompletionRule rule)
        {
            if (rule.delaySeconds > 0f)
            {
                yield return new WaitForSeconds(rule.delaySeconds);
            }

            if (!isActiveAndEnabled || director == null)
            {
                yield break;
            }

            director.PlaySequence(rule.nextStoryIds);
        }

        private void OnValidate()
        {
            if (rules == null)
            {
                return;
            }

            for (int index = 0; index < rules.Length; index++)
            {
                StoryCompletionRule rule = rules[index];
                if (rule == null || string.IsNullOrWhiteSpace(rule.finishedStoryId))
                {
                    continue;
                }

                for (int otherIndex = index + 1; otherIndex < rules.Length; otherIndex++)
                {
                    StoryCompletionRule other = rules[otherIndex];
                    if (other == null || string.IsNullOrWhiteSpace(other.finishedStoryId))
                    {
                        continue;
                    }

                    if (string.Equals(rule.finishedStoryId.Trim(), other.finishedStoryId.Trim(), StringComparison.Ordinal))
                    {
                        Debug.LogWarning(
                            $"[Story] 完成规则列表里有多条规则监听同一个剧情：{rule.finishedStoryId}",
                            this);
                    }
                }
            }
        }
    }
}
