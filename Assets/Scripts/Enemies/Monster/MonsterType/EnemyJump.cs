using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace TeamProject01.Gameplay
{
    [RequireComponent(typeof(EnemyMovement))]
    public sealed class EnemyJump : MonoBehaviour // ?멸렇癒쇳듃瑜?媛먯??섎㈃ ?욎쑝濡??먰봽?쒕떎.
    {
        [Header("Jump Setting")]
        [Min(0.0f)]
        [SerializeField] private float jumpHeight = 2.0f; // ?먰봽 ?믪씠

        [Min(0.05f)]
        [SerializeField] private float jumpDuration = 0.5f; // ?먰봽 ?쒓컙

        // 議곗꽦?먯텛媛-0626 - 李⑹? ???낇겕由??먯꽭瑜??좎??섎ŉ ?대룞怨?怨듦꺽???좎떆 硫덉텧 ?쒓컙
        [Min(0.0f)]
        [SerializeField] private float landingRecoveryDuration = 0.25f;

        [Header("Segment Detection")]
        [Min(0.1f)]
        [SerializeField] private float segmentDetectDistance = 2.0f; // ?욎そ ?멸렇癒쇳듃 媛먯? 嫄곕━

        [Min(0.1f)]
        [SerializeField] private float jumpLandingDistance = 5.0f; // ?멸렇癒쇳듃瑜??섏? ??異붽? 李⑹? 嫄곕━

        [Min(0.0f)]
        [SerializeField] private float jumpCooldown = 1.5f; // 李⑹? ???ъ젏???湲곗떆媛?

        [Header("Off Mesh Link Jump")]
        [SerializeField] private bool useOffMeshLinkJump = false; // NavMesh Off-Mesh Link에 의한 지형 점프를 사용할지

        [Header("Landing Shockwave")]
        [Min(0.1f)]
        [SerializeField] private float shockwaveRadius = 7.0f; // 李⑹? 異⑷꺽??踰붿쐞

        [Min(0.0f)]
        [SerializeField] private float shockwavePushDistance = 3.0f; // ?멸렇癒쇳듃 諛由?嫄곕━

        [Min(0.01f)]
        [SerializeField] private float shockwaveRecoveryDuration = 1.5f; // ?먮옒 寃쎈줈濡?蹂듦뎄?섎뒗 ?쒓컙

        // 議곗꽦?먯텛媛-0630 - ?멸렇癒쇳듃 ?먰봽 李⑹? ?쒓컙 ?낃컝?쇱쭚 VFX瑜??앹꽦?섍린 ?꾪븳 ?ㅼ젙
        [Header("Landing Crack VFX")]
        [SerializeField] private GameObject landingCrackVfxPrefab; // 議곗꽦?먯텛媛-0630 - 李⑹? ?쒓컙 ?앹꽦???낃컝?쇱쭚 VFX Prefab

        [Min(0.0f)]
        [SerializeField] private float landingCrackGroundHeight = 0.03f; // 議곗꽦?먯텛媛-0630 - VFX媛 諛붾떏??臾삵엳吏 ?딅룄濡??щ┫ ?믪씠

        [Min(0.1f)]
        [SerializeField] private float landingCrackScale = 0.65f; // 議곗꽦?먯텛媛-0630 - ?섎━??紐ъ뒪?곗슜 ?낃컝?쇱쭚 VFX ?ш린 諛곗쑉

        [Min(0.01f)]
        [SerializeField] private float landingCrackLifeTime = 2.0f; // 議곗꽦?먯텛媛-0630 - ?앹꽦???낃컝?쇱쭚 VFX ?쒓굅 ?쒓컙

        ////// ?덇굔以異붽?-0622 - EnemyJumpTest???대룞 Script Component 李몄“ 援ъ“瑜?媛?몄삩??
        private EnemyMovement enemyMovement;
        private NavMeshAgent navAgent;
        private EnemyHealth enemyHealth;
        private EnemySupportDebuffState supportDebuffState;
        private Coroutine jumpRoutine;
        private float cooldownTimer;
        private bool movementDisabledByStatusCancel;

        // 議곗꽦?먯텛媛-0626 - ?먰봽 ?좊땲硫붿씠??Bridge媛 ?꾩옱 ?먰봽 ?곹깭瑜??쎌쓣 ???덈룄濡?怨듦컻?쒕떎.
        public bool IsJumping { get; private set; }
        public event System.Action<Vector3> Landed;

        ////// ?덇굔以異붽?-0622 - SegmentBlocker???쒖꽦 紐⑸줉???쎄린 ?꾪븳 李몄“瑜?媛?몄삩??
        private static FieldInfo activeBlockersField;

        private void Awake()
        {
            ////// ?덇굔以異붽?-0622 - 媛숈? GameObject???대룞怨?AI Navigation Component瑜?李얜뒗??
            enemyMovement = GetComponent<EnemyMovement>();
            navAgent = GetComponent<NavMeshAgent>();
            enemyHealth = GetComponent<EnemyHealth>();
            supportDebuffState = GetComponent<EnemySupportDebuffState>();

            ////// ?덇굔以異붽?-0622 - SegmentBlocker???쒖꽦 ?멸렇癒쇳듃 紐⑸줉??李얜뒗??
            if (activeBlockersField == null)
            {
                activeBlockersField = typeof(SegmentBlocker).GetField("ActiveBlockers", BindingFlags.NonPublic | BindingFlags.Static);
            }

            ////// ?덇굔以異붽?-0622 - Off-Mesh Link ?대룞? EnemyJump媛 吏곸젒 泥섎━?쒕떎.
            if (navAgent != null)
            {
                navAgent.autoTraverseOffMeshLink = false;
            }
        }

        private void OnEnable()
        {
            IsJumping = false;
            jumpRoutine = null;
            cooldownTimer = 0.0f;
            movementDisabledByStatusCancel = false;
        }

        private void Update()
        {
            if (IsDead())
            {
                CancelJumpBecauseDead();
                return;
            }

            if (IsJumpBlockedByStatus())
            {
                CancelJumpBecauseStatus();
                return;
            }

            RestoreMovementAfterStatusCancelIfNeeded();

            if (jumpRoutine != null) // 이미 점프 중이라면
            {
                return;
            }

            cooldownTimer -= Time.deltaTime;

            ////// 안건준추가-0622 - NavMeshAgent가 Off-Mesh Link에 도착하면 지형 점프를 시작한다.
            if (useOffMeshLinkJump && navAgent != null && navAgent.enabled && navAgent.isOnNavMesh && navAgent.isOnOffMeshLink)
            {
                jumpRoutine = StartCoroutine(JumpOffMeshLink());
                return;
            }

            if (cooldownTimer > 0.0f) // ?ъ젏???湲곗떆媛꾩씠 ?⑥븯?ㅻ㈃
            {
                return;
            }

            ////// ?덇굔以異붽?-0622 - ?욎そ ?멸렇癒쇳듃瑜?媛먯??섎㈃ ?멸렇癒쇳듃 ?덈㉧濡??먰봽?쒕떎.
            if (IsSegmentAhead(out Vector3 landingPoint))
            {
                jumpRoutine = StartCoroutine(JumpOverSegment(landingPoint));
            }
        }

        private void OnDisable()
        {
            // ?먰봽 ?꾩쨷 鍮꾪솢?깊솕?섏뼱???대룞 ?곹깭媛 ?⑥? ?딅룄濡?蹂듦뎄?쒕떎.
            if (jumpRoutine != null)
            {
                StopCoroutine(jumpRoutine);
                jumpRoutine = null;
            }

            // 議곗꽦?먯텛媛-0626 - 鍮꾪솢?깊솕?????먰봽 ?좊땲硫붿씠???곹깭媛 ?⑥? ?딅룄濡??댁젣?쒕떎.
            IsJumping = false;
            movementDisabledByStatusCancel = false;

            SetEnemyMovementEnabled(true);

            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.updatePosition = true;
                navAgent.updateRotation = true;
                navAgent.isStopped = false;
            }
        }

        ////// ?덇굔以異붽?-0622 - Off-Mesh Link???쒖옉?먯뿉???앹젏源뚯? ?щЪ?좎쑝濡??대룞?쒕떎.
        private IEnumerator JumpOffMeshLink()
        {
            if (IsDead())
            {
                CancelJumpBecauseDead();
                yield break;
            }

            if (IsJumpBlockedByStatus())
            {
                CancelJumpBecauseStatus();
                yield break;
            }

            // 조성원추가-0626 - 지형 점프가 시작됐다고 저장한다.
            IsJumping = true;
            movementDisabledByStatusCancel = false;

            SetEnemyMovementEnabled(false);

            navAgent.isStopped = true;
            navAgent.updatePosition = false;
            navAgent.updateRotation = false;

            OffMeshLinkData link = navAgent.currentOffMeshLinkData;
            Vector3 from = transform.position;
            Vector3 to = link.endPos + Vector3.up * navAgent.baseOffset;

            yield return ArcMove(from, to, jumpHeight, jumpDuration);

            if (IsDead())
            {
                CancelJumpBecauseDead();
                yield break;
            }

            if (IsJumpBlockedByStatus())
            {
                CancelJumpBecauseStatus();
                yield break;
            }

            transform.position = to;
            Landed?.Invoke(transform.position);

            if (navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.CompleteOffMeshLink();
                navAgent.updatePosition = true;
                navAgent.updateRotation = true;

                // 議곗꽦?먯닔??0626 - 李⑹? ?뚮났???앸궇 ?뚭퉴吏 NavMeshAgent???뺤? ?곹깭瑜??좎??쒕떎.
                navAgent.isStopped = true;
            }

            // 조성원추가-0626 - 착지 후 남은 웅크린 애니메이션 동안 이동하지 않는다.
            yield return WaitLandingRecovery();

            if (IsDead())
            {
                CancelJumpBecauseDead();
                yield break;
            }

            if (IsJumpBlockedByStatus())
            {
                CancelJumpBecauseStatus();
                yield break;
            }

            cooldownTimer = jumpCooldown;

            // 議곗꽦?먯텛媛-0626 - 李⑹? ?뚮났源뚯? ?앸궃 ???먰봽 ?곹깭瑜??댁젣?쒕떎.
            IsJumping = false;

            SetEnemyMovementEnabled(true);

            // 議곗꽦?먯텛媛-0626 - 李⑹? ?뚮났???앸궃 ??NavMeshAgent ?대룞???ㅼ떆 ?덉슜?쒕떎.
            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = false;
            }

            jumpRoutine = null;
        }

        ////// ?덇굔以異붽?-0622 - 紐ъ뒪???욎そ???먰봽???멸렇癒쇳듃媛 ?덈뒗吏 ?뺤씤?쒕떎.
        private bool IsSegmentAhead(out Vector3 landingPoint)
        {
            landingPoint = Vector3.zero;

            if (IsDead() || IsJumpBlockedByStatus())
            {
                return false;
            }

            var blockers = activeBlockersField?.GetValue(null) as System.Collections.Generic.List<SegmentBlocker>;

            if (blockers == null || blockers.Count == 0)
            {
                return false;
            }

            Vector3 myPosition = transform.position;
            Vector3 forward;

            ////// ?덇굔以異붽?-0622 - NavMeshAgent ?띾룄媛 ?덉쑝硫??대룞 諛⑺뼢?쇰줈 ?ъ슜?쒕떎.
            if (navAgent != null && navAgent.velocity.sqrMagnitude > 0.01f)
            {
                forward = navAgent.velocity;
            }
            else
            {
                forward = transform.forward;
            }

            forward.y = 0.0f;

            if (forward.sqrMagnitude < 0.001f)
            {
                return false;
            }

            forward.Normalize();

            for (int i = 0; i < blockers.Count; i++)
            {
                SegmentBlocker blocker = blockers[i];

                if (blocker == null)
                {
                    continue;
                }

                if (!blocker.isActiveAndEnabled || !blocker.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 toBlocker = blocker.transform.position - myPosition;

                toBlocker.y = 0.0f;

                float forwardDistance = Vector3.Dot(toBlocker, forward);

                if (forwardDistance < 0.0f)
                {
                    continue;
                }

                Vector3 lateralOffset = toBlocker - forward * forwardDistance;

                float monsterRadius = navAgent != null ? navAgent.radius : 0.5f;

                float combinedRadius = blocker.BlockRadius + monsterRadius;

                if (forwardDistance > segmentDetectDistance || lateralOffset.magnitude >= combinedRadius)
                {
                    continue;
                }

                landingPoint = blocker.transform.position + forward * (combinedRadius + jumpLandingDistance);

                landingPoint.y = myPosition.y;

                return true;
            }

            return false;
        }

        ////// ?덇굔以異붽?-0622 - EnemyMovement瑜?硫덉텛怨??멸렇癒쇳듃 ?덈㉧濡??먰봽?쒕떎.
        private IEnumerator JumpOverSegment(Vector3 landingPoint)
        {
            if (IsDead())
            {
                CancelJumpBecauseDead();
                yield break;
            }

            if (IsJumpBlockedByStatus())
            {
                CancelJumpBecauseStatus();
                yield break;
            }

            // 조성원추가-0626 - 세그먼트 점프가 시작됐다고 저장한다.
            IsJumping = true;
            movementDisabledByStatusCancel = false;

            SetEnemyMovementEnabled(false);

            bool canControlAgent = navAgent != null && navAgent.enabled && navAgent.isOnNavMesh;

            if (canControlAgent)
            {
                navAgent.isStopped = true;
                navAgent.updatePosition = false;
                navAgent.updateRotation = false;
            }

            Vector3 from = transform.position;

            yield return ArcMove(from, landingPoint, jumpHeight, jumpDuration);

            if (IsDead())
            {
                CancelJumpBecauseDead();
                yield break;
            }

            if (IsJumpBlockedByStatus())
            {
                CancelJumpBecauseStatus();
                yield break;
            }

            transform.position = landingPoint;

            ////// ?덇굔以異붽?-0622 - 李⑹? ?꾩튂 洹쇱쿂??NavMesh ?꾩튂濡?Agent瑜?留욎텣??
            if (canControlAgent)
            {
                if (NavMesh.SamplePosition(landingPoint, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                    navAgent.Warp(hit.position);
                }

                navAgent.updatePosition = true;
                navAgent.updateRotation = true;

                // 議곗꽦?먯닔??0626 - 李⑹? ?뚮났???앸궇 ?뚭퉴吏 NavMeshAgent???뺤? ?곹깭瑜??좎??쒕떎.
                navAgent.isStopped = true;
            }

            SpawnLandingCrackVfx(transform.position); // 議곗꽦?먯텛媛-0630 - 李⑹? ?꾩튂媛 ?뺤젙?????낃컝?쇱쭚 VFX瑜??앹꽦?쒕떎.
            Landed?.Invoke(transform.position);

            ApplyLandingShockwave(); // ?멸렇癒쇳듃 ?먰봽 李⑹? 吏?먯뿉 異⑷꺽??諛쒖깮

            // 조성원추가-0626 - 착지 후 남은 웅크린 애니메이션 동안 이동하지 않는다.
            yield return WaitLandingRecovery();

            if (IsDead())
            {
                CancelJumpBecauseDead();
                yield break;
            }

            if (IsJumpBlockedByStatus())
            {
                CancelJumpBecauseStatus();
                yield break;
            }

            cooldownTimer = jumpCooldown;

            // 議곗꽦?먯텛媛-0626 - 李⑹? ?뚮났源뚯? ?앸궃 ???먰봽 ?곹깭瑜??댁젣?쒕떎.
            IsJumping = false;

            SetEnemyMovementEnabled(true);

            // 議곗꽦?먯텛媛-0626 - 李⑹? ?뚮났???앸궃 ??NavMeshAgent ?대룞???ㅼ떆 ?덉슜?쒕떎.
            if (canControlAgent && navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = false;
            }

            jumpRoutine = null;
        }

        // 議곗꽦?먯텛媛-0630 - ?멸렇癒쇳듃 ?먰봽 李⑹? ?꾩튂???낃컝?쇱쭚 VFX瑜??앹꽦?쒕떎.
        private void SpawnLandingCrackVfx(Vector3 position)
        {
            SpawnOneShotVfx(landingCrackVfxPrefab, position, landingCrackGroundHeight, landingCrackScale, landingCrackLifeTime);
        }

        // 議곗꽦?먯텛媛-0630 - ?⑤컻??VFX瑜??앹꽦?섍퀬 吏???쒓컙 ???쒓굅?쒕떎.
        private void SpawnOneShotVfx(GameObject prefab, Vector3 position, float groundHeight, float scaleMultiplier, float lifeTime)
        {
            if (prefab == null)
            {
                return;
            }

            Vector3 spawnPosition = position;
            spawnPosition.y += groundHeight;

            GameObject vfx = Instantiate(prefab, spawnPosition, Quaternion.identity, transform.parent);
            vfx.transform.localScale = vfx.transform.localScale * scaleMultiplier;
            Destroy(vfx, lifeTime);
        }

        private void ApplyLandingShockwave() // 李⑹? 二쇰????곌껐 ?멸렇癒쇳듃瑜?諛붽묑履쎌쑝濡?誘쇰떎.
        {
            MonsterInteractionApi.RequestSegmentShockwave(transform.position, shockwaveRadius, shockwavePushDistance, shockwaveRecoveryDuration);
        }

        ////// ?덇굔以異붽?-0622 - ?쒖옉?먭낵 李⑹????ъ씠瑜??щЪ?좎쑝濡??대룞?쒕떎.
        private IEnumerator ArcMove(Vector3 from, Vector3 to, float height, float duration)
        {
            float elapsed = 0.0f;

            while (elapsed < duration)
            {
                if (IsDead())
                {
                    CancelJumpBecauseDead();
                    yield break;
                }

                if (IsJumpBlockedByStatus())
                {
                    CancelJumpBecauseStatus();
                    yield break;
                }

                elapsed += Time.deltaTime;

                float progress = Mathf.Clamp01(elapsed / duration);

                Vector3 position = Vector3.Lerp(from, to, progress);

                position.y += Mathf.Sin(progress * Mathf.PI) * height;

                transform.position = position;

                Vector3 direction = to - from;
                direction.y = 0.0f;

                if (direction.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }

                yield return null;
            }

            transform.position = to;
        }

        private IEnumerator WaitLandingRecovery()
        {
            if (landingRecoveryDuration <= 0.0f)
            {
                yield break;
            }

            float elapsed = 0.0f;

            while (elapsed < landingRecoveryDuration)
            {
                if (IsDead())
                {
                    CancelJumpBecauseDead();
                    yield break;
                }

                if (IsJumpBlockedByStatus())
                {
                    CancelJumpBecauseStatus();
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        ////// 안건준추가-0622 - 점프 중에는 EnemyMovement를 끄고 착지 후 다시 켠다.
        private void SetEnemyMovementEnabled(bool enabled)
        {
            if (enemyMovement != null)
            {
                enemyMovement.enabled = enabled;
            }
        }

        private bool IsDead()
        {
            return enemyHealth != null && enemyHealth.IsDead;
        }

        private bool IsJumpBlockedByStatus()
        {
            if (supportDebuffState == null)
            {
                TryGetComponent(out supportDebuffState);
            }

            return supportDebuffState != null && supportDebuffState.IsFrozen;
        }

        private void CancelJumpBecauseDead()
        {
            if (jumpRoutine != null)
            {
                StopCoroutine(jumpRoutine);
                jumpRoutine = null;
            }

            IsJumping = false;
            movementDisabledByStatusCancel = false;
            SetEnemyMovementEnabled(false);

            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.updatePosition = true;
                navAgent.updateRotation = true;
                navAgent.isStopped = true;
            }
        }

        private void CancelJumpBecauseStatus()
        {
            bool hadActiveJump = jumpRoutine != null || IsJumping;

            if (jumpRoutine != null)
            {
                StopCoroutine(jumpRoutine);
                jumpRoutine = null;
            }

            IsJumping = false;

            if (!hadActiveJump)
            {
                return;
            }

            cooldownTimer = Mathf.Max(cooldownTimer, jumpCooldown);
            movementDisabledByStatusCancel = true;
            SetEnemyMovementEnabled(false);

            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.updatePosition = true;
                navAgent.updateRotation = true;
                navAgent.isStopped = true;

                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                    navAgent.Warp(hit.position);
                }
            }
        }

        private void RestoreMovementAfterStatusCancelIfNeeded()
        {
            if (!movementDisabledByStatusCancel)
            {
                return;
            }

            movementDisabledByStatusCancel = false;
            SetEnemyMovementEnabled(true);

            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.updatePosition = true;
                navAgent.updateRotation = true;
                navAgent.isStopped = false;
            }
        }
    }
}
