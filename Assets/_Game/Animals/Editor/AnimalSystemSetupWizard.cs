using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Animals.Editor
{
    public class AnimalSystemSetupWizard : EditorWindow
    {
        private string prefabFolder = "Assets/AnimalsPack/Prefabs";
        private string soFolder = "Assets/_Game/Animals/SO";

        [MenuItem("Tools/Animals/Setup Wizard")]
        public static void Open()
        {
            GetWindow<AnimalSystemSetupWizard>("Animal Setup Wizard");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Animal System Setup", EditorStyles.boldLabel);
            prefabFolder = EditorGUILayout.TextField("Prefab Folder", prefabFolder);
            soFolder = EditorGUILayout.TextField("SO Output Folder", soFolder);

            if (GUILayout.Button("Create Manager + Pool"))
            {
                CreateManagerAndPool();
            }

            if (GUILayout.Button("Generate Species Config Assets"))
            {
                GenerateSpeciesConfigs();
            }

            EditorGUILayout.HelpBox("建议 Layer/Tag: Rabbit, Fox, Butterfly, Crawler。若项目无 AI Navigation，请在 Package Manager 安装 AI Navigation。", MessageType.Info);
        }

        private static void CreateManagerAndPool()
        {
            var poolGo = GameObject.Find("AnimalPool") ?? new GameObject("AnimalPool");
            if (!poolGo.TryGetComponent(out AnimalPool pool))
            {
                pool = poolGo.AddComponent<AnimalPool>();
            }

            var mgrGo = GameObject.Find("AnimalSpawnManager") ?? new GameObject("AnimalSpawnManager");
            if (!mgrGo.TryGetComponent(out AnimalSpawnManager mgr))
            {
                mgr = mgrGo.AddComponent<AnimalSpawnManager>();
            }

            var so = new SerializedObject(mgr);
            so.FindProperty("pool").objectReferenceValue = pool;
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = mgrGo;
            EditorUtility.SetDirty(mgrGo);
        }

        private void GenerateSpeciesConfigs()
        {
            if (!AssetDatabase.IsValidFolder(soFolder))
            {
                Directory.CreateDirectory(soFolder);
                AssetDatabase.Refresh();
            }

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) continue;

                var id = prefab.name;
                var assetPath = $"{soFolder}/ASC_{id}.asset";
                if (AssetDatabase.LoadAssetAtPath<AnimalSpeciesConfig>(assetPath) != null) continue;

                var so = ScriptableObject.CreateInstance<AnimalSpeciesConfig>();
                so.speciesId = id;
                so.prefab = prefab;
                ApplyDefaults(so, id);
                AssetDatabase.CreateAsset(so, assetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ApplyDefaults(AnimalSpeciesConfig so, string id)
        {
            so.spawnMinDist = 12f;
            so.spawnMaxDist = 45f;
            so.stopAnimatorDist = 45f;
            so.sleepDist = 55f;
            so.despawnDist = 70f;
            so.useNavMesh = true;
            so.requiresLineOfSight = true;

            var key = id.ToLowerInvariant();
            if (key.Contains("butter"))
            {
                so.maxAlive = 20;
                so.useNavMesh = false;
                so.walkSpeed = 1.5f;
                so.runSpeed = 3.5f;
            }
            else if (key.Contains("crawler") || key.Contains("bug") || key.Contains("insect"))
            {
                so.maxAlive = 12;
                so.useNavMesh = false;
                so.walkSpeed = 1f;
                so.runSpeed = 2.5f;
            }
            else if (key.Contains("rabbit"))
            {
                so.maxAlive = 6;
                so.walkSpeed = 2f;
                so.runSpeed = 5f;
            }
            else if (key.Contains("fox"))
            {
                so.maxAlive = 3;
                so.walkSpeed = 2f;
                so.runSpeed = 5.5f;
                so.chaseDist = 22f;
                so.chaseGiveUpDist = 35f;
            }
            else
            {
                so.maxAlive = 6;
            }
        }
    }
}
