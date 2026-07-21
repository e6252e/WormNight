using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyRangedAnimatorBridge : MonoBehaviour // 원거리 몬스터의 이동, 공격, 피격 애니메이션을 연결하는 Script Component
    {
        public static readonly int IsMovingParameter = Animator.StringToHash("IsMoving"); // 이동 상태 Animator Parameter
        public static readonly int AttackParameter = Animator.StringToHash("Attack"); // 공격 Animator Trigger
        public static readonly int HitParameter = Animator.StringToHash("Hit"); // 피격 Animator Trigger

        [Header("Animator")]
        [SerializeField] private Animator animator; // 원거리 몬스터 모델에 붙어 있는 Animator

        private EnemyMovement enemyMovement; // 몬스터 이동 상태를 읽을 EnemyMovement
        private EnemyRangedAttack enemyRangedAttack; // 원거리 공격 실행 이벤트를 받을 EnemyRangedAttack
        private EnemyHealth enemyHealth; // 몬스터의 현재 HP를 읽을 EnemyHealth
        private EnemySupportDebuffState supportDebuffState; // 동결 상태를 확인할 EnemySupportDebuffState
        private EnemySegmentCutCaster segmentCutCaster; // 현재 절단 마법 시전 중인지 확인할 EnemySegmentCutCaster
        private EnemySlowZoneThrower slowZoneThrower; // 장판 투척 준비 중인지 확인할 EnemySlowZoneThrower
        private EnemySlowZoneThrowerAnimatorBridge slowZoneThrowerAnimatorBridge; // Throw 애니메이션 잠금 상태를 확인할 Bridge

        private float previousHp; // 직전 프레임의 HP

        private void Awake()
        {
            enemyMovement = GetComponent<EnemyMovement>(); // 같은 GameObject의 EnemyMovement를 찾는다.
            enemyRangedAttack = GetComponent<EnemyRangedAttack>(); // 같은 GameObject의 EnemyRangedAttack을 찾는다.
            enemyHealth = GetComponent<EnemyHealth>(); // 같은 GameObject의 EnemyHealth를 찾는다.
            supportDebuffState = GetComponent<EnemySupportDebuffState>(); // 같은 GameObject의 동결 상태 Script Component를 찾는다.
            segmentCutCaster = GetComponent<EnemySegmentCutCaster>(); // 같은 GameObject의 절단 마법 Script Component를 찾는다.
            slowZoneThrower = GetComponent<EnemySlowZoneThrower>(); // 같은 GameObject의 장판 투척 Script Component를 찾는다.
            slowZoneThrowerAnimatorBridge = GetComponent<EnemySlowZoneThrowerAnimatorBridge>(); // 같은 GameObject의 장판 투척 Animator Bridge를 찾는다.

            if (animator == null) // Inspector에서 Animator가 연결되지 않았다면
            {
                animator = GetComponentInChildren<Animator>(true); // 자식 모델에서 Animator를 자동으로 찾는다.
            }

            if (enemyHealth != null) // EnemyHealth가 있다면
            {
                previousHp = enemyHealth.CurrentHp; // 시작 HP를 직전 HP로 저장한다.
            }
        }

        private void OnEnable()
        {
            if (enemyRangedAttack != null) // EnemyRangedAttack이 있다면
            {
                enemyRangedAttack.AttackPerformed += PlayAttack; // 원거리 공격이 발생할 때 PlayAttack을 실행하도록 연결한다.
            }

            if (enemyHealth != null) // EnemyHealth가 있다면
            {
                previousHp = enemyHealth.CurrentHp; // 다시 활성화될 때 HP 비교값을 초기화한다.
            }

            UpdateMovementAnimation(); // 활성화되는 순간 현재 이동 상태를 Animator에 반영한다.
        }

        private void Update()
        {
            UpdateMovementAnimation(); // 매 프레임 이동 애니메이션 상태를 갱신한다.
            UpdateHitAnimation(); // 매 프레임 HP 감소 여부를 확인해 피격 애니메이션을 실행한다.
        }

        private void OnDisable()
        {
            if (enemyRangedAttack != null) // EnemyRangedAttack이 있다면
            {
                enemyRangedAttack.AttackPerformed -= PlayAttack; // 비활성화될 때 공격 이벤트 연결을 해제한다.
            }

            if (!CanUseAnimator()) // Animator가 Parameter를 받을 수 없는 상태라면
            {
                return; // 초기화하지 않고 종료한다.
            }

            animator.SetBool(IsMovingParameter, false); // 비활성화될 때 이동 상태를 끈다.
            animator.ResetTrigger(AttackParameter); // 남아 있을 수 있는 공격 Trigger를 초기화한다.
            animator.ResetTrigger(HitParameter); // 남아 있을 수 있는 피격 Trigger를 초기화한다.
        }

        public void PlayAttack() // 원거리 공격이 실행될 때 공격 애니메이션을 재생하는 함수
        {
            if (enemyHealth != null && enemyHealth.IsDead) // 몬스터가 죽은 상태라면
            {
                return; // 죽은 몬스터는 공격 애니메이션을 실행하지 않는다.
            }

            if (!CanUseAnimator()) // Animator가 Parameter를 받을 수 없는 상태라면
            {
                return; // 공격 애니메이션을 실행하지 않는다.
            }

            animator.ResetTrigger(AttackParameter); // 이전 공격 Trigger가 남아 있다면 초기화한다.
            animator.SetTrigger(AttackParameter); // 공격 Trigger를 실행한다.
        }

        private void PlayHit() // 몬스터가 피해를 받았을 때 피격 애니메이션을 재생하는 함수
        {
            if (enemyHealth != null && enemyHealth.IsDead) // 몬스터가 죽은 상태라면
            {
                return; // 죽은 몬스터는 피격 애니메이션을 실행하지 않는다.
            }

            if (IsSlowZoneThrowing()) // 장판 투척 준비 또는 Throw 애니메이션 중이라면
            {
                return; // Throw 흐름을 끊지 않기 위해 Hit 애니메이션을 생략한다.
            }

            if (!CanUseAnimator()) // Animator가 Parameter를 받을 수 없는 상태라면
            {
                return; // 피격 애니메이션을 실행하지 않는다.
            }

            animator.ResetTrigger(HitParameter); // 이전 피격 Trigger가 남아 있다면 초기화한다.
            animator.SetTrigger(HitParameter); // 피격 Trigger를 실행한다.
        }

        private void UpdateMovementAnimation() // 현재 몬스터 이동 상태를 Animator에 전달하는 함수
        {
            if (!CanUseAnimator() || enemyMovement == null) // Animator 또는 EnemyMovement가 없다면
            {
                return; // 이동 상태를 계산할 수 없으므로 종료한다.
            }

            if (enemyHealth != null && enemyHealth.IsDead) // 몬스터가 죽은 상태라면
            {
                animator.SetBool(IsMovingParameter, false); // 죽은 몬스터는 이동 애니메이션을 끈다.
                return; // 더 계산하지 않는다.
            }

            bool isFrozen = supportDebuffState != null && supportDebuffState.IsFrozen; // 현재 동결 상태인지 확인한다.
            bool isSlowZoneThrowing = IsSlowZoneThrowing(); // 장판 투척 준비 또는 Throw 애니메이션 중인지 확인한다.
            bool isMoving = enemyMovement.enabled && !enemyMovement.IsInStopRange && !isFrozen && !isSlowZoneThrowing; // 이동 Script가 켜져 있고 공격 사거리 밖이며 동결/투척 중이 아니라면 이동 중이다.

            animator.SetBool(IsMovingParameter, isMoving); // 계산된 이동 상태를 Animator에 전달한다.
        }

        private void UpdateHitAnimation() // HP 감소 여부를 확인하는 함수
        {
            if (enemyHealth == null) // EnemyHealth가 없다면
            {
                return; // HP를 비교할 수 없으므로 종료한다.
            }

            float currentHp = enemyHealth.CurrentHp; // 현재 HP를 가져온다.

            if (currentHp < previousHp && !enemyHealth.IsDead) // HP가 감소했지만 아직 죽지 않았다면
            {
                bool isCastingSegmentCut = segmentCutCaster != null && segmentCutCaster.IsCasting; // 현재 절단 마법 시전 중인지 확인한다.
                bool isSlowZoneThrowing = IsSlowZoneThrowing(); // 현재 장판 투척 준비 또는 Throw 애니메이션 중인지 확인한다.

                if (!isCastingSegmentCut && !isSlowZoneThrowing) // 절단 마법이나 장판 투척 중이 아닐 때만
                {
                    PlayHit(); // 피격 애니메이션을 실행한다.
                }
            }

            previousHp = currentHp; // 피격 애니메이션을 생략해도 현재 HP로 갱신하여 나중에 Hit가 뒤늦게 실행되지 않게 한다.
        }

        private bool IsSlowZoneThrowing() // 장판 투척 준비 또는 Throw 애니메이션 중인지 확인하는 함수
        {
            if (slowZoneThrower != null && slowZoneThrower.IsPreparingThrow) // 실제 투사체 발사를 기다리는 중이라면
            {
                return true; // 장판 투척 중으로 판단한다.
            }

            if (slowZoneThrowerAnimatorBridge != null && slowZoneThrowerAnimatorBridge.IsThrowing) // Throw 애니메이션 잠금 시간이 남아 있다면
            {
                return true; // 장판 투척 중으로 판단한다.
            }

            return false; // 장판 투척 중이 아니다.
        }

        private bool CanUseAnimator() // Animator에 Parameter를 보내도 되는지 확인하는 함수
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
    }
}