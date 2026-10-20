using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>立绘 key 到 Sprite 的映射项。</summary>
    [Serializable]
    public sealed class StoryPortraitEntry
    {
        [Tooltip("立绘 key，对应剧情行里的 portraitKey（如 玩家 / 狄斯 / ？？）")]
        public string key;

        [Tooltip("该 key 对应的立绘图片。留空时该角色不显示立绘")]
        public Sprite sprite;
    }

    /// <summary>
    /// 剧情立绘映射表。换美术只改这里，不用改剧情资产。
    /// </summary>
    [CreateAssetMenu(menuName = "Spotlight/剧情立绘集", fileName = "StoryPortraitSet")]
    public sealed class StoryPortraitSet : ScriptableObject
    {
        [Tooltip("立绘 key → Sprite 的映射列表。key 与剧情行里的 portraitKey 对应")]
        public List<StoryPortraitEntry> entries = new List<StoryPortraitEntry>();

        private readonly Dictionary<string, Sprite> map = new Dictionary<string, Sprite>();
        private bool mapBuilt;

        private void OnEnable()
        {
            mapBuilt = false;
        }

        private void OnValidate()
        {
            mapBuilt = false;
        }

        /// <summary>按 key 取立绘。key 为空或未配置时返回 false。</summary>
        public bool TryGet(string key, out Sprite sprite)
        {
            EnsureMap();
            if (string.IsNullOrWhiteSpace(key))
            {
                sprite = null;
                return false;
            }

            return map.TryGetValue(key.Trim(), out sprite) && sprite != null;
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
                StoryPortraitEntry entry = entries[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                {
                    continue;
                }

                string key = entry.key.Trim();
                if (map.ContainsKey(key))
                {
                    Debug.LogWarning($"[Story] 立绘集 {name} 存在重复 key：{key}", this);
                    continue;
                }

                map.Add(key, entry.sprite);
            }
        }
    }
}
