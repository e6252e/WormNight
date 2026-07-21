using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class NexusAttackSlotManager : MonoBehaviour // Nexus 주변 공격 자리를 관리하는 Script
    {
        private struct SlotReservation // 공격 자리 예약 정보
        {
            public EnemyController enemy; // 이 자리를 예약한 몬스터
            public Vector3 position; // 예약된 공격 자리 위치
            public float attackRadius; // 이 몬스터가 요청한 공격 사거리 반지름
            public int slotIndex; // 이 몬스터가 예약한 공격 슬롯 번호
        }

        private Transform nexusTransform; // 공격 자리 기준이 될 Nexus Transform

        private string nexusObjectName = "Nexus_Core"; // Inspector 연결이 없을 때 자동으로 찾을 Nexus 오브젝트 이름

        [Header("Slot Setting")]
        [Min(0.1f)]
        [SerializeField] private float minAttackRadius = 1.0f; // 너무 작은 공격 사거리 요청을 막기 위한 최소 반지름

        [Min(0.0f)]
        [SerializeField] private float slotInsidePadding = 0.4f; // 공격 사거리보다 안쪽에 슬롯을 만들 거리

        [Min(0.1f)]
        [SerializeField] private float slotOccupyRadius = 1.0f; // 한 몬스터가 차지하는 공격 자리 반지름

        [Min(4)]
        [SerializeField] private int minCandidateCount = 12; // 공격 자리 후보 최소 개수

        [Min(4)]
        [SerializeField] private int maxCandidateCount = 64; // 공격 자리 후보 최대 개수

        [Min(0.0f)]
        [SerializeField] private float slotGroundHeight = 0.72f; // 공격 자리 위치의 높이 보정

        [Header("Slot Request")]
        [Min(0.0f)]
        [SerializeField] private float defaultSlotRequestPadding = 6.0f; // 몬스터들이 공격 사거리보다 얼마나 바깥에서 미리 자리를 예약할지 정하는 공통값

        [Header("Overlap Slot")]
        [SerializeField] private bool allowOverlapWhenFull = true; // 빈 공격 슬롯이 없을 때 기존 공격 슬롯에 겹쳐 배정할지

        [Header("Gizmo")]
        [SerializeField] private bool drawSlotGizmos = true; // Scene에서 공격 자리 표시 여부

        [SerializeField] private bool drawSlotLines = true; // Nexus 중심에서 슬롯까지 선을 표시할지

        [SerializeField] private float[] previewAttackRadii = new float[] { 6.0f, 10.0f }; // Scene에서 미리 볼 공격 사거리 반지름 목록

        private readonly List<SlotReservation> reservations = new List<SlotReservation>(128); // 현재 예약된 공격 자리 목록

        public float DefaultSlotRequestPadding
        {
            get
            {
                return Mathf.Max(0.0f, defaultSlotRequestPadding);
            }
        }

        private void Awake()
        {
            FindNexusIfNeeded(); // Nexus 참조가 비어 있으면 자동으로 찾는다.
        }

        private void OnValidate()
        {
            if (minAttackRadius < 0.1f) // 최소 반지름이 너무 작다면
            {
                minAttackRadius = 0.1f; // 최소값으로 보정한다.
            }

            if (slotInsidePadding < 0.0f) // 안쪽 보정값이 음수라면
            {
                slotInsidePadding = 0.0f; // 최소값으로 보정한다.
            }

            if (slotOccupyRadius < 0.1f) // 슬롯 점유 반지름이 너무 작다면
            {
                slotOccupyRadius = 0.1f; // 최소값으로 보정한다.
            }

            if (minCandidateCount < 4) // 후보 최소 개수가 너무 작다면
            {
                minCandidateCount = 4; // 최소 4개로 보정한다.
            }

            if (maxCandidateCount < minCandidateCount) // 후보 최대 개수가 최소 개수보다 작다면
            {
                maxCandidateCount = minCandidateCount; // 최대 개수를 최소 개수에 맞춘다.
            }

            if (defaultSlotRequestPadding < 0.0f) // 슬롯 요청 추가 거리가 음수라면
            {
                defaultSlotRequestPadding = 0.0f; // 최소값으로 보정한다.
            }
        }

        public bool TryReserveNearestSlot(EnemyController requester, Vector3 fromPosition, float attackRadius, out Vector3 slotPosition) // 공격 사거리 반지름 기준으로 빈 공격 자리 또는 겹침 공격 자리를 예약한다.
        {
            FindNexusIfNeeded(); // Nexus 참조가 비어 있으면 자동으로 찾는다.
            CleanupInvalidReservations(); // 죽었거나 사라진 예약을 정리한다.

            slotPosition = Vector3.zero; // 실패 기본값

            if (requester == null) // 요청 몬스터가 없다면
            {
                return false; // 예약할 수 없다.
            }

            if (nexusTransform == null) // Nexus 참조가 없다면
            {
                return false; // 위치를 계산할 수 없다.
            }

            float safeAttackRadius = Mathf.Max(minAttackRadius, attackRadius); // 공격 사거리 반지름을 최소값 이상으로 보정한다.
            int existingIndex = FindReservationIndex(requester); // 이미 예약한 자리가 있는지 확인한다.

            if (existingIndex >= 0) // 기존 예약이 있다면
            {
                SlotReservation existingReservation = reservations[existingIndex]; // 기존 예약 정보를 가져온다.

                if (Mathf.Abs(existingReservation.attackRadius - safeAttackRadius) > 0.05f) // 공격 사거리가 바뀌었다면
                {
                    reservations.RemoveAt(existingIndex); // 기존 예약을 제거하고 새 반지름 기준으로 다시 예약한다.
                }
                else
                {
                    slotPosition = existingReservation.position; // 기존 예약 위치를 반환한다.
                    return true; // 기존 예약 있음
                }
            }

            int candidateCount = CalculateCandidateCount(safeAttackRadius); // 반지름에 맞는 후보 자리 개수를 계산한다.

            if (TryFindNearestEmptySlot(requester, fromPosition, safeAttackRadius, candidateCount, out Vector3 emptySlotPosition, out int emptySlotIndex)) // 빈 공격 슬롯이 있다면
            {
                AddReservation(requester, emptySlotPosition, safeAttackRadius, emptySlotIndex); // 빈 공격 슬롯을 예약한다.
                slotPosition = emptySlotPosition; // 예약된 위치를 반환한다.
                return true; // 예약 성공
            }

            if (allowOverlapWhenFull && TryFindLeastStackedSlot(requester, fromPosition, safeAttackRadius, candidateCount, out Vector3 stackedSlotPosition, out int stackedSlotIndex)) // 빈 슬롯이 없다면 가장 적게 겹친 공격 슬롯을 찾는다.
            {
                AddReservation(requester, stackedSlotPosition, safeAttackRadius, stackedSlotIndex); // 기존 공격 슬롯에 겹쳐 예약한다.
                slotPosition = stackedSlotPosition; // 예약된 위치를 반환한다.
                return true; // 예약 성공
            }

            return false; // 겹침 배정까지 허용하지 않으면 예약 실패
        }

        public void ReleaseSlot(EnemyController requester) // 특정 몬스터가 예약한 공격 자리를 해제한다.
        {
            if (requester == null) // 요청 몬스터가 없다면
            {
                return; // 처리하지 않는다.
            }

            for (int i = reservations.Count - 1; i >= 0; i--) // 뒤에서부터 예약 목록을 순회한다.
            {
                if (reservations[i].enemy == requester) // 요청 몬스터가 예약한 자리라면
                {
                    reservations.RemoveAt(i); // 예약을 제거한다.
                }
            }
        }

        public Vector3 GetSlotPosition(float attackRadius, int slotIndex, int candidateCount) // 공격 사거리 반지름과 후보 번호로 월드 위치를 계산한다.
        {
            FindNexusIfNeeded(); // Nexus 참조가 비어 있으면 자동으로 찾는다.

            if (nexusTransform == null) // Nexus 참조가 없다면
            {
                return transform.position; // 임시로 자기 위치를 반환한다.
            }

            float safeAttackRadius = Mathf.Max(minAttackRadius, attackRadius); // 공격 반지름을 보정한다.
            float slotRadius = GetSlotRadius(safeAttackRadius); // 실제 슬롯 위치는 공격 사거리보다 조금 안쪽으로 잡는다.
            int safeCandidateCount = Mathf.Max(1, candidateCount); // 후보 개수를 보정한다.
            float angle = 360.0f / safeCandidateCount * slotIndex; // 후보 번호에 따른 각도를 계산한다.
            float radian = angle * Mathf.Deg2Rad; // 각도를 라디안으로 변환한다.
            Vector3 direction = new Vector3(Mathf.Cos(radian), 0.0f, Mathf.Sin(radian)); // Nexus 중심에서 후보 자리 방향을 계산한다.
            Vector3 position = nexusTransform.position + direction * slotRadius; // Nexus 중심 기준 후보 자리 위치를 계산한다.
            position.y = slotGroundHeight; // 높이를 보정한다.
            return position; // 후보 자리 위치를 반환한다.
        }

        private float GetSlotRadius(float attackRadius) // 공격 사거리에서 실제 슬롯 반지름을 계산한다.
        {
            float safeAttackRadius = Mathf.Max(minAttackRadius, attackRadius); // 공격 반지름을 보정한다.
            float slotRadius = safeAttackRadius - slotInsidePadding; // 공격 사거리보다 안쪽으로 슬롯을 넣는다.
            return Mathf.Max(minAttackRadius, slotRadius); // 최소 반지름보다 작아지지 않게 보정한다.
        }

        private bool TryFindNearestEmptySlot(EnemyController requester, Vector3 fromPosition, float attackRadius, int candidateCount, out Vector3 bestPosition, out int bestSlotIndex) // 가장 가까운 빈 공격 슬롯을 찾는다.
        {
            bestPosition = Vector3.zero; // 실패 기본값
            bestSlotIndex = -1; // 실패 기본값

            float bestDistanceSqr = float.MaxValue; // 가장 가까운 자리 비교값
            bool foundSlot = false; // 빈 자리를 찾았는지 여부

            for (int i = 0; i < candidateCount; i++) // 모든 후보 자리를 순회한다.
            {
                Vector3 candidatePosition = GetSlotPosition(attackRadius, i, candidateCount); // 후보 자리 위치를 계산한다.

                if (IsPositionBlocked(candidatePosition, requester)) // 다른 몬스터가 이미 차지한 자리라면
                {
                    continue; // 빈 슬롯 후보에서는 제외한다.
                }

                float distanceSqr = (candidatePosition - fromPosition).sqrMagnitude; // 현재 위치에서 후보 자리까지의 거리 제곱을 계산한다.

                if (distanceSqr < bestDistanceSqr) // 더 가까운 후보라면
                {
                    bestDistanceSqr = distanceSqr; // 최단 거리 값을 갱신한다.
                    bestPosition = candidatePosition; // 최적 위치를 갱신한다.
                    bestSlotIndex = i; // 최적 슬롯 번호를 저장한다.
                    foundSlot = true; // 빈 자리를 찾았다고 표시한다.
                }
            }

            return foundSlot; // 빈 슬롯 발견 여부를 반환한다.
        }

        private bool TryFindLeastStackedSlot(EnemyController requester, Vector3 fromPosition, float attackRadius, int candidateCount, out Vector3 bestPosition, out int bestSlotIndex) // 가장 적게 겹친 공격 슬롯을 찾는다.
        {
            bestPosition = Vector3.zero; // 실패 기본값
            bestSlotIndex = -1; // 실패 기본값

            int bestStackCount = int.MaxValue; // 가장 적게 겹친 수
            float bestDistanceSqr = float.MaxValue; // 같은 겹침 수일 때 가까운 자리 비교값
            bool foundSlot = false; // 겹침 자리를 찾았는지 여부

            for (int i = 0; i < candidateCount; i++) // 모든 후보 자리를 순회한다.
            {
                Vector3 candidatePosition = GetSlotPosition(attackRadius, i, candidateCount); // 후보 자리 위치를 계산한다.
                int stackCount = CountReservationsNearPosition(candidatePosition, requester); // 이 후보 위치에 이미 몇 마리가 배정되었는지 계산한다.
                float distanceSqr = (candidatePosition - fromPosition).sqrMagnitude; // 현재 위치에서 후보 자리까지의 거리 제곱을 계산한다.

                if (stackCount < bestStackCount) // 더 적게 겹친 후보라면
                {
                    bestStackCount = stackCount; // 겹침 수를 갱신한다.
                    bestDistanceSqr = distanceSqr; // 거리 값을 갱신한다.
                    bestPosition = candidatePosition; // 최적 위치를 갱신한다.
                    bestSlotIndex = i; // 최적 슬롯 번호를 저장한다.
                    foundSlot = true; // 후보를 찾았다고 표시한다.
                    continue; // 다음 후보를 확인한다.
                }

                if (stackCount == bestStackCount && distanceSqr < bestDistanceSqr) // 겹침 수가 같고 더 가까운 후보라면
                {
                    bestDistanceSqr = distanceSqr; // 거리 값을 갱신한다.
                    bestPosition = candidatePosition; // 최적 위치를 갱신한다.
                    bestSlotIndex = i; // 최적 슬롯 번호를 저장한다.
                    foundSlot = true; // 후보를 찾았다고 표시한다.
                }
            }

            return foundSlot; // 겹침 슬롯 발견 여부를 반환한다.
        }

        private int CountReservationsNearPosition(Vector3 candidatePosition, EnemyController requester) // 후보 위치에 몇 마리가 이미 배정되었는지 계산한다.
        {
            int count = 0; // 겹친 예약 수
            float stackDistance = Mathf.Max(0.05f, slotOccupyRadius * 0.5f); // 같은 슬롯으로 볼 거리
            float stackDistanceSqr = stackDistance * stackDistance; // 거리 비교용 제곱값

            for (int i = reservations.Count - 1; i >= 0; i--) // 예약 목록을 순회한다.
            {
                SlotReservation reservation = reservations[i]; // 현재 예약 정보를 가져온다.

                if (reservation.enemy == null || reservation.enemy.IsDead) // 예약자가 없거나 죽었다면
                {
                    reservations.RemoveAt(i); // 잘못된 예약을 제거한다.
                    continue; // 다음 예약을 확인한다.
                }

                if (reservation.enemy == requester) // 자기 자신의 예약이라면
                {
                    continue; // 겹침 수에서 제외한다.
                }

                Vector3 reservedPosition = reservation.position; // 다른 몬스터가 예약한 위치를 가져온다.
                reservedPosition.y = candidatePosition.y; // 높이 차이를 제거한다.

                if ((reservedPosition - candidatePosition).sqrMagnitude <= stackDistanceSqr) // 같은 슬롯에 배정된 몬스터라면
                {
                    count++; // 겹침 수를 증가한다.
                }
            }

            return count; // 겹침 수를 반환한다.
        }

        private void AddReservation(EnemyController requester, Vector3 position, float attackRadius, int slotIndex) // 새 예약 정보를 추가한다.
        {
            SlotReservation reservation = new SlotReservation(); // 새 예약 정보를 만든다.
            reservation.enemy = requester; // 예약 몬스터를 저장한다.
            reservation.position = position; // 예약 위치를 저장한다.
            reservation.attackRadius = attackRadius; // 요청 반지름을 저장한다.
            reservation.slotIndex = slotIndex; // 예약 슬롯 번호를 저장한다.

            reservations.Add(reservation); // 예약 목록에 추가한다.
        }

        private int FindReservationIndex(EnemyController requester) // 이미 예약한 자리의 인덱스를 찾는다.
        {
            for (int i = reservations.Count - 1; i >= 0; i--) // 예약 목록을 순회한다.
            {
                SlotReservation reservation = reservations[i]; // 현재 예약 정보를 가져온다.

                if (reservation.enemy == null || reservation.enemy.IsDead) // 예약자가 없거나 죽었다면
                {
                    reservations.RemoveAt(i); // 잘못된 예약을 제거한다.
                    continue; // 다음 예약을 확인한다.
                }

                if (reservation.enemy == requester) // 요청 몬스터의 예약이라면
                {
                    return i; // 예약 인덱스를 반환한다.
                }
            }

            return -1; // 기존 예약 없음
        }

        private bool IsPositionBlocked(Vector3 candidatePosition, EnemyController requester) // 후보 위치가 다른 예약자와 겹치는지 확인한다.
        {
            float blockDistance = slotOccupyRadius * 2.0f; // 두 몬스터가 서로 겹치지 않기 위한 최소 거리
            float blockDistanceSqr = blockDistance * blockDistance; // 거리 비교용 제곱값

            for (int i = reservations.Count - 1; i >= 0; i--) // 예약 목록을 순회한다.
            {
                SlotReservation reservation = reservations[i]; // 현재 예약 정보를 가져온다.

                if (reservation.enemy == null || reservation.enemy.IsDead) // 예약자가 없거나 죽었다면
                {
                    reservations.RemoveAt(i); // 잘못된 예약을 제거한다.
                    continue; // 다음 예약을 확인한다.
                }

                if (reservation.enemy == requester) // 자기 자신의 예약이라면
                {
                    continue; // 겹침 검사에서 제외한다.
                }

                Vector3 reservedPosition = reservation.position; // 다른 몬스터가 예약한 위치를 가져온다.
                reservedPosition.y = candidatePosition.y; // 높이 차이를 제거한다.

                if ((reservedPosition - candidatePosition).sqrMagnitude < blockDistanceSqr) // 다른 예약 위치와 너무 가깝다면
                {
                    return true; // 이 후보는 막혀 있다.
                }
            }

            return false; // 사용할 수 있는 위치다.
        }

        private int CalculateCandidateCount(float attackRadius) // 공격 반지름에 따라 후보 자리 개수를 계산한다.
        {
            float safeAttackRadius = Mathf.Max(minAttackRadius, attackRadius); // 공격 반지름을 보정한다.
            float slotRadius = GetSlotRadius(safeAttackRadius); // 실제 슬롯 반지름을 가져온다.
            float circumference = 2.0f * Mathf.PI * slotRadius; // 해당 반지름의 원 둘레를 계산한다.
            float spacing = Mathf.Max(0.1f, slotOccupyRadius * 2.0f); // 후보 간 최소 간격을 계산한다.
            int calculatedCount = Mathf.CeilToInt(circumference / spacing); // 원 둘레에 들어갈 수 있는 후보 수를 계산한다.
            return Mathf.Clamp(calculatedCount, minCandidateCount, maxCandidateCount); // 최소/최대 후보 수로 제한한다.
        }

        private void CleanupInvalidReservations() // 죽었거나 사라진 몬스터 예약을 정리한다.
        {
            for (int i = reservations.Count - 1; i >= 0; i--) // 예약 목록을 뒤에서부터 순회한다.
            {
                if (reservations[i].enemy == null || reservations[i].enemy.IsDead) // 예약자가 없거나 죽었다면
                {
                    reservations.RemoveAt(i); // 예약을 제거한다.
                }
            }
        }

        private void FindNexusIfNeeded() // Nexus 참조가 비어 있으면 이름으로 자동 탐색한다.
        {
            if (nexusTransform != null) // 이미 Nexus가 연결되어 있다면
            {
                return; // 다시 찾지 않는다.
            }

            GameObject nexusObject = GameObject.Find(nexusObjectName); // 이름으로 Nexus 오브젝트를 찾는다.

            if (nexusObject == null) // Nexus를 찾지 못했다면
            {
                return; // 자동 연결하지 않는다.
            }

            nexusTransform = nexusObject.transform; // 찾은 Nexus Transform을 저장한다.
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawSlotGizmos) // 슬롯 표시를 끈 상태라면
            {
                return; // 표시하지 않는다.
            }

            FindNexusIfNeeded(); // Scene 표시를 위해 Nexus 참조를 자동으로 찾는다.

            if (nexusTransform == null) // Nexus 참조가 없다면
            {
                return; // 슬롯을 표시할 수 없다.
            }

            if (previewAttackRadii == null) // 미리보기 반지름 배열이 없다면
            {
                return; // 표시하지 않는다.
            }

            for (int r = 0; r < previewAttackRadii.Length; r++) // 미리보기 반지름 목록을 순회한다.
            {
                float previewRadius = Mathf.Max(minAttackRadius, previewAttackRadii[r]); // 미리보기 반지름을 보정한다.
                int candidateCount = CalculateCandidateCount(previewRadius); // 미리보기 후보 개수를 계산한다.

                for (int i = 0; i < candidateCount; i++) // 해당 반지름의 모든 후보를 순회한다.
                {
                    Vector3 slotPosition = GetSlotPosition(previewRadius, i, candidateCount); // 후보 위치를 계산한다.
                    Gizmos.DrawWireSphere(slotPosition, slotOccupyRadius); // 후보 위치를 원으로 표시한다.

                    if (drawSlotLines) // 중심선 표시가 켜져 있다면
                    {
                        Gizmos.DrawLine(nexusTransform.position, slotPosition); // Nexus 중심에서 후보 위치까지 선을 그린다.
                    }
                }
            }
        }
    }
}