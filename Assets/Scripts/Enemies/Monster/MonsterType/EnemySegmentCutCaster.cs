using System.Collections;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemySegmentCutCaster : MonoBehaviour // 컨보이 꼬리 세그먼트를 추적해 절단 마법을 쓰는 몬스터
    {
        private Transform target; // 전찬우수정-0630 - 넥서스가 아니라 현재 컨보이 꼬리 세그먼트를 사거리 판정 대상으로 사용한다.

        [Header("Cast Reference")]
        [SerializeField] private Transform castPoint; // 마법 투사체가 생성되는 위치

        [SerializeField] private SegmentCutMagicEffect magicEffectPrefab; // 선택된 무기 세그먼트에 표시할 경고 효과 Prefab

        [SerializeField] private SegmentCutProjectile projectilePrefab; // 선택된 무기 세그먼트를 추적할 마법 구체 Prefab

        [Header("Cast Setting")]
        [Min(0.0f)]
        [SerializeField] private float firstCastDelay = 5.0f; // 전찬우수정-0630 - 첫 시전 딜레이

        [Min(1.0f)]
        [SerializeField] private float castRange = 10.0f; // 새로운 마법 시전을 시작할 수 있는 거리

        [Min(1.0f)]
        [SerializeField] private float castInterval = 15.0f; // 전찬우수정-0630 - 마법 발사 후 다음 시전까지의 대기시간

        [Min(0.1f)]
        [SerializeField] private float castDelay = 2.0f; // 선택된 세그먼트를 표시한 뒤 발사하기까지의 준비시간

        private EnemyController ownerController; // 이 Script Component가 붙은 몬스터의 EnemyController

        private Transform selectedTargetSegment; // 이번 마법에서 선택된 무기 세그먼트

        private float castTimer; // 다음 마법 시도까지 남은 시간

        private Coroutine castCoroutine; // 현재 진행 중인 마법 시전 Coroutine

        private SegmentCutMagicEffect currentMagicEffect; // 현재 선택된 세그먼트에 표시된 경고 효과

        private bool hasStartedFirstCast; // 첫 절단 마법을 시작했는지 저장한다.

        public event System.Action CastStarted;

        public event System.Action ProjectileLaunched;

        public float CastRange
        {
            get
            {
                return castRange;
            }
        }

        public bool IsCasting
        {
            get
            {
                return castCoroutine != null;
            }
        }

        public bool ShouldPrioritizeCast // 이동과 기본 원거리 공격보다 절단 마법을 우선할지 반환한다.
        {
            get
            {
                if (!isActiveAndEnabled)
                {
                    return false;
                }

                RefreshTailTarget();

                if (target == null || projectilePrefab == null || magicEffectPrefab == null)
                {
                    return false;
                }

                if (!IsTargetInCastRange())
                {
                    return false;
                }

                if (IsCasting)
                {
                    return true;
                }

                if (!MonsterInteractionApi.HasAvailableSegmentCutTarget())
                {
                    return false;
                }

                if (!hasStartedFirstCast)
                {
                    return true;
                }

                return castTimer <= Time.deltaTime;
            }
        }

        private void Awake()
        {
            ownerController = GetComponent<EnemyController>();

            RefreshTailTarget();
        }

        private void OnEnable()
        {
            castTimer = firstCastDelay;

            hasStartedFirstCast = false;
        }

        private void OnDisable()
        {
            CancelCast();
        }

        private void Update()
        {
            RefreshTailTarget();

            if (target == null)
            {
                return;
            }

            if (projectilePrefab == null)
            {
                return;
            }

            if (magicEffectPrefab == null)
            {
                return;
            }

            if (EnemySupportDebuffState.IsEnemyFrozen(ownerController))
            {
                return;
            }

            if (castCoroutine != null)
            {
                return;
            }

            castTimer -= Time.deltaTime;

            if (castTimer > 0.0f)
            {
                return;
            }

            if (!IsTargetInCastRange())
            {
                return;
            }

            if (!MonsterInteractionApi.TryGetRandomAttachedWeaponSegment(out Transform weaponSegment))
            {
                return;
            }

            selectedTargetSegment = weaponSegment;

            FaceSelectedTarget();

            hasStartedFirstCast = true;

            castCoroutine = StartCoroutine(CastRoutine());

            CastStarted?.Invoke();
        }

        public bool TryGetTailFollowTarget(out Transform tailTarget)
        {
            return MonsterInteractionApi.TryGetSegmentCutTailFollowTarget(out tailTarget);
        }

        public bool IsTargetInCastRange()
        {
            RefreshTailTarget();

            if (target == null)
            {
                return false;
            }

            Vector3 offset = target.position - transform.position;

            offset.y = 0.0f;

            return offset.sqrMagnitude <= castRange * castRange;
        }

        private void RefreshTailTarget()
        {
            if (MonsterInteractionApi.TryGetSegmentCutTailFollowTarget(out Transform tailTarget))
            {
                target = tailTarget;
                return;
            }

            target = null;
        }

        private IEnumerator CastRoutine()
        {
            CreateMagicEffect();

            FaceSelectedTarget();

            float timer = 0.0f;

            while (timer < castDelay)
            {
                RefreshTailTarget();

                if (target == null || selectedTargetSegment == null || !MonsterInteractionApi.IsAttachedSegmentCutTarget(selectedTargetSegment))
                {
                    ReleaseSelectedTargetReservation();

                    CancelCurrentMagicEffect();

                    FinishCast();

                    yield break;
                }

                if (EnemySupportDebuffState.IsEnemyFrozen(ownerController))
                {
                    yield return null;

                    continue;
                }

                FaceSelectedTarget();

                timer += Time.deltaTime;

                yield return null;
            }

            FaceSelectedTarget();

            LaunchProjectile();

            FinishCast();
        }

        private void FaceSelectedTarget()
        {
            FaceTransform(selectedTargetSegment);
        }

        private void FaceTailTarget()
        {
            RefreshTailTarget();

            FaceTransform(target);
        }

        private void FaceTransform(Transform lookTarget)
        {
            if (lookTarget == null)
            {
                return;
            }

            Vector3 direction = lookTarget.position - transform.position;

            direction.y = 0.0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void CreateMagicEffect()
        {
            CancelCurrentMagicEffect();

            if (magicEffectPrefab == null)
            {
                return;
            }

            if (selectedTargetSegment == null)
            {
                return;
            }

            Vector3 effectPosition = selectedTargetSegment.position + Vector3.up * 0.05f;

            currentMagicEffect = Instantiate(magicEffectPrefab, effectPosition, Quaternion.identity, selectedTargetSegment);

            currentMagicEffect.ShowWarning();
        }

        private void LaunchProjectile()
        {
            if (projectilePrefab == null)
            {
                ReleaseSelectedTargetReservation();

                CancelCurrentMagicEffect();

                return;
            }

            if (selectedTargetSegment == null || !MonsterInteractionApi.IsAttachedSegmentCutTarget(selectedTargetSegment))
            {
                ReleaseSelectedTargetReservation();

                CancelCurrentMagicEffect();

                return;
            }

            Vector3 spawnPosition = castPoint != null ? castPoint.position : transform.position;

            Vector3 direction = selectedTargetSegment.position - spawnPosition;

            Quaternion spawnRotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : transform.rotation;

            SegmentCutProjectile projectile = Instantiate(projectilePrefab, spawnPosition, spawnRotation);

            SegmentCutMagicEffect projectileMagicEffect = currentMagicEffect;

            currentMagicEffect = null;

            projectile.Initialize(selectedTargetSegment, projectileMagicEffect);

            ProjectileLaunched?.Invoke();
        }

        private void FinishCast()
        {
            castCoroutine = null;

            selectedTargetSegment = null;

            castTimer = castInterval;

            FaceTailTarget();
        }

        private void CancelCast()
        {
            if (castCoroutine != null)
            {
                StopCoroutine(castCoroutine);

                castCoroutine = null;
            }

            ReleaseSelectedTargetReservation();

            selectedTargetSegment = null;

            CancelCurrentMagicEffect();
        }

        private void ReleaseSelectedTargetReservation()
        {
            if (selectedTargetSegment == null)
            {
                return;
            }

            MonsterInteractionApi.ReleaseSegmentCutTarget(selectedTargetSegment);
        }

        private void CancelCurrentMagicEffect()
        {
            if (currentMagicEffect == null)
            {
                return;
            }

            currentMagicEffect.Cancel();

            currentMagicEffect = null;
        }
    }
}
