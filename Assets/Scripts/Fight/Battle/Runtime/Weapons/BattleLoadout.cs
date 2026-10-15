using System.Collections.Generic;

namespace Spotlight.Battle
{
    public sealed class BattleLoadout
    {
        public const int MaximumWeapons = 3;
        private readonly List<WeaponRuntime> weapons = new List<WeaponRuntime>(MaximumWeapons);

        public IReadOnlyList<WeaponRuntime> Weapons => weapons;

        public bool TryAdd(WeaponDefinition definition)
        {
            if (definition == null || weapons.Count >= MaximumWeapons)
            {
                return false;
            }

            weapons.Add(new WeaponRuntime(definition));
            return true;
        }

        public void Clear()
        {
            weapons.Clear();
        }

        public WeaponDefinition RemoveAt(int index)
        {
            if (index < 0 || index >= weapons.Count)
            {
                return null;
            }

            WeaponDefinition removed = weapons[index].Definition;
            weapons.RemoveAt(index);
            return removed;
        }

        public void Tick(float deltaTime)
        {
            foreach (WeaponRuntime weapon in weapons)
            {
                weapon.Tick(deltaTime);
            }
        }
    }
}
