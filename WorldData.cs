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

        public const string customDataKey = "Waystones";
        
        public static bool saveNextGroundPositionAsShipLocation;

        public static List<DirectionSearch.Direction> GetSavedDirections()
        {
            List<DirectionSearch.Direction> result = new();

            WorldData data = GetWorldData(GetState());
            if (data == null)
                return result;

            if (data.lastShip != null && data.lastShip != Vector3.zero && locationShowLastShip.Value)
                result.Add(new DirectionSearch.Direction("$ws_location_last_ship", data.lastShip));

            if (data.lastPosition != null && data.lastPosition != Vector3.zero && locationShowLastPoint.Value)
                result.Add(new DirectionSearch.Direction("$ws_location_last_location", data.lastPosition));

            if (locationShowWaystones.Value)
                WaystoneList.activatedWaystones.Do(waystone => result.Add(new DirectionSearch.Direction($"$ws_piece_waystone_name \"{waystone.Item1}\"", waystone.Item2, waystone.Item3)));

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
