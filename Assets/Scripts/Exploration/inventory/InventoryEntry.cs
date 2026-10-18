using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 背包里的一个条目（运行时数据）。每件武器 / 物品唯一，因此不记录数量。
    /// 只存 ID 与显示用的图标 / 名称，不引用场景物体。
    /// </summary>
    public sealed class InventoryEntry
    {
        internal InventoryEntry(int id, InventoryItemKind kind, Sprite icon, string displayName)
        {
            Id = id;
            Kind = kind;
            Icon = icon;
            DisplayName = displayName;
        }

        /// <summary>4 位物品 ID（1 开头=武器，2 开头=物品）。</summary>
        public int Id { get; }

        /// <summary>类别，由 ID 前缀决定。</summary>
        public InventoryItemKind Kind { get; }

        /// <summary>背包图标。</summary>
        public Sprite Icon { get; internal set; }

        /// <summary>显示名称。</summary>
        public string DisplayName { get; internal set; }
    }
}
