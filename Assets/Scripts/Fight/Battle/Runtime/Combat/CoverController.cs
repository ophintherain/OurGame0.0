using System;
using UnityEngine;

namespace Spotlight.Battle
{
    public sealed class CoverController : MonoBehaviour
    {
        [SerializeField] private BattleLane lane = BattleLane.Foreground;
        [SerializeField] private int column = 3;
        [SerializeField, Min(0)] private int maximumBlockCount = 1;
        [SerializeField] private Sprite brokenMask;
        [SerializeField] private Vector2 maskCanvasCenter = new Vector2(0f, -1.4f);
        [SerializeField] private Vector2 maskCanvasSize = new Vector2(17.78f, 10f);

        private SpriteRenderer maskRenderer;

        public event Action Broken;

        public BattlePosition Position => new BattlePosition(lane, column);
        public int RemainingBlockCount { get; private set; }
        public bool IsBroken => RemainingBlockCount <= 0;

        public void Configure(BattlePosition position, int blockCount = 1)
        {
            lane = position.Lane;
            column = position.Column;
            maximumBlockCount = Mathf.Max(0, blockCount);
            ResetCover();
        }

        public void ResetCover()
        {
            RemainingBlockCount = Mathf.Max(0, maximumBlockCount);
            UpdateVisual();
        }

        public void ConfigureBrokenMask(Sprite mask)
        {
            brokenMask = mask;
            UpdateVisual();
        }

        public bool TryBlock(BattlePosition targetPosition, PlayerPosture posture, DamageKind damageKind)
        {
            if (IsBroken || Position != targetPosition || posture != PlayerPosture.Prone ||
                damageKind == DamageKind.Melee)
            {
                return false;
            }

            RemainingBlockCount--;
            UpdateVisual();
            if (IsBroken)
            {
                Broken?.Invoke();
            }

            return true;
        }

        private void Awake()
        {
            ResetCover();
        }

        private void UpdateVisual()
        {
            if (brokenMask == null)
            {
                if (maskRenderer != null) maskRenderer.enabled = false;
                return;
            }

            if (!IsBroken && maskRenderer == null) return;
            if (maskRenderer == null)
            {
                GameObject maskObject = new GameObject("BrokenTableMask");
                maskObject.transform.SetParent(transform.parent, false);
                maskRenderer = maskObject.AddComponent<SpriteRenderer>();
                maskRenderer.sortingOrder = 21;
            }

            maskRenderer.sprite = brokenMask;
            maskRenderer.transform.localPosition = maskCanvasCenter;
            Vector2 spriteSize = brokenMask.rect.size / brokenMask.pixelsPerUnit;
            maskRenderer.transform.localScale = new Vector3(
                maskCanvasSize.x / spriteSize.x, maskCanvasSize.y / spriteSize.y, 1f);
            maskRenderer.enabled = IsBroken;
        }
    }
}
