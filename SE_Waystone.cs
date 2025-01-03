using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;
using static UnityEngine.ParticleSystem;

namespace Waystones
{
    public class SE_Waystone : SE_Stats
    {
        public const string statusEffectWaystonesName = "WaystoneFastTravel";
        public static readonly int statusEffectWaystonesHash = statusEffectWaystonesName.GetStableHashCode();

        public const string vfx_WaystonesName = "vfx_Waystones";
        public static readonly int vfx_WaystonesHash = vfx_WaystonesName.GetStableHashCode();
        public const string vfx_WaystonesParticles = "Sparcs";
        public const string vfx_WaystonesSfx = "Sfx";
        public const string vfx_WaystonesLight = "Light";

        public const string statusEffectName = "$se_waystone_name";
        public const string statusEffectTooltip = "$se_waystone_tooltip";

        public static readonly int s_gpower = ZSyncAnimation.GetHash("gpower");

        public static Sprite iconWaystones;
        public static GameObject vfx_Waystones;

        [NonSerialized]
        public Vector3 targetPoint = Vector3.zero;
        [NonSerialized]
        public Quaternion targetRotation = Quaternion.identity;
        [NonSerialized]
        public double targetCooldown = 0;
        [NonSerialized]
        private bool lookDirTriggered;
        [NonSerialized]
        private bool teleportTriggered;
        [NonSerialized]
        private ZNetView effectNetView;

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);

            if (m_startEffectInstances == null)
                return;

            if (effectNetView == null)
                effectNetView = m_startEffectInstances[0].GetComponent<ZNetView>();

            WaystoneTravelEffect.SetProgress(effectNetView, m_time / m_ttl);

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
                    localPlayer.TeleportTo(targetPoint, targetRotation == Quaternion.identity ? localPlayer.transform.rotation : targetRotation, distantTeleport: true);
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
                    WorldData.SetCooldown(teleportTriggered ? targetCooldown : Waystones.cooldownShort.Value);
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

            if (!(bool)vfx_Waystones)
            {
                WayStone waystone = Resources.FindObjectsOfTypeAll<WayStone>().FirstOrDefault();
                if (waystone == null)
                    return;

                vfx_Waystones = CustomPrefabs.InitPrefabClone(ObjectDB.instance.GetStatusEffect(SEMan.s_statusEffectCold).m_startEffects.m_effectPrefabs[0].m_prefab, vfx_WaystonesName);
                for (int i = vfx_Waystones.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = vfx_Waystones.transform.GetChild(i);
                    child.parent = null;
                    UnityEngine.Object.Destroy(child.gameObject);
                }

                WaystoneTravelEffect.initial = true;
                vfx_Waystones.AddComponent<WaystoneTravelEffect>();
                WaystoneTravelEffect.initial = false;

                ZSyncTransform zst = vfx_Waystones.GetComponent<ZSyncTransform>();
                zst.m_syncRotation = true;
                zst.m_useGravity = false;
                zst.m_syncScale = false;

                Instantiate(waystone.transform.Find("WayEffect/sfx"), vfx_Waystones.transform).name = vfx_WaystonesSfx;

                GameObject pointLight = Instantiate(waystone.transform.Find("WayEffect/Point light"), vfx_Waystones.transform).gameObject;
                pointLight.name = vfx_WaystonesLight;
                pointLight.transform.localPosition = new Vector3(0f, 0f, -0.2f);

                Light light = pointLight.GetComponent<Light>();
                light.cullingMask = -1;
                light.shadows = LightShadows.None;
                light.intensity = 0f;

                GameObject sparcs = Instantiate(waystone.transform.Find("WayEffect/Particle System sparcs"), vfx_Waystones.transform).gameObject;
                sparcs.name = vfx_WaystonesParticles;
                sparcs.transform.localPosition = new Vector3(0f, 0f, -0.1f);

                ParticleSystem ps = sparcs.GetComponent<ParticleSystem>();

                MainModule main = ps.main;
                main.maxParticles = Waystones.particlesMaxAmount.Value;
                main.duration = 20f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                main.simulationSpeed = 1f;
                main.startSpeed = 1;
                main.startLifetime = 1;

                EmissionModule emission = ps.emission;

                MinMaxCurve rot = emission.rateOverTime;
                rot.mode = ParticleSystemCurveMode.Curve;
                rot.curve = AnimationCurve.Linear(0, Waystones.particlesMinRateOverTime.Value, 1, Waystones.particlesMaxRateOverTime.Value);

                emission.rateOverTime = rot;

                if (Waystones.particlesCollision.Value)
                {
                    CollisionModule collision = ps.collision;
                    collision.enabled = true;
                    collision.type = ParticleSystemCollisionType.World;
                }

                ForceOverLifetimeModule force = ps.forceOverLifetime;
                force.enabled = true;

                MinMaxCurve forceZ = force.z;
                forceZ.mode = ParticleSystemCurveMode.Curve;
                forceZ.curve = AnimationCurve.Linear(0, Waystones.particlesMinForceOverTime.Value, 1, Waystones.particlesMaxForceOverTime.Value);

                force.z = forceZ;
                force.zMultiplier = 3;
            }

            if ((bool)vfx_Waystones)
            {
                if (ZNetScene.instance.m_namedPrefabs.ContainsKey(vfx_WaystonesHash))
                {
                    ZNetScene.instance.m_prefabs.Remove(ZNetScene.instance.m_namedPrefabs[vfx_WaystonesHash]);
                    ZNetScene.instance.m_namedPrefabs.Remove(vfx_WaystonesHash);
                }

                ZNetScene.instance.m_prefabs.Add(vfx_Waystones);
                ZNetScene.instance.m_namedPrefabs.Add(vfx_WaystonesHash, vfx_Waystones);
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
                    if (!odb.m_StatusEffects.Any(se => se.name == statusEffectWaystonesName))
                    {
                        SE_Waystone statusEffect = ScriptableObject.CreateInstance<SE_Waystone>();
                        statusEffect.name = statusEffectWaystonesName;
                        statusEffect.m_nameHash = statusEffectWaystonesHash;
                        statusEffect.m_icon = iconWaystones;
                        statusEffect.m_noiseModifier = 1;
                        statusEffect.m_stealthModifier = -1;
                        statusEffect.m_staminaDrainPerSec = 10f;

                        statusEffect.m_name = statusEffectName;
                        statusEffect.m_tooltip = statusEffectTooltip;

                        statusEffect.m_startEffects.m_effectPrefabs = new[] {
                            new EffectList.EffectData()
                                {
                                    m_prefab = vfx_Waystones,
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
        public static class ObjectDB_CopyOtherDB_AddStatusEffects
        {
            private static void Postfix(ObjectDB __instance)
            {
                ObjectDB_Awake_AddStatusEffects.AddCustomStatusEffects(__instance);
            }
        }
    }
}
