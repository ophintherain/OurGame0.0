using System;
using UnityEngine;

namespace Spotlight
{
    public sealed class GameplayCoordinator : MonoBehaviour
    {
        [SerializeField] private bool enterExploringOnStart = true;

        public event Action<GameplayPhase, GameplayPhase> PhaseChanged;

        public GameplayPhase CurrentPhase { get; private set; } = GameplayPhase.Initializing;

        public void Configure(bool shouldEnterExploringOnStart)
        {
            enterExploringOnStart = shouldEnterExploringOnStart;
        }

        private void Start()
        {
            if (enterExploringOnStart)
            {
                TryChangePhase(GameplayPhase.Exploring);
            }
        }

        public bool TryChangePhase(GameplayPhase next)
        {
            if (next == CurrentPhase)
            {
                return false;
            }

            if (!IsAllowedTransition(CurrentPhase, next))
            {
                Debug.LogWarning($"[Gameplay] Illegal phase transition: {CurrentPhase} -> {next}.", this);
                return false;
            }

            GameplayPhase previous = CurrentPhase;
            CurrentPhase = next;
            PhaseChanged?.Invoke(previous, next);
            return true;
        }

        private static bool IsAllowedTransition(GameplayPhase current, GameplayPhase next)
        {
            if (next == GameplayPhase.PlayerDead && current != GameplayPhase.PlayerDead)
            {
                return true;
            }

            return current switch
            {
                GameplayPhase.Initializing => next == GameplayPhase.Exploring,
                GameplayPhase.Exploring => next == GameplayPhase.AnomalyUnlocked,
                GameplayPhase.AnomalyUnlocked => next == GameplayPhase.EnteringBattle,
                GameplayPhase.EnteringBattle => next == GameplayPhase.Battling,
                GameplayPhase.Battling => next == GameplayPhase.BattleResult,
                GameplayPhase.BattleResult => next == GameplayPhase.Transitioning,
                GameplayPhase.Transitioning => next == GameplayPhase.Initializing,
                GameplayPhase.PlayerDead => next == GameplayPhase.Transitioning,
                _ => false
            };
        }
    }
}
