using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyPortalTotemCasterAnimatorBridge : MonoBehaviour // 포탈 토템 시전 상태를 Animator에 전달하는 Script Component
    {
        private static readonly int IsPortalChannelingParameter = Animator.StringToHash("IsPortalChanneling"); // 포탈 채널링 Animator Bool

        [Header("Animator")]
        [SerializeField] private Animator animator; // 기사 모델에 붙어 있는 Animator

        private EnemyPortalTotemCaster portalTotemCaster; // 포탈 토템 생성 기능 Script Component

        private void Awake()
        {
            portalTotemCaster = GetComponent<EnemyPortalTotemCaster>(); // 같은 GameObject의 EnemyPortalTotemCaster를 찾는다.

            if (animator == null) // Inspector에서 Animator가 연결되지 않았다면
            {
                animator = GetComponentInChildren<Animator>(true); // 자식 모델에서 Animator를 자동으로 찾는다.
            }
        }

        private void OnEnable()
        {
            UpdatePortalAnimation(); // 활성화되는 순간 현재 포탈 상태를 Animator에 반영한다.
        }

        private void Update()
        {
            UpdatePortalAnimation(); // 매 프레임 포탈 생성 상태를 Animator에 전달한다.
        }

        private void OnDisable()
        {
            if (!CanUseAnimator()) // Animator를 사용할 수 없다면
            {
                return; // 초기화하지 않는다.
            }

            animator.SetBool(IsPortalChannelingParameter, false); // 비활성화될 때 포탈 채널링 애니메이션을 끈다.
        }

        private void UpdatePortalAnimation() // 현재 포탈 생성 상태를 Animator Bool에 반영한다.
        {
            if (!CanUseAnimator() || portalTotemCaster == null) // Animator나 포탈 시전 Script가 없다면
            {
                return; // 상태를 전달할 수 없다.
            }

            animator.SetBool(IsPortalChannelingParameter, portalTotemCaster.IsChanneling); // 포탈 생성 중이면 Portal State로 보내고, 끝나면 Idle로 돌아가게 한다.
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