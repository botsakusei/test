using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;

namespace GonPresetAnimator.Editor
{
    public static class FixActions
    {
        public static List<FixResult> Apply(AvatarContext context, List<DiagnosticIssue> issues)
        {
            var results = new List<FixResult>();
            foreach (var issue in issues)
            {
                if (!issue.CanAutoFix) continue;

                switch (issue.Id)
                {
                    case "ANIMATOR_MISSING":
                        if (context.AvatarRoot != null)
                        {
                            var animator = context.AvatarRoot.GetComponent<Animator>();
                            if (animator == null)
                            {
                                animator = Undo.AddComponent<Animator>(context.AvatarRoot);
                                results.Add(FixResult.Applied(issue.Id, "Animator added."));
                            }
                            context.Animator = animator;
                        }
                        break;
                    case "CTRL_MISSING":
                        if (context.GeneratedController != null)
                        {
                            Undo.RecordObject(context.Animator, "Assign Controller");
                            context.Animator.runtimeAnimatorController = context.GeneratedController;
                            EditorUtility.SetDirty(context.Animator);
                            results.Add(FixResult.Applied(issue.Id, "Controller assigned."));
                        }
                        break;
                    case "ANIMATOR_DISABLED":
                        Undo.RecordObject(context.Animator, "Enable Animator");
                        context.Animator.enabled = true;
                        EditorUtility.SetDirty(context.Animator);
                        results.Add(FixResult.Applied(issue.Id, "Animator enabled."));
                        break;
                    case "ANIMATOR_SPEED":
                        Undo.RecordObject(context.Animator, "Animator Speed");
                        context.Animator.speed = 1f;
                        EditorUtility.SetDirty(context.Animator);
                        results.Add(FixResult.Applied(issue.Id, "Animator.speed=1"));
                        break;
                    case "ANIMATOR_CULLING":
                        Undo.RecordObject(context.Animator, "Animator culling");
                        context.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        EditorUtility.SetDirty(context.Animator);
                        results.Add(FixResult.Applied(issue.Id, "Animator culling set to AlwaysAnimate"));
                        break;
                    case "LAYER_WEIGHT":
                        ApplyLayerWeights(context.Animator.runtimeAnimatorController as AnimatorController, results);
                        break;
                    case "CLIP_CURVES":
                        if (context.SwordConstraint != null && context.SheathedClip != null && context.UnsheathedClip != null)
                        {
                            ClipBuilder.BuildConstraintClips(
                                context.AvatarRoot.transform,
                                context.SwordConstraint,
                                context.RightHandConstraint,
                                context.LeftHandConstraint,
                                context.SheathedClip,
                                context.UnsheathedClip);
                            results.Add(FixResult.Applied(issue.Id, "Rebuilt constraint curves."));
                        }
                        break;
                    case "PARAM_TYPE":
                        if (context.Animator != null && context.GeneratedController != null)
                        {
                            Undo.RecordObject(context.GeneratedController, "Fix Parameter Type");
                            var controller = context.GeneratedController;
                            var param = controller.parameters.FirstOrDefault(p => p.name == context.PresetParameterName);
                            if (param != null)
                            {
                                controller.RemoveParameter(param);
                            }
                            var type = context.PresetParameterType == ParameterType.Int
                                ? AnimatorControllerParameterType.Int
                                : context.PresetParameterType == ParameterType.Float
                                    ? AnimatorControllerParameterType.Float
                                    : AnimatorControllerParameterType.Bool;
                            controller.AddParameter(context.PresetParameterName, type);
                            EditorUtility.SetDirty(controller);
                            results.Add(FixResult.Applied(issue.Id, "Animator parameter type corrected."));
                        }
                        break;
                    case "CONSTRAINT_INACTIVE":
                        if (context.SwordConstraint != null)
                        {
                            Undo.RecordObject(context.SwordConstraint, "Enable Constraint");
                            context.SwordConstraint.constraintActive = true;
                            EditorUtility.SetDirty(context.SwordConstraint);
                            results.Add(FixResult.Applied(issue.Id, "constraintActive enabled."));
                        }
                        break;
                    case "CONSTRAINT_SOURCES":
                        if (context.SwordConstraint != null && context.SheathAnchor != null && context.HandleAnchor != null)
                        {
                            Undo.RecordObject(context.SwordConstraint, "Reset Constraint Sources");
                            context.SwordConstraint.SetSources(new List<ConstraintSource>());
                            context.SwordConstraint.AddSource(new ConstraintSource { sourceTransform = context.SheathAnchor, weight = 1f });
                            context.SwordConstraint.AddSource(new ConstraintSource { sourceTransform = context.HandleAnchor, weight = 0f });
                            context.SwordConstraint.constraintActive = true;
                            context.SwordConstraint.locked = true;
                            EditorUtility.SetDirty(context.SwordConstraint);
                            results.Add(FixResult.Applied(issue.Id, "Constraint sources reset."));
                        }
                        break;
                }
            }

            AssetDatabase.SaveAssets();
            return results;
        }

        private static void ApplyLayerWeights(AnimatorController controller, List<FixResult> results)
        {
            if (controller == null) return;
            Undo.RecordObject(controller, "Fix Layer Weights");
            var layers = controller.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].defaultWeight <= 0f)
                {
                    layers[i].defaultWeight = 1f;
                }
            }
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
            results.Add(FixResult.Applied("LAYER_WEIGHT", "Layer weights set to 1."));
        }
    }

    public class FixResult
    {
        public string Id;
        public string Detail;
        public bool Applied;

        public static FixResult Applied(string id, string detail)
        {
            return new FixResult
            {
                Id = id,
                Detail = detail,
                Applied = true
            };
        }
    }
}
