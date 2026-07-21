using UnityEngine;

namespace TeamProject01.Gameplay
{
    [RequireComponent(typeof(EnemySlowZoneThrower))]
    public sealed class EnemySlowZoneThrowerAnimatorBridge : MonoBehaviour // 슬로우 장판 투척 애니메이션을 연결하는 Script Component
    {
        public static readonly int ThrowParameter = Animator.StringToHash("Throw"); // 장판 투척 Animator Trigger

        [Header("Animator")]
        [SerializeField] private Animator animator; // 몬스터 모델에 붙어 있는 Animator

        [Header("Throw Lock")]
        [Min(0.1f)]
        [SerializeField] private float throwLockDuration = 0.8f; // Throw 애니메이션이 재생 중이라고 판단할 시간

        private EnemySlowZoneThrower slowZoneThrower; // 장판 투척 이벤트를 받을 EnemySlowZoneThrower
        private EnemyHealth enemyHealth; // 죽은 상태인지 확인할 EnemyHealth

        private float throwLockTimer; // Throw 재실행을 막는 남은 시간

        public bool IsThrowing // 다른 Animator Bridge가 Throw 중인지 확인할 수 있게 공개한다.
        {
            get
            {
                return throwLockTimer > 0.0f; // Throw 잠금 시간이 남아 있으면 Throw 중으로 판단한다.
            }
        }

        private void Awake()
        {
            slowZoneThrower = GetComponent<EnemySlowZoneThrower>(); // 같은 GameObject의 EnemySlowZoneThrower를 찾는다.
            enemyHealth = GetComponent<EnemyHealth>(); // 같은 GameObject의 EnemyHealth를 찾는다.

            if (animator == null) // Inspector에서 Animator가 연결되지 않았다면
            {
                animator = GetComponentInChildren<Animator>(true); // 자식 모델에서 Animator를 자동으로 찾는다.
            }
        }

        private void OnEnable()
        {
            if (slowZoneThrower != null) // EnemySlowZoneThrower가 있다면
            {
                slowZoneThrower.ThrowStarted += PlayThrow; // 장판 투척이 시작될 때 PlayThrow를 실행하도록 연결한다.
            }

            throwLockTimer = 0.0f; // 활성화될 때 Throw 잠금 시간을 초기화한다.
        }

        private void Update()
        {
            if (throwLockTimer > 0.0f) // Throw 잠금 시간이 남아 있다면
            {
                throwLockTimer -= Time.deltaTime; // 지난 시간만큼 감소시킨다.
            }
        }

        private void OnDisable()
        {
            if (slowZoneThrower != null) // EnemySlowZoneThrower가 있다면
            {
                slowZoneThrower.ThrowStarted -= PlayThrow; // 비활성화될 때 장판 투척 이벤트 연결을 해제한다.
            }

            throwLockTimer = 0.0f; // 비활성화될 때 Throw 잠금 시간을 초기화한다.

            if (!CanUseAnimator()) // Animator가 Parameter를 받을 수 없는 상태라면
            {
                return; // 초기화하지 않는다.
            }

            animator.ResetTrigger(ThrowParameter); // 남아 있을 수 있는 투척 Trigger만 초기화한다.
        }

        public void PlayThrow() // 장판 투척이 실행될 때 투척 애니메이션을 재생하는 함수
        {
            if (enemyHealth != null && enemyHealth.IsDead) // 몬스터가 죽은 상태라면
            {
                return; // 죽은 몬스터는 Throw 애니메이션을 실행하지 않는다.
            }

            if (throwLockTimer > 0.0f) // 이미 Throw 애니메이션 중이라면
            {
                return; // Throw를 중복 실행하지 않는다.
            }

            if (!CanUseAnimator()) // Animator가 Parameter를 받을 수 없는 상태라면
            {
                return; // 투척 애니메이션을 실행하지 않는다.
            }

            throwLockTimer = throwLockDuration; // 일정 시간 동안 Throw 중으로 잠근다.

            animator.ResetTrigger(ThrowParameter); // 이전 Throw Trigger가 남아 있다면 초기화한다.
            animator.SetTrigger(ThrowParameter); // Throw Trigger를 실행한다.
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