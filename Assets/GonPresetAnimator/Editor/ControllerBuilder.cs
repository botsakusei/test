using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace GonPresetAnimator.Editor
{
    public static class ControllerBuilder
    {
        public static AnimatorController BuildController(string path, PresetDefinition preset, AnimationClip sheathedClip, AnimationClip unsheathedClip, AvatarMask mask)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }

            controller.layers = Array.Empty<AnimatorControllerLayer>();

            EnsureParameter(controller, preset);

            var layer = new AnimatorControllerLayer
            {
                name = "GonPreset_FX",
                defaultWeight = 1f,
                stateMachine = new AnimatorStateMachine(),
                blendingMode = AnimatorLayerBlendingMode.Override,
                avatarMask = mask
            };

            layer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);

            var stateSheathed = layer.stateMachine.AddState(preset.sheathedStateName);
            stateSheathed.motion = sheathedClip;
            stateSheathed.writeDefaultValues = false;

            var stateUnsheathed = layer.stateMachine.AddState(preset.unsheathedStateName);
            stateUnsheathed.motion = unsheathedClip;
            stateUnsheathed.writeDefaultValues = false;

            layer.stateMachine.defaultState = stateSheathed;

            var toUnsheathed = layer.stateMachine.AddAnyStateTransition(stateUnsheathed);
            toUnsheathed.hasExitTime = false;
            toUnsheathed.duration = preset.transitionDuration;
            AddCondition(toUnsheathed, preset, true);

            var toSheathed = layer.stateMachine.AddAnyStateTransition(stateSheathed);
            toSheathed.hasExitTime = false;
            toSheathed.duration = preset.transitionDuration;
            AddCondition(toSheathed, preset, false);

            controller.AddLayer(layer);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void EnsureParameter(AnimatorController controller, PresetDefinition preset)
        {
            var existing = Array.Find(controller.parameters, parameter => parameter.name == preset.parameterName);
            var type = ToAnimatorParameterType(preset.parameterType);
            if (existing == null)
            {
                controller.AddParameter(preset.parameterName, type);
                return;
            }

            if (existing.type != type)
            {
                controller.RemoveParameter(existing);
                controller.AddParameter(preset.parameterName, type);
            }
        }

        private static AnimatorControllerParameterType ToAnimatorParameterType(ParameterType type)
        {
            switch (type)
            {
                case ParameterType.Int:
                    return AnimatorControllerParameterType.Int;
                case ParameterType.Float:
                    return AnimatorControllerParameterType.Float;
                default:
                    return AnimatorControllerParameterType.Bool;
            }
        }

        private static void AddCondition(AnimatorStateTransition transition, PresetDefinition preset, bool unsheathed)
        {
            switch (preset.parameterType)
            {
                case ParameterType.Bool:
                    transition.AddCondition(unsheathed ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, preset.parameterName);
                    break;
                case ParameterType.Int:
                    transition.AddCondition(unsheathed ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, 0f, preset.parameterName);
                    break;
                case ParameterType.Float:
                    transition.AddCondition(unsheathed ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, 0f, preset.parameterName);
                    break;
            }
        }
    }
}
