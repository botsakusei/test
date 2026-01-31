using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;

namespace GonPresetAnimator.Editor
{
    public class PresetAnimatorWindow : EditorWindow
    {
        private const string RootFolder = "Assets/GonPresetAnimator";
        private const string GeneratedFolder = RootFolder + "/Generated";

        private GameObject _avatarRoot;
        private Transform _weaponRoot;
        private Transform _sheathAnchor;
        private Transform _handleAnchor;
        private Transform _handleTarget;
        private Transform _sheathMouthTarget;
        private PresetDatabase _database;
        private int _presetIndex;
        private bool _scanCloneIfAvailable = true;
        private string _report = string.Empty;
        private Vector2 _scroll;

        [MenuItem("Tools/Gon Preset Animator")]
        public static void ShowWindow()
        {
            var window = GetWindow<PresetAnimatorWindow>();
            window.titleContent = new GUIContent("Gon Preset Animator");
            window.Show();
        }

        private void OnEnable()
        {
            _database = PresetDatabase.LoadOrCreate();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Target Avatar", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _avatarRoot = (GameObject)EditorGUILayout.ObjectField(_avatarRoot, typeof(GameObject), true);
                if (GUILayout.Button("Use Selection", GUILayout.Width(120)))
                {
                    _avatarRoot = Selection.activeGameObject;
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Weapon", EditorStyles.boldLabel);
            _weaponRoot = (Transform)EditorGUILayout.ObjectField("Weapon Root", _weaponRoot, typeof(Transform), true);
            _sheathAnchor = (Transform)EditorGUILayout.ObjectField("Sheath Anchor", _sheathAnchor, typeof(Transform), true);
            _handleAnchor = (Transform)EditorGUILayout.ObjectField("Handle Anchor", _handleAnchor, typeof(Transform), true);
            _handleTarget = (Transform)EditorGUILayout.ObjectField("Handle Target (Right Hand)", _handleTarget, typeof(Transform), true);
            _sheathMouthTarget = (Transform)EditorGUILayout.ObjectField("Sheath Mouth Target (Left Hand)", _sheathMouthTarget, typeof(Transform), true);

            EditorGUILayout.Space();
            _scanCloneIfAvailable = EditorGUILayout.ToggleLeft("Scan Clone (if exists)", _scanCloneIfAvailable);

            EditorGUILayout.Space();
            DrawPresetSelector();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_avatarRoot == null || _weaponRoot == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Scan"))
                    {
                        RunScan();
                    }
                    if (GUILayout.Button("Fix (Dry-run)"))
                    {
                        RunDryFix();
                    }
                    if (GUILayout.Button("Apply Fix"))
                    {
                        RunApplyFix();
                    }
                    if (GUILayout.Button("Build/Apply"))
                    {
                        RunBuildApply();
                    }
                }
            }

            if (EditorApplication.isPlaying)
            {
                DrawPreviewControls();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (!string.IsNullOrEmpty(_report))
            {
                EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
            }
            else
            {
                EditorGUILayout.HelpBox("Scan を実行すると診断レポートが表示されます。", MessageType.Info);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawPresetSelector()
        {
            EditorGUILayout.LabelField("Preset", EditorStyles.boldLabel);
            if (_database == null)
            {
                EditorGUILayout.HelpBox("PresetDatabase が見つかりません。", MessageType.Warning);
                return;
            }

            var presets = _database.presets.Where(preset => preset != null).ToList();
            if (presets.Count == 0)
            {
                EditorGUILayout.HelpBox("Preset がありません。", MessageType.Warning);
                if (GUILayout.Button("Create Sample Presets"))
                {
                    PresetSamples.CreateSamples();
                    _database = PresetDatabase.LoadOrCreate();
                }
                return;
            }

            var names = presets.Select(preset => preset.displayName).ToArray();
            _presetIndex = Mathf.Clamp(_presetIndex, 0, names.Length - 1);
            _presetIndex = EditorGUILayout.Popup("Preset", _presetIndex, names);
        }

        private PresetDefinition GetSelectedPreset()
        {
            var presets = _database.presets.Where(preset => preset != null).ToList();
            if (presets.Count == 0) return null;
            _presetIndex = Mathf.Clamp(_presetIndex, 0, presets.Count - 1);
            return presets[_presetIndex];
        }

        private void RunScan()
        {
            var context = BuildContext();
            var issues = Diagnostics.Scan(context);
            _report = BuildReport("Scan", issues, new List<FixResult>());
            Debug.Log(_report);
        }

        private void RunDryFix()
        {
            var context = BuildContext();
            var issues = Diagnostics.Scan(context);
            var plan = issues.Where(issue => issue.CanAutoFix).ToList();
            var fakeResults = plan.Select(issue => new FixResult { Id = issue.Id, Detail = issue.FixHint, Applied = false }).ToList();
            _report = BuildReport("Dry-run", issues, fakeResults);
            Debug.Log(_report);
        }

        private void RunApplyFix()
        {
            var context = BuildContext();
            var issues = Diagnostics.Scan(context);
            var results = FixActions.Apply(context, issues);
            _report = BuildReport("Apply Fix", issues, results);
            Debug.Log(_report);
        }

        private void RunBuildApply()
        {
            var preset = GetSelectedPreset();
            if (preset == null) return;

            var context = BuildContext();
            var outputFolder = Path.Combine(GeneratedFolder, context.AvatarRoot.name);
            EnsureFolder(outputFolder);

            string controllerPath = Path.Combine(outputFolder, $"{preset.id}_FX.controller").Replace("\\", "/");
            string sheathedPath = Path.Combine(outputFolder, $"{preset.id}_Sheathed.anim").Replace("\\", "/");
            string unsheathedPath = Path.Combine(outputFolder, $"{preset.id}_Unsheathed.anim").Replace("\\", "/");
            string maskPath = Path.Combine(outputFolder, $"{preset.id}.mask").Replace("\\", "/");

            var sheathedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(sheathedPath) ?? new AnimationClip();
            if (AssetDatabase.GetAssetPath(sheathedClip) == string.Empty)
            {
                AssetDatabase.CreateAsset(sheathedClip, sheathedPath);
            }

            var unsheathedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(unsheathedPath) ?? new AnimationClip();
            if (AssetDatabase.GetAssetPath(unsheathedClip) == string.Empty)
            {
                AssetDatabase.CreateAsset(unsheathedClip, unsheathedPath);
            }

            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath) ?? new AvatarMask();
            if (AssetDatabase.GetAssetPath(mask) == string.Empty)
            {
                AssetDatabase.CreateAsset(mask, maskPath);
            }

            EnsureAnchors(context);
            EnsureConstraints(context, preset);

            ClipBuilder.BuildConstraintClips(
                context.AvatarRoot.transform,
                context.SwordConstraint,
                context.RightHandConstraint,
                context.LeftHandConstraint,
                sheathedClip,
                unsheathedClip);

            BuildMask(context, mask);
            var controller = ControllerBuilder.BuildController(controllerPath, preset, sheathedClip, unsheathedClip, mask);

            context.GeneratedController = controller;
            context.SheathedClip = sheathedClip;
            context.UnsheathedClip = unsheathedClip;

            bool maApplied = MAIntegration.TryApply(context.AvatarRoot, preset, controller);
            if (!maApplied)
            {
                Debug.LogWarning("Modular Avatar が見つからないため、MA統合はスキップしました。");
            }

            AssetDatabase.SaveAssets();
            _report = "Build/Apply completed.";
        }

        private AvatarContext BuildContext()
        {
            var preset = GetSelectedPreset();
            var context = new AvatarContext();
            context.AvatarRoot = ResolveAvatarRoot();
            context.CloneRoot = FindClone(context.AvatarRoot);
            context.EffectiveRoot = context.CloneRoot != null && _scanCloneIfAvailable && EditorApplication.isPlaying ? context.CloneRoot : context.AvatarRoot;
            context.Animator = context.EffectiveRoot != null ? context.EffectiveRoot.GetComponentInChildren<Animator>(true) : null;
            context.WeaponRoot = _weaponRoot;
            context.SheathAnchor = _sheathAnchor;
            context.HandleAnchor = _handleAnchor;
            context.HandleTarget = _handleTarget;
            context.SheathMouthTarget = _sheathMouthTarget;
            if (preset != null)
            {
                context.PresetParameterName = preset.parameterName;
                context.PresetParameterType = preset.parameterType;
            }
            return context;
        }

        private GameObject ResolveAvatarRoot()
        {
            if (_avatarRoot == null) return null;
            return _avatarRoot;
        }

        private static GameObject FindClone(GameObject root)
        {
            if (root == null) return null;
            return GameObject.Find($"{root.name}(Clone)");
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace("\\", "/");
                string name = Path.GetFileName(path);
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureFolder(parent);
                }
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private void EnsureAnchors(AvatarContext context)
        {
            var root = context.AvatarRoot.transform.Find("GonPresetSystem");
            if (root == null)
            {
                var go = new GameObject("GonPresetSystem");
                Undo.RegisterCreatedObjectUndo(go, "Create GonPresetSystem");
                go.transform.SetParent(context.AvatarRoot.transform, false);
                root = go.transform;
            }

            var anchors = root.Find("Anchors");
            if (anchors == null)
            {
                var go = new GameObject("Anchors");
                Undo.RegisterCreatedObjectUndo(go, "Create Anchors");
                go.transform.SetParent(root, false);
                anchors = go.transform;
            }

            context.SheathAnchor = EnsureAnchor(anchors, context.SheathAnchor, "SheathAnchor", context.WeaponRoot);
            context.HandleAnchor = EnsureAnchor(anchors, context.HandleAnchor, "HandleAnchor", context.WeaponRoot);
            context.HandleTarget = EnsureAnchor(anchors, context.HandleTarget, "HandleTarget", context.WeaponRoot);
            context.SheathMouthTarget = EnsureAnchor(anchors, context.SheathMouthTarget, "SheathMouthTarget", context.WeaponRoot);
        }

        private Transform EnsureAnchor(Transform parent, Transform current, string name, Transform weaponRoot)
        {
            if (current != null) return current;
            var anchor = parent.Find(name);
            if (anchor == null)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
                anchor = go.transform;
                anchor.SetParent(parent, false);
                if (weaponRoot != null)
                {
                    anchor.position = weaponRoot.position;
                }
            }
            return anchor;
        }

        private void EnsureConstraints(AvatarContext context, PresetDefinition preset)
        {
            if (context.WeaponRoot == null) return;
            var constraint = context.WeaponRoot.GetComponent<ParentConstraint>();
            if (constraint == null)
            {
                constraint = Undo.AddComponent<ParentConstraint>(context.WeaponRoot.gameObject);
            }

            constraint.constraintActive = false;
            constraint.locked = false;
            constraint.SetSources(new List<ConstraintSource>());
            constraint.AddSource(new ConstraintSource { sourceTransform = context.SheathAnchor, weight = 1f });
            constraint.AddSource(new ConstraintSource { sourceTransform = context.HandleAnchor, weight = 0f });
            constraint.translationAtRest = context.WeaponRoot.localPosition;
            constraint.rotationAtRest = context.WeaponRoot.localEulerAngles;
            constraint.constraintActive = true;
            constraint.locked = true;
            constraint.weight = 1f;
            context.SwordConstraint = constraint;

            var animator = context.Animator;
            if (animator != null)
            {
                var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (preset.useRightHandConstraint && rightHand != null && context.HandleTarget != null)
                {
                    context.RightHandConstraint = EnsureHandConstraint(rightHand, context.HandleTarget);
                }
                var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                if (preset.useLeftHandConstraint && leftHand != null && context.SheathMouthTarget != null)
                {
                    context.LeftHandConstraint = EnsureHandConstraint(leftHand, context.SheathMouthTarget);
                }
            }
        }

        private ParentConstraint EnsureHandConstraint(Transform hand, Transform target)
        {
            var constraint = hand.GetComponent<ParentConstraint>();
            if (constraint == null)
            {
                constraint = Undo.AddComponent<ParentConstraint>(hand.gameObject);
            }
            constraint.constraintActive = false;
            constraint.locked = false;
            constraint.SetSources(new List<ConstraintSource>());
            constraint.AddSource(new ConstraintSource { sourceTransform = target, weight = 1f });
            constraint.translationAtRest = hand.localPosition;
            constraint.rotationAtRest = hand.localEulerAngles;
            constraint.constraintActive = true;
            constraint.locked = true;
            constraint.weight = 0f;
            return constraint;
        }

        private void BuildMask(AvatarContext context, AvatarMask mask)
        {
            mask.transformCount = 0;
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            }
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);

            var root = context.AvatarRoot.transform;
            AddMaskTransform(mask, root, context.WeaponRoot);
            AddMaskTransform(mask, root, context.SheathAnchor);
            AddMaskTransform(mask, root, context.HandleAnchor);
            AddMaskTransform(mask, root, context.HandleTarget);
            AddMaskTransform(mask, root, context.SheathMouthTarget);
            EditorUtility.SetDirty(mask);
        }

        private static void AddMaskTransform(AvatarMask mask, Transform root, Transform target)
        {
            if (target == null) return;
            string path = AnimationUtility.CalculateTransformPath(target, root);
            if (!string.IsNullOrEmpty(path))
            {
                mask.AddTransformPath(root.Find(path), true);
            }
        }

        private void DrawPreviewControls()
        {
            var preset = GetSelectedPreset();
            if (preset == null) return;
            if (_avatarRoot == null) return;
            var animator = _avatarRoot.GetComponentInChildren<Animator>(true);
            if (animator == null) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Set Sheathed"))
                {
                    SetParameter(animator, preset, false);
                }
                if (GUILayout.Button("Set Unsheathed"))
                {
                    SetParameter(animator, preset, true);
                }
            }
        }

        private void SetParameter(Animator animator, PresetDefinition preset, bool unsheathed)
        {
            switch (preset.parameterType)
            {
                case ParameterType.Bool:
                    animator.SetBool(preset.parameterName, unsheathed);
                    break;
                case ParameterType.Int:
                    animator.SetInteger(preset.parameterName, unsheathed ? 1 : 0);
                    break;
                case ParameterType.Float:
                    animator.SetFloat(preset.parameterName, unsheathed ? 1f : 0f);
                    break;
            }
        }

        private string BuildReport(string title, List<DiagnosticIssue> issues, List<FixResult> fixes)
        {
            var lines = new List<string>
            {
                $"== Gon Preset Animator ({title}) ==",
                $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                ""
            };

            if (issues.Count == 0)
            {
                lines.Add("問題は検出されませんでした。");
            }
            else
            {
                lines.Add("■ Issues:");
                foreach (var issue in issues)
                {
                    lines.Add($"- [{issue.Severity}] {issue.Id}: {issue.Message}");
                    lines.Add($"  修正案: {issue.FixHint}");
                    lines.Add($"  自動修正: {(issue.CanAutoFix ? "可" : "不可")}");
                }
            }

            lines.Add("");
            lines.Add("■ Fixes:");
            if (fixes.Count == 0)
            {
                lines.Add("修正はありません。");
            }
            else
            {
                foreach (var fix in fixes)
                {
                    lines.Add($"- [{(fix.Applied ? "APPLIED" : "PLANNED")}] {fix.Id}: {fix.Detail}");
                }
            }

            return string.Join("\n", lines);
        }
    }

    public class AvatarContext
    {
        public GameObject AvatarRoot;
        public GameObject CloneRoot;
        public GameObject EffectiveRoot;
        public Animator Animator;
        public Transform WeaponRoot;
        public Transform SheathAnchor;
        public Transform HandleAnchor;
        public Transform HandleTarget;
        public Transform SheathMouthTarget;
        public ParentConstraint SwordConstraint;
        public ParentConstraint RightHandConstraint;
        public ParentConstraint LeftHandConstraint;
        public AnimatorController GeneratedController;
        public AnimationClip SheathedClip;
        public AnimationClip UnsheathedClip;
        public string PresetParameterName;
        public ParameterType PresetParameterType = ParameterType.Bool;
    }
}
