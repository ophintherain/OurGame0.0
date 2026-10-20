using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 给规则条件读取的整数状态目标（例如电视音量档位）。
    /// 规则资产只保存 key，条件运行时通过静态表读取当前值。
    /// </summary>
    public sealed class RuleStateTarget : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("规则条件里引用的 key，例如 TV。必须与 RuleHorrorCondition 的 targetKey 完全一致")]
        private string key = string.Empty;

        [SerializeField]
        [Tooltip("当前整数状态值。由场景脚本（如 ExplorationTelevision）在运行时更新，一般不需要手动改")]
        private int value;

        private static readonly Dictionary<string, RuleStateTarget> registry =
            new Dictionary<string, RuleStateTarget>();

        /// <summary>当前注册的 key。</summary>
        public string Key => key;

        /// <summary>当前整数状态值。</summary>
        public int Value => value;

        private void OnEnable()
        {
            Register();
        }

        private void OnDisable()
        {
            Unregister();
        }

        /// <summary>更新状态值。</summary>
        public void SetValue(int newValue)
        {
            value = newValue;
        }

        /// <summary>修改 key 并同步更新注册表。</summary>
        public void SetKey(string newKey)
        {
            bool active = isActiveAndEnabled;
            if (active)
            {
                Unregister();
            }

            key = newKey;

            if (active)
            {
                Register();
            }
        }

        /// <summary>按 key 读取状态值；目标不存在时返回 false。</summary>
        public static bool TryGetValue(string targetKey, out int result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(targetKey))
            {
                return false;
            }

            if (!registry.TryGetValue(targetKey.Trim(), out RuleStateTarget target) || target == null)
            {
                return false;
            }

            result = target.value;
            return true;
        }

        private void Register()
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogWarning("[Rule] RuleStateTarget 没有填 key，规则条件无法读取它。", this);
                return;
            }

            registry[key.Trim()] = this;
        }

        private void Unregister()
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            string trimmed = key.Trim();
            if (registry.TryGetValue(trimmed, out RuleStateTarget target) && target == this)
            {
                registry.Remove(trimmed);
            }
        }
    }
}
