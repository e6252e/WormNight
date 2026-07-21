using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemySlowZoneProjectile : MonoBehaviour // 슬로우 장판을 만드는 투사체
    {
        [Min(0.1f)]
        [SerializeField] private float moveSpeed = 8.0f; // 투사체 이동 속도

        [Min(0.1f)]
        [SerializeField] private float arcHeight = 3.0f; // 포물선 높이

        [Min(0.1f)]
        [SerializeField] private float lifeTime = 5.0f; // 투사체 최대 유지 시간

        [Range(0.01f, 1.0f)]
        [SerializeField] private float telegraphStartAlpha = 0.1f; // 착탄 예고 표시 시작 투명도

        [Range(0.01f, 1.0f)]
        [SerializeField] private float telegraphEndAlpha = 1.0f; // 착탄 직전 투명도

        private Vector3 startPosition; //투사체가 발사된 시작 위치
        private Vector3 targetPosition; //투사체가 도착할 목표 위치

        private float lifeTimer; // 투사체가 생성된 뒤 지난 시간
        private float travelTimer; // 투사체가 날아간 시간
        private float travelDuration; // 시작 위치에서 목표 위치까지 도착하는데 걸릴 시간

        private bool isConfigured; // Configure가 호출되었는지 확인하는 값

        private EnemySlowZone slowZonePrefab; // 착탄 시 생성할 슬로우 장판 Prefab
        private GameObject areaTelegraphPrefab; // 착탄 예고 범위 표시 Prefab
        private EnemyHealth ownerHealth; // 이 투사체를 발사한 몬스터의 EnemyHealth

        private Transform slowZoneRoot; // 생성된 장판을 정리할 부모 Transform
        private Transform telegraphRoot; // 생성된 범위 표시를 정리할 부모 Transform

        private float slowZoneRadius; // 생성될 장판 반경
        private float slowZoneLifeTime; // 생성될 장판 유지 시간
        private float speedMultiplier; // 생성될 장판의 슬로우 배율

        private float telegraphGroundHeight; // 범위 표시 바닥 높이
        private float slowZoneGroundHeight; // 장판 바닥 높이

        private GameObject currentTelegraph; // 현재 생성된 착탄 예고 표시

        private void Update()
        {
            if (ownerHealth != null && ownerHealth.IsDead) // 발사한 몬스터가 죽었다면
            {
                DestroyTelegraph(); // 남아있는 범위 표시를 제거한다.
                Destroy(gameObject); // 죽은 몬스터의 투사체는 장판을 만들지 않고 제거한다.
                return;
            }

            lifeTimer += Time.deltaTime; // 지난 시간만큼 유지 시간을 증가시킨다.

            if (lifeTimer >= lifeTime) // 최대 유지 시간이 끝났다면
            {
                DestroyTelegraph(); // 남아있는 범위 표시를 제거한다.
                Destroy(gameObject); // 투사체를 제거한다.
                return;
            }

            if (!isConfigured) // 아직 설정이 끝나지 않았다면
            {
                return; // 이동하지 않는다.
            }

            travelTimer += Time.deltaTime; // 지난 시간만큼 비행 시간을 증가시킨다.

            float progress = travelTimer / travelDuration; // 현재 비행 진행도를 계산한다.
            progress = Mathf.Clamp01(progress); // 0에서 1 사이로 제한한다.

            UpdateTelegraphAlpha(progress); // 진행도에 맞게 착탄 예고 표시를 진하게 만든다.
            UpdateProjectilePosition(progress); // 포물선 위치를 계산해서 적용한다.

            if (progress >= 1.0f) // 착탄 위치에 도착했다면
            {
                CreateSlowZone(); // 슬로우 장판을 생성한다.
                DestroyTelegraph(); // 착탄 예고 표시를 제거한다.
                Destroy(gameObject); // 투사체를 제거한다.
            }
        }

        private void OnDisable()
        {
            DestroyTelegraph(); // 투사체가 중간에 삭제되어도 범위 표시가 남지 않게 제거한다.
        }

        public void Configure(Vector3 targetPosition, EnemySlowZone slowZonePrefab, GameObject areaTelegraphPrefab, Transform slowZoneRoot, Transform telegraphRoot,
                              float slowZoneRadius, float slowZoneLifeTime, float speedMultiplier, float telegraphGroundHeight, float slowZoneGroundHeight) // 기존 호출을 유지하기 위한 Configure 함수
        {
            Configure(targetPosition, slowZonePrefab, areaTelegraphPrefab, slowZoneRoot, telegraphRoot, slowZoneRadius, slowZoneLifeTime, speedMultiplier, telegraphGroundHeight, slowZoneGroundHeight, null); // 시전자 정보 없이 설정한다.
        }

        public void Configure(Vector3 targetPosition, EnemySlowZone slowZonePrefab, GameObject areaTelegraphPrefab, Transform slowZoneRoot, Transform telegraphRoot,
                              float slowZoneRadius, float slowZoneLifeTime, float speedMultiplier, float telegraphGroundHeight, float slowZoneGroundHeight, EnemyHealth ownerHealth) //투사체를 생성한 뒤 필요한 값을 넣어주는 함수다.
        {
            this.slowZonePrefab = slowZonePrefab;
            this.areaTelegraphPrefab = areaTelegraphPrefab;
            this.ownerHealth = ownerHealth;

            this.slowZoneRoot = slowZoneRoot;
            this.telegraphRoot = telegraphRoot;

            this.slowZoneRadius = Mathf.Max(0.1f, slowZoneRadius);
            this.slowZoneLifeTime = Mathf.Max(0.1f, slowZoneLifeTime);
            this.speedMultiplier = Mathf.Clamp(speedMultiplier, 0.1f, 1.0f);

            this.telegraphGroundHeight = Mathf.Max(0.0f, telegraphGroundHeight);
            this.slowZoneGroundHeight = Mathf.Max(0.0f, slowZoneGroundHeight);

            startPosition = transform.position; // 현재 위치를 발사 시작 위치로 저장한다.
            this.targetPosition = GroundService.ProjectToGround(targetPosition, this.slowZoneGroundHeight); // 착탄 위치를 바닥 높이에 맞춘다.

            Vector3 flatOffset = this.targetPosition - startPosition; // 시작 위치에서 착탄 위치까지의 거리 벡터
            flatOffset.y = 0.0f; // 평면 거리 기준으로 계산한다.

            float distance = flatOffset.magnitude; // 평면 거리
            travelDuration = distance / moveSpeed; // 거리와 속도로 도착 시간을 계산한다.
            travelDuration = Mathf.Max(0.1f, travelDuration); // 너무 짧은 비행 시간이 되지 않게 보정한다.

            lifeTimer = 0.0f; // 유지 시간 초기화
            travelTimer = 0.0f; // 비행 시간 초기화

            CreateTelegraph(); // 착탄 위치에 범위 예고 표시를 만든다.

            isConfigured = true; // 설정 완료
        }

        private void UpdateProjectilePosition(float progress)
        {
            Vector3 flatPosition = Vector3.Lerp(startPosition, targetPosition, progress); // 시작 위치에서 착탄 위치까지 직선 보간한다.

            float height = Mathf.Sin(progress * Mathf.PI) * arcHeight; // 중간 지점에서 가장 높아지는 포물선 높이

            Vector3 nextPosition = flatPosition + Vector3.up * height; // 평면 위치에 높이를 더한 최종 위치

            Vector3 moveDirection = nextPosition - transform.position; // 현재 위치에서 다음 위치로 향하는 방향

            transform.position = nextPosition; // 계산된 위치 적용

            if (moveDirection.sqrMagnitude > 0.0001f) // 이동 방향이 충분히 있다면
            {
                transform.rotation = Quaternion.LookRotation(moveDirection.normalized, Vector3.up); // 이동 방향을 바라보게 회전한다.
            }
        }

        private void CreateTelegraph()
        {
            if (areaTelegraphPrefab == null) // 범위 표시 Prefab이 없다면
            {
                return; //종료한다.
            }

            Vector3 telegraphPosition = GroundService.ProjectToGround(targetPosition, telegraphGroundHeight); // 범위 표시 위치를 바닥 높이에 맞춘다.

            currentTelegraph = Instantiate(areaTelegraphPrefab, telegraphPosition, Quaternion.identity, telegraphRoot); // 범위 표시 생성

            float diameter = slowZoneRadius * 2.0f; // 반경을 지름으로 변환한다.
            currentTelegraph.transform.localScale = new Vector3(diameter, currentTelegraph.transform.localScale.y, diameter); // 범위 표시 크기를 장판 반경에 맞춘다.

            SetTelegraphAlpha(currentTelegraph, telegraphStartAlpha); // 처음에는 흐리게 표시한다.
        }

        private void UpdateTelegraphAlpha(float progress)
        {
            if (currentTelegraph == null) // 범위 표시가 없다면
            {
                return; // 투명도 변경 불가
            }

            float alpha = Mathf.Lerp(telegraphStartAlpha, telegraphEndAlpha, progress); // 진행도에 따라 점점 진하게 만든다.
            SetTelegraphAlpha(currentTelegraph, alpha); // 계산된 알파를 적용한다.
        }

        private void DestroyTelegraph()
        {
            if (currentTelegraph != null) // 범위 표시가 남아 있다면
            {
                Destroy(currentTelegraph); // 제거한다.
                currentTelegraph = null; // 참조를 비운다.
            }
        }

        private void CreateSlowZone()
        {
            if (ownerHealth != null && ownerHealth.IsDead) // 착탄 직전에 시전자가 죽었다면
            {
                return; // 죽은 몬스터의 장판은 생성하지 않는다.
            }

            if (slowZonePrefab == null) // 생성할 슬로우 장판 Prefab이 없다면
            {
                return; // 만들 수 없으므로 종료한다.
            }

            Vector3 slowZonePosition = GroundService.ProjectToGround(targetPosition, slowZoneGroundHeight); // 장판 생성 위치를 바닥 높이에 맞춘다.

            EnemySlowZone slowZone = Instantiate(slowZonePrefab, slowZonePosition, Quaternion.identity, slowZoneRoot); // 슬로우 장판 생성
            slowZone.Configure(slowZoneRadius, slowZoneLifeTime, speedMultiplier); // 장판 수치 전달
        }

        private void SetTelegraphAlpha(GameObject telegraph, float alpha)
        {
            if (telegraph == null) // 범위 표시가 없다면
            {
                return; //종료한다.
            }

            alpha = Mathf.Clamp01(alpha); // 0에서 1 사이로 제한한다.

            Renderer[] renderers = telegraph.GetComponentsInChildren<Renderer>(); // 범위 표시의 모든 Renderer를 가져온다.

            for (int i = 0; i < renderers.Length; i++) // Renderer를 순회한다.
            {
                Material material = renderers[i].material; // 현재 Renderer의 Material을 가져온다.

                if (material.HasProperty("_BaseColor"))
                {
                    Color color = material.GetColor("_BaseColor");
                    color.a = alpha; // 알파 변경
                    material.SetColor("_BaseColor", color);
                }
                else if (material.HasProperty("_Color"))
                {
                    Color color = material.color;
                    color.a = alpha;
                    material.color = color;
                }
            }
        }
    }
}