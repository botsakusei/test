using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GonPresetAnimator.Editor
{
    public class PresetDatabase : ScriptableObject
    {
        public List<PresetDefinition> presets = new List<PresetDefinition>();

        public PresetDefinition GetById(string id)
        {
            return presets.FirstOrDefault(preset => preset != null && preset.id == id);
        }

    public static PresetDatabase LoadOrCreate()
    {
        const string path = "Assets/GonPresetAnimator/Presets/PresetDatabase.asset";
        var db = AssetDatabase.LoadAssetAtPath<PresetDatabase>(path);
        if (db != null) return db;

        if (!AssetDatabase.IsValidFolder("Assets/GonPresetAnimator/Presets"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/GonPresetAnimator"))
            {
                AssetDatabase.CreateFolder("Assets", "GonPresetAnimator");
            }
            AssetDatabase.CreateFolder("Assets/GonPresetAnimator", "Presets");
        }

        db = CreateInstance<PresetDatabase>();
        AssetDatabase.CreateAsset(db, path);
        AssetDatabase.SaveAssets();
        return db;
    }
}
}
