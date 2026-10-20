using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 探索阶段的玩家血量。数值用静态字段保存，切换场景后沿用当前血量；
    /// 归零后等待片刻，重置满血并重载当前场景（本期用重载场景代替正式的重开挑战 UI）。
    /// </summary>
    public sealed class ExplorationHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)]
        [Tooltip("最大生命值（满血值）。只在本次运行的第一次初始化时生效，之后跨场景沿用已保存的上限；改这里不会给已开始的运行回血")]
        private float maxHealth = 100f;

        [SerializeField, Min(0f)]
        [Tooltip("血量归零后等待多少秒再重载场景，单位秒。用于播完血条闪烁，建议与血条的「闪烁总时长」保持一致")]
        private float deathDelaySeconds = 0.8f;

        [SerializeField]
        [Tooltip("血量归零后是否自动重载当前场景。勾选 = 重新开始挑战（当前用重载场景代替）；后续接入正式重开 UI 时可取消勾选")]
        private bool reloadSceneOnDeath = true;

        private static float savedMaxHealth = -1f;
        private static float savedCurrentHealth;
        private bool deathHandled;

        /// <summary>血量变化（旧值, 新值）。</summary>
        public event Action<float, float> HealthChanged;

        /// <summary>血量归零。</summary>
        public event Action Died;

        /// <summary>血量上限，跨场景沿用第一次初始化的值。</summary>
        public float MaxHealth => savedMaxHealth > 0f ? savedMaxHealth : maxHealth;

        /// <summary>当前血量。</summary>
        public float CurrentHealth => savedCurrentHealth;

        /// <summary>当前血量比例，0 ~ 1。</summary>
        public float Normalized => MaxHealth <= 0f ? 0f : Mathf.Clamp01(CurrentHealth / MaxHealth);

        /// <summary>是否已经归零。</summary>
        public bool IsDead => CurrentHealth <= 0f;

        private void Awake()
        {
            if (savedMaxHealth <= 0f)
            {
                savedMaxHealth = Mathf.Max(1f, maxHealth);
                savedCurrentHealth = savedMaxHealth;
            }
        }

        /// <summary>扣血，返回实际扣掉的血量；已经归零或 amount 不为正时返回 0。</summary>
        public float ApplyDamage(float amount)
        {
            if (amount <= 0f || IsDead)
            {
                return 0f;
            }

            float previous = CurrentHealth;
            SetCurrent(previous - amount);
            return previous - CurrentHealth;
        }

        /// <summary>回血，返回实际回复的血量；已经归零或 amount 不为正时返回 0。</summary>
        public float Heal(float amount)
        {
            if (amount <= 0f || IsDead)
            {
                return 0f;
            }

            float previous = CurrentHealth;
            SetCurrent(previous + amount);
            return CurrentHealth - previous;
        }

        /// <summary>回满血（死亡重开前也会调用）。</summary>
        public void ResetToFull()
        {
            SetCurrent(MaxHealth);
        }

        [ContextMenu("调试：扣 10 血")]
        private void DebugDamageTen()
        {
            ApplyDamage(10f);
        }

        [ContextMenu("调试：回满血")]
        private void DebugRefill()
        {
            ResetToFull();
        }

        private void SetCurrent(float value)
        {
            float previous = CurrentHealth;
            savedCurrentHealth = Mathf.Clamp(value, 0f, MaxHealth);
            if (Mathf.Approximately(previous, savedCurrentHealth))
            {
                return;
            }

            if (savedCurrentHealth > 0f)
            {
                deathHandled = false;
            }

            HealthChanged?.Invoke(previous, savedCurrentHealth);

            if (previous > 0f && savedCurrentHealth <= 0f)
            {
                HandleDeath();
            }
        }

        private void HandleDeath()
        {
            if (deathHandled)
            {
                return;
            }

            deathHandled = true;
            Died?.Invoke();

            if (reloadSceneOnDeath)
            {
                StartCoroutine(RestartAfterDelay());
            }
        }

        private IEnumerator RestartAfterDelay()
        {
            if (deathDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(deathDelaySeconds);
            }

            // 先回满再重载：新场景的 ExplorationHealth 会直接沿用静态值，否则一进场又是 0 血。
            ResetToFull();

            Scene active = SceneManager.GetActiveScene();
            if (active.buildIndex >= 0)
            {
                SceneManager.LoadScene(active.buildIndex);
            }
            else
            {
                SceneManager.LoadScene(active.name);
            }
        }
    }
}
