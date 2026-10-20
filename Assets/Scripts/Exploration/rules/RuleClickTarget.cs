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

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

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

            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogWarning("[Rule] RuleClickTarget 没有填 key，点击已忽略。", this);
                return;
            }

            Clicked?.Invoke(key, transform.position);
        }

        private void OnDrawGizmos()
        {
            Vector3 center = (Vector2)transform.position + offset;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(center, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 0f));
        }
    }
}
