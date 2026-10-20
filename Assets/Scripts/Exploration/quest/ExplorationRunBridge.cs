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
        private string sceneName;
        private int carriage;
        private bool leaving;

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

            carriage = sceneName == "first" ? 1 : sceneName == "second" ? 2 : 3;
            RunProgress.SetCarriage(carriage);
            inventory = FindObjectOfType<ExplorationInventory>();
            tasks = FindObjectOfType<ExplorationTaskPanel>();
            if (inventory == null || tasks == null)
            {
                Debug.LogError("[Run] Exploration scene is missing inventory or tasks.");
                enabled = false;
                return;
            }
            foreach (int id in RunProgress.WeaponIds())
                inventory.Add(id, RunProgress.Icon(id), RunProgress.Name(id));
            if (RunProgress.ExplorationCompleted(carriage)) tasks.RestoreObserveTaskCompleted();
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
