using System;
using UnityEngine;

namespace Spotlight.Battle
{
    public sealed class CombatHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;

        public event Action<float, float> HealthChanged;
        public event Action Died;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead => currentHealth <= 0f;
        public float Normalized => maxHealth <= 0f ? 0f : currentHealth / maxHealth;

        public void Configure(float maximumHealth, bool fill = true)
        {
            maxHealth = Mathf.Max(1f, maximumHealth);
            currentHealth = fill ? maxHealth : Mathf.Clamp(currentHealth, 0f, maxHealth);
        }

        public void ResetToFull()
        {
            SetCurrent(maxHealth);
        }

        public float ApplyDamage(float amount)
        {
            if (amount <= 0f || IsDead)
            {
                return 0f;
            }

            float previous = currentHealth;
            SetCurrent(currentHealth - amount);
            return previous - currentHealth;
        }

        public float Heal(float amount)
        {
            if (amount <= 0f || IsDead)
            {
                return 0f;
            }

            float previous = currentHealth;
            SetCurrent(currentHealth + amount);
            return currentHealth - previous;
        }

        private void Awake()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }

        private void SetCurrent(float value)
        {
            float previous = currentHealth;
            currentHealth = Mathf.Clamp(value, 0f, maxHealth);
            if (Mathf.Approximately(previous, currentHealth))
            {
                return;
            }

            HealthChanged?.Invoke(previous, currentHealth);
            if (previous > 0f && currentHealth <= 0f)
            {
                Died?.Invoke();
            }
        }
    }
}
