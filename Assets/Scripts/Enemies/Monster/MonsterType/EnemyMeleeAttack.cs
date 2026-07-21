using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyMeleeAttack : MonoBehaviour // 근거리 몬스터
    {
        private Transform nexus; // 공격 타겟이 되는 Nexus Transform

        [Min(0)]
        [SerializeField] private int attackDamage = 1; // 피해량

        [Min(0.1f)]
        [SerializeField] private float attackRange = 1.6f; // 공격할 수 있는 거리

        [Min(0.1f)]
        [SerializeField] private float attackDelay = 1.0f; // 공격 사이의 대기 시간, 공격속도 역할

        public event System.Action AttackPerformed; // 공격 애니메이션 Bridge에 공격 시작을 알려주는 이벤트

        public float AttackRange // EnemyMovement가 근거리 공격 사거리를 읽기 위한 property
        {
            get
            {
                return attackRange * GetHatchlingAttackRangeMultiplier(); // 성장형 몬스터라면 성장 사거리 배율까지 반영한다.
            }
        }

        private float attackTimer; // 다음 공격까지 남은 시간을 저장하는 변수

        private EnemyBuffReceiver buffReceiver; // 같은 GameObject에 붙은 버프 상태 Script Component 참조
        private EnemyHatchlingGrowth hatchlingGrowth; // 같은 GameObject에 붙은 해츨링 성장 Script Component 참조

        private void Awake()
        {
            buffReceiver = GetComponent<EnemyBuffReceiver>(); // 같은 GameObject에 붙은 EnemyBuffReceiver Script Component를 찾는다.
            hatchlingGrowth = GetComponent<EnemyHatchlingGrowth>(); // 같은 GameObject에 붙은 EnemyHatchlingGrowth Script Component를 찾는다.

            if (nexus == null) // Inspector에서 Nexus가 연결되지 않았다면
            {
                GameObject nexusObject = GameObject.Find("Nexus_Core"); // 씬에서 이름이 Nexus_Core인 GameObject를 찾는다.
                nexus = nexusObject != null ? nexusObject.transform : null; // 찾았다면 Transform을 저장하고, 못 찾았다면 null로 둔다.
            }
        }

        private void Update()
        {
            if (nexus == null) // 공격 대상이 없다면
            {
                return; // 공격하지 않고 종료한다.
            }

            attackTimer -= Time.deltaTime; // 지난 시간만큼 공격 대기 시간을 줄인다.

            if (attackTimer > 0f) // 아직 공격 대기 시간이 남아 있다면
            {
                return; // 이번 프레임에는 공격하지 않는다.
            }

            Vector3 offset = nexus.position - transform.position; // 현재 몬스터 위치에서 Nexus까지의 방향과 거리 벡터를 구한다.
            offset.y = 0f; // 3D 평면 거리만 사용할 것이므로 높이 차이는 제거한다.

            float finalAttackRange = AttackRange; // 성장형 몬스터라면 성장 사거리 배율까지 반영한 최종 공격 사거리를 가져온다.

            if (offset.sqrMagnitude > finalAttackRange * finalAttackRange) // Nexus가 최종 공격 사거리 밖이라면
            {
                return; // 공격하지 않고 종료한다.
            }

            float attackPowerMultiplier = 1.0f; // 기본 공격력 버프 배율
            float attackSpeedMultiplier = 1.0f; // 기본 공격속도 버프 배율

            if (buffReceiver != null) // 버프 상태 Script Component가 있다면
            {
                attackPowerMultiplier = buffReceiver.GetAttackPowerMultiplier(); // 현재 공격력 버프 배율을 가져온다.
                attackSpeedMultiplier = buffReceiver.GetAttackSpeedMultiplier(); // 현재 공격속도 버프 배율을 가져온다.
            }

            float hatchlingAttackPowerMultiplier = 1.0f; // 기본 몬스터 성장 공격력 배율
            float hatchlingAttackSpeedMultiplier = 1.0f; // 기본 몬스터 성장 공격속도 배율

            if (hatchlingGrowth != null) // 몬스터 성장 Script Component가 있다면
            {
                hatchlingAttackPowerMultiplier = hatchlingGrowth.AttackPowerMultiplier; // 현재 몬스터 성장 공격력 배율을 가져온다.
                hatchlingAttackSpeedMultiplier = hatchlingGrowth.AttackSpeedMultiplier; // 현재 몬스터 성장 공격속도 배율을 가져온다.
            }

            int finalAttackDamage = Mathf.Max(0, Mathf.RoundToInt(attackDamage * attackPowerMultiplier * hatchlingAttackPowerMultiplier)); // 버프와 성장 배율을 적용한 최종 피해량을 계산한다.
            float finalAttackDelay = Mathf.Max(0.01f, attackDelay / attackSpeedMultiplier / hatchlingAttackSpeedMultiplier); // 버프와 성장 배율을 적용한 최종 공격 대기 시간을 계산한다.

            FaceNexus(); // 공격 애니메이션이 시작되기 전에 Nexus 방향을 바라보게 한다.

            AttackPerformed?.Invoke(); // 공격 애니메이션 Bridge에 공격 시작을 알린다.
            NexusController.TryApplyDamage(nexus, finalAttackDamage); // 최종 피해량을 Nexus에 요청한다.
            attackTimer = finalAttackDelay; // 공격 후 다음 공격까지 대기 시간을 다시 설정한다.
        }

        private void FaceNexus() // 현재 위치에서 Nexus를 바라보도록 회전시키는 함수
        {
            if (nexus == null) // 바라볼 Nexus가 없다면
            {
                return; // 회전하지 않고 종료한다.
            }

            Vector3 direction = nexus.position - transform.position; // 몬스터에서 Nexus까지의 방향을 계산한다.
            direction.y = 0.0f; // 수평 방향으로만 회전하도록 높이 차이를 제거한다.

            if (direction.sqrMagnitude <= 0.0001f) // 회전 방향을 계산할 수 없을 정도로 거리가 짧다면
            {
                return; // 잘못된 회전을 적용하지 않고 종료한다.
            }

            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up); // 몬스터가 Nexus 방향을 바라보게 한다.
        }

        private float GetHatchlingAttackRangeMultiplier() // 성장형 몬스터의 공격 사거리 배율을 가져온다.
        {
            if (hatchlingGrowth == null) // 해츨링 성장 Script가 없다면
            {
                return 1.0f; // 일반 몬스터는 기본 사거리 배율을 사용한다.
            }

            return hatchlingGrowth.AttackRangeMultiplier; // 해츨링 성장 Script의 공격 사거리 배율을 반환한다.
        }

        public void Configure(Transform nexus, int attackDamage, float attackRange, float attackDelay) // Spawner나 Controller가 공격 초기값을 넣어주는 함수
        {
            this.nexus = nexus; // 매개변수 nexus를 내부 nexus field에 저장한다.
            this.attackDamage = attackDamage; // 매개변수 attackDamage를 내부 attackDamage field에 저장한다.
            this.attackRange = attackRange; // 매개변수 attackRange를 내부 attackRange field에 저장한다.
            this.attackDelay = attackDelay; // 매개변수 attackDelay를 내부 attackDelay field에 저장한다.
        }

        public void ApplyAttackDamageMultiplier(float multiplier) // 웨이브 난이도 넥서스 피해 배율 적용
        {
            if (attackDamage <= 0 || multiplier <= 0f || Mathf.Approximately(multiplier, 1f)) // 적용할 필요가 없는 값이라면
            {
                return; // 적용하지 않는다.
            }

            attackDamage = Mathf.Max(1, Mathf.RoundToInt(attackDamage * multiplier)); // 기본 피해량에 배율을 적용한다.
        }
    }
}
