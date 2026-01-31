using System;
using UnityEditor;
using UnityEngine;

namespace GonPresetAnimator.Editor
{
    public static class MAIntegration
    {
        public static bool TryApply(GameObject avatarRoot, PresetDefinition preset, RuntimeAnimatorController controller)
        {
            var paramType = FindType("nadena.dev.modular_avatar.core.ModularAvatarParameters");
            var menuType = FindType("nadena.dev.modular_avatar.core.ModularAvatarMenuItem");
            var mergeType = FindType("nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator");

            if (paramType == null || menuType == null || mergeType == null)
            {
                return false;
            }

            var paramComp = EnsureComponent(avatarRoot, paramType);
            var menuComp = EnsureComponent(avatarRoot, menuType);
            var mergeComp = EnsureComponent(avatarRoot, mergeType);

            ApplyParameters(paramComp, preset);
            ApplyMenuItem(menuComp, preset);
            ApplyMergeAnimator(mergeComp, controller);
            return true;
        }

        private static void ApplyParameters(Component component, PresetDefinition preset)
        {
            var so = new SerializedObject(component);
            var list = so.FindProperty("parameters");
            if (list == null || !list.isArray) return;

            int index = FindParameterIndex(list, preset.parameterName);
            if (index < 0)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                index = list.arraySize - 1;
            }

            var element = list.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("name").stringValue = preset.parameterName;
            SetEnum(element.FindPropertyRelative("valueType"), preset.parameterType);
            SetEnum(element.FindPropertyRelative("syncType"), preset.parameterType);
            element.FindPropertyRelative("saved").boolValue = true;
            element.FindPropertyRelative("defaultValue").floatValue = preset.defaultBool ? 1f : 0f;
            so.ApplyModifiedProperties();
        }

        private static void ApplyMenuItem(Component component, PresetDefinition preset)
        {
            var so = new SerializedObject(component);
            so.FindProperty("control").FindPropertyRelative("name").stringValue = preset.displayName;
            var typeProp = so.FindProperty("control").FindPropertyRelative("type");
            if (typeProp != null && typeProp.propertyType == SerializedPropertyType.Enum)
            {
                typeProp.enumValueIndex = preset.menuType == MenuType.Toggle ? 1 : 0;
            }
            var paramProp = so.FindProperty("control").FindPropertyRelative("parameter");
            if (paramProp != null)
            {
                paramProp.FindPropertyRelative("name").stringValue = preset.parameterName;
            }
            so.ApplyModifiedProperties();
        }

        private static void ApplyMergeAnimator(Component component, RuntimeAnimatorController controller)
        {
            var so = new SerializedObject(component);
            var prop = so.FindProperty("animatorController");
            if (prop != null)
            {
                prop.objectReferenceValue = controller;
            }
            so.ApplyModifiedProperties();
        }

        private static int FindParameterIndex(SerializedProperty list, string name)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("name")?.stringValue == name)
                {
                    return i;
                }
            }
            return -1;
        }

        private static void SetEnum(SerializedProperty prop, ParameterType type)
        {
            if (prop == null || prop.propertyType != SerializedPropertyType.Enum) return;
            var name = type == ParameterType.Bool ? "Bool" : type == ParameterType.Int ? "Int" : "Float";
            int index = Array.FindIndex(prop.enumNames, entry => entry == name);
            if (index >= 0)
            {
                prop.enumValueIndex = index;
            }
        }

        private static Component EnsureComponent(GameObject target, Type type)
        {
            var component = target.GetComponent(type);
            if (component == null)
            {
                component = target.AddComponent(type);
            }
            return component;
        }

        private static Type FindType(string typeName)
        {
            var direct = Type.GetType(typeName);
            if (direct != null) return direct;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;
            }
            return null;
        }
    }
}
