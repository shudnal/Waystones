using UnityEngine;
using static UnityEngine.ParticleSystem;

namespace Waystones
{
    internal class WaystoneTravelEffect : MonoBehaviour
    {
        private ZNetView m_nview;

        private float volume;
        private AudioSource sfx;
        private MainModule main;
        private Light light;

        public static readonly int progressHash = "TravelProgress".GetStableHashCode();
        public static bool initial = false;

        void Awake()
        {
            if (initial)
                return;

            m_nview = GetComponent<ZNetView>();
        }

        void Start()
        {
            sfx = base.transform.Find(SE_Waystone.vfx_WaystonesSfx).GetComponent<AudioSource>();
            sfx.enabled = true;
            volume = sfx.volume; // Initial value

            main = base.transform.Find(SE_Waystone.vfx_WaystonesParticles).GetComponent<ParticleSystem>().main;

            light = base.transform.Find(SE_Waystone.vfx_WaystonesLight).GetComponent<Light>();
        }

        void Update()
        {
            float progress = GetProgress(m_nview);

            main.startLifetime = 0.5f + 5.5f * progress;
            main.startSpeed = 4f - 3.5f * progress;
            main.simulationSpeed = 1f + progress;

            light.intensity = 1f + 2f * progress;
            light.range = 5f + 15f * progress;

            sfx.volume = volume * progress;
        }

        internal static void SetProgress(ZNetView netView, float progress)
        {
            if (netView == null || !netView.IsValid())
                return;

            netView.GetZDO().Set(progressHash, progress);
        }

        internal static float GetProgress(ZNetView netView)
        {
            if (netView == null || !netView.IsValid())
                return 0f;

            return netView.GetZDO().GetFloat(progressHash, 0f);
        }
    }
}
