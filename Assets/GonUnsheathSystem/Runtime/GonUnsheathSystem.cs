using UnityEngine;
using UnityEngine.Animations;

namespace Gon.Unsheath
{
    [DisallowMultipleComponent]
    public class GonUnsheathSystem : MonoBehaviour
    {
        [Header("References")]
        public Animator AvatarAnimator;
        public Transform SwordRoot;
        public Transform SheathAnchor;
        public Transform HandAnchor;
        public Transform HandleTarget;
        public Transform SheathMouthTarget;
        public Transform HandleTargetOffset;
        public Transform SheathMouthTargetOffset;

        [Header("Constraints")]
        public ParentConstraint SwordConstraint;
        public ParentConstraint RightHandConstraint;
        public ParentConstraint LeftHandConstraint;
        public ParentConstraint HandAnchorConstraint;

        [Header("Settings")]
        public string ParameterName = "GON_SHEATHED";
        public string MenuName = "Sheathe / Unsheathe";
        [Range(0.1f, 0.5f)]
        public float TransitionTime = 0.25f;

        [Header("Generated Assets")]
        public RuntimeAnimatorController GeneratedController;
        public AnimationClip SheathedClip;
        public AnimationClip UnsheathedClip;
        public AvatarMask GeneratedMask;
    }
}
