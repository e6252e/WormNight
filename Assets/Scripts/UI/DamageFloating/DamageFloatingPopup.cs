using System;
using TMPro;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    [RequireComponent(typeof(TextMeshPro))]
    public sealed class DamageFloatingPopup : MonoBehaviour // 월드 데미지 숫자
    {
        [SerializeField] private float lifetime = 1.15f; // 표시 시간
        [SerializeField] private float riseSpeed = 1.25f; // 상승 속도
        [SerializeField] private Vector3 randomOffsetRange = new Vector3(0.25f, 0.08f, 0.25f); // 겹침 방지
        [SerializeField] private float minCameraScaleMultiplier = 0.75f; // 최소 보정
        [SerializeField] private float maxCameraScaleMultiplier = 1.85f; // 최대 보정
        [SerializeField] private float referenceCameraDistance = 12f; // 기준 거리

        private TextMeshPro text; // TMP
        private Material runtimeStyleMaterial; // 팝업 전용 재질
        private Action<DamageFloatingPopup> onFinished; // 반환 콜백
        private Camera targetCamera; // 기준 카메라
        private Vector3 baseScale; // 기본 스케일
        private Color startColor; // 시작 색
        private float spawnTime; // 생성 시각

        private void Awake() // 초기화
        {
            ResolveText(); // TMP 확보
            baseScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale; // 스케일 보정
        }

        private void OnDestroy() // 재질 정리
        {
            if (runtimeStyleMaterial != null)
            {
                Destroy(runtimeStyleMaterial); // 런타임 재질 제거
                runtimeStyleMaterial = null; // 참조 해제
            }
        }

        private void Update() // 이동/페이드
        {
            float age = Time.time - spawnTime; // 경과 시간
            float normalized = lifetime > 0f ? Mathf.Clamp01(age / lifetime) : 1f; // 진행률

            transform.position += Vector3.up * riseSpeed * Time.deltaTime; // 위로 이동
            FaceCamera(); // 카메라 바라보기
            ApplyCameraScale(); // 거리 보정

            if (text != null)
            {
                Color color = startColor; // 색 복사
                color.a = Mathf.Lerp(startColor.a, 0f, normalized); // 알파 감소
                text.color = color; // 표시 반영
            }

            if (age >= lifetime)
            {
                gameObject.SetActive(false); // 비활성화
                onFinished?.Invoke(this); // 풀 반환
            }
        }

        public void Initialize(string displayText, Color color, Vector3 worldPosition, float fontSize, TMP_FontAsset fontAsset, Action<DamageFloatingPopup> finishedCallback) // 표시 시작
        {
            ResolveText(); // TMP 확보
            targetCamera = Camera.main; // 메인 카메라
            onFinished = finishedCallback; // 반환 콜백
            spawnTime = Time.time; // 시작 시간
            startColor = color; // 시작 색

            transform.position = worldPosition + new Vector3(
                UnityEngine.Random.Range(-randomOffsetRange.x, randomOffsetRange.x),
                UnityEngine.Random.Range(0f, randomOffsetRange.y),
                UnityEngine.Random.Range(-randomOffsetRange.z, randomOffsetRange.z)); // 표시 위치

            if (text != null)
            {
                ApplyFont(fontAsset); // 폰트 적용
                text.text = displayText; // 숫자
                text.fontSize = fontSize; // 크기
                text.alignment = TextAlignmentOptions.Center; // 중앙 정렬
                text.color = startColor; // 색 적용
                DamageFloatingTextStyle.Apply(text, ref runtimeStyleMaterial); // 그림자 적용
            }

            gameObject.SetActive(true); // 활성화
            FaceCamera(); // 즉시 방향 보정
            ApplyCameraScale(); // 즉시 크기 보정
        }

        private void ResolveText() // TMP 찾기
        {
            if (text == null)
            {
                text = GetComponent<TextMeshPro>(); // 같은 오브젝트
            }

            if (text != null)
            {
                text.raycastTarget = false; // UI 입력 방해 방지
            }
        }

        private void ApplyFont(TMP_FontAsset fontAsset) // 폰트 교체
        {
            if (text == null || fontAsset == null)
            {
                return; // 적용 대상 없음
            }

            if (text.font == fontAsset)
            {
                return; // 이미 적용됨
            }

            text.font = fontAsset; // 폰트 적용
            if (fontAsset.material != null)
            {
                text.fontSharedMaterial = fontAsset.material; // 기준 재질 적용
            }

            if (runtimeStyleMaterial != null)
            {
                Destroy(runtimeStyleMaterial); // 이전 폰트 재질 제거
                runtimeStyleMaterial = null; // 새 재질 생성 준비
            }
        }

        private void FaceCamera() // 빌보드
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main; // 카메라 재탐색
            }

            if (targetCamera == null)
            {
                return; // 카메라 없음
            }

            transform.rotation = Quaternion.LookRotation(transform.position - targetCamera.transform.position, Vector3.up); // 카메라 정면
        }

        private void ApplyCameraScale() // 거리 기반 크기 보정
        {
            if (targetCamera == null)
            {
                transform.localScale = baseScale; // 기본 크기
                return; // 보정 없음
            }

            float referenceDistance = Mathf.Max(0.01f, referenceCameraDistance); // 기준 거리
            float distance = Vector3.Distance(targetCamera.transform.position, transform.position); // 현재 거리
            float multiplier = Mathf.Clamp(distance / referenceDistance, minCameraScaleMultiplier, maxCameraScaleMultiplier); // 보정값
            transform.localScale = baseScale * multiplier; // 크기 적용
        }
    }
}
