using System.Collections;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class BossJumpShockwaveAttack : MonoBehaviour // Rage Phase에서 컨보이 근처로 점프하고 착지 충격파를 발생시키는 보스 패턴
    {
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor"); // URP Material의 기본 색상 Property ID
        private static readonly int ColorProperty = Shader.PropertyToID("_Color"); // Standard Material의 기본 색상 Property ID

        [Header("Telegraph")]
        [SerializeField] private GameObject landingTelegraphPrefab; // 보스가 착지할 위치를 표시할 원형 예고 Prefab

        [Min(0.0f)]
        [SerializeField] private float telegraphGroundHeight = 0.03f; // 예고 표시가 지면에 묻히지 않도록 적용할 높이

        [Range(0.01f, 1.0f)]
        [SerializeField] private float telegraphStartAlpha = 0.07f; // 예고 표시가 처음 생성됐을 때의 투명도

        [Range(0.01f, 1.0f)]
        [SerializeField] private float telegraphEndAlpha = 1.0f; // 점프 직전 예고 표시의 투명도

        [Header("Jump VFX")]
        [SerializeField] private GameObject takeoffVfxPrefab; // 점프 전 땅치기 순간 생성할 VFX

        [SerializeField] private GameObject landingImpactVfxPrefab; // 착지 순간 생성할 VFX

        [Min(0.0f)]
        [SerializeField] private float takeoffVfxGroundHeight = 0.03f; // 점프 전 VFX가 지면에 묻히지 않도록 적용할 높이

        [Min(0.0f)]
        [SerializeField] private float landingImpactVfxGroundHeight = 0.03f; // 착지 VFX가 지면에 묻히지 않도록 적용할 높이

        [Min(0.1f)]
        [SerializeField] private float takeoffVfxScale = 1.0f; // 점프 전 VFX 크기 배율

        [Min(0.1f)]
        [SerializeField] private float landingImpactVfxScale = 1.5f; // 착지 VFX 크기 배율

        [Min(0.01f)]
        [SerializeField] private float takeoffVfxLifeTime = 2.0f; // 점프 전 VFX 제거 시간

        [Min(0.01f)]
        [SerializeField] private float landingImpactVfxLifeTime = 2.5f; // 착지 VFX 제거 시간

        [Header("Landing Position")]
        [Min(0.0f)]
        [SerializeField] private float minimumLandingDistance = 2.5f; // 컨보이 중심에서 착지 지점까지의 최소 거리

        [Min(0.1f)]
        [SerializeField] private float maximumLandingDistance = 5.0f; // 컨보이 중심에서 착지 지점까지의 최대 거리

        [Header("Jump")]
        [Min(0.1f)]
        [SerializeField] private float jumpHeight = 8.0f; // 점프 포물선의 최대 높이

        [Min(0.05f)]
        [SerializeField] private float jumpDuration = 0.9f; // 시작 위치에서 착지 위치까지 이동하는 시간

        [Header("Landing Shockwave")]
        [Min(0.1f)]
        [SerializeField] private float shockwaveRadius = 7.0f; // 착지 충격파가 세그먼트를 감지할 범위

        [Min(0.0f)]
        [SerializeField] private float shockwavePushDistance = 3.0f; // 충격파가 컨보이 경로를 바깥쪽으로 변형할 거리

        [Min(0.01f)]
        [SerializeField] private float shockwaveRecoveryDuration = 1.5f; // 충격파 이후 컨보이가 원래 경로로 복구되는 기준 시간

        [Header("Timing")]
        [Min(0.1f)]
        [SerializeField] private float attackInterval = 9.0f; // 다음 점프 충격파 패턴을 사용할 때까지의 시간

        [Min(0.1f)]
        [SerializeField] private float telegraphDuration = 1.2f; // 예고 표시 후 보스가 점프하기까지의 시간

        [Min(0.0f)]
        [SerializeField] private float landingRecoveryDuration = 0.7f; // 착지 후 다른 보스 행동을 허용하기까지의 시간

        private BossController bossController; // 보스 Phase와 행동 잠금을 관리하는 Script Component

        private Transform convoyTarget; // 보스가 점프할 위치의 기준이 되는 컨보이 Transform

        private Coroutine attackCoroutine; // 현재 실행 중인 점프 공격 Coroutine

        private GameObject activeTelegraph; // 현재 생성된 착지 예고 표시

        private float nextAttackTime; // 다음 점프 공격이 가능한 시간

        private float jumpGroundHeight; // 점프를 시작했을 때 Boss01의 지면 높이

        private bool ownsActionLock; // 이 Script가 BossController 행동 잠금을 소유하고 있는지 나타내는 값

        private bool isAirborne; // 현재 Boss01이 점프 중인지 나타내는 값

        private bool jumpInterrupted; // 점프 도중 보스가 사망해 공격이 중단됐는지 나타내는 값

        public bool IsAttacking { get; private set; } // 현재 점프 충격파 패턴이 진행 중인지 나타내는 값

        private void Awake()
        {
            bossController = GetComponent<BossController>(); // 같은 Boss01에 붙어 있는 BossController를 가져온다.
            TryFindConvoyTarget(); // MonsterInteractionApi에 등록된 현재 컨보이를 찾는다.
        }

        private void Start()
        {
            ScheduleNextAttack(); // 보스 생성 후 첫 번째 점프 공격 시간을 예약한다.
        }

        private void Update()
        {
            if (bossController == null || bossController.IsDead) // BossController가 없거나 보스가 사망했다면
            {
                return; // 새로운 점프 공격을 시작하지 않는다.
            }

            if (bossController.CurrentPhase != BossPhase.Rage) // 현재 보스 Phase가 Rage가 아니라면
            {
                return; // 점프 충격파 패턴을 사용하지 않는다.
            }

            if (attackCoroutine != null) // 이미 점프 공격이 진행 중이라면
            {
                return; // 중복 공격을 시작하지 않는다.
            }

            if (bossController.IsActionRunning) // 순간이동이나 다른 보스 패턴이 진행 중이라면
            {
                return; // 동시에 점프 공격을 시작하지 않는다.
            }

            if (Time.time < nextAttackTime) // 점프 공격 간격이 아직 끝나지 않았다면
            {
                return; // 다음 공격 시간이 될 때까지 기다린다.
            }

            if (!TryFindConvoyTarget()) // 현재 컨보이를 찾지 못했다면
            {
                nextAttackTime = Time.time + 1.0f; // 1초 뒤 다시 확인하도록 예약한다.
                return; // 점프 공격을 시작하지 않는다.
            }

            if (landingTelegraphPrefab == null) // 착지 예고 Prefab이 연결되지 않았다면
            {
                nextAttackTime = Time.time + 1.0f; // 1초 뒤 다시 확인하도록 예약한다.
                return; // 점프 공격을 시작하지 않는다.
            }

            if (!bossController.TryBeginAction()) // 보스 행동 잠금을 얻지 못했다면
            {
                return; // 다른 패턴이 먼저 실행 중이므로 기다린다.
            }

            ownsActionLock = true; // 이 Script가 행동 잠금을 소유한다고 저장한다.
            attackCoroutine = StartCoroutine(AttackRoutine()); // 점프 충격파 공격 Coroutine을 시작한다.
        }

        private void OnDisable()
        {
            if (attackCoroutine != null) // 실행 중인 점프 공격 Coroutine이 있다면
            {
                StopCoroutine(attackCoroutine); // 현재 Coroutine을 중단한다.
                attackCoroutine = null; // Coroutine 참조를 비운다.
            }

            if (isAirborne) // 점프 도중 Script가 비활성화됐다면
            {
                Vector3 groundedPosition = transform.position; // 현재 Boss01 위치를 가져온다.
                groundedPosition.y = jumpGroundHeight; // 점프 시작 당시의 지면 높이로 되돌린다.
                transform.position = groundedPosition; // Boss01이 공중에 남지 않도록 위치를 적용한다.
            }

            CleanupTelegraph(); // 남아 있는 착지 예고 표시를 제거한다.

            IsAttacking = false; // 공격 상태를 해제한다.
            isAirborne = false; // 점프 상태를 해제한다.
            jumpInterrupted = false; // 점프 중단 상태를 초기화한다.

            ReleaseActionLock(); // 이 Script가 소유한 행동 잠금을 해제한다.
        }

        private IEnumerator AttackRoutine() // 착지 예고부터 점프와 충격파까지 처리하는 전체 공격 흐름
        {
            IsAttacking = true; // 점프 충격파 패턴이 시작됐다고 저장한다.
            jumpInterrupted = false; // 이전 점프 중단 상태를 초기화한다.

            if (!TryFindConvoyTarget()) // 공격 시작 시점에 컨보이를 찾지 못했다면
            {
                FinishAttack(); // 공격 상태를 정리한다.
                yield break; // 점프를 시작하지 않고 종료한다.
            }

            Vector3 jumpStartPosition = transform.position; // Boss01의 현재 위치를 점프 시작점으로 저장한다.
            Vector3 landingPosition = CalculateLandingPosition(jumpStartPosition.y); // 컨보이 근처의 무작위 착지 지점을 계산한다.

            SpawnLandingTelegraph(landingPosition); // 계산된 착지 위치에 원형 예고 표시를 생성한다.

            float telegraphTimer = 0.0f; // 예고 표시가 유지된 시간을 저장한다.

            while (telegraphTimer < telegraphDuration) // 설정된 예고 시간이 끝날 때까지 반복한다.
            {
                if (!CanContinueTelegraph()) // 예고 도중 보스가 사망하거나 Rage Phase가 끝났다면
                {
                    FinishAttack(); // 예고 표시와 행동 잠금을 정리한다.
                    yield break; // 점프하지 않고 종료한다.
                }

                telegraphTimer += Time.deltaTime; // 지난 프레임 시간을 예고 타이머에 더한다.

                float progress = Mathf.Clamp01(telegraphTimer / telegraphDuration); // 예고 진행도를 0에서 1 사이로 계산한다.
                float alpha = Mathf.Lerp(telegraphStartAlpha, telegraphEndAlpha, progress); // 예고가 점점 진해지도록 투명도를 계산한다.

                SetTelegraphAlpha(activeTelegraph, alpha); // 현재 착지 예고 표시의 투명도를 변경한다.

                yield return null; // 다음 프레임까지 기다린다.
            }

            if (bossController == null || bossController.IsDead) // 점프 직전에 보스가 사망했다면
            {
                FinishAttack(); // 공격 상태를 정리한다.
                yield break; // 점프하지 않고 종료한다.
            }

            SpawnTakeoffVfx(jumpStartPosition); // 점프 직전 땅치기 VFX를 생성한다.
            BeginJumpState(jumpStartPosition.y); // 점프 중 비활성화 안전 처리를 위해 지면 높이와 점프 상태를 저장한다.

            yield return ArcMove(jumpStartPosition, landingPosition); // Boss01을 착지 위치까지 포물선으로 이동시킨다.

            isAirborne = false; // 공중 상태를 해제한다.

            if (jumpInterrupted || bossController == null || bossController.IsDead) // 점프 도중 보스가 사망했다면
            {
                CleanupTelegraph(); // 착지 예고 표시를 제거한다.
                FinishAttack(); // 공격 상태를 정리한다.
                yield break; // 충격파를 발생시키지 않고 종료한다.
            }

            transform.position = landingPosition; // 포물선 계산 후 최종 착지 위치를 정확하게 맞춘다.

            CleanupTelegraph(); // 착지와 동시에 예고 표시를 제거한다.
            SpawnLandingImpactVfx(landingPosition); // 착지 순간 충격 VFX를 생성한다.
            GameplaySfxEmitter.TryPlayAt(transform, GameplaySfxCue.BossJumpImpact, landingPosition, true);

            ApplyLandingShockwave(); // 기존 컨보이 세그먼트 충격파 API를 호출한다.

            yield return new WaitForSeconds(landingRecoveryDuration); // 착지 후 짧은 회복시간 동안 행동 잠금을 유지한다.

            FinishAttack(); // 공격 상태를 종료하고 다음 점프 시간을 예약한다.
        }

        private Vector3 CalculateLandingPosition(float bossGroundHeight) // 컨보이 근처의 무작위 착지 위치를 계산하는 함수
        {
            float minimumDistance = Mathf.Min(minimumLandingDistance, maximumLandingDistance); // 두 거리 중 작은 값을 최소 거리로 사용한다.
            float maximumDistance = Mathf.Max(minimumLandingDistance, maximumLandingDistance); // 두 거리 중 큰 값을 최대 거리로 사용한다.

            Vector2 randomCircleDirection = Random.insideUnitCircle; // XZ 평면에서 사용할 무작위 방향을 선택한다.

            if (randomCircleDirection.sqrMagnitude <= 0.0001f) // 무작위 방향의 길이가 너무 작다면
            {
                randomCircleDirection = Vector2.right; // 기본 오른쪽 방향을 사용한다.
            }

            randomCircleDirection.Normalize(); // 무작위 방향의 길이를 1로 만든다.

            float landingDistance = Random.Range(minimumDistance, maximumDistance); // 컨보이에서 떨어질 착지 거리를 무작위로 선택한다.

            Vector3 landingOffset = new Vector3(randomCircleDirection.x, 0.0f, randomCircleDirection.y) * landingDistance; // XZ 평면의 착지 오프셋을 계산한다.
            Vector3 landingPosition = convoyTarget.position + landingOffset; // 컨보이 위치에 무작위 오프셋을 더한다.

            landingPosition.y = bossGroundHeight; // 착지 후 Boss01이 기존 지면 높이를 유지하도록 설정한다.

            return landingPosition; // 계산된 최종 착지 위치를 반환한다.
        }

        private IEnumerator ArcMove(Vector3 from, Vector3 to) // Boss01을 시작점에서 착지점까지 포물선으로 이동시키는 함수
        {
            float elapsed = 0.0f; // 점프가 진행된 시간을 저장한다.

            while (elapsed < jumpDuration) // 설정된 점프 시간이 끝날 때까지 반복한다.
            {
                if (bossController == null || bossController.IsDead) // 점프 도중 보스가 사망했다면
                {
                    jumpInterrupted = true; // 점프가 중단됐다고 저장한다.
                    yield break; // 포물선 이동을 즉시 종료한다.
                }

                elapsed += Time.deltaTime; // 지난 프레임 시간을 점프 진행시간에 더한다.

                float progress = Mathf.Clamp01(elapsed / jumpDuration); // 점프 진행도를 0에서 1 사이로 계산한다.

                Vector3 position = Vector3.Lerp(from, to, progress); // 시작점과 착지점 사이의 기본 직선 위치를 계산한다.
                position.y += Mathf.Sin(progress * Mathf.PI) * jumpHeight; // 사인 곡선을 이용해 포물선 높이를 더한다.

                Vector3 movementDirection = to - from; // 점프 시작점에서 착지점까지의 방향을 계산한다.
                movementDirection.y = 0.0f; // 보스가 위아래로 기울지 않도록 Y축 방향을 제거한다.

                transform.position = position; // 계산된 포물선 위치로 Boss01을 이동시킨다.

                if (movementDirection.sqrMagnitude > 0.0001f) // 착지 방향이 유효하다면
                {
                    transform.rotation = Quaternion.LookRotation(movementDirection.normalized, Vector3.up); // 보스가 착지 방향을 바라보게 한다.
                }

                yield return null; // 다음 프레임까지 기다린다.
            }

            transform.position = to; // 점프가 끝나면 최종 착지 위치를 정확하게 적용한다.
        }

        private void BeginJumpState(float groundHeight) // 점프 중 비활성화 상황에 대비해 현재 지면 높이와 점프 상태를 저장한다.
        {
            jumpGroundHeight = groundHeight; // 점프 시작 당시의 Boss01 지면 높이를 저장한다.
            isAirborne = true; // Boss01이 점프 상태라고 저장한다.
        }

        private void SpawnTakeoffVfx(Vector3 position) // 점프 전 땅치기 VFX를 생성한다.
        {
            SpawnOneShotVfx(takeoffVfxPrefab, position, takeoffVfxGroundHeight, takeoffVfxScale, takeoffVfxLifeTime);
        }

        private void SpawnLandingImpactVfx(Vector3 position) // 착지 순간 충격 VFX를 생성한다.
        {
            SpawnOneShotVfx(landingImpactVfxPrefab, position, landingImpactVfxGroundHeight, landingImpactVfxScale, landingImpactVfxLifeTime);
        }

        private void SpawnOneShotVfx(GameObject prefab, Vector3 position, float groundHeight, float scaleMultiplier, float lifeTime) // 단발성 VFX를 바닥에 생성하고 자동 제거한다.
        {
            if (prefab == null)
            {
                return;
            }

            Vector3 spawnPosition = GroundService.ProjectToGround(position, groundHeight);
            Transform runtimeRoot = MonsterRuntimeRoot.GetRootOrFallback(transform.parent);
            GameObject vfx = Instantiate(prefab, spawnPosition, Quaternion.identity, runtimeRoot);
            vfx.transform.localScale = vfx.transform.localScale * scaleMultiplier;
            Destroy(vfx, lifeTime);
        }

        private void ApplyLandingShockwave() // 착지 위치를 중심으로 기존 컨보이 충격파 시스템을 호출하는 함수
        {
            MonsterInteractionApi.RequestSegmentShockwave(transform.position, shockwaveRadius, shockwavePushDistance, shockwaveRecoveryDuration); // 기존 EnemyJump와 동일한 세그먼트 충격파 API를 호출한다.
        }

        private bool TryFindConvoyTarget() // MonsterInteractionApi에 등록된 컨보이 Transform을 가져오는 함수
        {
            if (MonsterInteractionApi.TryGetConvoyTarget(out Transform target)) // 현재 활성화된 컨보이가 등록되어 있다면
            {
                convoyTarget = target; // 조회된 컨보이 Transform을 저장한다.
                return true; // 컨보이를 찾았다고 반환한다.
            }

            convoyTarget = null; // 컨보이를 찾지 못했다면 기존 참조를 비운다.
            return false; // 컨보이를 찾지 못했다고 반환한다.
        }

        private bool CanContinueTelegraph() // 예고 단계에서 점프를 계속 준비할 수 있는지 확인하는 함수
        {
            if (bossController == null || bossController.IsDead) // BossController가 없거나 보스가 사망했다면
            {
                return false; // 점프 공격을 계속할 수 없다.
            }

            if (bossController.CurrentPhase != BossPhase.Rage) // 점프 전에 Rage Phase가 끝났다면
            {
                return false; // 이번 점프 공격을 취소한다.
            }

            if (convoyTarget == null || !convoyTarget.gameObject.activeInHierarchy) // 컨보이가 없거나 비활성화됐다면
            {
                return TryFindConvoyTarget(); // 현재 컨보이를 다시 확인한다.
            }

            return true; // 예고와 점프 준비를 계속할 수 있다고 반환한다.
        }

        private void SpawnLandingTelegraph(Vector3 landingPosition) // 계산된 착지 위치에 원형 예고 표시를 생성하는 함수
        {
            CleanupTelegraph(); // 이전 공격에서 남아 있을 수 있는 예고 표시를 제거한다.

            Vector3 telegraphPosition = GroundService.ProjectToGround(landingPosition, telegraphGroundHeight);

            Transform runtimeRoot = MonsterRuntimeRoot.GetRootOrFallback(transform.parent); // Monsters Runtime Root를 가져온다.

            activeTelegraph = Instantiate(landingTelegraphPrefab, telegraphPosition, Quaternion.identity, runtimeRoot); // Monsters 아래에 착지 예고 Prefab을 생성한다.

            Vector3 telegraphScale = activeTelegraph.transform.localScale; // 예고 Prefab의 기존 Scale을 가져온다.
            telegraphScale.x = shockwaveRadius * 2.0f; // 예고 표시의 X 크기를 충격파 지름에 맞춘다.
            telegraphScale.z = shockwaveRadius * 2.0f; // 예고 표시의 Z 크기를 충격파 지름에 맞춘다.
            activeTelegraph.transform.localScale = telegraphScale; // 계산된 예고 표시 크기를 적용한다.

            SetTelegraphAlpha(activeTelegraph, telegraphStartAlpha); // 생성 직후 예고 표시의 시작 투명도를 적용한다.
        }

        private void SetTelegraphAlpha(GameObject telegraph, float alpha) // 착지 예고 표시의 투명도를 변경하는 함수
        {
            if (telegraph == null) // 예고 표시가 제거됐거나 생성되지 않았다면
            {
                return; // Material을 수정하지 않는다.
            }

            Renderer[] renderers = telegraph.GetComponentsInChildren<Renderer>(true); // 예고 표시와 자식의 모든 Renderer를 가져온다.

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++) // 모든 Renderer를 순회한다.
            {
                Material[] materials = renderers[rendererIndex].materials; // 현재 Renderer가 사용하는 Material 인스턴스를 가져온다.

                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++) // 모든 Material을 순회한다.
                {
                    Material material = materials[materialIndex]; // 현재 수정할 Material을 가져온다.

                    if (material == null) // Material이 없다면
                    {
                        continue; // 다음 Material을 확인한다.
                    }

                    if (material.HasProperty(BaseColorProperty)) // URP 기본 색상 Property가 있다면
                    {
                        Color color = material.GetColor(BaseColorProperty); // 기존 색상을 가져온다.
                        color.a = alpha; // 기존 RGB는 유지하고 Alpha만 변경한다.
                        material.SetColor(BaseColorProperty, color); // 변경된 색상을 Material에 적용한다.
                    }

                    if (material.HasProperty(ColorProperty)) // Standard 기본 색상 Property가 있다면
                    {
                        Color color = material.GetColor(ColorProperty); // 기존 색상을 가져온다.
                        color.a = alpha; // 기존 RGB는 유지하고 Alpha만 변경한다.
                        material.SetColor(ColorProperty, color); // 변경된 색상을 Material에 적용한다.
                    }
                }
            }
        }

        private void CleanupTelegraph() // 현재 생성된 착지 예고 표시를 제거하는 함수
        {
            if (activeTelegraph != null) // 착지 예고 표시가 존재한다면
            {
                Destroy(activeTelegraph); // 예고 표시 GameObject를 제거한다.
                activeTelegraph = null; // 제거된 예고 표시 참조를 비운다.
            }
        }

        private void ScheduleNextAttack() // 다음 점프 충격파 공격 시간을 예약하는 함수
        {
            nextAttackTime = Time.time + attackInterval; // 현재 시간에 설정된 공격 간격을 더한다.
        }

        private void FinishAttack() // 점프 충격파 공격 상태를 정리하는 함수
        {
            CleanupTelegraph(); // 남아 있을 수 있는 착지 예고 표시를 제거한다.

            IsAttacking = false; // 공격 진행 상태를 해제한다.
            isAirborne = false; // 점프 상태를 해제한다.
            jumpInterrupted = false; // 점프 중단 상태를 초기화한다.

            ReleaseActionLock(); // 다른 보스 패턴이 실행될 수 있도록 행동 잠금을 해제한다.
            ScheduleNextAttack(); // 다음 점프 충격파 공격 시간을 예약한다.

            attackCoroutine = null; // 현재 공격 Coroutine 참조를 비운다.
        }

        private void ReleaseActionLock() // 이 Script가 소유한 BossController 행동 잠금을 해제하는 함수
        {
            if (!ownsActionLock) // 이 Script가 행동 잠금을 가지고 있지 않다면
            {
                return; // 다른 보스 패턴의 잠금에는 영향을 주지 않는다.
            }

            if (bossController != null) // BossController가 존재한다면
            {
                bossController.EndAction(); // 보스 행동 잠금을 해제한다.
            }

            ownsActionLock = false; // 행동 잠금을 더 이상 소유하지 않는다고 저장한다.
        }
    }
}
