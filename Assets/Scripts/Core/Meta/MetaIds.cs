namespace TeamProject01.Gameplay
{
    public static class MetaWormIds // 타이틀 지렁이 ID
    {
        public const string Basic = "worm_basic"; // 기본형
        public const string Attack = "worm_attack"; // 공격형
        public const string Mobility = "worm_mobility"; // 이속형
        public const string Support = "worm_support"; // 지원형
        public const string Magic = "worm_magic"; // 마법형

        public const string Defense = "worm_defense"; // 이전 지원형
        public const string Armed = "worm_armed"; // 이전 공격형
        public const string Charge = "worm_charge"; // 이전 이속형

        public static string Normalize(string wormId) // 저장값 보정
        {
            if (string.IsNullOrWhiteSpace(wormId))
            {
                return Basic; // 기본값
            }

            string normalized = wormId.Trim(); // 공백 제거
            switch (normalized)
            {
                case Defense:
                    return Support; // 구 방어형
                case Armed:
                    return Attack; // 구 무장형
                case Charge:
                    return Mobility; // 구 돌격형
                default:
                    return normalized; // 현행 ID
            }
        }
    }

    public static class MetaMapIds // 타이틀 맵 ID
    {
        public const string Map1 = "map_01"; // 현재 선택 가능
        public const string Map2 = "map_02"; // 업데이트 예정
        public const string Map3 = "map_03"; // 업데이트 예정
        public const string Map4 = "map_04"; // 업데이트 예정
        public const string Map5 = "map_05"; // 업데이트 예정

        public static string Normalize(string mapId) // 저장값 보정
        {
            if (string.IsNullOrWhiteSpace(mapId))
            {
                return Map1; // 기본 맵
            }

            string normalized = mapId.Trim(); // 공백 제거
            switch (normalized)
            {
                case Map1:
                case Map2:
                case Map3:
                case Map4:
                case Map5:
                    return normalized; // 등록 맵
                default:
                    return Map1; // 알 수 없는 값은 기본 맵
            }
        }
    }

    public enum MetaUpgradeId // 업그레이드 종류
    {
        GoldBonus,
        DiamondBonus,
        TurnBonus,
        CollisionForce,
        BaseAttack,
        AttackSpeed,
        NexusMaxHp,
        NexusRegen
    }
}
