using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class WeaponEquipWizard : EditorWindow
{
    private enum HandSide { Right, Left }
    private enum SheathLocation { Hips, Spine }

    private GameObject _target;
    private HandSide _hand = HandSide.Right;
    private SheathLocation _sheath = SheathLocation.Hips;
    private float _blendTime = 0.2f;

    private float _drawOffsetDistance = 0.05f;
    private float _drawOffsetTime = 0.12f;

    private int _drawPointCount = 2;
    private float _drawPointSpacing = 0.05f;
    private float _segmentTime = 0.12f;

    private bool _previewMode = false;
    private float _previewT = 0f;

    private const string Prefix = "GX_";
    private const string WeaponRootName = Prefix + "WeaponRoot";
    private const string HandAnchorR = Prefix + "HandAnchor_R";
    private const string HandAnchorL = Prefix + "HandAnchor_L";
    private const string SheathAnchorName = Prefix + "SheathAnchor";
    private const string DrawDirectionName = Prefix + "DrawDirection";
    private const string DrawPointPrefix = Prefix + "DrawPoint_";
    private const int MaxDrawPoints = 10;

    [MenuItem("Tools/Weapon Equip Wizard")]
    public static void Open()
    {
        GetWindow<WeaponEquipWizard>("WeaponEquipWizard");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Weapon Equip Wizard", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            _target = Selection.activeGameObject;
            EditorGUILayout.ObjectField("Target", _target, typeof(GameObject), true);
        }

        _hand = (HandSide)EditorGUILayout.EnumPopup("Hand", _hand);
        _sheath = (SheathLocation)EditorGUILayout.EnumPopup("Sheath", _sheath);
        _blendTime = EditorGUILayout.FloatField("BlendTime", _blendTime);
        if (_blendTime < 0f) _blendTime = 0f;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Legacy Draw Offset", EditorStyles.boldLabel);
        _drawOffsetDistance = EditorGUILayout.FloatField("Draw Offset Distance", _drawOffsetDistance);
        _drawOffsetTime = EditorGUILayout.FloatField("Draw Offset Time", _drawOffsetTime);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Draw Points", EditorStyles.boldLabel);
        _drawPointCount = EditorGUILayout.IntSlider("Draw Point Count", _drawPointCount, 0, MaxDrawPoints);
        _drawPointSpacing = EditorGUILayout.FloatField("Draw Point Spacing", _drawPointSpacing);
        _segmentTime = EditorGUILayout.FloatField("Default Segment Time", _segmentTime);

        using (new EditorGUI.DisabledScope(_target == null))
        {
            if (GUILayout.Button("経由ポイント追加（現在の姿勢）"))
            {
                AddDrawPointFromCurrentPose();
            }
        }

        EditorGUILayout.Space(8);

        using (new EditorGUI.DisabledScope(_target == null))
        {
            if (GUILayout.Button("Create / Update"))
            {
                CreateOrUpdate();
            }

            if (GUILayout.Button("Select Anchors"))
            {
                SelectAnchors();
            }

            if (GUILayout.Button("Select Draw Points"))
            {
                SelectDrawPoints();
            }

            if (GUILayout.Button("Test Toggle ON/OFF"))
            {
                ToggleTest();
            }
        }

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Position Adjustment", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(_target == null))
        {
            if (GUILayout.Button("手持ち位置 調整モード"))
            {
                EnterAdjustMode(true);
            }

            if (GUILayout.Button("納刀位置 調整モード"))
            {
                EnterAdjustMode(false);
            }

            if (GUILayout.Button("調整モード終了"))
            {
                ExitAdjustMode();
            }
        }

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(_target == null))
        {
            _previewMode = EditorGUILayout.Toggle("Preview Mode", _previewMode);
            if (_previewMode)
            {
                _previewT = EditorGUILayout.Slider("Preview T", _previewT, 0f, 1f);
            }

            if (GUILayout.Button("Apply Preview"))
            {
                ApplyPreview(_previewMode, _previewT);
            }

            if (GUILayout.Button("Clear Preview"))
            {
                ApplyPreview(false, 0f);
            }
        }
    }

    private void CreateOrUpdate()
    {
        if (_target == null)
        {
            EditorUtility.DisplayDialog("Error", "Targetが選択されていません。", "OK");
            return;
        }

        var animator = _target.GetComponentInParent<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
        {
            EditorUtility.DisplayDialog("Error", "HumanoidのAnimatorが見つかりません。", "OK");
            return;
        }

        Transform handBone = _hand == HandSide.Right
            ? animator.GetBoneTransform(HumanBodyBones.RightHand)
            : animator.GetBoneTransform(HumanBodyBones.LeftHand);

        Transform sheathBone = _sheath == SheathLocation.Hips
            ? animator.GetBoneTransform(HumanBodyBones.Hips)
            : animator.GetBoneTransform(HumanBodyBones.Spine);

        if (handBone == null || sheathBone == null)
        {
            EditorUtility.DisplayDialog("Error", "必要なボーンが見つかりません。", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        // 1) WeaponRoot
        Transform weaponRoot = FindOrCreateChild(_target.transform, WeaponRootName);
        MoveRenderersIntoRoot(_target.transform, weaponRoot);

        // 2) HandAnchor
        string handAnchorName = _hand == HandSide.Right ? HandAnchorR : HandAnchorL;
        Transform handAnchor = FindOrCreateChild(handBone, handAnchorName);

        // 3) SheathAnchor
        Transform sheathAnchor = FindOrCreateChild(sheathBone, SheathAnchorName);

        // 4) Draw Direction + Draw Points
        Transform drawDirection = FindOrCreateChild(sheathBone, DrawDirectionName);
        List<Transform> drawPoints = _drawPointCount > 0
            ? FindOrCreateDrawPoints(sheathBone, drawDirection, Mathf.Min(_drawPointCount, MaxDrawPoints), _drawPointSpacing)
            : new List<Transform>();

        // 5) Follower
        var follower = weaponRoot.GetComponent<WeaponBladeFollower>();
        if (follower == null)
        {
            Undo.AddComponent<WeaponBladeFollower>(weaponRoot.gameObject);
            follower = weaponRoot.GetComponent<WeaponBladeFollower>();
        }

        follower.HandAnchor = handAnchor;
        follower.SheathAnchor = sheathAnchor;
        follower.DrawDirection = drawDirection;

        follower.DrawPoints = drawPoints.ToArray();
        follower.SegmentTimes = BuildSegmentTimes(follower.DrawPoints.Length + 1, _segmentTime, follower.SegmentTimes);
        follower.DefaultSegmentTime = _segmentTime;

        follower.BlendTime = _blendTime;
        follower.DrawOffsetDistance = _drawOffsetDistance;
        follower.DrawOffsetTime = _drawOffsetTime;

        EditorUtility.SetDirty(follower);
        Undo.CollapseUndoOperations(group);
    }

    private void AddDrawPointFromCurrentPose()
    {
        if (_target == null) return;

        Transform weaponRoot = _target.transform.Find(WeaponRootName);
        if (weaponRoot == null)
        {
            EditorUtility.DisplayDialog("Error", "WeaponRootが見つかりません。先にCreate / Updateを実行してください。", "OK");
            return;
        }

        var follower = weaponRoot.GetComponent<WeaponBladeFollower>();
        if (follower == null)
        {
            Undo.AddComponent<WeaponBladeFollower>(weaponRoot.gameObject);
            follower = weaponRoot.GetComponent<WeaponBladeFollower>();
        }

        Transform sheathAnchor = follower.SheathAnchor;
        if (sheathAnchor == null)
        {
            var animator = _target.GetComponentInParent<Animator>();
            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            {
                sheathAnchor = _sheath == SheathLocation.Hips
                    ? animator.GetBoneTransform(HumanBodyBones.Hips)
                    : animator.GetBoneTransform(HumanBodyBones.Spine);
            }
        }

        if (sheathAnchor == null)
        {
            EditorUtility.DisplayDialog("Error", "SheathAnchorが見つかりません。", "OK");
            return;
        }

        List<Transform> points = new List<Transform>();
        if (follower.DrawPoints != null)
        {
            points.AddRange(follower.DrawPoints.Where(p => p != null));
        }

        if (points.Count >= MaxDrawPoints)
        {
            EditorUtility.DisplayDialog("Info", $"経由ポイントは最大{MaxDrawPoints}個までです。", "OK");
            return;
        }

        int nextIndex = GetNextDrawPointIndex(points);
        string name = $"{DrawPointPrefix}{nextIndex:00}";

        Transform newPoint = sheathAnchor.Find(name);
        if (newPoint == null)
        {
            newPoint = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(newPoint.gameObject, "Create " + name);
        }

        newPoint.SetParent(sheathAnchor, true);
        newPoint.position = weaponRoot.position;
        newPoint.rotation = weaponRoot.rotation;

        points.Add(newPoint);

        Undo.RecordObject(follower, "Add Draw Point");
        follower.DrawPoints = points.ToArray();
        follower.SegmentTimes = BuildSegmentTimes(follower.DrawPoints.Length + 1, _segmentTime, follower.SegmentTimes);
        follower.DefaultSegmentTime = _segmentTime;
        if (follower.SheathAnchor == null) follower.SheathAnchor = sheathAnchor;

        EditorUtility.SetDirty(follower);
        Selection.activeObject = newPoint.gameObject;
        SceneView.RepaintAll();
    }

    private static int GetNextDrawPointIndex(List<Transform> points)
    {
        bool[] used = new bool[MaxDrawPoints + 1];
        foreach (var p in points)
        {
            if (p == null) continue;
            if (!p.name.StartsWith(DrawPointPrefix)) continue;

            string suffix = p.name.Substring(DrawPointPrefix.Length);
            if (int.TryParse(suffix, out int index))
            {
                if (index >= 1 && index <= MaxDrawPoints)
                {
                    used[index] = true;
                }
            }
        }

        for (int i = 1; i <= MaxDrawPoints; i++)
        {
            if (!used[i]) return i;
        }

        return MaxDrawPoints;
    }

    private void SelectAnchors()
    {
        if (_target == null) return;

        var animator = _target.GetComponentInParent<Animator>();
        if (animator == null) return;

        Transform handBone = _hand == HandSide.Right
            ? animator.GetBoneTransform(HumanBodyBones.RightHand)
            : animator.GetBoneTransform(HumanBodyBones.LeftHand);

        Transform sheathBone = _sheath == SheathLocation.Hips
            ? animator.GetBoneTransform(HumanBodyBones.Hips)
            : animator.GetBoneTransform(HumanBodyBones.Spine);

        if (handBone == null || sheathBone == null) return;

        string handAnchorName = _hand == HandSide.Right ? HandAnchorR : HandAnchorL;
        Transform handAnchor = handBone.Find(handAnchorName);
        Transform sheathAnchor = sheathBone.Find(SheathAnchorName);

        Selection.objects = new Object[]
        {
            handAnchor != null ? handAnchor.gameObject : null,
            sheathAnchor != null ? sheathAnchor.gameObject : null
        }.Where(o => o != null).ToArray();
    }

    private void SelectDrawPoints()
    {
        if (_target == null) return;

        Transform weaponRoot = _target.transform.Find(WeaponRootName);
        if (weaponRoot == null) return;

        var follower = weaponRoot.GetComponent<WeaponBladeFollower>();
        if (follower == null) return;

        List<Object> list = new List<Object>();
        if (follower.DrawDirection != null) list.Add(follower.DrawDirection.gameObject);

        if (follower.DrawPoints != null)
        {
            for (int i = 0; i < follower.DrawPoints.Length; i++)
            {
                if (follower.DrawPoints[i] != null)
                {
                    list.Add(follower.DrawPoints[i].gameObject);
                }
            }
        }

        if (list.Count > 0)
        {
            Selection.objects = list.ToArray();
        }
    }

    private void ToggleTest()
    {
        if (_target == null) return;

        Transform weaponRoot = _target.transform.Find(WeaponRootName);
        if (weaponRoot == null) return;

        var follower = weaponRoot.GetComponent<WeaponBladeFollower>();
        if (follower == null) return;

        follower.Toggle();
        EditorUtility.SetDirty(follower);
    }

    private void EnterAdjustMode(bool equip)
    {
        if (_target == null) return;

        Transform weaponRoot = _target.transform.Find(WeaponRootName);
        if (weaponRoot == null) return;

        var follower = weaponRoot.GetComponent<WeaponBladeFollower>();
        if (follower == null) return;

        Transform targetAnchor = equip ? follower.HandAnchor : follower.SheathAnchor;
        if (targetAnchor == null) return;

        Undo.RecordObject(follower, "Set Adjust Mode");
        follower.IsEquipped = equip;
        follower.PreviewMode = false;
        EditorUtility.SetDirty(follower);

        Selection.activeObject = targetAnchor.gameObject;
        SceneView.RepaintAll();
    }

    private void ExitAdjustMode()
    {
        if (_target == null) return;

        Selection.activeObject = _target;
        SceneView.RepaintAll();
    }

    private void ApplyPreview(bool enabled, float t)
    {
        if (_target == null) return;

        Transform weaponRoot = _target.transform.Find(WeaponRootName);
        if (weaponRoot == null) return;

        var follower = weaponRoot.GetComponent<WeaponBladeFollower>();
        if (follower == null) return;

        Undo.RecordObject(follower, "Apply Preview");
        follower.PreviewMode = enabled;
        follower.PreviewT = t;
        EditorUtility.SetDirty(follower);
        SceneView.RepaintAll();
    }

    private static Transform FindOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child;

        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static List<Transform> FindOrCreateDrawPoints(
        Transform sheathBone,
        Transform drawDirection,
        int count,
        float spacing)
    {
        List<Transform> list = new List<Transform>();

        Vector3 dirWorld = drawDirection != null ? drawDirection.forward : sheathBone.forward;
        Vector3 dirLocal = sheathBone.InverseTransformDirection(dirWorld).normalized;

        for (int i = 0; i < count; i++)
        {
            string name = $"{DrawPointPrefix}{i + 1:00}";
            Transform t = sheathBone.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                Undo.RegisterCreatedObjectUndo(t.gameObject, "Create " + name);
                t.SetParent(sheathBone, false);
                t.localPosition = dirLocal * spacing * (i + 1);
                t.localRotation = Quaternion.identity;
            }
            list.Add(t);
        }

        return list;
    }

    private static float[] BuildSegmentTimes(int segmentCount, float defaultTime, float[] existing)
    {
        if (segmentCount <= 0) return new float[0];

        float[] times = new float[segmentCount];

        if (existing != null && existing.Length == segmentCount)
        {
            for (int i = 0; i < segmentCount; i++)
            {
                times[i] = existing[i] > 0f ? existing[i] : defaultTime;
            }
            return times;
        }

        for (int i = 0; i < segmentCount; i++)
        {
            times[i] = defaultTime;
        }
        return times;
    }

    private static void MoveRenderersIntoRoot(Transform root, Transform weaponRoot)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.transform != weaponRoot)
            .ToArray();

        foreach (var r in renderers)
        {
            if (IsUnder(r.transform, weaponRoot)) continue;
            Undo.SetTransformParent(r.transform, weaponRoot, "Move Renderer to WeaponRoot");
        }
    }

    private static bool IsUnder(Transform t, Transform parent)
    {
        while (t != null)
        {
            if (t == parent) return true;
            t = t.parent;
        }
        return false;
    }
}