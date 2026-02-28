using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TA.ToolSuite
{
    public sealed class TTS_RenderAuditAdapter : ITTSModule
    {
        public string Name => "Render Audit";

        public void Draw()
        {
            var check = Check();
            EditorGUILayout.HelpBox(check.Available ? "TA Toolchain Render Audit available." : check.Reason, check.Available ? MessageType.Info : MessageType.Warning);
            using (new EditorGUI.DisabledScope(!check.Available))
            {
                if (GUILayout.Button("Open Render Audit Window"))
                {
                    EditorApplication.ExecuteMenuItem("Tools/TA Toolchain/Render Audit");
                }

                if (GUILayout.Button("Open Unified Render Reports Folder"))
                {
                    var scene = EditorSceneManager.GetActiveScene().name;
                    var folder = TTS_ReportRouter.EnsureToolSceneRunFolder("Reports", "RenderAudit", scene);
                    EditorUtility.RevealInFinder(folder);
                }
            }
        }

        public TTS_QuickCheckItem Check()
        {
            var exists = Directory.Exists("Assets/_Tools/TA_Toolchain");
            return new TTS_QuickCheckItem
            {
                Name = Name,
                Available = exists,
                Reason = exists ? "OK" : "Assets/_Tools/TA_Toolchain missing."
            };
        }
    }
}
