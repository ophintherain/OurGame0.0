using System;
using UnityEngine;

namespace Spotlight.Battle
{
    public sealed class PlayerBattleController : MonoBehaviour
    {
        [SerializeField] private BattleArena arena;
        [SerializeField] private CombatHealth health;
        [SerializeField] private BattleLane startingLane = BattleLane.Corridor;
        [SerializeField] private int startingColumn = 8;
        [SerializeField] private float onTableVisualOffsetY = 2f;
        [SerializeField] private float backgroundTableVisualOffsetY = 1.5f;
        [SerializeField] private float proneVisualOffsetY = -0.3f;

        private SpriteRenderer spriteRenderer;
        private bool facingRight = true;
        private Vector3 artworkBaseScale;
        private const float ArtworkGroundOffsetY = 2.25f;
        private const float ForegroundArtworkDropY = -0.07f;
        private const float BackgroundArtworkDropY = 0.7f;
        private const float ArtworkFootOffsetY = 2.14f;
        private int standingSortingOrder;
        private float stunRemaining;
        private float invulnerabilityRemaining;
        private float jumpRemaining;
        private const float JumpDuration = 0.3f;
        private const float WalkDuration = 0.24f;
        private Vector3 jumpStart;
        private Vector3 jumpEnd;
        private float walkRemaining;
        private Vector3 walkStart;

        public event Action<BattlePosition, BattlePosition> PositionChanged;
        public event Action<PlayerPosture, PlayerPosture> PostureChanged;

        public BattlePosition Position { get; private set; }
        public PlayerPosture Posture { get; private set; } = PlayerPosture.Standing;
        public CombatHealth Health => health;
        public bool IsBattleActive { get; private set; }
        public bool CanAct => IsBattleActive && stunRemaining <= 0f && jumpRemaining <= 0f;
        public bool IsJumping => jumpRemaining > 0f;
        public bool IsInvulnerable => invulnerabilityRemaining > 0f;

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
            stunRemaining = 0f;
            invulnerabilityRemaining = 0f;
            jumpRemaining = 0f;
            walkRemaining = 0f;
            Posture = PlayerPosture.Standing;
            facingRight = true;
            CacheSpriteRenderer();
            SetPosition(arena.Clamp(new BattlePosition(startingLane, startingColumn)), true);
        }

        public void EndBattle()
        {
            IsBattleActive = false;
        }

        public void RefreshVisualPosition()
        {
            if (IsBattleActive && arena != null) ApplyVisualPose();
        }

        public void ApplyStun(float seconds)
        {
            stunRemaining = Mathf.Max(stunRemaining, seconds);
        }

        public void ApplyInvulnerability(float seconds)
        {
            invulnerabilityRemaining = Mathf.Max(invulnerabilityRemaining, seconds);
        }

        public void PushAwayFrom(int sourceColumn, int columns)
        {
            if (!IsBattleActive || arena == null || columns <= 0) return;
            jumpRemaining = 0f;
            walkRemaining = 0f;
            int direction = Position.Column >= sourceColumn ? 1 : -1;
            BattlePosition next = arena.Clamp(new BattlePosition(Position.Lane,
                Position.Column + direction * columns));
            if (next.Lane != BattleLane.Corridor &&
                arena.TryFindNearestTable(next.Lane, next.Column, out BattlePosition pushedTable))
                next = pushedTable;
            if (Posture == PlayerPosture.OnTable && !arena.IsTablePosition(next))
            {
                PlayerPosture previous = Posture;
                Posture = PlayerPosture.Standing;
                PostureChanged?.Invoke(previous, Posture);
            }
            if (!SetPosition(next, false)) ApplyVisualPose();
        }

        public void SimulateStep(float deltaTime)
        {
            float step = Mathf.Max(0f, deltaTime);
            stunRemaining = Mathf.Max(0f, stunRemaining - step);
            invulnerabilityRemaining = Mathf.Max(0f, invulnerabilityRemaining - step);
            if (jumpRemaining > 0f)
            {
                jumpRemaining = Mathf.Max(0f, jumpRemaining - step);
                ApplyVisualPose();
            }
            if (walkRemaining > 0f)
            {
                walkRemaining = Mathf.Max(0f, walkRemaining - step);
                ApplyVisualPose();
            }
        }

        private void Update()
        {
            if (IsBattleActive)
            {
                SimulateStep(Time.deltaTime);
            }
        }

        public bool TryMoveHorizontal(int direction)
        {
            if (!CanAct || direction == 0)
            {
                return false;
            }

            if (Position.Lane != BattleLane.Corridor)
            {
                if (!arena.TryFindNextTable(Position, direction, out BattlePosition landing))
                    return false;
                if (Posture != PlayerPosture.OnTable) return SetPosition(landing, false);
                Vector3 takeoff = transform.position;
                walkRemaining = 0f;
                if (!SetPosition(landing, false, false)) return false;
                jumpStart = takeoff;
                jumpEnd = transform.position;
                jumpRemaining = JumpDuration;
                ApplyVisualPose();
                return true;
            }
            BattlePosition next = arena.Clamp(new BattlePosition(Position.Lane,
                Position.Column + Math.Sign(direction)));
            return SetPosition(next, false);
        }

        public bool TryChangeLane(int direction)
        {
            if (!CanAct || direction == 0 || Posture == PlayerPosture.OnTable)
            {
                return false;
            }

            int nextLaneValue = Mathf.Clamp((int)Position.Lane + Math.Sign(direction), 0, 2);
            BattlePosition next = new BattlePosition((BattleLane)nextLaneValue, Position.Column);
            if (next.Lane != BattleLane.Corridor && !arena.IsTablePosition(next)) return false;
            return SetPosition(next, false);
        }

        public bool TrySetPosture(PlayerPosture next)
        {
            if (!CanAct || Posture == next || !arena.IsPostureAllowed(Position, next))
            {
                return false;
            }

            PlayerPosture previous = Posture;
            Posture = next;
            walkRemaining = 0f;
            ApplyVisualPose();
            PostureChanged?.Invoke(previous, next);
            return true;
        }

        public bool TryTogglePosture(PlayerPosture posture)
        {
            return TrySetPosture(Posture == posture ? PlayerPosture.Standing : posture);
        }

        private bool SetPosition(BattlePosition next, bool force, bool animate = true)
        {
            if (!force && next == Position)
            {
                return false;
            }

            BattlePosition previous = Position;
            Vector3 departure = transform.position;
            Position = next;
            if (!force && next.Column != previous.Column)
                facingRight = next.Column > previous.Column;
            if (!force && animate)
            {
                walkStart = departure;
                walkRemaining = WalkDuration;
            }
            ApplyVisualPose();
            PositionChanged?.Invoke(previous, Position);
            return true;
        }

        private void ApplyVisualPose()
        {
            Vector3 position = arena.GetWorldPosition(Position);
            Vector3 scale = Vector3.one;
            if (Posture == PlayerPosture.Prone)
            {
                scale = new Vector3(1.25f, 0.5f, 1f);
                position.y += proneVisualOffsetY;
            }
            else if (Posture == PlayerPosture.OnTable)
            {
                scale = new Vector3(0.85f, 0.85f, 1f);
                position.y += Position.Lane == BattleLane.Foreground
                    ? onTableVisualOffsetY : backgroundTableVisualOffsetY;
            }

            transform.position = position;
            if (walkRemaining > 0f)
            {
                float progress = 1f - walkRemaining / WalkDuration;
                transform.position = Vector3.Lerp(walkStart, position, progress * progress * (3f - 2f * progress));
            }
            if (jumpRemaining > 0f)
            {
                float progress = 1f - jumpRemaining / JumpDuration;
                transform.position = Vector3.Lerp(jumpStart, jumpEnd, progress) +
                    Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 0.9f);
            }
            transform.localScale = scale;
            CacheSpriteRenderer();
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = !facingRight;
                if (spriteRenderer.transform != transform)
                {
                    float perspectiveScale = Position.Lane switch
                    {
                        BattleLane.Foreground => 1.1f,
                        BattleLane.Background => 0.9f,
                        _ => 1f
                    };
                    spriteRenderer.transform.localScale = artworkBaseScale * perspectiveScale;
                    Vector3 artworkPosition = spriteRenderer.transform.localPosition;
                    float laneDrop = Position.Lane switch
                    {
                        BattleLane.Foreground => ForegroundArtworkDropY,
                        BattleLane.Background => BackgroundArtworkDropY,
                        _ => 0f
                    };
                    artworkPosition.y = ArtworkGroundOffsetY - laneDrop +
                        ArtworkFootOffsetY * (perspectiveScale - 1f);
                    spriteRenderer.transform.localPosition = artworkPosition;
                }
                spriteRenderer.sortingOrder = Posture == PlayerPosture.Prone
                    ? standingSortingOrder - 5
                    : standingSortingOrder;
            }
        }

        private void CacheSpriteRenderer()
        {
            if (spriteRenderer != null)
            {
                return;
            }

            foreach (SpriteRenderer candidate in GetComponentsInChildren<SpriteRenderer>())
            {
                if (!candidate.enabled) continue;
                spriteRenderer = candidate;
                break;
            }
            if (spriteRenderer != null)
            {
                standingSortingOrder = spriteRenderer.sortingOrder;
                artworkBaseScale = spriteRenderer.transform.localScale;
            }
        }
    }
}
