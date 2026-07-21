using UnityEngine;
using System.Collections.Generic;

namespace TeamProject01.Gameplay
{
    public sealed class EnemySpawner : MonoBehaviour // 몬스터 스폰 전체 관리자
    {
        private enum SpawnDirection // 스폰 방향 구분
        {
            Front, // 앞쪽 게이트
            Back, // 뒤쪽 게이트
            Left, // 왼쪽 게이트
            Right, // 오른쪽 게이트
            FrontLeft, // 앞왼쪽 대각선 게이트
            FrontRight, // 앞오른쪽 대각선 게이트
            BackLeft, // 뒤왼쪽 대각선 게이트
            BackRight // 뒤오른쪽 대각선 게이트
        }

        private const int CardinalSpawnDirectionCount = 4; // 기존 Stage Rules가 사용하던 십자 4방향 개수
        private const int AllSpawnDirectionCount = 8; // 외부 WaveController가 사용할 수 있는 최대 8방향 개수

        [System.Serializable]
        private sealed class MonsterSpawnEntry // 한 번 소환할 몬스터 종류와 개수
        {
            [SerializeField] private EnemyController prefab; // 생성할 몬스터 Prefab

            [Min(0)]
            [SerializeField] private int count = 1; // 한 번 소환할 때 이 몬스터를 몇 마리 만들지

            public EnemyController Prefab
            {
                get
                {
                    return prefab;
                }
            }

            public int Count
            {
                get
                {
                    return count;
                }
            }
        }

        [System.Serializable]
        private sealed class StageSpawnRule // 누적형 단계별 스폰 규칙
        {
            [SerializeField] private string stageName = "Stage 1"; // Inspector에서 구분하기 위한 이름

            [Min(0.0f)]
            [SerializeField] private float startTime = 0.0f; // 게임 시작 후 몇 초부터 이 규칙이 켜질지

            [Min(0.1f)]
            [SerializeField] private float spawnInterval = 8.0f; // 이 규칙이 켜진 뒤 몇 초마다 군단을 반복 소환할지

            [Min(1)]
            [SerializeField] private int spawnGroupCount = 1; // 한 번 소환할 때 몇 개의 게이트에서 군단을 만들지

            [Min(1)]
            [SerializeField] private int frontRowCount = 3; // 이 단계에서 한 줄에 최대 몇 마리까지 배치할지

            [SerializeField] private MonsterSpawnEntry[] monsterEntries; // 이 규칙으로 한 번 소환할 몬스터 조합

            public string StageName
            {
                get
                {
                    return stageName;
                }
            }

            public float StartTime
            {
                get
                {
                    return startTime;
                }
            }

            public float SpawnInterval
            {
                get
                {
                    return spawnInterval;
                }
            }

            public int SpawnGroupCount
            {
                get
                {
                    return spawnGroupCount;
                }
            }

            public int FrontRowCount
            {
                get
                {
                    return frontRowCount;
                }
            }

            public MonsterSpawnEntry[] MonsterEntries
            {
                get
                {
                    return monsterEntries;
                }
            }
        }

        public readonly struct ExternalSpawnEntry // WaveController 같은 외부 시스템이 요청할 몬스터 조합 항목
        {
            public readonly EnemyController Prefab; // 생성할 몬스터 Prefab
            public readonly int Count; // 이 항목에서 생성할 몬스터 수

            public ExternalSpawnEntry(EnemyController prefab, int count)
            {
                Prefab = prefab;
                Count = count;
            }

            public bool IsValid // Prefab과 수량이 모두 정상인지 확인한다.
            {
                get
                {
                    return Prefab != null && Count > 0;
                }
            }
        }

        public readonly struct ExternalSpawnDirectionSet // 외부 Wave 시스템이 한 Stage 동안 고정해서 사용할 방향 묶음
        {
            private readonly int[] directionIndexes; // EnemySpawner 내부 방향 enum을 밖에 직접 노출하지 않기 위한 저장값

            public ExternalSpawnDirectionSet(int[] directionIndexes)
            {
                this.directionIndexes = directionIndexes;
            }

            public bool IsValid
            {
                get
                {
                    return directionIndexes != null && directionIndexes.Length > 0;
                }
            }

            internal int[] GetDirectionIndexes()
            {
                return directionIndexes;
            }
        }

        public readonly struct ExternalSpawnCongestionOptions // 외부 Wave 시스템이 스폰 지점 혼잡도에 따라 생성 위치를 뒤로 미는 옵션
        {
            public readonly bool Enabled; // 꺼져 있으면 기존 스폰 위치를 그대로 사용한다.
            public readonly float CheckRadius; // 스폰 예정 위치 주변을 얼마나 넓게 검사할지
            public readonly int MonsterThreshold; // 이 수 이상 몬스터가 있으면 혼잡하다고 본다.
            public readonly float PushDistance; // 혼잡할 때 한 번에 뒤로 미는 거리
            public readonly float MaxPushDistance; // 한 스폰 묶음에서 최대한 뒤로 밀 수 있는 거리

            public ExternalSpawnCongestionOptions(bool enabled, float checkRadius, int monsterThreshold, float pushDistance, float maxPushDistance)
            {
                Enabled = enabled;
                CheckRadius = checkRadius;
                MonsterThreshold = monsterThreshold;
                PushDistance = pushDistance;
                MaxPushDistance = maxPushDistance;
            }

            public static ExternalSpawnCongestionOptions Disabled
            {
                get
                {
                    return new ExternalSpawnCongestionOptions(false, 0.0f, 0, 0.0f, 0.0f);
                }
            }
        }

        public readonly struct ExternalSpawnSpreadOptions // 외부 Wave 시스템이 선택된 방향 안에서 스폰 위치를 퍼뜨리는 옵션
        {
            public readonly bool Enabled; // 꺼져 있으면 기존 오와열 배치만 사용한다.
            public readonly float Amount; // 숫자가 클수록 선택된 방향 안에서 더 넓게 퍼진다.

            public ExternalSpawnSpreadOptions(bool enabled, float amount)
            {
                Enabled = enabled && amount > 0.0f;
                Amount = Mathf.Max(0.0f, amount);
            }

            public static ExternalSpawnSpreadOptions Disabled
            {
                get
                {
                    return new ExternalSpawnSpreadOptions(false, 0.0f);
                }
            }
        }

        public enum ExternalSpawnFormationMode // 외부 Wave 시스템이 사용할 스폰 대형
        {
            Rows = 0, // 기존 사각 오와열
            FilledCircleRows = 1 // 원 안을 오와열로 꽉 채운 대형
        }

        public readonly struct ExternalSpawnFormationOptions // 외부 Wave 시스템의 실제 대형 옵션
        {
            public readonly ExternalSpawnFormationMode Mode; // 생성할 대형 종류
            public readonly bool DisableEntryRedistribution; // 켜면 몬스터 단위 재섞기와 방향별 재분배를 건너뛴다.
            public readonly float CenterJitterRadius; // 대형 중심을 선택된 게이트 주변에서 랜덤 이동할 반경
            public readonly bool RandomizeCircleRotation; // 원형 오와열 방향을 매 묶음마다 조금 다르게 돌릴지

            public ExternalSpawnFormationOptions(ExternalSpawnFormationMode mode, bool disableEntryRedistribution, float centerJitterRadius, bool randomizeCircleRotation)
            {
                Mode = mode;
                DisableEntryRedistribution = disableEntryRedistribution;
                CenterJitterRadius = Mathf.Max(0.0f, centerJitterRadius);
                RandomizeCircleRotation = randomizeCircleRotation;
            }

            public bool UsesFilledCircle
            {
                get
                {
                    return Mode == ExternalSpawnFormationMode.FilledCircleRows || (int)Mode == 2;
                }
            }

            public bool UsesFilledArea
            {
                get
                {
                    return UsesFilledCircle;
                }
            }

            public static ExternalSpawnFormationOptions Rows
            {
                get
                {
                    return new ExternalSpawnFormationOptions(ExternalSpawnFormationMode.Rows, false, 0.0f, false);
                }
            }
        }

        private Transform nexus; // Nexus Transform, Inspector에는 노출하지 않고 자동 탐색한다.

        private Transform monsterRoot; // 생성된 몬스터를 정리할 부모 Transform
        private readonly List<EnemyController> congestionCheckResults = new List<EnemyController>(64); // 스폰 위치 혼잡도 검사에 재사용할 임시 목록

        [Header("Spawn Gates")]
        [SerializeField] private Transform[] frontGates; // 앞쪽 스폰 게이트 목록
        [SerializeField] private Transform[] backGates; // 뒤쪽 스폰 게이트 목록
        [SerializeField] private Transform[] leftGates; // 왼쪽 스폰 게이트 목록
        [SerializeField] private Transform[] rightGates; // 오른쪽 스폰 게이트 목록

        [Header("Diagonal Spawn Gates")]
        [SerializeField] private Transform[] frontLeftGates; // 앞왼쪽 대각선 스폰 게이트 목록
        [SerializeField] private Transform[] frontRightGates; // 앞오른쪽 대각선 스폰 게이트 목록
        [SerializeField] private Transform[] backLeftGates; // 뒤왼쪽 대각선 스폰 게이트 목록
        [SerializeField] private Transform[] backRightGates; // 뒤오른쪽 대각선 스폰 게이트 목록

        [Header("Group Formation Setting")]
        [Min(0.0f)]
        [SerializeField] private float groupForwardOffset = 3.0f; // 게이트 앞쪽으로 군단 중심을 얼마나 밀지

        [Min(0.1f)]
        [SerializeField] private float columnSpacing = 1.5f; // 몬스터 좌우 간격

        [Min(0.1f)]
        [SerializeField] private float rowSpacing = 1.5f; // 몬스터 앞뒤 간격

        [Min(0.0f)]
        [SerializeField] private float spawnGroundHeight = 0.72f; // 스폰 위치를 바닥 위로 올릴 높이

        [Range(1, 300)]
        [SerializeField, HideInInspector] private int maxActiveMonsters = 120; // 기존 Stage Rules가 사용할 최대 활성 몬스터 수

        [Header("Wave Spawn Limit")]
        [SerializeField] private bool useWaveMaxActiveMonsterLimit = true; // WaveSystem 외부 스폰에 전용 최대 수 제한을 적용할지 정한다.

        [Min(1)]
        [SerializeField] private int waveMaxActiveMonsters = 3000; // WaveSystem 외부 스폰에서 허용할 최대 활성 몬스터 수

        [Min(0.0f)]
        [SerializeField] private float firstSpawnDelay = 1.0f; // 각 규칙이 켜진 뒤 첫 스폰까지 대기 시간

        [Header("Stage Rules")]
        [SerializeField] private StageSpawnRule[] stageRules; // 누적형 단계별 스폰 규칙 목록

        private float elapsedGameTime; // 스폰 시스템이 켜진 뒤 지난 시간
        private float[] stageSpawnTimers; // Stage Rule별 다음 스폰까지 남은 시간
        private int spawnSerial; // 생성된 몬스터 이름 번호

        private void Awake()
        {
            if(nexus == null)
            {
                GameObject nexusObject = GameObject.Find("Nexus_Core");
                nexus = nexusObject != null ? nexusObject.transform : null;
            }

            monsterRoot = MonsterRuntimeRoot.GetRootOrFallback(transform);
        }

        private void OnEnable()
        {
            elapsedGameTime = 0.0f; // 게임 진행 시간을 초기화한다.
            ResetStageSpawnTimers(); // Stage별 스폰 타이머를 초기화한다.
        }

        private void Update()
        {
            if (nexus == null) // Nexus가 없다면
            {
                return; // 스폰하지 않는다.
            }

            if (stageRules == null || stageRules.Length == 0) // 단계 규칙이 없다면
            {
                return; // 스폰하지 않는다.
            }

            EnsureStageSpawnTimers(); // Stage Rule 개수와 타이머 배열 개수를 맞춘다.

            elapsedGameTime += Time.deltaTime; // 전체 진행 시간을 증가시킨다.

            UpdateActiveStageRules(); // 시작 시간이 지난 Stage Rule들을 누적 실행한다.
        }

        private void ResetStageSpawnTimers() // Stage별 첫 스폰 타이머 초기화
        {
            if (stageRules == null) // Stage Rules가 없다면
            {
                stageSpawnTimers = null; // 타이머도 비운다.
                return;
            }

            stageSpawnTimers = new float[stageRules.Length]; // Stage Rule 개수만큼 타이머를 만든다.

            for (int i = 0; i < stageSpawnTimers.Length; i++) // 모든 타이머를 순회한다.
            {
                stageSpawnTimers[i] = firstSpawnDelay; // 각 Stage Rule이 켜진 뒤 첫 스폰까지의 시간을 저장한다.
            }
        }

        private void EnsureStageSpawnTimers() // Stage Rules 개수와 타이머 배열 개수 맞추기
        {
            if (stageRules == null) // Stage Rules가 없다면
            {
                stageSpawnTimers = null; // 타이머도 비운다.
                return;
            }

            if (stageSpawnTimers != null && stageSpawnTimers.Length == stageRules.Length) // 개수가 이미 맞다면
            {
                return; // 다시 만들지 않는다.
            }

            ResetStageSpawnTimers(); // 개수가 다르면 다시 만든다.
        }

        private void UpdateActiveStageRules() // 시작 시간이 지난 모든 Stage Rule을 처리한다.
        {
            for (int i = 0; i < stageRules.Length; i++) // 모든 Stage Rule을 순회한다.
            {
                StageSpawnRule rule = stageRules[i]; // 현재 Stage Rule

                if (rule == null) // 비어 있다면
                {
                    continue; // 건너뛴다.
                }

                if (elapsedGameTime < rule.StartTime) // 아직 이 규칙이 켜질 시간이 아니라면
                {
                    continue; // 실행하지 않는다.
                }

                stageSpawnTimers[i] -= Time.deltaTime; // 이 Stage Rule의 스폰 대기 시간을 줄인다.

                if (stageSpawnTimers[i] > 0.0f) // 아직 스폰 시간이 남았다면
                {
                    continue; // 이번 프레임에는 이 규칙으로 스폰하지 않는다.
                }

                SpawnStageGroups(rule); // 이 Stage Rule에 맞는 군단을 생성한다.
                stageSpawnTimers[i] = Mathf.Max(0.1f, rule.SpawnInterval); // 다음 반복 스폰 시간을 설정한다.
            }
        }

        private int GetExternalWaveSpawnCapacity() // WaveSystem 외부 스폰에서 사용할 남은 생성 가능 수
        {
            if (!useWaveMaxActiveMonsterLimit)
            {
                return int.MaxValue;
            }

            return Mathf.Max(0, waveMaxActiveMonsters - EnemyController.ActiveCount);
        }

        public bool TrySpawnExternalEntries(ExternalSpawnEntry[] entries, int spawnGroupCount, int frontRowCount) // 외부 WaveController가 몬스터 조합 생성을 요청하는 입구
        {
            return TrySpawnExternalEntries(entries, spawnGroupCount, frontRowCount, spawnGroupCount); // 기존 호출은 요청 군단 수만큼 방향을 사용한다.
        }

        public bool TrySpawnExternalEntries(ExternalSpawnEntry[] entries, int spawnGroupCount, int frontRowCount, int directionCount) // 외부 WaveController가 사용할 방향 수까지 지정하는 입구
        {
            if (nexus == null) // Nexus가 아직 잡히지 않았다면
            {
                GameObject nexusObject = GameObject.Find("Nexus_Core"); // 기존 Awake와 같은 기준으로 Nexus를 찾는다.
                nexus = nexusObject != null ? nexusObject.transform : null; // 찾은 결과를 저장한다.
            }

            if (nexus == null) // Nexus가 없으면 스폰 기준이 아직 준비되지 않은 상태다.
            {
                return false;
            }

            if (monsterRoot == null) // 몬스터 정리 부모가 비어 있다면
            {
                monsterRoot = transform; // 기존 Awake와 같은 기준으로 자기 자신을 사용한다.
            }

            if (entries == null || entries.Length == 0) // 생성할 조합이 없다면
            {
                return false;
            }

            int capacity = GetExternalWaveSpawnCapacity(); // WaveSystem 외부 스폰 전용 생성 가능 수

            if (capacity <= 0) // 이미 최대 몬스터 수에 도달했다면
            {
                return false;
            }

            int groupCount = Mathf.Max(1, spawnGroupCount); // 한 번 요청으로 몇 개 군단을 만들지
            int safeFrontRowCount = Mathf.Max(1, frontRowCount); // 한 줄에 배치할 몬스터 수
            SpawnDirection[] selectedDirections = PickRandomDirectionsForExternalWave(directionCount); // 이번 요청에서 사용할 방향 후보를 고른다.
            bool spawnedAny = false; // 실제로 한 마리라도 만들었는지 기록한다.

            if (selectedDirections == null || selectedDirections.Length == 0) // 사용할 방향이 없다면
            {
                return false;
            }

            for (int i = 0; i < groupCount; i++) // 요청된 군단 수만큼 반복한다.
            {
                if (capacity <= 0) // 더 이상 만들 수 없다면
                {
                    return spawnedAny;
                }

                SpawnDirection direction = selectedDirections[i % selectedDirections.Length]; // 선택된 방향들을 순서대로 사용한다.
                Transform gate = PickRandomGate(direction); // 해당 방향에 연결된 게이트 중 하나를 고른다.

                if (gate == null) // 사용할 수 있는 게이트가 없다면
                {
                    return spawnedAny;
                }

                if (SpawnExternalGroupAtGate(entries, safeFrontRowCount, gate, ref capacity)) // 선택한 게이트에서 외부 요청 조합을 만든다.
                {
                    spawnedAny = true;
                }
            }

            return spawnedAny;
        }

        public bool TrySpawnExternalEntriesDistributed(ExternalSpawnEntry[] entries, int directionCount, int frontRowCount) // 외부 요청 몬스터 총량을 여러 게이트에 나눠서 생성하는 입구
        {
            return TrySpawnExternalEntriesDistributed(entries, directionCount, frontRowCount, null);
        }

        public ExternalSpawnDirectionSet PickExternalSpawnDirections(int directionCount) // 한 Stage 동안 고정해서 사용할 방향 묶음을 뽑는다.
        {
            return new ExternalSpawnDirectionSet(ConvertToDirectionIndexes(PickRandomDirectionsForExternalWave(directionCount)));
        }

        public bool TrySpawnExternalEntriesDistributed(ExternalSpawnEntry[] entries, ExternalSpawnDirectionSet directionSet, int frontRowCount) // 이미 뽑아둔 방향 묶음으로 외부 스폰을 처리한다.
        {
            return TrySpawnExternalEntriesDistributed(entries, directionSet, frontRowCount, null);
        }

        public bool TrySpawnExternalEntriesDistributed(ExternalSpawnEntry[] entries, ExternalSpawnDirectionSet directionSet, int frontRowCount, ExternalSpawnCongestionOptions congestionOptions) // 혼잡도 옵션을 적용해서 외부 스폰을 처리한다.
        {
            return TrySpawnExternalEntriesDistributed(entries, directionSet, frontRowCount, congestionOptions, null);
        }

        public bool TrySpawnExternalEntriesDistributed(ExternalSpawnEntry[] entries, ExternalSpawnDirectionSet directionSet, int frontRowCount, List<EnemyController> spawnedMonsters) // 생성 몬스터 기록까지 필요한 고정 방향 외부 스폰 입구
        {
            return TrySpawnExternalEntriesDistributed(entries, ConvertToSpawnDirections(directionSet.GetDirectionIndexes()), frontRowCount, spawnedMonsters, ExternalSpawnCongestionOptions.Disabled, ExternalSpawnSpreadOptions.Disabled, ExternalSpawnFormationOptions.Rows);
        }

        public bool TrySpawnExternalEntriesDistributed(ExternalSpawnEntry[] entries, ExternalSpawnDirectionSet directionSet, int frontRowCount, ExternalSpawnCongestionOptions congestionOptions, List<EnemyController> spawnedMonsters) // 생성 몬스터 기록과 혼잡도 옵션이 모두 필요한 외부 스폰 입구
        {
            return TrySpawnExternalEntriesDistributed(entries, ConvertToSpawnDirections(directionSet.GetDirectionIndexes()), frontRowCount, spawnedMonsters, congestionOptions, ExternalSpawnSpreadOptions.Disabled, ExternalSpawnFormationOptions.Rows);
        }

        public bool TrySpawnExternalEntriesDistributed(ExternalSpawnEntry[] entries, ExternalSpawnDirectionSet directionSet, int frontRowCount, ExternalSpawnCongestionOptions congestionOptions, ExternalSpawnSpreadOptions spreadOptions, List<EnemyController> spawnedMonsters) // 생성 몬스터 기록, 혼잡도, 스폰 퍼짐 옵션이 모두 필요한 외부 스폰 입구
        {
            return TrySpawnExternalEntriesDistributed(entries, directionSet, frontRowCount, congestionOptions, spreadOptions, ExternalSpawnFormationOptions.Rows, spawnedMonsters);
        }

        public bool TrySpawnExternalEntriesDistributed(ExternalSpawnEntry[] entries, ExternalSpawnDirectionSet directionSet, int frontRowCount, ExternalSpawnCongestionOptions congestionOptions, ExternalSpawnSpreadOptions spreadOptions, ExternalSpawnFormationOptions formationOptions, List<EnemyController> spawnedMonsters) // 대형 옵션까지 포함해서 외부 스폰을 처리한다.
        {
            return TrySpawnExternalEntriesDistributed(entries, ConvertToSpawnDirections(directionSet.GetDirectionIndexes()), frontRowCount, spawnedMonsters, congestionOptions, spreadOptions, formationOptions);
        }

        public bool TrySpawnExternalEntriesDistributed(ExternalSpawnEntry[] entries, int directionCount, int frontRowCount, List<EnemyController> spawnedMonsters) // 생성된 몬스터 목록까지 필요한 외부 요청 입구
        {
            if (nexus == null) // Nexus가 아직 잡히지 않았다면
            {
                GameObject nexusObject = GameObject.Find("Nexus_Core"); // 기존 Awake와 같은 기준으로 Nexus를 찾는다.
                nexus = nexusObject != null ? nexusObject.transform : null; // 찾은 결과를 저장한다.
            }

            if (nexus == null) // Nexus가 없으면 스폰 기준이 아직 준비되지 않은 상태다.
            {
                return false;
            }

            if (monsterRoot == null) // 몬스터 정리 부모가 비어 있다면
            {
                monsterRoot = transform; // 기존 Awake와 같은 기준으로 자기 자신을 사용한다.
            }

            if (entries == null || entries.Length == 0) // 생성할 조합이 없다면
            {
                return false;
            }

            int capacity = GetExternalWaveSpawnCapacity(); // WaveSystem 외부 스폰 전용 생성 가능 수

            if (capacity <= 0) // 이미 최대 몬스터 수에 도달했다면
            {
                return false;
            }

            SpawnDirection[] selectedDirections = PickRandomDirectionsForExternalWave(directionCount); // 이번 요청에서 사용할 방향을 고른다.
            return TrySpawnExternalEntriesDistributed(entries, selectedDirections, frontRowCount, spawnedMonsters, ExternalSpawnCongestionOptions.Disabled, ExternalSpawnSpreadOptions.Disabled, ExternalSpawnFormationOptions.Rows);
        }

        private bool TrySpawnExternalEntriesDistributed(ExternalSpawnEntry[] entries, SpawnDirection[] selectedDirections, int frontRowCount, List<EnemyController> spawnedMonsters, ExternalSpawnCongestionOptions congestionOptions, ExternalSpawnSpreadOptions spreadOptions, ExternalSpawnFormationOptions formationOptions) // 선택된 방향 목록을 기준으로 몬스터를 분산 생성한다.
        {
            if (nexus == null) // Nexus가 아직 잡히지 않았다면
            {
                GameObject nexusObject = GameObject.Find("Nexus_Core"); // 기존 Awake와 같은 기준으로 Nexus를 찾는다.
                nexus = nexusObject != null ? nexusObject.transform : null; // 찾은 결과를 저장한다.
            }

            if (nexus == null) // Nexus가 없으면 스폰 기준이 아직 준비되지 않은 상태다.
            {
                return false;
            }

            if (monsterRoot == null) // 몬스터 정리 부모가 비어 있다면
            {
                monsterRoot = transform; // 기존 Awake와 같은 기준으로 자기 자신을 사용한다.
            }

            if (entries == null || entries.Length == 0) // 생성할 조합이 없다면
            {
                return false;
            }

            int capacity = GetExternalWaveSpawnCapacity(); // WaveSystem 외부 스폰 전용 생성 가능 수

            if (capacity <= 0) // 이미 최대 몬스터 수에 도달했다면
            {
                return false;
            }

            if (selectedDirections == null || selectedDirections.Length == 0) // 사용할 방향이 없다면
            {
                return false;
            }

            int safeFrontRowCount = Mathf.Max(1, frontRowCount); // 한 줄에 배치할 몬스터 수

            if (formationOptions.DisableEntryRedistribution) // 임시: WaveSystem의 몬스터 단위 재섞기와 방향별 재분배를 건너뛴다.
            {
                return TrySpawnExternalEntriesAsSingleFormationGroup(entries, selectedDirections, safeFrontRowCount, ref capacity, spawnedMonsters, congestionOptions, spreadOptions, formationOptions);
            }

            List<ExternalSpawnEntry>[] distributedEntries = new List<ExternalSpawnEntry>[selectedDirections.Length]; // 방향별로 나눠 담을 몬스터 목록

            for (int i = 0; i < distributedEntries.Length; i++) // 방향 수만큼 빈 목록을 만든다.
            {
                distributedEntries[i] = new List<ExternalSpawnEntry>();
            }

            List<ExternalSpawnEntry> shuffledEntries = new List<ExternalSpawnEntry>(); // 몬스터 종류가 줄 단위로 뭉치지 않게 한 마리 단위 목록으로 모은다.

            for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++) // 요청된 몬스터 조합을 순회한다.
            {
                ExternalSpawnEntry entry = entries[entryIndex]; // 현재 조합 항목

                if (!entry.IsValid) // Prefab이나 수량이 비정상이라면
                {
                    continue; // 분산 대상에서 제외한다.
                }

                for (int countIndex = 0; countIndex < entry.Count; countIndex++) // 몬스터를 한 마리 단위로 나눠 담는다.
                {
                    shuffledEntries.Add(new ExternalSpawnEntry(entry.Prefab, 1)); // 나중에 섞을 수 있도록 임시 목록에 모은다.
                }
            }

            if (shuffledEntries.Count <= 0) // 실제로 나눠 담을 몬스터가 없다면
            {
                return false;
            }

            ShuffleExternalSpawnEntries(shuffledEntries); // 기본몹/스켈레톤/원거리 몬스터가 줄 단위로 뭉치지 않도록 섞는다.

            for (int shuffledIndex = 0; shuffledIndex < shuffledEntries.Count; shuffledIndex++) // 섞인 몬스터 목록을 순회한다.
            {
                int groupIndex = shuffledIndex % distributedEntries.Length; // 선택된 방향들에 번갈아 분배한다.
                distributedEntries[groupIndex].Add(shuffledEntries[shuffledIndex]); // 해당 방향에 한 마리 추가한다.
            }

            bool spawnedAny = false; // 실제 생성 여부

            for (int i = 0; i < selectedDirections.Length; i++) // 선택된 방향별로 스폰한다.
            {
                if (capacity <= 0) // 더 이상 만들 수 없다면
                {
                    return spawnedAny;
                }

                if (distributedEntries[i].Count <= 0) // 이 방향에 배정된 몬스터가 없다면
                {
                    continue;
                }

                Transform gate = PickRandomGate(selectedDirections[i]); // 이 방향에 연결된 게이트 중 하나를 고른다.

                if (gate == null) // 이 방향의 게이트가 비어 있다면
                {
                    continue; // 다른 방향은 계속 시도한다.
                }

                if (SpawnExternalGroupAtGate(distributedEntries[i].ToArray(), safeFrontRowCount, gate, ref capacity, spawnedMonsters, congestionOptions, spreadOptions, formationOptions)) // 배정된 몬스터만 이 게이트에 생성한다.
                {
                    spawnedAny = true;
                }
            }

            return spawnedAny;
        }

        private bool TrySpawnExternalEntriesAsSingleFormationGroup(ExternalSpawnEntry[] entries, SpawnDirection[] selectedDirections, int frontRowCount, ref int capacity, List<EnemyController> spawnedMonsters, ExternalSpawnCongestionOptions congestionOptions, ExternalSpawnSpreadOptions spreadOptions, ExternalSpawnFormationOptions formationOptions) // 몬스터 목록을 다시 섞지 않고 하나의 대형으로 생성한다.
        {
            for (int i = 0; i < selectedDirections.Length; i++)
            {
                Transform gate = PickRandomGate(selectedDirections[i]);

                if (gate == null)
                {
                    continue;
                }

                return SpawnExternalGroupAtGate(entries, frontRowCount, gate, ref capacity, spawnedMonsters, congestionOptions, spreadOptions, formationOptions);
            }

            return false;
        }

        private static void ShuffleExternalSpawnEntries(List<ExternalSpawnEntry> entries) // 외부 웨이브 몬스터 배치 순서를 섞는다.
        {
            if (entries == null || entries.Count <= 1) // 섞을 필요가 없다면
            {
                return; // 그대로 둔다.
            }

            for (int i = entries.Count - 1; i > 0; i--) // 뒤에서부터 하나씩 무작위 위치와 교환한다.
            {
                int swapIndex = Random.Range(0, i + 1); // 0부터 현재 위치까지 중 하나를 고른다.
                ExternalSpawnEntry temp = entries[i]; // 현재 값을 임시 저장한다.
                entries[i] = entries[swapIndex]; // 무작위 위치 값을 현재 위치로 옮긴다.
                entries[swapIndex] = temp; // 임시 저장한 값을 무작위 위치로 옮긴다.
            }
        }

        private void SpawnStageGroups(StageSpawnRule rule) // 현재 Stage Rule의 군단 스폰
        {
            int capacity = Mathf.Max(0, maxActiveMonsters - EnemyController.ActiveCount); // 남은 생성 가능 몬스터 수

            if (capacity <= 0) // 생성 가능 수가 없다면
            {
                return; // 스폰하지 않는다.
            }

            int groupCount = Mathf.Max(1, rule.SpawnGroupCount); // 한 번에 만들 군단 수

            for (int i = 0; i < groupCount; i++) // 군단 수만큼 반복한다.
            {
                if (capacity <= 0) // 더 이상 생성할 수 없다면
                {
                    return; // 종료한다.
                }

                Transform gate = PickRandomGateFromAllDirections(); // Front, Back, Left, Right 중 랜덤 게이트를 고른다.

                if (gate == null) // 사용할 게이트가 없다면
                {
                    return; // 스폰할 위치가 없으므로 종료한다.
                }

                SpawnGroupAtGate(rule, gate, ref capacity); // 선택한 게이트에서 군단을 생성한다.
            }
        }

        private void SpawnGroupAtGate(StageSpawnRule rule, Transform gate, ref int capacity) // 특정 게이트에서 군단 생성
        {
            MonsterSpawnEntry[] entries = rule.MonsterEntries; // 현재 규칙의 몬스터 조합

            if (entries == null || entries.Length == 0) // 몬스터 조합이 없다면
            {
                return; // 생성하지 않는다.
            }

            int totalMonsterCount = GetTotalMonsterCount(entries); // 이 군단에서 생성할 전체 몬스터 수를 계산한다.

            if (totalMonsterCount <= 0) // 생성할 몬스터가 없다면
            {
                return; // 종료한다.
            }

            int frontRowCount = Mathf.Max(1, rule.FrontRowCount); // 한 줄에 세울 최대 몬스터 수

            Vector3 groupCenter = gate.position + gate.forward * groupForwardOffset; // 게이트 앞쪽으로 민 군단 앞줄 중심 위치
            groupCenter = GroundService.ProjectToGround(groupCenter, spawnGroundHeight); // 바닥 높이에 맞춘다.

            int formationIndex = 0; // 오와열 배치 순서

            for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++) // 몬스터 조합을 순회한다.
            {
                MonsterSpawnEntry entry = entries[entryIndex]; // 현재 조합 항목

                if (entry == null || entry.Prefab == null || entry.Count <= 0) // 유효하지 않다면
                {
                    continue; // 건너뛴다.
                }

                for (int countIndex = 0; countIndex < entry.Count; countIndex++) // 설정된 개수만큼 생성한다.
                {
                    if (capacity <= 0) // 최대 몬스터 수에 도달했다면
                    {
                        return; // 생성 중지
                    }

                    Vector3 formationOffset = GetFormationOffset(formationIndex, totalMonsterCount, frontRowCount, gate); // 오와열 위치 오프셋을 계산한다.
                    Vector3 spawnPosition = groupCenter + formationOffset; // 최종 생성 위치를 계산한다.
                    spawnPosition = GroundService.ProjectToGround(spawnPosition, spawnGroundHeight); // 바닥 높이에 맞춘다.

                    SpawnMonster(entry.Prefab, spawnPosition, gate.rotation); // 몬스터 하나 생성

                    formationIndex++; // 다음 오와열 위치로 이동한다.
                    capacity--; // 남은 생성 가능 수 감소
                }
            }
        }

        private bool SpawnExternalGroupAtGate(ExternalSpawnEntry[] entries, int frontRowCount, Transform gate, ref int capacity) // 외부 요청 조합을 특정 게이트에서 생성
        {
            return SpawnExternalGroupAtGate(entries, frontRowCount, gate, ref capacity, null);
        }

        private bool SpawnExternalGroupAtGate(ExternalSpawnEntry[] entries, int frontRowCount, Transform gate, ref int capacity, List<EnemyController> spawnedMonsters) // 생성된 몬스터 목록까지 기록하는 외부 요청 처리
        {
            return SpawnExternalGroupAtGate(entries, frontRowCount, gate, ref capacity, spawnedMonsters, ExternalSpawnCongestionOptions.Disabled, ExternalSpawnSpreadOptions.Disabled, ExternalSpawnFormationOptions.Rows);
        }

        private bool SpawnExternalGroupAtGate(ExternalSpawnEntry[] entries, int frontRowCount, Transform gate, ref int capacity, List<EnemyController> spawnedMonsters, ExternalSpawnCongestionOptions congestionOptions, ExternalSpawnSpreadOptions spreadOptions) // 생성된 몬스터 목록, 혼잡도, 스폰 퍼짐 옵션까지 처리하는 외부 요청
        {
            return SpawnExternalGroupAtGate(entries, frontRowCount, gate, ref capacity, spawnedMonsters, congestionOptions, spreadOptions, ExternalSpawnFormationOptions.Rows);
        }

        private bool SpawnExternalGroupAtGate(ExternalSpawnEntry[] entries, int frontRowCount, Transform gate, ref int capacity, List<EnemyController> spawnedMonsters, ExternalSpawnCongestionOptions congestionOptions, ExternalSpawnSpreadOptions spreadOptions, ExternalSpawnFormationOptions formationOptions) // 생성된 몬스터 목록, 혼잡도, 스폰 퍼짐, 대형 옵션까지 처리하는 외부 요청
        {
            if (entries == null || entries.Length == 0) // 몬스터 조합이 없다면
            {
                return false; // 생성하지 않는다.
            }

            int totalMonsterCount = GetTotalMonsterCount(entries); // 이 군단에서 생성할 전체 몬스터 수를 계산한다.

            if (totalMonsterCount <= 0) // 생성할 몬스터가 없다면
            {
                return false;
            }

            Vector3 outwardDirection = GetSpawnOutwardDirection(gate); // 넥서스에서 게이트로 향하는 바깥 방향
            Vector3 groupCenter = gate.position + outwardDirection * groupForwardOffset; // 바깥 방향으로 민 군단 앞줄 중심 위치
            groupCenter = ApplyCongestionPush(groupCenter, congestionOptions); // 스폰 위치가 이미 붐비면 넥서스 반대 방향으로 조금 더 밀어낸다.
            groupCenter += GetExternalWaveFormationCenterOffset(formationOptions, outwardDirection); // 대형 중심만 랜덤 이동해서 전체 대형은 유지한다.
            groupCenter = GroundService.ProjectToGround(groupCenter, spawnGroundHeight); // 바닥 높이에 맞춘다.

            Vector2[] filledCircleCoordinates = formationOptions.UsesFilledArea ? BuildFilledCircleGridCoordinates(totalMonsterCount) : null;
            float formationRotationDegrees = formationOptions.UsesFilledCircle && formationOptions.RandomizeCircleRotation ? UnityEngine.Random.Range(0.0f, 360.0f) : 0.0f;
            int formationIndex = 0; // 오와열 배치 순서
            bool spawnedAny = false; // 실제 생성 여부

            for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++) // 몬스터 조합을 순회한다.
            {
                ExternalSpawnEntry entry = entries[entryIndex]; // 현재 조합 항목

                if (!entry.IsValid) // 유효하지 않다면
                {
                    continue; // 건너뛴다.
                }

                for (int countIndex = 0; countIndex < entry.Count; countIndex++) // 설정된 개수만큼 생성한다.
                {
                    if (capacity <= 0) // 최대 몬스터 수에 도달했다면
                    {
                        return spawnedAny; // 생성 중지
                    }

                    Vector3 formationOffset = GetExternalWaveFormationOffset(formationIndex, totalMonsterCount, frontRowCount, outwardDirection, formationOptions, filledCircleCoordinates, formationRotationDegrees); // 외부 웨이브용 오와열 위치 오프셋을 계산한다.
                    Vector3 spreadOffset = formationOptions.UsesFilledArea ? Vector3.zero : GetExternalWaveSpreadOffset(spreadOptions, outwardDirection); // 원형 대형에서는 개별 랜덤 퍼짐을 막아 대형을 보존한다.
                    Vector3 spawnPosition = groupCenter + formationOffset + spreadOffset; // 최종 생성 위치를 계산한다.
                    spawnPosition = GroundService.ProjectToGround(spawnPosition, spawnGroundHeight); // 바닥 높이에 맞춘다.

                    EnemyController spawnedMonster = SpawnMonster(entry.Prefab, spawnPosition, gate.rotation); // 몬스터 하나 생성

                    if (spawnedMonster != null && spawnedMonsters != null)
                    {
                        spawnedMonsters.Add(spawnedMonster); // 특수 웨이브 보상 판정을 위해 생성 몬스터를 기록한다.
                    }

                    formationIndex++; // 다음 오와열 위치로 이동한다.
                    capacity--; // 남은 생성 가능 수 감소
                    spawnedAny = true; // 최소 한 마리는 생성했다.
                }
            }

            return spawnedAny;
        }

        private int GetTotalMonsterCount(MonsterSpawnEntry[] entries) // 한 군단에 생성될 총 몬스터 수 계산
        {
            int totalCount = 0; // 총 몬스터 수

            if (entries == null) // 조합이 없다면
            {
                return 0; // 0 반환
            }

            for (int i = 0; i < entries.Length; i++) // 조합을 순회한다.
            {
                MonsterSpawnEntry entry = entries[i]; // 현재 항목

                if (entry == null || entry.Prefab == null || entry.Count <= 0) // 유효하지 않다면
                {
                    continue; // 제외한다.
                }

                totalCount += entry.Count; // 생성 개수를 더한다.
            }

            return totalCount; // 총 몬스터 수를 반환한다.
        }

        private int GetTotalMonsterCount(ExternalSpawnEntry[] entries) // 외부 요청 조합의 총 몬스터 수 계산
        {
            int totalCount = 0; // 총 몬스터 수

            if (entries == null) // 조합이 없다면
            {
                return 0; // 0 반환
            }

            for (int i = 0; i < entries.Length; i++) // 조합을 순회한다.
            {
                ExternalSpawnEntry entry = entries[i]; // 현재 항목

                if (!entry.IsValid) // 유효하지 않다면
                {
                    continue; // 제외한다.
                }

                totalCount += entry.Count; // 생성 개수를 더한다.
            }

            return totalCount; // 총 몬스터 수를 반환한다.
        }

        private Vector3 ApplyCongestionPush(Vector3 groupCenter, ExternalSpawnCongestionOptions options) // 스폰 위치가 붐비면 넥서스 반대 방향으로 생성 위치를 민다.
        {
            if (!options.Enabled || nexus == null) // 옵션이 꺼져 있거나 기준점이 없다면
            {
                return groupCenter; // 기존 위치 사용
            }

            float checkRadius = Mathf.Max(0.1f, options.CheckRadius); // 검사 반경은 0보다 커야 한다.
            int monsterThreshold = Mathf.Max(1, options.MonsterThreshold); // 최소 1마리 이상부터 혼잡 판정 가능
            float pushDistance = Mathf.Max(0.0f, options.PushDistance); // 한 번에 밀 거리
            float maxPushDistance = Mathf.Max(0.0f, options.MaxPushDistance); // 최대 밀 거리

            if (pushDistance <= 0.0f || maxPushDistance <= 0.0f) // 밀 거리가 없다면
            {
                return groupCenter; // 기존 위치 사용
            }

            Vector3 pushDirection = groupCenter - nexus.position; // 넥서스에서 스폰 위치로 향하는 방향
            pushDirection.y = 0.0f; // 평면 방향만 사용

            if (pushDirection.sqrMagnitude <= 0.0001f) // 방향 계산이 불가능하다면
            {
                return groupCenter; // 안전하게 기존 위치 사용
            }

            pushDirection.Normalize();

            Vector3 adjustedCenter = groupCenter;
            float pushedDistance = 0.0f;

            while (pushedDistance < maxPushDistance) // 최대 거리 안에서 여러 번 재검사한다.
            {
                EnemyController.CollectActiveInRange(adjustedCenter, checkRadius, congestionCheckResults);

                if (congestionCheckResults.Count < monsterThreshold) // 주변 몬스터가 기준보다 적다면
                {
                    break; // 더 밀 필요가 없다.
                }

                float stepDistance = Mathf.Min(pushDistance, maxPushDistance - pushedDistance);
                adjustedCenter += pushDirection * stepDistance; // 한 단계 더 바깥쪽으로 이동
                pushedDistance += stepDistance;
            }

            congestionCheckResults.Clear(); // 다음 검사에 남지 않게 비운다.
            return adjustedCenter;
        }

        private Vector3 GetSpawnOutwardDirection(Transform gate) // 넥서스에서 게이트로 향하는 외부 웨이브 스폰 방향 계산
        {
            if (gate == null)
            {
                return Vector3.forward;
            }

            Vector3 outwardDirection = nexus != null ? gate.position - nexus.position : gate.forward;
            outwardDirection.y = 0.0f;

            if (outwardDirection.sqrMagnitude <= 0.0001f)
            {
                outwardDirection = gate.forward;
                outwardDirection.y = 0.0f;
            }

            if (outwardDirection.sqrMagnitude <= 0.0001f)
            {
                return Vector3.forward;
            }

            outwardDirection.Normalize();
            return outwardDirection;
        }

        private Vector3 GetExternalWaveFormationOffset(int unitIndex, int totalMonsterCount, int frontRowCount, Vector3 outwardDirection, ExternalSpawnFormationOptions formationOptions, Vector2[] filledCircleCoordinates, float formationRotationDegrees) // 외부 웨이브 전용 오와열 배치 오프셋 계산
        {
            if (formationOptions.UsesFilledArea)
            {
                return GetExternalWaveFilledCircleFormationOffset(unitIndex, filledCircleCoordinates, outwardDirection, formationRotationDegrees);
            }

            return GetExternalWaveRowFormationOffset(unitIndex, totalMonsterCount, frontRowCount, outwardDirection);
        }

        private Vector3 GetExternalWaveRowFormationOffset(int unitIndex, int totalMonsterCount, int frontRowCount, Vector3 outwardDirection) // 기존 외부 웨이브용 사각 오와열 배치 오프셋 계산
        {
            int rowIndex = unitIndex / frontRowCount;
            int columnIndex = unitIndex % frontRowCount;

            int rowStartIndex = rowIndex * frontRowCount;
            int remainingCount = totalMonsterCount - rowStartIndex;
            int rowCount = Mathf.Min(frontRowCount, Mathf.Max(0, remainingCount));

            if (rowCount <= 0)
            {
                rowCount = frontRowCount;
            }

            float centeredColumn = columnIndex - (rowCount - 1) * 0.5f;
            float sideOffset = centeredColumn * columnSpacing;
            float backOffset = rowIndex * rowSpacing;

            ResolveExternalWaveAxes(outwardDirection, out Vector3 forward, out Vector3 right);
            return right * sideOffset + forward * backOffset;
        }

        private Vector3 GetExternalWaveFilledCircleFormationOffset(int unitIndex, Vector2[] filledCircleCoordinates, Vector3 outwardDirection, float rotationDegrees) // 원 안을 오와열로 꽉 채운 배치 오프셋 계산
        {
            if (filledCircleCoordinates == null || filledCircleCoordinates.Length == 0)
            {
                return Vector3.zero;
            }

            int safeIndex = Mathf.Clamp(unitIndex, 0, filledCircleCoordinates.Length - 1);
            Vector2 coordinate = filledCircleCoordinates[safeIndex];

            if (Mathf.Abs(rotationDegrees) > 0.001f)
            {
                float radians = rotationDegrees * Mathf.Deg2Rad;
                float cos = Mathf.Cos(radians);
                float sin = Mathf.Sin(radians);
                coordinate = new Vector2(
                    coordinate.x * cos - coordinate.y * sin,
                    coordinate.x * sin + coordinate.y * cos);
            }

            ResolveExternalWaveAxes(outwardDirection, out Vector3 forward, out Vector3 right);
            return right * coordinate.x + forward * coordinate.y;
        }

        private Vector2[] BuildFilledCircleGridCoordinates(int totalMonsterCount) // 몬스터 수만큼 원 내부 격자 좌표를 만든다.
        {
            int safeCount = Mathf.Max(1, totalMonsterCount);

            if (safeCount == 1)
            {
                return new[] { Vector2.zero };
            }

            float safeColumnSpacing = Mathf.Max(0.1f, columnSpacing);
            float safeRowSpacing = Mathf.Max(0.1f, rowSpacing);
            float radiusStep = Mathf.Min(safeColumnSpacing, safeRowSpacing) * 0.5f;
            float radius = radiusStep;
            List<Vector2> candidates = new List<Vector2>(safeCount * 2);

            for (int guard = 0; guard < 256; guard++)
            {
                candidates.Clear();
                int halfColumnCount = Mathf.CeilToInt(radius / safeColumnSpacing);
                int halfRowCount = Mathf.CeilToInt(radius / safeRowSpacing);
                float radiusSqr = radius * radius + 0.001f;

                for (int row = -halfRowCount; row <= halfRowCount; row++)
                {
                    float y = row * safeRowSpacing;

                    for (int column = -halfColumnCount; column <= halfColumnCount; column++)
                    {
                        float x = column * safeColumnSpacing;

                        if (x * x + y * y <= radiusSqr)
                        {
                            candidates.Add(new Vector2(x, y));
                        }
                    }
                }

                if (candidates.Count >= safeCount)
                {
                    break;
                }

                radius += radiusStep;
            }

            candidates.Sort(CompareCircleDistanceThenRows);

            if (candidates.Count > safeCount)
            {
                candidates.RemoveRange(safeCount, candidates.Count - safeCount);
            }

            RecenterCoordinates(candidates);
            candidates.Sort(CompareRowsThenColumns);
            return candidates.ToArray();
        }

        private static Vector3 GetExternalWaveSpreadOffset(ExternalSpawnSpreadOptions options, Vector3 outwardDirection) // 선택된 게이트 방향 안에서 스폰 위치를 자연스럽게 퍼뜨린다.
        {
            if (!options.Enabled || options.Amount <= 0.0f)
            {
                return Vector3.zero;
            }

            ResolveExternalWaveAxes(outwardDirection, out Vector3 forward, out Vector3 right);
            float spreadAmount = Mathf.Max(0.0f, options.Amount);
            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle;
            float sideOffset = randomOffset.x * spreadAmount;
            float depthOffset = randomOffset.y * spreadAmount * 0.6f;

            return right * sideOffset + forward * depthOffset;
        }

        private static Vector3 GetExternalWaveFormationCenterOffset(ExternalSpawnFormationOptions options, Vector3 outwardDirection) // 대형 전체 중심을 랜덤 이동한다.
        {
            if (!options.UsesFilledArea || options.CenterJitterRadius <= 0.0f)
            {
                return Vector3.zero;
            }

            ResolveExternalWaveAxes(outwardDirection, out Vector3 forward, out Vector3 right);
            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * options.CenterJitterRadius;
            randomOffset.y = Mathf.Abs(randomOffset.y); // 스폰포인트 랜덤 범위만 넥서스 반대쪽 반원으로 제한한다.
            return right * randomOffset.x + forward * randomOffset.y;
        }

        private static void ResolveExternalWaveAxes(Vector3 outwardDirection, out Vector3 forward, out Vector3 right) // 외부 웨이브 대형 계산에 쓸 평면 축을 만든다.
        {
            forward = outwardDirection;
            forward.y = 0.0f;

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            right = Vector3.Cross(Vector3.up, forward);

            if (right.sqrMagnitude <= 0.0001f)
            {
                right = Vector3.right;
            }

            right.Normalize();
        }

        private static void RecenterCoordinates(List<Vector2> coordinates) // 선택된 좌표들의 평균점을 대형 중심에 맞춘다.
        {
            if (coordinates == null || coordinates.Count == 0)
            {
                return;
            }

            Vector2 center = Vector2.zero;

            for (int i = 0; i < coordinates.Count; i++)
            {
                center += coordinates[i];
            }

            center /= coordinates.Count;

            for (int i = 0; i < coordinates.Count; i++)
            {
                coordinates[i] -= center;
            }
        }

        private static int CompareCircleDistanceThenRows(Vector2 a, Vector2 b) // 중심에 가까운 좌표부터 고른다.
        {
            int distanceCompare = a.sqrMagnitude.CompareTo(b.sqrMagnitude);

            if (distanceCompare != 0)
            {
                return distanceCompare;
            }

            return CompareRowsThenColumns(a, b);
        }

        private static int CompareRowsThenColumns(Vector2 a, Vector2 b) // 최종 배치는 줄 단위로 정렬한다.
        {
            int rowCompare = a.y.CompareTo(b.y);

            if (rowCompare != 0)
            {
                return rowCompare;
            }

            return a.x.CompareTo(b.x);
        }

        private Vector3 GetFormationOffset(int unitIndex, int totalMonsterCount, int frontRowCount, Transform gate) // 오와열 배치 오프셋 계산
        {
            int rowIndex = unitIndex / frontRowCount; // 몇 번째 줄인지 계산한다.
            int columnIndex = unitIndex % frontRowCount; // 해당 줄에서 몇 번째 칸인지 계산한다.

            int rowStartIndex = rowIndex * frontRowCount; // 현재 줄의 시작 인덱스
            int remainingCount = totalMonsterCount - rowStartIndex; // 현재 줄부터 남은 몬스터 수
            int rowCount = Mathf.Min(frontRowCount, Mathf.Max(0, remainingCount)); // 현재 줄에 실제로 배치될 몬스터 수

            if (rowCount <= 0) // 안전장치
            {
                rowCount = frontRowCount; // 기본 줄 개수 사용
            }

            float centeredColumn = columnIndex - (rowCount - 1) * 0.5f; // 현재 줄 가운데를 기준으로 좌우 위치를 계산한다.
            float sideOffset = centeredColumn * columnSpacing; // 좌우 간격 적용
            float backOffset = rowIndex * rowSpacing; // 줄 번호에 따른 뒤쪽 간격 적용

            Vector3 right = gate.right; // 게이트 기준 오른쪽 방향
            right.y = 0.0f; // 높이 제거

            if (right.sqrMagnitude <= 0.0001f) // 오른쪽 방향 계산이 불가능하다면
            {
                right = Vector3.right; // 월드 오른쪽 방향 사용
            }

            right.Normalize(); // 길이 1로 만든다.

            Vector3 forward = gate.forward; // 게이트 기준 앞 방향
            forward.y = 0.0f; // 높이 제거

            if (forward.sqrMagnitude <= 0.0001f) // 앞 방향 계산이 불가능하다면
            {
                forward = Vector3.forward; // 월드 앞 방향 사용
            }

            forward.Normalize(); // 길이 1로 만든다.

            return right * sideOffset - forward * backOffset; // 앞줄 기준으로 뒤쪽 줄을 추가한 오와열 위치를 반환한다.
        }

        private EnemyController SpawnMonster(EnemyController prefab, Vector3 spawnPosition, Quaternion gateRotation) // 몬스터 하나 생성
        {
            Transform root = monsterRoot != null ? monsterRoot : transform; // 몬스터 부모 선택
            EnemyController monster = Instantiate(prefab, spawnPosition, gateRotation, root); // 몬스터 생성

            monster.name = $"{prefab.name}_{++spawnSerial:000}"; // 생성된 몬스터 이름에 번호를 붙인다.
            return monster;
        }

        private Transform PickRandomGateFromAllDirections() // 모든 방향 중 랜덤 게이트 선택
        {
            for (int i = 0; i < 20; i++) // 여러 번 시도한다.
            {
                SpawnDirection direction = (SpawnDirection)Random.Range(0, CardinalSpawnDirectionCount); // 기존 Stage Rules는 십자 4방향 중 하나 선택
                Transform gate = PickRandomGate(direction); // 해당 방향 게이트 선택

                if (gate != null) // 게이트가 있다면
                {
                    return gate; // 반환
                }
            }

            return null; // 사용할 수 있는 게이트가 없다.
        }

        private SpawnDirection[] PickRandomDirectionsForExternalWave(int directionCount) // 외부 웨이브가 사용할 방향을 8방향 후보에서 랜덤 선택
        {
            SpawnDirection[] candidates = new SpawnDirection[AllSpawnDirectionCount]; // 연결 가능한 방향 후보
            int candidateCount = 0; // 실제 연결된 방향 개수

            for (int i = 0; i < AllSpawnDirectionCount; i++) // 8방향 전체를 확인한다.
            {
                SpawnDirection direction = (SpawnDirection)i; // 현재 방향

                if (!HasValidGate(direction)) // 이 방향에 연결된 게이트가 없다면
                {
                    continue; // 후보에서 제외한다.
                }

                candidates[candidateCount] = direction; // 후보에 추가한다.
                candidateCount++; // 후보 개수 증가
            }

            if (candidateCount <= 0) // 사용할 수 있는 방향이 없다면
            {
                return null;
            }

            int selectedCount = Mathf.Clamp(directionCount, 1, candidateCount); // 요청 방향 수를 실제 후보 개수 안으로 제한한다.

            for (int i = 0; i < selectedCount; i++) // Fisher-Yates 방식으로 앞쪽 selectedCount개만 섞는다.
            {
                int randomIndex = Random.Range(i, candidateCount); // 아직 선택되지 않은 후보 중 하나
                SpawnDirection temp = candidates[i]; // 현재 값을 임시 저장
                candidates[i] = candidates[randomIndex]; // 랜덤 후보를 앞으로 이동
                candidates[randomIndex] = temp; // 기존 값을 뒤로 이동
            }

            SpawnDirection[] selectedDirections = new SpawnDirection[selectedCount]; // 최종 선택 방향 배열

            for (int i = 0; i < selectedCount; i++) // 선택된 방향만 복사한다.
            {
                selectedDirections[i] = candidates[i];
            }

            return selectedDirections; // 이번 웨이브 요청에서 사용할 방향 목록
        }

        private static int[] ConvertToDirectionIndexes(SpawnDirection[] directions) // 내부 방향 enum을 외부 저장용 숫자 배열로 바꾼다.
        {
            if (directions == null || directions.Length == 0)
            {
                return null;
            }

            int[] directionIndexes = new int[directions.Length];

            for (int i = 0; i < directions.Length; i++)
            {
                directionIndexes[i] = (int)directions[i];
            }

            return directionIndexes;
        }

        private static SpawnDirection[] ConvertToSpawnDirections(int[] directionIndexes) // 외부 저장용 숫자 배열을 내부 방향 enum으로 되돌린다.
        {
            if (directionIndexes == null || directionIndexes.Length == 0)
            {
                return null;
            }

            SpawnDirection[] directions = new SpawnDirection[directionIndexes.Length];

            for (int i = 0; i < directionIndexes.Length; i++)
            {
                directions[i] = (SpawnDirection)Mathf.Clamp(directionIndexes[i], 0, AllSpawnDirectionCount - 1);
            }

            return directions;
        }

        private Transform PickRandomGate(SpawnDirection direction) // 방향별 게이트 배열에서 하나 선택
        {
            if (direction == SpawnDirection.Front)
            {
                return PickValidGate(frontGates);
            }

            if (direction == SpawnDirection.Back)
            {
                return PickValidGate(backGates);
            }

            if (direction == SpawnDirection.Left)
            {
                return PickValidGate(leftGates);
            }

            if (direction == SpawnDirection.Right)
            {
                return PickValidGate(rightGates);
            }

            if (direction == SpawnDirection.FrontLeft)
            {
                return PickValidGate(frontLeftGates);
            }

            if (direction == SpawnDirection.FrontRight)
            {
                return PickValidGate(frontRightGates);
            }

            if (direction == SpawnDirection.BackLeft)
            {
                return PickValidGate(backLeftGates);
            }

            if (direction == SpawnDirection.BackRight)
            {
                return PickValidGate(backRightGates);
            }

            return null;
        }

        private bool HasValidGate(SpawnDirection direction) // 해당 방향에 연결된 게이트가 하나라도 있는지 확인
        {
            if (direction == SpawnDirection.Front)
            {
                return HasValidGate(frontGates);
            }

            if (direction == SpawnDirection.Back)
            {
                return HasValidGate(backGates);
            }

            if (direction == SpawnDirection.Left)
            {
                return HasValidGate(leftGates);
            }

            if (direction == SpawnDirection.Right)
            {
                return HasValidGate(rightGates);
            }

            if (direction == SpawnDirection.FrontLeft)
            {
                return HasValidGate(frontLeftGates);
            }

            if (direction == SpawnDirection.FrontRight)
            {
                return HasValidGate(frontRightGates);
            }

            if (direction == SpawnDirection.BackLeft)
            {
                return HasValidGate(backLeftGates);
            }

            if (direction == SpawnDirection.BackRight)
            {
                return HasValidGate(backRightGates);
            }

            return false;
        }

        private bool HasValidGate(Transform[] gates) // 게이트 배열에 실제 Transform이 하나라도 있는지 확인
        {
            if (gates == null || gates.Length == 0) // 배열이 없다면
            {
                return false;
            }

            for (int i = 0; i < gates.Length; i++) // 배열을 순회한다.
            {
                if (gates[i] != null) // 연결된 게이트가 있다면
                {
                    return true;
                }
            }

            return false;
        }

        private Transform PickValidGate(Transform[] gates) // 비어 있지 않은 게이트 중 하나 선택
        {
            if (gates == null || gates.Length == 0) // 배열이 없다면
            {
                return null; // 선택 불가
            }

            int validCount = 0; // 실제 연결된 게이트 개수

            for (int i = 0; i < gates.Length; i++) // 배열을 순회한다.
            {
                if (gates[i] != null) // 연결된 게이트라면
                {
                    validCount++; // 유효 개수 증가
                }
            }

            if (validCount <= 0) // 유효한 게이트가 없다면
            {
                return null; // 선택 불가
            }

            int randomIndex = Random.Range(0, validCount); // 유효 게이트 중 랜덤 순번 선택

            for (int i = 0; i < gates.Length; i++) // 다시 배열을 순회한다.
            {
                if (gates[i] == null) // 비어 있다면
                {
                    continue; // 건너뛴다.
                }

                if (randomIndex == 0) // 선택된 순번이라면
                {
                    return gates[i]; // 이 게이트 반환
                }

                randomIndex--; // 다음 유효 게이트로 이동
            }

            return null; // 안전용 fallback
        }
    }
}
