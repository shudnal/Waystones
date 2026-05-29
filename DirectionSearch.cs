using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using static Waystones.Waystones;

namespace Waystones
{
    public static class DirectionSearch
    {
        public class Direction
        {
            public string name;
            public Vector3 position;
            public Quaternion rotation;
            public double cooldown;
            public int travelCost;
            public int arrivalCharge;
            private const int NoArrivalCharge = int.MinValue;
            private readonly WaystoneList.WaystoneData waystone;

            public Direction(string name, Vector3 position)
                : this(name, position, Quaternion.identity, null)
            {
            }

            public Direction(string name, Vector3 position, Quaternion rotation)
                : this(name, position, rotation, null)
            {
            }

            internal Direction(string name, WaystoneList.WaystoneData waystone)
                : this(name, waystone.searchPosition, waystone.searchRotation, waystone)
            {
            }

            private Direction(string name, Vector3 position, Quaternion rotation, WaystoneList.WaystoneData waystone)
            {
                this.name = name;
                this.position = position;
                this.rotation = rotation;
                this.waystone = waystone;
                cooldown = WorldData.GetCooldownTimeToTarget(position);

                Vector3 from = Player.m_localPlayer ? Player.m_localPlayer.transform.position : Vector3.zero;
                travelCost = WorldData.GetTravelChargeCost(from, position);
                arrivalCharge = waystone == null ? NoArrivalCharge : waystone.charge;
            }

            public static readonly StringBuilder _sb = new(5);

            public string GetHoverText()
            {
                _sb.Clear();
                _sb.Append(name);

                if (waystoneMode.Value == WaystoneMode.Cooldown)
                {
                    _sb.Append($"\n[<color=yellow><b>$KEY_Use</b></color>] $ws_tooltip_moving_to");
                    _sb.Append($"\n\n$ws_tooltip_cooldown_after <color=#add8e6>{WorldData.TimerString(cooldown)}</color>");
                }
                else if (waystoneMode.Value == WaystoneMode.Charge)
                {
                    _sb.Append($"\n[<color=yellow><b>$KEY_Use</b></color>] $ws_tooltip_moving_to");
                    _sb.Append($"\n\n$ws_tooltip_travel_cost <color=#add8e6>{travelCost}</color>");
                    if (WaystoneList.IsPlayerChargeStorage())
                    {
                        int currentCharge = WorldData.GetPlayerCharge();
                        _sb.Append($"\n$ws_tooltip_player_charge <color=#add8e6>{currentCharge}</color>");
                        _sb.Append($"\n$ws_tooltip_player_charge_after <color=#add8e6>{currentCharge - travelCost}</color>");
                    }
                    else if (arrivalCharge != NoArrivalCharge)
                    {
                        _sb.Append($"\n$ws_tooltip_arrival_charge <color=#add8e6>{arrivalCharge}</color>");
                        bool canReturn = WaystoneList.HasEnoughWaystoneCharge(arrivalCharge, travelCost, allowWaystoneChargeOverdraft.Value);
                        _sb.Append($"\n$ws_tooltip_return_check <color=#add8e6>{(canReturn ? "$menu_yes" : "$menu_no")}</color>");
                    }
                }
                else
                {
                    float distance = Utils.DistanceXZ(Player.m_localPlayer.transform.position, position);
                    string distanceString = FormatUnits(distance, orientationDistanceUnit.Value == DistanceUnit.Yards ? " yd" : "m");
                    _sb.Append($"\n$ws_tooltip_distance <color=#add8e6>{distanceString}</color>");
                }
                return Localization.instance.Localize(_sb.ToString());
            }

            private static string FormatUnits(float value, string unit)
            {
                if (value < 1000f)
                    return $"{Mathf.RoundToInt(value)}{unit}";

                float kilo = value / 1000f;

                return kilo >= 10f
                    ? $"{kilo:0}k{unit}"
                    : $"{kilo:0.#}k{unit}";
            }
        }

        private static List<Direction> directions = new();
        private static Direction current;
        private static bool activated;
        private static readonly Direction placeOfMystery = new("$ws_location_random_point", Vector3.zero);
        private static float currentAngle;
        private static WaystoneList.WaystoneData sourceWaystone;

        private static float defaultFoV;
        private static float targetFoV;

        public static CanvasGroup screenBlackener;
        public static AudioSource screenBlackenerSfx;

        public static bool IsActivated { get { return activated; } }

        internal static void Toggle()
        {
            if (activated)
                Exit();
            else if (useShortcutToEnter.Value && CanCast() && WaystoneSmall.IsSearchAllowed(Player.m_localPlayer, validateCharge: false))
                WaystoneList.EnterSearchMode();
        }

        internal static void Enter()
        {
            Player player = Player.m_localPlayer;
            if (!CanCast() || !WaystoneSmall.IsSearchAllowed(player, validateCharge: false))
                return;

            sourceWaystone = WaystoneList.GetSearchSourceWaystone() ?? WaystoneList.GetClosestActivatedWaystoneData(player.transform.position);
            if (waystoneMode.Value == WaystoneMode.Charge && !WaystoneList.HasEnoughTravelCharge(sourceWaystone, WorldData.MinWaystoneChargeCost, allowWaystoneChargeOverdraft.Value))
            {
                sourceWaystone = null;
                player.Message(MessageHud.MessageType.Center, "$ws_message_not_enough_charge");
                return;
            }

            if (!activated)
            {
                Game.FadeTimeScale(slowFactorTime.Value, 4f);
                targetFoV = defaultFoV;

                LogInfo($"Search mode activated at {player.transform.position}");
            }

            FillDirections();
            activated = true;
            WaystoneList.UpdatePins();
        }

        internal static void Exit(bool force = false)
        {
            if (!CanCast() && !force)
                return;

            if (activated)
            {
                GameCamera.instance.m_fov = defaultFoV;

                if (Game.m_timeScale >= slowFactorTime.Value)
                    Game.FadeTimeScale(1f, 1f);

                if (waystoneMode.Value == WaystoneMode.Cooldown && !WorldData.IsOnCooldown())
                    WorldData.SetCooldown(cooldownSearchMode.Value);
               
                LogInfo($"Search mode ended");
            }

            activated = false;
            current = null;
            currentAngle = 0f;
            sourceWaystone = null;

            WaystoneList.UpdatePins();
        }

        internal static void FillDirections()
        {
            directions.Clear();

            if (locationShowCurrentSpawn.Value)
                directions.Add(new Direction("$ws_location_spawn_point", GetSpawnPoint()));

            ZoneSystem.instance.tempIconList.Clear();
            ZoneSystem.instance.GetLocationIcons(ZoneSystem.instance.tempIconList);
            foreach (KeyValuePair<Vector3, string> loc in ZoneSystem.instance.tempIconList)
            {
                if (loc.Value == "StartTemple" && locationShowStartTemple.Value)
                    directions.Add(new Direction("$ws_location_start_temple", loc.Key));
                else if (loc.Value == "Vendor_BlackForest" && locationShowHaldor.Value)
                    directions.Add(new Direction("$npc_haldor", loc.Key));
                else if (loc.Value == "Hildir_camp" && locationShowHildir.Value)
                    directions.Add(new Direction("$npc_hildir", loc.Key));
                else if (loc.Value == "BogWitch_Camp" && locationShowBogWitch.Value)
                    directions.Add(new Direction("$npc_bogwitch", loc.Key));
            }

            PlayerProfile profile = Game.instance.GetPlayerProfile();
            if (profile.HaveDeathPoint() && locationShowLastTombstone.Value)
                directions.Add(new Direction("$ws_location_last_tombstone", profile.GetDeathPoint()));

            directions.AddRange(WorldData.GetSavedDirections());

            directions.Do(d => LogInfo($"{Localization.instance.Localize(d.name)} {d.position} {WorldData.TimerString(d.cooldown)} {(Utils.DistanceXZ(Player.m_localPlayer.transform.position, d.position) < 10f ? "(filtered, too close)" : "")}"));

            directions.RemoveAll(d => Utils.DistanceXZ(Player.m_localPlayer.transform.position, d.position) < 10f);

            if (waystoneMode.Value == WaystoneMode.Orientation)
            {
                float maxDistance = orientationWaystoneVisibilityDistance.Value;
                directions.RemoveAll(d =>
                {
                    float distance = Utils.DistanceXZ(Player.m_localPlayer.transform.position, d.position);
                    if (distance <= maxDistance)
                        return false;

                    return d.name switch
                    {
                        "$ws_location_spawn_point" => !orientationShowDistantCurrentSpawn.Value,
                        "$ws_location_last_tombstone" => !orientationShowDistantLastTombstone.Value,
                        "$ws_location_last_ship" => !orientationShowDistantLastShip.Value,
                        "$ws_location_last_location" => !orientationShowDistantLastPoint.Value,
                        "$ws_location_start_temple" => !orientationShowDistantStartTemple.Value,
                        "$npc_haldor" => !orientationShowDistantHaldor.Value,
                        "$npc_hildir" => !orientationShowDistantHildir.Value,
                        "$npc_bogwitch" => !orientationShowDistantBogWitch.Value,
                        _ => d.name.Contains("$ws_piece_waystone_name") && !orientationShowDistantWaystones.Value
                    };
                });
            }
        }

        internal static Vector3 GetSpawnPoint()
        {
            PlayerProfile playerProfile = Game.instance.GetPlayerProfile();
            if (playerProfile.HaveCustomSpawnPoint())
            {
                return playerProfile.GetCustomSpawnPoint();
            }

            return playerProfile.GetHomePoint();
        }

        internal static void Update()
        {
            if (shortcut.Value.IsDown())
                Toggle();
            else if (activated
                && (ZInput.GetButtonDown("Block") ||
                    ZInput.GetButtonDown("JoyButtonB")))
            {
                Exit();
            }

            if (current != null && (ZInput.GetButton("Use") || ZInput.GetButton("JoyUse")) && CanCast())
            {
                Direction selected = current == placeOfMystery
                    ? new Direction("$ws_location_random_point", GetRandomPoint())
                    : current;

                if (waystoneMode.Value == WaystoneMode.Orientation)
                {
                    Exit();
                    return;
                }

                if (waystoneMode.Value == WaystoneMode.Charge)
                {
                    if (!WaystoneList.HasEnoughTravelCharge(sourceWaystone, selected.travelCost, allowWaystoneChargeOverdraft.Value))
                    {
                        MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "$ws_message_not_enough_charge");
                        return;
                    }
                }

                TeleportAttempt(selected.position, selected.rotation, selected.cooldown, selected.name, sourceWaystone, selected.travelCost);
                Exit();
            }

            if (activated && !CanCast())
                Exit(force: true);

            if (!activated)
                return;

            current = null;

            targetFoV -= Mathf.Clamp(ZInput.GetMouseScrollWheel(), -1f, 1f);
            if (ZInput.GetButton("JoyAltKeys") && !Hud.InRadial())
            {
                if (ZInput.GetButton("JoyCamZoomIn"))
                {
                    targetFoV -= 1f;
                }
                else if (ZInput.GetButton("JoyCamZoomOut"))
                {
                    targetFoV += 1f;
                }
            }

            targetFoV = Mathf.Clamp(targetFoV, defaultFoV - fovDelta.Value, defaultFoV + fovDelta.Value);

            GameCamera.instance.m_fov = Mathf.MoveTowards(GameCamera.instance.m_fov, targetFoV, fovDelta.Value);

            Vector3 look = Player.m_localPlayer.GetLookDir();
            currentAngle = Vector3.Angle(look, Vector3.down);
            if (currentAngle < GetCurrentSensivity() && locationShowRandomPoint.Value)
            {
                current = placeOfMystery;
                return;
            }

            if (directions.Count == 0)
                return;

            Vector3 pos = Player.m_localPlayer.GetEyePoint();
            directions = directions.OrderBy(dir => Vector3.Angle(look, dir.position - pos)).ToList();

            currentAngle = Vector3.Angle(look, directions[0].position - pos);
            if (currentAngle < GetCurrentSensivity())
                current = directions[0];
        }

        private static float GetCurrentSensivity()
        {
            return directionSensitivity.Value * targetFoV / defaultFoV;
        }

        private static float GetCurrentScreenSensivityThreshold()
        {
            return directionSensitivityThreshold.Value * targetFoV / defaultFoV;
        }

        private static float GetCurrentSfxSensivityThreshold()
        {
            return sfxSensitivityThreshold.Value * targetFoV / defaultFoV;
        }

        private static Vector3 GetRandomPoint()
        {
            Vector3 pos = Vector3.zero;
            do
            {
                pos = GetRandomPointInRadius(Vector3.zero, WorldGenerator.worldSize);
            }
            while (!IsValidRandomPointForTeleport(ref pos));

            return pos;
        }

        private static bool IsValidRandomPointForTeleport(ref Vector3 pos)
        {
            Heightmap.Biome biome = WorldGenerator.instance.GetBiome(pos);
            if (biome == Heightmap.Biome.Ocean || biome == Heightmap.Biome.None || !Player.m_localPlayer.m_knownBiome.Contains(biome))
                return false;

            pos = new Vector3(pos.x, ZoneSystem.c_WaterLevel + 1, pos.z);
            
            return true;
        }

        public static Vector3 GetRandomPointInRadius(Vector3 center, float radius)
        {
            float f = UnityEngine.Random.value * (float)Math.PI * 2f;
            float num = UnityEngine.Random.Range(0f, radius);

            return center + new Vector3(Mathf.Sin(f) * num, 0f, Mathf.Cos(f) * num);
        }

        [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateCrosshair))]
        public static class Hud_UpdateCrosshair_HoverTextDirectionMode
        {
            private static void Postfix(Hud __instance)
            {
                if (activated && current != null)
                {
                    __instance.m_hoverName.SetText(current.GetHoverText());
                    __instance.m_crosshair.color = __instance.m_hoverName.text.Length > 0 ? Color.yellow : new Color(1f, 1f, 1f, 0.5f);
                }
            }
        }

        [HarmonyPatch(typeof(Hud), nameof(Hud.Awake))]
        public static class Hud_Awake_BlackPanelInit
        {
            private static void Postfix(Hud __instance)
            {
                GameObject blocker = UnityEngine.Object.Instantiate(__instance.m_loadingScreen.gameObject, __instance.m_loadingScreen.transform.parent);
                blocker.name = "Waystones_DirectionSearchBlack";
                blocker.transform.SetSiblingIndex(0);

                blocker.transform.Find("Loading/TopFade").SetParent(blocker.transform);
                blocker.transform.Find("Loading/BottomFade").SetParent(blocker.transform);

                for (int i = blocker.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = blocker.transform.GetChild(i);
                    switch (child.name)
                    {
                        case "Loading":
                        case "Sleeping":
                        case "Teleporting":
                        case "Image":
                        case "Tip":
                        case "panel_separator":
                            UnityEngine.Object.Destroy(child.gameObject);
                            break;
                    }
                }

                // sfx Magic_CollectorLoop
                GameObject prefab = ZNetScene.instance.GetPrefab("guard_stone");
                if (prefab != null)
                {
                    GameObject sfx = UnityEngine.Object.Instantiate(prefab.transform.Find("WayEffect/sfx").gameObject, blocker.transform);
                    sfx.name = "sfx";
                    sfx.AddComponent<FollowPlayer>();
                    sfx.SetActive(true);

                    screenBlackenerSfx = sfx.GetComponent<AudioSource>();
                }

                screenBlackener = blocker.GetComponent<CanvasGroup>();
                screenBlackener.gameObject.SetActive(false);

                LogInfo("Blackener panel initialized");
            }
        }

        [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateBlackScreen))]
        public static class Hud_UpdateBlackScreen_DirectionModeScreenEffect
        {
            private static void Postfix(float dt)
            {
                if (activated)
                {
                    screenBlackener.gameObject.SetActive(value: true);
                    screenBlackener.alpha = Mathf.MoveTowards(screenBlackener.alpha, Mathf.Lerp(fadeMin.Value, fadeMax.Value, currentAngle / Mathf.Max(GetCurrentScreenSensivityThreshold(), GetCurrentSensivity())), dt);
                    screenBlackenerSfx.volume = Mathf.MoveTowards(screenBlackenerSfx.volume, Mathf.Lerp(sfxMax.Value, sfxMin.Value, currentAngle / Mathf.Max(GetCurrentSfxSensivityThreshold(), GetCurrentSensivity())), dt * 3);
                    screenBlackenerSfx.pitch = Mathf.MoveTowards(screenBlackenerSfx.pitch, Mathf.Lerp(sfxPitchMax.Value, sfxPitchMin.Value, currentAngle / Mathf.Max(GetCurrentSfxSensivityThreshold(), GetCurrentSensivity())), dt);
                }
                else
                {
                    screenBlackener.alpha = Mathf.MoveTowards(screenBlackener.alpha, 0f, dt / 2f);
                    screenBlackenerSfx.volume = Mathf.MoveTowards(screenBlackenerSfx.volume, 0f, dt);
                    screenBlackenerSfx.pitch = Mathf.MoveTowards(screenBlackenerSfx.pitch, 1f, dt);
                    if (screenBlackener.alpha <= 0f)
                        screenBlackener.gameObject.SetActive(value: false);
                }
            }
        }

        [HarmonyPatch]
        public static class JoyRightStick_SlowFactor
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetJoyRightStickX));
                yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetJoyRightStickY));
            }

            private static void Postfix(ref float __result)
            {
                if (!activated)
                    return;

                if (!Game.CanPause())
                    __result /= 2f;
                else if (Game.m_timeScale != 0)
                    __result *= Mathf.Clamp(1 / Game.m_timeScale, 1f, 2f);
            }
        }

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetMouseDelta))]
        public static class ZInput_GetMouseDelta_SlowFactor
        {
            private static void Postfix(ref Vector2 __result)
            {
                if (!activated)
                    return;

                __result *= Mathf.Clamp(__result.magnitude / slowFactorLookDeceleration.Value, slowFactorLookMinimum.Value, 1f);
            }
        }

        [HarmonyPatch]
        public static class StopDirectionMode_Postfix
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(Menu), nameof(Menu.Show));
                yield return AccessTools.Method(typeof(Menu), nameof(Menu.Hide));
                yield return AccessTools.Method(typeof(Game), nameof(Game.Unpause));
                yield return AccessTools.Method(typeof(Game), nameof(Game.Pause));
                yield return AccessTools.Method(typeof(ZoneSystem), nameof(ZoneSystem.Start));
                yield return AccessTools.Method(typeof(ZoneSystem), nameof(ZoneSystem.OnDestroy));
                yield return AccessTools.Method(typeof(FejdStartup), nameof(FejdStartup.Start));
                yield return AccessTools.Method(typeof(FejdStartup), nameof(FejdStartup.OnDestroy));
                yield return AccessTools.Method(typeof(Player), nameof(Player.SetSleeping));
                yield return AccessTools.Method(typeof(Player), nameof(Player.UseHotbarItem));
                yield return AccessTools.Method(typeof(Player), nameof(Player.StartGuardianPower));
                yield return AccessTools.Method(typeof(Player), nameof(Player.StopEmote));
            }

            private static void Postfix() => Exit(force: true);
        }

        [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.Awake))]
        public static class GameCamera_Awake_SetDefaultPov
        {
            private static void Postfix(GameCamera __instance) => defaultFoV = __instance.m_fov == 0f ? 65f : __instance.m_fov;
        }

        [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateCamera))]
        public static class GameCamera_UpdateCamera_BlockCameraDistance
        {
            private static void Prefix(GameCamera __instance, ref float __state)
            {
                if (activated)
                {
                    __state = __instance.m_zoomSens;
                    __instance.m_zoomSens = 0f;
                }
            }
            private static void Postfix(GameCamera __instance, float __state)
            {
                if (activated)
                    __instance.m_zoomSens = __state;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
        public static class Player_SetControls_SearchModeExit
        {
            private static void Postfix(Player __instance, Vector3 movedir, bool attack, bool secondaryAttack, bool block, bool jump, bool crouch, bool run, bool autoRun, bool dodge)
            {
                if (!activated)
                    return;

                if (__instance != Player.m_localPlayer)
                    return;

                if (movedir.magnitude > 0.05f || attack || secondaryAttack || block || jump || crouch || run || autoRun || dodge)
                    Exit(force: true);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.FindHoverObject))]
        public static class Player_FindHoverObject_SearchMode
        {
            private static bool Prefix(Player __instance, out GameObject hover, out Character hoverCreature)
            {
                hover = null;
                hoverCreature = null;

                if (__instance != Player.m_localPlayer)
                    return true;

                if (!activated)
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.GetHoverObject))]
        public static class Player_GetHoverObject_SearchMode
        {
            private static bool Prefix(Player __instance, ref GameObject __result)
            {
                if (__instance != Player.m_localPlayer)
                    return true;

                if (!activated)
                    return true;
                
                __result = null;
                return false;
            }
        }

        [HarmonyPatch(typeof(Minimap), nameof(Minimap.OnMapLeftDown))]
        public static class Minimap_ShowPinNameInput_BlockPinDialogInSearchMode
        {
            private static bool Prefix(Minimap __instance)
            {
                if (!IsActivated)
                    return true;

                __instance.m_leftClickTime = Time.time;
                __instance.m_leftDownTime = Time.time;

                return false;
            }
        }
    }
}
