using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Spotlight.Exploration.StoryEditor
{
    /// <summary>
    /// 生成灰盒剧情 UI：Canvas 下的 StoryOverlay、StorySystem，并自动接线。
    /// 入口：Spotlight/剧情/生成灰盒 UI
    /// </summary>
    public static class StoryGrayboxBuilder
    {
        private const string LibraryPath = "Assets/Modules/Story/StoryLibrary.asset";
        private const string PortraitSetPath = "Assets/Modules/Story/StoryPortraitSet.asset";
        private const string AudioSetPath = "Assets/Modules/Story/StoryAudioSet.asset";

        [MenuItem("Spotlight/剧情/生成灰盒 UI")]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("剧情 UI", "请在编辑模式下生成，不要进入 Play。", "好");
                return;
            }

            if (FindInScene("StoryOverlay") != null || FindInScene("StorySystem") != null)
            {
                EditorUtility.DisplayDialog(
                    "剧情 UI",
                    "场景里已经存在 StoryOverlay 或 StorySystem。\n请先删除或改名，再重新生成。",
                    "好");
                return;
            }

            Canvas canvas = FindInActiveScene<Canvas>();
            if (canvas == null)
            {
                canvas = CreateCanvas();
            }

            EnsureRaycaster(canvas);
            EnsureEventSystem();

            GameObject storyOverlay = CreateStoryOverlay(canvas.transform);
            GameObject storySystem = CreateStorySystem(canvas.transform);

            StoryOverlayView view = storySystem.GetComponent<StoryOverlayView>();
            StoryDirector director = storySystem.GetComponent<StoryDirector>();
            AudioSource audioSource = storySystem.GetComponent<AudioSource>();

            GameObject narrationPanel;
            GameObject dialoguePanel;
            GameObject topHintPanel;
            GameObject choicePanel;
            TMP_Text narrationText;
            TMP_Text speakerText;
            TMP_Text bodyText;
            TMP_Text topHintText;
            Image portraitImage;
            GameObject continueHint;
            Button choiceButtonTemplate;
            BuildPanels(storyOverlay.transform, out narrationPanel, out dialoguePanel, out topHintPanel,
                out choicePanel, out narrationText, out speakerText, out bodyText, out topHintText,
                out portraitImage, out continueHint, out choiceButtonTemplate);

            WireView(view, storyOverlay, narrationPanel, dialoguePanel, topHintPanel, choicePanel,
                narrationText, speakerText, bodyText, topHintText, portraitImage, continueHint,
                choiceButtonTemplate);

            WireDirector(director, view, audioSource);

            Undo.RegisterCreatedObjectUndo(storyOverlay, "Create Story Overlay");
            Undo.RegisterCreatedObjectUndo(storySystem, "Create Story System");

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = storySystem;
            EditorGUIUtility.PingObject(storyOverlay);

            StoryLibrary library = AssetDatabase.LoadAssetAtPath<StoryLibrary>(LibraryPath);
            if (library == null)
            {
                EditorUtility.DisplayDialog(
                    "剧情 UI 已生成",
                    "没有找到 StoryLibrary.asset。\n请先运行 Spotlight/剧情/从 Excel 导入，再把生成的 StoryLibrary 拖到 StoryDirector.library。",
                    "好");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "剧情 UI 已生成",
                    "StoryOverlay 和 StorySystem 已生成并接线。\n请保存场景，然后检查 StoryDirector 的 player / 背包 / 任务 / 交互引用。",
                    "好");
            }

            Debug.Log("[Story] 灰盒剧情 UI 已生成。StoryOverlay 在编辑模式保持显示，运行时由 StoryOverlayView 自动隐藏。");
        }

        private static Canvas CreateCanvas()
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Canvas");
            return canvas;
        }

        private static void EnsureRaycaster(Canvas canvas)
        {
            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private static void EnsureEventSystem()
        {
            if (FindInActiveScene<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create EventSystem");
        }

        private static GameObject CreateStoryOverlay(Transform parent)
        {
            GameObject overlay = new GameObject("StoryOverlay", typeof(RectTransform));
            overlay.transform.SetParent(parent, false);
            overlay.transform.SetAsLastSibling();
            Stretch(overlay.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            return overlay;
        }

        private static GameObject CreateStorySystem(Transform parent)
        {
            GameObject system = new GameObject("StorySystem", typeof(RectTransform));
            system.transform.SetParent(parent, false);

            AudioSource audioSource = system.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            system.AddComponent<StoryOverlayView>();
            system.AddComponent<StoryDirector>();
            return system;
        }

        private static void BuildPanels(Transform root,
            out GameObject narrationPanel, out GameObject dialoguePanel, out GameObject topHintPanel,
            out GameObject choicePanel, out TMP_Text narrationText, out TMP_Text speakerText,
            out TMP_Text bodyText, out TMP_Text topHintText, out Image portraitImage,
            out GameObject continueHint, out Button choiceButtonTemplate)
        {
            TMP_FontAsset font = FindChineseFont();

            // 全屏旁白
            narrationPanel = CreateImageObject(root, "NarrationPanel", Color.black, false);
            Stretch(narrationPanel.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            narrationText = CreateText(narrationPanel.transform, "NarrationText", "旁白示例……",
                40f, TextAlignmentOptions.Center, Color.white, font);
            Stretch(narrationText.rectTransform, 200f, 200f, 120f, 120f);

            // 底部对话框
            dialoguePanel = CreateImageObject(root, "DialoguePanel",
                new Color(0.06f, 0.07f, 0.09f, 0.92f), false);
            RectTransform dialogueRect = dialoguePanel.GetComponent<RectTransform>();
            dialogueRect.anchorMin = new Vector2(0.5f, 0f);
            dialogueRect.anchorMax = new Vector2(0.5f, 0f);
            dialogueRect.pivot = new Vector2(0.5f, 0f);
            dialogueRect.anchoredPosition = new Vector2(0f, 40f);
            dialogueRect.sizeDelta = new Vector2(1600f, 280f);

            portraitImage = CreateImageObject(dialoguePanel.transform, "Portrait",
                new Color(1f, 1f, 1f, 0.15f), false).GetComponent<Image>();
            RectTransform portraitRect = portraitImage.rectTransform;
            portraitRect.anchorMin = new Vector2(0f, 0f);
            portraitRect.anchorMax = new Vector2(0f, 0f);
            portraitRect.pivot = new Vector2(0.5f, 0f);
            portraitRect.anchoredPosition = new Vector2(140f, 210f);
            portraitRect.sizeDelta = new Vector2(220f, 320f);

            speakerText = CreateText(dialoguePanel.transform, "SpeakerText", "角色名",
                30f, TextAlignmentOptions.Left, new Color(1f, 0.85f, 0.4f, 1f), font);
            RectTransform speakerRect = speakerText.rectTransform;
            speakerRect.anchorMin = new Vector2(0f, 1f);
            speakerRect.anchorMax = new Vector2(0f, 1f);
            speakerRect.pivot = new Vector2(0f, 1f);
            speakerRect.anchoredPosition = new Vector2(40f, -24f);
            speakerRect.sizeDelta = new Vector2(500f, 50f);

            bodyText = CreateText(dialoguePanel.transform, "BodyText", "剧情正文示例。",
                28f, TextAlignmentOptions.TopLeft, Color.white, font);
            RectTransform bodyRect = bodyText.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, 1f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.pivot = new Vector2(0.5f, 1f);
            bodyRect.anchoredPosition = new Vector2(0f, -70f);
            bodyRect.sizeDelta = new Vector2(-80f, 170f);

            continueHint = CreateText(dialoguePanel.transform, "ContinueHint", "点击继续",
                22f, TextAlignmentOptions.BottomRight, new Color(0.75f, 0.75f, 0.75f, 1f), font).gameObject;
            RectTransform hintRect = continueHint.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(1f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(1f, 0f);
            hintRect.anchoredPosition = new Vector2(-30f, 20f);
            hintRect.sizeDelta = new Vector2(200f, 40f);

            // 顶部提示 / 广播 / 警告
            topHintPanel = CreateImageObject(root, "TopHintPanel",
                new Color(0.06f, 0.07f, 0.09f, 0.85f), false);
            RectTransform topRect = topHintPanel.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0.5f, 1f);
            topRect.anchorMax = new Vector2(0.5f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.anchoredPosition = new Vector2(0f, -40f);
            topRect.sizeDelta = new Vector2(1200f, 120f);

            topHintText = CreateText(topHintPanel.transform, "TopHintText", "引导 / 广播提示",
                32f, TextAlignmentOptions.Center, Color.white, font);
            Stretch(topHintText.rectTransform, 24f, 24f, 20f, 20f);

            // 选项
            choicePanel = new GameObject("ChoicePanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
            choicePanel.transform.SetParent(root, false);
            RectTransform choiceRect = choicePanel.GetComponent<RectTransform>();
            choiceRect.anchorMin = new Vector2(0.5f, 0.5f);
            choiceRect.anchorMax = new Vector2(0.5f, 0.5f);
            choiceRect.pivot = new Vector2(0.5f, 0.5f);
            choiceRect.anchoredPosition = Vector2.zero;
            choiceRect.sizeDelta = new Vector2(800f, 400f);

            VerticalLayoutGroup layout = choicePanel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            choiceButtonTemplate = CreateChoiceButton(choicePanel.transform, font);
            choiceButtonTemplate.gameObject.SetActive(false);

            // 编辑时先显示对话框，其余面板隐藏；运行时由 StoryOverlayView 控制
            narrationPanel.SetActive(false);
            dialoguePanel.SetActive(true);
            topHintPanel.SetActive(false);
            choicePanel.SetActive(false);
        }

        private static Button CreateChoiceButton(Transform parent, TMP_FontAsset font)
        {
            GameObject buttonObject = new GameObject("ChoiceButtonTemplate",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(700f, 64f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.15f, 0.17f, 0.22f, 0.95f);
            image.raycastTarget = true;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 700f;
            layoutElement.preferredHeight = 64f;

            TMP_Text label = CreateText(buttonObject.transform, "Label", "选项",
                26f, TextAlignmentOptions.Center, Color.white, font);
            Stretch(label.rectTransform, 12f, 12f, 6f, 6f);

            return button;
        }

        private static void WireView(StoryOverlayView view, GameObject panelRoot,
            GameObject narrationPanel, GameObject dialoguePanel, GameObject topHintPanel,
            GameObject choicePanel, TMP_Text narrationText, TMP_Text speakerText, TMP_Text bodyText,
            TMP_Text topHintText, Image portraitImage, GameObject continueHint, Button choiceButtonTemplate)
        {
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("panelRoot").objectReferenceValue = panelRoot;
            serialized.FindProperty("narrationRoot").objectReferenceValue = narrationPanel;
            serialized.FindProperty("dialogueRoot").objectReferenceValue = dialoguePanel;
            serialized.FindProperty("topHintRoot").objectReferenceValue = topHintPanel;
            serialized.FindProperty("choiceRoot").objectReferenceValue = choicePanel;
            serialized.FindProperty("narrationText").objectReferenceValue = narrationText;
            serialized.FindProperty("speakerText").objectReferenceValue = speakerText;
            serialized.FindProperty("bodyText").objectReferenceValue = bodyText;
            serialized.FindProperty("topHintText").objectReferenceValue = topHintText;
            serialized.FindProperty("portraitImage").objectReferenceValue = portraitImage;
            serialized.FindProperty("continueHint").objectReferenceValue = continueHint;
            serialized.FindProperty("choiceButtonTemplate").objectReferenceValue = choiceButtonTemplate;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
        }

        private static void WireDirector(StoryDirector director, StoryOverlayView view, AudioSource audioSource)
        {
            StoryLibrary library = AssetDatabase.LoadAssetAtPath<StoryLibrary>(LibraryPath);
            StoryPortraitSet portraitSet = AssetDatabase.LoadAssetAtPath<StoryPortraitSet>(PortraitSetPath);
            StoryAudioSet audioSet = AssetDatabase.LoadAssetAtPath<StoryAudioSet>(AudioSetPath);

            ExplorationPlayerController player = FindInActiveScene<ExplorationPlayerController>();
            BackpackPanel backpackPanel = FindInActiveScene<BackpackPanel>();
            ExplorationTaskPanel taskPanel = FindInActiveScene<ExplorationTaskPanel>();
            ExplorationInteractables interactables = FindInActiveScene<ExplorationInteractables>();

            SerializedObject serialized = new SerializedObject(director);
            serialized.FindProperty("library").objectReferenceValue = library;
            serialized.FindProperty("view").objectReferenceValue = view;
            serialized.FindProperty("player").objectReferenceValue = player;
            serialized.FindProperty("backpackPanel").objectReferenceValue = backpackPanel;
            serialized.FindProperty("taskPanel").objectReferenceValue = taskPanel;
            serialized.FindProperty("interactables").objectReferenceValue = interactables;
            serialized.FindProperty("portraitSet").objectReferenceValue = portraitSet;
            serialized.FindProperty("audioSet").objectReferenceValue = audioSet;
            serialized.FindProperty("audioSource").objectReferenceValue = audioSource;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);

            if (player == null)
            {
                Debug.LogWarning("[Story] 灰盒生成时没有找到 ExplorationPlayerController，请手动拖到 StoryDirector.player。");
            }

            if (backpackPanel == null || taskPanel == null || interactables == null)
            {
                Debug.LogWarning("[Story] 灰盒生成时有引用未找到，请检查 StoryDirector 的背包 / 任务 / 交互引用。");
            }
        }

        private static TMP_Text CreateText(Transform parent, string name, string text, float fontSize,
            TextAlignmentOptions alignment, Color color, TMP_FontAsset font)
        {
            GameObject textObject = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;

            if (font != null)
            {
                tmp.font = font;
                if (font.material != null)
                {
                    tmp.fontSharedMaterial = font.material;
                }
            }

            return tmp;
        }

        private static GameObject CreateImageObject(Transform parent, string name, Color color, bool raycastTarget)
        {
            GameObject imageObject = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return imageObject;
        }

        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static TMP_FontAsset FindChineseFont()
        {
            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font != null && font.name.ToLowerInvariant().Contains("sourcehansans"))
                {
                    return font;
                }
            }

            Debug.LogWarning("[Story] 没有找到 sourcehansans 字体，灰盒文本可能无法显示中文。");
            return TMP_Settings.defaultFontAsset;
        }

        private static T FindInActiveScene<T>() where T : Component
        {
            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                T component = roots[index].GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static GameObject FindInScene(string name)
        {
            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                Transform[] transforms = roots[index].GetComponentsInChildren<Transform>(true);
                for (int child = 0; child < transforms.Length; child++)
                {
                    if (transforms[child].name == name)
                    {
                        return transforms[child].gameObject;
                    }
                }
            }

            return null;
        }
    }
}
