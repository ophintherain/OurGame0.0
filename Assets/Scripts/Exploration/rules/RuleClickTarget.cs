using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 规则怪谈的点击目标：挂在空物体上，用世界坐标矩形判定点击，不需要 Collider。
    /// Scene 视图会用黄色线框画出点击范围，方便对齐壁画、窗户等画面元素。
    /// </summary>
    public sealed class RuleClickTarget : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("规则配置里引用的 key，例如 Window / MuralEye。留空时点击会被忽略并输出警告")]
        private string key = string.Empty;

        [SerializeField]
        [Tooltip("点击矩形的宽高（世界单位）。Scene 视图的黄色线框就是这个范围，按画面元素大小调整")]
        private Vector2 size = new Vector2(2f, 3f);

        [SerializeField]
        [Tooltip("点击矩形相对物体位置的偏移（世界单位）。物体不在矩形中心时用它微调")]
        private Vector2 offset;

        [SerializeField]
        [Tooltip("勾选后不使用世界坐标，而是判断鼠标是否在屏幕归一化区域内。适合“点击屏幕右下角”这类规则")]
        private bool screenSpace;

        [SerializeField]
        [Tooltip("屏幕归一化点击区域，x/y/宽/高都是 0~1。默认 x=0.70、y=0、宽=0.30、高=0.30，即右下角。仅 screenSpace 勾选时生效")]
        private Rect screenRect = new Rect(0.70f, 0f, 0.30f, 0.30f);

        /// <summary>目标的 key。</summary>
        public string Key => key;

        /// <summary>被点击（参数为 key 和目标世界坐标）。</summary>
        public event Action<string, Vector3> Clicked;

        private void Update()
        {
            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            // 屏幕空间点击通常落在对话 / 广播 UI 上，不能被 UI 的 Raycast 拦截
            if (!screenSpace && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (screenSpace)
            {
                Vector2 normalized = new Vector2(
                    Input.mousePosition.x / Mathf.Max(1f, Screen.width),
                    Input.mousePosition.y / Mathf.Max(1f, Screen.height));

                if (!screenRect.Contains(normalized))
                {
                    return;
                }
            }
            else
            {
                Camera worldCamera = Camera.main;
                if (worldCamera == null)
                {
                    return;
                }

                Vector3 world = worldCamera.ScreenToWorldPoint(Input.mousePosition);
                Vector2 center = (Vector2)transform.position + offset;
                float halfWidth = Mathf.Abs(size.x) * 0.5f;
                float halfHeight = Mathf.Abs(size.y) * 0.5f;

                if (Mathf.Abs(world.x - center.x) > halfWidth ||
                    Mathf.Abs(world.y - center.y) > halfHeight)
                {
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogWarning("[Rule] RuleClickTarget 没有填 key，点击已忽略。", this);
                return;
            }

            Clicked?.Invoke(key, transform.position);
        }

        private void OnDrawGizmos()
        {
            if (screenSpace)
            {
                return;
            }

            Vector3 center = (Vector2)transform.position + offset;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(center, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 0f));
        }
    }
}
