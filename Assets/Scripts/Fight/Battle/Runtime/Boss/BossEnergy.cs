using System;
using UnityEngine;

namespace Spotlight.Battle
{
    public sealed class BossEnergy : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maximum = 200f;
        [SerializeField, Min(0f)] private float passivePercentPerSecond = 15f;
        [SerializeField, Min(0f)] private float damageConversionRatio = 0.2f;

        public event Action<float, float> EnergyChanged;

        public float Maximum => maximum;
        public float Current { get; private set; }
        public float Normalized => maximum <= 0f ? 0f : Current / maximum;
        public bool IsFull => Current >= maximum;

        public void Configure(float maxEnergy, float passivePercent, float damageRatio)
        {
            maximum = Mathf.Max(1f, maxEnergy);
            passivePercentPerSecond = Mathf.Max(0f, passivePercent);
            damageConversionRatio = Mathf.Max(0f, damageRatio);
            Current = Mathf.Clamp(Current, 0f, maximum);
        }

        public void ResetEnergy()
        {
            SetCurrent(0f);
        }

        public void SimulateStep(float deltaTime)
        {
            Add(maximum * passivePercentPerSecond * 0.01f * Mathf.Max(0f, deltaTime));
        }

        public void AddFromDamage(float appliedDamage)
        {
            Add(Mathf.Max(0f, appliedDamage) * damageConversionRatio);
        }

        public bool TrySpendFull()
        {
            if (!IsFull)
            {
                return false;
            }

            SetCurrent(0f);
            return true;
        }

        private void Add(float amount)
        {
            SetCurrent(Current + Mathf.Max(0f, amount));
        }

        private void SetCurrent(float value)
        {
            float previous = Current;
            Current = Mathf.Clamp(value, 0f, maximum);
            if (!Mathf.Approximately(previous, Current))
            {
                EnergyChanged?.Invoke(previous, Current);
            }
        }
    }
}
