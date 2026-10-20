using UnityEngine;

namespace Spotlight.Battle
{
    // The three slots, confirm button and nine-item strip follow the GDD layout.
    public sealed class BattleLoadoutScreen : MonoBehaviour
    {
        private const float DesignWidth = 624f;
        private const float DesignHeight = 351f;
        private static readonly float[] SlotCenters = { 170f, 310f, 448f };
        private static readonly Color Background = new Color(0.12f, 0.09f, 0.085f);
        private static readonly Color Panel = new Color(0.20f, 0.145f, 0.13f);
        private static readonly Color Gold = new Color(0.82f, 0.63f, 0.38f);
        private static readonly Color Ivory = new Color(0.98f, 0.91f, 0.78f);
        // All three Retry layers share a 3000x1688 canvas; keep their original alignment.
        private static readonly Rect RetryYesButton = new Rect(192f, 181f, 99f, 47f);
        private static readonly Rect RetryNoButton = new Rect(307f, 189f, 99f, 43f);
        private BattleCoordinator battle;
        private WeaponController weapons;
        private BattleStatusHUD hud;
        private Texture2D retryDialog;
        private Texture2D retryYes;
        private Texture2D retryNo;

        public void Configure(BattleCoordinator coordinator, WeaponController weaponController,
            BossController battleBoss, BattleStatusHUD statusHud)
        {
            battle = coordinator;
            weapons = weaponController;
            hud = statusHud;
            retryDialog = LoadRetryTexture("RetryDialog");
            retryYes = LoadRetryTexture("RetryYes");
            retryNo = LoadRetryTexture("RetryNo");
        }

        private void Update()
        {
            if (battle == null || !battle.IsRetryPromptOpen) return;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                UISoundPlayer.Play(UISoundCue.Click);
                battle.TryConfirmRetry();
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                UISoundPlayer.Play(UISoundCue.Click);
                battle.TryDeclineRetry();
            }
        }

        private void OnGUI()
        {
            if (Spotlight.Shared.SpotlightSceneTransition.IsTransitioning) return;
            if (battle == null || weapons == null) return;
            if (battle.IsVictoryEffectPlaying) return;
            if (battle.IsRetryPromptOpen)
            {
                DrawRetryPrompt();
                return;
            }
            if (battle.HasPendingWeaponDiscard)
            {
                DrawDiscardPrompt();
                return;
            }
            if (!battle.IsLoadoutSelectionOpen) return;

            int oldDepth = GUI.depth;
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUI.depth = -100;
            Fill(new Rect(0, 0, Screen.width, Screen.height), Background);
            float scale = Mathf.Min(Screen.width / DesignWidth, Screen.height / DesignHeight);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - DesignWidth * scale) / 2f,
                (Screen.height - DesignHeight * scale) / 2f, 0f), Quaternion.identity,
                new Vector3(scale, scale, 1f));

            Fill(new Rect(0, 0, DesignWidth, DesignHeight), Background);
            Fill(new Rect(18f, 24f, 588f, 244f), Panel);
            Border(new Rect(18f, 24f, 588f, 244f), 1f, Gold);
            GUI.Label(new Rect(0f, 43f, DesignWidth, 30f), "选择出战武器",
                LabelStyle(21, TextAnchor.MiddleCenter, Ivory));
            for (int slot = 0; slot < 3; slot++) DrawSlot(slot);
            DrawConfirm();
            DrawInventory();

            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
            GUI.depth = oldDepth;
        }

        private void DrawRetryPrompt()
        {
            int oldDepth = GUI.depth;
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUI.depth = -200;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.8f));
            float scale = Mathf.Min(Screen.width / DesignWidth, Screen.height / DesignHeight);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - DesignWidth * scale) / 2f,
                (Screen.height - DesignHeight * scale) / 2f, 0f), Quaternion.identity,
                new Vector3(scale, scale, 1f));

            Rect canvas = new Rect(0f, 0f, DesignWidth, DesignHeight);
            if (retryDialog != null && retryYes != null && retryNo != null)
            {
                GUI.DrawTexture(canvas, retryDialog, ScaleMode.StretchToFill, true);
                GUI.DrawTexture(canvas, retryYes, ScaleMode.StretchToFill, true);
                GUI.DrawTexture(canvas, retryNo, ScaleMode.StretchToFill, true);
            }
            else
                GUI.Label(new Rect(0f, 110f, DesignWidth, 45f), "Retry?",
                    LabelStyle(30, TextAnchor.MiddleCenter, Ivory));
            if (GUI.Button(RetryYesButton, retryYes == null ? "YES" : "", GUIStyle.none))
            {
                UISoundPlayer.Play(UISoundCue.Click);
                battle.TryConfirmRetry();
            }
            if (GUI.Button(RetryNoButton, retryNo == null ? "NO" : "", GUIStyle.none))
            {
                UISoundPlayer.Play(UISoundCue.Click);
                battle.TryDeclineRetry();
            }

            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
            GUI.depth = oldDepth;
        }

        private static Texture2D LoadRetryTexture(string name)
        {
            Sprite sprite = Resources.Load<Sprite>(name);
            return sprite != null ? sprite.texture : null;
        }

        private void DrawSlot(int slot)
        {
            float center = SlotCenters[slot];
            DrawDisc(new Rect(center - 30f, 116f, 60f, 60f), Background);
            for (int dot = 0; dot < 44; dot++)
            {
                float angle = dot * Mathf.PI * 2f / 44f;
                Fill(new Rect(center + Mathf.Cos(angle) * 32f - 1.4f,
                    146f + Mathf.Sin(angle) * 32f - 1.4f, 2.8f, 2.8f), Gold);
            }

            WeaponDefinition definition = weapons.SelectedWeaponAt(slot);
            if (definition == null) return;
            hud?.DrawAvailableWeaponIcon(definition, new Rect(center - 22f, 124f, 44f, 44f));
            if (GUI.Button(new Rect(center - 33f, 113f, 66f, 66f), GUIContent.none, GUIStyle.none))
            {
                UISoundPlayer.Play(UISoundCue.PageTurn);
                weapons.TryRemoveSelectionSlot(slot);
            }
        }

        private void DrawConfirm()
        {
            Rect button = new Rect(264f, 210f, 96f, 36f);
            bool ready = weapons.SelectedCount > 0 && !battle.HasPendingWeaponDiscard;
            Fill(button, ready ? Gold : new Color(0.39f, 0.34f, 0.3f));
            Border(button, 1f, ready ? Ivory : Gold);
            GUI.Label(button, "确认", LabelStyle(17, TextAnchor.MiddleCenter,
                ready ? Background : Ivory));
            if (GUI.Button(button, GUIContent.none, GUIStyle.none) &&
                ready)
            {
                UISoundPlayer.Play(UISoundCue.Click);
                battle.TryConfirmLoadoutAndStart();
            }
        }

        private void DrawInventory()
        {
            const float top = 298f;
            float cellWidth = DesignWidth / 9f;
            Fill(new Rect(0f, top - 1.5f, DesignWidth, 1.5f), Gold);
            for (int index = 0; index < 9; index++)
            {
                Rect cell = new Rect(index * cellWidth, top, cellWidth, DesignHeight - top);
                bool available = weapons.AvailableWeapons != null &&
                    index < weapons.AvailableWeapons.Count &&
                    weapons.AvailableWeapons[index] != null && weapons.IsOwned(index);
                Fill(cell, Panel);
                Border(cell, 1f, Gold);
                if (!available) continue;
                hud?.DrawAvailableWeaponIcon(weapons.AvailableWeapons[index],
                    new Rect(cell.x + 11f, cell.y + 3f, cell.width - 22f, cell.height - 6f));
                if (weapons.IsSelected(index))
                    Fill(new Rect(cell.x + 1f, cell.y + 1f, cell.width - 2f, cell.height - 2f),
                        new Color(0.32f, 0.32f, 0.32f, 0.72f));
                if (!weapons.IsSelected(index) &&
                    GUI.Button(cell, new GUIContent("", weapons.AvailableWeapons[index].DisplayName), GUIStyle.none))
                {
                    UISoundPlayer.Play(UISoundCue.PageTurn);
                    weapons.TrySelect(index);
                }
            }
        }

        private void DrawDiscardPrompt()
        {
            int oldDepth = GUI.depth;
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUI.depth = -100;
            Fill(new Rect(0, 0, Screen.width, Screen.height), Background);
            float scale = Mathf.Min(Screen.width / DesignWidth, Screen.height / DesignHeight);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - DesignWidth * scale) / 2f,
                (Screen.height - DesignHeight * scale) / 2f, 0f), Quaternion.identity,
                new Vector3(scale, scale, 1f));
            Rect panel = new Rect(85f, 85f, 454f, 180f);
            Fill(panel, Panel);
            Border(panel, 2f, Gold);
            GUI.Label(new Rect(110f, 105f, 404f, 35f), "战斗胜利",
                LabelStyle(23, TextAnchor.MiddleCenter, Ivory));
            GUI.Label(new Rect(110f, 140f, 404f, 27f), "生命值偏低，请选择一把武器丢弃",
                LabelStyle(14, TextAnchor.MiddleCenter, Ivory));
            int count = weapons.Loadout.Weapons.Count;
            for (int slot = 0; slot < count; slot++)
            {
                int selectedSlot = slot;
                Rect button = new Rect(116f + slot * 136f, 190f, 120f, 38f);
                Border(button, 1f, Gold);
                GUI.Label(button, "丢弃 " + weapons.Loadout.Weapons[slot].Definition.DisplayName,
                    LabelStyle(14, TextAnchor.MiddleCenter, Ivory));
                if (GUI.Button(button, GUIContent.none, GUIStyle.none))
                {
                    UISoundPlayer.Play(UISoundCue.Click);
                    battle.ResolveWeaponDiscard(selectedSlot);
                }
            }
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
            GUI.depth = oldDepth;
        }

        private static GUIStyle LabelStyle(int size, TextAnchor alignment, Color color)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                alignment = alignment,
                normal = { textColor = color }
            };
        }

        private static void Border(Rect rect, float thickness, Color color)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void DrawDisc(Rect bounds, Color color)
        {
            float radius = bounds.width * 0.5f;
            float centerX = bounds.x + radius;
            float centerY = bounds.y + radius;
            for (int line = 0; line < Mathf.CeilToInt(bounds.height); line++)
            {
                float dy = line + 0.5f - radius;
                float halfWidth = Mathf.Sqrt(Mathf.Max(0f, radius * radius - dy * dy));
                Fill(new Rect(centerX - halfWidth, centerY + dy, halfWidth * 2f, 1f), color);
            }
        }

    }
}
