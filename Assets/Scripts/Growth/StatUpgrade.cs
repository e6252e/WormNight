// 안건준 추가 - 0622
using TeamProject01.Gameplay;
using UnityEngine;

public class StatUpgrade : MonoBehaviour
{
    private const string DefaultCardState = "기존";

    public enum StatCardTier
    {
        Normal = 1, // 일반 (1배, 흰색)
        Rare = 2, // 레어 (2배, 노란색)
        Unique = 3 // 유니크 (3배, 초록색)
    }

    [Header("카드 표시")]
    [SerializeField] private StatUpgradeDefinition definition; // 데이터 에셋 기반 카드 정의
    [SerializeField] private string cardState = DefaultCardState; // 밸런스 테이블 표시 상태
    [SerializeField] private string displayName; // 카드 이름
    [TextArea(2, 4)][SerializeField] private string description; // 카드 설명, (N)=등급 수치

    [Header("코어 성장 연동")]
    [Min(1)][SerializeField] private int levelDelta = 1; // 선택 시 소비할 레벨 증가량
    [Header("공격력 배율 보너스")]
    [SerializeField] private float damageMultiplierBonus; // 공격력 배율 보너스
    [Header("밀리 공격력 배율 보너스")]
    [SerializeField] private float meleeDamageMultiplierBonus; // 밀리 공격력 배율 보너스
    [Header("마법 공격력 배율 보너스")]
    [SerializeField] private float magicDamageMultiplierBonus; // 마법 공격력 배율 보너스
    [Header("쿨타임 감소 보너스")]
    [SerializeField] private float attackSpeedMultiplierBonus; // 쿨타임 감소 보너스
    [Header("회전력 보너스")]
    [SerializeField] private float turnSpeedBonus; // 회전력 보너스
    [Header("충돌힘 보너스")]
    [SerializeField] private float collisionForceBonus; // 충돌힘 보너스
    [Header("재결합 범위 보너스")]
    [SerializeField] private float rejoinRangeBonus; // 재결합 범위 보너스
    [Header("넥서스 체력 보너스")]
    [SerializeField] private float nexusHealthBonus; // 넥서스 체력 보너스

    private float upgradeMultiplier = 1f; // 생성 시 1, 2, 3
    private StatCardTier currentTier = StatCardTier.Normal; // 현재 등급

    public StatUpgradeDefinition Definition => definition; // 데이터 에셋 정의
    public StatCardTier CurrentTier => currentTier; // 외부 등급 조회
    public bool IsRareUpgrade => currentTier == StatCardTier.Rare; // 레어 카드 여부
    public bool IsUniqueUpgrade => currentTier == StatCardTier.Unique; // 유니크 카드 여부
    public string CardState => definition != null ? definition.ResolvedCardState : (string.IsNullOrWhiteSpace(cardState) ? DefaultCardState : cardState.Trim()); // 밸런스 테이블 상태
    public string DisplayName => definition != null ? definition.DisplayName ?? string.Empty : displayName ?? string.Empty; // 카드 표시 이름
    public string Description => definition != null ? definition.Description ?? string.Empty : description ?? string.Empty; // 카드 설명
    public float DamageMultiplierBonus => definition != null ? definition.DamageMultiplierBonus : damageMultiplierBonus; // 공격력 배율 보너스
    public float MeleeDamageMultiplierBonus => definition != null ? definition.MeleeDamageMultiplierBonus : meleeDamageMultiplierBonus; // 밀리 공격력 배율 보너스
    public float MagicDamageMultiplierBonus => definition != null ? definition.MagicDamageMultiplierBonus : magicDamageMultiplierBonus; // 마법 공격력 배율 보너스
    public float AttackSpeedMultiplierBonus => definition != null ? definition.AttackSpeedMultiplierBonus : attackSpeedMultiplierBonus; // 쿨타임 감소 보너스
    public float TurnSpeedBonus => definition != null ? definition.TurnSpeedBonus : turnSpeedBonus; // 핸들링 보너스
    public float CollisionForceBonus => definition != null ? definition.CollisionForceBonus : collisionForceBonus; // 충돌힘 보너스
    public float RejoinRangeBonus => definition != null ? definition.RejoinRangeBonus : rejoinRangeBonus; // 재결합 범위 보너스
    public float NexusHealthBonus => definition != null ? definition.NexusHealthBonus : nexusHealthBonus; // 넥서스 체력 보너스

    public readonly struct CardSpawnResolve // 생성 시 사용할 프리팹
    {
        public readonly GameObject Prefab;

        public CardSpawnResolve(GameObject prefab)
        {
            Prefab = prefab;
        }
    }

    public CardSpawnResolve ResolveCardSpawn(StatCardTier tier, GameObject defaultPrefab) // 등급별 Instantiate 대상
    {
        return new CardSpawnResolve(defaultPrefab); // 등급별 프리팹 교체 미사용 — 이팩트로 대체
    }

    public void CopyStatValuesFrom(StatUpgrade source) // 등급 프리팹 → 풀 프리팹 수치 복사
    {
        if (source == null)
        {
            return; // 복사 대상 없음
        }

        definition = source.definition;
        cardState = source.cardState;
        displayName = source.displayName;
        description = source.description;
        levelDelta = source.levelDelta;
        damageMultiplierBonus = source.damageMultiplierBonus;
        meleeDamageMultiplierBonus = source.meleeDamageMultiplierBonus;
        magicDamageMultiplierBonus = source.magicDamageMultiplierBonus;
        attackSpeedMultiplierBonus = source.attackSpeedMultiplierBonus;
        turnSpeedBonus = source.turnSpeedBonus;
        collisionForceBonus = source.collisionForceBonus;
        rejoinRangeBonus = source.rejoinRangeBonus;
        nexusHealthBonus = source.nexusHealthBonus; // 안건준 수정 - 0622 — 넥서스 체력 보너스도 등급 프리팹에서 복사
    }

    public void ConfigureFromDefinition(StatUpgradeDefinition source, StatCardTier tier) // 데이터 에셋 주입
    {
        definition = source;
        ApplySpawnTier(tier);
    }

    public void RollSpawnVariant(float rareChancePercent, float uniqueChancePercent) // 생성 시 등급·배율 결정
    {
        ApplySpawnTier(RollTier(rareChancePercent, uniqueChancePercent)); // 등급 및 배율 적용
    }

    public static StatCardTier RollTier(float rareChancePercent, float uniqueChancePercent) // 등급 난수
    {
        float uniqueChance = Mathf.Clamp(uniqueChancePercent, 0f, 100f) * 0.01f; // 유니크 확률(0~1)
        float rareChance = Mathf.Clamp(rareChancePercent, 0f, 100f) * 0.01f; // 레어 확률(0~1)
        float roll = Random.value; // 0~1 난수

        if (roll < uniqueChance)
        {
            return StatCardTier.Unique; // 유니크
        }

        if (roll < uniqueChance + rareChance)
        {
            return StatCardTier.Rare; // 레어
        }

        return StatCardTier.Normal; // 일반
    }

    public void ApplySpawnTier(StatCardTier tier) // 생성 후 등급·배율 반영
    {
        currentTier = tier; // 등급 저장
        upgradeMultiplier = GetTierMultiplier(tier); // 스탯 배율
    }

    public static float GetTierMultiplier(StatCardTier tier)
    {
        return tier switch
        {
            StatCardTier.Unique => 3f,
            StatCardTier.Rare => 2f,
            _ => 1f
        };
    }

    public GrowthStatData CreateGrowthStatData() // 코어로 보낼 성장값 생성
    {
        if (definition != null)
        {
            return definition.CreateGrowthStatData(currentTier);
        }

        return GrowthStatData.CreateConvoyUpgrade(
            levelDelta,
            damageMultiplierBonus * upgradeMultiplier,
            attackSpeedMultiplierBonus * upgradeMultiplier,
            turnSpeedBonus * upgradeMultiplier,
            collisionForceBonus * upgradeMultiplier,
            rejoinRangeBonus * upgradeMultiplier,
            meleeDamageMultiplierBonus * upgradeMultiplier,
            magicDamageMultiplierBonus * upgradeMultiplier);
    }

    public bool TryApplyToCore() // 코어에 성장값 적용
    {
        GrowthStatData growth = CreateGrowthStatData(); // 적용할 데이터 준비
        int resolvedNexusBonus = GetResolvedNexusHealthBonus(); // 안건준 추가 - 0622 — 등급 배율 적용된 넥서스 최대 체력 보너스
        int resolvedLevelDelta = definition != null ? Mathf.Max(1, definition.LevelDelta) : levelDelta; // 데이터 에셋 기반 레벨 소비량
        bool hasNexusBonus = resolvedNexusBonus > 0; // 안건준 추가 - 0622 — 넥서스 체력 카드 여부

        if (!growth.HasAnyValue && !hasNexusBonus) // 안건준 수정 - 0622 — 넥서스 체력만 있어도 적용 가능
        {
            return false; // 적용 실패
        }

        if (growth.HasAnyValue) // 안건준 수정 - 0622 — 컨보이 스탯/레벨 소비
        {
            if (!CoreStatProvider.TryApplyGrowth(growth)) // 경험치 소비 + 스탯 반영
            {
                return false;
            }
        }
        else if (resolvedLevelDelta > 0) // 안건준 추가 - 0622 — 넥서스 체력만 있는 카드는 레벨만 소비
        {
            GrowthStatData levelOnly = GrowthStatData.CreateConvoyUpgrade(resolvedLevelDelta, 0f, 0f, 0f, 0f, 0f); // 레벨 delta만 전달
            if (!CoreStatProvider.TryApplyGrowth(levelOnly))
            {
                return false; // 레벨업 조건 미충족
            }
        }

        if (hasNexusBonus) // 안건준 추가 - 0622 — 코어 적용 성공 후 넥서스 최대 체력 반영
        {
            TryApplyNexusHealthBonus(resolvedNexusBonus);
        }

        return true; // 안건준 수정 - 0622 — 넥서스/컨보이 적용 완료
    }

    // 안건준 추가 - 0622
    private int GetResolvedNexusHealthBonus() // 등급 배율(1/2/3배) 적용 후 정수 보너스
    {
        return definition != null
            ? definition.GetResolvedNexusHealthBonus(currentTier)
            : Mathf.RoundToInt(nexusHealthBonus * upgradeMultiplier);
    }

    // 안건준 추가 - 0622
    private void TryApplyNexusHealthBonus(int amount) // NexusController에 최대 체력 보너스 전달
    {
        NexusController nexus = NexusController.Active; // 씬에 등록된 넥서스
        if (nexus == null)
        {
            Debug.LogWarning("[StatUpgrade] NexusController.Active 없음 — 최대 체력 보너스 미적용", this);
            return;
        }

        nexus.IncreaseMaxHealth(amount); // 최대 체력만 증가
    }

}
