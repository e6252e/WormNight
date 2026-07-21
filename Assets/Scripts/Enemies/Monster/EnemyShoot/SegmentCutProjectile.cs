using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class SegmentCutProjectile : MonoBehaviour // 세그먼트 분리 마법 구체
    {
        [Header("Movement")]
        [Min(0.1f)]
        [SerializeField] private float moveSpeed = 8.0f; // 마법 구체 이동속도

        [Min(1.0f)]
        [SerializeField] private float turnSpeed = 220.0f; // 초당 회전속도

        [Min(0.1f)]
        [SerializeField] private float lifeTime = 5.0f; // 최대 생존시간

        [Header("Effect Reference")]
        [SerializeField] private GameObject impactEffectPrefab; // 머리 방어 또는 세그먼트 적중 시 생성할 충돌 효과 Prefab 전체

        private Transform targetSegment; // 추적할 무기 세그먼트
        private SegmentCutMagicEffect targetMagicEffect; // 투사체가 사라질 때 함께 제거할 절단 대상 표시 효과
        private float lifeTimer; // 남은 생존시간
        private bool resolved; // 방어 또는 적중 결과가 이미 처리됐는지 확인
        private bool targetReservationReleased; // 대상 세그먼트 예약을 이미 해제했는지 확인
        private bool targetMagicEffectRemoved; // 절단 대상 표시 효과를 이미 제거했는지 확인

        public void Initialize(Transform target) // 기존 호출부 호환용 초기화 함수
        {
            Initialize(target, null); // 표시 효과 없이 기존 방식으로 초기화한다.
        }

        public void Initialize(Transform target, SegmentCutMagicEffect magicEffect) // 추적 대상과 절단 대상 표시 효과를 함께 전달받는다.
        {
            targetSegment = target; // 마법사가 선택한 무기 세그먼트를 저장한다.
            targetMagicEffect = magicEffect; // 투사체가 사라질 때 제거할 대상 표시 효과를 저장한다.
            lifeTimer = lifeTime; // 투사체 생존시간을 초기화한다.
            resolved = false; // 아직 충돌 결과가 처리되지 않은 상태로 초기화한다.
            targetReservationReleased = false; // 대상 예약이 아직 해제되지 않은 상태로 초기화한다.
            targetMagicEffectRemoved = false; // 대상 표시 효과가 아직 제거되지 않은 상태로 초기화한다.
        }

        private void Update()
        {
            if (resolved)
            {
                return; // 이미 방어 또는 적중 처리가 끝났다면 더 이상 이동하지 않는다.
            }

            if (targetSegment == null)
            {
                ReleaseTargetReservation(); // 추적 대상 예약을 해제한다.
                RemoveTargetMagicEffect(); // 추적 대상 표시 효과를 제거한다.
                Destroy(gameObject); // 선택된 세그먼트가 사라졌다면 투사체를 제거한다.
                return;
            }

            if (!MonsterInteractionApi.IsAttachedSegmentCutTarget(targetSegment))
            {
                ReleaseTargetReservation(); // 더 이상 연결된 대상이 아니므로 예약을 해제한다.
                RemoveTargetMagicEffect(); // 절단할 수 없는 대상이 되었으므로 표시 효과를 제거한다.
                Destroy(gameObject); // 유효하지 않은 대상을 추적 중인 투사체를 제거한다.
                return;
            }

            lifeTimer -= Time.deltaTime; // 지난 시간만큼 생존시간을 감소시킨다.

            if (lifeTimer <= 0.0f)
            {
                ReleaseTargetReservation(); // 제한시간이 끝났으므로 대상 예약을 해제한다.
                RemoveTargetMagicEffect(); // 투사체가 사라지므로 대상 표시 효과를 제거한다.
                Destroy(gameObject); // 제한시간 안에 적중하지 못했다면 투사체를 제거한다.
                return;
            }

            Vector3 targetPosition = targetSegment.position; // 선택된 세그먼트의 현재 위치를 가져온다.
            Vector3 direction = targetPosition - transform.position; // 투사체에서 대상까지의 방향을 계산한다.

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return; // 방향을 계산할 수 없을 정도로 가까우면 이번 프레임에는 이동하지 않는다.
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up); // 대상 세그먼트를 바라보는 회전을 만든다.

            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime); // 회전속도 제한을 적용해 대상 방향으로 회전한다.
            transform.position += transform.forward * (moveSpeed * Time.deltaTime); // 현재 앞 방향으로 마법 구체를 이동시킨다.
        }

        private void OnTriggerEnter(Collider other)
        {
            if (resolved)
            {
                return; // 이미 결과가 처리됐다면 추가 충돌을 무시한다.
            }

            if (MonsterInteractionApi.IsConvoyHeadCollider(other))
            {
                resolved = true; // 머리가 마법 구체를 막았으므로 결과 처리를 완료한다.

                PlayImpactEffect(transform.position); // 머리가 마법을 막은 위치에 충돌 효과를 생성한다.
                ReleaseTargetReservation(); // 대상 세그먼트 예약을 해제한다.
                RemoveTargetMagicEffect(); // 절단 마법이 머리에 막혔으므로 대상 표시 효과를 제거한다.
                Destroy(gameObject); // 세그먼트를 분리하지 않고 투사체만 제거한다.
                return;
            }

            if (!MonsterInteractionApi.IsTargetWeaponSegmentCollider(other, targetSegment))
            {
                return; // 선택된 대상이 아닌 다른 Collider와 세그먼트는 무시한다.
            }

            resolved = true; // 선택된 무기 세그먼트 적중 결과를 한 번만 처리한다.

            PlayImpactEffect(transform.position); // 선택된 세그먼트에 적중한 위치에 충돌 효과를 생성한다.
            MonsterInteractionApi.RequestSegmentCut(targetSegment); // 선택된 세그먼트를 기준으로 실제 분리를 요청한다.
            ReleaseTargetReservation(); // 적중 처리가 끝났으므로 대상 예약을 해제한다.
            RemoveTargetMagicEffect(); // 지정된 세그먼트에 적중했으므로 대상 표시 효과를 제거한다.
            Destroy(gameObject); // 적중 처리가 끝났으므로 투사체를 제거한다.
        }

        private void OnDestroy()
        {
            ReleaseTargetReservation(); // 다른 원인으로 투사체가 제거되어도 대상 예약을 해제한다.
            RemoveTargetMagicEffect(); // 다른 원인으로 투사체가 제거되어도 대상 표시 효과를 제거한다.
        }

        private void ReleaseTargetReservation()
        {
            if (targetReservationReleased)
            {
                return; // 대상 예약을 이미 해제했다면 중복 처리하지 않는다.
            }

            MonsterInteractionApi.ReleaseSegmentCutTarget(targetSegment); // 다른 절단 몬스터가 해당 세그먼트를 선택할 수 있도록 예약을 해제한다.

            targetReservationReleased = true; // 대상 예약 해제가 완료됐음을 저장한다.
        }

        private void RemoveTargetMagicEffect()
        {
            if (targetMagicEffectRemoved)
            {
                return; // 대상 표시 효과를 이미 제거했다면 중복 처리하지 않는다.
            }

            targetMagicEffectRemoved = true; // 제거 요청이 한 번만 실행되도록 상태를 먼저 저장한다.

            if (targetMagicEffect == null)
            {
                return; // 전달받은 대상 표시 효과가 없다면 종료한다.
            }

            targetMagicEffect.Cancel(); // 절단 대상 세그먼트에 남아 있는 표시 효과를 제거한다.

            targetMagicEffect = null; // 제거한 대상 표시 효과 참조를 비운다.
        }

        private void PlayImpactEffect(Vector3 effectPosition)
        {
            if (impactEffectPrefab == null)
            {
                return; // 연결된 충돌 효과 Prefab이 없다면 효과를 생성하지 않는다.
            }

            GameObject impactEffect = Instantiate(impactEffectPrefab, effectPosition, Quaternion.identity); // 충돌 위치에 충돌 효과 Prefab 전체를 생성한다.

            ParticleSystem[] particleSystems = impactEffect.GetComponentsInChildren<ParticleSystem>(true); // Root와 모든 자식에서 Particle System을 찾는다.

            float destroyDelay = 0.0f; // 가장 오래 재생되는 Particle System의 종료시간
            bool foundParticleSystem = false; // 충돌 효과 안에서 Particle System을 찾았는지 확인

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i]; // 현재 재생할 Particle System을 가져온다.

                if (particleSystem == null)
                {
                    continue; // 유효하지 않은 Particle System은 건너뛴다.
                }

                foundParticleSystem = true; // 충돌 효과 안에서 Particle System을 찾았음을 저장한다.

                ParticleSystem.MainModule main = particleSystem.main; // 현재 Particle System의 Main 설정을 가져온다.

                main.stopAction = ParticleSystemStopAction.None; // 자식 Particle System이 자신의 GameObject를 자동으로 제거하지 않게 한다.

                particleSystem.Clear(false); // 이전 Particle이 남아 있을 가능성을 제거한다.
                particleSystem.Play(false); // 현재 Particle System을 처음부터 재생한다.

                float particleDestroyDelay = main.startDelay.constantMax + main.duration + main.startLifetime.constantMax; // 현재 Particle System이 완전히 끝나는 시간을 계산한다.

                destroyDelay = Mathf.Max(destroyDelay, particleDestroyDelay); // 가장 오래 재생되는 종료시간을 저장한다.
            }

            if (!foundParticleSystem)
            {
                destroyDelay = 2.0f; // Particle System이 없다면 안전하게 2초 후 충돌 효과를 제거한다.
            }

            Destroy(impactEffect, Mathf.Max(0.1f, destroyDelay)); // 모든 자식 파티클 재생이 끝난 뒤 충돌 효과 Root 전체를 제거한다.
        }
    }
}