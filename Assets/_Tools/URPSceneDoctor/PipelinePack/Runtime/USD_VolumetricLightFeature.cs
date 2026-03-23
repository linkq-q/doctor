using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPSceneDoctor.Runtime
{
    public sealed class USD_VolumetricLightSettings : ScriptableObject
    {
        [Range(0f, 1f)] public float intensity = 0.18f;
        [Range(0.1f, 1f)] public float anisotropy = 0.55f;
        [Range(0.01f, 0.2f)] public float jitter = 0.05f;
    }

    public sealed class USD_VolumetricLightFeature : ScriptableRendererFeature
    {
        class Pass : ScriptableRenderPass
        {
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // Base Pack v1: placeholder pass for safe installation wiring.
            }
        }

        [SerializeField] private USD_VolumetricLightSettings settings;
        private Pass _pass;

        public override void Create()
        {
            _pass = new Pass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || settings.intensity <= 0f) return;
            renderer.EnqueuePass(_pass);
        }

        public void SetSettings(USD_VolumetricLightSettings s)
        {
            settings = s;
        }
    }
}
