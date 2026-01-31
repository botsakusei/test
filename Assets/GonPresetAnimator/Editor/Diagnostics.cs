using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;

namespace GonPresetAnimator.Editor
{
    public static class Diagnostics
    {
        public static List<DiagnosticIssue> Scan(AvatarContext context)
        {
            var issues = new List<DiagnosticIssue>();
            if (context.AvatarRoot == null)
            {
                issues.Add(DiagnosticIssue.Error("AVATAR_NULL", "Avatarが指定されていません。", "Avatarを指定してください。", false));
                return issues;
            }

            var animator = context.Animator;
            if (animator == null)
            {
                issues.Add(DiagnosticIssue.Error("ANIMATOR_MISSING", "Animatorが見つかりません。", "Animatorを追加してください。", true));
                return issues;
            }

            if (animator.runtimeAnimatorController == null)
            {
                issues.Add(DiagnosticIssue.Error("CTRL_MISSING", "AnimatorControllerが未設定です。", "Controllerを割り当てます。", context.GeneratedController != null));
            }

            if (!animator.enabled)
            {
                issues.Add(DiagnosticIssue.Error("ANIMATOR_DISABLED", "Animatorが無効です。", "Animatorを有効化します。", true));
            }

            if (Mathf.Approximately(animator.speed, 0f))
            {
                issues.Add(DiagnosticIssue.Error("ANIMATOR_SPEED", "Animator.speedが0です。", "Animator.speedを1に戻します。", true));
            }

            if (animator.cullingMode == AnimatorCullingMode.CullCompletely)
            {
                issues.Add(DiagnosticIssue.Warning("ANIMATOR_CULLING", "Animator.cullingModeがCullCompletelyです。", "AlwaysAnimateへ変更します。", true));
            }

            var controller = animator.runtimeAnimatorController as AnimatorController;
            if (controller != null)
            {
                if (controller.layers.Length == 0)
                {
                    issues.Add(DiagnosticIssue.Warning("CTRL_LAYERS", "AnimatorControllerにレイヤーがありません。", "Controller再生成を推奨します。", false));
                }

                foreach (var layer in controller.layers)
                {
                    if (layer.defaultWeight <= 0f)
                    {
                        issues.Add(DiagnosticIssue.Warning("LAYER_WEIGHT", $"Layer {layer.name} のweightが0です。", "Weightを1にします。", true));
                    }
                }

                if (!string.IsNullOrEmpty(context.PresetParameterName))
                {
                    if (!controller.parameters.Any(param => param.name == context.PresetParameterName))
                    {
                        issues.Add(DiagnosticIssue.Error("PARAM_MISSING", $"Animatorにパラメータ {context.PresetParameterName} がありません。", "Controllerを再生成してください。", false));
                    }
                    else
                    {
                        var parameter = controller.parameters.First(param => param.name == context.PresetParameterName);
                        if (ParameterTypeMap.FromAnimator(parameter.type) != context.PresetParameterType)
                        {
                            issues.Add(DiagnosticIssue.Error("PARAM_TYPE", $"Animatorパラメータ型が一致しません。({parameter.type})", "型を統一してください。", true));
                        }
                    }
                }

                if (context.SheathedClip != null)
                {
                    if (!HasConstraintCurves(context.SheathedClip))
                    {
                        issues.Add(DiagnosticIssue.Error("CLIP_CURVES", "Sheathed ClipにConstraintカーブがありません。", "Clipを再生成します。", true));
                    }
                }
            }

            var constraint = context.SwordConstraint;
            if (constraint == null)
            {
                issues.Add(DiagnosticIssue.Error("CONSTRAINT_MISSING", "Sword ParentConstraintがありません。", "Constraintを再生成します。", true));
            }
            else
            {
                if (!constraint.constraintActive)
                {
                    issues.Add(DiagnosticIssue.Warning("CONSTRAINT_INACTIVE", "Constraintが無効です。", "constraintActiveを有効化します。", true));
                }
                if (constraint.sourceCount < 2)
                {
                    issues.Add(DiagnosticIssue.Error("CONSTRAINT_SOURCES", "ConstraintのSourceが不足しています。", "Sourceを再設定します。", true));
                }
            }

            if (context.SheathedClip != null)
            {
                var missing = FindMissingBindings(context.AvatarRoot.transform, context.SheathedClip);
                if (missing.Count > 0)
                {
                    issues.Add(DiagnosticIssue.Error("CLIP_BINDING", $"Clipのバインド先が見つかりません: {missing[0]}", "Clipを再生成してください。", false));
                }
            }

            if (context.CloneRoot != null && context.EffectiveRoot == context.AvatarRoot && EditorApplication.isPlaying)
            {
                issues.Add(DiagnosticIssue.Warning("CLONE_MISMATCH", "Play中にCloneが存在しますがOriginalを見ています。", "Clone選択を推奨します。", false));
            }

            if (!string.IsNullOrEmpty(context.PresetParameterName))
            {
                var expressionType = ExpressionParameterScanner.TryGetExpressionParameterType(context.AvatarRoot, context.PresetParameterName);
                if (expressionType.HasValue && expressionType.Value != context.PresetParameterType)
                {
                    issues.Add(DiagnosticIssue.Warning("EXPR_PARAM_TYPE", $"ExpressionParameters型が一致しません。({expressionType.Value})", "ExpressionParametersを確認してください。", false));
                }
            }

            return issues;
        }

        private static bool HasConstraintCurves(AnimationClip clip)
        {
            return AnimationUtility.GetCurveBindings(clip)
                .Any(binding => binding.type == typeof(ParentConstraint) && binding.propertyName.Contains("m_Sources"));
        }

        private static List<string> FindMissingBindings(Transform avatarRoot, AnimationClip clip)
        {
            var missing = new List<string>();
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (string.IsNullOrEmpty(binding.path)) continue;
                if (avatarRoot.Find(binding.path) == null)
                {
                    missing.Add(binding.path);
                }
            }
            return missing;
        }
    }

    public static class ParameterTypeMap
    {
        public static ParameterType FromAnimator(AnimatorControllerParameterType type)
        {
            switch (type)
            {
                case AnimatorControllerParameterType.Int:
                    return ParameterType.Int;
                case AnimatorControllerParameterType.Float:
                    return ParameterType.Float;
                default:
                    return ParameterType.Bool;
            }
        }
    }

    public static class ExpressionParameterScanner
    {
        public static ParameterType? TryGetExpressionParameterType(GameObject avatarRoot, string parameterName)
        {
            var descriptorType = Type.GetType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor, VRCSDK3A")
                ?? Type.GetType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor");
            if (descriptorType == null) return null;

            var descriptor = avatarRoot.GetComponent(descriptorType);
            if (descriptor == null) return null;

            var descriptorSO = new SerializedObject(descriptor);
            var parametersProp = descriptorSO.FindProperty("expressionParameters");
            if (parametersProp == null || parametersProp.objectReferenceValue == null) return null;

            var parametersSO = new SerializedObject(parametersProp.objectReferenceValue);
            var list = parametersSO.FindProperty("parameters");
            if (list == null || !list.isArray) return null;

            for (int i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                string name = element.FindPropertyRelative("name")?.stringValue ?? string.Empty;
                if (name != parameterName) continue;
                var typeProp = element.FindPropertyRelative("valueType");
                if (typeProp == null || typeProp.propertyType != SerializedPropertyType.Enum) return null;
                var typeName = typeProp.enumNames[typeProp.enumValueIndex];
                return typeName == "Int" ? ParameterType.Int : typeName == "Float" ? ParameterType.Float : ParameterType.Bool;
            }

            return null;
        }
    }

    public class DiagnosticIssue
    {
        public string Id;
        public DiagnosticSeverity Severity;
        public string Message;
        public string FixHint;
        public bool CanAutoFix;

        public static DiagnosticIssue Error(string id, string message, string fixHint, bool canAutoFix)
        {
            return new DiagnosticIssue
            {
                Id = id,
                Severity = DiagnosticSeverity.Error,
                Message = message,
                FixHint = fixHint,
                CanAutoFix = canAutoFix
            };
        }

        public static DiagnosticIssue Warning(string id, string message, string fixHint, bool canAutoFix)
        {
            return new DiagnosticIssue
            {
                Id = id,
                Severity = DiagnosticSeverity.Warning,
                Message = message,
                FixHint = fixHint,
                CanAutoFix = canAutoFix
            };
        }
    }

    public enum DiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }
}
