namespace Spotlight.Battle
{
    public readonly struct DamageResolution
    {
        public DamageResolution(float appliedDamage, bool blocked, bool targetDied)
        {
            AppliedDamage = appliedDamage;
            Blocked = blocked;
            TargetDied = targetDied;
        }

        public float AppliedDamage { get; }
        public bool Blocked { get; }
        public bool TargetDied { get; }
    }
}
