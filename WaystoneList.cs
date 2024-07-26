using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static WaystoneTeleporter.WaystoneTeleporter;

namespace WaystoneTeleporter
{
    [Serializable]
    internal class MarkedWaystone
    {
        public long worldUID;
        public string name;
        public Vector3 position;
        public ushort zdoUserKey;
        public uint zdoID;

        public static Sprite iconWaystone;
        public static readonly List<Minimap.PinData> waystonePins = new List<Minimap.PinData>();

        public const string customDataKey = "WaystoneList";

        public static void UpdatePins()
        {
            foreach (Minimap.PinData pin in waystonePins)
                Minimap.instance.RemovePin(pin);

            waystonePins.Clear();
            if (showOnMap.Value)
                foreach (MarkedWaystone waystone in GetCurrentWorldList())
                    waystonePins.Add(Minimap.instance.AddPin(waystone.position, (Minimap.PinType)WaystoneIconType.pinType, waystone.name, save: false, isChecked: false, Player.m_localPlayer.GetPlayerID()));   
        }

        public static IEnumerable<MarkedWaystone> GetCurrentWorldList()
        {
            return GetWorldList(GetMarkedWaystones());
        }

        public static bool IsEnabled(ZDO zdo)
        {
            if (zdo == null || !zdo.IsValid())
                return false;

            List<MarkedWaystone> state = GetMarkedWaystones();

            return GetWorldList(state).Any(d => zdo.m_uid.UserKey == d.zdoUserKey && zdo.m_uid.ID == d.zdoID);
        }

        private static IEnumerator UpdatePinsOnMapReady()
        {
            yield return new WaitUntil(() => Minimap.instance);

            UpdatePins();
        }

        public static void EnableWaystone(ZDO zdo)
        {
            if (zdo == null || !zdo.IsValid())
                return;

            List<MarkedWaystone> state = GetMarkedWaystones();

            if (GetWorldList(state).Any(d => zdo.m_uid.UserKey == d.zdoUserKey && zdo.m_uid.ID == d.zdoID))
                return;

            MarkedWaystone waystone = new MarkedWaystone()
            {
                worldUID = ZNet.instance.GetWorldUID(),
                name = zdo.GetString(ZDOVars.s_tag),
                position = zdo.GetPosition(),
                zdoUserKey = zdo.m_uid.UserKey,
                zdoID = zdo.m_uid.ID
            };

            state.Add(waystone);

            Player.m_localPlayer.m_customData[customDataKey] = SaveWaystoneList(state);

            LogInfo($"Waystone enabled: {waystone.name} {waystone.position}");
        }

        public static void DisableWaystone(ZDO zdo)
        {
            if (zdo == null || !zdo.IsValid())
                return;

            List<MarkedWaystone> state = GetMarkedWaystones();

            if (state.RemoveAll(d => d.worldUID == ZNet.instance.GetWorldUID() && zdo.m_uid.UserKey == d.zdoUserKey && zdo.m_uid.ID == d.zdoID) == 0)
                return;

            Player.m_localPlayer.m_customData[customDataKey] = SaveWaystoneList(state);

            LogInfo($"Waystone disabled: {zdo.GetString(ZDOVars.s_tag)} {zdo.GetPosition()}");
        }

        private static IEnumerable<MarkedWaystone> GetWorldList(List<MarkedWaystone> state)
        {
            return state.Where(d => d.worldUID == ZNet.instance.GetWorldUID());
        }

        private static List<MarkedWaystone> GetMarkedWaystones()
        {
            return Player.m_localPlayer.m_customData.TryGetValue(customDataKey, out string value) ? GetWaystoneList(value) : new List<MarkedWaystone>();
        }

        private static List<MarkedWaystone> GetWaystoneList(string value)
        {
            List<MarkedWaystone> data = new List<MarkedWaystone>();
            SplitToLines(value).Do(line => data.Add(JsonUtility.FromJson<MarkedWaystone>(line)));
            return data;
        }

        private static string SaveWaystoneList(List<MarkedWaystone> list)
        {
            StringBuilder sb = new StringBuilder();
            list.Do(data => sb.AppendLine(JsonUtility.ToJson(data)));
            return sb.ToString();
        }

        private static IEnumerable<string> SplitToLines(string input)
        {
            if (input == null)
            {
                yield break;
            }

            using (System.IO.StringReader reader = new System.IO.StringReader(input))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
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
        
        [HarmonyPatch(typeof(Player), nameof(Player.SetLocalPlayer))]
        public static class Player_SetLocalPlayer_AddWaystonePins
        {
            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer)
                    instance.StartCoroutine(UpdatePinsOnMapReady());
            }
        }
    }
}
