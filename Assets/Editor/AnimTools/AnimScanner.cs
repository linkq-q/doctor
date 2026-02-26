using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AnimTools
{
    public class AnimScanner
    {
        private readonly AnimClassifier _classifier;

        public AnimScanner(AnimClassifier classifier)
        {
            _classifier = classifier;
        }

        public List<SpeciesAnimData> ScanProject()
        {
            var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets" });
            var allClips = new List<ClipInfo>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue;
                if (path.StartsWith("Packages/")) continue;

                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) continue;

                allClips.Add(new ClipInfo
                {
                    clip = clip,
                    clipName = clip.name,
                    assetPath = path,
                    fromFbx = path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase),
                    speciesKey = _classifier.ResolveSpeciesKey(clip.name),
                    category = _classifier.ResolveCategory(clip.name)
                });
            }

            return allClips
                .GroupBy(c => c.speciesKey)
                .OrderBy(g => g.Key)
                .Select(g => new SpeciesAnimData
                {
                    speciesKey = g.Key,
                    clips = g.OrderBy(c => c.clipName).ToList()
                })
                .ToList();
        }
    }
}
