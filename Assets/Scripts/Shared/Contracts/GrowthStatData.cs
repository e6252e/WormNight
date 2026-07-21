using UnityEngine;

namespace TeamProject01.Gameplay
{
    [System.Serializable]
    public struct GrowthStatData // 레벨시스템 → 코어 전달값
    {
        public GrowthChoiceType ChoiceType; // 선택 종류
        public int LevelDelta; // 레벨 증가량
        public float DamageMultiplierBonus; // 공격력 배율 증가
        public float MeleeDamageMultiplierBonus; // 밀리 공격력 배율 증가
        public float MagicDamageMultiplierBonus; // 마법 공격력 배율 증가
        public float AttackSpeedMultiplierBonus; // 공격속도 배율 증가
        public float TurnSpeedBonus; // 회전력 증가
        public float CollisionForceBonus; // 충돌힘 증가
        public float RejoinRangeBonus; // 재결합 범위 증가
        public string SegmentId; // 추가할 세그먼트 ID
        public int SegmentAddCount; // 추가할 세그먼트 수
        public SegmentUpgradeData SegmentUpgrade; // 특정 세그먼트 강화값

        public bool HasConvoyUpgrade => DamageMultiplierBonus != 0f
            || MeleeDamageMultiplierBonus != 0f
            || MagicDamageMultiplierBonus != 0f
            || AttackSpeedMultiplierBonus != 0f
            || TurnSpeedBonus != 0f
            || CollisionForceBonus != 0f
            || RejoinRangeBonus != 0f; // 공통 성장 여부

        public bool HasSegmentAddRequest => SegmentAddCount > 0 && !string.IsNullOrWhiteSpace(SegmentId); // 세그먼트 추가 여부
        public bool HasSegmentUpgrade => SegmentUpgrade.IsValid; // 세그먼트 강화 여부
        public bool HasAnyValue => LevelDelta != 0 || HasConvoyUpgrade || HasSegmentAddRequest || HasSegmentUpgrade; // 적용 여부

        public static GrowthStatData Create(int levelDelta, float damageBonus, float attackSpeedBonus, float turnBonus, float rejoinBonus) // 생성
        {
            return CreateConvoyUpgrade(levelDelta, damageBonus, attackSpeedBonus, turnBonus, 0f, rejoinBonus); // 기존 호환
        }

        public static GrowthStatData CreateConvoyUpgrade(int levelDelta, float damageBonus, float attackSpeedBonus, float turnBonus, float collisionForceBonus, float rejoinBonus, float meleeDamageBonus = 0f, float magicDamageBonus = 0f) // 컨보이 강화
        {
            GrowthStatData data = default; // 값 준비
            data.ChoiceType = GrowthChoiceType.ConvoyUpgrade; // 선택 종류
            data.LevelDelta = levelDelta; // 레벨 저장
            data.DamageMultiplierBonus = damageBonus; // 공격력 저장
            data.MeleeDamageMultiplierBonus = meleeDamageBonus; // 밀리 공격력 저장
            data.MagicDamageMultiplierBonus = magicDamageBonus; // 마법 공격력 저장
            data.AttackSpeedMultiplierBonus = attackSpeedBonus; // 공격속도 저장
            data.TurnSpeedBonus = turnBonus; // 회전력 저장
            data.CollisionForceBonus = collisionForceBonus; // 충돌힘 저장
            data.RejoinRangeBonus = Mathf.Max(0f, rejoinBonus); // 범위 저장
            return data; // 결과 반환
        }

        public static GrowthStatData CreateAddSegment(int levelDelta, string segmentId, int count = 1) // 세그먼트 추가
        {
            GrowthStatData data = default; // 값 준비
            data.ChoiceType = GrowthChoiceType.AddSegment; // 선택 종류
            data.LevelDelta = levelDelta; // 레벨 저장
            data.SegmentId = string.IsNullOrWhiteSpace(segmentId) ? string.Empty : segmentId.Trim(); // 추가 ID
            data.SegmentAddCount = Mathf.Max(1, count); // 추가 수
            return data; // 결과 반환
        }

        public static GrowthStatData CreateSegmentUpgrade(int levelDelta, SegmentUpgradeData upgrade) // 세그먼트 강화
        {
            GrowthStatData data = default; // 값 준비
            data.ChoiceType = GrowthChoiceType.UpgradeSegment; // 선택 종류
            data.LevelDelta = levelDelta; // 레벨 저장
            data.SegmentUpgrade = upgrade; // 강화값
            return data; // 결과 반환
        }

        public static GrowthStatData CreateSegmentUpgrade(int levelDelta, string segmentId, float damageBonus, float attackSpeedBonus, float rangeBonus, float areaRadiusBonus) // 세그먼트 강화
        {
            SegmentUpgradeData upgrade = SegmentUpgradeData.Create(segmentId, damageBonus, attackSpeedBonus, rangeBonus, areaRadiusBonus); // 강화값
            return CreateSegmentUpgrade(levelDelta, upgrade); // 결과 반환
        }
    }
}
