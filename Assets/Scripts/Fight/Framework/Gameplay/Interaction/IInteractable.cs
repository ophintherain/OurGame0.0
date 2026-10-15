using UnityEngine;

namespace Spotlight
{
    public interface IInteractable
    {
        bool CanInteract(PlayerContext context);
        InteractionResult Interact(PlayerContext context);
        Transform GetMarkerAnchor();
    }
}
