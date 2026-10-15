using NUnit.Framework;
using Spotlight.Battle;
using UnityEngine;

namespace Spotlight.Tests.EditMode
{
    public sealed class BattleEditModeTests
    {
        [Test]
        public void Arena_UsesEightColumns_AndRestrictsCorridorPosture()
        {
            GameObject arenaObject = new GameObject("ArenaTest");
            BattleArena arena = arenaObject.AddComponent<BattleArena>();
            arena.Configure(8, 1.6f, -5.6f);

            BattlePosition clamped = arena.Clamp(new BattlePosition(BattleLane.Background, 99));
            Assert.That(clamped.Column, Is.EqualTo(7));
            Assert.That(arena.IsPostureAllowed(new BattlePosition(BattleLane.Corridor, 2),
                PlayerPosture.OnTable), Is.False);
            Assert.That(arena.IsPostureAllowed(new BattlePosition(BattleLane.Foreground, 2),
                PlayerPosture.OnTable), Is.False);
            Object.DestroyImmediate(arenaObject);
        }

        [Test]
        public void PlayerBattleMovement_OnTableOnlyJumpsToAdjacentTable()
        {
            GameObject arenaObject = new GameObject("ArenaTest");
            BattleArena arena = arenaObject.AddComponent<BattleArena>();
            arena.Configure(8, 1f, 0f);
            GameObject firstTableObject = new GameObject("Table5");
            CoverController firstTable = firstTableObject.AddComponent<CoverController>();
            firstTable.Configure(new BattlePosition(BattleLane.Foreground, 5));
            GameObject secondTableObject = new GameObject("Table4");
            CoverController secondTable = secondTableObject.AddComponent<CoverController>();
            secondTable.Configure(new BattlePosition(BattleLane.Foreground, 4));
            arena.ConfigureTables(new[] { firstTable, secondTable });
            GameObject playerObject = new GameObject("BattlePlayerTest");
            CombatHealth health = playerObject.AddComponent<CombatHealth>();
            PlayerBattleController player = playerObject.AddComponent<PlayerBattleController>();
            player.Configure(arena, health, new BattlePosition(BattleLane.Foreground, 6));
            player.BeginBattle();

            Assert.That(player.TryMoveHorizontal(-1), Is.True);
            Assert.That(player.Position.Column, Is.EqualTo(5));
            Assert.That(player.TrySetPosture(PlayerPosture.OnTable), Is.True);
            Assert.That(player.TryMoveHorizontal(-1), Is.True);
            Assert.That(player.Position.Column, Is.EqualTo(4));
            Assert.That(player.TryMoveHorizontal(-1), Is.False);
            Assert.That(player.Position.Column, Is.EqualTo(4));

            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(firstTableObject);
            Object.DestroyImmediate(secondTableObject);
            Object.DestroyImmediate(arenaObject);
        }

        [Test]
        public void WeaponController_SelectsAtMostThree_AndDiscardRemovesOwnership()
        {
            GameObject gameObject = new GameObject("WeaponControllerTest");
            WeaponController controller = gameObject.AddComponent<WeaponController>();
            WeaponDefinition[] definitions = new WeaponDefinition[4];
            for (int index = 0; index < definitions.Length; index++)
            {
                definitions[index] = ScriptableObject.CreateInstance<WeaponDefinition>();
                definitions[index].Configure("weapon_" + index, "Weapon " + index, 10f, 0f, 8,
                    DamageKind.Ranged);
            }
            controller.Configure(definitions, null, null, null);

            Assert.That(controller.IsSelected(0), Is.True);
            Assert.That(controller.IsSelected(3), Is.False);
            Assert.That(controller.TryToggleSelection(3), Is.False);
            Assert.That(controller.TryToggleSelection(2), Is.True);
            Assert.That(controller.TryToggleSelection(3), Is.True);
            controller.BeginBattle();
            Assert.That(controller.Loadout.Weapons, Has.Count.EqualTo(3));
            Assert.That(controller.RemoveWeaponAt(0), Is.Not.Null);
            Assert.That(controller.IsOwned(0), Is.False);
            Assert.That(controller.TryToggleSelection(0), Is.False);

            foreach (WeaponDefinition definition in definitions) Object.DestroyImmediate(definition);
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void Cover_BlocksExactlyOneRangedHit_WhileProne()
        {
            GameObject targetObject = new GameObject("TargetTest");
            CombatHealth health = targetObject.AddComponent<CombatHealth>();
            health.Configure(100f);
            GameObject coverObject = new GameObject("CoverTest");
            CoverController cover = coverObject.AddComponent<CoverController>();
            BattlePosition position = new BattlePosition(BattleLane.Foreground, 3);
            cover.Configure(position, 1);

            DamageResolution first = DamageResolver.Resolve(health, 25f, DamageKind.Ranged,
                position, PlayerPosture.Prone, cover);
            DamageResolution second = DamageResolver.Resolve(health, 25f, DamageKind.Ranged,
                position, PlayerPosture.Prone, cover);

            Assert.That(first.Blocked, Is.True);
            Assert.That(first.AppliedDamage, Is.Zero);
            Assert.That(second.Blocked, Is.False);
            Assert.That(second.AppliedDamage, Is.EqualTo(25f));
            Assert.That(health.CurrentHealth, Is.EqualTo(75f));
            Object.DestroyImmediate(coverObject);
            Object.DestroyImmediate(targetObject);
        }

        [Test]
        public void BossEnergy_UsesMaximumPercentage_AndDamageConversion()
        {
            GameObject gameObject = new GameObject("BossEnergyTest");
            BossEnergy energy = gameObject.AddComponent<BossEnergy>();
            energy.Configure(200f, 15f, 0.2f);
            energy.ResetEnergy();

            energy.SimulateStep(1f);
            energy.AddFromDamage(100f);

            Assert.That(energy.Current, Is.EqualTo(50f).Within(0.001f));
            energy.SimulateStep(5f);
            Assert.That(energy.IsFull, Is.True);
            Assert.That(energy.TrySpendFull(), Is.True);
            Assert.That(energy.Current, Is.Zero);
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void BattleLoadout_AcceptsAtMostThreeWeapons()
        {
            BattleLoadout loadout = new BattleLoadout();
            WeaponDefinition weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.Configure("test", "Test", 10f, 1f, 2, DamageKind.Ranged);

            Assert.That(loadout.TryAdd(weapon), Is.True);
            Assert.That(loadout.TryAdd(weapon), Is.True);
            Assert.That(loadout.TryAdd(weapon), Is.True);
            Assert.That(loadout.TryAdd(weapon), Is.False);
            Assert.That(loadout.Weapons, Has.Count.EqualTo(3));
            Object.DestroyImmediate(weapon);
        }
    }
}
