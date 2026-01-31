using System;
using UnityEngine;

namespace GonPresetAnimator.Editor
{
    [CreateAssetMenu(menuName = "GonPresetAnimator/Preset Definition", fileName = "PresetDefinition")]
    public class PresetDefinition : ScriptableObject
    {
        public string id = "preset";
        public string displayName = "Preset";
        [TextArea] public string description = "Preset description";

        [Header("Parameter")]
        public string parameterName = "GON_SHEATHED";
        public ParameterType parameterType = ParameterType.Bool;
        public int defaultInt = 0;
        public float defaultFloat = 0f;
        public bool defaultBool = true;
        public MenuType menuType = MenuType.Toggle;

        [Header("Animator")]
        public float transitionDuration = 0.25f;
        public string sheathedStateName = "Sheathed";
        public string unsheathedStateName = "Unsheathed";

        [Header("Constraints")]
        public bool useRightHandConstraint = true;
        public bool useLeftHandConstraint = true;
        public bool requireBothHands = false;

        [Header("Timings")]
        public SwitchMode switchMode = SwitchMode.Smooth;

        [Header("Advanced")]
        public bool applyAnimatorMask = true;
        public bool allowMirror = false;
    }

    public enum ParameterType
    {
        Bool,
        Int,
        Float
    }

    public enum MenuType
    {
        Toggle,
        Button,
        SubMenu
    }

    public enum SwitchMode
    {
        Instant,
        Smooth
    }
}
