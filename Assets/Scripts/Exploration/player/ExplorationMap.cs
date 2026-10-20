using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 探索地图（横向若干格）的配置与坐标换算。
    /// 参数只在 Inspector 配置，运行期不做任何修改。
    /// 世界坐标公式：x = originX + 列号 * cellSpacing，y = groundY。
    /// </summary>
    public sealed class ExplorationMap : MonoBehaviour
    {
        [SerializeField, Min(2)]
        [Tooltip("横向格子总数，最小 2。改这里就是改地图长度")]
        private int cellCount = 9;

        [SerializeField, Min(0.01f)]
        [Tooltip("相邻格子的中心距离（世界单位）。决定格子疏密，也决定相机的半屏宽")]
        private float cellSpacing = 1.6f;

        [SerializeField]
        [Tooltip("第 0 格中心的 X。让 originX + (格数-1)×格间距÷2 ≈ 0 就是左右居中")]
        private float originX = -6.4f;

        [SerializeField]
        [Tooltip("地图所在高度，也是角色站立的 Y（角色 sprite 轴心在中心，脚底在 groundY − 0.7）")]
        private float groundY = -1.1f;

        [SerializeField]
        [Tooltip("哪些列是桌子格，只有这些格按 W 才能上桌。填列号，例如 0, 2, 4, 6, 8")]
        private int[] tableColumns = { 0, 2, 4, 6, 8 };

        /// <summary>格子总数。</summary>
        public int CellCount => cellCount;

        /// <summary>相邻格子的中心距离（世界单位）。</summary>
        public float CellSpacing => cellSpacing;

        /// <summary>第 0 格中心的 X。</summary>
        public float OriginX => originX;

        /// <summary>地图所在的高度。</summary>
        public float GroundY => groundY;

        /// <summary>地图横向左边界（最左格的外沿）。</summary>
        public float MapMinX => originX - cellSpacing * 0.5f;

        /// <summary>地图横向右边界（最右格的外沿）。</summary>
        public float MapMaxX => originX + (cellCount - 1) * cellSpacing + cellSpacing * 0.5f;

        /// <summary>列号是否在 0 ~ CellCount-1 之间。</summary>
        public bool IsValidColumn(int column)
        {
            return column >= 0 && column < cellCount;
        }

        /// <summary>把列号收敛到合法范围。</summary>
        public int ClampColumn(int column)
        {
            return Mathf.Clamp(column, 0, cellCount - 1);
        }

        /// <summary>列号对应的世界坐标。</summary>
        public Vector3 GetWorldPosition(int column)
        {
            return new Vector3(originX + ClampColumn(column) * cellSpacing, groundY, 0f);
        }

        /// <summary>该列是否是桌子格（只有桌子格允许上桌）。</summary>
        public bool IsTableColumn(int column)
        {
            if (tableColumns == null)
            {
                return false;
            }

            for (int index = 0; index < tableColumns.Length; index++)
            {
                if (tableColumns[index] == column)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnValidate()
        {
            cellCount = Mathf.Max(2, cellCount);
            cellSpacing = Mathf.Max(0.01f, cellSpacing);
        }

        private void OnDrawGizmosSelected()
        {
            Color cellColor = new Color(0.2f, 0.8f, 1f, 1f);
            Color tableColor = new Color(1f, 0.75f, 0.2f, 1f);
            const float cellHeight = 1.2f;
            float cellWidth = Mathf.Max(0.01f, cellSpacing);
            for (int column = 0; column < Mathf.Max(2, cellCount); column++)
            {
                // 线框宽度取 cellSpacing，相邻格紧挨着，方便直接跟背景图对齐
                Vector3 center = GetWorldPosition(column);
                Gizmos.color = IsTableColumn(column) ? tableColor : cellColor;
                Gizmos.DrawWireCube(center, new Vector3(cellWidth, cellHeight, 0f));
                Gizmos.DrawLine(center + Vector3.down * (cellHeight * 0.5f),
                    center + Vector3.up * (cellHeight * 0.5f));
            }
        }
    }
}
