using UnityEngine;

namespace Spotlight.Battle
{
    public sealed class BattleCommandController : MonoBehaviour, IGameplayInputSink
    {
        [SerializeField] private BattleCoordinator battle;
        [SerializeField] private PlayerBattleController player;
        [SerializeField] private WeaponController weapons;

        public bool CanHandleInput => battle != null;

        public void Configure(BattleCoordinator coordinator, PlayerBattleController battlePlayer,
            WeaponController weaponController)
        {
            battle = coordinator;
            player = battlePlayer;
            weapons = weaponController;
        }

        public bool TryHandle(GameplayInputCommand command)
        {
            if (battle == null) return false;
            if (!battle.IsBattleActive)
            {
                return command == GameplayInputCommand.StartOrRestart && battle.TryStartOrRestart();
            }

            return command switch
            {
                GameplayInputCommand.MoveLeft => player.TryMoveHorizontal(-1),
                GameplayInputCommand.MoveRight => player.TryMoveHorizontal(1),
                GameplayInputCommand.LaneFront => player.TryChangeLane(-1),
                GameplayInputCommand.LaneBack => player.TryChangeLane(1),
                GameplayInputCommand.Stand => player.TrySetPosture(PlayerPosture.Standing),
                GameplayInputCommand.Prone => player.TryTogglePosture(PlayerPosture.Prone),
                GameplayInputCommand.OnTable => player.TryTogglePosture(PlayerPosture.OnTable),
                GameplayInputCommand.UseWeapon1 => weapons.TryUse(0),
                GameplayInputCommand.UseWeapon2 => weapons.TryUse(1),
                GameplayInputCommand.UseWeapon3 => weapons.TryUse(2),
                GameplayInputCommand.Escape => battle.TryEscape(),
                GameplayInputCommand.ToggleOverviewZoom => battle.TryToggleOverviewZoom(),
                _ => false
            };
        }
    }
}
