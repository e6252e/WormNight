using UnityEngine;

namespace TeamProject01.Gameplay
{
    [RequireComponent(typeof(EnemySuicideCharger))]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemySuicideAnimatorBridge : MonoBehaviour
    {
        private static readonly int IsMovingParameter = Animator.StringToHash("IsMoving"); // 이동 Bool Parameter의 Hash 값을 저장한다.
        private static readonly int IsChargingParameter = Animator.StringToHash("IsCharging"); // 자폭 준비 Bool Parameter의 Hash 값을 저장한다.
        private static readonly int HitParameter = Animator.StringToHash("Hit"); // 피격 Trigger Parameter의 Hash 값을 저장한다.

        [Header("Animator")]
        [SerializeField] private Animator animator; // 자폭 몬스터 모델의 Animator를 연결한다.

        [Header("Charge VFX")]
        [SerializeField] private GameObject chargeVfxRoot; // 자폭 준비 중 표시할 충전 VFX의 부모 오브젝트를 연결한다.

        [Header("Charge End VFX")]
        [SerializeField] private GameObject chargeEndVfxPrefab; // 자폭 완료 순간 생성할 Step3 End VFX Prefab을 연결한다.

        [Min(0.01f)]
        [SerializeField] private float chargeEndVfxScale = 1.0f; // 마지막 폭발 VFX의 기본 크기를 설정한다.

        [Min(0.1f)]
        [SerializeField] private float chargeEndVfxImpactMultiplier = 1.5f; // 마지막 폭발의 시각적 크기를 추가로 강조한다.

        [Min(0.1f)]
        [SerializeField] private float chargeEndVfxLifetime = 3.0f; // 생성된 자폭 완료 VFX가 제거되기까지의 시간이다.

        private EnemySuicideCharger suicideCharger; // 자폭 몬스터의 이동 및 자폭 준비 상태를 가져온다.
        private EnemyHealth enemyHealth; // 현재 HP와 사망 여부를 확인한다.

        private Vector3 initialMonsterWorldScale; // 충전 전 몬스터의 월드 크기를 저장한다.
        private float previousHp; // 직전 프레임의 HP를 저장한다.

        private void Awake()
        {
            suicideCharger = GetComponent<EnemySuicideCharger>(); // 같은 GameObject에서 자폭 행동 Script를 가져온다.
            enemyHealth = GetComponent<EnemyHealth>(); // 같은 GameObject에서 체력 Script를 가져온다.
            initialMonsterWorldScale = transform.lossyScale; // 자폭 충전 전 몬스터의 월드 크기를 저장한다.

            if (animator == null) // Inspector에서 Animator가 연결되지 않았다면
            {
                animator = GetComponentInChildren<Animator>(true); // 자식 모델에서 Animator를 자동으로 찾는다.
            }

            if (enemyHealth != null) // EnemyHealth를 정상적으로 찾았다면
            {
                previousHp = enemyHealth.CurrentHp; // 현재 HP를 최초 비교값으로 저장한다.
            }

            SetChargeVfxActive(false); // 자폭 준비 전에는 충전 VFX를 숨긴다.
        }

        private void OnEnable()
        {
            if (suicideCharger != null) // 자폭 행동 Script가 있다면
            {
                suicideCharger.Exploded -= PlayChargeEndVfx; // 중복 등록되어 있을 가능성을 제거한다.
                suicideCharger.Exploded += PlayChargeEndVfx; // 실제 자폭 순간에 완료 VFX를 실행하도록 이벤트를 연결한다.
            }

            if (enemyHealth != null) // EnemyHealth가 있다면
            {
                previousHp = enemyHealth.CurrentHp; // 다시 활성화될 때 HP 비교값을 현재 HP로 초기화한다.
            }

            UpdateAnimatorState(); // 활성화되는 순간 현재 행동 상태를 Animator에 반영한다.
            UpdateChargeVfxState(); // 활성화되는 순간 현재 충전 상태를 VFX에 반영한다.
        }

        private void Update()
        {
            UpdateAnimatorState(); // 매 프레임 이동 및 자폭 준비 상태를 Animator에 전달한다.
            UpdateChargeVfxState(); // 매 프레임 자폭 준비 상태에 맞춰 충전 VFX를 켜거나 끈다.
            UpdateHitAnimation(); // 매 프레임 HP 감소 여부를 확인한다.
        }

        private void OnDisable()
        {
            if (suicideCharger != null) // 자폭 행동 Script가 있다면
            {
                suicideCharger.Exploded -= PlayChargeEndVfx; // 비활성화될 때 폭발 이벤트 연결을 해제한다.
            }

            SetChargeVfxActive(false); // 비활성화될 때 충전 VFX가 남지 않도록 끈다.

            if (animator == null) // Animator가 없다면
            {
                return; // 초기화할 Animator가 없으므로 종료한다.
            }

            if (!animator.isActiveAndEnabled) // Animator가 비활성화되어 있다면
            {
                return; // 비활성화된 Animator에는 Parameter를 전달하지 않는다.
            }

            if (animator.runtimeAnimatorController == null) // Animator Controller가 없다면
            {
                return; // Parameter를 처리할 Controller가 없으므로 종료한다.
            }

            animator.SetBool(IsMovingParameter, false); // 비활성화될 때 이동 상태를 해제한다.
            animator.SetBool(IsChargingParameter, false); // 비활성화될 때 자폭 준비 상태를 해제한다.
            animator.ResetTrigger(HitParameter); // 남아 있을 수 있는 피격 Trigger를 초기화한다.
        }

        private void UpdateAnimatorState()
        {
            if (animator == null || suicideCharger == null) // Animator 또는 자폭 행동 Script가 없다면
            {
                return; // 행동 상태를 전달할 수 없으므로 종료한다.
            }

            if (!animator.isActiveAndEnabled) // Animator가 비활성화되어 있다면
            {
                return; // 비활성화된 Animator에는 값을 전달하지 않는다.
            }

            if (animator.runtimeAnimatorController == null) // Animator Controller가 없다면
            {
                return; // Parameter를 처리할 수 없으므로 종료한다.
            }

            bool isCharging = suicideCharger.IsCharging; // 현재 자폭 준비 상태를 가져온다.

            animator.SetBool(IsMovingParameter, suicideCharger.IsMoving); // 현재 돌진 이동 여부를 IsMoving에 전달한다.
            animator.SetBool(IsChargingParameter, isCharging); // 현재 자폭 준비 여부를 IsCharging에 전달한다.

            if (isCharging) // 자폭 준비 중이라면
            {
                animator.ResetTrigger(HitParameter); // 남아 있는 피격 Trigger를 제거해 Charge 애니메이션이 끊기지 않게 한다.
            }
        }

        private void UpdateChargeVfxState()
        {
            if (suicideCharger == null) // 자폭 행동 Script가 없다면
            {
                SetChargeVfxActive(false); // 충전 상태를 확인할 수 없으므로 VFX를 끈다.
                return; // 더 이상 처리하지 않는다.
            }

            SetChargeVfxActive(suicideCharger.IsCharging); // 자폭 준비 중일 때만 충전 VFX를 활성화한다.
        }

        private void SetChargeVfxActive(bool active)
        {
            if (chargeVfxRoot == null) // 충전 VFX가 연결되지 않았다면
            {
                return; // 활성화 상태를 변경할 수 없으므로 종료한다.
            }

            if (chargeVfxRoot.activeSelf == active) // 이미 원하는 활성화 상태라면
            {
                return; // 같은 값을 반복해서 적용하지 않는다.
            }

            chargeVfxRoot.SetActive(active); // 충전 상태에 맞춰 VFX 루트를 켜거나 끈다.
        }

        private void PlayChargeEndVfx()
        {
            SetChargeVfxActive(false); // 폭발 순간에는 충전 중 VFX를 먼저 끈다.

            if (chargeEndVfxPrefab == null) // 완료 VFX Prefab이 연결되지 않았다면
            {
                return; // 생성할 VFX가 없으므로 종료한다.
            }

            Transform runtimeRoot = MonsterRuntimeRoot.GetRootOrFallback(transform.parent); // 몬스터가 제거되어도 VFX가 남도록 Monsters 루트를 가져온다.

            GameObject chargeEndVfx = Instantiate(chargeEndVfxPrefab, transform.position, Quaternion.identity, runtimeRoot); // 현재 자폭 위치에 완료 VFX를 별도로 생성한다.

            SetParticleScalingModeToHierarchy(chargeEndVfx); // Transform 크기가 모든 Particle System에 적용되도록 설정한다.

            float chargeGrowthMultiplier = GetChargeGrowthMultiplier(); // 충전 전과 폭발 직전 크기를 비교해 성장 배율을 계산한다.
            Vector3 prefabScale = chargeEndVfxPrefab.transform.localScale; // 원본 Step3 End Prefab의 기본 크기를 가져온다.
            float finalScaleMultiplier = chargeEndVfxScale * chargeGrowthMultiplier * chargeEndVfxImpactMultiplier; // 기본 크기와 성장 배율, 폭발 강조 배율을 합쳐 최종 배율을 계산한다.

            chargeEndVfx.transform.localScale = prefabScale * finalScaleMultiplier; // 계산한 최종 크기를 폭발 VFX에 적용한다.
            chargeEndVfx.SetActive(true); // 연결된 Prefab이 비활성화 상태여도 생성 직후 재생되도록 활성화한다.

            Destroy(chargeEndVfx, chargeEndVfxLifetime); // 재생 시간이 끝난 뒤 생성된 VFX만 제거한다.
        }

        private float GetChargeGrowthMultiplier()
        {
            float initialScale = GetLargestScale(initialMonsterWorldScale); // 충전 전 몬스터의 가장 큰 월드 축 크기를 가져온다.
            float currentScale = GetLargestScale(transform.lossyScale); // 폭발 직전 몬스터의 가장 큰 월드 축 크기를 가져온다.

            if (initialScale <= 0.0001f) // 충전 전 크기가 0에 가까워 배율을 계산할 수 없다면
            {
                return 1.0f; // 기본 배율을 반환한다.
            }

            return Mathf.Max(1.0f, currentScale / initialScale); // 몬스터가 실제로 커진 비율을 반환한다.
        }

        private float GetLargestScale(Vector3 scale)
        {
            float largestScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)); // 세 축 중 가장 큰 절댓값을 계산한다.
            return largestScale; // 계산된 크기를 반환한다.
        }

        private void SetParticleScalingModeToHierarchy(GameObject vfxRoot)
        {
            if (vfxRoot == null) // 생성된 VFX가 없다면
            {
                return; // Particle System을 변경할 수 없으므로 종료한다.
            }

            ParticleSystem[] particleSystems = vfxRoot.GetComponentsInChildren<ParticleSystem>(true); // 폭발 VFX 아래의 모든 Particle System을 가져온다.

            for (int i = 0; i < particleSystems.Length; i++) // 모든 Particle System을 순회한다.
            {
                if (particleSystems[i] == null) // 현재 Particle System이 없다면
                {
                    continue; // 다음 Particle System으로 넘어간다.
                }

                ParticleSystem.MainModule mainModule = particleSystems[i].main; // 현재 Particle System의 Main Module을 가져온다.
                mainModule.scalingMode = ParticleSystemScalingMode.Hierarchy; // 부모 Transform 크기가 입자 전체에 적용되도록 설정한다.
            }
        }

        private void UpdateHitAnimation()
        {
            if (enemyHealth == null || suicideCharger == null) // EnemyHealth 또는 자폭 행동 Script가 없다면
            {
                return; // 피격 상태를 확인할 수 없으므로 종료한다.
            }

            float currentHp = enemyHealth.CurrentHp; // 현재 HP를 가져온다.

            if (currentHp < previousHp && !enemyHealth.IsDead && !suicideCharger.IsCharging) // HP가 감소했고 살아 있으며 자폭 준비 중이 아니라면
            {
                PlayHit(); // 피격 애니메이션을 실행한다.
            }

            previousHp = currentHp; // 현재 HP를 다음 프레임 비교값으로 저장한다.
        }

        private void PlayHit()
        {
            if (animator == null) // Animator가 없다면
            {
                return; // 피격 애니메이션을 실행할 수 없으므로 종료한다.
            }

            if (!animator.isActiveAndEnabled) // Animator가 비활성화되어 있다면
            {
                return; // Trigger를 전달하지 않는다.
            }

            if (animator.runtimeAnimatorController == null) // Animator Controller가 없다면
            {
                return; // Trigger를 처리할 수 없으므로 종료한다.
            }

            animator.ResetTrigger(HitParameter); // 이전 피격 Trigger가 남아 있다면 초기화한다.
            animator.SetTrigger(HitParameter); // 새로운 피격 Trigger를 실행한다.
        }
    }
}