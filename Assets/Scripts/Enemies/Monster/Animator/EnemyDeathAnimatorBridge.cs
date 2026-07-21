using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyDeathAnimatorBridge : MonoBehaviour
    {
        private static readonly int IsMovingParameter = Animator.StringToHash("IsMoving"); // 이동 Animator Bool
        private static readonly int AttackParameter = Animator.StringToHash("Attack"); // 공격 Animator Trigger
        private static readonly int HitParameter = Animator.StringToHash("Hit"); // 피격 Animator Trigger
        private static readonly int ThrowParameter = Animator.StringToHash("Throw"); // 장판 투척 Animator Trigger
        private static readonly int DeathParameter = Animator.StringToHash("Death"); // 사망 Animator Trigger

        [Header("Animator")]
        [SerializeField] private Animator animator; // 몬스터 모델에 붙어 있는 Animator

        [Header("Death")]
        [Min(0.1f)]
        [SerializeField] private float deathDuration = 2.0f; // 죽은 뒤 GameObject를 제거하기까지 기다릴 시간

        [SerializeField] private GameObject hpBarRoot; // 죽을 때 숨길 HP Bar Root

        [Header("Direct Death State")]
        [SerializeField] private bool useDirectDeathState = true; // Death Trigger가 밀릴 때 Death State로 직접 보내기 위한 옵션
        [SerializeField] private string deathStateFullPath = "Base Layer.Death"; // Animator의 Death State 전체 경로
        [Min(0.0f)]
        [SerializeField] private float deathCrossFadeDuration = 0.03f; // Death State로 직접 전환할 때 사용할 짧은 전환 시간

        private bool deathStarted; // 죽음 처리가 이미 시작됐는지 저장한다.

        public bool IsDeathPlaying
        {
            get
            {
                return deathStarted; // 죽음 처리가 시작됐으면 true를 반환한다.
            }
        }

        private void Awake()
        {
            if (animator == null) // Inspector에서 Animator가 연결되지 않았다면
            {
                animator = GetComponentInChildren<Animator>(true); // 자식 모델에서 Animator를 자동으로 찾는다.
            }
        }

        public bool TryBeginDeath()
        {
            if (deathStarted) // 이미 죽음 처리가 시작됐다면
            {
                return true; // 중복 처리하지 않고 성공으로 반환한다.
            }

            if (!CanUseAnimator()) // Animator가 Death 애니메이션을 받을 수 없는 상태라면
            {
                return false; // Death 애니메이션을 시작하지 못했으므로 실패를 반환한다.
            }

            deathStarted = true; // 여기부터는 반드시 Death 처리 중으로 판단한다.

            StopAnimatorConflicts(); // Attack, Hit, Throw, Move가 Death를 방해하지 못하게 정리한다.
            StopGameplayBehaviours(); // 이동, 공격, 장판 같은 실제 행동 Script를 정지한다.
            DisablePhysics(); // 죽은 몬스터가 물리적으로 밀리거나 충돌하지 않게 정리한다.
            HideHpBar(); // HP Bar를 숨긴다.
            PlayDeathAnimation(); // Death 애니메이션을 실행한다.

            Destroy(gameObject, deathDuration); // Death 애니메이션을 볼 시간을 준 뒤 GameObject를 제거한다.

            return true; // Death 처리가 정상 시작됐음을 반환한다.
        }

        private void StopGameplayBehaviours()
        {
            EnemyMovement enemyMovement = GetComponent<EnemyMovement>(); // 이동 Script를 찾는다.

            if (enemyMovement != null) // 이동 Script가 있다면
            {
                enemyMovement.enabled = false; // 죽은 뒤 이동하지 못하게 끈다.
            }

            EnemyMeleeAttack enemyMeleeAttack = GetComponent<EnemyMeleeAttack>(); // 근거리 공격 Script를 찾는다.

            if (enemyMeleeAttack != null) // 근거리 공격 Script가 있다면
            {
                enemyMeleeAttack.enabled = false; // 죽은 뒤 근거리 공격하지 못하게 끈다.
            }

            EnemyRangedAttack enemyRangedAttack = GetComponent<EnemyRangedAttack>(); // 원거리 공격 Script를 찾는다.

            if (enemyRangedAttack != null) // 원거리 공격 Script가 있다면
            {
                enemyRangedAttack.enabled = false; // 죽은 뒤 원거리 공격하지 못하게 끈다.
            }

            EnemySegmentCutCaster enemySegmentCutCaster = GetComponent<EnemySegmentCutCaster>(); // 절단 마법 Script를 찾는다.

            if (enemySegmentCutCaster != null) // 절단 마법 Script가 있다면
            {
                enemySegmentCutCaster.enabled = false; // 죽은 뒤 절단 마법을 쓰지 못하게 끈다.
            }

            EnemySlowZoneThrower enemySlowZoneThrower = GetComponent<EnemySlowZoneThrower>(); // 장판 투척 Script를 찾는다.

            if (enemySlowZoneThrower != null) // 장판 투척 Script가 있다면
            {
                enemySlowZoneThrower.enabled = false; // 죽은 뒤 장판 투척이 시작되지 않게 끈다.
            }

            EnemySlowZoneThrowerAnimatorBridge enemySlowZoneThrowerAnimatorBridge = GetComponent<EnemySlowZoneThrowerAnimatorBridge>(); // 장판 투척 Animator Bridge를 찾는다.

            if (enemySlowZoneThrowerAnimatorBridge != null) // 장판 투척 Animator Bridge가 있다면
            {
                enemySlowZoneThrowerAnimatorBridge.enabled = false; // 죽은 뒤 Throw Trigger가 Death를 방해하지 않게 끈다.
            }
        }

        private void DisablePhysics()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true); // 몬스터와 자식의 Collider를 모두 찾는다.

            for (int i = 0; i < colliders.Length; i++) // 모든 Collider를 순회한다.
            {
                colliders[i].enabled = false; // 죽은 뒤 충돌하지 않게 끈다.
            }

            Rigidbody enemyRigidbody = GetComponent<Rigidbody>(); // Rigidbody를 찾는다.

            if (enemyRigidbody == null) // Rigidbody가 없다면
            {
                return; // 물리 정리할 것이 없으므로 종료한다.
            }

            if (!enemyRigidbody.isKinematic) // 물리 이동 중인 Rigidbody라면
            {
                enemyRigidbody.linearVelocity = Vector3.zero; // 현재 이동 속도를 제거한다.
                enemyRigidbody.angularVelocity = Vector3.zero; // 현재 회전 속도를 제거한다.
            }

            enemyRigidbody.useGravity = false; // 죽은 뒤 중력으로 흔들리지 않게 끈다.
            enemyRigidbody.isKinematic = true; // 죽은 뒤 물리 계산에서 제외한다.
        }

        private void HideHpBar()
        {
            if (hpBarRoot == null) // HP Bar Root가 연결되지 않았다면
            {
                return; // 숨길 HP Bar가 없으므로 종료한다.
            }

            hpBarRoot.SetActive(false); // 죽은 뒤 HP Bar를 숨긴다.
        }

        private void StopAnimatorConflicts()
        {
            if (!CanUseAnimator()) // Animator가 사용할 수 없는 상태라면
            {
                return; // Animator Parameter를 건드리지 않는다.
            }

            SetBoolIfExists(IsMovingParameter, false); // 이동 애니메이션을 끈다.
            ResetTriggerIfExists(AttackParameter); // 남아 있을 수 있는 공격 Trigger를 제거한다.
            ResetTriggerIfExists(HitParameter); // 남아 있을 수 있는 피격 Trigger를 제거한다.
            ResetTriggerIfExists(ThrowParameter); // 남아 있을 수 있는 장판 투척 Trigger를 제거한다.
            ResetTriggerIfExists(DeathParameter); // 이전 Death Trigger가 남아 있다면 정리한다.
        }

        private void PlayDeathAnimation()
        {
            if (!CanUseAnimator()) // Animator가 사용할 수 없는 상태라면
            {
                return; // Death 애니메이션을 실행하지 않는다.
            }

            SetTriggerIfExists(DeathParameter); // Death Trigger가 있는 Controller라면 Death Trigger를 실행한다.

            if (useDirectDeathState) // 직접 Death State 진입 옵션이 켜져 있다면
            {
                TryPlayDeathStateDirectly(); // Trigger 전환이 밀리는 경우를 막기 위해 Death State 직접 진입을 시도한다.
            }

            animator.Update(0.0f); // 같은 프레임에 Animator가 Death 전환을 즉시 평가하게 한다.
        }

        private void TryPlayDeathStateDirectly()
        {
            if (string.IsNullOrEmpty(deathStateFullPath)) // Death State 경로가 비어 있다면
            {
                return; // 직접 진입할 State를 알 수 없으므로 종료한다.
            }

            int deathStateHash = Animator.StringToHash(deathStateFullPath); // Death State 전체 경로를 Hash로 바꾼다.

            if (!animator.HasState(0, deathStateHash)) // Base Layer에 해당 Death State가 없다면
            {
                return; // 직접 진입하지 않고 Trigger 방식만 사용한다.
            }

            animator.CrossFadeInFixedTime(deathStateHash, deathCrossFadeDuration, 0); // Death State로 직접 전환한다.
        }

        private bool CanUseAnimator()
        {
            if (animator == null) // Animator가 없다면
            {
                return false; // 사용할 수 없다.
            }

            if (!animator.isActiveAndEnabled) // Animator가 비활성화되어 있다면
            {
                return false; // 사용할 수 없다.
            }

            if (animator.runtimeAnimatorController == null) // Animator Controller가 없다면
            {
                return false; // 사용할 수 없다.
            }

            return true; // Animator Parameter를 사용할 수 있다.
        }

        private bool HasParameter(int parameterHash, AnimatorControllerParameterType parameterType)
        {
            if (!CanUseAnimator()) // Animator가 사용할 수 없는 상태라면
            {
                return false; // Parameter를 확인할 수 없다.
            }

            AnimatorControllerParameter[] parameters = animator.parameters; // 현재 Animator Controller의 Parameter 목록을 가져온다.

            for (int i = 0; i < parameters.Length; i++) // 모든 Parameter를 순회한다.
            {
                if (parameters[i].nameHash == parameterHash && parameters[i].type == parameterType) // 이름 Hash와 타입이 모두 맞다면
                {
                    return true; // 해당 Parameter가 존재한다.
                }
            }

            return false; // 해당 Parameter가 없다.
        }

        private void SetBoolIfExists(int parameterHash, bool value)
        {
            if (!HasParameter(parameterHash, AnimatorControllerParameterType.Bool)) // Bool Parameter가 없다면
            {
                return; // 없는 Parameter는 건드리지 않는다.
            }

            animator.SetBool(parameterHash, value); // Bool 값을 설정한다.
        }

        private void ResetTriggerIfExists(int parameterHash)
        {
            if (!HasParameter(parameterHash, AnimatorControllerParameterType.Trigger)) // Trigger Parameter가 없다면
            {
                return; // 없는 Trigger는 건드리지 않는다.
            }

            animator.ResetTrigger(parameterHash); // Trigger를 초기화한다.
        }

        private void SetTriggerIfExists(int parameterHash)
        {
            if (!HasParameter(parameterHash, AnimatorControllerParameterType.Trigger)) // Trigger Parameter가 없다면
            {
                return; // 없는 Trigger는 실행하지 않는다.
            }

            animator.SetTrigger(parameterHash); // Trigger를 실행한다.
        }
    }
}