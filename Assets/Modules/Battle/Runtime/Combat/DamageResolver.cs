namespace Spotlight.Battle
{
    public static class DamageResolver
    {
        public static DamageResolution Resolve(CombatHealth target, float damage, DamageKind damageKind,
            BattlePosition targetPosition, PlayerPosture targetPosture, CoverController cover = null)
        {
            if (target == null || target.IsDead || damage <= 0f)
            {
                return new DamageResolution(0f, false, target != null && target.IsDead);
            }

            if (cover != null && cover.TryBlock(targetPosition, targetPosture, damageKind))
            {
                return new DamageResolution(0f, true, false);
            }

            float applied = target.ApplyDamage(damage);
            return new DamageResolution(applied, false, target.IsDead);
        }
    }
}
