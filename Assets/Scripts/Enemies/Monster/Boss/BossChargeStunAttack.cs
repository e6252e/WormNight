using System.Collections;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class BossChargeStunAttack : MonoBehaviour // Rage Phase에서 컨보이 앞쪽 경로를 예고한 뒤 고정 직선 돌진하는 보스 패턴
    {
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        [Header("Telegraph")]
        [SerializeField] private GameObject chargeTelegraphPrefab; // 보스 돌진 경로를 표시할 예고 Prefab

        [Min(0.1f)]
        [SerializeField] private float telegraphWidth = 2.5f; // 돌진 예고선의 가로 폭

        [Min(0.0f)]
        [SerializeField] private float telegraphGroundHeight = 0.03f; // 예고선이 지면에 묻히지 않도록 적용할 높이

        [Header("Telegraph Charge Visual")]
        [Range(0.0f, 1.0f)]
        [SerializeField] private float telegraphStartAlpha = 0.15f; // 돌진 예고 시작 시 범위 표시 투명도

        [Range(0.0f, 1.0f)]
        [SerializeField] private float telegraphEndAlpha = 0.85f; // 돌진 직전 범위 표시 투명도

        [Header("Charge")]
        [Min(0.1f)]
        [SerializeField] private float chargeSpeed = 24.0f; // 보스의 돌진 이동속도

        [Min(0.1f)]
        [SerializeField] private float chargeCollisionRadius = 1.2f; // 고속 돌진 중 머리 충돌을 검사할 SphereCast 반경

        [Min(0.0f)]
        [SerializeField] private float chargeCollisionHeight = 0.8f; // Boss01 기준 충돌 검사 중심의 높이

        [Header("Fixed Charge Path")]
        [Min(0.0f)]
        [SerializeField] private float aimAheadDistance = 3.0f; // 컨보이 진행 방향 앞쪽으로 조준점을 밀어낼 거리

        [Min(0.0f)]
        [SerializeField] private float aimLockDuration = 0.3f; // 돌진 직전 경로를 고정해 플레이어가 최종 회피할 수 있게 하는 시간

        [Min(0.1f)]
        [SerializeField] private float maximumChargeDistance = 28.0f; // 보스가 고정 직선 경로로 돌진할 최대 거리

        [Header("Charge Impact")]
        [Min(0.1f)]
        [SerializeField] private float impactRadius = 4.0f; // 머리 충돌 후 기존 충격파 API를 적용할 범위

        [Min(0.0f)]
        [SerializeField] private float pushDistance = 1.8f; // 충돌한 컨보이를 살짝 밀어낼 거리

        [Min(0.0f)]
        [SerializeField] private float impactSourceBackOffset = 1.0f; // 넉백 방향을 돌진 방향으로 만들기 위한 충격 중심 보정 거리

        [Header("Charge Distance Based Stun")]
        [Min(0.01f)]
        [SerializeField] private float minimumStunDuration = 0.2f; // 짧은 거리에서 명중했을 때 적용할 최소 스턴 시간

        [Min(0.01f)]
        [SerializeField] private float maximumStunDuration = 7.0f; // 최대 돌진 거리 가까이에서 명중했을 때 적용할 최대 스턴 시간

        [Header("Timing")]
        [Min(0.1f)]
        [SerializeField] private float attackInterval = 11.0f; // 다음 돌진 공격까지의 대기시간

        [Min(0.1f)]
        [SerializeField] private float telegraphDuration = 1.2f; // 돌진 전에 경로를 보여주는 시간

        [Min(0.0f)]
        [SerializeField] private float recoveryDuration = 0.7f; // 돌진 성공 또는 실패 후 다른 행동까지 기다리는 시간

        private readonly RaycastHit[] sphereCastHits = new RaycastHit[32]; // 고속 이동 중 충돌 결과를 저장할 배열
        private readonly Collider[] overlapHits = new Collider[32]; // 현재 위치에서 겹친 Collider를 저장할 배열

        private BossController bossController; // 보스 Phase와 행동 잠금을 관리하는 Script Component
        private Transform convoyTarget; // 돌진 경로를 조준할 컨보이 머리 Transform

        private Coroutine attackCoroutine; // 현재 실행 중인 돌진 공격 Coroutine
        private GameObject activeTelegraph; // 현재 생성된 돌진 예고선

        private MaterialPropertyBlock telegraphPropertyBlock; // 예고선 Material을 직접 복제하지 않고 색상만 덮어쓰기 위한 PropertyBlock
        private Renderer[] telegraphRenderers; // 현재 예고선 안의 Renderer 목록
        private Color[] telegraphBaseColors; // 각 Renderer의 원래 색상
        private bool[] telegraphUsesBaseColor; // URP 계열 _BaseColor 사용 여부
        private bool[] telegraphUsesColor; // Built-in 계열 _Color 사용 여부

        private Vector3 lockedChargeDirection; // 돌진 시작 직전에 고정된 돌진 방향
        private float nextAttackTime; // 다음 돌진 공격이 가능한 시간

        private bool ownsActionLock; // 이 Script가 보스 행동 잠금을 가지고 있는지 나타내는 값
        private bool chargeImpactApplied; // 이번 돌진에서 충돌 효과가 이미 적용됐는지 나타내는 값

        public bool IsAttacking { get; private set; } // 현재 보스 돌진 공격이 진행 중인지 나타내는 값
        public bool IsChargePreparing { get; private set; } // 현재 돌진 예고 / 게이지 모으기 단계인지 나타내는 값
        public bool IsCharging { get; private set; } // 현재 실제 돌진 이동 단계인지 나타내는 값

        private void Awake()
        {
            bossController = GetComponent<BossController>(); // 같은 Boss01에 붙어 있는 BossController를 가져온다.
            TryFindConvoyTarget(); // 등록된 컨보이 타겟을 미리 확인한다.
        }

        private void Start()
        {
            ScheduleNextAttack(); // 보스 생성 후 첫 돌진 공격 시간을 예약한다.
        }

        private void Update()
        {
            if (bossController == null || bossController.IsDead) // BossController가 없거나 보스가 사망했다면
            {
                return; // 새로운 돌진 공격을 시작하지 않는다.
            }

            if (bossController.CurrentPhase != BossPhase.Rage) // 현재 보스 Phase가 Rage가 아니라면
            {
                return; // 돌진 공격을 사용하지 않는다.
            }

            if (attackCoroutine != null) // 이미 돌진 공격이 진행 중이라면
            {
                return; // 돌진 공격을 중복 실행하지 않는다.
            }

            if (bossController.IsActionRunning) // 다른 보스 행동이 진행 중이라면
            {
                return; // 동시에 돌진하지 않는다.
            }

            if (Time.time < nextAttackTime) // 아직 다음 돌진 시간이 되지 않았다면
            {
                return; // 공격 간격이 끝날 때까지 기다린다.
            }

            if (!TryFindConvoyTarget()) // 현재 컨보이 타겟을 찾지 못했다면
            {
                nextAttackTime = Time.time + 1.0f; // 1초 뒤 다시 타겟을 확인한다.
                return; // 돌진 공격을 시작하지 않는다.
            }

            if (chargeTelegraphPrefab == null) // 돌진 예고 Prefab이 연결되지 않았다면
            {
                nextAttackTime = Time.time + 1.0f; // 1초 뒤 다시 확인한다.
                return; // 돌진 공격을 시작하지 않는다.
            }

            if (!bossController.TryBeginAction()) // 보스 행동 잠금을 얻지 못했다면
            {
                return; // 다른 보스 행동이 끝날 때까지 기다린다.
            }

            ownsActionLock = true; // 이 Script가 행동 잠금을 소유한다고 저장한다.
            attackCoroutine = StartCoroutine(AttackRoutine()); // 돌진 공격 Coroutine을 시작한다.
        }

        private void OnDisable()
        {
            if (attackCoroutine != null) // 실행 중인 돌진 Coroutine이 있다면
            {
                StopCoroutine(attackCoroutine); // 현재 돌진 Coroutine을 중단한다.
                attackCoroutine = null; // Coroutine 참조를 비운다.
            }

            CleanupTelegraph(); // 남아 있는 돌진 예고선을 제거한다.
            ClearAttackState(); // 돌진 상태값을 초기화한다.
            ReleaseActionLock(); // 이 Script가 가진 행동 잠금을 해제한다.
        }

        private IEnumerator AttackRoutine()
        {
            IsAttacking = true; // 돌진 공격이 시작됐다고 저장한다.
            IsChargePreparing = true; // 돌진 준비 단계가 시작됐다고 저장한다.
            IsCharging = false; // 아직 실제 돌진은 시작하지 않았다고 저장한다.
            chargeImpactApplied = false; // 이번 돌진의 충돌 적용 상태를 초기화한다.

            SpawnChargeTelegraph(); // 돌진 예고선을 생성한다.

            float telegraphTimer = 0.0f; // 예고 진행 시간을 저장한다.
            bool pathLocked = false; // 경로 고정 여부를 저장한다.
            float lockStartTime = Mathf.Max(0.0f, telegraphDuration - aimLockDuration); // 경로 고정 시작 시간을 계산한다.

            while (telegraphTimer < telegraphDuration) // 예고 시간이 끝날 때까지 반복한다.
            {
                if (!CanContinueAttack()) // 예고 중 보스나 타겟 상태가 유효하지 않다면
                {
                    FinishAttack(); // 공격 상태를 정리한다.
                    yield break; // 돌진하지 않고 종료한다.
                }

                telegraphTimer += Time.deltaTime; // 예고 시간을 증가시킨다.
                float chargeRate = Mathf.Clamp01(telegraphTimer / telegraphDuration); // 예고 진행률을 계산한다.

                if (!pathLocked && telegraphTimer >= lockStartTime) // 경로 고정 시간이 됐다면
                {
                    lockedChargeDirection = CalculateChargeDirection(); // 현재 컨보이 앞쪽 기준으로 돌진 방향을 고정한다.
                    pathLocked = true; // 경로가 고정됐다고 저장한다.
                }

                if (pathLocked) // 경로가 고정됐다면
                {
                    ApplyChargeTelegraph(lockedChargeDirection); // 고정된 경로를 표시한다.
                }
                else
                {
                    ApplyChargeTelegraph(CalculateChargeDirection()); // 아직 조준 중이면 컨보이 앞쪽을 따라 예고선을 갱신한다.
                }

                ApplyChargeTelegraphChargeRate(chargeRate); // 기를 모을수록 예고선이 진해지게 한다.

                yield return null; // 다음 프레임까지 기다린다.
            }

            if (!pathLocked) // 예고 시간이 너무 짧아 고정되지 않았다면
            {
                lockedChargeDirection = CalculateChargeDirection(); // 돌진 직전에 경로를 강제로 고정한다.
            }

            CleanupTelegraph(); // 돌진 직전에 예고선을 제거한다.

            if (lockedChargeDirection.sqrMagnitude <= 0.0001f) // 고정 방향이 유효하지 않다면
            {
                FinishAttack(); // 공격 상태를 정리한다.
                yield break; // 돌진하지 않고 종료한다.
            }

            IsChargePreparing = false; // 돌진 준비 단계를 끝낸다.
            IsCharging = true; // 실제 돌진 이동 단계가 시작됐다고 저장한다.

            yield return ChargeMove(); // 고정된 직선 경로로 돌진한다.

            IsCharging = false; // 실제 돌진 이동 단계가 끝났다고 저장한다.

            if (bossController == null || bossController.IsDead) // 돌진 종료 시점에 보스가 사망했다면
            {
                FinishAttack(); // 공격 상태를 정리한다.
                yield break; // 회복시간 없이 종료한다.
            }

            yield return new WaitForSeconds(recoveryDuration); // 돌진 후 회복 시간을 기다린다.

            FinishAttack(); // 공격 상태와 행동 잠금을 정리한다.
        }

        private IEnumerator ChargeMove()
        {
            float traveledDistance = 0.0f; // 지금까지 돌진한 거리를 저장한다.
            float maxDistance = GetMaximumChargeDistance(); // 최대 돌진 거리를 가져온다.

            transform.rotation = Quaternion.LookRotation(lockedChargeDirection, Vector3.up); // 돌진 방향을 바라보게 한다.

            while (traveledDistance < maxDistance) // 최대 거리까지 반복한다.
            {
                if (bossController == null || bossController.IsDead) // 보스가 사망했다면
                {
                    yield break; // 돌진을 중단한다.
                }

                if (bossController.CurrentPhase != BossPhase.Rage) // Rage Phase가 끝났다면
                {
                    yield break; // 돌진을 중단한다.
                }

                float remainingDistance = maxDistance - traveledDistance; // 남은 돌진 거리를 계산한다.
                float frameDistance = Mathf.Min(chargeSpeed * Time.deltaTime, remainingDistance); // 이번 프레임 이동 거리를 계산한다.

                if (frameDistance <= 0.0f) // 이동 거리가 없다면
                {
                    yield return null; // 다음 프레임까지 기다린다.
                    continue; // 이동 계산을 건너뛴다.
                }

                Vector3 currentPosition = transform.position; // 현재 보스 위치를 가져온다.

                if (TryDetectConvoyHeadHit(currentPosition, lockedChargeDirection, frameDistance, out float hitDistance)) // 이번 이동 구간에서 컨보이 머리를 맞췄다면
                {
                    float safeHitDistance = Mathf.Clamp(hitDistance, 0.0f, frameDistance); // 충돌 위치까지의 거리를 제한한다.
                    float actualChargeDistance = Mathf.Clamp(traveledDistance + safeHitDistance, 0.0f, maxDistance); // 실제 충돌 지점까지의 전체 돌진 거리를 계산한다.

                    transform.position = currentPosition + lockedChargeDirection * safeHitDistance; // 충돌 위치까지 이동한다.
                    transform.rotation = Quaternion.LookRotation(lockedChargeDirection, Vector3.up); // 돌진 방향을 유지한다.

                    ApplyChargeImpact(lockedChargeDirection, actualChargeDistance); // 거리 비례 스턴과 넉백을 적용한다.

                    yield break; // 명중했으므로 돌진을 종료한다.
                }

                transform.position = currentPosition + lockedChargeDirection * frameDistance; // 고정 경로를 따라 이동한다.
                transform.rotation = Quaternion.LookRotation(lockedChargeDirection, Vector3.up); // 고정된 돌진 방향을 유지한다.

                traveledDistance += frameDistance; // 누적 이동 거리를 증가시킨다.

                yield return null; // 다음 프레임까지 기다린다.
            }
        }

        private Vector3 CalculateChargeDirection()
        {
            Vector3 aimPosition = GetAimPosition(); // 컨보이 앞쪽 조준점을 가져온다.
            Vector3 direction = aimPosition - transform.position; // 보스에서 조준점까지의 방향을 계산한다.
            direction.y = 0.0f; // 지면 기준 방향으로 제한한다.

            if (direction.sqrMagnitude <= 0.0001f) // 방향이 유효하지 않다면
            {
                direction = transform.forward; // 보스의 앞 방향을 사용한다.
                direction.y = 0.0f; // 지면 기준 방향으로 제한한다.
            }

            if (direction.sqrMagnitude <= 0.0001f) // 보스 앞 방향도 유효하지 않다면
            {
                direction = Vector3.forward; // 월드 기준 앞 방향을 사용한다.
            }

            direction.Normalize(); // 방향의 길이를 1로 만든다.

            return direction; // 최종 돌진 방향을 반환한다.
        }

        private Vector3 GetAimPosition()
        {
            if (convoyTarget == null) // 컨보이 타겟이 없다면
            {
                return transform.position + transform.forward * aimAheadDistance; // 보스 앞쪽을 임시 조준점으로 사용한다.
            }

            Vector3 forward = convoyTarget.forward; // 컨보이 머리의 앞 방향을 가져온다.
            forward.y = 0.0f; // 지면 기준 방향으로 제한한다.

            if (forward.sqrMagnitude <= 0.0001f) // 컨보이 앞 방향이 유효하지 않다면
            {
                forward = convoyTarget.position - transform.position; // 보스에서 컨보이로 향하는 방향을 사용한다.
                forward.y = 0.0f; // 지면 기준 방향으로 제한한다.
            }

            if (forward.sqrMagnitude <= 0.0001f) // 대체 방향도 유효하지 않다면
            {
                forward = Vector3.forward; // 월드 기준 앞 방향을 사용한다.
            }

            forward.Normalize(); // 방향의 길이를 1로 만든다.

            return convoyTarget.position + forward * aimAheadDistance; // 컨보이 앞쪽 조준점을 반환한다.
        }

        private void ApplyChargeTelegraph(Vector3 direction)
        {
            if (activeTelegraph == null) // 예고선이 없다면
            {
                return; // 배치하지 않는다.
            }

            if (direction.sqrMagnitude <= 0.0001f) // 방향이 유효하지 않다면
            {
                return; // 배치하지 않는다.
            }

            direction.Normalize(); // 방향의 길이를 1로 만든다.

            float distance = GetMaximumChargeDistance(); // 최대 돌진 거리를 가져온다.
            Vector3 telegraphPosition = transform.position + direction * (distance * 0.5f); // 돌진 경로 중앙 위치를 계산한다.
            telegraphPosition.y = telegraphGroundHeight; // 예고선 높이를 적용한다.

            activeTelegraph.transform.position = telegraphPosition; // 예고선을 경로 중앙에 배치한다.
            activeTelegraph.transform.rotation = Quaternion.LookRotation(direction, Vector3.up); // 예고선을 돌진 방향으로 회전한다.

            Vector3 telegraphScale = activeTelegraph.transform.localScale; // 현재 예고선 크기를 가져온다.
            telegraphScale.x = telegraphWidth; // 예고선 폭을 적용한다.
            telegraphScale.z = distance; // 예고선 길이를 최대 돌진 거리로 맞춘다.
            activeTelegraph.transform.localScale = telegraphScale; // 계산된 크기를 적용한다.
        }

        private void CacheTelegraphRenderers()
        {
            if (activeTelegraph == null) // 예고선이 없다면
            {
                ClearTelegraphRendererCache(); // Renderer 캐시를 비운다.
                return; // 더 처리하지 않는다.
            }

            telegraphRenderers = activeTelegraph.GetComponentsInChildren<Renderer>(true); // 예고선 자식 Renderer를 모두 찾는다.
            telegraphBaseColors = new Color[telegraphRenderers.Length]; // Renderer별 원래 색상을 저장할 배열을 만든다.
            telegraphUsesBaseColor = new bool[telegraphRenderers.Length]; // _BaseColor 사용 여부 배열을 만든다.
            telegraphUsesColor = new bool[telegraphRenderers.Length]; // _Color 사용 여부 배열을 만든다.

            for (int i = 0; i < telegraphRenderers.Length; i++) // 모든 Renderer를 확인한다.
            {
                Renderer targetRenderer = telegraphRenderers[i]; // 현재 Renderer를 가져온다.

                if (targetRenderer == null || targetRenderer.sharedMaterial == null) // Renderer나 Material이 없다면
                {
                    telegraphBaseColors[i] = Color.white; // 기본 색상을 저장한다.
                    continue; // 다음 Renderer를 확인한다.
                }

                Material material = targetRenderer.sharedMaterial; // 원본 Material을 가져온다.

                if (material.HasProperty(BaseColorProperty)) // URP Lit/Unlit 계열 색상 속성이 있다면
                {
                    telegraphBaseColors[i] = material.GetColor(BaseColorProperty); // 현재 색상을 저장한다.
                    telegraphUsesBaseColor[i] = true; // _BaseColor를 사용한다고 표시한다.
                }
                else if (material.HasProperty(ColorProperty)) // Built-in 계열 색상 속성이 있다면
                {
                    telegraphBaseColors[i] = material.GetColor(ColorProperty); // 현재 색상을 저장한다.
                    telegraphUsesColor[i] = true; // _Color를 사용한다고 표시한다.
                }
                else
                {
                    telegraphBaseColors[i] = Color.white; // 색상 속성이 없다면 기본 색상을 사용한다.
                }
            }
        }

        private void ApplyChargeTelegraphChargeRate(float chargeRate)
        {
            if (telegraphRenderers == null || telegraphRenderers.Length == 0) // Renderer 캐시가 없다면
            {
                return; // 투명도를 변경할 대상이 없으므로 종료한다.
            }

            if (telegraphPropertyBlock == null) // PropertyBlock이 아직 없다면
            {
                telegraphPropertyBlock = new MaterialPropertyBlock(); // 새 PropertyBlock을 만든다.
            }

            float startAlpha = Mathf.Clamp01(telegraphStartAlpha); // 시작 투명도를 0~1로 제한한다.
            float endAlpha = Mathf.Clamp01(telegraphEndAlpha); // 종료 투명도를 0~1로 제한한다.
            float alpha = Mathf.Lerp(startAlpha, endAlpha, Mathf.Clamp01(chargeRate)); // 차지 진행률에 따라 현재 투명도를 계산한다.

            for (int i = 0; i < telegraphRenderers.Length; i++) // 모든 Renderer에 적용한다.
            {
                Renderer targetRenderer = telegraphRenderers[i]; // 현재 Renderer를 가져온다.

                if (targetRenderer == null) // Renderer가 없다면
                {
                    continue; // 다음 Renderer를 확인한다.
                }

                Color color = telegraphBaseColors != null && i < telegraphBaseColors.Length ? telegraphBaseColors[i] : Color.white; // 저장된 원래 색상을 가져온다.
                color.a = alpha; // 현재 차지 진행률 알파값을 적용한다.

                targetRenderer.GetPropertyBlock(telegraphPropertyBlock); // 기존 PropertyBlock 값을 가져온다.

                if (telegraphUsesBaseColor != null && i < telegraphUsesBaseColor.Length && telegraphUsesBaseColor[i]) // _BaseColor를 쓰는 Material이라면
                {
                    telegraphPropertyBlock.SetColor(BaseColorProperty, color); // _BaseColor에 색상을 적용한다.
                }

                if (telegraphUsesColor != null && i < telegraphUsesColor.Length && telegraphUsesColor[i]) // _Color를 쓰는 Material이라면
                {
                    telegraphPropertyBlock.SetColor(ColorProperty, color); // _Color에 색상을 적용한다.
                }

                if ((telegraphUsesBaseColor == null || i >= telegraphUsesBaseColor.Length || !telegraphUsesBaseColor[i]) && (telegraphUsesColor == null || i >= telegraphUsesColor.Length || !telegraphUsesColor[i])) // 어떤 색상 속성인지 확인할 수 없다면
                {
                    telegraphPropertyBlock.SetColor(BaseColorProperty, color); // 가능성 있는 색상 속성에 적용한다.
                    telegraphPropertyBlock.SetColor(ColorProperty, color); // 가능성 있는 색상 속성에 적용한다.
                }

                targetRenderer.SetPropertyBlock(telegraphPropertyBlock); // 계산된 색상 값을 Renderer에 적용한다.
            }
        }

        private void ClearTelegraphRendererCache()
        {
            telegraphRenderers = null; // Renderer 캐시를 비운다.
            telegraphBaseColors = null; // 색상 캐시를 비운다.
            telegraphUsesBaseColor = null; // _BaseColor 사용 여부를 비운다.
            telegraphUsesColor = null; // _Color 사용 여부를 비운다.
        }

        private bool TryDetectConvoyHeadHit(Vector3 currentPosition, Vector3 chargeDirection, float frameDistance, out float hitDistance)
        {
            hitDistance = 0.0f; // 기본 충돌 거리를 초기화한다.

            Vector3 sphereCenter = currentPosition + Vector3.up * chargeCollisionHeight; // 현재 충돌 검사 중심을 계산한다.

            int overlapCount = Physics.OverlapSphereNonAlloc(sphereCenter, chargeCollisionRadius, overlapHits, Physics.AllLayers, QueryTriggerInteraction.Collide); // 현재 위치에서 이미 겹쳤는지 검사한다.

            for (int i = 0; i < overlapCount; i++) // 현재 겹친 Collider를 확인한다.
            {
                Collider overlapCollider = overlapHits[i]; // 현재 Collider를 가져온다.

                if (overlapCollider == null) // Collider가 없다면
                {
                    continue; // 다음 Collider를 확인한다.
                }

                if (MonsterInteractionApi.IsConvoyHeadCollider(overlapCollider)) // 컨보이 머리라면
                {
                    hitDistance = 0.0f; // 현재 위치에서 충돌했다고 저장한다.
                    return true; // 충돌 성공
                }
            }

            int hitCount = Physics.SphereCastNonAlloc(sphereCenter, chargeCollisionRadius, chargeDirection, sphereCastHits, frameDistance, Physics.AllLayers, QueryTriggerInteraction.Collide); // 이동 경로를 검사한다.

            float closestDistance = frameDistance; // 가장 가까운 충돌 거리를 저장한다.
            bool foundHit = false; // 충돌 발견 여부를 저장한다.

            for (int i = 0; i < hitCount; i++) // 감지된 충돌을 확인한다.
            {
                RaycastHit hit = sphereCastHits[i]; // 현재 충돌 결과를 가져온다.

                if (hit.collider == null) // Collider가 없다면
                {
                    continue; // 다음 결과를 확인한다.
                }

                if (!MonsterInteractionApi.IsConvoyHeadCollider(hit.collider)) // 컨보이 머리가 아니라면
                {
                    continue; // 무시한다.
                }

                if (hit.distance > closestDistance) // 기존 충돌보다 멀다면
                {
                    continue; // 무시한다.
                }

                closestDistance = hit.distance; // 더 가까운 충돌 거리로 갱신한다.
                foundHit = true; // 충돌을 찾았다고 저장한다.
            }

            hitDistance = closestDistance; // 최종 충돌 거리를 반환한다.

            return foundHit; // 충돌 여부를 반환한다.
        }

        private void ApplyChargeImpact(Vector3 chargeDirection, float actualChargeDistance)
        {
            if (chargeImpactApplied) // 이미 충돌 효과를 적용했다면
            {
                return; // 중복 적용하지 않는다.
            }

            chargeImpactApplied = true; // 충돌 효과가 적용됐다고 저장한다.

            float stunDuration = CalculateStunDuration(actualChargeDistance); // 거리 비례 스턴 시간을 계산한다.
            Vector3 impactCenter = transform.position - chargeDirection * impactSourceBackOffset; // 충격 중심을 보스 뒤쪽으로 보정한다.
            impactCenter.y = 0.0f; // 지면 기준으로 제한한다.

            MonsterInteractionApi.RequestSegmentShockwave(impactCenter, impactRadius, pushDistance, stunDuration); // 넉백과 스턴을 적용한다.
        }

        private float CalculateStunDuration(float actualChargeDistance)
        {
            float rate = Mathf.Clamp01(actualChargeDistance / GetMaximumChargeDistance()); // 최대 거리 대비 충돌 거리 비율을 계산한다.
            float minDuration = Mathf.Min(minimumStunDuration, maximumStunDuration); // 작은 값을 최소 스턴으로 사용한다.
            float maxDuration = Mathf.Max(minimumStunDuration, maximumStunDuration); // 큰 값을 최대 스턴으로 사용한다.

            return Mathf.Lerp(minDuration, maxDuration, rate); // 거리 비율에 따라 스턴 시간을 반환한다.
        }

        private bool TryFindConvoyTarget()
        {
            if (MonsterInteractionApi.TryGetConvoyTarget(out Transform target)) // 활성 컨보이 타겟이 있다면
            {
                convoyTarget = target; // 컨보이 타겟을 저장한다.
                return true; // 성공 반환
            }

            convoyTarget = null; // 타겟을 비운다.
            return false; // 실패 반환
        }

        private bool CanContinueAttack()
        {
            if (bossController == null || bossController.IsDead) // 보스가 없거나 사망했다면
            {
                return false; // 공격 불가
            }

            if (bossController.CurrentPhase != BossPhase.Rage) // Rage Phase가 아니라면
            {
                return false; // 공격 불가
            }

            if (convoyTarget == null || !convoyTarget.gameObject.activeInHierarchy) // 컨보이 타겟이 없다면
            {
                return TryFindConvoyTarget(); // 다시 찾는다.
            }

            return true; // 공격 가능
        }

        private void SpawnChargeTelegraph()
        {
            CleanupTelegraph(); // 기존 예고선을 제거한다.

            Transform runtimeRoot = MonsterRuntimeRoot.GetRootOrFallback(transform.parent); // Runtime Root를 가져온다.
            activeTelegraph = Instantiate(chargeTelegraphPrefab, transform.position, Quaternion.identity, runtimeRoot); // 예고선을 생성한다.

            CacheTelegraphRenderers(); // 예고선 Renderer와 원래 색상을 저장한다.
            ApplyChargeTelegraph(CalculateChargeDirection()); // 생성 직후 현재 방향으로 배치한다.
            ApplyChargeTelegraphChargeRate(0.0f); // 처음에는 흐리게 표시한다.
        }

        private void CleanupTelegraph()
        {
            if (activeTelegraph != null) // 예고선이 존재한다면
            {
                Destroy(activeTelegraph); // 예고선을 제거한다.
                activeTelegraph = null; // 참조를 비운다.
            }

            ClearTelegraphRendererCache(); // 예고선 Renderer 캐시를 정리한다.
        }

        private float GetMaximumChargeDistance()
        {
            return Mathf.Max(0.1f, maximumChargeDistance); // 최대 돌진 거리를 안전하게 반환한다.
        }

        private void ScheduleNextAttack()
        {
            nextAttackTime = Time.time + attackInterval; // 다음 공격 가능 시간을 예약한다.
        }

        private void FinishAttack()
        {
            CleanupTelegraph(); // 예고선을 제거한다.
            ClearAttackState(); // 공격 상태값을 초기화한다.
            ReleaseActionLock(); // 행동 잠금을 해제한다.
            ScheduleNextAttack(); // 다음 공격 시간을 예약한다.

            attackCoroutine = null; // Coroutine 참조를 비운다.
        }

        private void ClearAttackState()
        {
            IsAttacking = false; // 공격 상태를 해제한다.
            IsChargePreparing = false; // 돌진 준비 상태를 해제한다.
            IsCharging = false; // 실제 돌진 상태를 해제한다.
            chargeImpactApplied = false; // 충돌 효과 적용 상태를 초기화한다.
            lockedChargeDirection = Vector3.zero; // 고정 방향을 초기화한다.
        }

        private void ReleaseActionLock()
        {
            if (!ownsActionLock) // 행동 잠금을 가지고 있지 않다면
            {
                return; // 처리하지 않는다.
            }

            if (bossController != null) // BossController가 있다면
            {
                bossController.EndAction(); // 보스 행동 잠금을 해제한다.
            }

            ownsActionLock = false; // 행동 잠금 소유 상태를 해제한다.
        }
    }
}