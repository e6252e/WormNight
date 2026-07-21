using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace TeamProject01.Gameplay
{
    [CreateAssetMenu(menuName = "OZ/Segments/Weapon Definition", fileName = "WE_##_Name")]
    public sealed class WeaponDefinition : ScriptableObject // 무기(세그먼트) 강화 카드 1종
    {
        public string EnhancementId; // 예: WE_Cannon_DamageBoost
        public string DisplayName; // UI 이름
        [TextArea(2, 4)] public string Description; // UI 설명
        public string TargetSegmentId; // 적용 대상 세그먼트 ID (예: SG01_Cannon)

        [Header("공격력 보너스")]
        [Header("토글 체크할경우 %곱연산됨")]
        [Min(0f)] public float BaseDamage = 0f; // 일반 카드
        public bool BaseDamageUsePercent; // 일반: %곱연산 사용
        [Header("")]
        public float BaseDamageRare = 0f; // 레어
        public bool BaseDamageUsePercentRare; // 레어 %곱연산 사용
        [Header("")]
        public float BaseDamageUnique = 0f; // 유니크
        public bool BaseDamageUsePercentUnique; // 유니크 %곱연산 사용

        [Header("관통 피해 비율 증가")]
        [Range(0f, 1f)] public float SawPierceDamageRatio = 0f; // 일반 카드 (0.02=+2%p)
        [Header("")]
        [Range(0f, 1f)] public float SawPierceDamageRatioRare = 0f; // 레어
        [Header("")]
        [Range(0f, 1f)] public float SawPierceDamageRatioUnique = 0f; // 유니크

        [Header("투사체 속도 보너스")]
        [Header("토글 체크할경우 %곱연산됨")]
        [Min(0f)] public float ProjectileSpeed = 0f; // 일반 카드
        public bool ProjectileSpeedUsePercent; // 일반: %곱연산 사용
        [Header("")]
        public float ProjectileSpeedRare = 0f; // 레어
        public bool ProjectileSpeedUsePercentRare; // 레어 %곱연산 사용
        [Header("")]
        public float ProjectileSpeedUnique = 0f; // 유니크
        public bool ProjectileSpeedUsePercentUnique; // 유니크 %곱연산 사용

        [Header("사거리 보너스")]
        [Header("토글 체크할경우 %곱연산됨")]
        [Min(0f)] public float SearchRange = 0f; // 일반 카드
        public bool SearchRangeUsePercent; // 일반: %곱연산 사용
        [Header("")]
        public float SearchRangeRare = 0f; // 레어
        public bool SearchRangeUsePercentRare; // 레어 %곱연산 사용
        [Header("")]
        public float SearchRangeUnique = 0f; // 유니크
        public bool SearchRangeUsePercentUnique; // 유니크 %곱연산 사용

        [Header("연쇄 단계 증가")]
        [Min(0)] public int MaxChainDepth = 0; // 일반 카드
        [Header("")]
        [Min(0)] public int MaxChainDepthRare = 0; // 레어
        [Header("")]
        [Min(0)] public int MaxChainDepthUnique = 0; // 유니크

        [Header("연쇄 거리 증가")]
        [Header("토글 체크할경우 %곱연산됨")]
        [Min(0f)] public float ChainRange = 0f; // 일반 카드
        public bool ChainRangeUsePercent; // 일반: %곱연산 사용
        [Header("")]
        public float ChainRangeRare = 0f; // 레어
        public bool ChainRangeUsePercentRare; // 레어 %곱연산 사용
        [Header("")]
        public float ChainRangeUnique = 0f; // 유니크
        public bool ChainRangeUsePercentUnique; // 유니크 %곱연산 사용

        [Header("체인 단계별 피해 감소율")]
        [Range(0f, 1f)] public float ChainDamageFalloff = 0f; // 일반 카드
        [Header("")]
        [Range(0f, 1f)] public float ChainDamageFalloffRare = 0f; // 레어
        [Header("")]
        [Range(0f, 1f)] public float ChainDamageFalloffUnique = 0f; // 유니크

        [Header("발사 수 증가")]
        [Min(0)] public int ProjectileCount = 0; // 일반 카드
        [Header("")]
        [Min(0)] public int ProjectileCountRare = 0; // 레어
        [Header("")]
        [Min(0)] public int ProjectileCountUnique = 0; // 유니크

        //전찬우 수정-0622
        [Header("공격 기준 쿨타임 감소")]
        [FormerlySerializedAs("MinAttackInterval")]
        [FormerlySerializedAs("MaxAttackInterval")]
        [Range(0f, 1f)] public float CooldownReduction = 0f; // 일반 (0.1=기준 쿨 10% 감소)
        [Header("")]
        [FormerlySerializedAs("MinAttackIntervalRare")]
        [FormerlySerializedAs("MaxAttackIntervalRare")]
        [Range(0f, 1f)] public float CooldownReductionRare = 0f; // 레어
        [Header("")]
        [FormerlySerializedAs("MinAttackIntervalUnique")]
        [FormerlySerializedAs("MaxAttackIntervalUnique")]
        [Range(0f, 1f)] public float CooldownReductionUnique = 0f; // 유니크

        [Header("좌우 각각의 부채꼴 각도 증가")]
        [Min(0f)] public float SideConeAngle = 0f; // 일반 (보너스 각도, 0=효과 없음)
        [Header("")]
        [Min(0f)] public float SideConeAngleRare = 0f; // 레어
        [Header("")]
        [Min(0f)] public float SideConeAngleUnique = 0f; // 유니크

        [Header("지속 시간 증가")]
        [Header("토글 체크할경우 %곱연산됨")]
        [Min(0f)] public float LaserDuration = 0f; // 일반 카드
        public bool LaserDurationUsePercent; // 일반: %곱연산 사용
        [Header("")]
        public float LaserDurationRare = 0f; // 레어
        public bool LaserDurationUsePercentRare; // 레어 %곱연산 사용
        [Header("")]
        public float LaserDurationUnique = 0f; // 유니크
        public bool LaserDurationUsePercentUnique; // 유니크 %곱연산 사용

        [Header("레이저 틱 간격 감소")]
        [Range(0f, 1f)] public float LaserTickInterval = 0f; // 일반 (0.1=틱 간격 10% 감소)
        [Header("")]
        [Range(0f, 1f)] public float LaserTickIntervalRare = 0f; // 레어
        [Header("")]
        [Range(0f, 1f)] public float LaserTickIntervalUnique = 0f; // 유니크

        [Header("착지 지점부터 굴러가는 거리 증가")]
        [Header("토글 체크할경우 %곱연산됨")]
        [Min(0f)] public float LandingRollDistance = 0f; // 일반 카드
        public bool LandingRollDistanceUsePercent; // 일반: %곱연산 사용
        [Header("")]
        public float LandingRollDistanceRare = 0f; // 레어
        public bool LandingRollDistanceUsePercentRare; // 레어 %곱연산 사용
        [Header("")]
        public float LandingRollDistanceUnique = 0f; // 유니크
        public bool LandingRollDistanceUsePercentUnique; // 유니크 %곱연산 사용

        [Header("굴러가는 시간")]
        [Header("토글 체크할경우 %곱연산됨")]
        [Min(0f)] public float LandingRollDuration = 0f; // 일반 카드
        public bool LandingRollDurationUsePercent; // 일반: %곱연산 사용
        [Header("")]
        public float LandingRollDurationRare = 0f; // 레어
        public bool LandingRollDurationUsePercentRare; // 레어 %곱연산 사용
        [Header("")]
        public float LandingRollDurationUnique = 0f; // 유니크
        public bool LandingRollDurationUsePercentUnique; // 유니크 %곱연산 사용

        [Header("관통 수 보너스")]
        [Min(0)] public int PierceCount = 0; // 일반 카드
        [Header("")]
        [Min(0)] public int PierceCountRare = 0; // 레어
        [Header("")]
        [Min(0)] public int PierceCountUnique = 0; // 유니크

        [Header("폭발 반경 보너스")]
        [Header("토글 체크할경우 %곱연산됨")]
        [Min(0f)] public float ExplosionRadius = 0f; // 일반 카드
        public bool ExplosionRadiusUsePercent; // 일반: %곱연산 사용
        [Header("")]
        public float ExplosionRadiusRare = 0f; // 레어
        public bool ExplosionRadiusUsePercentRare; // 레어 %곱연산 사용
        [Header("")]
        public float ExplosionRadiusUnique = 0f; // 유니크
        public bool ExplosionRadiusUsePercentUnique; // 유니크 %곱연산 사용

        [Header("카드 이미지 변경")]
        public GameObject CardPrefabOverride; // 강화별 공통 카드 프리팹 (등급 무관 동일 이미지)
        // 안건준 추가 - 0623 : 카드에 표시할 세그먼트 레벨별 이미지 (인덱스 0=Lv1, 1=Lv2, 2=Lv3 ...)
        public Sprite CardIconSprite; // Lv1 기본 아이콘 (레벨별 배열 미설정 시 fallback)
        public Sprite[] CardIconSpritesPerLevel; // 레벨별 아이콘 배열 (0=Lv1, 1=Lv2, ...)
        // 안건준 추가 - 0623 : 카드 아이콘 크기 조절 (0 = 원본, -100 = 절반, +100 = 두배)
        [Range(-100f, 100f)] public float CardIconSizeOffset = 0f; // 아이콘 크기 조절

        public string NormalizedId => string.IsNullOrWhiteSpace(EnhancementId) ? string.Empty : EnhancementId.Trim(); // 비교 ID
        public string NormalizedTargetSegmentId => string.IsNullOrWhiteSpace(TargetSegmentId) ? string.Empty : TargetSegmentId.Trim(); // 대상 ID

        // 안건준 추가 - 0623 : 현재 세그먼트 레벨에 맞는 아이콘 반환 (배열 없으면 CardIconSprite fallback)
        public Sprite GetIconSpriteForLevel(int segmentLevel)
        {
            int idx = Mathf.Max(0, segmentLevel - 1); // 레벨 1 → 인덱스 0
            if (CardIconSpritesPerLevel != null && idx < CardIconSpritesPerLevel.Length && CardIconSpritesPerLevel[idx] != null)
            {
                return CardIconSpritesPerLevel[idx]; // 레벨별 전용 스프라이트
            }

            return CardIconSprite; // fallback: Lv1 기본 스프라이트
        }
        public bool HasId => !string.IsNullOrWhiteSpace(EnhancementId); // ID 존재
        public bool HasTarget => !string.IsNullOrWhiteSpace(TargetSegmentId); // 대상 존재

        // 건춘추가 - 0621 ======
        public bool HasAnyStatBonus => HasTieredFloat(BaseDamage, BaseDamageRare, BaseDamageUnique)
            || HasTieredFloat(SawPierceDamageRatio, SawPierceDamageRatioRare, SawPierceDamageRatioUnique)
            || HasTieredFloat(ProjectileSpeed, ProjectileSpeedRare, ProjectileSpeedUnique)
            || HasTieredFloat(SearchRange, SearchRangeRare, SearchRangeUnique)
            || HasTieredInt(MaxChainDepth, MaxChainDepthRare, MaxChainDepthUnique)
            || HasTieredFloat(ChainRange, ChainRangeRare, ChainRangeUnique)
            || HasTieredFloat(ChainDamageFalloff, ChainDamageFalloffRare, ChainDamageFalloffUnique)
            || HasTieredInt(ProjectileCount, ProjectileCountRare, ProjectileCountUnique)
            || HasTieredFloat(CooldownReduction, CooldownReductionRare, CooldownReductionUnique)
            || HasTieredFloat(SideConeAngle, SideConeAngleRare, SideConeAngleUnique)
            || HasTieredFloat(LaserDuration, LaserDurationRare, LaserDurationUnique)
            || HasTieredFloat(LaserTickInterval, LaserTickIntervalRare, LaserTickIntervalUnique)
            || HasTieredFloat(LandingRollDistance, LandingRollDistanceRare, LandingRollDistanceUnique)
            || HasTieredFloat(LandingRollDuration, LandingRollDurationRare, LandingRollDurationUnique)
            || HasTieredInt(PierceCount, PierceCountRare, PierceCountUnique)
            || HasTieredFloat(ExplosionRadius, ExplosionRadiusRare, ExplosionRadiusUnique); // 수치 보너스 존재

        public readonly struct CardSpawnResolve // 생성 시 사용할 프리팹
        {
            public readonly GameObject Prefab;

            public CardSpawnResolve(GameObject prefab)
            {
                Prefab = prefab;
            }
        }

        public CardSpawnResolve ResolveCardSpawn(StatUpgrade.StatCardTier tier, GameObject defaultPrefab, SegmentAddCard templatePresentation = null)
        {
            GameObject resolvedPrefab = ResolveSpawnPrefabForTier(tier, defaultPrefab, templatePresentation); // 등급별 프리팹
            return new CardSpawnResolve(resolvedPrefab != null ? resolvedPrefab : defaultPrefab);
        }

        public float GetBaseDamage(StatUpgrade.StatCardTier tier) => GetTieredFlatValue(BaseDamage, BaseDamageRare, BaseDamageUnique, BaseDamageUsePercent, BaseDamageUsePercentRare, BaseDamageUsePercentUnique, tier);

        public float GetBaseDamagePercent(StatUpgrade.StatCardTier tier) => GetTieredPercentValue(BaseDamage, BaseDamageRare, BaseDamageUnique, BaseDamageUsePercent, BaseDamageUsePercentRare, BaseDamageUsePercentUnique, tier);

        public bool UsesCurrentBaseDamagePercent(StatUpgrade.StatCardTier tier) // 유니크 피해 %는 선택 당시 현재 무기 피해 기준
        {
            return tier == StatUpgrade.StatCardTier.Unique
                && ResolveTieredUsePercent(BaseDamageUsePercent, BaseDamageUsePercentRare, BaseDamageUsePercentUnique, tier);
        }

        public float GetSawPierceDamageRatio(StatUpgrade.StatCardTier tier) => ResolveTieredFloat(SawPierceDamageRatio, SawPierceDamageRatioRare, SawPierceDamageRatioUnique, tier);

        public float GetProjectileSpeed(StatUpgrade.StatCardTier tier) => GetTieredFlatValue(ProjectileSpeed, ProjectileSpeedRare, ProjectileSpeedUnique, ProjectileSpeedUsePercent, ProjectileSpeedUsePercentRare, ProjectileSpeedUsePercentUnique, tier);

        public float GetProjectileSpeedPercent(StatUpgrade.StatCardTier tier) => GetTieredPercentValue(ProjectileSpeed, ProjectileSpeedRare, ProjectileSpeedUnique, ProjectileSpeedUsePercent, ProjectileSpeedUsePercentRare, ProjectileSpeedUsePercentUnique, tier);

        public float GetSearchRange(StatUpgrade.StatCardTier tier) => GetTieredFlatValue(SearchRange, SearchRangeRare, SearchRangeUnique, SearchRangeUsePercent, SearchRangeUsePercentRare, SearchRangeUsePercentUnique, tier);

        public float GetSearchRangePercent(StatUpgrade.StatCardTier tier) => GetTieredPercentValue(SearchRange, SearchRangeRare, SearchRangeUnique, SearchRangeUsePercent, SearchRangeUsePercentRare, SearchRangeUsePercentUnique, tier);

        public int GetMaxChainDepth(StatUpgrade.StatCardTier tier) => ResolveTieredInt(MaxChainDepth, MaxChainDepthRare, MaxChainDepthUnique, tier);

        public float GetChainRange(StatUpgrade.StatCardTier tier) => GetTieredFlatValue(ChainRange, ChainRangeRare, ChainRangeUnique, ChainRangeUsePercent, ChainRangeUsePercentRare, ChainRangeUsePercentUnique, tier);

        public float GetChainRangePercent(StatUpgrade.StatCardTier tier) => GetTieredPercentValue(ChainRange, ChainRangeRare, ChainRangeUnique, ChainRangeUsePercent, ChainRangeUsePercentRare, ChainRangeUsePercentUnique, tier);

        public float GetChainDamageFalloff(StatUpgrade.StatCardTier tier) => ResolveTieredFloat(ChainDamageFalloff, ChainDamageFalloffRare, ChainDamageFalloffUnique, tier);

        public int GetProjectileCount(StatUpgrade.StatCardTier tier) => ResolveTieredInt(ProjectileCount, ProjectileCountRare, ProjectileCountUnique, tier);

        //전찬우 수정-0622
        public float GetCooldownReduction(StatUpgrade.StatCardTier tier) => ResolveTieredFloat(CooldownReduction, CooldownReductionRare, CooldownReductionUnique, tier);

        public float GetSideConeAngle(StatUpgrade.StatCardTier tier) => ResolveTieredFloat(SideConeAngle, SideConeAngleRare, SideConeAngleUnique, tier);

        public float GetLaserDuration(StatUpgrade.StatCardTier tier) => GetTieredFlatValue(LaserDuration, LaserDurationRare, LaserDurationUnique, LaserDurationUsePercent, LaserDurationUsePercentRare, LaserDurationUsePercentUnique, tier);

        public float GetLaserDurationPercent(StatUpgrade.StatCardTier tier) => GetTieredPercentValue(LaserDuration, LaserDurationRare, LaserDurationUnique, LaserDurationUsePercent, LaserDurationUsePercentRare, LaserDurationUsePercentUnique, tier);

        public float GetLaserTickInterval(StatUpgrade.StatCardTier tier) => ResolveTieredFloat(LaserTickInterval, LaserTickIntervalRare, LaserTickIntervalUnique, tier);

        public float GetLandingRollDistance(StatUpgrade.StatCardTier tier) => GetTieredFlatValue(LandingRollDistance, LandingRollDistanceRare, LandingRollDistanceUnique, LandingRollDistanceUsePercent, LandingRollDistanceUsePercentRare, LandingRollDistanceUsePercentUnique, tier);

        public float GetLandingRollDistancePercent(StatUpgrade.StatCardTier tier) => GetTieredPercentValue(LandingRollDistance, LandingRollDistanceRare, LandingRollDistanceUnique, LandingRollDistanceUsePercent, LandingRollDistanceUsePercentRare, LandingRollDistanceUsePercentUnique, tier);

        public float GetLandingRollDuration(StatUpgrade.StatCardTier tier) => GetTieredFlatValue(LandingRollDuration, LandingRollDurationRare, LandingRollDurationUnique, LandingRollDurationUsePercent, LandingRollDurationUsePercentRare, LandingRollDurationUsePercentUnique, tier);

        public float GetLandingRollDurationPercent(StatUpgrade.StatCardTier tier) => GetTieredPercentValue(LandingRollDuration, LandingRollDurationRare, LandingRollDurationUnique, LandingRollDurationUsePercent, LandingRollDurationUsePercentRare, LandingRollDurationUsePercentUnique, tier);

        public int GetPierceCount(StatUpgrade.StatCardTier tier) => ResolveTieredInt(PierceCount, PierceCountRare, PierceCountUnique, tier);

        public float GetExplosionRadius(StatUpgrade.StatCardTier tier) => GetTieredFlatValue(ExplosionRadius, ExplosionRadiusRare, ExplosionRadiusUnique, ExplosionRadiusUsePercent, ExplosionRadiusUsePercentRare, ExplosionRadiusUsePercentUnique, tier);

        public float GetExplosionRadiusPercent(StatUpgrade.StatCardTier tier) => GetTieredPercentValue(ExplosionRadius, ExplosionRadiusRare, ExplosionRadiusUnique, ExplosionRadiusUsePercent, ExplosionRadiusUsePercentRare, ExplosionRadiusUsePercentUnique, tier);

        private static bool HasTieredFloat(float normal, float rare, float unique) => normal > 0f || rare > 0f || unique > 0f;

        private static bool HasTieredInt(int normal, int rare, int unique) => normal > 0 || rare > 0 || unique > 0;

        private static float GetTieredFlatValue(
            float normal,
            float rare,
            float unique,
            bool normalUsePercent,
            bool rareUsePercent,
            bool uniqueUsePercent,
            StatUpgrade.StatCardTier tier)
        {
            if (ResolveTieredUsePercent(normalUsePercent, rareUsePercent, uniqueUsePercent, tier))
            {
                return 0f; // % 모드면 고정 보너스 없음
            }

            return ResolveTieredFloat(normal, rare, unique, tier);
        }

        private static float GetTieredPercentValue(
            float normal,
            float rare,
            float unique,
            bool normalUsePercent,
            bool rareUsePercent,
            bool uniqueUsePercent,
            StatUpgrade.StatCardTier tier)
        {
            if (!ResolveTieredUsePercent(normalUsePercent, rareUsePercent, uniqueUsePercent, tier))
            {
                return 0f; // 고정 모드면 % 보너스 없음
            }

            return ResolveTieredFloat(normal, rare, unique, tier);
        }

        private static bool ResolveTieredUsePercent(
            bool normalUsePercent,
            bool rareUsePercent,
            bool uniqueUsePercent,
            StatUpgrade.StatCardTier tier)
        {
            switch (tier)
            {
                case StatUpgrade.StatCardTier.Rare:
                    return rareUsePercent;
                case StatUpgrade.StatCardTier.Unique:
                    return uniqueUsePercent;
                default:
                    return normalUsePercent;
            }
        }

        private static float ResolveTieredFloat(float normal, float rare, float unique, StatUpgrade.StatCardTier tier)
        {
            switch (tier)
            {
                case StatUpgrade.StatCardTier.Rare:
                    return rare;
                case StatUpgrade.StatCardTier.Unique:
                    return unique;
                default:
                    return normal;
            }
        }

        private static int ResolveTieredInt(int normal, int rare, int unique, StatUpgrade.StatCardTier tier)
        {
            switch (tier)
            {
                case StatUpgrade.StatCardTier.Rare:
                    return rare;
                case StatUpgrade.StatCardTier.Unique:
                    return unique;
                default:
                    return normal;
            }
        }

        private GameObject ResolveSpawnPrefabForTier(StatUpgrade.StatCardTier tier, GameObject defaultPrefab, SegmentAddCard templatePresentation)
        {
            // 안건준 수정 - 0623 : 레어/유니크 전용 프리팹 제거 — 등급 무관 공통 프리팹 사용
            if (CardPrefabOverride != null)
            {
                return CardPrefabOverride;
            }

            return defaultPrefab;
        }
        // 건춘추가 - 0621 ======
    }

    [Serializable]
        public struct WeaponStatBonusData // 세그먼트별 무기 강화 누적값
        {
            public float BaseDamageBonus; // 누적 피해 고정 보너스
            // 건춘추가 - 0621 ======
        public float BaseDamagePercentMultiplier; // 기초 피해 기준 % 보너스 (0.1=기초 피해의 10% 추가)
        public float SawPierceDamageRatioBonus; // 누적 관통 피해 비율 보너스 (더하기)
        public float ProjectileSpeedBonus; // 누적 투사체 속도 고정 보너스
        public float ProjectileSpeedPercentMultiplier; // 누적 투사체 속도 % 곱연산
        public float SearchRangeBonus; // 누적 사거리 고정 보너스
        public float SearchRangePercentMultiplier; // 누적 사거리 % 곱연산
        public int MaxChainDepthBonus; // 누적 연쇄 단계 보너스
        public float ChainRangeBonus; // 누적 연쇄 거리 고정 보너스
        public float ChainRangePercentMultiplier; // 누적 연쇄 거리 % 곱연산
        public float ChainDamageFalloffBonus; // 누적 체인 피해 유지 비율 보너스 (기본 + 보너스)
        public int ProjectileCountBonus; // 누적 발사 수 보너스
        //전찬우 수정-0622
        public float CooldownReductionMultiplier; // 누적 기준 쿨 % 감소 곱연산 (0.9=10% 감소)
        public float SideConeAngleBonus; // 누적 부채꼴 각도 보너스
        public float LaserDurationBonus; // 누적 레이저 지속 시간 고정 보너스
        public float LaserDurationPercentMultiplier; // 누적 레이저 지속 % 곱연산
        public float LaserTickIntervalReductionMultiplier; // 누적 레이저 틱 간격 % 감소 곱연산
        public float LandingRollDistanceBonus; // 누적 굴러가는 거리 고정 보너스
        public float LandingRollDistancePercentMultiplier; // 누적 굴러가는 거리 % 곱연산
        public float LandingRollDurationBonus; // 누적 굴러가는 시간 고정 보너스
        public float LandingRollDurationPercentMultiplier; // 누적 굴러가는 시간 % 곱연산
        public int PierceCountBonus; // 누적 관통 보너스
        public float ExplosionRadiusBonus; // 누적 폭발 반경 고정 보너스
        public float ExplosionRadiusPercentMultiplier; // 누적 폭발 반경 % 곱연산
        // 건춘추가 - 0621 ======

        // 건춘추가 - 0621 ======
        public bool HasAny => BaseDamageBonus > 0f
            || HasPercentMultiplier(BaseDamagePercentMultiplier)
            || SawPierceDamageRatioBonus > 0f
            || ProjectileSpeedBonus > 0f
            || HasPercentMultiplier(ProjectileSpeedPercentMultiplier)
            || SearchRangeBonus > 0f
            || HasPercentMultiplier(SearchRangePercentMultiplier)
            || MaxChainDepthBonus > 0
            || ChainRangeBonus > 0f
            || HasPercentMultiplier(ChainRangePercentMultiplier)
            || ChainDamageFalloffBonus > 0f
            || ProjectileCountBonus > 0
            || HasReductionMultiplier(CooldownReductionMultiplier)
            || SideConeAngleBonus > 0f
            || LaserDurationBonus > 0f
            || HasPercentMultiplier(LaserDurationPercentMultiplier)
            || HasReductionMultiplier(LaserTickIntervalReductionMultiplier)
            || LandingRollDistanceBonus > 0f
            || HasPercentMultiplier(LandingRollDistancePercentMultiplier)
            || LandingRollDurationBonus > 0f
            || HasPercentMultiplier(LandingRollDurationPercentMultiplier)
            || PierceCountBonus > 0
            || ExplosionRadiusBonus > 0f
            || HasPercentMultiplier(ExplosionRadiusPercentMultiplier); // 보너스 존재 여부

        public void AddDefinition(WeaponDefinition definition, StatUpgrade.StatCardTier tier = StatUpgrade.StatCardTier.Normal, float profileBaseDamage = 0f) // 강화 1종 누적 (등급별 수치)
        {
            if (definition == null)
            {
                return; // null 무시
            }

            BaseDamageBonus += definition.GetBaseDamage(tier);
            AddBaseDamagePercent(definition, tier, profileBaseDamage);
            SawPierceDamageRatioBonus += definition.GetSawPierceDamageRatio(tier);
            ProjectileSpeedBonus += definition.GetProjectileSpeed(tier);
            ApplyPercentMultiplier(ref ProjectileSpeedPercentMultiplier, definition.GetProjectileSpeedPercent(tier));
            SearchRangeBonus += definition.GetSearchRange(tier);
            ApplyPercentMultiplier(ref SearchRangePercentMultiplier, definition.GetSearchRangePercent(tier));
            MaxChainDepthBonus += definition.GetMaxChainDepth(tier);
            ChainRangeBonus += definition.GetChainRange(tier);
            ApplyPercentMultiplier(ref ChainRangePercentMultiplier, definition.GetChainRangePercent(tier));
            ChainDamageFalloffBonus += definition.GetChainDamageFalloff(tier);
            ProjectileCountBonus += definition.GetProjectileCount(tier);
            ApplyReductionMultiplier(ref CooldownReductionMultiplier, definition.GetCooldownReduction(tier));
            SideConeAngleBonus += definition.GetSideConeAngle(tier);
            LaserDurationBonus += definition.GetLaserDuration(tier);
            ApplyPercentMultiplier(ref LaserDurationPercentMultiplier, definition.GetLaserDurationPercent(tier));
            ApplyReductionMultiplier(ref LaserTickIntervalReductionMultiplier, definition.GetLaserTickInterval(tier));
            LandingRollDistanceBonus += definition.GetLandingRollDistance(tier);
            ApplyPercentMultiplier(ref LandingRollDistancePercentMultiplier, definition.GetLandingRollDistancePercent(tier));
            LandingRollDurationBonus += definition.GetLandingRollDuration(tier);
            ApplyPercentMultiplier(ref LandingRollDurationPercentMultiplier, definition.GetLandingRollDurationPercent(tier));
            PierceCountBonus += definition.GetPierceCount(tier);
            ExplosionRadiusBonus += definition.GetExplosionRadius(tier);
            ApplyPercentMultiplier(ref ExplosionRadiusPercentMultiplier, definition.GetExplosionRadiusPercent(tier));
        }

        private void AddBaseDamagePercent(WeaponDefinition definition, StatUpgrade.StatCardTier tier, float profileBaseDamage) // 피해 % 등급 규칙
        {
            float percentRate = definition.GetBaseDamagePercent(tier); // 에셋 값
            if (percentRate <= 0.0001f)
            {
                return; // 피해 % 없음
            }

            if (definition.UsesCurrentBaseDamagePercent(tier))
            {
                float currentDamage = ResolveBaseDamage(profileBaseDamage); // 선택 시점 현재 무기 피해
                BaseDamageBonus += Mathf.Max(0f, currentDamage) * percentRate; // 1회 고정 보너스로 잠금
                return;
            }

            ApplyPercentMultiplier(ref BaseDamagePercentMultiplier, percentRate); // 일반/레어 %는 기초 피해 기준
        }

        public static float ToPercentDisplayRate(float storedPercentRate) => storedPercentRate; // UI용 (0.1=10%)

        public static float ToReductionDisplayRate(float storedMultiplier) => 1f - GetReductionMultiplier(storedMultiplier); // UI용 (0.1=10% 감소)

        private static bool HasPercentMultiplier(float storedPercentRate) => storedPercentRate > 0.0001f;

        private static bool HasReductionMultiplier(float storedMultiplier) => storedMultiplier > 0f && storedMultiplier < 1f - 0.0001f;

        private static float GetReductionMultiplier(float storedMultiplier) => storedMultiplier > 0f ? storedMultiplier : 1f; // 0=미적용(×1)

        private static void ApplyPercentMultiplier(ref float storedPercentRate, float rate)
        {
            if (rate <= 0.0001f)
            {
                return; // % 보너스 없음
            }

            storedPercentRate += rate; // 카드마다 % 보너스 누적 (0.1 + 0.1 = 0.2)
        }

        private static void ApplyReductionMultiplier(ref float storedMultiplier, float reductionRate)
        {
            if (reductionRate <= 0.0001f)
            {
                return; // 감소 없음
            }

            storedMultiplier = GetReductionMultiplier(storedMultiplier) * (1f - Mathf.Clamp01(reductionRate)); // 카드마다 해당 스탯 % 감소 곱연산
        }

        // 건준수정 - 0621 ======
        private static float ApplyFlatAndPercent(float profileValue, float flatBonus, float percentRate)
        {
            float finalBeforePercent = profileValue + flatBonus; // 고정 보너스 반영 후 최종 스탯
            return finalBeforePercent + finalBeforePercent * percentRate; // 최종 스탯 × % 만큼 증감
        }
        // 건준수정 - 0621 ======

        public float ResolveBaseDamage(float profileValue)
        {
            float baseValue = Mathf.Max(0f, profileValue); // 세그먼트 레벨 기초 피해
            return Mathf.Max(0f, baseValue + BaseDamageBonus + baseValue * BaseDamagePercentMultiplier); // 기초% + 고정
        }

        public float ResolveSawPierceDamageRatio(float profileValue) => Mathf.Clamp01(profileValue + SawPierceDamageRatioBonus);

        public float ResolveProjectileSpeed(float profileValue) => Mathf.Max(0.1f, ApplyFlatAndPercent(profileValue, ProjectileSpeedBonus, ProjectileSpeedPercentMultiplier));

        public float ResolveSearchRange(float profileValue) => Mathf.Max(0.1f, ApplyFlatAndPercent(profileValue, SearchRangeBonus, SearchRangePercentMultiplier));

        public int ResolveMaxChainDepth(int profileValue) => Mathf.Max(0, profileValue + MaxChainDepthBonus);

        public float ResolveChainRange(float profileValue) => Mathf.Max(0.1f, ApplyFlatAndPercent(profileValue, ChainRangeBonus, ChainRangePercentMultiplier));

        // 건준수정 - 0621 ======
        public float ResolveChainDamageFalloff(float profileValue) => Mathf.Clamp01(profileValue + ChainDamageFalloffBonus); // 기본 + 보너스
        // 건준수정 - 0621 ======

        public int ResolveProjectileCount(int profileValue) => Mathf.Max(1, profileValue + ProjectileCountBonus);

        //전찬우 수정-0622
        public float ResolveCooldown(float profileValue) => Mathf.Max(0.05f, profileValue * GetReductionMultiplier(CooldownReductionMultiplier));

        public float ResolveSideConeAngle(float profileValue) => Mathf.Clamp(profileValue + SideConeAngleBonus, 1f, 180f);

        public float ResolveLaserDuration(float profileValue) => Mathf.Max(0.05f, ApplyFlatAndPercent(profileValue, LaserDurationBonus, LaserDurationPercentMultiplier));

        // 건춘추가 - 0621 ======
        public float ResolveLaserTickInterval(float profileValue) => Mathf.Max(0.02f, profileValue * GetReductionMultiplier(LaserTickIntervalReductionMultiplier)); // 틱 간격 % 감소
        // 건춘추가 - 0621 ======

        public float ResolveLandingRollDistance(float profileValue) => Mathf.Max(0f, ApplyFlatAndPercent(profileValue, LandingRollDistanceBonus, LandingRollDistancePercentMultiplier));

        public float ResolveLandingRollDuration(float profileValue) => Mathf.Max(0.01f, ApplyFlatAndPercent(profileValue, LandingRollDurationBonus, LandingRollDurationPercentMultiplier));

        public int ResolvePierceCount(int profileValue) => Mathf.Max(0, profileValue + PierceCountBonus);

        public float ResolveExplosionRadius(float profileValue) => Mathf.Max(0.1f, ApplyFlatAndPercent(profileValue, ExplosionRadiusBonus, ExplosionRadiusPercentMultiplier));
        // 건춘추가 - 0621 ======
    }
}
