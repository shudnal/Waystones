using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ServerSync;
using System.Linq;
using System.IO;
using System.Reflection;
using UnityEngine.Rendering;
using LocalizationManager;
using System.Collections.Generic;
using System;
using YamlDotNet.Serialization;

namespace Waystones
{
    [BepInPlugin(pluginID, pluginName, pluginVersion)]
    public class Waystones : BaseUnityPlugin
    {
        public const string pluginID = "shudnal.Waystones";
        public const string pluginName = "Waystones";
        public const string pluginVersion = "1.0.14";

        private readonly Harmony harmony = new(pluginID);

        internal static readonly ConfigSync configSync = new(pluginID) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };

        internal static Waystones instance;

        internal static ConfigEntry<bool> configLocked;
        internal static ConfigEntry<bool> loggingEnabled;
        internal static ConfigEntry<KeyboardShortcut> shortcut;
        internal static ConfigEntry<string> pieceRecipe;
        internal static ConfigEntry<bool> itemSacrifitionReduceCooldown;
        internal static ConfigEntry<bool> disableWaystoneSparcs;

        internal static ConfigEntry<bool> locationWaystonesShowOnMap;
        internal static ConfigEntry<bool> locationShowCurrentSpawn;
        internal static ConfigEntry<bool> locationShowLastPoint;
        internal static ConfigEntry<bool> locationShowLastShip;
        internal static ConfigEntry<bool> locationShowLastTombstone;
        internal static ConfigEntry<bool> locationShowStartTemple;
        internal static ConfigEntry<bool> locationShowHaldor;
        internal static ConfigEntry<bool> locationShowHildir;
        internal static ConfigEntry<bool> locationShowBogWitch;
        internal static ConfigEntry<bool> locationShowWaystones;
        internal static ConfigEntry<bool> locationShowRandomPoint;

        internal static ConfigEntry<bool> useShortcutToEnter;
        internal static ConfigEntry<bool> allowEncumbered;
        internal static ConfigEntry<bool> allowNonTeleportableItems;
        internal static ConfigEntry<bool> emitNoiseOnTeleportation;
        internal static ConfigEntry<bool> allowWet;
        internal static ConfigEntry<bool> allowSensed;
        internal static ConfigEntry<bool> allowNonSitting;
        internal static ConfigEntry<int> tagCharactersLimit;
        internal static ConfigEntry<bool> allowForEveryone;

        internal static ConfigEntry<float> directionSensitivity;
        internal static ConfigEntry<float> directionSensitivityThreshold;
        internal static ConfigEntry<float> fadeMax;
        internal static ConfigEntry<float> fadeMin;
        internal static ConfigEntry<float> slowFactorTime;
        internal static ConfigEntry<float> slowFactorLookDeceleration;
        internal static ConfigEntry<float> slowFactorLookMinimum;
        internal static ConfigEntry<float> fovDelta;
        internal static ConfigEntry<float> sfxSensitivityThreshold;
        internal static ConfigEntry<float> sfxMax;
        internal static ConfigEntry<float> sfxMin;
        internal static ConfigEntry<float> sfxPitchMax;
        internal static ConfigEntry<float> sfxPitchMin;

        internal static ConfigEntry<CooldownTime> cooldownTime;
        internal static ConfigEntry<int> cooldownMaximum;
        internal static ConfigEntry<int> cooldownMinimum;
        internal static ConfigEntry<int> cooldownDistanceMaximum;
        internal static ConfigEntry<int> cooldownDistanceMinimum;
        internal static ConfigEntry<int> cooldownShort;
        internal static ConfigEntry<int> cooldownSearchMode;

        internal static ConfigEntry<bool> particlesCollision;
        internal static ConfigEntry<int> particlesMaxAmount;
        internal static ConfigEntry<int> particlesMinRateOverTime;
        internal static ConfigEntry<int> particlesMaxRateOverTime;
        internal static ConfigEntry<int> particlesMinForceOverTime;
        internal static ConfigEntry<int> particlesMaxForceOverTime;

        public static readonly CustomSyncedValue<Dictionary<string, int>> itemsToReduceCooldown = new(configSync, "Items to reduce cooldowns", new Dictionary<string, int>());

        public static string configDirectory;
        internal static FileSystemWatcher configWatcher;
        private const string itemsToReduceCooldownFilter = $"{pluginID}.reduce_cooldowns.*";

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

            StartCoroutine(Localizer.Load());
        }

        public void ConfigInit()
        {
            config("General", "NexusID", 2832, "Nexus mod ID for updates", false);

            configLocked = config("General", "Lock Configuration", defaultValue: true, "Configuration is locked and can be changed by server admins only.");
            loggingEnabled = config("General", "Logging enabled", defaultValue: false, "Enable logging. [Not Synced with Server]", false);
            pieceRecipe = config("General", "Recipe", defaultValue: "SurtlingCore:1,GreydwarfEye:5,Stone:5", "Piece recipe");
            disableWaystoneSparcs = config("General", "Disable waystone sparcs", defaultValue: false, "Enable sacrifition of item from list to reduce waystone cooldown. Restart required. [Not Synced with Server]", false);

            pieceRecipe.SettingChanged += (sender, args) => PieceWaystone.SetPieceRequirements();

            itemSacrifitionReduceCooldown = config("Item sacrifition", "Sacrifice item from list to reduce cooldown", defaultValue: true, "Enable sacrifition of item from list to reduce waystone cooldown");
            
            locationWaystonesShowOnMap = config("Locations", "Show waystones on map", defaultValue: true, "Show waystone map pins");
            locationShowCurrentSpawn = config("Locations", "Show current spawn", defaultValue: true, "Show current spawn point in search mode");
            locationShowLastPoint = config("Locations", "Show last location", defaultValue: true, "Show last location from where you used fast travel last time in search mode");
            locationShowLastShip = config("Locations", "Show last ship", defaultValue: true, "Show last ship position in search mode");
            locationShowLastTombstone = config("Locations", "Show last tombstone", defaultValue: true, "Show last death position in search mode");
            locationShowStartTemple = config("Locations", "Show sacrificial stones", defaultValue: true, "Show sacrificial stones positioin in search mode");
            locationShowHaldor = config("Locations", "Show Haldor", defaultValue: true, "Show Haldor location in search mode");
            locationShowHildir = config("Locations", "Show Hildir", defaultValue: true, "Show Hildir location in search mode");
            locationShowBogWitch = config("Locations", "Show Bog Witch", defaultValue: true, "Show Bog Witch location in search mode");
            locationShowWaystones = config("Locations", "Show waystones", defaultValue: true, "Show waystones network in search mode");
            locationShowRandomPoint = config("Locations", "Show random point", defaultValue: true, "Show random point position in search mode");

            emitNoiseOnTeleportation = config("Restrictions", "Emit noise on fast travelling", defaultValue: true, "If enabled then you will attract attention of nearby enemies on fast travelling start.");
            allowEncumbered = config("Restrictions", "Ignore encumbered to start search", defaultValue: false, "If enabled then encumbrance check before search start will be omitted.");
            allowNonTeleportableItems = config("Restrictions", "Ignore nonteleportable items to start search", defaultValue: false, "If enabled then inventory check before search start will be omitted.");
            allowWet = config("Restrictions", "Ignore wet status to start search", defaultValue: false, "If enabled then Wet status check before search start will be omitted.");
            allowSensed = config("Restrictions", "Ignore nearby enemies to start search", defaultValue: false, "If enabled then Sensed by nearby enemies check before search start will be omitted.");
            allowNonSitting = config("Restrictions", "Ignore sitting to start search", defaultValue: false, "If enabled then sitting position check before search start will be omitted.");
            useShortcutToEnter = config("Restrictions", "Use shortcut to toggle search mode", defaultValue: false, "If set you can enter direction search mode by pressing shortcut. If not set - you have to sit in front of the waystone to start search mode.");
            shortcut = config("Restrictions", "Shortcut", defaultValue: new KeyboardShortcut(KeyCode.Y), "Enter/Exit direction search mode [Not Synced with Server]", false);
            tagCharactersLimit = config("Restrictions", "Tag characters limit", defaultValue: 15, "Max length of waystone tag. Values less than 10 will be ignored.");
            allowForEveryone = config("Restrictions", "Allow all players to use activated waystones", defaultValue: false, "If enabled, any player can use a waystone once it has been activated by someone.");

            directionSensitivity = config("Search mode", "Target sensitivity threshold", defaultValue: 2f, "Angle between look direction and target direction for location to appear in search mode");
            directionSensitivityThreshold = config("Search mode", "Screen sensitivity threshold", defaultValue: 6f, "Angle between look direction and target direction for location to start appearing in search mode");
            fadeMax = config("Search mode", "Screen fade max", defaultValue: 0.98f, "Screen darkness when sensitivity threshold is not met.");
            fadeMin = config("Search mode", "Screen fade min", defaultValue: 0.88f, "Screen darkness when looking at target");
            sfxSensitivityThreshold = config("Search mode", "Sound effect sensitivity threshold", defaultValue: 20f, "Angle between look direction and target direction for sound to start appearing in direction search mode");
            sfxMax = config("Search mode", "Sound effect max volume", defaultValue: 1.1f, "Volume of sound effect played in direction mode when looking at a target [Not Synced with Server]", false);
            sfxMin = config("Search mode", "Sound effect min volume", defaultValue: 0.4f, "Volume of sound effect played in direction mode when sensitivity threshold is not met [Not Synced with Server]", false);
            sfxPitchMax = config("Search mode", "Sound effect max pitch", defaultValue: 1.0f, "Pitch of sound effect played in direction mode when looking at a target");
            sfxPitchMin = config("Search mode", "Sound effect min pitch", defaultValue: 0.8f, "Pitch of sound effect played in direction mode when sensitivity threshold is not met.");
            slowFactorTime = config("Search mode", "Slow factor time", defaultValue: 0.25f, "Multiplier of speed ​​of time (singleplayer)");
            slowFactorLookDeceleration = config("Search mode", "Slow factor mouse deceleration", defaultValue: 60f, "Mouse camera sensitivity acceleration factor. [Not Synced with Server]" +
                                                                                                                    "\nIncrease to make mouse acceleration proportionally lower, decrease to make mouse movement faster ", false);
            slowFactorLookMinimum = config("Search mode", "Slow factor mouse minimum", defaultValue: 0.08f, "Minimum mouse camera sensitivity factor in search mode. [Not Synced with Server]", false);
            fovDelta = config("Search mode", "FoV delta", defaultValue: 40f, "How much camera FoV can be changed both sides using zoom");

            cooldownTime = config("Travel cooldown", "Time", defaultValue: CooldownTime.WorldTime, "Time type to calculate cooldown." +
                                                                                                     "\nWorld time - calculate from time passed in game world" +
                                                                                                     "\nGlobal time - calculate from real world time");
            cooldownDistanceMaximum = config("Travel cooldown", "Fast travelling distance maximum", defaultValue: 5000, "If fast travelling distance is larger then that cooldown will be set to maximum. World radius is 10000.");
            cooldownDistanceMinimum = config("Travel cooldown", "Fast travelling distance minimum", defaultValue: 500, "If fast travelling distance is smaller then that cooldown will be set to minimum. World radius is 10000.");
            cooldownMaximum = config("Travel cooldown", "Fast travelling cooldown maximum", defaultValue: 7200, "Maximal cooldown to be set after successfull fast travelling");
            cooldownMinimum = config("Travel cooldown", "Fast travelling cooldown minimum", defaultValue: 600, "Minimal cooldown to be set after successfull fast travelling");
            cooldownShort = config("Travel cooldown", "Fast travelling interrupted cooldown", defaultValue: 60, "Cooldown to be set if fast travelling was interrupted");
            cooldownSearchMode = config("Travel cooldown", "Search mode cooldown", defaultValue: 30, "Cooldown to be set on search mode exit");

            particlesCollision = config("Travelling effect", "Particles physics collision", defaultValue: false, "Make particles emitted while fast travelling collide with objects. Restart required.");
            particlesMaxAmount = config("Travelling effect", "Particles amount maximum", defaultValue: 8000, "Maximum amount of particles emitted. Restart required.");
            particlesMaxRateOverTime = config("Travelling effect", "Particles rate over time maximum", defaultValue: 4000, "Maximum amount of particles emitted per second at the curve end. Restart required.");
            particlesMinRateOverTime = config("Travelling effect", "Particles rate over time minimum", defaultValue: 50, "Minimum amount of particles emitted per second at the curve start. Restart required.");
            particlesMaxForceOverTime = config("Travelling effect", "Particles force over time maximum", defaultValue: 10, "Maximum emission force of particles emitted at the curve end. Restart required.");
            particlesMinForceOverTime = config("Travelling effect", "Particles force over time minimum", defaultValue: 5, "Minimum emission force of particles emitted at the curve start. Restart required.");

            InitCommands();
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

            if ((Chat.instance == null || !Chat.instance.HasFocus()) && !Console.IsVisible() && !Menu.IsVisible() && (bool)TextViewer.instance && !TextViewer.instance.IsVisible() && !Player.m_localPlayer.InCutscene() && Player.m_localPlayer.GetSEMan().HaveStatusEffect(SE_Waystone.statusEffectWaystonesHash))
            {
                if (ZInput.GetButtonDown("Block") || ZInput.GetButtonDown("JoyButtonB"))
                {
                    ZInput.ResetButtonStatus("Block");
                    ZInput.ResetButtonStatus("JoyButtonB");
                    Player.m_localPlayer.GetSEMan().RemoveStatusEffect(SE_Waystone.statusEffectWaystonesHash);
                }
            }

            DirectionSearch.Update();
        }

        public static void InitCommands()
        {
            new Terminal.ConsoleCommand("setwaystonecooldown", "seconds", delegate (Terminal.ConsoleEventArgs args)
            {
                WorldData.SetCooldown(args.TryParameterInt(1, 0));
            }, isCheat: true);
        }

        public static void TeleportAttempt(Vector3 targetPoint, Quaternion targetRotation, double cooldown, string location)
        {
            if (!CanCast())
                return;

            if (!location.IsNullOrWhiteSpace())
                MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, $"$ws_tooltip_moving_to {location}");

            SEMan seman = Player.m_localPlayer.GetSEMan();
            if (seman.HaveStatusEffect(SE_Waystone.statusEffectWaystonesHash))
            {
                Player.m_localPlayer.GetSEMan().RemoveStatusEffect(SE_Waystone.statusEffectWaystonesHash);
            }
            else
            {
                if (WaystoneSmall.IsSearchAllowed(Player.m_localPlayer))
                {
                    SE_Waystone se = Player.m_localPlayer.GetSEMan().AddStatusEffect(SE_Waystone.statusEffectWaystonesHash) as SE_Waystone;
                    if (se != null)
                    {
                        se.targetPoint = targetPoint;
                        se.targetCooldown = cooldown;
                        se.targetRotation = targetRotation;

                        if (emitNoiseOnTeleportation.Value)
                        {
                            Player.m_localPlayer.AddNoise(50f);
                            BaseAI.DoProjectileHitNoise(Player.m_localPlayer.transform.position, 50f, Player.m_localPlayer);
                        }

                        if (!location.IsNullOrWhiteSpace())
                        {
                            MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, $"$ws_message_travelling_to {location}");
                            se.m_name = location;
                            LogInfo($"Teleport initiated to {location} pos {targetPoint} cooldown {WorldData.TimerString(cooldown)}");
                        }
                    }
                }
            }
        }

        public static bool CanCast()
        {
            Player player = Player.m_localPlayer;

            return !(player == null || player.IsDead() || player.InCutscene() || player.IsTeleporting() || player.InInterior()) &&
                    (Chat.instance == null || !Chat.instance.HasFocus()) &&
                    !Console.IsVisible() && !Menu.IsVisible() && TextViewer.instance != null &&
                    !TextViewer.instance.IsVisible() && !TextInput.IsVisible() &&
                    !GameCamera.InFreeFly() && !StoreGui.IsVisible() && !InventoryGui.IsVisible();
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
            LoadIcon("SE_Waystone.png", ref SE_Waystone.iconWaystones);
            LoadIcon("item_waystone.png", ref PieceWaystone.itemWaystone);
            LoadIcon("icon_waystone.png", ref WaystoneList.iconWaystone);
        }

        internal static void LoadIcon(string filename, ref Sprite icon)
        {
            Texture2D tex = new(2, 2);
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
            return Player.m_localPlayer != null && Player.m_localPlayer.GetSEMan().HaveStatusEffect(SE_Waystone.statusEffectWaystonesHash);
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
                if (PreventPlayerInput())
                    __result = 0f;
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
                if (__instance.GetSEMan().HaveStatusEffect(SE_Waystone.statusEffectWaystonesHash))
                    mouseLook = Vector2.zero;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.ActivateGuardianPower))]
        public static class Player_ActivateGuardianPower_PreventGPowerUseWhenTeleporting
        {
            private static bool Prefix(Player __instance)
            {
                return !__instance.GetSEMan().HaveStatusEffect(SE_Waystone.statusEffectWaystonesHash);
            }
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        public static class ZoneSystem_Start_InitSeasonStateAndConfigWatcher
        {
            private static void Postfix()
            {
                SetupConfigWatcher(enabled: true);
            }
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.OnDestroy))]
        public static class ZoneSystem_OnDestroy_DisableConfigWatcher
        {
            private static void Postfix()
            {
                SetupConfigWatcher(enabled: false);
            }
        }

        public static void SetupConfigWatcher(bool enabled)
        {
            if (!Directory.Exists(Paths.ConfigPath))
                return;

            if (enabled)
                ReadInitialConfigs();

            if (configWatcher == null)
            {
                configWatcher = new FileSystemWatcher(Paths.ConfigPath, itemsToReduceCooldownFilter);
                configWatcher.Changed += new FileSystemEventHandler(ReadConfigs);
                configWatcher.Created += new FileSystemEventHandler(ReadConfigs);
                configWatcher.Renamed += new RenamedEventHandler(ReadConfigs);
                configWatcher.Deleted += new FileSystemEventHandler(ReadConfigs);
                configWatcher.IncludeSubdirectories = false;
                configWatcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            }

            configWatcher.EnableRaisingEvents = enabled;
        }

        private static void ReadInitialConfigs()
        {
            foreach (FileInfo file in new DirectoryInfo(Paths.ConfigPath).GetFiles(itemsToReduceCooldownFilter, SearchOption.AllDirectories))
                ReadConfigFile(file.Name, file.FullName);
        }

        private static void ReadConfigs(object sender, FileSystemEventArgs eargs)
        {
            if (eargs is RenamedEventArgs)
                ReadInitialConfigs();
            else
                ReadConfigFile(eargs.Name, eargs.FullPath);
        }

        private static void ReadConfigFile(string filename, string fullname)
        {
            Dictionary<string, int> newValue = new();
            try
            {
                string content = File.ReadAllText(fullname);
#nullable enable
                if (content is not null)
                    foreach (KeyValuePair<string, int> kv in new DeserializerBuilder().IgnoreFields().Build().Deserialize<Dictionary<string, int>?>(content) ?? new Dictionary<string, int>())
                        newValue[kv.Key] = kv.Value;
#nullable disable
            }
            catch (Exception e)
            {
                LogInfo($"Error reading file ({fullname})! Error: {e.Message}");
            }

            itemsToReduceCooldown.AssignValueSafe(newValue);

            LogInfo($"Loaded {newValue.Count} items from file {filename}");
        }
    }
}
