using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Waystones
{
    public static class ItemNameTokens
    {
        public static readonly Dictionary<string, string> itemNames = new(StringComparer.OrdinalIgnoreCase);

        [HarmonyPatch(typeof(Player), nameof(Player.Load))]
        private static class Player_Load_UpdateRegisters
        {
            private static void Prefix() => UpdateRegisters();
        }

        public static void UpdateRegisters()
        {
            if (!ObjectDB.instance)
                return;

            itemNames.Clear();
            foreach (GameObject item in ObjectDB.instance.m_items)
            {
                if (item == null || item.GetComponent<ItemDrop>() is not ItemDrop itemDrop)
                    continue;

                ItemDrop.ItemData itemData = itemDrop.m_itemData;
                ItemDrop.ItemData.SharedData shared = itemData?.m_shared;
                if (shared == null || string.IsNullOrWhiteSpace(shared.m_name) || !shared.m_name.StartsWith("$"))
                    continue;

                itemNames[item.name] = shared.m_name;
                itemNames[shared.m_name] = shared.m_name;
            }

            Waystones.ReadInitialConfigs();
        }

        public static string GetItemName(this string input) => itemNames.TryGetValue((input ?? "").Trim(), out string name) ? name : input;
    }
}