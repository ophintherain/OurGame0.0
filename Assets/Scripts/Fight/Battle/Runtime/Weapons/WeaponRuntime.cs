using UnityEngine;

namespace Spotlight.Battle
{
    public sealed class WeaponRuntime
    {
        public WeaponRuntime(WeaponDefinition definition)
        {
            Definition = definition;
        }

        public WeaponDefinition Definition { get; }
        public float CooldownRemaining { get; private set; }
        public bool IsReady => Definition != null && CooldownRemaining <= 0f;

        public void Tick(float deltaTime)
        {
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Mathf.Max(0f, deltaTime));
        }

        public bool TryConsumeUse()
        {
            if (!IsReady)
            {
                return false;
            }

            CooldownRemaining = Definition.CooldownSeconds;
            return true;
        }

        public void Reset()
        {
            CooldownRemaining = 0f;
        }
    }
}
