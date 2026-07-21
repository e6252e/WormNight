using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamProject01.Gameplay
{
    public class QuarterViewCamera : MonoBehaviour // 쿼터뷰 카메라
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset;

        [Header("View")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float distance = 15f;
        [SerializeField] private float pitch = 55f;
        [SerializeField] private float yaw;
        [SerializeField, Range(1f, 179f)] private float normalFieldOfView = 60f; // 기본 FOV

        [Header("Target Blend")]
        [SerializeField] private float targetSwitchSharpness = 6f;
        [SerializeField] private float targetSwitchSnapDistance = 0.05f;
        [SerializeField] private float targetSwitchMaxDuration = 1.2f;

        [Header("Mouse Wheel Zoom")]
        [SerializeField] private bool enableMouseWheelZoom = true;
        [SerializeField] private float zoomSpeed = 1.25f;
        [SerializeField] private float minDistance = 6f;
        [SerializeField] private float maxDistance = 22f;
        [SerializeField] private float zoomSharpness = 18f;
        [SerializeField] private PlayerPickupInteractor pickupInteractor;

        [Header("Nexus View")]
        [SerializeField] private float nexusMaxDistance = 34f;
        [SerializeField, Range(1f, 179f)] private float nexusFieldOfView = 66f;
        [SerializeField] private float nexusSouthFocusOffset; // 넥서스 모드 남쪽 초점 보정
        [SerializeField] private float fieldOfViewSharpness = 8f;

        [Header("Manual Rotate")]
        [SerializeField] private Key rotateLeftKey = Key.Q;
        [SerializeField] private Key rotateRightKey = Key.E;
        [SerializeField] private float rotateSpeed = 90f;

        private Vector3 focusPosition; // 추적 위치
        private bool hasFocusPosition; // 추적 초기화
        private bool isBlendingTarget; // 타겟 전환 중
        private float targetSwitchElapsed; // 전환 시간
        private float targetDistance; // 목표 거리
        private bool nexusViewMode; // 넥서스 시점
        private float shakeDuration; // 남은 카메라 흔들림 시간
        private float shakeTotalDuration; // 흔들림 기준 시간
        private float shakeAmplitude; // 흔들림 세기
        private float shakeFrequency = 38f; // 흔들림 속도
        private float shakeSeed; // 흔들림 난수

        private void Awake()
        {
            if (viewCamera == null)
                viewCamera = GetComponent<Camera>(); // 표시 카메라

            normalFieldOfView = Mathf.Clamp(normalFieldOfView, 1f, 179f); // 인스펙터 FOV
            if (viewCamera != null)
                viewCamera.fieldOfView = normalFieldOfView; // 기본값 즉시 반영

            targetDistance = Mathf.Clamp(distance, minDistance, GetEffectiveMaxDistance()); // 줌 초기값

            if (pickupInteractor == null)
                pickupInteractor = FindFirstObjectByType<PlayerPickupInteractor>(); // 픽업 UI
        }

        private void OnValidate()
        {
            normalFieldOfView = Mathf.Clamp(normalFieldOfView, 1f, 179f); // 기본 FOV 제한
            nexusFieldOfView = Mathf.Clamp(nexusFieldOfView, 1f, 179f); // 넥서스 FOV 제한

            if (viewCamera == null)
                viewCamera = GetComponent<Camera>(); // 표시 카메라

            if (!Application.isPlaying && viewCamera != null)
                viewCamera.fieldOfView = normalFieldOfView; // 에디터 미리보기 동기화
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            UpdateYawInput();
            UpdateZoomInput();
            UpdateZoomDistance();
            UpdateFieldOfView();
            UpdateFocusPosition();
            ApplyCameraTransform();
        }

        public void SetTarget(Transform newTarget)
        {
            SetTarget(newTarget, false); // 부드럽게 전환
        }

        public void SetTarget(Transform newTarget, bool snap)
        {
            if (target == newTarget && !snap)
                return; // 변화 없음

            bool shouldBlend = hasFocusPosition && !snap && newTarget != null; // 전환 보간
            target = newTarget; // 추적 대상
            if (!shouldBlend)
            {
                hasFocusPosition = false; // 위치 재초기화
                isBlendingTarget = false; // 보간 해제
                return;
            }

            isBlendingTarget = true; // 부드러운 이동
            targetSwitchElapsed = 0f; // 시간 초기화
        }

        public void SetYaw(float newYaw)
        {
            yaw = newYaw; // yaw 고정
            hasFocusPosition = false; // 즉시 재정렬
        }

        public void SetNexusViewMode(bool enabled)
        {
            if (nexusViewMode != enabled && hasFocusPosition)
            {
                isBlendingTarget = true; // 넥서스 초점 보정 전환
                targetSwitchElapsed = 0f; // 전환 시간 초기화
            }

            nexusViewMode = enabled; // 넥서스 모드
            targetDistance = Mathf.Clamp(targetDistance, minDistance, GetEffectiveMaxDistance()); // 범위 보정
        }

        public void AddShake(float duration, float amplitude, float frequency = 38f) // 외부 연출용 카메라 흔들림
        {
            if (duration <= 0f || amplitude <= 0f)
            {
                return; // 흔들림 없음
            }

            shakeDuration = Mathf.Max(shakeDuration, duration); // 더 긴 흔들림 유지
            shakeTotalDuration = Mathf.Max(shakeTotalDuration, duration); // 감쇠 기준
            shakeAmplitude = Mathf.Max(shakeAmplitude, amplitude); // 더 강한 흔들림 유지
            shakeFrequency = Mathf.Max(1f, frequency); // 속도 보정
            shakeSeed = Random.value * 1000f; // 패턴 갱신
        }

        private void UpdateYawInput()
        {
            Keyboard keyboard = Keyboard.current; // 입력
            if (keyboard == null)
                return;

            float input = 0f;

            if (keyboard[rotateLeftKey].isPressed)
                input -= 1f;

            if (keyboard[rotateRightKey].isPressed)
                input += 1f;

            if (Mathf.Approximately(input, 0f))
                return;

            yaw += input * rotateSpeed * Time.deltaTime; // Q/E 회전
        }

        private void UpdateZoomInput()
        {
            if (!enableMouseWheelZoom || GameplayInputBlocker.IsGameplayInputBlocked)
                return;

            if (pickupInteractor == null)
                pickupInteractor = FindFirstObjectByType<PlayerPickupInteractor>(); // 픽업 UI

            if (pickupInteractor != null && pickupInteractor.HasActivePickupCandidates)
                return; // 픽업 선택 우선

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            float scroll = mouse.scroll.ReadValue().y; // 휠 입력
            if (Mathf.Abs(scroll) <= 0.01f)
                return;

            targetDistance = Mathf.Clamp(targetDistance - Mathf.Sign(scroll) * zoomSpeed, minDistance, GetEffectiveMaxDistance()); // 목표 줌
        }

        private void UpdateZoomDistance()
        {
            targetDistance = Mathf.Clamp(targetDistance, minDistance, GetEffectiveMaxDistance()); // 범위 보정
            float t = 1f - Mathf.Exp(-Mathf.Max(0f, zoomSharpness) * Time.deltaTime); // 보간값
            distance = Mathf.Lerp(distance, targetDistance, t); // 거리 보간
        }

        private void UpdateFieldOfView()
        {
            if (viewCamera == null)
                return; // 카메라 없음

            float targetFieldOfView = nexusViewMode ? Mathf.Max(normalFieldOfView, nexusFieldOfView) : normalFieldOfView; // 목표 FOV
            float t = 1f - Mathf.Exp(-Mathf.Max(0f, fieldOfViewSharpness) * Time.deltaTime); // 보간값
            viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, targetFieldOfView, t); // FOV 보간
        }

        private void UpdateFocusPosition()
        {
            Vector3 targetPosition = target.position + targetOffset + GetNexusFocusOffset(); // 목표 위치

            if (!hasFocusPosition)
            {
                focusPosition = targetPosition; // 즉시 이동
                hasFocusPosition = true; // 초기화 완료
                isBlendingTarget = false; // 보간 없음
                return;
            }

            if (!isBlendingTarget)
            {
                focusPosition = targetPosition; // 즉시 추적
                return;
            }

            float t = 1f - Mathf.Exp(-Mathf.Max(0f, targetSwitchSharpness) * Time.deltaTime); // 보간값
            focusPosition = Vector3.Lerp(focusPosition, targetPosition, t); // 전환 이동
            targetSwitchElapsed += Time.deltaTime; // 시간 누적

            float snapDistance = Mathf.Max(0f, targetSwitchSnapDistance); // 스냅 거리
            if ((focusPosition - targetPosition).sqrMagnitude <= snapDistance * snapDistance
                || targetSwitchElapsed >= Mathf.Max(0.05f, targetSwitchMaxDuration))
            {
                focusPosition = targetPosition; // 전환 완료
                isBlendingTarget = false; // 일반 추적
            }
        }

        private float GetEffectiveMaxDistance()
        {
            float normalMax = Mathf.Max(minDistance, maxDistance); // 기본 최대
            return nexusViewMode ? Mathf.Max(normalMax, nexusMaxDistance) : normalMax; // 넥서스 확장
        }

        private Vector3 GetNexusFocusOffset()
        {
            return nexusViewMode ? Vector3.back * nexusSouthFocusOffset : Vector3.zero; // 양수면 월드 남쪽(-Z)
        }

        private void ApplyCameraTransform()
        {
            Quaternion viewRotation = Quaternion.Euler(pitch, yaw, 0f); // 뷰 회전
            Vector3 cameraOffset = viewRotation * Vector3.back * Mathf.Max(0f, distance); // 카메라 offset
            Vector3 cameraPosition = focusPosition + cameraOffset; // 카메라 위치
            Vector3 shakeOffset = ConsumeShakeOffset(viewRotation); // 카메라 흔들림

            transform.position = cameraPosition + shakeOffset;
            transform.rotation = Quaternion.LookRotation(focusPosition - cameraPosition, Vector3.up); // 대상 바라봄
        }

        private Vector3 ConsumeShakeOffset(Quaternion viewRotation) // 현재 프레임 흔들림 오프셋
        {
            if (shakeDuration <= 0f || shakeAmplitude <= 0f)
            {
                return Vector3.zero; // 흔들림 없음
            }

            shakeDuration = Mathf.Max(0f, shakeDuration - Time.deltaTime); // 시간 감소
            float duration = Mathf.Max(0.01f, shakeTotalDuration); // 0 나눗셈 방지
            float fade = Mathf.Clamp01(shakeDuration / duration); // 남은 비율
            float time = Time.time * shakeFrequency + shakeSeed; // 샘플 시간
            float x = Mathf.PerlinNoise(time, shakeSeed) * 2f - 1f; // 좌우
            float y = Mathf.PerlinNoise(shakeSeed, time) * 2f - 1f; // 상하
            Vector3 right = viewRotation * Vector3.right; // 카메라 기준 오른쪽
            Vector3 up = viewRotation * Vector3.up; // 카메라 기준 위
            Vector3 offset = (right * x + up * y) * (shakeAmplitude * fade); // 감쇠 적용

            if (shakeDuration <= 0f)
            {
                shakeAmplitude = 0f; // 종료 정리
                shakeTotalDuration = 0f; // 종료 정리
            }

            return offset; // 최종 흔들림
        }
    }
}
