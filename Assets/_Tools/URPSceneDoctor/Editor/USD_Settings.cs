using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    // This class intentionally lives under Editor/ so USD_Settings.asset binds to Assembly-CSharp-Editor:URPSceneDoctor.Editor:USD_Settings.
    public class USD_Settings : ScriptableObject
    {
        [Header("Paths")]
        public string reportsRoot = "Assets/_Tools/URPSceneDoctor/Reports";
        public string snapshotsRoot = "Assets/_Tools/URPSceneDoctor/Snapshots";
        public string patchesRoot = "Assets/_Tools/URPSceneDoctor/Patches";
        public string defaultRulePackPath = "Assets/_Tools/URPSceneDoctor/Config/RulePacks/DefaultRulePack.json";
        public string batchSceneRoot = "Assets/_Tools/URPSceneDoctor/SamplePacks";

        [Header("Logging")]
        public bool verboseLogs;

        [Header("Apply")]
        public float defaultApplyStrength = 1f;

        [Header("Screenshot")]
        public int screenshotWidth = 1920;
        public int screenshotHeight = 1080;

        [Header("UX")]
        public bool enableLearningHints = true;
        public string policyBrightnessBucket = "Mid";
        public string language = "Auto";
        public string promptLanguage = "zh";

        [Header("Camera")]
        public string cameraMode = "Auto";
        public Camera manualCamera;

        [Header("Catalog / Policy")]
        public USD_LabelCatalogAsset labelCatalog;
        public USD_TastePolicyAsset defaultTastePolicy;

        [Header("LLM")]
        public string llmProvider = "DeepSeek";
        public string llmEndpointMode = "Auto";
        public string llmBaseUrl = "https://api.deepseek.com/v1";
        public string llmModel = "deepseek-chat";
        public int llmTimeoutSec = 30;
        public int singleCallTimeoutSec = 60;
        public int llmMaxTokens = 600;
        public float llmTemperature = 0.2f;
        public bool showAiSendAndReceiveToast = true;
        public bool dumpRawResponseToFile = true;
        public string rawResponseDumpPath = "Assets/_Tools/URPSceneDoctor/AITuningRuns/{runId}/raw/llm_response.txt";

        [Header("SMH")]
        public float smhMaxOffset = 0.1f;

        public static USD_Settings GetOrCreateSettings()
        {
            const string settingsPath = "Assets/_Tools/URPSceneDoctor/Config/USD_Settings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<USD_Settings>(settingsPath);
            if (settings != null)
            {
                if (settings.defaultTastePolicy == null)
                {
                    settings.defaultTastePolicy = USD_TastePolicyUtil.GetOrCreateDefaultPolicy();
                    EditorUtility.SetDirty(settings);
                }
                if (settings.labelCatalog == null)
                {
                    settings.labelCatalog = USD_LabelCatalogUtil.GetOrCreateDefault();
                    EditorUtility.SetDirty(settings);
                }
                settings.language = USD_Loc.GetLanguageMode();
                AssetDatabase.SaveAssets();
                return settings;
            }

            USD_EditorUtil.EnsureFolder("Assets/_Tools/URPSceneDoctor/Config");
            settings = CreateInstance<USD_Settings>();
            settings.defaultTastePolicy = USD_TastePolicyUtil.GetOrCreateDefaultPolicy();
            settings.labelCatalog = USD_LabelCatalogUtil.GetOrCreateDefault();
            settings.language = USD_Loc.GetLanguageMode();
            AssetDatabase.CreateAsset(settings, settingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }
}
