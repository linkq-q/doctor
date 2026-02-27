using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace URPSceneDoctor.Editor
{
    public static class USD_DemoSceneUtil
    {
        public const string DemoScenePath = "Assets/_Tools/URPSceneDoctor/Samples/Scenes/USD_DemoScene.unity";

        public static void OpenOrCreateDemoSceneWithPrompt()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            OpenOrCreateDemoScene();
        }

        public static void OpenOrCreateDemoScene()
        {
            USD_EditorUtil.EnsureFolder("Assets/_Tools/URPSceneDoctor/Samples");
            USD_EditorUtil.EnsureFolder("Assets/_Tools/URPSceneDoctor/Samples/Scenes");

            Scene scene;
            if (System.IO.File.Exists(DemoScenePath))
            {
                scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            EnsureDemoContent();
            EditorSceneManager.SaveScene(scene, DemoScenePath);

            SceneView.lastActiveSceneView?.FrameSelected();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath));
        }

        private static void EnsureDemoContent()
        {
            RenderSettings.skybox = null;

            EnsureCamera();
            EnsureDirectionalLight();
            EnsureGround();
            EnsureCubes();
        }

        private static void EnsureCamera()
        {
            var existing = GameObject.Find("Main Camera");
            if (existing != null) return;

            var cameraGo = new GameObject("Main Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 4f, -10f);
            cameraGo.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
        }

        private static void EnsureDirectionalLight()
        {
            var existing = GameObject.Find("Directional Light");
            if (existing == null)
            {
                existing = new GameObject("Directional Light");
                existing.AddComponent<Light>();
            }

            var light = existing.GetComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;
            existing.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        }

        private static void EnsureGround()
        {
            if (GameObject.Find("Ground") != null) return;

            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Ground";
            plane.transform.position = Vector3.zero;
        }

        private static void EnsureCubes()
        {
            for (var i = 0; i < 5; i++)
            {
                var name = "DemoCube_" + i;
                if (GameObject.Find(name) != null) continue;

                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = name;
                cube.transform.position = new Vector3(-4f + i * 2f, 0.5f, i % 2 == 0 ? 2f : 4f);
                cube.transform.localScale = new Vector3(1f, 1f + i * 0.2f, 1f);
            }
        }
    }
}
