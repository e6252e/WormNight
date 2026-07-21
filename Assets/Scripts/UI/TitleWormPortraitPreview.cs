using System;
using UnityEngine;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    public sealed class TitleWormPortraitPreview : MonoBehaviour // 지렁이 선택 3D 초상화
    {
        [Header("Source")]
        public MetaProgressionManager Meta; // 선택 데이터

        [Header("Render")]
        public RawImage TargetImage; // 웜 UI 출력
        public RawImage StarterBodyTargetImage; // 스타터 UI 출력
        public Camera PortraitCamera; // 초상화 카메라
        public Camera StarterBodyPortraitCamera; // 스타터 초상화 카메라
        public Transform PreviewRoot; // 프리뷰 루트
        public Transform WormAnchor; // 지렁이 위치
        public Transform StarterBodyAnchor; // 스타터 바디 위치
        [Min(64)] public int TextureWidth = 768; // 렌더 텍스처 폭
        [Min(64)] public int TextureHeight = 512; // 렌더 텍스처 높이
        public bool MatchTargetImageAspect = true; // UI 칸 비율 맞춤
        [Range(1, 8)] public int RenderTextureAntiAliasing = 4; // 외곽 부드럽게
        public bool UseHdrRenderTexture; // 조명 표현 강화
        public Color CameraBackground = new Color(0.78f, 0.88f, 0.92f, 1f); // 단색 배경
        public Rect WormUvRect = new Rect(0f, 0f, 0.5f, 1f); // 웜 표시 영역
        public Rect StarterBodyUvRect = new Rect(0.5f, 0f, 0.5f, 1f); // 스타터 표시 영역
        [Range(0, 31)] public int WormPreviewLayer = 30; // 웜 전용 렌더 레이어
        [Range(0, 31)] public int StarterBodyPreviewLayer = 29; // 스타터 전용 렌더 레이어

        [Header("Layout")]
        public Vector3 CameraLocalPosition = new Vector3(0f, 1.2f, -6.2f); // 카메라 위치
        public Vector3 CameraLookAtLocalPosition = new Vector3(0f, 0.32f, 0.25f); // 시선 위치
        [Min(0.1f)] public float OrthographicSize = 1.4f; // 화면 크기
        public Vector3 WormLocalPosition = new Vector3(0f, -0.12f, 0f); // 앞쪽 캐릭터
        public Vector3 WormLocalEulerAngles = new Vector3(0f, 160f, 0f); // 캐릭터 각도
        [Min(0.1f)] public float WormTargetHeight = 2.28f; // 캐릭터 높이
        public Vector3 StarterBodyLocalPosition = new Vector3(0f, -0.18f, 0f); // 뒤쪽 바디
        public Vector3 StarterBodyLocalEulerAngles = new Vector3(0f, 145f, 0f); // 바디 각도
        [Min(0.1f)] public float StarterBodyTargetHeight = 1.42f; // 바디 높이
        [Range(0f, 180f)] public float IdleRotationSpeed = 8f; // 약한 회전

        [Header("Mouse Control")]
        [Min(0.1f)] public float MinOrthographicSize = 1.25f; // 최대 확대
        [Min(0.1f)] public float MaxOrthographicSize = 2.45f; // 최대 축소
        [Min(0f)] public float IdleResumeDelay = 1.25f; // 조작 후 자동 회전 대기

        [Header("Worm Prefabs")]
        public GameObject BasicWormPrefab; // 기본형
        public GameObject AttackWormPrefab; // 공격형
        public GameObject MobilityWormPrefab; // 이속형
        public GameObject SupportWormPrefab; // 지원형
        public GameObject MagicWormPrefab; // 마법형

        [Header("Starter Body Prefabs")]
        public GameObject BasicStarterBodyPrefab; // 기본 스타터
        public GameObject AttackStarterBodyPrefab; // 공격형 스타터
        public GameObject MobilityStarterBodyPrefab; // 이속형 스타터
        public GameObject SupportStarterBodyPrefab; // 지원형 스타터
        public GameObject MagicStarterBodyPrefab; // 마법형 스타터

        private RenderTexture renderTexture; // 웜 런타임 텍스처
        private RenderTexture starterBodyRenderTexture; // 스타터 런타임 텍스처
        private GameObject activeWorm; // 현재 지렁이
        private GameObject activeStarterBody; // 현재 스타터 바디
        private string activeWormId; // 현재 표시 ID
        private float manualYaw; // 사용자가 돌린 각도
        private float idleResumeTime; // 자동 회전 재개 시각

        private void Awake() // 초기화
        {
            ResolveReferences(); // 참조 보정
            EnsureRenderTexture(); // 출력 텍스처
            Refresh(); // 첫 표시
        }

        private void OnEnable() // 이벤트 연결
        {
            ResolveReferences(); // 재활성 보정
            if (Meta != null)
            {
                Meta.SelectedWormChanged += OnSelectedWormChanged; // 선택 변경
            }

            Refresh(); // 즉시 갱신
        }

        private void OnDisable() // 이벤트 해제
        {
            if (Meta != null)
            {
                Meta.SelectedWormChanged -= OnSelectedWormChanged; // 선택 변경 해제
            }
        }

        private void OnDestroy() // 텍스처 정리
        {
            if (PortraitCamera != null && PortraitCamera.targetTexture == renderTexture)
            {
                PortraitCamera.targetTexture = null; // 카메라 해제
            }

            if (TargetImage != null && TargetImage.texture == renderTexture)
            {
                TargetImage.texture = null; // UI 해제
            }

            if (StarterBodyTargetImage != null && StarterBodyTargetImage.texture == renderTexture)
            {
                StarterBodyTargetImage.texture = null; // 스타터 UI 해제
            }

            if (StarterBodyTargetImage != null && StarterBodyTargetImage.texture == starterBodyRenderTexture)
            {
                StarterBodyTargetImage.texture = null; // 스타터 UI 해제
            }

            if (renderTexture != null)
            {
                renderTexture.Release(); // GPU 해제
                Destroy(renderTexture); // 오브젝트 해제
                renderTexture = null;
            }

            if (starterBodyRenderTexture != null)
            {
                starterBodyRenderTexture.Release(); // GPU 해제
                Destroy(starterBodyRenderTexture); // 오브젝트 해제
                starterBodyRenderTexture = null;
            }
        }

        private void LateUpdate() // 약한 생동감
        {
            EnsureRenderTexture(); // UI 비율 변화 반영

            OrthographicSize = Mathf.Clamp(OrthographicSize, MinOrthographicSize, MaxOrthographicSize); // 줌 제한
            ApplyCamera(PortraitCamera, renderTexture, CameraLookAtLocalPosition, IndependentStarterOutput ? 1 << WormPreviewLayer : -1); // 웜 카메라
            ApplyCamera(StarterBodyPortraitCamera, starterBodyRenderTexture, CameraLookAtLocalPosition, 1 << StarterBodyPreviewLayer); // 스타터 카메라

            if (PreviewRoot != null)
            {
                if (IdleRotationSpeed > 0f && Time.unscaledTime >= idleResumeTime)
                {
                    manualYaw += IdleRotationSpeed * Time.unscaledDeltaTime; // 자동 회전
                }

                PreviewRoot.localRotation = Quaternion.Euler(0f, manualYaw, 0f); // 전체 프리뷰 회전
            }
        }

        public void Refresh() // 현재 선택 표시
        {
            ResolveReferences(); // 참조 보정
            EnsureRenderTexture(); // 출력 준비

            string wormId = Meta != null ? Meta.SelectedWormId : MetaWormIds.Basic; // 현재 선택
            ShowWorm(wormId); // 모델 갱신
        }

        public void PreviewWorm(string wormId) // 외부 미리보기
        {
            ShowWorm(wormId); // 선택 전 표시용
        }

        public void AddManualYaw(float deltaYaw) // 마우스 회전
        {
            manualYaw += deltaYaw; // 누적 회전
            PauseIdleMotion(); // 자동 회전 잠시 정지
        }

        public void ZoomBy(float deltaSize) // 마우스 휠 줌
        {
            OrthographicSize = Mathf.Clamp(OrthographicSize + deltaSize, MinOrthographicSize, MaxOrthographicSize); // 줌 반영
            PauseIdleMotion(); // 자동 회전 잠시 정지
        }

        public void PauseIdleMotion() // 조작 중 자동 회전 정지
        {
            idleResumeTime = Time.unscaledTime + IdleResumeDelay; // 재개 예약
        }

        private void OnSelectedWormChanged(string wormId) // 선택 변경 이벤트
        {
            ShowWorm(wormId); // 모델 교체
        }

        private void ShowWorm(string wormId) // 지렁이/스타터 표시
        {
            string normalized = MetaWormIds.Normalize(wormId); // ID 보정
            if (string.Equals(activeWormId, normalized, StringComparison.OrdinalIgnoreCase)
                && activeWorm != null
                && activeStarterBody != null)
            {
                return; // 이미 표시 중
            }

            ClearActiveModels(); // 기존 제거

            GameObject wormPrefab = ResolveWormPrefab(normalized); // 지렁이 프리팹
            GameObject starterPrefab = ResolveStarterBodyPrefab(normalized); // 스타터 바디 프리팹

            if (wormPrefab != null && WormAnchor != null)
            {
                activeWorm = Instantiate(wormPrefab, WormAnchor, false); // 지렁이 생성
                activeWorm.name = $"Portrait_{wormPrefab.name}"; // 이름
                SetupPreviewInstance(activeWorm, WormLocalPosition, WormLocalEulerAngles, WormTargetHeight); // 배치
                if (IndependentStarterOutput)
                {
                    SetLayerRecursively(activeWorm, WormPreviewLayer); // 웜 카메라 전용
                }
            }

            if (starterPrefab != null && StarterBodyAnchor != null)
            {
                activeStarterBody = Instantiate(starterPrefab, StarterBodyAnchor, false); // 스타터 바디 생성
                activeStarterBody.name = $"Portrait_{starterPrefab.name}"; // 이름
                SetupPreviewInstance(activeStarterBody, StarterBodyLocalPosition, StarterBodyLocalEulerAngles, StarterBodyTargetHeight); // 배치
                if (IndependentStarterOutput)
                {
                    SetLayerRecursively(activeStarterBody, StarterBodyPreviewLayer); // 스타터 카메라 전용
                }
            }

            activeWormId = normalized; // 상태 저장
        }

        private void ResolveReferences() // 기본 참조 찾기
        {
            if (Meta == null)
            {
                Meta = FindFirstObjectByType<MetaProgressionManager>(); // 씬 메타
            }
        }

        private void EnsureRenderTexture() // 렌더 텍스처 준비
        {
            if (PortraitCamera == null || TargetImage == null)
            {
                return; // 필수 참조 없음
            }

            renderTexture = EnsureRenderTextureFor(TargetImage, renderTexture, "RT_TitleWormPortrait"); // 웜 텍스처
            ApplyOutputImage(TargetImage, renderTexture, IndependentStarterOutput ? new Rect(0f, 0f, 1f, 1f) : WormUvRect); // 웜 UI 연결

            if (IndependentStarterOutput)
            {
                starterBodyRenderTexture = EnsureRenderTextureFor(StarterBodyTargetImage, starterBodyRenderTexture, "RT_TitleStarterBodyPortrait"); // 스타터 텍스처
                ApplyOutputImage(StarterBodyTargetImage, starterBodyRenderTexture, new Rect(0f, 0f, 1f, 1f)); // 스타터 UI 연결
            }
            else
            {
                ApplyOutputImage(StarterBodyTargetImage, renderTexture, StarterBodyUvRect); // 구형 분할 출력
            }
        }

        private RenderTexture EnsureRenderTextureFor(RawImage image, RenderTexture current, string textureName) // 렌더 텍스처 준비
        {
            if (image == null)
            {
                return current; // 대상 없음
            }

            int width = Mathf.Max(64, TextureWidth); // 폭 보정
            int height = Mathf.Max(64, TextureHeight); // 높이 보정
            if (MatchTargetImageAspect && TryGetImageAspect(image, out float targetAspect))
            {
                height = Mathf.Max(64, Mathf.RoundToInt(width / targetAspect)); // UI 비율 맞춤
            }

            int antiAliasing = Mathf.ClosestPowerOfTwo(Mathf.Clamp(RenderTextureAntiAliasing, 1, 8)); // 샘플 보정
            RenderTextureFormat colorFormat = UseHdrRenderTexture ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.ARGB32; // 색 포맷
            if (current == null || current.width != width || current.height != height || current.antiAliasing != antiAliasing || current.format != colorFormat)
            {
                if (current != null)
                {
                    current.Release(); // 기존 해제
                    Destroy(current);
                }

                current = new RenderTexture(width, height, 24, colorFormat)
                {
                    name = textureName,
                    useMipMap = false,
                    autoGenerateMips = false,
                    antiAliasing = antiAliasing
                };
                current.Create(); // 생성
            }

            return current; // 결과
        }

        private bool TryGetImageAspect(RawImage image, out float aspect) // UI 출력 비율
        {
            aspect = 0f; // 기본값
            RectTransform rectTransform = image != null ? image.rectTransform : null; // UI Rect
            if (rectTransform == null)
            {
                return false; // 없음
            }

            Rect rect = rectTransform.rect; // 현재 크기
            float width = rect.width; // 출력 폭
            float height = rect.height; // 출력 높이
            if (width <= 1f || height <= 1f)
            {
                return false; // 아직 레이아웃 전
            }

            aspect = Mathf.Clamp(width / height, 0.25f, 4f); // 비율 제한
            return true; // 성공
        }

        private bool IndependentStarterOutput => StarterBodyTargetImage != null && StarterBodyPortraitCamera != null; // 독립 출력 여부

        private void ApplyOutputImage(RawImage image, RenderTexture texture, Rect uvRect) // UI 출력 연결
        {
            if (image == null)
            {
                return; // 대상 없음
            }

            image.texture = texture; // 텍스처 연결
            image.uvRect = uvRect; // 좌우 분할
        }

        private void ApplyCamera(Camera camera, RenderTexture texture, Vector3 focusLocalPosition, int cullingMask) // 카메라 설정
        {
            if (camera == null || PreviewRoot == null || texture == null)
            {
                return; // 대상 없음
            }

            camera.transform.localPosition = CameraLocalPosition + new Vector3(focusLocalPosition.x, 0f, focusLocalPosition.z); // 대상 앞 배치
            camera.orthographic = true; // UI용
            camera.allowHDR = UseHdrRenderTexture; // 밝은 조명
            camera.allowMSAA = RenderTextureAntiAliasing > 1; // 외곽 보정
            camera.orthographicSize = OrthographicSize; // 크기 반영
            camera.backgroundColor = CameraBackground; // 배경 반영
            camera.clearFlags = CameraClearFlags.SolidColor; // 배경 고정
            camera.targetTexture = texture; // 출력 연결
            if (cullingMask >= 0)
            {
                camera.cullingMask = cullingMask; // 전용 레이어만 출력
            }

            camera.transform.LookAt(PreviewRoot.TransformPoint(focusLocalPosition)); // 대상 고정
        }

        private void SetupPreviewInstance(GameObject instance, Vector3 localPosition, Vector3 localEuler, float targetHeight) // 프리뷰 배치
        {
            if (instance == null)
            {
                return; // 대상 없음
            }

            instance.transform.localPosition = localPosition; // 위치
            instance.transform.localRotation = Quaternion.Euler(localEuler); // 회전
            instance.transform.localScale = Vector3.one; // 초기화
            DisableGameplayComponents(instance); // 프리뷰 전용
            FitToHeight(instance, targetHeight); // 크기 정규화
        }

        private void FitToHeight(GameObject instance, float targetHeight) // 모델 높이 맞춤
        {
            Bounds bounds;
            if (!TryGetRendererBounds(instance, out bounds) || bounds.size.y <= 0.0001f)
            {
                return; // 렌더러 없음
            }

            float scale = Mathf.Max(0.01f, targetHeight) / bounds.size.y; // 목표 높이
            instance.transform.localScale *= scale; // 스케일 적용

            if (!TryGetRendererBounds(instance, out bounds))
            {
                return; // 재계산 실패
            }

            Vector3 offset = instance.transform.position - bounds.center; // 중심 보정
            instance.transform.position += offset; // 앵커 중심으로 이동
        }

        private static bool TryGetRendererBounds(GameObject root, out Bounds bounds) // 렌더러 bounds
        {
            Renderer[] renderers = root != null ? root.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>(); // 렌더러
            bool hasBounds = false; // 유효 여부
            bounds = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue; // 없음
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds; // 첫 bounds
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds); // 합치기
            }

            return hasBounds; // 결과
        }

        private static void DisableGameplayComponents(GameObject root) // 게임 로직 비활성
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true); // 콜라이더
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false; // UI 프리뷰
            }

            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true); // 물리
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                rigidbodies[i].isKinematic = true; // 물리 정지
                rigidbodies[i].detectCollisions = false; // 충돌 차단
            }

            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true); // 런타임 스크립트
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null)
                {
                    behaviours[i].enabled = false; // 초상화 전용
                }
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer) // 렌더 레이어 적용
        {
            if (root == null)
            {
                return; // 대상 없음
            }

            root.layer = Mathf.Clamp(layer, 0, 31); // 현재 오브젝트
            Transform rootTransform = root.transform; // 루트
            for (int i = 0; i < rootTransform.childCount; i++)
            {
                SetLayerRecursively(rootTransform.GetChild(i).gameObject, layer); // 자식
            }
        }

        private void ClearActiveModels() // 기존 모델 제거
        {
            if (activeWorm != null)
            {
                Destroy(activeWorm); // 지렁이 제거
                activeWorm = null;
            }

            if (activeStarterBody != null)
            {
                Destroy(activeStarterBody); // 스타터 제거
                activeStarterBody = null;
            }
        }

        private GameObject ResolveWormPrefab(string wormId) // 지렁이 프리팹
        {
            switch (MetaWormIds.Normalize(wormId))
            {
                case MetaWormIds.Attack:
                    return AttackWormPrefab != null ? AttackWormPrefab : BasicWormPrefab; // 공격형
                case MetaWormIds.Mobility:
                    return MobilityWormPrefab != null ? MobilityWormPrefab : BasicWormPrefab; // 이속형
                case MetaWormIds.Support:
                    return SupportWormPrefab != null ? SupportWormPrefab : BasicWormPrefab; // 지원형
                case MetaWormIds.Magic:
                    return MagicWormPrefab != null ? MagicWormPrefab : BasicWormPrefab; // 마법형
                default:
                    return BasicWormPrefab; // 기본형
            }
        }

        private GameObject ResolveStarterBodyPrefab(string wormId) // 스타터 바디 프리팹
        {
            switch (MetaWormIds.Normalize(wormId))
            {
                case MetaWormIds.Attack:
                    return AttackStarterBodyPrefab != null ? AttackStarterBodyPrefab : BasicStarterBodyPrefab; // 공격형
                case MetaWormIds.Mobility:
                    return MobilityStarterBodyPrefab != null ? MobilityStarterBodyPrefab : BasicStarterBodyPrefab; // 이속형
                case MetaWormIds.Support:
                    return SupportStarterBodyPrefab != null ? SupportStarterBodyPrefab : BasicStarterBodyPrefab; // 지원형
                case MetaWormIds.Magic:
                    return MagicStarterBodyPrefab != null ? MagicStarterBodyPrefab : BasicStarterBodyPrefab; // 마법형
                default:
                    return BasicStarterBodyPrefab; // 기본형
            }
        }
    }
}
