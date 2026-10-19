using System;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>剧情行的表现类型。</summary>
    public enum StoryLineKind
    {
        /// <summary>底部对话框：角色名 + 正文 + 立绘。</summary>
        Dialogue,

        /// <summary>全屏黑底白字旁白。</summary>
        Narration,

        /// <summary>顶部居中引导提示。</summary>
        Tutorial,

        /// <summary>顶部广播 / 规则文本。</summary>
        Broadcast,

        /// <summary>顶部醒目警告。</summary>
        Warning,

        /// <summary>选项按钮组，点击任一选项后继续。</summary>
        Choice
    }

    /// <summary>
    /// 一条剧情行。由 Excel 导入工具生成，运行时只读。
    /// </summary>
    [Serializable]
    public sealed class StoryLine
    {
        [Tooltip("表现类型：Dialogue=底部对话，Narration=黑屏旁白，Tutorial=顶部引导，Broadcast=顶部广播，Warning=顶部警告，Choice=选项组")]
        public StoryLineKind kind;

        [Tooltip("角色名或特殊对象名。底部对话时显示在角色名位置；旁白填「旁白」；选项行留空")]
        public string speaker;

        [TextArea(2, 6)]
        [Tooltip("正文。Choice 行此字段留空，选项文字放在 choices 里")]
        public string text;

        [Tooltip("选项文字列表，仅 Choice 类型使用。点击任一选项后继续到下一行")]
        public string[] choices = Array.Empty<string>();

        [Tooltip("立绘 key，由 StoryPortraitSet 映射到 Sprite。留空则不显示立绘")]
        public string portraitKey;

        [Tooltip("音效 key，由 StoryAudioSet 映射到 AudioClip。留空则不播放音效")]
        public string audioCue;

        [Tooltip("Excel 的「演出备注」原文，本期只记录不执行，方便后续接演出效果")]
        public string stageNote;

        [Tooltip("Excel 的「场景」原文（黑屏 / 月台 / 1001 / 1002 / 1003 / 2001），本期只记录")]
        public string sceneTag;

        [Tooltip("Excel 的「需求备注」原文（规则编号等），本期只记录")]
        public string note;
    }
}
