using System;
using System.Collections.Generic;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    [Serializable]
    public sealed class USD_DeltaEntry
    {
        public string sceneName;
        public string timestamp;
        public string deltaPatchPath;
        public string snapshotBeforePath;
        public string snapshotAfterPath;
        public string notes;
        public List<string> tags = new List<string>();
    }

    public sealed class USD_DeltaLibraryAsset : ScriptableObject
    {
        public List<USD_DeltaEntry> entries = new List<USD_DeltaEntry>();
        public string lastUpdated;
    }
}
