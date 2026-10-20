using UnityEngine;

namespace Spotlight
{
    public enum UISoundCue
    {
        Click,
        PageTurn,
        OpenBackpack,
        CloseBackpack,
        OpenTaskBar,
        CloseTaskBar,
        SwitchAction,
        ExplorationDamage
    }

    // UI sounds are loaded by name so new UI screens can use the same cues without scene wiring.
    public static class UISoundPlayer
    {
        private static readonly string[] ClipNames =
        {
            "Click", "PageTurn", "OpenBackpack", "CloseBackpack",
            "OpenTaskBar", "CloseTaskBar", "SwitchAction", "ExplorationDamage"
        };

        private static readonly AudioClip[] Clips = new AudioClip[8];
        private static AudioSource source;

        public static void Play(UISoundCue cue)
        {
            int index = (int)cue;
            if (index < 0 || index >= Clips.Length) return;

            AudioClip clip = Clips[index];
            if (clip == null)
            {
                clip = Resources.Load<AudioClip>("UIAudio/" + ClipNames[index]);
                Clips[index] = clip;
            }
            if (clip == null) return;

            if (source == null)
            {
                GameObject audioObject = new GameObject("UISoundPlayer");
                Object.DontDestroyOnLoad(audioObject);
                source = audioObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.volume = 0.65f;
            }

            source.PlayOneShot(clip);
        }
    }
}
