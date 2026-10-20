using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Spotlight.Shared;

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
        private bool fightSceneLoadout;

        public event Action BattleStarted;
        public event Action<BattleResult> BattleFinished;

        public bool IsBattleActive { get; private set; }
        public bool IsLoadoutSelectionOpen { get; private set; }
        public bool IsRetryPromptOpen { get; private set; }
        public BattleResult LastResult { get; private set; }
        public bool HasPendingWeaponDiscard => LastResult.RequiresWeaponDiscard;
        public System.Collections.Generic.IReadOnlyList<CoverController> Covers => covers ?? Array.Empty<CoverController>();
        public BattleArena Arena => arena;

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
            if (IsBattleActive || IsLoadoutSelectionOpen || gameplayCoordinator == null ||
                player == null || boss == null || arena == null)
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
            boss.SetWeaponController(weapons);
            weapons?.ConfigureCarriage(boss.CarriageIndex);
            boss.BeginBattle();
            weapons?.BeginBattle();
            LastResult = default;
            IsRetryPromptOpen = false;
            IsBattleActive = true;
            BattleHitFeedback feedback = GetComponent<BattleHitFeedback>();
            if (feedback == null) feedback = gameObject.AddComponent<BattleHitFeedback>();
            feedback.Configure(this, player, boss, weapons, cameraRig);

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
            if (IsBattleActive || IsLoadoutSelectionOpen || IsRetryPromptOpen) return false;
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

        public bool TryConfirmLoadoutAndStart()
        {
            if (!IsLoadoutSelectionOpen || weapons == null ||
                weapons.SelectedCount == 0 || LastResult.RequiresWeaponDiscard)
                return false;

            IsLoadoutSelectionOpen = false;
            if (TryStartOrRestart()) return true;
            IsLoadoutSelectionOpen = true;
            return false;
        }

        public bool TryConfirmRetry()
        {
            if (!fightSceneLoadout || !IsRetryPromptOpen || IsBattleActive ||
                gameplayCoordinator.CurrentPhase != GameplayPhase.PlayerDead) return false;
            IsRetryPromptOpen = false;
            weapons?.PrepareSelection();
            IsLoadoutSelectionOpen = true;
            return true;
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
            int itemId = removed.Id switch
            {
                "truce" => 1001, "barrage" => 1002, "leap" => 1003,
                "mimic" => 1004, "poet" => 1005, "needle" => 1006,
                "servitude" => 1007, "edge" => 1008, "recorder" => 1009,
                _ => 0
            };
            if (itemId != 0) RunProgress.RemoveWeapon(itemId);
            return true;
        }

        public bool TryEscape()
        {
            if (!IsBattleActive || !player.CanAct || !arena.IsEscapePosition(player.Position))
            {
                return false;
            }

            Finish(BattleOutcome.Escaped);
            return true;
        }

        public bool TryReturnToLoadoutForDebug()
        {
            if (!IsBattleActive) return false;
            Finish(BattleOutcome.Escaped);
            return true;
        }

        public bool TryToggleOverviewZoom()
        {
            return IsBattleActive && cameraRig != null && cameraRig.ToggleOverviewZoom();
        }

        private IEnumerator Start()
        {
            fightSceneLoadout = SceneManager.GetActiveScene().path.StartsWith("Assets/Scenes/fight/");
            if (fightSceneLoadout)
            {
                inputRouter?.SetWorldInputEnabled(false);
                if (explorationMovement != null) explorationMovement.enabled = false;
                battleVisualRoot?.SetActive(true);
                cameraRig?.SetBattleView(true);
                player?.BeginBattle();
                player?.EndBattle();
                weapons?.ConfigureCarriage(boss.CarriageIndex);
                if (SceneManager.GetActiveScene().name != "fight_test")
                {
                    weapons?.ApplyRunInventory();
                    gameObject.AddComponent<BattleRunBridge>().Initialize(this, boss);
                }
                weapons?.PrepareSelection();
                IsLoadoutSelectionOpen = true;
                BattleLoadoutScreen screen = GetComponent<BattleLoadoutScreen>();
                if (screen == null) screen = gameObject.AddComponent<BattleLoadoutScreen>();
                screen.Configure(this, weapons, boss, GetComponent<BattleStatusHUD>());
                yield break;
            }

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
            weapons?.EndBattle();
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
            if (fightSceneLoadout)
            {
                if (outcome == BattleOutcome.PlayerDead)
                {
                    IsLoadoutSelectionOpen = false;
                    IsRetryPromptOpen = true;
                }
                else
                {
                    weapons?.PrepareSelection();
                    IsLoadoutSelectionOpen = true;
                }
            }
        }
    }
}
