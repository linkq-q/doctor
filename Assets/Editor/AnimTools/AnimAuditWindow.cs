using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TA.ToolSuite;

namespace AnimTools
{
    public class AnimAuditWindow : EditorWindow
    {
        private const string DefaultRulesPath = "Assets/Editor/AnimTools/AnimNamingRules.asset";

        private AnimNamingRules _rules;
        private AnimClassifier _classifier;
        private AnimScanner _scanner;
        private AnimReportWriter _reportWriter;
        private ControllerGenerator _generator;

        private List<SpeciesAnimData> _species = new List<SpeciesAnimData>();
        private Dictionary<string, bool> _selected = new Dictionary<string, bool>();
        private Vector2 _scroll;

        private string _reportDir;
        private string _controllerDir;
        private bool _generateReports = true;
        private bool _generateControllers = true;
        private GeneratorMode _mode = GeneratorMode.Simple;

        [MenuItem("Tools/Anim Tools/Anim Audit & Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<AnimAuditWindow>("Anim Audit");
            window.minSize = new Vector2(820, 500);
            window.Show();
        }

        private void OnEnable()
        {
            var sceneName = EditorSceneManager.GetActiveScene().name;
            _reportDir = TTS_ReportRouter.RouteAnimReportsOutputDir(sceneName);
            _controllerDir = TTS_ReportRouter.RouteAnimControllersOutputDir(sceneName);
            LoadOrCreateRules();
            BuildServices();
        }

        private void LoadOrCreateRules()
        {
            _rules = AssetDatabase.LoadAssetAtPath<AnimNamingRules>(DefaultRulesPath);
            if (_rules != null) return;

            Directory.CreateDirectory(Path.GetDirectoryName(DefaultRulesPath) ?? "Assets");
            _rules = AnimNamingRules.CreateDefaultInstance();
            AssetDatabase.CreateAsset(_rules, DefaultRulesPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void BuildServices()
        {
            _classifier = new AnimClassifier(_rules);
            _scanner = new AnimScanner(_classifier);
            _reportWriter = new AnimReportWriter(_classifier);
            _generator = new ControllerGenerator(_classifier);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("AnimPack 审计与生成工具", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan Project", GUILayout.Height(30)))
            {
                ScanProject();
            }

            if (GUILayout.Button("Select All", GUILayout.Height(30)))
            {
                foreach (var key in _species.Select(s => s.speciesKey).ToList()) _selected[key] = true;
            }

            if (GUILayout.Button("Select None", GUILayout.Height(30)))
            {
                foreach (var key in _species.Select(s => s.speciesKey).ToList()) _selected[key] = false;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            _rules = (AnimNamingRules)EditorGUILayout.ObjectField("Naming Rules", _rules, typeof(AnimNamingRules), false);
            if (GUILayout.Button("Reload Services"))
            {
                BuildServices();
            }

            _reportDir = EditorGUILayout.TextField("Report Output Dir", _reportDir);
            _controllerDir = EditorGUILayout.TextField("Controller Output Dir", _controllerDir);
            _generateReports = EditorGUILayout.Toggle("Generate Reports", _generateReports);
            _generateControllers = EditorGUILayout.Toggle("Generate Controllers", _generateControllers);
            _mode = (GeneratorMode)EditorGUILayout.EnumPopup("Generation Mode", _mode);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Species Count: {_species.Count}", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var s in _species)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                _selected[s.speciesKey] = EditorGUILayout.Toggle(_selected.TryGetValue(s.speciesKey, out var value) && value, GUILayout.Width(18));
                EditorGUILayout.LabelField($"{s.speciesKey}  (clips: {s.clips.Count})", EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField(
                    $"Locomotion {s.Count(AnimCategory.Locomotion)} | Action {s.Count(AnimCategory.Action)} | Reaction {s.Count(AnimCategory.Reaction)} | Death {s.Count(AnimCategory.Death)} | Swim {s.Count(AnimCategory.Swim)} | Fly {s.Count(AnimCategory.Fly)} | Other {s.Count(AnimCategory.Other)}",
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate Reports", GUILayout.Height(32)))
            {
                GenerateReports();
            }

            if (GUILayout.Button("Generate Controllers", GUILayout.Height(32)))
            {
                GenerateControllers();
            }

            if (GUILayout.Button("Generate All (Reports + Controllers)", GUILayout.Height(32)))
            {
                GenerateAll();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ScanProject()
        {
            if (_rules == null) LoadOrCreateRules();
            BuildServices();

            _species = _scanner.ScanProject();
            _selected = _species.ToDictionary(s => s.speciesKey, _ => true);
            Debug.Log($"[AnimTools] Scanned {_species.Count} species.");
        }

        private List<SpeciesAnimData> SelectedSpecies()
        {
            return _species.Where(s => _selected.TryGetValue(s.speciesKey, out var selected) && selected).ToList();
        }

        private Dictionary<string, string> GenerateControllersInternal()
        {
            var selected = SelectedSpecies();
            var output = new Dictionary<string, string>();
            foreach (var species in selected)
            {
                var path = _generator.Generate(species, _controllerDir, _mode);
                output[species.speciesKey] = path;
            }
            AssetDatabase.Refresh();
            return output;
        }

        private void GenerateReports()
        {
            var selected = SelectedSpecies();
            _reportWriter.WriteReports(selected, _reportDir, null);
            Debug.Log($"[AnimTools] Reports generated: {selected.Count}");
        }

        private void GenerateControllers()
        {
            var generated = GenerateControllersInternal();
            Debug.Log($"[AnimTools] Controllers generated: {generated.Count}");
        }

        private void GenerateAll()
        {
            var generated = _generateControllers ? GenerateControllersInternal() : null;
            if (_generateReports)
            {
                var selected = SelectedSpecies();
                _reportWriter.WriteReports(selected, _reportDir, generated);
            }

            Debug.Log("[AnimTools] Generate All completed.");
        }
    }
}
