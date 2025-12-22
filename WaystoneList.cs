using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using static Waystones.Waystones;

namespace Waystones
{
    internal static class WaystoneList
    {
        public static Sprite iconWaystone;
        public static readonly List<Minimap.PinData> waystonePins = new();
        public static readonly List<Tuple<string, Vector3, Quaternion>> activatedWaystones = new();

        // Server
        public static readonly HashSet<ZDO> waystoneObjects = new();

        public const string customDataKey = "WaystoneList";

        public static void UpdatePins()
        {
            foreach (Minimap.PinData pin in waystonePins)
                Minimap.instance.RemovePin(pin);

            waystonePins.Clear();
            if (locationWaystonesShowOnMap.Value && DirectionSearch.IsActivated)
                foreach (Tuple<string, Vector3, Quaternion> waystone in activatedWaystones)
                    waystonePins.Add(Minimap.instance.AddPin(waystone.Item2, (Minimap.PinType)WaystoneIconType.pinType, waystone.Item1, save: false, isChecked: false, Player.m_localPlayer.GetPlayerID()));
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
        
        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        public static class ZoneSystem_Start_WaystoneList
        {
            private static void Postfix()
            {
                RegisterRPCs();
            }
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.OnDestroy))]
        public static class ZoneSystem_OnDestroy_WaystoneList
        {
            private static void Postfix()
            {
                waystoneObjects.Clear();
            }
        }

        internal static void RegisterRPCs()
        {
            if (ZNet.instance.IsServer())
            {
                ZRoutedRpc.instance.Register<long>("MarkedLocationRequest", RPC_MarkedLocationRequest);
            }
            else
            {
                ZRoutedRpc.instance.Register<ZPackage>("MarkedLocationResponse", RPC_MarkedLocationResponse);
            }
        }

        public static void EnterSearchMode()
        {
            if (!ZNet.instance.IsServer())
            {
                MarkedLocationRequest();
                return;
            }

            if (!Player.m_localPlayer)
                return;

            GetActivatedWaystones(Player.m_localPlayer.GetPlayerID());
            DirectionSearch.Enter();
        }

        public static void MarkedLocationRequest()
        {
            LogInfo($"Marked location request");

            ZRoutedRpc.instance.InvokeRoutedRPC("MarkedLocationRequest", Player.m_localPlayer.GetPlayerID());
        }

        public static List<Tuple<string, Vector3, Quaternion>> GetActivatedWaystones(long playerID)
        {
            activatedWaystones.Clear();
            foreach (ZDO zdo in waystoneObjects)
                if (WaystoneSmall.IsWaystoneActivated(zdo, playerID))
                {
                    string tag = zdo.GetString(ZDOVars.s_tag);
                    if (string.IsNullOrWhiteSpace(tag))
                        continue;

                    Vector3 position = zdo.GetPosition();
                    Quaternion rotation = zdo.GetRotation();
                    Vector3 vector = rotation * Vector3.forward;
                    Vector3 pos = position + vector * 1 + Vector3.up;

                    activatedWaystones.Add(Tuple.Create(tag, pos, rotation * Quaternion.Euler(0, 180f, 0)));
                }

            return activatedWaystones;
        }

        public static void RPC_MarkedLocationRequest(long sender, long playerID)
        {
            // Server
            GetActivatedWaystones(playerID);

            ZPackage zPackage = new();
            zPackage.Write(activatedWaystones.Count);

            foreach (Tuple<string, Vector3, Quaternion> waystone in activatedWaystones)
            {
                zPackage.Write(waystone.Item1);
                zPackage.Write(waystone.Item2);
                zPackage.Write(waystone.Item3);
            }

            ZRoutedRpc.instance.InvokeRoutedRPC(sender, "MarkedLocationResponse", zPackage);
        }

        public static void RPC_MarkedLocationResponse(long sender, ZPackage pkg)
        {
            LogInfo($"Server responded with activated location list");

            activatedWaystones.Clear();
            int num = pkg.ReadInt();
            for (int i = 0; i < num; i++)
                activatedWaystones.Add(Tuple.Create(pkg.ReadString(), pkg.ReadVector3(), pkg.ReadQuaternion()));

            DirectionSearch.Enter();
        }

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.Load))]
        public static class ZDOMan_Load_WaystoneListInit
        {
            private static void Postfix(ZDOMan __instance)
            {
                foreach (KeyValuePair<ZDOID, ZDO> item in __instance.m_objectsByID)
                    if (item.Value.GetPrefab() == PieceWaystone.waystoneHash)
                        waystoneObjects.Add(item.Value);
            }
        }

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.CreateNewZDO), new Type[3] { typeof(ZDOID), typeof(Vector3), typeof(int) })]
        public static class ZDOMan_CreateNewZDO_WaystoneListAddNew
        {
            private static void Postfix(int prefabHashIn, ZDO __result)
            {
                if (((prefabHashIn != 0) ? prefabHashIn : __result.GetPrefab()) == PieceWaystone.waystoneHash)
                    waystoneObjects.Add(__result);
            }
        }

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.HandleDestroyedZDO))]
        public static class ZDOMan_HandleDestroyedZDO_WaystoneListRemove
        {
            private static void Prefix(ZDOMan __instance, ZDOID uid)
            {
                ZDO zDO = __instance.GetZDO(uid);
                if (zDO == null)
                    return;

                if (zDO.GetPrefab() == PieceWaystone.waystoneHash)
                    waystoneObjects.Remove(zDO);
            }
        }

        [HarmonyPatch(typeof(ZDO), nameof(ZDO.Deserialize))]
        public static class ZDO_Deserialize_WaystoneListAdd
        {
            private static void Postfix(ZDO __instance)
            {
                if (__instance.GetPrefab() == PieceWaystone.waystoneHash)
                    waystoneObjects.Add(__instance);
            }
        }
    }
}