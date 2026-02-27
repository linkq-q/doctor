using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TA.ToolSuite
{
    public sealed class TTS_SceneDoctorAdapter : ITTSModule
    {
        public string Name => "Scene Doctor";

        public void Draw()
        {
            var check = Check();
            EditorGUILayout.HelpBox(check.Available ? "URP Scene Doctor available." : check.Reason, check.Available ? MessageType.Info : MessageType.Warning);
            using (new EditorGUI.DisabledScope(!check.Available))
            {
                if (GUILayout.Button("Open Scene Doctor"))
                {
                    EditorApplication.ExecuteMenuItem("Tools/URP Scene Doctor");
                }
            }
        }

        public TTS_QuickCheckItem Check()
        {
            var exists = Directory.Exists("Assets/_Tools/URPSceneDoctor");
            return new TTS_QuickCheckItem
            {
                Name = Name,
                Available = exists,
                Reason = exists ? "OK" : "URP Scene Doctor not installed. Merge/install URPSceneDoctor first."
            };
        }
    }
}
