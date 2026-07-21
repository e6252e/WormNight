using UnityEngine;

namespace TeamProject01.Gameplay
{
    [RequireComponent(typeof(EnemySegmentCutCaster))]
    public sealed class EnemySegmentCutCasterAnimatorBridge : MonoBehaviour
    {
        private static readonly int CastParameter = Animator.StringToHash("Cast"); // 절단 마법 시전 준비 Trigger의 해시값이다.
        private static readonly int FireParameter = Animator.StringToHash("Fire"); // 절단 투사체 발사 Trigger의 해시값이다.

        [Header("Animator")]
        [SerializeField] private Animator animator; // 절단 마법 애니메이션을 재생할 Animator이다.

        private EnemySegmentCutCaster segmentCutCaster; // 절단 마법의 시전과 발사 이벤트를 제공하는 Script Component이다.

        private void Awake()
        {
            segmentCutCaster = GetComponent<EnemySegmentCutCaster>(); // 같은 GameObject의 절단 마법 Script Component를 찾는다.

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true); // Inspector에 연결되지 않았다면 자식에서 Animator를 자동으로 찾는다.
            }
        }

        private void OnEnable()
        {
            if (segmentCutCaster == null)
            {
                return; // 절단 마법 Script Component가 없다면 이벤트를 연결하지 않는다.
            }

            segmentCutCaster.CastStarted += PlayCast; // 절단 마법 시전 시작 이벤트를 구독한다.
            segmentCutCaster.ProjectileLaunched += PlayFire; // 실제 투사체 발사 이벤트를 구독한다.
        }

        private void OnDisable()
        {
            if (segmentCutCaster != null)
            {
                segmentCutCaster.CastStarted -= PlayCast; // 비활성화될 때 시전 시작 이벤트 구독을 해제한다.
                segmentCutCaster.ProjectileLaunched -= PlayFire; // 비활성화될 때 투사체 발사 이벤트 구독을 해제한다.
            }

            if (animator == null)
            {
                return; // Animator가 없다면 Trigger 초기화를 하지 않는다.
            }

            animator.ResetTrigger(CastParameter); // 남아 있을 수 있는 Cast Trigger를 초기화한다.
            animator.ResetTrigger(FireParameter); // 남아 있을 수 있는 Fire Trigger를 초기화한다.
        }

        private void PlayCast()
        {
            if (animator == null)
            {
                return; // Animator가 없다면 시전 애니메이션을 실행하지 않는다.
            }

            animator.ResetTrigger(CastParameter); // 이전 프레임에 남아 있을 수 있는 Cast Trigger를 초기화한다.
            animator.SetTrigger(CastParameter); // 절단 마법 준비 애니메이션을 실행한다.
        }

        private void PlayFire()
        {
            if (animator == null)
            {
                return; // Animator가 없다면 발사 애니메이션을 실행하지 않는다.
            }

            animator.ResetTrigger(FireParameter); // 이전 프레임에 남아 있을 수 있는 Fire Trigger를 초기화한다.
            animator.SetTrigger(FireParameter); // 절단 투사체 발사 애니메이션을 실행한다.
        }
    }
}