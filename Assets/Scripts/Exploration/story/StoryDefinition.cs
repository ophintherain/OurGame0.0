using System;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 一个可独立播放的剧情段落。同一 storyId 的表格行组成一个资产。
    /// 段落之间可以插入探索、拾取、广播或战斗，由玩法系统决定何时 Play。
    /// </summary>
    [CreateAssetMenu(menuName = "Spotlight/剧情定义", fileName = "Story")]
    public sealed class StoryDefinition : ScriptableObject
    {
        [Tooltip("段落唯一 ID，对应 Excel 的「剧情ID」列。触发源按这个 ID 播放，生成后不要随意改")]
        public string storyId;

        [Tooltip("章节 / 备注名，仅用于在 Inspector 里辨认，不参与运行逻辑")]
        public string chapterTitle;

        [Tooltip("勾选后本次运行只播放一次；取消勾选可重复触发")]
        public bool playOnce = true;

        [Tooltip("勾选后播放期间锁住玩家移动，并禁用背包 / 任务面板；广播和引导应取消勾选")]
        public bool lockPlayer = true;

        [Tooltip("勾选后播完自动调用任务面板的 CompleteObserveTask()，用于「观测怪异」任务")]
        public bool completeObserveTaskOnFinish;

        [Tooltip("按顺序播放的行。由导入工具生成，建议改 Excel 后重新导入，不要手改这里")]
        public StoryLine[] lines = Array.Empty<StoryLine>();

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(storyId))
            {
                Debug.LogWarning($"[Story] 资产 {name} 的 storyId 为空。", this);
            }

            if (lines == null || lines.Length == 0)
            {
                Debug.LogWarning($"[Story] 资产 {name} 没有任何剧情行。", this);
                return;
            }

            int emptyTextCount = 0;
            for (int index = 0; index < lines.Length; index++)
            {
                StoryLine line = lines[index];
                if (line == null)
                {
                    continue;
                }

                if (line.kind != StoryLineKind.Choice && string.IsNullOrWhiteSpace(line.text))
                {
                    emptyTextCount++;
                }
            }

            if (emptyTextCount > 0)
            {
                Debug.LogWarning($"[Story] 资产 {name} 有 {emptyTextCount} 行正文为空。", this);
            }
        }
    }
}
