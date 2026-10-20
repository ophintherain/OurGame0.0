using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Spotlight.Exploration.StoryEditor
{
    /// <summary>
    /// StoryAudioSet 的 Inspector：按音效条目分组显示，并给每条加试听 / 停止按钮。
    /// 试听会把该条目的所有音频层按各自音量同时播放，环境音循环播放。
    /// </summary>
    [CustomEditor(typeof(StoryAudioSet))]
    public sealed class StoryAudioSetEditor : UnityEditor.Editor
    {
        private static GameObject previewRoot;
        private static readonly List<AudioSource> previewSources = new List<AudioSource>();

        [InitializeOnLoadMethod]
        private static void RegisterPreviewCleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
            EditorApplication.quitting -= StopPreview;
            EditorApplication.quitting += StopPreview;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty stopKey = serializedObject.FindProperty("stopAmbientKey");
            if (stopKey != null)
            {
                EditorGUILayout.PropertyField(stopKey, new GUIContent("停止环境音的指令"));
                EditorGUILayout.Space();
            }

            SerializedProperty entries = serializedObject.FindProperty("entries");
            EditorGUILayout.LabelField("音效列表", EditorStyles.boldLabel);

            for (int index = 0; index < entries.arraySize; index++)
            {
                if (DrawEntry(entries.GetArrayElementAtIndex(index), index))
                {
                    StopPreview();
                    RemoveEntry(entries, index);
                    break;
                }
            }

            if (GUILayout.Button("添加音效"))
            {
                entries.InsertArrayElementAtIndex(entries.arraySize);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private bool DrawEntry(SerializedProperty entry, int index)
        {
            SerializedProperty key = entry.FindPropertyRelative("key");
            bool removeRequested = false;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string title = string.IsNullOrWhiteSpace(key.stringValue)
                        ? $"（未命名 {index}）"
                        : key.stringValue;
                    EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

                    if (GUILayout.Button("试听", GUILayout.Width(52f)))
                    {
                        Preview(entry);
                    }

                    if (GUILayout.Button("停止", GUILayout.Width(52f)))
                    {
                        StopPreview();
                    }

                    if (GUILayout.Button("删除", GUILayout.Width(44f)))
                    {
                        removeRequested = true;
                    }
                }

                EditorGUILayout.PropertyField(key, new GUIContent("音效 Key"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("clips"), new GUIContent("音频层"), true);
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("volume"), new GUIContent("整体音量"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("isAmbient"), new GUIContent("长音环境音（循环）"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("fadeIn"), new GUIContent("淡入秒数"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("fadeOut"), new GUIContent("淡出秒数"));
            }

            return removeRequested;
        }

        private static void RemoveEntry(SerializedProperty entries, int index)
        {
            int oldSize = entries.arraySize;
            entries.DeleteArrayElementAtIndex(index);

            // 引用类型元素第一次删除只置空，需要再删一次才真正移除
            if (entries.arraySize == oldSize)
            {
                entries.DeleteArrayElementAtIndex(index);
            }
        }

        private static void Preview(SerializedProperty entry)
        {
            StopPreview();

            SerializedProperty clips = entry.FindPropertyRelative("clips");
            if (clips == null || clips.arraySize == 0)
            {
                return;
            }

            float entryVolume = Mathf.Clamp01(entry.FindPropertyRelative("volume").floatValue);
            bool loop = entry.FindPropertyRelative("isAmbient").boolValue;

            GameObject root = new GameObject("[StoryAudioPreview]");
            root.hideFlags = HideFlags.HideAndDontSave;

            for (int index = 0; index < clips.arraySize; index++)
            {
                SerializedProperty layer = clips.GetArrayElementAtIndex(index);
                AudioClip clip = layer.FindPropertyRelative("clip").objectReferenceValue as AudioClip;
                if (clip == null)
                {
                    continue;
                }

                float layerVolume = Mathf.Clamp01(layer.FindPropertyRelative("volume").floatValue);

                AudioSource source = root.AddComponent<AudioSource>();
                source.clip = clip;
                source.playOnAwake = false;
                source.loop = loop;
                source.spatialBlend = 0f;
                source.volume = Mathf.Clamp01(entryVolume * layerVolume);
                source.Play();
                previewSources.Add(source);
            }

            if (previewSources.Count == 0)
            {
                Object.DestroyImmediate(root);
                return;
            }

            previewRoot = root;
        }

        private static void StopPreview()
        {
            if (previewRoot != null)
            {
                Object.DestroyImmediate(previewRoot);
            }

            previewRoot = null;
            previewSources.Clear();
        }
    }
}
