using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            StartCoroutine(LoadRoutine(sceneName, requestSource));
            return true;
        }

        private IEnumerator LoadRoutine(string sceneName, string requestSource)
        {
            LoadStarted?.Invoke(sceneName, requestSource);
            AsyncOperation operation;

            try
            {
                operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                FinishWithFailure(sceneName, requestSource, exception.Message);
                yield break;
            }

            if (operation == null)
            {
                FinishWithFailure(sceneName, requestSource, "Unity did not create an async load operation.");
                yield break;
            }

            while (!operation.isDone)
            {
                float normalizedProgress = Mathf.Clamp01(operation.progress / 0.9f);
                LoadProgressed?.Invoke(sceneName, normalizedProgress);
                yield return null;
            }

            LoadProgressed?.Invoke(sceneName, 1f);
            yield return null;
            SetInputEnabled(true);
            IsLoading = false;
            LoadCompleted?.Invoke(sceneName, requestSource);
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
