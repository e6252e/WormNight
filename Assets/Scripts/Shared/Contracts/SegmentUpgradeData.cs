using UnityEngine;

namespace TeamProject01.Gameplay
{
    [System.Serializable]
    public struct SegmentUpgradeData // 레벨시스템 → 코어 세그먼트 강화값
    {
        public string SegmentId; // 대상 세그먼트 ID
        public float DamageMultiplierBonus; // 공격력 배율 증가
        public float AttackSpeedMultiplierBonus; // 공격속도 배율 증가
        public float RangeBonus; // 탐색/사거리 증가
        public float AreaRadiusBonus; // 폭발/범위 증가

        public bool HasAnyValue => DamageMultiplierBonus != 0f
            || AttackSpeedMultiplierBonus != 0f
            || RangeBonus != 0f
            || AreaRadiusBonus != 0f; // 적용 여부

        public bool IsValid => !string.IsNullOrWhiteSpace(SegmentId) && HasAnyValue; // 대상 포함 여부

        public static SegmentUpgradeData None => default; // 기본값

        public static SegmentUpgradeData Create(string segmentId, float damageBonus, float attackSpeedBonus, float rangeBonus, float areaRadiusBonus) // 생성
        {
            SegmentUpgradeData data = default; // 값 준비
            data.SegmentId = string.IsNullOrWhiteSpace(segmentId) ? string.Empty : segmentId.Trim(); // ID 보정
            data.DamageMultiplierBonus = damageBonus; // 공격력
            data.AttackSpeedMultiplierBonus = attackSpeedBonus; // 공속
            data.RangeBonus = rangeBonus; // 사거리
            data.AreaRadiusBonus = areaRadiusBonus; // 범위
            return data; // 결과 반환
        }

        public void AddValues(SegmentUpgradeData other) // 누적
        {
            if (string.IsNullOrWhiteSpace(SegmentId))
            {
                SegmentId = other.SegmentId; // 최초 ID
            }

            DamageMultiplierBonus += other.DamageMultiplierBonus; // 공격력 누적
            AttackSpeedMultiplierBonus += other.AttackSpeedMultiplierBonus; // 공속 누적
            RangeBonus += other.RangeBonus; // 사거리 누적
            AreaRadiusBonus += other.AreaRadiusBonus; // 범위 누적
        }

        public float ApplyDamage(float damage) // 세그먼트별 공격력 적용
        {
            return Mathf.Max(0f, damage) * Mathf.Max(0f, 1f + DamageMultiplierBonus); // 배율
        }

        public float ApplyFireInterval(float interval) // 세그먼트별 공속 적용
        {
            return Mathf.Max(0.05f, interval / Mathf.Max(0.01f, 1f + AttackSpeedMultiplierBonus)); // 쿨타임
        }

        public float ApplyRange(float range) // 세그먼트별 사거리 적용
        {
            return Mathf.Max(0.1f, range + RangeBonus); // 거리
        }

        public float ApplyAreaRadius(float radius) // 세그먼트별 범위 적용
        {
            return Mathf.Max(0.1f, radius + AreaRadiusBonus); // 범위
        }
    }
}
