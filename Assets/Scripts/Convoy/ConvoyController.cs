using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TeamProject01.Gameplay
{
    public enum ConvoyControlMode // 조작 모드
    {
        RelativeTurn, // 진행방향 좌우 턴
        WasdDirection, // 자동전진 WASD
        MousePointer, // 마우스 추적
        WasdManualForward // 수동전진 WASD
    }

    public sealed partial class ConvoyController : MonoBehaviour // 컨보이 본체
    {
        private const ConvoyControlMode DefaultControlMode = ConvoyControlMode.WasdDirection; // 기본 조작
        private const string HeadJointName = "Joint";
        private const string FrontSocketName = "FrontSocket";
        private const string RearSocketName = "RearSocket";

        [Header("Scene References")]
        public Transform SegmentRoot; // 세그먼트 부모
        public Transform DetachedTailRoot; // 분리 꼬리 부모
        public Transform ProjectileRoot; // 투사체 부모
        public Transform HeadVisual; // 머리 표시
        public Transform HeadJoint; // visual chain anchor for the starter segment
        public GroundCheck HeadGroundCheck; // 머리 바닥 체크
        public GameObject SegmentPrefab; // 세그먼트 프리팹
        public Material HeadMaterial; // 머리 재질
        public Material SegmentMaterial; // 몸통 재질 A
        public Material SegmentAltMaterial; // 몸통 재질 B

        [Header("Movement Feel")]
        [Min(0f)] public float BaseSpeed = 6f; // 전진 속도
        [Min(1f)] public float TurnSpeed = 138f; // 최대 회전
        [Min(1f)] public float TurnResponse = 11f; // 회전 반응
        [Min(1f)] public float TurnReleaseResponse = 17f; // 회전 복귀
        [Min(1f)] public float DirectionSteerFullTurnAngle = 42f; // 최대 조향각
        public ConvoyControlMode ControlMode = DefaultControlMode; // 현재 모드

        [Header("Body Follow")]
        public bool EnableStartingSegments = false; // 시작 세그먼트 자동 생성
        public bool ClearScenePlacedSegmentsOnStart = true; // 씬 배치 시작 세그먼트 제거
        [Range(0, 40)] public int StartingSegmentCount = 0; // 시작 길이
        [Range(1, 100)] public int MaxSegmentCount = 100; // 최대 길이
        [Min(0.1f)] public float SegmentSpacing = 1.18f; // 몸통 간격
        [Min(0.01f)] public float MinPathSampleDistance = 0.08f; // 경로 샘플 간격
        [Min(1f)] public float SegmentFollowResponse = 24f; // 추적 반응
        [Min(1f)] public float SegmentTurnResponse = 22f; // 회전 추적
        [Min(128)] public int PathSampleLimit = 2048; // 경로 보관량
        public Vector3 HeadScale = new Vector3(1.25f, 0.6f, 1.45f); // 머리 크기
        public Vector3 SegmentScale = Vector3.one; // 몸통 크기
        public bool PreventSegmentVerticalSquash = true; // 새 모델 납작해짐 방지
        [Min(0.01f)] public float MinimumSegmentScaleY = 1f; // 최소 세로 크기
        [Min(0f)] public float VisualCenterHeight = 0.32f; // 표시 높이
        [Min(0f)] public float HeadVisualLean = 8f; // 회전 기울기

        [Header("Tail Collision")]
        public bool EnableTailCollision = true; // 꼬리 충돌 사용
        [Range(1, 12)] public int TailCollisionSafeSegmentCount = 4; // 앞쪽 안전칸
        [Min(0.1f)] public float TailCollisionRadius = 0.82f; // 충돌 반경
        public bool EnableHeadMonsterBlocker = true; // 머리 몬스터 밀기
        [Min(0.1f)] public float HeadMonsterBlockRadius = 0.95f; // 머리 차단 반경
        [Min(0f)] public float TailCutCooldown = 0.45f; // 재절단 대기

        [Header("Detached Tail Physics")]
        public bool EnableHeadPhysicsCollider = true; // 머리 밀기 콜라이더
        public bool EnableDetachedTailPhysics = true; // 분리 물리 사용
        [Min(0.01f)] public float DetachedTailMass = 0.8f; // 분리 질량
        [Min(0f)] public float DetachedTailLinearDamping = 0.75f; // 이동 감쇠
        [Min(0f)] public float DetachedTailAngularDamping = 1.5f; // 회전 감쇠
        [Min(0f)] public float TailBurstForce = 16.5f; // 절단 폭발 힘
        [Min(0f)] public float TailBurstRadius = 6.4f; // 폭발 반경
        [Min(0f)] public float TailBurstUpward = 1.7f; // 위쪽 힘
        [Min(0f)] public float TailBurstTorque = 8.5f; // 회전 힘
        [Range(1f, 85f)] public float DetachedTailJointAngle = 38f; // 링크 굽힘각
        [Min(0f)] public float DetachedTailJointProjection = 0.18f; // 링크 보정 거리

        [Header("Detached Tail Rejoin")]
        public bool EnableDetachedTailRejoin = true; // 재결합 사용
        [Min(0.01f)] public float DetachedTailSettleSpeed = 0.08f; // 안착 속도
        [Min(0.01f)] public float DetachedTailSettleAngularSpeed = 0.55f; // 안착 회전속도
        [Min(0f)] public float DetachedTailSettleTime = 1.1f; // 안착 유지시간
        [Min(0f)] public float DetachedTailMinRejoinAge = 1.8f; // 최소 재결합 대기
        [Min(0.1f)] public float RejoinAreaRadius = 1.15f; // 재결합 반경
        [Min(0f)] public float RejoinAreaForwardOffset = 1.65f; // 머리 앞 거리
        [Min(0f)] public float RejoinAreaHeight = 0.08f; // 표시 높이
        [Range(12, 96)] public int RejoinAreaSegments = 48; // 원 세그먼트
        public Color RejoinAreaColor = new Color(0.35f, 1f, 0.78f, 0.82f); // 재결합 색

        [Header("Segment Weapons")]
        public bool EnableSegmentAutoFire = true; // 자동 발사 사용

        [Header("Segment Catalog Add")]
        public bool UseCatalogForDefaultAdd = true; // 기본 추가는 카탈로그 랜덤 사용
        public SegmentCatalogAsset DefaultAddSegmentCatalog; // 스페이스/자동 추가 후보

        [Header("Segment Add Limit")]
        public bool RestrictAddedSegmentsToAllowedId = true; // 자동 추가 세그먼트 제한
        public string AllowedAddedSegmentId = "SG01_Cannon"; // 현재 자동 추가 허용 세그먼트

        private readonly List<Transform> segments = new List<Transform>(128); // 연결 몸통
        private readonly List<GroundCheck> segmentGroundChecks = new List<GroundCheck>(128); // 몸통 바닥 체크
        private readonly List<ConvoySegmentRuntime> segmentRuntimes = new List<ConvoySegmentRuntime>(128); // 몸통 런타임
        private readonly Dictionary<string, int> currentSegmentLevelsById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); // 세그먼트별 현재 모델 레벨
        private readonly Dictionary<string, SegmentDefinition> segmentDefinitionsById = new Dictionary<string, SegmentDefinition>(StringComparer.OrdinalIgnoreCase); // 레벨 프리팹 찾기용 정의
        private readonly List<DetachedTailGroup> detachedTails = new List<DetachedTailGroup>(16); // 분리 꼬리
        private readonly List<Vector3> path = new List<Vector3>(2048); // 머리 경로
        private Material rejoinAreaMaterial; // 재결합 재질
        private Vector3 startPosition; // 시작 위치
        private Quaternion startRotation; // 시작 회전
        private float currentTurnVelocity; // 현재 회전속도
        private float currentTurnInput; // 현재 회전입력
        private float currentForwardSpeed; // 현재 전진속도
        private float tailCutCooldownRemaining; // 절단 쿨타임
        private int detachedTailSerial; // 분리 그룹 번호

        // 외부 피격 효과 런타임
        private Vector3 knockbackDirection; // 외부 넉백 방향
        private float knockbackDistanceRemaining; // 남은 넉백 거리
        private float knockbackTimeRemaining; // 남은 넉백 시간
        private float knockbackTotalTime; // 넉백 전체 시간
        private float knockbackElapsedTime; // 넉백 진행 시간
        private float knockbackHeight; // 넉백 중 공중으로 뜨는 높이
        private float knockbackVisualHeight; // 비주얼 공중 높이
        public int SegmentCount => segments.Count; // 표시 길이
        public int MaxSegments => MaxSegmentCount; // 외부 최대 길이
        public bool CanAddSegment => CanAddDefaultSegment(); // 기본 추가 가능
        public CoreStatData CurrentCoreStats => CoreStatProvider.GetCurrentOrDefault(); // 현재 성장값
        public float CurrentSpeed => currentForwardSpeed; // HUD 속도
        public float CurrentTurnVelocity => currentTurnVelocity; // HUD 회전
        public float CurrentTurnInput => currentTurnInput; // 머리 기울기
        public ConvoyControlMode CurrentControlMode => ControlMode; // HUD 모드
        public string CurrentControlModeLabel => IsAutoOrbitActive ? GetAutoOrbitModeLabel() : GetControlModeLabel(ControlMode); // HUD 모드명
        public event Action<int> SegmentCountChanged; // 세그먼트 수 변경

        private void Awake() // 참조 준비
        {
            ApplyDefaultControlModeIfNeeded(); // 기존 씬 기본값 보정
            startPosition = transform.position; // 리셋 위치
            startRotation = transform.rotation; // 리셋 회전
            EnsureHeadVisual(); // 머리 보강
            ApplySelectedWormVisualFromCurrentLoadout(); // 선택 지렁이 외형
            EnsureHeadMonsterBlocker(); // 머리 차단 보강
            ConfigureGroundChecks(); // 바닥 체크 연결
            EnsureHeadPhysicsCollider(); // 머리 충돌 보강
            EnsureSegmentRoot(); // 몸통 루트 보강
            EnsureDetachedTailRoot(); // 분리 루트 보강
            EnsureProjectileRoot(); // 투사체 루트 보강
            RegisterDefaultAddCatalogDefinitions(); // 카탈로그 후보 등록
            CollectExistingSegments(); // 씬 배치 몸통 수집
            ClearInitialSegmentsIfNeeded(); // 시작 몸통 제거
        }

        private void ApplyDefaultControlModeIfNeeded() // 기본 조작 보정
        {
            if (ControlMode != ConvoyControlMode.RelativeTurn)
            {
                return; // 이미 명시된 다른 모드
            }

            ControlMode = DefaultControlMode; // 구 씬 기본값을 WASD로 전환
        }

        // 컨보이 타겟 등록은 ConvoyController가 직접 책임진다.
        private void OnEnable() // 컨보이가 활성화될 때 몬스터용 타겟 API에 자기 자신을 등록한다.
        {
            MonsterInteractionApi.RegisterConvoyTarget(transform); // 몬스터가 GameObject.Find 없이 컨보이를 찾을 수 있게 한다.
        }

        // 컨보이 비활성화 시 API에 남은 참조와 요청을 정리한다.
        private void OnDisable() // 컨보이가 꺼질 때 몬스터용 타겟 API에서 자기 자신을 해제한다.
        {
            MonsterInteractionApi.UnregisterConvoyTarget(transform); // 비활성화된 컨보이가 몬스터 타겟으로 남지 않게 한다.
            MonsterInteractionApi.ClearConvoyKnockbackRequests(); // 씬 전환/비활성화 때 소비되지 않은 넉백 요청을 비운다.
        }

        private void Start() // 시작 세팅
        {
            startPosition = SnapHeadToGround(startPosition); // 시작 바닥 보정
            transform.position = SnapHeadToGround(transform.position); // 현재 바닥 보정
            currentForwardSpeed = BaseSpeed; // 초기 속도
            ResetPath(); // 경로 초기화
            EnsureStarterSegmentFromCurrentLoadout(); // 타이틀 선택 스타터 생성

            if (EnableStartingSegments)
            {
                while (GetRegularSegmentCount() < StartingSegmentCount)
                {
                    if (!AddSegment(SegmentPrefab, false))
                    {
                        break; // 최대치 도달
                    }
                }
            }

            SnapSegmentsToPath(); // 몸통 정렬
        }

        private void Update() // 이동 루프
        {
            float deltaTime = Time.deltaTime; // 프레임 시간
            if (deltaTime <= 0f)
            {
                return; // 정지 프레임
            }

            WormInput input = ReadInput(); // 입력 수집

            if (input.Reset)
            {
                ResetWorm(); // 시작 위치
                return; // 이번 프레임 종료
            }

            if (input.AddSegment)
            {
                AddDefaultSegment(true); // 테스트 랜덤 추가
            }

            if (input.RemoveSegment)
            {
                RemoveSegment(); // 테스트 제거
            }

            if (IsAutoOrbitActive && HasAutoOrbitCancelInput(input))
            {
                CancelAutoOrbit(); // 수동 조작 복귀
            }

            ApplyControl(input, deltaTime); // 모드별 조향

            Vector3 currentPosition = transform.position; // 이동하기 전 현재 위치를 저장한다.

            if (MonsterInteractionApi.TryConsumeConvoyKnockback(currentPosition, out Vector3 apiKnockbackDirection, out float apiKnockbackDistance, out float apiKnockbackDuration, out float apiKnockbackHeight)) // 몬스터가 요청한 컨보이 넉백이 있는지 확인한다.
            {
                ApplyKnockback(apiKnockbackDirection, apiKnockbackDistance, apiKnockbackDuration, apiKnockbackHeight); // 실제 이동 적용은 컨보이 컨트롤러가 책임진다.
            }

            float slowMultiplier = MonsterInteractionApi.GetConvoySpeedMultiplier(currentPosition); // 현재 컨보이 위치에 적용될 슬로우 배율을 가져온다.

            Vector3 forwardDisplacement = transform.forward * (currentForwardSpeed * slowMultiplier * deltaTime); // 기본 전진 이동량을 계산한다.

            float knockbackVerticalOffset; // 이번 프레임에 공중으로 뜰 높이
            Vector3 knockbackDisplacement = ConsumeKnockbackDisplacement(deltaTime, out knockbackVerticalOffset); // 수평 넉백 이동량과 공중 높이를 계산한다.

            Vector3 desiredPosition = currentPosition + forwardDisplacement + knockbackDisplacement; // 전진 이동량과 넉백 이동량을 합친다.

            desiredPosition = SnapHeadToGround(desiredPosition); // 이동하려는 위치를 먼저 바닥 높이에 맞춘다.

            desiredPosition = MonsterInteractionApi.ResolveConvoyPosition(currentPosition, desiredPosition, HeadMonsterBlockRadius); // 적 장애물과 겹치지 않도록 컨보이 위치를 보정한다.

            knockbackVisualHeight = knockbackVerticalOffset; // 루트 대신 비주얼만 상승

            transform.position = desiredPosition; // 최종 보정된 위치를 적용한다.      

            SamplePathIfNeeded(); // 경로 기록
            UpdateHeadVisual(deltaTime); // 머리 표시

            UpdateSegments(deltaTime); // 몸통 추적
            UpdateSegmentWeapons(deltaTime); // 세그먼트 사격
            UpdateTailCollision(deltaTime); // 자기 충돌
            UpdateDetachedTailGroups(deltaTime); // 분리 꼬리 갱신
            PrunePath(); // 경로 정리
        }

        private bool AddSegment(GameObject segmentPrefab, bool snapToPath) // 몸통 추가
        {
            GameObject resolvedPrefab = ResolveSegmentPrefabForCurrentLevel(segmentPrefab); // 현재 레벨 반영
            if (!CanAddSegmentPrefab(resolvedPrefab))
            {
                return false; // 최대 길이 또는 허용되지 않은 세그먼트
            }

            Transform segment = CreateSegment(segments.Count, resolvedPrefab); // 새 몸통
            if (segment == null)
            {
                return false; // 프리팹 없음
            }

            segments.Add(segment); // 체인 등록
            segmentGroundChecks.Add(GetSegmentGroundCheck(segment)); // 바닥 체크 등록
            segmentRuntimes.Add(GetSegmentRuntime(segment, segments.Count - 1, true)); // 런타임 등록

            if (snapToPath)
            {
                SnapSegmentToPath(segment, segments.Count - 1); // 끝 위치 정렬
            }

            NotifySegmentCountChanged(); // 길이 변경 알림
            return true; // 추가 성공
        }

        private Vector3 GetSafeSegmentScale() // 몸통 크기 보정
        {
            float yScale = Mathf.Max(0.01f, SegmentScale.y); // 기본 세로 크기
            if (PreventSegmentVerticalSquash)
            {
                yScale = Mathf.Max(MinimumSegmentScaleY, yScale); // 납작해짐 방지
            }

            return new Vector3(
                Mathf.Max(0.01f, SegmentScale.x),
                yScale,
                Mathf.Max(0.01f, SegmentScale.z)); // 0 스케일 방지
        }

        private void ClearInitialSegmentsIfNeeded() // 시작 세그먼트 제거
        {
            if (EnableStartingSegments || !ClearScenePlacedSegmentsOnStart || segments.Count == 0)
            {
                return; // 제거 필요 없음
            }

            for (int i = segments.Count - 1; i >= 0; i--)
            {
                Transform segment = segments[i]; // 기존 배치 몸통
                if (segment != null)
                {
                    DestroyUnityObject(segment.gameObject); // 런타임 시작 몸통 제거
                }
            }

            segments.Clear(); // 체인 비움
            segmentGroundChecks.Clear(); // 바닥 체크 비움
            segmentRuntimes.Clear(); // 런타임 비움
            NotifySegmentCountChanged(); // 길이 0 알림
        }

        public bool TryAddSegment() // 외부 추가 입구
        {
            return AddDefaultSegment(true); // 기본 랜덤 추가
        }

        public bool TryAddSegment(GameObject segmentPrefab) // 프리팹 지정 추가 입구
        {
            return AddSegment(segmentPrefab, true); // 지정 세그먼트 추가
        }

        public int AddSegments(int count, bool snapToPath) // 여러 세그먼트 추가
        {
            int added = 0; // 추가 수
            int targetCount = Mathf.Max(0, count); // 음수 방지
            for (int i = 0; i < targetCount; i++)
            {
                if (!AddDefaultSegment(snapToPath))
                {
                    break; // 더 이상 추가 불가
                }

                added++; // 성공 누적
            }

            return added; // 실제 추가 수
        }

        public bool CanAddSegmentPrefab(GameObject segmentPrefab) // 지정 프리팹 추가 가능
        {
            GameObject resolvedPrefab = ResolveSegmentPrefabForCurrentLevel(segmentPrefab); // 현재 레벨 반영
            return segments.Count < MaxSegmentCount && IsAllowedSegmentPrefab(resolvedPrefab); // 길이/허용 ID 확인
        }

        public bool TryGetRandomAddableSegmentPrefab(out GameObject prefab) // 카탈로그 랜덤 후보 제공
        {
            if (TryPickCatalogSegmentPrefab(out prefab))
            {
                return true; // 카탈로그 후보
            }

            prefab = ResolveSegmentPrefabForCurrentLevel(SegmentPrefab); // 기존 기본값
            return CanAddSegmentPrefab(prefab); // fallback 가능 여부
        }

        private bool AddDefaultSegment(bool snapToPath) // 기본 추가
        {
            return TryGetRandomAddableSegmentPrefab(out GameObject prefab) && AddSegment(prefab, snapToPath); // 랜덤 생성
        }

        private bool CanAddDefaultSegment() // 기본 추가 가능
        {
            return TryGetRandomAddableSegmentPrefab(out _); // 후보 존재 여부
        }

        private bool TryPickCatalogSegmentPrefab(out GameObject prefab) // 카탈로그에서 추가 후보 선택
        {
            prefab = null; // 기본값
            if (!UseCatalogForDefaultAdd)
            {
                return false; // 카탈로그 미사용
            }

            SegmentCatalogAsset catalog = GetDefaultAddCatalog(); // 등록 데이터
            if (catalog == null || catalog.Segments == null || catalog.Segments.Length == 0)
            {
                return false; // 후보 없음
            }

            int startIndex = UnityEngine.Random.Range(0, catalog.Segments.Length); // 랜덤 시작
            for (int i = 0; i < catalog.Segments.Length; i++)
            {
                int index = (startIndex + i) % catalog.Segments.Length; // 순환 검사
                SegmentDefinition definition = catalog.Segments[index]; // 후보 정의
                if (TryGetAddableCatalogSegmentPrefab(definition, out prefab))
                {
                    return true; // 사용 가능
                }
            }

            return false; // 추가 가능한 후보 없음
        }

        private bool TryGetAddableCatalogSegmentPrefab(SegmentDefinition definition, out GameObject prefab) // 정의 → 현재 레벨 프리팹
        {
            prefab = null; // 기본값
            if (definition == null || !definition.HasId || definition.StarterOnly || !definition.CanAddByLevelChoice)
            {
                return false; // 추가 후보 아님
            }

            RegisterSegmentDefinition(definition); // 레벨 추적 등록
            int level = GetCurrentSegmentLevelInternal(definition.NormalizedId, definition); // 현재 레벨
            return definition.TryGetSegmentPrefab(level, out prefab) && CanAddSegmentPrefab(prefab); // 생성 가능 확인
        }

        private SegmentCatalogAsset GetDefaultAddCatalog() // 기본 추가 카탈로그
        {
            if (DefaultAddSegmentCatalog != null)
            {
                return DefaultAddSegmentCatalog; // 컨보이 직접 설정
            }

            return CoreStatProvider.Active != null ? CoreStatProvider.Active.SegmentCatalogAsset : null; // 코어 fallback
        }

        private void RegisterDefaultAddCatalogDefinitions() // 카탈로그 정의 등록
        {
            SegmentCatalogAsset catalog = GetDefaultAddCatalog(); // 등록 데이터
            if (catalog == null || catalog.Segments == null)
            {
                return; // 없음
            }

            for (int i = 0; i < catalog.Segments.Length; i++)
            {
                RegisterSegmentDefinition(catalog.Segments[i]); // 추가 레벨 후보 등록
            }
        }

        private bool IsAllowedSegmentPrefab(GameObject segmentPrefab) // 추가 허용 프리팹 확인
        {
            if (segmentPrefab == null)
            {
                return false; // 프리팹 없음
            }

            if (!RestrictAddedSegmentsToAllowedId || string.IsNullOrWhiteSpace(AllowedAddedSegmentId))
            {
                return true; // 제한 비활성
            }

            string allowedId = AllowedAddedSegmentId.Trim(); // 허용 ID
            if (string.Equals(segmentPrefab.name, allowedId, StringComparison.OrdinalIgnoreCase))
            {
                return true; // 프리팹 이름 일치
            }

            if (!TryGetSegmentIdFromPrefab(segmentPrefab, out string segmentId))
            {
                return false; // ID 판단 불가
            }

            return string.Equals(segmentId, allowedId, StringComparison.OrdinalIgnoreCase); // ID 일치 여부
        }

        private static bool TryGetSegmentIdFromPrefab(GameObject segmentPrefab, out string segmentId) // 프리팹 ID 확인
        {
            segmentId = string.Empty; // 기본값
            if (segmentPrefab == null)
            {
                return false; // 프리팹 없음
            }

            SegmentWeaponBehaviour weapon = segmentPrefab.GetComponent<SegmentWeaponBehaviour>(); // 세그먼트 무기
            if (weapon == null)
            {
                return false; // ID 판단 불가
            }

            segmentId = string.IsNullOrWhiteSpace(weapon.SegmentId)
                ? GetSegmentIdFromWeaponType(weapon.GetType())
                : weapon.SegmentId.Trim(); // 명시 ID 우선
            return !string.IsNullOrWhiteSpace(segmentId); // 유효 ID
        }

        private static string GetSegmentIdFromWeaponType(Type weaponType) // 무기 타입명 기반 ID
        {
            string typeName = weaponType != null ? weaponType.Name : string.Empty; // 타입명
            return typeName.EndsWith("Weapon", StringComparison.Ordinal) ? typeName.Substring(0, typeName.Length - 6) : typeName; // SG01_Cannon
        }

        public int GetSegmentCount() // 외부 길이 조회
        {
            return segments.Count; // 현재 연결 길이
        }

        public void RemoveSegment() // 몸통 제거
        {
            if (segments.Count <= 1)
            {
                return; // 최소 길이
            }

            int index = segments.Count - 1; // 마지막 순번
            Transform segment = segments[index]; // 마지막 몸통
            segments.RemoveAt(index); // 체인 해제
            RemoveSegmentGroundCheck(index); // 바닥 체크 해제
            RemoveSegmentRuntime(index); // 런타임 해제

            if (segment != null)
            {
                DestroyUnityObject(segment.gameObject); // 오브젝트 제거
            }

            NotifySegmentCountChanged(); // 길이 변경 알림
        }

        public void ResetWorm() // 위치 리셋
        {
            CancelAutoOrbit(); // 자동궤도 해제
            transform.SetPositionAndRotation(startPosition, startRotation); // 시작 pose
            transform.position = SnapHeadToGround(transform.position); // 머리 바닥 보정
            currentTurnVelocity = 0f; // 회전 초기화
            currentTurnInput = 0f; // 입력 초기화
            currentForwardSpeed = GetAutoForwardSpeed(); // 속도 복구
            tailCutCooldownRemaining = 0f; // 절단 쿨 초기화

            knockbackDirection = Vector3.zero; // 넉백 방향 초기화
            knockbackDistanceRemaining = 0.0f; // 남은 넉백 거리 초기화
            knockbackTimeRemaining = 0.0f; // 남은 넉백 시간 초기화
            knockbackTotalTime = 0.0f; // 넉백 전체 시간 초기화
            knockbackElapsedTime = 0.0f; // 넉백 진행 시간 초기화
            knockbackHeight = 0.0f; // 넉백 높이 초기화
            knockbackVisualHeight = 0.0f; // 비주얼 높이 초기화
            SyncSegmentRuntimes(true); // 런타임 보정
            ResetPath(); // 경로 재생성
            SnapSegmentsToPath(); // 몸통 정렬
            UpdateHeadVisual(1f); // 머리 정렬
        }

        public void SetControlMode(ConvoyControlMode mode) // 모드 변경
        {
            if (IsAutoOrbitActive)
            {
                CancelAutoOrbit(); // 모드 버튼 입력은 수동 전환
            }

            if (ControlMode == mode)
            {
                return; // 같은 모드
            }

            ControlMode = mode; // 모드 적용
            currentTurnVelocity = 0f; // 관성 제거
            currentTurnInput = 0f; // 입력 제거
        }

        public void ApplyKnockback(Vector3 direction, float distance, float duration, float height = 0.0f) // 외부에서 컨보이를 밀어내고 공중으로 띄우는 API
        {
            direction.y = 0.0f; // 수평 이동 방향은 바닥 평면 기준으로만 계산한다.

            if (direction.sqrMagnitude <= 0.0001f) // 방향이 없다면
            {
                return; // 넉백을 적용하지 않는다.
            }

            knockbackDirection = direction.normalized; // 넉백 방향을 길이 1로 저장한다.
            knockbackDistanceRemaining = Mathf.Max(0.0f, distance);  // 밀릴 거리를 저장한다.
            knockbackTotalTime = Mathf.Max(0.01f, duration); // 넉백 전체 시간을 저장한다.
            knockbackTimeRemaining = knockbackTotalTime; // 남은 시간을 전체 시간으로 초기화한다.
            knockbackElapsedTime = 0.0f; // 진행 시간을 초기화한다.

            knockbackHeight = Mathf.Max(0.0f, height); // 공중으로 뜰 최대 높이를 저장한다.
            knockbackVisualHeight = 0.0f; // 새 넉백은 바닥에서 시작
        }

        private Vector3 ConsumeKnockbackDisplacement(float deltaTime, out float verticalOffset) // 이번 프레임 넉백 이동량과 공중 높이를 계산한다.
        {
            verticalOffset = 0.0f; // 기본 공중 높이는 0이다.

            if (knockbackDistanceRemaining <= 0.0f || knockbackTimeRemaining <= 0.0f) // 남은 넉백이 없다면
            {
                return Vector3.zero; // 이동량 없음
            }

            float moveDistance = knockbackDistanceRemaining * (deltaTime / knockbackTimeRemaining); // 이번 프레임 수평 넉백 거리를 계산한다.
            moveDistance = Mathf.Min(moveDistance, knockbackDistanceRemaining); // 남은 거리보다 많이 움직이지 않게 제한한다.

            knockbackDistanceRemaining -= moveDistance; // 사용한 거리만큼 남은 넉백 거리를 줄인다.
            knockbackTimeRemaining -= deltaTime; // 지난 시간만큼 남은 넉백 시간을 줄인다.
            knockbackElapsedTime += deltaTime; // 지난 시간만큼 넉백 진행 시간을 늘린다.

            float progress = Mathf.Clamp01(knockbackElapsedTime / knockbackTotalTime); // 넉백 진행률을 계산한다.
            verticalOffset = Mathf.Sin(progress * Mathf.PI) * knockbackHeight; // 중간에서 가장 높아지는 포물선 높이를 계산한다.

            if (knockbackDistanceRemaining <= 0.0f || knockbackTimeRemaining <= 0.0f) // 넉백이 끝났다면
            {
                knockbackDistanceRemaining = 0.0f; // 남은 거리 초기화
                knockbackTimeRemaining = 0.0f; // 남은 시간 초기화
                knockbackElapsedTime = 0.0f; // 진행 시간 초기화
                knockbackTotalTime = 0.0f; // 전체 시간 초기화
                knockbackHeight = 0.0f; // 공중 높이 초기화
                verticalOffset = 0.0f; // 끝나는 순간 바닥으로 내려오게 한다.
            }

            return knockbackDirection * moveDistance; // 이번 프레임 수평 넉백 이동량을 반환한다.
        }
        private void NotifySegmentCountChanged() // 길이 변경 알림
        {
            SegmentCountChanged?.Invoke(segments.Count); // 현재 길이 전달
            OnSegmentCountChangedForAutoOrbit(); // 자동궤도 반지름 갱신
        }

        private float GetEffectiveTurnSpeed() // 성장 반영 회전력
        {
            CoreStatData stats = CoreStatProvider.GetCurrentOrDefault(); // 코어 성장값
            return Mathf.Max(1f, TurnSpeed + stats.TurnSpeedBonus); // 보너스 적용
        }

        private float GetEffectiveRejoinAreaRadius() // 성장 반영 재결합 반경
        {
            CoreStatData stats = CoreStatProvider.GetCurrentOrDefault(); // 코어 성장값
            return Mathf.Max(0.1f, RejoinAreaRadius + stats.RejoinRangeBonus); // 보너스 적용
        }

        private static float ExpLerp(float current, float target, float sharpness, float deltaTime) // 지수 보간
        {
            return Mathf.Lerp(current, target, ExpLerpFactor(sharpness, deltaTime)); // 값 보간
        }

        private static float ExpLerpFactor(float sharpness, float deltaTime) // 보간 계수
        {
            return 1f - Mathf.Exp(-sharpness * deltaTime); // 프레임 독립
        }

        private static void DestroyUnityObject(UnityEngine.Object target) // Unity 제거
        {
            if (target == null)
            {
                return; // 대상 없음
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target); // PlayMode 제거
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target); // Editor 제거
            }
        }

        private void OnDrawGizmosSelected() // 경로 gizmo
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f); // 경로 색

            for (int i = 1; i < path.Count; i++)
            {
                Gizmos.DrawLine(path[i - 1], path[i]); // 샘플 연결
            }
        }

        private struct WormInput // 입력 묶음
        {
            public float Turn; // 좌우 턴
            public Vector2 Move; // 목표 방향
            public bool AddSegment; // 테스트 추가
            public bool RemoveSegment; // 테스트 제거
            public bool Reset; // 리셋
            public bool HasMouseWorld; // 마우스 유효
            public Vector3 MouseWorld; // 마우스 위치
        }

        private sealed class DetachedTailGroup // 분리 꼬리 묶음
        {
            public readonly Transform Root; // 그룹 루트
            public readonly List<Transform> Segments = new List<Transform>(32); // 포함 몸통
            public Transform RejoinArea; // 재결합 영역
            public LineRenderer RejoinLine; // 재결합 원
            public Vector3 RejoinCenter; // 재결합 중심
            public float Age; // 분리 시간
            public float SettledTime; // 안착 시간
            public bool RejoinReady; // 재결합 가능

            public DetachedTailGroup(Transform root) // 생성자
            {
                Root = root; // 루트 저장
            }
        }
    }
}

