using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.Exploration
{
    /// <summary>
    /// HUD 血条：最大生命值框保持完整，当前血量层按比例缩短（左端固定）；
    /// 扣血时框层与填充层一起闪烁。
    /// </summary>
    public sealed class ExplorationHealthBar : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("玩家血量数据（Player 上的 ExplorationHealth）。留空会在启动时自动查找场景里的一个")]
        private ExplorationHealth health;

        [SerializeField]
        [Tooltip("最大生命值框（HUD 下的 RawImage），始终完整显示，只在闪烁时改变透明度")]
        private RawImage frameImage;

        [SerializeField]
        [Tooltip("当前血量层（框的子物体 RawImage），宽度按「当前血量 ÷ 最大血量」缩短，左端固定、右端往左收")]
        private RawImage fillImage;

        [SerializeField, Min(0f)]
        [Tooltip("一次扣血后血条闪烁的总时长，单位秒。建议与 ExplorationHealth 的死亡延时保持一致，否则会闪到一半就重载")]
        private float blinkTotalSeconds = 0.8f;

        [SerializeField, Min(0.01f)]
        [Tooltip("闪烁时明暗切换的间隔，单位秒。数值越小闪得越快，0.08 约为每秒闪 6 次")]
        private float blinkIntervalSeconds = 0.08f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("闪烁时血条的最低透明度。0 = 完全消失，1 = 不闪；0.2 表示闪到两成透明")]
        private float blinkMinAlpha = 0.2f;

        private RectTransform fillRect;
        private float originalFillWidth;
        private float originalFillX;
        private Color frameColor;
        private Color fillColor;
        private bool colorsCached;
        private Coroutine blinkCoroutine;

        private void Awake()
        {
            if (health == null)
            {
                health = FindObjectOfType<ExplorationHealth>();
            }

            if (health == null || frameImage == null || fillImage == null)
            {
                Debug.LogError(
                    "[Exploration] ExplorationHealthBar 缺少 health / frameImage / fillImage 引用，血条不会更新。",
                    this);
                enabled = false;
                return;
            }

            fillRect = fillImage.rectTransform;
            originalFillWidth = fillRect.sizeDelta.x;
            originalFillX = fillRect.anchoredPosition.x;
            frameColor = frameImage.color;
            fillColor = fillImage.color;
            colorsCached = true;

            health.HealthChanged += OnHealthChanged;
        }

        private void Start()
        {
            // 放在 Start：等 Player 上的 ExplorationHealth.Awake 先初始化静态血量，避免一开始显示成空血。
            Refresh(health.Normalized);
        }

        private void OnDisable()
        {
            StopBlink();
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.HealthChanged -= OnHealthChanged;
            }
        }

        private void OnHealthChanged(float previous, float current)
        {
            Refresh(health.Normalized);

            if (current < previous)
            {
                StartBlink();
            }
        }

        /// <summary>按血量比例缩短填充层：宽度缩放，并把位置左移保持左端不动。</summary>
        private void Refresh(float normalized)
        {
            if (fillRect == null)
            {
                return;
            }

            float width = originalFillWidth * Mathf.Clamp01(normalized);
            Vector2 size = fillRect.sizeDelta;
            size.x = width;
            fillRect.sizeDelta = size;

            Vector2 position = fillRect.anchoredPosition;
            position.x = originalFillX - (originalFillWidth - width) * 0.5f;
            fillRect.anchoredPosition = position;
        }

        private void StartBlink()
        {
            StopBlink();
            blinkCoroutine = StartCoroutine(BlinkCoroutine());
        }

        private void StopBlink()
        {
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
            }

            RestoreColors();
        }

        private IEnumerator BlinkCoroutine()
        {
            float elapsed = 0f;
            bool dim = true;

            while (elapsed < blinkTotalSeconds)
            {
                ApplyBlinkAlpha(dim ? blinkMinAlpha : 1f);
                dim = !dim;

                float step = Mathf.Min(blinkIntervalSeconds, blinkTotalSeconds - elapsed);
                yield return new WaitForSeconds(step);
                elapsed += step;
            }

            RestoreColors();
            blinkCoroutine = null;
        }

        private void ApplyBlinkAlpha(float factor)
        {
            if (frameImage != null)
            {
                Color color = frameColor;
                color.a = frameColor.a * factor;
                frameImage.color = color;
            }

            if (fillImage != null)
            {
                Color color = fillColor;
                color.a = fillColor.a * factor;
                fillImage.color = color;
            }
        }

        private void RestoreColors()
        {
            if (!colorsCached)
            {
                return;
            }

            if (frameImage != null)
            {
                frameImage.color = frameColor;
            }

            if (fillImage != null)
            {
                fillImage.color = fillColor;
            }
        }
    }
}
