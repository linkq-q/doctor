using UnityEditor;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    public sealed class USD_CameraPickResult
    {
        public Camera camera;
        public string reason;
    }

    public static class USD_CameraPicker
    {
        public static USD_CameraPickResult PickAutoCamera()
        {
            var result = new USD_CameraPickResult();
            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);

            foreach (var c in cams)
            {
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
                if (!"MainCamera".Equals(c.tag)) continue;
                if (IsLikelyUICamera(c)) continue;
                result.camera = c;
                result.reason = "Picked MainCamera tag camera.";
                return result;
            }

            foreach (var c in cams)
            {
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
                if (IsLikelyUICamera(c)) continue;
                if (c.fieldOfView < 20f || c.fieldOfView > 120f) continue;
                result.camera = c;
                result.reason = "Picked first enabled non-UI camera.";
                return result;
            }

            result.camera = null;
            result.reason = "No valid scene camera found; fallback to SceneView.";
            return result;
        }

        private static bool IsLikelyUICamera(Camera c)
        {
            if (c.clearFlags == CameraClearFlags.Depth || c.clearFlags == CameraClearFlags.Nothing) return true;
            return c.cullingMask == (1 << 5); // UI layer only
        }
    }
}
