using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Shared
{
    // In-memory progress for one run. A new game clears it; scene changes do not.
    public static class RunProgress
    {
        private static readonly HashSet<int> weapons = new HashSet<int>();
        private static readonly Dictionary<int, Sprite> icons = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, string> names = new Dictionary<int, string>();
        private static readonly HashSet<int> completedExplorations = new HashSet<int>();
        public static bool Active { get; private set; }
        public static int Carriage { get; private set; } = 1;
        public static bool HasWeapon(int id) => weapons.Contains(id);
        public static bool ExplorationCompleted(int carriage) => completedExplorations.Contains(carriage);
        public static void Begin()
        {
            weapons.Clear();
            icons.Clear();
            names.Clear();
            completedExplorations.Clear();
            Carriage = 1;
            Active = true;
        }
        public static void AddWeapon(int id, Sprite icon = null, string displayName = null)
        {
            if (id < 1000 || id >= 2000) return;
            weapons.Add(id);
            if (icon != null) icons[id] = icon;
            if (!string.IsNullOrEmpty(displayName)) names[id] = displayName;
        }
        public static void RemoveWeapon(int id)
        {
            weapons.Remove(id);
            icons.Remove(id);
            names.Remove(id);
        }
        public static int[] WeaponIds()
        {
            var result = new int[weapons.Count];
            weapons.CopyTo(result);
            return result;
        }
        public static Sprite Icon(int id) => icons.TryGetValue(id, out Sprite icon) ? icon : null;
        public static string Name(int id) => names.TryGetValue(id, out string name) ? name : null;
        public static void CompleteExploration(int carriage) => completedExplorations.Add(carriage);
        public static void SetCarriage(int carriage) => Carriage = carriage;
    }
}
