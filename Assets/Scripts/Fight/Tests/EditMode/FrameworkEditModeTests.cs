using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spotlight.Tests.EditMode
{
    public sealed class FrameworkEditModeTests
    {
        [Test]
        public void RunSession_BeginNewRun_ResetsFirstStageData()
        {
            GameObject gameObject = new GameObject("RunSessionTest");
            RunSession session = gameObject.AddComponent<RunSession>();

            session.BeginNewRun();
            string firstId = session.RunId;
            session.BeginNewRun();

            Assert.That(session.HasStarted, Is.True);
            Assert.That(session.CurrentCarriageIndex, Is.EqualTo(1));
            Assert.That(session.RunId, Is.Not.Empty);
            Assert.That(session.RunId, Is.Not.EqualTo(firstId));
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void GameplayCoordinator_ValidatesOrder_AndDoesNotRepeatEvents()
        {
            GameObject gameObject = new GameObject("CoordinatorTest");
            GameplayCoordinator coordinator = gameObject.AddComponent<GameplayCoordinator>();
            coordinator.Configure(false);
            int eventCount = 0;
            coordinator.PhaseChanged += (_, _) => eventCount++;

            Assert.That(coordinator.TryChangePhase(GameplayPhase.Exploring), Is.True);
            Assert.That(coordinator.TryChangePhase(GameplayPhase.Exploring), Is.False);
            LogAssert.Expect(LogType.Warning, "[Gameplay] Illegal phase transition: Exploring -> Battling.");
            Assert.That(coordinator.TryChangePhase(GameplayPhase.Battling), Is.False);
            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(coordinator.CurrentPhase, Is.EqualTo(GameplayPhase.Exploring));
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void PlayerMovement_ClampsTargetAndFinalPosition_WithoutChangingYZ()
        {
            GameObject gameObject = new GameObject("PlayerMovementTest");
            gameObject.transform.position = new Vector3(0f, 3f, 4f);
            PlayerMovement movement = gameObject.AddComponent<PlayerMovement>();
            movement.Configure(5f, -8f, 8f);

            movement.SetTargetX(100f);
            for (int index = 0; index < 300; index++)
            {
                movement.SimulateStep(0.02f);
            }

            Assert.That(movement.TargetX, Is.EqualTo(8f));
            Assert.That(gameObject.transform.position.x, Is.EqualTo(8f).Within(0.001f));
            Assert.That(gameObject.transform.position.y, Is.EqualTo(3f));
            Assert.That(gameObject.transform.position.z, Is.EqualTo(4f));
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void CameraController_ClampsRequestedTargetAndFinalPosition()
        {
            GameObject gameObject = new GameObject("CameraControllerTest");
            CameraController controller = gameObject.AddComponent<CameraController>();
            controller.Configure(gameObject.transform, -6f, 6f, 0.2f);

            controller.SetTargetX(100f);
            for (int index = 0; index < 600; index++)
            {
                controller.SimulateStep(0.02f);
            }

            Assert.That(controller.TargetX, Is.EqualTo(6f));
            Assert.That(gameObject.transform.position.x, Is.InRange(-6f, 6f));
            Assert.That(gameObject.transform.position.x, Is.EqualTo(6f).Within(0.01f));
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void CameraController_PlayerBeyondViewportEdge_RequestsFollowMovement()
        {
            GameObject cameraObject = new GameObject("CameraFollowTest");
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            CameraController controller = cameraObject.AddComponent<CameraController>();
            controller.Configure(cameraObject.transform, -6f, 6f, 0.2f);

            GameObject playerObject = new GameObject("FollowTarget");
            playerObject.transform.position = new Vector3(8f, 0f, 0f);
            controller.SetFollowTarget(playerObject.transform, camera);
            controller.UpdateFollowTarget();

            Assert.That(controller.TargetX, Is.GreaterThan(0f));
            Assert.That(controller.TargetX, Is.LessThanOrEqualTo(6f));
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(cameraObject);
        }
    }
}
