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
        public void LaneSwitch_KeepsTheSameTableNumber_AndSkipsMissingFrontTables()
        {
            GameObject arenaObject = new GameObject("ArenaTest");
            BattleArena arena = arenaObject.AddComponent<BattleArena>();
            arena.Configure(9, 1.8f, -7.2f);
            int[] frontColumns = { 2, 4, 6 };
            int[] backColumns = { 0, 2, 4, 6, 8 };
            CoverController[] covers = new CoverController[frontColumns.Length + backColumns.Length];
            int index = 0;
            foreach (int column in frontColumns)
            {
                GameObject table = new GameObject("FrontTable");
                table.transform.position = new Vector3(column == 2 ? -4.3f : column == 4 ? 1.03f : 6.52f,
                    0f, 0f);
                covers[index] = table.AddComponent<CoverController>();
                covers[index++].Configure(new BattlePosition(BattleLane.Foreground, column));
            }
            foreach (int column in backColumns)
            {
                GameObject table = new GameObject("BackTable");
                table.transform.position = new Vector3(column switch
                {
                    0 => -7.65f,
                    2 => -3.9f,
                    4 => -0.05f,
                    6 => 3.75f,
                    _ => 7.6f
                }, 0f, 0f);
                covers[index] = table.AddComponent<CoverController>();
                covers[index++].Configure(new BattlePosition(BattleLane.Background, column));
            }
            arena.ConfigureTables(covers);
            Assert.That(arena.GetWorldPosition(new BattlePosition(BattleLane.Corridor, 2)).x,
                Is.EqualTo(-4.1f).Within(0.001f));
            Assert.That(arena.GetWorldPosition(new BattlePosition(BattleLane.Corridor, 4)).x,
                Is.EqualTo(0.49f).Within(0.001f));
            Assert.That(arena.GetWorldPosition(new BattlePosition(BattleLane.Corridor, 6)).x,
                Is.EqualTo(5.135f).Within(0.001f));
            Assert.That(arena.GetWorldPosition(new BattlePosition(BattleLane.Corridor, 5)).x,
                Is.EqualTo(2.8125f).Within(0.001f));

            GameObject playerObject = new GameObject("PlayerTest");
            CombatHealth health = playerObject.AddComponent<CombatHealth>();
            PlayerBattleController player = playerObject.AddComponent<PlayerBattleController>();
            player.Configure(arena, health, new BattlePosition(BattleLane.Corridor, 6));
            player.BeginBattle();

            Assert.That(player.TryChangeLane(-1), Is.True);
            Assert.That(player.Position, Is.EqualTo(new BattlePosition(BattleLane.Foreground, 6)));
            Assert.That(player.TryMoveHorizontal(1), Is.False);
            Assert.That(player.TryChangeLane(1), Is.True);
            Assert.That(player.TryChangeLane(1), Is.True);
            Assert.That(player.Position, Is.EqualTo(new BattlePosition(BattleLane.Background, 6)));
            Assert.That(player.TryChangeLane(-1), Is.True);
            Assert.That(player.TryMoveHorizontal(1), Is.True);
            Assert.That(player.TryMoveHorizontal(1), Is.True);
            Assert.That(player.Position, Is.EqualTo(new BattlePosition(BattleLane.Corridor, 8)));
            Assert.That(player.TryChangeLane(-1), Is.False);
            Assert.That(player.TryChangeLane(1), Is.True);
            Assert.That(player.Position, Is.EqualTo(new BattlePosition(BattleLane.Background, 8)));

            Object.DestroyImmediate(playerObject);
            foreach (CoverController cover in covers) Object.DestroyImmediate(cover.gameObject);
            Object.DestroyImmediate(arenaObject);
        }

        [Test]
        public void PlayerBattleMovement_OnTableJumpsToNextTableAcrossGap()
        {
            GameObject arenaObject = new GameObject("ArenaTest");
            BattleArena arena = arenaObject.AddComponent<BattleArena>();
            arena.Configure(8, 1f, 0f);
            GameObject firstTableObject = new GameObject("Table5");
            CoverController firstTable = firstTableObject.AddComponent<CoverController>();
            firstTable.Configure(new BattlePosition(BattleLane.Foreground, 5));
            firstTableObject.transform.position = arena.GetWorldPosition(firstTable.Position);
            GameObject secondTableObject = new GameObject("Table3");
            CoverController secondTable = secondTableObject.AddComponent<CoverController>();
            secondTable.Configure(new BattlePosition(BattleLane.Foreground, 3));
            secondTableObject.transform.position = arena.GetWorldPosition(secondTable.Position);
            arena.ConfigureTables(new[] { firstTable, secondTable });
            GameObject playerObject = new GameObject("BattlePlayerTest");
            SpriteRenderer renderer = playerObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 10;
            CombatHealth health = playerObject.AddComponent<CombatHealth>();
            PlayerBattleController player = playerObject.AddComponent<PlayerBattleController>();
            player.Configure(arena, health, new BattlePosition(BattleLane.Corridor, 6));
            player.BeginBattle();

            Assert.That(player.TryMoveHorizontal(-1), Is.True);
            Assert.That(player.Position.Column, Is.EqualTo(5));
            Assert.That(player.TryChangeLane(-1), Is.True);
            Assert.That(player.TryMoveHorizontal(-1), Is.False);
            float tableY = arena.GetWorldPosition(player.Position).y;
            Assert.That(player.TrySetPosture(PlayerPosture.OnTable), Is.True);
            Assert.That(playerObject.transform.position.y, Is.GreaterThan(tableY));
            Assert.That(player.TryMoveHorizontal(-1), Is.True);
            Assert.That(player.Position.Column, Is.EqualTo(3));
            Assert.That(player.IsJumping, Is.True);
            player.SimulateStep(0.3f);
            Assert.That(player.IsJumping, Is.False);
            Assert.That(playerObject.transform.position.y, Is.GreaterThan(arena.GetWorldPosition(player.Position).y));
            Assert.That(player.TryMoveHorizontal(-1), Is.False);
            Assert.That(player.Position.Column, Is.EqualTo(3));

            Assert.That(player.TrySetPosture(PlayerPosture.Prone), Is.True);
            Assert.That(playerObject.transform.position.y, Is.LessThan(arena.GetWorldPosition(player.Position).y));
            Assert.That(renderer.sortingOrder, Is.LessThan(10));
            Assert.That(player.TrySetPosture(PlayerPosture.Standing), Is.True);
            Assert.That(playerObject.transform.position.y, Is.EqualTo(arena.GetWorldPosition(player.Position).y));
            Assert.That(renderer.sortingOrder, Is.EqualTo(10));

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

        [Test]
        public void FirstCarriageSkills_PushStunAndDealTimedMaxHealthDamage()
        {
            GameObject arenaObject = new GameObject("SkillArena");
            BattleArena arena = arenaObject.AddComponent<BattleArena>();
            arena.Configure(8, 1f, 0f);

            GameObject playerObject = new GameObject("SkillPlayer");
            CombatHealth playerHealth = playerObject.AddComponent<CombatHealth>();
            PlayerBattleController player = playerObject.AddComponent<PlayerBattleController>();
            player.Configure(arena, playerHealth, new BattlePosition(BattleLane.Corridor, 5));

            GameObject bossObject = new GameObject("SkillBoss");
            CombatHealth bossHealth = bossObject.AddComponent<CombatHealth>();
            bossHealth.Configure(1000f);
            BossMovement movement = bossObject.AddComponent<BossMovement>();
            movement.Configure(arena, 4, 1.5f);
            BossController boss = bossObject.AddComponent<BossController>();
            boss.Configure(bossHealth, bossObject.AddComponent<BossEnergy>(), movement, player,
                System.Array.Empty<CoverController>());

            WeaponDefinition[] definitions = new WeaponDefinition[3];
            WeaponSkill[] skills = { WeaponSkill.Truce, WeaponSkill.Needle, WeaponSkill.Barrage };
            for (int index = 0; index < definitions.Length; index++)
            {
                definitions[index] = ScriptableObject.CreateInstance<WeaponDefinition>();
                definitions[index].Configure("skill_" + index, "Skill " + index, 0f, 7f,
                    index == 0 ? 1 : 7, DamageKind.Ranged);
                definitions[index].ConfigureSkill(skills[index], 1);
            }
            GameObject weaponObject = new GameObject("SkillWeapons");
            WeaponController weapons = weaponObject.AddComponent<WeaponController>();
            weapons.Configure(definitions, player, boss, arena);
            boss.SetWeaponController(weapons);
            player.BeginBattle();
            boss.BeginBattle();
            weapons.BeginBattle();

            Assert.That(weapons.TryUse(0), Is.True);
            Assert.That(boss.Position.Column, Is.EqualTo(2));
            Assert.That(boss.StunRemaining, Is.EqualTo(2f));
            Assert.That(weapons.TryUse(1), Is.True);
            Assert.That(weapons.TryUse(2), Is.True);
            weapons.SimulateStep(5f);
            Assert.That(bossHealth.CurrentHealth, Is.EqualTo(640f).Within(0.01f));

            foreach (WeaponDefinition definition in definitions) Object.DestroyImmediate(definition);
            Object.DestroyImmediate(weaponObject);
            Object.DestroyImmediate(bossObject);
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(arenaObject);
        }

        [Test]
        public void LaterCarriageSkills_UnlockByCarriage_AndInvisibilityRejectsTargeting()
        {
            GameObject arenaObject = new GameObject("LaterArena");
            BattleArena arena = arenaObject.AddComponent<BattleArena>();
            arena.Configure(8, 1f, 0f);
            GameObject playerObject = new GameObject("LaterPlayer");
            CombatHealth playerHealth = playerObject.AddComponent<CombatHealth>();
            PlayerBattleController player = playerObject.AddComponent<PlayerBattleController>();
            player.Configure(arena, playerHealth, new BattlePosition(BattleLane.Corridor, 5));
            GameObject bossObject = new GameObject("LaterBoss");
            CombatHealth bossHealth = bossObject.AddComponent<CombatHealth>();
            BossMovement movement = bossObject.AddComponent<BossMovement>();
            movement.Configure(arena, 4, 1.5f);
            BossController boss = bossObject.AddComponent<BossController>();
            boss.Configure(bossHealth, bossObject.AddComponent<BossEnergy>(), movement, player,
                System.Array.Empty<CoverController>());
            boss.ConfigureCarriage(2);
            WeaponDefinition[] definitions = new WeaponDefinition[4];
            for (int index = 0; index < definitions.Length; index++)
            {
                definitions[index] = ScriptableObject.CreateInstance<WeaponDefinition>();
                definitions[index].Configure("unlock_" + index, "Skill", 0f, 7f, 7,
                    DamageKind.Ranged);
                definitions[index].ConfigureSkill(index == 0 ? WeaponSkill.Needle : WeaponSkill.Leap,
                    index == 3 ? 2 : 1);
            }
            GameObject weaponObject = new GameObject("LaterWeapons");
            WeaponController weapons = weaponObject.AddComponent<WeaponController>();
            weapons.Configure(definitions, player, boss, arena);
            Assert.That(weapons.IsOwned(3), Is.False);
            weapons.ConfigureCarriage(2);
            Assert.That(weapons.IsOwned(3), Is.True);
            player.BeginBattle();
            boss.BeginBattle();
            weapons.BeginBattle();
            boss.ApplyInvisibility(8f);
            Assert.That(weapons.TryUse(0), Is.False);
            Assert.That(weapons.Loadout.Weapons[0].IsReady, Is.True);

            foreach (WeaponDefinition definition in definitions) Object.DestroyImmediate(definition);
            Object.DestroyImmediate(weaponObject);
            Object.DestroyImmediate(bossObject);
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(arenaObject);
        }

        [Test]
        public void ThirdCarriageBoss_AttackAddsPersistentBleedStack()
        {
            GameObject arenaObject = new GameObject("BleedArena");
            BattleArena arena = arenaObject.AddComponent<BattleArena>();
            arena.Configure(8, 1f, 0f);
            GameObject playerObject = new GameObject("BleedPlayer");
            CombatHealth playerHealth = playerObject.AddComponent<CombatHealth>();
            playerHealth.Configure(100f);
            PlayerBattleController player = playerObject.AddComponent<PlayerBattleController>();
            player.Configure(arena, playerHealth, new BattlePosition(BattleLane.Corridor, 4));
            GameObject bossObject = new GameObject("BleedBoss");
            CombatHealth bossHealth = bossObject.AddComponent<CombatHealth>();
            BossMovement movement = bossObject.AddComponent<BossMovement>();
            movement.Configure(arena, 4, 1.5f);
            BossController boss = bossObject.AddComponent<BossController>();
            boss.Configure(bossHealth, bossObject.AddComponent<BossEnergy>(), movement, player,
                System.Array.Empty<CoverController>());
            boss.ConfigureCarriage(3);
            player.BeginBattle();
            boss.BeginBattle();
            boss.SimulateStep(3f);
            boss.SimulateStep(1f);
            Assert.That(boss.BleedStacks, Is.EqualTo(1));
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(85f).Within(0.01f));
            boss.SimulateStep(1f);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(84.3f).Within(0.01f));

            Object.DestroyImmediate(bossObject);
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(arenaObject);
        }
    }
}
