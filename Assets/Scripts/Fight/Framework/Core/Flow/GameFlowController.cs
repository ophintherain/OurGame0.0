using UnityEngine;
using Spotlight.Shared;

namespace Spotlight
{
    public sealed class GameFlowController : MonoBehaviour
    {
        [SerializeField] private RunSession runSession;
        [SerializeField] private SceneLoader sceneLoader;

        public void Initialize(RunSession session, SceneLoader loader)
        {
            runSession = session;
            sceneLoader = loader;
        }

        public void StartNewRun()
        {
            if (runSession == null || sceneLoader == null)
            {
                Debug.LogError("[GameFlow] Cannot start a run because the core services are not initialized.", this);
                return;
            }

            if (sceneLoader.RequestLoad("zero", nameof(GameFlowController)))
            {
                runSession.BeginNewRun();
                RunProgress.Begin();
            }
        }

        public bool EnterFirstExploration()
        {
            if (sceneLoader == null)
            {
                Debug.LogError("[GameFlow] Cannot enter exploration because SceneLoader is missing.", this);
                return false;
            }

            return sceneLoader.RequestLoad("first", nameof(GameFlowController));
        }

        public void EnterGameplay()
        {
            if (sceneLoader == null)
            {
                Debug.LogError("[GameFlow] Cannot enter Gameplay because SceneLoader is missing.", this);
                return;
            }

            sceneLoader.RequestLoad("Gameplay", nameof(GameFlowController));
        }
    }
}
