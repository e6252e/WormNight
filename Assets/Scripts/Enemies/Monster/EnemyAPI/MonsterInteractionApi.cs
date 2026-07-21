// 전찬우생성
using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public static class MonsterInteractionApi
    {
        private struct KnockbackRequest
        {
            public Vector3 Center;
            public float Radius;
            public float Distance;
            public float Duration;
            public float Height;
            public float ExpireTime;
        }

        private static readonly List<KnockbackRequest> knockbackRequests = new List<KnockbackRequest>(16); // 소비 대기 넉백
        private static Transform convoyTarget; // 컨보이 타겟 캐시

        public static void RegisterConvoyTarget(Transform target)
        {
            if (target == null)
            {
                return;
            }

            convoyTarget = target;
        }

        public static void UnregisterConvoyTarget(Transform target)
        {
            if (convoyTarget == target)
            {
                convoyTarget = null;
            }
        }

        public static bool TryGetConvoyTarget(out Transform target)
        {
            if (convoyTarget != null && convoyTarget.gameObject.activeInHierarchy)
            {
                target = convoyTarget;
                return true;
            }

            target = null;
            return false;
        }

        public static bool TryGetRandomAttachedWeaponSegment(out Transform targetSegment) //조성원추가-0622 무작위 부착 무기 세그먼트 요청
        {
            targetSegment = null;

            if (!TryGetConvoyController(out ConvoyController convoyController))
            {
                return false;
            }

            return convoyController.TryGetRandomAttachedWeaponSegment(out targetSegment);
        }

        public static bool TryGetSegmentCutTailFollowTarget(out Transform tailSegment) // 전찬우수정-0630 - 절단 몬스터가 따라갈 컨보이 꼬리 세그먼트 요청
        {
            tailSegment = null;

            if (!TryGetConvoyController(out ConvoyController convoyController))
            {
                return false;
            }

            return convoyController.TryGetSegmentCutTailFollowTarget(out tailSegment);
        }

        public static bool TeleportMonster(EnemyController enemy, Vector3 destination) // 지원형 웜홀 몬스터 이동
        {
            if (enemy == null || enemy.IsDead)
            {
                return false; // 대상 없음
            }

            EnemyMovement movement = enemy.GetComponent<EnemyMovement>();
            if (movement != null)
            {
                movement.ForceTeleport(destination); // 이동 상태까지 정리
                return true;
            }

            enemy.transform.position = GroundService.ProjectToGround(destination, 0f); // fallback
            return true;
        }

        public static bool HasAvailableSegmentCutTarget() //조성원추가-0626 절단 가능한 무기 세그먼트가 존재하는지 확인
        {
            if (!TryGetConvoyController(out ConvoyController convoyController)) //조성원추가-0626 등록된 컨보이 또는 ConvoyController를 찾지 못했다면
            {
                return false; //조성원추가-0626 절단 가능한 대상이 없다고 반환한다.
            }

            return convoyController.HasAvailableSegmentCutTarget(); //조성원추가-0626 대상을 예약하지 않고 절단 가능한 세그먼트 존재 여부만 반환한다.
        }

        public static bool IsConvoyHeadCollider(Collider other) //조성원추가-0622 컨보이 머리 충돌 확인
        {
            if (!TryGetConvoyController(out ConvoyController convoyController))
            {
                return false;
            }

            return convoyController.IsConvoyHeadCollider(other);
        }

        public static bool IsTargetWeaponSegmentCollider(Collider other, Transform targetSegment) //조성원추가-0622 선택된 무기 세그먼트 충돌 확인
        {
            if (!TryGetConvoyController(out ConvoyController convoyController))
            {
                return false;
            }

            return convoyController.IsTargetSegmentCollider(other, targetSegment);
        }

        public static bool RequestSegmentCut(Transform targetSegment) //조성원추가-0622 선택된 세그먼트 절단 요청
        {
            if (!TryGetConvoyController(out ConvoyController convoyController))
            {
                return false;
            }

            return convoyController.TryCutTailFromTargetSegment(targetSegment);
        }

        public static bool IsAttachedSegmentCutTarget(Transform targetSegment) //조성원추가-0622 절단 대상 연결 상태 확인
        {
            if (!TryGetConvoyController(out ConvoyController convoyController))
            {
                return false;
            }

            return convoyController.IsAttachedSegmentCutTarget(targetSegment);
        }

        public static void ReleaseSegmentCutTarget(Transform targetSegment) //조성원추가-0622 절단 대상 예약 해제
        {
            if (!TryGetConvoyController(out ConvoyController convoyController))
            {
                return;
            }

            convoyController.ReleaseSegmentCutTarget(targetSegment);
        }

        public static int RequestSegmentShockwave(Vector3 center, float radius, float pushDistance, float recoveryDuration) //조성원추가-0622 점프 몬스터의 착지 충격파를 컨보이에 요청
        {
            if (!TryGetConvoyController(out ConvoyController convoyController)) //조성원추가-0622 등록된 컨보이를 찾지 못했다면
            {
                return 0; //조성원추가-0622 영향받은 세그먼트 없음
            }

            return convoyController.ApplySegmentShockwave(center, radius, pushDistance, recoveryDuration); //조성원추가-0622 범위 안 연결 세그먼트에 충격파 적용
        }

        private static bool TryGetConvoyController(out ConvoyController convoyController) //조성원추가-0622 등록된 컨보이에서 ConvoyController 확인
        {
            convoyController = null;

            if (!TryGetConvoyTarget(out Transform target))
            {
                return false;
            }

            convoyController = target.GetComponent<ConvoyController>();

            if (convoyController == null)
            {
                convoyController = target.GetComponentInParent<ConvoyController>();
            }

            return convoyController != null;
        }

        public static void RequestConvoyKnockback(Vector3 center, float radius, float distance, float duration)
        {
            RequestConvoyKnockback(center, radius, distance, duration, 0.0f);
        }

        public static void RequestConvoyKnockback(Vector3 center, float radius, float distance, float duration, float height)
        {
            center.y = 0.0f; // 바닥 평면 기준

            KnockbackRequest request = new KnockbackRequest();
            request.Center = center;
            request.Radius = Mathf.Max(0.1f, radius);
            request.Distance = Mathf.Max(0.0f, distance);
            request.Duration = Mathf.Max(0.01f, duration);
            request.Height = Mathf.Max(0.0f, height);
            request.ExpireTime = Time.time + 0.25f;

            knockbackRequests.Add(request);
        }

        public static bool TryConsumeConvoyKnockback(Vector3 targetPosition, out Vector3 direction, out float distance, out float duration)
        {
            return TryConsumeConvoyKnockback(targetPosition, out direction, out distance, out duration, out _);
        }

        public static bool TryConsumeConvoyKnockback(Vector3 targetPosition, out Vector3 direction, out float distance, out float duration, out float height)
        {
            targetPosition.y = 0.0f; // 바닥 평면 기준

            for (int i = knockbackRequests.Count - 1; i >= 0; i--)
            {
                KnockbackRequest request = knockbackRequests[i];

                if (Time.time > request.ExpireTime)
                {
                    knockbackRequests.RemoveAt(i);
                    continue;
                }

                Vector3 offset = targetPosition - request.Center;
                offset.y = 0.0f;

                if (offset.sqrMagnitude > request.Radius * request.Radius)
                {
                    continue;
                }

                direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector3.forward;

                distance = request.Distance;
                duration = request.Duration;
                height = request.Height;

                knockbackRequests.RemoveAt(i);
                return true;
            }

            direction = Vector3.zero;
            distance = 0.0f;
            duration = 0.0f;
            height = 0.0f;

            return false;
        }

        public static void ClearConvoyKnockbackRequests()
        {
            knockbackRequests.Clear();
        }

        public static float GetConvoySpeedMultiplier(Vector3 convoyPosition)
        {
            return EnemySlowZone.GetSpeedMultiplier(convoyPosition);
        }

        public static Vector3 ResolveConvoyPosition(Vector3 currentPosition, Vector3 desiredPosition, float moverRadius)
        {
            return EnemyObstacle.ResolvePosition(currentPosition, desiredPosition, moverRadius);
        }

        public static Vector3 ResolveMonsterPosition(Vector3 currentPosition, Vector3 desiredPosition, float monsterRadius)
        {
            return SegmentBlocker.ResolveMonsterPosition(currentPosition, desiredPosition, monsterRadius);
        }

        public static Vector3 ResolveMonsterCrowdPosition(EnemyCrowdBlocker crowdBlocker, Vector3 currentPosition, Vector3 desiredPosition, float fallbackRadius)
        {
            return EnemyCrowdBlocker.ResolvePosition(crowdBlocker, currentPosition, desiredPosition, fallbackRadius);
        }
    }
}
