using UnityEngine;

namespace Spotlight
{
    public sealed class InteractionProbe : MonoBehaviour, IInteractable
    {
        [SerializeField] private int interactionCount;

        public int InteractionCount => interactionCount;

        public bool CanInteract(PlayerContext context)
        {
            return true;
        }

        public InteractionResult Interact(PlayerContext context)
        {
            interactionCount++;
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = interactionCount % 2 == 0
                    ? new Color(0.2f, 0.85f, 0.45f, 1f)
                    : new Color(1f, 0.8f, 0.15f, 1f);
            }

            return InteractionResult.Succeeded;
        }

        public Transform GetMarkerAnchor()
        {
            return transform;
        }
    }
}
