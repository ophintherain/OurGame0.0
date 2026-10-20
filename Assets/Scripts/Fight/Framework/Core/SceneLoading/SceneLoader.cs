using System;
using UnityEngine;
using Spotlight.Shared;

namespace Spotlight
{
    public sealed class SceneLoader : MonoBehaviour
    {
        public event Action<string, string> LoadStarted;
        public event Action<string, float> LoadProgressed;
        public event Action<string, string> LoadCompleted;
        public event Action<string, string, string> LoadFailed;

        public bool IsLoading { get; private set; }

        public bool RequestLoad(string sceneName, string requestSource)
        {
            if (IsLoading)
            {
                Debug.LogWarning($"[SceneLoader] Rejected '{sceneName}' from '{requestSource}': a scene is already loading.", this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                string reason = "The scene is empty or is not enabled in Build Settings.";
                Debug.LogError($"[SceneLoader] Failed to load '{sceneName}' requested by '{requestSource}': {reason}", this);
                LoadFailed?.Invoke(sceneName, requestSource, reason);
                return false;
            }

            IsLoading = true;
            SetInputEnabled(false);
            if (!SpotlightSceneTransition.TryLoad(sceneName,
                progress => LoadProgressed?.Invoke(sceneName, progress),
                () =>
                {
                    SetInputEnabled(true);
                    IsLoading = false;
                    LoadCompleted?.Invoke(sceneName, requestSource);
                },
                reason => FinishWithFailure(sceneName, requestSource, reason)))
            {
                FinishWithFailure(sceneName, requestSource, "A transition is already running.");
                return false;
            }
            LoadStarted?.Invoke(sceneName, requestSource);
            return true;
        }

        private void FinishWithFailure(string sceneName, string requestSource, string reason)
        {
            SetInputEnabled(true);
            IsLoading = false;
            Debug.LogError($"[SceneLoader] Failed to load '{sceneName}' requested by '{requestSource}': {reason}", this);
            LoadFailed?.Invoke(sceneName, requestSource, reason);
        }

        private static void SetInputEnabled(bool enabled)
        {
            InputRouter[] routers = FindObjectsOfType<InputRouter>(true);
            foreach (InputRouter router in routers)
            {
                router.SetInputEnabled(enabled);
            }
        }
    }
}
