using UnityEngine;

public class PT_WayStone : MonoBehaviour, Hoverable, Interactable
{
    [TextArea]
    public string m_activateMessage = "You touch the cold stone surface and you think of home.";

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
        {
            return "Activated waystone";
        }

        return Localization.instance.Localize("Waystone\n[<color=yellow><b>$KEY_Use</b></color>] Activate");
    }

    public string GetHoverName()
    {
        return "Waystone";
    }

    public bool Interact(Humanoid character, bool hold, bool alt)
    {
        if (hold)
            return false;

        if (!m_activeObject.activeSelf)
        {
            character.Message(MessageHud.MessageType.Center, m_activateMessage);
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