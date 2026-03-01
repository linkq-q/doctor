using System.Linq;
using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public static class USD_SettingsDebugMenu
    {
        [MenuItem("Tools/URPSceneDoctor/Debug/Print USD Settings")]
        public static void PrintUsdSettings()
        {
            var guid = AssetDatabase.FindAssets("t:USD_Settings").FirstOrDefault();
            var path = string.IsNullOrEmpty(guid) ? "Assets/_Tools/URPSceneDoctor/Config/USD_Settings.asset" : AssetDatabase.GUIDToAssetPath(guid);
            var settings = AssetDatabase.LoadAssetAtPath<USD_Settings>(path);
            if (settings == null)
            {
                Debug.LogError("[USD][Debug] USD_Settings asset not found or still missing script. Path=" + path);
                return;
            }

            Debug.Log("[USD][Debug] USD_Settings loaded: " + path +
                      "\nllmProvider=" + settings.llmProvider +
                      "\nllmBaseUrl=" + settings.llmBaseUrl +
                      "\nllmModel=" + settings.llmModel +
                      "\nllmTimeoutSec=" + settings.llmTimeoutSec +
                      "\nsingleCallTimeoutSec=" + settings.singleCallTimeoutSec +
                      "\nllmMaxTokens=" + settings.llmMaxTokens +
                      "\nllmTemperature=" + settings.llmTemperature +
                      "\nshowAiSendAndReceiveToast=" + settings.showAiSendAndReceiveToast +
                      "\ndumpRawResponseToFile=" + settings.dumpRawResponseToFile +
                      "\nrawResponseDumpPath=" + settings.rawResponseDumpPath +
                      "\nsmhMaxOffset=" + settings.smhMaxOffset);

            EditorGUIUtility.PingObject(settings);
            Selection.activeObject = settings;
        }
    }
}
