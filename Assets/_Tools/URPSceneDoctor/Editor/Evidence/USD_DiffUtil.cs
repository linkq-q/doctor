using System.Collections.Generic;

namespace URPSceneDoctor.Editor
{
    public static class USD_DiffUtil
    {
        public static USD_DiffReport BuildDiff(string sceneName, string timestamp, USD_ScanSnapshot before, USD_ScanSnapshot after, List<USD_WorkOrder> workOrders, USD_DeltaPatch patch)
        {
            var diff = new USD_DiffReport { sceneName = sceneName, timestamp = timestamp };
            Add(diff.pipelineChanges, "URP.shadowDistance", before.shadowDistance.ToString(), after.shadowDistance.ToString());
            Add(diff.pipelineChanges, "URP.shadowCascadeCount", before.shadowCascadeCount.ToString(), after.shadowCascadeCount.ToString());
            Add(diff.pipelineChanges, "URP.msaaSampleCount", before.msaaSampleCount.ToString(), after.msaaSampleCount.ToString());
            Add(diff.pipelineChanges, "URP.renderScale", before.renderScale.ToString(), after.renderScale.ToString());
            Add(diff.pipelineChanges, "URP.supportsHDR", before.supportsHDR.ToString(), after.supportsHDR.ToString());

            Add(diff.volumeChanges, "Volumes.hasGlobalVolume", before.hasGlobalVolume.ToString(), after.hasGlobalVolume.ToString());
            Add(diff.volumeChanges, "Volumes.globalVolumeProfileName", before.globalVolumeProfileName, after.globalVolumeProfileName);
            Add(diff.volumeChanges, "Volumes.enabledOverridesCount", before.enabledOverrides.Count.ToString(), after.enabledOverrides.Count.ToString());

            Add(diff.sceneChanges, "Scene.reflectionProbeCount", before.reflectionProbeCount.ToString(), after.reflectionProbeCount.ToString());
            Add(diff.sceneChanges, "Scene.transparentRendererCount", before.transparentRendererCount.ToString(), after.transparentRendererCount.ToString());

            foreach (var wo in workOrders) diff.workOrderIds.Add(wo.id);
            if (patch != null)
            {
                diff.tuningChangedFieldCount = patch.changedFields.Count;
                for (var i = 0; i < patch.changedFields.Count && i < 5; i++)
                {
                    diff.tuningTopChanges.Add($"{patch.changedFields[i].path}: {patch.changedFields[i].deltaHint}");
                }
            }

            return diff;
        }

        private static void Add(List<USD_FieldChange> list, string path, string before, string after)
        {
            if (before == after) return;
            list.Add(new USD_FieldChange { path = path, before = before, after = after });
        }
    }
}
