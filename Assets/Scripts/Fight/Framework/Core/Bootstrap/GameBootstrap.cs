using System.Collections;
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
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
