using UnityEngine;

namespace TeamProject01.Gameplay
{
    [RequireComponent(typeof(EnemyJump))]
    public sealed class EnemyJumpAnimatorBridge : MonoBehaviour
    {
        // Animator Controller의 IsJumping Bool Parameter를 빠르게 찾기 위한 Hash 값
        public static readonly int IsJumpingParameter = Animator.StringToHash("IsJumping");

        [Header("Animator")]
        [SerializeField] private Animator animator; // 점프 애니메이션을 재생할 자식 모델의 Animator

        private EnemyJump enemyJump; // 같은 GameObject에 붙은 실제 점프 기능 Script

        private void Awake()
        {
            enemyJump = GetComponent<EnemyJump>(); // 같은 GameObject의 EnemyJump를 저장한다.

            if (animator == null) // Inspector에서 Animator가 연결되지 않았다면
            {
                animator = GetComponentInChildren<Animator>(true); // 자식 오브젝트에서 Animator를 자동으로 찾는다.
            }
        }

        private void OnEnable()
        {
            UpdateJumpAnimation(); // 활성화될 때 현재 점프 상태를 Animator에 즉시 반영한다.
        }

        private void Update()
        {
            UpdateJumpAnimation(); // 매 프레임 실제 점프 상태를 Animator에 전달한다.
        }

        private void OnDisable()
        {
            if (!CanControlAnimator()) // Animator를 안전하게 제어할 수 없다면
            {
                return; // Parameter를 변경하지 않고 종료한다.
            }

            animator.SetBool(IsJumpingParameter, false); // 비활성화될 때 점프 상태가 남지 않도록 해제한다.
        }

        private void UpdateJumpAnimation()
        {
            if (!CanControlAnimator() || enemyJump == null) // 필요한 Component가 준비되지 않았다면
            {
                return; // 점프 애니메이션을 갱신하지 않는다.
            }

            animator.SetBool(IsJumpingParameter, enemyJump.IsJumping); // EnemyJump의 실제 점프 상태를 Animator에 전달한다.
        }

        private bool CanControlAnimator()
        {
            return animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null; // Animator와 Controller가 정상적으로 작동 중인지 확인한다.
        }
    }
}