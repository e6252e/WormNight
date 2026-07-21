using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class ConvoyController
    {
        private enum AutoOrbitState // 자동궤도 상태
        {
            Off,
            Approach,
            Drive
        }

        [Header("Auto Orbit")]
        public bool EnableAutoOrbit = true; // 자동궤도 사용
        [Min(0f)] public float AutoOrbitMinimumRadius = 9f; // 최소 궤도 반지름
        [Min(0f)] public float AutoOrbitNexusClearance = 5.5f; // 넥서스 여유 거리
        [Range(0, 8)] public int AutoOrbitExtraSegmentBuffer = 2; // 추가 세그먼트 여유
        [Min(0.1f)] public float AutoOrbitApproachTolerance = 0.75f; // 궤도 도착 허용
        [Min(0.1f)] public float AutoOrbitRepathThreshold = 0.75f; // 재진입 기준
        [Min(0.1f)] public float AutoOrbitCorrectionBand = 3f; // 반지름 보정 폭
        [Range(0f, 2f)] public float AutoOrbitRadialCorrectionWeight = 0.75f; // 안팎 보정 강도

        private AutoOrbitState autoOrbitState; // 현재 자동궤도 상태
        private Transform autoOrbitCenter; // 넥서스 중심
        private QuarterViewCamera autoOrbitCamera; // 전환 카메라
        private float autoOrbitRadius; // 목표 궤도 반지름
        private bool autoOrbitClockwise = true; // 궤도 방향

        public bool IsAutoOrbitActive => autoOrbitState != AutoOrbitState.Off; // HUD 선택 상태

        public void ToggleAutoOrbit() // 자동궤도 토글
        {
            if (IsAutoOrbitActive)
            {
                CancelAutoOrbit(); // 수동 해제
                return;
            }

            TryStartAutoOrbit(); // 자동 시작
        }

        public bool TryStartAutoOrbit() // 자동궤도 시작
        {
            if (!EnableAutoOrbit)
            {
                return false; // 기능 비활성
            }

            if (!TryResolveAutoOrbitCenter(out Transform center))
            {
                Debug.LogWarning("[Convoy] 자동궤도 실패: Nexus_Core를 찾을 수 없습니다.", this);
                return false;
            }

            autoOrbitCenter = center; // 중심 저장
            autoOrbitRadius = CalculateAutoOrbitRadius(center); // 최적 반지름
            autoOrbitState = AutoOrbitState.Approach; // 진입 시작
            SelectAutoOrbitDirection(center.position); // 시계/반시계 결정
            FocusAutoOrbitCamera(center, true); // 넥서스 시점
            return true;
        }

        public void CancelAutoOrbit() // 자동궤도 해제
        {
            CancelAutoOrbit(true); // 카메라 복귀
        }

        private void CancelAutoOrbit(bool restoreCamera) // 자동궤도 내부 해제
        {
            if (!IsAutoOrbitActive)
            {
                return; // 이미 꺼짐
            }

            autoOrbitState = AutoOrbitState.Off; // 상태 해제
            autoOrbitCenter = null; // 중심 해제
            currentTurnVelocity = 0f; // 자동 조향 관성 제거
            currentTurnInput = 0f; // 기울기 제거

            if (restoreCamera)
            {
                FocusAutoOrbitCamera(transform, false); // 플레이어 복귀
            }
        }

        private bool TryApplyAutoOrbitControl(float deltaTime) // 자동궤도 조향
        {
            if (!IsAutoOrbitActive)
            {
                return false; // 비활성
            }

            if (autoOrbitCenter == null && !TryResolveAutoOrbitCenter(out autoOrbitCenter))
            {
                CancelAutoOrbit(); // 중심 상실
                return false;
            }

            Vector3 desiredDirection = GetAutoOrbitDirection(autoOrbitCenter.position); // 목표 방향
            currentForwardSpeed = GetAutoForwardSpeed(); // 자동 전진
            ApplyDirectionSteer(desiredDirection, deltaTime); // 기존 조향 재사용
            return true;
        }

        private Vector3 GetAutoOrbitDirection(Vector3 centerPosition) // 자동궤도 목표 방향
        {
            Vector3 radial = GetAutoOrbitRadial(centerPosition); // 중심 -> 현재
            float distance = GetAutoOrbitDistance(centerPosition); // 현재 반지름
            float error = distance - autoOrbitRadius; // 반지름 오차

            if (autoOrbitState == AutoOrbitState.Approach)
            {
                Vector3 targetPoint = centerPosition + radial * autoOrbitRadius; // 가까운 궤도점
                Vector3 approach = targetPoint - transform.position; // 진입 방향
                approach.y = 0f; // 수평화
                if (Mathf.Abs(error) <= AutoOrbitApproachTolerance || approach.sqrMagnitude <= 0.01f)
                {
                    autoOrbitState = AutoOrbitState.Drive; // 궤도 주행 전환
                    return GetAutoOrbitDriveDirection(radial, error); // 접선 시작
                }

                return approach; // 궤도선까지 이동
            }

            if (Mathf.Abs(error) > AutoOrbitCorrectionBand * 2f)
            {
                autoOrbitState = AutoOrbitState.Approach; // 크게 벗어나면 재진입
            }

            return GetAutoOrbitDriveDirection(radial, error); // 궤도 주행
        }

        private Vector3 GetAutoOrbitDriveDirection(Vector3 radial, float radiusError) // 접선+반지름 보정
        {
            Vector3 tangent = autoOrbitClockwise
                ? new Vector3(radial.z, 0f, -radial.x)
                : new Vector3(-radial.z, 0f, radial.x); // 시계/반시계 접선
            float correction = Mathf.Clamp(radiusError / AutoOrbitCorrectionBand, -1f, 1f); // 오차 비율
            return tangent - radial * (correction * AutoOrbitRadialCorrectionWeight); // 안팎 보정
        }

        private void OnSegmentCountChangedForAutoOrbit() // 길이 변경 재계산
        {
            if (!IsAutoOrbitActive || autoOrbitCenter == null)
            {
                return; // 갱신 없음
            }

            float newRadius = CalculateAutoOrbitRadius(autoOrbitCenter); // 새 반지름
            if (Mathf.Abs(newRadius - autoOrbitRadius) < AutoOrbitRepathThreshold)
            {
                return; // 작은 변화 무시
            }

            autoOrbitRadius = newRadius; // 새 목표
            autoOrbitState = AutoOrbitState.Approach; // 새 궤도 진입
        }

        private bool HasAutoOrbitCancelInput(WormInput input) // 수동 취소 입력
        {
            return HasMoveInput(input.Move) || Mathf.Abs(input.Turn) > 0.01f; // WASD/스틱
        }

        private string GetAutoOrbitModeLabel() // HUD 표시명
        {
            if (autoOrbitState == AutoOrbitState.Approach)
            {
                return $"자동궤도 진입 R{autoOrbitRadius:0.0}"; // 진입 중
            }

            return $"자동궤도 R{autoOrbitRadius:0.0}"; // 궤도 주행
        }

        private float CalculateAutoOrbitRadius(Transform center) // 최적 반지름 계산
        {
            float spacing = Mathf.Max(0.1f, SegmentSpacing); // 기본 차간 거리
            float chainLength = segments.Count > 0 ? GetSegmentDistanceBehindHead(segments.Count - 1) : 0f; // 실제 꼬리 길이
            float bufferedLength = chainLength + spacing * (1 + Mathf.Max(0, AutoOrbitExtraSegmentBuffer)); // 머리+여유
            float tailSafeRadius = bufferedLength / (Mathf.PI * 2f); // 원둘레 기준
            float nexusSafeRadius = GetNexusHorizontalRadius(center) + Mathf.Max(0f, AutoOrbitNexusClearance); // 넥서스 여유
            return Mathf.Max(AutoOrbitMinimumRadius, tailSafeRadius, nexusSafeRadius); // 최종 반지름
        }

        private float GetNexusHorizontalRadius(Transform center) // 넥서스 반경 추정
        {
            if (center == null)
            {
                return 0f; // 중심 없음
            }

            float radius = 0f; // 최대 반경
            Collider[] colliders = center.GetComponentsInChildren<Collider>(true); // 하위 포함
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i]; // 후보
                if (collider == null)
                {
                    continue; // 누락
                }

                Vector3 extents = collider.bounds.extents; // 월드 반경
                radius = Mathf.Max(radius, extents.x, extents.z); // 수평 최대
            }

            return radius; // 결과
        }

        private float GetAutoOrbitDistance(Vector3 centerPosition) // 중심 거리
        {
            Vector3 offset = transform.position - centerPosition; // 중심 -> 현재
            offset.y = 0f; // 수평화
            return offset.magnitude; // 거리
        }

        private Vector3 GetAutoOrbitRadial(Vector3 centerPosition) // 중심 기준 방향
        {
            Vector3 radial = transform.position - centerPosition; // 중심 -> 현재
            radial.y = 0f; // 수평화
            if (radial.sqrMagnitude > 0.0001f)
            {
                return radial.normalized; // 현재 방향
            }

            Vector3 fallback = -transform.forward; // 중심 겹침 fallback
            fallback.y = 0f; // 수평화
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward; // 최종 방향
        }

        private void SelectAutoOrbitDirection(Vector3 centerPosition) // 궤도 방향 선택
        {
            Vector3 radial = GetAutoOrbitRadial(centerPosition); // 중심 방향
            Vector3 clockwise = new Vector3(radial.z, 0f, -radial.x); // 시계 접선
            Vector3 counterClockwise = -clockwise; // 반시계 접선
            Vector3 forward = transform.forward; // 현재 진행
            forward.y = 0f; // 수평화
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward; // fallback
            }

            autoOrbitClockwise = Vector3.Dot(forward.normalized, clockwise) >= Vector3.Dot(forward.normalized, counterClockwise); // 덜 꺾이는 방향
        }

        private bool TryResolveAutoOrbitCenter(out Transform center) // 넥서스 찾기
        {
            if (NexusController.Active != null)
            {
                center = NexusController.Active.transform; // 활성 넥서스
                return true;
            }

            GameObject nexusObject = GameObject.Find("Nexus_Core"); // 이름 fallback
            center = nexusObject != null ? nexusObject.transform : null; // 결과
            return center != null;
        }

        private void FocusAutoOrbitCamera(Transform target, bool nexusMode) // 카메라 타겟 전환
        {
            if (target == null)
            {
                return; // 대상 없음
            }

            if (autoOrbitCamera == null)
            {
                Camera mainCamera = Camera.main; // 메인 카메라
                autoOrbitCamera = mainCamera != null ? mainCamera.GetComponent<QuarterViewCamera>() : null; // 컴포넌트
            }

            if (autoOrbitCamera == null)
            {
                autoOrbitCamera = FindFirstObjectByType<QuarterViewCamera>(); // 씬 fallback
            }

            if (autoOrbitCamera != null)
            {
                autoOrbitCamera.SetNexusViewMode(nexusMode); // 줌/FOV 모드
                autoOrbitCamera.SetTarget(target); // 시점 변경
            }
        }
    }
}
