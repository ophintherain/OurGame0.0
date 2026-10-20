using System.Collections;
using NUnit.Framework;
using Spotlight.Battle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.Tests.PlayMode
{
    public sealed class FrameworkPlayModeTests
    {
        [UnityTest]
        public IEnumerator DuplicateBootstrap_KeepsOnlyOnePersistentRoot()
        {
            if (GameBootstrap.Instance != null)
            {
                Object.Destroy(GameBootstrap.Instance.gameObject);
                yield return null;
            }

            GameObject first = CreateBootstrap("First", false);
            GameObject second = CreateBootstrap("Second", false);
            yield return null;

            GameBootstrap[] bootstraps = Object.FindObjectsOfType<GameBootstrap>(true);
            Assert.That(bootstraps, Has.Length.EqualTo(1));
            Assert.That(GameBootstrap.Instance, Is.Not.Null);

            Object.Destroy(first);
            if (second != null)
            {
                Object.Destroy(second);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameplayGreybox_RoutesUiAndInteractionWithoutClickMovement()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Single);
            yield return null;
            Physics2D.SyncTransforms();
            Canvas.ForceUpdateCanvases();

            InputRouter router = Object.FindObjectOfType<InputRouter>();
            Camera camera = Camera.main;
            PlayerMovement player = Object.FindObjectOfType<PlayerMovement>();
            CameraController cameraController = Object.FindObjectOfType<CameraController>();
            InteractionProbe probe = Object.FindObjectOfType<InteractionProbe>();
            RectTransform uiPanel = GameObject.Find("InputBlockingPanel").GetComponent<RectTransform>();

            Assert.That(router, Is.Not.Null);

            Vector2 uiPoint = RectTransformUtility.WorldToScreenPoint(null, uiPanel.position);
            Assert.That(router.RoutePointerDown(uiPoint), Is.EqualTo(InputRouteResult.UI));

            int beforeInteraction = probe.InteractionCount;
            Vector2 probePoint = camera.WorldToScreenPoint(probe.transform.position);
            Assert.That(router.RoutePointerDown(probePoint), Is.EqualTo(InputRouteResult.Interaction));
            Assert.That(probe.InteractionCount, Is.EqualTo(beforeInteraction + 1));

            Vector2 walkablePoint = camera.WorldToScreenPoint(new Vector3(0f, -2f, 0f));
            float targetBeforeClick = player.TargetX;
            Assert.That(router.RoutePointerDown(walkablePoint), Is.EqualTo(InputRouteResult.Ignored));
            Assert.That(player.TargetX, Is.EqualTo(targetBeforeClick));

            router.SetInputEnabled(false);
            float playerTargetBeforeLockedClick = player.TargetX;
            Assert.That(router.RoutePointerDown(walkablePoint), Is.EqualTo(InputRouteResult.Disabled));
            Assert.That(player.TargetX, Is.EqualTo(playerTargetBeforeLockedClick));

            router.SetInputEnabled(true);
            float cameraStartX = camera.transform.position.x;
            player.SetTargetX(8f);
            float followDeadline = Time.realtimeSinceStartup + 5f;
            while (player.transform.position.x < 7.9f && Time.realtimeSinceStartup < followDeadline)
            {
                yield return null;
            }

            for (int index = 0; index < 20; index++)
            {
                yield return null;
            }

            Assert.That(player.transform.position.x, Is.EqualTo(8f).Within(0.1f));
            Assert.That(camera.transform.position.x, Is.GreaterThan(cameraStartX));
            Assert.That(camera.transform.position.x, Is.InRange(cameraController.MinX, cameraController.MaxX));
        }

        [UnityTest]
        public IEnumerator SceneLoader_RejectsSecondRequestWhileLoading()
        {
            GameObject loaderObject = new GameObject("SceneLoaderTest");
            Object.DontDestroyOnLoad(loaderObject);
            SceneLoader loader = loaderObject.AddComponent<SceneLoader>();

            Assert.That(loader.RequestLoad("Gameplay", "PlayModeTest"), Is.True);
            Assert.That(loader.IsLoading, Is.True);
            LogAssert.Expect(LogType.Warning,
                "[SceneLoader] Rejected 'Gameplay' from 'DuplicateRequest': a scene is already loading.");
            Assert.That(loader.RequestLoad("Gameplay", "DuplicateRequest"), Is.False);

            float deadline = Time.realtimeSinceStartup + 15f;
            while (loader != null && loader.IsLoading && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(loader, Is.Not.Null);
            Assert.That(loader.IsLoading, Is.False);
            Object.Destroy(loaderObject);
        }

        [UnityTest]
        public IEnumerator BattleGreybox_CanStartAndEscapeFromLeftCorridorEnd()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Single);
            yield return null;

            BattleCoordinator battle = Object.FindObjectOfType<BattleCoordinator>();
            PlayerBattleController player = Object.FindObjectOfType<PlayerBattleController>();
            GameplayCoordinator phases = Object.FindObjectOfType<GameplayCoordinator>();
            WeaponController weapons = Object.FindObjectOfType<WeaponController>();
            InputRouter inputRouter = Object.FindObjectOfType<InputRouter>();
            BattleCameraRig cameraRig = Object.FindObjectOfType<BattleCameraRig>();

            Assert.That(battle, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(battle.TryBeginBattle(), Is.True);
            Assert.That(phases.CurrentPhase, Is.EqualTo(GameplayPhase.Battling));
            Assert.That(weapons.Loadout.Weapons, Has.Count.EqualTo(3));
            Assert.That(inputRouter.WorldInputEnabled, Is.False);
            Assert.That(inputRouter.RouteCommand(GameplayInputCommand.Prone), Is.True);
            Assert.That(player.Posture, Is.EqualTo(PlayerPosture.Prone));
            Assert.That(inputRouter.RouteCommand(GameplayInputCommand.Stand), Is.True);
            Assert.That(inputRouter.RouteCommand(GameplayInputCommand.ToggleOverviewZoom), Is.True);
            Assert.That(cameraRig.IsOverviewZoom, Is.True);
            Assert.That(inputRouter.RouteCommand(GameplayInputCommand.ToggleOverviewZoom), Is.True);
            Assert.That(cameraRig.IsOverviewZoom, Is.False);
            Assert.That(inputRouter.RouteCommand(GameplayInputCommand.MoveLeft), Is.True);
            Assert.That(player.Position.Column, Is.EqualTo(7));

            for (int index = 0; index < 7; index++)
            {
                Assert.That(player.TryMoveHorizontal(-1), Is.True);
            }

            Assert.That(player.Position, Is.EqualTo(new BattlePosition(BattleLane.Corridor, 0)));
            Assert.That(battle.TryEscape(), Is.True);
            Assert.That(battle.LastResult.Outcome, Is.EqualTo(BattleOutcome.Escaped));
            Assert.That(phases.CurrentPhase, Is.EqualTo(GameplayPhase.BattleResult));
        }

        [UnityTest]
        public IEnumerator FightScene_OpensLoadoutBeforeBattle()
        {
            yield return SceneManager.LoadSceneAsync("fight_1", LoadSceneMode.Single);
            BattleCoordinator battle = Object.FindObjectOfType<BattleCoordinator>();
            Assert.That(battle, Is.Not.Null);

            float deadline = Time.realtimeSinceStartup + 3f;
            while (!battle.IsLoadoutSelectionOpen && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(battle.IsLoadoutSelectionOpen, Is.True);
            Assert.That(Object.FindObjectOfType<GameplayCoordinator>().CurrentPhase,
                Is.EqualTo(GameplayPhase.Exploring));
            Assert.That(GameObject.Find("BattleEnvironment"), Is.Not.Null);
        }

        private static GameObject CreateBootstrap(string name, bool autoStart)
        {
            GameObject gameObject = new GameObject(name);
            RunSession session = gameObject.AddComponent<RunSession>();
            SceneLoader loader = gameObject.AddComponent<SceneLoader>();
            GameFlowController flow = gameObject.AddComponent<GameFlowController>();
            GameBootstrap bootstrap = gameObject.AddComponent<GameBootstrap>();
            flow.Initialize(session, loader);
            bootstrap.Configure(session, loader, flow, autoStart);
            return gameObject;
        }
    }
}
