namespace TeamProject01.Gameplay
{
    public static class RunResultContext // 스테이지 -> 타이틀 결과 전달
    {
        private static bool hasPendingResult; // 미처리 결과 존재 여부
        private static RunResultData pendingResult; // 타이틀에서 소비할 결과

        public static bool HasPendingResult => hasPendingResult; // 타이틀 진입 확인용

        public static void SetPendingResult(RunResultData result)
        {
            pendingResult = result; // 결과 저장
            hasPendingResult = true; // 소비 대기
        }

        public static bool TryConsumePendingResult(out RunResultData result)
        {
            result = pendingResult; // 현재 결과 반환
            if (!hasPendingResult)
            {
                return false; // 결과 없음
            }

            Clear(); // 1회성 소비
            return true;
        }

        public static void Clear()
        {
            pendingResult = default; // 결과 초기화
            hasPendingResult = false; // 소비 완료
        }
    }
}
