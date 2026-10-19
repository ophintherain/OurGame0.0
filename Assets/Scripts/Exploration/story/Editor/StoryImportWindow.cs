using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Spotlight.Exploration.StoryEditor
{
    /// <summary>
    /// 剧情表导入窗口：读取带「剧情ID」列的 xlsx，按 ID 生成段落资产并更新 StoryLibrary。
    /// 入口：Spotlight/剧情/从 Excel 导入
    /// </summary>
    public sealed class StoryImportWindow : EditorWindow
    {
        private const string DefaultOutputFolder = "Assets/Modules/Story/Definitions";
        private const string DefaultLibraryPath = "Assets/Modules/Story/StoryLibrary.asset";
        private const string DefaultSourceFile = "聚光灯文本剧情-带剧情ID.xlsx";

        [SerializeField] private string sourcePath = string.Empty;
        [SerializeField] private string outputFolder = DefaultOutputFolder;
        [SerializeField] private string libraryPath = DefaultLibraryPath;
        [SerializeField] private Vector2 scroll;

        private readonly List<string> report = new List<string>();
        private readonly List<string> warnings = new List<string>();

        private sealed class Segment
        {
            public string Id;
            public readonly List<StoryLine> Lines = new List<StoryLine>();
            public int LastRow;
            public bool AllNonLocking = true;
        }

        [MenuItem("Spotlight/剧情/从 Excel 导入")]
        private static void Open()
        {
            StoryImportWindow window = GetWindow<StoryImportWindow>("剧情导入");
            window.minSize = new Vector2(560f, 460f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("剧情导入");

            if (string.IsNullOrEmpty(sourcePath))
            {
                string candidate = ResolveProjectPath(DefaultSourceFile);
                if (File.Exists(candidate))
                {
                    sourcePath = candidate;
                }
            }

            if (string.IsNullOrEmpty(outputFolder))
            {
                outputFolder = DefaultOutputFolder;
            }

            if (string.IsNullOrEmpty(libraryPath))
            {
                libraryPath = DefaultLibraryPath;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("剧情表", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            sourcePath = EditorGUILayout.TextField("xlsx 路径", sourcePath);
            if (GUILayout.Button("选择...", GUILayout.Width(70f)))
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string selected = EditorUtility.OpenFilePanel("选择剧情表", projectRoot, "xlsx");
                if (!string.IsNullOrEmpty(selected))
                {
                    sourcePath = selected;
                }
            }
            EditorGUILayout.EndHorizontal();

            outputFolder = EditorGUILayout.TextField("资产输出目录", outputFolder);
            libraryPath = EditorGUILayout.TextField("剧情库资产", libraryPath);

            EditorGUILayout.HelpBox(
                "表头行必须包含：剧情ID、对象、内容；可选列：演出备注、音效、场景、需求备注。\n" +
                "剧情ID 为空的行不会导入；同一 ID 的行会组成一个可独立播放的段落。",
                MessageType.Info);

            EditorGUILayout.Space(6f);

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(sourcePath)))
            {
                if (GUILayout.Button("导入", GUILayout.Height(30f)))
                {
                    Import();
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("导入报告", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(260f));
            for (int index = 0; index < report.Count; index++)
            {
                EditorGUILayout.LabelField(report[index], EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        private void Import()
        {
            report.Clear();
            warnings.Clear();

            string fullPath = ResolvePath(sourcePath);
            if (!File.Exists(fullPath))
            {
                AddError($"找不到文件：{fullPath}");
                return;
            }

            XlsxTable table;
            try
            {
                table = XlsxTableReader.ReadFirstWorksheet(fullPath);
            }
            catch (Exception exception)
            {
                AddError($"读取 xlsx 失败：{exception.Message}");
                Debug.LogException(exception);
                return;
            }

            XlsxRow header = FindHeaderRow(table);
            if (header == null)
            {
                AddError("没有找到表头行：需要同时包含「剧情ID」「对象」「内容」三列。");
                return;
            }

            Dictionary<string, int> columns = BuildColumnMap(header);
            int storyIdColumn = FindColumn(columns, "剧情ID");
            int typeColumn = FindColumn(columns, "对象");
            int textColumn = FindColumn(columns, "内容");
            int stageColumn = FindColumn(columns, "演出备注");
            int audioColumn = FindColumn(columns, "音效");
            int sceneColumn = FindColumn(columns, "场景");
            int noteColumn = FindColumn(columns, "需求备注");

            if (storyIdColumn < 0 || typeColumn < 0 || textColumn < 0)
            {
                AddError("缺少必需列：剧情ID / 对象 / 内容。");
                return;
            }

            List<Segment> segments = new List<Segment>();
            Dictionary<string, Segment> segmentMap = new Dictionary<string, Segment>();
            int importedLines = 0;
            int skippedNoId = 0;
            int skippedTransition = 0;
            int skippedItem = 0;
            int skippedEmpty = 0;
            string lastImportedStoryId = null;

            for (int index = 0; index < table.Rows.Count; index++)
            {
                XlsxRow row = table.Rows[index];
                if (row.RowNumber <= header.RowNumber)
                {
                    continue;
                }

                string storyId = Get(row, storyIdColumn);
                if (string.IsNullOrWhiteSpace(storyId))
                {
                    skippedNoId++;
                    continue;
                }

                string type = Get(row, typeColumn);
                string text = Get(row, textColumn);
                string stage = Get(row, stageColumn);
                string audio = Get(row, audioColumn);
                string scene = Get(row, sceneColumn);
                string note = Get(row, noteColumn);

                if (type == "转场" || text == "转场")
                {
                    skippedTransition++;
                    continue;
                }

                if (stage.Contains("道具说明"))
                {
                    skippedItem++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    skippedEmpty++;
                    continue;
                }

                StoryLineKind kind = ResolveKind(type, stage, scene);

                if (lastImportedStoryId != null &&
                    lastImportedStoryId != storyId &&
                    segmentMap.ContainsKey(storyId))
                {
                    warnings.Add($"剧情ID {storyId} 出现在多个不相邻的块，已按首次出现顺序合并。");
                }

                lastImportedStoryId = storyId;

                if (!segmentMap.TryGetValue(storyId, out Segment segment))
                {
                    segment = new Segment { Id = storyId };
                    segmentMap.Add(storyId, segment);
                    segments.Add(segment);
                }

                if (kind == StoryLineKind.Choice && segment.Lines.Count > 0)
                {
                    StoryLine lastLine = segment.Lines[segment.Lines.Count - 1];
                    if (lastLine.kind == StoryLineKind.Choice && segment.LastRow + 1 == row.RowNumber)
                    {
                        lastLine.choices = lastLine.choices.Concat(new[] { text }).ToArray();
                        segment.LastRow = row.RowNumber;
                        importedLines++;
                        continue;
                    }
                }

                StoryLine line = new StoryLine
                {
                    kind = kind,
                    speaker = kind == StoryLineKind.Choice ? string.Empty : type,
                    text = kind == StoryLineKind.Choice ? string.Empty : text,
                    choices = kind == StoryLineKind.Choice
                        ? new[] { text }
                        : Array.Empty<string>(),
                    portraitKey = InferPortraitKey(stage, type, kind),
                    audioCue = audio,
                    stageNote = stage,
                    sceneTag = scene,
                    note = note
                };

                segment.Lines.Add(line);
                segment.LastRow = row.RowNumber;
                importedLines++;

                if (kind != StoryLineKind.Broadcast && kind != StoryLineKind.Tutorial)
                {
                    segment.AllNonLocking = false;
                }
            }

            if (segments.Count == 0)
            {
                AddError("没有可导入的剧情行，请检查剧情ID列是否填写。");
                return;
            }

            int created = 0;
            int updated = 0;
            try
            {
                Dictionary<string, StoryDefinition> existing = CollectExistingDefinitions();

                AssetDatabase.StartAssetEditing();
                try
                {
                    EnsureFolder(outputFolder);
                    List<StoryDefinition> imported = new List<StoryDefinition>();

                    for (int index = 0; index < segments.Count; index++)
                    {
                        Segment segment = segments[index];
                        StoryLine[] lines = segment.Lines.ToArray();
                        bool shouldLockPlayer = !segment.AllNonLocking;

                        if (existing.TryGetValue(segment.Id, out StoryDefinition definition))
                        {
                            definition.lines = lines;
                            EditorUtility.SetDirty(definition);
                            updated++;

                            if (definition.lockPlayer == segment.AllNonLocking)
                            {
                                warnings.Add(
                                    $"剧情ID {segment.Id} 的 lockPlayer 与内容类型不匹配，请检查该资产。");
                            }
                        }
                        else
                        {
                            definition = ScriptableObject.CreateInstance<StoryDefinition>();
                            definition.storyId = segment.Id;
                            definition.chapterTitle = segment.Id;
                            definition.playOnce = true;
                            definition.lockPlayer = shouldLockPlayer;
                            definition.completeObserveTaskOnFinish = false;
                            definition.lines = lines;

                            string assetPath = $"{outputFolder.TrimEnd('/')}/{SanitizeFileName(segment.Id)}.asset";
                            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                            {
                                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
                            }

                            AssetDatabase.CreateAsset(definition, assetPath);
                            created++;
                        }

                        imported.Add(definition);
                    }

                    UpdateLibrary(imported);
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                AddError($"生成资产失败：{exception.Message}");
                Debug.LogException(exception);
                return;
            }

            report.Add(
                $"完成：读取 {table.Rows.Count} 行，导入 {importedLines} 行，段落 {segments.Count} 个（新建 {created}，更新 {updated}）。");
            report.Add(
                $"跳过：无剧情ID {skippedNoId}，转场 {skippedTransition}，道具说明 {skippedItem}，空正文 {skippedEmpty}。");

            for (int index = 0; index < segments.Count; index++)
            {
                Segment segment = segments[index];
                report.Add(
                    $"{segment.Id}：{segment.Lines.Count} 行，lockPlayer={(!segment.AllNonLocking).ToString().ToLowerInvariant()}");
            }

            for (int index = 0; index < warnings.Count; index++)
            {
                report.Add($"警告：{warnings[index]}");
            }

            Debug.Log(
                $"[Story] 导入完成：段落 {segments.Count} 个，行 {importedLines} 条，新建 {created}，更新 {updated}。");
            Repaint();
        }

        private void UpdateLibrary(List<StoryDefinition> imported)
        {
            string folder = Path.GetDirectoryName(libraryPath)?.Replace('\\', '/');
            EnsureFolder(folder);

            if (!libraryPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                libraryPath += ".asset";
            }

            StoryLibrary library = AssetDatabase.LoadAssetAtPath<StoryLibrary>(libraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<StoryLibrary>();
                AssetDatabase.CreateAsset(library, libraryPath);
                report.Add($"新建剧情库：{libraryPath}");
            }
            else
            {
                int removed = library.stories == null
                    ? 0
                    : library.stories.Count(story => story != null && !imported.Contains(story));
                if (removed > 0)
                {
                    report.Add($"从剧情库移除 {removed} 个未在本表中出现的段落（资产文件保留）。");
                }
            }

            library.stories = imported;
            EditorUtility.SetDirty(library);
        }

        private static Dictionary<string, StoryDefinition> CollectExistingDefinitions()
        {
            Dictionary<string, StoryDefinition> existing =
                new Dictionary<string, StoryDefinition>(StringComparer.Ordinal);

            string[] guids = AssetDatabase.FindAssets("t:StoryDefinition");
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                StoryDefinition definition = AssetDatabase.LoadAssetAtPath<StoryDefinition>(path);
                if (definition == null || string.IsNullOrWhiteSpace(definition.storyId))
                {
                    continue;
                }

                string id = definition.storyId.Trim();
                if (existing.ContainsKey(id))
                {
                    Debug.LogWarning($"[Story] 存在多个 storyId 为 {id} 的资产，保留第一个：{path}");
                    continue;
                }

                existing.Add(id, definition);
            }

            return existing;
        }

        private static XlsxRow FindHeaderRow(XlsxTable table)
        {
            for (int index = 0; index < table.Rows.Count; index++)
            {
                XlsxRow row = table.Rows[index];
                bool hasStoryId = row.Cells.Any(cell => cell.Trim() == "剧情ID");
                bool hasType = row.Cells.Any(cell => cell.Trim() == "对象");
                bool hasText = row.Cells.Any(cell => cell.Trim() == "内容");
                if (hasStoryId && hasType && hasText)
                {
                    return row;
                }
            }

            return null;
        }

        private static Dictionary<string, int> BuildColumnMap(XlsxRow header)
        {
            Dictionary<string, int> columns = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < header.Cells.Length; index++)
            {
                string name = header.Cells[index].Trim();
                if (name.Length > 0 && !columns.ContainsKey(name))
                {
                    columns.Add(name, index);
                }
            }

            return columns;
        }

        private static int FindColumn(Dictionary<string, int> columns, string name)
        {
            return columns.TryGetValue(name, out int index) ? index : -1;
        }

        private static StoryLineKind ResolveKind(string type, string stage, string scene)
        {
            switch (type)
            {
                case "选项":
                    return StoryLineKind.Choice;
                case "引导":
                    return StoryLineKind.Tutorial;
                case "广播":
                    return StoryLineKind.Broadcast;
                case "警告":
                    return StoryLineKind.Warning;
            }

            if (stage.Contains("黑底白字") || scene == "黑屏")
            {
                return StoryLineKind.Narration;
            }

            return StoryLineKind.Dialogue;
        }

        private static string InferPortraitKey(string stage, string speaker, StoryLineKind kind)
        {
            if (kind != StoryLineKind.Dialogue)
            {
                return string.Empty;
            }

            if (stage.Contains("鸟头人"))
            {
                return "firefeather";
            }

            if (stage.Contains("雪之偶") || stage.Contains("小女孩"))
            {
                return "snowdoll";
            }

            if (stage.Contains("研究员"))
            {
                return "researcher";
            }

            if (stage.Contains("主角"))
            {
                return "player";
            }

            return speaker;
        }

        private static string Get(XlsxRow row, int column)
        {
            return column >= 0 ? row.Get(column).Trim() : string.Empty;
        }

        private static string SanitizeFileName(string value)
        {
            string result = value ?? string.Empty;
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int index = 0; index < invalid.Length; index++)
            {
                result = result.Replace(invalid[index], '_');
            }

            return string.IsNullOrWhiteSpace(result) ? "Story" : result;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (folder == "Assets" || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return Path.IsPathRooted(path) ? path : ResolveProjectPath(path);
        }

        private static string ResolveProjectPath(string path)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        private void AddError(string message)
        {
            report.Add($"错误：{message}");
            Debug.LogError($"[Story] {message}");
            Repaint();
        }
    }
}
