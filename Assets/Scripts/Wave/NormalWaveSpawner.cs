using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class NormalWaveSpawner : MonoBehaviour
    {
        [Serializable]
        public sealed class SpawnScaleStep
        {
            [Min(1)]
            public int startStage = 1; // 이 Stage부터 적용할 몬스터 수 배율입니다.

            [Min(0)]
            public int spawnScalePercent = 100; // 100이면 기본 수량 그대로, 110이면 10% 증가입니다.
        }

        [Serializable]
        public sealed class DifficultyScaleStep
        {
            [Min(1)]
            public int startStage = 1; // 이 Stage부터 적용할 난이도 배율입니다.

            [Min(1)]
            public int healthScalePercent = 100; // 몬스터 최대 체력 배율입니다.

            [Min(1)]
            public int moveSpeedScalePercent = 100; // 몬스터 이동속도 배율입니다.

            [Min(1)]
            public int nexusDamageScalePercent = 100; // Nexus에 주는 피해 배율입니다.
        }

        [Serializable]
        public sealed class MonsterRatioEntry
        {
            public EnemyController prefab; // Project 창의 몬스터 Prefab을 Inspector에서 직접 연결합니다.

            [Range(0, 100)]
            public int ratioPercent = 100; // 이 조합 안에서 해당 몬스터가 차지하는 비율입니다.
        }

        [Serializable]
        public sealed class NormalComposition
        {
            public string compositionId = "N01"; // 문서와 대화에서 구분하기 쉬운 조합 ID입니다.
            public string displayName = "기본 근접"; // Inspector에 보일 이름입니다.

            [Min(1)]
            public int minStage = 1; // 이 Stage부터 후보 조합에 포함됩니다.

            [Min(0)]
            public int weight = 100; // 여러 후보 중 선택될 확률 가중치입니다.

            public MonsterRatioEntry[] monsters = Array.Empty<MonsterRatioEntry>(); // 실제로 섞어 스폰할 몬스터 목록입니다.

            public bool IsAvailable(int stage)
            {
                return stage >= minStage && weight > 0 && HasValidMonster();
            }

            private bool HasValidMonster()
            {
                if (monsters == null)
                {
                    return false;
                }

                for (int i = 0; i < monsters.Length; i++)
                {
                    MonsterRatioEntry monster = monsters[i];

                    if (monster != null && monster.prefab != null && monster.ratioPercent > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private readonly struct CountEntry
        {
            public readonly EnemyController Prefab;
            public readonly int Count;

            public CountEntry(EnemyController prefab, int count)
            {
                Prefab = prefab;
                Count = count;
            }
        }

        [Header("참조")]
        [SerializeField] private EnemySpawner enemySpawner; // 실제 생성은 기존 EnemySpawner API에 맡깁니다.

        [Header("수량 설정")]
        [Min(0)]
        [SerializeField] private int baseSpawnCount = 30; // Stage 1 기준 일반 몬스터 수입니다.

        [SerializeField] private SpawnScaleStep[] spawnScaleSteps =
        {
            new SpawnScaleStep { startStage = 1, spawnScalePercent = 100 },
            new SpawnScaleStep { startStage = 6, spawnScalePercent = 110 },
            new SpawnScaleStep { startStage = 12, spawnScalePercent = 130 }
        };

        [Serializable]
        public sealed class NormalSpawnCountOverride
        {
            [Min(1)]
            public int stage = 1; // 정확히 이 Stage에서만 적용할 일반 몬스터 수입니다.

            [Min(0)]
            public int normalSpawnCount = 30; // 엘리트를 제외한 일반 몬스터 수입니다.
        }

        [Header("초중반 일반 수량 보정")]
        [SerializeField] private NormalSpawnCountOverride[] normalSpawnCountOverrides =
        {
            new NormalSpawnCountOverride { stage = 1, normalSpawnCount = 35 },
            new NormalSpawnCountOverride { stage = 2, normalSpawnCount = 35 },
            new NormalSpawnCountOverride { stage = 3, normalSpawnCount = 40 },
            new NormalSpawnCountOverride { stage = 4, normalSpawnCount = 40 },
            new NormalSpawnCountOverride { stage = 5, normalSpawnCount = 55 },
            new NormalSpawnCountOverride { stage = 6, normalSpawnCount = 60 },
            new NormalSpawnCountOverride { stage = 7, normalSpawnCount = 65 },
            new NormalSpawnCountOverride { stage = 8, normalSpawnCount = 75 },
            new NormalSpawnCountOverride { stage = 9, normalSpawnCount = 80 },
            new NormalSpawnCountOverride { stage = 10, normalSpawnCount = 85 },
            new NormalSpawnCountOverride { stage = 11, normalSpawnCount = 90 },
            new NormalSpawnCountOverride { stage = 12, normalSpawnCount = 100 },
            new NormalSpawnCountOverride { stage = 13, normalSpawnCount = 105 },
            new NormalSpawnCountOverride { stage = 14, normalSpawnCount = 110 },
            new NormalSpawnCountOverride { stage = 15, normalSpawnCount = 115 },
            new NormalSpawnCountOverride { stage = 16, normalSpawnCount = 130 },
            new NormalSpawnCountOverride { stage = 17, normalSpawnCount = 135 },
            new NormalSpawnCountOverride { stage = 18, normalSpawnCount = 140 },
            new NormalSpawnCountOverride { stage = 19, normalSpawnCount = 145 }
        };

        [Header("난이도 배율")]
        [SerializeField] private DifficultyScaleStep[] difficultyScaleSteps =
        {
            new DifficultyScaleStep { startStage = 1, healthScalePercent = 100, moveSpeedScalePercent = 100, nexusDamageScalePercent = 100 },
            new DifficultyScaleStep { startStage = 8, healthScalePercent = 105, moveSpeedScalePercent = 104, nexusDamageScalePercent = 100 },
            new DifficultyScaleStep { startStage = 12, healthScalePercent = 110, moveSpeedScalePercent = 107, nexusDamageScalePercent = 100 },
            new DifficultyScaleStep { startStage = 15, healthScalePercent = 115, moveSpeedScalePercent = 110, nexusDamageScalePercent = 100 },
            new DifficultyScaleStep { startStage = 18, healthScalePercent = 120, moveSpeedScalePercent = 112, nexusDamageScalePercent = 100 },
            new DifficultyScaleStep { startStage = 20, healthScalePercent = 130, moveSpeedScalePercent = 115, nexusDamageScalePercent = 110 },
            new DifficultyScaleStep { startStage = 25, healthScalePercent = 145, moveSpeedScalePercent = 118, nexusDamageScalePercent = 110 },
            new DifficultyScaleStep { startStage = 30, healthScalePercent = 160, moveSpeedScalePercent = 121, nexusDamageScalePercent = 120 },
            new DifficultyScaleStep { startStage = 35, healthScalePercent = 180, moveSpeedScalePercent = 124, nexusDamageScalePercent = 120 },
            new DifficultyScaleStep { startStage = 40, healthScalePercent = 200, moveSpeedScalePercent = 127, nexusDamageScalePercent = 130 }
        };

        [Header("일반 몬스터 조합")]
        [SerializeField] private NormalComposition[] normalCompositions =
        {
            CreateComposition("N01", "기본 근접", 1, 100, 100),
            CreateComposition("N02", "근접 섞기", 3, 80, 75, 25),
            CreateComposition("N03", "근접 + 원거리", 5, 70, 65, 20, 15),
            CreateComposition("N04", "원거리 + 석궁", 10, 50, 50, 25, 15, 10)
        };

        [Header("고급 설정")]
        [Range(1, 100)]
        [SerializeField] private int spawnWindowPercent = 40; // Stage 시간 중 앞쪽 몇 % 안에 집중 스폰할지입니다.

        [Min(1)]
        [SerializeField] private int spawnBatchCount = 4; // 일반 몬스터가 없을 때 쓰는 기존 묶음 수 fallback입니다.

        [Header("일반 스폰 분할")]
        [Min(0.1f)]
        [SerializeField] private float normalSpawnWindowSeconds = 20.0f; // 일반 몬스터를 이 시간 안에 나누어 스폰합니다.

        [Min(1)]
        [SerializeField] private int normalSpawnSplitDivisor = 5; // 일반 총량을 몇 등분 기준으로 1회 최대 수량을 잡을지입니다.

        [Min(1)]
        [SerializeField] private int minMonstersPerSpawnTick = 15; // 초반이 너무 잘게 쪼개지지 않게 하는 1회 최소 기준입니다.

        [Min(1)]
        [SerializeField] private int maxMonstersPerSpawnTick = 50; // 후반 한 번 스폰 덩어리의 최대 크기입니다.

        [SerializeField] private bool avoidPreviousSpawnDirections = true; // 바로 직전에 쓴 게이트 방향은 다음 틱 후보에서 제외합니다.

        [Range(1, 8)]
        [SerializeField] private int batchGateCount = 1; // 한 묶음이 실제로 사용할 게이트 방향 수입니다.

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
        [SerializeField] private int frontRowCount = 5; // 한 게이트에서 앞줄에 몇 마리씩 세울지입니다.

        [Header("스폰 대형 설정")]
        [SerializeField] private EnemySpawner.ExternalSpawnFormationMode spawnFormationMode = EnemySpawner.ExternalSpawnFormationMode.FilledCircleRows; // 일반 웨이브가 사용할 기본 대형입니다.

        [SerializeField] private bool disableWaveSpawnRedistribution = true; // 임시: 몬스터 단위 재섞기와 방향별 재분배를 끄고 한 묶음을 하나의 대형으로 유지합니다.

        [Min(0.0f)]
        [SerializeField] private float circleFormationCenterJitterRadius = 6.0f; // 대형 중심을 선택된 게이트 바깥쪽 반원 범위 안에서 랜덤 이동할 반경입니다.

        [SerializeField] private bool randomizeCircleFormationRotation = true; // FilledCircleRows에서만 원형 오와열 방향을 묶음마다 랜덤 회전합니다.

        [Header("스폰 퍼짐 설정")]
        [SerializeField] private bool useSpawnSpread = false; // 켜면 선택된 방향 안에서 몬스터 위치를 조금 퍼뜨립니다.

        [Min(0.0f)]
        [SerializeField] private float spawnSpreadAmount = 8.0f; // 숫자가 클수록 한 줄이 아니라 넓게 퍼져서 생성됩니다.

        [Header("스폰 혼잡 보정")]
        [SerializeField] private bool useCongestionPush = false; // 켜면 스폰 예정 위치가 붐빌 때 넥서스 반대 방향으로 조금 더 멀리 생성합니다.

        [Min(0.1f)]
        [SerializeField] private float congestionCheckRadius = 8.0f; // 스폰 예정 위치 주변을 검사할 반경입니다.

        [Min(1)]
        [SerializeField] private int congestionMonsterThreshold = 12; // 이 수 이상 몬스터가 있으면 혼잡하다고 판단합니다.

        [Min(0.0f)]
        [SerializeField] private float congestionPushDistance = 5.0f; // 혼잡할 때 한 번에 뒤로 미는 거리입니다.

        [Min(0.0f)]
        [SerializeField] private float congestionMaxPushDistance = 20.0f; // 한 번 스폰에서 최대한 뒤로 밀 수 있는 거리입니다.

        private Coroutine spawnRoutine; // 현재 Stage의 일반 몬스터 스폰 루틴입니다.
        private EnemySpawner.ExternalSpawnDirectionSet lastNormalSpawnDirectionSet; // 가장 마지막 일반 몬스터 스폰 방향입니다.
        private bool hasLastNormalSpawnDirectionSet; // 마지막 일반 스폰 방향이 유효한지 표시합니다.

        public int CalculateTotalSpawnCount(int stage)
        {
            float scale = GetScaleForStage(stage) / 100.0f;
            return Mathf.Max(0, Mathf.RoundToInt(baseSpawnCount * scale));
        }

        public int ResolveNormalSpawnCount(int stage, int fallbackSpawnCount)
        {
            if (normalSpawnCountOverrides == null)
            {
                return Mathf.Max(0, fallbackSpawnCount);
            }

            for (int i = 0; i < normalSpawnCountOverrides.Length; i++)
            {
                NormalSpawnCountOverride spawnCountOverride = normalSpawnCountOverrides[i];

                if (spawnCountOverride != null && spawnCountOverride.stage == stage)
                {
                    return Mathf.Max(0, spawnCountOverride.normalSpawnCount);
                }
            }

            return Mathf.Max(0, fallbackSpawnCount);
        }

        public void BeginStage(int stage, float stageDurationSeconds, int spawnCount, WaveController waveTracker = null)
        {
            ResolveEnemySpawner();
            StopCurrentRoutine();
            hasLastNormalSpawnDirectionSet = false;

            if (enemySpawner == null)
            {
                return;
            }

            List<CountEntry> totalEntries = new List<CountEntry>();

            if (spawnCount > 0)
            {
                NormalComposition composition = PickComposition(stage);

                if (composition != null)
                {
                    totalEntries.AddRange(BuildCountEntries(composition.monsters, spawnCount, stage));
                }
            }

            waveTracker?.BeginCurrentStageEnemyTracking(stage, GetTotalCount(totalEntries));

            if (totalEntries.Count == 0)
            {
                return;
            }

            int gateCount = GetGateCountForStage(stage);
            EnemySpawner.ExternalSpawnDirectionSet directionSet = enemySpawner.PickExternalSpawnDirections(gateCount);
            spawnRoutine = StartCoroutine(SpawnStageRoutine(stage, stageDurationSeconds, totalEntries, directionSet, waveTracker));
        }

        private IEnumerator SpawnStageRoutine(int stage, float stageDurationSeconds, List<CountEntry> totalEntries, EnemySpawner.ExternalSpawnDirectionSet directionSet, WaveController waveTracker)
        {
            int normalSpawnCount = GetTotalCount(totalEntries);
            int normalMonstersPerTick = ResolveMonstersPerSpawnTick(normalSpawnCount);
            int safeBatchCount = ResolveSpawnTickCount(normalSpawnCount, normalMonstersPerTick);
            float spawnWindowSeconds = ResolveSpawnWindowSeconds(stageDurationSeconds);
            WaveStageDifficulty difficulty = ResolveDifficultyForStage(stage);
            EnemySpawner.ExternalSpawnCongestionOptions congestionOptions = BuildCongestionOptions();
            EnemySpawner.ExternalSpawnSpreadOptions spreadOptions = BuildSpreadOptions();
            EnemySpawner.ExternalSpawnFormationOptions formationOptions = BuildFormationOptions();
            List<EnemyController> spawnedBatchMonsters = new List<EnemyController>();
            List<EnemyController> normalSpawnQueue = BuildSpawnQueue(totalEntries);
            int[] previousBatchDirectionIndexes = Array.Empty<int>();

            ShuffleEnemyPrefabs(normalSpawnQueue);

            for (int batchIndex = 0; batchIndex < safeBatchCount; batchIndex++)
            {
                if (batchIndex > 0)
                {
                    yield return new WaitForSeconds(spawnWindowSeconds / safeBatchCount);
                }

                EnemySpawner.ExternalSpawnDirectionSet batchDirectionSet = BuildBatchDirectionSet(directionSet, previousBatchDirectionIndexes, out previousBatchDirectionIndexes);
                EnemySpawner.ExternalSpawnEntry[] normalBatchEntries = BuildBatchEntriesFromQueue(normalSpawnQueue, batchIndex, normalMonstersPerTick);

                if (normalBatchEntries.Length > 0)
                {
                    spawnedBatchMonsters.Clear();

                    if (enemySpawner.TrySpawnExternalEntriesDistributed(normalBatchEntries, batchDirectionSet, frontRowCount, congestionOptions, spreadOptions, formationOptions, spawnedBatchMonsters))
                    {
                        lastNormalSpawnDirectionSet = batchDirectionSet;
                        hasLastNormalSpawnDirectionSet = true;
                        ApplyStageDifficulty(spawnedBatchMonsters, difficulty);
                        waveTracker?.RegisterCurrentStageEnemies(stage, spawnedBatchMonsters);
                    }
                }
            }

            waveTracker?.CompleteCurrentStageEnemySpawning(stage);
            spawnRoutine = null;
        }

        public bool TryGetLastNormalSpawnDirectionSet(out EnemySpawner.ExternalSpawnDirectionSet directionSet)
        {
            directionSet = lastNormalSpawnDirectionSet;
            return hasLastNormalSpawnDirectionSet && directionSet.IsValid;
        }

        private int ResolveMonstersPerSpawnTick(int normalSpawnCount)
        {
            if (normalSpawnCount <= 0)
            {
                return 0;
            }

            int divisor = Mathf.Max(1, normalSpawnSplitDivisor);
            int minPerTick = Mathf.Max(1, minMonstersPerSpawnTick);
            int maxPerTick = Mathf.Max(minPerTick, maxMonstersPerSpawnTick);
            return Mathf.Clamp(Mathf.RoundToInt(normalSpawnCount / (float)divisor), minPerTick, maxPerTick);
        }

        private int ResolveSpawnTickCount(int normalSpawnCount, int normalMonstersPerTick)
        {
            if (normalSpawnCount <= 0 || normalMonstersPerTick <= 0)
            {
                return Mathf.Max(1, spawnBatchCount);
            }

            return Mathf.Max(1, Mathf.CeilToInt(normalSpawnCount / (float)normalMonstersPerTick));
        }

        private float ResolveSpawnWindowSeconds(float stageDurationSeconds)
        {
            float fallbackWindowSeconds = stageDurationSeconds * (spawnWindowPercent / 100.0f);
            float configuredWindowSeconds = normalSpawnWindowSeconds > 0.0f ? normalSpawnWindowSeconds : fallbackWindowSeconds;
            return Mathf.Max(0.1f, Mathf.Min(stageDurationSeconds, configuredWindowSeconds));
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
                disableWaveSpawnRedistribution,
                circleFormationCenterJitterRadius,
                randomizeCircleFormationRotation);
        }

        private void StopCurrentRoutine()
        {
            if (spawnRoutine == null)
            {
                return;
            }

            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        private int GetScaleForStage(int stage)
        {
            int result = 100;

            if (spawnScaleSteps == null)
            {
                return result;
            }

            for (int i = 0; i < spawnScaleSteps.Length; i++)
            {
                SpawnScaleStep step = spawnScaleSteps[i];

                if (step != null && stage >= step.startStage)
                {
                    result = step.spawnScalePercent;
                }
            }

            return Mathf.Max(0, result);
        }

        public WaveStageDifficulty ResolveDifficultyForStage(int stage)
        {
            DifficultyScaleStep step = GetDifficultyStepForStage(stage);
            int healthPercent = step != null ? step.healthScalePercent : 100;
            int speedPercent = step != null ? step.moveSpeedScalePercent : 100;
            int damagePercent = step != null ? step.nexusDamageScalePercent : 100;

            return new WaveStageDifficulty(stage, healthPercent / 100.0f, speedPercent / 100.0f, damagePercent / 100.0f);
        }

        private DifficultyScaleStep GetDifficultyStepForStage(int stage)
        {
            DifficultyScaleStep result = null;

            if (difficultyScaleSteps == null)
            {
                return result;
            }

            for (int i = 0; i < difficultyScaleSteps.Length; i++)
            {
                DifficultyScaleStep step = difficultyScaleSteps[i];

                if (step != null && stage >= step.startStage)
                {
                    result = step;
                }
            }

            return result;
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

        private NormalComposition PickComposition(int stage)
        {
            if (normalCompositions == null)
            {
                return null;
            }

            int latestMinStage = int.MinValue;

            for (int i = 0; i < normalCompositions.Length; i++)
            {
                NormalComposition composition = normalCompositions[i];

                if (composition != null && composition.IsAvailable(stage))
                {
                    latestMinStage = Mathf.Max(latestMinStage, composition.minStage);
                }
            }

            if (latestMinStage == int.MinValue)
            {
                return null;
            }

            int totalWeight = 0;

            for (int i = 0; i < normalCompositions.Length; i++)
            {
                NormalComposition composition = normalCompositions[i];

                if (composition != null && composition.minStage == latestMinStage && composition.IsAvailable(stage))
                {
                    totalWeight += composition.weight;
                }
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            int randomWeight = UnityEngine.Random.Range(0, totalWeight);

            for (int i = 0; i < normalCompositions.Length; i++)
            {
                NormalComposition composition = normalCompositions[i];

                if (composition == null || composition.minStage != latestMinStage || !composition.IsAvailable(stage))
                {
                    continue;
                }

                randomWeight -= composition.weight;

                if (randomWeight < 0)
                {
                    return composition;
                }
            }

            return null;
        }

        private static List<CountEntry> BuildCountEntries(MonsterRatioEntry[] ratios, int totalCount, int stage)
        {
            List<CountEntry> results = new List<CountEntry>();

            if (ratios == null || ratios.Length == 0 || totalCount <= 0)
            {
                return results;
            }

            int totalRatio = 0;

            for (int i = 0; i < ratios.Length; i++)
            {
                MonsterRatioEntry ratio = ratios[i];

                if (ratio != null && ratio.prefab != null && ratio.ratioPercent > 0 && IsNormalMonsterUnlockedForStage(ratio.prefab, stage))
                {
                    totalRatio += ratio.ratioPercent;
                }
            }

            if (totalRatio <= 0)
            {
                return results;
            }

            int assignedCount = 0;
            int lastValidIndex = -1;

            for (int i = 0; i < ratios.Length; i++)
            {
                MonsterRatioEntry ratio = ratios[i];

                if (ratio == null || ratio.prefab == null || ratio.ratioPercent <= 0 || !IsNormalMonsterUnlockedForStage(ratio.prefab, stage))
                {
                    continue;
                }

                int count = Mathf.FloorToInt(totalCount * (ratio.ratioPercent / (float)totalRatio));
                assignedCount += count;
                lastValidIndex = results.Count;
                results.Add(new CountEntry(ratio.prefab, count));
            }

            int remainder = totalCount - assignedCount;

            if (remainder > 0 && lastValidIndex >= 0)
            {
                CountEntry last = results[lastValidIndex];
                results[lastValidIndex] = new CountEntry(last.Prefab, last.Count + remainder);
            }

            results.RemoveAll(entry => entry.Count <= 0);
            return results;
        }

        private static int GetTotalCount(List<CountEntry> entries)
        {
            int totalCount = 0;

            if (entries == null)
            {
                return totalCount;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                totalCount += Mathf.Max(0, entries[i].Count);
            }

            return totalCount;
        }

        private static List<EnemyController> BuildSpawnQueue(List<CountEntry> totalEntries)
        {
            List<EnemyController> results = new List<EnemyController>();

            if (totalEntries == null)
            {
                return results;
            }

            for (int i = 0; i < totalEntries.Count; i++)
            {
                CountEntry entry = totalEntries[i];

                if (entry.Prefab == null || entry.Count <= 0)
                {
                    continue;
                }

                for (int countIndex = 0; countIndex < entry.Count; countIndex++)
                {
                    results.Add(entry.Prefab);
                }
            }

            return results;
        }

        private static bool IsNormalMonsterUnlockedForStage(EnemyController prefab, int stage)
        {
            return stage >= GetNormalMonsterUnlockStage(prefab);
        }

        private static int GetNormalMonsterUnlockStage(EnemyController prefab)
        {
            if (prefab == null)
            {
                return 1;
            }

            string prefabName = prefab.name;

            if (prefabName.IndexOf("Ranged_SkeletonCrossbow", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 10; // 석궁은 초반 시야 압박이 커서 10웨이브부터 해금
            }

            if (prefabName.IndexOf("Ranged_Normal", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 5; // 일반 원거리는 5웨이브부터 해금
            }

            if (prefabName.IndexOf("Melee_SkeletonDagger", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 3; // 빠른 단검은 3웨이브부터 해금
            }

            return 1;
        }

        private static void ShuffleEnemyPrefabs(List<EnemyController> prefabs)
        {
            if (prefabs == null)
            {
                return;
            }

            for (int i = 0; i < prefabs.Count; i++)
            {
                int pickIndex = UnityEngine.Random.Range(i, prefabs.Count);
                EnemyController picked = prefabs[pickIndex];
                prefabs[pickIndex] = prefabs[i];
                prefabs[i] = picked;
            }
        }

        private static EnemySpawner.ExternalSpawnEntry[] BuildBatchEntriesFromQueue(List<EnemyController> spawnQueue, int batchIndex, int maxCount)
        {
            if (spawnQueue == null || spawnQueue.Count == 0 || maxCount <= 0)
            {
                return Array.Empty<EnemySpawner.ExternalSpawnEntry>();
            }

            int startIndex = batchIndex * maxCount;

            if (startIndex >= spawnQueue.Count)
            {
                return Array.Empty<EnemySpawner.ExternalSpawnEntry>();
            }

            int endIndex = Mathf.Min(startIndex + maxCount, spawnQueue.Count);
            List<EnemySpawner.ExternalSpawnEntry> results = new List<EnemySpawner.ExternalSpawnEntry>();

            for (int i = startIndex; i < endIndex; i++)
            {
                EnemyController prefab = spawnQueue[i];

                if (prefab == null)
                {
                    continue;
                }

                int existingIndex = FindEntryIndex(results, prefab);

                if (existingIndex >= 0)
                {
                    EnemySpawner.ExternalSpawnEntry existing = results[existingIndex];
                    results[existingIndex] = new EnemySpawner.ExternalSpawnEntry(existing.Prefab, existing.Count + 1);
                }
                else
                {
                    results.Add(new EnemySpawner.ExternalSpawnEntry(prefab, 1));
                }
            }

            return results.ToArray();
        }

        private static int FindEntryIndex(List<EnemySpawner.ExternalSpawnEntry> entries, EnemyController prefab)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Prefab == prefab)
                {
                    return i;
                }
            }

            return -1;
        }

        private EnemySpawner.ExternalSpawnDirectionSet BuildBatchDirectionSet(EnemySpawner.ExternalSpawnDirectionSet stageDirectionSet, int[] previousDirectionIndexes, out int[] selectedDirectionIndexes)
        {
            int[] stageDirectionIndexes = stageDirectionSet.GetDirectionIndexes();

            if (stageDirectionIndexes == null || stageDirectionIndexes.Length == 0)
            {
                selectedDirectionIndexes = Array.Empty<int>();
                return stageDirectionSet;
            }

            int safeBatchGateCount = disableWaveSpawnRedistribution ? 1 : Mathf.Clamp(batchGateCount, 1, stageDirectionIndexes.Length);

            if (safeBatchGateCount >= stageDirectionIndexes.Length)
            {
                selectedDirectionIndexes = stageDirectionIndexes;
                return stageDirectionSet;
            }

            List<int> candidates = BuildDirectionCandidates(stageDirectionIndexes, previousDirectionIndexes, safeBatchGateCount);
            int[] batchDirectionIndexes = PickRandomDirectionIndexes(candidates, safeBatchGateCount);
            selectedDirectionIndexes = batchDirectionIndexes;
            return new EnemySpawner.ExternalSpawnDirectionSet(batchDirectionIndexes);
        }

        private List<int> BuildDirectionCandidates(int[] stageDirectionIndexes, int[] previousDirectionIndexes, int safeBatchGateCount)
        {
            List<int> candidates = new List<int>(stageDirectionIndexes.Length);

            for (int i = 0; i < stageDirectionIndexes.Length; i++)
            {
                int directionIndex = stageDirectionIndexes[i];

                if (avoidPreviousSpawnDirections && stageDirectionIndexes.Length > safeBatchGateCount && ContainsDirectionIndex(previousDirectionIndexes, directionIndex))
                {
                    continue;
                }

                candidates.Add(directionIndex);
            }

            if (candidates.Count >= safeBatchGateCount)
            {
                return candidates;
            }

            candidates.Clear();

            for (int i = 0; i < stageDirectionIndexes.Length; i++)
            {
                candidates.Add(stageDirectionIndexes[i]);
            }

            return candidates;
        }

        private static int[] PickRandomDirectionIndexes(List<int> candidates, int count)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return Array.Empty<int>();
            }

            int safeCount = Mathf.Clamp(count, 1, candidates.Count);
            int[] selected = new int[safeCount];

            for (int i = 0; i < safeCount; i++)
            {
                int pickIndex = UnityEngine.Random.Range(i, candidates.Count);
                int picked = candidates[pickIndex];
                candidates[pickIndex] = candidates[i];
                candidates[i] = picked;
                selected[i] = picked;
            }

            return selected;
        }

        private static bool ContainsDirectionIndex(int[] directionIndexes, int target)
        {
            if (directionIndexes == null)
            {
                return false;
            }

            for (int i = 0; i < directionIndexes.Length; i++)
            {
                if (directionIndexes[i] == target)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ApplyStageDifficulty(List<EnemyController> spawnedMonsters, WaveStageDifficulty difficulty)
        {
            if (spawnedMonsters == null)
            {
                return;
            }

            for (int i = 0; i < spawnedMonsters.Count; i++)
            {
                EnemyStageDifficultyApplier.Apply(spawnedMonsters[i], difficulty);
            }
        }

        private void ResolveEnemySpawner()
        {
            if (enemySpawner == null)
            {
                enemySpawner = FindFirstObjectByType<EnemySpawner>();
            }
        }

        private static NormalComposition CreateComposition(string id, string name, int minStage, int weight, params int[] ratios)
        {
            NormalComposition composition = new NormalComposition
            {
                compositionId = id,
                displayName = name,
                minStage = minStage,
                weight = weight,
                monsters = new MonsterRatioEntry[Mathf.Max(0, ratios.Length)]
            };

            for (int i = 0; i < composition.monsters.Length; i++)
            {
                composition.monsters[i] = new MonsterRatioEntry
                {
                    ratioPercent = ratios[i]
                };
            }

            return composition;
        }
    }
}
