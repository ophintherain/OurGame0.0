using System;
using UnityEngine;

namespace Spotlight.Battle
{
    public sealed class PlayerBattleController : MonoBehaviour
    {
        [SerializeField] private BattleArena arena;
        [SerializeField] private CombatHealth health;
        [SerializeField] private BattleLane startingLane = BattleLane.Corridor;
        [SerializeField] private int startingColumn = 7;

        public event Action<BattlePosition, BattlePosition> PositionChanged;
        public event Action<PlayerPosture, PlayerPosture> PostureChanged;

        public BattlePosition Position { get; private set; }
        public PlayerPosture Posture { get; private set; } = PlayerPosture.Standing;
        public CombatHealth Health => health;
        public bool IsBattleActive { get; private set; }

        public void Configure(BattleArena battleArena, CombatHealth combatHealth,
            BattlePosition initialPosition)
        {
            arena = battleArena;
            health = combatHealth;
            startingLane = initialPosition.Lane;
            startingColumn = initialPosition.Column;
        }

        public void BeginBattle()
        {
            if (arena == null)
            {
                Debug.LogError("[Battle] PlayerBattleController has no BattleArena.", this);
                return;
            }

            IsBattleActive = true;
            Posture = PlayerPosture.Standing;
            SetPosition(arena.Clamp(new BattlePosition(startingLane, startingColumn)), true);
        }

        public void EndBattle()
        {
            IsBattleActive = false;
        }

        public bool TryMoveHorizontal(int direction)
        {
            if (!IsBattleActive || direction == 0)
            {
                return false;
            }

            BattlePosition next = arena.Clamp(new BattlePosition(Position.Lane,
                Position.Column + Math.Sign(direction)));
            if (Posture == PlayerPosture.OnTable && !arena.IsTablePosition(next))
            {
                return false;
            }
            return SetPosition(next, false);
        }

        public bool TryChangeLane(int direction)
        {
            if (!IsBattleActive || direction == 0 || Posture == PlayerPosture.OnTable)
            {
                return false;
            }

            int nextLaneValue = Mathf.Clamp((int)Position.Lane + Math.Sign(direction), 0, 2);
            BattlePosition next = new BattlePosition((BattleLane)nextLaneValue, Position.Column);
            return SetPosition(next, false);
        }

        public bool TrySetPosture(PlayerPosture next)
        {
            if (!IsBattleActive || Posture == next || !arena.IsPostureAllowed(Position, next))
            {
                return false;
            }

            PlayerPosture previous = Posture;
            Posture = next;
            ApplyVisualPose();
            PostureChanged?.Invoke(previous, next);
            return true;
        }

        public bool TryTogglePosture(PlayerPosture posture)
        {
            return TrySetPosture(Posture == posture ? PlayerPosture.Standing : posture);
        }

        private bool SetPosition(BattlePosition next, bool force)
        {
            if (!force && next == Position)
            {
                return false;
            }

            BattlePosition previous = Position;
            Position = next;
            transform.position = arena.GetWorldPosition(Position);
            PositionChanged?.Invoke(previous, Position);
            return true;
        }

        private void ApplyVisualPose()
        {
            Vector3 scale = Vector3.one;
            if (Posture == PlayerPosture.Prone)
            {
                scale = new Vector3(1.25f, 0.5f, 1f);
            }
            else if (Posture == PlayerPosture.OnTable)
            {
                scale = new Vector3(0.85f, 0.85f, 1f);
            }

            transform.localScale = scale;
        }
    }
}
