using System;
using System.Collections;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 转场结束后的剧情触发器。挂在转场物体上，由转场动画 / Timeline / UnityEvent 调用 PlayAfterTransition()。
    /// 场景加载后的自动播放请直接用 StoryDirector.autoPlayOnStart，不需要这个组件。
    /// </summary>
    public sealed class StoryTransitionTrigger : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("剧情调度器。留空会自动在场景里查找 StoryDirector")]
        private StoryDirector director;

        [SerializeField]
        [Tooltip("转场结束后要按顺序播放的剧情ID列表。中间有探索 / 战斗时不要把它们放进同一个序列")]
        private string[] storyIds = Array.Empty<string>();

        [SerializeField, Min(0f)]
        [Tooltip("转场结束后延迟多少秒再播放，0 = 立即播放")]
        private float delaySeconds;

        /// <summary>由转场动画 / Timeline / UnityEvent 调用。</summary>
        public void PlayAfterTransition()
        {
            director ??= FindObjectOfType<StoryDirector>();
            if (director == null)
            {
                Debug.LogWarning("[Story] StoryTransitionTrigger 找不到 StoryDirector。", this);
                return;
            }

            if (storyIds == null || storyIds.Length == 0)
            {
                Debug.LogWarning("[Story] StoryTransitionTrigger 没有配置剧情ID。", this);
                return;
            }

            if (delaySeconds > 0f)
            {
                StartCoroutine(PlayAfterDelay());
            }
            else
            {
                director.PlaySequence(storyIds);
            }
        }

        private IEnumerator PlayAfterDelay()
        {
            yield return new WaitForSeconds(delaySeconds);
            director.PlaySequence(storyIds);
        }
    }
}
