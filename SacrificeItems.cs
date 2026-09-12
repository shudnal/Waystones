using System;
using System.Collections.Generic;

namespace Waystones
{
    internal static class SacrificeItems
    {
        internal readonly struct Definition
        {
            internal readonly string key;
            internal readonly string itemName;
            internal readonly int amount;
            internal readonly int value;

            internal Definition(string key, string itemName, int amount, int value)
            {
                this.key = key;
                this.itemName = itemName;
                this.amount = amount;
                this.value = value;
            }
        }

        internal static bool IsSupportedConfigFile(string path)
        {
            string fileName = System.IO.Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            string baseName = Waystones.pluginID + ".reduce_cooldowns";
            return fileName.Equals(baseName + ".json", StringComparison.OrdinalIgnoreCase)
                || fileName.Equals(baseName + ".yaml", StringComparison.OrdinalIgnoreCase)
                || fileName.Equals(baseName + ".yml", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryNormalizeConfigKey(string key, out string normalizedKey)
        {
            normalizedKey = "";
            if (!TryParseKey(key, out string itemName, out int amount))
                return false;

            normalizedKey = amount == 1 ? itemName : $"{itemName}:{amount}";
            return true;
        }

        internal static bool TryParseKey(string key, out string itemName, out int amount)
        {
            itemName = "";
            amount = 1;

            if (string.IsNullOrWhiteSpace(key))
                return false;

            string trimmed = key.Trim();
            int separatorIndex = trimmed.LastIndexOf(':');
            if (separatorIndex < 0)
            {
                itemName = trimmed;
                return true;
            }

            string amountText = trimmed[(separatorIndex + 1)..].Trim();
            if (!int.TryParse(amountText, out int parsedAmount))
            {
                itemName = trimmed;
                return true;
            }

            if (parsedAmount <= 0)
                return false;

            string parsedItemName = trimmed[..separatorIndex].Trim();
            if (string.IsNullOrWhiteSpace(parsedItemName))
                return false;

            itemName = parsedItemName;
            amount = parsedAmount;
            return true;
        }

        internal static bool TryGetDefinition(Dictionary<string, int> definitions, ItemDrop.ItemData item, out Definition definition)
        {
            definition = default;
            bool found = false;

            if (definitions == null || item?.m_shared == null)
                return false;

            foreach (KeyValuePair<string, int> entry in definitions)
            {
                if (entry.Value <= 0 || !TryParseKey(entry.Key, out string itemName, out int amount) || !MatchesItem(item, itemName))
                    continue;

                Definition candidate = new(entry.Key, itemName, amount, entry.Value);
                if (!found || IsPreferred(candidate, definition))
                {
                    definition = candidate;
                    found = true;
                }
            }

            return found;
        }

        private static bool IsPreferred(Definition candidate, Definition current)
        {
            bool candidateSingle = candidate.amount == 1;
            bool currentSingle = current.amount == 1;
            if (candidateSingle != currentSingle)
                return candidateSingle;

            if (candidate.amount != current.amount)
                return candidate.amount < current.amount;

            return string.Compare(candidate.key, current.key, StringComparison.OrdinalIgnoreCase) < 0;
        }

        internal static bool MatchesItem(ItemDrop.ItemData item, string configuredItemName)
        {
            if (item?.m_shared == null || string.IsNullOrWhiteSpace(configuredItemName))
                return false;

            string itemName = configuredItemName.Trim();
            if (string.Equals(item.m_shared.m_name, itemName, StringComparison.OrdinalIgnoreCase))
                return true;

            if (item.m_dropPrefab != null && string.Equals(item.m_dropPrefab.name, itemName, StringComparison.OrdinalIgnoreCase))
                return true;

            string sharedName = itemName.GetItemName();
            return !string.Equals(sharedName, itemName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.m_shared.m_name, sharedName, StringComparison.OrdinalIgnoreCase);
        }

        internal static int CountOwnInventoryItems(Inventory inventory, string itemName)
        {
            if (inventory == null || string.IsNullOrWhiteSpace(itemName))
                return 0;

            int count = 0;
            foreach (ItemDrop.ItemData item in inventory.m_inventory)
            {
                if (item.m_worldLevel >= Game.m_worldLevel && MatchesItem(item, itemName))
                    count += item.m_stack;
            }

            return count;
        }

        internal static int CountAvailableItems(Inventory inventory, string itemName)
        {
            if (inventory == null || string.IsNullOrWhiteSpace(itemName))
                return 0;

            string sharedName = itemName.GetItemName();
            if (!string.IsNullOrWhiteSpace(sharedName) && sharedName.StartsWith("$", StringComparison.Ordinal))
                return inventory.CountItems(sharedName);

            return CountOwnInventoryItems(inventory, itemName);
        }

        internal static bool RemoveItems(Inventory inventory, ItemDrop.ItemData preferredItem, string itemName, int amount)
        {
            if (inventory == null || amount <= 0 || CountOwnInventoryItems(inventory, itemName) < amount)
                return false;

            List<ItemDrop.ItemData> stacks = new();
            if (preferredItem != null && preferredItem.m_worldLevel >= Game.m_worldLevel && MatchesItem(preferredItem, itemName) && inventory.m_inventory.Contains(preferredItem))
                stacks.Add(preferredItem);

            foreach (ItemDrop.ItemData item in inventory.m_inventory)
            {
                if (ReferenceEquals(item, preferredItem) || item.m_worldLevel < Game.m_worldLevel || !MatchesItem(item, itemName))
                    continue;

                stacks.Add(item);
            }

            int remaining = amount;
            foreach (ItemDrop.ItemData stack in stacks)
            {
                int removeAmount = Math.Min(stack.m_stack, remaining);
                if (removeAmount <= 0)
                    continue;

                if (!inventory.RemoveItem(stack, removeAmount))
                    return false;

                remaining -= removeAmount;
                if (remaining == 0)
                    return true;
            }

            return false;
        }
    }
}
