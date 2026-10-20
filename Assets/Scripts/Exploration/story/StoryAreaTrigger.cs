using System;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>玩家走到某个列区间时播放指定剧情。</summary>
    [Serializable]
    public sealed class StoryAreaTrigger
    {
        [Tooltip("起始列号（含）。与结束列共同组成触发区间，列号对应 ExplorationMap 的格子")]
        public int startColumn;

        [Tooltip("结束列号（含）。小于起始列也可以，脚本会自动按大小排序")]
        public int endColumn;

        [Tooltip("进入该区间时要播放的剧情 ID，对应 Excel 的「剧情ID」列")]
        public string storyId;
    }
}
