using System;
using System.Collections.Generic;
using UnityEngine;
using Spotlight.Shared;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 探索背包的数据与规则。只管"拥有哪些条目"，不碰 UI。
    /// 每件武器 / 物品唯一，不记录数量：拥有就是一条记录，没有就查不到。
    /// 类别按 4 位 ID 前缀判定：1000 ~ 1999 武器，2000 ~ 2999 物品。
    /// </summary>
    public sealed class ExplorationInventory : MonoBehaviour
    {
        private readonly List<InventoryEntry> entries = new List<InventoryEntry>();

        /// <summary>条目变化时触发，UI 订阅它刷新。</summary>
        public event Action Changed;

        /// <summary>添加一件物品。ID 非法或已拥有时返回 false。</summary>
        public bool Add(int itemId, Sprite icon = null, string displayName = null,
            string description = null)
        {
            if (itemId < 1000 || itemId > 2999)
            {
                Debug.LogWarning($"[Exploration] 物品 ID {itemId} 不合法：应为 4 位数，1 开头=武器、2 开头=物品。");
                return false;
            }

            if (Find(itemId) != null)
            {
                // 每件唯一，重复添加视为无效
                return false;
            }

            InventoryItemKind kind = itemId < 2000 ? InventoryItemKind.Weapon : InventoryItemKind.Item;
            entries.Add(new InventoryEntry(itemId, kind, icon, displayName, description));
            if (kind == InventoryItemKind.Weapon)
            {
                RunProgress.AddWeapon(itemId, icon, displayName);
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>移除一件物品。没有该条目时返回 false。</summary>
        public bool Remove(int itemId)
        {
            InventoryEntry existing = Find(itemId);
            if (existing == null)
            {
                return false;
            }

            entries.Remove(existing);
            Changed?.Invoke();
            return true;
        }

        /// <summary>是否已拥有该 ID。</summary>
        public bool Contains(int itemId)
        {
            return Find(itemId) != null;
        }

        /// <summary>取某一类别的条目，按 ID 升序。</summary>
        public List<InventoryEntry> GetEntries(InventoryItemKind kind)
        {
            List<InventoryEntry> result = new List<InventoryEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Kind == kind)
                {
                    result.Add(entries[i]);
                }
            }

            result.Sort((left, right) => left.Id.CompareTo(right.Id));
            return result;
        }

        private InventoryEntry Find(int itemId)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Id == itemId)
                {
                    return entries[i];
                }
            }

            return null;
        }
    }
}
