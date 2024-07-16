using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;
using static UnityEngine.ParticleSystem;

namespace PocketTeleporter
{
    public class SE_PocketTeleporter : SE_Stats
    {
        public const string statusEffectPocketTeleporterName = "PocketTeleporter";
        public static readonly int statusEffectPocketTeleporterHash = statusEffectPocketTeleporterName.GetStableHashCode();

        public const string vfx_PocketTeleporterName = "vfx_PocketTeleporter";
        public static readonly int vfx_PocketTeleporterHash = vfx_PocketTeleporterName.GetStableHashCode();
        public const string vfx_PocketTeleporterParticles = "Sparcs";
        public const string vfx_PocketTeleporterSfx = "Sfx";
        public const string vfx_PocketTeleporterLight = "Light";

        public const string statusEffectName = "$se_PocketTeleporter_name";
        public const string statusEffectTooltip = "$se_PocketTeleporter_tooltip";

        public static readonly int s_gpower = ZSyncAnimation.GetHash("gpower");

        public static Sprite iconPocketTeleporter;
        public static GameObject vfx_PocketTeleporter;

        [NonSerialized]
        private float volume;
        [NonSerialized]
        private AudioSource sfx;
        [NonSerialized]
        private MainModule main;
        [NonSerialized]
        private Light light;

        [NonSerialized]
        public Vector3 targetPoint = Vector3.zero;
        [NonSerialized]
        public double targetCooldown = 0;
        [NonSerialized]
        private bool lookDirTriggered;
        [NonSerialized]
        private bool teleportTriggered;

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);

            if (m_startEffectInstances == null)
                return;

            main.startLifetime = 0.5f + 5.5f * (m_time / m_ttl);
            main.startSpeed = 4f - 3.5f * (m_time / m_ttl);
            main.simulationSpeed = 1f + (m_time / m_ttl);

            light.intensity = 1f + 2f * (m_time / m_ttl);
            light.range = 5f + 15f * (m_time / m_ttl);

            sfx.volume = volume * (m_time / m_ttl);

            if (ControlLocalPlayer())
            {
                Player localPlayer = Player.m_localPlayer;
                if (!lookDirTriggered && targetPoint != Vector3.zero)
                {
                    lookDirTriggered = true;
                    localPlayer.SetLookDir(targetPoint - localPlayer.transform.position, 5f);
                    localPlayer.GetZAnim().SetTrigger("gpower");
                }

                if (localPlayer.m_lookTransitionTime > 0f)
                    localPlayer.transform.rotation = localPlayer.m_lookYaw;

                localPlayer.SetMouseLookForward(false);
                localPlayer.m_lookPitch = Mathf.MoveTowards(localPlayer.m_lookPitch, 0f, dt * 10f);

                localPlayer.GetZAnim().SetSpeed(0.125f);

                if (!teleportTriggered && GetRemaningTime() < 0.75f && targetPoint != Vector3.zero)
                {
                    localPlayer.TeleportTo(targetPoint, localPlayer.transform.rotation, distantTeleport: true);
                    WorldData.SaveLastPosition(localPlayer.transform.position);
                    teleportTriggered = true;
                }
            }

            if (targetPoint != Vector3.zero)
            {
                Vector3 dir = targetPoint - m_character.transform.position;
                dir.Normalize();
                m_startEffectInstances[0].transform.rotation = Quaternion.LookRotation(dir);

                m_startEffectInstances[0].transform.localPosition = Vector3.MoveTowards(m_startEffectInstances[0].transform.localPosition, new Vector3(0, 1.5f, -0.3f), dt / 10f);
            }
        }

        public override void Setup(Character character)
        {
            base.Setup(character);

            if (m_startEffectInstances.Length == 0)
                return;

            GameObject effect = m_startEffectInstances[0];

            sfx = effect.transform.Find(vfx_PocketTeleporterSfx).GetComponent<AudioSource>();
            volume = sfx.volume;

            main = effect.transform.Find(vfx_PocketTeleporterParticles).GetComponent<ParticleSystem>().main;

            light = effect.transform.Find(vfx_PocketTeleporterLight).GetComponent<Light>();
        }

        public override void Stop()
        {
            base.Stop();
            
            if (m_character)
            {
                m_character.GetZAnim().SetSpeed(1f);
                m_character.m_lookTransitionTime = 0f;

                if (m_character == Player.m_localPlayer)
                    WorldData.SetCooldown(teleportTriggered ? targetCooldown : PocketTeleporter.cooldownShort.Value);
            }
        }

        public override void OnDamaged(HitData hit, Character attacker)
        {
            base.OnDamaged(hit, attacker);
            Stop();
            m_time = m_ttl + 1;
        }

        private bool ControlLocalPlayer()
        {
            return m_character != null && m_character == Player.m_localPlayer;
        }

        public static void RegisterEffects()
        {
            if (!ZNetScene.instance)
                return;

            if (!(bool)vfx_PocketTeleporter)
            {
                WayStone waystone = Resources.FindObjectsOfTypeAll<WayStone>().FirstOrDefault();
                if (waystone == null)
                    return;

                vfx_PocketTeleporter = CustomPrefabs.InitPrefabClone(ObjectDB.instance.GetStatusEffect(SEMan.s_statusEffectCold).m_startEffects.m_effectPrefabs[0].m_prefab, vfx_PocketTeleporterName);
                for (int i = vfx_PocketTeleporter.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = vfx_PocketTeleporter.transform.GetChild(i);
                    child.parent = null;
                    UnityEngine.Object.Destroy(child.gameObject);
                }

                Instantiate(waystone.transform.Find("WayEffect/sfx"), vfx_PocketTeleporter.transform).name = vfx_PocketTeleporterSfx;

                GameObject pointLight = Instantiate(waystone.transform.Find("WayEffect/Point light"), vfx_PocketTeleporter.transform).gameObject;
                pointLight.name = vfx_PocketTeleporterLight;
                pointLight.transform.localPosition = new Vector3(0f, 0f, -0.2f);

                Light light = pointLight.GetComponent<Light>();
                light.cullingMask = -1;
                light.shadows = LightShadows.None;
                light.intensity = 0f;

                GameObject sparcs = Instantiate(waystone.transform.Find("WayEffect/Particle System sparcs"), vfx_PocketTeleporter.transform).gameObject;
                sparcs.name = vfx_PocketTeleporterParticles;
                sparcs.transform.localPosition = new Vector3(0f, 0f, -0.1f);

                ParticleSystem ps = sparcs.GetComponent<ParticleSystem>();

                MainModule main = ps.main;
                main.maxParticles = PocketTeleporter.particlesMaxAmount.Value;
                main.duration = 20f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                main.simulationSpeed = 1f;
                main.startSpeed = 1;
                main.startLifetime = 1;

                EmissionModule emission = ps.emission;

                MinMaxCurve rot = emission.rateOverTime;
                rot.mode = ParticleSystemCurveMode.Curve;
                rot.curve = AnimationCurve.Linear(0, PocketTeleporter.particlesMinRateOverTime.Value, 1, PocketTeleporter.particlesMaxRateOverTime.Value);

                emission.rateOverTime = rot;

                if (PocketTeleporter.particlesCollision.Value)
                {
                    CollisionModule collision = ps.collision;
                    collision.enabled = true;
                    collision.type = ParticleSystemCollisionType.World;
                }

                ForceOverLifetimeModule force = ps.forceOverLifetime;
                force.enabled = true;

                MinMaxCurve forceZ = force.z;
                forceZ.mode = ParticleSystemCurveMode.Curve;
                forceZ.curve = AnimationCurve.Linear(0, PocketTeleporter.particlesMinForceOverTime.Value, 1, PocketTeleporter.particlesMaxForceOverTime.Value);

                force.z = forceZ;
                force.zMultiplier = 3;
            }

            if ((bool)vfx_PocketTeleporter && !ZNetScene.instance.m_namedPrefabs.ContainsKey(vfx_PocketTeleporterHash))
            {
                ZNetScene.instance.m_prefabs.Add(vfx_PocketTeleporter);
                ZNetScene.instance.m_namedPrefabs[vfx_PocketTeleporterHash] = vfx_PocketTeleporter;
            }
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        public static class ObjectDB_Awake_AddStatusEffects
        {
            public static void AddCustomStatusEffects(ObjectDB odb)
            {
                RegisterEffects();

                if (odb.m_StatusEffects.Count > 0)
                {
                    if (!odb.m_StatusEffects.Any(se => se.name == statusEffectPocketTeleporterName))
                    {
                        SE_PocketTeleporter statusEffect = ScriptableObject.CreateInstance<SE_PocketTeleporter>();
                        statusEffect.name = statusEffectPocketTeleporterName;
                        statusEffect.m_nameHash = statusEffectPocketTeleporterHash;
                        statusEffect.m_icon = iconPocketTeleporter;
                        statusEffect.m_noiseModifier = 1;
                        statusEffect.m_stealthModifier = -1;
                        statusEffect.m_staminaDrainPerSec = 10f;

                        statusEffect.m_name = statusEffectName;
                        statusEffect.m_tooltip = statusEffectTooltip;

                        statusEffect.m_startEffects.m_effectPrefabs = new[] {
                            new EffectList.EffectData()
                                {
                                    m_prefab = vfx_PocketTeleporter,
                                    m_enabled = true,
                                    m_inheritParentScale = true,
                                    m_attach = true,
                                }
                        };

                        statusEffect.m_ttl = 10;

                        odb.m_StatusEffects.Add(statusEffect);
                    }
                }
            }

            private static void Postfix(ObjectDB __instance)
            {
                AddCustomStatusEffects(__instance);
            }
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        public static class ObjectDB_CopyOtherDB_SE_Season
        {
            private static void Postfix(ObjectDB __instance)
            {
                ObjectDB_Awake_AddStatusEffects.AddCustomStatusEffects(__instance);
            }
        }

        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.SetupGui))]
        public static class FejdStartup_SetupGui_AddLocalizedWords
        {
            private static void Postfix()
            {
                Localization_SetupLanguage_AddLocalizedWords.AddTranslations(Localization.instance);
            }
        }

        [HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
        public static class Localization_SetupLanguage_AddLocalizedWords
        {
            private static void Postfix(Localization __instance)
            {
                AddTranslations(__instance);
            }

            public static void AddTranslations(Localization localization)
            {
                localization.AddWord(statusEffectName.Replace("$", ""), GetName(localization));
                localization.AddWord(statusEffectTooltip.Replace("$", ""), GetTooltip(localization));
            }

            private static string GetName(Localization localization)
            {
                return GetTranslation(localization, "$npc_dvergrmage_random_goodbye1").Replace(".", "");
            }

            private static string GetTooltip(Localization localization)
            {
                return GetTranslation(localization, "$npc_dvergrmage_random_goodbye4", "Teleporting").Replace(".", "");
            }

            private static string GetTranslation(Localization localization, string word, string defaultValue = "")
            {
                string key = word.Replace("$", "");
                return localization.m_translations.ContainsKey(key) ? localization.m_translations[key] : defaultValue;
            }
        }
    }
}
