using UnityEngine;

public class PT_WayStone : MonoBehaviour, Hoverable, Interactable
{
    public GameObject m_activeObject;

    public EffectList m_activeEffect = new EffectList();

    public static bool initial = false;

    public void Awake()
    {
        if (initial)
            return;

        m_activeObject = base.transform.Find("WayEffect").gameObject;
        m_activeObject.SetActive(value: false);
    }

    public string GetHoverText()
    {
        if (m_activeObject.activeSelf)
            return Localization.instance.Localize("$pt_piece_waystone_activated\n[<color=yellow><b>$KEY_Use</b></color>] $pt_tooltip_start_search");

        return Localization.instance.Localize("$pt_piece_waystone_name\n[<color=yellow><b>$KEY_Use</b></color>] $pt_piece_waystone_activate");
    }

    public string GetHoverName()
    {
        return "$pt_piece_waystone_name";
    }

    public bool Interact(Humanoid character, bool hold, bool alt)
    {
        if (hold)
            return false;

        if (!m_activeObject.activeSelf)
        {
            character.Message(MessageHud.MessageType.Center, "$pt_piece_waystone_activation");
            m_activeObject.SetActive(value: true);
            m_activeEffect.Create(base.gameObject.transform.position, base.gameObject.transform.rotation);
        }

        return true;
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item)
    {
        return false;
    }
}