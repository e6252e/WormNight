using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace TeamProject01.Gameplay
{
    public sealed class ManaOrbCollectSpecialWave : MonoBehaviour
    {
        private enum ManaOrbSpawnShape
        {
            [InspectorName("별")]
            Star = 0,
            [InspectorName("나선")]
            Spiral = 1,
            [InspectorName("S자")]
            SShape = 2,
            [InspectorName("십자가")]
            Cross = 3,
            [InspectorName("안 전 조 차")]
            SafetyText = 4,
        }

        private const int ManaOrbSpawnShapeCount = 4;
        private const float SafetyTextLetterWidth = 1.0f;
        private const float SafetyTextLetterHeight = 1.35f;
        private const float SafetyTextLetterSpacing = 0.26f;
        private const float SafetyTextTotalWidth = SafetyTextLetterWidth * 4.0f + SafetyTextLetterSpacing * 3.0f;
        private const float SafetyTextWidthScale = 1.9f;

        private static readonly Vector2[][] SafetyTextStrokes =
        {
            new[] { TextPoint(0, 0.30f, 0.60f), TextPoint(0, 0.18f, 0.67f), TextPoint(0, 0.12f, 0.82f), TextPoint(0, 0.18f, 0.97f), TextPoint(0, 0.30f, 1.04f), TextPoint(0, 0.42f, 0.97f), TextPoint(0, 0.48f, 0.82f), TextPoint(0, 0.42f, 0.67f), TextPoint(0, 0.30f, 0.60f) },
            new[] { TextPoint(0, 0.68f, 0.55f), TextPoint(0, 0.68f, 1.15f) },
            new[] { TextPoint(0, 0.68f, 0.84f), TextPoint(0, 0.91f, 0.84f) },
            new[] { TextPoint(0, 0.18f, 0.22f), TextPoint(0, 0.18f, 0.39f), TextPoint(0, 0.82f, 0.39f) },
            new[] { TextPoint(1, 0.17f, 1.12f), TextPoint(1, 0.58f, 1.12f) },
            new[] { TextPoint(1, 0.38f, 1.12f), TextPoint(1, 0.18f, 0.76f) },
            new[] { TextPoint(1, 0.38f, 1.12f), TextPoint(1, 0.60f, 0.76f) },
            new[] { TextPoint(1, 0.74f, 0.58f), TextPoint(1, 0.74f, 1.14f) },
            new[] { TextPoint(1, 0.50f, 0.86f), TextPoint(1, 0.74f, 0.86f) },
            new[] { TextPoint(1, 0.20f, 0.22f), TextPoint(1, 0.20f, 0.39f), TextPoint(1, 0.84f, 0.39f) },
            new[] { TextPoint(2, 0.18f, 1.12f), TextPoint(2, 0.62f, 1.12f) },
            new[] { TextPoint(2, 0.40f, 1.12f), TextPoint(2, 0.18f, 0.78f) },
            new[] { TextPoint(2, 0.40f, 1.12f), TextPoint(2, 0.62f, 0.78f) },
            new[] { TextPoint(2, 0.40f, 0.46f), TextPoint(2, 0.40f, 0.68f) },
            new[] { TextPoint(2, 0.15f, 0.46f), TextPoint(2, 0.66f, 0.46f) },
            new[] { TextPoint(3, 0.22f, 1.22f), TextPoint(3, 0.58f, 1.22f) },
            new[] { TextPoint(3, 0.18f, 1.02f), TextPoint(3, 0.62f, 1.02f) },
            new[] { TextPoint(3, 0.40f, 1.02f), TextPoint(3, 0.18f, 0.72f) },
            new[] { TextPoint(3, 0.40f, 1.02f), TextPoint(3, 0.62f, 0.72f) },
            new[] { TextPoint(3, 0.76f, 0.52f), TextPoint(3, 0.76f, 1.13f) },
            new[] { TextPoint(3, 0.76f, 0.83f), TextPoint(3, 0.96f, 0.83f) },
        };

        [Header("Special Wave Chance")]
        [Min(1)]
        [SerializeField] private int minStartStage = 6; // 이 Stage부터 등장 확률을 체크합니다.
        [Range(0, 100)]
        [SerializeField] private int baseChancePercent = 20; // 첫 등장 체크 확률입니다.
        [Range(0, 100)]
        [SerializeField] private int chanceIncreaseOnFailPercent = 5; // 등장 실패 시 다음 체크에 더할 확률입니다.
        [Range(0, 100)]
        [SerializeField] private int maxChancePercent = 60; // 확률이 이 값보다 커지지 않게 제한합니다.
        [Min(0)]
        [SerializeField] private int cooldownStageCount = 5; // 한 번 등장한 뒤 다시 등장하기 전 대기 Stage 수입니다.
        [SerializeField] private bool blockBossStage = true; // 보스 Stage에서는 등장하지 않게 합니다.

        [Header("Mana Orb Collect")]
        [FormerlySerializedAs("goldPickupPrefab")]
        [SerializeField] private GameObject manaOrbPickupPrefab; // 맵에 뿌릴 마력 구슬 오브젝트입니다.
        [FormerlySerializedAs("goldRoot")]
        [SerializeField] private Transform manaOrbRoot; // 생성된 마력 구슬을 정리할 부모입니다.
        [SerializeField] private Transform nexus; // 마력 구슬 생성 반경의 중심입니다.
        [Min(1)]
        [FormerlySerializedAs("goldSpawnCount")]
        [SerializeField] private int manaOrbSpawnCount = 80; // 특수웨이브 중 생성할 마력 구슬 개수입니다.
        [Min(1.0f)]
        [SerializeField] private float collectDurationSeconds = 20.0f; // 마력 구슬을 수집할 수 있는 시간입니다.
        [Range(0.0f, 200.0f)]
        [SerializeField] private float minSpawnRadius = 15.0f; // Nexus 기준 최소 생성 반경입니다.
        [Range(0.0f, 250.0f)]
        [SerializeField] private float maxSpawnRadius = 45.0f; // Nexus 기준 최대 생성 반경입니다.
        [FormerlySerializedAs("useStarSpawnPattern")]
        [SerializeField] private bool randomizeSpawnShape = true; // 특수웨이브마다 배치 도형을 랜덤 선택합니다.
        [SerializeField] private ManaOrbSpawnShape fixedSpawnShape = ManaOrbSpawnShape.Star; // 랜덤을 끌 때 사용할 고정 도형입니다.
        [Range(0, 100)]
        [SerializeField] private int safetyTextShapeChancePercent = 3; // 랜덤 도형 선택 전에 낮은 확률로 "안 전 조 차" 글자 배치를 선택합니다.
        [Range(-180.0f, 180.0f)]
        [FormerlySerializedAs("starSpawnRotationDegrees")]
        [SerializeField] private float shapeSpawnRotationDegrees = 90.0f; // 도형 첫 꼭짓점이 향하는 각도입니다.
        [FormerlySerializedAs("goldHeightOffset")]
        [SerializeField] private float manaOrbHeightOffset = 0.35f; // 마력 구슬 생성 높이 보정값입니다.
        [Min(0.1f)]
        [SerializeField] private float collectRadius = 1.5f; // 마력 구슬 수집 판정 거리입니다.

        [Header("Reward Threshold")]
        [Range(0, 100)]
        [SerializeField] private int normalChestPercent = 50; // 일반 상자가 나오는 최소 수집률입니다.
        [Range(0, 100)]
        [SerializeField] private int rareChestPercent = 70; // 레어 상자가 나오는 최소 수집률입니다.
        [Range(0, 100)]
        [SerializeField] private int uniqueChestPercent = 90; // 유니크 상자가 나오는 최소 수집률입니다.

        [Header("Reward Chest Prefabs")]
        [SerializeField] private BonusChest normalChestPrefab; // 일반 상자 프리팹입니다.
        [SerializeField] private BonusChest rareChestPrefab; // 레어 상자 프리팹입니다.
        [SerializeField] private BonusChest uniqueChestPrefab; // 유니크 상자 프리팹입니다.
        [SerializeField] private Transform chestRoot; // 생성된 보상 상자를 정리할 부모입니다.

        [Header("Reward Layout")]
        [SerializeField] private Transform rewardCenter; // 보상 상자 줄맞춤 중심 위치입니다.
        [SerializeField] private Vector3 fallbackRewardDirection = Vector3.back; // rewardCenter가 없을 때 Nexus 기준 배치 방향입니다.
        [Min(0.0f)]
        [SerializeField] private float fallbackRewardDistance = 8.0f; // rewardCenter가 없을 때 Nexus에서 떨어질 거리입니다.
        [Min(0.0f)]
        [SerializeField] private float chestSpacing = 4.0f; // 상자 사이 간격입니다.
        [SerializeField] private float chestHeightOffset = 0.0f; // 상자 생성 높이 보정값입니다.
        [Min(0.0f)]
        [SerializeField] private float rewardStageMaxWaitSeconds = 0.0f; // 0이면 상자가 모두 사라질 때까지 기다립니다.

        private readonly List<ManaOrbPickup> activeManaOrbPickups = new List<ManaOrbPickup>(); // 현재 이벤트 마력 구슬 목록입니다.
        private readonly List<BonusChest> activeRewardChests = new List<BonusChest>(); // 현재 보상 상자 목록입니다.
        private Coroutine runningRoutine; // 특수웨이브 진행 루틴입니다.
        private Action onFinished; // 특수웨이브 완료 시 WaveController로 돌려줄 콜백입니다.
        private int failedChanceCount; // 등장 실패 누적 횟수입니다.
        private int lastTriggeredStage = -99999; // 마지막으로 특수웨이브가 등장한 Stage입니다.
        private int collectedManaOrbCount; // 이번 이벤트에서 먹은 마력 구슬 수입니다.
        private int spawnedManaOrbCount; // 이번 이벤트에서 생성한 마력 구슬 수입니다.
        private bool rewardStageActive; // 보상 상자 대기 중인지 여부입니다.
        private bool collectStageActive; // 마력 구슬을 먹을 수 있는 수집 시간인지 기록합니다.
        private float collectEndTime; // 수집 단계가 끝나는 Time.time 기준 시각입니다.
        private ManaOrbSpawnShape currentSpawnShape = ManaOrbSpawnShape.Star; // 이번 특수웨이브에서 선택된 배치 도형입니다.

        public bool IsRunning => runningRoutine != null;
        public bool IsCollectStageActive => collectStageActive;
        public bool IsRewardStageActive => rewardStageActive;
        public int CollectedManaOrbCount => collectedManaOrbCount;
        public int SpawnedManaOrbCount => spawnedManaOrbCount;
        public int RemainingManaOrbCount => Mathf.Clamp(spawnedManaOrbCount - collectedManaOrbCount, 0, spawnedManaOrbCount);
        public float RemainingCollectSeconds => collectStageActive ? Mathf.Max(0.0f, collectEndTime - Time.time) : 0.0f;
        public float CollectedPercent => spawnedManaOrbCount > 0 ? collectedManaOrbCount / (float)spawnedManaOrbCount * 100.0f : 0.0f;
        public int CurrentChancePercent => Mathf.Clamp(baseChancePercent + failedChanceCount * chanceIncreaseOnFailPercent, baseChancePercent, maxChancePercent);

        public bool TryBeginStage(int stage, bool isBossStage, Action finishedCallback)
        {
            if (!CanCheckStage(stage, isBossStage))
            {
                return false;
            }

            int chance = CurrentChancePercent;
            int roll = UnityEngine.Random.Range(0, 100);

            if (roll >= chance)
            {
                failedChanceCount++;
                return false;
            }

            failedChanceCount = 0;
            lastTriggeredStage = stage;
            BeginSpecialWave(finishedCallback);
            return true;
        }

        public void BeginSpecialWave(Action finishedCallback)
        {
            StopSpecialWave(false);
            onFinished = finishedCallback;
            runningRoutine = StartCoroutine(RunSpecialWaveRoutine());
        }

        public void StopSpecialWave(bool notifyFinished)
        {
            if (runningRoutine != null)
            {
                StopCoroutine(runningRoutine);
                runningRoutine = null;
            }

            ClearManaOrbPickups();
            ClearRewardChests();
            collectStageActive = false;
            rewardStageActive = false;

            if (notifyFinished)
            {
                NotifyFinished();
            }
        }

        public void NotifyManaOrbCollected(ManaOrbPickup pickup)
        {
            if (pickup == null)
            {
                return;
            }

            activeManaOrbPickups.Remove(pickup);
            collectedManaOrbCount = Mathf.Min(spawnedManaOrbCount, collectedManaOrbCount + 1);
        }

        [ContextMenu("Test Start Mana Orb Collect Special Wave")]
        public void TestStartSpecialWave()
        {
            BeginSpecialWave(null);
        }

        [ContextMenu("Clear Special Wave Objects")]
        public void ClearSpecialWaveObjects()
        {
            StopSpecialWave(false);
        }

        private bool CanCheckStage(int stage, bool isBossStage)
        {
            if (IsRunning)
            {
                return false;
            }

            if (stage < minStartStage)
            {
                return false;
            }

            if (blockBossStage && isBossStage)
            {
                return false;
            }

            if (stage - lastTriggeredStage <= cooldownStageCount)
            {
                return false;
            }

            return true;
        }

        private IEnumerator RunSpecialWaveRoutine()
        {
            collectedManaOrbCount = 0;
            spawnedManaOrbCount = Mathf.Max(0, manaOrbSpawnCount);
            collectStageActive = true;
            rewardStageActive = false;

            SpawnManaOrbPickups();

            collectEndTime = Time.time + Mathf.Max(0.1f, collectDurationSeconds);
            while (Time.time < collectEndTime)
            {
                CleanupManaOrbList();

                if (HasCollectedAllManaOrbs())
                {
                    break;
                }

                yield return null;
            }

            collectStageActive = false;
            ClearManaOrbPickups();
            SpawnRewardChests();
            rewardStageActive = true;

            float rewardStartTime = Time.time;
            while (!AreRewardChestsCleared())
            {
                if (rewardStageMaxWaitSeconds > 0.0f && Time.time - rewardStartTime >= rewardStageMaxWaitSeconds)
                {
                    break;
                }

                yield return null;
            }

            ClearRewardChests();
            rewardStageActive = false;
            runningRoutine = null;
            NotifyFinished();
        }

        private void SpawnManaOrbPickups()
        {
            ClearManaOrbPickups();

            Transform root = manaOrbRoot != null ? manaOrbRoot : transform;
            Vector3 center = ResolveNexusPosition();
            currentSpawnShape = ResolveManaOrbSpawnShape();

            for (int i = 0; i < spawnedManaOrbCount; i++)
            {
                Vector3 position = GetManaOrbSpawnPosition(center, i, spawnedManaOrbCount, currentSpawnShape);
                GameObject manaOrbObject = manaOrbPickupPrefab != null
                    ? Instantiate(manaOrbPickupPrefab, position, Quaternion.identity, root)
                    : CreateFallbackManaOrbPickup(position, root);

                if (manaOrbObject == null)
                {
                    continue;
                }

                ManaOrbPickup pickup = manaOrbObject.GetComponent<ManaOrbPickup>();
                if (pickup == null)
                {
                    pickup = manaOrbObject.AddComponent<ManaOrbPickup>();
                }

                pickup.Configure(this, collectRadius);
                activeManaOrbPickups.Add(pickup);
            }
        }

        private void SpawnRewardChests()
        {
            ClearRewardChests();

            List<BonusChest> rewardPrefabs = BuildRewardPrefabList(Mathf.RoundToInt(CollectedPercent));
            if (rewardPrefabs.Count <= 0)
            {
                return;
            }

            Vector3 center = ResolveRewardCenter();
            Vector3 right = ResolveRewardRight();
            Transform root = chestRoot != null ? chestRoot : transform;

            for (int i = 0; i < rewardPrefabs.Count; i++)
            {
                BonusChest prefab = rewardPrefabs[i];
                if (prefab == null)
                {
                    continue;
                }

                float centeredIndex = i - (rewardPrefabs.Count - 1) * 0.5f;
                Vector3 position = center + right * centeredIndex * chestSpacing;
                position.y += chestHeightOffset;

                BonusChest chest = Instantiate(prefab, position, Quaternion.identity, root);
                chest.ConfigureChoiceGroup(root, false, 0.0f);
                activeRewardChests.Add(chest);
            }
        }

        private List<BonusChest> BuildRewardPrefabList(int collectPercent)
        {
            List<BonusChest> results = new List<BonusChest>(3);

            if (collectPercent >= normalChestPercent && normalChestPrefab != null)
            {
                results.Add(normalChestPrefab);
            }

            if (collectPercent >= rareChestPercent && rareChestPrefab != null)
            {
                results.Add(rareChestPrefab);
            }

            if (collectPercent >= uniqueChestPercent && uniqueChestPrefab != null)
            {
                results.Add(uniqueChestPrefab);
            }

            return results;
        }

        private bool AreRewardChestsCleared()
        {
            for (int i = activeRewardChests.Count - 1; i >= 0; i--)
            {
                BonusChest chest = activeRewardChests[i];

                if (chest == null)
                {
                    activeRewardChests.RemoveAt(i);
                }
            }

            return activeRewardChests.Count <= 0;
        }

        private bool HasCollectedAllManaOrbs()
        {
            return spawnedManaOrbCount > 0 && RemainingManaOrbCount <= 0;
        }

        private ManaOrbSpawnShape ResolveManaOrbSpawnShape()
        {
            if (!randomizeSpawnShape)
            {
                return fixedSpawnShape;
            }

            if (safetyTextShapeChancePercent > 0 && UnityEngine.Random.Range(0, 100) < safetyTextShapeChancePercent)
            {
                return ManaOrbSpawnShape.SafetyText;
            }

            return (ManaOrbSpawnShape)UnityEngine.Random.Range(0, ManaOrbSpawnShapeCount);
        }

        private Vector3 GetManaOrbSpawnPosition(Vector3 center, int index, int count, ManaOrbSpawnShape shape)
        {
            float innerRadius = Mathf.Min(minSpawnRadius, maxSpawnRadius);
            float outerRadius = Mathf.Max(minSpawnRadius, maxSpawnRadius);

            if (outerRadius <= 0.001f)
            {
                Vector3 fallbackPosition = center;
                fallbackPosition.y += manaOrbHeightOffset;
                return fallbackPosition;
            }

            if (innerRadius <= 0.001f)
            {
                innerRadius = outerRadius * 0.42f;
            }

            Vector3 position = center + GetShapePerimeterOffset(shape, index, count, innerRadius, outerRadius);
            position.y += manaOrbHeightOffset;
            return position;
        }

        private Vector3 GetShapePerimeterOffset(ManaOrbSpawnShape shape, int index, int count, float innerRadius, float outerRadius)
        {
            switch (shape)
            {
                case ManaOrbSpawnShape.Spiral:
                    return GetSpiralOffset(index, count, innerRadius, outerRadius);
                case ManaOrbSpawnShape.SShape:
                    return GetSShapeOffset(index, count, outerRadius);
                case ManaOrbSpawnShape.Cross:
                    return GetCrossPerimeterOffset(index, count, innerRadius, outerRadius);
                case ManaOrbSpawnShape.SafetyText:
                    return GetSafetyTextOffset(index, count, outerRadius);
                default:
                    return GetStarPerimeterOffset(index, count, innerRadius, outerRadius);
            }
        }

        private Vector3 GetStarPerimeterOffset(int index, int count, float innerRadius, float outerRadius)
        {
            const int starPointCount = 5;
            const int starVertexCount = starPointCount * 2;

            float normalizedIndex = count <= 1 ? 0.0f : Mathf.Repeat(index / (float)count, 1.0f);
            float segmentPosition = normalizedIndex * starVertexCount;
            int segmentIndex = Mathf.FloorToInt(segmentPosition) % starVertexCount;
            int nextSegmentIndex = (segmentIndex + 1) % starVertexCount;
            float segmentT = segmentPosition - Mathf.Floor(segmentPosition);

            Vector3 start = GetStarVertexOffset(segmentIndex, innerRadius, outerRadius);
            Vector3 end = GetStarVertexOffset(nextSegmentIndex, innerRadius, outerRadius);
            return Vector3.Lerp(start, end, segmentT);
        }

        private Vector3 GetStarVertexOffset(int vertexIndex, float innerRadius, float outerRadius)
        {
            const int starPointCount = 5;

            float radius = vertexIndex % 2 == 0 ? outerRadius : innerRadius;
            float angle = shapeSpawnRotationDegrees * Mathf.Deg2Rad + vertexIndex * Mathf.PI / starPointCount;
            return new Vector3(Mathf.Cos(angle) * radius, 0.0f, Mathf.Sin(angle) * radius);
        }

        private Vector3 GetSpiralOffset(int index, int count, float innerRadius, float outerRadius)
        {
            const float spiralTurnCount = 2.35f;

            float t = GetOpenShapeT(index, count);
            float radius = Mathf.Lerp(innerRadius * 0.3f, outerRadius, t);
            float angle = shapeSpawnRotationDegrees * Mathf.Deg2Rad + t * Mathf.PI * 2.0f * spiralTurnCount;
            return new Vector3(Mathf.Cos(angle) * radius, 0.0f, Mathf.Sin(angle) * radius);
        }

        private Vector3 GetSShapeOffset(int index, int count, float outerRadius)
        {
            float t = GetOpenShapeT(index, count);
            float x = Mathf.Sin((t - 0.5f) * Mathf.PI * 2.0f) * outerRadius * 0.58f;
            float z = (t - 0.5f) * outerRadius * 2.0f;
            return RotateShapeOffset(new Vector3(x, 0.0f, z));
        }

        private Vector3 GetCrossPerimeterOffset(int index, int count, float innerRadius, float outerRadius)
        {
            float armHalfWidth = Mathf.Clamp(innerRadius, outerRadius * 0.22f, outerRadius * 0.42f);
            Vector3[] vertices =
            {
                new Vector3(-armHalfWidth, 0.0f, outerRadius),
                new Vector3(armHalfWidth, 0.0f, outerRadius),
                new Vector3(armHalfWidth, 0.0f, armHalfWidth),
                new Vector3(outerRadius, 0.0f, armHalfWidth),
                new Vector3(outerRadius, 0.0f, -armHalfWidth),
                new Vector3(armHalfWidth, 0.0f, -armHalfWidth),
                new Vector3(armHalfWidth, 0.0f, -outerRadius),
                new Vector3(-armHalfWidth, 0.0f, -outerRadius),
                new Vector3(-armHalfWidth, 0.0f, -armHalfWidth),
                new Vector3(-outerRadius, 0.0f, -armHalfWidth),
                new Vector3(-outerRadius, 0.0f, armHalfWidth),
                new Vector3(-armHalfWidth, 0.0f, armHalfWidth),
            };

            return RotateShapeOffset(GetPolylineOffset(vertices, index, count, true));
        }

        private Vector3 GetSafetyTextOffset(int index, int count, float outerRadius)
        {
            Vector2 point = GetWeightedStrokePoint(SafetyTextStrokes, GetOpenShapeT(index, count));
            return GetSafetyTextOffset(point, outerRadius);
        }

        private Vector3 GetSafetyTextOffset(Vector2 point, float outerRadius)
        {
            float scale = outerRadius * SafetyTextWidthScale / SafetyTextTotalWidth;
            float x = (point.x - SafetyTextTotalWidth * 0.5f) * scale;
            float z = (point.y - SafetyTextLetterHeight * 0.5f) * scale;
            return RotateShapeOffset(new Vector3(x, 0.0f, z));
        }

        private Vector3 GetPolylineOffset(IReadOnlyList<Vector3> vertices, int index, int count, bool closed)
        {
            if (vertices == null || vertices.Count <= 0)
            {
                return Vector3.zero;
            }

            if (vertices.Count == 1)
            {
                return vertices[0];
            }

            int segmentCount = closed ? vertices.Count : vertices.Count - 1;
            float normalizedIndex = count <= 1 ? 0.0f : Mathf.Repeat(index / (float)count, 1.0f);
            float segmentPosition = normalizedIndex * segmentCount;
            int segmentIndex = Mathf.FloorToInt(segmentPosition);
            float segmentT = segmentPosition - segmentIndex;

            if (closed)
            {
                segmentIndex %= vertices.Count;
                int nextSegmentIndex = (segmentIndex + 1) % vertices.Count;
                return Vector3.Lerp(vertices[segmentIndex], vertices[nextSegmentIndex], segmentT);
            }

            segmentIndex = Mathf.Clamp(segmentIndex, 0, segmentCount - 1);
            return Vector3.Lerp(vertices[segmentIndex], vertices[segmentIndex + 1], segmentT);
        }

        private Vector3 RotateShapeOffset(Vector3 offset)
        {
            float angle = shapeSpawnRotationDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            float x = offset.x * cos - offset.z * sin;
            float z = offset.x * sin + offset.z * cos;
            return new Vector3(x, offset.y, z);
        }

        private static float GetOpenShapeT(int index, int count)
        {
            return count <= 1 ? 0.0f : Mathf.Clamp01(index / (float)(count - 1));
        }

        private static Vector2 TextPoint(int letterIndex, float x, float y)
        {
            return new Vector2(letterIndex * (SafetyTextLetterWidth + SafetyTextLetterSpacing) + x, y);
        }

        private static Vector2 GetWeightedStrokePoint(IReadOnlyList<Vector2[]> strokes, float normalizedPosition)
        {
            if (strokes == null || strokes.Count <= 0)
            {
                return Vector2.zero;
            }

            float totalLength = 0.0f;
            for (int i = 0; i < strokes.Count; i++)
            {
                totalLength += GetStrokeLength(strokes[i]);
            }

            if (totalLength <= 0.0001f)
            {
                return strokes[0] != null && strokes[0].Length > 0 ? strokes[0][0] : Vector2.zero;
            }

            float targetLength = Mathf.Clamp01(normalizedPosition) * totalLength;
            float accumulatedLength = 0.0f;

            for (int strokeIndex = 0; strokeIndex < strokes.Count; strokeIndex++)
            {
                Vector2[] stroke = strokes[strokeIndex];

                if (stroke == null || stroke.Length <= 0)
                {
                    continue;
                }

                for (int pointIndex = 1; pointIndex < stroke.Length; pointIndex++)
                {
                    Vector2 start = stroke[pointIndex - 1];
                    Vector2 end = stroke[pointIndex];
                    float segmentLength = Vector2.Distance(start, end);

                    if (segmentLength <= 0.0001f)
                    {
                        continue;
                    }

                    if (accumulatedLength + segmentLength >= targetLength)
                    {
                        float segmentT = (targetLength - accumulatedLength) / segmentLength;
                        return Vector2.Lerp(start, end, segmentT);
                    }

                    accumulatedLength += segmentLength;
                }
            }

            Vector2[] lastStroke = strokes[strokes.Count - 1];
            return lastStroke != null && lastStroke.Length > 0 ? lastStroke[lastStroke.Length - 1] : Vector2.zero;
        }

        private static float GetStrokeLength(IReadOnlyList<Vector2> stroke)
        {
            if (stroke == null || stroke.Count <= 1)
            {
                return 0.0f;
            }

            float length = 0.0f;
            for (int i = 1; i < stroke.Count; i++)
            {
                length += Vector2.Distance(stroke[i - 1], stroke[i]);
            }

            return length;
        }

        private void CleanupManaOrbList()
        {
            for (int i = activeManaOrbPickups.Count - 1; i >= 0; i--)
            {
                if (activeManaOrbPickups[i] == null)
                {
                    activeManaOrbPickups.RemoveAt(i);
                }
            }
        }

        private void ClearManaOrbPickups()
        {
            for (int i = activeManaOrbPickups.Count - 1; i >= 0; i--)
            {
                ManaOrbPickup pickup = activeManaOrbPickups[i];

                if (pickup != null)
                {
                    DestroyImmediateSafe(pickup.gameObject);
                }
            }

            activeManaOrbPickups.Clear();
        }

        private void ClearRewardChests()
        {
            for (int i = activeRewardChests.Count - 1; i >= 0; i--)
            {
                BonusChest chest = activeRewardChests[i];

                if (chest != null)
                {
                    DestroyImmediateSafe(chest.gameObject);
                }
            }

            activeRewardChests.Clear();
        }

        private Vector3 ResolveNexusPosition()
        {
            if (nexus != null)
            {
                return nexus.position;
            }

            return transform.position;
        }

        private Vector3 ResolveRewardCenter()
        {
            if (rewardCenter != null)
            {
                return rewardCenter.position;
            }

            Vector3 basePosition = ResolveNexusPosition();
            Vector3 direction = fallbackRewardDirection;
            direction.y = 0.0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector3.back;
            }

            direction.Normalize();
            return basePosition + direction * fallbackRewardDistance;
        }

        private Vector3 ResolveRewardRight()
        {
            if (rewardCenter != null)
            {
                Vector3 right = rewardCenter.right;
                right.y = 0.0f;

                if (right.sqrMagnitude > 0.0001f)
                {
                    right.Normalize();
                    return right;
                }
            }

            return Vector3.right;
        }

        private void NotifyFinished()
        {
            Action callback = onFinished;
            onFinished = null;
            callback?.Invoke();
        }

        private static GameObject CreateFallbackManaOrbPickup(Vector3 position, Transform root)
        {
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fallback.name = "ManaOrbPickup_Fallback";
            fallback.transform.SetParent(root, false);
            fallback.transform.position = position;
            fallback.transform.localScale = Vector3.one * 0.7f;
            Renderer renderer = fallback.GetComponent<Renderer>();

            if (renderer != null)
            {
                renderer.sharedMaterial = null;
                renderer.material.color = new Color(1.0f, 0.78f, 0.08f, 1.0f);
            }

            Collider collider = fallback.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return fallback;
        }

        private static void DestroyImmediateSafe(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = ResolveNexusPosition();

            Gizmos.color = new Color(1.0f, 0.78f, 0.08f, 0.75f);
            Gizmos.DrawWireSphere(center, minSpawnRadius);
            Gizmos.DrawWireSphere(center, maxSpawnRadius);

            ManaOrbSpawnShape previewShape = Application.isPlaying && IsRunning ? currentSpawnShape : fixedSpawnShape;
            DrawShapeSpawnGizmo(center, previewShape);

            Gizmos.color = new Color(0.3f, 0.8f, 1.0f, 0.85f);
            Gizmos.DrawWireSphere(ResolveRewardCenter(), 0.6f);
        }

        private void DrawShapeSpawnGizmo(Vector3 center, ManaOrbSpawnShape shape)
        {
            float innerRadius = Mathf.Min(minSpawnRadius, maxSpawnRadius);
            float outerRadius = Mathf.Max(minSpawnRadius, maxSpawnRadius);

            if (outerRadius <= 0.001f)
            {
                return;
            }

            if (innerRadius <= 0.001f)
            {
                innerRadius = outerRadius * 0.42f;
            }

            Gizmos.color = new Color(1.0f, 0.95f, 0.15f, 0.95f);

            if (shape == ManaOrbSpawnShape.SafetyText)
            {
                DrawSafetyTextSpawnGizmo(center, outerRadius);
                return;
            }

            int lineCount = GetShapeGizmoLineCount(shape);
            Vector3 previous = center + GetShapePerimeterOffset(shape, 0, lineCount, innerRadius, outerRadius);
            int endIndex = IsClosedShape(shape) ? lineCount : lineCount - 1;
            for (int i = 1; i <= endIndex; i++)
            {
                Vector3 next = center + GetShapePerimeterOffset(shape, i % lineCount, lineCount, innerRadius, outerRadius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        private static int GetShapeGizmoLineCount(ManaOrbSpawnShape shape)
        {
            switch (shape)
            {
                case ManaOrbSpawnShape.Spiral:
                case ManaOrbSpawnShape.SShape:
                    return 96;
                case ManaOrbSpawnShape.Cross:
                    return 12;
                case ManaOrbSpawnShape.SafetyText:
                    return 160;
                default:
                    return 10;
            }
        }

        private static bool IsClosedShape(ManaOrbSpawnShape shape)
        {
            return shape == ManaOrbSpawnShape.Star || shape == ManaOrbSpawnShape.Cross;
        }

        private void DrawSafetyTextSpawnGizmo(Vector3 center, float outerRadius)
        {
            for (int strokeIndex = 0; strokeIndex < SafetyTextStrokes.Length; strokeIndex++)
            {
                Vector2[] stroke = SafetyTextStrokes[strokeIndex];

                if (stroke == null || stroke.Length <= 1)
                {
                    continue;
                }

                Vector3 previous = center + GetSafetyTextOffset(stroke[0], outerRadius);
                for (int pointIndex = 1; pointIndex < stroke.Length; pointIndex++)
                {
                    Vector3 next = center + GetSafetyTextOffset(stroke[pointIndex], outerRadius);
                    Gizmos.DrawLine(previous, next);
                    previous = next;
                }
            }
        }
    }
}
