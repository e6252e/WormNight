using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemySupportDebuffState : MonoBehaviour
    {
        private const float StatusFloatingCooldownSeconds = 0.45f;
        private const float StatusTickStaggerDuration = 0.04f;

        private float freezeTimer;
        private float incomingDamageMultiplier = 1f;
        private float incomingDamageTimer;
        private float moveSpeedSlowMultiplier = 1f;
        private float moveSpeedSlowTimer;
        private readonly List<Behaviour> disabledByFreezeBehaviours = new List<Behaviour>(6);
        private bool freezeBehavioursDisabled;
        private EnemyController enemyController;
        private EnemyHealth enemyHealth;
        private StatusBodyVfxController bodyVfx;
        private CombatStatusEffectKind activeStatusEffect;
        private GameObject activeStatusVfxPrefab;
        private float activeStatusTimer;
        private float activeStatusDuration;
        private float activeStatusTickInterval;
        private float activeStatusTickTimer;
        private float activeStatusDamagePerSecond;
        private float activeStatusMoveSpeedMultiplier = 1f;
        private float activeStatusIncomingDamageMultiplier = 1f;
        private DamageType activeStatusTickDamageType = DamageType.Direct;
        private int activeStatusSourceSegmentIndex = -1;
        private GameObject activeStatusSourceObject;
        private float statusFloatingCooldown;

        public bool IsFrozen => freezeTimer > 0f;
        public float MoveSpeedMultiplier
        {
            get
            {
                float multiplier = 1f;
                if (moveSpeedSlowTimer > 0f)
                {
                    multiplier = Mathf.Min(multiplier, Mathf.Clamp(moveSpeedSlowMultiplier, 0.05f, 1f));
                }

                if (activeStatusTimer > 0f)
                {
                    multiplier = Mathf.Min(multiplier, Mathf.Clamp(activeStatusMoveSpeedMultiplier, 0.05f, 1f));
                }

                return multiplier;
            }
        }

        private void Awake()
        {
            enemyController = GetComponent<EnemyController>();
            enemyHealth = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            SubscribeHealth();
        }

        public static EnemySupportDebuffState GetOrAdd(EnemyController enemy)
        {
            if (enemy == null)
            {
                return null;
            }

            if (!enemy.TryGetComponent(out EnemySupportDebuffState state))
            {
                state = enemy.gameObject.AddComponent<EnemySupportDebuffState>();
            }

            return state;
        }

        public static bool IsEnemyFrozen(EnemyController enemy) //조성원추가-0622 동결 몬스터 상태 확인
        {
            if (enemy == null) //조성원추가-0622 확인할 몬스터가 없으면 동결상태가 아니다.
            {
                return false; //조성원추가-0622 동결되지 않음으로 반환
            }

            if (!enemy.TryGetComponent(out EnemySupportDebuffState state)) //조성원추가-0622 디버프 상태 확인
            {
                return false; //조성원추가-0622 디버프상태가 없다면 동결되지 않은것으로 반환
            }
            return state.IsFrozen; //조성원추가-0622 현재 동결상태를 반환
        }

        public void ApplyFreeze(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            EnemySuicideCharger suicideCharger = GetComponent<EnemySuicideCharger>(); // 조성원추가-0626 - 자폭 몬스터의 현재 충전 상태를 확인한다.

            if (suicideCharger != null && suicideCharger.IsCharging) // 조성원추가-0626 - 이미 자폭 준비 중이라면
            {
                return; // 조성원추가-0626 - 동결 상태와 동결 시간을 적용하지 않아 자폭이 중단되지 않게 한다.
            }

            bool wasFrozen = IsFrozen;
            freezeTimer = Mathf.Max(freezeTimer, Mathf.Max(0f, duration));
            if (!wasFrozen)
            {
                DisableFreezeBehaviours();
            }
        }

        public void ApplyIncomingDamageMultiplier(float multiplier, float duration)
        {
            if (multiplier <= 1f || duration <= 0f)
            {
                return;
            }

            ClearActiveStatusEffect(true);
            ClearMoveSpeedSlow();
            incomingDamageMultiplier = Mathf.Max(1f, multiplier);
            incomingDamageTimer = Mathf.Max(0f, duration);
        }

        public void ApplyMoveSpeedSlow(float multiplier, float duration)
        {
            if (multiplier >= 1f || duration <= 0f)
            {
                return;
            }

            EnemySuicideCharger suicideCharger = GetComponent<EnemySuicideCharger>(); // 조성원추가-0626 - 자폭 몬스터의 현재 충전 상태를 확인한다.

            if (suicideCharger != null && suicideCharger.IsCharging) // 조성원추가-0626 - 이미 자폭 준비 중이라면
            {
                return; // 조성원추가-0626 - 이동속도 감소를 적용하지 않아 자폭 진행 상태를 유지한다.
            }

            ClearActiveStatusEffect(true);
            ClearIncomingDamageMultiplier();
            moveSpeedSlowMultiplier = Mathf.Clamp(multiplier, 0.05f, 1f);
            moveSpeedSlowTimer = Mathf.Max(0f, duration);
        }

        public void ApplyStatusEffect(
            CombatStatusEffectKind kind,
            int sourceSegmentIndex,
            GameObject sourceObject,
            Vector3 hitPosition,
            GameObject vfxPrefab,
            float durationOverride = 0f,
            float incomingDamageMultiplierOverride = 0f)
        {
            if (IsOwnerDead())
            {
                ClearAllDebuffs(true);
                return;
            }

            if (!CombatStatusEffectCatalog.TryGet(kind, out CombatStatusEffectDefinition definition) || !definition.IsEnemyDebuff)
            {
                ClearActiveStatusEffect(true);
                return;
            }

            bool sameStatus = activeStatusTimer > 0f && activeStatusEffect == kind && activeStatusVfxPrefab == vfxPrefab;
            float retainedTickTimer = sameStatus ? activeStatusTickTimer : 0f;
            if (!sameStatus)
            {
                ClearActiveStatusEffect(true);
            }

            ClearIncomingDamageMultiplier();
            ClearMoveSpeedSlow();

            activeStatusEffect = kind;
            activeStatusVfxPrefab = vfxPrefab;
            activeStatusDuration = durationOverride > 0f ? durationOverride : definition.Duration;
            activeStatusTimer = Mathf.Max(0.05f, activeStatusDuration);
            activeStatusTickInterval = Mathf.Max(0f, definition.TickInterval);
            activeStatusDamagePerSecond = Mathf.Max(0f, definition.DamagePerSecond);
            activeStatusTickTimer = sameStatus && activeStatusDamagePerSecond > 0f
                ? Mathf.Clamp(retainedTickTimer, Time.deltaTime, activeStatusTickInterval)
                : activeStatusTickInterval;
            activeStatusMoveSpeedMultiplier = CanApplyMoveSpeedPenalty() ? definition.MoveSpeedMultiplier : 1f;
            activeStatusIncomingDamageMultiplier = incomingDamageMultiplierOverride > 1f
                ? incomingDamageMultiplierOverride
                : Mathf.Max(1f, definition.IncomingDamageMultiplier);
            activeStatusTickDamageType = definition.TickDamageType;
            activeStatusSourceSegmentIndex = sourceSegmentIndex;
            activeStatusSourceObject = sourceObject;

            if (statusFloatingCooldown <= 0f || !sameStatus)
            {
                DamageFloatingSpawner.SpawnStatusEffect(definition.DisplayName, definition.FloatingColor, ResolveStatusFloatingPosition(hitPosition));
                statusFloatingCooldown = StatusFloatingCooldownSeconds;
            }

            if (!sameStatus && vfxPrefab != null)
            {
                GetOrAddBodyVfx().Show(vfxPrefab, definition.VfxEffectName);
            }
        }

        public DamageData ApplyIncomingDamageBonus(DamageData damage)
        {
            float multiplier = 1f;
            if (incomingDamageTimer > 0f && incomingDamageMultiplier > 1f)
            {
                multiplier = Mathf.Max(multiplier, incomingDamageMultiplier);
            }

            if (activeStatusTimer > 0f && activeStatusIncomingDamageMultiplier > 1f)
            {
                multiplier = Mathf.Max(multiplier, activeStatusIncomingDamageMultiplier);
            }

            return multiplier > 1f ? damage.WithAmount(damage.Amount * multiplier) : damage;
        }

        private void Update()
        {
            if (IsOwnerDead())
            {
                ClearAllDebuffs(true);
                return;
            }

            if (statusFloatingCooldown > 0f)
            {
                statusFloatingCooldown -= Time.deltaTime;
            }

            if (freezeTimer > 0f)
            {
                freezeTimer -= Time.deltaTime;
                if (freezeTimer <= 0f)
                {
                    RestoreFreezeBehaviours();
                }
            }

            UpdateActiveStatusEffect();

            if (incomingDamageTimer <= 0f)
            {
                UpdateMoveSpeedSlowTimer();
                return;
            }

            incomingDamageTimer -= Time.deltaTime;
            if (incomingDamageTimer <= 0f)
            {
                incomingDamageMultiplier = 1f;
            }

            UpdateMoveSpeedSlowTimer();
        }

        private void OnDisable()
        {
            UnsubscribeHealth();
            ClearAllDebuffs(true);
        }

        private void SubscribeHealth()
        {
            if (enemyHealth == null)
            {
                enemyHealth = GetComponent<EnemyHealth>();
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
            if (changedHealth != null && changedHealth.IsDead)
            {
                ClearAllDebuffs(true); // 사망 애니메이션 중에도 디버프 VFX 즉시 제거
            }
        }

        private bool IsOwnerDead()
        {
            if (enemyController == null)
            {
                enemyController = GetComponent<EnemyController>();
            }

            if (enemyHealth == null)
            {
                enemyHealth = GetComponent<EnemyHealth>();
            }

            return (enemyController != null && enemyController.IsDead) || (enemyHealth != null && enemyHealth.IsDead);
        }

        private void ClearAllDebuffs(bool stopVfx)
        {
            ClearActiveStatusEffect(stopVfx);
            ClearIncomingDamageMultiplier();
            ClearMoveSpeedSlow();
            freezeTimer = 0f;
            RestoreFreezeBehaviours();
        }

        private void UpdateActiveStatusEffect()
        {
            if (activeStatusTimer <= 0f)
            {
                return;
            }

            activeStatusTimer -= Time.deltaTime;
            TickActiveStatusDamage();
            if (activeStatusTimer <= 0f)
            {
                ClearActiveStatusEffect(true);
            }
        }

        private void TickActiveStatusDamage()
        {
            if (activeStatusDamagePerSecond <= 0f || activeStatusTickInterval <= 0f)
            {
                return;
            }

            activeStatusTickTimer -= Time.deltaTime;
            if (activeStatusTickTimer > 0f)
            {
                return;
            }

            activeStatusTickTimer += activeStatusTickInterval;
            EnemyController controller = enemyController != null ? enemyController : GetComponent<EnemyController>();
            if (controller == null || controller.IsDead)
            {
                return;
            }

            float damageAmount = activeStatusDamagePerSecond * activeStatusTickInterval;
            DamageData tickDamage = DamageData.Create(
                damageAmount,
                activeStatusTickDamageType,
                activeStatusSourceSegmentIndex,
                transform.position,
                activeStatusSourceObject);
            controller.ApplyDamage(tickDamage);
            ApplyStatusTickStagger(tickDamage);
        }

        private void ApplyStatusTickStagger(DamageData tickDamage)
        {
            EnemyController controller = enemyController != null ? enemyController : GetComponent<EnemyController>();
            if (controller == null)
            {
                return;
            }

            Vector3 origin = tickDamage.SourceObject != null
                ? tickDamage.SourceObject.transform.position
                : transform.position - transform.forward;
            Vector3 direction = transform.position - origin;
            direction.y = 0f;
            MonsterFeedbackData feedback = MonsterFeedbackData.Create(
                origin,
                direction,
                transform.position,
                0f,
                0.01f,
                StatusTickStaggerDuration,
                tickDamage.SourceSegmentIndex,
                tickDamage.Type,
                tickDamage.SourceObject);
            MonsterFeedbackApi.TryApplyFeedback(controller, feedback);
        }

        private void UpdateMoveSpeedSlowTimer()
        {
            if (moveSpeedSlowTimer <= 0f)
            {
                return;
            }

            moveSpeedSlowTimer -= Time.deltaTime;
            if (moveSpeedSlowTimer <= 0f)
            {
                moveSpeedSlowMultiplier = 1f;
            }
        }

        private void ClearActiveStatusEffect(bool stopVfx)
        {
            activeStatusEffect = CombatStatusEffectKind.None;
            activeStatusVfxPrefab = null;
            activeStatusTimer = 0f;
            activeStatusDuration = 0f;
            activeStatusTickInterval = 0f;
            activeStatusTickTimer = 0f;
            activeStatusDamagePerSecond = 0f;
            activeStatusMoveSpeedMultiplier = 1f;
            activeStatusIncomingDamageMultiplier = 1f;
            activeStatusTickDamageType = DamageType.Direct;
            activeStatusSourceSegmentIndex = -1;
            activeStatusSourceObject = null;

            if (stopVfx && bodyVfx != null)
            {
                bodyVfx.Clear();
            }
        }

        private void ClearIncomingDamageMultiplier()
        {
            incomingDamageMultiplier = 1f;
            incomingDamageTimer = 0f;
        }

        private void ClearMoveSpeedSlow()
        {
            moveSpeedSlowMultiplier = 1f;
            moveSpeedSlowTimer = 0f;
        }

        private bool CanApplyMoveSpeedPenalty()
        {
            EnemySuicideCharger suicideCharger = GetComponent<EnemySuicideCharger>();
            return suicideCharger == null || !suicideCharger.IsCharging;
        }

        private Vector3 ResolveStatusFloatingPosition(Vector3 hitPosition)
        {
            return hitPosition.sqrMagnitude > 0.0001f ? hitPosition : transform.position;
        }

        private StatusBodyVfxController GetOrAddBodyVfx()
        {
            if (bodyVfx == null)
            {
                bodyVfx = GetComponent<StatusBodyVfxController>();
            }

            if (bodyVfx == null)
            {
                bodyVfx = gameObject.AddComponent<StatusBodyVfxController>();
            }

            return bodyVfx;
        }

        private void DisableFreezeBehaviours()
        {
            if (freezeBehavioursDisabled)
            {
                return;
            }

            disabledByFreezeBehaviours.Clear();
            DisableIfEnabled(GetComponent<EnemyMeleeAttack>());
            DisableIfEnabled(GetComponent<EnemyRangedAttack>());
            DisableIfEnabled(GetComponent<EnemySlowZoneThrower>());
            DisableIfEnabled(GetComponent<EnemyObstacleSummoner>());
            DisableIfEnabled(GetComponent<EnemyBuffCaster>());
            DisableIfEnabled(GetComponent<EnemySuicideCharger>());
            freezeBehavioursDisabled = true;
        }

        private void DisableIfEnabled(Behaviour behaviour)
        {
            if (behaviour == null || !behaviour.enabled)
            {
                return;
            }

            behaviour.enabled = false;
            disabledByFreezeBehaviours.Add(behaviour);
        }

        private void RestoreFreezeBehaviours()
        {
            if (!freezeBehavioursDisabled)
            {
                return;
            }

            for (int i = 0; i < disabledByFreezeBehaviours.Count; i++)
            {
                Behaviour behaviour = disabledByFreezeBehaviours[i];
                if (behaviour != null)
                {
                    behaviour.enabled = true;
                }
            }

            disabledByFreezeBehaviours.Clear();
            freezeBehavioursDisabled = false;
        }
    }
}
