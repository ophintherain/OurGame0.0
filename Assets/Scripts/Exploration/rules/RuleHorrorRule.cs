using System;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 一组规则怪谈：对应一段 2 行广播，分两段推进。
    /// </summary>
    [Serializable]
    public sealed class RuleHorrorRule
    {
        [Tooltip("必填。剧情库里的广播段 ID，例如 ch1_rule_01")]
        public string storyId;

        [Tooltip("Inspector 辨认用的规则编号 / 名字，不参与运行逻辑")]
        public string title;

        [Tooltip("触发前必须拥有的物品 ID；0 = 不限制。没有该物品时本组规则会等待，不会跳过")]
        public int requiredItemId;

        [Tooltip("两段规则。第一段完成后自动播第二句并进入第二段；第二段完成后整组完成")]
        public RuleStage[] stages = new RuleStage[2];
    }
}
