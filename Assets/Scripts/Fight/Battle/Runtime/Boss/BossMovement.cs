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

        public BattlePosition Position { get; private set; }

        public void Configure(BattleArena battleArena, int initialColumn, float interval)
        {
            arena = battleArena;
            startingColumn = initialColumn;
            stepInterval = Mathf.Max(0.1f, interval);
        }

        public void BeginBattle()
        {
            remaining = stepInterval;
            SetColumn(startingColumn);
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

        private void SetColumn(int column)
        {
            Position = arena.Clamp(new BattlePosition(BattleLane.Corridor, column));
            transform.position = arena.GetWorldPosition(Position);
        }
    }
}
