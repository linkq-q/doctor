using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TA.ToolSuite
{
    public sealed class TTS_AnimToolsAdapter : ITTSModule
    {
        public string Name => "Anim Tools";

        public void Draw()
        {
            var check = Check();
            EditorGUILayout.HelpBox(check.Available ? "Anim Tools available." : check.Reason, check.Available ? MessageType.Info : MessageType.Warning);
            using (new EditorGUI.DisabledScope(!check.Available))
            {
                if (GUILayout.Button("Open Anim Audit & Generator"))
                {
                    EditorApplication.ExecuteMenuItem("Tools/Anim Tools/Anim Audit & Generator");
                }

                var scene = EditorSceneManager.GetActiveScene().name;
                EditorGUILayout.LabelField("Recommended Report Output", TTS_ReportRouter.RouteAnimReportsOutputDir(scene));
                EditorGUILayout.LabelField("Recommended Controller Output", TTS_ReportRouter.RouteAnimControllersOutputDir(scene));
                EditorGUILayout.HelpBox("Anim Tools supports Undo-safe controller generation (vetted in ControllerGenerator).", MessageType.None);
            }
        }

        public TTS_QuickCheckItem Check()
        {
            var exists = Directory.Exists("Assets/Editor/AnimTools");
            return new TTS_QuickCheckItem
            {
                Name = Name,
                Available = exists,
                Reason = exists ? "OK" : "Assets/Editor/AnimTools missing."
            };
        }
    }
}
