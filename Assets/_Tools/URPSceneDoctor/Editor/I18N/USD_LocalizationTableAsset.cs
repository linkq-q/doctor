using System;
using System.Collections.Generic;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    [Serializable]
    public sealed class USD_LocEntry
    {
        public string key;
        [TextArea] public string zh;
        [TextArea] public string en;
        public string note;
    }

    public sealed class USD_LocalizationTableAsset : ScriptableObject
    {
        public List<USD_LocEntry> entries = new List<USD_LocEntry>();
    }
}
