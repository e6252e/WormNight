using System;
using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EliteMixController : MonoBehaviour
    {
        [Serializable]
        public sealed class EliteRatioStep
        {
            [Min(1)]
            public int startStage = 6; // 이 Stage부터 엘리트 비율을 적용합니다.

            [Range(0, 100)]
            public int eliteRatioPercent = 10; // 전체 웨이브 수량 중 몇 %를 엘리트로 바꿀지입니다.
        }

        [Serializable]
        public sealed class EliteEntry
        {
            public EnemyController prefab; // Inspector에서 엘리트 몬스터 Prefab을 연결합니다.

            [Range(0, 100)]
            public int ratioPercent = 100; // 이 조합 안에서 해당 엘리트가 차지하는 비율입니다.
        }

        [Serializable]
        public sealed class EliteComposition
        {
            public string compositionId = "E01"; // Inspector에서 구분하기 위한 ID입니다.

            [Min(1)]
            public int minStage = 1; // 이 Stage부터 조합 후보에 포함됩니다.

            [Min(0)]
            public int weight = 100; // 여러 조합 후보 중 선택될 확률 가중치입니다.

            public string combinationType = "진행방해"; // 충돌 규칙에서 비교할 조합 성격입니다.

            public EliteEntry[] elites = Array.Empty<EliteEntry>(); // 실제로 섞어 스폰할 엘리트 목록입니다.

            public bool IsAvailable()
            {
                return weight > 0 && HasValidElite();
            }

            private bool HasValidElite()
            {
                if (elites == null)
                {
                    return false;
                }

                for (int i = 0; i < elites.Length; i++)
                {
                    EliteEntry entry = elites[i];

                    if (entry != null && entry.prefab != null && entry.ratioPercent > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        [Serializable]
        public sealed class BlockedEliteCombination
        {
            public string combinationA = "진행방해"; // 같이 나오면 안 되는 첫 번째 조합 성격입니다.
            public string combinationB = "강제분리"; // 같이 나오면 안 되는 두 번째 조합 성격입니다.

            public bool IsMatch(string first, string second)
            {
                string safeFirst = Normalize(first);
                string safeSecond = Normalize(second);
                string safeA = Normalize(combinationA);
                string safeB = Normalize(combinationB);

                if (string.IsNullOrEmpty(safeFirst) || string.IsNullOrEmpty(safeSecond) || string.IsNullOrEmpty(safeA) || string.IsNullOrEmpty(safeB))
                {
                    return false;
                }

                return safeFirst == safeA && safeSecond == safeB || safeFirst == safeB && safeSecond == safeA;
            }
        }

        public readonly struct EliteStagePlan
        {
            public readonly EnemySpawner.ExternalSpawnEntry[] Entries;
            public readonly string CombinationType;
            public readonly int TotalCount;

            public EliteStagePlan(EnemySpawner.ExternalSpawnEntry[] entries, string combinationType)
            {
                Entries = entries ?? Array.Empty<EnemySpawner.ExternalSpawnEntry>();
                CombinationType = Normalize(combinationType);
                TotalCount = CalculateTotalCount(Entries);
            }

            public bool HasEntries => Entries != null && Entries.Length > 0 && TotalCount > 0;

            private static int CalculateTotalCount(EnemySpawner.ExternalSpawnEntry[] entries)
            {
                int total = 0;

                for (int i = 0; i < entries.Length; i++)
                {
                    EnemySpawner.ExternalSpawnEntry entry = entries[i];

                    if (entry.IsValid)
                    {
                        total += entry.Count;
                    }
                }

                return total;
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

        private enum EliteKind
        {
            Suicide,
            Slow,
            Jump,
            Obstacle,
            Growth,
            Buff,
            Shield,
            SegmentCut,
            Portal
        }

        private readonly struct FixedEliteSpawn
        {
            public readonly EliteKind Kind;
            public readonly int Count;

            public FixedEliteSpawn(EliteKind kind, int count)
            {
                Kind = kind;
                Count = count;
            }
        }

        private readonly struct ChallengeEliteRule
        {
            public readonly EliteKind Kind;
            public readonly int Weight;
            public readonly int MaxPerStage;

            public ChallengeEliteRule(EliteKind kind, int weight, int maxPerStage)
            {
                Kind = kind;
                Weight = weight;
                MaxPerStage = maxPerStage;
            }
        }

        private static readonly Dictionary<int, FixedEliteSpawn[]> FixedEliteSchedule = new Dictionary<int, FixedEliteSpawn[]>
        {
            { 1, Array.Empty<FixedEliteSpawn>() },
            { 2, new[] { new FixedEliteSpawn(EliteKind.Suicide, 1) } },
            { 3, new[] { new FixedEliteSpawn(EliteKind.Suicide, 1) } },
            { 4, new[] { new FixedEliteSpawn(EliteKind.Slow, 1) } },
            { 5, new[] { new FixedEliteSpawn(EliteKind.Suicide, 1), new FixedEliteSpawn(EliteKind.Slow, 1) } },
            { 6, new[] { new FixedEliteSpawn(EliteKind.Suicide, 1), new FixedEliteSpawn(EliteKind.Slow, 1) } },
            { 7, new[] { new FixedEliteSpawn(EliteKind.Suicide, 2) } },
            { 8, new[] { new FixedEliteSpawn(EliteKind.Slow, 2), new FixedEliteSpawn(EliteKind.Suicide, 1) } },
            { 9, new[] { new FixedEliteSpawn(EliteKind.Suicide, 2), new FixedEliteSpawn(EliteKind.Slow, 1) } },
            { 10, new[] { new FixedEliteSpawn(EliteKind.Slow, 2), new FixedEliteSpawn(EliteKind.Suicide, 1) } },
            { 11, new[] { new FixedEliteSpawn(EliteKind.Jump, 1), new FixedEliteSpawn(EliteKind.Slow, 2), new FixedEliteSpawn(EliteKind.Suicide, 1) } },
            { 12, new[] { new FixedEliteSpawn(EliteKind.Obstacle, 1), new FixedEliteSpawn(EliteKind.Suicide, 2), new FixedEliteSpawn(EliteKind.Slow, 1) } },
            { 13, new[] { new FixedEliteSpawn(EliteKind.Obstacle, 2), new FixedEliteSpawn(EliteKind.Slow, 2) } },
            { 14, new[] { new FixedEliteSpawn(EliteKind.Jump, 1), new FixedEliteSpawn(EliteKind.Suicide, 2), new FixedEliteSpawn(EliteKind.Slow, 1) } },
            { 15, new[] { new FixedEliteSpawn(EliteKind.Obstacle, 2), new FixedEliteSpawn(EliteKind.Suicide, 2), new FixedEliteSpawn(EliteKind.Slow, 1) } },
            { 16, new[] { new FixedEliteSpawn(EliteKind.Obstacle, 2), new FixedEliteSpawn(EliteKind.Slow, 2), new FixedEliteSpawn(EliteKind.Suicide, 1) } },
            { 17, new[] { new FixedEliteSpawn(EliteKind.Jump, 1), new FixedEliteSpawn(EliteKind.Obstacle, 2), new FixedEliteSpawn(EliteKind.Slow, 2) } },
            { 18, new[] { new FixedEliteSpawn(EliteKind.Obstacle, 3), new FixedEliteSpawn(EliteKind.Suicide, 2), new FixedEliteSpawn(EliteKind.Slow, 1) } },
            { 19, new[] { new FixedEliteSpawn(EliteKind.Obstacle, 3), new FixedEliteSpawn(EliteKind.Slow, 2), new FixedEliteSpawn(EliteKind.Suicide, 1) } },
            { 20, Array.Empty<FixedEliteSpawn>() },
            { 21, new[] { new FixedEliteSpawn(EliteKind.Growth, 1), new FixedEliteSpawn(EliteKind.Suicide, 2), new FixedEliteSpawn(EliteKind.Slow, 2) } },
            { 22, new[] { new FixedEliteSpawn(EliteKind.Growth, 1), new FixedEliteSpawn(EliteKind.Slow, 2), new FixedEliteSpawn(EliteKind.Obstacle, 2) } },
            { 23, new[] { new FixedEliteSpawn(EliteKind.Growth, 1), new FixedEliteSpawn(EliteKind.Obstacle, 3), new FixedEliteSpawn(EliteKind.Suicide, 2) } },
            { 24, new[] { new FixedEliteSpawn(EliteKind.Obstacle, 3), new FixedEliteSpawn(EliteKind.Suicide, 2), new FixedEliteSpawn(EliteKind.Slow, 1) } },
            { 25, new[] { new FixedEliteSpawn(EliteKind.Buff, 1), new FixedEliteSpawn(EliteKind.Suicide, 3), new FixedEliteSpawn(EliteKind.Slow, 2) } },
            { 26, new[] { new FixedEliteSpawn(EliteKind.Buff, 1), new FixedEliteSpawn(EliteKind.Slow, 3), new FixedEliteSpawn(EliteKind.Obstacle, 2) } },
            { 27, new[] { new FixedEliteSpawn(EliteKind.Buff, 1), new FixedEliteSpawn(EliteKind.Obstacle, 3), new FixedEliteSpawn(EliteKind.Suicide, 3) } },
            { 28, new[] { new FixedEliteSpawn(EliteKind.Jump, 1), new FixedEliteSpawn(EliteKind.Buff, 1), new FixedEliteSpawn(EliteKind.Suicide, 3), new FixedEliteSpawn(EliteKind.Slow, 2) } },
            { 29, new[] { new FixedEliteSpawn(EliteKind.Buff, 1), new FixedEliteSpawn(EliteKind.Slow, 3), new FixedEliteSpawn(EliteKind.Obstacle, 3) } },
            { 30, new[] { new FixedEliteSpawn(EliteKind.Shield, 1), new FixedEliteSpawn(EliteKind.Suicide, 3), new FixedEliteSpawn(EliteKind.Obstacle, 3) } },
            { 31, new[] { new FixedEliteSpawn(EliteKind.SegmentCut, 1), new FixedEliteSpawn(EliteKind.Slow, 3), new FixedEliteSpawn(EliteKind.Suicide, 3) } },
            { 32, new[] { new FixedEliteSpawn(EliteKind.SegmentCut, 1), new FixedEliteSpawn(EliteKind.Shield, 1), new FixedEliteSpawn(EliteKind.Slow, 3), new FixedEliteSpawn(EliteKind.Obstacle, 3) } },
            { 33, new[] { new FixedEliteSpawn(EliteKind.SegmentCut, 1), new FixedEliteSpawn(EliteKind.Buff, 1), new FixedEliteSpawn(EliteKind.Suicide, 3), new FixedEliteSpawn(EliteKind.Obstacle, 3) } },
            { 34, new[] { new FixedEliteSpawn(EliteKind.Portal, 1), new FixedEliteSpawn(EliteKind.Shield, 1), new FixedEliteSpawn(EliteKind.Suicide, 3), new FixedEliteSpawn(EliteKind.Slow, 3) } },
            { 35, new[] { new FixedEliteSpawn(EliteKind.SegmentCut, 1), new FixedEliteSpawn(EliteKind.Portal, 1), new FixedEliteSpawn(EliteKind.Shield, 1), new FixedEliteSpawn(EliteKind.Obstacle, 2), new FixedEliteSpawn(EliteKind.Slow, 2) } },
            { 36, new[] { new FixedEliteSpawn(EliteKind.Portal, 1), new FixedEliteSpawn(EliteKind.Obstacle, 3), new FixedEliteSpawn(EliteKind.Suicide, 3) } },
            { 37, new[] { new FixedEliteSpawn(EliteKind.SegmentCut, 1), new FixedEliteSpawn(EliteKind.Portal, 1), new FixedEliteSpawn(EliteKind.Slow, 3), new FixedEliteSpawn(EliteKind.Suicide, 3) } },
            { 38, new[] { new FixedEliteSpawn(EliteKind.Portal, 1), new FixedEliteSpawn(EliteKind.Shield, 1), new FixedEliteSpawn(EliteKind.Obstacle, 3), new FixedEliteSpawn(EliteKind.Suicide, 3) } },
            { 39, new[] { new FixedEliteSpawn(EliteKind.SegmentCut, 1), new FixedEliteSpawn(EliteKind.Portal, 1), new FixedEliteSpawn(EliteKind.Obstacle, 3), new FixedEliteSpawn(EliteKind.Slow, 3) } }
        };

        private static readonly ChallengeEliteRule[] ChallengeEliteRules =
        {
            new ChallengeEliteRule(EliteKind.Suicide, 16, 4),
            new ChallengeEliteRule(EliteKind.Slow, 16, 4),
            new ChallengeEliteRule(EliteKind.Obstacle, 16, 4),
            new ChallengeEliteRule(EliteKind.Buff, 10, 3),
            new ChallengeEliteRule(EliteKind.Shield, 10, 3),
            new ChallengeEliteRule(EliteKind.Growth, 8, 3),
            new ChallengeEliteRule(EliteKind.Jump, 8, 3),
            new ChallengeEliteRule(EliteKind.SegmentCut, 8, 3),
            new ChallengeEliteRule(EliteKind.Portal, 8, 3)
        };

        private readonly HashSet<string> activeCombinationTypes = new HashSet<string>(); // 살아있는 엘리트 조합 성격을 임시로 모읍니다.

        [Header("엘리트 등장 스케줄")]
        [SerializeField] private bool useScriptedEliteSchedule = true; // Stage별 등장 종류/수량만 고정하고 실제 스폰 방식은 기존 흐름을 사용합니다.

        [Min(1)]
        [SerializeField] private int challengeStartStage = 40; // 이 Stage부터 도전모드 랜덤 엘리트 등장표를 사용합니다.

        [Min(0)]
        [SerializeField] private int challengeBaseEliteCount = 9; // 도전모드 시작 엘리트 수입니다.

        [Min(1)]
        [SerializeField] private int challengeEliteIncreaseIntervalStages = 2; // 몇 Stage마다 엘리트 수를 1 늘릴지입니다.

        [Header("엘리트 비율 단계")]
        [SerializeField] private EliteRatioStep[] eliteRatioSteps =
        {
            new EliteRatioStep { startStage = 6, eliteRatioPercent = 10 },
            new EliteRatioStep { startStage = 12, eliteRatioPercent = 15 },
            new EliteRatioStep { startStage = 20, eliteRatioPercent = 20 }
        };

        [Header("엘리트 조합")]
        [SerializeField] private EliteComposition[] eliteCompositions =
        {
            CreateComposition("E01", 100, "진행방해", 70, 30),
            CreateComposition("E02", 80, "강제분리", 50, 50),
            CreateComposition("E03", 70, "소환압박", 50, 50)
        };

        [Header("엘리트 충돌 규칙")]
        [SerializeField] private bool checkAliveEliteCombinations = true; // 살아있는 엘리트와 새 조합이 충돌하는지 검사합니다.
        [SerializeField] private BlockedEliteCombination[] blockedEliteCombinations =
        {
            new BlockedEliteCombination { combinationA = "진행방해", combinationB = "강제분리" }
        };

        public int CalculateEliteCount(int stage, int totalSpawnCount)
        {
            if (useScriptedEliteSchedule)
            {
                if (TryGetFixedEliteSpawns(stage, out FixedEliteSpawn[] fixedSpawns))
                {
                    return CalculateFixedEliteCount(fixedSpawns);
                }

                if (IsChallengeStage(stage))
                {
                    return CalculateChallengeEliteCount(stage);
                }
            }

            int ratio = GetEliteRatioForStage(stage);
            return Mathf.Clamp(Mathf.RoundToInt(totalSpawnCount * (ratio / 100.0f)), 0, Mathf.Max(0, totalSpawnCount));
        }

        public EliteStagePlan BuildStagePlan(int stage, int eliteCount)
        {
            if (useScriptedEliteSchedule)
            {
                if (TryGetFixedEliteSpawns(stage, out FixedEliteSpawn[] fixedSpawns))
                {
                    return BuildFixedStagePlan(fixedSpawns, "고정엘리트");
                }

                if (IsChallengeStage(stage))
                {
                    return BuildChallengeStagePlan(stage);
                }
            }

            if (eliteCount <= 0)
            {
                return default;
            }

            EliteComposition composition = PickComposition(stage);

            if (composition == null)
            {
                return default;
            }

            List<CountEntry> counts = BuildCountEntries(composition.elites, eliteCount);
            return new EliteStagePlan(BuildEntries(counts), composition.combinationType);
        }

        private bool IsChallengeStage(int stage)
        {
            return stage >= Mathf.Max(1, challengeStartStage);
        }

        private int CalculateChallengeEliteCount(int stage)
        {
            int startStage = Mathf.Max(1, challengeStartStage);
            int interval = Mathf.Max(1, challengeEliteIncreaseIntervalStages);
            int increase = Mathf.Max(0, stage - startStage) / interval;
            return Mathf.Max(0, challengeBaseEliteCount + increase);
        }

        private EliteStagePlan BuildFixedStagePlan(FixedEliteSpawn[] fixedSpawns, string combinationType)
        {
            if (fixedSpawns == null || fixedSpawns.Length == 0)
            {
                return default;
            }

            Dictionary<EliteKind, EnemyController> prefabLookup = BuildElitePrefabLookup();
            List<CountEntry> counts = new List<CountEntry>();

            for (int i = 0; i < fixedSpawns.Length; i++)
            {
                FixedEliteSpawn spawn = fixedSpawns[i];

                if (spawn.Count <= 0 || !prefabLookup.TryGetValue(spawn.Kind, out EnemyController prefab) || prefab == null)
                {
                    continue;
                }

                counts.Add(new CountEntry(prefab, spawn.Count));
            }

            return new EliteStagePlan(BuildEntries(counts), combinationType);
        }

        private EliteStagePlan BuildChallengeStagePlan(int stage)
        {
            int targetCount = CalculateChallengeEliteCount(stage);

            if (targetCount <= 0)
            {
                return default;
            }

            Dictionary<EliteKind, EnemyController> prefabLookup = BuildElitePrefabLookup();
            Dictionary<EliteKind, int> pickedCounts = new Dictionary<EliteKind, int>();
            int capacity = CalculateChallengeCapacity(prefabLookup);
            int cappedTargetCount = Mathf.Min(targetCount, capacity);

            for (int i = 0; i < cappedTargetCount; i++)
            {
                if (!TryPickChallengeElite(prefabLookup, pickedCounts, out EliteKind pickedKind))
                {
                    break;
                }

                pickedCounts.TryGetValue(pickedKind, out int currentCount);
                pickedCounts[pickedKind] = currentCount + 1;
            }

            List<CountEntry> counts = BuildChallengeCountEntries(prefabLookup, pickedCounts);
            return new EliteStagePlan(BuildEntries(counts), "도전랜덤엘리트");
        }

        private static int CalculateChallengeCapacity(Dictionary<EliteKind, EnemyController> prefabLookup)
        {
            int capacity = 0;

            for (int i = 0; i < ChallengeEliteRules.Length; i++)
            {
                ChallengeEliteRule rule = ChallengeEliteRules[i];

                if (prefabLookup.ContainsKey(rule.Kind))
                {
                    capacity += Mathf.Max(0, rule.MaxPerStage);
                }
            }

            return capacity;
        }

        private static bool TryPickChallengeElite(Dictionary<EliteKind, EnemyController> prefabLookup, Dictionary<EliteKind, int> pickedCounts, out EliteKind pickedKind)
        {
            pickedKind = default;
            int totalWeight = 0;

            for (int i = 0; i < ChallengeEliteRules.Length; i++)
            {
                ChallengeEliteRule rule = ChallengeEliteRules[i];

                if (CanPickChallengeRule(rule, prefabLookup, pickedCounts))
                {
                    totalWeight += Mathf.Max(0, rule.Weight);
                }
            }

            if (totalWeight <= 0)
            {
                return false;
            }

            int randomWeight = UnityEngine.Random.Range(0, totalWeight);

            for (int i = 0; i < ChallengeEliteRules.Length; i++)
            {
                ChallengeEliteRule rule = ChallengeEliteRules[i];

                if (!CanPickChallengeRule(rule, prefabLookup, pickedCounts))
                {
                    continue;
                }

                randomWeight -= Mathf.Max(0, rule.Weight);

                if (randomWeight < 0)
                {
                    pickedKind = rule.Kind;
                    return true;
                }
            }

            return false;
        }

        private static bool CanPickChallengeRule(ChallengeEliteRule rule, Dictionary<EliteKind, EnemyController> prefabLookup, Dictionary<EliteKind, int> pickedCounts)
        {
            if (rule.Weight <= 0 || rule.MaxPerStage <= 0 || !prefabLookup.ContainsKey(rule.Kind))
            {
                return false;
            }

            pickedCounts.TryGetValue(rule.Kind, out int currentCount);
            return currentCount < rule.MaxPerStage;
        }

        private static List<CountEntry> BuildChallengeCountEntries(Dictionary<EliteKind, EnemyController> prefabLookup, Dictionary<EliteKind, int> pickedCounts)
        {
            List<CountEntry> counts = new List<CountEntry>();

            for (int i = 0; i < ChallengeEliteRules.Length; i++)
            {
                EliteKind kind = ChallengeEliteRules[i].Kind;

                if (!pickedCounts.TryGetValue(kind, out int count) || count <= 0 || !prefabLookup.TryGetValue(kind, out EnemyController prefab))
                {
                    continue;
                }

                counts.Add(new CountEntry(prefab, count));
            }

            return counts;
        }

        public void CollectElitePrefabsByNameTokens(string[] nameTokens, List<EnemyController> results)
        {
            if (nameTokens == null || results == null)
            {
                return;
            }

            for (int i = 0; i < nameTokens.Length; i++)
            {
                string token = nameTokens[i];

                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (TryFindElitePrefabByNameToken(token, out EnemyController prefab) && prefab != null && !results.Contains(prefab))
                {
                    results.Add(prefab);
                }
            }
        }

        private bool TryFindElitePrefabByNameToken(string nameToken, out EnemyController prefab)
        {
            prefab = null;

            if (eliteCompositions == null)
            {
                return false;
            }

            for (int i = 0; i < eliteCompositions.Length; i++)
            {
                EliteComposition composition = eliteCompositions[i];

                if (composition == null || composition.elites == null)
                {
                    continue;
                }

                for (int entryIndex = 0; entryIndex < composition.elites.Length; entryIndex++)
                {
                    EliteEntry entry = composition.elites[entryIndex];

                    if (entry == null || entry.prefab == null)
                    {
                        continue;
                    }

                    if (entry.prefab.name.IndexOf(nameToken, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    prefab = entry.prefab;
                    return true;
                }
            }

            return false;
        }

        private Dictionary<EliteKind, EnemyController> BuildElitePrefabLookup()
        {
            Dictionary<EliteKind, EnemyController> result = new Dictionary<EliteKind, EnemyController>();

            if (eliteCompositions == null)
            {
                return result;
            }

            for (int i = 0; i < eliteCompositions.Length; i++)
            {
                EliteComposition composition = eliteCompositions[i];

                if (composition == null || composition.elites == null)
                {
                    continue;
                }

                for (int entryIndex = 0; entryIndex < composition.elites.Length; entryIndex++)
                {
                    EliteEntry entry = composition.elites[entryIndex];

                    if (entry == null || entry.prefab == null || !TryResolveEliteKind(entry.prefab, out EliteKind kind) || result.ContainsKey(kind))
                    {
                        continue;
                    }

                    result.Add(kind, entry.prefab);
                }
            }

            return result;
        }

        private static bool TryResolveEliteKind(EnemyController prefab, out EliteKind kind)
        {
            kind = default;

            if (prefab == null)
            {
                return false;
            }

            string prefabName = prefab.name;

            if (prefabName.IndexOf("SuicideCharger", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = EliteKind.Suicide;
                return true;
            }

            if (prefabName.IndexOf("SlowThrower", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = EliteKind.Slow;
                return true;
            }

            if (prefabName.IndexOf("SkeletonGolemJumper", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = EliteKind.Jump;
                return true;
            }

            if (prefabName.IndexOf("ObstacleSingle", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = EliteKind.Obstacle;
                return true;
            }

            if (prefabName.IndexOf("DragonHatchling", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = EliteKind.Growth;
                return true;
            }

            if (prefabName.IndexOf("BuffCaster", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = EliteKind.Buff;
                return true;
            }

            if (prefabName.IndexOf("AreaShield", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = EliteKind.Shield;
                return true;
            }

            if (prefabName.IndexOf("SegmentCutCaster", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = EliteKind.SegmentCut;
                return true;
            }

            if (prefabName.IndexOf("PortalTotemCaster", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = EliteKind.Portal;
                return true;
            }

            return false;
        }

        private static bool TryGetFixedEliteSpawns(int stage, out FixedEliteSpawn[] fixedSpawns)
        {
            return FixedEliteSchedule.TryGetValue(stage, out fixedSpawns);
        }

        private static int CalculateFixedEliteCount(FixedEliteSpawn[] fixedSpawns)
        {
            int count = 0;

            if (fixedSpawns == null)
            {
                return count;
            }

            for (int i = 0; i < fixedSpawns.Length; i++)
            {
                count += Mathf.Max(0, fixedSpawns[i].Count);
            }

            return count;
        }

        private int GetEliteRatioForStage(int stage)
        {
            int result = 0;

            if (eliteRatioSteps == null)
            {
                return result;
            }

            for (int i = 0; i < eliteRatioSteps.Length; i++)
            {
                EliteRatioStep step = eliteRatioSteps[i];

                if (step != null && stage >= step.startStage)
                {
                    result = step.eliteRatioPercent;
                }
            }

            return Mathf.Clamp(result, 0, 100);
        }

        private EliteComposition PickComposition(int stage)
        {
            if (eliteCompositions == null)
            {
                return null;
            }

            if (checkAliveEliteCombinations)
            {
                WaveSpawnedEliteMarker.CollectActiveCombinationTypes(activeCombinationTypes);
            }
            else
            {
                activeCombinationTypes.Clear();
            }

            int totalWeight = 0;

            for (int i = 0; i < eliteCompositions.Length; i++)
            {
                EliteComposition composition = eliteCompositions[i];

                if (CanUseComposition(composition, stage))
                {
                    totalWeight += composition.weight;
                }
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            int randomWeight = UnityEngine.Random.Range(0, totalWeight);

            for (int i = 0; i < eliteCompositions.Length; i++)
            {
                EliteComposition composition = eliteCompositions[i];

                if (!CanUseComposition(composition, stage))
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

        private bool CanUseComposition(EliteComposition composition, int stage)
        {
            if (composition == null || !composition.IsAvailable())
            {
                return false;
            }

            if (stage < Mathf.Max(1, composition.minStage))
            {
                return false;
            }

            if (!checkAliveEliteCombinations)
            {
                return true;
            }

            string newType = Normalize(composition.combinationType);

            if (string.IsNullOrEmpty(newType))
            {
                return true;
            }

            foreach (string activeType in activeCombinationTypes)
            {
                if (IsBlockedCombination(activeType, newType))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsBlockedCombination(string activeType, string newType)
        {
            if (blockedEliteCombinations == null)
            {
                return false;
            }

            for (int i = 0; i < blockedEliteCombinations.Length; i++)
            {
                BlockedEliteCombination blocked = blockedEliteCombinations[i];

                if (blocked != null && blocked.IsMatch(activeType, newType))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<CountEntry> BuildCountEntries(EliteEntry[] ratios, int totalCount)
        {
            List<CountEntry> results = new List<CountEntry>();

            if (ratios == null || ratios.Length == 0 || totalCount <= 0)
            {
                return results;
            }

            int totalRatio = 0;

            for (int i = 0; i < ratios.Length; i++)
            {
                EliteEntry ratio = ratios[i];

                if (ratio != null && ratio.prefab != null && ratio.ratioPercent > 0)
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
                EliteEntry ratio = ratios[i];

                if (ratio == null || ratio.prefab == null || ratio.ratioPercent <= 0)
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

        private static EnemySpawner.ExternalSpawnEntry[] BuildEntries(List<CountEntry> counts)
        {
            List<EnemySpawner.ExternalSpawnEntry> entries = new List<EnemySpawner.ExternalSpawnEntry>();

            for (int i = 0; i < counts.Count; i++)
            {
                CountEntry count = counts[i];

                if (count.Prefab != null && count.Count > 0)
                {
                    entries.Add(new EnemySpawner.ExternalSpawnEntry(count.Prefab, count.Count));
                }
            }

            return entries.ToArray();
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static EliteComposition CreateComposition(string id, int weight, string combinationType, params int[] ratios)
        {
            EliteComposition composition = new EliteComposition
            {
                compositionId = id,
                weight = weight,
                combinationType = combinationType,
                elites = new EliteEntry[Mathf.Max(0, ratios.Length)]
            };

            for (int i = 0; i < composition.elites.Length; i++)
            {
                composition.elites[i] = new EliteEntry
                {
                    ratioPercent = ratios[i]
                };
            }

            return composition;
        }
    }
}
