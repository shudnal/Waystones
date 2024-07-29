using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Waystones.Waystones;
using static UnityEngine.ParticleSystem;

namespace Waystones
{
    internal class PieceWaystone
    {
        public static Sprite itemWaystone;
        internal static GameObject waystonePrefab;
        internal const string waystoneName = "Waystone_small";
        public static int waystoneHash = waystoneName.GetStableHashCode();

        public const string waystonePieceName = "$ws_piece_waystone_name";
        public const string waystonePieceDescription = "$ws_piece_waystone_description";

        public static void RegisterPiece()
        {
            if (!(bool)waystonePrefab)
            {
                WayStone waystone = Resources.FindObjectsOfTypeAll<WayStone>().FirstOrDefault(ws => ws.name == "Waystone");
                if (waystone == null)
                    return;

                waystonePrefab = CustomPrefabs.InitPrefabClone(waystone.gameObject, waystoneName);
                waystonePrefab.transform.localScale *= 0.15f;

                LODGroup lodGroup = waystonePrefab.GetComponent<LODGroup>();

                LOD[] lods = lodGroup.GetLODs();

                List<Renderer> renderers = lods[0].renderers.Where(r => r.name == "model").ToList();
                renderers.Add(waystonePrefab.transform.Find("WayEffect/Particle System sparcs").GetComponent<ParticleSystemRenderer>());
                lods[0].renderers = renderers.ToArray();
                lodGroup.SetLODs(lods);

                UnityEngine.Object.Destroy(waystonePrefab.transform.Find("WayEffect/Particle System").gameObject);
                UnityEngine.Object.Destroy(waystonePrefab.transform.Find("WayEffect/sfx").gameObject);

                Transform particles = waystonePrefab.transform.Find("WayEffect/Particle System (1)");
                particles.localScale *= 0.75f;
                MainModule main = particles.GetComponent<ParticleSystem>().main;
                main.startSize = 0.75f;
                main.startSpeedMultiplier = 0.1f;
                main.maxParticles = 75;
                ShapeModule shape = particles.GetComponent<ParticleSystem>().shape;
                shape.radius = 0.005f;
                EmissionModule emission = particles.GetComponent<ParticleSystem>().emission;
                emission.rateOverTime = 5f;

                waystonePrefab.transform.Find("WayEffect/Particle System sparcs").localScale *= 0.4f;

                Light innerLight = waystonePrefab.transform.Find("WayEffect/Point light").GetComponent<Light>();
                innerLight.intensity = 1.1f;

                Light light = waystonePrefab.transform.Find("Point light (1)").GetComponent<Light>();
                light.intensity = 1.1f;
                light.gameObject.AddComponent<LightLod>().m_lightDistance = 50f;

                waystonePrefab.GetComponentInChildren<EffectArea>().m_type = EffectArea.Type.Fire;
                
                ZNetView netview = CustomPrefabs.AddComponent(waystonePrefab, typeof(ZNetView)) as ZNetView;
                netview.m_persistent = true;
                netview.m_distant = true;
                netview.m_type = ZDO.ObjectType.Solid;

                Piece piece = CustomPrefabs.AddComponent(waystonePrefab, typeof(Piece)) as Piece;
                piece.m_icon = itemWaystone;
                piece.m_name = waystonePieceName;
                piece.m_description = waystonePieceDescription;
                piece.m_clipGround = true;
                piece.m_clipEverything = true;
                piece.m_notOnTiltingSurface = true;
                piece.m_noClipping = false;
                piece.m_randomTarget = false;

                WearNTear wnt = CustomPrefabs.AddComponent(waystonePrefab, typeof(WearNTear)) as WearNTear;

                GameObject m_new = new GameObject("new");
                m_new.transform.SetParent(waystonePrefab.transform);
                m_new.transform.SetSiblingIndex(0);

                waystonePrefab.transform.Find("model").SetParent(m_new.transform);

                wnt.m_new = m_new;
                wnt.m_worn = m_new;
                wnt.m_broken = m_new;

                wnt.m_noRoofWear = false;
                wnt.m_noSupportWear = false;
                wnt.m_burnable = false;
                wnt.m_supports = false;
                wnt.m_materialType = WearNTear.MaterialType.Stone;
                wnt.m_health = 1000;
                wnt.m_damages.m_pierce = HitData.DamageModifier.Resistant;
                wnt.m_damages.m_chop = HitData.DamageModifier.Ignore;
                wnt.m_damages.m_pickaxe = HitData.DamageModifier.Ignore;
                wnt.m_damages.m_fire = HitData.DamageModifier.Resistant;
                wnt.m_damages.m_frost = HitData.DamageModifier.Resistant;
                wnt.m_damages.m_poison = HitData.DamageModifier.Immune;
                wnt.m_damages.m_spirit = HitData.DamageModifier.Immune;

                WayStone original = waystonePrefab.GetComponent<WayStone>();

                WaystoneSmall.initial = true;

                WaystoneSmall ws_waystone = waystonePrefab.AddComponent<WaystoneSmall>();

                WaystoneSmall.initial = false;

                UnityEngine.Object.Destroy(original);

                GameObject point = new GameObject("GuidePoint");
                point.transform.SetParent(waystonePrefab.transform);
                point.transform.localPosition = new Vector3(-0.5f, 8.9f, -1.3f);

                GuidePoint guidePoint = CustomPrefabs.AddComponent(point, typeof(GuidePoint)) as GuidePoint;
                guidePoint.m_text.m_alwaysSpawn = false;
                guidePoint.m_text.m_key = "ws_waystone";
                guidePoint.m_text.m_topic = "$ws_tutorial_waystone_topic";
                guidePoint.m_text.m_label = "$ws_tutorial_waystone_label";
                guidePoint.m_text.m_text = "$ws_tutorial_waystone_text";

                LogInfo("Waystone prefab added");
            }
            
            if ((bool)waystonePrefab)
            {
                GameObject stone_pile = ZNetScene.instance.GetPrefab("stone_pile");
                if (stone_pile != null)
                {
                    waystonePrefab.GetComponent<Piece>().m_placeEffect.m_effectPrefabs = stone_pile.GetComponent<Piece>().m_placeEffect.m_effectPrefabs.ToArray();
                    waystonePrefab.GetComponent<WearNTear>().m_destroyedEffect.m_effectPrefabs = stone_pile.GetComponent<WearNTear>().m_destroyedEffect.m_effectPrefabs.ToArray();
                    waystonePrefab.GetComponent<WearNTear>().m_hitEffect.m_effectPrefabs = stone_pile.GetComponent<WearNTear>().m_hitEffect.m_effectPrefabs.ToArray();
                }

                WaystoneSmall componentWayStone = waystonePrefab.GetComponent<WaystoneSmall>();

                WayStone waystone = Resources.FindObjectsOfTypeAll<WayStone>().FirstOrDefault(obj => obj.name == "Waystone");
                if (waystone != null)
                    componentWayStone.m_activateEffect.m_effectPrefabs = waystone.m_activeEffect.m_effectPrefabs.ToArray();

                PrivateArea privateArea = ZNetScene.instance.GetPrefab("guard_stone")?.GetComponent<PrivateArea>();
                if (privateArea != null)
                    componentWayStone.m_deactivateEffect.m_effectPrefabs = privateArea.m_removedPermittedEffect.m_effectPrefabs.ToArray();

                GuidePoint guidePoint = ZNetScene.instance.GetPrefab("piece_workbench")?.GetComponentInChildren<GuidePoint>();
                if (guidePoint != null)
                    waystonePrefab.GetComponentInChildren<GuidePoint>().m_ravenPrefab = guidePoint.m_ravenPrefab;

                if (ZNetScene.instance.m_namedPrefabs.ContainsKey(waystoneHash))
                {
                    ZNetScene.instance.m_prefabs.Remove(ZNetScene.instance.m_namedPrefabs[waystoneHash]);
                    ZNetScene.instance.m_namedPrefabs.Remove(waystoneHash);
                }

                ZNetScene.instance.m_prefabs.Add(waystonePrefab);
                ZNetScene.instance.m_namedPrefabs.Add(waystoneHash, waystonePrefab);

                SetPieceRequirements();

                PieceTable hammer = Resources.FindObjectsOfTypeAll<PieceTable>().FirstOrDefault(ws => ws.name == "_HammerPieceTable");
                if (hammer != null && !hammer.m_pieces.Contains(waystonePrefab))
                    hammer.m_pieces.Add(waystonePrefab);
            }
        }

        public static void SetPieceRequirements()
        {
            if (waystonePrefab == null)
                return;

            Piece piece = waystonePrefab.GetComponent<Piece>();

            List<Piece.Requirement> requirements = new List<Piece.Requirement>();
            foreach (string requirement in pieceRecipe.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] req = requirement.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (req.Length != 2)
                    continue;

                int amount = int.Parse(req[1]);
                if (amount <= 0)
                    continue;

                var prefab = ObjectDB.instance.GetItemPrefab(req[0].Trim());
                if (prefab == null)
                    continue;

                requirements.Add(new Piece.Requirement()
                {
                    m_amount = amount,
                    m_resItem = prefab.GetComponent<ItemDrop>(),
                    m_recover = true
                });
            };

            piece.m_resources = requirements.ToArray();
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        public static class ZNetScene_Awake_AddPiece
        {
            private static void Postfix()
            {
                RegisterPiece();
            }
        }
    }
}
