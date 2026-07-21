using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class GroundCheck : MonoBehaviour // 개별 바닥 체크
    {
        public Collider GroundCollider; // 바닥 콜라이더
        [Min(0.1f)] public float CastHeight = 2f; // 검사 시작 높이
        [Min(0.1f)] public float CastDistance = 8f; // 검사 거리
        public float GroundOffset; // 바닥 위 높이

        public bool IsGrounded { get; private set; } // 바닥 감지 여부
        public Vector3 GroundPoint { get; private set; } // 바닥 위치
        public Vector3 GroundNormal { get; private set; } = Vector3.up; // 바닥 방향

        public Vector3 Snap(Vector3 position) // 저장 높이 사용
        {
            return Snap(position, GroundOffset); // 공통 처리
        }

        public Vector3 Snap(Vector3 position, float offset) // 위치 바닥 보정
        {
            GroundOffset = offset; // 최근 높이 저장
            if (TryCheck(position, out Vector3 grounded))
            {
                return grounded + Vector3.up * GroundOffset; // 바닥 위 위치
            }

            IsGrounded = false; // 감지 실패
            GroundPoint = position; // 실패 기준점
            GroundNormal = Vector3.up; // 기본 방향
            position.y = GroundOffset; // 평면 fallback
            return position; // 보정 결과
        }

        public bool TryCheck(Vector3 position, out Vector3 point) // 바닥 검사
        {
            Collider groundCollider = ResolveGroundCollider(); // 바닥 참조
            Ray ray = new Ray(position + Vector3.up * CastHeight, Vector3.down); // 하향 검사
            float distance = CastHeight + CastDistance; // 총 검사 거리

            if (groundCollider != null && groundCollider.Raycast(ray, out RaycastHit hit, distance))
            {
                ApplyHit(hit); // 결과 저장
                point = hit.point; // 바닥점 반환
                return true; // 성공
            }

            point = Vector3.zero; // 실패값
            return false; // 실패
        }

        private Collider ResolveGroundCollider() // 바닥 참조 찾기
        {
            if (GroundCollider != null)
            {
                return GroundCollider; // 지정 콜라이더
            }

            GroundService service = GroundService.Active; // 월드 서비스
            GroundCollider = service != null ? service.GroundCollider : null; // 서비스 바닥
            return GroundCollider; // 결과 반환
        }

        private void ApplyHit(RaycastHit hit) // 검사 결과 저장
        {
            IsGrounded = true; // 감지 성공
            GroundPoint = hit.point; // 바닥 위치
            GroundNormal = hit.normal; // 바닥 방향
        }
    }
}
