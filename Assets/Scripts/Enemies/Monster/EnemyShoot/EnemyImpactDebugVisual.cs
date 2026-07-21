using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyImpactDebugVisual : MonoBehaviour // VFX가 없을 때 사용할 임시 임팩트 표시 + 즉시 피해 처리
    {
        [Header("Attack")]
        [Min(0)]
        [SerializeField] private int damage = 1; // 이 임팩트 Prefab이 가진 기본 피해량

        [Header("Visual")]
        [Min(0.1f)]
        [SerializeField] private float lifeTime = 0.7f; // 임팩트 오브젝트가 유지될 시간

        [Min(0.0f)]
        [SerializeField] private float startScaleMultiplier = 0.3f; // 처음 생성될 때 크기 배율

        [Min(0.0f)]
        [SerializeField] private float endScaleMultiplier = 1.3f; // 사라지기 직전 크기 배율

        [Min(0.01f)]
        [SerializeField] private float blinkInterval = 0.08f; // 깜빡이는 간격

        private Renderer targetRenderer; // 깜빡임을 적용할 Renderer

        private Vector3 baseScale; // Prefab 원래 크기
        private float lifeTimer; // 생성 후 지난 시간

        private void Awake()
        {
            baseScale = transform.localScale; // Prefab의 원래 크기를 저장한다.

            if (targetRenderer == null) // Renderer 참조가 아직 없다면
            {
                targetRenderer = GetComponentInChildren<Renderer>(); // 자식까지 포함해서 Renderer를 찾는다.
            }

            transform.localScale = baseScale * startScaleMultiplier; // 처음에는 작게 보이게 한다.
        }

        private void Update()
        {
            lifeTimer += Time.deltaTime; // 지난 시간만큼 유지 시간을 증가시킨다.

            float progress = lifeTimer / lifeTime; // 현재 진행률을 계산한다.
            progress = Mathf.Clamp01(progress); // 진행률을 0~1 사이로 제한한다.

            float scaleMultiplier = Mathf.Lerp(startScaleMultiplier, endScaleMultiplier, progress); // 시작 크기에서 끝 크기까지 점점 커지게 한다.
            transform.localScale = baseScale * scaleMultiplier; // 계산된 크기를 적용한다.

            ApplyBlink(); // 깜빡임을 적용한다.

            if (lifeTimer >= lifeTime) // 유지 시간이 끝났다면
            {
                Destroy(gameObject); // 임팩트 오브젝트를 제거한다.
            }
        }

        public void Configure(float newLifeTime) // 외부에서 유지 시간만 설정하는 함수
        {
            lifeTime = Mathf.Max(0.1f, newLifeTime); // 유지 시간을 최소 0.1초로 제한해서 저장한다.
            lifeTimer = 0.0f; // 유지 시간 타이머를 초기화한다.
        }

        public void Configure(Transform target, float newLifeTime) // 외부에서 공격 대상과 유지 시간을 설정하는 함수
        {
            Configure(target, newLifeTime, 1.0f); // 공격력 버프 배율 없이 기본 피해량으로 처리한다.
        }

        public void Configure(Transform target, float newLifeTime, float attackPowerMultiplier) // 외부에서 공격 대상, 유지 시간, 공격력 버프 배율을 설정하는 함수
        {
            Configure(newLifeTime); // 기존 유지 시간 설정 함수를 재사용한다.

            if (target == null) // 공격 대상이 없다면
            {
                return; // 피해를 적용하지 않는다.
            }

            attackPowerMultiplier = Mathf.Max(0.0f, attackPowerMultiplier); // 공격력 배율이 음수가 되지 않게 제한한다.

            int finalDamage = Mathf.Max(0, Mathf.RoundToInt(damage * attackPowerMultiplier)); // Prefab 기본 damage에 공격력 버프 배율을 적용한다.

            NexusController.TryApplyDamage(target, finalDamage); // 최종 피해량으로 Nexus에 피해를 준다.
        }

        private void ApplyBlink() // Renderer를 켜고 끄며 번쩍이는 느낌을 주는 함수
        {
            if (targetRenderer == null) // Renderer가 없다면
            {
                return; // 깜빡임을 적용할 수 없으므로 종료한다.
            }

            if (blinkInterval <= 0.0f) // 깜빡임 간격이 잘못되었다면
            {
                targetRenderer.enabled = true; // Renderer를 켜둔다.
                return; // 종료한다.
            }

            int blinkIndex = Mathf.FloorToInt(lifeTimer / blinkInterval); // 현재 시간이 몇 번째 깜빡임 구간인지 계산한다.
            bool visible = blinkIndex % 2 == 0; // 짝수 구간이면 보이게 한다.

            targetRenderer.enabled = visible; // Renderer 표시 상태를 적용한다.
        }
    }
}