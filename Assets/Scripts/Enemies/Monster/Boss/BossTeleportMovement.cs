using System.Collections;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class BossTeleportMovement : MonoBehaviour
    {
        [Header("Teleport Interval")]
        [Min(0.1f)]
        [SerializeField] private float normalInterval = 8.0f; // Normal 상태의 순간이동 간격

        [Min(0.1f)]
        [SerializeField] private float aggressiveInterval = 7.0f; // Aggressive 상태의 순간이동 간격

        [Min(0.1f)]
        [SerializeField] private float rageInterval = 6.0f; // Rage 상태의 순간이동 간격

        [Min(0.1f)]
        [SerializeField] private float berserkInterval = 5.0f; // Berserk 상태의 순간이동 간격

        [Header("Teleport Timing")]
        [Min(0.0f)]
        [SerializeField] private float telegraphDuration = 1.0f; // 도착 위치를 미리 보여주는 시간

        [Min(0.0f)]
        [SerializeField] private float disappearDuration = 0.15f; // 보스가 사라져 있는 시간

        [Min(0.0f)]
        [SerializeField] private float recoveryDuration = 0.5f; // 순간이동 후 다음 행동까지의 대기시간

        [Header("Teleport Position")]
        [Min(0.1f)]
        [SerializeField] private float minimumTeleportRadius = 12.0f; // Nexus로부터 순간이동할 수 있는 최소 거리

        [Min(0.1f)]
        [SerializeField] private float maximumTeleportRadius = 22.0f; // Nexus로부터 순간이동할 수 있는 최대 거리

        [Min(0.1f)]
        [SerializeField] private float minimumMoveDistance = 8.0f; // 현재 위치에서 최소한 이동해야 하는 거리

        [Min(0.1f)]
        [SerializeField] private float convoySafeDistance = 6.0f; // 컨보이 머리 근처에 나타나지 않도록 하는 안전거리

        [Min(0.0f)]
        [SerializeField] private float groundHeight = 0.0f; // 보스 Root의 바닥 높이 보정값

        [Min(1)]
        [SerializeField] private int positionSearchAttempts = 20; // 적합한 위치를 찾기 위해 반복할 최대 횟수

        [Header("Visual")]
        [SerializeField] private GameObject bossVisualRoot; // 순간이동할 때 숨길 BossVisual 오브젝트

        [SerializeField] private Transform teleportMarker; // 순간이동 도착 위치를 미리 표시할 오브젝트

        [Min(0.0f)]
        [SerializeField] private float markerHeight = 0.05f; // 마커가 바닥에 파묻히지 않도록 올릴 높이

        private BossController bossController; // 보스 상태와 행동 잠금을 관리하는 Script Component
        private Transform nexus; // 순간이동 위치의 중심이 되는 Nexus_Core
        private Coroutine teleportCoroutine; // 현재 실행 중인 순간이동 Coroutine
        private float nextTeleportTime; // 다음 순간이동이 가능한 시간
        private bool ownsActionLock; // 이 Script가 보스 행동 잠금을 사용 중인지 나타내는 값

        public bool IsTeleporting { get; private set; } // 현재 순간이동 과정이 진행 중인지 외부에서 읽는 값

        private void Awake()
        {
            bossController = GetComponent<BossController>(); // 같은 Boss01에서 BossController를 가져온다.
            FindNexus(); // Nexus_Core를 찾아 저장한다.

            if (teleportMarker != null)
            {
                teleportMarker.SetParent(null, true); // 마커가 보스를 따라 이동하지 않도록 Boss01에서 분리한다.
                teleportMarker.gameObject.SetActive(false); // 처음에는 순간이동 마커를 숨긴다.
            }
        }

        private void Start()
        {
            ScheduleNextTeleport(); // 보스가 생성된 뒤 첫 순간이동 시간을 예약한다.
        }

        private void Update()
        {
            if (bossController == null || bossController.IsDead) // BossController가 없거나 보스가 사망했다면
            {
                return; // 순간이동을 실행하지 않는다.
            }

            if (teleportCoroutine != null) // 이미 순간이동이 진행 중이라면
            {
                return; // 새로운 순간이동을 시작하지 않는다.
            }

            if (bossController.IsActionRunning) // 다른 공격 패턴이 실행 중이라면
            {
                return; // 공격 도중에는 순간이동하지 않는다.
            }

            if (Time.time < nextTeleportTime) // 순간이동 간격이 아직 지나지 않았다면
            {
                return; // 현재 위치를 유지한다.
            }

            if (nexus == null)
            {
                FindNexus(); // Nexus 참조가 없어졌다면 다시 찾는다.

                if (nexus == null)
                {
                    nextTeleportTime = Time.time + 1.0f; // Nexus를 찾지 못했다면 1초 뒤 다시 시도한다.
                    return;
                }
            }

            if (!TryFindTeleportDestination(out Vector3 destination)) // 이동 가능한 위치를 찾지 못했다면
            {
                nextTeleportTime = Time.time + 1.0f; // 1초 뒤 위치 탐색을 다시 시도한다.
                return;
            }

            if (!bossController.TryBeginAction()) // 보스 행동 잠금을 얻지 못했다면
            {
                return; // 다른 패턴이 먼저 시작된 것이므로 순간이동하지 않는다.
            }

            ownsActionLock = true; // 이 Script가 행동 잠금을 사용 중이라고 저장한다.
            teleportCoroutine = StartCoroutine(TeleportRoutine(destination)); // 순간이동 과정을 시작한다.
        }

        private void OnDisable()
        {
            if (teleportCoroutine != null)
            {
                StopCoroutine(teleportCoroutine); // 진행 중인 순간이동 Coroutine을 중단한다.
                teleportCoroutine = null;
            }

            RestoreVisualState(); // 비활성화될 때 보스 외형과 마커 상태를 복구한다.
            IsTeleporting = false; // 순간이동 상태를 종료한다.
            ReleaseActionLock(); // 이 Script가 사용하던 행동 잠금을 해제한다.
        }

        private void OnDestroy()
        {
            if (teleportMarker != null)
            {
                Destroy(teleportMarker.gameObject); // Boss01에서 분리했던 순간이동 마커를 함께 제거한다.
            }
        }

        private IEnumerator TeleportRoutine(Vector3 destination)
        {
            IsTeleporting = true; // 순간이동 과정이 시작됐다고 저장한다.
            ShowTeleportMarker(destination); // 도착 예정 위치에 마커를 표시한다.

            yield return new WaitForSeconds(telegraphDuration); // 플레이어가 도착 위치를 확인할 시간을 준다.

            if (bossController == null || bossController.IsDead)
            {
                FinishTeleport(); // 예고 도중 보스가 죽었다면 순간이동을 취소하고 정리한다.
                yield break;
            }

            GameplaySfxEmitter.TryPlayAt(transform, GameplaySfxCue.BossTeleport, transform.position, true);

            if (bossVisualRoot != null)
            {
                bossVisualRoot.SetActive(false); // 실제 이동 직전에 보스 외형을 숨긴다.
            }

            yield return new WaitForSeconds(disappearDuration); // 보스가 사라지는 짧은 시간을 만든다.

            if (bossController == null || bossController.IsDead)
            {
                FinishTeleport(); // 사라진 상태에서 죽었다면 외형을 복구하고 종료한다.
                yield break;
            }

            transform.position = destination; // Boss01 Root를 선택된 도착 위치로 이동시킨다.
            LookAtNexus(); // 순간이동 후 Nexus 방향을 바라보게 한다.

            if (teleportMarker != null)
            {
                teleportMarker.gameObject.SetActive(false); // 도착했으므로 위치 예고 마커를 숨긴다.
            }

            if (bossVisualRoot != null)
            {
                bossVisualRoot.SetActive(true); // 도착 위치에서 보스 외형을 다시 표시한다.
            }

            yield return new WaitForSeconds(recoveryDuration); // 순간이동 직후 공격이 즉시 시작되지 않도록 잠시 기다린다.

            FinishTeleport(); // 순간이동 상태와 행동 잠금을 정리한다.
        }

        private bool TryFindTeleportDestination(out Vector3 destination)
        {
            destination = transform.position; // 위치 탐색에 실패하면 현재 위치를 기본값으로 사용한다.

            float minimumRadius = Mathf.Min(minimumTeleportRadius, maximumTeleportRadius);
            float maximumRadius = Mathf.Max(minimumTeleportRadius, maximumTeleportRadius);
            float minimumMoveDistanceSqr = minimumMoveDistance * minimumMoveDistance;
            float convoySafeDistanceSqr = convoySafeDistance * convoySafeDistance;

            MonsterInteractionApi.TryGetConvoyTarget(out Transform convoyTarget); // 등록된 컨보이 머리를 가져온다.

            for (int i = 0; i < positionSearchAttempts; i++)
            {
                Vector2 randomDirection = Random.insideUnitCircle; // 평면상의 무작위 방향을 만든다.

                if (randomDirection.sqrMagnitude <= 0.0001f)
                {
                    continue; // 방향의 길이가 너무 작다면 다시 선택한다.
                }

                randomDirection.Normalize(); // 방향의 길이를 1로 만든다.

                float radius = Random.Range(minimumRadius, maximumRadius); // Nexus로부터 떨어질 거리를 무작위로 선택한다.

                Vector3 candidate = nexus.position;
                candidate.x += randomDirection.x * radius;
                candidate.z += randomDirection.y * radius;
                candidate = GroundService.ProjectToGround(candidate, groundHeight); // 선택한 위치를 실제 바닥 높이에 맞춘다.

                Vector3 currentOffset = candidate - transform.position;
                currentOffset.y = 0.0f;

                if (currentOffset.sqrMagnitude < minimumMoveDistanceSqr)
                {
                    continue; // 현재 위치와 너무 가깝다면 다른 위치를 찾는다.
                }

                if (convoyTarget != null)
                {
                    Vector3 convoyOffset = candidate - convoyTarget.position;
                    convoyOffset.y = 0.0f;

                    if (convoyOffset.sqrMagnitude < convoySafeDistanceSqr)
                    {
                        continue; // 컨보이 머리에 너무 가까운 위치라면 다른 위치를 찾는다.
                    }
                }

                destination = candidate; // 모든 조건을 통과한 위치를 최종 도착 위치로 저장한다.
                return true; // 적합한 위치를 찾았다고 반환한다.
            }

            return false; // 반복 횟수 안에 적합한 위치를 찾지 못했다.
        }

        private void FindNexus()
        {
            GameObject nexusObject = GameObject.Find("Nexus_Core"); // 이름이 Nexus_Core인 GameObject를 찾는다.
            nexus = nexusObject != null ? nexusObject.transform : null; // 찾았다면 Transform을 저장한다.
        }

        private void ShowTeleportMarker(Vector3 destination)
        {
            if (teleportMarker == null)
            {
                return; // 연결된 마커가 없다면 표시하지 않는다.
            }

            teleportMarker.position = destination + Vector3.up * markerHeight; // 선택된 도착 위치에 마커를 배치한다.
            teleportMarker.gameObject.SetActive(true); // 순간이동 위치 예고를 표시한다.
        }

        private void LookAtNexus()
        {
            if (nexus == null)
            {
                return; // Nexus가 없다면 회전하지 않는다.
            }

            Vector3 direction = nexus.position - transform.position;
            direction.y = 0.0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return; // 방향을 계산할 수 없다면 회전하지 않는다.
            }

            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up); // 보스가 Nexus 방향을 바라보게 한다.
        }

        private void ScheduleNextTeleport()
        {
            nextTeleportTime = Time.time + GetTeleportInterval(); // 현재 상태에 맞는 시간이 지난 뒤 순간이동하도록 예약한다.
        }

        private float GetTeleportInterval()
        {
            if (bossController == null)
            {
                return normalInterval; // BossController가 없다면 Normal 간격을 사용한다.
            }

            switch (bossController.CurrentPhase)
            {
                case BossPhase.Aggressive:
                    return aggressiveInterval;

                case BossPhase.Rage:
                    return rageInterval;

                case BossPhase.Berserk:
                    return berserkInterval;

                default:
                    return normalInterval;
            }
        }

        private void FinishTeleport()
        {
            RestoreVisualState(); // 마커를 숨기고 보스 외형을 표시한다.
            IsTeleporting = false; // 순간이동 상태를 종료한다.
            ReleaseActionLock(); // 다음 공격이나 순간이동이 가능하도록 잠금을 해제한다.
            ScheduleNextTeleport(); // 다음 순간이동 시간을 예약한다.
            teleportCoroutine = null; // 실행 중인 Coroutine 참조를 비운다.
        }

        private void RestoreVisualState()
        {
            if (teleportMarker != null)
            {
                teleportMarker.gameObject.SetActive(false); // 순간이동 예고 마커를 숨긴다.
            }

            if (bossVisualRoot != null)
            {
                bossVisualRoot.SetActive(true); // 보스 외형이 숨은 상태로 남지 않게 한다.
            }
        }

        private void ReleaseActionLock()
        {
            if (!ownsActionLock)
            {
                return; // 이 Script가 잠금을 사용하지 않았다면 해제하지 않는다.
            }

            if (bossController != null)
            {
                bossController.EndAction(); // BossController의 행동 잠금을 해제한다.
            }

            ownsActionLock = false; // 더 이상 행동 잠금을 소유하지 않는다고 저장한다.
        }
    }
}
