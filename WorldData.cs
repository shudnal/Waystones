using System;
using System.Collections.Generic;
using System.Globalization;
using static Waystones.Waystones;
using HarmonyLib;
using UnityEngine;
using System.Text;

namespace Waystones
{
    [Serializable]
    public class WorldData
    {
        public long worldUID;
        public string globalTime;
        public double worldTime;
        public Vector3 lastShip = Vector3.zero;
        public Vector3 lastPosition = Vector3.zero;
        public bool playerChargeInitialized;
        public int playerCharge;

        public const string customDataKey = "Waystones";
        public static int MaxWaystoneCharge => Mathf.Clamp(maxWaystoneCharge.Value, 1, 100000);
        public static int MinWaystoneChargeCost => Mathf.Clamp(chargeCostMinimum.Value, 1, 100000);
        public static int MaxWaystoneChargeCost => Mathf.Max(MinWaystoneChargeCost, Mathf.Clamp(chargeCostMaximum.Value, 1, 100000));
        public static int MinWaystoneChargeDistance => Mathf.Clamp(chargeDistanceMinimum.Value, 1, 100000);
        public static int MaxWaystoneChargeDistance => Mathf.Max(MinWaystoneChargeDistance, Mathf.Clamp(chargeDistanceMaximum.Value, 1, 100000));

        public static bool saveNextGroundPositionAsShipLocation;

        public static List<DirectionSearch.Direction> GetSavedDirections()
        {
            List<DirectionSearch.Direction> result = new();

            WorldData data = GetWorldData(GetState());
            if (data != null)
            {
                if (data.lastShip != Vector3.zero && locationShowLastShip.Value)
                    result.Add(new DirectionSearch.Direction("$ws_location_last_ship", data.lastShip, DirectionSearch.DirectionIconType.Point));

                if (data.lastPosition != Vector3.zero && locationShowLastPoint.Value)
                    result.Add(new DirectionSearch.Direction("$ws_location_last_location", data.lastPosition, DirectionSearch.DirectionIconType.Point));
            }

            if (locationShowWaystones.Value)
                WaystoneList.activatedWaystones.Do(waystone => result.Add(new DirectionSearch.Direction($"$ws_piece_waystone_name \"{waystone.tag}\"", waystone)));

            return result;
        }

        public static void SaveLastPosition(Vector3 position)
        {
            List<WorldData> state = GetState();

            GetWorldData(state, createIfEmpty: true).lastPosition = position;

            Player.m_localPlayer.m_customData[customDataKey] = SaveWorldDataList(state);

            LogInfo("Last teleport location saved: " + position);
        }

        public static void SaveLastShip(Vector3 position)
        {
            List<WorldData> state = GetState();

            GetWorldData(state, createIfEmpty: true).lastShip = position;

            Player.m_localPlayer.m_customData[customDataKey] = SaveWorldDataList(state);

            LogInfo("Last ship location saved: " + position);
        }

        private double GetCooldownTime()
        {
            if (!ZNet.instance)
                return 0;

            if (cooldownTime.Value == CooldownTime.WorldTime)
                return worldTime == 0 ? 0 : Math.Max(worldTime - ZNet.instance.GetTimeSeconds(), 0);
            else if (DateTime.TryParse(globalTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time))
                return Math.Max((time - GetTime()).TotalSeconds, 0);

            return 0;
        }

        private void SetCooldownTime(double cooldown)
        {
            if (cooldownTime.Value == CooldownTime.GlobalTime)
                globalTime = GetTime().AddSeconds(cooldown).ToString(CultureInfo.InvariantCulture);
            else
                worldTime = ZNet.instance.GetTimeSeconds() + cooldown;
        }

        public static double GetCooldownTimeToTarget(Vector3 target)
        {
            // Random point
            if (target == Vector3.zero)
                return cooldownMinimum.Value;

            float distance = Utils.DistanceXZ(Player.m_localPlayer.transform.position, target);
            if (distance < cooldownDistanceMinimum.Value)
                return cooldownMinimum.Value;
            else if (distance > cooldownDistanceMaximum.Value)
                return cooldownMaximum.Value;

            return Mathf.Lerp(cooldownMinimum.Value, cooldownMaximum.Value, (distance - cooldownDistanceMinimum.Value) / (cooldownDistanceMaximum.Value - cooldownDistanceMinimum.Value));
        }

        public static int GetTravelChargeCost(Vector3 from, Vector3 target)
        {
            int minCost = MinWaystoneChargeCost;
            int maxCost = MaxWaystoneChargeCost;

            if (target == Vector3.zero)
                return minCost;

            float distance = Utils.DistanceXZ(from, target);
            int minDistance = MinWaystoneChargeDistance;
            int maxDistance = MaxWaystoneChargeDistance;

            if (distance < minDistance)
                return minCost;
            if (distance > maxDistance)
                return maxCost;
            if (maxDistance == minDistance)
                return maxCost;

            float t = (distance - minDistance) / (maxDistance - minDistance);
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(minCost, maxCost, t)), minCost, maxCost);
        }

        public static int GetDefaultCharge()
        {
            return defaultWaystoneChargeFull.Value ? MaxWaystoneCharge : 0;
        }

        private int GetStoredPlayerCharge()
        {
            return playerChargeInitialized ? playerCharge : GetDefaultCharge();
        }

        private static int NormalizeStoredCharge(int charge)
        {
            return allowWaystoneChargeOverflow.Value ? charge : Mathf.Min(charge, MaxWaystoneCharge);
        }

        public static int GetPlayerCharge()
        {
            if (!Player.m_localPlayer || !ZNet.instance)
                return 0;

            WorldData data = GetWorldData(GetState());
            return data == null ? GetDefaultCharge() : NormalizeStoredCharge(data.GetStoredPlayerCharge());
        }

        public static int GetPotentialPlayerChargeAdded(int amount)
        {
            if (amount <= 0)
                return 0;

            int current = GetPlayerCharge();
            if (allowWaystoneChargeOverflow.Value)
                return amount;

            int next = Mathf.Min(current + amount, MaxWaystoneCharge);
            return Mathf.Max(0, next - current);
        }

        public static int AddPlayerCharge(int amount)
        {
            int added = GetPotentialPlayerChargeAdded(amount);
            if (added <= 0 || !Player.m_localPlayer || !ZNet.instance)
                return 0;

            List<WorldData> state = GetState();
            WorldData data = GetWorldData(state, createIfEmpty: true);
            int current = NormalizeStoredCharge(data.GetStoredPlayerCharge());

            data.playerChargeInitialized = true;
            data.playerCharge = current + added;
            Player.m_localPlayer.m_customData[customDataKey] = SaveWorldDataList(state);

            LogInfo($"Player waystone charge added: +{added}, current: {data.playerCharge}");
            return added;
        }

        public static bool TryConsumePlayerCharge(int amount, bool allowOverdraftWhenPositive)
        {
            if (amount <= 0 || !Player.m_localPlayer || !ZNet.instance)
                return false;

            List<WorldData> state = GetState();
            WorldData data = GetWorldData(state, createIfEmpty: true);
            int current = NormalizeStoredCharge(data.GetStoredPlayerCharge());

            if (current < amount && !(allowOverdraftWhenPositive && current > 0))
                return false;

            data.playerChargeInitialized = true;
            data.playerCharge = current - amount;
            Player.m_localPlayer.m_customData[customDataKey] = SaveWorldDataList(state);

            LogInfo($"Player waystone charge consumed: -{amount}, current: {data.playerCharge}");
            return true;
        }

        private static DateTime GetTime()
        {
            return DateTime.Now.ToUniversalTime();
        }

        internal static WorldData GetWorldData(List<WorldData> state, bool createIfEmpty = false)
        {
            long uid = ZNet.instance.GetWorldUID();
            WorldData data = state.Find(d => d.worldUID == uid);
            if (createIfEmpty && data == null)
            {
                data = new WorldData
                {
                    worldUID = uid
                };

                state.Add(data);
            }

            return data;
        }

        public static void SetCooldown(double cooldown)
        {
            if (!ZNet.instance)
                return;

            List<WorldData> state = GetState();

            GetWorldData(state, createIfEmpty: true).SetCooldownTime(cooldown);

            Player.m_localPlayer.m_customData[customDataKey] = SaveWorldDataList(state);

            LogInfo($"Cooldown set {TimerString(cooldown)}");
        }

        public static bool TryReduceCooldown(int seconds)
        {
            if (!ZNet.instance)
                return false;

            List<WorldData> state = GetState();

            WorldData data = GetWorldData(state);
            if (data == null)
                return false;

            data.SetCooldownTime(Math.Max(data.GetCooldownTime() - seconds, 0));

            Player.m_localPlayer.m_customData[customDataKey] = SaveWorldDataList(state);

            LogInfo($"Cooldown set {TimerString(data.GetCooldownTime())}");
            return true;
        }

        internal static bool IsOnCooldown()
        {
            WorldData data = GetWorldData(GetState());
            return data != null && data.GetCooldownTime() > 0;
        }

        internal static string GetCooldownString()
        {
            WorldData data = GetWorldData(GetState());
            return data == null ? "" : TimerString(data.GetCooldownTime());
        }

        public static string TimerString(double seconds)
        {
            if (seconds < 60)
                return DateTime.FromBinary(599266080000000000).AddSeconds(seconds).ToString(@"ss\s");

            TimeSpan span = TimeSpan.FromSeconds(seconds);
            if (span.Hours > 0)
                return $"{(int)span.TotalHours}{new DateTime(span.Ticks).ToString(@"\h mm\m")}";
            else if (span.Seconds == 0)
                return new DateTime(span.Ticks).ToString(@"mm\m");
            else
                return new DateTime(span.Ticks).ToString(@"mm\m ss\s");
        }

        private static List<WorldData> GetState()
        {
            return Player.m_localPlayer.m_customData.TryGetValue(customDataKey, out string value) ? GetWorldDataList(value) : new List<WorldData>();
        }

        private static List<WorldData> GetWorldDataList(string value)
        {
            List<WorldData> data = new();
            SplitToLines(value).Do(line => data.Add(JsonUtility.FromJson<WorldData>(line)));
            return data;
        }

        private static string SaveWorldDataList(List<WorldData> list)
        {
            StringBuilder sb = new();
            list.Do(data => sb.AppendLine(JsonUtility.ToJson(data)));
            return sb.ToString();
        }

        private static IEnumerable<string> SplitToLines(string input)
        {
            if (input == null)
            {
                yield break;
            }

            using (System.IO.StringReader reader = new(input))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }

        [HarmonyPatch(typeof(Ship), nameof(Ship.OnTriggerExit))]
        public static class Ship_OnTriggerExit_LastShipPosition
        {
            private static void Prefix(Ship __instance, Collider collider)
            {
                if (Player.m_localPlayer && Player.m_localPlayer == collider.GetComponent<Player>() && Ship.s_currentShips.Contains(__instance))
                    saveNextGroundPositionAsShipLocation = true;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.FixedUpdate))]
        public static class Player_FixedUpdate_SaveLastShipPosition
        {
            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer)
                    return;

                if (saveNextGroundPositionAsShipLocation && __instance.IsOnGround() && !__instance.InWater() && __instance.GetStandingOnShip() == null)
                {
                    saveNextGroundPositionAsShipLocation = false;
                    SaveLastShip(Player.m_localPlayer.transform.position);
                }
            }
        }
    }
}
