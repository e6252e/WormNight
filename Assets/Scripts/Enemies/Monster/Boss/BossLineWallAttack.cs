using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class BossLineWallAttack : MonoBehaviour
    {
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor"); // URP Material의 기본 색상 Property ID
        private static readonly int ColorProperty = Shader.PropertyToID("_Color"); // Standard Material의 기본 색상 Property ID

        [Header("Prefabs")]
        [SerializeField] private EnemyObstacle obstaclePrefab; // 기존 EnemyObstacle Prefab

        [SerializeField] private GameObject telegraphPrefab; // 기존 장애물 예고 Prefab

        [Header("Maze Wall Count")]
        [Min(1)]
        [SerializeField] private int wallLineCount = 6; // 한 번의 패턴에서 생성할 일자 벽 개수

        [Min(1)]
        [SerializeField] private int minimumObstacleCountPerLine = 4; // 가장 짧은 벽의 장애물 개수

        [Min(1)]
        [SerializeField] private int maximumObstacleCountPerLine = 7; // 가장 긴 벽의 장애물 개수

        [Header("Maze Wall Layout")]
        [Min(0.1f)]
        [SerializeField] private float obstacleSpacing = 2.2f; // 같은 벽에 배치되는 장애물 중심 간격

        [Min(0.1f)]
        [SerializeField] private float obstacleRadius = 1.2f; // 기존 EnemyObstacle의 컨보이 차단 반경

        [Min(0.0f)]
        [SerializeField] private float wallFieldForwardOffset = 5.0f; // 미로 생성 영역을 컨보이 앞쪽으로 이동시키는 거리

        [Min(0.0f)]
        [SerializeField] private float wallCenterForwardRange = 8.0f; // 각 벽 중심의 전후 무작위 범위

        [Min(0.0f)]
        [SerializeField] private float wallCenterSideRange = 10.0f; // 각 벽 중심의 좌우 무작위 범위

        [Min(0.0f)]
        [SerializeField] private float minimumLineCenterDistance = 4.0f; // 서로 다른 벽 중심이 유지해야 하는 최소 거리

        [Min(0.0f)]
        [SerializeField] private float convoySafeRadius = 3.5f; // 컨보이 바로 주변에 장애물이 생성되지 않게 하는 안전 반경

        [Min(1)]
        [SerializeField] private int maximumPlacementAttemptsPerLine = 20; // 벽 하나의 유효한 위치를 찾기 위한 최대 재시도 횟수

        [Header("Ground Height")]
        [Min(0.0f)]
        [SerializeField] private float telegraphGroundHeight = 0.03f; // 예고 표시의 지면 높이

        [Min(0.0f)]
        [SerializeField] private float obstacleGroundHeight = 1.0f; // 실제 장애물 중심의 지면 높이

        [Header("Timing")]
        [Min(0.1f)]
        [SerializeField] private float attackInterval = 8.0f; // 다음 미로 패턴까지의 대기시간

        [Min(0.1f)]
        [SerializeField] private float telegraphDuration = 1.5f; // 장애물 생성 전 예고 시간

        [Min(1.0f)]
        [SerializeField] private float obstacleLifeTime = 6.0f; // 생성된 미로 장애물의 유지시간

        [Min(0.0f)]
        [SerializeField] private float recoveryDuration = 0.5f; // 장애물 생성 후 행동 잠금을 유지할 시간

        [Header("Telegraph")]
        [Range(0.01f, 1.0f)]
        [SerializeField] private float telegraphStartAlpha = 0.07f; // 예고 표시의 시작 투명도

        [Range(0.01f, 1.0f)]
        [SerializeField] private float telegraphEndAlpha = 1.0f; // 예고 표시의 마지막 투명도

        private readonly List<GameObject> activeTelegraphs = new List<GameObject>(64); // 현재 생성된 모든 예고 표시

        private BossController bossController; // 보스 Phase와 행동 잠금을 관리하는 Script Component

        private Transform convoyTarget; // 미로 생성의 기준이 되는 컨보이 Transform

        private Coroutine attackCoroutine; // 현재 실행 중인 미로 생성 Coroutine

        private float nextAttackTime; // 다음 공격이 가능한 시간

        private bool ownsActionLock; // 이 Script가 보스 행동 잠금을 소유하는지 나타내는 값

        public bool IsAttacking { get; private set; } // 현재 미로 생성 패턴이 진행 중인지 나타내는 값

        private void Awake()
        {
            bossController = GetComponent<BossController>(); // 같은 Boss01에서 BossController를 가져온다.
            TryFindConvoyTarget(); // 현재 등록된 컨보이를 찾는다.
        }

        private void Start()
        {
            ScheduleNextAttack(); // 첫 번째 미로 패턴 시간을 예약한다.
        }

        private void Update()
        {
            if (bossController == null || bossController.IsDead) // BossController가 없거나 보스가 사망했다면
            {
                return; // 미로 패턴을 시작하지 않는다.
            }

            if (bossController.CurrentPhase != BossPhase.Aggressive) // 현재 Phase가 Aggressive가 아니라면
            {
                return; // 일자 벽 미로 패턴을 사용하지 않는다.
            }

            if (attackCoroutine != null) // 이미 패턴이 진행 중이라면
            {
                return; // 중복 실행하지 않는다.
            }

            if (bossController.IsActionRunning) // 순간이동이나 다른 보스 패턴이 실행 중이라면
            {
                return; // 동시에 미로를 생성하지 않는다.
            }

            if (Time.time < nextAttackTime) // 공격 간격이 아직 끝나지 않았다면
            {
                return; // 다음 공격 시간까지 기다린다.
            }

            if (!TryFindConvoyTarget()) // 현재 컨보이를 찾지 못했다면
            {
                nextAttackTime = Time.time + 1.0f; // 1초 뒤 다시 시도한다.
                return;
            }

            if (obstaclePrefab == null || telegraphPrefab == null) // 필요한 Prefab이 연결되지 않았다면
            {
                nextAttackTime = Time.time + 1.0f; // 1초 뒤 다시 확인한다.
                return;
            }

            if (!bossController.TryBeginAction()) // 보스 행동 잠금을 얻지 못했다면
            {
                return; // 다른 패턴이 끝날 때까지 기다린다.
            }

            ownsActionLock = true; // 이 Script가 행동 잠금을 소유한다고 저장한다.
            attackCoroutine = StartCoroutine(AttackRoutine()); // 미로 생성 공격을 시작한다.
        }

        private void OnDisable()
        {
            if (attackCoroutine != null) // 실행 중인 Coroutine이 있다면
            {
                StopCoroutine(attackCoroutine); // 현재 공격 Coroutine을 중단한다.
                attackCoroutine = null; // Coroutine 참조를 비운다.
            }

            CleanupTelegraphs(); // 남아 있는 예고 표시를 제거한다.
            IsAttacking = false; // 공격 상태를 해제한다.
            ReleaseActionLock(); // 행동 잠금을 해제한다.
        }

        private IEnumerator AttackRoutine()
        {
            IsAttacking = true; // 미로 생성 공격이 시작됐다고 저장한다.

            if (!TryFindConvoyTarget()) // 공격 시작 시점에 컨보이가 없다면
            {
                FinishAttack(); // 공격 상태를 정리한다.
                yield break;
            }

            List<Vector3> wallPositions = CalculateMazeWallPositions(); // 여러 일자 벽의 모든 장애물 위치를 계산한다.

            if (wallPositions.Count == 0) // 유효한 장애물 위치를 하나도 만들지 못했다면
            {
                FinishAttack(); // 공격 상태를 정리한다.
                yield break;
            }

            SpawnTelegraphs(wallPositions); // 계산된 모든 위치에 예고 표시를 생성한다.

            float timer = 0.0f; // 예고 표시의 진행 시간을 저장한다.

            while (timer < telegraphDuration) // 예고 시간이 끝날 때까지 반복한다.
            {
                if (!CanContinueAttack()) // 예고 도중 보스가 죽거나 Phase가 바뀌었다면
                {
                    FinishAttack(); // 예고와 행동 잠금을 정리한다.
                    yield break;
                }

                timer += Time.deltaTime; // 지난 프레임 시간을 예고 타이머에 더한다.

                float progress = Mathf.Clamp01(timer / telegraphDuration); // 예고 진행도를 계산한다.
                float alpha = Mathf.Lerp(telegraphStartAlpha, telegraphEndAlpha, progress); // 예고 표시가 점점 진해지게 계산한다.

                SetAllTelegraphAlpha(alpha); // 모든 예고 표시의 투명도를 변경한다.

                yield return null; // 다음 프레임까지 기다린다.
            }

            CleanupTelegraphs(); // 실제 장애물 생성 직전에 예고 표시를 제거한다.

            if (!CanContinueAttack()) // 장애물 생성 직전에 패턴을 계속할 수 있는지 확인한다.
            {
                FinishAttack(); // 공격 상태를 정리한다.
                yield break;
            }

            SpawnObstacles(wallPositions); // 계산된 모든 위치에 기존 EnemyObstacle을 생성한다.

            yield return new WaitForSeconds(recoveryDuration); // 짧은 회복시간 동안 행동 잠금을 유지한다.

            FinishAttack(); // 공격 상태와 행동 잠금을 정리한다.
        }

        private List<Vector3> CalculateMazeWallPositions()
        {
            List<Vector3> allPositions = new List<Vector3>(64); // 모든 벽의 장애물 위치를 저장한다.
            List<Vector3> acceptedLineCenters = new List<Vector3>(wallLineCount); // 승인된 벽 중심 위치를 저장한다.

            Vector3 convoyForward = GetPlanarForward(); // 컨보이의 지면 기준 앞 방향을 가져온다.
            Vector3 convoyRight = new Vector3(convoyForward.z, 0.0f, -convoyForward.x); // 컨보이의 지면 기준 오른쪽 방향을 계산한다.

            int[] directionOrder = { 0, 1, 2, 3 }; // 0도·45도·90도·135도 방향 번호를 만든다.
            ShuffleDirectionOrder(directionOrder); // 첫 번째 방향 순서를 무작위로 섞는다.

            int lineCount = Mathf.Max(1, wallLineCount); // 벽 개수가 최소 1개가 되도록 보정한다.
            int minimumCount = Mathf.Max(1, minimumObstacleCountPerLine); // 최소 벽 길이를 1 이상으로 보정한다.
            int maximumCount = Mathf.Max(minimumCount, maximumObstacleCountPerLine); // 최대 벽 길이가 최소 길이보다 작지 않게 보정한다.

            for (int lineIndex = 0; lineIndex < lineCount; lineIndex++) // 생성할 벽 개수만큼 반복한다.
            {
                if (lineIndex > 0 && lineIndex % directionOrder.Length == 0) // 네 방향을 한 번씩 사용했다면
                {
                    ShuffleDirectionOrder(directionOrder); // 다음 네 벽의 방향 순서를 다시 무작위로 섞는다.
                }

                int directionIndex = directionOrder[lineIndex % directionOrder.Length]; // 현재 벽이 사용할 방향 번호를 가져온다.
                Vector3 wallDirection = GetWallDirection(convoyForward, directionIndex); // 실제 월드 기준 벽 방향을 계산한다.
                int obstacleCount = Random.Range(minimumCount, maximumCount + 1); // 현재 벽의 길이를 무작위로 선택한다.

                bool created = TryCreateWallLine(convoyForward, convoyRight, wallDirection, obstacleCount, acceptedLineCenters, out Vector3 wallCenter, out Vector3[] linePositions); // 안전한 벽 위치를 찾는다.

                if (!created) // 최대 재시도 횟수 안에 유효한 위치를 찾지 못했다면
                {
                    continue; // 해당 벽은 건너뛰고 다음 벽을 계산한다.
                }

                acceptedLineCenters.Add(wallCenter); // 승인된 벽 중심을 목록에 등록한다.
                AddUniquePositions(allPositions, linePositions); // 다른 벽과 중복되지 않는 장애물 위치만 추가한다.
            }

            return allPositions; // 모든 미로 장애물 위치를 반환한다.
        }

        private bool TryCreateWallLine(Vector3 convoyForward, Vector3 convoyRight, Vector3 wallDirection, int obstacleCount, List<Vector3> acceptedLineCenters, out Vector3 wallCenter, out Vector3[] linePositions)
        {
            int attempts = Mathf.Max(1, maximumPlacementAttemptsPerLine); // 최대 배치 재시도 횟수를 1 이상으로 보정한다.

            for (int attempt = 0; attempt < attempts; attempt++) // 유효한 벽 위치를 찾을 때까지 반복한다.
            {
                float forwardOffset = wallFieldForwardOffset + Random.Range(-wallCenterForwardRange, wallCenterForwardRange); // 컨보이 앞뒤 방향의 무작위 위치를 계산한다.
                float sideOffset = Random.Range(-wallCenterSideRange, wallCenterSideRange); // 컨보이 좌우 방향의 무작위 위치를 계산한다.

                Vector3 candidateCenter = convoyTarget.position + convoyForward * forwardOffset + convoyRight * sideOffset; // 현재 벽의 후보 중심 위치를 계산한다.
                candidateCenter.y = 0.0f; // 벽 중심 계산을 XZ 평면으로 제한한다.

                if (!IsLineCenterFarEnough(candidateCenter, acceptedLineCenters)) // 기존 벽 중심과 너무 가깝다면
                {
                    continue; // 새로운 무작위 위치를 다시 계산한다.
                }

                Vector3[] candidatePositions = CalculateLinePositions(candidateCenter, wallDirection, obstacleCount); // 현재 후보 벽의 모든 장애물 위치를 계산한다.

                if (!IsLineSafeForConvoy(candidatePositions)) // 벽이 컨보이 바로 위를 지나간다면
                {
                    continue; // 새로운 무작위 위치를 다시 계산한다.
                }

                wallCenter = candidateCenter; // 승인된 벽 중심을 반환값으로 저장한다.
                linePositions = candidatePositions; // 승인된 장애물 위치 배열을 반환값으로 저장한다.
                return true; // 유효한 벽 위치를 찾았다고 반환한다.
            }

            wallCenter = Vector3.zero; // 유효한 벽 위치를 찾지 못했다면 기본값을 저장한다.
            linePositions = null; // 유효한 장애물 위치 배열이 없다고 저장한다.
            return false; // 벽 생성 위치를 찾지 못했다고 반환한다.
        }

        private Vector3[] CalculateLinePositions(Vector3 wallCenter, Vector3 wallDirection, int obstacleCount)
        {
            int count = Mathf.Max(1, obstacleCount); // 장애물 개수가 최소 1개가 되도록 보정한다.
            float centerIndex = (count - 1) * 0.5f; // 벽 중심을 배열의 가운데에 맞추기 위한 값을 계산한다.

            Vector3[] positions = new Vector3[count]; // 현재 벽의 장애물 위치 배열을 생성한다.

            for (int i = 0; i < count; i++) // 현재 벽의 장애물 개수만큼 반복한다.
            {
                float offset = (i - centerIndex) * obstacleSpacing; // 벽 중심에서 현재 장애물까지의 거리를 계산한다.
                Vector3 position = wallCenter + wallDirection * offset; // 벽 방향을 따라 장애물 위치를 계산한다.
                position.y = 0.0f; // 위치 계산을 XZ 평면으로 제한한다.

                positions[i] = position; // 계산된 위치를 배열에 저장한다.
            }

            return positions; // 현재 벽의 모든 장애물 위치를 반환한다.
        }

        private bool IsLineCenterFarEnough(Vector3 candidateCenter, List<Vector3> acceptedLineCenters)
        {
            float minimumDistance = Mathf.Max(0.0f, minimumLineCenterDistance); // 최소 벽 중심 거리를 0 이상으로 보정한다.
            float minimumDistanceSqr = minimumDistance * minimumDistance; // 거리 비교를 위해 제곱값을 계산한다.

            for (int i = 0; i < acceptedLineCenters.Count; i++) // 이미 승인된 모든 벽 중심을 확인한다.
            {
                Vector3 offset = candidateCenter - acceptedLineCenters[i]; // 두 벽 중심 사이의 방향과 거리를 계산한다.
                offset.y = 0.0f; // XZ 평면 거리만 사용한다.

                if (offset.sqrMagnitude < minimumDistanceSqr) // 두 벽 중심이 설정된 최소 거리보다 가깝다면
                {
                    return false; // 현재 벽 중심을 사용하지 않는다.
                }
            }

            return true; // 기존 벽 중심과 충분히 떨어져 있다고 반환한다.
        }

        private bool IsLineSafeForConvoy(Vector3[] linePositions)
        {
            float safeRadius = Mathf.Max(0.0f, convoySafeRadius); // 컨보이 안전 반경을 0 이상으로 보정한다.
            float safeRadiusSqr = safeRadius * safeRadius; // 거리 비교를 위해 제곱값을 계산한다.

            for (int i = 0; i < linePositions.Length; i++) // 현재 벽의 모든 장애물 위치를 확인한다.
            {
                Vector3 offset = linePositions[i] - convoyTarget.position; // 컨보이와 장애물 사이의 거리를 계산한다.
                offset.y = 0.0f; // XZ 평면 거리만 사용한다.

                if (offset.sqrMagnitude < safeRadiusSqr) // 장애물이 컨보이 안전 반경 안에 있다면
                {
                    return false; // 현재 벽 전체를 사용하지 않는다.
                }
            }

            return true; // 현재 벽이 컨보이를 즉시 가두지 않는다고 반환한다.
        }

        private void AddUniquePositions(List<Vector3> allPositions, Vector3[] linePositions)
        {
            float minimumSeparation = Mathf.Max(0.1f, obstacleRadius * 0.75f); // 교차 지점의 장애물 중복 생성을 막기 위한 최소 거리를 계산한다.
            float minimumSeparationSqr = minimumSeparation * minimumSeparation; // 거리 비교를 위해 제곱값을 계산한다.

            for (int positionIndex = 0; positionIndex < linePositions.Length; positionIndex++) // 현재 벽의 모든 위치를 확인한다.
            {
                Vector3 candidatePosition = linePositions[positionIndex]; // 추가할 장애물 후보 위치를 가져온다.
                bool isDuplicate = false; // 기존 장애물 위치와 중복되는지 저장한다.

                for (int existingIndex = 0; existingIndex < allPositions.Count; existingIndex++) // 이미 등록된 모든 장애물 위치를 확인한다.
                {
                    Vector3 offset = candidatePosition - allPositions[existingIndex]; // 두 장애물 위치 사이의 거리를 계산한다.
                    offset.y = 0.0f; // XZ 평면 거리만 사용한다.

                    if (offset.sqrMagnitude < minimumSeparationSqr) // 기존 장애물과 너무 가까운 위치라면
                    {
                        isDuplicate = true; // 중복 위치라고 저장한다.
                        break; // 추가 거리 검사를 중단한다.
                    }
                }

                if (!isDuplicate) // 기존 장애물과 중복되지 않는 위치라면
                {
                    allPositions.Add(candidatePosition); // 전체 미로 장애물 위치 목록에 추가한다.
                }
            }
        }

        private Vector3 GetPlanarForward()
        {
            Vector3 forward = convoyTarget.forward; // 컨보이가 현재 바라보는 방향을 가져온다.
            forward.y = 0.0f; // 지면 방향 계산을 위해 Y축을 제거한다.

            if (forward.sqrMagnitude <= 0.0001f) // 컨보이 방향을 사용할 수 없다면
            {
                forward = transform.forward; // 보스의 앞 방향을 대신 사용한다.
                forward.y = 0.0f; // Y축을 제거한다.
            }

            if (forward.sqrMagnitude <= 0.0001f) // 보스 방향도 사용할 수 없다면
            {
                forward = Vector3.forward; // 월드 기준 앞 방향을 사용한다.
            }

            return forward.normalized; // 길이가 1인 지면 기준 앞 방향을 반환한다.
        }

        private Vector3 GetWallDirection(Vector3 baseDirection, int directionIndex)
        {
            float angle = directionIndex * 45.0f; // 방향 번호를 0도·45도·90도·135도로 변환한다.

            Vector3 wallDirection = Quaternion.AngleAxis(angle, Vector3.up) * baseDirection; // 컨보이 방향을 기준으로 벽 방향을 회전시킨다.
            wallDirection.y = 0.0f; // 벽 방향의 Y축을 제거한다.

            return wallDirection.normalized; // 길이가 1인 최종 벽 방향을 반환한다.
        }

        private void ShuffleDirectionOrder(int[] directionOrder)
        {
            for (int i = directionOrder.Length - 1; i > 0; i--) // 배열의 뒤쪽부터 순회한다.
            {
                int randomIndex = Random.Range(0, i + 1); // 현재 범위 안에서 무작위 위치를 선택한다.
                int temporaryValue = directionOrder[i]; // 현재 값을 임시로 저장한다.

                directionOrder[i] = directionOrder[randomIndex]; // 무작위 위치의 값을 현재 위치로 이동한다.
                directionOrder[randomIndex] = temporaryValue; // 임시로 저장한 값을 무작위 위치로 이동한다.
            }
        }

        private bool TryFindConvoyTarget()
        {
            if (MonsterInteractionApi.TryGetConvoyTarget(out Transform target)) // 등록된 컨보이 Transform이 존재한다면
            {
                convoyTarget = target; // 컨보이 참조를 저장한다.
                return true; // 컨보이를 찾았다고 반환한다.
            }

            convoyTarget = null; // 컨보이를 찾지 못했다면 기존 참조를 비운다.
            return false; // 컨보이를 찾지 못했다고 반환한다.
        }

        private void SpawnTelegraphs(List<Vector3> wallPositions)
        {
            CleanupTelegraphs(); // 이전 공격에서 남은 예고 표시를 먼저 제거한다.

            Transform runtimeRoot = MonsterRuntimeRoot.GetRootOrFallback(transform.parent); // Monsters Runtime Root를 가져온다.

            for (int i = 0; i < wallPositions.Count; i++) // 계산된 모든 장애물 위치를 순회한다.
            {
                Vector3 telegraphPosition = wallPositions[i]; // 현재 장애물 위치를 가져온다.
                telegraphPosition.y = telegraphGroundHeight; // 예고 표시의 지면 높이를 적용한다.

                GameObject telegraph = Instantiate(telegraphPrefab, telegraphPosition, Quaternion.identity, runtimeRoot); // 기존 예고 Prefab을 생성한다.

                Vector3 telegraphScale = telegraph.transform.localScale; // 예고 Prefab의 기존 Scale을 가져온다.
                telegraphScale.x = obstacleRadius * 2.0f; // X 크기를 장애물 지름에 맞춘다.
                telegraphScale.z = obstacleRadius * 2.0f; // Z 크기를 장애물 지름에 맞춘다.
                telegraph.transform.localScale = telegraphScale; // 변경된 크기를 적용한다.

                activeTelegraphs.Add(telegraph); // 생성된 예고 표시를 정리용 목록에 등록한다.
            }

            SetAllTelegraphAlpha(telegraphStartAlpha); // 모든 예고 표시를 시작 투명도로 설정한다.
        }

        private void SpawnObstacles(List<Vector3> wallPositions)
        {
            Transform runtimeRoot = MonsterRuntimeRoot.GetRootOrFallback(transform.parent); // Monsters Runtime Root를 가져온다.

            for (int i = 0; i < wallPositions.Count; i++) // 계산된 모든 장애물 위치를 순회한다.
            {
                Vector3 obstaclePosition = wallPositions[i]; // 현재 장애물 위치를 가져온다.
                obstaclePosition.y = obstacleGroundHeight; // 실제 장애물의 지면 높이를 적용한다.

                EnemyObstacle obstacle = Instantiate(obstaclePrefab, obstaclePosition, Quaternion.identity, runtimeRoot); // 기존 EnemyObstacle을 생성한다.

                obstacle.Configure(obstacleRadius, obstacleLifeTime); // 기존 장애물에 차단 반경과 유지시간을 전달한다.
            }
        }

        private void SetAllTelegraphAlpha(float alpha)
        {
            for (int i = 0; i < activeTelegraphs.Count; i++) // 현재 생성된 모든 예고 표시를 순회한다.
            {
                SetTelegraphAlpha(activeTelegraphs[i], alpha); // 현재 예고 표시의 투명도를 변경한다.
            }
        }

        private void SetTelegraphAlpha(GameObject telegraph, float alpha)
        {
            if (telegraph == null) // 예고 표시가 이미 제거됐다면
            {
                return; // Material을 수정하지 않는다.
            }

            Renderer[] renderers = telegraph.GetComponentsInChildren<Renderer>(true); // 예고 표시와 자식의 모든 Renderer를 가져온다.

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++) // 모든 Renderer를 순회한다.
            {
                Material[] materials = renderers[rendererIndex].materials; // 현재 Renderer의 Material 인스턴스를 가져온다.

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
                        color.a = alpha; // Alpha 값만 변경한다.
                        material.SetColor(BaseColorProperty, color); // 변경된 색상을 적용한다.
                    }

                    if (material.HasProperty(ColorProperty)) // Standard 기본 색상 Property가 있다면
                    {
                        Color color = material.GetColor(ColorProperty); // 기존 색상을 가져온다.
                        color.a = alpha; // Alpha 값만 변경한다.
                        material.SetColor(ColorProperty, color); // 변경된 색상을 적용한다.
                    }
                }
            }
        }

        private bool CanContinueAttack()
        {
            if (bossController == null || bossController.IsDead) // BossController가 없거나 보스가 사망했다면
            {
                return false; // 패턴을 계속할 수 없다.
            }

            if (bossController.CurrentPhase != BossPhase.Aggressive) // Aggressive Phase가 끝났다면
            {
                return false; // 실제 장애물을 생성하지 않는다.
            }

            if (convoyTarget == null || !convoyTarget.gameObject.activeInHierarchy) // 컨보이 참조가 없거나 비활성화됐다면
            {
                return TryFindConvoyTarget(); // 컨보이를 다시 찾는다.
            }

            return true; // 미로 생성 패턴을 계속할 수 있다고 반환한다.
        }

        private void CleanupTelegraphs()
        {
            for (int i = 0; i < activeTelegraphs.Count; i++) // 생성된 모든 예고 표시를 순회한다.
            {
                if (activeTelegraphs[i] != null) // 현재 예고 표시가 존재한다면
                {
                    Destroy(activeTelegraphs[i]); // 예고 표시를 제거한다.
                }
            }

            activeTelegraphs.Clear(); // 예고 표시 목록을 비운다.
        }

        private void ScheduleNextAttack()
        {
            nextAttackTime = Time.time + attackInterval; // 현재 시간에 공격 간격을 더해 다음 공격 시간을 예약한다.
        }

        private void FinishAttack()
        {
            CleanupTelegraphs(); // 남아 있는 예고 표시를 제거한다.
            IsAttacking = false; // 공격 상태를 해제한다.
            ReleaseActionLock(); // 보스 행동 잠금을 해제한다.
            ScheduleNextAttack(); // 다음 미로 생성 시간을 예약한다.
            attackCoroutine = null; // 현재 Coroutine 참조를 비운다.
        }

        private void ReleaseActionLock()
        {
            if (!ownsActionLock) // 이 Script가 행동 잠금을 가지고 있지 않다면
            {
                return; // 다른 패턴의 행동 잠금에 영향을 주지 않는다.
            }

            if (bossController != null) // BossController가 존재한다면
            {
                bossController.EndAction(); // 보스 행동 잠금을 해제한다.
            }

            ownsActionLock = false; // 행동 잠금 소유 상태를 해제한다.
        }
    }
}