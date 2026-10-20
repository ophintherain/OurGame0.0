using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Spotlight.Battle
{
    // Keeps the exploration story system unchanged while presenting its UI inside fight scenes.
    public sealed class BattleStoryBridge : MonoBehaviour
    {
        private BattleCoordinator battle;
        private MonoBehaviour story;
        private MethodInfo play;
        private PropertyInfo isPlaying;
        private int carriage;

        public bool Initialize(BattleCoordinator coordinator, int carriageIndex)
        {
            battle = coordinator;
            carriage = carriageIndex;
            foreach (MonoBehaviour component in FindObjectsOfType<MonoBehaviour>())
            {
                if (component.GetType().FullName != "Spotlight.Exploration.StoryDirector")
                    continue;
                story = component;
                break;
            }
            if (story != null)
            {
                play = story.GetType().GetMethod("Play", new[] { typeof(string) });
                isPlaying = story.GetType().GetProperty("IsPlaying");
            }
            if (story == null || !story.enabled || play == null || isPlaying == null)
            {
                Debug.LogError("[BattleStory] Fight scene has no working StoryDirector.", this);
                return false;
            }

            foreach (MonoBehaviour component in FindObjectsOfType<MonoBehaviour>())
            {
                if (component.GetType().FullName == "Spotlight.Exploration.ExplorationPlayerController")
                    component.enabled = false;
            }

            StartCoroutine(PlayBeforeBattle());
            return true;
        }

        private IEnumerator PlayBeforeBattle()
        {
            yield return null; // The loadout UI and StoryOverlay finish initialization first.
            // Only the second monster has entrance dialogue in the story sheet.
            // The other chapters' exploration narration and broadcasts are not monster lines.
            if (carriage == 2) yield return PlayAndWait("boss2_pre");
            if (battle != null) battle.OpenInitialLoadoutAfterStory();
        }

        public IEnumerator PlayAfterVictory()
        {
            string id = carriage == 1 ? "boss1_post" :
                carriage == 2 ? "boss2_post" : "boss3_post";
            yield return PlayAndWait(id);
        }

        private IEnumerator PlayAndWait(string id)
        {
            if (story == null || !(bool)play.Invoke(story, new object[] { id }))
            {
                Debug.LogError($"[BattleStory] Cannot play story {id}.", this);
                yield break;
            }
            while (story != null && (bool)isPlaying.GetValue(story)) yield return null;
        }
    }
}
