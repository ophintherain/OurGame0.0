using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Spotlight.Shared
{
    // Shared by exploration and combat; survives the scene being replaced.
    public sealed class SpotlightSceneTransition : MonoBehaviour
    {
        private const float Duration = 0.45f;
        private static SpotlightSceneTransition instance;
        private RawImage curtain;
        private Material material;
        private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
        public static bool IsTransitioning => instance != null && instance.running;
        private bool running;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize() => EnsureInstance();

        public static bool TryLoad(string sceneName, Action<float> onProgress = null,
            Action onComplete = null, Action<string> onFailure = null)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                onFailure?.Invoke("Scene is missing from Build Settings: " + sceneName);
                return false;
            }
            SpotlightSceneTransition host = EnsureInstance();
            if (host == null || host.running) return false;
            host.StartCoroutine(host.LoadRoutine(sceneName, onProgress, onComplete, onFailure));
            return true;
        }

        private static SpotlightSceneTransition EnsureInstance()
        {
            if (instance != null) return instance;
            var host = new GameObject("Shared Scene Transition");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<SpotlightSceneTransition>();
            return instance;
        }

        private bool EnsureCurtain()
        {
            if (curtain != null) return true;
            Shader shader = Shader.Find("Spotlight/SceneTransition");
            if (shader == null)
            {
                Debug.LogError("[Transition] Spotlight/SceneTransition shader is missing.");
                return false;
            }
            material = new Material(shader) { hideFlags = HideFlags.DontSave };
            var canvasObject = new GameObject("Spotlight Curtain", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = short.MaxValue;
            SortingLayer[] layers = SortingLayer.layers;
            canvas.sortingLayerID = layers[layers.Length - 1].id;
            var imageObject = new GameObject("Iris", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            curtain = imageObject.GetComponent<RawImage>();
            curtain.material = material;
            curtain.raycastTarget = true;
            canvasObject.SetActive(false);
            return true;
        }

        private IEnumerator LoadRoutine(string sceneName, Action<float> onProgress,
            Action onComplete, Action<string> onFailure)
        {
            running = true;
            if (!EnsureCurtain())
            {
                running = false;
                onFailure?.Invoke("Transition shader is missing.");
                yield break;
            }
            curtain.transform.parent.gameObject.SetActive(true);
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HideSceneCanvases();
            material.SetVector("_Center", FindPlayerScreenPosition());
            yield return Animate(0f, 1f);
            AsyncOperation operation = null;
            string failure = null;
            try { operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single); }
            catch (Exception exception) { failure = exception.Message; }
            if (operation == null)
            {
                yield return Animate(1f, 0f);
                curtain.transform.parent.gameObject.SetActive(false);
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                RestoreSceneCanvases();
                running = false;
                onFailure?.Invoke(failure ?? "Unity did not create an async load operation.");
                yield break;
            }
            while (!operation.isDone)
            {
                onProgress?.Invoke(Mathf.Clamp01(operation.progress / 0.9f));
                yield return null;
            }
            onProgress?.Invoke(1f);
            HideSceneCanvases(); // The newly loaded scene has a fresh set of UI canvases.
            yield return null;
            material.SetVector("_Center", FindPlayerScreenPosition());
            yield return Animate(1f, 0f);
            curtain.transform.parent.gameObject.SetActive(false);
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            RestoreSceneCanvases();
            running = false;
            onComplete?.Invoke();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (running) HideSceneCanvases();
        }

        private void HideSceneCanvases()
        {
            Canvas curtainCanvas = curtain.GetComponentInParent<Canvas>();
            foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas == null || canvas == curtainCanvas || !canvas.gameObject.scene.IsValid() ||
                    !canvas.enabled) continue;
                hiddenCanvases.Add(canvas);
                canvas.enabled = false;
            }
        }

        private void RestoreSceneCanvases()
        {
            foreach (Canvas canvas in hiddenCanvases)
                if (canvas != null) canvas.enabled = true;
            hiddenCanvases.Clear();
        }

        private IEnumerator Animate(float from, float to)
        {
            float elapsed = 0f;
            while (elapsed < Duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Duration));
                material.SetFloat("_Progress", Mathf.Lerp(from, to, t));
                yield return null;
            }
            material.SetFloat("_Progress", to);
        }

        private static Vector4 FindPlayerScreenPosition()
        {
            GameObject player = GameObject.Find("Player");
            Camera camera = Camera.main;
            if (player == null || camera == null) return new Vector4(0.5f, 0.5f, 0f, 0f);
            Vector3 point = camera.WorldToViewportPoint(player.transform.position);
            if (point.z <= 0f) return new Vector4(0.5f, 0.5f, 0f, 0f);
            return new Vector4(Mathf.Clamp01(point.x), Mathf.Clamp01(point.y), 0f, 0f);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            RestoreSceneCanvases();
            if (instance == this) instance = null;
            if (material != null) Destroy(material);
        }
    }
}
