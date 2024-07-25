using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static WaystoneTeleporter.WaystoneTeleporter;

namespace WaystoneTeleporter
{
    [Serializable]
    internal class WaystoneList
    {
        public long worldUID;
        public string name;
        public Vector3 position;
        public ushort zdoUserKey;
        public uint zdoID;

        public static Sprite iconWaystone;
        public static readonly List<Minimap.PinData> waystonePins = new List<Minimap.PinData>();

        public const string customDataKey = "WaystoneList";

        public static void asd()
        {
            foreach (Minimap.PinData pin in waystonePins)
                Minimap.instance.RemovePin(pin);

            waystonePins.Clear();
            /*if (showOnMap.Value)
            {
                for (int num21 = 0; num21 < 1; num21++)
                    waystonePins.Add(Minimap.instance.AddPin(Vector3.zero, (Minimap.PinType)WaystoneIconType.pinType, "", save: false, isChecked: false, Player.m_localPlayer.GetPlayerID()));   
            }*/
        }

        [HarmonyPatch(typeof(Minimap), nameof(Minimap.Start))]
        public static class WaystoneIconType
        {
            public static int pinType;

            private static void Postfix(Minimap __instance)
            {
                pinType = __instance.m_visibleIconTypes.Length;

                bool[] visibleIcons = new bool[pinType + 1];
                Array.Copy(__instance.m_visibleIconTypes, visibleIcons, pinType);
                
                __instance.m_visibleIconTypes = visibleIcons;
                __instance.m_icons.Add(new Minimap.SpriteData
                {
                    m_name = (Minimap.PinType)pinType,
                    m_icon = iconWaystone,
                });
            }
        }
    }
}
