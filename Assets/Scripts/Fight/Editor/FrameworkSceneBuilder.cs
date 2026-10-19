using System;
using System.Collections.Generic;
using System.IO;
using Spotlight;
using Spotlight.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.Editor
{
    public static class FrameworkSceneBuilder
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string ModulesFolder = "Assets/Modules";
        private const string BootstrapPath = ScenesFolder + "/Bootstrap.unity";
        private const string GameplayPath = ScenesFolder + "/Gameplay.unity";
        private const string ExplorationPath = ScenesFolder + "/explore/first.unity";
        private const string FirstFightPath = ScenesFolder + "/fight/fight_1.unity";
        private const string SecondFightPath = ScenesFolder + "/fight/fight_2.unity";
        private const string ThirdFightPath = ScenesFolder + "/fight/fight_3.unity";
        private const string TestFightPath = ScenesFolder + "/fight/fight_test.unity";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string BattleDataFolder = ModulesFolder + "/Battle/Data";
        private const string TrucePath = BattleDataFolder + "/Truce.asset";
        private const string NeedlePath = BattleDataFolder + "/Needle.asset";
        private const string BarragePath = BattleDataFolder + "/Barrage.asset";
        private const string LeapPath = BattleDataFolder + "/Leap.asset";
        private const string MimicPath = BattleDataFolder + "/Mimic.asset";
        private const string ServitudePath = BattleDataFolder + "/Servitude.asset";
        private const string PoetPath = BattleDataFolder + "/Poet.asset";
        private const string EdgePath = BattleDataFolder + "/Edge.asset";
        private const string RecorderPath = BattleDataFolder + "/Recorder.asset";
        private const string ArtworkFolder = "Assets/sprite";

        private static readonly string[] DeferredFolders =
        {
            ModulesFolder + "/Tasks",
            ModulesFolder + "/Rules",
            ModulesFolder + "/Anomalies",
            ModulesFolder + "/Inventory",
            ModulesFolder + "/UI",
            ModulesFolder + "/Audio",
            ModulesFolder + "/Save",
            ModulesFolder + "/Data"
        };

        [MenuItem("Spotlight/Framework/Rebuild Scenes")]
        public static void Rebuild()
        {
            EnsureFolder(ScenesFolder);
            foreach (string folder in DeferredFolders)
            {
                EnsureFolder(folder);
            }
            EnsureFolder(BattleDataFolder);

            int interactableLayer = EnsureLayer("Interactable");
            int walkableLayer = EnsureLayer("Walkable");
            WeaponDefinition[] skillWeapons = CreateOrUpdateSkillWeapons();

            BuildBootstrapScene();
            BuildGameplayScene(interactableLayer, walkableLayer, skillWeapons);
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Spotlight] Framework scenes, layers, folders and Build Settings rebuilt successfully.");
        }

        private static void BuildBootstrapScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("PersistentRoot");
            RunSession session = root.AddComponent<RunSession>();
            SceneLoader loader = root.AddComponent<SceneLoader>();
            GameFlowController flow = root.AddComponent<GameFlowController>();
            GameBootstrap bootstrap = root.AddComponent<GameBootstrap>();

            flow.Initialize(session, loader);
            bootstrap.Configure(session, loader, flow, true);
            EditorSceneManager.SaveScene(scene, BootstrapPath);
        }

        private static void BuildGameplayScene(int interactableLayer, int walkableLayer,
            WeaponDefinition[] skillWeapons)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject gameplayRoot = new GameObject("GameplayRoot");
            GameObject systems = NewChild(gameplayRoot, "Systems");
            GameplayCoordinator coordinator = systems.AddComponent<GameplayCoordinator>();
            coordinator.Configure(true);
            InteractionController interactionController = systems.AddComponent<InteractionController>();
            InputRouter inputRouter = systems.AddComponent<InputRouter>();

            GameObject world = NewChild(gameplayRoot, "World");
            CreateArtwork("ExplorationBackground", world.transform, ArtworkFolder + "/Background/1001.png",
                Vector3.zero, new Vector2(17.78f, 10f), -100);
            CreateSpriteBox("WalkableArea", world.transform, new Vector3(0f, -2f, 0f),
                new Vector2(18f, 1.2f), new Color(0.22f, 0.25f, 0.31f, 1f), walkableLayer, -10)
                .AddComponent<WalkableArea>();

            GameObject playerObject = CreateSpriteBox("Player", world.transform, new Vector3(0f, -1.1f, 0f),
                new Vector2(0.8f, 1.4f), new Color(0.2f, 0.62f, 1f, 1f), 0, 10);
            SpriteRenderer playerArtwork = CreateArtwork("PlayerArtwork", playerObject.transform,
                ArtworkFolder + "/Player/4001b.png", playerObject.transform.position,
                new Vector2(3.2f, 5.2f), 10);
            if (playerArtwork != null)
            {
                playerArtwork.transform.localPosition = new Vector3(0f, 2.25f, 0f);
                playerObject.GetComponent<SpriteRenderer>().enabled = false;
            }
            PlayerMovement playerMovement = playerObject.AddComponent<PlayerMovement>();
            playerMovement.Configure(5f, -8f, 8f);

            GameObject probeObject = CreateSpriteBox("InteractionProbe", world.transform, new Vector3(5.3f, -1.75f, 0f),
                new Vector2(0.9f, 0.9f), new Color(0.2f, 0.85f, 0.45f, 1f), interactableLayer, 12);
            InteractionProbe probe = probeObject.AddComponent<InteractionProbe>();

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(gameplayRoot.transform);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera mainCamera = cameraObject.AddComponent<Camera>();
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 5f;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 1f);
            cameraObject.AddComponent<AudioListener>();
            CameraController cameraController = cameraObject.AddComponent<CameraController>();
            cameraController.Configure(cameraObject.transform, -6f, 6f, 0.2f);
            cameraController.SetFollowTarget(playerObject.transform, mainCamera);

            BuildBattleGreybox(gameplayRoot, systems, world, coordinator, inputRouter,
                playerMovement, playerObject, mainCamera, skillWeapons);

            interactionController.Configure(playerObject.transform, coordinator);
            inputRouter.Configure(mainCamera, interactionController, playerMovement,
                1 << interactableLayer, 1 << walkableLayer);

            CreateEventSystem(gameplayRoot.transform);
            CreateBlockingTestUI(gameplayRoot.transform);

            FrameworkDebugOverlay overlay = systems.AddComponent<FrameworkDebugOverlay>();
            overlay.Configure(coordinator, inputRouter, playerMovement, cameraController, probe);

            EditorSceneManager.SaveScene(scene, GameplayPath);
        }

        private static void BuildBattleGreybox(GameObject gameplayRoot, GameObject systems, GameObject world,
            GameplayCoordinator coordinator, InputRouter inputRouter, PlayerMovement playerMovement,
            GameObject playerObject, Camera mainCamera, WeaponDefinition[] skillWeapons)
        {
            GameObject battleSystems = NewChild(systems, "BattleSystems");
            BattleArena arena = battleSystems.AddComponent<BattleArena>();
            arena.Configure(9, 1.8f, -7.2f, 0);

            CombatHealth playerHealth = playerObject.AddComponent<CombatHealth>();
            playerHealth.Configure(100f);
            PlayerBattleController battlePlayer = playerObject.AddComponent<PlayerBattleController>();
            battlePlayer.Configure(arena, playerHealth,
                new BattlePosition(BattleLane.Corridor, 8));
            battlePlayer.ConfigureArtwork(
                AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkFolder + "/Player/4001b.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkFolder + "/Player/4001a.png"));

            GameObject battleVisualRoot = NewChild(world, "BattleEnvironment");
            CreateArtwork("BattleBackground", battleVisualRoot.transform, ArtworkFolder + "/Background/10011a.png",
                Vector3.zero, new Vector2(17.78f, 10f), -100);
            CreateArtwork("BattleForeground", battleVisualRoot.transform,
                ArtworkFolder + "/Background/10011b.png", new Vector3(0f, -1.4f, 0f),
                new Vector2(17.78f, 10f), 20);
            CoverController[] covers =
            {
                CreateArtworkTable(battleVisualRoot.transform, BattleLane.Foreground, 2,
                    -4.3f, -3.8f, new Vector2(2.1f, 2.2f)),
                CreateArtworkTable(battleVisualRoot.transform, BattleLane.Foreground, 4,
                    1.03f, -3.8f, new Vector2(2.1f, 2.2f)),
                CreateArtworkTable(battleVisualRoot.transform, BattleLane.Foreground, 6,
                    6.52f, -3.8f, new Vector2(2.1f, 2.2f)),
                CreateArtworkTable(battleVisualRoot.transform, BattleLane.Background, 0,
                    -7.65f, -0.85f, new Vector2(1.6f, 1.3f)),
                CreateArtworkTable(battleVisualRoot.transform, BattleLane.Background, 2,
                    -3.9f, -0.85f, new Vector2(1.6f, 1.3f)),
                CreateArtworkTable(battleVisualRoot.transform, BattleLane.Background, 4,
                    -0.05f, -0.85f, new Vector2(1.6f, 1.3f)),
                CreateArtworkTable(battleVisualRoot.transform, BattleLane.Background, 6,
                    3.75f, -0.85f, new Vector2(1.6f, 1.3f)),
                CreateArtworkTable(battleVisualRoot.transform, BattleLane.Background, 8,
                    7.6f, -0.85f, new Vector2(1.6f, 1.3f))
            };
            arena.ConfigureTables(covers);
            CreateBattleGrid(battleVisualRoot.transform, arena);

            GameObject bossObject = CreateSpriteBox("PrototypeBoss", battleVisualRoot.transform,
                arena.GetWorldPosition(new BattlePosition(BattleLane.Corridor, 4)),
                new Vector2(1.2f, 1.8f), new Color(0.88f, 0.2f, 0.24f, 1f), 0, 15);
            Sprite bossSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkFolder + "/Bosses/4002.png");
            if (bossSprite != null)
            {
                bossObject.GetComponent<SpriteRenderer>().enabled = false;
                SpriteRenderer bossArtwork = CreateArtwork("BossArtwork", bossObject.transform, ArtworkFolder + "/Bosses/4002.png",
                    bossObject.transform.position, new Vector2(3.4f, 5f), 15);
                if (bossArtwork != null)
                    bossArtwork.transform.localPosition = new Vector3(0f, 1.75f, 0f);
            }
            CombatHealth bossHealth = bossObject.AddComponent<CombatHealth>();
            bossHealth.Configure(1200f);
            BossEnergy bossEnergy = bossObject.AddComponent<BossEnergy>();
            bossEnergy.Configure(200f, 15f, 0.2f);
            BossMovement bossMovement = bossObject.AddComponent<BossMovement>();
            bossMovement.Configure(arena, 4, 1.5f);
            BossController boss = bossObject.AddComponent<BossController>();
            boss.Configure(bossHealth, bossEnergy, bossMovement, battlePlayer, covers);
            boss.ConfigureAttacks(3f, 1f, 15f, 30f);

            WeaponController weapons = battleSystems.AddComponent<WeaponController>();
            weapons.Configure(skillWeapons, battlePlayer, boss, arena);

            BattleCameraRig cameraRig = battleSystems.AddComponent<BattleCameraRig>();
            cameraRig.Configure(mainCamera, 5f, 6.2f, battleVisualRoot.transform,
                arena, battlePlayer);

            BattleCoordinator battle = battleSystems.AddComponent<BattleCoordinator>();
            battle.Configure(coordinator, inputRouter, playerMovement, battlePlayer, boss, weapons,
                arena, covers, battleVisualRoot, cameraRig);

            BattleCommandController commands = battleSystems.AddComponent<BattleCommandController>();
            commands.Configure(battle, battlePlayer, weapons);
            inputRouter.SetCommandSink(commands);

            GameObject primaryWarning = CreateVisualBox("PrimaryAttackWarning", battleVisualRoot.transform,
                Vector3.zero, new Vector2(2.2f, 1.35f), new Color(1f, 0.15f, 0.08f, 0.65f), 25);
            GameObject secondaryWarning = CreateVisualBox("SecondaryAttackWarning", battleVisualRoot.transform,
                Vector3.zero, new Vector2(2.2f, 1.35f), new Color(1f, 0.55f, 0.08f, 0.65f), 24);
            BattleTelegraphView telegraphView = battleSystems.AddComponent<BattleTelegraphView>();
            telegraphView.Configure(boss, arena, primaryWarning, secondaryWarning);

            BattleDebugOverlay battleOverlay = battleSystems.AddComponent<BattleDebugOverlay>();
            battleOverlay.Configure(battle, battlePlayer, boss, weapons);
            BattleStatusHUD statusHUD = battleSystems.AddComponent<BattleStatusHUD>();
            statusHUD.Configure(battle, battlePlayer, boss,
                AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkFolder + "/UI/5002b1.png"),
                new[]
                {
                    AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkFolder + "/UI/5002b2.png"),
                    AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkFolder + "/UI/5002b3.png"),
                    AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkFolder + "/UI/5002b4.png")
                },
                AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkFolder + "/UI/5002c1.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkFolder + "/UI/5002c11.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkFolder + "/UI/5002c2.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkFolder + "/UI/5002c22.png"));
            statusHUD.ConfigureOverview(arena,
                AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkFolder + "/UI/5002a1.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkFolder + "/UI/5002a2.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkFolder + "/UI/5002a3.png"));
            Sprite[] weaponIcons = new Sprite[9];
            for (int index = 0; index < weaponIcons.Length; index++)
                weaponIcons[index] = AssetDatabase.LoadAssetAtPath<Sprite>(
                    ArtworkFolder + "/Weapons/" + (index + 1) + ".png");
            statusHUD.ConfigureWeapons(weapons, weaponIcons);
            battleVisualRoot.SetActive(false);
        }

        private static void CreateBattleGrid(Transform parent, BattleArena arena)
        {
            Color[] colors =
            {
                new Color(0.12f, 0.25f, 0.32f, 0.18f),
                new Color(0.18f, 0.18f, 0.23f, 0.22f),
                new Color(0.18f, 0.28f, 0.2f, 0.18f)
            };

            for (int lane = 0; lane < 3; lane++)
            {
                for (int column = 0; column < arena.ColumnCount; column++)
                {
                    BattlePosition position = new BattlePosition((BattleLane)lane, column);
                    if (position.Lane != BattleLane.Corridor && !arena.IsTablePosition(position))
                        continue;
                    CreateVisualBox($"Cell_{position.Lane}_{column}", parent, arena.GetWorldPosition(position),
                        new Vector2(1.8f, 1.35f), colors[lane], 0);
                }
            }
        }

        private static GameObject CreateSpriteBox(string name, Transform parent, Vector3 position,
            Vector2 size, Color color, int layer, int sortingOrder)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent);
            gameObject.transform.position = position;
            gameObject.transform.localScale = Vector3.one;
            gameObject.layer = layer;

            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;

            BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.isTrigger = true;
            return gameObject;
        }

        private static CoverController CreateArtworkTable(Transform parent, BattleLane lane,
            int column, float x, float y, Vector2 size)
        {
            string row = lane == BattleLane.Foreground ? "Front" : "Back";
            GameObject table = CreateSpriteBox($"Cover_{row}_{column}", parent,
                new Vector3(x, y, 0f), size, new Color(0.55f, 0.34f, 0.18f, 1f), 0, 6);
            CoverController cover = table.AddComponent<CoverController>();
            cover.Configure(new BattlePosition(lane, column), 1);
            if (lane == BattleLane.Foreground)
            {
                int maskNumber = column == 2 ? 3 : column == 4 ? 2 : 1;
                cover.ConfigureBrokenMask(AssetDatabase.LoadAssetAtPath<Sprite>(
                    ArtworkFolder + "/Carriage/1/010" + maskNumber + ".png"));
            }
            table.GetComponent<BoxCollider2D>().offset = lane == BattleLane.Foreground
                ? new Vector2(0f, 1f) : new Vector2(0f, 0.48f);
            table.GetComponent<SpriteRenderer>().enabled = false;
            return cover;
        }

        private static GameObject CreateVisualBox(string name, Transform parent, Vector3 position,
            Vector2 size, Color color, int sortingOrder)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent);
            gameObject.transform.position = position;

            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return gameObject;
        }

        private static SpriteRenderer CreateArtwork(string name, Transform parent, string path, Vector3 position,
            Vector2 size, int sortingOrder)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"[Spotlight] Artwork missing: {path}");
                return null;
            }

            GameObject artwork = new GameObject(name);
            artwork.transform.SetParent(parent);
            artwork.transform.position = position;
            artwork.transform.localScale = new Vector3(size.x / sprite.bounds.size.x,
                size.y / sprite.bounds.size.y, 1f);
            SpriteRenderer renderer = artwork.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static WeaponDefinition[] CreateOrUpdateSkillWeapons()
        {
            return new[]
            {
                CreateOrUpdateWeapon(TrucePath, "truce", "调停",
                    0f, 7f, 1, WeaponSkill.Truce, 1, 3),
                CreateOrUpdateWeapon(NeedlePath, "needle", "针刺",
                    0f, 10f, 7, WeaponSkill.Needle, 1, 2),
                CreateOrUpdateWeapon(BarragePath, "barrage", "弹雨",
                    0f, 12f, 7, WeaponSkill.Barrage, 1, 1),
                CreateOrUpdateWeapon(LeapPath, "leap", "跃迁", 0f, 7f, 7, WeaponSkill.Leap, 2, 9),
                CreateOrUpdateWeapon(MimicPath, "mimic", "归我", 0f, 7f, 7, WeaponSkill.Mimic, 2, 4),
                CreateOrUpdateWeapon(ServitudePath, "servitude", "附庸", 0f, 12f, 7, WeaponSkill.Servitude, 2, 6),
                CreateOrUpdateWeapon(PoetPath, "poet", "诗者", 0f, 7f, 7, WeaponSkill.Poet, 3, 7),
                CreateOrUpdateWeapon(EdgePath, "edge", "夺锋", 0f, 10f, 7, WeaponSkill.Edge, 3, 8),
                CreateOrUpdateWeapon(RecorderPath, "recorder", "记录者", 0f, 20f, 7, WeaponSkill.Recorder, 3, 5)
            };
        }

        private static WeaponDefinition CreateOrUpdateWeapon(string path, string id, string displayName,
            float damage, float cooldown, int range, WeaponSkill skill, int carriage, int priority)
        {
            WeaponDefinition weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (weapon == null)
            {
                weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(weapon, path);
            }

            weapon.Configure(id, displayName, damage, cooldown, range, DamageKind.Ranged);
            weapon.ConfigureSkill(skill, carriage, priority);
            EditorUtility.SetDirty(weapon);
            return weapon;
        }

        private static void CreateEventSystem(Transform parent)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.transform.SetParent(parent);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private static void CreateBlockingTestUI(Transform parent)
        {
            GameObject canvasObject = new GameObject("TestUI", typeof(RectTransform));
            canvasObject.transform.SetParent(parent);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject panelObject = new GameObject("InputBlockingPanel", typeof(RectTransform));
            panelObject.transform.SetParent(canvasObject.transform, false);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-20f, -20f);
            panelRect.sizeDelta = new Vector2(360f, 130f);
            Image panelImage = panelObject.AddComponent<Image>();
            panelImage.color = new Color(0.34f, 0.19f, 0.5f, 0.92f);
            panelImage.raycastTarget = true;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(panelObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(16f, 12f);
            labelRect.offsetMax = new Vector2(-16f, -12f);
            Text label = labelObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 22;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = "UI BLOCK TEST\nClick here: world input must not fire";
            label.raycastTarget = true;
        }

        private static GameObject NewChild(GameObject parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent.transform);
            return child;
        }

        private static void ConfigureBuildSettings()
        {
            var scenePaths = new List<string> { BootstrapPath, GameplayPath };
            if (File.Exists(ExplorationPath)) scenePaths.Add(ExplorationPath);
            foreach (string fightPath in new[] { FirstFightPath, SecondFightPath, ThirdFightPath, TestFightPath })
            {
                if (File.Exists(fightPath)) scenePaths.Add(fightPath);
            }
            if (File.Exists(SampleScenePath))
            {
                scenePaths.Add(SampleScenePath);
            }

            EditorBuildSettings.scenes = scenePaths.ConvertAll(path => new EditorBuildSettingsScene(path, true)).ToArray();
        }

        private static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0)
            {
                return existing;
            }

            Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets.Length == 0)
            {
                throw new InvalidOperationException("Could not open ProjectSettings/TagManager.asset.");
            }

            SerializedObject tagManager = new SerializedObject(tagManagerAssets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int index = 8; index < 32; index++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(index);
                if (!string.IsNullOrEmpty(layer.stringValue))
                {
                    continue;
                }

                layer.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return index;
            }

            throw new InvalidOperationException($"No free user layer is available for '{layerName}'.");
        }

        private static void EnsureFolder(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            string parent = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
            string name = Path.GetFileName(normalized);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException($"Invalid asset folder path: {assetPath}");
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
