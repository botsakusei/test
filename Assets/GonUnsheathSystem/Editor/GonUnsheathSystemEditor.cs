using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using Gon.Unsheath;

[CustomEditor(typeof(GonUnsheathSystem))]
public class GonUnsheathSystemEditor : Editor
{
    private const string RootFolder = "Assets/GonUnsheathSystem";
    private const string GeneratedFolder = RootFolder + "/Generated";
    private const string AnchorsRootName = "Gon_Anchors";
    private const string SheathAnchorName = "SheathAnchor";
    private const string HandAnchorName = "HandAnchor";
    private const string HandleTargetName = "HandleTarget";
    private const string SheathMouthTargetName = "SheathMouthTarget";
    private const string HandleTargetOffsetName = "HandleTargetOffset";
    private const string SheathMouthTargetOffsetName = "SheathMouthTargetOffset";

    public override void OnInspectorGUI()
    {
        var system = (GonUnsheathSystem)target;
        DrawDefaultInspector();

        EditorGUILayout.Space(8);

        using (new EditorGUI.DisabledScope(system == null))
        {
            if (GUILayout.Button("Auto Setup"))
            {
                AutoSetup(system);
            }

            if (GUILayout.Button("Build / Apply"))
            {
                BuildApply(system);
            }
        }
    }

    private static void AutoSetup(GonUnsheathSystem system)
    {
        if (system == null) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        var animator = system.AvatarAnimator != null
            ? system.AvatarAnimator
            : system.GetComponentInParent<Animator>();

        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
        {
            EditorUtility.DisplayDialog("Error", "Humanoid Animatorが見つかりません。", "OK");
            return;
        }

        system.AvatarAnimator = animator;
        Undo.RecordObject(system, "Auto Setup GonUnsheathSystem");

        if (system.SwordRoot == null)
        {
            Transform swordRoot = system.transform.Find("SwordRoot");
            if (swordRoot == null)
            {
                var swordRootGo = new GameObject("SwordRoot");
                Undo.RegisterCreatedObjectUndo(swordRootGo, "Create SwordRoot");
                swordRoot = swordRootGo.transform;
                swordRoot.SetParent(system.transform, false);
            }
            system.SwordRoot = swordRoot;
        }

        Transform anchorsRoot = FindOrCreateChild(animator.transform, AnchorsRootName);
        Transform sheathAnchor = FindOrCreateChild(anchorsRoot, SheathAnchorName);
        Transform handAnchor = FindOrCreateChild(anchorsRoot, HandAnchorName);
        Transform handleTarget = FindOrCreateChild(anchorsRoot, HandleTargetName);
        Transform sheathMouthTarget = FindOrCreateChild(anchorsRoot, SheathMouthTargetName);
        Transform handleTargetOffset = FindOrCreateChild(handleTarget, HandleTargetOffsetName);
        Transform sheathMouthTargetOffset = FindOrCreateChild(sheathMouthTarget, SheathMouthTargetOffsetName);

        system.SheathAnchor = sheathAnchor;
        system.HandAnchor = handAnchor;
        system.HandleTarget = handleTarget;
        system.SheathMouthTarget = sheathMouthTarget;
        system.HandleTargetOffset = handleTargetOffset;
        system.SheathMouthTargetOffset = sheathMouthTargetOffset;

        Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);

        if (rightHand == null || leftHand == null)
        {
            EditorUtility.DisplayDialog("Error", "RightHand/LeftHandボーンが見つかりません。", "OK");
            return;
        }

        if (system.SwordRoot != null)
        {
            sheathAnchor.position = system.SwordRoot.position;
            sheathAnchor.rotation = system.SwordRoot.rotation;

            handleTarget.position = system.SwordRoot.TransformPoint(new Vector3(0f, 0.05f, 0.02f));
            handleTarget.rotation = system.SwordRoot.rotation;

            sheathMouthTarget.position = system.SwordRoot.TransformPoint(new Vector3(0f, 0.01f, -0.02f));
            sheathMouthTarget.rotation = system.SwordRoot.rotation;
        }

        handAnchor.position = rightHand.position;
        handAnchor.rotation = rightHand.rotation;

        handleTargetOffset.localPosition = Vector3.zero;
        handleTargetOffset.localRotation = Quaternion.identity;
        sheathMouthTargetOffset.localPosition = Vector3.zero;
        sheathMouthTargetOffset.localRotation = Quaternion.identity;

        system.HandAnchorConstraint = EnsureParentConstraint(handAnchor, rightHand, true, system.HandAnchorConstraint);
        system.RightHandConstraint = EnsureParentConstraint(rightHand, handleTargetOffset, false, system.RightHandConstraint);
        system.LeftHandConstraint = EnsureParentConstraint(leftHand, sheathMouthTargetOffset, false, system.LeftHandConstraint);
        system.SwordConstraint = EnsureSwordConstraint(system.SwordRoot, sheathAnchor, handAnchor, system.SwordConstraint);

        EditorUtility.SetDirty(system);
        Undo.CollapseUndoOperations(group);
        Selection.activeObject = system.gameObject;
    }

    private static ParentConstraint EnsureParentConstraint(
        Transform target,
        Transform source,
        bool defaultWeight,
        ParentConstraint existing)
    {
        if (target == null || source == null) return existing;

        ParentConstraint constraint = existing != null
            ? existing
            : target.GetComponent<ParentConstraint>();

        if (constraint == null)
        {
            constraint = Undo.AddComponent<ParentConstraint>(target.gameObject);
        }

        constraint.constraintActive = false;
        constraint.locked = false;
        var sources = new List<ConstraintSource>();
        constraint.SetSources(sources);

        var cs = new ConstraintSource { sourceTransform = source, weight = 1f };
        constraint.AddSource(cs);
        constraint.translationAtRest = target.localPosition;
        constraint.rotationAtRest = target.localRotation;
        constraint.SetTranslationOffset(0, target.InverseTransformPoint(source.position));
        constraint.SetRotationOffset(0, (Quaternion.Inverse(source.rotation) * target.rotation).eulerAngles);
        constraint.constraintActive = true;
        constraint.locked = true;
        constraint.weight = defaultWeight ? 1f : 0f;

        return constraint;
    }

    private static ParentConstraint EnsureSwordConstraint(
        Transform swordRoot,
        Transform sheathAnchor,
        Transform handAnchor,
        ParentConstraint existing)
    {
        if (swordRoot == null || sheathAnchor == null || handAnchor == null) return existing;

        ParentConstraint constraint = existing != null
            ? existing
            : swordRoot.GetComponent<ParentConstraint>();

        if (constraint == null)
        {
            constraint = Undo.AddComponent<ParentConstraint>(swordRoot.gameObject);
        }

        constraint.constraintActive = false;
        constraint.locked = false;
        var sources = new List<ConstraintSource>();
        constraint.SetSources(sources);

        constraint.AddSource(new ConstraintSource { sourceTransform = sheathAnchor, weight = 1f });
        constraint.AddSource(new ConstraintSource { sourceTransform = handAnchor, weight = 0f });
        constraint.translationAtRest = swordRoot.localPosition;
        constraint.rotationAtRest = swordRoot.localRotation;
        constraint.SetTranslationOffset(0, swordRoot.InverseTransformPoint(sheathAnchor.position));
        constraint.SetRotationOffset(0, (Quaternion.Inverse(sheathAnchor.rotation) * swordRoot.rotation).eulerAngles);
        constraint.SetTranslationOffset(1, swordRoot.InverseTransformPoint(handAnchor.position));
        constraint.SetRotationOffset(1, (Quaternion.Inverse(handAnchor.rotation) * swordRoot.rotation).eulerAngles);
        constraint.constraintActive = true;
        constraint.locked = true;
        constraint.weight = 1f;

        return constraint;
    }

    private static void BuildApply(GonUnsheathSystem system)
    {
        if (system == null) return;

        if (system.AvatarAnimator == null)
        {
            EditorUtility.DisplayDialog("Error", "Animatorが設定されていません。Auto Setupを先に実行してください。", "OK");
            return;
        }

        if (system.SwordRoot == null || system.SwordConstraint == null ||
            system.RightHandConstraint == null || system.LeftHandConstraint == null)
        {
            EditorUtility.DisplayDialog("Error", "必要な参照が不足しています。Auto Setupを先に実行してください。", "OK");
            return;
        }

        string folderName = system.AvatarAnimator.gameObject.name;
        string outputFolder = Path.Combine(GeneratedFolder, folderName);
        EnsureFolder(outputFolder);

        string controllerPath = Path.Combine(outputFolder, "GonUnsheath_FX.controller").Replace("\\", "/");
        string sheathedPath = Path.Combine(outputFolder, "GonUnsheath_Sheathed.anim").Replace("\\", "/");
        string unsheathedPath = Path.Combine(outputFolder, "GonUnsheath_Unsheathed.anim").Replace("\\", "/");
        string maskPath = Path.Combine(outputFolder, "GonUnsheath.mask").Replace("\\", "/");

        var controller = CreateOrLoadAnimatorController(controllerPath);
        var sheathedClip = CreateOrLoadClip(sheathedPath);
        var unsheathedClip = CreateOrLoadClip(unsheathedPath);
        var mask = CreateOrLoadMask(maskPath);

        BuildClips(system, sheathedClip, unsheathedClip);
        BuildMask(system, mask);
        BuildAnimatorController(system, controller, sheathedClip, unsheathedClip, mask);

        system.GeneratedController = controller;
        system.SheathedClip = sheathedClip;
        system.UnsheathedClip = unsheathedClip;
        system.GeneratedMask = mask;

        ApplyModularAvatarComponents(system, controller);

        EditorUtility.SetDirty(system);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void BuildClips(GonUnsheathSystem system, AnimationClip sheathedClip, AnimationClip unsheathedClip)
    {
        sheathedClip.ClearCurves();
        unsheathedClip.ClearCurves();

        var avatarRoot = system.AvatarAnimator.transform;

        string swordPath = AnimationUtility.CalculateTransformPath(system.SwordRoot, avatarRoot);
        string rightHandPath = AnimationUtility.CalculateTransformPath(system.RightHandConstraint.transform, avatarRoot);
        string leftHandPath = AnimationUtility.CalculateTransformPath(system.LeftHandConstraint.transform, avatarRoot);

        AddCurve(sheathedClip, typeof(ParentConstraint), swordPath, "m_Sources.Array.data[0].weight", 1f);
        AddCurve(sheathedClip, typeof(ParentConstraint), swordPath, "m_Sources.Array.data[1].weight", 0f);
        AddCurve(unsheathedClip, typeof(ParentConstraint), swordPath, "m_Sources.Array.data[0].weight", 0f);
        AddCurve(unsheathedClip, typeof(ParentConstraint), swordPath, "m_Sources.Array.data[1].weight", 1f);

        AddCurve(sheathedClip, typeof(ParentConstraint), rightHandPath, "m_Weight", 0f);
        AddCurve(unsheathedClip, typeof(ParentConstraint), rightHandPath, "m_Weight", 1f);

        AddCurve(sheathedClip, typeof(ParentConstraint), leftHandPath, "m_Weight", 0f);
        AddCurve(unsheathedClip, typeof(ParentConstraint), leftHandPath, "m_Weight", 1f);

        EditorUtility.SetDirty(sheathedClip);
        EditorUtility.SetDirty(unsheathedClip);
    }

    private static void BuildAnimatorController(
        GonUnsheathSystem system,
        AnimatorController controller,
        AnimationClip sheathedClip,
        AnimationClip unsheathedClip,
        AvatarMask mask)
    {
        controller.layers = Array.Empty<AnimatorControllerLayer>();

        if (!controller.parameters.Any(p => p.name == system.ParameterName))
        {
            controller.AddParameter(system.ParameterName, AnimatorControllerParameterType.Bool);
        }

        var layer = new AnimatorControllerLayer
        {
            name = "Gon_Unsheath",
            defaultWeight = 1f,
            stateMachine = new AnimatorStateMachine(),
            avatarMask = mask,
            blendingMode = AnimatorLayerBlendingMode.Override
        };

        layer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);

        var stateSheathed = layer.stateMachine.AddState("Sheathed");
        stateSheathed.motion = sheathedClip;
        stateSheathed.writeDefaultValues = false;

        var stateUnsheathed = layer.stateMachine.AddState("Unsheathed");
        stateUnsheathed.motion = unsheathedClip;
        stateUnsheathed.writeDefaultValues = false;

        layer.stateMachine.defaultState = stateSheathed;

        var toUnsheathed = stateSheathed.AddTransition(stateUnsheathed);
        toUnsheathed.hasExitTime = false;
        toUnsheathed.duration = system.TransitionTime;
        toUnsheathed.AddCondition(AnimatorConditionMode.IfNot, 0f, system.ParameterName);

        var toSheathed = stateUnsheathed.AddTransition(stateSheathed);
        toSheathed.hasExitTime = false;
        toSheathed.duration = system.TransitionTime;
        toSheathed.AddCondition(AnimatorConditionMode.If, 0f, system.ParameterName);

        controller.AddLayer(layer);

        EditorUtility.SetDirty(controller);
    }

    private static void BuildMask(GonUnsheathSystem system, AvatarMask mask)
    {
        mask.transformCount = 0;

        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
        {
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        }

        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);

        Transform root = system.AvatarAnimator.transform;
        AddMaskTransform(mask, root, system.SwordRoot);
        AddMaskTransform(mask, root, system.SheathAnchor);
        AddMaskTransform(mask, root, system.HandAnchor);
        AddMaskTransform(mask, root, system.HandleTarget);
        AddMaskTransform(mask, root, system.SheathMouthTarget);

        EditorUtility.SetDirty(mask);
    }

    private static void AddMaskTransform(AvatarMask mask, Transform root, Transform target)
    {
        if (target == null) return;
        string path = AnimationUtility.CalculateTransformPath(target, root);
        var transform = string.IsNullOrEmpty(path) ? root : root.Find(path);
        if (transform != null)
        {
            mask.AddTransformPath(transform, true);
        }
    }

    private static void AddCurve(AnimationClip clip, Type type, string path, string property, float value)
    {
        var binding = new EditorCurveBinding
        {
            path = path,
            type = type,
            propertyName = property
        };

        var curve = new AnimationCurve();
        curve.AddKey(0f, value);
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    private static void ApplyModularAvatarComponents(GonUnsheathSystem system, RuntimeAnimatorController controller)
    {
        var go = system.gameObject;
        Type paramType = FindType("nadena.dev.modular_avatar.core.ModularAvatarParameters");
        Type menuType = FindType("nadena.dev.modular_avatar.core.ModularAvatarMenuItem");
        Type mergeType = FindType("nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator");

        if (paramType == null || menuType == null || mergeType == null)
        {
            Debug.LogWarning("Modular Avatarが見つからないため、MAコンポーネントの自動設定をスキップしました。");
            return;
        }

        var paramComp = EnsureComponent(go, paramType);
        var menuComp = EnsureComponent(go, menuType);
        var mergeComp = EnsureComponent(go, mergeType);

        ApplyParameters(paramComp, system.ParameterName);
        ApplyMenuItem(menuComp, system.MenuName, system.ParameterName);
        ApplyMergeAnimator(mergeComp, controller);
    }

    private static void ApplyParameters(Component comp, string paramName)
    {
        var so = new SerializedObject(comp);
        var list = so.FindProperty("parameters");
        if (list == null || !list.isArray)
        {
            Debug.LogWarning("MA Parametersの設定に失敗しました。");
            return;
        }

        for (int i = 0; i < list.arraySize; i++)
        {
            var element = list.GetArrayElementAtIndex(i);
            var nameProp = element.FindPropertyRelative("name");
            if (nameProp != null && nameProp.stringValue == paramName)
            {
                so.ApplyModifiedProperties();
                return;
            }
        }

        int newIndex = list.arraySize;
        list.InsertArrayElementAtIndex(newIndex);
        var newElem = list.GetArrayElementAtIndex(newIndex);
        newElem.FindPropertyRelative("name")?.SetStringValue(paramName);
        SetEnumByName(newElem.FindPropertyRelative("valueType"), "Bool");
        SetEnumByName(newElem.FindPropertyRelative("syncType"), "Bool");
        newElem.FindPropertyRelative("defaultValue")?.SetFloatValue(1f);
        newElem.FindPropertyRelative("saved")?.SetBoolValue(true);
        so.ApplyModifiedProperties();
    }

    private static void ApplyMenuItem(Component comp, string menuName, string paramName)
    {
        var so = new SerializedObject(comp);
        so.FindProperty("menuName")?.SetStringValue(menuName);

        var controlProp = so.FindProperty("control");
        if (controlProp != null)
        {
            controlProp.FindPropertyRelative("name")?.SetStringValue(menuName);
            SetEnumByName(controlProp.FindPropertyRelative("type"), "Toggle");
            var parameterProp = controlProp.FindPropertyRelative("parameter");
            parameterProp?.FindPropertyRelative("name")?.SetStringValue(paramName);
        }

        so.ApplyModifiedProperties();
    }

    private static void ApplyMergeAnimator(Component comp, RuntimeAnimatorController controller)
    {
        var so = new SerializedObject(comp);
        so.FindProperty("animator")?.SetObjectReferenceValue(controller);
        SetEnumByName(so.FindProperty("layerType"), "FX");
        so.ApplyModifiedProperties();
    }

    private static AnimatorController CreateOrLoadAnimatorController(string path)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        }
        return controller;
    }

    private static AnimationClip CreateOrLoadClip(string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(clip, path);
        }
        return clip;
    }

    private static AvatarMask CreateOrLoadMask(string path)
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, path);
        }
        return mask;
    }

    private static void EnsureFolder(string path)
    {
        string normalized = path.Replace("\\", "/");
        string[] parts = normalized.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private static Transform FindOrCreateChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null) return child;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static Component EnsureComponent(GameObject go, Type type)
    {
        var comp = go.GetComponent(type);
        if (comp == null)
        {
            comp = Undo.AddComponent(go, type);
        }
        return comp;
    }

    private static Type FindType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(fullName);
            if (type != null) return type;
        }
        return null;
    }

    private static void SetEnumByName(SerializedProperty prop, string name)
    {
        if (prop == null || prop.propertyType != SerializedPropertyType.Enum) return;
        var names = prop.enumNames;
        for (int i = 0; i < names.Length; i++)
        {
            if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase))
            {
                prop.enumValueIndex = i;
                return;
            }
        }
    }
}

internal static class SerializedPropertyExtensions
{
    public static void SetStringValue(this SerializedProperty prop, string value)
    {
        if (prop == null) return;
        prop.stringValue = value;
    }

    public static void SetFloatValue(this SerializedProperty prop, float value)
    {
        if (prop == null) return;
        prop.floatValue = value;
    }

    public static void SetBoolValue(this SerializedProperty prop, bool value)
    {
        if (prop == null) return;
        prop.boolValue = value;
    }

    public static void SetObjectReferenceValue(this SerializedProperty prop, UnityEngine.Object value)
    {
        if (prop == null) return;
        prop.objectReferenceValue = value;
    }
}

internal static class GonUnsheathPrefabBootstrap
{
    private const string PrefabFolder = "Assets/GonUnsheathSystem/Prefabs";
    private const string PrefabPath = PrefabFolder + "/GonUnsheathSystem.prefab";

    [InitializeOnLoadMethod]
    private static void EnsurePrefabExists()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            return;
        }

        EnsureFolder(PrefabFolder);

        var temp = new GameObject("GonUnsheathSystem");
        try
        {
            temp.AddComponent<GonUnsheathSystem>();
            PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(temp);
        }
    }

    private static void EnsureFolder(string path)
    {
        string normalized = path.Replace("\\", "/");
        string[] parts = normalized.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }
}
