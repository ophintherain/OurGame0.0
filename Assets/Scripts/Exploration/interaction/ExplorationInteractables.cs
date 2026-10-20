using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 可探索点：每个点在场景里自由摆放（Inspector 列表 + Scene 拖拽手柄）。
    /// 点的「排 / 列」由世界坐标自动换算，仍要求与玩家同排、且列差为 1 才能交互。
    /// 鼠标点击按立绘实际显示范围判定；没有配置立绘时用格子大小的占位框。
    /// 获得物品会写入 ExplorationInventory；剧情类型本期只派发 StoryTriggered 事件。
    /// </summary>
    public sealed class ExplorationInteractables : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("必填。指向场景里 Map 物体上的 ExplorationMap；留空会在 Awake 报错并禁用本脚本")]
        private ExplorationMap map;

        [SerializeField]
        [Tooltip("必填。指向 Player 上的 ExplorationPlayerController，用于读取当前列、姿势和输入是否被锁")]
        private ExplorationPlayerController player;

        [SerializeField]
        [Tooltip("获得物品时写入的背包。留空会在场景里自动查找一个 ExplorationInventory")]
        private ExplorationInventory inventory;

        [SerializeField]
        [Tooltip("顶部功能提示组件。留空会在场景里自动查找一个 ExplorationHintPanel")]
        private ExplorationHintPanel hintPanel;

        [SerializeField]
        [Tooltip("可探索点列表。每项填世界坐标和类型；选中本物体后可在 Scene 视图里拖动黄色手柄自由摆放")]
        private List<ExplorationPoint> points = new List<ExplorationPoint>();

        [SerializeField, Min(0.01f)]
        [Tooltip("没有配置立绘时，占位点击区域的高度（世界单位）；宽度取地图格宽。该区域默认透明，看不见但仍可点击")]
        private float rowHeight = 1.4f;

        [SerializeField]
        [Tooltip("所有交互点立绘的 Sorting Order，数值越大越靠前，需要高于背景和角色")]
        private int highlightSortingOrder = 20;

        /// <summary>剧情触发（剧情标识）。本期没有订阅者，留给后续剧情系统接入。</summary>
        public event Action<string> StoryTriggered;

        /// <summary>任意可探索点触发（给后续提示、音效等使用）。</summary>
        public event Action<ExplorationPoint> PointTriggered;

        /// <summary>当前配置的可探索点（只读，供编辑器预览使用）。</summary>
        public IReadOnlyList<ExplorationPoint> Points => points;

        private sealed class PointRuntime
        {
            public ExplorationPoint Point;
            public SpriteRenderer Renderer;
            public bool Collected;
        }

        private readonly List<PointRuntime> runtimePoints = new List<PointRuntime>();

        private void Awake()
        {
            if (map == null || player == null)
            {
                Debug.LogError("[Exploration] ExplorationInteractables 缺少 map 或 player 引用。", this);
                enabled = false;
                return;
            }

            if (inventory == null)
            {
                inventory = FindObjectOfType<ExplorationInventory>();
            }

            if (inventory == null)
            {
                Debug.LogWarning("[Exploration] 场景里没有 ExplorationInventory，获得物品类型的可探索点不会生效。", this);
            }

            if (hintPanel == null)
            {
                hintPanel = FindObjectOfType<ExplorationHintPanel>();
            }

            if (points == null)
            {
                points = new List<ExplorationPoint>();
            }

            CreatePointVisuals();

            if (inventory != null)
            {
                inventory.Changed += OnInventoryChanged;
                HideOwnedPoints();
            }
        }

        private void OnDestroy()
        {
            if (inventory != null)
            {
                inventory.Changed -= OnInventoryChanged;
            }
        }

        /// <summary>背包变化时，把已经拥有的物品对应的交互点隐藏（不管物品是从哪来的）。</summary>
        private void OnInventoryChanged()
        {
            HideOwnedPoints();
        }

        private void HideOwnedPoints()
        {
            if (inventory == null)
            {
                return;
            }

            for (int index = 0; index < runtimePoints.Count; index++)
            {
                PointRuntime entry = runtimePoints[index];
                if (entry.Collected || entry.Point == null ||
                    entry.Point.type != ExplorationPointType.Item)
                {
                    continue;
                }

                if (inventory.Contains(entry.Point.itemId))
                {
                    HidePoint(entry.Point);
                }
            }
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0) && TryGetPointUnderPointer(out ExplorationPoint point))
            {
                Trigger(point);
            }
        }

        /// <summary>鼠标是否点在某个「当前可探索」的交互点立绘范围内。</summary>
        private bool TryGetPointUnderPointer(out ExplorationPoint point)
        {
            point = null;

            if (!player.ControlEnabled || player.IsMoving)
            {
                return false;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return false;
            }

            Camera worldCamera = Camera.main;
            if (worldCamera == null)
            {
                return false;
            }

            Vector3 world = worldCamera.ScreenToWorldPoint(Input.mousePosition);

            // 后创建的在上层，优先命中
            for (int index = runtimePoints.Count - 1; index >= 0; index--)
            {
                PointRuntime entry = runtimePoints[index];
                if (entry.Renderer == null || entry.Collected)
                {
                    continue;
                }

                GetDisplayRect(entry.Point, out Vector3 center, out Vector3 size);
                if (Mathf.Abs(world.x - center.x) > size.x * 0.5f ||
                    Mathf.Abs(world.y - center.y) > size.y * 0.5f)
                {
                    continue;
                }

                point = entry.Point;
                return true;
            }

            return false;
        }

        private void Trigger(ExplorationPoint point)
        {
            string pointLabel = $"位置 ({point.position.x:0.##}, {point.position.y:0.##})";

            switch (point.type)
            {
                case ExplorationPointType.Item:
                    if (inventory == null)
                    {
                        Debug.LogWarning($"[Exploration] {pointLabel} 没有可用的背包，无法获得物品 {point.itemId}。", this);
                        break;
                    }

                    bool added = inventory.Add(point.itemId, point.icon, point.displayName, point.hintText);
                    if (added && hintPanel != null && !string.IsNullOrWhiteSpace(point.hintText))
                    {
                        hintPanel.Show(point.hintText);
                    }

                    // 背包变化事件里会统一隐藏；这里再兜底一次
                    if (inventory.Contains(point.itemId))
                    {
                        HidePoint(point);
                    }
                    else
                    {
                        Debug.Log($"[Exploration] 物品 {point.itemId} 未加入背包（ID 不合法）。");
                    }

                    break;

                case ExplorationPointType.Story:
                    if (string.IsNullOrEmpty(point.storyId))
                    {
                        Debug.LogWarning($"[Exploration] {pointLabel} 的剧情点没有填 storyId。", this);
                        break;
                    }

                    StoryTriggered?.Invoke(point.storyId);
                    break;
            }

            PointTriggered?.Invoke(point);
        }

        /// <summary>物品被拿走后隐藏立绘与点击区域，之后不再响应点击。</summary>
        private void HidePoint(ExplorationPoint point)
        {
            for (int index = 0; index < runtimePoints.Count; index++)
            {
                PointRuntime entry = runtimePoints[index];
                if (entry.Point != point)
                {
                    continue;
                }

                if (entry.Collected)
                {
                    return;
                }

                entry.Collected = true;
                if (entry.Renderer != null)
                {
                    entry.Renderer.gameObject.SetActive(false);
                }

                Debug.Log(
                    $"[Exploration] 物品 {point.itemId} 已入手，隐藏交互点 ({point.position.x:0.##}, {point.position.y:0.##})。",
                    this);

                return;
            }
        }

        /// <summary>
        /// 给每个可探索点创建常驻表现：配置了 visual 就显示立绘；
        /// 没配置就用占位高亮框（填了 highlightSprite 用它，否则用运行时生成的白色方块并拉伸到交互区域大小）。
        /// </summary>
        private void CreatePointVisuals()
        {
            runtimePoints.Clear();

            GameObject rootObject = new GameObject("PointVisuals");
            rootObject.transform.SetParent(transform, false);
            Transform root = rootObject.transform;

            for (int index = 0; index < points.Count; index++)
            {
                ExplorationPoint point = points[index];
                if (point == null)
                {
                    continue;
                }

                SpriteRenderer renderer = point.visualObject != null
                    ? point.visualObject.GetComponent<SpriteRenderer>()
                    : null;

                if (renderer != null)
                {
                    // 场景里已经摆好的立绘：保持用户的大小 / 位置 / Sorting Order，不生成副本
                    runtimePoints.Add(new PointRuntime { Point = point, Renderer = renderer, Collected = false });
                    continue;
                }

                if (point.visualObject != null)
                {
                    Debug.LogWarning(
                        $"[Exploration] 交互点 {index} 的 visualObject 没有 SpriteRenderer，已退回自动生成占位。",
                        point.visualObject);
                }

                GameObject visualObject = new GameObject($"Point_{index}");
                visualObject.transform.SetParent(root, false);
                visualObject.transform.position = new Vector3(point.position.x, point.position.y, 0f);

                renderer = visualObject.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = highlightSortingOrder;
                float scale = Mathf.Max(0.01f, point.visualScale);

                if (point.visual != null)
                {
                    renderer.sprite = point.visual;
                    renderer.color = Color.white;
                    visualObject.transform.localScale = Vector3.one * scale;
                }
                else
                {
                    // 没配立绘：保留一个透明点击区域（看不见，但仍可点击）
                    renderer.sprite = CreateUnitSprite();
                    renderer.color = new Color(1f, 1f, 1f, 0f);
                    visualObject.transform.localScale = new Vector3(map.CellSpacing * scale, rowHeight * scale, 1f);
                }

                runtimePoints.Add(new PointRuntime { Point = point, Renderer = renderer, Collected = false });
            }
        }

        private static Sprite CreateUnitSprite()
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        private void OnDrawGizmos()
        {
            if (points == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 1f);
            for (int index = 0; index < points.Count; index++)
            {
                ExplorationPoint point = points[index];
                if (point == null)
                {
                    continue;
                }

                GetDisplayRect(point, out Vector3 center, out Vector3 size);
                Gizmos.DrawWireCube(center, size);
                Gizmos.DrawSphere(new Vector3(point.position.x, point.position.y, 0f), 0.05f);
            }
        }

        /// <summary>
        /// 取得交互点的点击矩形（世界坐标中心 + 尺寸），编辑模式预览和运行时点击判定使用同一套规则：
        /// 每个轴的优先级都是「自定义 rectSize > 立绘 bounds（无立绘则格宽 / rowHeight）× visualScale」。
        /// </summary>
        public void GetDisplayRect(ExplorationPoint point, out Vector3 center, out Vector3 size)
        {
            float scale = Mathf.Max(0.01f, point.visualScale);
            center = new Vector3(point.position.x, point.position.y, 0f);

            bool hasCustomWidth = point.rectSize.x > 0f;
            bool hasCustomHeight = point.rectSize.y > 0f;

            // 场景里已经摆好的立绘：直接按 SpriteRenderer 的世界包围盒算点击范围
            if (point.visualObject != null)
            {
                SpriteRenderer sceneRenderer = point.visualObject.GetComponent<SpriteRenderer>();
                if (sceneRenderer != null && sceneRenderer.sprite != null)
                {
                    Bounds bounds = sceneRenderer.bounds;
                    center = bounds.center;
                    size = new Vector3(
                        hasCustomWidth ? point.rectSize.x : bounds.size.x,
                        hasCustomHeight ? point.rectSize.y : bounds.size.y,
                        0f);
                    return;
                }
            }

            Sprite sprite = point.visual;
            if (sprite != null)
            {
                Bounds spriteBounds = sprite.bounds;
                center += spriteBounds.center * scale;
                size = new Vector3(
                    hasCustomWidth ? point.rectSize.x : spriteBounds.size.x * scale,
                    hasCustomHeight ? point.rectSize.y : spriteBounds.size.y * scale,
                    0f);
                return;
            }

            float width = map != null ? map.CellSpacing : 1f;
            size = new Vector3(
                hasCustomWidth ? point.rectSize.x : width * scale,
                hasCustomHeight ? point.rectSize.y : rowHeight * scale,
                0f);
        }
    }
}
