using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ServerSync;
using System.Linq;
using System.IO;
using System.Reflection;
using UnityEngine.Rendering;

namespace PocketTeleporter
{
    [BepInPlugin(pluginID, pluginName, pluginVersion)]
    public class PocketTeleporter : BaseUnityPlugin
    {
        const string pluginID = "shudnal.PocketTeleporter";
        const string pluginName = "Pocket Teleporter";
        const string pluginVersion = "1.0.0";

        private readonly Harmony harmony = new Harmony(pluginID);

        internal static readonly ConfigSync configSync = new ConfigSync(pluginID) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };

        internal static PocketTeleporter instance;

        internal static ConfigEntry<bool> configLocked;
        internal static ConfigEntry<bool> loggingEnabled;
        internal static ConfigEntry<KeyboardShortcut> shortcut;
        internal static ConfigEntry<float> directionSensitivity;
        internal static ConfigEntry<float> directionSensitivityThreshold;
        internal static ConfigEntry<float> fadeMax;
        internal static ConfigEntry<float> fadeDelta;
        internal static ConfigEntry<float> slowFactor;
        internal static ConfigEntry<float> fovDelta;

        internal static ConfigEntry<CooldownTime> cooldownTime;
        internal static ConfigEntry<int> cooldownFull;
        internal static ConfigEntry<int> cooldownShort;

        internal static ConfigEntry<bool> particlesCollision;

        public static string configDirectory;

        public const string customDataKey = "PocketTeleporterCooldown";

        public enum CooldownTime
        {
            WorldTime,
            GlobalTime
        }

        private void Awake()
        {
            harmony.PatchAll();

            instance = this;

            ConfigInit();
            _ = configSync.AddLockingConfigEntry(configLocked);

            Game.isModded = true;

            configDirectory = Path.Combine(Paths.ConfigPath, pluginID);
            
            LoadIcons();
        }

        public void ConfigInit()
        {
            config("General", "NexusID", 2832, "Nexus mod ID for updates", false);

            configLocked = config("General", "Lock Configuration", defaultValue: true, "Configuration is locked and can be changed by server admins only.");
            loggingEnabled = config("General", "Logging enabled", defaultValue: false, "Enable logging. [Not Synced with Server]", false);

            shortcut = config("Direction search", "Shortcut", defaultValue: new KeyboardShortcut(KeyCode.Y, new KeyCode[0] { }), "Enter/exit direction mode.");
            directionSensitivity = config("Direction search", "Sensivity", defaultValue: 2f, "Angle between look direction and target direction for location to appear in direction mode");
            directionSensitivityThreshold = config("Direction search", "Sensivity threshold", defaultValue: 6f, "Angle between look direction and target direction for location to start appearing in direction mode");
            fadeMax = config("Direction search", "Screen fade max", defaultValue: 0.98f, "Angle between look direction and target direction for location to start appearing in direction mode");
            fadeDelta = config("Direction search", "Screen fade delta", defaultValue: 0.1f, "Angle between look direction and target direction for location to start appearing in direction mode");
            slowFactor = config("Direction search", "Slow factor", defaultValue: 0.2f, "Multiplier of speed ​​of time and look");
            fovDelta = config("Direction search", "FoV delta", defaultValue: 40f, "How much camera FoV can be changed");

            cooldownTime = config("Teleport cooldown", "Time", defaultValue: CooldownTime.WorldTime, "Time type to calculate cooldown." +
                                                                                                     "\nWorld time - calculate from time passed in game world" +
                                                                                                     "\nGlobal time - calculate from real world time");
            cooldownFull = config("Teleport cooldown", "Teleportation successful", defaultValue: 7200, "Cooldown to be set after successfull teleportation");
            cooldownShort = config("Teleport cooldown", "Teleportation interrupted", defaultValue: 300, "Cooldown to be set if teleportation was interrupted");

            particlesCollision = config("Misc", "Particles physics collision", defaultValue: false, "Make particles emitted while teleporting collide with objects. Restart required.");
        }

        private void OnDestroy()
        {
            Config.Save();
            instance = null;
            harmony?.UnpatchSelf();
        }

        public void LateUpdate()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return;

            if (Player.m_localPlayer == null)
                return;

            if (shortcut.Value.IsDown())
                Player.m_localPlayer.GetSEMan().RemoveStatusEffect(SE_PocketTeleporter.statusEffectPocketTeleporterHash);

            DirectionSearch.Update();

            // $placeofmystery	Загадочное место - смотреть вверх
            // $npc_haldor
            // $npc_hildir
            // start temple
            // last tombstone
            // last ship
            // last location in 5 min
            // Ставим waystone, включаем, садимся, входим в режим поиска, улетаем, сохраняем последнюю позицию, на объект TimedDestruction
        }

        public static void TeleportAttempt(Vector3 targetPoint, string message)
        {
            if (!CanCast())
                return;

            if (!message.IsNullOrWhiteSpace())
                MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, message);

            SEMan seman = Player.m_localPlayer.GetSEMan();
            if (seman.HaveStatusEffect(SE_PocketTeleporter.statusEffectPocketTeleporterHash))
            {
                Player.m_localPlayer.GetSEMan().RemoveStatusEffect(SE_PocketTeleporter.statusEffectPocketTeleporterHash);
            }
            else
            {
                if (IsNotInPosition(Player.m_localPlayer))
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$msg_cart_incorrectposition");
                else if (Player.m_localPlayer.IsEncumbered())
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$se_encumbered_start");
                else if (!Player.m_localPlayer.IsTeleportable())
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$msg_noteleport");
                else if (CooldownData.IsOnCooldown())
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"$hud_powernotready: {CooldownData.GetCooldownString()}");
                else
                {
                    SE_PocketTeleporter se = Player.m_localPlayer.GetSEMan().AddStatusEffect(SE_PocketTeleporter.statusEffectPocketTeleporterHash) as SE_PocketTeleporter;
                    if (se != null)
                    {
                        se.targetPoint = targetPoint;
                        Player.m_localPlayer.AddNoise(50f);
                        BaseAI.DoProjectileHitNoise(Player.m_localPlayer.transform.position, 50f, Player.m_localPlayer);
                        if (!message.IsNullOrWhiteSpace())
                            MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, message);
                    }
                }
            }
        }

        public static bool CanCast()
        {
            Player player = Player.m_localPlayer;

            return !(player == null || player.IsDead() || player.InCutscene() || player.IsTeleporting()) &&
                    (Chat.instance == null || !Chat.instance.HasFocus()) &&
                    !Console.IsVisible() && !Menu.IsVisible() && TextViewer.instance != null &&
                    !TextViewer.instance.IsVisible() && !TextInput.IsVisible() &&
                    !Minimap.IsOpen() && !GameCamera.InFreeFly() && !StoreGui.IsVisible() && !InventoryGui.IsVisible();
        }

        public static bool IsNotInPosition(Player player)
        {
            return player.IsAttachedToShip() || player.IsAttached() || player.IsDead() || player.IsRiding() || player.IsSleeping() ||
                   player.IsTeleporting() || player.InPlaceMode() || player.InBed() || player.InCutscene() || player.InInterior();
        }

        public static void LogInfo(object data)
        {
            if (loggingEnabled.Value)
                instance.Logger.LogInfo(data);
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, defaultValue, description);

            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, string description, bool synchronizedSetting = true) => config(group, name, defaultValue, new ConfigDescription(description), synchronizedSetting);

        private void LoadIcons()
        {
            LoadIcon("SE_PocketTeleporter.png", ref SE_PocketTeleporter.iconPocketTeleporter);
        }

        internal static void LoadIcon(string filename, ref Sprite icon)
        {
            Texture2D tex = new Texture2D(2, 2);
            if (LoadTexture(filename, ref tex))
                icon = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);
        }

        internal static bool LoadTexture(string filename, ref Texture2D tex)
        {
            string fileInConfigFolder = Path.Combine(configDirectory, filename);
            if (File.Exists(fileInConfigFolder))
            {
                LogInfo($"Loaded image: {fileInConfigFolder}");
                return tex.LoadImage(File.ReadAllBytes(fileInConfigFolder));
            }

            Assembly executingAssembly = Assembly.GetExecutingAssembly();

            string name = executingAssembly.GetManifestResourceNames().Single(str => str.EndsWith(filename));

            Stream resourceStream = executingAssembly.GetManifestResourceStream(name);

            byte[] data = new byte[resourceStream.Length];
            resourceStream.Read(data, 0, data.Length);

            tex.name = Path.GetFileNameWithoutExtension(filename);

            return tex.LoadImage(data, true);
        }

        private static bool PreventPlayerInput()
        {
            return Player.m_localPlayer != null && Player.m_localPlayer.GetSEMan().HaveStatusEffect(SE_PocketTeleporter.statusEffectPocketTeleporterHash);
        }

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetMouseDelta))]
        public static class ZInput_GetMouseDelta_PreventMouseInput
        {
            [HarmonyPriority(Priority.Last)]
            public static void Postfix(ref Vector2 __result)
            {
                if (PreventPlayerInput())
                    __result = Vector2.zero;
            }
        }

        [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.TakeInput))]
        public static class PlayerController_TakeInput_PreventPlayerMovements
        {
            [HarmonyPriority(Priority.First)]
            public static bool Prefix()
            {
                return !PreventPlayerInput();
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.SetMouseLook))]
        public static class Player_SetMouseLook_PreventPlayerMovements
        {
            [HarmonyPriority(Priority.First)]
            public static void Prefix(Player __instance, ref Vector2 mouseLook)
            {
                if (__instance.GetSEMan().HaveStatusEffect(SE_PocketTeleporter.statusEffectPocketTeleporterHash))
                    mouseLook = Vector2.zero;
            }
        }

    }
}
