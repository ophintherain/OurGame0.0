using UnityEngine;

namespace Spotlight.Battle
{
    [CreateAssetMenu(menuName = "Spotlight/Battle/Weapon Definition", fileName = "WeaponDefinition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField] private string id = "prototype_weapon";
        [SerializeField] private string displayName = "Prototype Weapon";
        [SerializeField, Min(0f)] private float damage = 120f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 1.25f;
        [SerializeField, Min(0)] private int rangeColumns = 4;
        [SerializeField] private DamageKind damageKind = DamageKind.Ranged;

        public string Id => id;
        public string DisplayName => displayName;
        public float Damage => damage;
        public float CooldownSeconds => cooldownSeconds;
        public int RangeColumns => rangeColumns;
        public DamageKind DamageKind => damageKind;

        public void Configure(string weaponId, string weaponName, float weaponDamage,
            float cooldown, int range, DamageKind kind)
        {
            id = string.IsNullOrWhiteSpace(weaponId) ? "prototype_weapon" : weaponId;
            displayName = string.IsNullOrWhiteSpace(weaponName) ? id : weaponName;
            damage = Mathf.Max(0f, weaponDamage);
            cooldownSeconds = Mathf.Max(0f, cooldown);
            rangeColumns = Mathf.Max(0, range);
            damageKind = kind;
        }
    }
}
