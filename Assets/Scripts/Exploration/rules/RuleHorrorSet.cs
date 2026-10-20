using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>一个场景里可随机触发的规则怪谈集合。</summary>
    [CreateAssetMenu(menuName = "Spotlight/规则怪谈集", fileName = "RuleHorrorSet")]
    public sealed class RuleHorrorSet : ScriptableObject
    {
        [Tooltip("本场景的规则列表。调度器只从这里面随机抽，全部完成后会清空进度重新循环")]
        public List<RuleHorrorRule> rules = new List<RuleHorrorRule>();
    }
}
