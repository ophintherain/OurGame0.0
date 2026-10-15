using UnityEngine;

namespace Spotlight
{
    public sealed class InteractionController : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private GameplayCoordinator gameplayCoordinator;

        public void Configure(Transform playerTransform, GameplayCoordinator coordinator)
        {
            player = playerTransform;
            gameplayCoordinator = coordinator;
        }

        public InteractionResult TryInteract(Collider2D target)
        {
            if (target == null)
            {
                return InteractionResult.NoTarget;
            }

            IInteractable interactable = FindInteractable(target);
            if (interactable == null)
            {
                return InteractionResult.NoTarget;
            }

            GameplayPhase phase = gameplayCoordinator != null
                ? gameplayCoordinator.CurrentPhase
                : GameplayPhase.Initializing;
            PlayerContext context = new PlayerContext(player, phase);

            if (!interactable.CanInteract(context))
            {
                return InteractionResult.Rejected;
            }

            return interactable.Interact(context);
        }

        private static IInteractable FindInteractable(Collider2D target)
        {
            MonoBehaviour[] behaviours = target.GetComponentsInParent<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IInteractable interactable)
                {
                    return interactable;
                }
            }

            return null;
        }
    }
}
