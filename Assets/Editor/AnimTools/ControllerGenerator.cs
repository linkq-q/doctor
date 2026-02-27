using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using TA.ToolSuite;

namespace AnimTools
{
    public enum GeneratorMode
    {
        Simple,
        WithSwimFly
    }

    public class ControllerGenerator
    {
        private readonly AnimClassifier _classifier;

        public ControllerGenerator(AnimClassifier classifier)
        {
            _classifier = classifier;
        }

        public string Generate(SpeciesAnimData species, string outputDir, GeneratorMode mode)
        {
            if (species == null || species.clips.Count == 0) return string.Empty;
            EnsureDirectory(outputDir);

            var controllerPath = Path.Combine(outputDir, $"{Sanitize(species.speciesKey)}.controller").Replace("\\", "/");
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            TTS_EditorUtil.RegisterCreated(controller, "Generate Anim Controller");
            var layer = controller.layers[0];
            var stateMachine = layer.stateMachine;

            AddBaseParameters(controller, mode);

            AnimatorState locomotionState;
            if (mode == GeneratorMode.WithSwimFly)
            {
                locomotionState = BuildModeDrivenLocomotion(controller, stateMachine, species);
            }
            else
            {
                locomotionState = stateMachine.AddState("Locomotion");
                locomotionState.motion = BuildGroundBlendTree(controller, species);
            }

            stateMachine.defaultState = locomotionState;

            var attackClip = _classifier.PickAttack(species)?.clip;
            var hitClip = _classifier.PickHit(species)?.clip;
            var deathClip = _classifier.PickDeath(species)?.clip;

            var attackState = attackClip != null ? stateMachine.AddState("Attack") : null;
            if (attackState != null) attackState.motion = attackClip;

            var hitState = hitClip != null ? stateMachine.AddState("Hit") : null;
            if (hitState != null) hitState.motion = hitClip;

            var deathState = deathClip != null ? stateMachine.AddState("Death") : null;
            if (deathState != null) deathState.motion = deathClip;

            if (attackState != null)
            {
                var t = stateMachine.AddAnyStateTransition(attackState);
                t.hasExitTime = false;
                t.duration = 0f;
                t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
                t.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

                var back = attackState.AddTransition(locomotionState);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0f;
            }

            if (hitState != null)
            {
                var t = stateMachine.AddAnyStateTransition(hitState);
                t.hasExitTime = false;
                t.duration = 0f;
                t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
                t.AddCondition(AnimatorConditionMode.If, 0f, "Hit");

                var back = hitState.AddTransition(locomotionState);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0f;
            }

            if (deathState != null)
            {
                var t = stateMachine.AddAnyStateTransition(deathState);
                t.hasExitTime = false;
                t.duration = 0f;
                t.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return controllerPath;
        }

        private AnimatorState BuildModeDrivenLocomotion(AnimatorController controller, AnimatorStateMachine sm, SpeciesAnimData species)
        {
            var state = sm.AddState("Locomotion");
            var rootTree = new BlendTree
            {
                name = "ModeRoot",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Mode",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(rootTree, controller);

            var ground = BuildGroundBlendTree(controller, species);
            rootTree.AddChild(ground, 0f);

            var swim = BuildCategoryBlendTree(controller, species, AnimCategory.Swim, "SwimLocomotion");
            if (swim != null) rootTree.AddChild(swim, 1f);

            var fly = BuildCategoryBlendTree(controller, species, AnimCategory.Fly, "FlyLocomotion");
            if (fly != null) rootTree.AddChild(fly, 2f);

            state.motion = rootTree;
            return state;
        }

        private BlendTree BuildGroundBlendTree(AnimatorController controller, SpeciesAnimData species)
        {
            var tree = new BlendTree
            {
                name = "GroundLocomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);

            var idle = _classifier.PickIdle(species)?.clip;
            var walk = _classifier.PickWalk(species)?.clip;
            var trot = _classifier.PickTrot(species)?.clip;
            var run = _classifier.PickRun(species)?.clip;

            if (idle != null) tree.AddChild(idle, 0f);
            if (walk != null) tree.AddChild(walk, 1f);
            if (trot != null) tree.AddChild(trot, 2f);
            if (run != null) tree.AddChild(run, 3f);

            if (tree.children.Length == 0)
            {
                var fallback = species.clips.First().clip;
                tree.AddChild(fallback, 0f);
            }

            return tree;
        }

        private BlendTree BuildCategoryBlendTree(AnimatorController controller, SpeciesAnimData species, AnimCategory category, string name)
        {
            var clips = species.clips.Where(c => c.category == category).Take(2).ToList();
            if (clips.Count == 0) return null;

            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);

            for (var i = 0; i < clips.Count; i++)
            {
                tree.AddChild(clips[i].clip, i == 0 ? 0f : 1f);
            }

            return tree;
        }

        private static void AddBaseParameters(AnimatorController controller, GeneratorMode mode)
        {
            AddIfMissing(controller, "Speed", AnimatorControllerParameterType.Float);
            AddIfMissing(controller, "Attack", AnimatorControllerParameterType.Trigger);
            AddIfMissing(controller, "Hit", AnimatorControllerParameterType.Trigger);
            AddIfMissing(controller, "IsDead", AnimatorControllerParameterType.Bool);
            if (mode == GeneratorMode.WithSwimFly)
            {
                AddIfMissing(controller, "Mode", AnimatorControllerParameterType.Int);
            }
        }

        private static void AddIfMissing(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (controller.parameters.Any(p => p.name == name)) return;
            controller.AddParameter(name, type);
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }

        private static string Sanitize(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }
    }
}
