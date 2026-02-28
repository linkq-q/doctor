using System;
using System.Collections.Generic;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    [Serializable]
    public sealed class USD_StyleGoal
    {
        public string id = "clean";
        public string name_zh = "干净";
        public string name_en = "Clean";
        [TextArea] public string description_zh;
        [TextArea] public string description_en;
        public string defaultStyleProfileId = "Clean Stylized";
    }

    [Serializable]
    public sealed class USD_IssueTag
    {
        public string id = "contrast_flat";
        public string name_zh = "对比平";
        public string name_en = "Flat contrast";
        [TextArea] public string description_zh;
        [TextArea] public string description_en;
    }

    public sealed class USD_LabelCatalogAsset : ScriptableObject
    {
        public int catalogVersion = 1;
        public string languageDefault = "zh";
        public List<USD_StyleGoal> styles = new List<USD_StyleGoal>();
        public List<USD_IssueTag> issues = new List<USD_IssueTag>();
    }
}
