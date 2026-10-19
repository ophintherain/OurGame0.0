using UnityEditor;
using UnityEngine;

namespace Spotlight.Exploration.EditorTools
{
    /// <summary>
    /// 给 ExplorationInteractables 的每个可探索点画 Scene 拖拽手柄。
    /// 选中 Interaction 物体后，直接在 Scene 视图里拖动黄色手柄即可自由摆放，位置写回 points[i].position。
    /// </summary>
    [CustomEditor(typeof(ExplorationInteractables))]
    public sealed class ExplorationInteractablesEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            SerializedProperty points = serializedObject.FindProperty("points");
            if (points == null || !points.isArray)
            {
                return;
            }

            ExplorationInteractables component = (ExplorationInteractables)target;

            for (int index = 0; index < points.arraySize; index++)
            {
                SerializedProperty element = points.GetArrayElementAtIndex(index);
                SerializedProperty position = element.FindPropertyRelative("position");
                if (position == null)
                {
                    continue;
                }

                Vector3 world = new Vector3(position.vector2Value.x, position.vector2Value.y, 0f);
                Vector2 size = Vector2.zero;

                // 无论有没有立绘都画出交互区域；没立绘只是运行时透明，编辑器里仍要能看到范围
                if (index < component.Points.Count && component.Points[index] != null)
                {
                    component.GetDisplayRect(component.Points[index], out Vector3 center, out Vector3 rectSize);
                    size = rectSize;
                    DrawRect(center, rectSize);
                }

                Handles.color = new Color(1f, 0.85f, 0.2f, 1f);
                Handles.Label(world + Vector3.up * 0.25f, $"交互点 {index}（{size.x:0.##} × {size.y:0.##}）");

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(world, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(target, "移动交互点");
                    position.vector2Value = new Vector2(moved.x, moved.y);
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }

        private static void DrawRect(Vector3 center, Vector3 size)
        {
            Vector3 half = new Vector3(size.x * 0.5f, size.y * 0.5f, 0f);
            Vector3[] corners =
            {
                center + new Vector3(-half.x, -half.y, 0f),
                center + new Vector3(-half.x, half.y, 0f),
                center + new Vector3(half.x, half.y, 0f),
                center + new Vector3(half.x, -half.y, 0f)
            };

            Color fill = new Color(0.2f, 0.8f, 1f, 0.12f);
            Color outline = new Color(1f, 0.85f, 0.2f, 1f);
            Handles.DrawSolidRectangleWithOutline(corners, fill, outline);
        }
    }
}
