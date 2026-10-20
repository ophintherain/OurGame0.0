using Spotlight.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.Exploration
{
    // The story scenes remain editable; this small bridge adds run exits and observation points.
    public sealed class ExplorationRunBridge : MonoBehaviour
    {
        private StoryDirector story;
        private ExplorationInventory inventory;
        private ExplorationTaskPanel tasks;
        private ExplorationPlayerController player;
        private ExplorationMap map;
        private TextMesh marker;
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
            player = FindObjectOfType<ExplorationPlayerController>();
            map = FindObjectOfType<ExplorationMap>();
            if (inventory == null || tasks == null || player == null || map == null)
            {
                Debug.LogError("[Run] Exploration scene is missing inventory, tasks, player or map.");
                enabled = false;
                return;
            }
            foreach (int id in RunProgress.WeaponIds())
                inventory.Add(id, RunProgress.Icon(id), RunProgress.Name(id));
            if (RunProgress.ExplorationCompleted(carriage)) tasks.CompleteObserveTask();

            var markerObject = new GameObject("RunInteractionMarker");
            marker = markerObject.AddComponent<TextMesh>();
            marker.anchor = TextAnchor.MiddleCenter;
            marker.alignment = TextAlignment.Center;
            marker.characterSize = 0.13f;
            marker.fontSize = 36;
            marker.color = new Color(1f, 0.85f, 0.45f);
            marker.GetComponent<MeshRenderer>().sortingOrder = 40;
        }

        private void OnDestroy()
        {
            if (story != null) story.StoryFinished -= OnStoryFinished;
            if (marker != null) Destroy(marker.gameObject);
        }

        private void OnStoryFinished(string id)
        {
            if (sceneName == "zero" && id == "ch1_04_explore_start" && !leaving)
            {
                leaving = SpotlightSceneTransition.TryLoad("first");
            }
        }

        private void Update()
        {
            if (marker == null || leaving) return;
            bool canObserve = tasks.WeaponTaskCompleted && !tasks.ObserveTaskCompleted;
            bool canExit = tasks.ObserveTaskCompleted;
            int observeColumn = Mathf.Max(0, map.CellCount - 3);
            int exitColumn = map.CellCount - 1;
            marker.gameObject.SetActive(canObserve || canExit);
            marker.text = canObserve ? "观测怪异 [E]" : "进入战斗 [E]";
            marker.transform.position = map.GetWorldPosition(canObserve ? observeColumn : exitColumn) +
                Vector3.up * 1.2f;
            if (!Input.GetKeyDown(KeyCode.E) || !player.ControlEnabled || player.IsMoving ||
                (story != null && story.IsPlaying)) return;
            if (canObserve && Mathf.Abs(player.CurrentColumn - observeColumn) <= 1)
            {
                tasks.CompleteObserveTask();
                RunProgress.CompleteExploration(carriage);
            }
            else if (canExit && player.CurrentColumn == exitColumn)
            {
                leaving = SpotlightSceneTransition.TryLoad("fight_" + carriage);
            }
        }
    }
}
