using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class SegmentBlocker : MonoBehaviour // 세그먼트 차단체
    {
        private static readonly List<SegmentBlocker> ActiveBlockers = new List<SegmentBlocker>(128); // 차단 목록

        [Min(0.1f)] public float BlockRadius = 0.82f; // 차단 반경

        private void OnEnable() // 목록 등록
        {
            if (!ActiveBlockers.Contains(this))
            {
                ActiveBlockers.Add(this); // 차단 등록
            }
        }

        private void OnDisable() // 목록 해제
        {
            ActiveBlockers.Remove(this); // 차단 제거
        }

        public void Configure(float blockRadius) // 값 설정
        {
            BlockRadius = Mathf.Max(0.1f, blockRadius); // 반경 보정
        }

        public static Vector3 ResolveMonsterPosition(Vector3 currentPosition, Vector3 desiredPosition, float monsterRadius) // 몬스터 밀어내기
        {
            CleanupActiveList(); // null 정리

            Vector3 result = desiredPosition; // 보정 위치
            result.y = desiredPosition.y; // 높이 유지

            for (int i = 0; i < ActiveBlockers.Count; i++)
            {
                SegmentBlocker blocker = ActiveBlockers[i]; // 검사 대상
                if (blocker == null)
                {
                    continue; // 삭제됨
                }

                result = ResolveAgainstBlocker(blocker, currentPosition, result, monsterRadius); // 단일 보정
            }

            return result; // 최종 위치
        }

        private static Vector3 ResolveAgainstBlocker(SegmentBlocker blocker, Vector3 currentPosition, Vector3 desiredPosition, float monsterRadius) // 단일 차단
        {
            Vector3 center = blocker.transform.position; // 세그먼트 중심
            center.y = desiredPosition.y; // 평면 기준

            Vector3 current = currentPosition; // 현재 평면
            current.y = desiredPosition.y; // 높이 통일

            Vector3 desired = desiredPosition; // 목표 평면
            desired.y = desiredPosition.y; // 높이 통일

            float minDistance = blocker.BlockRadius + Mathf.Max(0.05f, monsterRadius); // 합산 반경
            Vector3 closest = GetClosestPointOnMove(current, desired, center); // 이동선 최근점
            Vector3 offset = closest - center; // 중심 거리
            offset.y = 0f; // 평면 거리

            if (offset.sqrMagnitude >= minDistance * minDistance)
            {
                return desiredPosition; // 충돌 없음
            }

            Vector3 normal = GetPushNormal(offset, current - center, blocker.transform.forward); // 밀림 방향
            Vector3 resolved = center + normal * minDistance; // 바깥 위치
            resolved.y = desiredPosition.y; // 높이 유지
            return resolved; // 보정 반환
        }

        private static Vector3 GetClosestPointOnMove(Vector3 start, Vector3 end, Vector3 point) // 이동선 최근점
        {
            Vector3 move = end - start; // 이동 벡터
            move.y = 0f; // 평면 이동
            if (move.sqrMagnitude <= 0.0001f)
            {
                return end; // 거의 정지
            }

            Vector3 offset = point - start; // 점 방향
            offset.y = 0f; // 평면 거리
            float t = Mathf.Clamp01(Vector3.Dot(offset, move) / move.sqrMagnitude); // 선분 위치
            return start + move * t; // 최근점
        }

        private static Vector3 GetPushNormal(Vector3 primary, Vector3 fallback, Vector3 finalFallback) // 밀림 방향 선택
        {
            primary.y = 0f; // 평면화
            if (primary.sqrMagnitude > 0.0001f)
            {
                return primary.normalized; // 충돌 방향
            }

            fallback.y = 0f; // 평면화
            if (fallback.sqrMagnitude > 0.0001f)
            {
                return fallback.normalized; // 현재 위치 기준
            }

            finalFallback.y = 0f; // 평면화
            if (finalFallback.sqrMagnitude > 0.0001f)
            {
                return finalFallback.normalized; // 세그먼트 방향
            }

            return Vector3.forward; // 최종 fallback
        }

        private static void CleanupActiveList() // 목록 정리
        {
            ActiveBlockers.RemoveAll(blocker => blocker == null); // null 제거
        }
    }
}

