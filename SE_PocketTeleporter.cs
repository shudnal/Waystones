using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;
using static UnityEngine.ParticleSystem;

namespace WaystoneTeleporter
{
    public class SE_WaystoneTeleporter : SE_Stats
    {
        public const string statusEffectWaystoneTeleporterName = "WaystoneTeleporter";
        public static readonly int statusEffectWaystoneTeleporterHash = statusEffectWaystoneTeleporterName.GetStableHashCode();

        public const string vfx_WaystoneTeleporterName = "vfx_WaystoneTeleporter";
        public static readonly int vfx_WaystoneTeleporterHash = vfx_WaystoneTeleporterName.GetStableHashCode();
        public const string vfx_WaystoneTeleporterParticles = "Sparcs";
        public const string vfx_WaystoneTeleporterSfx = "Sfx";
        public const string vfx_WaystoneTeleporterLight = "Light";

        public const string statusEffectName = "$se_waystoneteleporter_name";
        public const string statusEffectTooltip = "$se_waystoneteleporter_tooltip";

        public static readonly int s_gpower = ZSyncAnimation.GetHash("gpower");

        public static Sprite iconWaystoneTeleporter;
        public static GameObject vfx_WaystoneTeleporter;

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

            sfx = effect.transform.Find(vfx_WaystoneTeleporterSfx).GetComponent<AudioSource>();
            volume = sfx.volume;

            main = effect.transform.Find(vfx_WaystoneTeleporterParticles).GetComponent<ParticleSystem>().main;

            light = effect.transform.Find(vfx_WaystoneTeleporterLight).GetComponent<Light>();

            character.StopEmote();
        }

        public override void Stop()
        {
            base.Stop();
            
            if (m_character)
            {
                m_character.GetZAnim().SetSpeed(1f);
                m_character.m_lookTransitionTime = 0f;

                if (m_character == Player.m_localPlayer)
                    WorldData.SetCooldown(teleportTriggered ? targetCooldown : WaystoneTeleporter.cooldownShort.Value);
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

            if (!(bool)vfx_WaystoneTeleporter)
            {
                WayStone waystone = Resources.FindObjectsOfTypeAll<WayStone>().FirstOrDefault();
                if (waystone == null)
                    return;

                vfx_WaystoneTeleporter = CustomPrefabs.InitPrefabClone(ObjectDB.instance.GetStatusEffect(SEMan.s_statusEffectCold).m_startEffects.m_effectPrefabs[0].m_prefab, vfx_WaystoneTeleporterName);
                for (int i = vfx_WaystoneTeleporter.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = vfx_WaystoneTeleporter.transform.GetChild(i);
                    child.parent = null;
                    UnityEngine.Object.Destroy(child.gameObject);
                }

                Instantiate(waystone.transform.Find("WayEffect/sfx"), vfx_WaystoneTeleporter.transform).name = vfx_WaystoneTeleporterSfx;

                GameObject pointLight = Instantiate(waystone.transform.Find("WayEffect/Point light"), vfx_WaystoneTeleporter.transform).gameObject;
                pointLight.name = vfx_WaystoneTeleporterLight;
                pointLight.transform.localPosition = new Vector3(0f, 0f, -0.2f);

                Light light = pointLight.GetComponent<Light>();
                light.cullingMask = -1;
                light.shadows = LightShadows.None;
                light.intensity = 0f;

                GameObject sparcs = Instantiate(waystone.transform.Find("WayEffect/Particle System sparcs"), vfx_WaystoneTeleporter.transform).gameObject;
                sparcs.name = vfx_WaystoneTeleporterParticles;
                sparcs.transform.localPosition = new Vector3(0f, 0f, -0.1f);

                ParticleSystem ps = sparcs.GetComponent<ParticleSystem>();

                MainModule main = ps.main;
                main.maxParticles = WaystoneTeleporter.particlesMaxAmount.Value;
                main.duration = 20f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                main.simulationSpeed = 1f;
                main.startSpeed = 1;
                main.startLifetime = 1;

                EmissionModule emission = ps.emission;

                MinMaxCurve rot = emission.rateOverTime;
                rot.mode = ParticleSystemCurveMode.Curve;
                rot.curve = AnimationCurve.Linear(0, WaystoneTeleporter.particlesMinRateOverTime.Value, 1, WaystoneTeleporter.particlesMaxRateOverTime.Value);

                emission.rateOverTime = rot;

                if (WaystoneTeleporter.particlesCollision.Value)
                {
                    CollisionModule collision = ps.collision;
                    collision.enabled = true;
                    collision.type = ParticleSystemCollisionType.World;
                }

                ForceOverLifetimeModule force = ps.forceOverLifetime;
                force.enabled = true;

                MinMaxCurve forceZ = force.z;
                forceZ.mode = ParticleSystemCurveMode.Curve;
                forceZ.curve = AnimationCurve.Linear(0, WaystoneTeleporter.particlesMinForceOverTime.Value, 1, WaystoneTeleporter.particlesMaxForceOverTime.Value);

                force.z = forceZ;
                force.zMultiplier = 3;
            }

            if ((bool)vfx_WaystoneTeleporter && !ZNetScene.instance.m_namedPrefabs.ContainsKey(vfx_WaystoneTeleporterHash))
            {
                ZNetScene.instance.m_prefabs.Add(vfx_WaystoneTeleporter);
                ZNetScene.instance.m_namedPrefabs[vfx_WaystoneTeleporterHash] = vfx_WaystoneTeleporter;
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
                    if (!odb.m_StatusEffects.Any(se => se.name == statusEffectWaystoneTeleporterName))
                    {
                        SE_WaystoneTeleporter statusEffect = ScriptableObject.CreateInstance<SE_WaystoneTeleporter>();
                        statusEffect.name = statusEffectWaystoneTeleporterName;
                        statusEffect.m_nameHash = statusEffectWaystoneTeleporterHash;
                        statusEffect.m_icon = iconWaystoneTeleporter;
                        statusEffect.m_noiseModifier = 1;
                        statusEffect.m_stealthModifier = -1;
                        statusEffect.m_staminaDrainPerSec = 10f;

                        statusEffect.m_name = statusEffectName;
                        statusEffect.m_tooltip = statusEffectTooltip;

                        statusEffect.m_startEffects.m_effectPrefabs = new[] {
                            new EffectList.EffectData()
                                {
                                    m_prefab = vfx_WaystoneTeleporter,
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
    }
}
