using Waystones;
using System.Text;
using UnityEngine;
using static Waystones.Waystones;
using System;
using System.Collections.Generic;
using System.Collections;
using HarmonyLib;
using System.Linq;
using Splatform;

public class WaystoneSmall : MonoBehaviour, TextReceiver, Hoverable, Interactable
{
    public GameObject m_activeObject;

    public EffectList m_activateEffect = new();
    public EffectList m_deactivateEffect = new();

    public static bool initial = false;
    public static StringBuilder sb = new();

    public ZNetView m_nview;

    public static float blockInputUntil;
    public const float waystoneHoldSetTagDelay = 0.35f;

    private const float sacrificeHoverItemsUpdateInterval = 1f;
    private static float nextSacrificeHoverItemsUpdate;
    private static readonly List<SacrificeHoverItem> sacrificeHoverItems = new();

    private class SacrificeHoverItem
    {
        public string itemName;
        public int amount;
        public int value;
        public int inventoryCount;
        public int totalCount;
    }

    public void Awake()
    {
        if (initial)
            return;

        m_activeObject = transform.Find("WayEffect").gameObject;
        m_activeObject.SetActive(value: false);

        m_nview = GetComponent<ZNetView>();
        if (m_nview != null && m_nview.IsValid())
        {
            InvokeRepeating("UpdateStatus", 0f, 1f);
            m_nview.Register<string, string>("RPC_SetTag", RPC_SetTag);
            m_nview.Register<long, string>("ToggleActivated", RPC_ToggleActivated);
            m_nview.Register<int>("RPC_AddCharge", RPC_AddCharge);
        }
    }

    public void RPC_ToggleActivated(long uid, long playerID, string name)
    {
        if (m_nview.IsOwner())
        {
            if (IsActivated(playerID))
                RemoveActivated(playerID);
            else
                AddActivated(playerID, name);
            
            UpdateStatus();
        }
    }

    public void RemoveActivated(long playerID)
    {
        List<KeyValuePair<long, string>> permittedPlayers = GetActivatedPlayers();
        if (permittedPlayers.RemoveAll(x => x.Key == playerID) > 0)
        {
            SetActivatedPlayers(permittedPlayers);
            m_deactivateEffect.Create(transform.position, transform.rotation);
        }
    }

    public bool IsActivated(long playerID)
    {
        return IsWaystoneActivated(m_nview.GetZDO(), playerID);
    }

    public void AddActivated(long playerID, string playerName)
    {
        List<KeyValuePair<long, string>> permittedPlayers = GetActivatedPlayers();
        foreach (KeyValuePair<long, string> item in permittedPlayers)
        {
            if (item.Key == playerID)
            {
                return;
            }
        }

        permittedPlayers.Add(new KeyValuePair<long, string>(playerID, playerName));
        SetActivatedPlayers(permittedPlayers);
        m_activateEffect.Create(transform.position, transform.rotation);
    }

    public void SetActivatedPlayers(List<KeyValuePair<long, string>> users)
    {
        m_nview.GetZDO().Set(ZDOVars.s_permitted, users.Count);
        for (int i = 0; i < users.Count; i++)
        {
            KeyValuePair<long, string> keyValuePair = users[i];
            m_nview.GetZDO().Set("pu_id" + i, keyValuePair.Key);
            m_nview.GetZDO().Set("pu_name" + i, keyValuePair.Value);
        }
    }

    public List<KeyValuePair<long, string>> GetActivatedPlayers()
    {
        return GetWaystoneActivatedPlayers(m_nview.GetZDO());
    }

    public bool IsEnabled()
    {
        if (Player.m_localPlayer == null) 
            return false;

        return IsActivated(Player.m_localPlayer.GetPlayerID());
    }

    public void UpdateStatus()
    {
        bool flag = IsEnabled();
        m_activeObject.SetActive(flag);
    }

    public string GetHoverText()
    {
        if (!m_nview.IsValid())
            return "";

        if (Player.m_localPlayer == null)
            return "";

        if (Player.m_localPlayer.InInterior())
            return GetHoverName();

        if (!PrivateArea.CheckAccess(transform.position, 0f, flash: false))
            return Localization.instance.Localize(GetHoverName() + "\n$piece_noaccess");

        sb.Clear();
        sb.Append(GetHoverName());
        
        string text = GetText().RemoveRichTextTags();
        if (text.Length > 0)
            sb.AppendFormat(" \"{0}\"", text);

        sb.Append("\n[<color=yellow><b>$KEY_Use</b></color>] $ws_tooltip_start_search $ws_piece_waystone_settag");

        string altKey = !ZInput.IsNonClassicFunctionality() || !ZInput.IsGamepadActive() ? "$KEY_AltPlace" : "$KEY_JoyAltKeys";
        sb.Append($"\n[<color=yellow><b>{altKey} + $KEY_Use</b></color>] {(IsActive() ? "$ws_piece_waystone_deactivate" : "$ws_piece_waystone_activate")}");

        if (waystoneMode.Value == WaystoneMode.Cooldown)
        {
            if (WorldData.IsOnCooldown())
                sb.Append($"\n$hud_powernotready: <color=#add8e6>{WorldData.GetCooldownString()}</color>");
        }
        else if (waystoneMode.Value == WaystoneMode.Charge)
        {
            if (WaystoneList.IsPlayerChargeStorage())
                sb.Append($"\n$ws_tooltip_player_charge <color=#add8e6>{WorldData.GetPlayerCharge()}</color>");
            else
                sb.Append($"\n$ws_tooltip_waystone_charge <color=#add8e6>{WaystoneList.GetWaystoneCharge(m_nview.GetZDO())}</color>");
        }

        AppendSacrificeItemsHoverText(Player.m_localPlayer);

        return Localization.instance.Localize(sb.ToString());
    }

    public string GetHoverName()
    {
        return IsActive() ? "$ws_piece_waystone_activated" : "$ws_piece_waystone_name";
    }

    public bool Interact(Humanoid human, bool hold, bool alt)
    {
        if (Player.m_localPlayer == null || Player.m_localPlayer.InInterior() || !PrivateArea.CheckAccess(transform.position))
            return true;

        if (hold)
        {
            if (ZInput.GetButtonPressedTimer("Use") + ZInput.GetButtonPressedTimer("JoyUse") > waystoneHoldSetTagDelay && !TextInput.IsVisible())
            {
                blockInputUntil = Time.time + 1f;
                ZInput.ResetButtonStatus("Use");
                ZInput.ResetButtonStatus("JoyUse");
                TextInput.instance.RequestText(this, "$ws_piece_waystone_tag", Math.Max(tagCharactersLimit.Value, 10));
            }
            return false;
        }

        Player player = human as Player;
        if (alt)
        {
            m_nview.InvokeRPC("ToggleActivated", player.GetPlayerID(), player.GetPlayerName());
            return true;
        }

        StartCoroutine(ActivationToggleRequested(player));
        return true;
    }

    public IEnumerator ActivationToggleRequested(Player player)
    {
        yield return new WaitWhile(() => ZInput.GetButton("Use") || ZInput.GetButton("JoyUse"));

        if (TextInput.IsVisible())
            yield break;

        if (IsSearchAllowed(player, validateCharge: false) && CanCast())
        {
            player.Message(MessageHud.MessageType.Center, "$ws_piece_waystone_activation");
            WaystoneList.EnterSearchMode(m_nview.GetZDO());
        }
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item)
    {
        if (waystoneMode.Value == WaystoneMode.Cooldown && itemSacrifitionReduceCooldown.Value)
        {
            int cooldown = 0;
            if (TryReduceCooldownOnItemSacrifice(user, item, item.m_shared.m_name, ref cooldown))
            {
                user.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$ws_piece_waystone_cooldown_reduced", cooldown.ToString()));
                if (WorldData.IsOnCooldown())
                    user.Message(MessageHud.MessageType.TopLeft, $"$hud_powernotready: {WorldData.GetCooldownString()}");
                return true;
            }
        }

        if (waystoneMode.Value == WaystoneMode.Charge)
        {
            int charges = 0;
            if (TryChargeWaystoneOnItemSacrifice(user, item, item.m_shared.m_name, ref charges))
            {
                user.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$ws_piece_waystone_charge_added", charges.ToString()));
                return true;
            }
        }

        return false;
    }

    private bool TryReduceCooldownOnItemSacrifice(Humanoid user, ItemDrop.ItemData item, string itemName, ref int cooldown)
    {
        if (itemName == null)
            return false;

        itemName = itemName.GetItemName();
        if (itemsToReduceCooldown.Value.TryGetValue(itemName, out int reduceCooldown) && (cooldown = reduceCooldown) > 0)
            return user.GetInventory().RemoveOneItem(item) && WorldData.TryReduceCooldown(reduceCooldown);

        if (itemsToReduceCooldown.Value.Keys.FirstOrDefault(key => key.StartsWith(itemName)) is string itemKey && itemsToReduceCooldown.Value.TryGetValue(itemKey, out int reduce) && (cooldown = reduce) > 0)
        {
            string[] pair = itemKey.Split(':');
            return pair.Length > 1 && pair[0] == itemName && int.TryParse(pair[1], out int amount) && CountItems(user.GetInventory(), itemName) >= amount && user.GetInventory().RemoveItem(item, amount) && WorldData.TryReduceCooldown(reduce);
        }

        return false;
    }

    private bool TryChargeWaystoneOnItemSacrifice(Humanoid user, ItemDrop.ItemData item, string itemName, ref int charge)
    {
        if (itemName == null || !m_nview.IsValid())
            return false;

        itemName = itemName.GetItemName();
        if (itemsToReduceCooldown.Value.TryGetValue(itemName, out int addCharge) && addCharge > 0)
            return TryConsumeChargeItem(user, item, itemName, addCharge, 1, ref charge);

        if (itemsToReduceCooldown.Value.Keys.FirstOrDefault(key => key == itemName || key.StartsWith(itemName + ":")) is string itemKey && itemsToReduceCooldown.Value.TryGetValue(itemKey, out int add) && add > 0)
        {
            string[] pair = itemKey.Split(':');
            if (pair.Length > 1 && pair[0] == itemName && int.TryParse(pair[1], out int amount))
                return TryConsumeChargeItem(user, item, itemName, add, amount, ref charge);
        }

        return false;
    }

    private bool TryConsumeChargeItem(Humanoid user, ItemDrop.ItemData item, string itemName, int addCharge, int amount, ref int charge)
    {
        if (amount <= 0 || CountItems(user.GetInventory(), itemName) < amount)
            return false;

        charge = WaystoneList.GetPotentialChargeAdded(m_nview.GetZDO(), addCharge);
        if (charge <= 0)
            return false;

        bool removed = amount == 1
            ? user.GetInventory().RemoveOneItem(item)
            : user.GetInventory().RemoveItem(item, amount);

        if (!removed)
            return false;

        AddCharge(addCharge);
        return true;
    }

    private void AddCharge(int amount)
    {
        if (!m_nview.IsValid() || amount <= 0)
            return;

        if (WaystoneList.IsPlayerChargeStorage())
        {
            WaystoneList.AddCharge(m_nview.GetZDO(), amount);
            return;
        }

        if (m_nview.IsOwner())
            WaystoneList.AddWaystoneCharge(m_nview.GetZDO(), amount);
        else
            m_nview.InvokeRPC("RPC_AddCharge", amount);
    }

    public void RPC_AddCharge(long sender, int amount)
    {
        if (m_nview.IsValid() && m_nview.IsOwner())
            WaystoneList.AddWaystoneCharge(m_nview.GetZDO(), amount);
    }

    private int CountItems(Inventory inventory, string itemName)
    {
        return CountOwnInventoryItems(inventory, itemName);
    }

    private static int CountOwnInventoryItems(Inventory inventory, string itemName)
    {
        if (inventory == null)
            return 0;

        int count = 0;
        foreach (ItemDrop.ItemData item in inventory.m_inventory)
            if (item.m_shared.m_name.GetItemName() == itemName && item.m_worldLevel >= Game.m_worldLevel)
                count += item.m_stack;

        return count;
    }

    private static int CountAvailableItems(Inventory inventory, string itemName)
    {
        if (inventory == null)
            return 0;

        return inventory.CountItems(itemName);
    }

    private static void AppendSacrificeItemsHoverText(Player player)
    {
        if (!showSacrificeItemsInHover.Value)
            return;

        if (!ShouldShowSacrificeItems())
            return;

        RefreshSacrificeHoverItems(player);
        if (sacrificeHoverItems.Count == 0)
            return;

        sb.Append("\n\n");
        sb.Append(waystoneMode.Value == WaystoneMode.Charge
            ? "$ws_tooltip_sacrifice_add_charge"
            : "$ws_tooltip_sacrifice_reduce_cooldown");

        string valueToken = waystoneMode.Value == WaystoneMode.Charge ? "$ws_tooltip_charges" : "$ws_tooltip_seconds";
        foreach (SacrificeHoverItem item in sacrificeHoverItems)
        {
            sb.Append("\n - ");
            sb.Append($"<color=#add8e6>{item.itemName}</color>");
            if (item.amount > 1)
                sb.Append($" x{item.amount}");

            sb.Append($" - {valueToken}: <color=orange>{(waystoneMode.Value == WaystoneMode.Charge ? "+" : "-")}{item.value}</color>. $settings_inventory: <color=yellow>{item.inventoryCount}</color>");
            if (item.totalCount != item.inventoryCount)
                sb.Append($", $item_total: <color=yellow>{item.totalCount}</color>");
        }
    }

    private static bool ShouldShowSacrificeItems()
    {
        return waystoneMode.Value == WaystoneMode.Charge
            || (waystoneMode.Value == WaystoneMode.Cooldown && itemSacrifitionReduceCooldown.Value);
    }

    private static void RefreshSacrificeHoverItems(Player player)
    {
        if (Time.time < nextSacrificeHoverItemsUpdate)
            return;

        nextSacrificeHoverItemsUpdate = Time.time + sacrificeHoverItemsUpdateInterval;
        sacrificeHoverItems.Clear();

        if (player == null)
            return;

        Inventory inventory = player.GetInventory();
        if (inventory == null)
            return;

        foreach (KeyValuePair<string, int> entry in itemsToReduceCooldown.Value)
        {
            if (entry.Value <= 0 || !TryParseSacrificeEntry(entry.Key, out string itemName, out int amount))
                continue;

            int inventoryCount = CountOwnInventoryItems(inventory, itemName);
            int totalCount = CountAvailableItems(inventory, itemName);
            if (inventoryCount <= 0 && totalCount <= 0)
                continue;

            sacrificeHoverItems.Add(new SacrificeHoverItem
            {
                itemName = itemName,
                amount = amount,
                value = entry.Value,
                inventoryCount = inventoryCount,
                totalCount = totalCount
            });
        }
    }

    private static bool TryParseSacrificeEntry(string key, out string itemName, out int amount)
    {
        itemName = key;
        amount = 1;

        if (string.IsNullOrWhiteSpace(key))
            return false;

        string[] pair = key.Split(':');
        if (pair.Length == 1)
            return true;

        if (pair.Length != 2 || string.IsNullOrWhiteSpace(pair[0]) || !int.TryParse(pair[1], out amount) || amount <= 0)
            return false;

        itemName = pair[0];
        return true;
    }

    private static string BuildConfiguredSacrificeItemsText()
    {
        if (!ShouldShowSacrificeItems())
            return "";

        if (itemsToReduceCooldown.Value.Count == 0)
            return "";

        StringBuilder builder = new();
        builder.Append(waystoneMode.Value == WaystoneMode.Charge
            ? "$ws_tooltip_sacrifice_add_charge"
            : "$ws_tooltip_sacrifice_reduce_cooldown");

        string valueToken = waystoneMode.Value == WaystoneMode.Charge ? "$ws_tooltip_charges" : "$ws_tooltip_seconds";
        foreach (KeyValuePair<string, int> entry in itemsToReduceCooldown.Value)
        {
            if (entry.Value <= 0 || !TryParseSacrificeEntry(entry.Key, out string itemName, out int amount))
                continue;

            builder.Append("\n - ");
            builder.Append(itemName);
            if (amount > 1)
                builder.Append($" x{amount}");
            builder.Append($" - {valueToken}: <color=#add8e6>{(waystoneMode.Value == WaystoneMode.Charge ? "+" : "-")}{entry.Value}</color>");
        }

        return builder.ToString();
    }

    public bool IsActive()
    {
        return m_activeObject.activeSelf;
    }

    public string GetText()
    {
        ZDO zDO = m_nview.GetZDO();
        if (zDO == null)
            return "";

        string text = zDO.GetString(ZDOVars.s_tagauthor);
        PlatformUserID userId = (string.IsNullOrEmpty(text) ? PlatformUserID.None : new PlatformUserID(text));
        return CensorShittyWords.FilterUGC(zDO.GetString(ZDOVars.s_tag), UGCType.Text, userId, 0L);
    }

    public void GetTagSignature(out string tagRaw, out string authorId)
    {
        ZDO zDO = m_nview.GetZDO();
        tagRaw = zDO.GetString(ZDOVars.s_tag);
        authorId = zDO.GetString(ZDOVars.s_tagauthor);
    }

    public void SetText(string text)
    {
        if (m_nview.IsValid())
            m_nview.InvokeRPC("RPC_SetTag", text, PlatformManager.DistributionPlatform.LocalUser.PlatformUserID.ToString());
    }

    public void RPC_SetTag(long sender, string tag, string authorId)
    {
        if (m_nview.IsValid() && m_nview.IsOwner())
        {
            GetTagSignature(out var tagRaw, out var authorId2);
            if (!(tagRaw == tag) || !(authorId2 == authorId))
            {
                ZDO zDO = m_nview.GetZDO();
                zDO.Set(ZDOVars.s_tag, tag);
                zDO.Set(ZDOVars.s_tagauthor, authorId);
            }
        }
    }

    public static List<KeyValuePair<long, string>> GetWaystoneActivatedPlayers(ZDO zdo)
    {
        List<KeyValuePair<long, string>> list = new();
        int @int = zdo.GetInt(ZDOVars.s_permitted);
        for (int i = 0; i < @int; i++)
        {
            long @long = zdo.GetLong("pu_id" + i, 0L);
            string @string = zdo.GetString("pu_name" + i);
            if (@long != 0L)
                list.Add(new KeyValuePair<long, string>(@long, @string));
        }

        return list;
    }

    public static bool IsWaystoneActivated(ZDO zdo, long playerID)
    {
        if (zdo == null)
            return false;

        foreach (KeyValuePair<long, string> permittedPlayer in GetWaystoneActivatedPlayers(zdo))
            if (allowForEveryone.Value || permittedPlayer.Key == playerID)
                return true;

        return false;
    }

    internal static bool IsSearchAllowed(Player player, bool validateCharge = true)
    {
        if (player == null)
            return false;

        if (player != Player.m_localPlayer)
            return false;

        if (waystoneMode.Value == WaystoneMode.Cooldown && WorldData.IsOnCooldown())
        {
            player.Message(MessageHud.MessageType.Center, $"$hud_powernotready: {WorldData.GetCooldownString()}");
            return false;
        }

        if (validateCharge && waystoneMode.Value == WaystoneMode.Charge)
        {
            if (!WaystoneList.CanStartSearchWithCharge(WaystoneList.GetCurrentTravelCharge(player.GetPlayerID(), player.transform.position)))
            {
                player.Message(MessageHud.MessageType.Center, "$ws_message_not_enough_charge");
                return false;
            }
        }

        if (IsNotInPosition(player))
        {
            player.Message(MessageHud.MessageType.Center, "$msg_cart_incorrectposition");
            return false;
        }
        else if (!allowEncumbered.Value && player.IsEncumbered())
        {
            player.Message(MessageHud.MessageType.Center, "$se_encumbered_start");
            return false;
        }
        else if (!allowNonTeleportableItems.Value && !player.IsTeleportable())
        {
            player.Message(MessageHud.MessageType.Center, "$msg_noteleport");
            return false;
        }
        else if (!allowWet.Value && player.GetSEMan().HaveStatusEffect(SEMan.s_statusEffectWet))
        {
            player.Message(MessageHud.MessageType.Center, "$msg_bedwet");
            return false;
        }
        else if (!allowSensed.Value && player.IsSensed())
        {
            player.Message(MessageHud.MessageType.Center, "$msg_bedenemiesnearby");
            return false;
        }
        else if (!allowNonSitting.Value && !player.IsSitting())
        {
            player.Message(MessageHud.MessageType.Center, "$ws_piece_waystone_sit");
            return false;
        }

        return true;
    }
        
    internal static bool IsNotInPosition(Player player)
    {
        return player.IsAttachedToShip() || player.IsAttached() || player.IsDead() || player.IsRiding() || player.IsSleeping() ||
               player.IsTeleporting() || player.InPlaceMode() || player.InBed() || player.InCutscene() || player.InInterior();
    }

    [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.UpdateTextsList))]
    public static class TextsDialog_UpdateTextsList_WaystoneSacrificeItems
    {
        private static void Postfix(TextsDialog __instance)
        {
            string text = BuildConfiguredSacrificeItemsText();
            if (text.Length == 0)
                return;

            string waystoneTopic = Localization.instance.Localize("$ws_tutorial_waystone_label");

            int waystoneIndex = __instance.m_texts.FindIndex(info =>
                info != null &&
                (info.m_topic == "$ws_tutorial_waystone_label" ||
                 info.m_topic == waystoneTopic ||
                 Localization.instance.Localize(info.m_topic) == waystoneTopic));

            if (waystoneIndex < 0)
                return;

            __instance.m_texts.Insert(waystoneIndex + 1, new TextsDialog.TextInfo("$ws_compendium_sacrifice_items_topic", text));
        }
    }

    [HarmonyPatch(typeof(TextInput), nameof(TextInput.Update))]
    public static class TextInput_Update_HoldUseButton
    {
        private static void Postfix(TextInput __instance)
        {
            __instance.m_inputField.readOnly = false;
            if (__instance.m_queuedSign is WaystoneSmall && TextInput.IsVisible() && blockInputUntil > Time.time)
                __instance.m_inputField.readOnly = true;
        }
    }
}