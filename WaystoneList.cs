using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using static Waystones.Waystones;

namespace Waystones
{
    internal static class WaystoneList
    {
        public class WaystoneData
        {
            public string tag;
            public Vector3 searchPosition;
            public Quaternion searchRotation;
            public Vector3 worldPosition;
            public int charge;
        }

        public static Sprite iconWaystone;
        public static readonly List<Minimap.PinData> waystonePins = new();
        public static readonly List<WaystoneData> activatedWaystones = new();
        private static WaystoneData searchSourceWaystone;

        public static readonly HashSet<ZDO> waystoneObjects = new();

        public const string customDataKey = "WaystoneList";
        public const string chargeZdoKey = "WaystoneCharges";
        private const float waystoneSearchPointOffset = 1f;
        private const float defaultWaystoneMatchDistance = 8f;
        private const string markedLocationRequestRpc = "MarkedLocationRequest";
        private const string markedLocationResponseRpc = "MarkedLocationResponse";
        private const string consumeWaystoneChargeRequestRpc = "ConsumeWaystoneChargeRequest";

        public static void UpdatePins()
        {
            foreach (Minimap.PinData pin in waystonePins)
                Minimap.instance.RemovePin(pin);

            waystonePins.Clear();
            if (locationWaystonesShowOnMap.Value && DirectionSearch.IsActivated)
                foreach (WaystoneData waystone in activatedWaystones)
                    waystonePins.Add(Minimap.instance.AddPin(waystone.searchPosition, (Minimap.PinType)WaystoneIconType.pinType, waystone.tag, save: false, isChecked: false, Player.m_localPlayer.GetPlayerID()));
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

                DirectionSearch.InitializeDirectionIcons(__instance);
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
                activatedWaystones.Clear();
                searchSourceWaystone = null;
            }
        }

        internal static void RegisterRPCs()
        {
            if (ZNet.instance.IsServer())
            {
                ZRoutedRpc.instance.Register<ZPackage>(markedLocationRequestRpc, RPC_MarkedLocationRequest);
                ZRoutedRpc.instance.Register<ZPackage>(consumeWaystoneChargeRequestRpc, RPC_ConsumeWaystoneChargeRequest);
            }
            else
            {
                ZRoutedRpc.instance.Register<ZPackage>(markedLocationResponseRpc, RPC_MarkedLocationResponse);
            }
        }

        public static void EnterSearchMode(ZDO sourceZdo = null)
        {
            Player player = Player.m_localPlayer;
            if (!player || ZNet.instance == null)
                return;

            if (!ZNet.instance.IsServer())
            {
                MarkedLocationRequest(sourceZdo);
                return;
            }

            GetActivatedWaystones(player.GetPlayerID());
            searchSourceWaystone = CreateSearchSourceWaystone(sourceZdo, player.transform.position);
            DirectionSearch.Enter();
        }

        public static void MarkedLocationRequest(ZDO sourceZdo = null)
        {
            Player player = Player.m_localPlayer;
            if (!player)
                return;

            LogInfo("Marked location request");

            ZPackage pkg = new();
            pkg.Write(player.GetPlayerID());
            pkg.Write(player.transform.position);

            bool hasSource = sourceZdo != null;
            pkg.Write(hasSource ? 1 : 0);
            if (hasSource)
                pkg.Write(sourceZdo.GetPosition());

            ZRoutedRpc.instance.InvokeRoutedRPC(markedLocationRequestRpc, pkg);
        }

        public static List<WaystoneData> GetActivatedWaystones(long playerID)
        {
            activatedWaystones.Clear();
            waystoneObjects.RemoveWhere(zdo => zdo == null);

            foreach (ZDO zdo in waystoneObjects)
            {
                if (!WaystoneSmall.IsWaystoneActivated(zdo, playerID))
                    continue;

                string tag = zdo.GetString(ZDOVars.s_tag);
                if (string.IsNullOrWhiteSpace(tag))
                    continue;

                activatedWaystones.Add(CreateWaystoneData(zdo, tag));
            }

            return activatedWaystones;
        }

        private static WaystoneData CreateWaystoneData(ZDO zdo, string tag)
        {
            if (zdo == null)
                return null;

            Vector3 position = zdo.GetPosition();
            Quaternion rotation = zdo.GetRotation();
            Vector3 forward = rotation * Vector3.forward;

            return new WaystoneData
            {
                tag = tag,
                worldPosition = position,
                searchPosition = position + forward * waystoneSearchPointOffset + Vector3.up,
                searchRotation = rotation * Quaternion.Euler(0, 180f, 0),
                charge = GetWaystoneCharge(zdo)
            };
        }

        private static WaystoneData CreateSearchSourceWaystone(ZDO sourceZdo, Vector3 playerPosition)
        {
            sourceZdo ??= GetClosestWaystone(playerPosition);
            return CreateWaystoneData(sourceZdo, sourceZdo == null ? "" : sourceZdo.GetString(ZDOVars.s_tag));
        }

        public static WaystoneData GetSearchSourceWaystone()
        {
            return searchSourceWaystone;
        }

        public static WaystoneData GetClosestActivatedWaystoneData(Vector3 point, float maxDistance = defaultWaystoneMatchDistance)
        {
            WaystoneData result = null;
            float closest = maxDistance;

            foreach (WaystoneData data in activatedWaystones)
            {
                float distance = Utils.DistanceXZ(point, data.worldPosition);
                if (distance <= closest)
                {
                    closest = distance;
                    result = data;
                }
            }

            return result;
        }

        public static int GetClosestActivatedWaystoneCharge(long playerID, Vector3 point, float maxDistance = defaultWaystoneMatchDistance)
        {
            WaystoneData data = GetClosestActivatedWaystoneData(point, maxDistance);
            if (data != null)
                return data.charge;

            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                ZDO zdo = GetClosestWaystone(point, maxDistance);
                return zdo == null ? 0 : GetWaystoneCharge(zdo);
            }

            return 0;
        }

        public static bool IsPlayerChargeStorage()
        {
            return waystoneChargeStorage.Value == ChargeStorage.Player;
        }

        public static int GetDefaultWaystoneCharge()
        {
            return WorldData.GetDefaultCharge();
        }

        public static int GetCurrentTravelCharge(long playerID, Vector3 point, float maxDistance = defaultWaystoneMatchDistance)
        {
            return IsPlayerChargeStorage()
                ? WorldData.GetPlayerCharge()
                : GetClosestActivatedWaystoneCharge(playerID, point, maxDistance);
        }

        public static int GetWaystoneCharge(ZDO zdo)
        {
            if (zdo == null)
                return 0;

            int charge = zdo.GetInt(chargeZdoKey, GetDefaultWaystoneCharge());
            return allowWaystoneChargeOverflow.Value ? charge : Mathf.Min(charge, WorldData.MaxWaystoneCharge);
        }

        public static int GetPotentialChargeAdded(ZDO zdo, int amount)
        {
            return IsPlayerChargeStorage()
                ? WorldData.GetPotentialPlayerChargeAdded(amount)
                : GetPotentialWaystoneChargeAdded(zdo, amount);
        }

        public static int GetPotentialWaystoneChargeAdded(ZDO zdo, int amount)
        {
            if (zdo == null || amount <= 0)
                return 0;

            int current = GetWaystoneCharge(zdo);
            int max = WorldData.MaxWaystoneCharge;

            if (allowWaystoneChargeOverflow.Value)
                return current < max ? amount : 0;

            int next = Mathf.Min(current + amount, max);
            return Mathf.Max(0, next - current);
        }

        public static bool CanStartSearchWithCharge(int current)
        {
            return HasEnoughCharge(current, WorldData.MinWaystoneChargeCost, allowWaystoneChargeOverdraft.Value);
        }

        public static bool HasEnoughTravelCharge(WaystoneData sourceWaystone, int amount, bool allowOverdraftWhenPositive)
        {
            int current = IsPlayerChargeStorage() ? WorldData.GetPlayerCharge() : sourceWaystone == null ? 0 : sourceWaystone.charge;
            return HasEnoughCharge(current, amount, allowOverdraftWhenPositive);
        }

        public static bool HasEnoughWaystoneCharge(WaystoneData waystone, int amount, bool allowOverdraftWhenPositive)
        {
            return waystone != null && HasEnoughCharge(waystone.charge, amount, allowOverdraftWhenPositive);
        }

        public static bool HasEnoughWaystoneCharge(int current, int amount, bool allowOverdraftWhenPositive)
        {
            return HasEnoughCharge(current, amount, allowOverdraftWhenPositive);
        }

        public static bool TryConsumeTravelCharge(WaystoneData sourceWaystone, long playerID, int amount, bool allowOverdraftWhenPositive = false)
        {
            return IsPlayerChargeStorage()
                ? WorldData.TryConsumePlayerCharge(amount, allowOverdraftWhenPositive)
                : TryConsumeWaystoneCharge(sourceWaystone, playerID, amount, allowOverdraftWhenPositive);
        }

        public static bool TryConsumeWaystoneCharge(WaystoneData waystone, long playerID, int amount, bool allowOverdraftWhenPositive = false)
        {
            if (waystone == null || amount <= 0)
                return false;

            if (!HasEnoughCharge(waystone.charge, amount, allowOverdraftWhenPositive))
                return false;

            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                ZDO zdo = GetClosestWaystone(waystone.worldPosition, 2f);
                if (!TryConsumeWaystoneCharge(zdo, amount, allowOverdraftWhenPositive))
                    return false;

                waystone.charge = GetWaystoneCharge(zdo);
                return true;
            }

            waystone.charge -= amount;
            ConsumeWaystoneChargeRequest(playerID, waystone.worldPosition, amount, allowOverdraftWhenPositive);
            return true;
        }

        public static bool TryConsumeWaystoneCharge(ZDO zdo, int amount, bool allowOverdraftWhenPositive = false)
        {
            if (zdo == null || amount <= 0)
                return false;

            int current = GetWaystoneCharge(zdo);
            if (!HasEnoughCharge(current, amount, allowOverdraftWhenPositive))
                return false;

            zdo.Set(chargeZdoKey, current - amount);
            return true;
        }

        private static bool HasEnoughCharge(int current, int amount, bool allowOverdraftWhenPositive)
        {
            return current >= amount || allowOverdraftWhenPositive && current > 0;
        }

        public static int AddCharge(ZDO zdo, int amount)
        {
            return IsPlayerChargeStorage()
                ? WorldData.AddPlayerCharge(amount)
                : AddWaystoneCharge(zdo, amount);
        }

        public static int AddWaystoneCharge(ZDO zdo, int amount)
        {
            int added = GetPotentialWaystoneChargeAdded(zdo, amount);
            if (added <= 0)
                return 0;

            zdo.Set(chargeZdoKey, GetWaystoneCharge(zdo) + added);
            return added;
        }

        public static ZDO GetClosestActivatedWaystone(long playerID, Vector3 point, float maxDistance = defaultWaystoneMatchDistance)
        {
            ZDO result = null;
            float closest = maxDistance;

            foreach (ZDO zdo in waystoneObjects)
            {
                if (!WaystoneSmall.IsWaystoneActivated(zdo, playerID))
                    continue;

                float distance = Utils.DistanceXZ(point, zdo.GetPosition());
                if (distance <= closest)
                {
                    closest = distance;
                    result = zdo;
                }
            }

            return result;
        }

        public static ZDO GetClosestWaystone(Vector3 point, float maxDistance = defaultWaystoneMatchDistance)
        {
            ZDO result = null;
            float closest = maxDistance;

            waystoneObjects.RemoveWhere(zdo => zdo == null);

            foreach (ZDO zdo in waystoneObjects)
            {
                float distance = Utils.DistanceXZ(point, zdo.GetPosition());
                if (distance <= closest)
                {
                    closest = distance;
                    result = zdo;
                }
            }

            return result;
        }

        private static void ConsumeWaystoneChargeRequest(long playerID, Vector3 worldPosition, int amount, bool allowOverdraftWhenPositive)
        {
            ZPackage pkg = new();
            pkg.Write(playerID);
            pkg.Write(worldPosition);
            pkg.Write(amount);
            pkg.Write(allowOverdraftWhenPositive ? 1 : 0);

            ZRoutedRpc.instance.InvokeRoutedRPC(consumeWaystoneChargeRequestRpc, pkg);
        }

        public static void RPC_ConsumeWaystoneChargeRequest(long sender, ZPackage pkg)
        {
            long playerID = pkg.ReadLong();
            Vector3 worldPosition = pkg.ReadVector3();
            int amount = pkg.ReadInt();
            pkg.ReadInt();
            bool allowOverdraftWhenPositive = allowWaystoneChargeOverdraft.Value;

            ZDO zdo = GetClosestWaystone(worldPosition, 2f);
            if (!TryConsumeWaystoneCharge(zdo, amount, allowOverdraftWhenPositive))
                LogInfo($"Rejected waystone charge consume request from {sender}. Player: {playerID}, amount: {amount}, position: {worldPosition}");
        }

        public static void RPC_MarkedLocationRequest(long sender, ZPackage request)
        {
            long playerID = request.ReadLong();
            Vector3 playerPosition = request.ReadVector3();

            ZDO sourceZdo = null;
            bool hasSource = request.ReadInt() != 0;
            if (hasSource)
                sourceZdo = GetClosestWaystone(request.ReadVector3(), 2f);

            GetActivatedWaystones(playerID);
            WaystoneData source = CreateSearchSourceWaystone(sourceZdo, playerPosition);

            ZPackage response = new();
            response.Write(activatedWaystones.Count);

            foreach (WaystoneData waystone in activatedWaystones)
                WriteWaystoneData(response, waystone);

            response.Write(source != null ? 1 : 0);
            if (source != null)
                WriteWaystoneData(response, source);

            ZRoutedRpc.instance.InvokeRoutedRPC(sender, markedLocationResponseRpc, response);
        }

        public static void RPC_MarkedLocationResponse(long sender, ZPackage pkg)
        {
            LogInfo("Server responded with activated location list");

            activatedWaystones.Clear();
            int num = pkg.ReadInt();
            for (int i = 0; i < num; i++)
                activatedWaystones.Add(ReadWaystoneData(pkg));

            searchSourceWaystone = pkg.ReadInt() != 0 ? ReadWaystoneData(pkg) : null;

            DirectionSearch.Enter();
        }

        private static void WriteWaystoneData(ZPackage pkg, WaystoneData waystone)
        {
            pkg.Write(waystone.tag ?? "");
            pkg.Write(waystone.searchPosition);
            pkg.Write(waystone.searchRotation);
            pkg.Write(waystone.worldPosition);
            pkg.Write(waystone.charge);
        }

        private static WaystoneData ReadWaystoneData(ZPackage pkg)
        {
            return new WaystoneData
            {
                tag = pkg.ReadString(),
                searchPosition = pkg.ReadVector3(),
                searchRotation = pkg.ReadQuaternion(),
                worldPosition = pkg.ReadVector3(),
                charge = pkg.ReadInt()
            };
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
