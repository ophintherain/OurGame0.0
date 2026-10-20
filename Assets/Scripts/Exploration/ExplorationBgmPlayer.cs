using System.Collections;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 单个探索场景（车厢）的全局背景音乐。
    /// 挂在场景里的任意物体上，进入场景自动播放；不使用 DontDestroyOnLoad，
    /// 切换场景时随场景一起停止，所以每个车厢可以配不同的音乐。
    /// </summary>
    public sealed class ExplorationBgmPlayer : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("该车厢的背景音乐文件。留空则不播放")]
        private AudioClip clip;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("背景音乐音量 0~1。0 = 静音，1 = 原始音量；建议低于剧情音效，避免盖住对白")]
        private float volume = 0.45f;

        [SerializeField, Min(0f)]
        [Tooltip("进入场景时淡入的秒数。0 = 立即满音量")]
        private float fadeInSeconds = 1f;

        [SerializeField]
        [Tooltip("是否循环播放。背景音乐一般保持勾选")]
        private bool loop = true;

        private AudioSource source;
        private Coroutine fade;

        /// <summary>当前背景音乐音量。</summary>
        public float CurrentVolume => volume;

        /// <summary>运行时修改音量（例如电视切换档位）；会中断淡入并立即生效。</summary>
        public void SetVolume(float value)
        {
            if (fade != null)
            {
                StopCoroutine(fade);
                fade = null;
            }

            volume = Mathf.Clamp01(value);

            if (source != null)
            {
                source.volume = volume;
            }
        }

        private void Awake()
        {
            if (clip == null)
            {
                return;
            }

            source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.Play();

            if (fadeInSeconds > 0f)
            {
                fade = StartCoroutine(FadeInRoutine());
            }
            else
            {
                source.volume = volume;
            }
        }

        private void OnDestroy()
        {
            if (fade != null)
            {
                StopCoroutine(fade);
                fade = null;
            }
        }

        private IEnumerator FadeInRoutine()
        {
            for (float elapsed = 0f; elapsed < fadeInSeconds; elapsed += Time.unscaledDeltaTime)
            {
                if (source == null)
                {
                    yield break;
                }

                source.volume = Mathf.Lerp(0f, volume, elapsed / fadeInSeconds);
                yield return null;
            }

            if (source != null)
            {
                source.volume = volume;
            }

            fade = null;
        }
    }
}
