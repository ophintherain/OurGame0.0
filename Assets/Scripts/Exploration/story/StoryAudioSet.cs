using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>音效 key 到 AudioClip 的映射项。</summary>
    [Serializable]
    public sealed class StoryAudioEntry
    {
        [Tooltip("音效 key，对应 Excel 的「音效」列（如 电话杂音 / 鸣笛声 / 滴水声）")]
        public string key;

        [Tooltip("该 key 对应的音频文件。留空时该音效静音")]
        public AudioClip clip;
    }

    /// <summary>
    /// 剧情音效映射表。换音频只改这里，不用改剧情资产。
    /// </summary>
    [CreateAssetMenu(menuName = "Spotlight/剧情音效集", fileName = "StoryAudioSet")]
    public sealed class StoryAudioSet : ScriptableObject
    {
        [Tooltip("音效 key → AudioClip 的映射列表。key 与剧情行里的 audioCue 对应")]
        public List<StoryAudioEntry> entries = new List<StoryAudioEntry>();

        private readonly Dictionary<string, AudioClip> map = new Dictionary<string, AudioClip>();
        private bool mapBuilt;

        private void OnEnable()
        {
            mapBuilt = false;
        }

        private void OnValidate()
        {
            mapBuilt = false;
        }

        /// <summary>按 key 取音效。key 为空或未配置时返回 false。</summary>
        public bool TryGet(string key, out AudioClip clip)
        {
            EnsureMap();
            if (string.IsNullOrWhiteSpace(key))
            {
                clip = null;
                return false;
            }

            return map.TryGetValue(key.Trim(), out clip) && clip != null;
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

                map.Add(key, entry.clip);
            }
        }
    }
}
