using System; //안건준 추가 - 0629 (CurrentStageChanged 이벤트용 Action<T>)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace TeamProject01.Gameplay
{
    public sealed class WaveController : MonoBehaviour
    {
        public enum WaveRunState
        {
            Normal,
            Boss,
            Special
        }

        [Header("스테이지 진행")]
        [SerializeField] private bool autoStart = true; // Play 시작 시 자동으로 웨이브를 시작할지입니다.

        [Min(1)]
        [SerializeField] private int startStage = 1; // 처음 시작할 Stage 번호입니다.

        [Min(1.0f)]
        [SerializeField] private float stageDurationSeconds = 40.0f; // 일반 Stage 하나의 길이입니다.

        [Header("스테이지 클리어 조건")]
        [SerializeField] private bool advanceWhenAllMonstersCleared = true; // 몬스터가 전부 정리되면 시간과 상관없이 다음 Stage로 넘길지입니다.

        [Min(0.0f)]
        [SerializeField] private float clearCheckDelaySeconds = 1.0f; // Stage 시작 직후 바로 넘어가는 일을 막기 위한 대기 시간입니다.

        [Header("다이아 클리어 보상")]
        [SerializeField] private bool spawnDiamondRewardOnWaveStepClear = true; // 일정 웨이브 클리어 보상
        [Min(1)]
        [SerializeField] private int diamondRewardWaveStep = 5; // 5웨이브 단위
        [SerializeField] private int[] diamondRewardByWaveStep = { 30, 45, 60, 75, 90, 105, 115, 125 }; // 5~40웨이브 보상표

        [Header("담당 컴포넌트 연결")]
        [SerializeField] private EnemySpawner enemySpawner; // 기존 EnemySpawner의 외부 스폰 API만 사용합니다.
        [SerializeField] private bool disableSpawnerStageRulesUpdate = true; // 기존 Stage Rules 자동 스폰과 중복되지 않게 막습니다.
        [SerializeField] private NormalWaveSpawner normalWaveSpawner; // 일반 몬스터 수량/조합 담당입니다.
        [SerializeField] private EliteMixController eliteMixController; // 엘리트 비율/조합 담당입니다.
        [SerializeField] private EliteWaveSpawner eliteWaveSpawner; // 엘리트 몬스터 지연 스폰 담당입니다.
        [SerializeField] private bool enableBossWave = true; // 보스 웨이브 사용 여부는 지휘자인 WaveController가 관리합니다.
        [SerializeField] private BossWaveController bossWaveController; // 보스 등장 담당입니다.
        [SerializeField] private BonusChestWaveSpawner bonusChestWaveSpawner; // 보스/보상 상자 담당 컴포넌트 연결용입니다.

        [Header("확장 자리")]
        [SerializeField] private bool enableSpecialWaveExtension; // 추후 보상/마력 구슬 특수 Stage를 붙이기 위한 스위치입니다.
        [SerializeField] private MonoBehaviour specialWaveController; // 아직 직접 호출하지 않는 확장 자리입니다.
        [FormerlySerializedAs("goldCollectSpecialWave")]
        [SerializeField] private ManaOrbCollectSpecialWave manaOrbCollectSpecialWave; // 마력 구슬 수집 특수 Stage를 담당하는 컴포넌트입니다.

        private float elapsedStageSeconds; // 현재 Stage 안에서 흐른 시간입니다.
        private int currentStage; // 현재 Stage 번호입니다.
        private bool isRunning; // 웨이브 진행 여부입니다.
        private bool specialWaveActive; // 외부 특수 웨이브가 일반 스폰을 잠글 때 사용하는 값입니다.
        private bool waitingForBossClearStage; // 보스 처치로 종료되는 Stage인지 기록합니다.
        private bool waitingForSpecialWaveStage; // 특수 Stage 보상 종료를 기다리는지 기록합니다.
        private bool skipSpecialWaveCheckOnce; // 보너스 Stage가 끝난 뒤 같은 Stage를 일반 웨이브로 시작하기 위한 플래그입니다.

        private readonly List<EnemyController> currentStageEnemies = new List<EnemyController>(256); // 이번 Stage에서 WaveSystem이 직접 생성한 몬스터 목록입니다.
        private int currentStageTargetEnemyCount; // 이번 Stage에 나올 예정이었던 몬스터 수입니다.
        private int currentStageDefeatedEnemyCount; // 이번 Stage 몬스터 중 이미 처치된 수입니다.
        private int currentStageTrackingStage; // 현재 추적 중인 Stage 번호입니다.

        public int CurrentStage => currentStage;
        public event Action<int> CurrentStageChanged; //안건준 추가 - 0629 (웨이브 변경 시 SaveData에 기록용 웨이브 번호 저장 알림)
        //안건준 추가 - 0630: 일반/보스/특수 전환 시 구독자에게 알림 (AudioManager BGM 전환)
        public event Action<WaveRunState> RunStateChanged;
        public float StageDurationSeconds => stageDurationSeconds;
        public float RemainingStageSeconds => Mathf.Max(0.0f, stageDurationSeconds - elapsedStageSeconds);
        public bool IsSpecialWaveActive => enableSpecialWaveExtension && specialWaveActive;
        public bool IsWaitingForBossClearStage => waitingForBossClearStage;
        public bool UsesStageTimer => !waitingForBossClearStage && !waitingForSpecialWaveStage;
        public ManaOrbCollectSpecialWave CurrentManaOrbCollectSpecialWave => IsSpecialWaveActive ? manaOrbCollectSpecialWave : null;
        public int CurrentStageTargetEnemyCount => currentStageTargetEnemyCount;
        public int CurrentStageRemainingEnemyCount
        {
            get
            {
                RefreshCurrentStageEnemyProgress();
                return Mathf.Max(0, currentStageTargetEnemyCount - currentStageDefeatedEnemyCount);
            }
        }

        //안건준 추가 - 0630: 현재 웨이브 종류 조회 — 특수 > 보스 > 일반 우선순위
        public WaveRunState CurrentState
        {
            get
            {
                if (IsSpecialWaveActive)
                {
                    return WaveRunState.Special;
                }

                if (waitingForBossClearStage || bossWaveController != null && bossWaveController.HasActiveBoss)
                {
                    return WaveRunState.Boss;
                }

                return WaveRunState.Normal;
            }
        }

        public void SetSpecialWaveActive(bool active)
        {
            specialWaveActive = enableSpecialWaveExtension && active;
        }

        public void BeginCurrentStageEnemyTracking(int stage, int targetEnemyCount)
        {
            RefreshCurrentStageEnemyProgress();
            currentStageTrackingStage = stage;
            currentStageTargetEnemyCount += Mathf.Max(0, targetEnemyCount);
        }

        public void RegisterCurrentStageEnemies(int stage, List<EnemyController> spawnedEnemies)
        {
            if (stage != currentStageTrackingStage || spawnedEnemies == null)
            {
                return;
            }

            for (int i = 0; i < spawnedEnemies.Count; i++)
            {
                EnemyController enemy = spawnedEnemies[i];

                if (enemy == null || currentStageEnemies.Contains(enemy))
                {
                    continue;
                }

                currentStageEnemies.Add(enemy);
            }
        }

        public void CompleteCurrentStageEnemySpawning(int stage)
        {
            if (stage != currentStageTrackingStage)
            {
                return;
            }

            RefreshCurrentStageEnemyProgress();
        }

        private void Reset()
        {
            autoStart = true;
            startStage = 1;
            stageDurationSeconds = 40.0f;
            disableSpawnerStageRulesUpdate = true;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start() //안건준 수정 - 0629 (SaveData 다이아·강화 복원 후 웨이브 1부터 시작)
        {
            if (autoStart)
            {
                StartWave();
            }
        }

        private void Update()
        {
            if (!isRunning)
            {
                return;
            }

            if (waitingForBossClearStage)
            {
                if (bossWaveController == null || !bossWaveController.HasActiveBoss)
                {
                    waitingForBossClearStage = false;
                    AdvanceStage();
                }

                return;
            }

            if (waitingForSpecialWaveStage)
            {
                return;
            }

            elapsedStageSeconds += Time.deltaTime;

            if (ShouldAdvanceByClear() || elapsedStageSeconds >= stageDurationSeconds)
            {
                AdvanceStage();
            }
        }

        public void StartWave()
        {
            ResolveReferences();
            DisableLegacySpawnerUpdateIfNeeded();

            currentStage = Mathf.Max(1, startStage);
            elapsedStageSeconds = 0.0f;
            specialWaveActive = false;
            waitingForBossClearStage = false;
            waitingForSpecialWaveStage = false;
            skipSpecialWaveCheckOnce = false;
            isRunning = true;
            ResetEnemyTracking();

            SegmentDpsDebugMeter.ResetRun(); // DPS 미터 전체 누적 초기화
            CurrentStageChanged?.Invoke(currentStage); //안건준 추가 - 0629 (웨이브 시작 시 구독자에게 현재 Stage 알림)
            StartCurrentStage();
        }

        private void StartCurrentStage()
        {
            ResolveReferences();
            SegmentDpsDebugMeter.BeginWave(currentStage); // 이번 웨이브 기록 초기화

            eliteWaveSpawner?.StopCurrentStage(); // 이전 Stage의 지연 엘리트 루틴이 남아 있으면 정리
            BeginCurrentStageEnemyTracking(currentStage, 0);
            specialWaveActive = false;
            waitingForSpecialWaveStage = false;

            if (!skipSpecialWaveCheckOnce && TryStartManaOrbCollectSpecialWave())
            {
                NotifyRunStateChanged(); //안건준 추가 - 0630: 특수 웨이브 시작 → EventStage BGM
                return;
            }

            skipSpecialWaveCheckOnce = false;

            bool bossSpawned = false;

            if (enableBossWave && bossWaveController != null)
            {
                bossSpawned = bossWaveController.BeginStage(currentStage);
                waitingForBossClearStage = bossSpawned && bossWaveController.ShouldEndStageOnBossClear;
            }

            if (bossSpawned && bossWaveController != null && bossWaveController.ShouldPauseNormalSpawn)
            {
                NotifyRunStateChanged(); //안건준 추가 - 0630: 보스 웨이브 시작 → Boss BGM
                return;
            }

            if (normalWaveSpawner == null)
            {
                NotifyRunStateChanged(); //안건준 추가 - 0630: 일반 스폰 없음 — 현재 상태만 알림
                return;
            }

            int totalSpawnCount = normalWaveSpawner.CalculateTotalSpawnCount(currentStage);
            int requestedEliteSpawnCount = eliteMixController != null
                ? eliteMixController.CalculateEliteCount(currentStage, totalSpawnCount)
                : 0;

            EliteMixController.EliteStagePlan elitePlan = eliteMixController != null
                ? eliteMixController.BuildStagePlan(currentStage, requestedEliteSpawnCount)
                : default;

            int normalSpawnCount = Mathf.Max(0, totalSpawnCount - elitePlan.TotalCount);
            normalSpawnCount = normalWaveSpawner.ResolveNormalSpawnCount(currentStage, normalSpawnCount);
            normalWaveSpawner.BeginStage(currentStage, stageDurationSeconds, normalSpawnCount, this);
            eliteWaveSpawner?.BeginStage(currentStage, elitePlan, normalWaveSpawner.ResolveDifficultyForStage(currentStage), normalWaveSpawner, this);
            NotifyRunStateChanged(); //안건준 추가 - 0630: 일반 웨이브 시작 → Stage BGM
        }

        //안건준 추가 - 0630: RunStateChanged 구독자에게 CurrentState 전달
        private void NotifyRunStateChanged()
        {
            RunStateChanged?.Invoke(CurrentState);
        }

        private bool TryStartManaOrbCollectSpecialWave()
        {
            if (!enableSpecialWaveExtension || manaOrbCollectSpecialWave == null)
            {
                return false;
            }

            bool isBossStage = enableBossWave && bossWaveController != null && bossWaveController.IsBossStage(currentStage);

            if (!manaOrbCollectSpecialWave.TryBeginStage(currentStage, isBossStage, HandleManaOrbCollectSpecialWaveFinished))
            {
                return false;
            }

            specialWaveActive = true;
            waitingForSpecialWaveStage = true;
            return true;
        }

        private void HandleManaOrbCollectSpecialWaveFinished()
        {
            if (!isRunning || !waitingForSpecialWaveStage)
            {
                return;
            }

            specialWaveActive = false;
            waitingForSpecialWaveStage = false;
            elapsedStageSeconds = 0.0f;
            skipSpecialWaveCheckOnce = true;
            StartCurrentStage();
        }

        private void RefreshCurrentStageEnemyProgress()
        {
            for (int i = currentStageEnemies.Count - 1; i >= 0; i--)
            {
                EnemyController enemy = currentStageEnemies[i];

                if (enemy != null && !enemy.IsDead)
                {
                    continue;
                }

                currentStageEnemies.RemoveAt(i);
                currentStageDefeatedEnemyCount = Mathf.Min(currentStageTargetEnemyCount, currentStageDefeatedEnemyCount + 1);
            }
        }

        private void ResetEnemyTracking()
        {
            currentStageTrackingStage = 0;
            currentStageTargetEnemyCount = 0;
            currentStageDefeatedEnemyCount = 0;
            currentStageEnemies.Clear();
        }

        private bool ShouldAdvanceByClear()
        {
            if (!advanceWhenAllMonstersCleared || IsSpecialWaveActive)
            {
                return false;
            }

            if (elapsedStageSeconds < clearCheckDelaySeconds)
            {
                return false;
            }

            return CurrentStageRemainingEnemyCount <= 0;
        }

        private void AdvanceStage()
        {
            int completedStage = currentStage; // 보상 기준 웨이브
            TrySpawnWaveClearDiamondReward(completedStage); // 클리어 다이아 픽업
            elapsedStageSeconds = 0.0f;
            skipSpecialWaveCheckOnce = false;
            currentStage++;
            CurrentStageChanged?.Invoke(currentStage); //안건준 추가 - 0629 (다음 웨이브 진입 시 구독자에게 Stage 알림)
            StartCurrentStage();
        }

        private void TrySpawnWaveClearDiamondReward(int completedStage) // 웨이브 클리어 다이아
        {
            if (!spawnDiamondRewardOnWaveStepClear || completedStage <= 0)
            {
                return; // 보상 비활성
            }

            if (completedStage % Mathf.Max(1, diamondRewardWaveStep) != 0)
            {
                return; // 보상 웨이브 아님
            }

            int reward = ResolveWaveClearDiamondReward(completedStage);
            if (reward <= 0)
            {
                return; // 지급 없음
            }

            RewardDropService.SpawnDiamond(reward, ResolveWaveRewardDropPosition()); // 월드 픽업 생성
        }

        private int ResolveWaveClearDiamondReward(int completedStage) // 보상표 조회
        {
            if (diamondRewardByWaveStep == null || diamondRewardByWaveStep.Length == 0)
            {
                return 0; // 테이블 없음
            }

            int stepIndex = Mathf.Max(0, completedStage / Mathf.Max(1, diamondRewardWaveStep) - 1);
            int clampedIndex = Mathf.Min(stepIndex, diamondRewardByWaveStep.Length - 1); // 40 이후는 마지막 값 반복
            return Mathf.Max(0, diamondRewardByWaveStep[clampedIndex]); // 안전 보정
        }

        private Vector3 ResolveWaveRewardDropPosition() // 웨이브 보상 위치
        {
            NexusController nexus = NexusController.Active;
            return nexus != null ? nexus.transform.position : transform.position; // 넥서스 근처 우선
        }

        private void ResolveReferences()
        {
            if (enemySpawner == null)
            {
                enemySpawner = FindFirstObjectByType<EnemySpawner>();
            }

            if (normalWaveSpawner == null)
            {
                normalWaveSpawner = ResolveWaveSiblingOrSceneComponent<NormalWaveSpawner>();
            }

            if (eliteMixController == null)
            {
                eliteMixController = ResolveWaveSiblingOrSceneComponent<EliteMixController>();
            }

            if (eliteWaveSpawner == null)
            {
                eliteWaveSpawner = ResolveWaveSiblingOrSceneComponent<EliteWaveSpawner>();
            }

            if (eliteWaveSpawner == null)
            {
                eliteWaveSpawner = gameObject.AddComponent<EliteWaveSpawner>(); // 기존 씬 수정 없이 런타임에서 분리 스포너를 보강
            }

            if (bossWaveController == null)
            {
                bossWaveController = ResolveWaveSiblingOrSceneComponent<BossWaveController>();
            }

            if (bonusChestWaveSpawner == null)
            {
                bonusChestWaveSpawner = ResolveWaveSiblingOrSceneComponent<BonusChestWaveSpawner>();
            }

            if (manaOrbCollectSpecialWave == null)
            {
                manaOrbCollectSpecialWave = ResolveWaveSiblingOrSceneComponent<ManaOrbCollectSpecialWave>();
            }
        }

        private T ResolveWaveSiblingOrSceneComponent<T>() where T : Component
        {
            T component = GetComponent<T>();

            if (component != null)
            {
                return component;
            }

            if (transform.parent != null)
            {
                component = transform.parent.GetComponentInChildren<T>(true);

                if (component != null)
                {
                    return component;
                }
            }

            return FindFirstObjectByType<T>();
        }

        private void DisableLegacySpawnerUpdateIfNeeded()
        {
            if (!disableSpawnerStageRulesUpdate || enemySpawner == null)
            {
                return;
            }

            enemySpawner.enabled = false;
        }
    }
}
