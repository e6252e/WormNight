using System.Collections;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyAreaShield : MonoBehaviour // 일정 시간마다 범위 방어막을 켜고 끈다.
    {
        [Header("Shield Setting")]
        [Min(0.1f)]
        [SerializeField] private float shieldRadius = 7.5f; // 방어막 반경

        [Min(0.0f)]
        [SerializeField] private float firstShieldDelay = 3.0f; // 생성 후 첫 방어막 대기 시간

        [Min(0.1f)]
        [SerializeField] private float shieldDuration = 6.0f; // 방어막 유지 시간

        [Min(0.1f)]
        [SerializeField] private float shieldInterval = 12.0f; // 방어막 종료 후 다시 켜질 때까지의 대기 시간

        [SerializeField] private GameObject shieldVisualRoot; // 방어막 시각 오브젝트

        [Header("Shield Animation")]
        [SerializeField] private Animator shieldAnimator; // 방어막 애니메이션을 재생할 Animator
        [SerializeField] private string shieldTriggerName = "Shield"; // Animator Controller의 Shield Trigger 이름

        private Coroutine shieldCycleCoroutine;
        private int shieldTriggerHash; // Shield Trigger 이름을 Hash로 저장한다.

        public float ShieldRadius
        {
            get
            {
                return shieldRadius;
            }
        }

        public bool IsShieldActive { get; private set; } // 현재 방어막 활성 상태
        public event System.Action<EnemyAreaShield, bool> ShieldStateChanged;

        private void Awake()
        {
            shieldTriggerHash = Animator.StringToHash(shieldTriggerName); // Trigger 이름을 Hash로 변환한다.

            if (shieldAnimator == null) // Inspector에 Animator가 연결되지 않았다면
            {
                shieldAnimator = GetComponentInChildren<Animator>(); // 자식 오브젝트에서 Animator를 자동으로 찾는다.
            }
        }

        private void OnEnable()
        {
            SetShieldActive(false); // 생성 직후에는 방어막을 끈다.
            shieldCycleCoroutine = StartCoroutine(ShieldCycleRoutine());
        }

        private void OnDisable()
        {
            if (shieldCycleCoroutine != null)
            {
                StopCoroutine(shieldCycleCoroutine);
                shieldCycleCoroutine = null;
            }

            SetShieldActive(false); // 사망하거나 비활성화되면 방어막을 해제한다.
        }

        private IEnumerator ShieldCycleRoutine() // 방어막 활성과 대기를 반복한다.
        {
            yield return new WaitForSeconds(firstShieldDelay);

            while (true)
            {
                SetShieldActive(true);

                yield return new WaitForSeconds(shieldDuration);

                SetShieldActive(false);

                yield return new WaitForSeconds(shieldInterval);
            }
        }

        private void SetShieldActive(bool active) // 보호 판정과 시각 효과를 함께 변경한다.
        {
            bool changed = IsShieldActive != active;
            IsShieldActive = active;

            if (active) // 방어막이 켜지는 순간
            {
                PlayShieldAnimation(); // Shield 애니메이션을 재생한다.
            }

            if (shieldVisualRoot != null)
            {
                shieldVisualRoot.SetActive(active);
            }

            if (active)
            {
                EnemyShieldRegistry.Register(transform, shieldRadius);
            }
            else
            {
                EnemyShieldRegistry.Unregister(transform);
            }

            if (changed)
            {
                ShieldStateChanged?.Invoke(this, active);
            }
        }

        private void PlayShieldAnimation() // Animator에 Shield Trigger를 전달한다.
        {
            if (shieldAnimator == null) // Animator가 없으면 애니메이션을 재생하지 않는다.
            {
                return;
            }

            if (string.IsNullOrEmpty(shieldTriggerName)) // Trigger 이름이 비어 있으면 실행하지 않는다.
            {
                return;
            }

            shieldAnimator.ResetTrigger(shieldTriggerHash); // 이전 Shield Trigger 상태를 정리한다.
            shieldAnimator.SetTrigger(shieldTriggerHash); // Shield 애니메이션을 실행한다.
        }
    }
}
