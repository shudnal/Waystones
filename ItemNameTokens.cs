using System;
using System.Collections.Generic;
using UnityEngine;

namespace Waystones
{
    public static class ItemNameTokens
    {
        public static readonly Dictionary<string, string> itemNames = new(StringComparer.OrdinalIgnoreCase);

        public static void UpdateRegisters()
        {
            itemNames.Clear();
            if (!ObjectDB.instance)
                return;

            foreach (GameObject item in ObjectDB.instance.m_items)
            {
                ItemDrop itemDrop = item ? item.GetComponent<ItemDrop>() : null;
                string sharedName = itemDrop?.m_itemData?.m_shared?.m_name;
                if (string.IsNullOrWhiteSpace(sharedName))
                    continue;

                itemNames[item.name] = sharedName;
                itemNames[sharedName] = sharedName;
            }
        }

        public static string GetItemName(this string input)
        {
            string itemName = (input ?? "").Trim();
            if (itemName.Length == 0)
                return itemName;

            if (itemNames.TryGetValue(itemName, out string name))
                return name;

            if (itemName.StartsWith("$", StringComparison.Ordinal))
            {
                itemNames[itemName] = itemName;
                return itemName;
            }

            if (!ObjectDB.instance)
                return itemName;

            GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(itemName);
            if (!itemPrefab)
            {
                foreach (GameObject prefab in ObjectDB.instance.m_items)
                {
                    if (prefab && string.Equals(prefab.name, itemName, StringComparison.OrdinalIgnoreCase))
                    {
                        itemPrefab = prefab;
                        break;
                    }
                }
            }

            ItemDrop itemDrop = itemPrefab ? itemPrefab.GetComponent<ItemDrop>() : null;
            string sharedName = itemDrop?.m_itemData?.m_shared?.m_name;
            if (string.IsNullOrWhiteSpace(sharedName))
                return itemName;

            itemNames[itemName] = sharedName;
            itemNames[sharedName] = sharedName;
            return sharedName;
        }
    }
}
