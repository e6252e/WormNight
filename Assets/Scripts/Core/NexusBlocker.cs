using UnityEngine;

namespace TeamProject01.Gameplay
{
    [RequireComponent(typeof(SegmentBlocker))]
    public sealed class NexusBlocker : MonoBehaviour // 넥서스 몬스터 차단
    {
        public Collider RadiusSource; // 반경 기준 콜라이더
        public bool UseColliderBounds = true; // 콜라이더 크기 사용

        [Min(0.1f)] public float FallbackBlockRadius = 3f; // 수동 반경
        [Min(0f)] public float BlockPadding = 0.05f; // 표면 여유

        private SegmentBlocker segmentBlocker; // 실제 차단체
        private static readonly Color BlockFillColor = new Color(0.0f, 0.85f, 1.0f, 0.12f); // 채움색
        private static readonly Color BlockLineColor = new Color(0.0f, 0.95f, 1.0f, 0.9f); // 외곽선
        private static readonly Color SourceLineColor = new Color(1.0f, 0.85f, 0.15f, 0.8f); // 기준선

        private void Reset()
        {
            RadiusSource = GetComponent<Collider>(); // 기본 콜라이더
            RefreshBlocker(); // 즉시 반영
        }

        private void Awake()
        {
            RefreshBlocker(); // 런타임 반영
        }

        private void OnValidate()
        {
            RefreshBlocker(); // 에디터 반영
        }

        public void RefreshBlocker()
        {
            if (RadiusSource == null)
            {
                RadiusSource = GetComponent<Collider>(); // fallback 콜라이더
            }

            if (segmentBlocker == null)
            {
                segmentBlocker = GetComponent<SegmentBlocker>(); // 차단 컴포넌트
            }

            if (segmentBlocker == null)
            {
                return; // RequireComponent 보정 전
            }

            segmentBlocker.Configure(CalculateBlockRadius()); // 반경 적용
        }

        private float CalculateBlockRadius()
        {
            if (UseColliderBounds && RadiusSource != null)
            {
                Bounds bounds = RadiusSource.bounds; // 월드 기준
                float radius = Mathf.Max(bounds.extents.x, bounds.extents.z); // 평면 반경
                return Mathf.Max(0.1f, radius + BlockPadding); // 여유 포함
            }

            return Mathf.Max(0.1f, FallbackBlockRadius + BlockPadding); // 수동값
        }

        private void OnDrawGizmosSelected()
        {
            float blockRadius = CalculateBlockRadius(); // 차단 반경
            Vector3 center = GetGizmoCenter(); // 표시 중심
            float sourceRadius = GetSourceRadius(); // 기준 반경

            DrawDisc(center, blockRadius, BlockFillColor, true); // 차단 영역
            DrawDisc(center, blockRadius, BlockLineColor, false); // 차단 외곽

            if (sourceRadius > 0f && sourceRadius < blockRadius)
            {
                DrawDisc(center, sourceRadius, SourceLineColor, false); // 콜라이더 기준
            }

            Gizmos.color = BlockLineColor; // 중심 표시
            Gizmos.DrawSphere(center + Vector3.up * 0.03f, 0.08f); // 중심점
        }

        private Vector3 GetGizmoCenter()
        {
            if (UseColliderBounds && RadiusSource != null)
            {
                Bounds bounds = RadiusSource.bounds; // 월드 기준
                return new Vector3(bounds.center.x, transform.position.y, bounds.center.z); // 바닥 중심
            }

            return transform.position; // 수동 중심
        }

        private float GetSourceRadius()
        {
            if (!UseColliderBounds || RadiusSource == null)
            {
                return 0f; // 기준 없음
            }

            Bounds bounds = RadiusSource.bounds; // 월드 기준
            return Mathf.Max(bounds.extents.x, bounds.extents.z); // 평면 반경
        }

        private static void DrawDisc(Vector3 center, float radius, Color color, bool filled)
        {
            const int SegmentCount = 96; // 원 해상도
            Vector3 previous = center + new Vector3(radius, 0f, 0f); // 시작점
            Gizmos.color = color; // 색상

            for (int i = 1; i <= SegmentCount; i++)
            {
                float angle = Mathf.PI * 2f * i / SegmentCount; // 각도
                Vector3 current = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius); // 현재점

                if (filled)
                {
                    Gizmos.DrawLine(center, current); // 채움선
                }

                Gizmos.DrawLine(previous, current); // 외곽선
                previous = current; // 다음 기준
            }
        }
    }
}
