using System.Collections;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class BossDiamondSiegeAttack : MonoBehaviour
    {
        [Header("Projectile")]
        [SerializeField] private BossDiamondProjectile projectilePrefab;

        [SerializeField] private Transform handFirePoint;

        [Header("Animation")]
        [SerializeField] private Animator bossAnimator;

        [SerializeField] private string diamondAnimationTriggerName = "Diamond";

        [Header("Berserk Formation")]
        [SerializeField] private Transform burstCenter;

        [Min(1)]
        [SerializeField] private int berserkShotCount = 24;

        [Min(1)]
        [SerializeField] private int formationRowCount = 3;

        [Min(0.1f)]
        [SerializeField] private float formationHorizontalSpacing = 1.3f;

        [Min(0.1f)]
        [SerializeField] private float formationVerticalSpacing = 1.1f;

        [Min(0.0f)]
        [SerializeField] private float formationDepthSpacing = 0.6f;

        [Min(0.0f)]
        [SerializeField] private float formationWingRise = 0.35f;

        [Min(0.0f)]
        [SerializeField] private float formationHoldDuration = 0.5f;

        [Min(0.01f)]
        [SerializeField] private float berserkLaunchInterval = 0.05f;

        [Header("Berserk Target Spread")]
        [Min(0.0f)]
        [SerializeField] private float berserkTargetMinimumRadius = 3.0f;

        [Min(0.1f)]
        [SerializeField] private float berserkTargetMaximumRadius = 8.0f;

        [Range(0.0f, 45.0f)]
        [SerializeField] private float berserkTargetAngleJitter = 8.0f;

        [Header("Normal Attack")]
        [Min(0.1f)]
        [SerializeField] private float normalAttackInterval = 6.0f;

        [Range(0.0f, 1.0f)]
        [SerializeField] private float normalBurstChance = 0.3f;

        [Min(1)]
        [SerializeField] private int normalBurstShotCount = 3;

        [Min(0.01f)]
        [SerializeField] private float normalBurstShotInterval = 0.35f;

        [Min(0.0f)]
        [SerializeField] private float normalSingleChargeDuration = 0.5f;

        [Min(0.1f)]
        [SerializeField] private float normalSingleChargeScaleMultiplier = 1.8f;

        [Min(0.0f)]
        [SerializeField] private float normalSingleChargeForwardOffset = 1.2f;

        [Min(0.0f)]
        [SerializeField] private float normalBurstChargeDuration = 0.2f;

        [Min(0.01f)]
        [SerializeField] private float normalRecoilAnimationSpeed = 2.5f;

        [Min(0.0f)]
        [SerializeField] private float normalRecoilAnimationDuration = 0.25f;

        [Header("Normal Fire Shake")]
        [SerializeField] private Transform normalFireShakeTarget;

        [Min(0.0f)]
        [SerializeField] private float normalFireShakeDuration = 0.15f;

        [Min(0.0f)]
        [SerializeField] private float normalFireShakeDistance = 0.18f;

        [Min(0.01f)]
        [SerializeField] private float normalFireShakeSpeed = 45.0f;

        [Header("Normal Burst Fire Shake")]
        [Min(0.0f)]
        [SerializeField] private float normalBurstFireShakeDuration = 0.07f;

        [Min(0.0f)]
        [SerializeField] private float normalBurstFireShakeDistance = 0.1f;

        [Min(0.01f)]
        [SerializeField] private float normalBurstFireShakeSpeed = 70.0f;

        [Header("Normal Burst Hand Recoil")]
        [SerializeField] private Transform normalBurstHandRecoilTarget;

        [Min(0.0f)]
        [SerializeField] private float normalBurstHandRecoilDistance = 0.12f;

        [Min(0.01f)]
        [SerializeField] private float normalBurstHandRecoilPushDuration = 0.04f;

        [Min(0.01f)]
        [SerializeField] private float normalBurstHandRecoilReturnDuration = 0.06f;

        [Header("Berserk Attack")]
        [Min(0.1f)]
        [SerializeField] private float berserkAttackInterval = 8.0f;

        [Header("Runtime Damage Profile")]
        [Min(0)]
        [SerializeField] private int normalProjectileNexusDamage = 1;

        [Min(0)]
        [SerializeField] private int berserkProjectileNexusDamage = 1;

        [Header("Attack Timing")]
        [Min(0.0f)]
        [SerializeField] private float windupDuration = 1.0f;

        [Min(0.0f)]
        [SerializeField] private float recoveryDuration = 0.5f;

        private BossController bossController;
        private Transform nexus;

        private Coroutine attackCoroutine;
        private Coroutine normalFireCoroutine;
        private Coroutine normalFireShakeCoroutine;
        private Coroutine normalBurstHandRecoilCoroutine;

        private BossDiamondProjectile chargingProjectile;

        private Vector3 normalFireShakeOriginalLocalPosition;
        private Vector3 normalBurstHandRecoilOriginalLocalPosition;

        private float nextAttackTime;
        private float pausedAnimatorSpeed = 1.0f;
        private float recoilAnimatorRestoreSpeed = 1.0f;

        private bool ownsActionLock;
        private bool waitsForNormalAnimationEvent;
        private bool hasFiredCurrentNormalAttack;
        private bool isAnimatorPausedByAttack;
        private bool isAnimatorRecoilSpeedActive;
        private bool hasNormalFireShakeOriginalPosition;
        private bool hasNormalBurstHandRecoilOriginalPosition;

        public bool IsAttacking { get; private set; }

        public void ApplyRuntimeDamageProfile(int normalDamage, int berserkDamage)
        {
            normalProjectileNexusDamage = Mathf.Max(0, normalDamage);
            berserkProjectileNexusDamage = Mathf.Max(0, berserkDamage);
        }

        private void Awake()
        {
            bossController = GetComponent<BossController>();
            FindNexus();
            FindAnimatorIfNeeded();
            FindFireShakeTargetIfNeeded();
            FindBurstHandRecoilTargetIfNeeded();
        }

        private void Start()
        {
            ScheduleNextAttack();
        }

        private void Update()
        {
            if (bossController == null || bossController.IsDead)
            {
                return;
            }

            if (!CanUseDiamondAttack(bossController.CurrentPhase))
            {
                return;
            }

            if (attackCoroutine != null)
            {
                return;
            }

            if (bossController.IsActionRunning)
            {
                return;
            }

            if (Time.time < nextAttackTime)
            {
                return;
            }

            if (nexus == null)
            {
                FindNexus();

                if (nexus == null)
                {
                    nextAttackTime = Time.time + 1.0f;
                    return;
                }
            }

            if (projectilePrefab == null)
            {
                nextAttackTime = Time.time + 1.0f;
                return;
            }

            BossPhase attackPhase = bossController.CurrentPhase;

            if (!HasRequiredAttackPoints(attackPhase))
            {
                nextAttackTime = Time.time + 1.0f;
                return;
            }

            if (!bossController.TryBeginAction())
            {
                return;
            }

            ownsActionLock = true;
            attackCoroutine = StartCoroutine(AttackRoutine(attackPhase));
        }

        private void OnDisable()
        {
            StopRunningCoroutines();
            DestroyChargingProjectile();
            ResumeBossAnimation();
            IsAttacking = false;
            waitsForNormalAnimationEvent = false;
            hasFiredCurrentNormalAttack = false;
            ReleaseActionLock();
        }

        // ?꾩옱 蹂댁뒪 Phase??留욎떠 Normal ?먮컮??怨듦꺽 ?먮뒗 Berserk ???怨듦꺽???ㅽ뻾?쒕떎.
        private IEnumerator AttackRoutine(BossPhase attackPhase)
        {
            IsAttacking = true;
            LookAtNexus();

            if (attackPhase == BossPhase.Normal)
            {
                yield return StartCoroutine(NormalAnimationAttackRoutine());
            }
            else
            {
                yield return new WaitForSeconds(windupDuration);

                if (!CanContinueAttack(attackPhase))
                {
                    FinishAttack();
                    yield break;
                }

                SpawnBerserkFormation();
            }

            if (bossController == null || bossController.IsDead)
            {
                FinishAttack();
                yield break;
            }

            yield return new WaitForSeconds(recoveryDuration);

            FinishAttack();
        }

        // Diamond ?좊땲硫붿씠?섏쓣 ?ъ깮?섍퀬 Animation Event ?먮뒗 fallback ??대컢?먯꽌 諛쒖궗?쒕떎.
        private IEnumerator NormalAnimationAttackRoutine()
        {
            waitsForNormalAnimationEvent = true;
            hasFiredCurrentNormalAttack = false;

            PlayDiamondAnimation();

            float elapsedTime = 0.0f;
            float eventWaitLimit = Mathf.Max(0.01f, windupDuration);

            while (!hasFiredCurrentNormalAttack && elapsedTime < eventWaitLimit)
            {
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            if (!hasFiredCurrentNormalAttack)
            {
                hasFiredCurrentNormalAttack = true;
                PauseBossAnimation();
                yield return StartCoroutine(FireRandomNormalAttack());

                if (CanContinueAttack(BossPhase.Normal))
                {
                    yield return StartCoroutine(PlayNormalRecoilAnimationRoutine());
                }
                else
                {
                    ResumeBossAnimation();
                }
            }

            while (normalFireCoroutine != null)
            {
                yield return null;
            }

            waitsForNormalAnimationEvent = false;
        }

        // Animation Event媛 ?먯쓣 ?대? ?뺥솗???꾨젅?꾩뿉???몄텧?섎뒗 ?⑥닔??
        public void OnDiamondAnimationFire()
        {
            if (!waitsForNormalAnimationEvent)
            {
                return;
            }

            if (hasFiredCurrentNormalAttack)
            {
                return;
            }

            if (!CanContinueAttack(BossPhase.Normal))
            {
                return;
            }

            hasFiredCurrentNormalAttack = true;
            PauseBossAnimation();
            normalFireCoroutine = StartCoroutine(AnimationEventNormalFireRoutine());
        }

        private IEnumerator AnimationEventNormalFireRoutine()
        {
            yield return StartCoroutine(FireRandomNormalAttack());

            if (CanContinueAttack(BossPhase.Normal))
            {
                yield return StartCoroutine(PlayNormalRecoilAnimationRoutine());
            }
            else
            {
                ResumeBossAnimation();
            }

            normalFireCoroutine = null;
        }

        private IEnumerator FireRandomNormalAttack()
        {
            bool useBurst = Random.value < normalBurstChance;

            if (useBurst)
            {
                yield return StartCoroutine(FireNormalBurstFromHand());
            }
            else
            {
                yield return StartCoroutine(FireNormalSingleFromHand());
            }
        }

        private IEnumerator FireNormalSingleFromHand()
        {
            if (!CanContinueAttack(BossPhase.Normal))
            {
                yield break;
            }

            yield return StartCoroutine(SpawnChargedProjectile(handFirePoint, normalSingleChargeDuration, normalSingleChargeScaleMultiplier, normalSingleChargeForwardOffset, normalFireShakeDuration, normalFireShakeDistance, normalFireShakeSpeed));
        }

        // 3?곕컻? 媛?諛쒖궗 ?쒓컙留덈떎 吏㏃? 紐??붾뱾由쇨낵 ????誘몃땲 諛섎룞??諛섎났?쒕떎.
        private IEnumerator FireNormalBurstFromHand()
        {
            int shotCount = Mathf.Max(1, normalBurstShotCount);

            for (int i = 0; i < shotCount; i++)
            {
                if (!CanContinueAttack(BossPhase.Normal))
                {
                    yield break;
                }

                yield return StartCoroutine(SpawnChargedProjectile(handFirePoint, normalBurstChargeDuration, 1.0f, 0.0f, normalBurstFireShakeDuration, normalBurstFireShakeDistance, normalBurstFireShakeSpeed));

                PlayNormalBurstHandRecoil();

                if (i < shotCount - 1)
                {
                    yield return new WaitForSeconds(normalBurstShotInterval);
                }
            }

            while (normalBurstHandRecoilCoroutine != null)
            {
                yield return null;
            }
        }

        // ?먮컮???욎뿉???ㅼ씠?꾨が?쒕? ?ㅼ슦怨? ?꾩슂?섎㈃ 而ㅼ????숈븞 ?욎쑝濡?諛?대궦 ??諛쒖궗?쒕떎.
        private IEnumerator SpawnChargedProjectile(Transform selectedFirePoint, float chargeDuration, float scaleMultiplier, float forwardOffset, float shakeDuration, float shakeDistance, float shakeSpeed)
        {
            Vector3 spawnPosition = selectedFirePoint.position;
            Quaternion spawnRotation = GetNexusRotation(spawnPosition);
            Vector3 chargedPosition = CalculateChargedPosition(spawnPosition, spawnRotation, forwardOffset);
            Transform runtimeRoot = MonsterRuntimeRoot.GetRootOrFallback(transform.parent);

            BossDiamondProjectile projectile = Instantiate(projectilePrefab, spawnPosition, spawnRotation, runtimeRoot);

            projectile.SetNexusDamage(normalProjectileNexusDamage);
            chargingProjectile = projectile;
            GameplaySfxEmitter.TryPlayAt(transform, GameplaySfxCue.BossDiamondCharge, spawnPosition, true);

            Transform projectileTransform = projectile.transform;
            Vector3 originalScale = projectileTransform.localScale;
            Vector3 chargedScale = originalScale * Mathf.Max(0.01f, scaleMultiplier);
            float elapsedTime = 0.0f;
            float duration = Mathf.Max(0.0f, chargeDuration);

            if (duration > 0.0f)
            {
                projectileTransform.localScale = Vector3.zero;
                projectileTransform.position = spawnPosition;

                while (elapsedTime < duration)
                {
                    if (projectile == null)
                    {
                        ClearChargingProjectile(projectile);
                        yield break;
                    }

                    if (!CanContinueAttack(BossPhase.Normal))
                    {
                        DestroyChargingProjectile();
                        yield break;
                    }

                    elapsedTime += Time.deltaTime;
                    float ratio = Mathf.Clamp01(elapsedTime / duration);
                    projectileTransform.localScale = Vector3.Lerp(Vector3.zero, chargedScale, ratio);
                    projectileTransform.position = Vector3.Lerp(spawnPosition, chargedPosition, ratio);
                    yield return null;
                }

                if (projectile == null)
                {
                    ClearChargingProjectile(projectile);
                    yield break;
                }
            }

            projectileTransform.localScale = chargedScale;
            projectileTransform.position = chargedPosition;

            PlayNormalFireShake(shakeDuration, shakeDistance, shakeSpeed);
            ClearChargingProjectile(projectile);
            GameplaySfxEmitter.TryPlayAt(transform, GameplaySfxCue.BossDiamondLaunch, chargedPosition, true);
            projectile.Configure(nexus);
        }

        private Vector3 CalculateChargedPosition(Vector3 spawnPosition, Quaternion spawnRotation, float forwardOffset)
        {
            float offset = Mathf.Max(0.0f, forwardOffset);

            if (offset <= 0.0f)
            {
                return spawnPosition;
            }

            Vector3 forwardDirection = spawnRotation * Vector3.forward;

            if (forwardDirection.sqrMagnitude <= 0.0001f)
            {
                return spawnPosition;
            }

            return spawnPosition + forwardDirection.normalized * offset;
        }

        private void SpawnBerserkFormation()
        {
            int shotCount = Mathf.Max(1, berserkShotCount);
            Transform runtimeRoot = MonsterRuntimeRoot.GetRootOrFallback(transform.parent);
            Vector3 burstPosition = burstCenter != null ? burstCenter.position : transform.position;
            GameplaySfxEmitter.TryPlayAt(transform, GameplaySfxCue.BossDiamondBurstLarge, burstPosition, true);

            for (int i = 0; i < shotCount; i++)
            {
                Vector3 spawnPosition = burstPosition;
                Vector3 formationPosition = CalculateFormationPosition(i);
                Vector3 homingTargetOffset = CalculateHomingTargetOffset(i, shotCount);
                Vector3 formationDirection = formationPosition - spawnPosition;

                Quaternion spawnRotation = formationDirection.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(formationDirection.normalized, Vector3.up) : burstCenter.rotation;

                float standbyDuration = formationHoldDuration + i * berserkLaunchInterval;

                BossDiamondProjectile projectile = Instantiate(projectilePrefab, spawnPosition, spawnRotation, runtimeRoot);

                projectile.SetNexusDamage(berserkProjectileNexusDamage);
                projectile.ConfigureFormationHoming(nexus, formationPosition, standbyDuration, homingTargetOffset);
            }
        }

        private Vector3 CalculateFormationPosition(int index)
        {
            int rows = Mathf.Max(1, formationRowCount);
            int pairIndex = index / 2;
            int side = index % 2 == 0 ? -1 : 1;
            int row = pairIndex % rows;
            int column = pairIndex / rows;

            float centeredRow = row - (rows - 1) * 0.5f;
            float localX = side * formationHorizontalSpacing * (column + 1);
            float localY = centeredRow * formationVerticalSpacing + column * formationWingRise;
            float localZ = -column * formationDepthSpacing;

            Vector3 localOffset = new Vector3(localX, localY, localZ);

            return burstCenter.TransformPoint(localOffset);
        }

        private Vector3 CalculateHomingTargetOffset(int index, int shotCount)
        {
            float minimumRadius = Mathf.Min(berserkTargetMinimumRadius, berserkTargetMaximumRadius);
            float maximumRadius = Mathf.Max(berserkTargetMinimumRadius, berserkTargetMaximumRadius);

            float angleStep = 360.0f / Mathf.Max(1, shotCount);
            float baseAngle = angleStep * index;
            float angle = baseAngle + Random.Range(-berserkTargetAngleJitter, berserkTargetAngleJitter);
            float radius = Random.Range(minimumRadius, maximumRadius);

            Vector3 nexusForward = nexus.position - burstCenter.position;
            nexusForward.y = 0.0f;

            if (nexusForward.sqrMagnitude <= 0.0001f)
            {
                nexusForward = transform.forward;
                nexusForward.y = 0.0f;
            }

            nexusForward.Normalize();

            Vector3 nexusRight = Vector3.Cross(Vector3.up, nexusForward).normalized;
            float angleRadians = angle * Mathf.Deg2Rad;
            Vector3 planarDirection = nexusRight * Mathf.Cos(angleRadians) + nexusForward * Mathf.Sin(angleRadians);

            return planarDirection.normalized * radius;
        }

        private bool HasRequiredAttackPoints(BossPhase attackPhase)
        {
            if (attackPhase == BossPhase.Berserk)
            {
                return burstCenter != null;
            }

            return handFirePoint != null;
        }

        private bool CanContinueAttack(BossPhase attackPhase)
        {
            if (bossController == null || bossController.IsDead)
            {
                return false;
            }

            if (nexus == null)
            {
                FindNexus();
            }

            if (nexus == null || projectilePrefab == null)
            {
                return false;
            }

            return HasRequiredAttackPoints(attackPhase);
        }

        private bool CanUseDiamondAttack(BossPhase phase)
        {
            return phase == BossPhase.Normal || phase == BossPhase.Berserk;
        }

        private Quaternion GetNexusRotation(Vector3 spawnPosition)
        {
            Vector3 direction = nexus.position - spawnPosition;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return transform.rotation;
            }

            return Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void FindNexus()
        {
            GameObject nexusObject = GameObject.Find("Nexus_Core");

            nexus = nexusObject != null ? nexusObject.transform : null;
        }

        private void FindAnimatorIfNeeded()
        {
            if (bossAnimator != null)
            {
                return;
            }

            bossAnimator = GetComponentInChildren<Animator>();
        }

        private void FindFireShakeTargetIfNeeded()
        {
            if (normalFireShakeTarget != null)
            {
                return;
            }

            if (bossAnimator == null)
            {
                return;
            }

            normalFireShakeTarget = bossAnimator.transform;
        }

        private void FindBurstHandRecoilTargetIfNeeded()
        {
            if (normalBurstHandRecoilTarget != null)
            {
                return;
            }

            if (handFirePoint == null)
            {
                return;
            }

            normalBurstHandRecoilTarget = handFirePoint.parent;
        }

        private void PlayDiamondAnimation()
        {
            if (bossAnimator == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(diamondAnimationTriggerName))
            {
                return;
            }

            bossAnimator.ResetTrigger(diamondAnimationTriggerName);
            bossAnimator.SetTrigger(diamondAnimationTriggerName);
        }

        private void PauseBossAnimation()
        {
            if (bossAnimator == null)
            {
                return;
            }

            if (isAnimatorPausedByAttack)
            {
                return;
            }

            if (isAnimatorRecoilSpeedActive)
            {
                RestoreRecoilAnimationSpeed();
            }

            pausedAnimatorSpeed = bossAnimator.speed;
            bossAnimator.speed = 0.0f;
            isAnimatorPausedByAttack = true;
        }

        // 諛쒖궗 ???먯쓣 ?ㅼ떆 媛?몄삤???좊땲硫붿씠??援ш컙留?鍮좊Ⅴ寃??ъ깮?쒕떎.
        private IEnumerator PlayNormalRecoilAnimationRoutine()
        {
            if (bossAnimator == null)
            {
                isAnimatorPausedByAttack = false;
                yield break;
            }

            float restoreSpeed = isAnimatorPausedByAttack ? pausedAnimatorSpeed : bossAnimator.speed;

            isAnimatorPausedByAttack = false;
            recoilAnimatorRestoreSpeed = restoreSpeed;
            isAnimatorRecoilSpeedActive = true;
            bossAnimator.speed = Mathf.Max(0.01f, normalRecoilAnimationSpeed);

            float duration = Mathf.Max(0.0f, normalRecoilAnimationDuration);

            if (duration > 0.0f)
            {
                yield return new WaitForSeconds(duration);
            }

            RestoreRecoilAnimationSpeed();
        }

        private void ResumeBossAnimation()
        {
            if (bossAnimator == null)
            {
                isAnimatorPausedByAttack = false;
                isAnimatorRecoilSpeedActive = false;
                return;
            }

            if (isAnimatorPausedByAttack)
            {
                bossAnimator.speed = pausedAnimatorSpeed;
                isAnimatorPausedByAttack = false;
            }

            if (isAnimatorRecoilSpeedActive)
            {
                RestoreRecoilAnimationSpeed();
            }
        }

        private void RestoreRecoilAnimationSpeed()
        {
            if (bossAnimator != null)
            {
                bossAnimator.speed = recoilAnimatorRestoreSpeed;
            }

            isAnimatorRecoilSpeedActive = false;
        }

        private void PlayNormalFireShake(float shakeDuration, float shakeDistance, float shakeSpeed)
        {
            FindFireShakeTargetIfNeeded();

            if (normalFireShakeTarget == null)
            {
                return;
            }

            if (shakeDuration <= 0.0f || shakeDistance <= 0.0f)
            {
                return;
            }

            StopNormalFireShake();
            normalFireShakeCoroutine = StartCoroutine(NormalFireShakeRoutine(shakeDuration, shakeDistance, shakeSpeed));
        }

        // 諛쒖궗 ?쒓컙 蹂댁뒪 Visual留?吏㏐쾶 ?ㅻ줈 ?붾뱾??諛섎룞??留뚮뱺??
        private IEnumerator NormalFireShakeRoutine(float shakeDuration, float shakeDistance, float shakeSpeed)
        {
            Transform shakeTarget = normalFireShakeTarget;
            normalFireShakeOriginalLocalPosition = shakeTarget.localPosition;
            hasNormalFireShakeOriginalPosition = true;

            Vector3 recoilWorldDirection = GetNormalFireRecoilWorldDirection();
            float elapsedTime = 0.0f;
            float duration = Mathf.Max(0.0f, shakeDuration);
            float distance = Mathf.Max(0.0f, shakeDistance);
            float speed = Mathf.Max(0.01f, shakeSpeed);

            while (elapsedTime < duration)
            {
                if (shakeTarget == null)
                {
                    hasNormalFireShakeOriginalPosition = false;
                    normalFireShakeCoroutine = null;
                    yield break;
                }

                elapsedTime += Time.deltaTime;
                float ratio = Mathf.Clamp01(elapsedTime / duration);
                float fade = 1.0f - ratio;
                float wave = Mathf.Sin(elapsedTime * speed);
                Vector3 worldOffset = recoilWorldDirection * wave * distance * fade;
                Vector3 localOffset = shakeTarget.parent != null ? shakeTarget.parent.InverseTransformVector(worldOffset) : worldOffset;

                shakeTarget.localPosition = normalFireShakeOriginalLocalPosition + localOffset;
                yield return null;
            }

            if (shakeTarget != null)
            {
                shakeTarget.localPosition = normalFireShakeOriginalLocalPosition;
            }

            hasNormalFireShakeOriginalPosition = false;
            normalFireShakeCoroutine = null;
        }

        private void PlayNormalBurstHandRecoil()
        {
            FindBurstHandRecoilTargetIfNeeded();

            if (normalBurstHandRecoilTarget == null)
            {
                return;
            }

            if (normalBurstHandRecoilDistance <= 0.0f)
            {
                return;
            }

            StopNormalBurstHandRecoil();
            normalBurstHandRecoilCoroutine = StartCoroutine(NormalBurstHandRecoilRoutine());
        }

        // 3?곕컻 諛쒖궗留덈떎 ????蹂몄쓣 吏㏐쾶 ?ㅻ줈 諛?덈떎媛 ?먯쐞移섏떆?⑤떎.
        private IEnumerator NormalBurstHandRecoilRoutine()
        {
            Transform recoilTarget = normalBurstHandRecoilTarget;
            normalBurstHandRecoilOriginalLocalPosition = recoilTarget.localPosition;
            hasNormalBurstHandRecoilOriginalPosition = true;

            Vector3 recoilWorldDirection = GetNormalFireRecoilWorldDirection();
            Vector3 worldOffset = recoilWorldDirection * Mathf.Max(0.0f, normalBurstHandRecoilDistance);
            Vector3 localOffset = recoilTarget.parent != null ? recoilTarget.parent.InverseTransformVector(worldOffset) : worldOffset;

            Vector3 startPosition = normalBurstHandRecoilOriginalLocalPosition;
            Vector3 pushedPosition = startPosition + localOffset;

            float pushDuration = Mathf.Max(0.01f, normalBurstHandRecoilPushDuration);
            float returnDuration = Mathf.Max(0.01f, normalBurstHandRecoilReturnDuration);

            float elapsedTime = 0.0f;

            while (elapsedTime < pushDuration)
            {
                if (recoilTarget == null)
                {
                    hasNormalBurstHandRecoilOriginalPosition = false;
                    normalBurstHandRecoilCoroutine = null;
                    yield break;
                }

                elapsedTime += Time.deltaTime;
                float ratio = Mathf.Clamp01(elapsedTime / pushDuration);
                recoilTarget.localPosition = Vector3.Lerp(startPosition, pushedPosition, ratio);
                yield return null;
            }

            elapsedTime = 0.0f;

            while (elapsedTime < returnDuration)
            {
                if (recoilTarget == null)
                {
                    hasNormalBurstHandRecoilOriginalPosition = false;
                    normalBurstHandRecoilCoroutine = null;
                    yield break;
                }

                elapsedTime += Time.deltaTime;
                float ratio = Mathf.Clamp01(elapsedTime / returnDuration);
                recoilTarget.localPosition = Vector3.Lerp(pushedPosition, startPosition, ratio);
                yield return null;
            }

            if (recoilTarget != null)
            {
                recoilTarget.localPosition = startPosition;
            }

            hasNormalBurstHandRecoilOriginalPosition = false;
            normalBurstHandRecoilCoroutine = null;
        }

        private Vector3 GetNormalFireRecoilWorldDirection()
        {
            if (nexus == null)
            {
                return -transform.forward;
            }

            Vector3 direction = transform.position - nexus.position;
            direction.y = 0.0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return -transform.forward;
            }

            return direction.normalized;
        }

        private void StopRunningCoroutines()
        {
            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
                attackCoroutine = null;
            }

            if (normalFireCoroutine != null)
            {
                StopCoroutine(normalFireCoroutine);
                normalFireCoroutine = null;
            }

            StopNormalFireShake();
            StopNormalBurstHandRecoil();
        }

        private void StopNormalFireShake()
        {
            if (normalFireShakeCoroutine != null)
            {
                StopCoroutine(normalFireShakeCoroutine);
                normalFireShakeCoroutine = null;
            }

            if (normalFireShakeTarget != null && hasNormalFireShakeOriginalPosition)
            {
                normalFireShakeTarget.localPosition = normalFireShakeOriginalLocalPosition;
            }

            hasNormalFireShakeOriginalPosition = false;
        }

        private void StopNormalBurstHandRecoil()
        {
            if (normalBurstHandRecoilCoroutine != null)
            {
                StopCoroutine(normalBurstHandRecoilCoroutine);
                normalBurstHandRecoilCoroutine = null;
            }

            if (normalBurstHandRecoilTarget != null && hasNormalBurstHandRecoilOriginalPosition)
            {
                normalBurstHandRecoilTarget.localPosition = normalBurstHandRecoilOriginalLocalPosition;
            }

            hasNormalBurstHandRecoilOriginalPosition = false;
        }

        private void ClearChargingProjectile(BossDiamondProjectile projectile)
        {
            if (chargingProjectile != projectile)
            {
                return;
            }

            chargingProjectile = null;
        }

        private void DestroyChargingProjectile()
        {
            if (chargingProjectile == null)
            {
                return;
            }

            Destroy(chargingProjectile.gameObject);
            chargingProjectile = null;
        }

        private void LookAtNexus()
        {
            if (nexus == null)
            {
                return;
            }

            Vector3 direction = nexus.position - transform.position;
            direction.y = 0.0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void ScheduleNextAttack()
        {
            nextAttackTime = Time.time + GetAttackInterval();
        }

        private float GetAttackInterval()
        {
            if (bossController != null && bossController.CurrentPhase == BossPhase.Berserk)
            {
                return berserkAttackInterval;
            }

            return normalAttackInterval;
        }

        private void FinishAttack()
        {
            ResumeBossAnimation();
            StopNormalFireShake();
            StopNormalBurstHandRecoil();
            waitsForNormalAnimationEvent = false;
            hasFiredCurrentNormalAttack = false;
            IsAttacking = false;
            ReleaseActionLock();
            ScheduleNextAttack();
            attackCoroutine = null;
        }

        private void ReleaseActionLock()
        {
            if (!ownsActionLock)
            {
                return;
            }

            if (bossController != null)
            {
                bossController.EndAction();
            }

            ownsActionLock = false;
        }
    }
}
