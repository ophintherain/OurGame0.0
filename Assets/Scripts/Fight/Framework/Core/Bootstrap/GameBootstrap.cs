using System.Collections;
using Spotlight.Shared;
using UnityEngine;

namespace Spotlight
{
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        [SerializeField] private bool autoStart = true;
        [SerializeField] private RunSession runSession;
        [SerializeField] private SceneLoader sceneLoader;
        [SerializeField] private GameFlowController gameFlowController;
        private StartMenuController startMenu;

        public void Configure(
            RunSession session,
            SceneLoader loader,
            GameFlowController flowController,
            bool shouldAutoStart)
        {
            runSession = session;
            sceneLoader = loader;
            gameFlowController = flowController;
            autoStart = shouldAutoStart;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            gameObject.name = "PersistentRoot";
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;

            runSession ??= GetComponent<RunSession>();
            sceneLoader ??= GetComponent<SceneLoader>();
            gameFlowController ??= GetComponent<GameFlowController>();
            gameFlowController?.Initialize(runSession, sceneLoader);
            startMenu = GetComponent<StartMenuController>();
            if (startMenu != null && gameFlowController != null)
            {
                startMenu.StartRequested += gameFlowController.StartNewRun;
                if (sceneLoader != null) sceneLoader.LoadFailed += HandleMenuLoadFailed;
            }
        }

        private IEnumerator Start()
        {
            if (!autoStart || Instance != this)
            {
                yield break;
            }

            yield return null;
            gameFlowController.StartNewRun();
        }

        private void OnDestroy()
        {
            if (startMenu != null && gameFlowController != null)
                startMenu.StartRequested -= gameFlowController.StartNewRun;
            if (sceneLoader != null) sceneLoader.LoadFailed -= HandleMenuLoadFailed;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HandleMenuLoadFailed(string sceneName, string requestSource, string reason)
        {
            startMenu?.UnlockInteraction();
        }
    }
}
