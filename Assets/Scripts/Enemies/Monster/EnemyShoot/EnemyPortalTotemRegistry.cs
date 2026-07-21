using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public static class EnemyPortalTotemRegistry // 현재 활성화된 입구 토템들을 관리하는 클래스
    {
        private static readonly List<EnemyPortalTotem> activeEntryTotems = new List<EnemyPortalTotem>(8);// 현재 활성화된 입구 토템 목록

        public static void RegisterEntryTotem(EnemyPortalTotem totem)// 입구 토템을 목록에 등록하는 함수
        {
            if (totem == null)
            {
                return;// 등록할 토템이 없다면 종료한다.
            }

            if (activeEntryTotems.Contains(totem))
            {
                return;// 이미 등록된 토템이라면 중복 등록하지 않는다.
            }

            activeEntryTotems.Add(totem);// 활성 입구 토템 목록에 추가한다.
        }

        public static void UnregisterEntryTotem(EnemyPortalTotem totem)// 입구 토템을 목록에서 제거하는 함수
        {
            if (totem == null)
            {
                return;// 제거할 토템이 없다면 종료한다.
            }

            activeEntryTotems.Remove(totem);// 활성 입구 토템 목록에서 제거한다.
        }

        public static bool TryGetAttractTarget(Vector3 enemyPosition, out EnemyPortalTotem targetTotem)// 몬스터를 유도할 가장 가까운 입구 토템을 찾는 함수
        {
            CleanupInactiveTotems();// 삭제되었거나 비활성화된 토템을 목록에서 정리한다.

            targetTotem = null;// 토템을 찾지 못했을 때 사용할 기본값

            float nearestDistanceSqr = float.MaxValue;// 현재까지 찾은 가장 가까운 거리 제곱값

            for (int i = 0; i < activeEntryTotems.Count; i++)
            {
                EnemyPortalTotem currentTotem = activeEntryTotems[i];// 현재 확인할 입구 토템

                if (currentTotem == null)
                {
                    continue;// 삭제된 토템이라면 제외한다.
                }

                if (!currentTotem.IsActive)
                {
                    continue;// 비활성화된 토템이라면 제외한다.
                }

                if (!currentTotem.IsEntry)
                {
                    continue;// 입구 토템이 아니라면 제외한다.
                }

                Vector3 offset = currentTotem.transform.position - enemyPosition;// 몬스터에서 토템까지의 거리 벡터를 구한다.

                offset.y = 0.0f;// 높이 차이를 제외하고 평면 거리만 계산한다.

                float distanceSqr = offset.sqrMagnitude;// 토템까지의 거리 제곱값을 계산한다.

                float attractRadius = currentTotem.AttractRadius;// 현재 토템의 몬스터 유도 범위를 가져온다.

                if (distanceSqr > attractRadius * attractRadius)
                {
                    continue;// 몬스터가 현재 토템의 유도 범위 밖이라면 제외한다.
                }

                if (distanceSqr >= nearestDistanceSqr)
                {
                    continue;// 기존에 찾은 토템보다 멀다면 목표를 바꾸지 않는다.
                }

                nearestDistanceSqr = distanceSqr;// 가장 가까운 거리 제곱값을 갱신한다.

                targetTotem = currentTotem;// 가장 가까운 입구 토템을 이동 목표로 저장한다.
            }

            return targetTotem != null;// 유효한 입구 토템을 찾았다면 true를 반환한다.
        }

        private static void CleanupInactiveTotems()// 사용할 수 없는 토템을 목록에서 정리하는 함수
        {
            activeEntryTotems.RemoveAll(totem => totem == null || !totem.IsActive || !totem.IsEntry);// 삭제되었거나 비활성화된 토템을 제거한다.
        }
    }
}