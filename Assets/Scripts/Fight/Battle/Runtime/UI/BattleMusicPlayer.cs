using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.Battle
{
    // Keeps exploration silent while playing menu, loadout, boss, and ending music.
    public sealed class BattleMusicPlayer : MonoBehaviour
    {
        private const float MusicVolume = 0.45f;
        private const float FadeSeconds = 0.6f;
        private static BattleMusicPlayer instance;
        private readonly AudioSource[] sources = new AudioSource[2];
        private BattleCoordinator battle;
        private BossController boss;
        private AudioSource current;
        private Coroutine fade;
        private string currentName;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var holder = new GameObject("BattleMusicPlayer");
            DontDestroyOnLoad(holder);
            instance = holder.AddComponent<BattleMusicPlayer>();
        }

        private void Awake()
        {
            for (int index = 0; index < sources.Length; index++)
            {
                sources[index] = gameObject.AddComponent<AudioSource>();
                sources[index].playOnAwake = false;
                sources[index].loop = true;
                sources[index].spatialBlend = 0f;
                sources[index].volume = 0f;
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeBattle();
            if (instance == this) instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UnsubscribeBattle();
            if (scene.name == "Bootstrap" || scene.name == "ending")
            {
                Play("carriage_1_loop");
                return;
            }
            if (scene.name == "zero" || scene.name == "first" || scene.name == "second" ||
                scene.name == "third")
            {
                StopMusic();
                return;
            }
            if (scene.name != "Gameplay" && !scene.path.StartsWith("Assets/Scenes/fight/"))
            {
                StopMusic();
                return;
            }

            battle = FindObjectOfType<BattleCoordinator>(true);
            boss = FindObjectOfType<BossController>(true);
            if (battle != null)
            {
                battle.BattleStarted += OnBattleStarted;
                battle.BattleFinished += OnBattleFinished;
            }
            PlayCarriage();
            if (battle != null && battle.IsBattleActive) OnBattleStarted();
        }

        private void OnBattleStarted()
        {
            // The supplied pack has two boss tracks; the third carriage uses track two.
            Play(boss != null && boss.CarriageIndex == 1 ? "battle_boss_1" : "battle_boss_2");
        }

        private void OnBattleFinished(BattleResult result) => PlayCarriage();

        private void PlayCarriage()
        {
            int carriage = boss != null ? Mathf.Clamp(boss.CarriageIndex, 1, 3) : 1;
            Play("carriage_" + carriage + "_loop");
        }

        private void Play(string name)
        {
            if (currentName == name && current != null && current.isPlaying) return;
            AudioClip clip = Resources.Load<AudioClip>("Music/" + name);
            if (clip == null) return;
            AudioSource next = current == sources[0] ? sources[1] : sources[0];
            if (fade != null) StopCoroutine(fade);
            next.Stop();
            next.clip = clip;
            next.volume = 0f;
            next.Play();
            AudioSource previous = current;
            current = next;
            currentName = name;
            fade = StartCoroutine(Fade(previous, next));
        }

        private IEnumerator Fade(AudioSource previous, AudioSource next)
        {
            float startVolume = previous != null ? previous.volume : 0f;
            float elapsed = 0f;
            while (elapsed < FadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float fraction = Mathf.Clamp01(elapsed / FadeSeconds);
                if (previous != null) previous.volume = Mathf.Lerp(startVolume, 0f, fraction);
                next.volume = MusicVolume * fraction;
                yield return null;
            }
            if (previous != null) previous.Stop();
            next.volume = MusicVolume;
            fade = null;
        }

        private void StopMusic()
        {
            if (fade != null) StopCoroutine(fade);
            fade = null;
            foreach (AudioSource source in sources) source?.Stop();
            current = null;
            currentName = null;
        }

        private void UnsubscribeBattle()
        {
            if (battle != null)
            {
                battle.BattleStarted -= OnBattleStarted;
                battle.BattleFinished -= OnBattleFinished;
            }
            battle = null;
            boss = null;
        }
    }
}
