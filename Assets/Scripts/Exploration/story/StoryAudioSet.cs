using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>音效里的一层音频：音频文件 + 这一层单独的音量。</summary>
    [Serializable]
    public sealed class StoryAudioClip
    {
        [Tooltip("音频文件。留空时这一层跳过")]
        public AudioClip clip;

        [Range(0f, 1f)]
        [Tooltip("这一层单独的音量，用于平衡叠加时各层的响度；最终音量 = 它 × 该音效的 volume × StoryDirector 的 audioVolume")]
        public float volume = 1f;
    }

    /// <summary>音效 key 到音频层的映射项。</summary>
    [Serializable]
    public sealed class StoryAudioEntry
    {
        [Tooltip("音效 key，对应 Excel 的「音效」列（如 电话杂音 / 鸣笛声 / 滴水声）")]
        public string key;

        [Tooltip("该 key 对应的音频层，可以填多个：播放这句时会同时叠加播放，整组时长取其中最短的一段。每层可以单独调音量")]
        public StoryAudioClip[] clips = Array.Empty<StoryAudioClip>();

        [Range(0f, 1f)]
        [Tooltip("该音效的整体音量，会同时缩放组内所有层；最终每层音量 = 这里的值 × 该层 volume × StoryDirector 的 audioVolume")]
        public float volume = 1f;

        [Tooltip("勾选后作为环境音：整组音频按最短时长循环播放，切换时淡入淡出；不勾则整组只播一遍，到最短时长结束")]
        public bool isAmbient;

        [Tooltip("该环境音淡入的秒数。0 = 立刻满音量；填 -1 或更小 = 使用 StoryDirector 的全局淡入淡出时长。仅对勾了 isAmbient 的音效生效")]
        public float fadeIn = -1f;

        [Tooltip("该环境音被下一句替换 / 段落结束时的淡出秒数。0 = 立刻停止；填 -1 或更小 = 使用 StoryDirector 的全局淡入淡出时长。仅对勾了 isAmbient 的音效生效")]
        public float fadeOut = -1f;
    }

    /// <summary>
    /// 剧情音效映射表。换音频只改这里，不用改剧情资产。
    /// </summary>
    [CreateAssetMenu(menuName = "Spotlight/剧情音效集", fileName = "StoryAudioSet")]
    public sealed class StoryAudioSet : ScriptableObject
    {
        [Tooltip("音效 key → AudioClip 的映射列表。key 与剧情行里的 audioCue 对应")]
        public List<StoryAudioEntry> entries = new List<StoryAudioEntry>();

        [Tooltip("在剧情表的「音效」列填这个词，表示在这一句停止当前正在播放的环境音（例如填「停止」）。留空则关闭这个指令")]
        public string stopAmbientKey = "停止";

        private readonly Dictionary<string, StoryAudioEntry> map = new Dictionary<string, StoryAudioEntry>();
        private bool mapBuilt;

        private void OnEnable()
        {
            mapBuilt = false;
        }

        private void OnValidate()
        {
            mapBuilt = false;
        }

        /// <summary>判断这条 audioCue 是否是「停止环境音」指令。</summary>
        public bool IsStopAmbientCue(string key)
        {
            return !string.IsNullOrWhiteSpace(stopAmbientKey) &&
                   !string.IsNullOrWhiteSpace(key) &&
                   string.Equals(key.Trim(), stopAmbientKey.Trim(), StringComparison.Ordinal);
        }

        /// <summary>按 key 取完整配置项（含音量、环境音标记与淡入淡出）。key 为空或未配置时返回 false。</summary>
        public bool TryGetEntry(string key, out StoryAudioEntry entry)
        {
            EnsureMap();
            entry = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return map.TryGetValue(key.Trim(), out entry);
        }

        private void EnsureMap()
        {
            if (mapBuilt)
            {
                return;
            }

            map.Clear();
            mapBuilt = true;

            if (entries == null)
            {
                return;
            }

            for (int index = 0; index < entries.Count; index++)
            {
                StoryAudioEntry entry = entries[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                {
                    continue;
                }

                string key = entry.key.Trim();
                if (map.ContainsKey(key))
                {
                    Debug.LogWarning($"[Story] 音效集 {name} 存在重复 key：{key}", this);
                    continue;
                }

                map.Add(key, entry);
            }
        }
    }
}
