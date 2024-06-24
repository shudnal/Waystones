using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using static PocketTeleporter.PocketTeleporter;

namespace PocketTeleporter
{
    [Serializable]
    public class CooldownData
    {
        [Serializable]
        public class WorldCooldownData
        {
            public long worldUID;
            public string globalTime;
            public double worldTime;

            public double GetCooldown()
            {
                if (!ZNet.instance)
                    return 0;

                if (cooldownTime.Value == CooldownTime.WorldTime)
                    return worldTime == 0 ? 0 : Math.Max(worldTime - ZNet.instance.GetTimeSeconds(), 0);
                else if (DateTime.TryParse(globalTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time))
                    return Math.Max((time - GetTime()).TotalSeconds, 0);

                return 0;
            }

            public bool IsOnCooldown()
            {
                return GetCooldown() > 0;
            }

            public void SetCooldown(double cooldown)
            {
                if (cooldownTime.Value == CooldownTime.GlobalTime)
                    globalTime = GetTime().AddSeconds(cooldown).ToString(CultureInfo.InvariantCulture);
                else
                    worldTime = ZNet.instance.GetTimeSeconds() + cooldown;
            }
        }

        public List<WorldCooldownData> worlds = new List<WorldCooldownData>();

        internal WorldCooldownData GetWorldData(long uid, bool createIfEmpty = false)
        {
            WorldCooldownData data = worlds.Find(d => d.worldUID == uid);
            if (createIfEmpty && data == null)
            {
                data = new WorldCooldownData
                {
                    worldUID = ZNet.instance.GetWorldUID()
                };

                worlds.Add(data);
            }

            return data;
        }

        internal static void SetCooldown(int cooldown)
        {
            CooldownData state = GetState();

            state.GetWorldData(ZNet.instance.GetWorldUID(), createIfEmpty: true).SetCooldown(cooldown);

            Player.m_localPlayer.m_customData[customDataKey] = JsonUtility.ToJson(state);
        }

        internal static bool IsOnCooldown()
        {
            WorldCooldownData data = GetState().GetWorldData(ZNet.instance.GetWorldUID());
            return data != null && data.IsOnCooldown();
        }

        internal static string GetCooldownString()
        {
            WorldCooldownData data = GetState().GetWorldData(ZNet.instance.GetWorldUID());
            return data == null ? "" : TimerString(data.GetCooldown());
        }

        private static string TimerString(double seconds)
        {
            if (seconds < 60)
                return DateTime.FromBinary(599266080000000000).AddSeconds(seconds).ToString(@"ss\s");

            TimeSpan span = TimeSpan.FromSeconds(seconds);
            if (span.Hours > 0)
                return $"{(int)span.TotalHours}{new DateTime(span.Ticks).ToString(@"\h mm\m")}";
            else
                return new DateTime(span.Ticks).ToString(@"mm\m ss\s");
        }

        private static DateTime GetTime()
        {
            return DateTime.Now.ToUniversalTime();
        }

        private static CooldownData GetState()
        {
            return Player.m_localPlayer.m_customData.TryGetValue(customDataKey, out string json) ? JsonUtility.FromJson<CooldownData>(json) : new CooldownData();
        }
    }
}
