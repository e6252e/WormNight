using UnityEngine;

namespace TeamProject01.Gameplay
{
    [CreateAssetMenu(menuName = "OZ/Segments/Attack Profile", fileName = "AP_SegmentAttack")]
    public sealed class SegmentAttackProfile : ScriptableObject // 세그먼트 공격 데이터
    {
        public string DisplayName; // 표시 이름
        [TextArea(2, 4)] public string Description; // 팀원 메모

        [Header("Pattern")]
        public SegmentAttackMoveType MoveType = SegmentAttackMoveType.StraightProjectile; // 이동 방식
        public SegmentAttackImpactType ImpactType = SegmentAttackImpactType.DirectDamage; // 명중 방식

        [Header("Target")]
        [Min(0.1f)] public float SearchRange = 24f; // 탐색 거리
        public SegmentTargetPriorityMode TargetPriorityMode = SegmentTargetPriorityMode.Nearest; // 타겟 우선순위
        // 공격 가능 범위를 원형/양옆 부채꼴 중 선택
        public SegmentAttackAreaMode AttackAreaMode = SegmentAttackAreaMode.FullCircle; // 타겟 탐색 범위 형태
        // SideCones일 때 한쪽 부채꼴의 전체 각도
        [Range(1f, 180f)] public float SideConeAngle = 100f; // 좌우 각각의 부채꼴 각도
        [Min(0.1f)] public float ClusterProbeRadius = 0.5f; // 밀집 탐색 반경
        [Min(1)] public int ClusterMinEnemyCount = 5; // 밀집으로 인정할 최소 몬스터 수
        [Min(0f)] public float TargetAimHeight = 0.45f; // 조준 높이
        [Min(0f)] public float AttackSpawnHeight = 0.42f; // 포구 fallback 높이

        [Header("Attack")]
        [Min(0f)] public float BaseDamage = 1f; // 기본 피해량
        [Min(0.05f)] public float Cooldown = 4f; // 공통 쿨타임
        public bool UseDamageTypeOverride; // 표시/속성 피해 타입 고정
        public DamageType DamageTypeOverride = DamageType.Projectile; // 고정 피해 타입

        [Header("Monster Feedback")]
        public bool ApplyMonsterFeedback = true; // 몬스터 피격 반응 사용
        [Min(0f)] public float MonsterKnockbackDistance = 0.25f; // 넉백 이동 거리
        [Min(0.01f)] public float MonsterKnockbackDuration = 0.08f; // 넉백 지속 시간
        [Min(0f)] public float MonsterStaggerDuration = 0.06f; // 경직 지속 시간
        [Range(0f, 3f)] public float MonsterExplosionFeedbackMultiplier = 2f; // 폭발 피드백 배율
        [Range(0f, 1f)] public float MonsterPierceFeedbackMultiplier = 0.35f; // 관통 피드백 배율
        [Range(0f, 1f)] public float MonsterContinuousFeedbackMultiplier = 0f; // 지속 피해 피드백 배율

        [Header("Projectile")]
        public GameObject ProjectilePrefab; // 투사체 프리팹
        [Min(1)] public int ProjectileCount = 1; // 동시 발사 수
        [Min(0f)] public float SpreadAngle = 0f; // 산탄 각도
        public bool FireProjectilesSequentially; // 순차 발사 사용
        [Min(1)] public int ProjectileVolleySize = 1; // 순차 발사 시 한 번에 묶어 발사할 수
        [Min(0f)] public float ProjectileFireDelay = 0.18f; // 순차 발사 지연
        public bool UseLoadedProjectileVisuals; // 장전 미사일 표시 사용
        [Range(0f, 1f)] public float LoadedProjectileReloadRatio = 0.5f; // 쿨타임 중 복구 시점
        public Vector3 ProjectileScale = Vector3.zero; // 0 이하면 프리팹 기본 크기 유지
        public Vector3 ProjectileVfxScale = Vector3.zero; // 0 이하면 투사체 VFX 자식 크기 유지
        [Min(0.1f)] public float ProjectileSpeed = 20f; // 투사체 속도
        [Min(0.05f)] public float ProjectileHitRadius = 0.5f; // 명중 반경
        [Min(0.1f)] public float ProjectileLifetime = 5f; // 생존 시간
        [Min(0)] public int PierceCount = 3; // 관통 가능 수
        [Range(0f, 1f)] public float PiercingProjectileDamageRatio = 1f; // 일반 관통탄 피해 비율
        [Min(0f)] public float ArcHeight = 3f; // 곡사 높이

        [Header("Impact Point Drop")]
        public bool UseVerticalImpactDrop; // 지점 타격 투사체를 착탄점 위에서 수직 낙하
        [Min(0f)] public float VerticalImpactDropHeight = 10f; // 착탄점 기준 생성 높이

        [Header("Flame Sphere")]
        public bool UseFlameMuzzleInfluence; // 화염 구체가 총구 이동을 약하게 따라갈지 여부
        [Range(0f, 1f)] public float FlameMuzzleInfluenceStrength = 0.3f; // 0이면 완전 직선 이동
        [Min(0f)] public float FlameMuzzleInfluenceRange = 0f; // 0이면 속도*수명 기준 자동 범위

        // 투석기 돌처럼 곡사 착지 후 잠깐 굴러가는 투사체에서만 사용
        [Header("Arc Landing Roll")]
        public bool RollAfterArcLanding; // 곡사 도착 후 바로 터뜨리지 않고 바닥을 굴릴지 여부
        [Min(0f)] public float LandingImpactRadius = 1.1f; // 착지 순간 들어가는 작은 범위 피해
        [Min(0f)] public float LandingRollDamageRadius = 0.75f; // 구르는 동안 돌 주변 피해 반경
        [Range(0f, 1f)] public float LandingRollDamageRatio = 1f; // 구르기 피해 배율
        // 착지 지점부터 굴러갈 거리
        [Min(0f)] public float LandingRollDistance = 0f; // 0이면 기존 곡사처럼 도착 즉시 명중 처리
        // 굴러가는 시간
        [Min(0.01f)] public float LandingRollDuration = 0.35f; // 돌이 바닥에서 굴러가는 연출 시간
        // 굴러가는 동안 모델이 회전하는 속도
        [Min(0f)] public float LandingRollSpinSpeed = 720f; // 초당 회전 각도

        [Header("Explosion")]
        [Min(0.1f)] public float ExplosionRadius = 3f; // 폭발 반경
        [Min(0.05f)] public float ExplosionLifetime = 0.35f; // 폭발 표시 시간

        [Header("Area Telegraph")]
        public GameObject AreaTelegraphPrefab; // 실제 피해 범위 바닥 표시
        [Min(0f)] public float AreaTelegraphGroundOffset = 0.025f; // 바닥 위 표시 높이
        [Range(0f, 1f)] public float AreaTelegraphAlpha = 0.35f; // 장판 투명도

        [Header("Debuff")]
        [Range(0.05f, 1f)] public float SlowMoveSpeedMultiplier = 1f; // 1이면 감속 없음
        [Min(0f)] public float SlowDuration = 0f; // 감속 지속 시간
        public CombatStatusEffectKind StatusEffectOnHit = CombatStatusEffectKind.None; // 명중 시 적용할 상태효과
        public GameObject StatusEffectVfxPrefab; // 상태효과 몸체 VFX

        [Header("Laser")]
        [Min(0.05f)] public float LaserDuration = 0.5f; // 레이저 지속 시간
        [Min(0.02f)] public float LaserTickInterval = 0.15f; // 지속 피해 간격

        [Header("Chain Lightning")]
        [Min(0.1f)] public float ChainRange = 4f; // 번개가 다음 몬스터를 찾는 거리
        [Min(0)] public int MaxChainDepth = 3; // 첫 타격 이후 번개가 이어지는 단계 수
        [Min(1)] public int ChainBranchCount = 2; // 한 번에 갈라질 수 있는 번개 줄기 수
        [Min(0f)] public float ChainDelay = 0.06f; // 다음 몬스터로 번개가 넘어가는 지연 시간
        [Min(0.03f)] public float ChainLineVfxLifetime = 0.4f; // 전기줄 시각 효과 제거 시간
        [Range(0f, 1f)] public float ChainDamageFalloff = 0.5f; // 체인 단계별 피해 감소율

        [Header("Saw Bounce")]
        [Range(0f, 1f)] public float SawPierceDamageRatio = 0.5f; // 목표까지 지나가는 적에게 주는 피해 비율
        [Range(0f, 1f)] public float SawTargetMinDistanceRatio = 0.5f; // 후보 중 중거리 이상을 고르는 기준
        [Min(0f)] public float SawSpinSpeed = 1440f; // 비행 중 초당 회전 각도
        [Header("Aim")]
        public bool RequireAimBeforeFire = true; // 조준 후 발사
        [Min(1f)] public float HeadTurnSpeed = 540f; // 머리 회전 속도
        [Min(0f)] public float FireAngleTolerance = 8f; // 발사 허용 각도
        public bool ContinueAimingDuringProjectileSequence; // 순차 발사 중 특수 조준 정책 사용
        [Range(0.01f, 1f)] public float FiringHeadTurnSpeedMultiplier = 1f; // 발사 중 머리 회전 배율
        public bool UseMuzzleDirectionDuringProjectileSequence; // 발사 중 투사체 방향을 현재 포구 기준으로 사용

        [Header("VFX Slots")]
        public GameObject MuzzleVfxPrefab; // 발사 VFX
        [Min(0f)] public float MuzzleVfxLifetime = 1.5f; // 발사 VFX 제거 시간
        public Vector3 MuzzleVfxScale = Vector3.one; // 발사 VFX 크기/길이 보정
        public GameObject HitVfxPrefab; // 명중 VFX
        [Min(0f)] public float HitVfxLifetime = 2f; // 명중 VFX 제거 시간
        public GameObject ExplosionVfxPrefab; // 폭발 VFX
        [Min(0f)] public float ExplosionVfxLifetime = 2f; // 폭발 VFX 제거 시간
        [Range(0f, 1f)] public float ExplosionVfxAlpha = 0.28f; // 임시 범위 표시 구체 투명도

        // 캐논 발사 순간 비주얼 루트가 포구 반대 방향으로 밀리는 반동값
        [Header("Fire Feedback")]
        [Min(0f)] public float RecoilDistance = 0f; // -Muzzle.forward 방향 이동 거리, 0이면 미사용
        // 캐논 발사 순간 몸체가 살짝 젖는 각도
        [Min(0f)] public float RecoilTiltAngle = 0f; // 반동 방향으로 기울어지는 각도
        // 반동으로 밀려나는 시간
        [Min(0.01f)] public float RecoilKickDuration = 0.05f; // 발사 직후 밀림 시간
        // 원래 위치로 돌아오는 시간
        [Min(0.01f)] public float RecoilReturnDuration = 0.14f; // 복귀 시간
        // 복귀 후 살짝 반대로 되받는 거리 비율
        [Min(0f)] public float RecoilSettleDistanceRatio = 0.22f; // 원점 통과 후 흔들림 거리 비율
        // 복귀 후 살짝 반대로 되받는 회전 비율
        [Min(0f)] public float RecoilSettleTiltRatio = 0.35f; // 원점 통과 후 흔들림 회전 비율
        // 되받은 뒤 자리 잡는 시간
        [Min(0.01f)] public float RecoilSettleDuration = 0.12f; // 무겁게 자리 잡는 시간
    }
}
