using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public class EnemyObstacleSummoner : MonoBehaviour // 엘리트 몬스터가 장애물을 소환하는 Script Component
    {
        [SerializeField] private EnemyObstacle obstaclePrefab; // 실제로 생성할 장애물 Prefab
        [SerializeField] private GameObject telegraphPrefab; // 장애물이 생성되기 전에 보여줄 범위 표시 Prefab

        private Transform summonTarget; // 장애물 소환 기준이 되는 대상 Transform

        [Min(0.1f)]
        [SerializeField] private float detectionRange = 15.0f; // 대상이 이 범위 안에 들어왔을 때만 장애물을 소환한다.

        private float telegraphStartAlpha = 0.07f; // 범위 표시가 처음 생성될 때의 투명도

        private float telegraphEndAlpha = 1.0f; // 장애물이 나오기 직전 범위 표시의 투명도

        [Min(0.01f)]
        [SerializeField] private float summonInterval = 5.0f; // 장애물을 실제로 생성한 뒤 다음 소환까지 기다릴 시간

        [Min(0.01f)]
        [SerializeField] private float summonDelay = 5.0f; // 범위 표시 후 몇 초 뒤 장애물을 생성할지

        [Min(0.1f)]
        [SerializeField] private float forwardSummonDistance = 5.0f; // 소환 기준 대상 앞쪽으로 얼마나 떨어진 곳에 장애물을 소환할지

        [Min(0.0f)]
        [SerializeField] private float sideRandomRange = 2.0f; // 소환 기준 대상 앞쪽 위치에서 좌우로 생성될 랜덤 범위

        [Min(0.0f)]
        [SerializeField] private float telegraphGroundHeight = 0.03f; // 범위 표시를 바닥보다 살짝 위에 표시할 높이

        [Min(0.0f)]
        [SerializeField] private float obstacleGroundHeight = 1.0f; // 장애물 중심을 바닥보다 위에 둘 높이

        [Min(0.01f)]
        [SerializeField] private float obstacleRadius = 1.2f; // 장애물 반경

        [Min(1.0f)]
        [SerializeField] private float obstacleLifeTime = 8.0f; // 생성된 장애물이 유지되는 시간

        [Min(2)]
        [SerializeField] private int maxActiveObstacles = 5; // 동시에 유지될 수 있는 장애물 최대 개수

        private readonly List<EnemyObstacle> spawnedObstacles = new List<EnemyObstacle>(); // 현재 생성되어 있는 장애물 목록

        private float summonTimer; // 다음 장애물 소환까지 남은 시간
        private bool isSummoning; // 범위 표시 후 장애물 생성 대기 중인지 확인하는 값

        public bool IsSummoning // 외부에서 현재 소환 중인지 읽기 위한 Property
        {
            get
            {
                return isSummoning; // 현재 장애물 소환 과정이 진행 중인지 반환한다.
            }
        }

        public event System.Action<Vector3> ObstacleSummoned;

        private Coroutine summonCoroutine; // 현재 실행 중인 장애물 소환 Coroutine
        private GameObject currentTelegraph; // 현재 생성되어 있는 범위 표시 오브젝트

        private void Awake()
        {
            TryFindSummonTarget(); // MonsterInteractionApi에서 컨보이 타겟을 찾는다.
        }

        private void OnEnable()
        {
            summonTimer = 0.0f; // 생성되거나 활성화되면 바로 첫 장애물 소환을 판단할 수 있게 한다.
            isSummoning = false; // 장애물 소환 대기 상태를 초기화한다.
            summonCoroutine = null; // 현재 실행 중인 Coroutine 참조를 초기화한다.
            currentTelegraph = null; // 현재 생성된 범위 표시 참조를 초기화한다.
        }

        private void OnDisable()
        {
            CancelSummon(true); // 비활성화될 때 진행 중인 범위 표시와 장애물 소환을 취소한다.
        }

        private void Update()
        {
            if (obstaclePrefab == null) // 장애물 Prefab이 없다면
            {
                return; // 실행하지 않는다.
            }

            if (telegraphPrefab == null) // 장애물 범위 표시 Prefab이 없다면
            {
                return; // 실행하지 않는다.
            }

            if (summonTarget == null) // 장애물 소환 기준 대상이 없다면
            {
                TryFindSummonTarget(); // MonsterInteractionApi에서 대상을 다시 찾는다.
            }

            if (summonTarget == null) // 소환 기준 대상이 없다면
            {
                CancelSummon(true); // 진행 중인 범위 표시와 장애물 소환을 취소한다.
                summonTimer = 0.0f; // 소환하지 못했으므로 쿨타임을 시작하지 않고 바로 재시도 가능한 상태로 둔다.
                return; // 기준이 없으므로 종료한다.
            }

            if (!IsTargetInDetectionRange()) // 소환 기준 대상이 감지 범위 안에 없다면
            {
                CancelSummon(true); // 진행 중인 범위 표시와 장애물 소환을 취소한다.
                summonTimer = 0.0f; // 범위 밖에서는 쿨타임을 돌리지 않고 초기 상태로 유지한다.
                return; // 장애물을 소환하지 않는다.
            }

            CleanupSpawnedObstacles(); // 이미 제거된 장애물을 목록에서 정리한다.

            if (spawnedObstacles.Count >= maxActiveObstacles) // 현재 생성된 장애물이 최대 개수 이상이라면
            {
                return; // 더 이상 장애물을 소환하지 않는다.
            }

            if (isSummoning) // 이미 범위 표시 후 장애물 생성 대기 중이라면
            {
                return; // 소환 준비 중에는 쿨타임을 줄이지 않고 중복 소환도 막는다.
            }

            summonTimer -= Time.deltaTime; // 지난 시간만큼 장애물 소환 대기 시간을 줄인다.

            if (summonTimer > 0.0f) // 아직 소환 대기 시간이 남아 있다면
            {
                return; // 이번 프레임에는 소환하지 않는다.
            }

            summonCoroutine = StartCoroutine(SummonObstacleRoutine()); // 범위 표시 후 장애물을 생성하는 Coroutine을 시작한다.
        }

        private IEnumerator SummonObstacleRoutine() // 범위 표시 후 장애물을 생성하는 Coroutine
        {
            isSummoning = true; // 현재 장애물 소환 과정이 진행 중이라고 표시한다.

            Vector3 spawnPosition = PickSummonPosition(); // 장애물이 생성될 위치를 정한다.

            Vector3 telegraphPosition = GroundService.ProjectToGround(spawnPosition, telegraphGroundHeight); // 범위 표시 위치를 바닥 기준 높이로 보정한다.
            Vector3 obstaclePosition = GroundService.ProjectToGround(spawnPosition, obstacleGroundHeight); // 실제 장애물 위치를 바닥 기준 높이로 보정한다.

            Transform runtimeRoot = MonsterRuntimeRoot.GetRootOrFallback(transform.parent); // Monsters를 찾고, 없으면 현재 몬스터의 부모를 사용한다.

            GameObject telegraph = Instantiate(telegraphPrefab, telegraphPosition, Quaternion.identity, runtimeRoot); // 범위 표시 Prefab을 Monsters 밑에 생성한다.
            currentTelegraph = telegraph; // 현재 생성된 범위 표시를 저장한다.

            telegraph.transform.localScale = new Vector3(obstacleRadius * 2.0f, telegraph.transform.localScale.y, obstacleRadius * 2.0f); // 범위 표시 크기를 장애물 반경에 맞춘다.

            SetTelegraphAlpha(telegraph, telegraphStartAlpha); // 범위 표시를 처음에는 흐리게 만든다.

            float timer = 0.0f; // 범위 표시가 유지된 시간을 저장한다.

            while (timer < summonDelay) // 범위 표시 시간이 끝나기 전까지 반복한다.
            {
                timer += Time.deltaTime; // 지난 시간만큼 범위 표시 시간을 증가시킨다.

                float progress = Mathf.Clamp01(timer / summonDelay); // 현재 진행도를 0에서 1 사이 값으로 계산한다.
                float alpha = Mathf.Lerp(telegraphStartAlpha, telegraphEndAlpha, progress); // 진행도에 따라 투명도를 점점 진하게 만든다.

                SetTelegraphAlpha(telegraph, alpha); // 계산된 투명도를 범위 표시에 적용한다.

                yield return null; // 다음 프레임까지 기다린다.
            }

            if (telegraph != null) // 범위 표시가 아직 남아 있다면
            {
                Destroy(telegraph); // 장애물이 나오기 직전에 범위 표시를 제거한다.
            }

            currentTelegraph = null; // 현재 범위 표시 참조를 비운다.

            EnemyObstacle obstacle = Instantiate(obstaclePrefab, obstaclePosition, Quaternion.identity, runtimeRoot); // 실제 장애물 Prefab을 Monsters 밑에 생성한다.
            obstacle.Configure(obstacleRadius, obstacleLifeTime); // 장애물 반경과 유지 시간을 장애물에게 전달한다.
            ObstacleSummoned?.Invoke(obstaclePosition);

            spawnedObstacles.Add(obstacle); // 생성된 장애물을 현재 장애물 목록에 등록한다.

            summonTimer = summonInterval; // 장애물이 실제로 생성된 뒤에 다음 소환 쿨타임을 시작한다.

            summonCoroutine = null; // 현재 Coroutine 참조를 비운다.
            isSummoning = false; // 장애물 소환 과정이 끝났다고 표시한다.
        }

        private void TryFindSummonTarget() // 컨보이 타겟을 찾는 함수
        {
            if (MonsterInteractionApi.TryGetConvoyTarget(out Transform apiTarget)) // 등록된 컨보이 타겟이 있는지 확인한다.
            {
                summonTarget = apiTarget; // 조회된 컨보이 Transform을 장애물 소환 기준으로 저장한다.
                return; // 소환 기준을 찾았으므로 종료한다.
            }

            summonTarget = null; // 등록된 컨보이가 없으면 소환 기준을 비워둔다.
        }

        private bool IsTargetInDetectionRange() // 대상이 감지 범위 안에 있는지 확인하는 함수
        {
            Vector3 offset = summonTarget.position - transform.position; // 엘리트 몬스터 위치에서 소환 기준 대상까지의 방향과 거리 벡터를 구한다.
            offset.y = 0.0f; // 높이 차이를 제거한다.

            return offset.sqrMagnitude <= detectionRange * detectionRange; // 대상이 감지 범위 안에 있는지 반환한다.
        }

        private Vector3 PickSummonPosition() // 장애물 소환 위치를 계산하는 함수
        {
            Transform target = summonTarget != null ? summonTarget : transform; // 소환 기준 대상이 있으면 그 대상을 사용하고, 없으면 자기 자신을 기준으로 사용한다.

            Vector3 forward = target.forward; // 소환 기준 대상이 바라보는 앞 방향을 가져온다.
            forward.y = 0.0f; // 높이 방향을 제거한다.

            if (forward.sqrMagnitude <= 0.0001f) // 앞 방향을 계산할 수 없다면
            {
                forward = transform.forward; // 엘리트 몬스터의 앞 방향을 대신 사용한다.
                forward.y = 0.0f; // 높이 방향을 제거한다.
            }

            if (forward.sqrMagnitude <= 0.0001f) // 그래도 앞 방향을 계산할 수 없다면
            {
                forward = Vector3.forward; // 월드 기준 앞 방향을 사용한다.
            }

            forward.Normalize(); // 앞 방향 벡터를 길이 1로 만든다.

            Vector3 right = new Vector3(forward.z, 0.0f, -forward.x); // 앞 방향을 기준으로 오른쪽 방향을 만든다.

            float sideOffset = Random.Range(-sideRandomRange, sideRandomRange); // 좌우 랜덤 위치를 정한다.

            Vector3 position = target.position + forward * forwardSummonDistance + right * sideOffset; // 대상 앞쪽 일정 거리 위치에 좌우 랜덤값을 더해 장애물 소환 위치를 계산한다.

            return position; // 계산된 장애물 소환 위치를 반환한다.
        }

        private void CleanupSpawnedObstacles() // 제거된 장애물 목록 정리
        {
            spawnedObstacles.RemoveAll(obstacle => obstacle == null); // 이미 제거된 장애물을 목록에서 제거한다.
        }

        private void CancelSummon(bool stopCoroutine) // 진행 중인 소환을 취소하는 함수
        {
            if (stopCoroutine && summonCoroutine != null) // 실행 중인 장애물 소환 Coroutine이 있고, 외부에서 중지해야 한다면
            {
                StopCoroutine(summonCoroutine); // 진행 중인 장애물 소환 Coroutine을 중지한다.
            }

            summonCoroutine = null; // Coroutine 참조를 비운다.

            if (currentTelegraph != null) // 현재 생성된 범위 표시가 있다면
            {
                Destroy(currentTelegraph); // 범위 표시 오브젝트를 제거한다.
                currentTelegraph = null; // 범위 표시 참조를 비운다.
            }

            isSummoning = false; // 장애물 소환 중 상태를 해제한다.
        }

        private void SetTelegraphAlpha(GameObject telegraph, float alpha) // 범위 표시 투명도를 조절하는 함수
        {
            if (telegraph == null) // 범위 표시 오브젝트가 없다면
            {
                return; // 투명도를 바꿀 수 없으므로 종료한다.
            }

            alpha = Mathf.Clamp01(alpha); // 투명도 값을 0에서 1 사이로 제한한다.

            Renderer[] renderers = telegraph.GetComponentsInChildren<Renderer>(); // 범위 표시와 자식 오브젝트들의 Renderer를 모두 가져온다.

            for (int i = 0; i < renderers.Length; i++) // 가져온 Renderer들을 하나씩 순회한다.
            {
                Material material = renderers[i].material; // 현재 Renderer가 사용하는 Material을 가져온다.

                if (material.HasProperty("_BaseColor")) // URP Lit 계열처럼 _BaseColor 속성이 있다면
                {
                    Color color = material.GetColor("_BaseColor"); // 현재 Base Color를 가져온다.
                    color.a = alpha; // Alpha 값을 새 투명도로 변경한다.
                    material.SetColor("_BaseColor", color); // 변경된 색상을 다시 Material에 적용한다.
                }
                else if (material.HasProperty("_Color")) // 기본 Material처럼 _Color 속성이 있다면
                {
                    Color color = material.color; // 현재 색상을 가져온다.
                    color.a = alpha; // Alpha 값을 새 투명도로 변경한다.
                    material.color = color; // 변경된 색상을 다시 Material에 적용한다.
                }
            }
        }
    }
}
