using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyMeleeAnimatorBridge : MonoBehaviour
    {
        public static readonly int IsMovingParameter = Animator.StringToHash("IsMoving");
        public static readonly int AttackParameter = Animator.StringToHash("Attack");
        public static readonly int HitParameter = Animator.StringToHash("Hit");

        public static readonly int IsAttackingParameter = Animator.StringToHash("IsAttacking");

        private static readonly int AttackState = Animator.StringToHash("Attack");

        [Header("Animator")]
        [SerializeField] private Animator animator;

        private EnemyMovement enemyMovement;
        private EnemyMeleeAttack enemyMeleeAttack;
        private EnemyHealth enemyHealth;
        private EnemySupportDebuffState supportDebuffState;
        private EnemyObstacleSummoner obstacleSummoner;

        private float previousHp;

        private bool isAttacking;

        private bool attackStateEntered;

        private void Awake()
        {
            enemyMovement = GetComponent<EnemyMovement>();
            enemyMeleeAttack = GetComponent<EnemyMeleeAttack>();
            enemyHealth = GetComponent<EnemyHealth>();
            supportDebuffState = GetComponent<EnemySupportDebuffState>();
            obstacleSummoner = GetComponent<EnemyObstacleSummoner>();

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (enemyHealth != null)
            {
                previousHp = enemyHealth.CurrentHp;
            }
        }

        private void OnEnable()
        {
            if (enemyMeleeAttack != null)
            {
                enemyMeleeAttack.AttackPerformed += PlayAttack;
            }

            if (enemyHealth != null)
            {
                previousHp = enemyHealth.CurrentHp;
            }

            isAttacking = false;

            attackStateEntered = false;

            if (animator != null)
            {
                animator.SetBool(IsAttackingParameter, false);
            }

            UpdateMovementAnimation();
        }

        private void Update()
        {
            UpdateMovementAnimation();

            UpdateAttackAnimation();

            UpdateHitAnimation();
        }

        private void OnDisable()
        {
            if (enemyMeleeAttack != null)
            {
                enemyMeleeAttack.AttackPerformed -= PlayAttack;
            }

            if (animator == null)
            {
                return;
            }

            animator.SetBool(IsMovingParameter, false);

            animator.SetBool(IsAttackingParameter, false);

            animator.ResetTrigger(AttackParameter);
            animator.ResetTrigger(HitParameter);

            isAttacking = false;

            attackStateEntered = false;
        }

        public void PlayAttack()
        {
            if (animator == null)
            {
                return;
            }

            isAttacking = true;

            attackStateEntered = false;

            animator.SetBool(IsAttackingParameter, true);

            animator.ResetTrigger(AttackParameter);
            animator.SetTrigger(AttackParameter);
        }

        private void PlayHit()
        {
            if (animator == null)
            {
                return;
            }

            animator.ResetTrigger(HitParameter);
            animator.SetTrigger(HitParameter);
        }

        private void UpdateAttackAnimation()
        {
            if (animator == null)
            {
                return;
            }

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            bool currentStateIsAttack = currentState.shortNameHash == AttackState;

            bool nextStateIsAttack = false;

            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
                nextStateIsAttack = nextState.shortNameHash == AttackState;
            }

            if (currentStateIsAttack || nextStateIsAttack)
            {
                attackStateEntered = true;

                if (!isAttacking)
                {
                    isAttacking = true;
                    animator.SetBool(IsAttackingParameter, true);
                }

                return;
            }

            if (!attackStateEntered)
            {
                return;
            }

            isAttacking = false;
            attackStateEntered = false;
            animator.SetBool(IsAttackingParameter, false);
        }

        private void UpdateMovementAnimation()
        {
            if (animator == null || enemyMovement == null)
            {
                return;
            }

            bool isFrozen = supportDebuffState != null && supportDebuffState.IsFrozen;
            bool isSummoning = obstacleSummoner != null && obstacleSummoner.IsSummoning;
            bool isMoving = enemyMovement.enabled && !enemyMovement.IsInStopRange && !isFrozen && !isSummoning;

            animator.SetBool(IsMovingParameter, isMoving);
        }

        private void UpdateHitAnimation()
        {
            if (enemyHealth == null)
            {
                return;
            }

            float currentHp = enemyHealth.CurrentHp;
            bool isSummoning = obstacleSummoner != null && obstacleSummoner.IsSummoning;

            if (currentHp < previousHp && !enemyHealth.IsDead && !isAttacking && !isSummoning)
            {
                PlayHit();
            }

            previousHp = currentHp;
        }
    }
}
