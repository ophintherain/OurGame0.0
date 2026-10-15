using System;
using System.Collections;
using UnityEngine;

namespace Spotlight.Battle
{
    public sealed class BattleCoordinator : MonoBehaviour
    {
        [SerializeField] private GameplayCoordinator gameplayCoordinator;
        [SerializeField] private InputRouter inputRouter;
        [SerializeField] private PlayerMovement explorationMovement;
        [SerializeField] private PlayerBattleController player;
        [SerializeField] private BossController boss;
        [SerializeField] private WeaponController weapons;
        [SerializeField] private BattleArena arena;
        [SerializeField] private CoverController[] covers;
        [SerializeField] private GameObject battleVisualRoot;
        [SerializeField] private BattleCameraRig cameraRig;
        [SerializeField] private bool autoStartOnScene;

        private bool subscribed;

        public event Action BattleStarted;
        public event Action<BattleResult> BattleFinished;

        public bool IsBattleActive { get; private set; }
        public BattleResult LastResult { get; private set; }
        public bool HasPendingWeaponDiscard => LastResult.RequiresWeaponDiscard;

        public void Configure(GameplayCoordinator phases, InputRouter router, PlayerMovement exploreMovement,
            PlayerBattleController battlePlayer, BossController battleBoss, WeaponController weaponController,
            BattleArena battleArena, CoverController[] battleCovers, GameObject visualRoot)
        {
            Configure(phases, router, exploreMovement, battlePlayer, battleBoss, weaponController,
                battleArena, battleCovers, visualRoot, null);
        }

        public void Configure(GameplayCoordinator phases, InputRouter router, PlayerMovement exploreMovement,
            PlayerBattleController battlePlayer, BossController battleBoss, WeaponController weaponController,
            BattleArena battleArena, CoverController[] battleCovers, GameObject visualRoot,
            BattleCameraRig battleCamera)
        {
            Unsubscribe();
            gameplayCoordinator = phases;
            inputRouter = router;
            explorationMovement = exploreMovement;
            player = battlePlayer;
            boss = battleBoss;
            weapons = weaponController;
            arena = battleArena;
            covers = battleCovers ?? Array.Empty<CoverController>();
            battleVisualRoot = visualRoot;
            cameraRig = battleCamera;
            Subscribe();
        }

        public bool TryBeginBattle()
        {
            if (IsBattleActive || gameplayCoordinator == null || player == null || boss == null || arena == null)
            {
                return false;
            }

            if (gameplayCoordinator.CurrentPhase == GameplayPhase.Exploring &&
                !gameplayCoordinator.TryChangePhase(GameplayPhase.AnomalyUnlocked))
            {
                return false;
            }

            if (gameplayCoordinator.CurrentPhase == GameplayPhase.AnomalyUnlocked &&
                !gameplayCoordinator.TryChangePhase(GameplayPhase.EnteringBattle))
            {
                return false;
            }

            if (gameplayCoordinator.CurrentPhase != GameplayPhase.EnteringBattle)
            {
                return false;
            }

            inputRouter?.SetWorldInputEnabled(false);
            if (explorationMovement != null)
            {
                explorationMovement.enabled = false;
            }

            battleVisualRoot?.SetActive(true);
            cameraRig?.SetBattleView(true);
            foreach (CoverController cover in covers)
            {
                cover?.ResetCover();
            }

            player.Health?.ResetToFull();
            player.BeginBattle();
            boss.BeginBattle();
            weapons?.BeginBattle();
            LastResult = default;
            IsBattleActive = true;

            if (!gameplayCoordinator.TryChangePhase(GameplayPhase.Battling))
            {
                IsBattleActive = false;
                return false;
            }

            BattleStarted?.Invoke();
            return true;
        }

        public bool TryStartOrRestart()
        {
            if (IsBattleActive) return false;
            if (gameplayCoordinator == null) return false;

            if (LastResult.RequiresWeaponDiscard)
            {
                ResolveWeaponDiscard(0);
            }

            if (gameplayCoordinator.CurrentPhase == GameplayPhase.BattleResult ||
                gameplayCoordinator.CurrentPhase == GameplayPhase.PlayerDead)
            {
                if (!gameplayCoordinator.TryChangePhase(GameplayPhase.Transitioning) ||
                    !gameplayCoordinator.TryChangePhase(GameplayPhase.Initializing) ||
                    !gameplayCoordinator.TryChangePhase(GameplayPhase.Exploring))
                {
                    return false;
                }
            }

            return TryBeginBattle();
        }

        public bool ResolveWeaponDiscard(int slot)
        {
            if (!LastResult.RequiresWeaponDiscard || weapons == null)
            {
                return false;
            }

            WeaponDefinition removed = weapons.RemoveWeaponAt(slot);
            if (removed == null)
            {
                return false;
            }

            LastResult = new BattleResult(LastResult.Outcome, LastResult.PlayerHealth,
                LastResult.BossHealth, false, removed.Id);
            return true;
        }

        public bool TryEscape()
        {
            if (!IsBattleActive || !arena.IsEscapePosition(player.Position))
            {
                return false;
            }

            Finish(BattleOutcome.Escaped);
            return true;
        }

        public bool TryToggleOverviewZoom()
        {
            return IsBattleActive && cameraRig != null && cameraRig.ToggleOverviewZoom();
        }

        private IEnumerator Start()
        {
            if (!autoStartOnScene)
            {
                yield break;
            }

            // GameplayCoordinator enters Exploring in Start; begin the battle on the next frame.
            yield return null;
            TryBeginBattle();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed || player == null || boss == null || player.Health == null || boss.Health == null)
            {
                return;
            }

            player.Health.Died += HandlePlayerDied;
            boss.Health.Died += HandleBossDied;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (player != null && player.Health != null)
            {
                player.Health.Died -= HandlePlayerDied;
            }
            if (boss != null && boss.Health != null)
            {
                boss.Health.Died -= HandleBossDied;
            }
            subscribed = false;
        }

        private void HandlePlayerDied()
        {
            Finish(BattleOutcome.PlayerDead);
        }

        private void HandleBossDied()
        {
            Finish(BattleOutcome.BossDefeated);
        }

        private void Finish(BattleOutcome outcome)
        {
            if (!IsBattleActive)
            {
                return;
            }

            IsBattleActive = false;
            player.EndBattle();
            boss.EndBattle();
            float playerHealth = player.Health != null ? player.Health.CurrentHealth : 0f;
            bool needsDiscard = outcome == BattleOutcome.BossDefeated && player.Health != null &&
                                player.Health.Normalized < 0.3f && weapons != null &&
                                weapons.Loadout.Weapons.Count > 0;
            LastResult = new BattleResult(outcome, playerHealth,
                boss.Health != null ? boss.Health.CurrentHealth : 0f, needsDiscard);

            if (needsDiscard && weapons.Loadout.Weapons.Count == 1)
            {
                ResolveWeaponDiscard(0);
            }

            if (outcome == BattleOutcome.PlayerDead)
            {
                gameplayCoordinator.TryChangePhase(GameplayPhase.PlayerDead);
            }
            else
            {
                gameplayCoordinator.TryChangePhase(GameplayPhase.BattleResult);
            }

            BattleFinished?.Invoke(LastResult);
        }
    }
}
