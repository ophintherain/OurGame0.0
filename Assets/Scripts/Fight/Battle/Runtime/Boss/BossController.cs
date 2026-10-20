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
        [SerializeField, Range(1, 3)] private int carriageIndex = 1;

        private float attackCooldown;
        private float telegraphRemaining;
        private bool telegraphActive;
        private BattleTelegraph pendingTelegraph;
        private float stunRemaining;
        private float invisibilityRemaining;
        private int strikesRemaining;
        private int bleedStacks;
        private float bleedTickRemaining;
        private bool crowSummoned;
        private float crowAttackCooldown;
        private float crowTelegraphRemaining;
        private int crowStrikesRemaining;
        private int crowAttackSequence;
        private BattleTelegraph crowTelegraph;
        private GameObject crowVisual;
        private SpriteRenderer[] bodyRenderers;
        private Color[] bodyColors;
        private WeaponController weapons;
        private WeaponSkill borrowedSkill;
        private WeaponSkill lastBorrowedSkill;
        private float borrowedNeedleRemaining;
        private float borrowedNeedleTickRemaining;
        private BattlePosition borrowedNeedlePosition;
        private int borrowedBarrageShots;
        private float borrowedBarrageTickRemaining;
        private float borrowedLeapRemaining;
        private float borrowedReflectRemaining;
        private float borrowedPoetRemaining;
        private float borrowedEdgeRemaining;
        private float borrowedRecorderRemaining;
        private float borrowedRecordedDamage;
        private bool reflectingDamage;
        private GameObject borrowedNeedleVisual;

        public event Action<BattleTelegraph> TelegraphStarted;
        public event Action<BattleTelegraph, DamageResolution> AttackResolved;

        public CombatHealth Health => health;
        public BossEnergy Energy => energy;
        public BattlePosition Position => movement != null ? movement.Position : default;
        public bool IsBattleActive { get; private set; }
        public bool HasActiveTelegraph => telegraphActive;
        public float StunRemaining => stunRemaining;
        public int CarriageIndex => carriageIndex;
        public bool IsInvisible => invisibilityRemaining > 0f;
        public bool HasCrow => crowSummoned;
        public bool HasCrowTelegraph => crowSummoned && crowTelegraphRemaining > 0f;
        public BattleTelegraph CrowTelegraph => crowTelegraph;
        public int BleedStacks => bleedStacks;
        public BossSkill LastSkill { get; private set; }
        public WeaponSkill BorrowedSkill => borrowedSkill;
        public WeaponSkill LastBorrowedSkill => lastBorrowedSkill;
        public bool IsBorrowedEdgeActive => borrowedEdgeRemaining > 0f;
        private bool manualSkillTesting;

        public void SetManualSkillTesting(bool enabled) => manualSkillTesting = enabled;

        public void SetBorrowedSkillForTesting(WeaponSkill skill) => borrowedSkill = skill;

        public void CastTripleStrikeForTesting()
        {
            if (!IsBattleActive || telegraphActive) return;
            LastSkill = BossSkill.TripleStrike;
            strikesRemaining = 3;
            StartAttack(true);
        }
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

        public void ConfigureCarriage(int carriage)
        {
            carriageIndex = Mathf.Clamp(carriage, 1, 3);
        }

        public void SetWeaponController(WeaponController controller)
        {
            weapons = controller;
        }

        public void BeginBattle()
        {
            IsBattleActive = true;
            attackCooldown = normalAttackInterval;
            telegraphActive = false;
            stunRemaining = 0f;
            invisibilityRemaining = 0f;
            strikesRemaining = 0;
            bleedStacks = 0;
            bleedTickRemaining = 1f;
            crowSummoned = false;
            crowAttackCooldown = 3f;
            crowTelegraphRemaining = 0f;
            crowStrikesRemaining = 0;
            crowAttackSequence = 0;
            borrowedNeedleRemaining = 0f;
            borrowedBarrageShots = 0;
            borrowedLeapRemaining = 0f;
            borrowedReflectRemaining = 0f;
            borrowedPoetRemaining = 0f;
            borrowedEdgeRemaining = 0f;
            borrowedRecorderRemaining = 0f;
            borrowedRecordedDamage = 0f;
            borrowedSkill = (WeaponSkill)(1 + (carriageIndex - 1) * 3 + UnityEngine.Random.Range(0, 3));
            lastBorrowedSkill = WeaponSkill.DirectDamage;
            LastSkill = BossSkill.None;
            UnsubscribeHealth();
            SubscribeHealth();
            SetBodyVisible(true);
            if (crowVisual != null) crowVisual.SetActive(false);
            if (borrowedNeedleVisual != null) borrowedNeedleVisual.SetActive(false);
            health?.ResetToFull();
            energy?.ResetEnergy();
            movement?.BeginBattle();
        }

        public void EndBattle()
        {
            IsBattleActive = false;
            telegraphActive = false;
            crowSummoned = false;
            crowTelegraphRemaining = 0f;
            SetBodyVisible(true);
            if (crowVisual != null) crowVisual.SetActive(false);
            if (borrowedNeedleVisual != null) borrowedNeedleVisual.SetActive(false);
            UnsubscribeHealth();
        }

        public void HideAfterDefeat()
        {
            SetBodyVisible(false);
            if (crowVisual != null) crowVisual.SetActive(false);
            if (borrowedNeedleVisual != null) borrowedNeedleVisual.SetActive(false);
        }

        public void NotifyDamageResolved(DamageResolution resolution)
        {
            energy?.AddFromDamage(resolution.AppliedDamage);
        }

        public void ApplyTruce(int sourceColumn, int knockbackColumns, float stunSeconds)
        {
            if (!IsBattleActive)
            {
                return;
            }

            movement?.PushAwayFrom(sourceColumn, knockbackColumns);
            stunRemaining = Mathf.Max(stunRemaining, stunSeconds);
            if (telegraphActive)
            {
                telegraphActive = false;
                strikesRemaining = 0;
                AttackResolved?.Invoke(pendingTelegraph, new DamageResolution(0f, false, false));
            }
            attackCooldown = Mathf.Max(attackCooldown, stunRemaining);
        }

        public void SimulateStep(float deltaTime)
        {
            if (!IsBattleActive || health == null || health.IsDead || player == null)
            {
                return;
            }

            float step = Mathf.Max(0f, deltaTime);
            TickPersistentEffects(step);
            TickCrow(step);
            if (stunRemaining > 0f)
            {
                stunRemaining = Mathf.Max(0f, stunRemaining - step);
                return;
            }
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

            if (manualSkillTesting) return;
            attackCooldown -= step * (borrowedLeapRemaining > 0f ? 1f / 0.7f : 1f);
            if (energy != null && energy.IsFull)
            {
                energy.TrySpendFull();
                StartSkill();
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
                int reach = borrowedPoetRemaining > 0f ? 2 : 1;
                int offset = UnityEngine.Random.Range(-reach, reach + 1);
                primary = new BattlePosition(primary.Lane,
                    Mathf.Clamp(primary.Column + offset, 0, movement.ColumnCount - 1));
                kind = DamageKind.Area;
            }

            float warning = telegraphSeconds * (borrowedLeapRemaining > 0f ? 0.7f : 1f);
            pendingTelegraph = new BattleTelegraph(primary, secondary, warning, kind);
            telegraphRemaining = warning;
            telegraphActive = true;
            attackCooldown = normalAttackInterval;
            TelegraphStarted?.Invoke(pendingTelegraph);
        }

        private void StartSkill()
        {
            if (UnityEngine.Random.Range(0, 3) == 0 && TryCastBorrowedSkill())
            {
                attackCooldown = normalAttackInterval;
                return;
            }

            if (carriageIndex == 1 && !crowSummoned && UnityEngine.Random.value < 0.5f)
            {
                SummonCrow();
                attackCooldown = normalAttackInterval;
                return;
            }

            if (carriageIndex == 2 && UnityEngine.Random.value < 0.5f)
            {
                ApplyInvisibility(8f);
                attackCooldown = normalAttackInterval;
                return;
            }

            LastSkill = BossSkill.TripleStrike;
            strikesRemaining = 3;
            StartAttack(true);
        }

        public bool TryCastBorrowedSkill()
        {
            if (!IsBattleActive) return false;
            WeaponSkill chosen = borrowedSkill;
            if (chosen == WeaponSkill.Mimic && weapons != null)
                chosen = weapons.LastUsedSkill;
            if (chosen == WeaponSkill.Mimic || chosen == WeaponSkill.DirectDamage)
                return false;

            switch (chosen)
            {
                case WeaponSkill.Truce:
                    player.PushAwayFrom(Position.Column, 2);
                    player.ApplyStun(2f);
                    break;
                case WeaponSkill.Needle:
                    borrowedNeedlePosition = player.Position;
                    borrowedNeedleRemaining = 3f;
                    borrowedNeedleTickRemaining = 1f;
                    ShowBorrowedNeedleZone();
                    break;
                case WeaponSkill.Barrage:
                    borrowedBarrageShots = 10;
                    borrowedBarrageTickRemaining = 0.5f;
                    break;
                case WeaponSkill.Leap:
                    borrowedLeapRemaining = 3f;
                    break;
                case WeaponSkill.Servitude:
                    borrowedReflectRemaining = 5f;
                    break;
                case WeaponSkill.Poet:
                    borrowedPoetRemaining = 5f;
                    break;
                case WeaponSkill.Edge:
                    borrowedEdgeRemaining = 5f;
                    break;
                case WeaponSkill.Recorder:
                    borrowedRecorderRemaining = 10f;
                    borrowedRecordedDamage = 0f;
                    break;
                default:
                    return false;
            }

            LastSkill = BossSkill.BorrowedWeapon;
            lastBorrowedSkill = chosen;
            return true;
        }

        public void SummonCrow()
        {
            if (!IsBattleActive || crowSummoned) return;
            crowSummoned = true;
            crowAttackCooldown = normalAttackInterval;
            crowAttackSequence = 0;
            LastSkill = BossSkill.SummonCrow;
            EnsureCrowVisual();
            crowVisual?.SetActive(true);
        }

        public void ApplyInvisibility(float seconds)
        {
            if (!IsBattleActive) return;
            invisibilityRemaining = Mathf.Max(invisibilityRemaining, seconds);
            LastSkill = BossSkill.Invisibility;
            SetBodyVisible(false);
        }

        private void ResolvePendingAttack()
        {
            telegraphActive = false;
            if (!pendingTelegraph.Contains(player.Position))
            {
                AttackResolved?.Invoke(pendingTelegraph, new DamageResolution(0f, false, false));
            }
            else
            {
                float damage = pendingTelegraph.DamageKind == DamageKind.Area
                    ? player.Health.MaxHealth * 0.1f : normalAttackDamage;
                bool isSkill = pendingTelegraph.DamageKind == DamageKind.Area;
                DamageResolution resolution = HitPlayer(damage, pendingTelegraph.DamageKind, isSkill);
                AttackResolved?.Invoke(pendingTelegraph, resolution);
            }

            if (strikesRemaining > 0 && IsBattleActive)
            {
                strikesRemaining--;
                if (strikesRemaining > 0) StartAttack(true);
            }
        }

        private DamageResolution HitPlayer(float damage, DamageKind kind, bool isSkill = true)
        {
            if (player.Health == null || player.IsInvulnerable)
                return new DamageResolution(0f, false, false);

            float multiplier = isSkill && weapons != null ? weapons.BossSkillDamageMultiplier : 1f;
            DamageResolution result = DamageResolver.Resolve(player.Health, damage * multiplier,
                kind, player.Position, player.Posture, FindCover(player.Position));
            if (carriageIndex == 3 && result.AppliedDamage > 0f) bleedStacks++;
            return result;
        }

        private void TickCrow(float step)
        {
            if (!crowSummoned || player.Health == null || player.Health.IsDead) return;
            if (crowTelegraphRemaining > 0f)
            {
                crowTelegraphRemaining -= step;
                if (crowTelegraphRemaining <= 0f)
                {
                    crowTelegraphRemaining = 0f;
                    if (crowTelegraph.Contains(player.Position))
                    {
                        float damage = crowTelegraph.DamageKind == DamageKind.Area
                            ? player.Health.MaxHealth * 0.04f : normalAttackDamage * 0.4f;
                        HitPlayer(damage, crowTelegraph.DamageKind);
                    }
                    if (crowStrikesRemaining > 0)
                    {
                        crowStrikesRemaining--;
                        if (crowStrikesRemaining > 0) StartCrowAttack(true);
                    }
                }
                return;
            }

            crowAttackCooldown -= step;
            if (crowAttackCooldown > 0f) return;
            crowAttackSequence++;
            bool triple = crowAttackSequence % 3 == 0;
            crowStrikesRemaining = triple ? 3 : 0;
            StartCrowAttack(triple);
            crowAttackCooldown = normalAttackInterval;
        }

        private void StartCrowAttack(bool empowered)
        {
            BattlePosition target = player.Position;
            if (empowered)
                target = new BattlePosition(target.Lane,
                    Mathf.Clamp(target.Column + UnityEngine.Random.Range(-1, 2), 0, movement.ColumnCount - 1));
            float warning = telegraphSeconds;
            crowTelegraph = new BattleTelegraph(target, null, warning,
                empowered ? DamageKind.Area : DamageKind.Ranged);
            crowTelegraphRemaining = warning;
            TelegraphStarted?.Invoke(crowTelegraph);
        }

        private void TickPersistentEffects(float step)
        {
            borrowedLeapRemaining = Mathf.Max(0f, borrowedLeapRemaining - step);
            borrowedReflectRemaining = Mathf.Max(0f, borrowedReflectRemaining - step);
            borrowedPoetRemaining = Mathf.Max(0f, borrowedPoetRemaining - step);
            borrowedEdgeRemaining = Mathf.Max(0f, borrowedEdgeRemaining - step);
            if (borrowedRecorderRemaining > 0f)
            {
                borrowedRecorderRemaining -= step;
                if (borrowedRecorderRemaining <= 0f)
                {
                    borrowedRecorderRemaining = 0f;
                    HitPlayer(borrowedRecordedDamage * 0.6f, DamageKind.Area);
                    borrowedRecordedDamage = 0f;
                }
            }

            if (borrowedNeedleRemaining > 0f)
            {
                float activeStep = Mathf.Min(step, borrowedNeedleRemaining);
                borrowedNeedleRemaining -= activeStep;
                borrowedNeedleTickRemaining -= activeStep;
                while (borrowedNeedleTickRemaining <= 0f && IsBattleActive)
                {
                    if (player.Position == borrowedNeedlePosition)
                        HitPlayer(player.Health.MaxHealth * 0.07f, DamageKind.Area);
                    borrowedNeedleTickRemaining += 1f;
                }
                if (borrowedNeedleRemaining <= 0f && borrowedNeedleVisual != null)
                    borrowedNeedleVisual.SetActive(false);
            }

            if (borrowedBarrageShots > 0)
            {
                borrowedBarrageTickRemaining -= step;
                while (borrowedBarrageShots > 0 && IsBattleActive && borrowedBarrageTickRemaining <= 0f)
                {
                    HitPlayer(player.Health.MaxHealth * 0.015f, DamageKind.Ranged);
                    borrowedBarrageShots--;
                    borrowedBarrageTickRemaining += 0.5f;
                }
            }

            if (invisibilityRemaining > 0f)
            {
                invisibilityRemaining = Mathf.Max(0f, invisibilityRemaining - step);
                if (invisibilityRemaining <= 0f) SetBodyVisible(true);
            }

            if (carriageIndex != 3 || bleedStacks <= 0 || player.Health == null) return;
            bleedTickRemaining -= step;
            while (bleedTickRemaining <= 0f && IsBattleActive)
            {
                if (!player.IsInvulnerable)
                {
                    float multiplier = weapons != null ? weapons.BossSkillDamageMultiplier : 1f;
                    player.Health.ApplyDamage(player.Health.MaxHealth * 0.007f * bleedStacks * multiplier);
                }
                bleedTickRemaining += 1f;
            }
        }

        private void SubscribeHealth()
        {
            if (health != null) health.HealthChanged += OnBossHealthChanged;
            if (player != null && player.Health != null) player.Health.HealthChanged += OnPlayerHealthChanged;
        }

        private void UnsubscribeHealth()
        {
            if (health != null) health.HealthChanged -= OnBossHealthChanged;
            if (player != null && player.Health != null) player.Health.HealthChanged -= OnPlayerHealthChanged;
        }

        private void OnBossHealthChanged(float previous, float current)
        {
            if (borrowedReflectRemaining <= 0f || current >= previous || reflectingDamage) return;
            reflectingDamage = true;
            HitPlayer((previous - current) * 0.7f, DamageKind.Area);
            reflectingDamage = false;
        }

        private void OnPlayerHealthChanged(float previous, float current)
        {
            if (borrowedRecorderRemaining > 0f && current < previous)
                borrowedRecordedDamage += previous - current;
        }

        private void EnsureCrowVisual()
        {
            if (crowVisual != null) return;
            SpriteRenderer source = GetComponentInChildren<SpriteRenderer>();
            if (source == null || source.sprite == null) return;
            crowVisual = new GameObject("CrowProxy");
            crowVisual.transform.SetParent(transform, false);
            crowVisual.transform.localPosition = new Vector3(0.7f, 1f, 0f);
            crowVisual.transform.localScale = Vector3.one * 0.35f;
            SpriteRenderer visual = crowVisual.AddComponent<SpriteRenderer>();
            visual.sprite = source.sprite;
            visual.color = Color.black;
            visual.sortingOrder = source.sortingOrder + 1;
        }

        private void ShowBorrowedNeedleZone()
        {
            if (borrowedNeedleVisual == null)
            {
                SpriteRenderer source = GetComponentInChildren<SpriteRenderer>();
                if (source == null || source.sprite == null) return;
                borrowedNeedleVisual = new GameObject("BossNeedleZoneProxy");
                SpriteRenderer visual = borrowedNeedleVisual.AddComponent<SpriteRenderer>();
                visual.sprite = source.sprite;
                visual.drawMode = SpriteDrawMode.Sliced;
                visual.size = new Vector2(1.3f, 1.1f);
                visual.color = new Color(1f, 0.15f, 0.15f, 0.45f);
                visual.sortingOrder = 3;
            }
            borrowedNeedleVisual.transform.position = player.transform.position;
            borrowedNeedleVisual.SetActive(true);
        }

        private void OnDisable()
        {
            UnsubscribeHealth();
            if (borrowedNeedleVisual != null)
            {
                if (Application.isPlaying) Destroy(borrowedNeedleVisual);
                else DestroyImmediate(borrowedNeedleVisual);
            }
        }

        private void SetBodyVisible(bool visible)
        {
            if (bodyRenderers == null)
            {
                bodyRenderers = GetComponentsInChildren<SpriteRenderer>(true);
                bodyColors = new Color[bodyRenderers.Length];
                for (int index = 0; index < bodyRenderers.Length; index++)
                    bodyColors[index] = bodyRenderers[index].color;
            }
            for (int index = 0; index < bodyRenderers.Length; index++)
            {
                if (bodyRenderers[index] == null || bodyRenderers[index].gameObject == crowVisual) continue;
                Color color = bodyColors[index];
                color.a = visible ? bodyColors[index].a : 0f;
                bodyRenderers[index].color = color;
            }
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
