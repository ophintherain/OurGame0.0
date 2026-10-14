using System;
using UnityEngine;

namespace Spotlight.Battle
{
    public sealed class BossController : MonoBehaviour
    {
        [SerializeField] private CombatHealth health;
        [SerializeField] private BossEnergy energy;
        [SerializeField] private BossMovement movement;
        [SerializeField] private PlayerBattleController player;
        [SerializeField] private CoverController[] covers;
        [SerializeField, Min(0.1f)] private float normalAttackInterval = 3f;
        [SerializeField, Min(0.1f)] private float telegraphSeconds = 1f;
        [SerializeField, Min(0f)] private float normalAttackDamage = 15f;
        [SerializeField, Min(0f)] private float skillDamage = 30f;

        private float attackCooldown;
        private float telegraphRemaining;
        private bool telegraphActive;
        private BattleTelegraph pendingTelegraph;

        public event Action<BattleTelegraph> TelegraphStarted;
        public event Action<BattleTelegraph, DamageResolution> AttackResolved;

        public CombatHealth Health => health;
        public BossEnergy Energy => energy;
        public BattlePosition Position => movement != null ? movement.Position : default;
        public bool IsBattleActive { get; private set; }
        public bool HasActiveTelegraph => telegraphActive;
        public BattleTelegraph ActiveTelegraph => pendingTelegraph;

        public void Configure(CombatHealth bossHealth, BossEnergy bossEnergy, BossMovement bossMovement,
            PlayerBattleController battlePlayer, CoverController[] battleCovers)
        {
            health = bossHealth;
            energy = bossEnergy;
            movement = bossMovement;
            player = battlePlayer;
            covers = battleCovers ?? Array.Empty<CoverController>();
        }

        public void ConfigureAttacks(float interval, float warningSeconds, float basicDamage,
            float empoweredDamage)
        {
            normalAttackInterval = Mathf.Max(0.1f, interval);
            telegraphSeconds = Mathf.Max(0.1f, warningSeconds);
            normalAttackDamage = Mathf.Max(0f, basicDamage);
            skillDamage = Mathf.Max(0f, empoweredDamage);
        }

        public void BeginBattle()
        {
            IsBattleActive = true;
            attackCooldown = normalAttackInterval;
            telegraphActive = false;
            health?.ResetToFull();
            energy?.ResetEnergy();
            movement?.BeginBattle();
        }

        public void EndBattle()
        {
            IsBattleActive = false;
            telegraphActive = false;
        }

        public void NotifyDamageResolved(DamageResolution resolution)
        {
            energy?.AddFromDamage(resolution.AppliedDamage);
        }

        public void SimulateStep(float deltaTime)
        {
            if (!IsBattleActive || health == null || health.IsDead || player == null)
            {
                return;
            }

            float step = Mathf.Max(0f, deltaTime);
            energy?.SimulateStep(step);
            movement?.SimulateStep(step, player.Position.Column);

            if (telegraphActive)
            {
                telegraphRemaining -= step;
                if (telegraphRemaining <= 0f)
                {
                    ResolvePendingAttack();
                }
                return;
            }

            attackCooldown -= step;
            if (energy != null && energy.IsFull)
            {
                energy.TrySpendFull();
                StartAttack(true);
            }
            else if (attackCooldown <= 0f)
            {
                StartAttack(false);
            }
        }

        private void Update()
        {
            SimulateStep(Time.deltaTime);
        }

        private void StartAttack(bool empowered)
        {
            BattlePosition primary = player.Position;
            BattlePosition? secondary = null;
            DamageKind kind = DamageKind.Ranged;
            if (empowered)
            {
                int adjacentColumn = primary.Column < 7 ? primary.Column + 1 : primary.Column - 1;
                secondary = new BattlePosition(primary.Lane, Mathf.Max(0, adjacentColumn));
                kind = DamageKind.Area;
            }

            pendingTelegraph = new BattleTelegraph(primary, secondary, telegraphSeconds, kind);
            telegraphRemaining = telegraphSeconds;
            telegraphActive = true;
            attackCooldown = normalAttackInterval;
            TelegraphStarted?.Invoke(pendingTelegraph);
        }

        private void ResolvePendingAttack()
        {
            telegraphActive = false;
            if (!pendingTelegraph.Contains(player.Position))
            {
                AttackResolved?.Invoke(pendingTelegraph, new DamageResolution(0f, false, false));
                return;
            }

            CoverController cover = FindCover(player.Position);
            float damage = pendingTelegraph.DamageKind == DamageKind.Area ? skillDamage : normalAttackDamage;
            DamageResolution resolution = DamageResolver.Resolve(player.Health, damage,
                pendingTelegraph.DamageKind, player.Position, player.Posture, cover);
            AttackResolved?.Invoke(pendingTelegraph, resolution);
        }

        private CoverController FindCover(BattlePosition position)
        {
            if (covers == null)
            {
                return null;
            }

            foreach (CoverController cover in covers)
            {
                if (cover != null && cover.Position == position)
                {
                    return cover;
                }
            }

            return null;
        }
    }
}
