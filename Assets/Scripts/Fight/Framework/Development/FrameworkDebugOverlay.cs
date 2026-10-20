using UnityEngine;

namespace Spotlight
{
    public sealed class FrameworkDebugOverlay : MonoBehaviour
    {
        [SerializeField] private GameplayCoordinator coordinator;
        [SerializeField] private InputRouter inputRouter;
        [SerializeField] private PlayerMovement player;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private InteractionProbe interactionProbe;

        public void Configure(GameplayCoordinator phaseCoordinator, InputRouter router,
            PlayerMovement playerMovement, CameraController controlledCamera, InteractionProbe probe)
        {
            coordinator = phaseCoordinator;
            inputRouter = router;
            player = playerMovement;
            cameraController = controlledCamera;
            interactionProbe = probe;
        }

        private void OnGUI()
        {
            if (Spotlight.Shared.SpotlightSceneTransition.IsTransitioning) return;
            GUILayout.BeginArea(new Rect(16, 16, 430, 110), GUI.skin.box);
            GUILayout.Label("SPOTLIGHT / FIRST-STAGE GREYBOX");
            GUILayout.Label($"Phase: {(coordinator != null ? coordinator.CurrentPhase.ToString() : "Missing")}");
            GUILayout.Label($"Input: {(inputRouter != null && inputRouter.InputEnabled ? "Enabled" : "Disabled")}");
            GUILayout.Label($"Last route: {(inputRouter != null ? inputRouter.LastRoute.ToString() : "Missing")}");
            GUILayout.EndArea();
        }
    }
}
