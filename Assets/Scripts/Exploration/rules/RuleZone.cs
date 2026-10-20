using System;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>规则怪谈使用的一段世界 X 坐标区间。</summary>
    [Serializable]
    public sealed class RuleZone
    {
        [Tooltip("区域边界之一（世界 X）。两个值谁大谁小都可以，判定时会自动取最小值和最大值")]
        public float minX = -1f;

        [Tooltip("区域另一边（世界 X）。配合 minX 形成一个水平区间")]
        public float maxX = 1f;

        /// <summary>世界 X 是否落在区间内。</summary>
        public bool Contains(float x)
        {
            float left = Mathf.Min(minX, maxX);
            float right = Mathf.Max(minX, maxX);
            return x >= left && x <= right;
        }
    }
}
