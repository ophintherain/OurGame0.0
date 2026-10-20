using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.Battle
{
    public sealed class BattleDebugOverlay : MonoBehaviour
    {
        [SerializeField] private BattleCoordinator battle;
        [SerializeField] private PlayerBattleController player;
        [SerializeField] private BossController boss;
        [SerializeField] private WeaponController weapons;

        private string lastFeedback = "Select up to 3 weapons, then start";
        private Vector2 scroll;
        private bool showTableColliders;

        public void Configure(BattleCoordinator coordinator, PlayerBattleController battlePlayer,
            BossController battleBoss, WeaponController weaponController)
        {
            battle = coordinator;
            player = battlePlayer;
            boss = battleBoss;
            weapons = weaponController;
        }

        private static bool ClickedButton(string label)
        {
            if (!GUILayout.Button(label)) return false;
            UISoundPlayer.Play(UISoundCue.Click);
            return true;
        }

        private void OnGUI()
        {
            if (Spotlight.Shared.SpotlightSceneTransition.IsTransitioning) return;
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName != "fight_test" && sceneName != "Gameplay") return;
            if (battle == null || player == null || boss == null || battle.IsLoadoutSelectionOpen)
            {
                return;
            }

            if (showTableColliders) DrawTableColliders();

            float top = 16f + BattleStatusHUD.OverviewHeight(Screen.width) + 18f;
            GUILayout.BeginArea(new Rect(18f, top, 450f,
                Mathf.Max(180f, Mathf.Min(610f, Screen.height - top - 18f))), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("BATTLE CONTROL / COMBAT HUD");
            GUILayout.Label($"Carriage: {boss.CarriageIndex}   Borrowed boss skill: {boss.BorrowedSkill}");
            GUILayout.Label($"Boss last skill: {boss.LastSkill} / {boss.LastBorrowedSkill}");
            GUILayout.Label($"Active: {battle.IsBattleActive}   Result: {battle.LastResult.Outcome}");
            GUILayout.Label($"Player: {player.Position}   {player.Posture}");
            GUILayout.Label($"Boss position: {boss.Position}");
            GUILayout.Label($"Boss: Crow {boss.HasCrow}, Invisible {boss.IsInvisible}, Bleed stacks {boss.BleedStacks}");
            if (weapons != null && weapons.NeedleRemaining > 0f)
                GUILayout.Label($"Needle zone: {weapons.NeedlePosition} ({weapons.NeedleRemaining:0.0}s)");
            if (weapons != null && weapons.BarrageShotsRemaining > 0)
                GUILayout.Label($"Barrage shots left: {weapons.BarrageShotsRemaining}");
            if (weapons != null && weapons.RecorderRemaining > 0f)
                GUILayout.Label($"Recorder: {weapons.RecordedDamage:0} / {weapons.RecorderRemaining:0.0}s");
            if (weapons != null && weapons.HasAlliedCrow)
                GUILayout.Label("Allied crow: active");
            if (weapons != null && (weapons.LeapRemaining > 0f || weapons.ServitudeRemaining > 0f ||
                                    weapons.PoetRemaining > 0f || weapons.EdgeRemaining > 0f))
                GUILayout.Label($"Buffs: Leap {weapons.LeapRemaining:0.0}s / Reflect {weapons.ServitudeRemaining:0.0}s / Range {weapons.PoetRemaining:0.0}s / Edge {weapons.EdgeRemaining:0.0}s");
            if (boss.HasActiveTelegraph)
            {
                GUILayout.Label($"WARNING: {boss.ActiveTelegraph.Primary} / {boss.ActiveTelegraph.Secondary}");
            }
            if (boss.HasCrowTelegraph)
                GUILayout.Label($"CROW WARNING: {boss.CrowTelegraph.Primary}");
            GUILayout.Label(lastFeedback);
            if (ClickedButton(showTableColliders ? "QA: Hide table colliders" : "QA: Show table colliders"))
                showTableColliders = !showTableColliders;
            if (showTableColliders)
                GUILayout.Label("Yellow: front collider | Cyan: back collider | Green: middle cells");

            if (!battle.IsBattleActive)
            {
                GUILayout.Label("QA carriage selection (same greybox scene):");
                GUILayout.BeginHorizontal();
                for (int carriage = 1; carriage <= 3; carriage++)
                {
                    int selected = carriage;
                    if (ClickedButton("Car " + carriage))
                    {
                        boss.ConfigureCarriage(selected);
                        weapons?.ConfigureCarriage(selected);
                        lastFeedback = "Carriage " + selected + " selected";
                    }
                }
                GUILayout.EndHorizontal();
                DrawWeaponSelection();
                if (battle.HasPendingWeaponDiscard)
                {
                    GUILayout.Label("HP below 30%: choose one weapon to discard before continuing.");
                    for (int index = 0; index < weapons.Loadout.Weapons.Count; index++)
                    {
                        int captured = index;
                        if (ClickedButton("Discard: " + weapons.Loadout.Weapons[index].Definition.DisplayName))
                        {
                            lastFeedback = battle.ResolveWeaponDiscard(captured)
                                ? "Weapon discarded"
                                : "Discard rejected";
                            break;
                        }
                    }
                }
                if (ClickedButton(battle.LastResult.Outcome == BattleOutcome.None ? "Start Battle" : "Restart Battle"))
                {
                    lastFeedback = battle.TryStartOrRestart() ? "Battle started" : "Cannot start battle";
                }
                GUILayout.Label("F/G/H weapons | Ctrl overview zoom | E escape | Enter restart");
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginHorizontal();
            if (ClickedButton("Use 1 (F)")) Invoke(() => weapons != null && weapons.TryUse(0), "Use 1");
            if (ClickedButton("Use 2 (G)")) Invoke(() => weapons != null && weapons.TryUse(1), "Use 2");
            if (ClickedButton("Use 3 (H)")) Invoke(() => weapons != null && weapons.TryUse(2), "Use 3");
            GUILayout.EndHorizontal();
            if (ClickedButton("Toggle Overview Zoom (Ctrl)"))
            {
                Invoke(() => battle.TryToggleOverviewZoom(), "Overview zoom");
            }
            if (ClickedButton("Escape (corridor left end only)"))
            {
                Invoke(() => battle.TryEscape(), "Escape");
            }
            if (ClickedButton("QA: Return to loadout / switch carriage"))
                Invoke(() => battle.TryReturnToLoadoutForDebug(), "Return to loadout");
            if (ClickedButton("QA: Cast borrowed boss skill"))
                Invoke(() => boss.TryCastBorrowedSkill(), "Borrowed boss skill");
            if (boss.CarriageIndex == 1 && ClickedButton("QA: Summon crow"))
            {
                boss.SummonCrow();
                lastFeedback = boss.HasCrow ? "Crow summoned" : "Crow already active";
            }
            if (boss.CarriageIndex == 2 && ClickedButton("QA: Boss invisibility (8s)"))
            {
                boss.ApplyInvisibility(8f);
                lastFeedback = boss.IsInvisible ? "Boss invisible" : "Invisibility rejected";
            }
            GUILayout.BeginHorizontal();
            if (ClickedButton("QA: Player -75 HP"))
            {
                float applied = player.Health != null ? player.Health.ApplyDamage(75f) : 0f;
                lastFeedback = applied > 0f ? "QA player damage applied" : "QA damage rejected";
            }
            if (ClickedButton("QA: Boss -1100 HP"))
            {
                float applied = boss.Health != null ? boss.Health.ApplyDamage(1100f) : 0f;
                lastFeedback = applied > 0f ? "QA boss damage applied" : "QA damage rejected";
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Rule: each front/back table blocks one ranged/area hit while prone.");
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawTableColliders()
        {
            Camera camera = Camera.main;
            if (camera == null || Event.current.type != EventType.Repaint) return;

            Color oldColor = GUI.color;
            foreach (CoverController cover in battle.Covers)
            {
                if (cover == null || !cover.gameObject.activeInHierarchy) continue;
                BoxCollider2D collider = cover.GetComponent<BoxCollider2D>();
                if (collider == null || !collider.enabled) continue;

                Bounds bounds = collider.bounds;
                Vector3 bottomLeft = camera.WorldToScreenPoint(bounds.min);
                Vector3 topRight = camera.WorldToScreenPoint(bounds.max);
                if (bottomLeft.z < 0f || topRight.z < 0f) continue;

                float left = Mathf.Min(bottomLeft.x, topRight.x);
                float top = Screen.height - Mathf.Max(bottomLeft.y, topRight.y);
                float width = Mathf.Abs(topRight.x - bottomLeft.x);
                float height = Mathf.Abs(topRight.y - bottomLeft.y);
                GUI.color = cover.Position.Lane == BattleLane.Foreground ? Color.yellow : Color.cyan;
                DrawOutline(new Rect(left, top, width, height), 3f);
                GUI.Label(new Rect(left + 3f, top + 3f, 160f, 22f), cover.name);
            }
            DrawMiddleCells(camera);
            DrawTableSlots(camera);
            GUI.color = oldColor;
        }

        private void DrawMiddleCells(Camera camera)
        {
            BattleArena arena = battle.Arena;
            if (arena == null) return;
            GUI.color = Color.green;
            for (int column = 0; column < arena.ColumnCount; column++)
            {
                Vector3 screen = camera.WorldToScreenPoint(
                    arena.GetWorldPosition(new BattlePosition(BattleLane.Corridor, column)));
                if (screen.z < 0f) continue;
                float x = screen.x;
                float y = Screen.height - screen.y;
                GUI.DrawTexture(new Rect(x - 4f, y - 4f, 8f, 8f), Texture2D.whiteTexture);
                GUI.Label(new Rect(x + 6f, y - 12f, 45f, 22f), column.ToString());
                if (column == 0) continue;
                Vector3 previous = camera.WorldToScreenPoint(
                    arena.GetWorldPosition(new BattlePosition(BattleLane.Corridor, column - 1)));
                float previousX = previous.x;
                GUI.DrawTexture(new Rect(Mathf.Min(previousX, x), y - 1f,
                    Mathf.Abs(x - previousX), 2f), Texture2D.whiteTexture);
            }
        }

        private void DrawTableSlots(Camera camera)
        {
            BattleArena arena = battle.Arena;
            if (arena == null) return;
            Color oldColor = GUI.color;
            foreach (CoverController cover in battle.Covers)
            {
                if (cover == null || !arena.IsTablePosition(cover.Position)) continue;
                Vector3 screen = camera.WorldToScreenPoint(arena.GetWorldPosition(cover.Position));
                if (screen.z < 0f) continue;
                float x = screen.x;
                float y = Screen.height - screen.y;
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(x - 5f, y - 5f, 10f, 10f), Texture2D.whiteTexture);
                GUI.Label(new Rect(x + 8f, y - 12f, 52f, 22f), "W " + cover.Position.Column);
            }
            GUI.color = oldColor;
        }


        private static void DrawOutline(Rect rect, float thickness)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
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
                    string lockState = definition.AvailableFromCarriage > weapons.CarriageIndex
                        ? "LOCKED until car " + definition.AvailableFromCarriage : "LOST";
                    GUILayout.Label("[" + lockState + "] " + definition.DisplayName);
                    continue;
                }
                string state = weapons.IsSelected(index) ? "[X]" : "[ ]";
                if (ClickedButton($"{state} {definition.DisplayName}  CD {definition.CooldownSeconds:0}s  RANGE {definition.RangeColumns}"))
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

    }
}
