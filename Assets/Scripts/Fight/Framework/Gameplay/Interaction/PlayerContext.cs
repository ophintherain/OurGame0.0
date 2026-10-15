using UnityEngine;

namespace Spotlight
{
    public readonly struct PlayerContext
    {
        public PlayerContext(Transform player, GameplayPhase phase)
        {
            Player = player;
            Phase = phase;
        }

        public Transform Player { get; }
        public GameplayPhase Phase { get; }
    }
}
