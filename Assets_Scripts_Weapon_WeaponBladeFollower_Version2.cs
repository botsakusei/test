using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class WeaponBladeFollower : MonoBehaviour
{
    public Transform HandAnchor;
    public Transform SheathAnchor;

    // 抜刀方向の基準（GX_DrawDirection）
    public Transform DrawDirection;

    // DrawPoints（GX_DrawPoint_01, 02...）
    public Transform[] DrawPoints;

    // 各区間の時間（DrawPoints+1個分）
    public float[] SegmentTimes;
    public float DefaultSegmentTime = 0.12f;

    public bool IsEquipped = true;
    public float BlendTime = 0.2f;

    // レガシー（DrawPoints未使用時のみ）
    public float DrawOffsetDistance = 0.05f;
    public float DrawOffsetTime = 0.12f;

    // プレビュー（Editモード用）
    public bool PreviewMode = false;
    [Range(0f, 1f)] public float PreviewT = 0f;

    private enum Transition { None, Drawing, Sheathing }
    private Transition _transition = Transition.None;
    private float _elapsed;

    private Transform _currentTarget;
    private Vector3 _velocityPos;
    private Quaternion _velocityRot;

    private readonly List<Transform> _pathPoints = new List<Transform>(8);
    private const int MaxDrawPoints = 10;

    public void Toggle()
    {
        IsEquipped = !IsEquipped;
        _elapsed = 0f;
        _transition = IsEquipped ? Transition.Drawing : Transition.Sheathing;
        _currentTarget = null;
    }

    private void OnEnable()
    {
        _currentTarget = null;
        _elapsed = 0f;
        _transition = Transition.None;
    }

    private void LateUpdate()
    {
        if (HandAnchor == null || SheathAnchor == null) return;

        bool hasDrawPoints = HasDrawPoints();

        if (!Application.isPlaying)
        {
            if (PreviewMode && hasDrawPoints)
            {
                if (TryEvaluatePathNormalized(PreviewT, true, out Vector3 p, out Quaternion r))
                {
                    transform.position = p;
                    transform.rotation = r;
                }
                return;
            }

            if (hasDrawPoints)
            {
                SnapTo(IsEquipped ? HandAnchor : SheathAnchor, Vector3.zero);
                return;
            }

            Vector3 drawDir = GetDrawDirection();
            if (IsEquipped)
            {
                SnapTo(HandAnchor, Vector3.zero);
            }
            else
            {
                SnapTo(SheathAnchor, -drawDir * DrawOffsetDistance);
            }
            return;
        }

        if (hasDrawPoints)
        {
            UpdateWithDrawPoints();
        }
        else
        {
            UpdateLegacy();
        }
    }

    private void UpdateWithDrawPoints()
    {
        if (_transition != Transition.None)
        {
            _elapsed += Time.deltaTime;
            float totalTime = GetTotalPathTime();

            if (_elapsed >= totalTime)
            {
                _elapsed = totalTime;
                _transition = Transition.None;
            }

            bool drawing = _transition == Transition.Drawing;
            if (TryEvaluatePathByTime(_elapsed, drawing, out Vector3 p, out Quaternion r))
            {
                transform.position = p;
                transform.rotation = r;
            }
            return;
        }

        Transform target = IsEquipped ? HandAnchor : SheathAnchor;
        if (_currentTarget != target)
        {
            _currentTarget = target;
            _velocityPos = Vector3.zero;
            _velocityRot = Quaternion.identity;
        }

        if (BlendTime <= 0f)
        {
            transform.position = target.position;
            transform.rotation = target.rotation;
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            target.position,
            ref _velocityPos,
            BlendTime
        );

        transform.rotation = SmoothDampRotation(
            transform.rotation,
            target.rotation,
            ref _velocityRot,
            BlendTime
        );
    }

    private void UpdateLegacy()
    {
        Vector3 drawDir = GetDrawDirection();

        if (_transition != Transition.None)
        {
            _elapsed += Time.deltaTime;

            if (_transition == Transition.Drawing &&
                _elapsed >= DrawOffsetTime + BlendTime)
            {
                _transition = Transition.None;
            }

            if (_transition == Transition.Sheathing &&
                _elapsed >= BlendTime + DrawOffsetTime)
            {
                _transition = Transition.None;
            }
        }

        Transform target;
        Vector3 offset = Vector3.zero;

        if (_transition == Transition.Drawing)
        {
            if (_elapsed < DrawOffsetTime)
            {
                target = SheathAnchor;
                offset = drawDir * DrawOffsetDistance;
            }
            else
            {
                target = HandAnchor;
            }
        }
        else if (_transition == Transition.Sheathing)
        {
            if (_elapsed < BlendTime)
            {
                target = SheathAnchor;
            }
            else
            {
                target = SheathAnchor;
                offset = -drawDir * DrawOffsetDistance;
            }
        }
        else
        {
            if (IsEquipped)
            {
                target = HandAnchor;
            }
            else
            {
                target = SheathAnchor;
                offset = -drawDir * DrawOffsetDistance;
            }
        }

        if (_currentTarget != target)
        {
            _currentTarget = target;
            _velocityPos = Vector3.zero;
            _velocityRot = Quaternion.identity;
        }

        if (BlendTime <= 0f)
        {
            transform.position = target.position + offset;
            transform.rotation = target.rotation;
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            target.position + offset,
            ref _velocityPos,
            BlendTime
        );

        transform.rotation = SmoothDampRotation(
            transform.rotation,
            target.rotation,
            ref _velocityRot,
            BlendTime
        );
    }

    private bool HasDrawPoints()
    {
        if (DrawPoints == null) return false;
        int count = 0;
        for (int i = 0; i < DrawPoints.Length; i++)
        {
            if (DrawPoints[i] != null)
            {
                count++;
                if (count >= 1) return true;
            }
        }
        return false;
    }

    private bool TryBuildPathPoints(List<Transform> path)
    {
        path.Clear();
        if (SheathAnchor == null || HandAnchor == null) return false;

        path.Add(SheathAnchor);

        if (DrawPoints != null)
        {
            int added = 0;
            for (int i = 0; i < DrawPoints.Length; i++)
            {
                if (DrawPoints[i] != null)
                {
                    path.Add(DrawPoints[i]);
                    added++;
                    if (added >= MaxDrawPoints) break;
                }
            }
        }

        path.Add(HandAnchor);
        return path.Count >= 2;
    }

    private float GetSegmentTime(int index, int segmentCount)
    {
        if (SegmentTimes != null && SegmentTimes.Length == segmentCount)
        {
            float t = SegmentTimes[index];
            if (t > 0f) return t;
        }
        return Mathf.Max(0.01f, DefaultSegmentTime);
    }

    private float GetTotalPathTime()
    {
        if (!TryBuildPathPoints(_pathPoints)) return 0f;

        int segmentCount = _pathPoints.Count - 1;
        float total = 0f;
        for (int i = 0; i < segmentCount; i++)
        {
            total += GetSegmentTime(i, segmentCount);
        }
        return total;
    }

    private bool TryEvaluatePathByTime(float time, bool drawing, out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero;
        rot = Quaternion.identity;

        if (!TryBuildPathPoints(_pathPoints)) return false;

        if (!drawing)
        {
            _pathPoints.Reverse();
        }

        int segmentCount = _pathPoints.Count - 1;
        if (segmentCount <= 0) return false;

        float total = 0f;
        for (int i = 0; i < segmentCount; i++)
        {
            total += GetSegmentTime(i, segmentCount);
        }

        float remaining = Mathf.Clamp(time, 0f, total);

        for (int i = 0; i < segmentCount; i++)
        {
            float segTime = GetSegmentTime(i, segmentCount);
            if (remaining > segTime)
            {
                remaining -= segTime;
                continue;
            }

            float t = segTime <= 0f ? 1f : remaining / segTime;

            Transform a = _pathPoints[i];
            Transform b = _pathPoints[i + 1];

            pos = Vector3.Lerp(a.position, b.position, t);
            rot = Quaternion.Slerp(a.rotation, b.rotation, t);
            return true;
        }

        Transform end = _pathPoints[_pathPoints.Count - 1];
        pos = end.position;
        rot = end.rotation;
        return true;
    }

    private bool TryEvaluatePathNormalized(float t, bool drawing, out Vector3 pos, out Quaternion rot)
    {
        float total = GetTotalPathTime();
        if (total <= 0f)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            return false;
        }
        return TryEvaluatePathByTime(total * Mathf.Clamp01(t), drawing, out pos, out rot);
    }

    private Vector3 GetDrawDirection()
    {
        if (DrawDirection != null)
        {
            return DrawDirection.forward.normalized;
        }

        Vector3 dir = (HandAnchor.position - SheathAnchor.position);
        if (dir.sqrMagnitude > 0.0001f) return dir.normalized;

        return transform.forward;
    }

    private void SnapTo(Transform target, Vector3 offset)
    {
        transform.position = target.position + offset;
        transform.rotation = target.rotation;
    }

    private static Quaternion SmoothDampRotation(
        Quaternion current,
        Quaternion target,
        ref Quaternion velocity,
        float time
    )
    {
        if (Time.deltaTime < Mathf.Epsilon) return current;

        float dot = Quaternion.Dot(current, target);
        float multi = dot > 0f ? 1f : -1f;

        target.x *= multi;
        target.y *= multi;
        target.z *= multi;
        target.w *= multi;

        Vector4 result = new Vector4(
            Mathf.SmoothDamp(current.x, target.x, ref velocity.x, time),
            Mathf.SmoothDamp(current.y, target.y, ref velocity.y, time),
            Mathf.SmoothDamp(current.z, target.z, ref velocity.z, time),
            Mathf.SmoothDamp(current.w, target.w, ref velocity.w, time)
        );

        Quaternion q = new Quaternion(result.x, result.y, result.z, result.w);
        return NormalizeQuaternion(q);
    }

    private static Quaternion NormalizeQuaternion(Quaternion q)
    {
        float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        if (mag > 0f)
        {
            float inv = 1f / mag;
            q.x *= inv; q.y *= inv; q.z *= inv; q.w *= inv;
        }
        return q;
    }
}