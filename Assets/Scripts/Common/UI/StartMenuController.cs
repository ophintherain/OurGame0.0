using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Spotlight.Shared
{
    // The five supplied images share a 2275 x 1280 canvas. Keep them aligned as layers.
    public sealed class StartMenuController : MonoBehaviour
    {
        private const float ArtWidth = 2275f;
        private const float ArtHeight = 1280f;

        [SerializeField] private Sprite background;
        [SerializeField] private Sprite startLabel;
        [SerializeField] private Sprite continueLabel;
        [SerializeField] private Sprite exitLabel;
        [SerializeField] private Sprite selectionArrow;

        private GameObject canvasObject;
        private RectTransform artworkRoot;
        private RectTransform arrowRect;
        private AudioSource audioSource;
        private AudioClip clickClip;
        private AudioClip switchClip;
        private int selectedIndex;
        private bool locked;

        public event Action StartRequested;

        public void Configure(Sprite backdrop, Sprite start, Sprite resume, Sprite exit, Sprite arrow)
        {
            background = backdrop;
            startLabel = start;
            continueLabel = resume;
            exitLabel = exit;
            selectionArrow = arrow;
        }

        private void Start()
        {
            if (background == null || startLabel == null || continueLabel == null ||
                exitLabel == null || selectionArrow == null)
            {
                Debug.LogError("[StartMenu] One or more start screen sprites are missing.", this);
                return;
            }

            BuildUi();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Update()
        {
            if (canvasObject == null || !canvasObject.activeSelf || locked) return;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                Select(selectedIndex == 0 ? 2 : 0, true);
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                Select(selectedIndex == 2 ? 0 : 2, true);
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                Activate(selectedIndex);
        }

        private void LateUpdate()
        {
            if (artworkRoot == null) return;
            float fit = Mathf.Min(Screen.width / ArtWidth, Screen.height / ArtHeight);
            artworkRoot.localScale = Vector3.one * fit;
        }

        private void BuildUi()
        {
            canvasObject = new GameObject("Start Menu Canvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            // Screen Space Overlay canvases must stay at the hierarchy root.
            DontDestroyOnLoad(canvasObject);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            audioSource = canvasObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            clickClip = Resources.Load<AudioClip>("UIAudio/Click");
            switchClip = Resources.Load<AudioClip>("UIAudio/SwitchAction");

            var blackObject = new GameObject("Letterbox", typeof(RectTransform), typeof(Image));
            blackObject.transform.SetParent(canvasObject.transform, false);
            RectTransform blackRect = blackObject.GetComponent<RectTransform>();
            blackRect.anchorMin = Vector2.zero;
            blackRect.anchorMax = Vector2.one;
            blackRect.offsetMin = blackRect.offsetMax = Vector2.zero;
            Image blackImage = blackObject.GetComponent<Image>();
            blackImage.color = Color.black;
            blackImage.raycastTarget = false;

            var artObject = new GameObject("Artwork 2275x1280", typeof(RectTransform));
            artObject.transform.SetParent(canvasObject.transform, false);
            artworkRoot = artObject.GetComponent<RectTransform>();
            artworkRoot.anchorMin = artworkRoot.anchorMax = new Vector2(0.5f, 0.5f);
            artworkRoot.pivot = new Vector2(0.5f, 0.5f);
            artworkRoot.sizeDelta = new Vector2(ArtWidth, ArtHeight);

            CreateArtworkImage("Background", background);
            CreateArtworkImage("Start", startLabel);
            Image continueImage = CreateArtworkImage("Continue (no save available)", continueLabel);
            continueImage.color = new Color(1f, 1f, 1f, 0.36f);
            CreateArtworkImage("Exit", exitLabel);
            arrowRect = CreateArtworkImage("Selection Arrow", selectionArrow).rectTransform;

            CreateHitArea("Start Game", 89f, 690f, 607f, 766f, 0);
            CreateHitArea("Exit Game", 82f, 979f, 605f, 1055f, 2);
            Select(0, false);

            EnsureEventSystem();
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            // Scene-local: gameplay scenes may provide their own EventSystem.
            new GameObject("Start Menu Event System",
                typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private Image CreateArtworkImage(string name, Sprite sprite)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(artworkRoot, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(ArtWidth, ArtHeight);
            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private void CreateHitArea(string name, float left, float top, float right, float bottom, int index)
        {
            const float padding = 16f;
            var hitObject = new GameObject(name, typeof(RectTransform), typeof(Image),
                typeof(Button), typeof(EventTrigger));
            hitObject.transform.SetParent(artworkRoot, false);
            RectTransform rect = hitObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(right - left + padding * 2f,
                bottom - top + padding * 2f);
            rect.anchoredPosition = new Vector2((left + right) * 0.5f - ArtWidth * 0.5f,
                ArtHeight * 0.5f - (top + bottom) * 0.5f);
            hitObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.001f);
            Button button = hitObject.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => Activate(index));
            var enter = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter,
                callback = new EventTrigger.TriggerEvent()
            };
            enter.callback.AddListener(_ => Select(index, true));
            hitObject.GetComponent<EventTrigger>().triggers.Add(enter);
        }

        private void Select(int index, bool playSound)
        {
            if (locked || (index != 0 && index != 2)) return;
            if (selectedIndex != index && playSound) Play(switchClip);
            selectedIndex = index;
            if (arrowRect != null)
                arrowRect.anchoredPosition = new Vector2(0f, index == 0 ? 0f : -289f);
        }

        private void Activate(int index)
        {
            if (locked || (index != 0 && index != 2)) return;
            Select(index, false);
            Play(clickClip);
            if (index == 2)
            {
                locked = true;
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                return;
            }

            locked = true;
            if (StartRequested == null)
            {
                Debug.LogError("[StartMenu] New game action is not connected.", this);
                locked = false;
                return;
            }
            StartRequested.Invoke();
        }

        public void UnlockInteraction() => locked = false;

        private void Play(AudioClip clip)
        {
            if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, 0.65f);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool isMenu = scene.name == "Bootstrap";
            if (canvasObject != null) canvasObject.SetActive(isMenu);
            if (isMenu)
            {
                EnsureEventSystem();
                locked = false;
                Select(0, false);
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (canvasObject != null) Destroy(canvasObject);
        }
    }
}
