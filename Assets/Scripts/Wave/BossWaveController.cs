using System;
using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class BossWaveController : MonoBehaviour
    {
        [Serializable]
        public sealed class BossEntry
        {
            public string bossId = "B01"; // 문서와 Inspector에서 구분하기 쉬운 보스 ID입니다.
            public string displayName = "초반 보스"; // Inspector에 보일 이름입니다.
            public EnemyController prefab; // Inspector에서 보스 Prefab을 직접 연결합니다.
        }

        [Serializable]
        public sealed class BossCombinationEntry
        {
            public EnemyController prefab; // 조합에 같이 등장할 보스 Prefab입니다.
        }

        [Serializable]
        public sealed class BossCombination
        {
            public string combinationId = "BC01"; // Inspector에서 구분하기 위한 조합 ID입니다.
            public string displayName = "보스 조합"; // Inspector에 보일 조합 이름입니다.
            public BossCombinationEntry[] bosses = Array.Empty<BossCombinationEntry>(); // 같이 등장할 보스 목록입니다.

            public bool IsAvailable()
            {
                if (bosses == null)
                {
                    return false;
                }

                for (int i = 0; i < bosses.Length; i++)
                {
                    BossCombinationEntry boss = bosses[i];

                    if (boss != null && boss.prefab != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        [Serializable]
        public sealed class BossStageProfile
        {
            [Min(1)]
            public int stage = 20; // 이 Stage를 보스 Stage로 사용합니다.

            public string profileName = "20 웨이브 보스"; // Inspector에서 구분하기 위한 이름입니다.

            [Min(1)]
            public float maxHp = 6000.0f; // 보스 생성 직후 적용할 최대 HP입니다.

            [Min(0)]
            public int normalProjectileDamage = 2; // 일반 다이아 발사 피해입니다.

            [Min(0)]
            public int berserkProjectileDamage = 1; // 버서크 다이아 난사 피해입니다.

            [Min(0)]
            public int eliteSummonTotalCount = 4; // 소환 패턴 1회당 총 엘리트 수입니다.

            [Min(1)]
            public int maximumActiveSummonedMonsters = 80; // 보스 소환 몬스터 활성 상한입니다.

            public string[] elitePrefabNameTokens = Array.Empty<string>(); // EliteMixController에 등록된 Prefab 이름 일부입니다.
        }

        [Header("참조")]
        [SerializeField] private EnemySpawner enemySpawner; // 실제 생성은 기존 EnemySpawner API에 맡깁니다.
        [SerializeField] private BonusChestWaveSpawner bonusChestWaveSpawner; // 보스 처치 후 상자 생성에 사용합니다.

        [Header("보스 진행 설정")]
        [HideInInspector]
        [SerializeField] private bool enableBossWave = true; // 이전 씬 데이터 호환용입니다. 사용 여부는 WaveController가 관리합니다.

        [Min(1)]
        [SerializeField] private int bossStartStage = 15; // 첫 보스가 나올 수 있는 Stage입니다.

        [Min(1)]
        [SerializeField] private int bossIntervalStage = 10; // 다음 보스가 다시 등장하기까지 필요한 Stage 간격입니다.

        [SerializeField] private bool blockAdditionalBossWhileAlive = true; // 보스가 살아있으면 새 보스 등장을 막습니다.
        [SerializeField] private bool spawnChestAfterBossClear = true; // 보스를 처치하면 보너스 상자를 생성합니다.
        [SerializeField] private bool pauseNormalSpawnWhileBossAlive = true; // 보스 Stage에서는 새 일반/엘리트 스폰을 멈춥니다.
        [SerializeField] private bool endBossStageOnBossClear = true; // 보스 Stage는 시간 대신 보스 처치로 종료합니다.

        [Header("보스 Stage 프로필")]
        [SerializeField] private BossStageProfile[] bossStageProfiles = CreateDefaultBossStageProfiles(); // Stage별 보스 체력/피해/소환 구성입니다.

        [Header("보스 등장 순서")]
        [SerializeField] private BossEntry[] bossSequence =
        {
            new BossEntry()
        };

        [Header("보스 조합")]
        [SerializeField] private bool enableBossCombination; // 켜두면 일정 Stage 이후 보스 조합을 사용합니다.

        [Min(1)]
        [SerializeField] private int bossCombinationStartStage = 60; // 이 Stage부터 보스 조합을 사용할 수 있습니다.

        [SerializeField] private BossCombination[] bossCombinations =
        {
            new BossCombination()
        };

        private readonly List<EnemyController> activeBosses = new List<EnemyController>(); // 현재 살아있는 보스 목록입니다.
        private bool waitingBossClearReward; // 보스 처치 보상 상자를 한 번만 주기 위한 플래그입니다.
        private EliteMixController eliteMixController; // 보스 소환용 엘리트 Prefab을 기존 엘리트 등장표에서 재사용합니다.

        public bool HasActiveBoss
        {
            get
            {
                CleanupActiveBosses();
                return activeBosses.Count > 0;
            }
        }

        public bool ShouldPauseNormalSpawn
        {
            get
            {
                return pauseNormalSpawnWhileBossAlive && HasActiveBoss;
            }
        }

        public bool ShouldEndStageOnBossClear => endBossStageOnBossClear;

        public bool IsBossStage(int stage)
        {
            return TryGetBossStageProfile(stage, out _);
        }

        public bool BeginStage(int stage)
        {
            CleanupActiveBosses();

            if (!CanSpawnBoss(stage) || !TryGetBossStageProfile(stage, out BossStageProfile stageProfile))
            {
                return false;
            }

            EnemySpawner.ExternalSpawnEntry[] entries = BuildBossSpawnEntries(stage);

            if (entries == null || entries.Length == 0)
            {
                return false;
            }

            ResolveReferences();

            if (enemySpawner == null)
            {
                return false;
            }

            List<EnemyController> spawnedBosses = new List<EnemyController>(entries.Length);

            int bossDirectionCount = Mathf.Max(1, entries.Length);

            if (!enemySpawner.TrySpawnExternalEntriesDistributed(entries, bossDirectionCount, 1, spawnedBosses))
            {
                return false;
            }

            for (int i = 0; i < spawnedBosses.Count; i++)
            {
                EnemyController spawnedBoss = spawnedBosses[i];

                if (spawnedBoss != null)
                {
                    ApplyBossStageProfile(spawnedBoss, stageProfile);
                    activeBosses.Add(spawnedBoss);
                }
            }

            waitingBossClearReward = activeBosses.Count > 0;
            return activeBosses.Count > 0;
        }

        private void Update()
        {
            CleanupActiveBosses();

            // WaveController가 먼저 HasActiveBoss를 확인하면 activeBosses가 이미 정리될 수 있습니다.
            // 그래서 "이전에 보스가 있었는지"보다 보상 대기 플래그와 현재 생존 여부만 봅니다.
            if (!waitingBossClearReward || activeBosses.Count > 0)
            {
                return;
            }

            waitingBossClearReward = false;

            if (spawnChestAfterBossClear)
            {
                ResolveReferences();

                if (bonusChestWaveSpawner != null)
                {
                    bonusChestWaveSpawner.SpawnBonusChestWave();
                }
            }
        }

        private bool CanSpawnBoss(int stage)
        {
            if (!IsBossStage(stage))
            {
                return false;
            }

            if (blockAdditionalBossWhileAlive && HasActiveBoss)
            {
                return false;
            }

            return true;
        }

        private EnemySpawner.ExternalSpawnEntry[] BuildBossSpawnEntries(int stage)
        {
            if (enableBossCombination && stage >= bossCombinationStartStage)
            {
                EnemySpawner.ExternalSpawnEntry[] combinationEntries = BuildBossCombinationEntries(stage);

                if (combinationEntries.Length > 0)
                {
                    return combinationEntries;
                }
            }

            BossEntry boss = PickSingleBossForStage(stage);

            if (boss == null || boss.prefab == null)
            {
                return Array.Empty<EnemySpawner.ExternalSpawnEntry>();
            }

            return new[]
            {
                new EnemySpawner.ExternalSpawnEntry(boss.prefab, 1)
            };
        }

        private BossEntry PickSingleBossForStage(int stage)
        {
            if (bossSequence == null || bossSequence.Length == 0)
            {
                return null;
            }

            int bossWaveIndex = GetBossWaveIndex(stage);
            int safeIndex = Mathf.Clamp(bossWaveIndex, 0, bossSequence.Length - 1);
            return bossSequence[safeIndex];
        }

        private EnemySpawner.ExternalSpawnEntry[] BuildBossCombinationEntries(int stage)
        {
            BossCombination combination = PickBossCombination(stage);

            if (combination == null || !combination.IsAvailable())
            {
                return Array.Empty<EnemySpawner.ExternalSpawnEntry>();
            }

            List<EnemySpawner.ExternalSpawnEntry> entries = new List<EnemySpawner.ExternalSpawnEntry>();

            for (int i = 0; i < combination.bosses.Length; i++)
            {
                BossCombinationEntry boss = combination.bosses[i];

                if (boss != null && boss.prefab != null)
                {
                    entries.Add(new EnemySpawner.ExternalSpawnEntry(boss.prefab, 1));
                }
            }

            return entries.ToArray();
        }

        private BossCombination PickBossCombination(int stage)
        {
            if (bossCombinations == null || bossCombinations.Length == 0)
            {
                return null;
            }

            int combinationWaveIndex = Mathf.Max(0, (stage - bossCombinationStartStage) / Mathf.Max(1, bossIntervalStage));
            int validIndex = 0;
            BossCombination fallback = null;

            for (int i = 0; i < bossCombinations.Length; i++)
            {
                BossCombination combination = bossCombinations[i];

                if (combination == null || !combination.IsAvailable())
                {
                    continue;
                }

                fallback = combination;

                if (validIndex == combinationWaveIndex)
                {
                    return combination;
                }

                validIndex++;
            }

            // 조합이 부족하면 마지막 유효 조합을 반복합니다.
            return fallback;
        }

        private int GetBossWaveIndex(int stage)
        {
            if (TryGetBossStageProfileIndex(stage, out int profileIndex))
            {
                return profileIndex;
            }

            return Mathf.Max(0, (stage - bossStartStage) / Mathf.Max(1, bossIntervalStage));
        }

        private bool TryGetBossStageProfile(int stage, out BossStageProfile profile)
        {
            if (TryGetBossStageProfileIndex(stage, out int profileIndex))
            {
                profile = GetBossStageProfiles()[profileIndex];
                return profile != null;
            }

            profile = null;
            return false;
        }

        private bool TryGetBossStageProfileIndex(int stage, out int profileIndex)
        {
            BossStageProfile[] profiles = GetBossStageProfiles();

            for (int i = 0; i < profiles.Length; i++)
            {
                BossStageProfile profile = profiles[i];

                if (profile != null && profile.stage == stage)
                {
                    profileIndex = i;
                    return true;
                }
            }

            profileIndex = -1;
            return false;
        }

        private BossStageProfile[] GetBossStageProfiles()
        {
            if (bossStageProfiles != null && bossStageProfiles.Length > 0)
            {
                return bossStageProfiles;
            }

            return CreateDefaultBossStageProfiles();
        }

        private void ApplyBossStageProfile(EnemyController boss, BossStageProfile profile)
        {
            if (boss == null || profile == null)
            {
                return;
            }

            EnemyHealth health = boss.GetComponent<EnemyHealth>();

            if (health != null)
            {
                health.SetMaxHp(profile.maxHp, true);
            }

            BossDiamondSiegeAttack diamondAttack = boss.GetComponent<BossDiamondSiegeAttack>();

            if (diamondAttack != null)
            {
                diamondAttack.ApplyRuntimeDamageProfile(profile.normalProjectileDamage, profile.berserkProjectileDamage);
            }

            BossSummonAttack summonAttack = boss.GetComponent<BossSummonAttack>();

            if (summonAttack != null)
            {
                EnemyController[] elitePrefabs = ResolveBossEliteSummonPrefabs(profile.elitePrefabNameTokens);
                summonAttack.ApplyRuntimeSummonProfile(elitePrefabs, profile.eliteSummonTotalCount, profile.maximumActiveSummonedMonsters);
            }
        }

        private EnemyController[] ResolveBossEliteSummonPrefabs(string[] nameTokens)
        {
            List<EnemyController> results = new List<EnemyController>();

            ResolveReferences();

            if (eliteMixController != null)
            {
                eliteMixController.CollectElitePrefabsByNameTokens(nameTokens, results);
            }

            return results.ToArray();
        }

        private void CleanupActiveBosses()
        {
            activeBosses.RemoveAll(boss => boss == null || boss.IsDead);
        }

        private void ResolveReferences()
        {
            if (enemySpawner == null)
            {
                enemySpawner = FindFirstObjectByType<EnemySpawner>();
            }

            if (bonusChestWaveSpawner == null)
            {
                bonusChestWaveSpawner = FindFirstObjectByType<BonusChestWaveSpawner>();
            }

            if (eliteMixController == null)
            {
                eliteMixController = FindFirstObjectByType<EliteMixController>();
            }
        }

        private static BossStageProfile[] CreateDefaultBossStageProfiles()
        {
            return new[]
            {
                new BossStageProfile
                {
                    stage = 20,
                    profileName = "20 웨이브 보스",
                    maxHp = 6000.0f,
                    normalProjectileDamage = 2,
                    berserkProjectileDamage = 1,
                    eliteSummonTotalCount = 4,
                    maximumActiveSummonedMonsters = 80,
                    elitePrefabNameTokens = new[] { "SuicideCharger", "SlowThrower", "SkeletonGolemJumper" }
                },
                new BossStageProfile
                {
                    stage = 40,
                    profileName = "40 웨이브 보스",
                    maxHp = 20000.0f,
                    normalProjectileDamage = 3,
                    berserkProjectileDamage = 2,
                    eliteSummonTotalCount = 8,
                    maximumActiveSummonedMonsters = 120,
                    elitePrefabNameTokens = new[] { "ObstacleSingle", "BuffCaster", "AreaShield", "SegmentCutCaster", "PortalTotemCaster" }
                },
                new BossStageProfile
                {
                    stage = 60,
                    profileName = "60 웨이브 보스",
                    maxHp = 40000.0f,
                    normalProjectileDamage = 3,
                    berserkProjectileDamage = 2,
                    eliteSummonTotalCount = 10,
                    maximumActiveSummonedMonsters = 150,
                    elitePrefabNameTokens = new[] { "SlowThrower", "ObstacleSingle", "AreaShield", "SegmentCutCaster", "PortalTotemCaster" }
                }
            };
        }
    }
}
