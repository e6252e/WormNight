namespace TeamProject01.Gameplay
{
    public enum SegmentAttackMoveType // 공격 이동 방식
    {
        StraightProjectile = 0, // 직선 투사체
        PiercingProjectile = 1, // 관통 투사체
        ArcProjectile = 2, // 곡사 투사체
        HomingProjectile = 3, // 추적 투사체
        Laser = 4, // 레이저
        ChainLightning = 5, // 즉시 체인 번개
        SawBounceProjectile = 6, // 톱날 관통 연쇄 투사체
        ExpandingFlameSphere = 7 // 전진하며 커지는 화염 판정 구체
    }

    public enum SegmentAttackImpactType // 명중 처리 방식
    {
        DirectDamage = 0, // 직접 피해
        PierceDamage = 1, // 관통 피해
        ExplosionArea = 2, // 폭발 범위 피해
        ContinuousDamage = 3, // 지속 피해
        ChainDamage = 4 // 체인 번개 피해
    }

    // 세그먼트가 몬스터를 찾을 때 사용하는 공격 가능 범위 형태
    public enum SegmentAttackAreaMode
    {
        FullCircle = 0, // 기존 방식: 사거리 안이면 모든 방향 공격 가능
        SideCones = 1 // 좌우 부채꼴 안에서만 공격 가능
    }

    public enum SegmentTargetPriorityMode // 타겟 우선순위
    {
        Nearest = 0, // 기존 방식: 가장 가까운 몬스터
        BossEliteThenFarthest = 1, // 보스 > 엘리트 > 일반, 같은 등급이면 가장 먼 몬스터
        DensestClusterOrRandom = 2 // 밀집 구역 우선, 없으면 사거리 내 랜덤 몬스터
    }
}
