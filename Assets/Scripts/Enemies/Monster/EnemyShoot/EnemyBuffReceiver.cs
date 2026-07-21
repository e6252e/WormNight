using UnityEngine;

namespace TeamProject01.Gameplay
{
    public class EnemyBuffReceiver : MonoBehaviour//몬스터 버프 상태 관리
    {        
        private EnemyHealth enemyHealth; // 전찬우수정 -0630 사망 시 버프 상태를 즉시 지우기 위한 체력 참조
        private EnemyController enemyController; // 전찬우수정 -0630 사망 애니메이션 대기 중인 컨트롤러 상태 확인
        private EnemyBuffType activeBuffType = EnemyBuffType.None; //현재 몬스터 적용 버프는 없음

        
        private float activeBuffMultiplier = 1.0f; //현재 적용 중인 버프 배율

        
        private float remainingBuffTime; //현재 버프 유지 시간

        public EnemyBuffType ActiveBuffType//현재 버프 종류를 읽기 위한 property
        {
            get
            {
                return activeBuffType; //현재 버프 종류를 반환한다.
            }
        }

        public bool HasActiveBuff //현재 버프가 적용 중인지 확인하는 property
        {
            get
            {
                return activeBuffType != EnemyBuffType.None && remainingBuffTime > 0.0f;//현재 버프 종류가 None이 아니고, 버프 유지 시간이 남아 있으면 true를 반환한다.
            }
        }

        public float ActiveBuffMultiplier//현재 적용 버프를 읽기 위한 property
        {
            get
            {
                return activeBuffMultiplier; //현재 적용 중인 버프를 반환한다.
            }
        }

        private void Awake()
        {
            enemyHealth = GetComponent<EnemyHealth>(); // 전찬우수정 -0630 사망 이벤트 구독용 체력 컴포넌트 연결
            enemyController = GetComponent<EnemyController>(); // 전찬우수정 -0630 보상 처리 후 사망 플래그 확인
        }

        private void OnEnable()
        {
            SubscribeHealth(); // 전찬우수정 -0630 죽는 즉시 버프 상태를 제거하도록 이벤트 연결
        }

        private void Update()
        {
            if (IsOwnerDead()) // 전찬우수정 -0630 사망 애니메이션 중 버프가 남아있지 않게 방어
            {
                ClearBuff();
                return;
            }

            if (!HasActiveBuff) //현재 적용 중인 버프가 없다면
            {
                return; // 계산하지 않고 종료한다.
            }

            remainingBuffTime -= Time.deltaTime; //지난 시간만큼 버프 유지 시간을 감소한다.

            if(remainingBuffTime <= 0) //버프 유지시간이 끝나면
            {
                ClearBuff(); //버프를 제거한다.
            }
        }

        private void OnDisable()
        {
            UnsubscribeHealth(); // 전찬우수정 -0630 파괴/비활성화 시 이벤트 참조 해제
            ClearBuff(); //버프가 비활성화 상태면 버프 상태를 초기화 한다.
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
            if (changedHealth != null && changedHealth.IsDead) // 전찬우수정 -0630 HP 사망 즉시 버프 상태 제거
            {
                ClearBuff();
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

        public void ApplyBuff(EnemyBuffType newBuffType, float newBuffMultiplier, float newBuffDuration) //외부에서 몬스터에게 버프를 적용하는 함수
        {
            if (IsOwnerDead()) // 전찬우수정 -0630 사망 처리된 몬스터에게 버프가 다시 들어오는 것 방지
            {
                ClearBuff();
                return;
            }

            if(newBuffType == EnemyBuffType.None) //적용할 버프가 None라면
            {
                ClearBuff(); //현재 버프를 제거한다.
                return; // 종료한다.
            }

            if(activeBuffType == newBuffType) //같은 종류의 버프라면
            {
                remainingBuffTime = Mathf.Max(0.1f, newBuffDuration);//유지시간만 새로 갱신한다.
                return;
            }

            //이속 1초 남음        //이속 1초 남음
            //이속 다시 받음 5초   //공격속도 받음 5초
            //이속 5초 갱신        //공격속도 5초갱신
            activeBuffType = newBuffType; //현재 적용 버프 종류를 저장한다.
            activeBuffMultiplier = Mathf.Max(1.0f, newBuffMultiplier); //현재 적용할 버프 배율을 최소 1로 제한
            remainingBuffTime = Mathf.Max(0.1f, newBuffDuration); //현재 적용할 버프 유지시간을 1초로 제한.
        }

        public void ClearBuff() //버프를 제거하는 함수
        {
            activeBuffType = EnemyBuffType.None; //현재 버프종류를 None로 한다.
            activeBuffMultiplier = 1.0f; //버프 적용을 기본값으로 저장한다.
            remainingBuffTime = 0.0f; //버프 유지시간을 0초로 저장한다.
        }

        public float GetAttackPowerMultiplier() //공격력 버프 종류를 반환하는 함수
        {
            if(!HasActiveBuff) //현재 버프가 없다면
            {
                return 1.0f; //현재 버프 적용을 기본으로 반환한다.
            }

            if(activeBuffType != EnemyBuffType.AttackPower)//현재 버프가 공격력 버프 종류가 아니라면
            {
                return 1.0f; //현재 버프 적용을 기본으로 반환한다.
            }

            return activeBuffMultiplier; //현재 버프 적용가능한 상태를 반환한다.
        }

        public float GetMoveSpeedMultiplier() //이동속도 버프 종류를 반환하는 함수
        {
            if (!HasActiveBuff) //현재 버프가 없다면
            {
                return 1.0f; //현재 버프 적용을 기본으로 반환한다.
            }

            if (activeBuffType != EnemyBuffType.MoveSpeed)//현재 버프가 이동속도 버프 종류가 아니라면
            {
                return 1.0f; //현재 버프 적용을 기본으로 반환한다.
            }

            return activeBuffMultiplier; //현재 버프 적용가능한 상태를 반환한다.
        }

        public float GetAttackSpeedMultiplier() //공격속도 버프 종류를 반환하는 함수
        {
            if (!HasActiveBuff) //현재 버프가 없다면
            {
                return 1.0f; //현재 버프 적용을 기본으로 반환한다.
            }

            if (activeBuffType != EnemyBuffType.AttackSpeed)//현재 버프가 공격속도 버프 종류가 아니라면
            {
                return 1.0f; //현재 버프 적용을 기본으로 반환한다.
            }

            return activeBuffMultiplier; //현재 버프 적용가능한 상태를 반환한다.
        }
    }
}
