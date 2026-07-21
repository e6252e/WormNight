using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyRangedAttack : MonoBehaviour // 원거리 몬스터의 Nexus 공격 Script Component
    {
        private enum RangedAttackType
        {
            ProjectileArc,
            TargetImpact
        }

        private Transform nexus;

        [SerializeField] private Transform firePoint;

        [SerializeField] private RangedAttackType attackType = RangedAttackType.ProjectileArc;

        [SerializeField] private EnemyProjectile projectilePrefab;

        [SerializeField] private GameObject impactPrefab;

        [Min(0.1f)]
        [SerializeField] private float attackRange = 6.0f;

        [Min(0.1f)]
        [SerializeField] private float attackDelay = 1.5f;

        [Min(0.1f)]
        [SerializeField] private float impactLifeTimeMin = 0.5f;

        [Min(0.1f)]
        [SerializeField] private float impactLifeTimeMax = 1.0f;

        [Min(0.0f)]
        [SerializeField] private float impactSurfaceOffset = 1.2f;

        [SerializeField] private float impactHeightOffset = 0.5f;

        [Range(0.0f, 1.0f)]
        [SerializeField] private float impactSideRandomAmount = 0.5f;

        [Header("Animation Timing")]
        [SerializeField] private bool waitForAnimationEvent;

        [Min(0.1f)]
        [SerializeField] private float animationEventTimeout = 2.0f;

        public float AttackRange
        {
            get
            {
                return attackRange;
            }
        }

        public event System.Action AttackPerformed;

        private float attackTimer;

        private bool attackPending;
        private float pendingAttackPowerMultiplier = 1.0f;
        private float pendingAttackDelay;
        private float pendingAttackTimeout;

        private EnemyBuffReceiver buffReceiver;
        private float stageAttackPowerMultiplier = 1.0f;

        private EnemySegmentCutCaster segmentCutCaster;

        private void Awake()
        {
            buffReceiver = GetComponent<EnemyBuffReceiver>();

            segmentCutCaster = GetComponent<EnemySegmentCutCaster>();

            if (nexus == null)
            {
                GameObject nexusObject = GameObject.Find("Nexus_Core");

                nexus = nexusObject != null ? nexusObject.transform : null;
            }
        }

        private void OnEnable()
        {
            if (segmentCutCaster != null)
            {
                segmentCutCaster.CastStarted += HandleSegmentCutCastStarted;

                segmentCutCaster.ProjectileLaunched += HandleSegmentCutProjectileLaunched;
            }
        }

        private void OnDisable()
        {
            if (segmentCutCaster != null)
            {
                segmentCutCaster.CastStarted -= HandleSegmentCutCastStarted;

                segmentCutCaster.ProjectileLaunched -= HandleSegmentCutProjectileLaunched;
            }

            CancelPendingAttack();
        }

        private void Update()
        {
            if (nexus == null)
            {
                CancelPendingAttack();
                return;
            }

            if (segmentCutCaster != null) // 전찬우수정-0630 - 절단 몬스터는 넥서스 원거리 공격을 사용하지 않고 꼬리 절단만 수행한다.
            {
                CancelPendingAttack();
                return;
            }

            if (attackPending)
            {
                FaceNexus();
                pendingAttackTimeout -= Time.deltaTime;

                if (pendingAttackTimeout <= 0.0f)
                {
                    ReleasePendingAttack();
                }

                return;
            }

            attackTimer -= Time.deltaTime;

            if (attackTimer > 0.0f)
            {
                return;
            }

            Vector3 offset = nexus.position - transform.position;

            offset.y = 0.0f;

            if (offset.sqrMagnitude > attackRange * attackRange)
            {
                return;
            }

            float attackSpeedMultiplier = 1.0f;

            if (buffReceiver != null)
            {
                attackSpeedMultiplier = buffReceiver.GetAttackSpeedMultiplier();
            }

            float finalAttackDelay = Mathf.Max(0.01f, attackDelay / attackSpeedMultiplier);

            float attackPowerMultiplier = GetAttackPowerMultiplier();

            FaceNexus();

            BeginAttack(attackPowerMultiplier, finalAttackDelay);
        }

        private void FaceNexus()
        {
            if (nexus == null)
            {
                return;
            }

            Vector3 direction = nexus.position - transform.position;

            direction.y = 0.0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void HandleSegmentCutCastStarted()
        {
            CancelPendingAttack();
        }

        private void HandleSegmentCutProjectileLaunched()
        {
            CancelPendingAttack();

            attackTimer = 0.0f;
        }

        private void BeginAttack(float attackPowerMultiplier, float finalAttackDelay)
        {
            if (waitForAnimationEvent)
            {
                attackPending = true;

                pendingAttackPowerMultiplier = attackPowerMultiplier;

                pendingAttackDelay = finalAttackDelay;

                pendingAttackTimeout = animationEventTimeout;
            }

            AttackPerformed?.Invoke();

            if (waitForAnimationEvent)
            {
                return;
            }

            ExecuteAttack(attackPowerMultiplier);

            attackTimer = finalAttackDelay;
        }

        public void ReleasePendingAttack()
        {
            if (segmentCutCaster != null) // 전찬우수정-0630 - 애니메이션 이벤트가 남아 있어도 절단 몬스터는 넥서스 공격을 발사하지 않는다.
            {
                CancelPendingAttack();
                return;
            }

            if (!attackPending)
            {
                return;
            }

            float attackPowerMultiplier = pendingAttackPowerMultiplier;

            float finalAttackDelay = pendingAttackDelay;

            attackPending = false;

            pendingAttackPowerMultiplier = 1.0f;

            pendingAttackDelay = 0.0f;

            pendingAttackTimeout = 0.0f;

            if (nexus != null)
            {
                FaceNexus();

                ExecuteAttack(attackPowerMultiplier);
            }

            attackTimer = finalAttackDelay;
        }

        private void CancelPendingAttack()
        {
            attackPending = false;

            pendingAttackPowerMultiplier = 1.0f;

            pendingAttackDelay = 0.0f;

            pendingAttackTimeout = 0.0f;
        }

        private void ExecuteAttack(float attackPowerMultiplier)
        {
            if (attackType == RangedAttackType.ProjectileArc)
            {
                ShootProjectile(attackPowerMultiplier);
                return;
            }

            if (attackType == RangedAttackType.TargetImpact)
            {
                SpawnTargetImpact(attackPowerMultiplier);
            }
        }

        private float GetAttackPowerMultiplier()
        {
            float buffMultiplier = buffReceiver != null ? buffReceiver.GetAttackPowerMultiplier() : 1.0f;
            return buffMultiplier * stageAttackPowerMultiplier;
        }

        private void ShootProjectile(float attackPowerMultiplier)
        {
            if (projectilePrefab == null)
            {
                return;
            }

            Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;

            Vector3 offset = nexus.position - spawnPosition;
            offset.y = 0.0f;

            Quaternion spawnRotation = transform.rotation;

            if (offset.sqrMagnitude > 0.0001f)
            {
                spawnRotation = Quaternion.LookRotation(offset.normalized, Vector3.up);
            }

            Transform runtimeRoot = MonsterRuntimeRoot.GetRootOrFallback(transform.parent);

            EnemyProjectile projectile = Instantiate(projectilePrefab, spawnPosition, spawnRotation, runtimeRoot);
            projectile.Configure(nexus, attackPowerMultiplier);
        }

        private void SpawnTargetImpact(float attackPowerMultiplier)
        {
            if (impactPrefab == null)
            {
                return;
            }

            Vector3 directionToCaster = transform.position - nexus.position;
            directionToCaster.y = 0.0f;

            if (directionToCaster.sqrMagnitude <= 0.0001f)
            {
                directionToCaster = -transform.forward;
                directionToCaster.y = 0.0f;
            }

            Vector3 centerDirection = directionToCaster.normalized;
            Vector3 sideDirection = Vector3.Cross(Vector3.up, centerDirection).normalized;

            float randomSide = Random.Range(-impactSideRandomAmount, impactSideRandomAmount);

            Vector3 surfaceDirection = centerDirection + sideDirection * randomSide;
            surfaceDirection.y = 0.0f;
            surfaceDirection.Normalize();

            Vector3 impactPosition = nexus.position + surfaceDirection * impactSurfaceOffset;
            impactPosition.y += impactHeightOffset;

            Quaternion impactRotation = Quaternion.LookRotation(-surfaceDirection, Vector3.up);

            Transform runtimeRoot = MonsterRuntimeRoot.GetRootOrFallback(transform.parent);

            GameObject impactObject = Instantiate(impactPrefab, impactPosition, impactRotation, runtimeRoot);

            float randomLifeTime = GetRandomImpactLifeTime();

            EnemyImpactDebugVisual impactVisual = impactObject.GetComponent<EnemyImpactDebugVisual>();

            if (impactVisual != null)
            {
                impactVisual.Configure(nexus, randomLifeTime, attackPowerMultiplier);
            }
            else
            {
                Destroy(impactObject, randomLifeTime);
            }
        }

        private float GetRandomImpactLifeTime()
        {
            float minLifeTime = Mathf.Min(impactLifeTimeMin, impactLifeTimeMax);
            float maxLifeTime = Mathf.Max(impactLifeTimeMin, impactLifeTimeMax);

            return Random.Range(minLifeTime, maxLifeTime);
        }

        public void Configure(Transform nexus, EnemyProjectile projectilePrefab, float attackRange, float attackDelay)
        {
            this.nexus = nexus;
            this.projectilePrefab = projectilePrefab;
            this.attackRange = attackRange;
            this.attackDelay = attackDelay;
        }

        public void Configure(Transform nexus, EnemyProjectile projectilePrefab, int unusedAttackDamage, float attackRange, float attackDelay)
        {
            this.nexus = nexus;
            this.projectilePrefab = projectilePrefab;
            this.attackRange = attackRange;
            this.attackDelay = attackDelay;
        }

        public void ApplyAttackPowerMultiplier(float multiplier)
        {
            if (multiplier <= 0.0f || Mathf.Approximately(multiplier, 1.0f))
            {
                return;
            }

            stageAttackPowerMultiplier = Mathf.Max(0.01f, stageAttackPowerMultiplier * multiplier);
        }
    }
}
