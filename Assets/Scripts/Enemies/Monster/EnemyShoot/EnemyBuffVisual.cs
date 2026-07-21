using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyBuffVisual : MonoBehaviour //받은 버프 종류에 따라 구분
    {
        private EnemyBuffReceiver buffReceiver;//버프 상태를 읽을 EnemyBuffReceiver Component
        private EnemyHealth enemyHealth; // 전찬우수정 -0630 사망 즉시 오라를 끄기 위한 체력 참조
        private EnemyController enemyController; // 전찬우수정 -0630 사망 애니메이션 중 오라 재점등 방지

        [SerializeField] private GameObject attackPowerAura; //공격력 버프 표시
        [SerializeField] private GameObject moveSpeedAura; //이동속도 버프 표시
        [SerializeField] private GameObject attackSpeedAura; //공격속도 버프 표시

        private EnemyBuffType visibleBuffType = EnemyBuffType.None; //현재 적용중인 버프 표시(None)

        private void Awake()
        {
            if(buffReceiver == null) //현재 buffReceiver가 연결되어 있지 않다면
            {
                buffReceiver = GetComponent<EnemyBuffReceiver>(); //연결한다.
            }

            enemyHealth = GetComponent<EnemyHealth>(); // 전찬우수정 -0630 사망 이벤트 구독용 체력 컴포넌트 연결
            enemyController = GetComponent<EnemyController>(); // 전찬우수정 -0630 컨트롤러 사망 플래그 확인
            ClearVisual(); //처음 시작할 떄 모든 아우라를 끈다.
        }

        private void OnEnable()
        {
            SubscribeHealth(); // 전찬우수정 -0630 HP 사망 이벤트 발생 시 즉시 오라 제거
        }

        private void Update()
        {
            if (IsOwnerDead()) // 전찬우수정 -0630 사망 애니메이션 동안 오라가 다시 켜지지 않게 방어
            {
                ClearVisual();
                return;
            }

            EnemyBuffType targetBuffType = EnemyBuffType.None; //이번 프레임에 표시할 버프 종류를 None로 저장한다.

            if (buffReceiver != null && buffReceiver.HasActiveBuff) //현재 버프가 적용중이라면
            {
                targetBuffType = buffReceiver.ActiveBuffType; //현재 아우라 표시할 대상으로 저장한다.
            }

            if(visibleBuffType == targetBuffType) //같은 아우라 버프가 표시 중이라면
            {
                return; //종료한다.
            }

            ApplyVisual(targetBuffType); //버프 종류에 맞게 아우라 표시를 갱신한다.
        }

        private void OnDisable()
        {
            UnsubscribeHealth(); // 전찬우수정 -0630 파괴/비활성화 시 이벤트 참조 해제
            ClearVisual(); //아우라를 끈다.
        }

        private void SubscribeHealth()
        {
            if (enemyHealth == null)
            {
                enemyHealth = GetComponent<EnemyHealth>(); // 전찬우수정 -0630 런타임 추가/재활성화 대응
            }

            if (enemyHealth != null)
            {
                enemyHealth.HealthChanged -= HandleHealthChanged;
                enemyHealth.HealthChanged += HandleHealthChanged;
            }
        }

        private void UnsubscribeHealth()
        {
            if (enemyHealth != null)
            {
                enemyHealth.HealthChanged -= HandleHealthChanged;
            }
        }

        private void HandleHealthChanged(EnemyHealth changedHealth)
        {
            if (changedHealth != null && changedHealth.IsDead) // 전찬우수정 -0630 HP 사망 즉시 버프 오라 제거
            {
                ClearVisual();
            }
        }

        private bool IsOwnerDead()
        {
            if (enemyController == null)
            {
                enemyController = GetComponent<EnemyController>(); // 전찬우수정 -0630 지연 사망 제거 대기 상태 확인
            }

            if (enemyHealth == null)
            {
                enemyHealth = GetComponent<EnemyHealth>(); // 전찬우수정 -0630 체력 사망 상태 확인
            }

            return (enemyController != null && enemyController.IsDead) || (enemyHealth != null && enemyHealth.IsDead);
        }

        public void ApplyVisual(EnemyBuffType buffType) //아우라 효과를 켜는 함수
        {
            visibleBuffType = buffType; //현재 적용중인 버프 상태에 맞게 아우라를 적용한다.

            SetAuraActive(attackPowerAura, buffType == EnemyBuffType.AttackPower); //공격 증가 아우라를 켠다.
            SetAuraActive(moveSpeedAura, buffType == EnemyBuffType.MoveSpeed); //이동속도 증가 아우라를 켠다.
            SetAuraActive(attackSpeedAura, buffType == EnemyBuffType.AttackSpeed); //공격속도 증가 아우라를 켠다.
        }

        public void ClearVisual() //아우라 효과를 끄는 함수
        {
            visibleBuffType = EnemyBuffType.None; //현재 적용중인 버프 상태를 None로 한다.

            SetAuraActive(attackPowerAura, false); //공격 증가 아우라를 종료한다.
            SetAuraActive(moveSpeedAura, false); //이동속도 증가 아우라를 종료한다.
            SetAuraActive(attackSpeedAura, false); //공격속도 증가 아우라를 종료한다.
        }

        private void SetAuraActive(GameObject auraObject, bool active) //GameObject 활성화 상태를 바꾸는 함수
        {
            if(auraObject == null) // auraObject가 연결되어 있지 않다면
            {
                return; //종료한다.
            }

            if(auraObject.activeSelf == active) //auraObject가 활성화 상태라면
            {
                return; //종료한다.
            }

            auraObject.SetActive(active); //auraObject GameObject를 켜거나 끈다.
        }
    }    
}
