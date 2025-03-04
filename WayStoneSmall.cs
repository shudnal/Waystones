﻿using Waystones;
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

    public EffectList m_activateEffect = new EffectList();
    public EffectList m_deactivateEffect = new EffectList();

    public static bool initial = false;
    public static StringBuilder sb = new StringBuilder();

    public ZNetView m_nview;

    public static float blockInputUntil;
    public const float waystoneHoldSetTagDelay = 0.35f;

    public void Awake()
    {
        if (initial)
            return;

        m_activeObject = base.transform.Find("WayEffect").gameObject;
        m_activeObject.SetActive(value: false);

        m_nview = GetComponent<ZNetView>();
        if (m_nview != null && m_nview.IsValid())
        {
            InvokeRepeating("UpdateStatus", 0f, 1f);
            m_nview.Register<string, string>("RPC_SetTag", RPC_SetTag);
            m_nview.Register<long, string>("ToggleActivated", RPC_ToggleActivated);
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
        if (permittedPlayers.RemoveAll((KeyValuePair<long, string> x) => x.Key == playerID) > 0)
        {
            SetActivatedPlayers(permittedPlayers);
            m_deactivateEffect.Create(base.transform.position, base.transform.rotation);
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
        m_activateEffect.Create(base.transform.position, base.transform.rotation);
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

        if (!PrivateArea.CheckAccess(base.transform.position, 0f, flash: false))
            return Localization.instance.Localize(GetHoverName() + "\n$piece_noaccess");

        sb.Clear();
        sb.Append(GetHoverName());
        
        string text = GetText().RemoveRichTextTags();
        if (text.Length > 0)
            sb.AppendFormat(" \"{0}\"", text);

        sb.Append("\n[<color=yellow><b>$KEY_Use</b></color>] $ws_tooltip_start_search $ws_piece_waystone_settag");

        string altKey = !ZInput.IsNonClassicFunctionality() || !ZInput.IsGamepadActive() ? "$KEY_AltPlace" : "$KEY_JoyAltKeys";
        sb.Append($"\n[<color=yellow><b>{altKey} + $KEY_Use</b></color>] {(IsActive() ? "$ws_piece_waystone_deactivate" : "$ws_piece_waystone_activate")}");

        return Localization.instance.Localize(sb.ToString());
    }

    public string GetHoverName()
    {
        return IsActive() ? "$ws_piece_waystone_activated" : "$ws_piece_waystone_name";
    }

    public bool Interact(Humanoid human, bool hold, bool alt)
    {
        if (Player.m_localPlayer == null || Player.m_localPlayer.InInterior() || !PrivateArea.CheckAccess(base.transform.position))
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

        if (IsSearchAllowed(player) && CanCast())
        {
            player.Message(MessageHud.MessageType.Center, "$ws_piece_waystone_activation");
            WaystoneList.EnterSearchMode();
        }
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item)
    {
        int cooldown = 0;
        if (itemSacrifitionReduceCooldown.Value && (TryReduceCooldownOnItemSacrifice(user, item, item.m_dropPrefab?.name, ref cooldown) || TryReduceCooldownOnItemSacrifice(user, item, item.m_shared.m_name, ref cooldown)))
        {
            user.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$ws_piece_waystone_cooldown_reduced", cooldown.ToString()));
            if (WorldData.IsOnCooldown())
                user.Message(MessageHud.MessageType.TopLeft, $"$hud_powernotready: {WorldData.GetCooldownString()}");

            return true;
        }

        return false;
    }

    private bool TryReduceCooldownOnItemSacrifice(Humanoid user, ItemDrop.ItemData item, string itemName, ref int cooldown)
    {
        if (itemName == null)
            return false;

        if (itemsToReduceCooldown.Value.TryGetValue(itemName, out int reduceCooldown) && (cooldown = reduceCooldown) > 0)
            return user.GetInventory().RemoveOneItem(item) && WorldData.TryReduceCooldown(reduceCooldown);

        if (itemsToReduceCooldown.Value.Keys.FirstOrDefault(key => key.StartsWith(itemName)) is string itemKey && itemsToReduceCooldown.Value.TryGetValue(itemKey, out int reduce) && (cooldown = reduce) > 0)
        {
            string[] pair = itemKey.Split(':');
            return pair.Length > 1 && pair[0] == itemName && int.TryParse(pair[1], out int amount) && CountItems(user.GetInventory(), itemName) >= amount && user.GetInventory().RemoveItem(item, amount) && WorldData.TryReduceCooldown(reduce);
        }

        return false;
    }

    private int CountItems(Inventory inventory, string itemName)
    {
        return inventory.m_inventory.Where(item => item.m_shared.m_name == itemName || item.m_dropPrefab?.name == itemName).Sum(item => item.m_stack);
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

        return CensorShittyWords.FilterUGC(zDO.GetString(ZDOVars.s_tag), UGCType.Text, new PlatformUserID(zDO.GetString(ZDOVars.s_tagauthor)), 0L);
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
        List<KeyValuePair<long, string>> list = new List<KeyValuePair<long, string>>();
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
            if (permittedPlayer.Key == playerID)
                return true;

        return false;
    }

    internal static bool IsSearchAllowed(Player player)
    {
        if (player == null)
            return false;

        if (player != Player.m_localPlayer)
            return false;

        if (WorldData.IsOnCooldown())
        {
            player.Message(MessageHud.MessageType.Center, $"$hud_powernotready: {WorldData.GetCooldownString()}");
            return false;
        }
        else if (IsNotInPosition(player))
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