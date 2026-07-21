namespace TeamProject01.Gameplay
{
    public static class RunLoadoutContext // 씬 전환용 시작 정보
    {
        private static RunStartBonusData startBonus = RunStartBonusData.Create(MetaWormIds.Basic, MetaMapIds.Map1); // 기본값
        private static bool hasStartBonus; // 타이틀 지정 여부

        public static RunStartBonusData CurrentStartBonus => startBonus; // 현재 보너스
        public static bool HasStartBonus => hasStartBonus; // 지정 여부

        public static void SetStartBonus(RunStartBonusData bonus) // 타이틀 → 스테이지
        {
            bonus.SelectedWormId = MetaWormIds.Normalize(bonus.SelectedWormId); // 지렁이 보정

            if (string.IsNullOrWhiteSpace(bonus.SelectedMapId))
            {
                bonus.SelectedMapId = MetaMapIds.Map1; // 기본 맵
            }

            startBonus = bonus; // 저장
            hasStartBonus = true; // 지정됨
        }

        public static bool TryGetStartBonus(out RunStartBonusData bonus) // 스테이지 조회
        {
            bonus = startBonus; // 현재값 반환
            return hasStartBonus; // 타이틀 지정 여부
        }

        public static void Clear() // 테스트 초기화
        {
            startBonus = RunStartBonusData.Create(MetaWormIds.Basic, MetaMapIds.Map1); // 기본 복구
            hasStartBonus = false; // 미지정
        }
    }
}
