using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyMovement : MonoBehaviour //몬스터 이동
    {
        private const float FallbackStopRadius = 1.6f; // 공격 Script가 없을 때만 사용할 예비 정지 거리

        private Transform nexus; // 이동 목표

        [Min(0.1f)]
        [SerializeField] private float moveSpeed = 1.25f; // 몬스터 이동 속도

        [Min(0.45f)]
        [SerializeField] private float bodyRadius = 0.6f; // 몬스터가 이동할 때 세그먼트와 겹치거나 밀고 들어가는 것을 보정한다.

        [Min(0f)]
        [SerializeField] private float groundHeight = 0.72f; // 바닥 위에 몬스터를 올려둘 높이 오프셋

        //컨보이 밀렸을 떄 뒤로 밀려나게 할때 사용할 변수(끼임현상 방지)
        private const float ContactKnockbackSpeed = 3.0f; // 전찬우추가-6019(몬스터피드백관련) - 접촉 밀림 기본 속도
        private const float ContactKnockbackDuration = 0.15f; // 전찬우추가-6019(몬스터피드백관련) - 접촉 밀림 기본 시간
        private float knockbackSpeed = ContactKnockbackSpeed; // 전찬우수정-6019(몬스터피드백관련) - 현재 넉백 속도
        private float knockbackDuration = ContactKnockbackDuration; // 전찬우수정-6019(몬스터피드백관련) - 현재 넉백 시간
        private Vector3 knockbackDirection; //현재 몬스터가 밀려나는 방향
        private float knockbackTimer; //밀리 상태 시간
        private float staggerTimer; // 전찬우추가-6019(몬스터피드백관련) - 경직 남은 시간

        private EnemyPortalTotem assignedPortalTotem; // 현재 몬스터가 이동할 입구 토템

        public bool IsInStopRange { get; private set; } // 현재 Nexus가 멈춤 거리 안에 있는지 외부에서 읽는 값

        private EnemyController enemyController; // 같은 GameObject에 붙은 EnemyController Script Component 참조

        private EnemyMeleeAttack meleeAttack; // 같은 GameObject에 붙은 근거리 공격 Script Component 참조
        private EnemyRangedAttack rangedAttack; // 같은 GameObject에 붙은 원거리 공격 Script Component 참조

        private EnemySlowZoneThrower slowZoneThrower; // 같은 GameObject에 붙은 슬로우 장판 투척 공격 Script Component 참조
        private EnemyObstacleSummoner obstacleSummoner; // 같은 GameObject에 붙은 장애물 소환 Script Component 참조
        private EnemySegmentCutCaster segmentCutCaster;//같은 GameObject에 붙은 절단 상태 Script Component 참조

        private EnemyBuffReceiver buffReceiver; // 같은 GameObject에 붙은 버프 상태 Script Component 참조
        private EnemySupportDebuffState supportDebuff; // 전찬우추가-0621 - 지원형 디버프 상태

        private EnemyPortalTotemCaster portalTotemCaster; // 같은 GameObject에 붙은 포탈 토템 소환 Script Component 참조
        private EnemyCrowdBlocker crowdBlocker; // 몬스터끼리 한 점에 겹치지 않도록 보정하는 Script Component 참조

        private void Awake()
        {
            enemyController = GetComponent<EnemyController>(); // 같은 GameObject에 붙은 EnemyController Script Component를 찾는다.
            crowdBlocker = GetComponent<EnemyCrowdBlocker>(); // 같은 GameObject에 붙은 EnemyCrowdBlocker Script Component를 찾는다.

            meleeAttack = GetComponent<EnemyMeleeAttack>(); // 같은 GameObject에 붙은 EnemyMeleeAttack Script Component를 찾는다.
            rangedAttack = GetComponent<EnemyRangedAttack>(); // 같은 GameObject에 붙은 EnemyRangedAttack Script Component를 찾는다.
            slowZoneThrower = GetComponent<EnemySlowZoneThrower>(); // 같은 GameObject에 붙은 EnemySlowZoneThrower Script Component를 찾는다.
            obstacleSummoner = GetComponent<EnemyObstacleSummoner>(); // 같은 GameObject에 붙은 EnemyObstacleSummoner Script Component를 찾는다.
            segmentCutCaster = GetComponent<EnemySegmentCutCaster>(); //같은 GameObject에 붙은 절단 상태 Script Component 참조

            buffReceiver = GetComponent<EnemyBuffReceiver>(); // 같은 GameObject에 붙은 EnemyBuffReceiver Script Component를 찾는다.
            supportDebuff = GetComponent<EnemySupportDebuffState>(); // 전찬우추가-0621 - 지원형 디버프 상태를 찾는다.

            portalTotemCaster = GetComponent<EnemyPortalTotemCaster>(); // 같은 GameObject에 붙은 포탈 토템 소환 Script Component를 찾는다.

            if (nexus == null) //Nexus가 연결되지 않았다면
            {
                GameObject nexusObject = GameObject.Find("Nexus_Core"); //씬에서 이름이 Nexus_Core인 GameObject를 찾는다.
                nexus = nexusObject != null ? nexusObject.transform : null; //찾았다면 Transform을 저장하고, 못 찾았다면 null로 둔다.
            }
        }

        private void Update()
        {
            SetCrowdMoving(false); // 실제 이동 분기에서만 켜서 정지 몬스터가 밀림을 받을 수 있게 한다.

            if (IsFrozenBySupport()) // 전찬우추가-0621 - 얼음종 동결 중 이동 정지
            {
                Vector3 resolvedPosition = ResolveMonsterPosition(transform.position, transform.position); // 정지 중 위치 보정
                transform.position = resolvedPosition; // 보정 위치 적용

                return;
            }

            if (knockbackTimer > 0.0f) //밀리는 시간이 유지되고 있다면
            {
                knockbackTimer -= Time.deltaTime;//밀리는 시간을 감소한다.

                Vector3 knockbackPosition = transform.position + knockbackDirection * (knockbackSpeed * Time.deltaTime); //밀려날 위치를 계산한다.
                knockbackPosition = GroundService.ProjectToGround(knockbackPosition, groundHeight); //바닥 높이에 맞춘다.

                Vector3 resolvedKnockbackPosition = MonsterInteractionApi.ResolveMonsterPosition(transform.position, knockbackPosition, bodyRadius); //밀리는 중에 컨보이와 겹치지 않게 한다.
                resolvedKnockbackPosition = ResolveCrowdPosition(transform.position, resolvedKnockbackPosition); // 몬스터끼리 겹치지 않게 한다.
                transform.position = resolvedKnockbackPosition; //보정 위치를 적용한다.

                return;
            }

            if (staggerTimer > 0.0f) // 전찬우추가-6019(몬스터피드백관련) - 경직 중 이동 정지
            {
                staggerTimer -= Time.deltaTime; // 전찬우추가-6019(몬스터피드백관련) - 경직 시간 감소

                Vector3 resolvedPosition = ResolveMonsterPosition(transform.position, transform.position); // 전찬우추가-6019(몬스터피드백관련) - 정지 중 위치 보정
                transform.position = resolvedPosition; // 전찬우추가-6019(몬스터피드백관련) - 보정 위치 적용

                return;
            }

            bool isSegmentCutFollower = segmentCutCaster != null; // 전찬우수정-0630 - 절단 몬스터는 넥서스가 아니라 컨보이 꼬리를 따라간다.
            Transform movementTarget = nexus; // 기본 몬스터는 Nexus를 이동 목표로 사용한다.

            if (isSegmentCutFollower)
            {
                assignedPortalTotem = null; // 전찬우수정-0630 - 절단 몬스터는 포탈 토템 유도 대신 꼬리 추적만 사용한다.

                if (!segmentCutCaster.TryGetTailFollowTarget(out movementTarget))
                {
                    IsInStopRange = false;

                    Vector3 resolvedPosition = ResolveMonsterPosition(transform.position, transform.position);
                    transform.position = resolvedPosition;

                    return;
                }
            }
            else if (nexus == null) //이동 목표가 없으면
            {
                return; //종료한다.
            }

            if (!isSegmentCutFollower && portalTotemCaster != null && portalTotemCaster.IsChanneling) // 토템 소환 몬스터가 집결 과정을 진행 중이라면
            {
                IsInStopRange = false; // Nexus 공격 사거리 안에 있는 상태는 아니라고 저장한다.

                Vector3 resolvedPosition = ResolveMonsterPosition(transform.position, transform.position); // 채널링 중에도 겹치지 않도록 위치를 보정한다.
                transform.position = resolvedPosition; // 보정된 위치를 적용한다.

                return; // 토템을 유지하는 동안 이동하지 않는다.
            }

            if (!isSegmentCutFollower)
            {
                UpdateAssignedPortalTotem(); // 현재 이동할 입구 토템을 찾거나 기존 토템 상태를 확인한다.
            }

            bool isMovingToPortalTotem = !isSegmentCutFollower && assignedPortalTotem != null; // 현재 입구 토템으로 이동 중인지 확인한다.

            Vector3 offset = movementTarget.position - transform.position; // 현재 몬스터 위치에서 이동 목표까지의 방향과 거리 벡터를 구한다.

            if (isMovingToPortalTotem) // 이동할 입구 토템이 있다면
            {
                offset = assignedPortalTotem.transform.position - transform.position; // Nexus 대신 입구 토템까지의 방향과 거리 벡터를 사용한다.
            }

            offset.y = 0f; //높이 차이는 제거한다.

            float stopDistance = isSegmentCutFollower ? segmentCutCaster.CastRange : GetStopDistance(); // 전찬우수정-0630 - 절단 몬스터는 절단 사거리를 꼬리 추적 정지 거리로 사용한다.

            if (isMovingToPortalTotem) // 입구 토템으로 이동 중이라면
            {
                stopDistance = assignedPortalTotem.EntryRadius; // 입구 토템의 순간이동 판정 범위를 멈춤 거리로 사용한다.
            }

            bool isTargetInStopRange = offset.sqrMagnitude <= stopDistance * stopDistance; // 현재 이동 목표가 멈춤 거리 안에 있는지 확인한다.

            bool isNexusInStopRange = !isSegmentCutFollower && !isMovingToPortalTotem && isTargetInStopRange; // Nexus가 공격 사거리 안에 있는지 확인한다.
            bool isSegmentCutTailInStopRange = isSegmentCutFollower && isTargetInStopRange; // 전찬우수정-0630 - 꼬리 세그먼트가 절단 사거리 안인지 확인한다.

            bool isSlowTargetInRange = !isSegmentCutFollower && !isMovingToPortalTotem && slowZoneThrower != null && slowZoneThrower.IsTargetInThrowRange(); // 컨보이가 슬로우 투척 사거리 안에 있는지 확인한다.
            bool isObstacleSummoning = obstacleSummoner != null && obstacleSummoner.IsSummoning; // 장애물 소환 과정이 진행 중인지 확인한다.

            // 조성원삭제-0626 - 절단 마법 쿨타임 중에도 20 사거리에서 계속 정지하므로 단순 사거리 조건을 사용하지 않는다.
            // bool isSegmentCutTargetInRange = !isMovingToPortalTotem && segmentCutCaster != null && segmentCutCaster.IsTargetInCastRange(); //컨보이가 절단 마법 시전 범위 안에 있는지 확인한다.

            bool shouldPrioritizeSegmentCut = isSegmentCutFollower && segmentCutCaster.ShouldPrioritizeCast; // 전찬우수정-0630 - 절단 몬스터는 꼬리 사거리 안에서만 절단 마법을 우선한다.

            if (isMovingToPortalTotem && isTargetInStopRange) // 입구 토템의 Entry Radius 안에 도착했다면
            {
                IsInStopRange = false; // Nexus 공격 사거리 안에 있는 상태는 아니라고 저장한다.

                Vector3 resolvedPosition = ResolveMonsterPosition(transform.position, transform.position); // 토템 주변에서 정지 중에도 겹치지 않도록 위치를 보정한다.
                transform.position = resolvedPosition; // 보정된 위치를 적용한다.

                return; // 순간이동할 때까지 입구 토템 주변에서 정지한다.
            }

            // 조성원삭제-0626 - 절단 마법 사거리 안에 있다는 이유만으로 계속 정지하는 기존 조건을 사용하지 않는다.
            // if (isNexusInStopRange || isSlowTargetInRange || isObstacleSummoning || isSegmentCutTargetInRange) // 공격 가능 거리거나, 투척 가능 거리거나, 장애물 소환 중이거나 절단마법 중이 거나

            if (isNexusInStopRange || isSegmentCutTailInStopRange || isSlowTargetInRange || isObstacleSummoning || shouldPrioritizeSegmentCut) // 전찬우수정-0630 - 절단 몬스터는 꼬리 사거리 안에서 정지한다.
            {
                IsInStopRange = isNexusInStopRange || isSegmentCutTailInStopRange || shouldPrioritizeSegmentCut; // 절단 몬스터의 정지 상태도 이동 애니메이션에 반영한다.

                if (isSegmentCutFollower && offset.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(offset.normalized, Vector3.up); // 꼬리 정지 중에도 꼬리 방향을 바라본다.
                }

                ////// 전찬우추가-0619 - 몬스터 위치 보정은 공용 상호작용 API를 통해서만 조회한다.
                Vector3 resolvedPosition = ResolveMonsterPosition(transform.position, transform.position); // 전찬우추가-0619 - 정지 중에도 겹치지 않도록 위치를 보정한다.
                transform.position = resolvedPosition; // 보정된 위치를 적용한다.

                return; // 공격, 투척, 소환 중이면 Nexus 쪽으로 더 이동하지 않는다.
            }

            IsInStopRange = false;

            Vector3 direction = offset.normalized;

            float moveSpeedBuffMultiplier = 1.0f; // 기본 이동속도 버프 배율

            if (buffReceiver != null) // 버프 상태 Script Component가 있다면
            {
                moveSpeedBuffMultiplier = buffReceiver.GetMoveSpeedMultiplier(); // 현재 이동속도 버프 배율을 가져온다.
            }

            float supportDebuffMoveMultiplier = GetSupportDebuffMoveSpeedMultiplier(); // 지원형 빙결/감속 배율
            Vector3 desiredPosition = transform.position + direction * (moveSpeed * moveSpeedBuffMultiplier * supportDebuffMoveMultiplier * Time.deltaTime); // 버프/디버프 배율까지 적용해서 이번 프레임 이동 목표 위치를 계산한다.
            desiredPosition = GroundService.ProjectToGround(desiredPosition, groundHeight); // 목표 위치를 바닥 기준 높이에 맞게 보정한다.

            ////// 전찬우추가-0619 - 몬스터 이동 위치 보정은 공용 상호작용 API를 통해서만 조회한다.
            Vector3 segmentResolvedPosition = MonsterInteractionApi.ResolveMonsterPosition(transform.position, desiredPosition, bodyRadius); // 전찬우추가-0619 - 세그먼트와 겹치지 않도록 이동 위치를 보정한다.

            Vector3 pushOffset = segmentResolvedPosition - desiredPosition; // 원래 이동하려던 위치에서 얼마나 밀려났는지 계산한다.

            if (pushOffset.sqrMagnitude > 0.0001f) // 세그먼트 때문에 위치가 보정되었다면
            {
                knockbackSpeed = ContactKnockbackSpeed; // 전찬우수정-6019(몬스터피드백관련) - 접촉 밀림 속도 복구
                knockbackDuration = ContactKnockbackDuration; // 전찬우수정-6019(몬스터피드백관련) - 접촉 밀림 시간 복구
                knockbackDirection = pushOffset.normalized; // 밀려난 방향을 저장한다.
                knockbackTimer = knockbackDuration; // 짧은 시간 동안 밀림 상태로 만든다.
            }

            Vector3 position = ResolveCrowdPosition(transform.position, segmentResolvedPosition, true); // 몬스터끼리 겹치지 않도록 최종 위치를 보정한다.
            transform.position = position; // 최종 보정된 위치를 몬스터 Transform에 적용한다.
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up); // 몬스터가 이동 방향을 바라보게 회전시킨다.
        }

        private void UpdateAssignedPortalTotem() // 몬스터가 이동할 입구 토템을 확인하는 함수
        {
            if (assignedPortalTotem != null) // 이미 이동 목표로 저장한 입구 토템이 있다면
            {
                if (!assignedPortalTotem.IsActive || !assignedPortalTotem.IsEntry) // 토템이 비활성화되었거나 입구 토템이 아니라면
                {
                    assignedPortalTotem = null; // 기존 입구 토템 목표를 제거한다.
                }
            }

            if (assignedPortalTotem != null) // 기존 입구 토템이 아직 유효하다면
            {
                return; // 같은 입구 토템을 계속 이동 목표로 사용한다.
            }

            if (enemyController != null && enemyController.Grade == EnemyGrade.Boss) // Boss 등급 몬스터라면
            {
                return; // 입구 토템을 이동 목표로 사용하지 않는다.
            }

            EnemyPortalTotem targetTotem; // Registry에서 찾은 입구 토템을 저장할 변수

            if (EnemyPortalTotemRegistry.TryGetAttractTarget(transform.position, out targetTotem)) // 현재 몬스터가 입구 토템 유도 범위 안에 있다면
            {
                assignedPortalTotem = targetTotem; // 찾은 입구 토템을 이동 목표로 저장한다.
            }
        }

        private float GetStopDistance() // 몬스터가 이동을 멈출 거리를 결정하는 함수
        {
            if (meleeAttack != null) // 근거리 공격 Script Component가 있다면
            {
                return meleeAttack.AttackRange; // 근거리 공격 사거리를 멈춤 거리로 사용한다.
            }

            if (rangedAttack != null) // 원거리 공격 Script Component가 있다면
            {
                return rangedAttack.AttackRange; // 원거리 공격 사거리를 멈춤 거리로 사용한다.
            }

            return FallbackStopRadius; // 공격 Script가 없는 몬스터라면 예비 정지 거리를 사용한다.
        }

        public void ApplyMonsterFeedback(MonsterFeedbackData feedback) // 전찬우추가-6019(몬스터피드백관련) - 공격 피드백 적용
        {
            if (!feedback.IsValid)
            {
                return;
            }

            if (feedback.HasStagger) // 전찬우추가-6019(몬스터피드백관련) - 경직 적용
            {
                staggerTimer = Mathf.Max(staggerTimer, feedback.StaggerDuration);
            }

            if (!feedback.HasKnockback)
            {
                return;
            }

            Vector3 direction = feedback.Direction; // 전찬우추가-6019(몬스터피드백관련) - 넉백 방향
            direction.y = 0.0f;

            if (direction.sqrMagnitude <= 0.0001f) // 전찬우추가-6019(몬스터피드백관련) - 방향 보정
            {
                direction = transform.position - feedback.Origin;
                direction.y = 0.0f;
            }

            if (direction.sqrMagnitude <= 0.0001f) // 전찬우추가-6019(몬스터피드백관련) - 최종 예비 방향
            {
                direction = -transform.forward;
                direction.y = 0.0f;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float duration = Mathf.Max(0.01f, feedback.KnockbackDuration); // 전찬우추가-6019(몬스터피드백관련) - 넉백 시간 보정
            knockbackDirection = direction.normalized; // 전찬우추가-6019(몬스터피드백관련) - 넉백 방향 저장
            knockbackDuration = duration; // 전찬우수정-6019(몬스터피드백관련) - 넉백 시간 갱신
            knockbackSpeed = feedback.KnockbackDistance / duration; // 전찬우수정-6019(몬스터피드백관련) - 넉백 속도 계산
            knockbackTimer = duration; // 전찬우수정-6019(몬스터피드백관련) - 넉백 타이머 시작
        }

        public void ForceTeleport(Vector3 worldPosition) // 지원형 웜홀 강제 위치 이동
        {
            assignedPortalTotem = null; // 기존 적 포탈 목표 해제
            knockbackTimer = 0f; // 밀림 중단
            staggerTimer = 0f; // 경직 중단
            knockbackDirection = Vector3.zero; // 방향 초기화
            IsInStopRange = false; // 넥서스 정지 상태 해제
            SetCrowdMoving(false); // 강제 이동 직전 몬스터 군집 이동 상태를 정리한다.

            if (crowdBlocker != null)
            {
                crowdBlocker.ClearPendingPush(); // 이전 프레임에 예약된 밀림이 순간이동 위치를 흔들지 않게 한다.
            }

            Vector3 groundedPosition = GroundService.ProjectToGround(worldPosition, groundHeight); // 바닥 높이 보정
            transform.position = ResolveMonsterPosition(transform.position, groundedPosition); // 겹침 보정
        }

        public void Configure(Transform nexus, float moveSpeed, float groundHeight)// Spawner나 Controller가 이동 초기값을 넣어주는 함수
        {
            this.nexus = nexus; // 이동 목표 Nexus를 저장한다.
            this.moveSpeed = moveSpeed; // 이동 속도를 저장한다.
            this.groundHeight = groundHeight; // 바닥 높이 오프셋을 저장한다.
        }

        public void ApplyMoveSpeedMultiplier(float multiplier) // 웨이브 난이도 이동속도 배율 적용
        {
            if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f))
            {
                return; // 적용 없음
            }

            moveSpeed = Mathf.Max(0.01f, moveSpeed * multiplier); // 기본 이동속도 배율
        }

        private bool IsFrozenBySupport() // 전찬우추가-0621 - 지원형 동결 여부
        {
            if (supportDebuff == null)
            {
                supportDebuff = GetComponent<EnemySupportDebuffState>(); // 런타임에 붙은 디버프 상태 재확인
            }

            return supportDebuff != null && supportDebuff.IsFrozen;
        }

        private float GetSupportDebuffMoveSpeedMultiplier()
        {
            if (supportDebuff == null)
            {
                supportDebuff = GetComponent<EnemySupportDebuffState>(); // 런타임에 붙은 디버프 상태 재확인
            }

            return supportDebuff != null ? supportDebuff.MoveSpeedMultiplier : 1f;
        }

        private Vector3 ResolveMonsterPosition(Vector3 currentPosition, Vector3 desiredPosition)
        {
            Vector3 segmentResolvedPosition = MonsterInteractionApi.ResolveMonsterPosition(currentPosition, desiredPosition, bodyRadius);
            return ResolveCrowdPosition(currentPosition, segmentResolvedPosition);
        }

        private Vector3 ResolveCrowdPosition(Vector3 currentPosition, Vector3 desiredPosition, bool isMoving = false)
        {
            if (crowdBlocker == null)
            {
                crowdBlocker = GetComponent<EnemyCrowdBlocker>();
            }

            SetCrowdMoving(isMoving);
            return MonsterInteractionApi.ResolveMonsterCrowdPosition(crowdBlocker, currentPosition, desiredPosition, bodyRadius);
        }

        private void SetCrowdMoving(bool isMoving)
        {
            if (crowdBlocker == null)
            {
                crowdBlocker = GetComponent<EnemyCrowdBlocker>();
            }

            if (crowdBlocker != null)
            {
                crowdBlocker.SetCrowdMoving(isMoving);
            }
        }
    }
}
