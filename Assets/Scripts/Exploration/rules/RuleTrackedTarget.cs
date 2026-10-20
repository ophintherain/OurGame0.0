using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 会被规则条件追踪的场景目标（例如巡逻人偶）。
    /// 规则资产只保存字符串 key，条件运行时通过静态表取目标当前 X 坐标。
    /// </summary>
    public sealed class RuleTrackedTarget : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("规则条件里引用的 key，例如 PatrolDoll。必须与 RuleHorrorCondition 的 targetKey 完全一致")]
        private string key = string.Empty;

        private static readonly Dictionary<string, RuleTrackedTarget> registry =
            new Dictionary<string, RuleTrackedTarget>();

        /// <summary>当前注册的 key。</summary>
        public string Key => key;

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogWarning("[Rule] RuleTrackedTarget 没有填 key，规则条件无法追踪它。", this);
                return;
            }

            registry[key.Trim()] = this;
        }

        private void OnDisable()
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            string trimmed = key.Trim();
            if (registry.TryGetValue(trimmed, out RuleTrackedTarget target) && target == this)
            {
                registry.Remove(trimmed);
            }
        }

        /// <summary>按 key 取目标当前世界 X；目标不存在时返回 false。</summary>
        public static bool TryGetPosition(string targetKey, out float x)
        {
            x = 0f;
            if (string.IsNullOrWhiteSpace(targetKey))
            {
                return false;
            }

            if (!registry.TryGetValue(targetKey.Trim(), out RuleTrackedTarget target) || target == null)
            {
                return false;
            }

            x = target.transform.position.x;
            return true;
        }
    }
}
