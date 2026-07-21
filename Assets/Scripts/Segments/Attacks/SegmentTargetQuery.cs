using System;
using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    internal static class SegmentTargetQuery // 세그먼트 타겟 공용 검색
    {
        public static bool IsEnemyUsable(EnemyController enemy) // 살아있는 몬스터 후보인지 확인
        {
            return enemy != null && !enemy.IsDead && enemy.isActiveAndEnabled; // 사망/비활성 제외
        }

        public static bool TryPickMidToLongRandomTarget(
            Vector3 origin,
            float range,
            float minDistanceRatio,
            int excludedEnemyId,
            Func<EnemyController, bool> isValidTarget,
            float targetAimHeight,
            out EnemyController target) // 중장거리 랜덤 후보
        {
            target = null;
            if (range <= 0f)
            {
                return false; // 사거리 없음
            }

            List<TargetCandidate> candidates = new List<TargetCandidate>(); // 전체 후보
            Collider[] hits = Physics.OverlapSphere(origin, range, ~0, QueryTriggerInteraction.Collide); // 범위 검색
            float farthestDistance = 0f; // 가장 먼 후보 거리
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i]; // 충돌체
                if (hit == null)
                {
                    continue; // 빈 콜라이더
                }

                EnemyController enemy = hit.GetComponentInParent<EnemyController>(); // 몬스터
                if (!IsEnemyUsable(enemy) || enemy.EnemyId == excludedEnemyId || ContainsCandidate(candidates, enemy.EnemyId))
                {
                    continue; // 대상 아님/제외/중복
                }

                if (isValidTarget != null && !isValidTarget(enemy))
                {
                    continue; // 무기별 범위 조건 실패
                }

                Vector3 center = GetEnemyHitPosition(enemy, origin, targetAimHeight); // 중심 위치
                float distance = GetHorizontalDistance(origin, center); // 수평 거리
                candidates.Add(new TargetCandidate(enemy, enemy.EnemyId, distance)); // 후보 등록
                farthestDistance = Mathf.Max(farthestDistance, distance); // 최대 거리 갱신
            }

            if (candidates.Count == 0)
            {
                return false; // 후보 없음
            }

            List<TargetCandidate> pickSource = FilterMidToLongCandidates(candidates, farthestDistance, minDistanceRatio); // 중장거리 우선
            int index = UnityEngine.Random.Range(0, pickSource.Count); // 균등 랜덤
            target = pickSource[index].Enemy; // 선택
            return target != null;
        }

        public static bool TryPickBossEliteThenFarthestTarget(
            Vector3 origin,
            float range,
            Func<EnemyController, bool> isValidTarget,
            float targetAimHeight,
            out EnemyController target) // 보스/엘리트 우선 + 장거리 대상
        {
            target = null; // 기본값
            if (range <= 0f)
            {
                return false; // 사거리 없음
            }

            List<EnemyController> candidates = new List<EnemyController>(32); // 범위 후보
            EnemyController.CollectActiveInRange(origin, range, candidates, isValidTarget); // 활성 몬스터 수집
            int bestGradePriority = -1; // 등급 우선순위
            float bestDistance = -1f; // 같은 등급 내 가장 먼 거리
            for (int i = 0; i < candidates.Count; i++)
            {
                EnemyController enemy = candidates[i]; // 후보
                if (!IsEnemyUsable(enemy))
                {
                    continue; // 빈 후보
                }

                int gradePriority = GetGradePriority(enemy.Grade); // 보스/엘리트 우선
                Vector3 center = GetEnemyHitPosition(enemy, origin, targetAimHeight); // 중심 위치
                float distance = GetHorizontalDistance(origin, center); // 수평 거리
                if (gradePriority < bestGradePriority)
                {
                    continue; // 낮은 등급
                }

                if (gradePriority == bestGradePriority && distance <= bestDistance)
                {
                    continue; // 같은 등급이면 더 먼 대상 우선
                }

                bestGradePriority = gradePriority; // 우선순위 갱신
                bestDistance = distance; // 거리 갱신
                target = enemy; // 대상 갱신
            }

            return target != null; // 발견 여부
        }

        public static bool TryPickDensestClusterOrRandomTarget(
            Vector3 origin,
            float range,
            float clusterRadius,
            int clusterMinEnemyCount,
            Func<EnemyController, bool> isValidTarget,
            float targetAimHeight,
            out EnemyController target,
            out Vector3 impactPoint) // 밀집 구역 우선, 없으면 랜덤
        {
            target = null;
            impactPoint = origin;
            if (range <= 0f)
            {
                return false; // 사거리 없음
            }

            List<EnemyController> candidates = new List<EnemyController>(32);
            EnemyController.CollectActiveInRange(origin, range, candidates, isValidTarget); // 사거리 후보
            if (candidates.Count == 0)
            {
                return false; // 후보 없음
            }

            float resolvedClusterRadius = Mathf.Max(0.1f, clusterRadius);
            float clusterRadiusSqr = resolvedClusterRadius * resolvedClusterRadius;
            int minCount = Mathf.Max(1, clusterMinEnemyCount);
            int bestCount = 0;
            Vector3 bestCenter = Vector3.zero;
            EnemyController bestTarget = null;

            for (int i = 0; i < candidates.Count; i++)
            {
                EnemyController centerEnemy = candidates[i];
                if (!IsEnemyUsable(centerEnemy))
                {
                    continue; // 빈 후보
                }

                Vector3 center = centerEnemy.transform.position;
                Vector3 sum = Vector3.zero;
                int count = 0;
                for (int j = 0; j < candidates.Count; j++)
                {
                    EnemyController other = candidates[j];
                    if (!IsEnemyUsable(other))
                    {
                        continue; // 빈 후보
                    }

                    Vector3 offset = other.transform.position - center;
                    offset.y = 0f;
                    if (offset.sqrMagnitude > clusterRadiusSqr)
                    {
                        continue; // 밀집 반경 밖
                    }

                    sum += other.transform.position;
                    count++;
                }

                if (count < minCount || count < bestCount)
                {
                    continue; // 조건 미달/더 작은 군집
                }

                Vector3 clusterCenter = sum / Mathf.Max(1, count);
                EnemyController centerTarget = FindNearestCandidate(candidates, clusterCenter);
                if (count == bestCount && bestTarget != null)
                {
                    float currentDistance = GetHorizontalDistance(origin, clusterCenter);
                    float bestDistance = GetHorizontalDistance(origin, bestCenter);
                    if (currentDistance >= bestDistance)
                    {
                        continue; // 동률이면 세그먼트에 더 가까운 군집 유지
                    }
                }

                bestCount = count;
                bestCenter = clusterCenter;
                bestTarget = centerTarget;
            }

            if (bestTarget != null)
            {
                target = bestTarget;
                impactPoint = GroundService.ProjectToGround(bestCenter, 0f);
                return true; // 밀집 지점 사용
            }

            int randomIndex = UnityEngine.Random.Range(0, candidates.Count);
            target = candidates[randomIndex];
            impactPoint = target != null
                ? GroundService.ProjectToGround(target.transform.position, 0f)
                : origin;
            return target != null; // 밀집 실패 시 랜덤 적
        }

        public static bool TryPickNearestToPointTarget(
            Vector3 origin,
            float range,
            Vector3 point,
            Func<EnemyController, bool> isValidTarget,
            float targetAimHeight,
            out EnemyController target) // 지정 지점에 가장 가까운 후보
        {
            target = null; // 기본값
            if (range <= 0f)
            {
                return false; // 사거리 없음
            }

            List<EnemyController> candidates = new List<EnemyController>(32);
            EnemyController.CollectActiveInRange(origin, range, candidates, isValidTarget); // 사거리 후보
            float bestDistance = float.PositiveInfinity; // 지점 기준 거리
            for (int i = 0; i < candidates.Count; i++)
            {
                EnemyController enemy = candidates[i]; // 후보
                if (!IsEnemyUsable(enemy))
                {
                    continue; // 빈 후보
                }

                Vector3 hitPosition = GetEnemyHitPosition(enemy, enemy.transform.position, targetAimHeight); // 중심
                float distance = GetHorizontalDistance(point, hitPosition); // 넥서스 등 기준점 거리
                if (distance >= bestDistance)
                {
                    continue; // 더 멂
                }

                bestDistance = distance; // 최단 갱신
                target = enemy; // 대상 저장
            }

            return target != null; // 발견 여부
        }

        public static Vector3 GetEnemyHitPosition(EnemyController enemy, Vector3 fallbackPosition, float targetAimHeight) // 몬스터 중심
        {
            if (enemy == null)
            {
                return fallbackPosition; // fallback
            }

            Collider targetCollider = enemy.GetComponentInChildren<Collider>();
            if (targetCollider != null)
            {
                return targetCollider.bounds.center; // 콜라이더 중심
            }

            return enemy.transform.position + Vector3.up * targetAimHeight; // 높이 보정
        }

        public static bool IsPositionInSideCones(Transform reference, Vector3 fallbackRight, Vector3 worldPosition, float sideConeAngle) // 좌우 부채꼴
        {
            return IsPositionInSideCone(reference, fallbackRight, worldPosition, sideConeAngle, 1)
                || IsPositionInSideCone(reference, fallbackRight, worldPosition, sideConeAngle, -1); // 좌우 중 하나
        }

        public static bool IsPositionInSideCone(Transform reference, Vector3 fallbackRight, Vector3 worldPosition, float sideConeAngle, int sideSign) // 한쪽 부채꼴
        {
            if (reference == null)
            {
                return true; // 기준 없음
            }

            Vector3 toTarget = worldPosition - reference.position; // 기준 -> 목표
            toTarget.y = 0f; // 수평 판정
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return true; // 거의 같은 위치
            }

            Vector3 right = GetHorizontalDirection(reference.right, fallbackRight, Vector3.right); // 오른쪽 중심
            Vector3 coneDirection = sideSign >= 0 ? right : -right; // 원하는 쪽 중심
            Vector3 targetDirection = toTarget.normalized; // 목표 방향
            float halfAngle = Mathf.Clamp(sideConeAngle, 1f, 180f) * 0.5f; // 한쪽 반각
            return Vector3.Angle(coneDirection, targetDirection) <= halfAngle;
        }

        public static float GetHorizontalDistance(Vector3 from, Vector3 to) // 수평 거리
        {
            from.y = 0f;
            to.y = 0f;
            return Vector3.Distance(from, to);
        }

        private static List<TargetCandidate> FilterMidToLongCandidates(List<TargetCandidate> candidates, float farthestDistance, float minDistanceRatio) // 중장거리 필터
        {
            float minDistance = farthestDistance * Mathf.Clamp01(minDistanceRatio); // 기준 거리
            List<TargetCandidate> distantCandidates = new List<TargetCandidate>(); // 중장거리 후보
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Distance >= minDistance)
                {
                    distantCandidates.Add(candidates[i]); // 기준 통과
                }
            }

            return distantCandidates.Count > 0 ? distantCandidates : candidates; // fallback
        }

        private static bool ContainsCandidate(List<TargetCandidate> candidates, int enemyId) // 후보 중복 확인
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Id == enemyId)
                {
                    return true; // 이미 있음
                }
            }

            return false;
        }

        private static EnemyController FindNearestCandidate(List<EnemyController> candidates, Vector3 point) // 중심에 가장 가까운 몬스터
        {
            EnemyController result = null;
            float bestDistanceSqr = float.PositiveInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                EnemyController candidate = candidates[i];
                if (!IsEnemyUsable(candidate))
                {
                    continue; // 빈 후보
                }

                Vector3 offset = candidate.transform.position - point;
                offset.y = 0f;
                float distanceSqr = offset.sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr)
                {
                    continue; // 더 멂
                }

                bestDistanceSqr = distanceSqr;
                result = candidate;
            }

            return result;
        }

        private static Vector3 GetHorizontalDirection(Vector3 primary, Vector3 secondary, Vector3 fallback) // 수평 방향
        {
            primary.y = 0f; // 수평화
            if (primary.sqrMagnitude > 0.0001f)
            {
                return primary.normalized; // 1순위
            }

            secondary.y = 0f; // 수평화
            if (secondary.sqrMagnitude > 0.0001f)
            {
                return secondary.normalized; // 2순위
            }

            fallback.y = 0f; // 수평화
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.right; // 최종
        }

        private static int GetGradePriority(EnemyGrade grade) // 등급 점수
        {
            switch (grade)
            {
                case EnemyGrade.Boss:
                    return 3; // 보스 최우선
                case EnemyGrade.Elite:
                    return 2; // 엘리트
                default:
                    return 1; // 일반
            }
        }

        private readonly struct TargetCandidate // 타겟 후보
        {
            public readonly EnemyController Enemy; // 대상 몬스터
            public readonly int Id; // 중복 방지 ID
            public readonly float Distance; // 기준 거리

            public TargetCandidate(EnemyController enemy, int id, float distance)
            {
                Enemy = enemy; // 대상
                Id = id; // ID
                Distance = distance; // 거리
            }
        }
    }
}
