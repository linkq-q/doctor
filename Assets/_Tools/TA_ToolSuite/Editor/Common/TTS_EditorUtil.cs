using UnityEditor;
using UnityEngine;

namespace TA.ToolSuite
{
    public static class TTS_EditorUtil
    {
        public static void Record(UnityEngine.Object obj, string action)
        {
            Undo.RecordObject(obj, action);
            EditorUtility.SetDirty(obj);
        }

        public static void RegisterCreated(UnityEngine.Object obj, string action)
        {
            Undo.RegisterCreatedObjectUndo(obj, action);
            EditorUtility.SetDirty(obj);
        }
    }
}
