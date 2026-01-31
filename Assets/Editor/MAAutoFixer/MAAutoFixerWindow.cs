using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class MAAutoFixerWindow : EditorWindow
{
    private const string WindowTitle = "MA Auto Fixer";
    private const string MenuPath = "Tools/MA Auto Fixer";

    private GameObject _avatarRoot;
    private Vector2 _scroll;
    private List<Issue> _issues = new List<Issue>();
    private List<FixAction> _plannedFixes = new List<FixAction>();
    private string _report = string.Empty;
    private bool _scanCloneIfAvailable = true;
    private bool _verboseLogging = true;
    private bool _allowExpressionParameterEdits = false;

    [MenuItem(MenuPath)]
    public static void ShowWindow()
    {
        var window = GetWindow<MAAutoFixerWindow>();
        window.titleContent = new GUIContent(WindowTitle);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Avatar", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            _avatarRoot = (GameObject)EditorGUILayout.ObjectField(_avatarRoot, typeof(GameObject), true);
            if (GUILayout.Button("Use Selection", GUILayout.Width(120)))
            {
                _avatarRoot = Selection.activeGameObject;
            }
        }

        if (_avatarRoot == null)
        {
            EditorGUILayout.HelpBox("Hierarchy のアバターを指定してください。", MessageType.Info);
        }

        EditorGUILayout.Space();

        _scanCloneIfAvailable = EditorGUILayout.ToggleLeft("Scan Clone (if exists)", _scanCloneIfAvailable);
        _verboseLogging = EditorGUILayout.ToggleLeft("Verbose Log", _verboseLogging);
        _allowExpressionParameterEdits = EditorGUILayout.ToggleLeft("Allow ExpressionParameters edits (Apply Fix)", _allowExpressionParameterEdits);

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

        DrawReport();
    }

    private void DrawReport()
    {
        EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        if (!string.IsNullOrEmpty(_report))
        {
            EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
        }
        else
        {
            EditorGUILayout.HelpBox("Scan を実行すると結果が表示されます。", MessageType.Info);
        }

        EditorGUILayout.EndScrollView();
    }

    private void RunScan()
    {
        var targets = ResolveScanTargets();
        _issues = Analyzer.Scan(targets, _verboseLogging);
        _plannedFixes.Clear();
        _report = Reporter.BuildReport(_issues, _plannedFixes, "Scan");
        Debug.Log(_report);
    }

    private void RunDryFix()
    {
        var targets = ResolveScanTargets();
        _issues = Analyzer.Scan(targets, _verboseLogging);
        _plannedFixes = FixPlanner.Plan(targets, _issues, _allowExpressionParameterEdits);
        _report = Reporter.BuildReport(_issues, _plannedFixes, "Dry-run");
        Debug.Log(_report);
    }

    private void RunApplyFix()
    {
        var targets = ResolveScanTargets();
        _issues = Analyzer.Scan(targets, _verboseLogging);
        _plannedFixes = FixPlanner.Plan(targets, _issues, _allowExpressionParameterEdits);
        var applied = FixApplier.Apply(targets, _plannedFixes, _allowExpressionParameterEdits);
        _report = Reporter.BuildReport(_issues, applied, "Apply");
        Debug.Log(_report);
    }

    private List<GameObject> ResolveScanTargets()
    {
        var targets = new List<GameObject>();
        if (_avatarRoot != null)
        {
            targets.Add(_avatarRoot);
        }

        if (_scanCloneIfAvailable && _avatarRoot != null)
        {
            string cloneName = $"{_avatarRoot.name}(Clone)";
            var clone = GameObject.Find(cloneName);
            if (clone != null && !targets.Contains(clone))
            {
                targets.Add(clone);
            }
        }

        return targets;
    }

    private enum Severity
    {
        Info,
        Warning,
        Error
    }

    private class Issue
    {
        public Severity SeverityLevel;
        public string Title;
        public string Cause;
        public string Impact;
        public string FixHint;
        public bool CanAutoFix;
        public string SourcePath;
    }

    private class FixAction
    {
        public string Title;
        public string Detail;
        public bool Applied;
        public string TargetPath;
    }

    private static class Reporter
    {
        public static string BuildReport(List<Issue> issues, List<FixAction> fixes, string mode)
        {
            var lines = new List<string>
            {
                $"== MA Auto Fixer ({mode}) ==",
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
                    lines.Add($"- [{issue.SeverityLevel}] {issue.Title}");
                    if (!string.IsNullOrEmpty(issue.Cause)) lines.Add($"  原因: {issue.Cause}");
                    if (!string.IsNullOrEmpty(issue.Impact)) lines.Add($"  影響: {issue.Impact}");
                    if (!string.IsNullOrEmpty(issue.FixHint)) lines.Add($"  修正案: {issue.FixHint}");
                    if (!string.IsNullOrEmpty(issue.SourcePath)) lines.Add($"  対象: {issue.SourcePath}");
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
                    string status = fix.Applied ? "APPLIED" : "PLANNED";
                    lines.Add($"- [{status}] {fix.Title}");
                    if (!string.IsNullOrEmpty(fix.Detail)) lines.Add($"  内容: {fix.Detail}");
                    if (!string.IsNullOrEmpty(fix.TargetPath)) lines.Add($"  対象: {fix.TargetPath}");
                }
            }

            return string.Join("\n", lines);
        }
    }

    private static class Analyzer
    {
        public static List<Issue> Scan(List<GameObject> avatarRoots, bool verbose)
        {
            var issues = new List<Issue>();
            if (avatarRoots == null || avatarRoots.Count == 0)
            {
                issues.Add(new Issue
                {
                    SeverityLevel = Severity.Error,
                    Title = "アバター未指定",
                    Cause = "アバターが選択されていません。",
                    Impact = "診断が実行できません。",
                    FixHint = "Hierarchy からアバターを指定してください。",
                    CanAutoFix = false
                });
                return issues;
            }

            foreach (var avatarRoot in avatarRoots)
            {
                if (avatarRoot == null) continue;
                if (verbose)
                {
                    Debug.Log($"[MAAutoFixer] Scan target: {GetGameObjectPath(avatarRoot)} (ID:{avatarRoot.GetInstanceID()})");
                }

                var sources = ParameterCollector.Collect(avatarRoot, verbose);

                foreach (var empty in sources.EmptyNameEntries)
                {
                    issues.Add(new Issue
                    {
                        SeverityLevel = Severity.Error,
                        Title = "パラメータ名が空欄",
                        Cause = "パラメータ名が空文字の定義が存在します。",
                        Impact = "MA-0006 の原因になり得ます。",
                        FixHint = "空欄パラメータを削除してください。",
                        CanAutoFix = true,
                        SourcePath = empty.SourcePath
                    });
                }

                foreach (var conflict in sources.Conflicts)
                {
                    issues.Add(new Issue
                    {
                        SeverityLevel = Severity.Error,
                        Title = "パラメータ型の競合",
                        Cause = $"{conflict.Name} に複数の型が定義されています。{conflict.ObservedTypes}",
                        Impact = "MA-0006 が発生し、ビルドが停止します。",
                        FixHint = $"優先順位に従って {conflict.TargetType} に統一します。",
                        CanAutoFix = true,
                        SourcePath = conflict.SourceSummary
                    });
                }

                foreach (var menuIssue in MenuInstallerScanner.Scan(avatarRoot))
                {
                    issues.Add(menuIssue);
                }
            }

            issues.Add(new Issue
            {
                SeverityLevel = Severity.Info,
                Title = "フォント警告について",
                Cause = "Noto Sans CJK JP のフォントが見つからない警告です。",
                Impact = "ビルド失敗の原因ではありません。",
                FixHint = "必要ならフォントを導入するか、NDMFの言語設定を変更してください。",
                CanAutoFix = false
            });

            return issues;
        }
    }

    private static class FixPlanner
    {
        public static List<FixAction> Plan(List<GameObject> avatarRoots, List<Issue> issues, bool allowExpressionEdits)
        {
            var planned = new List<FixAction>();
            if (avatarRoots == null || avatarRoots.Count == 0) return planned;

            foreach (var avatarRoot in avatarRoots)
            {
                if (avatarRoot == null) continue;
                var sources = ParameterCollector.Collect(avatarRoot, false);

                foreach (var empty in sources.EmptyNameEntries)
                {
                    planned.Add(new FixAction
                    {
                        Title = "空欄パラメータを削除",
                        Detail = empty.Detail,
                        TargetPath = empty.SourcePath
                    });
                }

                foreach (var conflict in sources.Conflicts)
                {
                    planned.Add(new FixAction
                    {
                        Title = "パラメータ型の統一",
                        Detail = $"{conflict.Name} -> {conflict.TargetType}",
                        TargetPath = conflict.SourceSummary
                    });
                }

                foreach (var fix in MenuInstallerScanner.PlanFixes(avatarRoot))
                {
                    planned.Add(fix);
                }

                if (!allowExpressionEdits && sources.Conflicts.Any(conflict => conflict.HasExpressionEntry))
                {
                    planned.Add(new FixAction
                    {
                        Title = "ExpressionParameters の変更は未許可",
                        Detail = "Allow ExpressionParameters edits を有効にすると修正対象に含めます。",
                        TargetPath = avatarRoot.name
                    });
                }
            }

            return planned;
        }
    }

    private static class FixApplier
    {
        public static List<FixAction> Apply(List<GameObject> avatarRoots, List<FixAction> plannedFixes, bool allowExpressionEdits)
        {
            var applied = new List<FixAction>();
            if (avatarRoots == null || avatarRoots.Count == 0) return applied;

            foreach (var avatarRoot in avatarRoots)
            {
                if (avatarRoot == null) continue;
                var sources = ParameterCollector.Collect(avatarRoot, false);
                ParameterFixer.ApplyEmptyNameFixes(sources.EmptyNameEntries, applied);
                ParameterFixer.ApplyConflicts(sources.Conflicts, applied, allowExpressionEdits);
                MenuInstallerScanner.ApplyFixes(avatarRoot, applied);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return applied;
        }
    }

    private enum ParameterType
    {
        Bool,
        Int,
        Float,
        Unknown
    }

    private class ParameterEntry
    {
        public string Name;
        public ParameterType Type;
        public UnityEngine.Object Owner;
        public string SourcePath;
        public string SourceDetail;
        public int Index;
        public SerializedObject SerializedOwner;
        public SerializedProperty Element;
        public bool IsExpressionParameter;
        public bool IsAnimatorParameter;
        public AnimatorController AnimatorController;
    }

    private class ConflictInfo
    {
        public string Name;
        public ParameterType TargetType;
        public List<ParameterEntry> Entries = new List<ParameterEntry>();
        public string SourceSummary;
        public bool HasExpressionEntry;
        public string ObservedTypes;
    }

    private class EmptyEntryInfo
    {
        public ParameterEntry Entry;
        public string SourcePath;
        public string Detail;
    }

    private class ParameterSources
    {
        public List<ParameterEntry> Entries = new List<ParameterEntry>();
        public List<ConflictInfo> Conflicts = new List<ConflictInfo>();
        public List<EmptyEntryInfo> EmptyNameEntries = new List<EmptyEntryInfo>();
    }

    private static class ParameterCollector
    {
        private static readonly string[] MaParamTypeNames =
        {
            "nadena.dev.modular_avatar.core.ModularAvatarParameters",
            "nadena.dev.modular_avatar.core.ParameterAssigner",
            "nadena.dev.modular_avatar.core.ModularAvatarParameter",
            "nadena.dev.modular_avatar.core.ModularAvatarRenameParameters"
        };

        public static ParameterSources Collect(GameObject avatarRoot, bool verbose)
        {
            var sources = new ParameterSources();
            CollectExpressionParameters(avatarRoot, sources, verbose);
            CollectAnimatorParameters(avatarRoot, sources, verbose);
            CollectMaParameters(avatarRoot, sources);
            sources.Conflicts = BuildConflicts(sources.Entries);
            sources.EmptyNameEntries = sources.Entries
                .Where(entry => string.IsNullOrEmpty(entry.Name))
                .Select(entry => new EmptyEntryInfo
                {
                    Entry = entry,
                    SourcePath = entry.SourcePath,
                    Detail = $"{entry.SourcePath} の Index {entry.Index}"
                })
                .ToList();
            if (verbose)
            {
                Debug.Log($"[MAAutoFixer] Parameter entries: {sources.Entries.Count}, Conflicts: {sources.Conflicts.Count}");
            }

            return sources;
        }

        private static void CollectExpressionParameters(GameObject avatarRoot, ParameterSources sources, bool verbose)
        {
            var descriptorType = FindType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor, VRCSDK3A")
                ?? FindType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor");
            if (descriptorType == null) return;

            var descriptor = avatarRoot.GetComponent(descriptorType);
            if (descriptor == null) return;

            if (verbose)
            {
                Debug.Log("[MAAutoFixer] VRCAvatarDescriptor found.");
            }

            var descriptorSO = new SerializedObject(descriptor);
            var parametersProp = descriptorSO.FindProperty("expressionParameters");
            if (parametersProp == null || parametersProp.objectReferenceValue == null) return;

            var parametersAsset = parametersProp.objectReferenceValue;
            if (verbose)
            {
                Debug.Log($"[MAAutoFixer] ExpressionParameters: {AssetDatabase.GetAssetPath(parametersAsset)}");
            }
            var parametersSO = new SerializedObject(parametersAsset);
            var list = parametersSO.FindProperty("parameters");
            if (list == null || !list.isArray) return;

            for (int i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                string name = element.FindPropertyRelative("name")?.stringValue ?? string.Empty;
                var type = ParameterTypeMapper.FromEnumProperty(element.FindPropertyRelative("valueType"));

                sources.Entries.Add(new ParameterEntry
                {
                    Name = name,
                    Type = type,
                    Owner = parametersAsset,
                    SourcePath = AssetDatabase.GetAssetPath(parametersAsset),
                    SourceDetail = $"ExpressionParameters: {AssetDatabase.GetAssetPath(parametersAsset)} ({type})",
                    Index = i,
                    SerializedOwner = parametersSO,
                    Element = element,
                    IsExpressionParameter = true
                });
            }
        }

        private static void CollectAnimatorParameters(GameObject avatarRoot, ParameterSources sources, bool verbose)
        {
            var controllers = new HashSet<AnimatorController>();
            void AddController(RuntimeAnimatorController controller)
            {
                if (controller is AnimatorController animatorController)
                {
                    controllers.Add(animatorController);
                }
            }

            foreach (var animator in avatarRoot.GetComponentsInChildren<Animator>(true))
            {
                AddController(animator.runtimeAnimatorController);
            }

            foreach (var controller in CollectControllersFromDescriptor(avatarRoot))
            {
                controllers.Add(controller);
            }

            if (verbose)
            {
                foreach (var controller in controllers)
                {
                    if (controller != null)
                    {
                        Debug.Log($"[MAAutoFixer] AnimatorController: {AssetDatabase.GetAssetPath(controller)}");
                    }
                }
            }

            foreach (var controller in controllers)
            {
                foreach (var param in controller.parameters)
                {
                    sources.Entries.Add(new ParameterEntry
                    {
                        Name = param.name,
                        Type = ParameterTypeMapper.FromAnimatorType(param.type),
                        Owner = controller,
                        SourcePath = AssetDatabase.GetAssetPath(controller),
                        SourceDetail = $"AnimatorController: {AssetDatabase.GetAssetPath(controller)} ({param.type})",
                        Index = -1,
                        IsAnimatorParameter = true,
                        AnimatorController = controller
                    });
                }
            }
        }

        private static void CollectMaParameters(GameObject avatarRoot, ParameterSources sources)
        {
            foreach (var typeName in MaParamTypeNames)
            {
                var type = FindType(typeName);
                if (type == null) continue;

                var components = avatarRoot.GetComponentsInChildren(type, true);
                foreach (var component in components)
                {
                    var so = new SerializedObject(component);
                    var list = so.FindProperty("parameters");
                    if (list == null || !list.isArray) continue;

                    for (int i = 0; i < list.arraySize; i++)
                    {
                        var element = list.GetArrayElementAtIndex(i);
                        string name = element.FindPropertyRelative("name")?.stringValue ?? string.Empty;
                        var typeProp = element.FindPropertyRelative("valueType") ?? element.FindPropertyRelative("syncType");
                        var pType = ParameterTypeMapper.FromEnumProperty(typeProp);
                        sources.Entries.Add(new ParameterEntry
                        {
                            Name = name,
                            Type = pType,
                            Owner = component,
                            SourcePath = component.name,
                            SourceDetail = $"MA Component: {component.name} ({pType})",
                            Index = i,
                            SerializedOwner = so,
                            Element = element
                        });
                    }
                }
            }
        }

        private static List<ConflictInfo> BuildConflicts(List<ParameterEntry> entries)
        {
            var conflicts = new List<ConflictInfo>();
            var grouped = entries
                .Where(entry => !string.IsNullOrEmpty(entry.Name))
                .GroupBy(entry => entry.Name);

            foreach (var group in grouped)
            {
                var types = group.Select(entry => entry.Type).Where(type => type != ParameterType.Unknown).Distinct().ToList();
                if (types.Count <= 1) continue;

                var entriesList = group.ToList();
                var target = ResolveTargetType(entriesList);
                var observed = string.Join(", ", types);
                var summary = string.Join(" / ", entriesList.Select(entry => entry.SourceDetail).Distinct());
                conflicts.Add(new ConflictInfo
                {
                    Name = group.Key,
                    TargetType = target,
                    Entries = entriesList,
                    SourceSummary = summary,
                    HasExpressionEntry = entriesList.Any(entry => entry.IsExpressionParameter),
                    ObservedTypes = $" (Observed: {observed})"
                });
            }

            return conflicts;
        }

        private static ParameterType ResolveTargetType(List<ParameterEntry> entries)
        {
            var expressionType = entries.FirstOrDefault(e => e.IsExpressionParameter && e.Type != ParameterType.Unknown)?.Type;
            if (expressionType.HasValue) return expressionType.Value;

            var animatorType = entries.FirstOrDefault(e => e.IsAnimatorParameter && e.Type != ParameterType.Unknown)?.Type;
            if (animatorType.HasValue) return animatorType.Value;

            var maType = entries.FirstOrDefault(e => !e.IsExpressionParameter && !e.IsAnimatorParameter && e.Type != ParameterType.Unknown)?.Type;
            if (maType.HasValue) return maType.Value;

            return ParameterType.Unknown;
        }

        private static IEnumerable<AnimatorController> CollectControllersFromDescriptor(GameObject avatarRoot)
        {
            var controllers = new HashSet<AnimatorController>();
            var descriptorType = FindType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor, VRCSDK3A")
                ?? FindType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor");
            if (descriptorType == null) return controllers;

            var descriptor = avatarRoot.GetComponent(descriptorType);
            if (descriptor == null) return controllers;

            var descriptorSO = new SerializedObject(descriptor);
            var baseLayers = descriptorSO.FindProperty("baseAnimationLayers");
            var specialLayers = descriptorSO.FindProperty("specialAnimationLayers");

            AddControllersFromLayerArray(baseLayers, controllers);
            AddControllersFromLayerArray(specialLayers, controllers);
            return controllers;
        }

        private static void AddControllersFromLayerArray(SerializedProperty layersArray, HashSet<AnimatorController> controllers)
        {
            if (layersArray == null || !layersArray.isArray) return;
            for (int i = 0; i < layersArray.arraySize; i++)
            {
                var layer = layersArray.GetArrayElementAtIndex(i);
                if (layer == null) continue;
                var controllerProp = layer.FindPropertyRelative("animatorController");
                if (controllerProp == null) continue;
                if (controllerProp.objectReferenceValue is AnimatorController controller)
                {
                    controllers.Add(controller);
                }
            }
        }
    }

    private static class ParameterTypeMapper
    {
        public static ParameterType FromEnumProperty(SerializedProperty enumProperty)
        {
            if (enumProperty == null || enumProperty.propertyType != SerializedPropertyType.Enum) return ParameterType.Unknown;
            string name = enumProperty.enumNames[enumProperty.enumValueIndex];
            return FromEnumName(name);
        }

        public static ParameterType FromEnumName(string name)
        {
            switch (name)
            {
                case "Bool":
                    return ParameterType.Bool;
                case "Int":
                    return ParameterType.Int;
                case "Float":
                    return ParameterType.Float;
                default:
                    return ParameterType.Unknown;
            }
        }

        public static ParameterType FromAnimatorType(AnimatorControllerParameterType type)
        {
            switch (type)
            {
                case AnimatorControllerParameterType.Bool:
                    return ParameterType.Bool;
                case AnimatorControllerParameterType.Int:
                    return ParameterType.Int;
                case AnimatorControllerParameterType.Float:
                    return ParameterType.Float;
                default:
                    return ParameterType.Unknown;
            }
        }
    }

    private static class ParameterFixer
    {
        public static void ApplyEmptyNameFixes(List<EmptyEntryInfo> emptyEntries, List<FixAction> applied)
        {
            foreach (var empty in emptyEntries)
            {
                if (empty.Entry == null || empty.Entry.Element == null) continue;
                if (empty.Entry.IsAnimatorParameter)
                {
                    var controller = empty.Entry.AnimatorController;
                    if (controller == null) continue;
                    var param = controller.parameters.FirstOrDefault(p => p.name == empty.Entry.Name);
                    if (param != null)
                    {
                        Undo.RecordObject(controller, "Remove empty animator parameter");
                        controller.RemoveParameter(param);
                        EditorUtility.SetDirty(controller);
                        applied.Add(new FixAction
                        {
                            Title = "空欄パラメータを削除",
                            Detail = "AnimatorController から削除",
                            TargetPath = AssetDatabase.GetAssetPath(controller),
                            Applied = true
                        });
                    }
                    continue;
                }

                var owner = empty.Entry.SerializedOwner;
                if (owner == null) continue;
                Undo.RecordObject(empty.Entry.Owner, "Remove empty parameter");
                var list = owner.FindProperty("parameters");
                if (list != null && list.isArray && empty.Entry.Index >= 0 && empty.Entry.Index < list.arraySize)
                {
                    list.DeleteArrayElementAtIndex(empty.Entry.Index);
                    owner.ApplyModifiedProperties();
                    EditorUtility.SetDirty(empty.Entry.Owner);
                    applied.Add(new FixAction
                    {
                        Title = "空欄パラメータを削除",
                        Detail = empty.Detail,
                        TargetPath = empty.SourcePath,
                        Applied = true
                    });
                }
            }
        }

        public static void ApplyConflicts(List<ConflictInfo> conflicts, List<FixAction> applied, bool allowExpressionEdits)
        {
            foreach (var conflict in conflicts)
            {
                foreach (var entry in conflict.Entries)
                {
                    if (entry.Type == conflict.TargetType) continue;

                    if (entry.IsAnimatorParameter)
                    {
                        ApplyAnimatorParameterType(entry, conflict.TargetType, applied);
                        continue;
                    }

                    if (entry.IsExpressionParameter && !allowExpressionEdits)
                    {
                        applied.Add(new FixAction
                        {
                            Title = "ExpressionParameters は未修正",
                            Detail = $"{entry.Name} ({entry.Type}) を {conflict.TargetType} に変更するには Allow ExpressionParameters edits が必要です。",
                            TargetPath = entry.SourcePath,
                            Applied = false
                        });
                        continue;
                    }

                    ApplySerializedParameterType(entry, conflict.TargetType, applied);
                }
            }
        }

        private static void ApplyAnimatorParameterType(ParameterEntry entry, ParameterType target, List<FixAction> applied)
        {
            var controller = entry.AnimatorController;
            if (controller == null) return;
            var param = controller.parameters.FirstOrDefault(p => p.name == entry.Name);
            if (param == null) return;

            Undo.RecordObject(controller, "Fix Animator Parameter Type");
            controller.RemoveParameter(param);
            controller.AddParameter(entry.Name, ToAnimatorType(target));
            EditorUtility.SetDirty(controller);
            applied.Add(new FixAction
            {
                Title = "Animator Parameter 型修正",
                Detail = $"{entry.Name} -> {target}",
                TargetPath = AssetDatabase.GetAssetPath(controller),
                Applied = true
            });
        }

        private static void ApplySerializedParameterType(ParameterEntry entry, ParameterType target, List<FixAction> applied)
        {
            if (entry.SerializedOwner == null || entry.Element == null) return;

            bool changed = false;
            var valueType = entry.Element.FindPropertyRelative("valueType");
            var syncType = entry.Element.FindPropertyRelative("syncType");
            if (SetEnum(valueType, target)) changed = true;
            if (SetEnum(syncType, target)) changed = true;

            if (!changed) return;

            Undo.RecordObject(entry.Owner, "Fix Parameter Type");
            entry.SerializedOwner.ApplyModifiedProperties();
            EditorUtility.SetDirty(entry.Owner);
            applied.Add(new FixAction
            {
                Title = "MA/Expression Parameters 型修正",
                Detail = $"{entry.Name} -> {target}",
                TargetPath = entry.SourcePath,
                Applied = true
            });
        }

        private static bool SetEnum(SerializedProperty enumProperty, ParameterType target)
        {
            if (enumProperty == null || enumProperty.propertyType != SerializedPropertyType.Enum) return false;
            var targetName = target.ToString();
            int index = Array.FindIndex(enumProperty.enumNames, name => name == targetName);
            if (index < 0 || enumProperty.enumValueIndex == index) return false;
            enumProperty.enumValueIndex = index;
            return true;
        }

        private static AnimatorControllerParameterType ToAnimatorType(ParameterType type)
        {
            switch (type)
            {
                case ParameterType.Bool:
                    return AnimatorControllerParameterType.Bool;
                case ParameterType.Int:
                    return AnimatorControllerParameterType.Int;
                case ParameterType.Float:
                    return AnimatorControllerParameterType.Float;
                default:
                    return AnimatorControllerParameterType.Bool;
            }
        }
    }

    private static class MenuInstallerScanner
    {
        private const string MenuInstallerTypeName = "nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller";

        public static List<Issue> Scan(GameObject avatarRoot)
        {
            var issues = new List<Issue>();
            var type = FindType(MenuInstallerTypeName);
            if (type == null) return issues;

            foreach (var component in avatarRoot.GetComponentsInChildren(type, true))
            {
                var so = new SerializedObject(component);
                var menuProp = FindMenuProperty(so);
                if (menuProp != null && menuProp.objectReferenceValue == null)
                {
                    issues.Add(new Issue
                    {
                        SeverityLevel = Severity.Error,
                        Title = "MA Menu Installer の参照先が未設定",
                        Cause = "Menu Installer のインストール先が null です。",
                        Impact = "MA-1200 の原因になり得ます。",
                        FixHint = "Avatar Descriptor の Expressions Menu を設定します。",
                        CanAutoFix = true,
                        SourcePath = component.name
                    });
                }
            }

            return issues;
        }

        public static List<FixAction> PlanFixes(GameObject avatarRoot)
        {
            var planned = new List<FixAction>();
            var type = FindType(MenuInstallerTypeName);
            if (type == null) return planned;

            var menuAsset = GetAvatarExpressionsMenu(avatarRoot);
            if (menuAsset == null) return planned;

            foreach (var component in avatarRoot.GetComponentsInChildren(type, true))
            {
                var so = new SerializedObject(component);
                var menuProp = FindMenuProperty(so);
                if (menuProp != null && menuProp.objectReferenceValue == null)
                {
                    planned.Add(new FixAction
                    {
                        Title = "MA Menu Installer の参照先を設定",
                        Detail = "Avatar Descriptor の Expressions Menu を設定します。",
                        TargetPath = component.name
                    });
                }
            }

            return planned;
        }

        public static void ApplyFixes(GameObject avatarRoot, List<FixAction> applied)
        {
            var type = FindType(MenuInstallerTypeName);
            if (type == null) return;

            var menuAsset = GetAvatarExpressionsMenu(avatarRoot);
            if (menuAsset == null) return;

            foreach (var component in avatarRoot.GetComponentsInChildren(type, true))
            {
                var so = new SerializedObject(component);
                var menuProp = FindMenuProperty(so);
                if (menuProp == null || menuProp.objectReferenceValue != null) continue;

                Undo.RecordObject(component, "Assign Menu Installer target");
                menuProp.objectReferenceValue = menuAsset;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(component);
                applied.Add(new FixAction
                {
                    Title = "MA Menu Installer の参照先を設定",
                    Detail = "Expressions Menu を指定",
                    TargetPath = component.name,
                    Applied = true
                });
            }
        }

        private static SerializedProperty FindMenuProperty(SerializedObject installerSO)
        {
            var candidates = new[] { "menu", "menuAsset", "menuToInstall" };
            foreach (var name in candidates)
            {
                var prop = installerSO.FindProperty(name);
                if (prop != null && prop.propertyType == SerializedPropertyType.ObjectReference)
                {
                    return prop;
                }
            }
            return null;
        }

        private static UnityEngine.Object GetAvatarExpressionsMenu(GameObject avatarRoot)
        {
            var descriptorType = FindType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor, VRCSDK3A")
                ?? FindType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor");
            if (descriptorType == null) return null;

            var descriptor = avatarRoot.GetComponent(descriptorType);
            if (descriptor == null) return null;

            var descriptorSO = new SerializedObject(descriptor);
            var menuProp = descriptorSO.FindProperty("expressionsMenu");
            return menuProp?.objectReferenceValue;
        }
    }

    private static string GetGameObjectPath(GameObject obj)
    {
        if (obj == null) return string.Empty;
        var path = obj.name;
        var current = obj.transform.parent;
        while (current != null)
        {
            path = $"{current.name}/{path}";
            current = current.parent;
        }
        return path;
    }

    private static Type FindType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return null;
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
