namespace TeamProject01.Gameplay
{
    public struct RunResultDisplayData // 결과 팝업 표시 데이터
    {
        public bool IsClear; // 클리어/오버
        public int ReachedWave; // 도달 웨이브
        public int KillCount; // 처치 수
        public int CollectedDiamond; // 먹은 다이아
        public int ClearDiamondBonus; // 클리어 보너스
        public int DisplayDiamond; // 실제 표시 다이아
    }
}
