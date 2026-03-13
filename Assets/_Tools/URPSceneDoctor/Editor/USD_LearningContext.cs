using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace URPSceneDoctor
{
    [Serializable]
    public sealed class USD_LearningContext
    {
        public enum LearningStage
        {
            Stage0 = 0,
            Stage1 = 1,
            Stage2 = 2,
            Stage3 = 3
        }

        public const float Stage0Weight = 0f;
        public const float Stage1Weight = 0f;
        public const float Stage2Weight = 0.3f;
        public const float Stage3Weight = 1f;

        public LearningStage stage;
        public int totalSamples;
        public int annotationCount;
        public int pairwiseCount;
        public int tasteNoteCount;
        public DeltaStatsSummary deltaStats = new DeltaStatsSummary();
        public AnnotationSummary annotation = new AnnotationSummary();
        public PairwisePreferenceSummary pairwise = new PairwisePreferenceSummary();
        public TasteNoteSummary taste = new TasteNoteSummary();
        public List<TimelineItem> recentTimeline = new List<TimelineItem>();

        public float PreferenceWeight => StageToWeight(stage);
        public bool HasLearningData => stage != LearningStage.Stage0;

        public static USD_LearningContext Build(string projectRootPath, int maxHistory = 20)
        {
            var context = new USD_LearningContext();
            var normalizedRoot = Path.GetFullPath(projectRootPath ?? string.Empty);
            if (!Directory.Exists(normalizedRoot))
            {
                return context;
            }

            var annotationFiles = SafeGetFiles(normalizedRoot, "annotation.json");
            var pairwiseFiles = SafeGetFiles(normalizedRoot, "pairwise_pref.json");
            var tasteFiles = SafeGetFiles(normalizedRoot, "taste_note.json");
            var deltaFiles = SafeGetFiles(normalizedRoot, "delta_stats.json");

            context.annotation = BuildAnnotationSummary(annotationFiles, maxHistory, context.recentTimeline);
            context.pairwise = BuildPairwiseSummary(pairwiseFiles, context.recentTimeline);
            context.taste = BuildTasteSummary(tasteFiles, maxHistory, context.recentTimeline);
            context.deltaStats = BuildDeltaStatsSummary(deltaFiles);

            context.annotationCount = context.annotation.total;
            context.pairwiseCount = context.pairwise.total;
            context.tasteNoteCount = context.taste.total;
            context.totalSamples = context.annotationCount + context.pairwiseCount + context.tasteNoteCount;
            context.stage = ComputeStage(context.totalSamples);

            context.recentTimeline = context.recentTimeline
                .OrderByDescending(item => item.timestampTicks)
                .Take(5)
                .ToList();

            return context;
        }

        public string ToJson(bool prettyPrint = true)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }

        public static LearningStage ComputeStage(int sampleCount)
        {
            if (sampleCount <= 0) return LearningStage.Stage0;
            if (sampleCount < 5) return LearningStage.Stage1;
            if (sampleCount < 10) return LearningStage.Stage2;
            return LearningStage.Stage3;
        }

        public static float StageToWeight(LearningStage learningStage)
        {
            switch (learningStage)
            {
                case LearningStage.Stage0:
                    return Stage0Weight;
                case LearningStage.Stage1:
                    return Stage1Weight;
                case LearningStage.Stage2:
                    return Stage2Weight;
                default:
                    return Stage3Weight;
            }
        }

        private static DeltaStatsSummary BuildDeltaStatsSummary(IEnumerable<string> deltaFiles)
        {
            var summary = new DeltaStatsSummary();
            var aggregate = new Dictionary<string, RunningStat>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in deltaFiles)
            {
                var payload = ReadJson<DeltaStatsFile>(file);
                if (payload?.parameters == null)
                {
                    continue;
                }

                foreach (var param in payload.parameters)
                {
                    if (string.IsNullOrWhiteSpace(param.name))
                    {
                        continue;
                    }

                    if (!aggregate.TryGetValue(param.name, out var stat))
                    {
                        stat = new RunningStat();
                        aggregate[param.name] = stat;
                    }

                    stat.Add(param.mean);
                    summary.totalStatFiles++;
                }
            }

            summary.parameters = aggregate
                .Select(kvp => new ParameterRange
                {
                    name = kvp.Key,
                    mean = Round2(kvp.Value.Mean),
                    stdDev = Round2(kvp.Value.StandardDeviation),
                    min = Round2(kvp.Value.Min),
                    max = Round2(kvp.Value.Max)
                })
                .OrderByDescending(p => p.stdDev)
                .ToList();

            return summary;
        }

        private static AnnotationSummary BuildAnnotationSummary(IEnumerable<string> files, int maxHistory, List<TimelineItem> timeline)
        {
            var summary = new AnnotationSummary();
            var scoreAggregate = new Dictionary<string, RunningStat>(StringComparer.OrdinalIgnoreCase);
            var tagCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in files.OrderByDescending(File.GetLastWriteTimeUtc).Take(maxHistory))
            {
                var payload = ReadJson<AnnotationFile>(file);
                if (payload == null)
                {
                    continue;
                }

                summary.total++;
                foreach (var score in payload.scores ?? Array.Empty<ScoreEntry>())
                {
                    if (string.IsNullOrWhiteSpace(score.name))
                    {
                        continue;
                    }

                    if (!scoreAggregate.TryGetValue(score.name, out var stat))
                    {
                        stat = new RunningStat();
                        scoreAggregate[score.name] = stat;
                    }
                    stat.Add(score.value);
                }

                foreach (var tag in payload.tags ?? Array.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(tag))
                    {
                        continue;
                    }
                    tagCount[tag] = tagCount.TryGetValue(tag, out var count) ? count + 1 : 1;
                }

                timeline.Add(TimelineItem.From(file, "annotation", payload.scene));
            }

            summary.averageScores = scoreAggregate.Select(kvp => new NamedValue
            {
                name = kvp.Key,
                value = Round2(kvp.Value.Mean)
            }).ToList();

            summary.topTags = tagCount.OrderByDescending(kvp => kvp.Value)
                .Take(5)
                .Select(kvp => new NamedCount { name = kvp.Key, count = kvp.Value })
                .ToList();

            return summary;
        }

        private static PairwisePreferenceSummary BuildPairwiseSummary(IEnumerable<string> files, List<TimelineItem> timeline)
        {
            var summary = new PairwisePreferenceSummary();
            foreach (var file in files)
            {
                var payload = ReadJson<PairwisePrefFile>(file);
                if (payload == null)
                {
                    continue;
                }

                summary.total++;
                var winner = (payload.winner ?? string.Empty).Trim().ToLowerInvariant();
                if (winner.Contains("a") || winner.Contains("safe") || winner.Contains("conservative"))
                {
                    summary.stableCount++;
                }
                else if (winner.Contains("b") || winner.Contains("style") || winner.Contains("aggressive"))
                {
                    summary.styleCount++;
                }

                timeline.Add(TimelineItem.From(file, "pairwise", payload.scene));
            }

            if (summary.total > 0)
            {
                summary.stableRatio = Round2((float)summary.stableCount / summary.total);
                summary.styleRatio = Round2((float)summary.styleCount / summary.total);
            }

            return summary;
        }

        private static TasteNoteSummary BuildTasteSummary(IEnumerable<string> files, int maxHistory, List<TimelineItem> timeline)
        {
            var summary = new TasteNoteSummary();
            var issues = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in files.OrderByDescending(File.GetLastWriteTimeUtc).Take(maxHistory))
            {
                var payload = ReadJson<TasteNoteFile>(file);
                if (payload == null)
                {
                    continue;
                }

                summary.total++;
                foreach (var issue in payload.issueTags ?? Array.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(issue))
                    {
                        continue;
                    }

                    issues[issue] = issues.TryGetValue(issue, out var count) ? count + 1 : 1;
                }

                timeline.Add(TimelineItem.From(file, "taste_note", payload.scene));
            }

            summary.topIssues = issues.OrderByDescending(kvp => kvp.Value)
                .Take(5)
                .Select(kvp => new NamedCount { name = kvp.Key, count = kvp.Value })
                .ToList();

            return summary;
        }

        private static T ReadJson<T>(string path) where T : class
        {
            try
            {
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                return JsonUtility.FromJson<T>(json);
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<string> SafeGetFiles(string root, string fileName)
        {
            try
            {
                return Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static float Round2(double value)
        {
            return (float)Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        [Serializable]
        public sealed class DeltaStatsSummary
        {
            public int totalStatFiles;
            public List<ParameterRange> parameters = new List<ParameterRange>();
        }

        [Serializable]
        public sealed class AnnotationSummary
        {
            public int total;
            public List<NamedValue> averageScores = new List<NamedValue>();
            public List<NamedCount> topTags = new List<NamedCount>();
        }

        [Serializable]
        public sealed class PairwisePreferenceSummary
        {
            public int total;
            public int stableCount;
            public int styleCount;
            public float stableRatio;
            public float styleRatio;
        }

        [Serializable]
        public sealed class TasteNoteSummary
        {
            public int total;
            public List<NamedCount> topIssues = new List<NamedCount>();
        }

        [Serializable]
        public sealed class NamedCount
        {
            public string name;
            public int count;
        }

        [Serializable]
        public sealed class NamedValue
        {
            public string name;
            public float value;
        }

        [Serializable]
        public sealed class ParameterRange
        {
            public string name;
            public float mean;
            public float stdDev;
            public float min;
            public float max;
        }

        [Serializable]
        public sealed class TimelineItem
        {
            public string source;
            public string scene;
            public string path;
            public string timestamp;
            public long timestampTicks;

            public static TimelineItem From(string filePath, string sourceType, string sceneName)
            {
                var utc = File.GetLastWriteTimeUtc(filePath);
                return new TimelineItem
                {
                    source = sourceType,
                    scene = string.IsNullOrWhiteSpace(sceneName) ? "unknown" : sceneName,
                    path = filePath.Replace('\\', '/'),
                    timestamp = utc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    timestampTicks = utc.Ticks
                };
            }
        }

        [Serializable]
        private sealed class DeltaStatsFile
        {
            public List<DeltaParameterEntry> parameters;
        }

        [Serializable]
        private sealed class DeltaParameterEntry
        {
            public string name;
            public float mean;
        }

        [Serializable]
        private sealed class AnnotationFile
        {
            public string scene;
            public List<ScoreEntry> scores;
            public List<string> tags;
        }

        [Serializable]
        private sealed class ScoreEntry
        {
            public string name;
            public float value;
        }

        [Serializable]
        private sealed class PairwisePrefFile
        {
            public string scene;
            public string winner;
        }

        [Serializable]
        private sealed class TasteNoteFile
        {
            public string scene;
            public List<string> issueTags;
        }

        private sealed class RunningStat
        {
            private int _count;
            private double _mean;
            private double _m2;

            public double Min { get; private set; } = double.MaxValue;
            public double Max { get; private set; } = double.MinValue;
            public double Mean => _mean;
            public double StandardDeviation => _count > 1 ? Math.Sqrt(_m2 / _count) : 0d;

            public void Add(double value)
            {
                _count++;
                if (value < Min) Min = value;
                if (value > Max) Max = value;

                var delta = value - _mean;
                _mean += delta / _count;
                var delta2 = value - _mean;
                _m2 += delta * delta2;
            }
        }
    }
}
