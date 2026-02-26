using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AnimTools
{
    [Serializable]
    public class SpeciesIndexEntry
    {
        public string species;
        public int clipCount;
        public List<CategoryCount> categoriesCount = new List<CategoryCount>();
        public string reportPath;
        public string generatedControllerPath;
    }

    [Serializable]
    public class CategoryCount
    {
        public string category;
        public int count;
    }

    [Serializable]
    public class GlobalIndex
    {
        public List<SpeciesIndexEntry> species = new List<SpeciesIndexEntry>();
    }

    public class AnimReportWriter
    {
        private readonly AnimClassifier _classifier;

        public AnimReportWriter(AnimClassifier classifier)
        {
            _classifier = classifier;
        }

        public GlobalIndex WriteReports(IEnumerable<SpeciesAnimData> speciesData, string reportDir, Dictionary<string, string> generatedControllerMap)
        {
            EnsureDirectory(reportDir);
            var index = new GlobalIndex();

            foreach (var species in speciesData)
            {
                var reportPath = Path.Combine(reportDir, SanitizeFileName(species.speciesKey) + ".md").Replace("\\", "/");
                var markdown = BuildSpeciesMarkdown(species, generatedControllerMap != null && generatedControllerMap.TryGetValue(species.speciesKey, out var path) ? path : string.Empty);
                File.WriteAllText(reportPath, markdown, Encoding.UTF8);

                var entry = new SpeciesIndexEntry
                {
                    species = species.speciesKey,
                    clipCount = species.clips.Count,
                    reportPath = reportPath,
                    generatedControllerPath = generatedControllerMap != null && generatedControllerMap.TryGetValue(species.speciesKey, out var p) ? p : string.Empty,
                    categoriesCount = Enum.GetValues(typeof(AnimCategory)).Cast<AnimCategory>()
                        .Select(cat => new CategoryCount { category = cat.ToString(), count = species.Count(cat) })
                        .ToList()
                };
                index.species.Add(entry);
            }

            var indexPath = Path.Combine(reportDir, "index.json").Replace("\\", "/");
            File.WriteAllText(indexPath, JsonUtility.ToJson(index, true), Encoding.UTF8);
            AssetDatabase.Refresh();
            return index;
        }

        private string BuildSpeciesMarkdown(SpeciesAnimData species, string generatedControllerPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Species: {species.speciesKey}");
            sb.AppendLine();
            sb.AppendLine($"- Total Clips: **{species.clips.Count}**");
            sb.AppendLine($"- FBX Clips: **{species.clips.Count(c => c.fromFbx)}**");
            sb.AppendLine($"- Standalone Clips: **{species.clips.Count(c => !c.fromFbx)}**");
            sb.AppendLine();

            sb.AppendLine("## Category Summary");
            foreach (AnimCategory category in Enum.GetValues(typeof(AnimCategory)))
            {
                sb.AppendLine($"- {category}: {species.Count(category)}");
            }

            sb.AppendLine();
            sb.AppendLine("## Clips by Category");
            foreach (AnimCategory category in Enum.GetValues(typeof(AnimCategory)))
            {
                sb.AppendLine();
                sb.AppendLine($"### {category}");
                var clips = species.clips.Where(c => c.category == category).ToList();
                if (clips.Count == 0)
                {
                    sb.AppendLine("- (none)");
                    continue;
                }

                foreach (var clip in clips)
                {
                    var loopTag = IsLoopCandidate(clip) ? " _(loop-candidate)_" : string.Empty;
                    sb.AppendLine($"- `{clip.clipName}` — `{clip.assetPath}`{loopTag}");
                }
            }

            var idle = _classifier.PickIdle(species);
            var walk = _classifier.PickWalk(species);
            var trot = _classifier.PickTrot(species);
            var run = _classifier.PickRun(species);
            var attack = _classifier.PickAttack(species);
            var hit = _classifier.PickHit(species);
            var death = _classifier.PickDeath(species);

            sb.AppendLine();
            sb.AppendLine("## Controller Generation Preview");
            sb.AppendLine($"- Idle: {ClipNameOrMissing(idle)}");
            sb.AppendLine($"- Walk: {ClipNameOrMissing(walk)}");
            sb.AppendLine($"- Trot: {ClipNameOrMissing(trot)}");
            sb.AppendLine($"- Run: {ClipNameOrMissing(run)}");
            sb.AppendLine($"- Attack: {ClipNameOrMissing(attack)}");
            sb.AppendLine($"- Hit: {ClipNameOrMissing(hit)}");
            sb.AppendLine($"- Death: {ClipNameOrMissing(death)}");
            sb.AppendLine();

            sb.AppendLine("### Parameters");
            sb.AppendLine("- Speed (float)");
            sb.AppendLine("- Attack (trigger)");
            sb.AppendLine("- Hit (trigger)");
            sb.AppendLine("- IsDead (bool)");
            sb.AppendLine();

            sb.AppendLine("### Transition Template");
            sb.AppendLine("- Entry -> Locomotion");
            sb.AppendLine("- AnyState -> Attack (Attack trigger && IsDead == false)");
            sb.AppendLine("- Attack -> Locomotion (ExitTime ~0.9)");
            sb.AppendLine("- AnyState -> Hit (Hit trigger && IsDead == false)");
            sb.AppendLine("- Hit -> Locomotion (ExitTime ~0.9)");
            sb.AppendLine("- AnyState -> Death (IsDead == true)");

            if (!string.IsNullOrWhiteSpace(generatedControllerPath))
            {
                sb.AppendLine();
                sb.AppendLine($"- Generated Controller: `{generatedControllerPath}`");
            }

            return sb.ToString();
        }

        private static bool IsLoopCandidate(ClipInfo clip)
        {
            var lower = clip.clipName.ToLowerInvariant();
            return lower.Contains("idle") || lower.Contains("walk") || lower.Contains("run") || lower.Contains("trot");
        }

        private static string ClipNameOrMissing(ClipInfo clip) => clip == null ? "(missing)" : clip.clipName;

        private static void EnsureDirectory(string dir)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var clean = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return string.IsNullOrWhiteSpace(clean) ? "UnknownSpecies" : clean;
        }
    }
}
