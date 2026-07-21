using TeamProject01.Gameplay;
using UnityEngine;

[CreateAssetMenu(menuName = "OZ/Growth/Stat Upgrade Definition", fileName = "SU_NewCard")]
public sealed class StatUpgradeDefinition : ScriptableObject // 공통 강화 카드 데이터
{
    private const string DefaultCardState = "기존";

    [Header("카드 표시")]
    public string CardId; // 예: DamageCard
    public string CardState = DefaultCardState; // 밸런스 테이블 상태
    public string DisplayName; // 카드 이름
    [TextArea(2, 4)] public string Description; // 카드 설명, (N)=등급 수치

    [Header("카드 프리팹/이미지")]
    public GameObject CardPrefabOverride; // 비우면 카탈로그 기본 프리팹 사용
    public Sprite CardIconSprite; // 카드 아이콘
    [Range(-100f, 100f)] public float CardIconSizeOffset = 0f; // 아이콘 크기 조절

    [Header("코어 성장 연동")]
    [Min(1)] public int LevelDelta = 1; // 선택 시 소비할 레벨 증가량
    [Header("공격력 배율 보너스")]
    public float DamageMultiplierBonus; // 전체 무기 공격력 배율 보너스
    [Header("밀리 공격력 배율 보너스")]
    public float MeleeDamageMultiplierBonus; // 밀리 공격력 배율 보너스
    [Header("마법 공격력 배율 보너스")]
    public float MagicDamageMultiplierBonus; // 마법 공격력 배율 보너스
    [Header("쿨타임 감소 보너스")]
    public float AttackSpeedMultiplierBonus; // 쿨타임 감소 보너스
    [Header("핸들링 보너스")]
    public float TurnSpeedBonus; // 핸들링 보너스
    [Header("충돌힘 보너스")]
    public float CollisionForceBonus; // 충돌힘 보너스
    [Header("재결합 범위 보너스")]
    public float RejoinRangeBonus; // 재결합 범위 보너스
    [Header("넥서스 체력 보너스")]
    public float NexusHealthBonus; // 넥서스 체력 보너스

    public string NormalizedId => string.IsNullOrWhiteSpace(CardId) ? name : CardId.Trim(); // 비교 ID
    public string ResolvedCardState => string.IsNullOrWhiteSpace(CardState) ? DefaultCardState : CardState.Trim(); // 상태 fallback
    public bool HasAnyStatValue => DamageMultiplierBonus != 0f
        || MeleeDamageMultiplierBonus != 0f
        || MagicDamageMultiplierBonus != 0f
        || AttackSpeedMultiplierBonus != 0f
        || TurnSpeedBonus != 0f
        || CollisionForceBonus != 0f
        || RejoinRangeBonus != 0f
        || NexusHealthBonus != 0f; // 카드 값 존재

    public GrowthStatData CreateGrowthStatData(global::StatUpgrade.StatCardTier tier)
    {
        float multiplier = global::StatUpgrade.GetTierMultiplier(tier);
        return GrowthStatData.CreateConvoyUpgrade(
            Mathf.Max(1, LevelDelta),
            DamageMultiplierBonus * multiplier,
            AttackSpeedMultiplierBonus * multiplier,
            TurnSpeedBonus * multiplier,
            CollisionForceBonus * multiplier,
            RejoinRangeBonus * multiplier,
            MeleeDamageMultiplierBonus * multiplier,
            MagicDamageMultiplierBonus * multiplier);
    }

    public int GetResolvedNexusHealthBonus(global::StatUpgrade.StatCardTier tier)
    {
        return Mathf.RoundToInt(NexusHealthBonus * global::StatUpgrade.GetTierMultiplier(tier));
    }
}
