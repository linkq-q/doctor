using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPSceneDoctor.Runtime
{
    public sealed class USD_DistanceFogSettings : ScriptableObject
    {
        [Range(0f, 1f)] public float intensity = 0.12f;
        public Color fogTint = new Color(0.62f, 0.68f, 0.75f, 1f);
        public float distanceStart = 20f;
        public float distanceEnd = 120f;
    }

    public sealed class USD_DistanceFogFeature : ScriptableRendererFeature
    {
        class Pass : ScriptableRenderPass
        {
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // Base Pack v1: placeholder pass for safe installation wiring.
            }
        }

        [SerializeField] private USD_DistanceFogSettings settings;
        private Pass _pass;

        public override void Create()
        {
            _pass = new Pass { renderPassEvent = RenderPassEvent.AfterRenderingSkybox };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || settings.intensity <= 0f) return;
            renderer.EnqueuePass(_pass);
        }

        public void SetSettings(USD_DistanceFogSettings s)
        {
            settings = s;
        }
    }
}
