using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Spotlight.Exploration.StoryEditor
{
    /// <summary>
    /// 把当前场景根 Canvas 的 CanvasScaler 修正为工程约定值。
    /// 入口：Spotlight/剧情/修正 CanvasScaler
    /// </summary>
    public static class StorySceneFixer
    {
        [MenuItem("Spotlight/剧情/修正 CanvasScaler")]
        private static void FixCanvasScaler()
        {
            Canvas canvas = FindOverlayCanvas();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("CanvasScaler", "当前场景没有找到 Canvas。", "好");
                return;
            }

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);
            }
            else
            {
                Undo.RecordObject(scaler, "Fix CanvasScaler");
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            EditorUtility.SetDirty(scaler);
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = canvas.gameObject;

            EditorUtility.DisplayDialog(
                "CanvasScaler",
                "已改为 Scale With Screen Size / 1920×1080 / Match 0.5。\n请检查现有 HUD、背包、任务 UI，并保存场景。",
                "好");
            Debug.Log("[Story] CanvasScaler 已修正为 1920×1080 / Match 0.5。");
        }

        private static Canvas FindOverlayCanvas()
        {
            Canvas fallback = null;
            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                Canvas[] canvases = roots[index].GetComponentsInChildren<Canvas>(true);
                for (int canvasIndex = 0; canvasIndex < canvases.Length; canvasIndex++)
                {
                    if (canvases[canvasIndex].renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        return canvases[canvasIndex];
                    }

                    if (fallback == null)
                    {
                        fallback = canvases[canvasIndex];
                    }
                }
            }

            return fallback;
        }
    }
}
