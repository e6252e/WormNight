using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class BonusChestWaveSpawner : MonoBehaviour
    {
        private const float Level2RewardRareChanceBonusPercent = 15.0f; // 희귀 상자 기본 레어 보너스
        private const float Level2RewardUniqueChanceBonusPercent = 5.0f; // 희귀 상자 기본 유니크 보너스
        private const float Level3RewardRareChanceBonusPercent = 30.0f; // 고급 상자 기본 레어 보너스
        private const float Level3RewardUniqueChanceBonusPercent = 15.0f; // 고급 상자 기본 유니크 보너스
        private const float DebugNexusChestMinSpawnRadius = 8.0f; // 디버그 넥서스 주변 상자 최소 반경
        private const float DebugNexusChestMaxSpawnRadius = 14.0f; // 디버그 넥서스 주변 상자 최대 반경

#pragma warning disable CS0649
        [System.Serializable]
        private sealed class BonusChestGradeRule
        {
            [Tooltip("Inspector에서 구분하기 위한 상자 등급 이름입니다.")]
            [InspectorName("등급 이름")]
            public string displayName = "Lv1 일반 상자";

            [Tooltip("이 등급에서 사용할 상자 프리팹입니다. 비워두면 기본 상자 프리팹을 사용합니다.")]
            [InspectorName("상자 프리팹")]
            public BonusChest prefab;

            [Tooltip("이 등급 상자가 뽑힐 확률입니다. 86이면 86%로 이해하면 됩니다.")]
            [InspectorName("등장 확률(%)")]
            [Range(0.0f, 100.0f)]
            public float chancePercent = 86.0f;

            [Tooltip("끄면 등급 인덱스 기본값을 사용합니다. Lv1=0/0, Lv2=+15/+5, Lv3=+30/+15")]
            [InspectorName("보상 확률 직접 지정")]
            public bool overrideRewardChoiceTierChanceBonus;

            [Tooltip("보상 선택 카드의 레어 등장 확률에 더할 값입니다.")]
            [InspectorName("보상 레어 확률 보너스(%)")]
            [Range(0.0f, 100.0f)]
            public float rewardChoiceRareChanceBonusPercent;

            [Tooltip("보상 선택 카드의 유니크 등장 확률에 더할 값입니다.")]
            [InspectorName("보상 유니크 확률 보너스(%)")]
            [Range(0.0f, 100.0f)]
            public float rewardChoiceUniqueChanceBonusPercent;
        }
#pragma warning restore CS0649

        [Header("참조 설정")]
        [Tooltip("등급별 프리팹이 비어 있을 때 사용할 기본 상자 프리팹입니다.")]
        [InspectorName("기본 상자 프리팹")]
        [SerializeField] private BonusChest chestPrefab; // 기존 세팅 호환용 기본 프리팹입니다.

        [Tooltip("생성된 상자를 정리할 부모 Transform입니다. 비워두면 이 오브젝트 아래에 생성합니다.")]
        [InspectorName("상자 생성 부모")]
        [SerializeField] private Transform chestRoot; // 생성된 상자를 묶어둘 부모입니다.

        [Header("생성 위치 설정")]
        [Tooltip("켜두면 컨보이 주변을 기준으로 상자를 생성합니다.")]
        [InspectorName("컨보이 주변에 생성")]
        [SerializeField] private bool spawnAroundConvoy = true; // 컨보이 주변 생성 여부입니다.

        [Tooltip("컨보이 위치를 찾지 못했을 때 사용할 기준 위치입니다.")]
        [InspectorName("대체 기준 위치")]
        [SerializeField] private Transform fallbackCenter; // 컨보이를 못 찾았을 때 사용할 기준입니다.

        [Tooltip("기준 위치에서 최소 몇 m 떨어져 생성할지 정합니다.")]
        [InspectorName("최소 생성 반경")]
        [Range(0.0f, 120.0f)]
        [SerializeField] private float minSpawnRadius = 35.0f; // 플레이어가 상자를 볼 여유를 주기 위한 최소 거리입니다.

        [Tooltip("기준 위치에서 최대 몇 m 떨어져 생성할지 정합니다.")]
        [InspectorName("최대 생성 반경")]
        [Range(1.0f, 160.0f)]
        [SerializeField] private float maxSpawnRadius = 55.0f; // 너무 멀리 가지 않도록 제한하는 거리입니다.

        [Tooltip("상자가 바닥 아래에 묻히지 않도록 올리는 높이입니다.")]
        [InspectorName("바닥 높이 보정")]
        [Range(0.0f, 5.0f)]
        [SerializeField] private float groundHeightOffset = 0.0f; // 바닥 투영 후 올릴 높이입니다.

        [Tooltip("상자끼리 최소 몇 m 이상 떨어지게 배치할지 정합니다.")]
        [InspectorName("상자 간 최소 거리")]
        [Range(0.0f, 60.0f)]
        [SerializeField] private float minChestSpacing = 14.0f; // 상자끼리 너무 붙지 않도록 막는 거리입니다.

        [Tooltip("상자가 너무 가까우면 위치를 다시 뽑는 횟수입니다.")]
        [InspectorName("위치 재시도 횟수")]
        [Range(1, 50)]
        [HideInInspector]
        [SerializeField] private int spawnPositionRetryCount = 16; // 퍼진 위치를 찾기 위한 재시도 횟수입니다.

        [Header("상자 선택 설정")]
        [Tooltip("보너스 웨이브마다 생성할 상자 개수입니다.")]
        [InspectorName("상자 생성 개수")]
        [Range(1, 8)]
        [SerializeField] private int chestSpawnCount = 3; // 기본 기획은 상자 3개입니다.

        [Tooltip("켜두면 여러 상자 중 하나만 열 수 있습니다.")]
        [InspectorName("하나만 선택 가능")]
        [SerializeField] private bool allowOnlyOneChoice = true; // 하나를 선택하면 나머지는 사라집니다.

        [Tooltip("선택되지 않은 상자가 사라지기 전까지 기다리는 시간입니다.")]
        [InspectorName("미선택 상자 제거 대기(초)")]
        [Range(0.0f, 10.0f)]
        [SerializeField] private float unselectedChestDestroyDelay = 0.2f; // 선택되지 않은 상자를 정리하는 시간입니다.

        [Header("등급 확률 설정")]
        [Tooltip("Lv2 이상 상자가 한 번의 보너스 웨이브에 최대 몇 개까지 나올지 정합니다.")]
        [InspectorName("Lv2 이상 최대 등장 수")]
        [Range(0, 8)]
        [SerializeField] private int maxHighGradeCount = 1; // 보상이 과하게 풀리지 않게 제한합니다.

        [Tooltip("켜두면 Lv3 상자가 등장한 웨이브에서는 Lv2 상자가 같이 나오지 않습니다.")]
        [InspectorName("Lv3 등장 시 Lv2 제외")]
        [SerializeField] private bool blockLevel2WhenLevel3Appears = true; // 고급 상자의 희소성을 지킵니다.

        [Tooltip("상자 등급별 프리팹, 등장 확률, 보상 선택 등급 확률 보너스를 정합니다.")]
        [InspectorName("상자 등급 목록")]
        [SerializeField] private BonusChestGradeRule[] chestGrades =
        {
            new BonusChestGradeRule
            {
                displayName = "Lv1 일반 상자",
                chancePercent = 86.0f
            },
            new BonusChestGradeRule
            {
                displayName = "Lv2 희귀 상자",
                chancePercent = 12.0f
            },
            new BonusChestGradeRule
            {
                displayName = "Lv3 고급 상자",
                chancePercent = 2.0f
            }
        };

        private readonly List<BonusChest> activeChests = new List<BonusChest>(); // 현재 보너스 웨이브에 생성된 상자들입니다.
        private BonusChest selectedChest; // 플레이어가 선택한 상자입니다.

        [ContextMenu("Spawn Bonus Chest Wave")]
        public void SpawnBonusChestWave()
        {
            ClearActiveChests();

            Transform root = chestRoot != null ? chestRoot : transform;
            Vector3 center = ResolveSpawnCenter();

            int highGradeCount = 0;
            bool level3Appeared = false;
            int spawnCount = Mathf.Max(1, chestSpawnCount);
            List<Vector3> usedPositions = new List<Vector3>(spawnCount);

            for (int i = 0; i < spawnCount; i++)
            {
                int selectedGradeIndex = RollGradeIndex(highGradeCount, level3Appeared);
                BonusChestGradeRule grade = GetGrade(selectedGradeIndex);
                if (grade == null)
                {
                    continue;
                }

                BonusChest spawnedChest = SpawnChest(grade, selectedGradeIndex, center, root, usedPositions);
                if (spawnedChest == null)
                {
                    continue;
                }

                activeChests.Add(spawnedChest);
                usedPositions.Add(spawnedChest.transform.position);

                if (selectedGradeIndex > 0)
                {
                    highGradeCount++;
                }

                if (selectedGradeIndex >= 2)
                {
                    level3Appeared = true;
                }
            }
        }

        [ContextMenu("Debug Spawn One Bonus Chest Near Nexus")]
        public void SpawnDebugBonusChestNearNexus()
        {
            Transform root = chestRoot != null ? chestRoot : transform; // 상자 부모
            Vector3 center = NexusController.Active != null ? NexusController.Active.transform.position : ResolveSpawnCenter(); // 넥서스 우선
            List<Vector3> usedPositions = CollectActiveChestPositions(); // 기존 상자와 겹침 방지
            int selectedGradeIndex = RollGradeIndex(0, false); // 1개 랜덤 등급
            BonusChestGradeRule grade = GetGrade(selectedGradeIndex);
            if (grade == null)
            {
                Debug.LogWarning("[BonusChestWaveSpawner] 디버그 상자 등급을 결정하지 못했습니다.", this);
                return;
            }

            BonusChest spawnedChest = SpawnChest(
                grade,
                selectedGradeIndex,
                center,
                root,
                usedPositions,
                DebugNexusChestMinSpawnRadius,
                DebugNexusChestMaxSpawnRadius);

            if (spawnedChest == null)
            {
                return;
            }

            activeChests.Add(spawnedChest);
            Debug.Log($"[BonusChestWaveSpawner] 디버그 보상상자 1개 생성: {spawnedChest.transform.position}", spawnedChest);
        }

        public bool TrySelectChest(BonusChest chest)
        {
            if (chest == null)
            {
                return false;
            }

            if (!allowOnlyOneChoice)
            {
                return true;
            }

            if (selectedChest != null)
            {
                return selectedChest == chest;
            }

            selectedChest = chest;
            RemoveUnselectedChests(chest);
            return true;
        }

        private BonusChest SpawnChest(
            BonusChestGradeRule grade,
            int gradeIndex,
            Vector3 center,
            Transform root,
            List<Vector3> usedPositions,
            float minRadiusOverride = -1.0f,
            float maxRadiusOverride = -1.0f)
        {
            BonusChest prefab = grade.prefab != null ? grade.prefab : chestPrefab;
            if (prefab == null)
            {
                Debug.LogWarning("[BonusChestWaveSpawner] 사용할 상자 프리팹이 없습니다.", this);
                return null;
            }

            Vector3 position = GetSeparatedSpawnPosition(center, usedPositions, minRadiusOverride, maxRadiusOverride);
            Quaternion rotation = Quaternion.Euler(0.0f, Random.Range(0.0f, 360.0f), 0.0f);
            BonusChest chest = Instantiate(prefab, position, rotation, root);
            chest.ConfigureOwner(this);
            chest.ConfigureChoiceGroup(root, allowOnlyOneChoice, unselectedChestDestroyDelay);
            chest.ConfigureRewardChoiceTierBonus(
                ResolveRewardChoiceRareChanceBonus(grade, gradeIndex),
                ResolveRewardChoiceUniqueChanceBonus(grade, gradeIndex));
            return chest;
        }

        private static float ResolveRewardChoiceRareChanceBonus(BonusChestGradeRule grade, int gradeIndex)
        {
            if (grade != null && grade.overrideRewardChoiceTierChanceBonus)
            {
                return Mathf.Clamp(grade.rewardChoiceRareChanceBonusPercent, 0.0f, 100.0f);
            }

            return gradeIndex switch
            {
                1 => Level2RewardRareChanceBonusPercent,
                2 => Level3RewardRareChanceBonusPercent,
                _ => 0.0f
            };
        }

        private static float ResolveRewardChoiceUniqueChanceBonus(BonusChestGradeRule grade, int gradeIndex)
        {
            if (grade != null && grade.overrideRewardChoiceTierChanceBonus)
            {
                return Mathf.Clamp(grade.rewardChoiceUniqueChanceBonusPercent, 0.0f, 100.0f);
            }

            return gradeIndex switch
            {
                1 => Level2RewardUniqueChanceBonusPercent,
                2 => Level3RewardUniqueChanceBonusPercent,
                _ => 0.0f
            };
        }

        private int RollGradeIndex(int currentHighGradeCount, bool level3Appeared)
        {
            if (chestGrades == null || chestGrades.Length == 0)
            {
                return -1;
            }

            float totalWeight = 0.0f;
            for (int i = 0; i < chestGrades.Length; i++)
            {
                if (!CanUseGrade(i, currentHighGradeCount, level3Appeared))
                {
                    continue;
                }

                BonusChestGradeRule grade = chestGrades[i];
                if (grade == null)
                {
                    continue;
                }

                totalWeight += Mathf.Max(0.0f, grade.chancePercent);
            }

            if (totalWeight <= 0.0f)
            {
                return FindFirstUsableGrade(currentHighGradeCount, level3Appeared);
            }

            float roll = Random.Range(0.0f, totalWeight);
            float accumulated = 0.0f;

            for (int i = 0; i < chestGrades.Length; i++)
            {
                if (!CanUseGrade(i, currentHighGradeCount, level3Appeared))
                {
                    continue;
                }

                BonusChestGradeRule grade = chestGrades[i];
                if (grade == null)
                {
                    continue;
                }

                accumulated += Mathf.Max(0.0f, grade.chancePercent);
                if (roll <= accumulated)
                {
                    return i;
                }
            }

            return FindFirstUsableGrade(currentHighGradeCount, level3Appeared);
        }

        private bool CanUseGrade(int gradeIndex, int currentHighGradeCount, bool level3Appeared)
        {
            if (gradeIndex < 0 || chestGrades == null || gradeIndex >= chestGrades.Length)
            {
                return false;
            }

            bool isHighGrade = gradeIndex > 0;
            if (isHighGrade && currentHighGradeCount >= maxHighGradeCount)
            {
                return false;
            }

            bool isLevel2 = gradeIndex == 1;
            if (isLevel2 && blockLevel2WhenLevel3Appears && level3Appeared)
            {
                return false;
            }

            return true;
        }

        private int FindFirstUsableGrade(int currentHighGradeCount, bool level3Appeared)
        {
            for (int i = 0; i < chestGrades.Length; i++)
            {
                if (CanUseGrade(i, currentHighGradeCount, level3Appeared))
                {
                    return i;
                }
            }

            return 0;
        }

        private BonusChestGradeRule GetGrade(int index)
        {
            if (chestGrades == null || chestGrades.Length == 0)
            {
                return null;
            }

            int safeIndex = Mathf.Clamp(index, 0, chestGrades.Length - 1);
            return chestGrades[safeIndex];
        }

        private void RemoveUnselectedChests(BonusChest selected)
        {
            for (int i = activeChests.Count - 1; i >= 0; i--)
            {
                BonusChest chest = activeChests[i];
                if (chest == null || chest == selected)
                {
                    continue;
                }

                chest.RemoveWithoutReward(unselectedChestDestroyDelay);
                activeChests.RemoveAt(i);
            }
        }

        private void ClearActiveChests()
        {
            selectedChest = null;

            for (int i = activeChests.Count - 1; i >= 0; i--)
            {
                BonusChest chest = activeChests[i];
                if (chest == null)
                {
                    continue;
                }

                DestroyChestObject(chest.gameObject);
            }

            activeChests.Clear();
        }

        private void DestroyChestObject(GameObject chestObject)
        {
            if (chestObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(chestObject);
                return;
            }

            DestroyImmediate(chestObject);
        }

        private Vector3 ResolveSpawnCenter()
        {
            if (spawnAroundConvoy && MonsterInteractionApi.TryGetConvoyTarget(out Transform convoyTarget))
            {
                return convoyTarget.position;
            }

            if (fallbackCenter != null)
            {
                return fallbackCenter.position;
            }

            return transform.position;
        }

        private List<Vector3> CollectActiveChestPositions()
        {
            List<Vector3> positions = new List<Vector3>(activeChests.Count); // 겹침 방지용
            for (int i = activeChests.Count - 1; i >= 0; i--)
            {
                BonusChest chest = activeChests[i];
                if (chest == null)
                {
                    activeChests.RemoveAt(i); // 제거된 상자 정리
                    continue;
                }

                positions.Add(chest.transform.position);
            }

            return positions;
        }

        private Vector3 GetRandomSpawnPosition(Vector3 center, float minRadiusOverride = -1.0f, float maxRadiusOverride = -1.0f)
        {
            float minRadius = minRadiusOverride >= 0.0f ? minRadiusOverride : minSpawnRadius;
            float maxRadius = maxRadiusOverride >= 0.0f ? maxRadiusOverride : maxSpawnRadius;
            float safeMinRadius = Mathf.Max(0.0f, minRadius);
            float safeMaxRadius = Mathf.Max(safeMinRadius + 0.1f, maxRadius);
            float radius = Random.Range(safeMinRadius, safeMaxRadius);
            float angle = Random.Range(0.0f, Mathf.PI * 2.0f);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * radius;
            Vector3 position = center + offset;

            return GroundService.ProjectToGround(position, groundHeightOffset);
        }

        private Vector3 GetSeparatedSpawnPosition(
            Vector3 center,
            List<Vector3> usedPositions,
            float minRadiusOverride = -1.0f,
            float maxRadiusOverride = -1.0f)
        {
            int retryCount = Mathf.Max(1, spawnPositionRetryCount);
            Vector3 fallbackPosition = GetRandomSpawnPosition(center, minRadiusOverride, maxRadiusOverride);

            for (int i = 0; i < retryCount; i++)
            {
                Vector3 candidate = i == 0 ? fallbackPosition : GetRandomSpawnPosition(center, minRadiusOverride, maxRadiusOverride);
                if (IsFarEnoughFromOtherChests(candidate, usedPositions))
                {
                    return candidate;
                }
            }

            return fallbackPosition; // 자리가 빡빡할 때는 생성 실패보다 마지막 후보를 쓰는 쪽이 테스트에 안전합니다.
        }

        private bool IsFarEnoughFromOtherChests(Vector3 candidate, List<Vector3> usedPositions)
        {
            if (usedPositions == null || usedPositions.Count == 0)
            {
                return true;
            }

            float safeSpacing = Mathf.Max(0.0f, minChestSpacing);
            float safeSpacingSqr = safeSpacing * safeSpacing;

            for (int i = 0; i < usedPositions.Count; i++)
            {
                Vector3 other = usedPositions[i];
                Vector3 flatDelta = new Vector3(candidate.x - other.x, 0.0f, candidate.z - other.z);
                if (flatDelta.sqrMagnitude < safeSpacingSqr)
                {
                    return false;
                }
            }

            return true;
        }

        private void OnValidate()
        {
            chestSpawnCount = Mathf.Max(1, chestSpawnCount);
            maxHighGradeCount = Mathf.Clamp(maxHighGradeCount, 0, chestSpawnCount);
            minChestSpacing = Mathf.Max(0.0f, minChestSpacing);
            spawnPositionRetryCount = Mathf.Max(1, spawnPositionRetryCount);

            if (maxSpawnRadius < minSpawnRadius + 0.1f)
            {
                maxSpawnRadius = minSpawnRadius + 0.1f;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = fallbackCenter != null ? fallbackCenter.position : transform.position;

            Gizmos.color = new Color(0.4f, 0.8f, 1.0f, 0.35f);
            Gizmos.DrawWireSphere(center, minSpawnRadius);

            Gizmos.color = new Color(0.2f, 1.0f, 0.5f, 0.35f);
            Gizmos.DrawWireSphere(center, maxSpawnRadius);
        }
    }
}
