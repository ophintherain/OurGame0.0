using System;
using UnityEngine;

namespace Spotlight.Battle
{
    public sealed class CoverController : MonoBehaviour
    {
        [SerializeField] private BattleLane lane = BattleLane.Foreground;
        [SerializeField] private int column = 3;
        [SerializeField, Min(0)] private int maximumBlockCount = 1;

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
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = IsBroken
                    ? new Color(0.25f, 0.25f, 0.25f, 0.7f)
                    : new Color(0.55f, 0.34f, 0.18f, 1f);
            }
        }
    }
}
