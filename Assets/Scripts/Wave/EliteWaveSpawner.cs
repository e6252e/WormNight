using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EliteWaveSpawner : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private EnemySpawner enemySpawner; // 실제 생성은 기존 EnemySpawner 외곽 스폰 API를 사용합니다.
        [SerializeField] private NormalWaveSpawner normalWaveSpawner; // 마지막 일반 몬스터 스폰 방향을 참조합니다.

        [Header("엘리트 지연 스폰")]
        [Min(0.0f)]
        [SerializeField] private float spawnDelaySeconds = 3.0f; // 웨이브 시작 후 엘리트가 등장하기까지의 시간입니다.

        [Range(1, 8)]
        [SerializeField] private int earlyGateCount = 2; // 초반에 사용할 랜덤 게이트 방향 수입니다.

        [Min(1)]
        [SerializeField] private int midGateStartStage = 10; // 중반 게이트 수를 적용할 시작 Stage입니다.

        [Range(1, 8)]
        [SerializeField] private int midGateCount = 4; // 중반에 사용할 랜덤 게이트 방향 수입니다.

        [Min(1)]
        [SerializeField] private int lateGateStartStage = 20; // 후반 게이트 수를 적용할 시작 Stage입니다.

        [Range(1, 8)]
        [SerializeField] private int lateGateCount = 8; // 후반에 사용할 랜덤 게이트 방향 수입니다.

        [Min(1)]
        [SerializeField] private int frontRowCount = 3; // 엘리트 대형의 앞줄 배치 수입니다.

        [Header("엘리트 대형 설정")]
        [SerializeField] private EnemySpawner.ExternalSpawnFormationMode spawnFormationMode = EnemySpawner.ExternalSpawnFormationMode.FilledCircleRows; // 엘리트 묶음 기본 대형입니다.

        [SerializeField] private bool keepEliteCombinationAsSingleGroup = true; // 켜면 엘리트 조합을 한 게이트의 한 묶음으로 유지합니다.

        [Min(0.0f)]
        [SerializeField] private float circleFormationCenterJitterRadius = 4.0f; // 대형 중심을 선택된 게이트 바깥쪽 반원 범위 안에서 랜덤 이동할 반경입니다.

        [SerializeField] private bool randomizeCircleFormationRotation = true; // FilledCircleRows에서 원형 오와열 방향을 랜덤 회전합니다.

        [Header("엘리트 난이도 적용")]
        [SerializeField] private bool applyNexusDamageScaleToElites = false; // 엘리트 공격력은 Prefab 기본값 2를 유지합니다.

        [Header("스폰 퍼짐 설정")]
        [SerializeField] private bool useSpawnSpread = false; // 켜면 선택된 방향 안에서 엘리트 위치를 조금 퍼뜨립니다.

        [Min(0.0f)]
        [SerializeField] private float spawnSpreadAmount = 6.0f; // 숫자가 클수록 넓게 퍼져 생성됩니다.

        [Header("스폰 혼잡 보정")]
        [SerializeField] private bool useCongestionPush = false; // 켜면 스폰 예정 위치가 붐빌 때 넥서스 반대 방향으로 더 멀리 생성합니다.

        [Min(0.1f)]
        [SerializeField] private float congestionCheckRadius = 8.0f; // 스폰 예정 위치 주변을 검사할 반경입니다.

        [Min(1)]
        [SerializeField] private int congestionMonsterThreshold = 12; // 이 수 이상 몬스터가 있으면 혼잡하다고 판단합니다.

        [Min(0.0f)]
        [SerializeField] private float congestionPushDistance = 5.0f; // 혼잡할 때 한 번에 뒤로 미는 거리입니다.

        [Min(0.0f)]
        [SerializeField] private float congestionMaxPushDistance = 20.0f; // 한 번 스폰에서 최대한 뒤로 밀 수 있는 거리입니다.

        private readonly List<EnemyController> spawnedElites = new List<EnemyController>(32); // 이번 엘리트 스폰 결과 임시 목록입니다.
        private Coroutine spawnRoutine; // 현재 Stage의 엘리트 지연 스폰 루틴입니다.

        public void BeginStage(int stage, EliteMixController.EliteStagePlan elitePlan, WaveStageDifficulty difficulty, NormalWaveSpawner normalSpawnSource = null, WaveController waveTracker = null)
        {
            ResolveEnemySpawner();
            ResolveNormalWaveSpawner(normalSpawnSource);
            StopCurrentStage();

            if (enemySpawner == null || !elitePlan.HasEntries)
            {
                return;
            }

            waveTracker?.BeginCurrentStageEnemyTracking(stage, elitePlan.TotalCount); // 10초 대기 중 조기 클리어를 막기 위해 예정 수량을 먼저 등록
            spawnRoutine = StartCoroutine(SpawnEliteStageRoutine(stage, elitePlan, difficulty, waveTracker));
        }

        public void StopCurrentStage()
        {
            if (spawnRoutine == null)
            {
                return;
            }

            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        private IEnumerator SpawnEliteStageRoutine(int stage, EliteMixController.EliteStagePlan elitePlan, WaveStageDifficulty difficulty, WaveController waveTracker)
        {
            float delay = Mathf.Max(0.0f, spawnDelaySeconds);

            if (delay > 0.0f)
            {
                yield return new WaitForSeconds(delay);
            }

            EnemySpawner.ExternalSpawnDirectionSet directionSet = ResolveEliteSpawnDirectionSet(stage);
            EnemySpawner.ExternalSpawnCongestionOptions congestionOptions = BuildCongestionOptions();
            EnemySpawner.ExternalSpawnSpreadOptions spreadOptions = BuildSpreadOptions();
            EnemySpawner.ExternalSpawnFormationOptions formationOptions = BuildFormationOptions();

            spawnedElites.Clear();

            if (enemySpawner.TrySpawnExternalEntriesDistributed(elitePlan.Entries, directionSet, frontRowCount, congestionOptions, spreadOptions, formationOptions, spawnedElites))
            {
                ApplyStageDifficulty(spawnedElites, difficulty, applyNexusDamageScaleToElites);
                MarkSpawnedElites(spawnedElites, elitePlan.CombinationType);
                waveTracker?.RegisterCurrentStageEnemies(stage, spawnedElites);
            }

            waveTracker?.CompleteCurrentStageEnemySpawning(stage);
            spawnRoutine = null;
        }

        private int GetGateCountForStage(int stage)
        {
            if (stage >= lateGateStartStage)
            {
                return lateGateCount;
            }

            if (stage >= midGateStartStage)
            {
                return midGateCount;
            }

            return earlyGateCount;
        }

        private EnemySpawner.ExternalSpawnDirectionSet ResolveEliteSpawnDirectionSet(int stage)
        {
            if (normalWaveSpawner != null && normalWaveSpawner.TryGetLastNormalSpawnDirectionSet(out EnemySpawner.ExternalSpawnDirectionSet lastNormalDirectionSet))
            {
                return lastNormalDirectionSet;
            }

            return enemySpawner.PickExternalSpawnDirections(GetGateCountForStage(stage));
        }

        private EnemySpawner.ExternalSpawnCongestionOptions BuildCongestionOptions()
        {
            if (!useCongestionPush)
            {
                return EnemySpawner.ExternalSpawnCongestionOptions.Disabled;
            }

            return new EnemySpawner.ExternalSpawnCongestionOptions(
                true,
                congestionCheckRadius,
                congestionMonsterThreshold,
                congestionPushDistance,
                congestionMaxPushDistance);
        }

        private EnemySpawner.ExternalSpawnSpreadOptions BuildSpreadOptions()
        {
            if (!useSpawnSpread)
            {
                return EnemySpawner.ExternalSpawnSpreadOptions.Disabled;
            }

            return new EnemySpawner.ExternalSpawnSpreadOptions(true, spawnSpreadAmount);
        }

        private EnemySpawner.ExternalSpawnFormationOptions BuildFormationOptions()
        {
            return new EnemySpawner.ExternalSpawnFormationOptions(
                spawnFormationMode,
                keepEliteCombinationAsSingleGroup,
                circleFormationCenterJitterRadius,
                randomizeCircleFormationRotation);
        }

        private static void MarkSpawnedElites(List<EnemyController> elites, string eliteCombinationType)
        {
            if (elites == null || string.IsNullOrWhiteSpace(eliteCombinationType))
            {
                return;
            }

            for (int i = 0; i < elites.Count; i++)
            {
                EnemyController enemy = elites[i];

                if (enemy == null)
                {
                    continue;
                }

                WaveSpawnedEliteMarker marker = enemy.GetComponent<WaveSpawnedEliteMarker>();

                if (marker == null)
                {
                    marker = enemy.gameObject.AddComponent<WaveSpawnedEliteMarker>();
                }

                marker.Initialize(eliteCombinationType);
            }
        }

        private static void ApplyStageDifficulty(List<EnemyController> elites, WaveStageDifficulty difficulty, bool applyNexusDamageMultiplier)
        {
            if (elites == null)
            {
                return;
            }

            for (int i = 0; i < elites.Count; i++)
            {
                EnemyStageDifficultyApplier.Apply(elites[i], difficulty, applyNexusDamageMultiplier);
            }
        }

        private void ResolveEnemySpawner()
        {
            if (enemySpawner == null)
            {
                enemySpawner = FindFirstObjectByType<EnemySpawner>();
            }
        }

        private void ResolveNormalWaveSpawner(NormalWaveSpawner normalSpawnSource)
        {
            if (normalSpawnSource != null)
            {
                normalWaveSpawner = normalSpawnSource;
                return;
            }

            if (normalWaveSpawner == null)
            {
                normalWaveSpawner = GetComponent<NormalWaveSpawner>();
            }

            if (normalWaveSpawner == null && transform.parent != null)
            {
                normalWaveSpawner = transform.parent.GetComponentInChildren<NormalWaveSpawner>(true);
            }

            if (normalWaveSpawner == null)
            {
                normalWaveSpawner = FindFirstObjectByType<NormalWaveSpawner>();
            }
        }
    }
}
