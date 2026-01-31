using UnityEditor;
using UnityEngine;

namespace GonPresetAnimator.Editor
{
    public static class PresetSamples
    {
        public static void CreateSamples()
        {
            const string folder = "Assets/GonPresetAnimator/Presets";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/GonPresetAnimator", "Presets");
            }

            var db = PresetDatabase.LoadOrCreate();
            db.presets.Clear();

            db.presets.Add(CreatePreset("instant_toggle", "Instant Toggle", SwitchMode.Instant, false, false));
            db.presets.Add(CreatePreset("smooth_toggle", "Smooth Toggle", SwitchMode.Smooth, true, true));
            db.presets.Add(CreatePreset("right_hand_only", "Right-hand Only", SwitchMode.Instant, true, false));
            db.presets.Add(CreatePreset("two_hand_support", "Two-hand Support", SwitchMode.Smooth, true, true));

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static PresetDefinition CreatePreset(string id, string name, SwitchMode mode, bool rightHand, bool leftHand)
        {
            var preset = ScriptableObject.CreateInstance<PresetDefinition>();
            preset.id = id;
            preset.displayName = name;
            preset.description = $"{name} preset";
            preset.parameterName = "GON_SHEATHED";
            preset.parameterType = ParameterType.Bool;
            preset.defaultBool = true;
            preset.menuType = MenuType.Toggle;
            preset.switchMode = mode;
            preset.useRightHandConstraint = rightHand;
            preset.useLeftHandConstraint = leftHand;
            preset.transitionDuration = mode == SwitchMode.Instant ? 0f : 0.25f;

            string path = $"Assets/GonPresetAnimator/Presets/{id}.asset";
            AssetDatabase.CreateAsset(preset, path);
            return preset;
        }
    }
}
