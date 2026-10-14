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
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string BattleDataFolder = ModulesFolder + "/Battle/Data";
        private const string PrototypeWeaponPath = BattleDataFolder + "/PrototypeWeapon.asset";
        private const string PrototypeShotgunPath = BattleDataFolder + "/PrototypeShotgun.asset";
        private const string PrototypeRiflePath = BattleDataFolder + "/PrototypeRifle.asset";

        private static readonly string[] DeferredFolders =
        {
            ModulesFolder + "/Tasks",
            ModulesFolder + "/Rules",
            ModulesFolder + "/Anomalies",
            ModulesFolder + "/Battle",
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
            WeaponDefinition[] prototypeWeapons = CreateOrUpdatePrototypeWeapons();

            BuildBootstrapScene();
            BuildGameplayScene(interactableLayer, walkableLayer, prototypeWeapons);
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
            WeaponDefinition[] prototypeWeapons)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject gameplayRoot = new GameObject("GameplayRoot");
            GameObject systems = NewChild(gameplayRoot, "Systems");
            GameplayCoordinator coordinator = systems.AddComponent<GameplayCoordinator>();
            coordinator.Configure(true);
            InteractionController interactionController = systems.AddComponent<InteractionController>();
            InputRouter inputRouter = systems.AddComponent<InputRouter>();

            GameObject world = NewChild(gameplayRoot, "World");
            CreateSpriteBox("WalkableArea", world.transform, new Vector3(0f, -2f, 0f),
                new Vector2(18f, 1.2f), new Color(0.22f, 0.25f, 0.31f, 1f), walkableLayer, -10)
                .AddComponent<WalkableArea>();

            GameObject playerObject = CreateSpriteBox("Player", world.transform, new Vector3(0f, -1.1f, 0f),
                new Vector2(0.8f, 1.4f), new Color(0.2f, 0.62f, 1f, 1f), 0, 10);
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
                playerMovement, playerObject, mainCamera, prototypeWeapons);

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
            GameObject playerObject, Camera mainCamera, WeaponDefinition[] prototypeWeapons)
        {
            GameObject battleSystems = NewChild(systems, "BattleSystems");
            BattleArena arena = battleSystems.AddComponent<BattleArena>();
            arena.Configure(8, 1.6f, -5.6f, 0);

            CombatHealth playerHealth = playerObject.AddComponent<CombatHealth>();
            playerHealth.Configure(100f);
            PlayerBattleController battlePlayer = playerObject.AddComponent<PlayerBattleController>();
            battlePlayer.Configure(arena, playerHealth,
                new BattlePosition(BattleLane.Corridor, 7));

            GameObject battleVisualRoot = NewChild(world, "BattleEnvironment");
            CreateBattleGrid(battleVisualRoot.transform, arena);

            GameObject coverAObject = CreateSpriteBox("Cover_Front_3", battleVisualRoot.transform,
                arena.GetWorldPosition(new BattlePosition(BattleLane.Foreground, 3)),
                new Vector2(1.25f, 0.8f), new Color(0.55f, 0.34f, 0.18f, 1f), 0, 6);
            CoverController coverA = coverAObject.AddComponent<CoverController>();
            coverA.Configure(new BattlePosition(BattleLane.Foreground, 3), 1);

            GameObject coverBObject = CreateSpriteBox("Cover_Back_5", battleVisualRoot.transform,
                arena.GetWorldPosition(new BattlePosition(BattleLane.Background, 5)),
                new Vector2(1.25f, 0.8f), new Color(0.55f, 0.34f, 0.18f, 1f), 0, 6);
            CoverController coverB = coverBObject.AddComponent<CoverController>();
            coverB.Configure(new BattlePosition(BattleLane.Background, 5), 1);
            GameObject coverCObject = CreateSpriteBox("Cover_Front_4", battleVisualRoot.transform,
                arena.GetWorldPosition(new BattlePosition(BattleLane.Foreground, 4)),
                new Vector2(1.25f, 0.8f), new Color(0.55f, 0.34f, 0.18f, 1f), 0, 6);
            CoverController coverC = coverCObject.AddComponent<CoverController>();
            coverC.Configure(new BattlePosition(BattleLane.Foreground, 4), 1);
            CoverController[] covers = { coverA, coverB, coverC };
            arena.ConfigureTables(covers);

            GameObject bossObject = CreateSpriteBox("PrototypeBoss", battleVisualRoot.transform,
                arena.GetWorldPosition(new BattlePosition(BattleLane.Corridor, 4)),
                new Vector2(1.2f, 1.8f), new Color(0.88f, 0.2f, 0.24f, 1f), 0, 15);
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
            weapons.Configure(prototypeWeapons, battlePlayer, boss, arena);

            BattleCameraRig cameraRig = battleSystems.AddComponent<BattleCameraRig>();
            cameraRig.Configure(mainCamera, 5f, 6.2f);

            BattleCoordinator battle = battleSystems.AddComponent<BattleCoordinator>();
            battle.Configure(coordinator, inputRouter, playerMovement, battlePlayer, boss, weapons,
                arena, covers, battleVisualRoot, cameraRig);

            BattleCommandController commands = battleSystems.AddComponent<BattleCommandController>();
            commands.Configure(battle, battlePlayer, weapons);
            inputRouter.SetCommandSink(commands);

            GameObject primaryWarning = CreateVisualBox("PrimaryAttackWarning", battleVisualRoot.transform,
                Vector3.zero, new Vector2(1.45f, 1.15f), new Color(1f, 0.15f, 0.08f, 0.65f), 25);
            GameObject secondaryWarning = CreateVisualBox("SecondaryAttackWarning", battleVisualRoot.transform,
                Vector3.zero, new Vector2(1.45f, 1.15f), new Color(1f, 0.55f, 0.08f, 0.65f), 24);
            BattleTelegraphView telegraphView = battleSystems.AddComponent<BattleTelegraphView>();
            telegraphView.Configure(boss, arena, primaryWarning, secondaryWarning);

            BattleDebugOverlay battleOverlay = battleSystems.AddComponent<BattleDebugOverlay>();
            battleOverlay.Configure(battle, battlePlayer, boss, weapons);
            battleVisualRoot.SetActive(false);
        }

        private static void CreateBattleGrid(Transform parent, BattleArena arena)
        {
            Color[] colors =
            {
                new Color(0.12f, 0.25f, 0.32f, 0.8f),
                new Color(0.18f, 0.18f, 0.23f, 0.8f),
                new Color(0.18f, 0.28f, 0.2f, 0.8f)
            };

            for (int lane = 0; lane < 3; lane++)
            {
                for (int column = 0; column < arena.ColumnCount; column++)
                {
                    BattlePosition position = new BattlePosition((BattleLane)lane, column);
                    CreateVisualBox($"Cell_{position.Lane}_{column}", parent, arena.GetWorldPosition(position),
                        new Vector2(1.45f, 1.15f), colors[lane], 0);
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

        private static WeaponDefinition[] CreateOrUpdatePrototypeWeapons()
        {
            return new[]
            {
                CreateOrUpdateWeapon(PrototypeWeaponPath, "prototype_sidearm", "Prototype Sidearm",
                    120f, 1.25f, 4),
                CreateOrUpdateWeapon(PrototypeShotgunPath, "prototype_shotgun", "Prototype Shotgun",
                    220f, 2.5f, 2),
                CreateOrUpdateWeapon(PrototypeRiflePath, "prototype_rifle", "Prototype Rifle",
                    80f, 0.65f, 6)
            };
        }

        private static WeaponDefinition CreateOrUpdateWeapon(string path, string id, string displayName,
            float damage, float cooldown, int range)
        {
            WeaponDefinition weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (weapon == null)
            {
                weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(weapon, path);
            }

            weapon.Configure(id, displayName, damage, cooldown, range, DamageKind.Ranged);
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
