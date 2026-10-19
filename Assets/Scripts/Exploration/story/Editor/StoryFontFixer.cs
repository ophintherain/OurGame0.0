using TMPro;
using UnityEditor;
using UnityEngine;

namespace Spotlight.Exploration.StoryEditor
{
    /// <summary>
    /// 修复剧情字体缺字：把 sourcehansans 字体资产设为 Dynamic，并启用 Multi Atlas Textures。
    /// 入口：Spotlight/剧情/修复字体缺失
    /// </summary>
    public static class StoryFontFixer
    {
        [MenuItem("Spotlight/剧情/修复字体缺失")]
        private static void FixMissingGlyphs()
        {
            TMP_FontAsset target = FindSourceHanSansFont();
            if (target == null)
            {
                EditorUtility.DisplayDialog(
                    "字体修复",
                    "没有找到名称包含 sourcehansans 的 TMP 字体资产。",
                    "好");
                return;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty source = serialized.FindProperty("m_SourceFontFile");
            if (source == null || source.objectReferenceValue == null)
            {
                EditorUtility.DisplayDialog(
                    "字体修复",
                    $"字体资产 {target.name} 没有绑定源字体文件，无法动态补字。\n请先在 Inspector 里指定 sourcehansans-medium.otf。",
                    "好");
                return;
            }

            SerializedProperty population = serialized.FindProperty("m_AtlasPopulationMode");
            SerializedProperty multiAtlas = serialized.FindProperty("m_IsMultiAtlasTexturesEnabled");

            if (population != null)
            {
                population.intValue = 1; // Dynamic
            }

            if (multiAtlas != null)
            {
                multiAtlas.boolValue = true;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "字体修复",
                $"已把 {target.name} 设为 Dynamic + Multi Atlas Textures。\n请重新进入 Play，缺失字形会自动补进新的图集。\n如果仍显示方块，再把图集尺寸改到 2048×2048 后重新生成。",
                "好");
            Debug.Log($"[Story] 字体资产 {target.name} 已启用 Dynamic + Multi Atlas Textures。");
        }

        private static TMP_FontAsset FindSourceHanSansFont()
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

            return null;
        }
    }
}
