using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class BossSummonAttack : MonoBehaviour // Aggressive Phase에서 일반 몬스터를 소환하는 보스 패턴
    {
        [System.Serializable]
        private sealed class SummonEntry // 소환할 몬스터 Prefab과 개수를 묶어서 Inspector에 표시하는 데이터
        {
            [SerializeField] private EnemyController prefab; // 소환할 기존 몬스터 Prefab

            [Min(0)]
            [SerializeField] private int count = 1; // 한 번의 패턴에서 이 몬스터를 소환할 개수

            public SummonEntry()
            {
            }

            public SummonEntry(EnemyController prefab, int count)
            {
                this.prefab = prefab;
                this.count = Mathf.Max(0, count);
            }

            public EnemyController Prefab
            {
                get
                {
                    return prefab; // 외부에서 설정된 몬스터 Prefab을 읽을 수 있도록 반환한다.
                }
            }

            public int Count
            {
                get
                {
                    return count; // 외부에서 설정된 소환 개수를 읽을 수 있도록 반환한다.
                }
            }
        }

        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor"); // URP Material의 기본 색상 Property ID
        private static readonly int ColorProperty = Shader.PropertyToID("_Color"); // Standard Material의 기본 색상 Property ID

        [Header("Summon Monsters")]
        [SerializeField] private SummonEntry[] summonEntries; // 소환할 몬스터 종류와 개수 목록

        [Min(1)]
        [SerializeField] private int maximumSummonsPerCast = 8; // 한 번의 소환 패턴에서 생성할 최대 몬스터 수

        [Min(1)]
        [SerializeField] private int maximumActiveMonsters = 120; // 보스 소환을 포함해 허용할 최대 활성 몬스터 수

        [Header("Summon Position")]
        [Min(0.0f)]
        [SerializeField] private float minimumSummonRadius = 4.0f; // Boss01 중심에서 가장 가까운 소환 거리

        [Min(0.1f)]
        [SerializeField] private float maximumSummonRadius = 8.0f; // Boss01 중심에서 가장 먼 소환 거리

        [Min(0.1f)]
        [SerializeField] private float minimumSpawnSeparation = 2.0f; // 소환 위치끼리 유지해야 하는 최소 간격

        [Min(0.0f)]
        [SerializeField] private float nexusSafeRadius = 4.0f; // Nexus 바로 위에 몬스터가 생성되지 않도록 하는 안전거리

        [Min(1)]
        [SerializeField] private int maximumPlacementAttempts = 20; // 몬스터 하나의 소환 위치를 찾기 위한 최대 재시도 횟수

        [Min(0.0f)]
        [SerializeField] private float spawnGroundHeight = 0.72f; // 생성된 몬스터의 바닥 기준 높이

        [Header("Telegraph")]
        [SerializeField] private GameObject summonTelegraphPrefab; // 몬스터가 생성될 위치를 표시하는 예고 Prefab

        [Min(0.0f)]
        [SerializeField] private float telegraphGroundHeight = 0.03f; // 예고 표시가 지면에 묻히지 않도록 적용할 높이

        [Min(0.1f)]
        [SerializeField] private float telegraphRadius = 1.2f; // 소환 예고 표시의 반지름

        [Range(0.01f, 1.0f)]
        [SerializeField] private float telegraphStartAlpha = 0.07f; // 예고 표시가 처음 생성됐을 때의 투명도

        [Range(0.01f, 1.0f)]
        [SerializeField] private float telegraphEndAlpha = 1.0f; // 몬스터 소환 직전 예고 표시의 투명도

        [Header("Timing")]
        [Min(0.1f)]
        [SerializeField] private float attackInterval = 11.0f; // 다음 소환 패턴을 사용할 때까지의 대기시간

        [Min(0.1f)]
        [SerializeField] private float windupDuration = 1.5f; // 예고 표시 후 실제 소환까지 기다리는 시간

        [Min(0.0f)]
        [SerializeField] private float summonInterval = 0.12f; // 각 몬스터를 순차적으로 생성할 때의 시간 간격

        [Min(0.0f)]
        [SerializeField] private float recoveryDuration = 0.6f; // 소환이 끝난 뒤 다른 보스 행동을 허용하기까지의 시간

        private readonly List<EnemyController> plannedPrefabs = new List<EnemyController>(16); // 이번 공격에서 생성할 몬스터 Prefab 목록

        private readonly List<Vector3> plannedPositions = new List<Vector3>(16); // 이번 공격에서 사용할 소환 위치 목록

        private readonly List<GameObject> activeTelegraphs = new List<GameObject>(16); // 현재 생성되어 있는 소환 예고 표시 목록

        private BossController bossController; // 보스 Phase와 행동 잠금을 관리하는 Script Component

        private Transform nexus; // 소환된 몬스터가 바라볼 Nexus_Core Transform

        private Coroutine attackCoroutine; // 현재 실행 중인 소환 공격 Coroutine

        private float nextAttackTime; // 다음 소환 공격을 시작할 수 있는 시간

        private int summonSerial; // 보스가 소환한 몬스터 이름에 붙일 일련번호

        private bool ownsActionLock; // 이 Script가 BossController의 행동 잠금을 소유하는지 나타내는 값

        public bool IsAttacking { get; private set; } // 현재 소환 패턴이 실행 중인지 외부에서 확인하는 값

        public void ApplyRuntimeSummonProfile(EnemyController[] prefabs, int totalSummonCount, int maxActiveCount)
        {
            summonEntries = BuildRuntimeSummonEntries(prefabs, totalSummonCount);
            maximumSummonsPerCast = Mathf.Max(1, totalSummonCount);
            maximumActiveMonsters = Mathf.Max(maximumSummonsPerCast, maxActiveCount);
        }

        private static SummonEntry[] BuildRuntimeSummonEntries(EnemyController[] prefabs, int totalSummonCount)
        {
            if (prefabs == null || prefabs.Length == 0 || totalSummonCount <= 0)
            {
                return System.Array.Empty<SummonEntry>();
            }

            List<EnemyController> validPrefabs = new List<EnemyController>();

            for (int i = 0; i < prefabs.Length; i++)
            {
                EnemyController prefab = prefabs[i];

                if (prefab != null && !ContainsPrefab(validPrefabs, prefab))
                {
                    validPrefabs.Add(prefab);
                }
            }

            if (validPrefabs.Count == 0)
            {
                return System.Array.Empty<SummonEntry>();
            }

            int entryCount = Mathf.Min(validPrefabs.Count, Mathf.Max(1, totalSummonCount));
            SummonEntry[] entries = new SummonEntry[entryCount];
            int baseCount = totalSummonCount / entryCount;
            int remainder = totalSummonCount % entryCount;

            for (int i = 0; i < entryCount; i++)
            {
                entries[i] = new SummonEntry(validPrefabs[i], baseCount + (i < remainder ? 1 : 0));
            }

            return entries;
        }

        private static bool ContainsPrefab(List<EnemyController> prefabs, EnemyController target)
        {
            for (int i = 0; i < prefabs.Count; i++)
            {
                if (prefabs[i] == target)
                {
                    return true;
                }
            }

            return false;
        }

        private void Awake()
        {
            bossController = GetComponent<BossController>(); // 같은 Boss01에 붙어 있는 BossController를 가져온다.
            FindNexus(); // 씬에서 Nexus_Core를 찾아 저장한다.
        }

        private void Start()
        {
            ScheduleNextAttack(); // 보스 생성 후 첫 번째 소환 공격 시간을 예약한다.
        }

        private void Update()
        {
            if (bossController == null || bossController.IsDead) // BossController가 없거나 보스가 사망했다면
            {
                return; // 새로운 소환 공격을 시작하지 않는다.
            }

            if (bossController.CurrentPhase != BossPhase.Aggressive) // 현재 Phase가 Aggressive가 아니라면
            {
                return; // 소환 패턴을 사용하지 않는다.
            }

            if (attackCoroutine != null) // 이미 소환 공격이 진행 중이라면
            {
                return; // 중복으로 소환 공격을 시작하지 않는다.
            }

            if (bossController.IsActionRunning) // 순간이동이나 미로 벽 등 다른 패턴이 실행 중이라면
            {
                return; // 동시에 소환 패턴을 시작하지 않는다.
            }

            if (Time.time < nextAttackTime) // 다음 공격 시간이 아직 되지 않았다면
            {
                return; // 공격 간격이 끝날 때까지 기다린다.
            }

            if (!HasValidSummonEntry()) // 유효한 소환 몬스터가 하나도 설정되지 않았다면
            {
                nextAttackTime = Time.time + 1.0f; // 1초 뒤 다시 확인하도록 시간을 예약한다.
                return; // 소환 패턴을 시작하지 않는다.
            }

            if (summonTelegraphPrefab == null) // 소환 예고 Prefab이 연결되지 않았다면
            {
                nextAttackTime = Time.time + 1.0f; // 1초 뒤 다시 확인하도록 시간을 예약한다.
                return; // 소환 패턴을 시작하지 않는다.
            }

            if (!bossController.TryBeginAction()) // 보스 행동 잠금을 얻지 못했다면
            {
                return; // 다른 패턴이 먼저 실행 중이므로 기다린다.
            }

            ownsActionLock = true; // 이 Script가 행동 잠금을 소유한다고 저장한다.
            attackCoroutine = StartCoroutine(AttackRoutine()); // 몬스터 소환 공격 Coroutine을 시작한다.
        }

        private void OnDisable()
        {
            if (attackCoroutine != null) // 실행 중인 소환 Coroutine이 있다면
            {
                StopCoroutine(attackCoroutine); // 현재 소환 Coroutine을 중단한다.
                attackCoroutine = null; // Coroutine 참조를 비운다.
            }

            CleanupTelegraphs(); // 남아 있는 소환 예고 표시를 모두 제거한다.
            ClearSummonPlan(); // 남아 있는 소환 계획 데이터를 비운다.

            IsAttacking = false; // 공격 진행 상태를 해제한다.
            ReleaseActionLock(); // 이 Script가 소유한 보스 행동 잠금을 해제한다.
        }

        private IEnumerator AttackRoutine() // 소환 위치 계산부터 실제 몬스터 생성까지 처리하는 전체 공격 흐름
        {
            IsAttacking = true; // 소환 공격이 시작됐다고 저장한다.

            if (!BuildSummonPlan()) // 활성 몬스터 제한이나 위치 문제로 소환 계획을 만들 수 없다면
            {
                FinishAttack(); // 공격 상태를 정리한다.
                yield break; // 몬스터를 생성하지 않고 종료한다.
            }

            SpawnTelegraphs(); // 계산된 모든 소환 위치에 예고 표시를 생성한다.

            float timer = 0.0f; // 예고 표시가 유지된 시간을 저장한다.

            while (timer < windupDuration) // 설정된 준비시간이 끝날 때까지 반복한다.
            {
                if (!CanContinueAttack()) // 준비 도중 보스가 사망하거나 Phase가 바뀌었다면
                {
                    FinishAttack(); // 예고와 행동 잠금을 정리한다.
                    yield break; // 실제 몬스터를 생성하지 않고 종료한다.
                }

                timer += Time.deltaTime; // 지난 프레임 시간을 준비 타이머에 더한다.

                float progress = Mathf.Clamp01(timer / windupDuration); // 소환 준비 진행도를 0에서 1 사이로 계산한다.
                float alpha = Mathf.Lerp(telegraphStartAlpha, telegraphEndAlpha, progress); // 예고 표시가 점점 진해지도록 투명도를 계산한다.

                SetAllTelegraphAlpha(alpha); // 모든 소환 예고 표시의 투명도를 변경한다.

                yield return null; // 다음 프레임까지 기다린다.
            }

            CleanupTelegraphs(); // 실제 몬스터를 생성하기 직전에 예고 표시를 제거한다.

            for (int i = 0; i < plannedPrefabs.Count; i++) // 계획된 모든 몬스터를 순회한다.
            {
                if (!CanContinueAttack()) // 소환 도중 보스가 사망하거나 Aggressive Phase가 끝났다면
                {
                    FinishAttack(); // 남은 소환 계획과 행동 잠금을 정리한다.
                    yield break; // 남은 몬스터를 생성하지 않고 종료한다.
                }

                if (EnemyController.ActiveCount >= maximumActiveMonsters) // 활성 몬스터 제한에 도달했다면
                {
                    break; // 더 이상 몬스터를 생성하지 않는다.
                }

                SpawnMonster(plannedPrefabs[i], plannedPositions[i]); // 현재 Prefab을 계산된 위치에 생성한다.

                if (i < plannedPrefabs.Count - 1 && summonInterval > 0.0f) // 마지막 몬스터가 아니고 생성 간격이 있다면
                {
                    yield return new WaitForSeconds(summonInterval); // 다음 몬스터를 생성하기 전까지 기다린다.
                }
            }

            yield return new WaitForSeconds(recoveryDuration); // 소환 종료 후 짧은 회복시간 동안 행동 잠금을 유지한다.

            FinishAttack(); // 소환 공격 상태를 종료하고 다음 공격 시간을 예약한다.
        }

        private bool BuildSummonPlan() // 이번 소환 공격에서 사용할 Prefab과 위치 목록을 만드는 함수
        {
            ClearSummonPlan(); // 이전 공격에서 남은 소환 계획을 먼저 제거한다.

            int availableCapacity = Mathf.Max(0, maximumActiveMonsters - EnemyController.ActiveCount); // 현재 추가로 생성할 수 있는 몬스터 수를 계산한다.
            int remainingSummonCount = Mathf.Min(Mathf.Max(1, maximumSummonsPerCast), availableCapacity); // 이번 공격에서 실제로 소환할 최대 수를 계산한다.

            if (remainingSummonCount <= 0) // 추가로 생성할 수 있는 몬스터가 없다면
            {
                return false; // 소환 계획을 만들지 않는다.
            }

            if (nexus == null) // Nexus 참조가 없다면
            {
                FindNexus(); // 씬에서 Nexus_Core를 다시 찾는다.
            }

            for (int entryIndex = 0; entryIndex < summonEntries.Length; entryIndex++) // 설정된 모든 소환 항목을 순회한다.
            {
                SummonEntry entry = summonEntries[entryIndex]; // 현재 소환 항목을 가져온다.

                if (entry == null || entry.Prefab == null || entry.Count <= 0) // 유효하지 않은 항목이라면
                {
                    continue; // 현재 항목을 건너뛴다.
                }

                for (int countIndex = 0; countIndex < entry.Count; countIndex++) // 현재 몬스터의 설정된 소환 개수만큼 반복한다.
                {
                    if (remainingSummonCount <= 0) // 이번 공격의 최대 소환 수에 도달했다면
                    {
                        return plannedPrefabs.Count > 0; // 만들어진 소환 계획이 있는지 반환한다.
                    }

                    if (!TryFindSummonPosition(out Vector3 spawnPosition)) // 유효한 소환 위치를 찾지 못했다면
                    {
                        continue; // 현재 몬스터 한 마리의 소환을 건너뛴다.
                    }

                    plannedPrefabs.Add(entry.Prefab); // 생성할 몬스터 Prefab을 계획 목록에 추가한다.
                    plannedPositions.Add(spawnPosition); // 계산된 소환 위치를 계획 목록에 추가한다.

                    remainingSummonCount--; // 이번 공격에서 남은 소환 가능 수를 감소시킨다.
                }
            }

            return plannedPrefabs.Count > 0; // 실제로 소환할 몬스터가 하나 이상 계획됐는지 반환한다.
        }

        private bool TryFindSummonPosition(out Vector3 spawnPosition) // 보스 주변에서 다른 소환 위치와 겹치지 않는 위치를 찾는 함수
        {
            int attempts = Mathf.Max(1, maximumPlacementAttempts); // 위치 계산 재시도 횟수를 최소 1 이상으로 보정한다.

            float minimumRadius = Mathf.Min(minimumSummonRadius, maximumSummonRadius); // 두 거리 중 작은 값을 최소 소환 반경으로 사용한다.
            float maximumRadius = Mathf.Max(minimumSummonRadius, maximumSummonRadius); // 두 거리 중 큰 값을 최대 소환 반경으로 사용한다.

            for (int attempt = 0; attempt < attempts; attempt++) // 유효한 위치를 찾을 때까지 반복한다.
            {
                Vector2 randomDirection = Random.insideUnitCircle; // XZ 평면에서 사용할 무작위 방향을 선택한다.

                if (randomDirection.sqrMagnitude <= 0.0001f) // 무작위 방향의 길이가 너무 작다면
                {
                    randomDirection = Vector2.right; // 기본 오른쪽 방향을 사용한다.
                }

                randomDirection.Normalize(); // 무작위 방향의 길이를 1로 만든다.

                float distance = Random.Range(minimumRadius, maximumRadius); // 보스에서 떨어질 소환 거리를 무작위로 선택한다.

                Vector3 candidatePosition = transform.position + new Vector3(randomDirection.x, 0.0f, randomDirection.y) * distance; // 보스 주변의 후보 소환 위치를 계산한다.
                candidatePosition = GroundService.ProjectToGround(candidatePosition, spawnGroundHeight); // 기존 스폰 시스템과 동일하게 바닥 높이에 맞춘다.

                if (!IsFarEnoughFromPlannedPositions(candidatePosition)) // 이미 계산된 다른 소환 위치와 너무 가깝다면
                {
                    continue; // 새로운 위치를 다시 계산한다.
                }

                if (!IsFarEnoughFromNexus(candidatePosition)) // Nexus 바로 위나 너무 가까운 위치라면
                {
                    continue; // 새로운 위치를 다시 계산한다.
                }

                spawnPosition = candidatePosition; // 유효한 소환 위치를 반환값에 저장한다.
                return true; // 소환 위치를 찾았다고 반환한다.
            }

            spawnPosition = Vector3.zero; // 유효한 소환 위치를 찾지 못했다면 기본값을 저장한다.
            return false; // 소환 위치를 찾지 못했다고 반환한다.
        }

        private bool IsFarEnoughFromPlannedPositions(Vector3 candidatePosition) // 이미 계산된 소환 위치와 충분히 떨어져 있는지 확인하는 함수
        {
            float separation = Mathf.Max(0.1f, minimumSpawnSeparation); // 최소 소환 간격을 0.1 이상으로 보정한다.
            float separationSqr = separation * separation; // 거리 비교를 위해 최소 간격의 제곱값을 계산한다.

            for (int i = 0; i < plannedPositions.Count; i++) // 이미 승인된 모든 소환 위치를 순회한다.
            {
                Vector3 offset = candidatePosition - plannedPositions[i]; // 두 소환 위치 사이의 방향과 거리를 계산한다.
                offset.y = 0.0f; // XZ 평면 거리만 비교하도록 Y축을 제거한다.

                if (offset.sqrMagnitude < separationSqr) // 두 소환 위치가 설정된 최소 간격보다 가깝다면
                {
                    return false; // 현재 후보 위치를 사용하지 않는다.
                }
            }

            return true; // 기존 소환 위치들과 충분히 떨어져 있다고 반환한다.
        }

        private bool IsFarEnoughFromNexus(Vector3 candidatePosition) // 소환 위치가 Nexus 안전거리 밖인지 확인하는 함수
        {
            if (nexus == null) // Nexus 참조가 없다면
            {
                return true; // Nexus 거리 제한 없이 현재 위치를 허용한다.
            }

            float safeRadius = Mathf.Max(0.0f, nexusSafeRadius); // Nexus 안전거리를 0 이상으로 보정한다.
            Vector3 offset = candidatePosition - nexus.position; // 후보 위치와 Nexus 사이의 방향과 거리를 계산한다.
            offset.y = 0.0f; // XZ 평면 거리만 비교하도록 Y축을 제거한다.

            return offset.sqrMagnitude >= safeRadius * safeRadius; // Nexus 안전거리 밖에 있는지 반환한다.
        }

        private void SpawnMonster(EnemyController prefab, Vector3 spawnPosition) // 기존 몬스터 Prefab을 Monsters 아래에 생성하는 함수
        {
            if (prefab == null) // 생성할 Prefab이 없다면
            {
                return; // 몬스터를 생성하지 않는다.
            }

            Transform runtimeRoot = MonsterRuntimeRoot.GetRootOrFallback(transform.parent); // Monsters를 찾고 없으면 Boss01의 부모를 사용한다.

            Quaternion spawnRotation = GetSpawnRotation(spawnPosition); // 소환 위치에서 Nexus를 바라보는 회전값을 계산한다.

            EnemyController summonedMonster = Instantiate(prefab, spawnPosition, spawnRotation, runtimeRoot); // 기존 몬스터 Prefab을 Monsters 아래에 생성한다.

            summonedMonster.name = $"{prefab.name}_BossSummon_{++summonSerial:000}"; // 보스가 소환한 몬스터임을 확인할 수 있도록 이름을 지정한다.
        }

        private Quaternion GetSpawnRotation(Vector3 spawnPosition) // 소환된 몬스터가 Nexus를 바라보도록 회전값을 계산하는 함수
        {
            if (nexus == null) // Nexus 참조가 없다면
            {
                return transform.rotation; // Boss01의 현재 회전값을 사용한다.
            }

            Vector3 direction = nexus.position - spawnPosition; // 소환 위치에서 Nexus까지의 방향을 계산한다.
            direction.y = 0.0f; // 몬스터가 위아래로 기울어지지 않도록 Y축을 제거한다.

            if (direction.sqrMagnitude <= 0.0001f) // Nexus 방향을 정상적으로 계산할 수 없다면
            {
                return transform.rotation; // Boss01의 현재 회전값을 사용한다.
            }

            return Quaternion.LookRotation(direction.normalized, Vector3.up); // Nexus를 바라보는 회전값을 반환한다.
        }

        private bool HasValidSummonEntry() // 유효한 소환 몬스터 설정이 하나 이상 있는지 확인하는 함수
        {
            if (summonEntries == null || summonEntries.Length == 0) // 소환 목록이 없거나 비어 있다면
            {
                return false; // 소환 가능한 몬스터가 없다고 반환한다.
            }

            for (int i = 0; i < summonEntries.Length; i++) // 모든 소환 항목을 순회한다.
            {
                SummonEntry entry = summonEntries[i]; // 현재 소환 항목을 가져온다.

                if (entry != null && entry.Prefab != null && entry.Count > 0) // Prefab과 개수가 유효하다면
                {
                    return true; // 소환 가능한 몬스터가 있다고 반환한다.
                }
            }

            return false; // 유효한 소환 항목을 찾지 못했다고 반환한다.
        }

        private bool CanContinueAttack() // 현재 소환 공격을 계속 실행할 수 있는지 확인하는 함수
        {
            if (bossController == null || bossController.IsDead) // BossController가 없거나 보스가 사망했다면
            {
                return false; // 소환 공격을 계속할 수 없다.
            }

            if (bossController.CurrentPhase != BossPhase.Aggressive) // 소환 도중 Aggressive Phase가 끝났다면
            {
                return false; // 남은 몬스터를 소환하지 않는다.
            }

            return true; // 소환 공격을 계속할 수 있다고 반환한다.
        }

        private void SpawnTelegraphs() // 계획된 모든 소환 위치에 예고 표시를 생성하는 함수
        {
            CleanupTelegraphs(); // 이전 공격에서 남은 예고 표시를 먼저 제거한다.

            Transform runtimeRoot = MonsterRuntimeRoot.GetRootOrFallback(transform.parent); // Monsters Runtime Root를 가져온다.

            for (int i = 0; i < plannedPositions.Count; i++) // 계획된 모든 소환 위치를 순회한다.
            {
                Vector3 telegraphPosition = plannedPositions[i]; // 현재 소환 위치를 가져온다.
                telegraphPosition.y = telegraphGroundHeight; // 예고 표시 전용 지면 높이를 적용한다.

                GameObject telegraph = Instantiate(summonTelegraphPrefab, telegraphPosition, Quaternion.identity, runtimeRoot); // Monsters 아래에 소환 예고 Prefab을 생성한다.

                Vector3 telegraphScale = telegraph.transform.localScale; // 예고 Prefab의 기존 Scale을 가져온다.
                telegraphScale.x = telegraphRadius * 2.0f; // 예고 표시의 X 크기를 설정된 지름에 맞춘다.
                telegraphScale.z = telegraphRadius * 2.0f; // 예고 표시의 Z 크기를 설정된 지름에 맞춘다.
                telegraph.transform.localScale = telegraphScale; // 계산된 예고 표시 크기를 적용한다.

                activeTelegraphs.Add(telegraph); // 생성된 예고 표시를 정리용 목록에 등록한다.
            }

            SetAllTelegraphAlpha(telegraphStartAlpha); // 생성된 모든 예고 표시의 시작 투명도를 설정한다.
        }

        private void SetAllTelegraphAlpha(float alpha) // 현재 생성된 모든 소환 예고 표시의 투명도를 변경하는 함수
        {
            for (int i = 0; i < activeTelegraphs.Count; i++) // 모든 소환 예고 표시를 순회한다.
            {
                SetTelegraphAlpha(activeTelegraphs[i], alpha); // 현재 예고 표시의 투명도를 변경한다.
            }
        }

        private void SetTelegraphAlpha(GameObject telegraph, float alpha) // 소환 예고 표시 하나의 투명도를 변경하는 함수
        {
            if (telegraph == null) // 예고 표시가 이미 제거됐다면
            {
                return; // Material을 수정하지 않는다.
            }

            Renderer[] renderers = telegraph.GetComponentsInChildren<Renderer>(true); // 예고 표시와 모든 자식의 Renderer를 가져온다.

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++) // 모든 Renderer를 순회한다.
            {
                Material[] materials = renderers[rendererIndex].materials; // 현재 Renderer가 사용하는 Material 인스턴스를 가져온다.

                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++) // 현재 Renderer의 모든 Material을 순회한다.
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

        private void CleanupTelegraphs() // 현재 생성된 모든 소환 예고 표시를 제거하는 함수
        {
            for (int i = 0; i < activeTelegraphs.Count; i++) // 모든 소환 예고 표시를 순회한다.
            {
                if (activeTelegraphs[i] != null) // 현재 예고 표시가 아직 존재한다면
                {
                    Destroy(activeTelegraphs[i]); // 예고 표시 GameObject를 제거한다.
                }
            }

            activeTelegraphs.Clear(); // 제거된 예고 표시 참조를 목록에서 모두 비운다.
        }

        private void ClearSummonPlan() // 현재 저장된 소환 계획을 제거하는 함수
        {
            plannedPrefabs.Clear(); // 생성할 몬스터 Prefab 목록을 비운다.
            plannedPositions.Clear(); // 몬스터 소환 위치 목록을 비운다.
        }

        private void FindNexus() // 씬에서 Nexus_Core를 자동으로 찾는 함수
        {
            GameObject nexusObject = GameObject.Find("Nexus_Core"); // 이름이 Nexus_Core인 GameObject를 찾는다.

            nexus = nexusObject != null ? nexusObject.transform : null; // 찾았다면 Transform을 저장하고 없으면 null을 저장한다.
        }

        private void ScheduleNextAttack() // 다음 소환 공격 시간을 예약하는 함수
        {
            nextAttackTime = Time.time + attackInterval; // 현재 시간에 설정된 공격 간격을 더한다.
        }

        private void FinishAttack() // 소환 공격 상태를 정리하는 함수
        {
            CleanupTelegraphs(); // 남아 있을 수 있는 소환 예고 표시를 제거한다.
            ClearSummonPlan(); // 현재 소환 계획 데이터를 비운다.

            IsAttacking = false; // 공격 진행 상태를 해제한다.
            ReleaseActionLock(); // 다른 보스 패턴이 실행될 수 있도록 행동 잠금을 해제한다.
            ScheduleNextAttack(); // 다음 소환 공격 시간을 예약한다.

            attackCoroutine = null; // 현재 공격 Coroutine 참조를 비운다.
        }

        private void ReleaseActionLock() // 이 Script가 소유한 BossController 행동 잠금을 해제하는 함수
        {
            if (!ownsActionLock) // 이 Script가 행동 잠금을 가지고 있지 않다면
            {
                return; // 다른 패턴의 행동 잠금에 영향을 주지 않는다.
            }

            if (bossController != null) // BossController가 존재한다면
            {
                bossController.EndAction(); // 보스 행동 잠금을 해제한다.
            }

            ownsActionLock = false; // 행동 잠금을 더 이상 소유하지 않는다고 저장한다.
        }
    }
}
