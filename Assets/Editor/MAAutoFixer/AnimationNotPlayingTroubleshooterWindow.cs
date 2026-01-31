using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;

namespace GonUnsheathSystem.Editor
{
    public class AnimationNotPlayingTroubleshooterWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Animation Not Playing Troubleshooter";
        private const string WindowTitle = "Animation Not Playing Troubleshooter";

        private GameObject _avatarRoot;
        private Animator _targetAnimator;
        private RuntimeAnimatorController _expectedController;
        private bool _scanCloneIfAvailable = true;
        private bool _applyToOriginalInEditMode = false;
        private bool _autoSelectClone = true;
        private Vector2 _scroll;
        private string _report = string.Empty;
        private List<Issue> _issues = new List<Issue>();
        private List<FixAction> _fixPlan = new List<FixAction>();

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<AnimationNotPlayingTroubleshooterWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Play Mode: {(EditorApplication.isPlaying ? "PLAYING" : "EDIT")}");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Target Avatar", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _avatarRoot = (GameObject)EditorGUILayout.ObjectField(_avatarRoot, typeof(GameObject), true);
                if (GUILayout.Button("Use Selection", GUILayout.Width(120)))
                {
                    _avatarRoot = Selection.activeGameObject;
                }
            }

            if (_avatarRoot != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select Clone", GUILayout.Width(120)))
                    {
                        var clone = FindClone(_avatarRoot);
                        if (clone != null)
                        {
                            Selection.activeGameObject = clone;
                        }
                    }
                    if (GUILayout.Button("Select Original", GUILayout.Width(120)))
                    {
                        Selection.activeGameObject = _avatarRoot;
                    }
                }
            }

            _targetAnimator = (Animator)EditorGUILayout.ObjectField("Target Animator (optional)", _targetAnimator, typeof(Animator), true);
            _expectedController = (RuntimeAnimatorController)EditorGUILayout.ObjectField("Expected Controller", _expectedController, typeof(RuntimeAnimatorController), false);

            EditorGUILayout.Space();
            _scanCloneIfAvailable = EditorGUILayout.ToggleLeft("Scan Clone (if exists)", _scanCloneIfAvailable);
            _autoSelectClone = EditorGUILayout.ToggleLeft("Prefer Clone in Play Mode", _autoSelectClone);
            _applyToOriginalInEditMode = EditorGUILayout.ToggleLeft("Apply Fix to Original in Edit Mode", _applyToOriginalInEditMode);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_avatarRoot == null))
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
                }
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

        private void RunScan()
        {
            var context = BuildContext();
            _issues = Scanner.Scan(context);
            _fixPlan.Clear();
            _report = Reporter.BuildReport(context, _issues, _fixPlan, "Scan");
            Debug.Log(_report);
        }

        private void RunDryFix()
        {
            var context = BuildContext();
            _issues = Scanner.Scan(context);
            _fixPlan = FixPlanner.Plan(context, _issues);
            _report = Reporter.BuildReport(context, _issues, _fixPlan, "Dry-run");
            Debug.Log(_report);
        }

        private void RunApplyFix()
        {
            var context = BuildContext();
            _issues = Scanner.Scan(context);
            _fixPlan = FixPlanner.Plan(context, _issues);
            var applied = FixApplier.Apply(context, _fixPlan);
            _report = Reporter.BuildReport(context, _issues, applied, "Apply");
            Debug.Log(_report);
        }

        private ScanContext BuildContext()
        {
            var context = new ScanContext
            {
                AvatarRoot = _avatarRoot,
                TargetAnimator = _targetAnimator,
                ExpectedController = _expectedController,
                ScanCloneIfAvailable = _scanCloneIfAvailable,
                PreferCloneInPlayMode = _autoSelectClone,
                ApplyToOriginalInEditMode = _applyToOriginalInEditMode
            };

            context.CloneRoot = FindClone(_avatarRoot);
            if (EditorApplication.isPlaying && context.PreferCloneInPlayMode && context.CloneRoot != null)
            {
                context.EffectiveRoot = context.CloneRoot;
            }
            else
            {
                context.EffectiveRoot = context.AvatarRoot;
            }

            return context;
        }

        private static GameObject FindClone(GameObject avatarRoot)
        {
            if (avatarRoot == null) return null;
            var clone = GameObject.Find($"{avatarRoot.name}(Clone)");
            return clone;
        }

        private class ScanContext
        {
            public GameObject AvatarRoot;
            public GameObject CloneRoot;
            public GameObject EffectiveRoot;
            public Animator TargetAnimator;
            public RuntimeAnimatorController ExpectedController;
            public bool ScanCloneIfAvailable;
            public bool PreferCloneInPlayMode;
            public bool ApplyToOriginalInEditMode;
        }

        private enum Severity
        {
            Info,
            Warning,
            Error
        }

        private class Issue
        {
            public string Id;
            public Severity SeverityLevel;
            public string Cause;
            public string Evidence;
            public string FixHint;
            public bool CanAutoFix;
        }

        private class FixAction
        {
            public string Id;
            public string Detail;
            public bool Applied;
        }

        private static class Reporter
        {
            public static string BuildReport(ScanContext context, List<Issue> issues, List<FixAction> fixes, string mode)
            {
                var lines = new List<string>
                {
                    $"== Animation Not Playing Troubleshooter ({mode}) ==",
                    $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    $"Avatar: {context.EffectiveRoot?.name ?? "None"}",
                    $"Play Mode: {EditorApplication.isPlaying}",
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
                        lines.Add($"- [{issue.SeverityLevel}] {issue.Id}");
                        if (!string.IsNullOrEmpty(issue.Cause)) lines.Add($"  原因: {issue.Cause}");
                        if (!string.IsNullOrEmpty(issue.Evidence)) lines.Add($"  根拠: {issue.Evidence}");
                        if (!string.IsNullOrEmpty(issue.FixHint)) lines.Add($"  修正案: {issue.FixHint}");
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
                        lines.Add($"- [{(fix.Applied ? "APPLIED" : "PLANNED")}] {fix.Id}");
                        if (!string.IsNullOrEmpty(fix.Detail)) lines.Add($"  内容: {fix.Detail}");
                    }
                }

                return string.Join("\n", lines);
            }
        }

        private static class Scanner
        {
            public static List<Issue> Scan(ScanContext context)
            {
                var issues = new List<Issue>();
                if (context.AvatarRoot == null)
                {
                    issues.Add(new Issue
                    {
                        Id = "A0",
                        SeverityLevel = Severity.Error,
                        Cause = "アバターが選択されていません。",
                        FixHint = "Hierarchy からアバターを指定してください。",
                        CanAutoFix = false
                    });
                    return issues;
                }

                bool cloneExists = context.CloneRoot != null;
                if (EditorApplication.isPlaying && cloneExists && context.EffectiveRoot == context.AvatarRoot)
                {
                    issues.Add(new Issue
                    {
                        Id = "A1",
                        SeverityLevel = Severity.Warning,
                        Cause = "Playモード中に Clone が存在するのに Original を見ています。",
                        Evidence = $"Clone: {context.CloneRoot.name}",
                        FixHint = "Select Clone を押すか、Prefer Clone in Play Mode を有効にしてください。",
                        CanAutoFix = false
                    });
                }

                var animator = ResolveAnimator(context);
                if (animator == null)
                {
                    issues.Add(new Issue
                    {
                        Id = "B1",
                        SeverityLevel = Severity.Error,
                        Cause = "Animator が見つかりません。",
                        FixHint = "アバター直下の Animator を指定してください。",
                        CanAutoFix = false
                    });
                    return issues;
                }

                if (animator.runtimeAnimatorController == null)
                {
                    issues.Add(new Issue
                    {
                        Id = "B2",
                        SeverityLevel = Severity.Error,
                        Cause = "Animator に Controller が割り当てられていません。",
                        FixHint = "期待する Controller を割り当ててください。",
                        CanAutoFix = context.ExpectedController != null
                    });
                }
                else if (context.ExpectedController != null && animator.runtimeAnimatorController != context.ExpectedController)
                {
                    issues.Add(new Issue
                    {
                        Id = "B3",
                        SeverityLevel = Severity.Warning,
                        Cause = "Animator が想定 Controller を参照していません。",
                        Evidence = $"Current: {animator.runtimeAnimatorController.name}",
                        FixHint = "Expected Controller を割り当てます。",
                        CanAutoFix = true
                    });
                }

                if (!animator.enabled)
                {
                    issues.Add(new Issue
                    {
                        Id = "C1",
                        SeverityLevel = Severity.Error,
                        Cause = "Animator.enabled が false です。",
                        FixHint = "Animator を有効化します。",
                        CanAutoFix = true
                    });
                }

                if (Mathf.Approximately(animator.speed, 0f))
                {
                    issues.Add(new Issue
                    {
                        Id = "C2",
                        SeverityLevel = Severity.Error,
                        Cause = "Animator.speed が 0 です。",
                        FixHint = "Animator.speed を 1 に戻します。",
                        CanAutoFix = true
                    });
                }

                if (animator.cullingMode == AnimatorCullingMode.CullCompletely)
                {
                    issues.Add(new Issue
                    {
                        Id = "C3",
                        SeverityLevel = Severity.Warning,
                        Cause = "Animator.cullingMode が Cull Completely です。",
                        FixHint = "Always Animate を推奨します。",
                        CanAutoFix = true
                    });
                }

                var controller = animator.runtimeAnimatorController as AnimatorController;
                if (controller != null)
                {
                    CheckLayerWeights(controller, issues);
                    CheckParameters(controller, issues);
                    CheckClipBindings(animator, controller, issues);
                    CheckAvatarMask(controller, animator, issues);
                }

                CheckConstraints(animator, issues);
                CheckConsoleErrors(issues);

                return issues;
            }

            private static Animator ResolveAnimator(ScanContext context)
            {
                if (context.TargetAnimator != null) return context.TargetAnimator;
                if (context.EffectiveRoot == null) return null;
                return context.EffectiveRoot.GetComponentInChildren<Animator>(true);
            }

            private static void CheckLayerWeights(AnimatorController controller, List<Issue> issues)
            {
                for (int i = 0; i < controller.layers.Length; i++)
                {
                    var layer = controller.layers[i];
                    if (layer.defaultWeight <= 0f)
                    {
                        issues.Add(new Issue
                        {
                            Id = "D1",
                            SeverityLevel = Severity.Warning,
                            Cause = $"Layer '{layer.name}' の defaultWeight が 0 です。",
                            FixHint = "レイヤーの Weight を 1 にします。",
                            CanAutoFix = true
                        });
                    }
                }
            }

            private static void CheckParameters(AnimatorController controller, List<Issue> issues)
            {
                if (!controller.parameters.Any())
                {
                    issues.Add(new Issue
                    {
                        Id = "E1",
                        SeverityLevel = Severity.Warning,
                        Cause = "AnimatorController にパラメータがありません。",
                        FixHint = "必要なパラメータを追加してください。",
                        CanAutoFix = false
                    });
                }
            }

            private static void CheckClipBindings(Animator animator, AnimatorController controller, List<Issue> issues)
            {
                var clips = controller.animationClips.Distinct().ToList();
                foreach (var clip in clips)
                {
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    foreach (var binding in bindings)
                    {
                        if (string.IsNullOrEmpty(binding.path)) continue;
                        if (animator.transform.Find(binding.path) == null)
                        {
                            issues.Add(new Issue
                            {
                                Id = "F1",
                                SeverityLevel = Severity.Error,
                                Cause = "クリップのバインド先が存在しません。",
                                Evidence = $"{clip.name}: {binding.path}",
                                FixHint = "Animator を正しい root に付けるか、クリップを再生成してください。",
                                CanAutoFix = false
                            });
                            return;
                        }
                    }
                }
            }

            private static void CheckConstraints(Animator animator, List<Issue> issues)
            {
                foreach (var constraint in animator.GetComponentsInChildren<IConstraint>(true))
                {
                    var component = constraint as Component;
                    if (component == null) continue;
                    bool active = constraint.constraintActive;
                    bool enabled = component.gameObject.activeInHierarchy;
                    if (!active)
                    {
                        issues.Add(new Issue
                        {
                            Id = "G1",
                            SeverityLevel = Severity.Warning,
                            Cause = $"{component.name} の constraintActive が false です。",
                            FixHint = "constraintActive を true にします。",
                            CanAutoFix = true
                        });
                    }
                    if (!enabled)
                    {
                        issues.Add(new Issue
                        {
                            Id = "G2",
                            SeverityLevel = Severity.Warning,
                            Cause = $"{component.name} が非アクティブです。",
                            FixHint = "GameObject を有効にしてください。",
                            CanAutoFix = false
                        });
                    }
                }
            }

            private static void CheckAvatarMask(AnimatorController controller, Animator animator, List<Issue> issues)
            {
                foreach (var layer in controller.layers)
                {
                    if (layer.avatarMask == null) continue;
                    if (layer.avatarMask.transformCount == 0) continue;

                    bool hasAnimatorRoot = layer.avatarMask.GetTransformActive(0);
                    if (!hasAnimatorRoot)
                    {
                        issues.Add(new Issue
                        {
                            Id = "H1",
                            SeverityLevel = Severity.Warning,
                            Cause = $"AvatarMask が Animator root を含んでいません: {layer.name}",
                            FixHint = "マスクに対象 Transform を追加してください。",
                            CanAutoFix = false
                        });
                    }
                }
            }

            private static void CheckConsoleErrors(List<Issue> issues)
            {
                foreach (var message in ConsoleReader.GetMessages(30))
                {
                    if (message.IndexOf("MA-0006", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        issues.Add(new Issue
                        {
                            Id = "J1",
                            SeverityLevel = Severity.Error,
                            Cause = "NDMF Console に MA-0006 が残っています。",
                            Evidence = message,
                            FixHint = "MA Auto Fixer で型競合を解消してください。",
                            CanAutoFix = false
                        });
                        return;
                    }
                }
            }
        }

        private static class FixPlanner
        {
            public static List<FixAction> Plan(ScanContext context, List<Issue> issues)
            {
                var plan = new List<FixAction>();
                foreach (var issue in issues.Where(i => i.CanAutoFix))
                {
                    plan.Add(new FixAction
                    {
                        Id = issue.Id,
                        Detail = issue.FixHint
                    });
                }

                if (!EditorApplication.isPlaying && !context.ApplyToOriginalInEditMode)
                {
                    plan.Add(new FixAction
                    {
                        Id = "SAFE-01",
                        Detail = "Edit Mode では Original に直接修正しません (Apply To Original を有効にしてください)。"
                    });
                }

                return plan;
            }
        }

        private static class FixApplier
        {
            public static List<FixAction> Apply(ScanContext context, List<FixAction> plan)
            {
                var applied = new List<FixAction>();
                var animator = context.TargetAnimator ?? context.EffectiveRoot?.GetComponentInChildren<Animator>(true);
                if (animator == null) return applied;

                bool canTouch = EditorApplication.isPlaying || context.ApplyToOriginalInEditMode;
                if (!canTouch) return applied;

                foreach (var fix in plan)
                {
                    switch (fix.Id)
                    {
                        case "B2":
                        case "B3":
                            if (context.ExpectedController != null)
                            {
                                Undo.RecordObject(animator, "Assign Controller");
                                animator.runtimeAnimatorController = context.ExpectedController;
                                EditorUtility.SetDirty(animator);
                                applied.Add(new FixAction { Id = fix.Id, Detail = "Controller を割当", Applied = true });
                            }
                            break;
                        case "C1":
                            Undo.RecordObject(animator, "Enable Animator");
                            animator.enabled = true;
                            EditorUtility.SetDirty(animator);
                            applied.Add(new FixAction { Id = fix.Id, Detail = "Animator.enabled = true", Applied = true });
                            break;
                        case "C2":
                            Undo.RecordObject(animator, "Animator speed");
                            animator.speed = 1f;
                            EditorUtility.SetDirty(animator);
                            applied.Add(new FixAction { Id = fix.Id, Detail = "Animator.speed = 1", Applied = true });
                            break;
                        case "C3":
                            Undo.RecordObject(animator, "Animator culling");
                            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                            EditorUtility.SetDirty(animator);
                            applied.Add(new FixAction { Id = fix.Id, Detail = "cullingMode = AlwaysAnimate", Applied = true });
                            break;
                        case "D1":
                            ApplyLayerWeights(animator.runtimeAnimatorController as AnimatorController, applied);
                            break;
                        case "G1":
                            ApplyConstraintActive(animator, applied);
                            break;
                    }
                }

                AssetDatabase.SaveAssets();
                return applied;
            }

            private static void ApplyLayerWeights(AnimatorController controller, List<FixAction> applied)
            {
                if (controller == null) return;
                Undo.RecordObject(controller, "Fix Layer Weights");
                var layers = controller.layers;
                for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i].defaultWeight <= 0f)
                    {
                        layers[i].defaultWeight = 1f;
                    }
                }
                controller.layers = layers;
                EditorUtility.SetDirty(controller);
                applied.Add(new FixAction { Id = "D1", Detail = "Layer defaultWeight を 1 に設定", Applied = true });
            }

            private static void ApplyConstraintActive(Animator animator, List<FixAction> applied)
            {
                foreach (var constraint in animator.GetComponentsInChildren<IConstraint>(true))
                {
                    if (!constraint.constraintActive)
                    {
                        Undo.RecordObject((Component)constraint, "Enable Constraint");
                        constraint.constraintActive = true;
                        EditorUtility.SetDirty((Component)constraint);
                        applied.Add(new FixAction { Id = "G1", Detail = "constraintActive = true", Applied = true });
                    }
                }
            }
        }

        private static class ConsoleReader
        {
            public static List<string> GetMessages(int maxEntries)
            {
                var results = new List<string>();
                var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor.dll")
                    ?? Type.GetType("UnityEditorInternal.LogEntries, UnityEditor.dll");
                var logEntryType = Type.GetType("UnityEditor.LogEntry, UnityEditor.dll")
                    ?? Type.GetType("UnityEditorInternal.LogEntry, UnityEditor.dll");
                if (logEntriesType == null || logEntryType == null) return results;

                var getCount = logEntriesType.GetMethod("GetCount", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var getEntry = logEntriesType.GetMethod("GetEntryInternal", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                    ?? logEntriesType.GetMethod("GetEntry", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var conditionField = logEntryType.GetField("condition", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (getCount == null || getEntry == null || conditionField == null) return results;

                int count = (int)getCount.Invoke(null, null);
                var entry = Activator.CreateInstance(logEntryType);
                for (int i = count - 1; i >= 0 && results.Count < maxEntries; i--)
                {
                    getEntry.Invoke(null, new[] { i, entry });
                    var condition = conditionField.GetValue(entry) as string;
                    if (!string.IsNullOrEmpty(condition))
                    {
                        results.Add(condition);
                    }
                }

                return results;
            }
        }
    }
}
