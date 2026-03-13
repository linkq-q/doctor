using System.Linq;
using System.Text;

namespace URPSceneDoctor
{
    public static class USD_LearningPromptFormatter
    {
        public static string BuildLearningContextBlock(USD_LearningContext context)
        {
            if (context == null || !context.HasLearningData)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.AppendLine("learning_context:");
            builder.AppendLine($"  stage: {(int)context.stage}");
            builder.AppendLine($"  weight: {context.PreferenceWeight:0.00}");
            builder.AppendLine($"  samples: {context.totalSamples}");

            if (context.deltaStats?.parameters?.Count > 0)
            {
                builder.AppendLine("  parameter_ranges:");
                foreach (var item in context.deltaStats.parameters.Take(10))
                {
                    builder.AppendLine($"    - {item.name}: mean={item.mean:0.00}, std={item.stdDev:0.00}, range=[{item.min:0.00},{item.max:0.00}]");
                }
            }

            if (context.pairwise != null && context.pairwise.total > 0)
            {
                builder.AppendLine($"  style_preference: stable={context.pairwise.stableRatio:0.00}, stylized={context.pairwise.styleRatio:0.00}");
            }

            var tags = context.annotation?.topTags?.Select(t => t.name).ToArray();
            if (tags != null && tags.Length > 0)
            {
                builder.AppendLine($"  frequent_annotation_tags: {string.Join(", ", tags)}");
            }

            var issues = context.taste?.topIssues?.Select(t => t.name).ToArray();
            if (issues != null && issues.Length > 0)
            {
                builder.AppendLine($"  frequent_taste_issues: {string.Join(", ", issues)}");
            }

            if (context.stage == USD_LearningContext.LearningStage.Stage1 || context.stage == USD_LearningContext.LearningStage.Stage2)
            {
                builder.AppendLine("  caution: sample size is limited, treat the preference as a weak prior");
            }

            return builder.ToString();
        }
    }
}
