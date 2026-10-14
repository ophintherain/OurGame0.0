using UnityEngine;

namespace Spotlight.Battle
{
    public sealed class BattleTelegraphView : MonoBehaviour
    {
        [SerializeField] private BossController boss;
        [SerializeField] private BattleArena arena;
        [SerializeField] private GameObject primaryIndicator;
        [SerializeField] private GameObject secondaryIndicator;

        private bool subscribed;

        public void Configure(BossController battleBoss, BattleArena battleArena,
            GameObject primary, GameObject secondary)
        {
            Unsubscribe();
            boss = battleBoss;
            arena = battleArena;
            primaryIndicator = primary;
            secondaryIndicator = secondary;
            Subscribe();
            Hide();
        }

        private void OnEnable()
        {
            Subscribe();
            Hide();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed || boss == null)
            {
                return;
            }

            boss.TelegraphStarted += Show;
            boss.AttackResolved += HandleResolved;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || boss == null)
            {
                return;
            }

            boss.TelegraphStarted -= Show;
            boss.AttackResolved -= HandleResolved;
            subscribed = false;
        }

        private void Show(BattleTelegraph telegraph)
        {
            if (arena == null || primaryIndicator == null)
            {
                return;
            }

            primaryIndicator.transform.position = arena.GetWorldPosition(telegraph.Primary);
            primaryIndicator.SetActive(true);
            if (secondaryIndicator != null)
            {
                secondaryIndicator.SetActive(telegraph.Secondary.HasValue);
                if (telegraph.Secondary.HasValue)
                {
                    secondaryIndicator.transform.position = arena.GetWorldPosition(telegraph.Secondary.Value);
                }
            }
        }

        private void HandleResolved(BattleTelegraph telegraph, DamageResolution resolution)
        {
            Hide();
        }

        private void Hide()
        {
            primaryIndicator?.SetActive(false);
            secondaryIndicator?.SetActive(false);
        }
    }
}
