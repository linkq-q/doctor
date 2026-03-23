using System.Collections.Generic;

namespace URPSceneDoctor.Editor
{
    public enum USD_RunMode
    {
        Scan,
        DryRun,
        Apply
    }

    public enum USD_ApplyMode
    {
        SafeNeutral,
        VisibleDemo
    }

    public sealed class USD_RunContext
    {
        public USD_RunMode Mode;
        public USD_ScanSnapshot Snapshot;
        public string SceneName;
        public string Timestamp;
        public string SelectedRulePackPath;
        public string OptionalDeltaPatchPath;
        public bool AllowModifyExistingAssets;
        public USD_TastePolicyAsset TastePolicy;
        public USD_DeltaStats LearningStats;
        public USD_ApplyMode ApplyMode;
        public USD_StyleProfileAsset StyleProfile;
    }

    public sealed class USD_ModuleResult
    {
        public string ModuleName;
        public USD_ScanSnapshot Snapshot;
        public readonly List<USD_WorkOrder> WorkOrders = new List<USD_WorkOrder>();
        public readonly List<string> AppliedChanges = new List<string>();
        public readonly List<string> Warnings = new List<string>();
    }

    public interface IToolModule
    {
        string ModuleName { get; }
        void DrawUI(USD_HubWindow hub);
        USD_ModuleResult Execute(USD_RunContext context);
    }
}
