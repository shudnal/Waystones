using PocketTeleporter;
using System.Text;
using UnityEngine;
using static PocketTeleporter.DirectionSearch;
using static PocketTeleporter.PocketTeleporter;

public class PT_WayStone : MonoBehaviour, Hoverable, Interactable
{
    public GameObject m_activeObject;

    public EffectList m_activateEffect = new EffectList();
    public EffectList m_markEffect = new EffectList();

    public static bool initial = false;
    public static StringBuilder sb = new StringBuilder();

    public ZNetView m_nview;

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
            m_nview.Register("ToggleEnabled", RPC_ToggleEnabled);
            if (m_nview.IsOwner())
                SetEnabled(enabled: false);
        }
    }

    public void RPC_ToggleEnabled(long uid)
    {
        if (m_nview.IsOwner())
        {
            SetEnabled(!IsEnabled());
        }
    }

    public bool IsEnabled()
    {
        if (!m_nview.IsValid())
        {
            return false;
        }

        return m_nview.GetZDO().GetBool(ZDOVars.s_enabled);
    }

    public void SetEnabled(bool enabled)
    {
        m_nview.GetZDO().Set(ZDOVars.s_enabled, enabled);
        UpdateStatus();
        if (enabled)
            m_activateEffect.Create(base.transform.position, base.transform.rotation);
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

        if (!IsActivated())
            return Localization.instance.Localize("$pt_piece_waystone_name\n[<color=yellow><b>$KEY_Use</b></color>] $pt_piece_waystone_activate");

        sb.Clear();
        sb.Append("$pt_piece_waystone_activated");
        sb.Append("\n[<color=yellow><b>$KEY_Use</b></color>] $pt_tooltip_start_search");

        Vector3 markedPosition = WorldData.GetMarkedPositionTooltip();

        if (markedPosition == Vector3.one || Utils.DistanceXZ(Player.m_localPlayer.transform.position, markedPosition) > 5f)
        {
            string altKey = !ZInput.IsNonClassicFunctionality() || !ZInput.IsGamepadActive() ? "$KEY_AltPlace" : "$KEY_JoyAltKeys";
            sb.Append($"\n[<color=yellow><b>{altKey} + $KEY_Use</b></color>] $pt_piece_waystone_mark");
        }
        else
        {
            sb.Append($"\n\n<color=#add8e6>$pt_location_marked_location</color>");
        }

        return Localization.instance.Localize(sb.ToString());
    }

    public string GetHoverName()
    {
        return "$pt_piece_waystone_name";
    }

    public bool Interact(Humanoid character, bool hold, bool alt)
    {
        if (hold)
            return false;

        if (!IsActivated())
        {
            if (!alt)
                m_nview.InvokeRPC("ToggleEnabled");
            return !alt;
        }

        if (alt)
            MarkCurrentLocation(character);
        else if (IsSearchAllowed(character as Player))
        {
            character.Message(MessageHud.MessageType.Center, "$pt_piece_waystone_activation");
            Enter();
        }

        return true;
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item)
    {
        return false;
    }

    public bool IsActivated()
    {
        return m_activeObject.activeSelf;
    }

    private void MarkCurrentLocation(Humanoid character)
    {
        if (character != null && character != Player.m_localPlayer)
            return;

        m_markEffect.Create(base.transform.position, base.transform.rotation);
        WorldData.SaveMarkedPosition(character.transform.position);
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
            player.Message(MessageHud.MessageType.Center, "$pt_piece_waystone_sit");
            return false;
        }

        return true;
    }
        
    internal static bool IsNotInPosition(Player player)
    {
        return player.IsAttachedToShip() || player.IsAttached() || player.IsDead() || player.IsRiding() || player.IsSleeping() ||
               player.IsTeleporting() || player.InPlaceMode() || player.InBed() || player.InCutscene() || player.InInterior();
    }

}