using UnityEngine;

namespace TeamProject01.Gameplay
{
    public enum SegmentSupportAbilityKind
    {
        None = 0,
        FinalDamageBuff = 1,
        PickupMagnet = 2,
        FreezeArea = 3,
        FinalAttackSpeedBuff = 4,
        HolyWaterVulnerabilitySpray = 5,
        WormholePortal = 6
    }

    [CreateAssetMenu(menuName = "OZ/Segments/Support Ability Profile", fileName = "SP_SG##_Support")]
    public sealed class SegmentSupportAbilityProfile : ScriptableObject
    {
        public SegmentSupportAbilityKind AbilityKind;

        [Header("Timing")]
        public bool StartsReady = true;
        [Min(0f)] public float Cooldown = 5f;
        [Min(0f)] public float ActiveDurationSeconds = 5f;
        [Min(0f)] public float EffectDurationSeconds = 5f;

        [Header("Targeting")]
        [Min(0f)] public float Range = 6f;
        [Min(0)] public int FrontSegmentCount;
        [Min(0)] public int BackSegmentCount;

        [Header("Multipliers")]
        [Min(0f)] public float FinalDamageMultiplier = 1f;
        [Min(0f)] public float FinalAttackSpeedMultiplier = 1f;
        [Min(0f)] public float IncomingDamageMultiplier = 1f;

        [Header("VFX")]
        public GameObject ActiveVfxPrefab;
        public GameObject RangeVfxPrefab;
        public GameObject TargetBodyVfxPrefab;
        public GameObject EnemyDebuffVfxPrefab;

        [Header("Status Effects")]
        public CombatStatusEffectKind EnemyStatusEffect = CombatStatusEffectKind.None;

        [Header("Wormhole Portal")]
        public GameObject WormholeProjectilePrefab; // 웜홀 투사체 프리팹
        public GameObject WormholeImpactVfxPrefab; // 폭발 지점 VFX
        public GameObject WormholeTeleportOutVfxPrefab; // 미사용: 폭발 자체를 블랙홀로 사용
        public GameObject WormholeTeleportArrivalVfxPrefab; // 미사용: 한 지점으로 직접 이동
        [Min(0.1f)] public float WormholeProjectileSpeed = 18f; // 투사체 속도
        [Min(0.05f)] public float WormholeProjectileHitRadius = 0.6f; // 투사체 명중 반경
        [Min(0.1f)] public float WormholeProjectileLifetime = 4f; // 투사체 수명
        [Min(0.1f)] public float WormholeExplosionRadius = 5f; // 텔레포트 대상 반경
        [Min(0f)] public float WormholeTeleportMinNexusDistance = 40f; // 넥서스 기준 최소 도착 거리
        [Min(0f)] public float WormholeTeleportMaxNexusDistance = 50f; // 넥서스 기준 최대 도착 거리
        [Min(1)] public int WormholeMaxTeleportTargets = 12; // 최대 이동 대상 수
        public bool WormholeAffectBosses; // 보스 이동 허용 여부
        [Min(0f)] public float WormholeTargetAimHeight = 0.6f; // 조준 높이
        [Min(0f)] public float WormholeAimTurnSpeed = 720f; // 머리 회전 속도
        [Range(0f, 45f)] public float WormholeFireAngleTolerance = 6f; // 발사 허용 각도
        [Min(0.05f)] public float WormholeVfxLifetime = 1.5f; // 웜홀 VFX 수명
        [Range(0f, 1f)] public float WormholeVfxAlpha = 1f; // 웜홀 VFX 알파
        public bool WormholeShowDebugRadius = true; // 임시 범위 링 표시

        [TextArea(2, 5)] public string Memo;
    }
}
