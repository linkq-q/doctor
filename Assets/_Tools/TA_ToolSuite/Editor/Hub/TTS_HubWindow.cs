using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TA.ToolSuite
{
    public sealed class TTS_HubWindow : EditorWindow
    {
        private const string Version = "v0.1-integration";
        private readonly string[] _tabs = { "Scene Doctor", "Render Audit", "Anim Tools", "Reports", "Settings" };
        private readonly List<ITTSModule> _modules = new List<ITTSModule>();
        private int _tab;
        private Vector2 _scroll;
        private List<TTS_QuickCheckItem> _quickCheckItems = new List<TTS_QuickCheckItem>();

        [MenuItem("Tools/TA Tool Suite")]
        public static void Open() => GetWindow<TTS_HubWindow>("TA Tool Suite");

        private void OnEnable()
        {
            _modules.Clear();
            _modules.Add(new TTS_SceneDoctorAdapter());
            _modules.Add(new TTS_RenderAuditAdapter());
            _modules.Add(new TTS_AnimToolsAdapter());
            _quickCheckItems = TTS_QuickCheck.Run(_modules);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox($"TA Tool Suite {Version}", MessageType.None);
            if (GUILayout.Button("Quick Check", GUILayout.Width(120)))
            {
                _quickCheckItems = TTS_QuickCheck.Run(_modules);
            }

            foreach (var item in _quickCheckItems)
            {
                EditorGUILayout.LabelField($"- {item.Name}: {(item.Available ? "Available" : "Missing")} ({item.Reason})");
            }

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            _tab = GUILayout.SelectionGrid(_tab, _tabs, 1, GUILayout.Width(220));
            EditorGUILayout.BeginVertical();
            DrawTab();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTab()
        {
            if (_tab == 0) _modules[0].Draw();
            else if (_tab == 1) _modules[1].Draw();
            else if (_tab == 2) _modules[2].Draw();
            else if (_tab == 3) DrawReportsTab();
            else DrawSettingsTab();
        }

        private void DrawReportsTab()
        {
            var root = "Assets/_Tools/TA_ToolSuite/Reports";
            TTS_ReportRouter.EnsureFolder(root);
            if (GUILayout.Button("Open Reports Root")) EditorUtility.RevealInFinder(root);

            var absRoot = TTS_ReportRouter.ToAbsolutePath(root);
            if (!Directory.Exists(absRoot)) return;

            var files = Directory.GetFiles(absRoot, "*.json", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(absRoot, "*.md", SearchOption.AllDirectories))
                .OrderByDescending(File.GetLastWriteTime)
                .Take(30)
                .ToList();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var file in files)
            {
                var rel = file.Replace('\\', '/');
                if (GUILayout.Button(rel, EditorStyles.miniButtonLeft))
                {
                    EditorUtility.RevealInFinder(file);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawSettingsTab()
        {
            EditorGUILayout.LabelField("Unified output roots");
            EditorGUILayout.SelectableLabel("Assets/_Tools/TA_ToolSuite/Reports", GUILayout.Height(18));
            EditorGUILayout.SelectableLabel("Assets/_Tools/TA_ToolSuite/Snapshots", GUILayout.Height(18));
            EditorGUILayout.SelectableLabel("Assets/_Tools/TA_ToolSuite/Patches", GUILayout.Height(18));
        }
    }
}
