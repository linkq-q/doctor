using System;
using System.Collections.Generic;
using UnityEngine;

namespace URPSceneDoctor.Editor
{
    [Serializable]
    public sealed class USD_Trigger
    {
        public string field;
        public string op;
        public string value;
    }

    [Serializable]
    public sealed class USD_PrescriptionTemplate
    {
        public int priority;
        public string actionText;
        public string targetPathHint;
        public string recommendedRange;
        public List<string> alternatives = new List<string>();
    }

    [Serializable]
    public sealed class USD_ApplyActionTemplate
    {
        public string type;
        public string payloadJson;
        public bool requiresUserOptIn;
    }

    [Serializable]
    public sealed class USD_Rule
    {
        public string id;
        public string title;
        public string severity;
        public string category;
        public List<USD_Trigger> triggers = new List<USD_Trigger>();
        public List<USD_Trigger> excludes = new List<USD_Trigger>();
        public List<string> symptoms = new List<string>();
        public string diagnosisTemplate;
        public List<USD_PrescriptionTemplate> prescriptions = new List<USD_PrescriptionTemplate>();
        public List<string> verificationTemplate = new List<string>();
        public List<USD_ApplyActionTemplate> applyActionsTemplate = new List<USD_ApplyActionTemplate>();
        public string notes;
    }

    [Serializable]
    public sealed class USD_RulePack
    {
        public string name;
        public string version;
        public string description;
        public List<USD_Rule> rules = new List<USD_Rule>();
    }

    public sealed class USD_RulePackAsset : ScriptableObject
    {
        [TextArea(5, 50)]
        public string json;
    }
}
