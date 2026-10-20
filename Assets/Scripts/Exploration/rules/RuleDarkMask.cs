using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 规则怪谈的熄灯遮罩：在相机前生成一个全屏黑色 Quad，
    /// 按壁画世界坐标挖出最多 4 个矩形洞，洞内保留背景里的壁画。
    /// </summary>
    public sealed class RuleDarkMask : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("用于计算屏幕位置与画面尺寸的相机。留空会自动取 Camera.main")]
        private Camera targetCamera;

        [SerializeField]
        [Tooltip("需要挖洞的壁画中心（世界坐标）。按第一车厢四张椭圆壁画的位置填写，最多使用前 4 个")]
        private Vector2[] muralCenters =
        {
            new Vector2(-5.84f, 1.85f),
            new Vector2(-1.96f, 1.85f),
            new Vector2(1.92f, 1.85f),
            new Vector2(5.80f, 1.85f)
        };

        [SerializeField]
        [Tooltip("每个洞的世界宽高。宽度略大于壁画、高度覆盖画框即可；Scene 视图选中本物体可看到黄色线框")]
        private Vector2 holeSize = new Vector2(1.5f, 2.6f);

        [SerializeField, Min(0.01f)]
        [Tooltip("熄灯 / 开灯的淡入淡出时长（秒）。0.25 约为四分之一秒")]
        private float fadeSeconds = 0.25f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int HoleCountId = Shader.PropertyToID("_HoleCount");
        private static readonly int[] HoleIds =
        {
            Shader.PropertyToID("_Hole0"),
            Shader.PropertyToID("_Hole1"),
            Shader.PropertyToID("_Hole2"),
            Shader.PropertyToID("_Hole3")
        };

        private GameObject maskObject;
        private SpriteRenderer maskRenderer;
        private Material maskMaterial;
        private Texture2D generatedTexture;
        private Sprite generatedSprite;
        private float currentAlpha;
        private float targetAlpha;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            CreateMask();
            ApplyAlpha(0f);
        }

        private void LateUpdate()
        {
            if (maskObject == null)
            {
                return;
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    return;
                }
            }

            UpdateQuad();
            UpdateHoles();

            float speed = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds);
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, speed);
            ApplyAlpha(currentAlpha);
        }

        private void OnDestroy()
        {
            if (maskObject != null)
            {
                Destroy(maskObject);
            }

            if (maskMaterial != null)
            {
                Destroy(maskMaterial);
            }

            if (generatedSprite != null)
            {
                Destroy(generatedSprite);
            }

            if (generatedTexture != null)
            {
                Destroy(generatedTexture);
            }
        }

        /// <summary>true = 熄灯（黑遮罩淡入）；false = 开灯（黑遮罩淡出）。</summary>
        public void SetDark(bool dark)
        {
            targetAlpha = dark ? 1f : 0f;
        }

        private void CreateMask()
        {
            Shader shader = Shader.Find("Spotlight/RuleDarkMask");
            if (shader == null)
            {
                Debug.LogError("[Rule] 找不到 Shader「Spotlight/RuleDarkMask」，熄灯表现已禁用。", this);
                enabled = false;
                return;
            }

            if (targetCamera == null)
            {
                Debug.LogError("[Rule] RuleDarkMask 找不到相机，熄灯表现已禁用。", this);
                enabled = false;
                return;
            }

            maskMaterial = new Material(shader);

            generatedTexture = new Texture2D(1, 1);
            generatedTexture.SetPixel(0, 0, Color.white);
            generatedTexture.Apply();
            generatedSprite = Sprite.Create(generatedTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);

            maskObject = new GameObject("RuleDarkMaskQuad");
            maskObject.transform.SetParent(targetCamera.transform, false);
            maskObject.transform.localPosition = new Vector3(0f, 0f, 1f);
            maskObject.transform.localRotation = Quaternion.identity;

            maskRenderer = maskObject.AddComponent<SpriteRenderer>();
            maskRenderer.sprite = generatedSprite;
            maskRenderer.material = maskMaterial;
            maskRenderer.sortingOrder = 1000;
            maskRenderer.enabled = false;

            UpdateQuad();
            UpdateHoles();
        }

        private void UpdateQuad()
        {
            if (targetCamera.orthographic)
            {
                float height = targetCamera.orthographicSize * 2f;
                float width = height * targetCamera.aspect;
                maskObject.transform.localScale = new Vector3(width, height, 1f);
            }
            else
            {
                // 透视相机：放在近裁剪面前方，按视距估算铺满画面
                float distance = Mathf.Max(0.1f, targetCamera.nearClipPlane + 0.1f);
                maskObject.transform.localPosition = new Vector3(0f, 0f, distance);
                float height = 2f * distance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float width = height * targetCamera.aspect;
                maskObject.transform.localScale = new Vector3(width, height, 1f);
            }
        }

        private void UpdateHoles()
        {
            int count = muralCenters != null ? Mathf.Min(4, muralCenters.Length) : 0;
            maskMaterial.SetFloat(HoleCountId, count);

            for (int index = 0; index < count; index++)
            {
                Vector3 viewport = targetCamera.WorldToViewportPoint(
                    new Vector3(muralCenters[index].x, muralCenters[index].y, 0f));

                float viewHeight = targetCamera.orthographic
                    ? targetCamera.orthographicSize * 2f
                    : 2f * maskObject.transform.localPosition.z *
                      Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float viewWidth = viewHeight * targetCamera.aspect;

                float sizeX = Mathf.Abs(holeSize.x) / Mathf.Max(0.01f, viewWidth);
                float sizeY = Mathf.Abs(holeSize.y) / Mathf.Max(0.01f, viewHeight);

                maskMaterial.SetVector(HoleIds[index],
                    new Vector4(viewport.x, viewport.y, sizeX, sizeY));
            }
        }

        private void ApplyAlpha(float alpha)
        {
            if (maskMaterial == null || maskRenderer == null)
            {
                return;
            }

            maskMaterial.SetColor(ColorId, new Color(0f, 0f, 0f, Mathf.Clamp01(alpha)));
            maskRenderer.enabled = alpha > 0.001f;
        }

        private void OnDrawGizmosSelected()
        {
            if (muralCenters == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Vector3 size = new Vector3(Mathf.Abs(holeSize.x), Mathf.Abs(holeSize.y), 0f);

            for (int index = 0; index < muralCenters.Length; index++)
            {
                Gizmos.DrawWireCube(new Vector3(muralCenters[index].x, muralCenters[index].y, 0f), size);
            }
        }
    }
}
