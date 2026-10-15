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

        private readonly BattleLoadout loadout = new BattleLoadout();

        public event Action<int, DamageResolution> WeaponUsed;

        public BattleLoadout Loadout => loadout;
        public System.Collections.Generic.IReadOnlyList<WeaponDefinition> AvailableWeapons => availableWeapons;

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

        public void BeginBattle()
        {
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

        public bool IsSelected(int index)
        {
            return index >= 0 && index < selectedWeapons.Length && selectedWeapons[index];
        }

        public bool IsOwned(int index)
        {
            return index >= 0 && index < ownedWeapons.Length && ownedWeapons[index];
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
            if (player == null || boss == null || arena == null || !player.IsBattleActive ||
                player.Posture != PlayerPosture.Standing || slot < 0 || slot >= loadout.Weapons.Count)
            {
                return false;
            }

            WeaponRuntime weapon = loadout.Weapons[slot];
            if (!weapon.IsReady || arena.ColumnDistance(player.Position, boss.Position) >
                weapon.Definition.RangeColumns)
            {
                return false;
            }

            if (!weapon.TryConsumeUse())
            {
                return false;
            }

            DamageResolution resolution = DamageResolver.Resolve(boss.Health, weapon.Definition.Damage,
                weapon.Definition.DamageKind, boss.Position, PlayerPosture.Standing);
            boss.NotifyDamageResolved(resolution);
            WeaponUsed?.Invoke(slot, resolution);
            return true;
        }

        private void Update()
        {
            loadout.Tick(Time.deltaTime);
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
