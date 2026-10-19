using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 全部剧情段落的清单。导入工具会更新它，StoryDirector 只引用这一个资产。
    /// </summary>
    [CreateAssetMenu(menuName = "Spotlight/剧情库", fileName = "StoryLibrary")]
    public sealed class StoryLibrary : ScriptableObject
    {
        [Tooltip("本场景可用的全部剧情段落。由导入工具按表格首次出现顺序自动填充")]
        public List<StoryDefinition> stories = new List<StoryDefinition>();
    }
}
