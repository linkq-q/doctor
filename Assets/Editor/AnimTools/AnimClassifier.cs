using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AnimTools
{
    public enum AnimCategory
    {
        Locomotion,
        Action,
        Reaction,
        Death,
        Swim,
        Fly,
        Other
    }

    [Serializable]
    public class CategoryKeywordRule
    {
        public AnimCategory category;
        public List<string> keywords = new List<string>();
    }

    [CreateAssetMenu(fileName = "AnimNamingRules", menuName = "Anim Tools/Anim Naming Rules")]
    public class AnimNamingRules : ScriptableObject
    {
        [Header("SpeciesKey")]
        [Tooltip("Optional regex used before fallback splitting. Must include one capture group.")]
        public string speciesRegex = @"^(Arm_[A-Za-z0-9]+)";

        [Header("Category Keywords")]
        public List<CategoryKeywordRule> categoryRules = new List<CategoryKeywordRule>();

        public static AnimNamingRules CreateDefaultInstance()
        {
            var rules = CreateInstance<AnimNamingRules>();
            rules.speciesRegex = @"^(Arm_[A-Za-z0-9]+)";
            rules.categoryRules = new List<CategoryKeywordRule>
            {
                NewRule(AnimCategory.Locomotion, "idle", "walk", "run", "trot", "rotate", "turn", "forward", "back"),
                NewRule(AnimCategory.Action, "attack", "bite", "claw", "jump", "drink", "eat", "hide", "landing"),
                NewRule(AnimCategory.Reaction, "hit", "hurt", "stun"),
                NewRule(AnimCategory.Death, "death", "die"),
                NewRule(AnimCategory.Swim, "swim"),
                NewRule(AnimCategory.Fly, "fly")
            };
            return rules;
        }

        private static CategoryKeywordRule NewRule(AnimCategory category, params string[] keywords)
        {
            return new CategoryKeywordRule
            {
                category = category,
                keywords = keywords.ToList()
            };
        }
    }

    [Serializable]
    public class ClipInfo
    {
        public AnimationClip clip;
        public string clipName;
        public string assetPath;
        public bool fromFbx;
        public string speciesKey;
        public AnimCategory category;
    }

    [Serializable]
    public class SpeciesAnimData
    {
        public string speciesKey;
        public List<ClipInfo> clips = new List<ClipInfo>();

        public Dictionary<AnimCategory, List<ClipInfo>> ByCategory()
        {
            return clips.GroupBy(c => c.category).ToDictionary(g => g.Key, g => g.ToList());
        }

        public int Count(AnimCategory category)
        {
            return clips.Count(c => c.category == category);
        }
    }

    public class AnimClassifier
    {
        private readonly AnimNamingRules _rules;

        public AnimClassifier(AnimNamingRules rules)
        {
            _rules = rules;
        }

        public string ResolveSpeciesKey(string clipName)
        {
            if (string.IsNullOrWhiteSpace(clipName)) return "Unknown";

            var pipeIndex = clipName.IndexOf('|');
            if (pipeIndex > 0) return clipName.Substring(0, pipeIndex).Trim();

            if (!string.IsNullOrWhiteSpace(_rules.speciesRegex))
            {
                var match = Regex.Match(clipName, _rules.speciesRegex);
                if (match.Success)
                {
                    if (match.Groups.Count > 1) return match.Groups[1].Value;
                    return match.Value;
                }
            }

            var split = clipName.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length == 0) return "Unknown";
            return split[0].Trim();
        }

        public AnimCategory ResolveCategory(string clipName)
        {
            var lower = (clipName ?? string.Empty).ToLowerInvariant();
            foreach (var rule in _rules.categoryRules)
            {
                if (rule.keywords.Any(keyword => lower.Contains(keyword.ToLowerInvariant())))
                {
                    return rule.category;
                }
            }

            return AnimCategory.Other;
        }

        public ClipInfo PickIdle(SpeciesAnimData species) =>
            PickBest(species, new[] { "idle" }, excludedTokens: new[] { "swim", "fly" });

        public ClipInfo PickWalk(SpeciesAnimData species) =>
            PickBest(species, new[] { "walk", "forward" }, anyOf: new[] { "walk" });

        public ClipInfo PickTrot(SpeciesAnimData species) =>
            PickBest(species, new[] { "trot" });

        public ClipInfo PickRun(SpeciesAnimData species) =>
            PickBest(species, new[] { "run", "forward" }, anyOf: new[] { "run" });

        public ClipInfo PickAttack(SpeciesAnimData species) =>
            PickBest(species, new[] { "attack_1" }, anyOf: new[] { "attack", "bite", "claw" })
            ?? PickBest(species, new[] { "attack" }, anyOf: new[] { "attack", "bite", "claw" });

        public ClipInfo PickHit(SpeciesAnimData species) =>
            PickBest(species, new[] { "hit_simple" }, anyOf: new[] { "hit", "hurt", "stun" })
            ?? PickBest(species, new[] { "hit" }, anyOf: new[] { "hit", "hurt", "stun" });

        public ClipInfo PickDeath(SpeciesAnimData species) =>
            PickBest(species, new[] { "death_1" }, anyOf: new[] { "death", "die" })
            ?? PickBest(species, new[] { "death" }, anyOf: new[] { "death", "die" });

        private ClipInfo PickBest(SpeciesAnimData species, string[] preferredTokens, string[] anyOf = null, string[] excludedTokens = null)
        {
            var pool = species.clips.AsEnumerable();
            if (anyOf != null && anyOf.Length > 0)
            {
                pool = pool.Where(c => anyOf.Any(token => c.clipName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            if (excludedTokens != null && excludedTokens.Length > 0)
            {
                pool = pool.Where(c => excludedTokens.All(token => c.clipName.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0));
            }

            var filtered = pool.ToList();
            if (filtered.Count == 0) return null;

            ClipInfo best = null;
            var bestScore = int.MinValue;

            foreach (var clip in filtered)
            {
                var score = 0;
                var lower = clip.clipName.ToLowerInvariant();
                foreach (var token in preferredTokens)
                {
                    if (lower.Contains(token.ToLowerInvariant())) score += 10;
                }

                if (clip.fromFbx) score += 1;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = clip;
                }
            }

            return best;
        }
    }
}
