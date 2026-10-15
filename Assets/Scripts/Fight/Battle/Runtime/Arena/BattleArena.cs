using UnityEngine;

namespace Spotlight.Battle
{
    public sealed class BattleArena : MonoBehaviour
    {
        [SerializeField, Min(2)] private int columnCount = 8;
        [SerializeField, Min(0.1f)] private float columnSpacing = 1.6f;
        [SerializeField] private float originX = -5.6f;
        [SerializeField] private float foregroundY = -2.4f;
        [SerializeField] private float corridorY = -0.8f;
        [SerializeField] private float backgroundY = 0.8f;
        [SerializeField] private int escapeColumn;
        [SerializeField] private CoverController[] tables;

        public int ColumnCount => columnCount;
        public int EscapeColumn => Mathf.Clamp(escapeColumn, 0, columnCount - 1);

        public void Configure(int columns, float spacing, float startX, int exitColumn = 0)
        {
            columnCount = Mathf.Max(2, columns);
            columnSpacing = Mathf.Max(0.1f, spacing);
            originX = startX;
            escapeColumn = Mathf.Clamp(exitColumn, 0, columnCount - 1);
        }

        public void ConfigureTables(CoverController[] tableCovers)
        {
            tables = tableCovers ?? System.Array.Empty<CoverController>();
        }

        public BattlePosition Clamp(BattlePosition position)
        {
            BattleLane lane = (BattleLane)Mathf.Clamp((int)position.Lane, 0, 2);
            return new BattlePosition(lane, Mathf.Clamp(position.Column, 0, columnCount - 1));
        }

        public Vector3 GetWorldPosition(BattlePosition position)
        {
            BattlePosition clamped = Clamp(position);
            float y = clamped.Lane switch
            {
                BattleLane.Foreground => foregroundY,
                BattleLane.Background => backgroundY,
                _ => corridorY
            };
            return new Vector3(originX + clamped.Column * columnSpacing, y, 0f);
        }

        public bool IsPostureAllowed(BattlePosition position, PlayerPosture posture)
        {
            if (position.Lane == BattleLane.Corridor)
            {
                return posture == PlayerPosture.Standing || posture == PlayerPosture.Prone;
            }

            return posture == PlayerPosture.Standing || posture == PlayerPosture.Prone ||
                   posture == PlayerPosture.OnTable && IsTablePosition(position);
        }

        public bool IsTablePosition(BattlePosition position)
        {
            if (tables == null)
            {
                return false;
            }

            foreach (CoverController table in tables)
            {
                if (table != null && table.Position == position)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsEscapePosition(BattlePosition position)
        {
            return position.Lane == BattleLane.Corridor && position.Column == EscapeColumn;
        }

        public int ColumnDistance(BattlePosition first, BattlePosition second)
        {
            return Mathf.Abs(first.Column - second.Column);
        }
    }
}
