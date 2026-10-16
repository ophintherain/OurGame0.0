using System;
using UnityEngine;

namespace Spotlight.Battle
{
    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition[] availableWeapons;
        [SerializeField] private bool[] selectedWeapons;
        [SerializeField] private bool[] ownedWeapons;
        [SerializeField] private PlayerBattleController player;
        [SerializeField] private BossController boss;
        [SerializeField] private BattleArena arena;
        [SerializeField, Range(1, 3)] private int carriageIndex = 1;

        private readonly BattleLoadout loadout = new BattleLoadout();
        private float needleRemaining;
        private float needleTickRemaining;
        private BattlePosition needlePosition;
        private int barrageShotsRemaining;
        private float barrageTickRemaining;
        private float leapRemaining;
        private float servitudeRemaining;
        private float poetRemaining;
        private float edgeRemaining;
        private float recorderRemaining;
        private float recordedDamage;
        private bool alliedCrow;
        private float alliedCrowTickRemaining;
        private bool reflectingDamage;
        private WeaponSkill lastUsedSkill = WeaponSkill.DirectDamage;
        private GameObject needleZoneVisual;
        private GameObject alliedCrowVisual;

        public event Action<int, DamageResolution> WeaponUsed;

        public BattleLoadout Loadout => loadout;
        public System.Collections.Generic.IReadOnlyList<WeaponDefinition> AvailableWeapons => availableWeapons;
        public int CarriageIndex => carriageIndex;
        public float SharedSkillDamageMultiplier => edgeRemaining > 0f ||
            (boss != null && boss.IsBorrowedEdgeActive) ? 1.5f : 1f;
        public WeaponSkill LastUsedSkill => lastUsedSkill;
        public float NeedleRemaining => needleRemaining;
        public BattlePosition NeedlePosition => needlePosition;
        public int BarrageShotsRemaining => barrageShotsRemaining;
        public float RecorderRemaining => recorderRemaining;
        public float RecordedDamage => recordedDamage;
        public bool HasAlliedCrow => alliedCrow;
        public float LeapRemaining => leapRemaining;
        public float ServitudeRemaining => servitudeRemaining;
        public float PoetRemaining => poetRemaining;
        public float EdgeRemaining => edgeRemaining;

        private void Awake()
        {
            EnsureInventoryInitialized();
        }

        public void Configure(WeaponDefinition weapon, PlayerBattleController battlePlayer,
            BossController battleBoss, BattleArena battleArena)
        {
            Configure(new[] { weapon }, battlePlayer, battleBoss, battleArena);
        }

        public void Configure(WeaponDefinition[] weapons, PlayerBattleController battlePlayer,
            BossController battleBoss, BattleArena battleArena)
        {
            availableWeapons = weapons ?? Array.Empty<WeaponDefinition>();
            selectedWeapons = new bool[availableWeapons.Length];
            ownedWeapons = new bool[availableWeapons.Length];
            for (int index = 0; index < selectedWeapons.Length && index < BattleLoadout.MaximumWeapons; index++)
            {
                ownedWeapons[index] = availableWeapons[index] != null;
                selectedWeapons[index] = availableWeapons[index] != null;
            }
            for (int index = BattleLoadout.MaximumWeapons; index < ownedWeapons.Length; index++)
            {
                ownedWeapons[index] = availableWeapons[index] != null;
            }
            player = battlePlayer;
            boss = battleBoss;
            arena = battleArena;
        }

        public void ConfigureCarriage(int carriage)
        {
            carriageIndex = Mathf.Clamp(carriage, 1, 3);
        }

        public void BeginBattle()
        {
            needleRemaining = 0f;
            barrageShotsRemaining = 0;
            leapRemaining = 0f;
            servitudeRemaining = 0f;
            poetRemaining = 0f;
            edgeRemaining = 0f;
            recorderRemaining = 0f;
            recordedDamage = 0f;
            alliedCrow = false;
            alliedCrowTickRemaining = 3f;
            if (needleZoneVisual != null) needleZoneVisual.SetActive(false);
            if (alliedCrowVisual != null) alliedCrowVisual.SetActive(false);
            lastUsedSkill = WeaponSkill.DirectDamage;
            UnsubscribeHealth();
            SubscribeHealth();
            EnsureInventoryInitialized();
            loadout.Clear();
            for (int index = 0; index < availableWeapons.Length; index++)
            {
                if (IsSelected(index))
                {
                    loadout.TryAdd(availableWeapons[index]);
                }
            }
        }

        public void EndBattle()
        {
            needleRemaining = 0f;
            barrageShotsRemaining = 0;
            recorderRemaining = 0f;
            leapRemaining = 0f;
            servitudeRemaining = 0f;
            poetRemaining = 0f;
            edgeRemaining = 0f;
            alliedCrow = false;
            if (needleZoneVisual != null) needleZoneVisual.SetActive(false);
            if (alliedCrowVisual != null) alliedCrowVisual.SetActive(false);
            UnsubscribeHealth();
        }

        public bool IsSelected(int index)
        {
            return index >= 0 && index < selectedWeapons.Length && selectedWeapons[index];
        }

        public bool IsOwned(int index)
        {
            return index >= 0 && index < ownedWeapons.Length && ownedWeapons[index] &&
                   availableWeapons[index] != null &&
                   availableWeapons[index].AvailableFromCarriage <= carriageIndex;
        }

        public bool TryToggleSelection(int index)
        {
            if (index < 0 || index >= availableWeapons.Length || availableWeapons[index] == null ||
                !IsOwned(index))
            {
                return false;
            }

            if (selectedWeapons[index])
            {
                int selectedCount = 0;
                foreach (bool selected in selectedWeapons) if (selected) selectedCount++;
                if (selectedCount <= 1) return false;
                selectedWeapons[index] = false;
                return true;
            }

            int count = 0;
            foreach (bool selected in selectedWeapons) if (selected) count++;
            if (count >= BattleLoadout.MaximumWeapons) return false;
            selectedWeapons[index] = true;
            return true;
        }

        public WeaponDefinition RemoveWeaponAt(int slot)
        {
            WeaponDefinition removed = loadout.RemoveAt(slot);
            if (removed == null) return null;
            for (int index = 0; index < availableWeapons.Length; index++)
            {
                if (availableWeapons[index] == removed)
                {
                    selectedWeapons[index] = false;
                    ownedWeapons[index] = false;
                    break;
                }
            }
            return removed;
        }

        public bool TryUse(int slot)
        {
            if (player == null || boss == null || arena == null || !player.CanAct ||
                player.Posture != PlayerPosture.Standing || slot < 0 || slot >= loadout.Weapons.Count)
            {
                return false;
            }

            WeaponRuntime weapon = loadout.Weapons[slot];
            int effectiveRange = weapon.Definition.RangeColumns * (poetRemaining > 0f ? 2 : 1);
            if (!weapon.IsReady || (RequiresTarget(weapon.Definition.Skill) &&
                (boss.IsInvisible || arena.ColumnDistance(player.Position, boss.Position) > effectiveRange)))
            {
                return false;
            }

            if (weapon.Definition.Skill == WeaponSkill.Mimic && boss.LastSkill == BossSkill.None)
                return false;
            if (!weapon.TryConsumeUse())
            {
                return false;
            }

            DamageResolution resolution = new DamageResolution(0f, false, false);
            switch (weapon.Definition.Skill)
            {
                case WeaponSkill.Truce:
                    boss.ApplyTruce(player.Position.Column, 2, 2f * HasteFactor);
                    break;
                case WeaponSkill.Needle:
                    needlePosition = boss.Position;
                    needleRemaining = 3f;
                    needleTickRemaining = HasteFactor;
                    ShowNeedleZone();
                    break;
                case WeaponSkill.Barrage:
                    barrageShotsRemaining = 10;
                    barrageTickRemaining = 0.5f * HasteFactor;
                    break;
                case WeaponSkill.Leap:
                    leapRemaining = 3f;
                    break;
                case WeaponSkill.Mimic:
                    if (!ApplyMimic()) return false;
                    break;
                case WeaponSkill.Servitude:
                    servitudeRemaining = 5f;
                    break;
                case WeaponSkill.Poet:
                    poetRemaining = 5f;
                    break;
                case WeaponSkill.Edge:
                    edgeRemaining = 5f;
                    break;
                case WeaponSkill.Recorder:
                    recorderRemaining = 10f;
                    recordedDamage = 0f;
                    break;
                case WeaponSkill.DirectDamage:
                    resolution = ApplySkillDamage(weapon.Definition.Damage);
                    break;
                default:
                    return false;
            }
            WeaponUsed?.Invoke(slot, resolution);
            if (weapon.Definition.Skill != WeaponSkill.Mimic)
                lastUsedSkill = weapon.Definition.Skill;
            return true;
        }

        private void Update()
        {
            SimulateStep(Time.deltaTime);
        }

        public void SimulateStep(float deltaTime)
        {
            float step = Mathf.Max(0f, deltaTime);
            loadout.Tick(step);
            leapRemaining = Mathf.Max(0f, leapRemaining - step);
            servitudeRemaining = Mathf.Max(0f, servitudeRemaining - step);
            poetRemaining = Mathf.Max(0f, poetRemaining - step);
            edgeRemaining = Mathf.Max(0f, edgeRemaining - step);
            if (recorderRemaining > 0f)
            {
                recorderRemaining -= step;
                if (recorderRemaining <= 0f)
                {
                    recorderRemaining = 0f;
                    ApplySkillDamage(recordedDamage * 0.6f);
                    recordedDamage = 0f;
                }
            }
            if (boss == null || !boss.IsBattleActive || boss.Health == null || boss.Health.IsDead)
            {
                needleRemaining = 0f;
                barrageShotsRemaining = 0;
                if (needleZoneVisual != null) needleZoneVisual.SetActive(false);
                return;
            }

            if (needleRemaining > 0f)
            {
                float activeStep = Mathf.Min(step, needleRemaining);
                needleRemaining -= activeStep;
                needleTickRemaining -= activeStep;
                while (needleTickRemaining <= 0f && boss.IsBattleActive)
                {
                    if (boss.Position == needlePosition && !boss.IsInvisible)
                    {
                        ApplySkillDamage(boss.Health.MaxHealth * 0.07f);
                    }
                    needleTickRemaining += HasteFactor;
                }
                if (needleRemaining <= 0f && needleZoneVisual != null)
                    needleZoneVisual.SetActive(false);
            }

            if (barrageShotsRemaining > 0)
            {
                barrageTickRemaining -= step;
                while (barrageShotsRemaining > 0 && barrageTickRemaining <= 0f && boss.IsBattleActive)
                {
                    if (!boss.IsInvisible)
                        ApplySkillDamage(boss.Health.MaxHealth * 0.015f);
                    barrageShotsRemaining--;
                    barrageTickRemaining += 0.5f * HasteFactor;
                }
            }

            if (alliedCrow)
            {
                alliedCrowTickRemaining -= step;
                if (alliedCrowTickRemaining <= 0f)
                {
                    if (!boss.IsInvisible) ApplySkillDamage(boss.Health.MaxHealth * 0.04f);
                    alliedCrowTickRemaining += 3f;
                }
            }
        }

        private DamageResolution ApplySkillDamage(float damage)
        {
            if (boss == null || boss.Health == null || boss.IsInvisible || !boss.IsBattleActive)
                return new DamageResolution(0f, false, false);
            DamageResolution resolution = DamageResolver.Resolve(boss.Health,
                damage * SharedSkillDamageMultiplier,
                DamageKind.Ranged, boss.Position, PlayerPosture.Standing);
            boss.NotifyDamageResolved(resolution);
            return resolution;
        }

        private bool ApplyMimic()
        {
            switch (boss.LastSkill)
            {
                case BossSkill.TripleStrike:
                    ApplySkillDamage(boss.Health.MaxHealth * 0.3f);
                    return true;
                case BossSkill.SummonCrow:
                    alliedCrow = true;
                    alliedCrowTickRemaining = 3f;
                    ShowAlliedCrow();
                    return true;
                case BossSkill.Invisibility:
                    player.ApplyInvulnerability(8f);
                    return true;
                case BossSkill.BorrowedWeapon:
                    lastUsedSkill = boss.LastBorrowedSkill;
                    return ApplyCopiedWeaponSkill(boss.LastBorrowedSkill);
                default:
                    return false;
            }
        }

        private bool ApplyCopiedWeaponSkill(WeaponSkill skill)
        {
            switch (skill)
            {
                case WeaponSkill.Truce:
                    boss.ApplyTruce(player.Position.Column, 2, 2f * HasteFactor);
                    return true;
                case WeaponSkill.Needle:
                    needlePosition = boss.Position;
                    needleRemaining = 3f;
                    needleTickRemaining = HasteFactor;
                    ShowNeedleZone();
                    return true;
                case WeaponSkill.Barrage:
                    barrageShotsRemaining = 10;
                    barrageTickRemaining = 0.5f * HasteFactor;
                    return true;
                case WeaponSkill.Leap:
                    leapRemaining = 3f;
                    return true;
                case WeaponSkill.Servitude:
                    servitudeRemaining = 5f;
                    return true;
                case WeaponSkill.Poet:
                    poetRemaining = 5f;
                    return true;
                case WeaponSkill.Edge:
                    edgeRemaining = 5f;
                    return true;
                case WeaponSkill.Recorder:
                    recorderRemaining = 10f;
                    recordedDamage = 0f;
                    return true;
                default:
                    return false;
            }
        }

        private static bool RequiresTarget(WeaponSkill skill)
        {
            return skill == WeaponSkill.DirectDamage || skill == WeaponSkill.Truce ||
                   skill == WeaponSkill.Needle || skill == WeaponSkill.Barrage;
        }

        private float HasteFactor => leapRemaining > 0f ? 0.7f : 1f;

        private void ShowNeedleZone()
        {
            if (needleZoneVisual == null)
            {
                SpriteRenderer source = player != null ? player.GetComponent<SpriteRenderer>() : null;
                if (source == null || source.sprite == null) return;
                needleZoneVisual = new GameObject("NeedleZoneProxy");
                SpriteRenderer visual = needleZoneVisual.AddComponent<SpriteRenderer>();
                visual.sprite = source.sprite;
                visual.drawMode = SpriteDrawMode.Sliced;
                visual.size = new Vector2(1.3f, 1.1f);
                visual.color = new Color(0.8f, 0.15f, 0.7f, 0.45f);
                visual.sortingOrder = 3;
            }
            needleZoneVisual.transform.position = arena.GetWorldPosition(needlePosition);
            needleZoneVisual.SetActive(true);
        }

        private void ShowAlliedCrow()
        {
            if (alliedCrowVisual == null)
            {
                SpriteRenderer source = player != null ? player.GetComponent<SpriteRenderer>() : null;
                if (source == null || source.sprite == null) return;
                alliedCrowVisual = new GameObject("AlliedCrowProxy");
                alliedCrowVisual.transform.SetParent(player.transform, false);
                alliedCrowVisual.transform.localPosition = new Vector3(-0.7f, 1f, 0f);
                alliedCrowVisual.transform.localScale = Vector3.one * 0.3f;
                SpriteRenderer visual = alliedCrowVisual.AddComponent<SpriteRenderer>();
                visual.sprite = source.sprite;
                visual.color = Color.black;
                visual.sortingOrder = source.sortingOrder + 1;
            }
            alliedCrowVisual.SetActive(true);
        }

        private void SubscribeHealth()
        {
            if (boss != null && boss.Health != null) boss.Health.HealthChanged += OnBossHealthChanged;
            if (player != null && player.Health != null) player.Health.HealthChanged += OnPlayerHealthChanged;
        }

        private void UnsubscribeHealth()
        {
            if (boss != null && boss.Health != null) boss.Health.HealthChanged -= OnBossHealthChanged;
            if (player != null && player.Health != null) player.Health.HealthChanged -= OnPlayerHealthChanged;
        }

        private void OnDisable()
        {
            UnsubscribeHealth();
            if (needleZoneVisual != null)
            {
                if (Application.isPlaying) Destroy(needleZoneVisual);
                else DestroyImmediate(needleZoneVisual);
            }
        }

        private void OnBossHealthChanged(float previous, float current)
        {
            if (recorderRemaining > 0f && current < previous)
                recordedDamage += previous - current;
        }

        private void OnPlayerHealthChanged(float previous, float current)
        {
            if (servitudeRemaining <= 0f || current >= previous || boss == null || !boss.IsBattleActive ||
                boss.Health == null || boss.Health.IsDead || reflectingDamage) return;
            float reflected = (previous - current) * 0.7f;
            reflectingDamage = true;
            ApplySkillDamage(reflected);
            reflectingDamage = false;
        }

        private void EnsureInventoryInitialized()
        {
            availableWeapons ??= Array.Empty<WeaponDefinition>();
            if (selectedWeapons == null || selectedWeapons.Length != availableWeapons.Length ||
                ownedWeapons == null || ownedWeapons.Length != availableWeapons.Length)
            {
                selectedWeapons = new bool[availableWeapons.Length];
                ownedWeapons = new bool[availableWeapons.Length];
            }

            bool hasOwnedWeapon = false;
            foreach (bool owned in ownedWeapons) hasOwnedWeapon |= owned;
            if (hasOwnedWeapon) return;

            for (int index = 0; index < availableWeapons.Length; index++)
            {
                ownedWeapons[index] = availableWeapons[index] != null;
                selectedWeapons[index] = availableWeapons[index] != null &&
                                         index < BattleLoadout.MaximumWeapons;
            }
        }
    }
}
