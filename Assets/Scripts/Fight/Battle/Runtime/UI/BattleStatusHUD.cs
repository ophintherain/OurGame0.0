using UnityEngine;

namespace Spotlight.Battle
{
    public sealed class BattleStatusHUD : MonoBehaviour
    {
        [SerializeField] private BattleCoordinator battle;
        [SerializeField] private PlayerBattleController player;
        [SerializeField] private BossController boss;
        [SerializeField] private BattleArena arena;
        [SerializeField] private Sprite overviewMap;
        [SerializeField] private Sprite playerMarker;
        [SerializeField] private Sprite bossMarker;
        [SerializeField] private Sprite playerPortrait;
        [SerializeField] private Sprite[] bossPortraits;
        [SerializeField] private Sprite healthTrack;
        [SerializeField] private Sprite healthFill;
        [SerializeField] private Sprite energyTrack;
        [SerializeField] private Sprite energyFill;
        [SerializeField] private WeaponController weapons;
        [SerializeField] private Sprite[] weaponIcons;

        private static readonly Rect[] WeaponIconPixels =
        {
            new Rect(225f, 147f, 98f, 182f),
            new Rect(135f, 385f, 346f, 137f),
            new Rect(571f, 209f, 123f, 91f),
            new Rect(577f, 378f, 131f, 177f),
            new Rect(897f, 199f, 212f, 121f),
            new Rect(202f, 571f, 136f, 166f),
            new Rect(563f, 634f, 169f, 95f),
            new Rect(962f, 379f, 70f, 122f),
            new Rect(874f, 608f, 300f, 90f)
        };

        private static readonly Rect PlayerPortraitPixels = new Rect(4873f, 173f, 476f, 476f);
        private static readonly Rect[] BossPortraitPixels =
        {
            new Rect(6101f, 810f, 476f, 476f),
            new Rect(5535f, 804f, 476f, 476f),
            new Rect(5521f, 266f, 476f, 476f)
        };
        private static readonly Rect HealthTrackPixels = new Rect(6125f, 386f, 1597f, 154f);
        private static readonly Rect HealthFillPixels = new Rect(6152f, 410f, 1536f, 109f);
        private static readonly Rect EnergyTrackPixels = new Rect(6126f, 585f, 1597f, 73f);
        private static readonly Rect EnergyFillPixels = new Rect(6158f, 603f, 1527f, 38f);
        private static readonly Rect OverviewMapPixels = new Rect(212f, 159f, 1889f, 1180f);
        private static readonly Rect PlayerMarkerPixels = new Rect(1108f, 675f, 116f, 133f);
        private static readonly Rect BossMarkerPixels = new Rect(1610f, 595f, 245f, 248f);

        public static float OverviewWidth(float screenWidth) => Mathf.Min(440f, screenWidth * 0.231f);
        public static float OverviewHeight(float screenWidth) =>
            OverviewWidth(screenWidth) * OverviewMapPixels.height / OverviewMapPixels.width;

        public void Configure(BattleCoordinator coordinator, PlayerBattleController battlePlayer,
            BossController battleBoss, Sprite portrait, Sprite[] carriagePortraits,
            Sprite hpTrack, Sprite hpFill, Sprite enTrack, Sprite enFill)
        {
            battle = coordinator;
            player = battlePlayer;
            boss = battleBoss;
            playerPortrait = portrait;
            bossPortraits = carriagePortraits;
            healthTrack = hpTrack;
            healthFill = hpFill;
            energyTrack = enTrack;
            energyFill = enFill;
        }

        public void ConfigureOverview(BattleArena battleArena,
            Sprite map, Sprite playerDot, Sprite bossDot)
        {
            arena = battleArena;
            overviewMap = map;
            playerMarker = playerDot;
            bossMarker = bossDot;
        }

        public void ConfigureWeapons(WeaponController weaponController, Sprite[] icons)
        {
            weapons = weaponController;
            weaponIcons = icons;
        }

        private void OnGUI()
        {
            if (battle == null || player == null || boss == null || !battle.IsBattleActive) return;

            int oldDepth = GUI.depth;
            Color oldColor = GUI.color;
            GUI.depth = -20;
            GUI.color = Color.white;
            float mapWidth = OverviewWidth(Screen.width);
            float left = Screen.width * 0.02f;
            float right = Screen.width - 12f;
            float portraitSize = Mathf.Clamp(Screen.width * 0.04f, 58f, 72f);
            float gap = 8f;
            float centerGap = 12f;
            float available = right - left - mapWidth - Mathf.Max(32f, Screen.width * 0.05f)
                - portraitSize * 2f - gap * 2f - centerGap;
            float barWidth = Mathf.Min(535f, available * 0.5f);
            float bossPortraitX = right - (barWidth * 2f + portraitSize * 2f + gap * 2f + centerGap);
            float bossBarX = bossPortraitX + portraitSize + gap;
            float playerPortraitX = bossBarX + barWidth + centerGap;
            float playerBarX = playerPortraitX + portraitSize + gap;

            int bossIndex = Mathf.Clamp(boss.CarriageIndex - 1, 0, 2);
            Sprite bossPortrait = bossPortraits != null && bossIndex < bossPortraits.Length
                ? bossPortraits[bossIndex] : null;
            DrawHealthBar(new Rect(bossBarX, 24f, barWidth, 33f), boss.Health);
            DrawEnergyBar(new Rect(bossBarX, 64f, barWidth, 16.5f));
            DrawSpritePart(bossPortrait, new Rect(bossPortraitX, 16f, portraitSize, portraitSize),
                BossPortraitPixels[bossIndex]);
            DrawSpritePart(playerPortrait, new Rect(playerPortraitX, 16f, portraitSize, portraitSize),
                PlayerPortraitPixels);
            DrawHealthBar(new Rect(playerBarX, 24f, barWidth, 33f), player.Health);
            DrawOverview(left, mapWidth);
            DrawWeaponSlots();
            GUI.color = oldColor;
            GUI.depth = oldDepth;
        }

        private void DrawOverview(float left, float width)
        {
            if (arena == null || overviewMap == null) return;
            Rect map = new Rect(left, 16f, width, OverviewHeight(Screen.width));
            DrawSpritePart(overviewMap, map, OverviewMapPixels);
            DrawMarker(playerMarker, PlayerMarkerPixels, player.Position, map, 18f);
            DrawMarker(bossMarker, BossMarkerPixels, boss.Position, map, 22f);
        }

        private void DrawMarker(Sprite marker, Rect pixels, BattlePosition position, Rect map, float size)
        {
            if (marker == null) return;
            float column = Mathf.Clamp(position.Column, 0, arena.ColumnCount - 1);
            float horizontal = position.Lane == BattleLane.Foreground
                ? Mathf.Lerp(0.3f, 0.7f, Mathf.Clamp01((column - 2f) / 4f))
                : Mathf.Lerp(0.095f, 0.905f, column / Mathf.Max(1f, arena.ColumnCount - 1f));
            float x = map.x + map.width * horizontal;
            float laneY = position.Lane switch
            {
                BattleLane.Background => 0.29f,
                BattleLane.Foreground => 0.72f,
                _ => 0.5f
            };
            float y = map.y + map.height * laneY;
            DrawSpritePart(marker, new Rect(x - size * 0.5f, y - size * 0.5f, size, size), pixels);
        }

        private void DrawHealthBar(Rect hp, CombatHealth health)
        {
            float current = health != null ? health.CurrentHealth : 0f;
            float maximum = health != null ? health.MaxHealth : 0f;
            DrawSpritePart(healthTrack, hp, HealthTrackPixels);
            DrawSpritePart(healthFill, new Rect(hp.x + 5.5f, hp.y + 5.5f,
                    hp.width - 11f, hp.height - 11f),
                HealthFillPixels, maximum > 0f ? current / maximum : 0f);
        }

        private void DrawEnergyBar(Rect en)
        {
            float energy = boss.Energy != null ? boss.Energy.Current : 0f;
            float capacity = boss.Energy != null ? boss.Energy.Maximum : 0f;
            DrawSpritePart(energyTrack, en, EnergyTrackPixels);
            DrawSpritePart(energyFill, new Rect(en.x + 6f, en.y + 4f, en.width - 12f, en.height - 8f),
                EnergyFillPixels, capacity > 0f ? energy / capacity : 0f);
        }

        private void DrawWeaponSlots()
        {
            if (weapons == null) return;
            int count = Mathf.Min(BattleLoadout.MaximumWeapons, weapons.Loadout.Weapons.Count);
            if (count == 0) return;

            float slotWidth = Mathf.Clamp(Screen.width * 0.085f, 92f, 116f);
            float slotHeight = slotWidth + 26f;
            float spacing = Mathf.Clamp(Screen.width * 0.012f, 9f, 18f);
            float startX = (Screen.width - count * slotWidth - (count - 1) * spacing) * 0.5f;
            float top = Screen.height - slotHeight - 18f;
            string[] keys = { "F", "G", "H" };

            GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(slotWidth * 0.14f), 12, 17),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.93f, 0.78f) }
            };
            GUIStyle timerStyle = new GUIStyle(nameStyle)
            {
                fontSize = Mathf.RoundToInt(slotWidth * 0.28f),
                normal = { textColor = Color.white }
            };
            GUIStyle cooldownStyle = new GUIStyle(nameStyle)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 12
            };

            for (int slot = 0; slot < count; slot++)
            {
                WeaponRuntime runtime = weapons.Loadout.Weapons[slot];
                WeaponDefinition definition = runtime.Definition;
                Rect card = new Rect(startX + slot * (slotWidth + spacing), top, slotWidth, slotHeight);
                DrawTint(card, new Color(0.13f, 0.09f, 0.09f, 0.84f));
                DrawTint(new Rect(card.x, card.y, card.width, 2f), new Color(0.75f, 0.61f, 0.42f));
                Rect iconArea = new Rect(card.x + 7f, card.y + 8f, card.width - 14f, card.width - 12f);
                int iconIndex = IconIndex(definition != null ? definition.Id : null);
                if (iconIndex >= 0 && weaponIcons != null && iconIndex < weaponIcons.Length)
                    DrawWeaponIcon(weaponIcons[iconIndex], iconArea, WeaponIconPixels[iconIndex]);

                float remaining = runtime.CooldownRemaining;
                if (remaining > 0f)
                {
                    float fraction = definition != null && definition.CooldownSeconds > 0f
                        ? Mathf.Clamp01(remaining / definition.CooldownSeconds) : 0f;
                    DrawTint(new Rect(iconArea.x, iconArea.y + iconArea.height * (1f - fraction),
                        iconArea.width, iconArea.height * fraction), new Color(0f, 0f, 0f, 0.7f));
                    GUI.Label(iconArea, Mathf.CeilToInt(remaining).ToString(), timerStyle);
                }

                GUI.Label(new Rect(card.x + 4f, card.y + card.height - 25f, card.width - 8f, 22f),
                    definition != null ? definition.DisplayName : "", nameStyle);
                DrawTint(new Rect(card.x + 3f, card.y + 4f, 23f, 19f),
                    new Color(0.58f, 0.43f, 0.28f, 0.95f));
                GUI.Label(new Rect(card.x + 3f, card.y + 3f, 23f, 20f), keys[slot], nameStyle);
                if (definition != null)
                    GUI.Label(new Rect(card.x + card.width - 45f, card.y + 3f, 40f, 20f),
                        definition.CooldownSeconds.ToString("0") + "s", cooldownStyle);
            }
        }

        private static int IconIndex(string id)
        {
            return id switch
            {
                "truce" => 0,
                "needle" => 1,
                "leap" => 2,
                "mimic" => 3,
                "poet" => 4,
                "barrage" => 5,
                "servitude" => 6,
                "edge" => 7,
                "recorder" => 8,
                _ => -1
            };
        }

        private static void DrawWeaponIcon(Sprite sprite, Rect area, Rect pixels)
        {
            if (sprite == null || sprite.texture == null) return;
            float aspect = pixels.width / pixels.height;
            float width = Mathf.Min(area.width, area.height * aspect);
            float height = width / aspect;
            Rect fitted = new Rect(area.x + (area.width - width) * 0.5f,
                area.y + (area.height - height) * 0.5f, width, height);
            Rect uv = new Rect(pixels.x / 1332f, (867f - pixels.yMax) / 867f,
                pixels.width / 1332f, pixels.height / 867f);
            GUI.DrawTextureWithTexCoords(fitted, sprite.texture, uv, true);
        }

        private static void DrawTint(Rect rect, Color tint)
        {
            Color previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void DrawSpritePart(Sprite sprite, Rect target, Rect sourcePixels, float fraction = 1f)
        {
            if (sprite == null || sprite.texture == null) return;
            float visible = Mathf.Clamp01(fraction);
            if (visible <= 0f) return;
            Rect uv = new Rect(sourcePixels.x / 8000f, (4500f - sourcePixels.yMax) / 4500f,
                sourcePixels.width / 8000f * visible, sourcePixels.height / 4500f);
            target.width *= visible;
            GUI.DrawTextureWithTexCoords(target, sprite.texture, uv, true);
        }
    }
}
