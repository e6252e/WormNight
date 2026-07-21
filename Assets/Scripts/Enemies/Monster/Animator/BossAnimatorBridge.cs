using UnityEngine;

namespace TeamProject01.Gameplay
{
    [RequireComponent(typeof(BossController))]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class BossAnimatorBridge : MonoBehaviour // Boss01의 보스 패턴 상태를 Animator Trigger로 전달하는 Script Component
    {
        private static readonly int HitParameter = Animator.StringToHash("Hit"); // 피격 Trigger
        private static readonly int DeathParameter = Animator.StringToHash("Death"); // 사망 Trigger
        private static readonly int TeleportOutParameter = Animator.StringToHash("TeleportOut"); // 순간이동으로 사라지는 Trigger
        private static readonly int DiamondParameter = Animator.StringToHash("Diamond"); // 일반 다이아몬드 포격 Trigger
        private static readonly int EnhancedDiamondParameter = Animator.StringToHash("EnhancedDiamond"); // 강화 다이아몬드 포격 Trigger
        private static readonly int SummonParameter = Animator.StringToHash("Summon"); // 소환 Trigger
        private static readonly int LineWallParameter = Animator.StringToHash("LineWall"); // 벽 / 미로 생성 Trigger
        private static readonly int ChargeReadyParameter = Animator.StringToHash("ChargeReady"); // 돌진 준비 Trigger
        private static readonly int ChargeParameter = Animator.StringToHash("Charge"); // 실제 돌진 Trigger
        private static readonly int JumpParameter = Animator.StringToHash("Jump"); // 점프 충격파 Trigger
        private static readonly int IsChargingParameter = Animator.StringToHash("IsCharging"); // 실제 돌진 중인지 나타내는 Bool

        [Header("Animator")]
        [SerializeField] private Animator animator; // Bat King No Root에 붙어 있는 Animator

        private BossController bossController; // Boss01의 Phase와 사망 상태를 관리하는 Script Component
        private EnemyHealth enemyHealth; // Boss01의 HP를 관리하는 Script Component
        private BossTeleportMovement teleportMovement; // 순간이동 패턴 Script Component
        private BossDiamondSiegeAttack diamondSiegeAttack; // 다이아몬드 포격 패턴 Script Component
        private BossSummonAttack summonAttack; // 몬스터 소환 패턴 Script Component
        private BossLineWallAttack lineWallAttack; // 벽 / 미로 생성 패턴 Script Component
        private BossChargeStunAttack chargeStunAttack; // 돌진 스턴 패턴 Script Component
        private BossJumpShockwaveAttack jumpShockwaveAttack; // 점프 충격파 패턴 Script Component

        private float previousHp; // 직전 프레임 HP

        private bool previousTeleporting; // 직전 프레임 순간이동 상태
        private bool previousDiamondAttacking; // 직전 프레임 다이아몬드 공격 상태
        private bool previousSummonAttacking; // 직전 프레임 소환 공격 상태
        private bool previousLineWallAttacking; // 직전 프레임 벽 / 미로 생성 상태
        private bool previousChargeAttacking; // 직전 프레임 돌진 공격 전체 상태
        private bool previousChargePreparing; // 직전 프레임 돌진 준비 상태
        private bool previousCharging; // 직전 프레임 실제 돌진 상태
        private bool previousJumpAttacking; // 직전 프레임 점프 공격 상태
        private bool deathPlayed; // Death Trigger를 이미 실행했는지 확인하는 값

        private bool hasHitParameter; // Animator에 Hit Trigger가 있는지 확인한 값
        private bool hasDeathParameter; // Animator에 Death Trigger가 있는지 확인한 값
        private bool hasTeleportOutParameter; // Animator에 TeleportOut Trigger가 있는지 확인한 값
        private bool hasDiamondParameter; // Animator에 Diamond Trigger가 있는지 확인한 값
        private bool hasEnhancedDiamondParameter; // Animator에 EnhancedDiamond Trigger가 있는지 확인한 값
        private bool hasSummonParameter; // Animator에 Summon Trigger가 있는지 확인한 값
        private bool hasLineWallParameter; // Animator에 LineWall Trigger가 있는지 확인한 값
        private bool hasChargeReadyParameter; // Animator에 ChargeReady Trigger가 있는지 확인한 값
        private bool hasChargeParameter; // Animator에 Charge Trigger가 있는지 확인한 값
        private bool hasJumpParameter; // Animator에 Jump Trigger가 있는지 확인한 값
        private bool hasIsChargingParameter; // Animator에 IsCharging Bool이 있는지 확인한 값

        private void Awake()
        {
            bossController = GetComponent<BossController>(); // 같은 Boss01에서 BossController를 찾는다.
            enemyHealth = GetComponent<EnemyHealth>(); // 같은 Boss01에서 EnemyHealth를 찾는다.
            teleportMovement = GetComponent<BossTeleportMovement>(); // 같은 Boss01에서 순간이동 Script를 찾는다.
            diamondSiegeAttack = GetComponent<BossDiamondSiegeAttack>(); // 같은 Boss01에서 다이아몬드 포격 Script를 찾는다.
            summonAttack = GetComponent<BossSummonAttack>(); // 같은 Boss01에서 소환 Script를 찾는다.
            lineWallAttack = GetComponent<BossLineWallAttack>(); // 같은 Boss01에서 벽 / 미로 생성 Script를 찾는다.
            chargeStunAttack = GetComponent<BossChargeStunAttack>(); // 같은 Boss01에서 돌진 Script를 찾는다.
            jumpShockwaveAttack = GetComponent<BossJumpShockwaveAttack>(); // 같은 Boss01에서 점프 충격파 Script를 찾는다.

            if (animator == null) // Inspector에서 Animator를 연결하지 않았다면
            {
                animator = GetComponentInChildren<Animator>(true); // 자식 오브젝트에서 Animator를 자동으로 찾는다.
            }

            if (enemyHealth != null) // EnemyHealth가 있다면
            {
                previousHp = enemyHealth.CurrentHp; // 시작 HP를 저장한다.
            }

            CacheAnimatorParameters(); // Animator Parameter 존재 여부를 미리 확인한다.
        }

        private void OnEnable()
        {
            CacheAnimatorParameters(); // Prefab 활성화 시 Animator Parameter를 다시 확인한다.
            RefreshPreviousStates(); // 현재 패턴 상태를 기준값으로 저장한다.
        }

        private void Update()
        {
            if (!CanUseAnimator()) // Animator를 사용할 수 없다면
            {
                return; // Trigger를 실행하지 않는다.
            }

            UpdateDeathAnimation(); // 사망 애니메이션을 확인한다.

            if (deathPlayed) // 이미 사망 애니메이션이 실행됐다면
            {
                return; // 다른 보스 스킬 애니메이션을 더 이상 실행하지 않는다.
            }

            UpdateHitAnimation(); // HP 감소에 따른 피격 애니메이션을 확인한다.
            UpdateTeleportAnimation(); // 순간이동 애니메이션을 확인한다.
            UpdateDiamondAnimation(); // 다이아몬드 포격 애니메이션을 확인한다.
            UpdateSummonAnimation(); // 소환 애니메이션을 확인한다.
            UpdateLineWallAnimation(); // 벽 / 미로 생성 애니메이션을 확인한다.
            UpdateChargeAnimation(); // 돌진 준비 / 돌진 애니메이션을 확인한다.
            UpdateJumpAnimation(); // 점프 충격파 애니메이션을 확인한다.
        }

        private void OnDisable()
        {
            ResetAllTriggers(); // 비활성화될 때 남아 있는 Trigger를 정리한다.
            SetBool(IsChargingParameter, hasIsChargingParameter, false); // 비활성화될 때 실제 돌진 Bool을 꺼 둔다.
        }

        private void UpdateDeathAnimation()
        {
            if (bossController == null || !bossController.IsDead) // 보스가 죽지 않았다면
            {
                return; // Death Trigger를 실행하지 않는다.
            }

            if (deathPlayed) // Death Trigger를 이미 실행했다면
            {
                return; // 중복 실행하지 않는다.
            }

            deathPlayed = true; // 사망 Trigger가 실행됐다고 저장한다.
            PlayTrigger(DeathParameter, hasDeathParameter); // Death Trigger를 실행한다.
        }

        private void UpdateHitAnimation()
        {
            if (enemyHealth == null) // EnemyHealth가 없다면
            {
                return; // HP 변화를 확인할 수 없다.
            }

            if (enemyHealth.CurrentHp < previousHp && !enemyHealth.IsDead) // HP가 줄었고 아직 죽지 않았다면
            {
                if (!IsHitAnimationAllowed()) // 현재 피격 애니메이션을 재생해도 되는 상태가 아니라면
                {
                    previousHp = enemyHealth.CurrentHp; // HP 기준값만 갱신하고
                    return; // Hit Trigger는 실행하지 않는다.
                }

                PlayTrigger(HitParameter, hasHitParameter); // Idle 상태일 때만 Hit Trigger를 실행한다.
            }

            previousHp = enemyHealth.CurrentHp; // 현재 HP를 다음 프레임 기준값으로 저장한다.
        }

        private bool IsHitAnimationAllowed()
        {
            if (!CanUseAnimator()) // Animator를 사용할 수 없다면
            {
                return false; // 피격 애니메이션을 실행하지 않는다.
            }

            if (bossController != null && bossController.IsActionRunning) // 보스 패턴이 실행 중이라면
            {
                return false; // 스킬 애니메이션을 Hit가 끊지 못하게 한다.
            }

            if (animator.IsInTransition(0)) // 다른 State로 전환 중이라면
            {
                return false; // 전환 중에는 Hit로 끼어들지 않는다.
            }

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0); // 현재 Animator State를 가져온다.

            if (!currentState.IsName("Idle")) // 현재 State가 Idle이 아니라면
            {
                return false; // 다른 애니메이션 중에는 Hit를 실행하지 않는다.
            }

            return true; // Idle 상태일 때만 Hit 애니메이션을 허용한다.
        }

        private void UpdateTeleportAnimation()
        {
            bool isTeleporting = teleportMovement != null && teleportMovement.IsTeleporting; // 현재 순간이동 중인지 확인한다.

            if (isTeleporting && !previousTeleporting) // 순간이동이 이번 프레임에 시작됐다면
            {
                PlayTrigger(TeleportOutParameter, hasTeleportOutParameter); // TeleportOut Trigger만 실행한다.
            }

            previousTeleporting = isTeleporting; // 현재 순간이동 상태를 다음 프레임 기준값으로 저장한다.
        }

        private void UpdateDiamondAnimation()
        {
            bool isAttacking = diamondSiegeAttack != null && diamondSiegeAttack.IsAttacking; // 현재 다이아몬드 포격 중인지 확인한다.

            if (isAttacking && !previousDiamondAttacking) // 다이아몬드 포격이 이번 프레임에 시작됐다면
            {
                if (bossController != null && bossController.CurrentPhase == BossPhase.Berserk) // Berserk Phase라면
                {
                    PlayTrigger(EnhancedDiamondParameter, hasEnhancedDiamondParameter); // 강화 다이아몬드 Trigger를 실행한다.
                }
                else
                {
                    PlayTrigger(DiamondParameter, hasDiamondParameter); // 일반 다이아몬드 Trigger를 실행한다.
                }
            }

            previousDiamondAttacking = isAttacking; // 현재 다이아몬드 공격 상태를 다음 프레임 기준값으로 저장한다.
        }

        private void UpdateSummonAnimation()
        {
            bool isAttacking = summonAttack != null && summonAttack.IsAttacking; // 현재 소환 패턴 중인지 확인한다.

            if (isAttacking && !previousSummonAttacking) // 소환 패턴이 이번 프레임에 시작됐다면
            {
                PlayTrigger(SummonParameter, hasSummonParameter); // Summon Trigger를 실행한다.
            }

            previousSummonAttacking = isAttacking; // 현재 소환 상태를 다음 프레임 기준값으로 저장한다.
        }

        private void UpdateLineWallAnimation()
        {
            bool isAttacking = lineWallAttack != null && lineWallAttack.IsAttacking; // 현재 벽 / 미로 생성 중인지 확인한다.

            if (isAttacking && !previousLineWallAttacking) // 벽 / 미로 생성이 이번 프레임에 시작됐다면
            {
                PlayTrigger(LineWallParameter, hasLineWallParameter); // LineWall Trigger를 실행한다.
            }

            previousLineWallAttacking = isAttacking; // 현재 벽 / 미로 생성 상태를 다음 프레임 기준값으로 저장한다.
        }

        private void UpdateChargeAnimation()
        {
            bool isAttacking = chargeStunAttack != null && chargeStunAttack.IsAttacking; // 현재 돌진 패턴 전체가 진행 중인지 확인한다.
            bool isChargePreparing = chargeStunAttack != null && chargeStunAttack.IsChargePreparing; // 현재 돌진 준비 단계인지 확인한다.
            bool isCharging = chargeStunAttack != null && chargeStunAttack.IsCharging; // 현재 실제 돌진 이동 중인지 확인한다.

            if (isChargePreparing && !previousChargePreparing) // 돌진 준비가 이번 프레임에 시작됐다면
            {
                PlayTrigger(ChargeReadyParameter, hasChargeReadyParameter); // ChargeReady Trigger를 실행한다.
            }

            if (isCharging && !previousCharging) // 실제 돌진 이동이 이번 프레임에 시작됐다면
            {
                SetBool(IsChargingParameter, hasIsChargingParameter, true); // Charge State가 Idle로 자동 복귀하지 않도록 IsCharging을 켠다.
                PlayTrigger(ChargeParameter, hasChargeParameter); // Charge Trigger를 실행한다.
            }

            if (!isCharging && previousCharging) // 실제 돌진 이동이 이번 프레임에 끝났다면
            {
                SetBool(IsChargingParameter, hasIsChargingParameter, false); // Charge State가 Idle로 복귀할 수 있게 IsCharging을 끈다.
            }

            if (!isAttacking && previousChargeAttacking) // 돌진 패턴 전체가 종료됐다면
            {
                SetBool(IsChargingParameter, hasIsChargingParameter, false); // 예외 상황에서도 IsCharging이 남지 않게 끈다.
            }

            previousChargeAttacking = isAttacking; // 현재 돌진 공격 전체 상태를 다음 프레임 기준값으로 저장한다.
            previousChargePreparing = isChargePreparing; // 현재 돌진 준비 상태를 다음 프레임 기준값으로 저장한다.
            previousCharging = isCharging; // 현재 실제 돌진 상태를 다음 프레임 기준값으로 저장한다.
        }

        private void UpdateJumpAnimation()
        {
            bool isAttacking = jumpShockwaveAttack != null && jumpShockwaveAttack.IsAttacking; // 현재 점프 충격파 패턴 중인지 확인한다.

            if (isAttacking && !previousJumpAttacking) // 점프 충격파가 이번 프레임에 시작됐다면
            {
                PlayTrigger(JumpParameter, hasJumpParameter); // Jump Trigger를 실행한다.
            }

            previousJumpAttacking = isAttacking; // 현재 점프 상태를 다음 프레임 기준값으로 저장한다.
        }

        private void PlayTrigger(int parameterHash, bool hasParameter)
        {
            if (!hasParameter) // Animator에 해당 Parameter가 없다면
            {
                return; // Trigger를 실행하지 않는다.
            }

            if (!CanUseAnimator()) // Animator를 사용할 수 없다면
            {
                return; // Trigger를 실행하지 않는다.
            }

            animator.ResetTrigger(parameterHash); // 같은 Trigger가 남아 있을 수 있으므로 먼저 초기화한다.
            animator.SetTrigger(parameterHash); // Animator Trigger를 실행한다.
        }

        private void SetBool(int parameterHash, bool hasParameter, bool value)
        {
            if (!hasParameter) // Animator에 해당 Bool Parameter가 없다면
            {
                return; // Bool 값을 변경하지 않는다.
            }

            if (!CanUseAnimator()) // Animator를 사용할 수 없다면
            {
                return; // Bool 값을 변경하지 않는다.
            }

            animator.SetBool(parameterHash, value); // Animator Bool 값을 변경한다.
        }

        private void ResetAllTriggers()
        {
            if (!CanUseAnimator()) // Animator를 사용할 수 없다면
            {
                return; // Trigger를 초기화하지 않는다.
            }

            ResetTrigger(HitParameter, hasHitParameter); // Hit Trigger를 초기화한다.
            ResetTrigger(DeathParameter, hasDeathParameter); // Death Trigger를 초기화한다.
            ResetTrigger(TeleportOutParameter, hasTeleportOutParameter); // TeleportOut Trigger를 초기화한다.
            ResetTrigger(DiamondParameter, hasDiamondParameter); // Diamond Trigger를 초기화한다.
            ResetTrigger(EnhancedDiamondParameter, hasEnhancedDiamondParameter); // EnhancedDiamond Trigger를 초기화한다.
            ResetTrigger(SummonParameter, hasSummonParameter); // Summon Trigger를 초기화한다.
            ResetTrigger(LineWallParameter, hasLineWallParameter); // LineWall Trigger를 초기화한다.
            ResetTrigger(ChargeReadyParameter, hasChargeReadyParameter); // ChargeReady Trigger를 초기화한다.
            ResetTrigger(ChargeParameter, hasChargeParameter); // Charge Trigger를 초기화한다.
            ResetTrigger(JumpParameter, hasJumpParameter); // Jump Trigger를 초기화한다.
        }

        private void ResetTrigger(int parameterHash, bool hasParameter)
        {
            if (!hasParameter) // Animator에 해당 Parameter가 없다면
            {
                return; // 초기화하지 않는다.
            }

            animator.ResetTrigger(parameterHash); // Trigger를 초기화한다.
        }

        private void RefreshPreviousStates()
        {
            previousTeleporting = teleportMovement != null && teleportMovement.IsTeleporting; // 현재 순간이동 상태를 기준값으로 저장한다.
            previousDiamondAttacking = diamondSiegeAttack != null && diamondSiegeAttack.IsAttacking; // 현재 다이아몬드 공격 상태를 기준값으로 저장한다.
            previousSummonAttacking = summonAttack != null && summonAttack.IsAttacking; // 현재 소환 상태를 기준값으로 저장한다.
            previousLineWallAttacking = lineWallAttack != null && lineWallAttack.IsAttacking; // 현재 벽 / 미로 생성 상태를 기준값으로 저장한다.
            previousChargeAttacking = chargeStunAttack != null && chargeStunAttack.IsAttacking; // 현재 돌진 공격 전체 상태를 기준값으로 저장한다.
            previousChargePreparing = chargeStunAttack != null && chargeStunAttack.IsChargePreparing; // 현재 돌진 준비 상태를 기준값으로 저장한다.
            previousCharging = chargeStunAttack != null && chargeStunAttack.IsCharging; // 현재 실제 돌진 상태를 기준값으로 저장한다.
            previousJumpAttacking = jumpShockwaveAttack != null && jumpShockwaveAttack.IsAttacking; // 현재 점프 상태를 기준값으로 저장한다.
            deathPlayed = bossController != null && bossController.IsDead; // 이미 죽은 상태로 활성화됐다면 Death 중복 실행을 막는다.

            if (enemyHealth != null) // EnemyHealth가 있다면
            {
                previousHp = enemyHealth.CurrentHp; // 현재 HP를 기준값으로 저장한다.
            }
        }

        private void CacheAnimatorParameters()
        {
            hasHitParameter = HasParameter(HitParameter); // Hit Parameter 존재 여부를 저장한다.
            hasDeathParameter = HasParameter(DeathParameter); // Death Parameter 존재 여부를 저장한다.
            hasTeleportOutParameter = HasParameter(TeleportOutParameter); // TeleportOut Parameter 존재 여부를 저장한다.
            hasDiamondParameter = HasParameter(DiamondParameter); // Diamond Parameter 존재 여부를 저장한다.
            hasEnhancedDiamondParameter = HasParameter(EnhancedDiamondParameter); // EnhancedDiamond Parameter 존재 여부를 저장한다.
            hasSummonParameter = HasParameter(SummonParameter); // Summon Parameter 존재 여부를 저장한다.
            hasLineWallParameter = HasParameter(LineWallParameter); // LineWall Parameter 존재 여부를 저장한다.
            hasChargeReadyParameter = HasParameter(ChargeReadyParameter); // ChargeReady Parameter 존재 여부를 저장한다.
            hasChargeParameter = HasParameter(ChargeParameter); // Charge Parameter 존재 여부를 저장한다.
            hasJumpParameter = HasParameter(JumpParameter); // Jump Parameter 존재 여부를 저장한다.
            hasIsChargingParameter = HasParameter(IsChargingParameter); // IsCharging Parameter 존재 여부를 저장한다.
        }

        private bool HasParameter(int parameterHash)
        {
            if (!CanUseAnimator()) // Animator를 사용할 수 없다면
            {
                return false; // Parameter가 없다고 처리한다.
            }

            AnimatorControllerParameter[] parameters = animator.parameters; // Animator Controller의 Parameter 목록을 가져온다.

            for (int i = 0; i < parameters.Length; i++) // 모든 Parameter를 확인한다.
            {
                if (parameters[i].nameHash == parameterHash) // 이름 Hash가 같다면
                {
                    return true; // Parameter가 존재한다.
                }
            }

            return false; // Parameter를 찾지 못했다.
        }

        private bool CanUseAnimator()
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
    }
}