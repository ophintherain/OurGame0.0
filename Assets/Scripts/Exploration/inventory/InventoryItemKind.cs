namespace Spotlight.Exploration
{
    /// <summary>
    /// 背包条目的类别。
    /// </summary>
    public enum InventoryItemKind
    {
        /// <summary>武器（4 位 ID 以 1 开头，1000 ~ 1999）。</summary>
        Weapon,

        /// <summary>物品（4 位 ID 以 2 开头，2000 ~ 2999）。</summary>
        Item
    }
}
