using System;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>一个阶段里多条条件的组合方式。</summary>
    public enum RuleConditionCombine
    {
        /// <summary>全部条件都满足才算符合。</summary>
        All,

        /// <summary>任意一条条件满足就算符合。</summary>
        Any
    }

    /// <summary>阶段开始时执行的场景动作。</summary>
    public enum RuleStageAction
    {
        /// <summary>不执行动作。</summary>
        None,

        /// <summary>熄灯（黑遮罩淡入，露出壁画洞）。</summary>
        LightsOff,

        /// <summary>开灯（黑遮罩淡出）。</summary>
        LightsOn,

        /// <summary>Boss 判定开始（激活 Boss 占位物，如果有配置）。</summary>
        SpawnBoss,

        /// <summary>播放环境音（循环），音效 key 由 soundCueKey 指定。</summary>
        PlayRuleSound,

        /// <summary>停止本规则开始时播放的环境音。</summary>
        StopRuleSound
    }

    /// <summary>
    /// 规则怪谈的一段。一组规则有两段：第一段完成后才播第二句并进入第二段。
    /// </summary>
    [Serializable]
    public sealed class RuleStage
    {
        [Tooltip("条件组合方式：All=全部满足；Any=任意一条满足。条件为空时视为满足")]
        public RuleConditionCombine combine = RuleConditionCombine.All;

        [Tooltip("这一段的持续条件。留空表示只要撑过 requiredHoldSeconds 就算完成")]
        public RuleHorrorCondition[] conditions = Array.Empty<RuleHorrorCondition>();

        [Min(0f)]
        [Tooltip("这一段开始后的宽限时间（秒）。宽限内不判定、不扣血")]
        public float graceSeconds = 4f;

        [Min(0f)]
        [Tooltip("满足条件后需要连续保持多少秒才算完成。条件为空时，它就是这一段的自动完成时间")]
        public float requiredHoldSeconds = 2f;

        [Min(0f)]
        [Tooltip("违规时每次结算扣多少血。结算间隔由 RuleHorrorDirector 的 damageTickSeconds 控制；默认 3")]
        public float damagePerTick = 3f;

        [Tooltip("点击这个 key 立即完成本段。非空时本段只能靠点击完成，点击前持续扣血")]
        public string completionClickKey;

        [Tooltip("本段生效期间点击这个 key 立即扣一次血，并重置保持计时。留空 = 点击不惩罚")]
        public string forbiddenClickKey;

        [Min(0f)]
        [Tooltip("点击 forbiddenClickKey 时一次扣多少血，默认 3")]
        public float forbiddenClickDamage = 3f;

        [Tooltip("本段开始时执行的场景动作：None=无；LightsOff=熄灯；LightsOn=开灯；SpawnBoss=开始 Boss 判定；PlayRuleSound=播放环境音；StopRuleSound=停止环境音")]
        public RuleStageAction action = RuleStageAction.None;

        [Tooltip("环境音 key，仅 PlayRuleSound 使用。对应 StoryAudioSet 里的条目名，例如「滴水声」。留空则不播")]
        public string soundCueKey;

        [Tooltip("点击完成本段时必须持有的物品 ID，0 = 不限制。没持有该物品时点击 completionClickKey 会按违规扣血，不会完成本段")]
        public int completionClickRequiredItemId;

        /// <summary>本段是否必须靠点击完成。</summary>
        public bool HasCompletionClick => !string.IsNullOrWhiteSpace(completionClickKey);

        /// <summary>当前状态下是否满足本段条件。</summary>
        public bool IsSatisfied(Vector3 playerPosition, ExplorationPosture posture,
            bool isMoving, bool movementKeyHeld, ExplorationInventory inventory,
            Vector3 bossPosition, bool hasBoss)
        {
            if (conditions == null || conditions.Length == 0)
            {
                return true;
            }

            bool any = combine == RuleConditionCombine.Any;
            bool hasValidCondition = false;

            for (int index = 0; index < conditions.Length; index++)
            {
                RuleHorrorCondition condition = conditions[index];
                if (condition == null)
                {
                    continue;
                }

                hasValidCondition = true;
                bool satisfied = condition.IsSatisfied(
                    playerPosition, posture, isMoving, movementKeyHeld, inventory,
                    bossPosition, hasBoss);

                if (any && satisfied)
                {
                    return true;
                }

                if (!any && !satisfied)
                {
                    return false;
                }
            }

            return !any || !hasValidCondition;
        }
    }
}
