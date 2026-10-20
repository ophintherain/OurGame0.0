using Spotlight.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.Exploration
{
    // The story scenes remain editable; this small bridge adds run state and inherited inventory.
    public sealed class ExplorationRunBridge : MonoBehaviour
    {
        private StoryDirector story;
        private ExplorationInventory inventory;
        private ExplorationTaskPanel tasks;
        private ExplorationPlayerController player;
        private ExplorationMap map;
        private ExplorationHintPanel hint;
        private string sceneName;
        private int carriage;
        private bool leaving;
        private bool exitReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "zero" && scene.name != "first" &&
                scene.name != "second" && scene.name != "third") return;
            new GameObject("ExplorationRunBridge").AddComponent<ExplorationRunBridge>();
        }

        private void Start()
        {
            sceneName = SceneManager.GetActiveScene().name;
            story = FindObjectOfType<StoryDirector>();
            if (sceneName == "zero")
            {
                if (story != null) story.StoryFinished += OnStoryFinished;
                return;
            }

            if (!RunProgress.Active) RunProgress.Begin();
            carriage = sceneName == "first" ? 1 : sceneName == "second" ? 2 : 3;
            RunProgress.SetCarriage(carriage);
            inventory = FindObjectOfType<ExplorationInventory>();
            tasks = FindObjectOfType<ExplorationTaskPanel>();
            player = FindObjectOfType<ExplorationPlayerController>();
            map = FindObjectOfType<ExplorationMap>();
            hint = FindObjectOfType<ExplorationHintPanel>();
            if (inventory == null || tasks == null || player == null || map == null)
            {
                Debug.LogError("[Run] Exploration scene is missing inventory, tasks, player, or map.");
                enabled = false;
                return;
            }
            foreach (int id in RunProgress.WeaponIds())
                inventory.Add(id, RunProgress.Icon(id), RunProgress.Name(id));
            if (RunProgress.ExplorationCompleted(carriage)) tasks.RestoreObserveTaskCompleted();
            if (carriage == 3 && FindObjectOfType<ExplorationBeatWeirdness>() == null)
                gameObject.AddComponent<ExplorationBeatWeirdness>();
        }

        private void Update()
        {
            if (carriage == 0 || leaving || SpotlightSceneTransition.IsTransitioning) return;
            if (!RunProgress.ExplorationCompleted(carriage)) return;

            if (!exitReady)
            {
                exitReady = true;
                if (!tasks.ObserveTaskCompleted) tasks.CompleteObserveTask();
                hint?.Show("怪异已完成。前往车厢最右端，按 E 进入战斗。");
            }

            if (story != null && story.IsPlaying) return;
            if (!player.ControlEnabled || player.IsMoving ||
                player.CurrentColumn != map.CellCount - 1 || !Input.GetKeyDown(KeyCode.E)) return;

            leaving = SpotlightSceneTransition.TryLoad("fight_" + carriage,
                onFailure: error =>
                {
                    leaving = false;
                    Debug.LogError("[Run] " + error, this);
                });
            if (!leaving) Debug.LogError("[Run] Could not load fight_" + carriage + ".", this);
        }

        private void OnDestroy()
        {
            if (story != null) story.StoryFinished -= OnStoryFinished;
        }

        private void OnStoryFinished(string id)
        {
            if (sceneName == "zero" && id == "ch1_04_explore_start" && !leaving)
            {
                leaving = SpotlightSceneTransition.TryLoad("first");
            }
        }
    }
}
