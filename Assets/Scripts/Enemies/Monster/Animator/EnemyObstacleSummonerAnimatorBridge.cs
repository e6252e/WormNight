using UnityEngine;

namespace TeamProject01.Gameplay
{
    [RequireComponent(typeof(EnemyObstacleSummoner))]
    public sealed class EnemyObstacleSummonerAnimatorBridge : MonoBehaviour // 장애물 소환 몬스터의 소환 애니메이션을 연결하는 Script Component
    {
        private static readonly int IsMovingParameter = Animator.StringToHash("IsMoving"); // 이동 Animator Bool
        private static readonly int AttackParameter = Animator.StringToHash("Attack"); // 공격 Animator Trigger
        private static readonly int HitParameter = Animator.StringToHash("Hit"); // 피격 Animator Trigger
        private static readonly int SummonParameter = Animator.StringToHash("Summon"); // 장애물 소환 Animator Trigger
        private static readonly int IsSummoningParameter = Animator.StringToHash("IsSummoning"); // 장애물 소환 중 Animator Bool

        [Header("Animator")]
        [SerializeField] private Animator animator; // Necromancer 모델에 붙은 Animator Component 참조

        private bool useDirectSummonState = true; // 공격 애니메이션 중에도 Summon State로 직접 전환할지
        private string summonStateFullPath = "Base Layer.Summon"; // Animator의 Summon State 전체 경로
        private float summonCrossFadeDuration = 0.03f; // Summon State로 전환할 때 사용할 짧은 전환 시간
        private bool useDirectReturnState = true; // 소환 취소/종료 시 Idle State로 직접 복귀할지
        private string returnStateFullPath = "Base Layer.Idle"; // 소환 취소/종료 후 돌아갈 State 전체 경로
        private float returnCrossFadeDuration = 0.05f; // 복귀 State로 전환할 때 사용할 짧은 전환 시간

        private EnemyObstacleSummoner obstacleSummoner; // 장애물 소환 상태를 읽을 EnemyObstacleSummoner 참조
        private EnemyHealth enemyHealth; // 몬스터가 사망했는지 확인할 EnemyHealth 참조

        private bool wasSummoning; // 이전 프레임에 장애물을 소환 중이었는지 저장한다.

        private void Awake()
        {
            obstacleSummoner = GetComponent<EnemyObstacleSummoner>(); // 같은 GameObject에 붙은 EnemyObstacleSummoner를 찾는다.
            enemyHealth = GetComponent<EnemyHealth>(); // 같은 GameObject에 붙은 EnemyHealth를 찾는다.

            if (animator == null) // Inspector에 Animator가 연결되지 않았다면
            {
                animator = GetComponentInChildren<Animator>(true); // 자식 오브젝트에서 Animator를 자동으로 찾는다.
            }
        }

        private void OnEnable()
        {
            wasSummoning = obstacleSummoner != null && obstacleSummoner.IsSummoning; // 활성화 시 현재 소환 상태를 초기값으로 저장한다.

            if (wasSummoning) // 이미 소환 중인 상태로 활성화됐다면
            {
                PlaySummonAnimation(); // Animator 상태도 소환 애니메이션으로 맞춘다.
            }
        }

        private void Update()
        {
            if (obstacleSummoner == null) // 장애물 소환 Script가 없다면
            {
                return; // 애니메이션을 갱신하지 않는다.
            }

            if (enemyHealth != null && enemyHealth.IsDead) // 몬스터가 사망했다면
            {
                if (wasSummoning) // 사망 직전 소환 중이었다면
                {
                    StopSummonAnimation(); // 소환 애니메이션 상태를 정리한다.
                }

                wasSummoning = false; // 이전 소환 상태를 초기화한다.
                return; // 죽은 몬스터는 소환 애니메이션을 실행하지 않는다.
            }

            bool isSummoning = obstacleSummoner.IsSummoning; // 현재 장애물 소환 상태를 가져온다.

            if (isSummoning && !wasSummoning) // 이전에는 소환 중이 아니었고 현재 소환이 시작됐다면
            {
                PlaySummonAnimation(); // 공격/피격 애니메이션을 끊고 소환 애니메이션을 실행한다.
            }

            if (!isSummoning && wasSummoning) // 이전에는 소환 중이었는데 현재 소환이 끝났거나 취소됐다면
            {
                StopSummonAnimation(); // 소환 애니메이션을 정리하고 다음 상태로 넘어가게 한다.
            }

            wasSummoning = isSummoning; // 현재 소환 상태를 다음 프레임 비교용으로 저장한다.
        }

        private void OnDisable()
        {
            wasSummoning = false; // 비활성화될 때 이전 소환 상태를 초기화한다.
            StopSummonAnimation(); // 남아 있을 수 있는 소환 애니메이션 상태를 정리한다.
        }

        private void PlaySummonAnimation() // 장애물 소환 애니메이션을 실행하는 함수
        {
            if (!CanUseAnimator()) // Animator를 사용할 수 없다면
            {
                return; // 애니메이션을 실행하지 않는다.
            }

            SetBoolIfExists(IsMovingParameter, false); // 소환 중에는 이동 애니메이션을 끈다.
            SetBoolIfExists(IsSummoningParameter, true); // Animator에 IsSummoning Bool이 있다면 소환 중으로 설정한다.

            ResetTriggerIfExists(AttackParameter); // 공격 애니메이션 Trigger를 제거해서 공격 애니메이션을 끊는다.
            ResetTriggerIfExists(HitParameter); // 피격 애니메이션 Trigger를 제거해서 피격 애니메이션이 끼어들지 않게 한다.
            ResetTriggerIfExists(SummonParameter); // 이전 Summon Trigger가 남아 있다면 초기화한다.
            SetTriggerIfExists(SummonParameter); // Summon Trigger가 있다면 실행한다.

            if (useDirectSummonState) // 직접 Summon State 진입 옵션이 켜져 있다면
            {
                TryCrossFadeState(summonStateFullPath, summonCrossFadeDuration); // Attack 중이어도 Summon State로 직접 전환한다.
            }

            animator.Update(0.0f); // 같은 프레임에 Animator 전환을 평가한다.
        }

        private void StopSummonAnimation() // 장애물 소환 애니메이션을 종료하는 함수
        {
            if (!CanUseAnimator()) // Animator를 사용할 수 없다면
            {
                return; // 정리할 Animator가 없으므로 종료한다.
            }

            ResetTriggerIfExists(SummonParameter); // 남아 있을 수 있는 Summon Trigger를 제거한다.
            SetBoolIfExists(IsSummoningParameter, false); // Animator에 IsSummoning Bool이 있다면 소환 중 상태를 끈다.

            if (useDirectReturnState) // 직접 복귀 State 진입 옵션이 켜져 있다면
            {
                TryCrossFadeState(returnStateFullPath, returnCrossFadeDuration); // 소환 취소/종료 후 Idle 쪽으로 직접 복귀시킨다.
            }

            animator.Update(0.0f); // 같은 프레임에 Animator 전환을 평가한다.
        }

        private bool CanUseAnimator() // Animator에 값을 보내도 되는지 확인하는 함수
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

            return true; // Animator를 사용할 수 있다.
        }

        private bool HasParameter(int parameterHash, AnimatorControllerParameterType parameterType) // Animator Parameter가 있는지 확인하는 함수
        {
            if (!CanUseAnimator()) // Animator를 사용할 수 없다면
            {
                return false; // Parameter를 확인할 수 없다.
            }

            AnimatorControllerParameter[] parameters = animator.parameters; // Animator Controller의 Parameter 목록을 가져온다.

            for (int i = 0; i < parameters.Length; i++) // 모든 Parameter를 순회한다.
            {
                if (parameters[i].nameHash == parameterHash && parameters[i].type == parameterType) // 이름과 타입이 모두 맞다면
                {
                    return true; // 해당 Parameter가 있다.
                }
            }

            return false; // 해당 Parameter가 없다.
        }

        private void SetBoolIfExists(int parameterHash, bool value) // Bool Parameter가 있을 때만 값을 설정하는 함수
        {
            if (!HasParameter(parameterHash, AnimatorControllerParameterType.Bool)) // Bool Parameter가 없다면
            {
                return; // 없는 Parameter는 건드리지 않는다.
            }

            animator.SetBool(parameterHash, value); // Bool 값을 설정한다.
        }

        private void ResetTriggerIfExists(int parameterHash) // Trigger Parameter가 있을 때만 초기화하는 함수
        {
            if (!HasParameter(parameterHash, AnimatorControllerParameterType.Trigger)) // Trigger Parameter가 없다면
            {
                return; // 없는 Trigger는 건드리지 않는다.
            }

            animator.ResetTrigger(parameterHash); // Trigger를 초기화한다.
        }

        private void SetTriggerIfExists(int parameterHash) // Trigger Parameter가 있을 때만 실행하는 함수
        {
            if (!HasParameter(parameterHash, AnimatorControllerParameterType.Trigger)) // Trigger Parameter가 없다면
            {
                return; // 없는 Trigger는 실행하지 않는다.
            }

            animator.SetTrigger(parameterHash); // Trigger를 실행한다.
        }

        private void TryCrossFadeState(string stateFullPath, float crossFadeDuration) // 특정 Animator State로 직접 전환하는 함수
        {
            if (!CanUseAnimator()) // Animator를 사용할 수 없다면
            {
                return; // 직접 전환하지 않는다.
            }

            if (string.IsNullOrEmpty(stateFullPath)) // State 경로가 비어 있다면
            {
                return; // 직접 전환할 State가 없으므로 종료한다.
            }

            int stateHash = Animator.StringToHash(stateFullPath); // State 전체 경로를 Hash로 바꾼다.

            if (!animator.HasState(0, stateHash)) // Base Layer에 해당 State가 없다면
            {
                return; // 직접 전환하지 않는다.
            }

            animator.CrossFadeInFixedTime(stateHash, crossFadeDuration, 0); // 해당 State로 직접 전환한다.
        }
    }
}