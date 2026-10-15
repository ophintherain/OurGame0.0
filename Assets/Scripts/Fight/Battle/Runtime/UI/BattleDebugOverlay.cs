using UnityEngine;

namespace Spotlight.Battle
{
    public sealed class BattleDebugOverlay : MonoBehaviour
    {
        [SerializeField] private BattleCoordinator battle;
        [SerializeField] private PlayerBattleController player;
        [SerializeField] private BossController boss;
        [SerializeField] private WeaponController weapons;

        private string lastFeedback = "Select up to 3 weapons, then start";

        public void Configure(BattleCoordinator coordinator, PlayerBattleController battlePlayer,
            BossController battleBoss, WeaponController weaponController)
        {
            battle = coordinator;
            player = battlePlayer;
            boss = battleBoss;
            weapons = weaponController;
        }

        private void OnGUI()
        {
            if (battle == null || player == null || boss == null)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(18f, 145f, 430f, 650f), GUI.skin.box);
            GUILayout.Label("BATTLE CONTROL / COMBAT HUD");
            GUILayout.Label($"Active: {battle.IsBattleActive}   Result: {battle.LastResult.Outcome}");
            GUILayout.Label($"Player HP: {Value(player.Health)}   {player.Position}   {player.Posture}");
            GUILayout.Label($"Boss HP: {Value(boss.Health)}   Energy: {EnergyValue()}   {boss.Position}");
            if (boss.HasActiveTelegraph)
            {
                GUILayout.Label($"WARNING: {boss.ActiveTelegraph.Primary} / {boss.ActiveTelegraph.Secondary}");
            }
            GUILayout.Label(lastFeedback);

            if (!battle.IsBattleActive)
            {
                DrawWeaponSelection();
                if (battle.HasPendingWeaponDiscard)
                {
                    GUILayout.Label("HP below 30%: choose one weapon to discard before continuing.");
                    for (int index = 0; index < weapons.Loadout.Weapons.Count; index++)
                    {
                        int captured = index;
                        if (GUILayout.Button("Discard: " + weapons.Loadout.Weapons[index].Definition.DisplayName))
                        {
                            lastFeedback = battle.ResolveWeaponDiscard(captured)
                                ? "Weapon discarded"
                                : "Discard rejected";
                            break;
                        }
                    }
                }
                if (GUILayout.Button(battle.LastResult.Outcome == BattleOutcome.None ? "Start Battle" : "Restart Battle"))
                {
                    lastFeedback = battle.TryStartOrRestart() ? "Battle started" : "Cannot start battle";
                }
                GUILayout.Label("Keyboard: A/D left-right | 1/2 front-back | R/T posture | Shift stand");
                GUILayout.Label("F/G/H weapons | Ctrl overview zoom | E escape | Enter restart");
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Move Left")) Invoke(() => player.TryMoveHorizontal(-1), "Move left");
            if (GUILayout.Button("Move Right")) Invoke(() => player.TryMoveHorizontal(1), "Move right");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Lane Front (1)")) Invoke(() => player.TryChangeLane(-1), "Lane front");
            if (GUILayout.Button("Lane Back (2)")) Invoke(() => player.TryChangeLane(1), "Lane back");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Stand (Shift)")) Invoke(() => player.TrySetPosture(PlayerPosture.Standing), "Stand");
            if (GUILayout.Button("Prone (R)")) Invoke(() => player.TryTogglePosture(PlayerPosture.Prone), "Prone toggle");
            if (GUILayout.Button("On Table (T)")) Invoke(() => player.TryTogglePosture(PlayerPosture.OnTable), "On table toggle");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fire 1 (F)")) Invoke(() => weapons != null && weapons.TryUse(0), "Fire 1");
            if (GUILayout.Button("Fire 2 (G)")) Invoke(() => weapons != null && weapons.TryUse(1), "Fire 2");
            if (GUILayout.Button("Fire 3 (H)")) Invoke(() => weapons != null && weapons.TryUse(2), "Fire 3");
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Toggle Overview Zoom (Ctrl)"))
            {
                Invoke(() => battle.TryToggleOverviewZoom(), "Overview zoom");
            }
            if (GUILayout.Button("Escape (corridor left end only)"))
            {
                Invoke(() => battle.TryEscape(), "Escape");
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("QA: Player -75 HP"))
            {
                float applied = player.Health != null ? player.Health.ApplyDamage(75f) : 0f;
                lastFeedback = applied > 0f ? "QA player damage applied" : "QA damage rejected";
            }
            if (GUILayout.Button("QA: Boss -1100 HP"))
            {
                float applied = boss.Health != null ? boss.Health.ApplyDamage(1100f) : 0f;
                lastFeedback = applied > 0f ? "QA boss damage applied" : "QA damage rejected";
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Rule: prone behind the brown table blocks one ranged/area hit.");
            GUILayout.EndArea();
        }

        private void DrawWeaponSelection()
        {
            if (weapons == null) return;
            GUILayout.Label("LOADOUT (click to toggle, minimum 1 / maximum 3)");
            for (int index = 0; index < weapons.AvailableWeapons.Count; index++)
            {
                WeaponDefinition definition = weapons.AvailableWeapons[index];
                if (definition == null) continue;
                int captured = index;
                if (!weapons.IsOwned(index))
                {
                    GUILayout.Label("[LOST] " + definition.DisplayName);
                    continue;
                }
                string state = weapons.IsSelected(index) ? "[X]" : "[ ]";
                if (GUILayout.Button($"{state} {definition.DisplayName}  DMG {definition.Damage:0}  RANGE {definition.RangeColumns}"))
                {
                    lastFeedback = weapons.TryToggleSelection(captured)
                        ? "Loadout updated"
                        : "Selection rejected (keep 1, max 3)";
                }
            }
        }

        private void Invoke(System.Func<bool> action, string label)
        {
            lastFeedback = action() ? label + " OK" : label + " rejected";
        }

        private static string Value(CombatHealth health)
        {
            return health == null ? "-" : $"{health.CurrentHealth:0}/{health.MaxHealth:0}";
        }

        private string EnergyValue()
        {
            return boss.Energy == null ? "-" : $"{boss.Energy.Current:0}/{boss.Energy.Maximum:0}";
        }
    }
}
