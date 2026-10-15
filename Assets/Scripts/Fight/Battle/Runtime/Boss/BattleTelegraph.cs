using System;

namespace Spotlight.Battle
{
    public readonly struct BattleTelegraph
    {
        public BattleTelegraph(BattlePosition primary, BattlePosition? secondary, float duration,
            DamageKind damageKind)
        {
            Primary = primary;
            Secondary = secondary;
            Duration = duration;
            DamageKind = damageKind;
        }

        public BattlePosition Primary { get; }
        public BattlePosition? Secondary { get; }
        public float Duration { get; }
        public DamageKind DamageKind { get; }

        public bool Contains(BattlePosition position)
        {
            return Primary == position || (Secondary.HasValue && Secondary.Value == position);
        }
    }
}
