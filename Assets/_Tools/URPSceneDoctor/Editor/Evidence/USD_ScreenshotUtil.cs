using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public sealed class USD_ShotCapture
    {
        public string path;
        public Vector3 position;
        public float yaw;
        public float pitch;
        public float fov;
    }

    public static class USD_ScreenshotUtil
    {
        public static List<USD_ShotCapture> CaptureSixShots(string outputFolder, int width, int height, Camera sourceCamera = null)
        {
            USD_EditorUtil.EnsureFolder(outputFolder);
            var camera = sourceCamera != null ? sourceCamera : SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : null;
            if (camera == null) return new List<USD_ShotCapture>();

            var captures = new List<USD_ShotCapture>();
            var variants = new[]
            {
                new Vector3(0f,0f,0f),
                new Vector3(0f,15f,0f),
                new Vector3(0f,-15f,0f),
                new Vector3(10f,0f,0f),
                new Vector3(-10f,0f,0f),
                new Vector3(0f,0f,0.1f)
            };

            var tempGo = new GameObject("USD_TempShotCamera");
            tempGo.hideFlags = HideFlags.HideAndDontSave;
            var tempCam = tempGo.AddComponent<Camera>();
            tempCam.CopyFrom(camera);

            for (var i = 0; i < variants.Length; i++)
            {
                tempGo.transform.position = camera.transform.position;
                tempGo.transform.rotation = camera.transform.rotation;
                tempGo.transform.Rotate(variants[i].x, variants[i].y, 0f, Space.Self);
                if (Mathf.Abs(variants[i].z) > 0.001f)
                {
                    tempGo.transform.position += tempGo.transform.forward * variants[i].z * 10f;
                }

                var path = $"{outputFolder}/shot_{i + 1:00}.png";
                CaptureCamera(tempCam, width, height, path);
                var euler = tempGo.transform.eulerAngles;
                captures.Add(new USD_ShotCapture
                {
                    path = path,
                    position = tempGo.transform.position,
                    yaw = euler.y,
                    pitch = euler.x,
                    fov = tempCam.fieldOfView
                });
            }

            Object.DestroyImmediate(tempGo);
            AssetDatabase.Refresh();
            return captures;
        }

        private static void CaptureCamera(Camera cam, int width, int height, string outputPath)
        {
            var rt = new RenderTexture(width, height, 24);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            File.WriteAllBytes(outputPath, tex.EncodeToPNG());
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
