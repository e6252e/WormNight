using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class GenericSegmentWeapon
    {
        private bool TryFindTarget(out EnemyController target) // 대상 탐색
        {
            hasResolvedImpactPoint = false; // 일반 타겟 기본값
            float range = GetEffectiveSearchRange(); // 강화 사거리
            if (AttackProfile.MoveType == SegmentAttackMoveType.SawBounceProjectile)
            {
                return TryFindLockedSawTarget(range, out target); // 톱날은 조준 대상 고정
            }

            ClearSawTargetLock(); // 다른 무기는 톱날 락 해제
            if (AttackProfile.TargetPriorityMode == SegmentTargetPriorityMode.BossEliteThenFarthest)
            {
                return TryFindPriorityTarget(range, out target); // 발리스타식 우선순위
            }

            if (AttackProfile.TargetPriorityMode == SegmentTargetPriorityMode.DensestClusterOrRandom)
            {
                return TryFindClusterOrRandomTarget(range, out target); // 수정구식 밀집/랜덤 지점
            }

            return EnemyController.TryFindNearest(transform.position, range, IsTargetInAttackArea, out target); // 데이터에셋의 공격 범위 형태까지 통과한 가까운 몬스터
        }

        private bool TryFindPriorityTarget(float range, out EnemyController target) // 등급 우선 + 가장 먼 대상
        {
            float aimHeight = AttackProfile != null ? AttackProfile.TargetAimHeight : 0.45f; // 조준 높이
            return SegmentTargetQuery.TryPickBossEliteThenFarthestTarget(transform.position, range, IsTargetInAttackArea, aimHeight, out target); // 공용 우선순위 검색
        }

        private bool TryFindClusterOrRandomTarget(float range, out EnemyController target) // 밀집 구역 우선 + 랜덤 fallback
        {
            float aimHeight = AttackProfile != null ? AttackProfile.TargetAimHeight : 0.45f; // 조준 높이
            bool found = SegmentTargetQuery.TryPickDensestClusterOrRandomTarget(
                transform.position,
                range,
                AttackProfile.ClusterProbeRadius,
                AttackProfile.ClusterMinEnemyCount,
                IsTargetInAttackArea,
                aimHeight,
                out target,
                out resolvedImpactPoint); // 공용 밀집 검색
            hasResolvedImpactPoint = found; // 지점 타격용 중심 저장
            return found;
        }

        private bool TryFindLockedSawTarget(float range, out EnemyController target) // 톱날 고정 대상 탐색
        {
            if (IsSawTargetLockValid(range))
            {
                target = lockedSawTarget; // 기존 대상 유지
                return true;
            }

            ClearSawTargetLock(); // 무효 대상 해제
            if (!TryFindDistantRandomTarget(transform.position, range, out target))
            {
                return false; // 새 대상 없음
            }

            lockedSawTarget = target; // 새 대상 고정
            return true;
        }

        private bool IsSawTargetLockValid(float range) // 톱날 대상 유지 조건
        {
            if (lockedSawTarget == null)
            {
                return false; // 고정 대상 없음
            }

            Vector3 center = GetEnemyHitPosition(lockedSawTarget); // 대상 중심
            float distance = SegmentTargetQuery.GetHorizontalDistance(transform.position, center); // 수평 거리
            return distance <= range && IsTargetInAttackArea(lockedSawTarget); // 사거리/범위 유지
        }

        private void ClearSawTargetLock() // 톱날 대상 해제
        {
            lockedSawTarget = null; // 다음 검색에서 재선택
        }

        private bool TryFindDistantRandomTarget(Vector3 origin, float range, out EnemyController target) // 중거리~장거리 랜덤 대상
        {
            float aimHeight = AttackProfile != null ? AttackProfile.TargetAimHeight : 0.45f; // 조준 높이
            return SegmentTargetQuery.TryPickMidToLongRandomTarget(origin, range, GetSawTargetMinDistanceRatio(), 0, IsTargetInAttackArea, aimHeight, out target); // 공용 후보 선택
        }

        // 원형/양옆 부채꼴 공격 범위 조건 확인
        private bool IsTargetInAttackArea(EnemyController target)
        {
            if (!IsTargetUsable(target))
            {
                return false; // 대상 없음
            }

            if (AttackProfile == null || AttackProfile.AttackAreaMode == SegmentAttackAreaMode.FullCircle)
            {
                return true; // 기존 원형 범위는 추가 각도 제한 없음
            }

            if (AttackProfile.AttackAreaMode == SegmentAttackAreaMode.SideCones)
            {
                return IsPositionInSideCones(target.transform.position); // 양옆 부채꼴 판정
            }

            return true; // 새 모드가 추가됐는데 아직 처리 전이면 기존 방식 유지
        }

        // 세그먼트 바디 기준 좌우 각각 SideConeAngle 안에 있는지 확인
        private bool IsPositionInSideCones(Vector3 worldPosition)
        {
            Transform reference = Segment != null ? Segment.transform : transform; // 머리 회전축이 아니라 세그먼트 바디 기준
            return SegmentTargetQuery.IsPositionInSideCones(reference, transform.right, worldPosition, GetEffectiveSideConeAngle()); // 공용 부채꼴 판정
        }

        private bool IsPositionInSideCone(Vector3 worldPosition, int sideSign) // 세그먼트 바디 기준 한쪽 부채꼴 확인
        {
            Transform reference = Segment != null ? Segment.transform : transform; // 세그먼트 바디 기준
            return SegmentTargetQuery.IsPositionInSideCone(reference, transform.right, worldPosition, GetEffectiveSideConeAngle(), sideSign); // 한쪽 부채꼴
        }

        private bool TryFindTargetInSideCone(int sideSign, out EnemyController target) // 한쪽 콘 안에서 가까운 대상 탐색
        {
            target = null;
            if (AttackProfile == null || AttackProfile.AttackAreaMode != SegmentAttackAreaMode.SideCones)
            {
                return false; // 좌우 콘 무기가 아니면 사용 안 함
            }

            int normalizedSide = NormalizeSideSign(sideSign);
            float range = GetEffectiveSearchRange(); // 강화 사거리
            return EnemyController.TryFindNearest(
                transform.position,
                range,
                enemy => IsTargetUsable(enemy) && IsPositionInSideCone(enemy.transform.position, normalizedSide),
                out target); // 지정 방향 우선 탐색
        }

        private int GetTargetSideSign(EnemyController target) // 대상이 좌/우 어느 콘에 있는지 계산
        {
            if (!IsTargetUsable(target))
            {
                return NormalizeSideSign(projectileSequencePreferredSide); // 기존 선호값 유지
            }

            Transform reference = Segment != null ? Segment.transform : transform; // 세그먼트 바디 기준
            Vector3 toTarget = target.transform.position - reference.position;
            toTarget.y = 0f;
            Vector3 right = reference.right;
            right.y = 0f;
            if (right.sqrMagnitude <= 0.0001f)
            {
                right = transform.right;
                right.y = 0f;
            }

            if (toTarget.sqrMagnitude <= 0.0001f || right.sqrMagnitude <= 0.0001f)
            {
                return NormalizeSideSign(projectileSequencePreferredSide); // 판단 불가 시 기존값
            }

            return Vector3.Dot(toTarget.normalized, right.normalized) >= 0f ? 1 : -1; // 오른쪽/왼쪽
        }

        private static int NormalizeSideSign(int sideSign) // 좌우 값 보정
        {
            return sideSign >= 0 ? 1 : -1;
        }

        private Vector3 GetEnemyHitPosition(EnemyController enemy) // 몬스터 중심 위치
        {
            float aimHeight = AttackProfile != null ? AttackProfile.TargetAimHeight : 0.45f; // 조준 높이
            return SegmentTargetQuery.GetEnemyHitPosition(enemy, transform.position, aimHeight); // 공용 중심 계산
        }

        private static bool IsTargetUsable(EnemyController target) // 살아있는 타겟인지 확인
        {
            return SegmentTargetQuery.IsEnemyUsable(target); // 사망/비활성 제외
        }

        private float GetSawTargetMinDistanceRatio() // 톱날 중장거리 후보 기준
        {
            return AttackProfile != null ? Mathf.Clamp01(AttackProfile.SawTargetMinDistanceRatio) : 0.5f; // 기본 절반 이상
        }

    }
}
