using System;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 一个可探索点的配置数据，直接在 Inspector 的列表里填写。
    /// 位置是世界坐标，可在 Scene 视图里用拖拽手柄自由摆放；
    /// 属于哪一排 / 哪一列由位置自动换算，不需要手动填。
    /// </summary>
    [Serializable]
    public sealed class ExplorationPoint
    {
        [Tooltip("交互点在场景里的世界坐标（X/Y）。选中 Interaction 物体后，可在 Scene 视图里拖动黄色手柄摆放")]
        public Vector2 position;

        [Tooltip("探索类型：Item=获得武器/物品，Story=触发剧情（本期只发事件，不做剧情表现）")]
        public ExplorationPointType type = ExplorationPointType.Item;

        [Tooltip("该交互点在世界里显示的图片立绘。留空时用格子大小的占位框代替，两种类型都适用")]
        public Sprite visual;

        [Range(0.01f, 10f)]
        [Tooltip("交互点立绘 / 占位框的等比缩放倍数，1 = 原始大小。点击范围和显示大小一起缩放")]
        public float visualScale = 1f;

        [Tooltip("自定义点击矩形的宽高（世界单位），可分别调整。两项都填 > 0 时按这个矩形判定，不再跟随立绘；留 0 的轴沿用立绘 / 地图格宽。不改变立绘本身的显示大小")]
        public Vector2 rectSize;

        [Tooltip("物品 ID，仅 Item 类型使用。必须 4 位数：1 开头=武器，2 开头=物品")]
        public int itemId = 1001;

        [Tooltip("物品在背包里显示的图标，仅 Item 类型使用，不影响场景里的交互点。场景立绘请填上面的 visual")]
        public Sprite icon;

        [Tooltip("物品在背包里显示的名称，仅 Item 类型使用")]
        public string displayName;

        [Tooltip("剧情标识，仅 Story 类型使用。点击后会通过 StoryTriggered 事件发出这个字符串")]
        public string storyId;
    }
}
