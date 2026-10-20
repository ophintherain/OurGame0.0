using System.Collections;
using Spotlight.Shared;
using UnityEngine;

namespace Spotlight.Battle
{
    // Routes finished production battles after the optional discard choice.
    public sealed class BattleRunBridge : MonoBehaviour
    {
        private BattleCoordinator battle;
        private BossController boss;
        private bool loading;

        public void Initialize(BattleCoordinator coordinator, BossController controller)
        {
            battle = coordinator;
            boss = controller;
            battle.BattleFinished += OnFinished;
        }

        private void OnDestroy()
        {
            if (battle != null) battle.BattleFinished -= OnFinished;
        }

        private void OnFinished(BattleResult result)
        {
            if (loading) return;
            if (result.Outcome == BattleOutcome.PlayerDead) return; // Loadout reopens in this scene.
            if (result.Outcome == BattleOutcome.Escaped)
                StartCoroutine(LoadAfterResult(ExploreScene(boss.CarriageIndex)));
            else if (result.Outcome == BattleOutcome.BossDefeated)
                StartCoroutine(LoadAfterResult(boss.CarriageIndex == 3 ? "ending" :
                    ExploreScene(boss.CarriageIndex + 1)));
        }

        private IEnumerator LoadAfterResult(string scene)
        {
            loading = true;
            yield return null;
            while (battle != null && battle.HasPendingWeaponDiscard) yield return null;
            if (battle == null) yield break;
            if (scene == "ending") RunProgress.SetCarriage(3);
            else if (scene == "second") RunProgress.SetCarriage(2);
            else if (scene == "third") RunProgress.SetCarriage(3);
            Spotlight.Shared.SpotlightSceneTransition.TryLoad(scene);
        }

        private static string ExploreScene(int carriage) => carriage == 1 ? "first" :
            carriage == 2 ? "second" : "third";
    }
}
