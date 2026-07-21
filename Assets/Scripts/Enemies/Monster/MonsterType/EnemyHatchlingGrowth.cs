using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace TeamProject01.Gameplay
{
    [RequireComponent(typeof(EnemyHatchlingConsumePresentationBridge))]
    public sealed class EnemyHatchlingGrowth : MonoBehaviour // 일반 몬스터를 잡아먹고 성장하는 해츨링 몬스터
    {
        private static readonly HashSet<EnemyController> reservedConsumeTargets = new HashSet<EnemyController>(); // 다른 해츨링이 이미 먹으려는 몬스터 목록

        [Header("Consume Setting")]
        [FormerlySerializedAs("absorbRange")]
        [Min(0.1f)]
        [SerializeField] private float consumeRange = 10.0f; // 주변 일반 몬스터를 잡아먹을 수 있는 범위

        [Min(0.1f)]
        [SerializeField] private float scanInterval = 0.75f; // 몇 초마다 주변 일반 몬스터를 찾을지

        [Range(0.0f, 1.0f)]
        [SerializeField] private float consumeChance = 0.35f; // 후보 몬스터를 찾았을 때 실제로 잡아먹을 확률

        [Min(0.0f)]
        [SerializeField] private float consumeDelayBeforeKill = 0.25f; // 먹기 VFX가 나온 뒤 대상 몬스터를 제거하기까지의 시간

        [Min(0.0f)]
        [SerializeField] private float consumeRecoveryDuration = 0.35f; // 먹은 뒤 해츨링이 다시 이동하기 전까지의 짧은 회복 시간

        [Header("Consume Teleport")]
        [Min(0.1f)]
        [SerializeField] private float spawnDistanceFromTarget = 1.6f; // 땅속 이동 후 대상 몬스터로부터 어느 거리에서 나타날지

        [Min(0.0f)]
        [SerializeField] private float consumeTeleportGroundHeight = 0.72f; // 땅속 이동 후 나타날 위치의 바닥 보정 높이

        [SerializeField] private bool stopTargetMovementWhileConsuming = true; // 먹힐 대상 몬스터의 이동을 멈출지

        [Header("Growth Setting")]
        [Min(1)]
        [SerializeField] private int maxGrowthStack = 100; // 최대 성장 스택

        [FormerlySerializedAs("maxHpIncreasePercentPerOrb")]
        [Min(0.0f)]
        [SerializeField] private float maxHpIncreasePercentPerConsume = 0.05f; // 포식 1회당 최대 HP 증가율

        [FormerlySerializedAs("attackPowerIncreasePercentPerOrb")]
        [Min(0.0f)]
        [SerializeField] private float attackPowerIncreasePercentPerConsume = 0.05f; // 포식 1회당 공격력 증가율

        [FormerlySerializedAs("attackSpeedIncreasePercentPerOrb")]
        [Min(0.0f)]
        [SerializeField] private float attackSpeedIncreasePercentPerConsume = 0.05f; // 포식 1회당 공격속도 증가율

        [FormerlySerializedAs("scaleIncreasePercentPerOrb")]
        [Min(0.0f)]
        [SerializeField] private float scaleIncreasePercentPerConsume = 0.05f; // 포식 1회당 크기 증가율

        [Min(0.0f)]
        [SerializeField] private float attackRangeIncreasePercentPerConsume = 0.03f; // 포식 1회당 공격 사거리 증가율

        [Min(1.0f)]
        [SerializeField] private float maxAttackRangeMultiplier = 1.3f; // 공격 사거리 최대 배율

        [Header("Runtime")]
        [SerializeField] private int growthStack; // 현재 먹은 몬스터 수, Inspector 확인용 런타임 값

        private readonly List<EnemyController> consumeCandidates = new List<EnemyController>(16); // 포식 후보 일반 몬스터 목록

        private Vector3 baseScale; // 성장하기 전 원래 크기
        private EnemyController enemyController; // 같은 GameObject에 붙은 EnemyController Script Component 참조
        private EnemyController reservedConsumeTarget; // 이 해츨링이 현재 먹기로 예약한 몬스터
        private EnemyHealth health; // 같은 GameObject에 붙은 EnemyHealth Script Component 참조
        private EnemyMovement enemyMovement; // 먹기 연출 중 이동을 잠시 멈추기 위한 EnemyMovement 참조
        private EnemyMovement lockedTargetMovement; // 포식 대상 몬스터의 이동 Script 참조
        private EnemyHatchlingConsumePresentationBridge consumePresentationBridge; // 포식 애니메이션과 VFX를 담당하는 Bridge 참조
        private Coroutine consumeRoutine; // 현재 실행 중인 먹기 Coroutine
        private float scanTimer; // 다음 일반 몬스터 탐색까지 남은 시간
        private bool isConsuming; // 먹기 연출 중인지 확인하는 값
        private bool lockedTargetMovementWasEnabled; // 포식 전 대상 몬스터 이동 Script 활성 상태

        public int GrowthStack // 외부에서 현재 성장 스택을 읽기 위한 property
        {
            get
            {
                return growthStack; // 현재 성장 스택을 반환한다.
            }
        }

        public bool CanGrow // 아직 더 성장할 수 있는지 확인하는 property
        {
            get
            {
                return growthStack < maxGrowthStack; // 성장 스택이 최대치보다 작으면 true를 반환한다.
            }
        }

        public bool IsConsuming // 현재 포식 연출 중인지 외부에서 읽기 위한 property
        {
            get
            {
                return isConsuming; // 현재 포식 상태를 반환한다.
            }
        }

        public float MaxHpIncreasePercentPerConsume // 포식 1회당 최대 HP 증가율 property
        {
            get
            {
                return maxHpIncreasePercentPerConsume; // 포식 1회당 최대 HP 증가율을 반환한다.
            }
        }

        public float MaxHpIncreasePercentPerOrb // 기존 다른 Script가 이 이름을 읽고 있을 경우를 위한 호환용 property
        {
            get
            {
                return maxHpIncreasePercentPerConsume; // 기존 구슬 증가율 대신 포식 증가율을 반환한다.
            }
        }

        public float AttackPowerMultiplier // 공격 Script가 읽을 성장 공격력 배율
        {
            get
            {
                return 1.0f + growthStack * attackPowerIncreasePercentPerConsume; // 성장 스택에 따른 공격력 배율을 반환한다.
            }
        }

        public float AttackSpeedMultiplier // 공격 Script가 읽을 성장 공격속도 배율
        {
            get
            {
                return 1.0f + growthStack * attackSpeedIncreasePercentPerConsume; // 성장 스택에 따른 공격속도 배율을 반환한다.
            }
        }

        public float AttackRangeMultiplier // 공격 Script와 이동 Script가 읽을 성장 공격 사거리 배율
        {
            get
            {
                float safeIncreasePercent = Mathf.Max(0.0f, attackRangeIncreasePercentPerConsume); // 음수 증가율을 방지한다.
                float safeMaxMultiplier = Mathf.Max(1.0f, maxAttackRangeMultiplier); // 최대 배율이 1보다 작아지지 않게 한다.
                float multiplier = 1.0f + growthStack * safeIncreasePercent; // 성장 스택에 따른 공격 사거리 배율을 계산한다.
                return Mathf.Min(multiplier, safeMaxMultiplier); // 최대 배율을 넘지 않게 제한한다.
            }
        }

        private void Awake()
        {
            baseScale = transform.localScale; // 성장 전 원래 크기를 저장한다.
            enemyController = GetComponent<EnemyController>(); // 같은 GameObject에 붙은 EnemyController를 찾는다.
            health = GetComponent<EnemyHealth>(); // 같은 GameObject에 붙은 EnemyHealth Script Component를 찾는다.
            enemyMovement = GetComponent<EnemyMovement>(); // 같은 GameObject에 붙은 EnemyMovement Script Component를 찾는다.
            consumePresentationBridge = GetComponent<EnemyHatchlingConsumePresentationBridge>(); // 같은 GameObject에 붙은 포식 연출 Bridge를 찾는다.
        }

        private void OnEnable()
        {
            scanTimer = scanInterval; // 처음 포식 탐색까지의 시간을 설정한다.
            isConsuming = false; // 활성화될 때 먹기 상태를 초기화한다.
            lockedTargetMovement = null; // 이전 대상 이동 참조를 초기화한다.
            lockedTargetMovementWasEnabled = false; // 이전 대상 이동 상태를 초기화한다.
            reservedConsumeTarget = null; // 이전 포식 예약 대상을 초기화한다.
            ResetConsumePresentationState(); // 활성화될 때 포식 연출 상태를 초기화한다.
            CleanupReservedConsumeTargets(); // 비어 있거나 죽은 예약 대상을 정리한다.
        }

        private void OnDisable()
        {
            if (consumeRoutine != null) // 먹기 Coroutine이 실행 중이었다면
            {
                StopCoroutine(consumeRoutine); // 비활성화 시 먹기 Coroutine을 중단한다.
                consumeRoutine = null; // Coroutine 참조를 비운다.
            }

            ReleaseLockedTargetMovement(); // 비활성화될 때 멈춰둔 대상 몬스터 이동을 복구한다.
            ReleaseReservedConsumeTarget(); // 비활성화될 때 예약한 포식 대상을 해제한다.
            isConsuming = false; // 비활성화될 때 먹기 상태가 남지 않게 한다.
            ResetConsumePresentationState(); // 비활성화될 때 포식 연출 상태를 초기화한다.
            SetEnemyMovementEnabled(true); // 비활성화 전에 이동 상태를 원래대로 돌린다.
        }

        private void Update()
        {
            if (!CanGrow) // 이미 최대 성장 상태라면
            {
                return; // 더 이상 일반 몬스터를 찾지 않는다.
            }

            if (isConsuming) // 이미 먹기 연출 중이라면
            {
                return; // 중복 포식을 시작하지 않는다.
            }

            scanTimer -= Time.deltaTime; // 지난 시간만큼 탐색 대기 시간을 줄인다.

            if (scanTimer > 0.0f) // 아직 탐색 시간이 남아 있다면
            {
                return; // 이번 프레임에는 일반 몬스터를 찾지 않는다.
            }

            scanTimer = scanInterval; // 다음 탐색 시간을 다시 설정한다.

            TryBeginConsumeNearbyMonster(); // 주변 일반 몬스터를 찾아 확률적으로 잡아먹는다.
        }

        private void TryBeginConsumeNearbyMonster() // 주변 일반 몬스터 중 하나를 랜덤으로 골라 먹기 시도를 시작한다.
        {
            CleanupReservedConsumeTargets(); // 후보를 찾기 전에 이미 죽었거나 사라진 예약 대상을 정리한다.

            EnemyController.CollectActiveInRange(transform.position, consumeRange, consumeCandidates, IsValidConsumeTarget); // 범위 안 일반 몬스터 후보를 수집한다.

            if (consumeCandidates.Count == 0) // 먹을 수 있는 일반 몬스터가 없다면
            {
                return; // 포식을 시도하지 않는다.
            }

            EnemyController target = consumeCandidates[Random.Range(0, consumeCandidates.Count)]; // 후보 중 랜덤으로 한 마리를 선택한다.

            if (Random.value > Mathf.Clamp01(consumeChance)) // 포식 확률에 실패했다면
            {
                return; // 이번 탐색에서는 먹지 않는다.
            }

            if (!TryReserveConsumeTarget(target)) // 다른 해츨링이 먼저 예약했거나 대상이 무효라면
            {
                return; // 포식을 시작하지 않는다.
            }

            consumeRoutine = StartCoroutine(ConsumeMonsterRoutine(target)); // 먹기 연출과 성장 처리를 시작한다.
        }

        private IEnumerator ConsumeMonsterRoutine(EnemyController target) // 선택한 일반 몬스터를 잡아먹고 성장하는 흐름
        {
            if (!IsValidConsumeTarget(target)) // 대상이 유효하지 않다면
            {
                EndConsumeRoutine(true); // 예약과 이동 상태를 복구한다.
                yield break; // 먹기 처리를 중단한다.
            }

            isConsuming = true; // 먹기 연출 중이라고 표시한다.
            SetConsumePresentationConsuming(true); // Bridge에 포식 중 상태를 전달한다.
            LockTargetMovement(target); // 선택된 일반 몬스터가 도망가지 않도록 이동을 멈춘다.
            SetEnemyMovementEnabled(false); // 포식 연출 동안 기본 Nexus 이동을 멈춘다.
            FaceEachOther(target); // 시작 전에 대상과 서로 바라보게 한다.

            if (consumePresentationBridge != null) // 포식 연출 Bridge가 있다면
            {
                yield return consumePresentationBridge.PlayBurrow(transform.position); // Burrow 애니메이션과 먼지 VFX를 Bridge에서 재생한다.
            }

            if (!IsValidConsumeTarget(target)) // Burrow 중 대상이 사라졌다면
            {
                EndConsumeRoutine(true); // 예약과 이동 상태를 복구한다.
                yield break; // 먹기 처리를 중단한다.
            }

            TeleportNearConsumeTarget(target); // 대상 근처 목표 지점으로 순간 이동한다.
            FaceEachOther(target); // 나타난 뒤 다시 서로 바라보게 한다.

            if (consumePresentationBridge != null) // 포식 연출 Bridge가 있다면
            {
                yield return consumePresentationBridge.PlaySpawn(transform.position); // Spawn 애니메이션과 먼지 VFX를 Bridge에서 재생한다.
            }

            if (!IsValidConsumeTarget(target)) // Spawn 중 대상이 사라졌다면
            {
                EndConsumeRoutine(true); // 예약과 이동 상태를 복구한다.
                yield break; // 먹기 처리를 중단한다.
            }

            FaceEachOther(target); // 물기 직전에 서로 정확히 마주보게 한다.

            if (consumePresentationBridge != null) // 포식 연출 Bridge가 있다면
            {
                consumePresentationBridge.PlayBite(); // Bite 애니메이션을 Bridge에서 재생한다.
            }

            if (consumePresentationBridge != null && consumePresentationBridge.BiteVfxDelay > 0.0f) // Bite 시작 후 VFX 지연 시간이 있다면
            {
                yield return new WaitForSeconds(consumePresentationBridge.BiteVfxDelay); // 물기 모션이 시작될 시간을 준다.
            }

            if (!IsValidConsumeTarget(target)) // Bite 중 대상이 사라졌다면
            {
                EndConsumeRoutine(true); // 예약과 이동 상태를 복구한다.
                yield break; // 먹기 처리를 중단한다.
            }

            if (consumePresentationBridge != null) // 포식 연출 Bridge가 있다면
            {
                consumePresentationBridge.SpawnConsumeVfx(transform.position, target.transform.position); // 먹는 순간 VFX를 Bridge에서 생성한다.
            }

            if (consumeDelayBeforeKill > 0.0f) // 대상 제거 전 대기 시간이 있다면
            {
                yield return new WaitForSeconds(consumeDelayBeforeKill); // 먹기 연출이 보일 시간을 준다.
            }

            if (IsValidConsumeTarget(target)) // 포식은 이미 확률 성공으로 확정됐으므로 대상 유효성만 확인한다.
            {
                target.KillByConsumed(); // 먹힌 일반 몬스터를 보상 없이 제거한다.
                lockedTargetMovement = null; // 제거된 대상 이동 Script를 복구하지 않도록 참조를 비운다.
                lockedTargetMovementWasEnabled = false; // 제거된 대상 이동 상태를 초기화한다.
                ReleaseReservedConsumeTarget(); // 먹은 대상의 예약을 해제한다.
                ConsumeOneMonster(); // 해츨링 성장 스택과 능력치를 증가시킨다.

                if (consumePresentationBridge != null) // 포식 연출 Bridge가 있다면
                {
                    consumePresentationBridge.SpawnGrowthVfx(transform); // 성장 성공 VFX를 Bridge에서 생성한다.
                }
            }
            else
            {
                EndConsumeRoutine(true); // 대상이 무효가 됐다면 상태를 복구한다.
                yield break; // 먹기 처리를 중단한다.
            }

            if (consumeRecoveryDuration > 0.0f) // 먹은 뒤 회복 시간이 있다면
            {
                yield return new WaitForSeconds(consumeRecoveryDuration); // 다시 움직이기 전 짧게 대기한다.
            }

            EndConsumeRoutine(true); // 포식이 끝난 뒤 이동과 예약 상태를 정리한다.
        }

        private void EndConsumeRoutine(bool restoreMovement) // 포식 Coroutine 종료 시 상태를 정리한다.
        {
            ReleaseLockedTargetMovement(); // 남아 있는 대상 이동 정지를 복구한다.
            ReleaseReservedConsumeTarget(); // 남아 있는 예약 상태를 해제한다.

            if (restoreMovement) // 이동을 복구해야 한다면
            {
                SetEnemyMovementEnabled(true); // 해츨링의 기본 이동을 다시 허용한다.
            }

            isConsuming = false; // 먹기 상태를 해제한다.
            SetConsumePresentationConsuming(false); // Bridge에 포식 종료 상태를 전달한다.
            ResetConsumePresentationVisualOffset(); // 포식이 끝나거나 중단되면 모델 높이를 원래대로 복구한다.
            consumeRoutine = null; // Coroutine 참조를 비운다.
        }

        private bool IsValidConsumeTarget(EnemyController target) // 포식 대상으로 사용할 수 있는 일반 몬스터인지 확인한다.
        {
            if (target == null) // 대상이 없다면
            {
                return false; // 포식 대상으로 사용할 수 없다.
            }

            if (target == enemyController) // 자기 자신이라면
            {
                return false; // 자기 자신은 먹지 않는다.
            }

            if (target.IsDead) // 이미 죽은 몬스터라면
            {
                return false; // 포식 대상으로 사용하지 않는다.
            }

            if (target.Grade != EnemyGrade.Monster) // 일반 몬스터 등급이 아니라면
            {
                return false; // 엘리트와 보스는 먹지 않는다.
            }

            if (IsReservedByOtherHatchling(target)) // 다른 해츨링이 이미 먹기로 예약했다면
            {
                return false; // 중복 포식 대상으로 사용하지 않는다.
            }

            return true; // 모든 조건을 통과했으므로 포식 대상으로 사용할 수 있다.
        }

        private void TeleportNearConsumeTarget(EnemyController target) // 대상 몬스터 근처 먹기 위치로 해츨링을 이동시킨다.
        {
            Vector3 direction = transform.position - target.transform.position; // 현재 해츨링 위치 기준으로 대상에서 해츨링 쪽 방향을 계산한다.
            direction.y = 0.0f; // 높이 차이는 제거한다.

            if (direction.sqrMagnitude < 0.0001f) // 방향을 계산할 수 없다면
            {
                direction = -target.transform.forward; // 대상의 뒤쪽 방향을 사용한다.
                direction.y = 0.0f; // 높이 차이는 제거한다.
            }

            if (direction.sqrMagnitude < 0.0001f) // 그래도 방향이 없다면
            {
                direction = Vector3.back; // 기본 방향을 사용한다.
            }

            direction.Normalize(); // 방향을 정규화한다.

            Vector3 spawnPosition = target.transform.position + direction * spawnDistanceFromTarget; // 대상 근처 목표 위치를 계산한다.
            spawnPosition = GroundService.ProjectToGround(spawnPosition, consumeTeleportGroundHeight); // 바닥 높이에 맞춰 보정한다.

            transform.position = spawnPosition; // 해츨링을 대상 근처로 순간 이동시킨다.
        }

        public void ConsumeGrowthOrb(EnemyGrowthOrb growthOrb) // 기존 성장 구슬 Script 컴파일 호환용 함수
        {
            if (growthOrb == null) // 구슬 정보가 없다면
            {
                return; // 아무 처리도 하지 않는다.
            }

            // 성장 방식이 일반 몬스터 포식으로 바뀌었기 때문에 구슬로는 성장하지 않는다.
        }

        private void ConsumeOneMonster() // 일반 몬스터 1마리를 먹었을 때 성장 처리
        {
            if (!CanGrow) // 이미 최대 성장 상태라면
            {
                return; // 더 이상 성장하지 않는다.
            }

            growthStack++; // 성장 스택을 1 증가시킨다.

            if (health != null) // 체력 Script Component가 있다면
            {
                health.IncreaseMaxHpByPercentKeepingRatio(maxHpIncreasePercentPerConsume); // 현재 체력 비율을 유지하면서 최대 체력을 증가시킨다.
            }

            ApplyScaleGrowth(); // 현재 성장 스택에 맞게 크기를 갱신한다.
        }

        private void ApplyScaleGrowth() // 성장 스택에 따라 크기를 갱신하는 함수
        {
            float scaleMultiplier = 1.0f + growthStack * scaleIncreasePercentPerConsume; // 성장 스택에 따른 크기 배율을 계산한다.

            transform.localScale = baseScale * scaleMultiplier; // 원래 크기를 기준으로 최종 크기를 적용한다.
        }

        private void FaceEachOther(EnemyController target) // 해츨링과 대상 몬스터가 서로 마주보게 한다.
        {
            FaceTransformToPosition(transform, target.transform.position); // 해츨링이 대상을 바라보게 한다.
            FaceTransformToPosition(target.transform, transform.position); // 대상 몬스터가 해츨링을 바라보게 한다.
        }

        private void FaceTransformToPosition(Transform source, Vector3 targetPosition) // 특정 Transform이 목표 위치를 바라보게 한다.
        {
            if (source == null) // 회전할 Transform이 없다면
            {
                return; // 회전하지 않는다.
            }

            Vector3 direction = targetPosition - source.position; // 바라볼 방향을 계산한다.
            direction.y = 0.0f; // 수평 방향만 사용한다.

            if (direction.sqrMagnitude < 0.0001f) // 방향이 너무 짧다면
            {
                return; // 회전하지 않는다.
            }

            source.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up); // 목표 방향을 바라보게 회전한다.
        }

        private void LockTargetMovement(EnemyController target) // 포식 대상 몬스터의 이동을 멈춘다.
        {
            ReleaseLockedTargetMovement(); // 이전에 멈춘 대상이 있다면 먼저 복구한다.

            if (!stopTargetMovementWhileConsuming) // 대상 이동 정지를 사용하지 않는다면
            {
                return; // 아무것도 멈추지 않는다.
            }

            if (target == null) // 대상이 없다면
            {
                return; // 멈출 수 없다.
            }

            lockedTargetMovement = target.GetComponent<EnemyMovement>(); // 대상 몬스터의 EnemyMovement를 찾는다.

            if (lockedTargetMovement == null) // 대상에 이동 Script가 없다면
            {
                return; // 멈출 이동이 없다.
            }

            lockedTargetMovementWasEnabled = lockedTargetMovement.enabled; // 원래 활성 상태를 저장한다.
            lockedTargetMovement.enabled = false; // 대상 몬스터 이동을 멈춘다.
        }

        private void ReleaseLockedTargetMovement() // 멈춰둔 대상 몬스터 이동을 복구한다.
        {
            if (lockedTargetMovement != null) // 멈춰둔 대상 이동 Script가 아직 있다면
            {
                lockedTargetMovement.enabled = lockedTargetMovementWasEnabled; // 원래 활성 상태로 되돌린다.
            }

            lockedTargetMovement = null; // 대상 이동 참조를 비운다.
            lockedTargetMovementWasEnabled = false; // 대상 이동 상태를 초기화한다.
        }

        private bool TryReserveConsumeTarget(EnemyController target) // 이 해츨링이 특정 몬스터를 먹기로 예약한다.
        {
            if (!IsValidConsumeTarget(target)) // 포식 대상으로 유효하지 않다면
            {
                return false; // 예약하지 않는다.
            }

            CleanupReservedConsumeTargets(); // 예약 전에 죽었거나 사라진 대상들을 정리한다.

            if (reservedConsumeTargets.Contains(target)) // 이미 다른 해츨링이 예약했다면
            {
                return false; // 중복 예약하지 않는다.
            }

            reservedConsumeTargets.Add(target); // 예약 목록에 대상을 추가한다.
            reservedConsumeTarget = target; // 이 해츨링의 현재 예약 대상으로 저장한다.

            return true; // 예약 성공
        }

        private void ReleaseReservedConsumeTarget() // 이 해츨링이 예약한 포식 대상을 해제한다.
        {
            if (reservedConsumeTarget != null) // 예약한 대상이 아직 유효하다면
            {
                reservedConsumeTargets.Remove(reservedConsumeTarget); // 예약 목록에서 제거한다.
            }
            else
            {
                CleanupReservedConsumeTargets(); // 대상이 이미 사라졌다면 예약 목록을 정리한다.
            }

            reservedConsumeTarget = null; // 현재 예약 대상을 비운다.
        }

        private bool IsReservedByOtherHatchling(EnemyController target) // 다른 해츨링이 이미 예약한 대상인지 확인한다.
        {
            if (target == null) // 대상이 없다면
            {
                return false; // 예약 여부를 확인하지 않는다.
            }

            CleanupReservedConsumeTargets(); // 확인 전에 죽었거나 사라진 예약 대상을 정리한다.

            return reservedConsumeTargets.Contains(target) && target != reservedConsumeTarget; // 다른 해츨링의 예약이면 true
        }

        private static void CleanupReservedConsumeTargets() // 죽었거나 사라진 예약 대상을 정리한다.
        {
            reservedConsumeTargets.RemoveWhere(target => target == null || target.IsDead); // 비어 있거나 죽은 대상 제거
        }

        private void SetEnemyMovementEnabled(bool enabled) // 먹기 연출 중 이동 Script를 켜고 끈다.
        {
            if (enemyMovement == null) // EnemyMovement가 없다면
            {
                return; // 이동 제어를 하지 않는다.
            }

            enemyMovement.enabled = enabled; // EnemyMovement 활성 상태를 변경한다.
        }

        private void SetConsumePresentationConsuming(bool consuming) // Bridge에 포식 중 상태를 전달한다.
        {
            if (consumePresentationBridge == null) // Bridge가 없다면
            {
                return; // 처리하지 않는다.
            }

            consumePresentationBridge.SetConsuming(consuming); // Animator IsConsuming Bool을 갱신한다.
        }

        private void ResetConsumePresentationVisualOffset() // Bridge의 VisualOffsetRoot를 원래 높이로 복구한다.
        {
            if (consumePresentationBridge == null) // Bridge가 없다면
            {
                return; // 처리하지 않는다.
            }

            consumePresentationBridge.ResetVisualOffset(); // 모델 높이를 원래대로 복구한다.
        }

        private void ResetConsumePresentationState() // Bridge의 포식 연출 상태를 초기화한다.
        {
            if (consumePresentationBridge == null) // Bridge가 없다면
            {
                return; // 처리하지 않는다.
            }

            consumePresentationBridge.SetConsuming(false); // Animator 포식 상태를 false로 초기화한다.
            consumePresentationBridge.ResetVisualOffset(); // 모델 높이를 원래대로 복구한다.
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position, consumeRange); // Scene에서 선택했을 때 포식 범위를 표시한다.
        }
    }
}