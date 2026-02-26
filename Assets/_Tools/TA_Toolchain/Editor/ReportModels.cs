using System;
using System.Collections.Generic;

namespace TA.Toolchain.RenderAudit
{
    public enum IssueSeverity
    {
        Error,
        Warning,
        Info
    }

    public enum IssueCategory
    {
        Performance,
        Texture,
        Material,
        Transparency,
        SSR,
        Reference,
        URP,
        Recommendation
    }

    public enum TargetType
    {
        SceneObject,
        Asset,
        ProjectSetting
    }

    public enum EstimatedCost
    {
        Low,
        Med,
        High
    }

    [Serializable]
    public sealed class ReportMeta
    {
        public string unityVersion;
        public string urpVersion;
        public string sceneName;
        public string timestamp;
    }

    [Serializable]
    public sealed class ThresholdSnapshot
    {
        public int realtimeShadowLightsWarn;
        public float shadowDistanceWarn;
        public int rendererCountWarn;
        public int transparentRendererCountWarn;
        public int particleSystemCountWarn;
        public int realtimeReflectionProbeWarn;
        public int textureMaxSizeWarn;
        public int texture2kCountWarn;
        public int texture4kCountWarn;
        public int triWarn;
        public int vertWarn;
        public int topNTextures;
        public int topNMeshes;
    }

    [Serializable]
    public sealed class Summary
    {
        public int issueCount;
        public int errorCount;
        public int warningCount;
        public int infoCount;

        public int rendererCount;
        public int transparentRendererCount;
        public int lightCount;
        public int realtimeShadowLightCount;
        public int particleSystemCount;
        public int reflectionProbeCount;

        public ThresholdSnapshot thresholds;
    }

    [Serializable]
    public sealed class Issue
    {
        public string id;
        public IssueSeverity severity;
        public IssueCategory category;
        public string title;
        public string detail;
        public TargetType targetType;
        public string targetPath;
        public string targetGuid;
        public string[] suggestionSteps;
    }

    [Serializable]
    public sealed class TextureOffender
    {
        public string assetPath;
        public string guid;
        public int width;
        public int height;
        public int maxSize;
        public bool mipmap;
        public bool sRGB;
        public string compression;
        public long estimatedBytes;
    }

    [Serializable]
    public sealed class MeshOffender
    {
        public string name;
        public string hierarchyPath;
        public int triangles;
        public int vertices;
    }

    [Serializable]
    public sealed class RendererOffender
    {
        public string name;
        public string hierarchyPath;
        public int materialCount;
        public bool transparent;
    }

    [Serializable]
    public sealed class LightOffender
    {
        public string name;
        public string hierarchyPath;
        public string type;
        public bool realtimeShadows;
        public float intensity;
    }

    [Serializable]
    public sealed class TopOffenders
    {
        public List<TextureOffender> textures = new List<TextureOffender>();
        public List<MeshOffender> meshes = new List<MeshOffender>();
        public List<RendererOffender> renderers = new List<RendererOffender>();
        public List<LightOffender> lights = new List<LightOffender>();
    }

    [Serializable]
    public sealed class RenderAuditReport
    {
        public ReportMeta meta = new ReportMeta();
        public Summary summary = new Summary();
        public List<Issue> issues = new List<Issue>();
        public TopOffenders topOffenders = new TopOffenders();
    }

    [Serializable]
    public sealed class Suggestion
    {
        public string title;
        public string why;
        public EstimatedCost estimatedCost;
        public string whereToImplement;
        public string evidenceMissing;
    }

    [Serializable]
    public sealed class CapabilityEvidence
    {
        public string path;
        public string keyword;
    }
}
