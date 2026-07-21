using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class SegmentTrebuchetFireMotion : MonoBehaviour // SG03 투석기 숟가락 발사 모션
    {
        public Transform PitchPivot; // 위아래 회전축
        public bool UseMotion = true; // 모션 사용 여부
        public Vector3 LocalRotationAxis = Vector3.forward; // PitchPivot 로컬 회전축
        public float ReleaseAngle = -90f; // 돌을 놓는 각도
        public float FollowThroughAngle = -128f; // 발사 후 더 내려가는 각도
        [Min(0.01f)] public float WindupDuration = 0.42f; // 90도까지 점점 빨라지는 시간
        [Min(0.01f)] public float FollowThroughDuration = 0.13f; // 발사 후 더 빠르게 휘두르는 시간
        // 발사 후 끝까지 젖혀진 숟가락 자세를 눈에 보이게 유지하는 시간
        [Min(0f)] public float FollowThroughHoldDuration = 0.32f; // 끝각도 유지 시간
        [HideInInspector, Min(0f)] public float ReturnDelay = 0.04f; // 기존 프리팹 저장값 보존용
        [Min(0.01f)] public float ReturnDuration = 0.55f; // 기본 각도로 돌아오는 시간
        public Ease WindupEase = Ease.InCubic; // 점점 빨라지는 가속
        public Ease FollowThroughEase = Ease.InQuad; // 발사 후 추가 가속
        public Ease ReturnEase = Ease.OutBack; // 복귀 탄성

        [Header("Throw Sway")]
        // 돌을 던진 순간 세그먼트 비주얼이 던진 방향으로 쏠리는 연출 사용
        public bool UseThrowSway = true; // 투석기 전용 전방 흔들림
        // 던진 방향으로 밀리는 거리
        [Min(0f)] public float ThrowSwayDistance = 0.12f; // 0이면 위치 흔들림 없음
        // 던진 방향으로 기울어지는 각도
        [Min(0f)] public float ThrowSwayTiltAngle = 3.2f; // 0이면 회전 흔들림 없음
        // 던지는 쪽으로 확 쏠리는 시간
        [Min(0.01f)] public float ThrowSwayKickDuration = 0.07f; // 앞으로 쏠림
        // 원위치 쪽으로 되돌아오는 시간
        [Min(0.01f)] public float ThrowSwayReturnDuration = 0.18f; // 복귀
        // 원점 반대쪽으로 살짝 되받는 거리 비율
        [Min(0f)] public float ThrowSwaySettleDistanceRatio = 0.18f; // 마지막 흔들림 거리
        // 원점 반대쪽으로 살짝 되받는 회전 비율
        [Min(0f)] public float ThrowSwaySettleTiltRatio = 0.25f; // 마지막 흔들림 각도
        // 마지막으로 둔하게 자리 잡는 시간
        [Min(0.01f)] public float ThrowSwaySettleDuration = 0.16f; // 최종 정착

        private Quaternion baseLocalRotation; // 프리팹에서 맞춰둔 기본 회전
        private bool hasBasePose; // 기본 자세 저장 여부
        private Sequence motionSequence; // 현재 모션 시퀀스

        public bool IsPlaying => motionSequence != null && motionSequence.IsActive(); // 모션 재생 중
        public bool CanPlayMotion => UseMotion && ResolvePitchPivot() != null; // 모션 재생 가능 여부

        private void Awake()
        {
            CaptureBasePoseIfNeeded(); // 생성 시 현재 자세 저장
        }

        private void OnDisable()
        {
            StopMotion(true); // 비활성 시 중간 회전 복구
        }

        public void CaptureBasePoseIfNeeded() // 현재 프리팹 기준 자세를 기본값으로 저장
        {
            Transform pivot = ResolvePitchPivot(); // 회전축
            if (pivot == null || hasBasePose)
            {
                return; // 저장 불가/이미 저장됨
            }

            baseLocalRotation = pivot.localRotation; // 기본 회전 저장
            hasBasePose = true; // 저장 완료
        }

        public IEnumerator PlayReleaseMotion(Action onRelease, Action onFollowThroughComplete = null) // 90도 도달 시 발사 콜백 실행
        {
            Transform pivot = ResolvePitchPivot(); // 회전축
            if (pivot == null)
            {
                onRelease?.Invoke(); // 회전축 없으면 기존 발사만 수행
                yield break;
            }

            CaptureBasePoseIfNeeded(); // 기본 자세 보장
            StopMotion(false); // 이전 모션 정리

            motionSequence = DOTween.Sequence(); // 새 시퀀스
            motionSequence.Append(pivot.DOLocalRotateQuaternion(GetTargetRotation(ReleaseAngle), WindupDuration).SetEase(WindupEase)); // 90도까지 가속
            motionSequence.AppendCallback(() => onRelease?.Invoke()); // 이 순간 돌 발사
            motionSequence.Append(pivot.DOLocalRotateQuaternion(GetTargetRotation(FollowThroughAngle), FollowThroughDuration).SetEase(FollowThroughEase)); // 발사 후 더 휘두름
            motionSequence.AppendCallback(() => onFollowThroughComplete?.Invoke()); // 끝까지 휘두른 뒤 후속 흔들림 실행
            float followThroughHoldTime = Mathf.Max(0f, FollowThroughHoldDuration); // 신규 유지 시간만 사용
            if (followThroughHoldTime > 0f)
            {
                motionSequence.AppendInterval(followThroughHoldTime); // 끝각도 유지
            }

            motionSequence.Append(pivot.DOLocalRotateQuaternion(baseLocalRotation, ReturnDuration).SetEase(ReturnEase)); // 기본 각도로 복귀

            yield return motionSequence.WaitForCompletion(); // 모션 종료 대기
            pivot.localRotation = baseLocalRotation; // 미세 오차 보정
            motionSequence = null; // 참조 해제
        }

        public void StopMotion(bool restoreBasePose) // 모션 중단
        {
            if (motionSequence != null)
            {
                motionSequence.Kill(false); // 콜백 없이 중단
                motionSequence = null; // 참조 해제
            }

            if (!restoreBasePose)
            {
                return; // 복구 불필요
            }

            Transform pivot = ResolvePitchPivot(); // 회전축
            if (pivot != null && hasBasePose)
            {
                pivot.localRotation = baseLocalRotation; // 기본 자세 복구
            }
        }

        private Quaternion GetTargetRotation(float angle) // 기본 회전 기준 목표 각도
        {
            Vector3 axis = ResolveRotationAxis(); // SG03 투석기 회전축 보정
            return baseLocalRotation * Quaternion.AngleAxis(angle, axis); // 기본 자세 기준 회전
        }

        // 기존 SG03 프리팹에 저장된 X축 값을 런타임에서 Z축으로 보정
        private Vector3 ResolveRotationAxis()
        {
            Vector3 axis = LocalRotationAxis.sqrMagnitude > 0.0001f ? LocalRotationAxis.normalized : Vector3.forward; // 축 보정
            if (IsLegacyXAxis(axis))
            {
                return Vector3.forward; // 기존 X축 저장값을 Z축 회전으로 교체
            }

            return axis; // 수동 설정값 사용
        }

        // 기존 프리팹의 기본값 Vector3.right 판별
        private static bool IsLegacyXAxis(Vector3 axis)
        {
            return Vector3.Dot(axis.normalized, Vector3.right) > 0.999f; // 거의 X축이면 기존 저장값으로 간주
        }

        private Transform ResolvePitchPivot() // PitchPivot 자동 검색
        {
            if (PitchPivot != null)
            {
                return PitchPivot; // 수동 연결
            }

            PitchPivot = FindChildRecursive(transform, "PitchPivot"); // 세그먼트 안 검색
            return PitchPivot; // 결과
        }

        private static Transform FindChildRecursive(Transform root, string childName) // 이름 검색
        {
            if (root == null)
            {
                return null; // 검색 불가
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i); // 하위
                if (child.name == childName)
                {
                    return child; // 발견
                }

                Transform found = FindChildRecursive(child, childName); // 재귀
                if (found != null)
                {
                    return found; // 발견
                }
            }

            return null; // 없음
        }
    }
}
