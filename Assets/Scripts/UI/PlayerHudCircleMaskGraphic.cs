using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PlayerHudCircleMaskGraphic : MaskableGraphic // HUD 프로필 원형 마스크
    {
        [SerializeField, Range(24, 192)] private int segments = 96; // 원 부드러움
        [SerializeField, Range(0.1f, 0.5f)] private float radiusRatio = 0.5f; // 짧은 변 기준 반지름
        [Header("Cutout")]
        [SerializeField] private bool cutoutEnabled = true; // 아래 배지 영역 컷
        [SerializeField] private Vector2 cutoutCenterOffset = new Vector2(-0.42f, -0.42f); // Rect 크기 기준 위치
        [SerializeField, Range(0.01f, 0.7f)] private float cutoutRadiusRatio = 0.32f; // 짧은 변 기준 반지름

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = GetPixelAdjustedRect(); // 현재 UI 크기
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * radiusRatio;
            int count = Mathf.Max(24, segments);

            if (TryPopulateCutoutMesh(vh, rect, center, radius, count))
            {
                return;
            }

            PopulateCircleMesh(vh, center, radius, count); // 컷이 없으면 원형
        }

        private void PopulateCircleMesh(VertexHelper vh, Vector2 center, float radius, int count)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = center;
            vh.AddVert(vertex);

            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * 2f * i / count;
                vertex.position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                vh.AddVert(vertex);
            }

            for (int i = 1; i <= count; i++)
            {
                int next = i == count ? 1 : i + 1;
                vh.AddTriangle(0, i, next);
            }
        }

        private bool TryPopulateCutoutMesh(VertexHelper vh, Rect rect, Vector2 center, float radius, int count)
        {
            if (!cutoutEnabled)
            {
                return false;
            }

            Vector2 cutoutCenter = center + new Vector2(rect.width * cutoutCenterOffset.x, rect.height * cutoutCenterOffset.y);
            float cutoutRadius = Mathf.Min(rect.width, rect.height) * cutoutRadiusRatio;
            if (!TryGetCircleIntersections(center, radius, cutoutCenter, cutoutRadius, out Vector2 first, out Vector2 second))
            {
                return false; // 외곽과 맞물릴 때만 깔끔한 컷 생성
            }

            List<Vector2> points = new List<Vector2>(count + 24);
            float firstMainAngle = AngleFrom(center, first);
            float secondMainAngle = AngleFrom(center, second);
            bool firstArcIsCutout = PointInsideCircle(PointOnArc(center, radius, firstMainAngle, secondMainAngle, false, 0.5f), cutoutCenter, cutoutRadius);

            float mainFrom = firstArcIsCutout ? secondMainAngle : firstMainAngle;
            float mainTo = firstArcIsCutout ? firstMainAngle : secondMainAngle;
            float mainDelta = DeltaAngle(mainFrom, mainTo, false);
            int mainSteps = Mathf.Max(8, Mathf.RoundToInt(count * mainDelta / (Mathf.PI * 2f)));
            AppendArc(points, center, radius, mainFrom, mainTo, mainSteps, false, true, true);

            Vector2 cutoutFromPoint = points[points.Count - 1];
            Vector2 cutoutToPoint = points[0];
            float cutoutFrom = AngleFrom(cutoutCenter, cutoutFromPoint);
            float cutoutTo = AngleFrom(cutoutCenter, cutoutToPoint);
            bool cutoutCcwInsideMain = PointInsideCircle(PointOnArc(cutoutCenter, cutoutRadius, cutoutFrom, cutoutTo, false, 0.5f), center, radius);
            bool cutoutClockwise = !cutoutCcwInsideMain;
            float cutoutDelta = DeltaAngle(cutoutFrom, cutoutTo, cutoutClockwise);
            int cutoutSteps = Mathf.Max(8, Mathf.RoundToInt(count * cutoutDelta / (Mathf.PI * 2f)));
            AppendArc(points, cutoutCenter, cutoutRadius, cutoutFrom, cutoutTo, cutoutSteps, cutoutClockwise, false, false);

            CleanupPoints(points);
            if (!PopulatePolygonMesh(vh, points))
            {
                return false;
            }

            return true;
        }

        private static bool TryGetCircleIntersections(Vector2 aCenter, float aRadius, Vector2 bCenter, float bRadius, out Vector2 first, out Vector2 second)
        {
            first = default;
            second = default;

            Vector2 between = bCenter - aCenter;
            float distance = between.magnitude;
            const float epsilon = 0.001f;
            if (distance <= epsilon || distance >= aRadius + bRadius - epsilon || distance <= Mathf.Abs(aRadius - bRadius) + epsilon)
            {
                return false;
            }

            Vector2 direction = between / distance;
            float along = (aRadius * aRadius - bRadius * bRadius + distance * distance) / (2f * distance);
            float heightSquared = aRadius * aRadius - along * along;
            if (heightSquared <= epsilon)
            {
                return false;
            }

            Vector2 basePoint = aCenter + direction * along;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            float height = Mathf.Sqrt(heightSquared);
            first = basePoint + perpendicular * height;
            second = basePoint - perpendicular * height;
            return true;
        }

        private static void AppendArc(List<Vector2> points, Vector2 center, float radius, float from, float to, int steps, bool clockwise, bool includeFirst, bool includeLast)
        {
            float delta = DeltaAngle(from, to, clockwise);
            int start = includeFirst ? 0 : 1;
            int end = includeLast ? steps : steps - 1;
            for (int i = start; i <= end; i++)
            {
                float angle = from + delta * i / steps;
                points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private bool PopulatePolygonMesh(VertexHelper vh, List<Vector2> points)
        {
            if (points.Count < 3)
            {
                return false;
            }

            if (SignedArea(points) < 0f)
            {
                points.Reverse(); // 귀 자르기 삼각분할은 반시계 기준
            }

            List<int> triangles = new List<int>((points.Count - 2) * 3);
            if (!Triangulate(points, triangles))
            {
                return false;
            }

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            for (int i = 0; i < points.Count; i++)
            {
                vertex.position = points[i];
                vh.AddVert(vertex);
            }

            for (int i = 0; i < triangles.Count; i += 3)
            {
                vh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
            }

            return true;
        }

        private static bool Triangulate(List<Vector2> points, List<int> triangles)
        {
            List<int> indices = new List<int>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                indices.Add(i);
            }

            int guard = points.Count * points.Count;
            while (indices.Count > 3 && guard-- > 0)
            {
                bool foundEar = false;
                for (int i = 0; i < indices.Count; i++)
                {
                    int previous = indices[(i - 1 + indices.Count) % indices.Count];
                    int current = indices[i];
                    int next = indices[(i + 1) % indices.Count];
                    if (!IsEar(points, indices, previous, current, next))
                    {
                        continue;
                    }

                    triangles.Add(previous);
                    triangles.Add(current);
                    triangles.Add(next);
                    indices.RemoveAt(i);
                    foundEar = true;
                    break;
                }

                if (!foundEar)
                {
                    return false;
                }
            }

            if (indices.Count == 3)
            {
                triangles.Add(indices[0]);
                triangles.Add(indices[1]);
                triangles.Add(indices[2]);
            }

            return true;
        }

        private static bool IsEar(List<Vector2> points, List<int> indices, int previous, int current, int next)
        {
            Vector2 a = points[previous];
            Vector2 b = points[current];
            Vector2 c = points[next];
            if (Cross(a, b, c) <= 0.0001f)
            {
                return false;
            }

            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];
                if (index == previous || index == current || index == next)
                {
                    continue;
                }

                if (PointInTriangle(points[index], a, b, c))
                {
                    return false;
                }
            }

            return true;
        }

        private static void CleanupPoints(List<Vector2> points)
        {
            const float epsilon = 0.0001f;
            for (int i = points.Count - 1; i > 0; i--)
            {
                if ((points[i] - points[i - 1]).sqrMagnitude <= epsilon)
                {
                    points.RemoveAt(i);
                }
            }

            if (points.Count > 1 && (points[0] - points[points.Count - 1]).sqrMagnitude <= epsilon)
            {
                points.RemoveAt(points.Count - 1);
            }
        }

        private static float AngleFrom(Vector2 center, Vector2 point)
        {
            Vector2 offset = point - center;
            return Mathf.Atan2(offset.y, offset.x);
        }

        private static float DeltaAngle(float from, float to, bool clockwise)
        {
            float delta = Mathf.Repeat(to - from, Mathf.PI * 2f);
            return clockwise ? delta - Mathf.PI * 2f : delta;
        }

        private static Vector2 PointOnArc(Vector2 center, float radius, float from, float to, bool clockwise, float t)
        {
            float angle = from + DeltaAngle(from, to, clockwise) * Mathf.Clamp01(t);
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private static bool PointInsideCircle(Vector2 point, Vector2 center, float radius)
        {
            return (point - center).sqrMagnitude <= radius * radius;
        }

        private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            return Cross(a, b, point) >= -0.0001f
                && Cross(b, c, point) >= -0.0001f
                && Cross(c, a, point) >= -0.0001f;
        }

        private static float SignedArea(List<Vector2> points)
        {
            float area = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Count];
                area += a.x * b.y - b.x * a.y;
            }

            return area * 0.5f;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            raycastTarget = false; // HUD 표시 전용
            SetVerticesDirty();
        }
#endif
    }
}
