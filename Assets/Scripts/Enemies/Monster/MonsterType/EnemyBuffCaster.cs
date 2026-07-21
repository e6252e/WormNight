using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyBuffCaster : MonoBehaviour // 주변 몬스터에게 랜덤 버프를 부여하는 몬스터
    {
        [Min(0.1f)]
        [SerializeField] private float buffRange = 8.0f; // 버프 범위

        [Min(0.1f)]
        [SerializeField] private float buffInterval = 8.0f; // 몇 초마다 버프를 부여할지

        [Range(0.0f, 1.0f)]
        [SerializeField] private float buffChance = 0.4f; // 범위 안 몬스터에게 버프를 줄 확률

        [Min(1)]
        [SerializeField] private int maxBuffCountPerCast = 5; // 한 번 시전할 때 버프를 받을 최대 몬스터 수

        [Min(1.0f)]
        [SerializeField] private float attackPowerMultiplier = 1.5f; // 공격력 버프 배율

        [Min(1.0f)]
        [SerializeField] private float moveSpeedMultiplier = 1.3f; // 이동속도 버프 배율

        [Min(1.0f)]
        [SerializeField] private float attackSpeedMultiplier = 1.5f; // 공격속도 버프 배율

        [Min(1.0f)]
        [SerializeField] private float buffDuration = 5.0f; // 버프 유지 시간

        [SerializeField] private bool canBuffSelf; // 자기 자신도 버프 대상에 포함할지

        private EnemyController ownerController; // 이 Script Component가 붙은 몬스터의 EnemyController

        private readonly List<EnemyController> nearbyEnemies = new List<EnemyController>(32);

        private readonly List<EnemyBuffReceiver> buffCandidates = new List<EnemyBuffReceiver>(32);

        private float buffTimer;

        private void Awake()
        {
            ownerController = GetComponent<EnemyController>();// 같은 GameObject에 붙은 EnemyController Script Component를 찾는다.
        }

        private void OnEnable()
        {
            buffTimer = buffInterval; // 생성되거나 다시 활성화된 직후 바로 버프하지 않도록 첫 대기 시간을 설정한다.
        }

        private void Update()
        {
            buffTimer -= Time.deltaTime;// 지난 시간만큼 버프 대기 시간을 감소시킨다.

            if (buffTimer > 0.0f)
            {
                return;// 아직 다음 시전 시간이 되지 않았다면 종료한다.
            }

            CastBuff();// 주변 몬스터를 찾아 버프를 부여한다.
            
            buffTimer = buffInterval;// 다음 버프 시전까지의 대기 시간을 다시 설정한다.
        }

        private void CastBuff() // 주변 몬스터를 찾아 버프를 부여하는 함수
        {
            List<EnemyBuffReceiver> candidates = FindBuffCandidates();// 현재 범위 안에서 버프를 받을 수 있는 몬스터 목록을 가져온다.

            int appliedCount = 0;// 이번 시전에서 실제로 버프를 부여한 몬스터 수

            for (int i = 0; i < candidates.Count; i++)
            {
                if (appliedCount >= maxBuffCountPerCast)
                {
                    return;// 최대 버프 대상 수에 도달했다면 시전을 종료한다.
                }

                if (Random.value > buffChance)
                {
                    continue;// 현재 후보 몬스터에 대한 확률 판정이 실패하면 다음 후보로 넘어간다.
                }

                ApplyRandomBuff(candidates[i]);// 현재 후보 몬스터에게 랜덤한 버프 하나를 적용한다.
                
                appliedCount++;// 실제 버프 적용 수를 증가시킨다.
            }
        }

        private List<EnemyBuffReceiver> FindBuffCandidates()
        {
            buffCandidates.Clear();// 이전 시전에서 저장된 버프 대상 목록을 비운다.

            EnemyController.CollectActiveInRange(transform.position, buffRange, nearbyEnemies); // 현재 위치를 기준으로 살아 있는 몬스터를 가져온다.

            for (int i = 0; i < nearbyEnemies.Count; i++)
            {
                EnemyController candidateController = nearbyEnemies[i];

                if (candidateController == null)
                {
                    continue;
                }

                if (!canBuffSelf && candidateController == ownerController)
                {
                    continue;
                }

                EnemyBuffReceiver receiver = candidateController.GetComponent<EnemyBuffReceiver>();

                if (receiver == null)
                {
                    continue;// 버프를 받을 Script Component가 없다면 제외한다.
                }

                buffCandidates.Add(receiver); // 버프를 받을 수 있는 후보 목록에 추가한다.
            }

            return buffCandidates; // 최종 버프 후보 목록을 반환한다.
        }

        private void ApplyRandomBuff(EnemyBuffReceiver receiver) // 몬스터에게 랜덤 버프를 적용하는 함수
        {
            if (receiver == null)
            {
                return; // 버프 대상이 없다면 종료한다.
            }

            EnemyBuffType buffType = PickRandomBuffType();// 적용할 버프 종류를 랜덤으로 선택한다.

            float multiplier = GetMultiplier(buffType);// 선택된 버프 종류에 해당하는 배율을 가져온다.

            receiver.ApplyBuff(buffType, multiplier, buffDuration);// 대상 몬스터에게 버프 종류, 배율, 유지 시간을 전달한다.
        }

        private EnemyBuffType PickRandomBuffType()// 랜덤으로 버프 종류를 선택하는 함수
        {
            int randomIndex = Random.Range(0, 3);// 0, 1, 2 중 하나를 랜덤으로 선택한다.

            if (randomIndex == 0)
            {
                return EnemyBuffType.AttackPower;// 0이면 공격력 버프를 반환한다.
            }

            if (randomIndex == 1)
            {
                return EnemyBuffType.MoveSpeed;// 1이면 이동속도 버프를 반환한다.
            }

            return EnemyBuffType.AttackSpeed;// 나머지 값인 2이면 공격속도 버프를 반환한다.
        }

        private float GetMultiplier(EnemyBuffType buffType) // 버프 종류에 맞는 고정 배율을 반환하는 함수
        {
            if (buffType == EnemyBuffType.AttackPower)
            {
                return attackPowerMultiplier;// 공격력 버프 배율을 반환한다.
            }

            if (buffType == EnemyBuffType.MoveSpeed)
            {
                return moveSpeedMultiplier;// 이동속도 버프 배율을 반환한다.
            }

            if (buffType == EnemyBuffType.AttackSpeed)
            {
                return attackSpeedMultiplier;// 공격속도 버프 배율을 반환한다.
            }

            return 1.0f; // 버프 종류가 None이라면 기본 배율을 반환한다.
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position, buffRange);// Scene 창에서 이 몬스터를 선택했을 때 버프 범위를 원으로 표시한다.
        }
    }
}