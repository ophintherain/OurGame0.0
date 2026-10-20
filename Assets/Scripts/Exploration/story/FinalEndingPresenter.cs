using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 终章场景表现：根据剧情行的 sceneTag 切换结局 CG，
    /// 黑屏行隐藏 CG，最后一句播完后只触发完成接口，不自动跳转。
    /// </summary>
    public sealed class FinalEndingPresenter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("场景里的 StoryDirector，用于监听当前行与播放结束")]
        private StoryDirector storyDirector;

        [SerializeField]
        [Tooltip("全屏结局 CG 图片（RawImage）。隐藏时露出相机的黑色背景")]
        private RawImage background;

        [SerializeField]
        [Tooltip("默认结局 CG，对应 sceneTag = 2001")]
        private Texture2D defaultTexture;

        [SerializeField]
        [Tooltip("后段结局 CG，对应 sceneTag = 20011")]
        private Texture2D finalTexture;

        [SerializeField]
        [Tooltip("黑屏行使用的 sceneTag，默认「黑屏」。匹配时隐藏 CG")]
        private string blackSceneTag = "黑屏";

        [SerializeField]
        [Tooltip("最后一句播完后触发，留给外部接返回开始页面 / 其他转场；这里不会自动加载场景")]
        private UnityEvent onFinished;

        /// <summary>剧情播放完成事件。</summary>
        public event Action Finished;

        private void Awake()
        {
            if (storyDirector == null)
            {
                storyDirector = FindObjectOfType<StoryDirector>();
            }
        }

        private void OnEnable()
        {
            if (storyDirector != null)
            {
                storyDirector.StoryStarted += OnStoryStarted;
                storyDirector.LineShown += OnLineShown;
                storyDirector.StoryFinished += OnStoryFinished;
            }
        }

        private void OnDisable()
        {
            if (storyDirector != null)
            {
                storyDirector.StoryStarted -= OnStoryStarted;
                storyDirector.LineShown -= OnLineShown;
                storyDirector.StoryFinished -= OnStoryFinished;
            }
        }

        private void Start()
        {
            ShowDefault();
        }

        private void OnStoryStarted(string storyId)
        {
            ShowDefault();
        }

        private void OnLineShown(StoryLine line, int lineIndex)
        {
            if (line == null || background == null)
            {
                return;
            }

            string tag = line.sceneTag;
            if (!string.IsNullOrEmpty(blackSceneTag) && tag == blackSceneTag)
            {
                background.enabled = false;
                return;
            }

            if (tag == "20011")
            {
                background.texture = finalTexture;
                background.enabled = finalTexture != null;
                return;
            }

            if (tag == "2001")
            {
                background.texture = defaultTexture;
                background.enabled = defaultTexture != null;
            }
        }

        private void OnStoryFinished(string storyId)
        {
            Debug.Log("[Final] ending_01 播放完成。", this);
            Finished?.Invoke();
            onFinished?.Invoke();
        }

        private void ShowDefault()
        {
            if (background == null)
            {
                return;
            }

            background.texture = defaultTexture;
            background.enabled = defaultTexture != null;
        }
    }
}
