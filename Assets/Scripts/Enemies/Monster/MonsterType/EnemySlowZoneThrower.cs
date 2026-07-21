using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemySlowZoneThrower : MonoBehaviour // 슬로우 장판 투척 엘리트 몬스터
    {
        private Transform target; // 투척 대상 위치를 저장한다.
        private EnemyHealth enemyHealth; // 몬스터가 죽었는지 확인할 EnemyHealth

        [SerializeField] private Transform firePoint; // 투사체가 발사될 위치

        [SerializeField] private EnemySlowZoneProjectile projectilePrefab; // 발사할 슬로우 투사체 Prefab

        [SerializeField] private EnemySlowZone slowZonePrefab; // 착탄 후 생성할 슬로우 장판 Prefab

        [SerializeField] private GameObject areaTelegraphPrefab; // 착탄 예고 표시 Prefab, EnemyAreaTelegraph 사용

        private Transform projectileRoot; // 생성된 투사체를 정리할 부모 Transform

        private Transform slowZoneRoot; // 생성된 장판을 정리할 부모 Transform

        private Transform telegraphRoot; // 생성된 범위 표시를 정리할 부모 Transform

        [Min(0.1f)]
        [SerializeField] private float throwRange = 10.0f; // 투척 사거리

        [Min(0.0f)]
        [SerializeField] private float forwardImpactDistance = 4.5f; // 소환 기준 대상 앞쪽으로 얼마나 떨어진 곳에 장판을 소환할지

        [Min(0.0f)]
        [SerializeField] private float sideRandomRange = 2.5f; //소환 기준 대상 앞쪽 위치에서 좌우로 생성 랜덤 범위

        [Min(0.1f)]
        [SerializeField] private float throwDelay = 3.0f; // 투척 간격

        [Min(0.0f)]
        [SerializeField] private float throwReleaseDelay = 0.45f; // Throw 애니메이션 시작 후 실제 투사체를 발사하기까지 기다릴 시간

        [Min(0.1f)]
        [SerializeField] private float slowZoneRadius = 3.0f; // 생성될 슬로우 장판 반경

        [Min(0.1f)]
        [SerializeField] private float slowZoneLifeTime = 4.0f; // 생성될 슬로우 장판 유지 시간

        [Range(0.1f, 1.0f)]
        [SerializeField] private float speedMultiplier = 0.5f; // 장판 안에서 플레이어 이동속도 배율

        [Min(0.0f)]
        [SerializeField] private float telegraphGroundHeight = 0.03f; // 착탄 예고 표시 높이

        [Min(0.0f)]
        [SerializeField] private float slowZoneGroundHeight = 0.04f; // 슬로우 장판 높이

        public float ThrowRange  //외부에서 사거리를 읽을 수 있게 열어둔 property
        {
            get
            {
                return throwRange; //사거리를 반환한다.
            }
        }

        public bool IsPreparingThrow // 장판 시전 준비 중인지 외부 Animator Bridge에서 확인할 수 있게 열어둔다.
        {
            get
            {
                return isPreparingThrow; // 실제 발사를 기다리는 중이면 true를 반환한다.
            }
        }

        private float attackTimer; // 다음 투척까지 남은 시간
        private bool isPreparingThrow; // 장판 투척 시전이 시작되어 실제 발사를 기다리는 중인지 저장한다.
        private float throwReleaseTimer; // 실제 투사체 발사까지 남은 시간
        private Vector3 preparedImpactPosition; // 시전 시작 순간 확정한 장판 착탄 위치

        public event System.Action ThrowStarted; // 장판 투척 애니메이션 시작 이벤트

        private void Awake()
        {
            enemyHealth = GetComponent<EnemyHealth>(); // 같은 GameObject의 EnemyHealth를 찾는다.
            TryFindTarget(); //MonsterInteractionAPI에서 컨보이 타겟을 찾는다.
            attackTimer = throwDelay; // 생성 직후 바로 던지지 않도록 첫 공격 대기 시간을 설정한다.
        }

        private void OnDisable()
        {
            CancelPreparingThrow(); // 비활성화될 때 진행 중인 장판 시전을 취소한다.
        }

        private void Update()
        {
            if (IsDead()) // 몬스터가 죽었다면
            {
                CancelPreparingThrow(); // 죽은 뒤 남아있는 장판 준비를 제거한다.
                return; // 죽은 몬스터는 장판을 던지지 않는다.
            }

            if (projectilePrefab == null) // 투사체 Prefab이 없다면
            {
                return; // 공격할 수 없다.
            }

            if (slowZonePrefab == null) // 장판 Prefab이 없다면
            {
                return; // 착탄 후 만들 것이 없으므로 공격하지 않는다.
            }

            if (areaTelegraphPrefab == null) // 착탄 예고 표시 Prefab이 없다면
            {
                return; // 경고 표시를 만들 수 없으므로 공격하지 않는다.
            }

            if (isPreparingThrow) // 이미 장판 시전이 시작되었다면
            {
                UpdatePreparingThrow(); // 범위 재검사 없이 저장된 위치로 발사를 진행한다.
                return; // 이번 프레임의 새 시전 검사는 하지 않는다.
            }

            if (target == null) // 대상이 없다면
            {
                TryFindTarget(); // MonsterInteractionAPI에서 다시 찾아본다.
            }

            if (target == null) // 그래도 대상이 없다면
            {
                return; // 공격하지 않는다.
            }

            attackTimer -= Time.deltaTime; // 지난 시간만큼 공격 대기 시간을 줄인다.

            if (attackTimer > 0.0f) // 아직 공격 대기 시간이 남아 있다면
            {
                return; // 이번 프레임에는 공격하지 않는다.
            }

            if (!IsTargetInThrowRange()) // 대상이 투척 사거리 밖이라면
            {
                return; // 아직 장판 시전을 시작하지 않는다.
            }

            BeginThrowPrepare(); // 사거리 안에서 투척 준비를 시작한다.
        }

        private void TryFindTarget()
        {
            ////// 전찬우추가-0619 - 컨보이 타겟은 MonsterInteractionApi에서만 조회한다.
            if (MonsterInteractionApi.TryGetConvoyTarget(out Transform apiTarget)) // 전찬우추가-0619 - 등록된 컨보이 타겟이 있는지 확인한다.
            {
                target = apiTarget; // 전찬우추가-0619 - 조회된 컨보이 Transform을 슬로우 투척 대상으로 저장한다.
                return; // 전찬우추가-0619 - 타겟을 찾았으므로 메서드를 종료한다.
            }

            target = null; // 전찬우추가-0619 - 등록된 컨보이가 없으면 투척 대상을 비워둔다.
        }

        public bool IsTargetInThrowRange()
        {
            if (IsDead()) // 몬스터가 죽었다면
            {
                return false; // 죽은 몬스터는 투척 사거리 안으로 판단하지 않는다.
            }

            if (target == null) //대상이 없다면
            {
                TryFindTarget(); //MonsterInteractionAPI에서 컨보이 타겟을 다시 찾아본다.
            }

            if (target == null) //대상이 없다면
            {
                return false; //실행하지 않는다.
            }

            Vector3 offset = target.position - transform.position; //몬스터에서 대상까지 거리 벡터
            offset.y = 0.0f; //높이는 제거한다.

            return offset.sqrMagnitude <= throwRange * throwRange; //대상이 투척 사거리 안에 있는지 반환한다.
        }

        private void BeginThrowPrepare()
        {
            if (IsDead()) // 몬스터가 죽었다면
            {
                return; // 장판 시전을 시작하지 않는다.
            }

            if (target == null) // 대상이 없다면
            {
                return; // 착탄 위치를 계산할 수 없으므로 종료한다.
            }

            preparedImpactPosition = PickImpactPosition(); // 시전 시작 순간의 착탄 위치를 확정한다.
            isPreparingThrow = true; // 실제 발사를 기다리는 상태로 변경한다.
            throwReleaseTimer = throwReleaseDelay; // Throw 애니메이션과 맞추기 위한 발사 대기 시간을 설정한다.
            ThrowStarted?.Invoke(); // 장판 투척 애니메이션 시작

            if (throwReleaseTimer <= 0.0f) // 발사 대기 시간이 없다면
            {
                ReleasePreparedThrow(); // 즉시 저장된 위치로 투사체를 발사한다.
            }
        }

        private void UpdatePreparingThrow()
        {
            if (IsDead()) // 준비 중에 죽었다면
            {
                CancelPreparingThrow(); // 죽은 뒤 발사되지 않도록 준비 상태를 취소한다.
                return; // 발사하지 않는다.
            }

            throwReleaseTimer -= Time.deltaTime; // 지난 시간만큼 발사 대기 시간을 줄인다.

            if (throwReleaseTimer > 0.0f) // 아직 발사 시간이 아니라면
            {
                return; // 계속 기다린다.
            }

            ReleasePreparedThrow(); // 저장된 착탄 위치로 투사체를 발사한다.
        }

        private Vector3 PickImpactPosition()
        {
            Vector3 forward = target.forward; // PlayerConvoy가 바라보는 앞 방향을 가져온다.
            forward.y = 0.0f; // 높이 방향을 제거한다.

            if (forward.sqrMagnitude <= 0.0001f) // 앞 방향을 계산할 수 없다면
            {
                forward = transform.forward; // 디버프 몬스터의 앞 방향을 대신 사용한다.
                forward.y = 0.0f; // 높이 방향을 제거한다.
            }

            if (forward.sqrMagnitude <= 0.0001f) // 그래도 앞 방향을 계산할 수 없다면
            {
                forward = Vector3.forward; // 월드 기준 앞 방향을 사용한다.
            }

            forward.Normalize(); // 앞 방향 벡터를 길이 1로 만든다.

            Vector3 right = new Vector3(forward.z, 0.0f, -forward.x); // 앞 방향을 기준으로 오른쪽 방향을 만든다.

            float sideOffset = Random.Range(-sideRandomRange, sideRandomRange); // 좌우 랜덤 위치를 정한다.

            Vector3 position = target.position + forward * forwardImpactDistance + right * sideOffset; // 대상 앞쪽 일정 거리 위치에 좌우 랜덤값을 더해 착탄 위치를 계산한다.

            return position; // 계산된 착탄 위치를 반환한다.
        }

        private void ReleasePreparedThrow()
        {
            if (IsDead()) // 발사 직전에 죽었다면
            {
                CancelPreparingThrow(); // 준비 상태를 제거한다.
                return; // 투사체를 만들지 않는다.
            }

            Shoot(preparedImpactPosition); // 시전 시작 때 저장해 둔 위치로 투사체를 발사한다.
            isPreparingThrow = false; // 시전 상태를 종료한다.
            throwReleaseTimer = 0.0f; // 발사 대기 시간을 초기화한다.
            attackTimer = throwDelay; // 다음 공격 대기 시간 설정
        }

        private void CancelPreparingThrow()
        {
            isPreparingThrow = false; // 진행 중인 장판 준비를 끈다.
            throwReleaseTimer = 0.0f; // 발사 대기 시간을 초기화한다.
        }

        private void Shoot(Vector3 targetPosition)
        {
            if (IsDead()) // 몬스터가 죽었다면
            {
                return; // 죽은 몬스터는 투사체를 만들지 않는다.
            }

            Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position; // FirePoint가 있으면 그 위치에서, 없으면 자기 위치에서 발사한다.

            Vector3 offset = targetPosition - spawnPosition; // 발사 위치에서 착탄 위치까지의 방향
            offset.y = 0.0f; //높이를 제거한다.

            Quaternion spawnRotation = transform.rotation; // 기본 회전값

            if (offset.sqrMagnitude > 0.0001f) // 방향을 계산할 수 있다면
            {
                spawnRotation = Quaternion.LookRotation(offset.normalized, Vector3.up); // 투사체가 착탄 방향을 바라보게 한다.
            }

            Transform runtimeRoot = MonsterRuntimeRoot.GetRootOrFallback(transform.parent); // Monsters를 찾고, 없으면 현재 몬스터의 부모를 사용한다.

            Transform finalProjectileRoot = projectileRoot != null ? projectileRoot : runtimeRoot; // 투사체 부모를 정한다.
            Transform finalSlowZoneRoot = slowZoneRoot != null ? slowZoneRoot : runtimeRoot; // 슬로우 장판 부모를 정한다.
            Transform finalTelegraphRoot = telegraphRoot != null ? telegraphRoot : runtimeRoot; // 예고 표시 부모를 정한다.

            EnemySlowZoneProjectile projectile = Instantiate(projectilePrefab, spawnPosition, spawnRotation, finalProjectileRoot); // 투사체를 Monsters 밑에 생성한다.

            projectile.Configure(targetPosition, slowZonePrefab, areaTelegraphPrefab, finalSlowZoneRoot, finalTelegraphRoot, slowZoneRadius, slowZoneLifeTime, speedMultiplier, telegraphGroundHeight, slowZoneGroundHeight, enemyHealth); // 투사체에 착탄 위치와 장판 정보와 시전자 체력 정보를 전달한다.
        }

        private bool IsDead()
        {
            return enemyHealth != null && enemyHealth.IsDead; // EnemyHealth가 있고 죽은 상태라면 true를 반환한다.
        }
    }
}