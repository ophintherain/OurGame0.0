using System;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>规则怪谈的持续判定条件类型。</summary>
    public enum RuleConditionType
    {
        /// <summary>玩家必须待在 zones 内。</summary>
        InZones,

        /// <summary>玩家必须待在 zones 外。</summary>
        OutZones,

        /// <summary>玩家必须保持指定姿势。</summary>
        Posture,

        /// <summary>玩家必须保持不移动。</summary>
        NotMoving,

        /// <summary>背包必须拥有 itemId。</summary>
        HasItem,

        /// <summary>背包必须没有 itemId。</summary>
        NotHasItem,

        /// <summary>与 Boss 的水平距离必须超过 range。</summary>
        OutOfBossRange,

        /// <summary>与被追踪目标的水平距离必须不超过 range。</summary>
        NearTarget,

        /// <summary>与被追踪目标的水平距离必须超过 range。</summary>
        OutOfTargetRange,

        /// <summary>被追踪目标的整数状态必须不小于 stateValue。</summary>
        TargetValueAtLeast,

        /// <summary>被追踪目标的整数状态必须不大于 stateValue。</summary>
        TargetValueAtMost
    }

    /// <summary>
    /// 一条持续判定条件。一个阶段里的条件按 All / Any 组合。
    /// </summary>
    [Serializable]
    public sealed class RuleHorrorCondition
    {
        [Tooltip("条件类型：InZones=必须在区域内；OutZones=必须在区域外；Posture=姿势；NotMoving=不能移动；HasItem=背包拥有物品；NotHasItem=背包没有物品；OutOfBossRange=Boss 距离；NearTarget=靠近被追踪目标；OutOfTargetRange=远离被追踪目标；TargetValueAtLeast=状态值不小于阈值；TargetValueAtMost=状态值不大于阈值")]
        public RuleConditionType type = RuleConditionType.InZones;

        [Tooltip("世界 X 区域列表，仅 InZones / OutZones 使用。InZones 留空表示永远不满足，OutZones 留空表示永远满足")]
        public RuleZone[] zones = Array.Empty<RuleZone>();

        [Tooltip("要求玩家保持的姿势，仅 Posture 使用")]
        public ExplorationPosture posture = ExplorationPosture.Standing;

        [Tooltip("物品 ID，仅 HasItem / NotHasItem 使用。必须 4 位数：1 开头=武器，2 开头=物品")]
        public int itemId;

        [Min(0f)]
        [Tooltip("与 Boss 或追踪目标的安全水平距离（世界单位），仅 OutOfBossRange / NearTarget / OutOfTargetRange 使用")]
        public float range = 3f;

        [Tooltip("被追踪目标的 key，仅 NearTarget / OutOfTargetRange / TargetValueAtLeast / TargetValueAtMost 使用。对应场景里 RuleTrackedTarget 或 RuleStateTarget 上填的 key")]
        public string targetKey;

        [Tooltip("状态阈值，仅 TargetValueAtLeast / TargetValueAtMost 使用。整数状态与它比较")]
        public int stateValue;

        /// <summary>当前状态下这条条件是否满足。</summary>
        public bool IsSatisfied(Vector3 playerPosition, ExplorationPosture currentPosture,
            bool isMoving, bool movementKeyHeld, ExplorationInventory inventory,
            Vector3 bossPosition, bool hasBoss)
        {
            switch (type)
            {
                case RuleConditionType.InZones:
                    if (zones == null || zones.Length == 0)
                    {
                        return false;
                    }

                    for (int index = 0; index < zones.Length; index++)
                    {
                        if (zones[index] != null && zones[index].Contains(playerPosition.x))
                        {
                            return true;
                        }
                    }

                    return false;

                case RuleConditionType.OutZones:
                    if (zones == null || zones.Length == 0)
                    {
                        return true;
                    }

                    for (int index = 0; index < zones.Length; index++)
                    {
                        if (zones[index] != null && zones[index].Contains(playerPosition.x))
                        {
                            return false;
                        }
                    }

                    return true;

                case RuleConditionType.Posture:
                    return currentPosture == posture;

                case RuleConditionType.NotMoving:
                    return !isMoving && !movementKeyHeld;

                case RuleConditionType.HasItem:
                    return inventory != null && inventory.Contains(itemId);

                case RuleConditionType.NotHasItem:
                    return inventory == null || !inventory.Contains(itemId);

                case RuleConditionType.OutOfBossRange:
                    return !hasBoss || Mathf.Abs(playerPosition.x - bossPosition.x) > range;

                case RuleConditionType.NearTarget:
                    return RuleTrackedTarget.TryGetPosition(targetKey, out float nearX) &&
                           Mathf.Abs(playerPosition.x - nearX) <= range;

                case RuleConditionType.OutOfTargetRange:
                    return !RuleTrackedTarget.TryGetPosition(targetKey, out float farX) ||
                           Mathf.Abs(playerPosition.x - farX) > range;

                case RuleConditionType.TargetValueAtLeast:
                    return RuleStateTarget.TryGetValue(targetKey, out int minValue) &&
                           minValue >= stateValue;

                case RuleConditionType.TargetValueAtMost:
                    return !RuleStateTarget.TryGetValue(targetKey, out int maxValue) ||
                           maxValue <= stateValue;

                default:
                    return true;
            }
        }
    }
}
