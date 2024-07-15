using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static PocketTeleporter.PocketTeleporter;

namespace PocketTeleporter
{
    internal class DirectionSearch
    {
        public class Direction
        {
            public string name;
            public Vector3 position;
            public Color color;
            public double cooldown;

            public Direction(string name, Vector3 position)
            {
                this.name = name; 
                this.position = position;
                color = Color.yellow;
                cooldown = CooldownData.GetCooldownTimeToTarget(position);
            }

            public string GetHoverText()
            {
                return Localization.instance.Localize($"\n[<color={ColorUtility.ToHtmlStringRGB(color)}><b>$KEY_Use</b></color>] $inventory_move: {name}\n$se_shield_ttl: {CooldownData.TimerString(cooldown)}");
            }
        }

        private static List<Direction> directions = new List<Direction>();
        private static Direction current;
        private static bool activated;
        private static readonly Direction placeOfMystery = new Direction("$placeofmystery", Vector2.zero);
        private static float currentAngle;

        private static float defaultFoV;
        private static float targetFoV;

        public static CanvasGroup screenBlackener;
        public static AudioSource screenBlackenerSfx;

        internal static void Toggle()
        {
            if (activated)
                Exit();
            else if (useShortcutToEnter.Value)
                Enter();
        }

        internal static void Enter()
        {
            if (!CanCast())
                return;

            if (!activated)
            {
                Game.FadeTimeScale(slowFactorTime.Value, 4f);
                targetFoV = defaultFoV;
            }

            FillDirections();
            activated = true;
        }

        internal static void Exit()
        {
            if (!CanCast())
                return;

            if (activated)
            {
                GameCamera.instance.m_fov = defaultFoV;

                if (Game.m_timeScale >= slowFactorTime.Value)
                    Game.FadeTimeScale(1f, 2f);
            } 

            activated = false;
            current = null;
            currentAngle = 0f;
        }

        internal static void FillDirections()
        {
            directions.Clear();

            directions.Add(new Direction("$piece_bed_currentspawn", GetSpawnPoint()));

            ZoneSystem.instance.GetLocationIcons(ZoneSystem.instance.tempIconList);
            foreach (KeyValuePair<Vector3, string> loc in ZoneSystem.instance.tempIconList)
            {
                if (loc.Value == "StartTemple")
                    directions.Add(new Direction("StartTemple", loc.Key));
                else if (loc.Value == "Vendor_BlackForest")
                    directions.Add(new Direction("$npc_haldor", loc.Key));
                else if (loc.Value == "Hildir_camp")
                    directions.Add(new Direction("$npc_hildir", loc.Key));
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
                    ZInput.GetButtonDown("JoyBlock") ||
                    ZInput.GetButtonDown("JoyButtonB")))
            {
                Exit();
            }

            if (current != null && (ZInput.GetButton("Use") || ZInput.GetButton("JoyUse")) && CanCast())
            {
                if (current == placeOfMystery)
                    current.position = GetRandomPoint();

                TeleportAttempt(current.position, current.cooldown, current.name);
                Exit();
            }

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
            if (currentAngle < GetCurrentSensivity())
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
                blocker.name = "PocketTeleporter_DirectionSearchBlack";
                blocker.transform.SetSiblingIndex(0);

                blocker.transform.Find("Loading/TopFade").SetParent(blocker.transform);
                blocker.transform.Find("Loading/BottomFade").SetParent(blocker.transform);

                UnityEngine.Object.Destroy(blocker.transform.Find("Loading").gameObject);
                UnityEngine.Object.Destroy(blocker.transform.Find("Sleeping").gameObject);
                UnityEngine.Object.Destroy(blocker.transform.Find("Teleporting").gameObject);

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

                __result *= Mathf.Max(slowFactorLook.Value, 0.01f);
            }
        }

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetMouseDelta))]
        public static class ZInput_GetMouseDelta_SlowFactor
        {
            private static void Postfix(ref Vector2 __result)
            {
                if (!activated)
                    return;

                __result *= Mathf.Max(slowFactorLook.Value, 0.01f);
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
            }

            private static void Postfix() => Exit();
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

                if (movedir.magnitude > 0.05f || attack  || secondaryAttack ||  block ||  jump || crouch || run || autoRun || dodge)
                    activated = false;
            }
        }

    }
}
