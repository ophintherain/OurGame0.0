using System;

namespace Spotlight.Battle
{
    [Serializable]
    public readonly struct BattleResult
    {
        public BattleResult(BattleOutcome outcome, float playerHealth, float bossHealth,
            bool requiresWeaponDiscard = false, string discardedWeaponId = null)
        {
            Outcome = outcome;
            PlayerHealth = playerHealth;
            BossHealth = bossHealth;
            RequiresWeaponDiscard = requiresWeaponDiscard;
            DiscardedWeaponId = discardedWeaponId;
        }

        public BattleOutcome Outcome { get; }
        public float PlayerHealth { get; }
        public float BossHealth { get; }
        public bool RequiresWeaponDiscard { get; }
        public string DiscardedWeaponId { get; }
    }
}
