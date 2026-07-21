// 안건준 추가 - 0622
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace TeamProject01.Gameplay
{
    /// <summary>
    /// 기존 EnemyMovement가 붙은 몬스터 프리팹에 추가해서 사용.
    /// 앞쪽에 컨보이 세그먼트가 감지되면 EnemyMovement를 잠시 멈추고 포물선 점프 후 재개한다.
    /// NavMeshAgent가 있으면 Off-Mesh Link 지형 점프도 처리한다.
    /// </summary>
    public sealed class EnemyJumpTest : MonoBehaviour
    {
        [Header("점프 설정")]
        [Min(0f)]
        [SerializeField] private float jumpHeight = 2f;

        [Min(0.05f)]
        [SerializeField] private float jumpDuration = 0.5f;

        [Header("세그먼트 감지")]
        [Tooltip("앞 이 거리 안에 세그먼트가 있으면 점프")]
        [Min(0.1f)]
        [SerializeField] private float segmentDetectDistance = 2f;

        [Tooltip("착지 후 재점프 금지 시간")]
        [Min(0f)]
        [SerializeField] private float jumpCooldown = 1.5f;

        private EnemyMovement enemyMovement; // 안건준 추가 - 0622 : 점프 중 EnemyMovement 일시 정지용 참조
        private NavMeshAgent navAgent;
        private Coroutine jumpRoutine;
        private float cooldownTimer;

        // SegmentBlocker.ActiveBlockers 리플렉션 접근
        private static FieldInfo activeBlockersField;

        private void Awake()
        {
            enemyMovement = GetComponent<EnemyMovement>();
            navAgent = GetComponent<NavMeshAgent>();

            if (activeBlockersField == null)
            {
                activeBlockersField = typeof(SegmentBlocker).GetField(
                    "ActiveBlockers",
                    BindingFlags.NonPublic | BindingFlags.Static);
            }

            // NavMeshAgent 있으면 Off-Mesh Link 직접 처리
            if (navAgent != null)
            {
                navAgent.autoTraverseOffMeshLink = false;
            }
        }

        private void Update()
        {
            if (jumpRoutine != null)
            {
                return;
            }

            cooldownTimer -= Time.deltaTime;

            // NavMeshAgent Off-Mesh Link 감지 (지형 점프)
            if (navAgent != null && navAgent.isOnNavMesh && navAgent.isOnOffMeshLink)
            {
                jumpRoutine = StartCoroutine(JumpOffMeshLink());
                return;
            }

            if (cooldownTimer > 0f)
            {
                return;
            }

            // 세그먼트 감지 점프
            if (IsSegmentAhead(out Vector3 landingPoint))
            {
                jumpRoutine = StartCoroutine(JumpOverSegment(landingPoint));
            }
        }

        // ─── Off-Mesh Link 점프 (NavMeshAgent 사용 시) ──────────────────────

        private IEnumerator JumpOffMeshLink()
        {
            SetEnemyMovementEnabled(false);
            navAgent.isStopped = true;
            navAgent.updatePosition = false;
            navAgent.updateRotation = false;

            OffMeshLinkData link = navAgent.currentOffMeshLinkData;
            Vector3 from = transform.position;
            Vector3 to = link.endPos + Vector3.up * navAgent.baseOffset;

            yield return ArcMove(from, to, jumpHeight, jumpDuration);

            transform.position = to;
            navAgent.CompleteOffMeshLink();
            navAgent.updatePosition = true;
            navAgent.updateRotation = true;
            navAgent.isStopped = false;

            cooldownTimer = jumpCooldown;
            jumpRoutine = null;
            SetEnemyMovementEnabled(true);
        }

        // ─── 세그먼트 감지 점프 ─────────────────────────────────────────────

        private bool IsSegmentAhead(out Vector3 landingPoint)
        {
            landingPoint = Vector3.zero;

            var blockers = activeBlockersField?.GetValue(null)
                as System.Collections.Generic.List<SegmentBlocker>;

            if (blockers == null || blockers.Count == 0)
            {
                return false;
            }

            Vector3 myPos = transform.position;

            // 이동 방향: NavMeshAgent velocity 우선, 없으면 transform.forward
            Vector3 forward = Vector3.zero;
            if (navAgent != null && navAgent.velocity.sqrMagnitude > 0.01f)
            {
                forward = navAgent.velocity;
            }
            else
            {
                forward = transform.forward;
            }

            forward.y = 0f;
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

                Vector3 toBlocker = blocker.transform.position - myPos;
                toBlocker.y = 0f;

                float dot = Vector3.Dot(toBlocker, forward);
                if (dot < 0f)
                {
                    continue; // 뒤쪽 무시
                }

                Vector3 lateral = toBlocker - forward * dot;
                float agentRadius = navAgent != null ? navAgent.radius : 0.5f;
                float combinedRadius = blocker.BlockRadius + agentRadius;

                if (dot <= segmentDetectDistance && lateral.magnitude < combinedRadius)
                {
                    landingPoint = blocker.transform.position + forward * (combinedRadius + 0.8f);
                    landingPoint.y = myPos.y;
                    return true;
                }
            }

            return false;
        }

        private IEnumerator JumpOverSegment(Vector3 landingPoint)
        {
            SetEnemyMovementEnabled(false);

            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = true;
                navAgent.updatePosition = false;
                navAgent.updateRotation = false;
            }

            Vector3 from = transform.position;

            yield return ArcMove(from, landingPoint, jumpHeight, jumpDuration);

            transform.position = landingPoint;

            if (navAgent != null && navAgent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(landingPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    navAgent.Warp(hit.position);
                }

                navAgent.updatePosition = true;
                navAgent.updateRotation = true;
                navAgent.isStopped = false;
            }

            cooldownTimer = jumpCooldown;
            jumpRoutine = null;
            SetEnemyMovementEnabled(true);
        }

        // ─── 공통 포물선 이동 ────────────────────────────────────────────────

        private IEnumerator ArcMove(Vector3 from, Vector3 to, float height, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                Vector3 pos = Vector3.Lerp(from, to, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * height;
                transform.position = pos;

                Vector3 dir = to - from;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                }

                yield return null;
            }
        }

        // ─── EnemyMovement 일시 정지/재개 ───────────────────────────────────
        // 안건준 추가 - 0622 : EnemyMovement.cs 자체는 수정하지 않고 enabled 토글로 점프 중 이동 제어
        private void SetEnemyMovementEnabled(bool enabled)
        {
            if (enemyMovement != null)
            {
                enemyMovement.enabled = enabled; // 안건준 추가 - 0622 : 점프 시작 시 false, 착지 후 true
            }
        }
    }
}
