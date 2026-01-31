using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

namespace GonPresetAnimator.Editor
{
    public static class ClipBuilder
    {
        public static void BuildConstraintClips(
            Transform avatarRoot,
            ParentConstraint swordConstraint,
            ParentConstraint rightHandConstraint,
            ParentConstraint leftHandConstraint,
            AnimationClip sheathedClip,
            AnimationClip unsheathedClip)
        {
            sheathedClip.ClearCurves();
            unsheathedClip.ClearCurves();

            if (avatarRoot == null) return;

            if (swordConstraint != null)
            {
                string path = AnimationUtility.CalculateTransformPath(swordConstraint.transform, avatarRoot);
                AddCurve(sheathedClip, path, "m_Weight", 1f);
                AddCurve(unsheathedClip, path, "m_Weight", 1f);

                int sourceCount = swordConstraint.sourceCount;
                for (int i = 0; i < sourceCount; i++)
                {
                    float sheathedWeight = i == 0 ? 1f : 0f;
                    float unsheathedWeight = i == 1 ? 1f : 0f;
                    AddCurve(sheathedClip, path, $"m_Sources.Array.data[{i}].weight", sheathedWeight);
                    AddCurve(unsheathedClip, path, $"m_Sources.Array.data[{i}].weight", unsheathedWeight);
                }
            }

            if (rightHandConstraint != null)
            {
                string path = AnimationUtility.CalculateTransformPath(rightHandConstraint.transform, avatarRoot);
                AddCurve(sheathedClip, path, "m_Weight", 0f);
                AddCurve(unsheathedClip, path, "m_Weight", 1f);
            }

            if (leftHandConstraint != null)
            {
                string path = AnimationUtility.CalculateTransformPath(leftHandConstraint.transform, avatarRoot);
                AddCurve(sheathedClip, path, "m_Weight", 0f);
                AddCurve(unsheathedClip, path, "m_Weight", 1f);
            }

            EditorUtility.SetDirty(sheathedClip);
            EditorUtility.SetDirty(unsheathedClip);
        }

        private static void AddCurve(AnimationClip clip, string path, string property, float value)
        {
            var binding = new EditorCurveBinding
            {
                path = path,
                type = typeof(ParentConstraint),
                propertyName = property
            };
            var curve = new AnimationCurve();
            curve.AddKey(0f, value);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
    }
}
