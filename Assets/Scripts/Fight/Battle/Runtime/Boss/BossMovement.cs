using System;
using UnityEngine;

namespace Spotlight.Battle
{
    public sealed class BossMovement : MonoBehaviour
    {
        [SerializeField] private BattleArena arena;
        [SerializeField, Min(0.1f)] private float stepInterval = 1.5f;
        [SerializeField] private int startingColumn = 4;

        private float remaining;
        private float travelRemaining;
        private float travelDuration;
        private float travelArcHeight;
        private Vector3 travelStart;
        private Vector3 travelEnd;
        private const float WalkDuration = 0.38f;
        private const float KnockbackDuration = 0.5f;

        public BattlePosition Position { get; private set; }
        public int ColumnCount => arena != null ? arena.ColumnCount : 9;

        public void Configure(BattleArena battleArena, int initialColumn, float interval)
        {
            arena = battleArena;
            startingColumn = initialColumn;
            stepInterval = Mathf.Max(0.1f, interval);
        }

        public void BeginBattle()
        {
            remaining = stepInterval;
            travelRemaining = 0f;
            Position = arena.Clamp(new BattlePosition(BattleLane.Corridor, startingColumn));
            transform.position = arena.GetWorldPosition(Position);
        }

        private void Update()
        {
            if (travelRemaining <= 0f) return;
            travelRemaining = Mathf.Max(0f, travelRemaining - Time.deltaTime);
            float progress = 1f - travelRemaining / travelDuration;
            float eased = progress * progress * (3f - 2f * progress);
            transform.position = Vector3.Lerp(travelStart, travelEnd, eased) +
                Vector3.up * (Mathf.Sin(progress * Mathf.PI) * travelArcHeight);
        }

        public void SimulateStep(float deltaTime, int targetColumn)
        {
            if (arena == null)
            {
                return;
            }

            remaining -= Mathf.Max(0f, deltaTime);
            if (remaining > 0f)
            {
                return;
            }

            remaining = stepInterval;
            int direction = Math.Sign(targetColumn - Position.Column);
            if (direction != 0)
            {
                SetColumn(Position.Column + direction);
            }
        }

        public void PushAwayFrom(int sourceColumn, int columns)
        {
            if (arena == null || columns <= 0)
            {
                return;
            }

            int direction = Position.Column >= sourceColumn ? 1 : -1;
            SetColumn(Position.Column + direction * columns, KnockbackDuration, 0.9f);
            remaining = stepInterval;
        }

        private void SetColumn(int column, float duration = WalkDuration, float arcHeight = 0f)
        {
            BattlePosition next = arena.Clamp(new BattlePosition(BattleLane.Corridor, column));
            if (next == Position) return;
            travelStart = transform.position;
            Position = next;
            travelEnd = arena.GetWorldPosition(Position);
            travelDuration = duration;
            travelRemaining = duration;
            travelArcHeight = arcHeight;
        }
    }
}
