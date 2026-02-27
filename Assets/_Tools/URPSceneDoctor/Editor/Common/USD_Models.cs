using System;
using System.Collections.Generic;

namespace URPSceneDoctor.Editor
{
    [Serializable]
    public sealed class USD_ScanSnapshot
    {
        public string unityVersion;
        public string urpPackageVersion;
        public string platformHint;
        public string activeQualityLevel;
        public string activeURPAssetGuid;
        public string activeURPAssetName;
        public string activeRendererDataGuid;
        public string activeRendererDataName;

        public bool hasDirectionalLight;
        public string directionalLightName;
        public bool dirLightShadowsEnabled;
        public float dirLightShadowStrength;
        public bool dirLightColorTemperatureEnabled;
        public float dirLightTemperature;
        public string ambientMode;
        public float ambientIntensity;
        public bool hasSkyboxMaterial;
        public bool hasAnyReflectionProbe;
        public int reflectionProbeCount;

        public float shadowDistance;
        public int shadowCascadeCount;
        public bool supportsHDR;
        public int msaaSampleCount;
        public float renderScale;
        public bool additionalLightsEnabled;
        public int additionalLightsPerObjectLimit;
        public bool postProcessingEnabled;

        public bool hasGlobalVolume;
        public string globalVolumeObjectName;
        public string globalVolumeProfileGuid;
        public string globalVolumeProfileName;
        public List<string> enabledOverrides = new List<string>();
        public int volumeCountTotal;
        public bool hasMultipleOverlappingGlobalLikeVolumes;

        public int rendererCount;
        public int materialCountDistinct;
        public int shaderCountDistinct;
        public int transparentRendererCount;
        public bool hasManyDifferentShaders;

        public List<string> warnings = new List<string>();
        public List<string> personalDeltaHints = new List<string>();
    }

    [Serializable]
    public sealed class USD_Prescription
    {
        public int priority;
        public string actionText;
        public string targetPathHint;
        public string recommendedRange;
        public List<string> alternatives = new List<string>();
    }

    [Serializable]
    public sealed class USD_Verification
    {
        public List<string> steps = new List<string>();
        public List<string> referenceShots = new List<string>();
        public string performanceBudgetHint;
    }

    [Serializable]
    public sealed class USD_ApplyAction
    {
        public string type;
        public string payloadJson;
        public bool requiresUserOptIn;
    }

    [Serializable]
    public sealed class USD_WorkOrder
    {
        public string id;
        public string title;
        public string severity;
        public string category;
        public List<string> symptoms = new List<string>();
        public List<string> evidence = new List<string>();
        public string diagnosis;
        public List<USD_Prescription> prescriptions = new List<USD_Prescription>();
        public USD_Verification verification = new USD_Verification();
        public string costNotes;
        public List<USD_ApplyAction> applyActions = new List<USD_ApplyAction>();
    }

    [Serializable]
    public sealed class USD_Report
    {
        public string toolVersion;
        public string module;
        public string sceneName;
        public string timestamp;
        public USD_ScanSnapshot snapshot;
        public List<USD_WorkOrder> workOrders = new List<USD_WorkOrder>();
        public List<string> appliedChanges = new List<string>();
        public List<string> warnings = new List<string>();
        public List<string> personalDeltaHints = new List<string>();
    }

    [Serializable]
    public sealed class USD_DeltaField
    {
        public string path;
        public string before;
        public string after;
        public string deltaHint;
    }

    [Serializable]
    public sealed class USD_DeltaPatch
    {
        public string sceneName;
        public string beforeSnapshotPath;
        public string afterSnapshotPath;
        public List<USD_DeltaField> changedFields = new List<USD_DeltaField>();
        public List<string> frequentPatterns = new List<string>();
        public List<string> recommendedRanges = new List<string>();
    }
}
