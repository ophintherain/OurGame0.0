using System;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>任务完成时播放指定剧情。</summary>
    [Serializable]
    public sealed class StoryTaskTrigger
    {
        [Tooltip("任务序号：0 = 收集武器，1 = 观测怪异。与 ExplorationTaskPanel.TaskCompleted 的参数对应")]
        public int taskIndex;

        [Tooltip("该任务完成时要播放的剧情 ID，对应 Excel 的「剧情ID」列")]
        public string storyId;
    }
}
