using System;
using UnityEngine;

namespace TA.Toolchain.RenderAudit
{
    public enum RenderAuditScanMode
    {
        CurrentScene
    }

    public enum RenderAuditReportFormat
    {
        JSON
    }

    [Serializable]
    public sealed class RenderAuditThresholds
    {
        public int realtimeShadowLightsWarn = 1;
        public float shadowDistanceWarn = 60f;
        public int rendererCountWarn = 2000;
        public int transparentRendererCountWarn = 300;
        public int particleSystemCountWarn = 80;
        public int realtimeReflectionProbeWarn = 0;
        public int textureMaxSizeWarn = 4096;
        public int texture2kCountWarn = 80;
        public int texture4kCountWarn = 10;
        public int topNTextures = 20;
        public int triWarn = 50000;
        public int vertWarn = 50000;
        public int topNMeshes = 20;
    }

    [CreateAssetMenu(menuName = "TA Toolchain/Render Audit Config", fileName = "RenderAuditConfig")]
    public sealed class RenderAuditConfig : ScriptableObject
    {
        public RenderAuditScanMode scanMode = RenderAuditScanMode.CurrentScene;
        public RenderAuditReportFormat reportFormat = RenderAuditReportFormat.JSON;
        public string outputDir = "Assets/_Tools/TA_ToolSuite/Reports/RenderAudit";

        [Header("Behaviors")]
        public bool pingSceneObjectsAndAssets = true;

        [Header("Thresholds")]
        public RenderAuditThresholds thresholds = new RenderAuditThresholds();

        [Header("Options")]
        public bool enableSSRDetect = true;
        public bool enableTransparencySortingDetect = true;
        public bool enableOverdrawApprox = true;
        public bool enableRecommendations = true;
        public bool enableScriptKeywordScan = true;
    }
}
