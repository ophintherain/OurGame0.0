using UnityEngine;

namespace Spotlight.Battle
{
    public sealed class BattleArena : MonoBehaviour
    {
        [SerializeField, Min(2)] private int columnCount = 9;
        [SerializeField, Min(0.1f)] private float columnSpacing = 1.8f;
        [SerializeField] private float originX = -7.2f;
        [SerializeField] private float foregroundY = -3.8f;
        [SerializeField] private float corridorY = -2.25f;
        [SerializeField] private float backgroundY = -0.85f;
        [SerializeField] private int escapeColumn;
        [SerializeField] private CoverController[] tables;
        private float visualScaleX = 1f;
        private float visualScaleY = 1f;

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

        public void SetVisualScale(float horizontal, float vertical)
        {
            visualScaleX = Mathf.Max(0.01f, horizontal);
            visualScaleY = Mathf.Max(0.01f, vertical);
        }

        public BattlePosition Clamp(BattlePosition position)
        {
            BattleLane lane = (BattleLane)Mathf.Clamp((int)position.Lane, 0, 2);
            return new BattlePosition(lane, Mathf.Clamp(position.Column, 0, columnCount - 1));
        }

        public bool TryFindNextTable(BattlePosition current, int direction, out BattlePosition next)
        {
            next = current;
            if (direction == 0 || current.Lane == BattleLane.Corridor) return false;
            int step = direction > 0 ? 1 : -1;
            for (int column = current.Column + step; column >= 0 && column < columnCount; column += step)
            {
                BattlePosition candidate = new BattlePosition(current.Lane, column);
                if (!IsTablePosition(candidate)) continue;
                next = candidate;
                return true;
            }
            return false;
        }

        public bool TryFindNearestTable(BattleLane lane, int column, out BattlePosition nearest)
        {
            nearest = default;
            if (lane == BattleLane.Corridor || tables == null) return false;
            int shortestDistance = int.MaxValue;
            foreach (CoverController table in tables)
            {
                if (table == null || table.Position.Lane != lane) continue;
                int distance = Mathf.Abs(table.Position.Column - column);
                if (distance >= shortestDistance) continue;
                shortestDistance = distance;
                nearest = table.Position;
            }
            return shortestDistance != int.MaxValue;
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
            if (clamped.Lane != BattleLane.Corridor && tables != null)
            {
                foreach (CoverController table in tables)
                {
                    if (table != null && table.Position == clamped)
                        return table.transform.position;
                }
            }
            float x = clamped.Lane == BattleLane.Corridor
                ? GetCorridorLocalX(clamped.Column)
                : originX + clamped.Column * columnSpacing;
            return new Vector3(x * visualScaleX,
                y * visualScaleY, 0f);
        }

        private float GetCorridorLocalX(int column)
        {
            if (column > 0 && column < columnCount - 1 && column % 2 != 0)
                return (GetCorridorAnchorX(column - 1) + GetCorridorAnchorX(column + 1)) * 0.5f;
            return GetCorridorAnchorX(column);
        }

        private float GetCorridorAnchorX(int column)
        {
            float frontX = 0f;
            float backX = 0f;
            bool hasFront = false;
            bool hasBack = false;
            if (tables != null)
            {
                foreach (CoverController table in tables)
                {
                    if (table == null || table.Position.Column != column) continue;
                    if (table.Position.Lane == BattleLane.Foreground)
                    {
                        frontX = table.transform.localPosition.x;
                        hasFront = true;
                    }
                    else if (table.Position.Lane == BattleLane.Background)
                    {
                        backX = table.transform.localPosition.x;
                        hasBack = true;
                    }
                }
            }
            return hasFront && hasBack ? (frontX + backX) * 0.5f
                : originX + column * columnSpacing;
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
