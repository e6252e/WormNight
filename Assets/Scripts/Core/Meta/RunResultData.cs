using System;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    [Serializable]
    public struct RunResultData // 한 판 결과 요약
    {
        [Min(0)] public int ReachedWave; // 도달 웨이브
        [Min(0f)] public float SurviveTime; // 생존 시간
        [Min(0)] public int KillCount; // 처치 수
        public bool IsClear; // 클리어 여부
        [Min(0)] public int CollectedDiamond; // 런 중 먹은 다이아
        [Min(0)] public int ClearDiamondBonus; // 40웨이브 클리어 보너스
        [Min(0)] public int EarnedDiamond; // 지급 다이아
        public bool HasExplicitEarnedDiamond; // 결과창에서 확정한 다이아 보상인지
        [Min(0)] public int EarnedGoldInRun; // 한 판 골드
        public string SelectedWormId; // 사용 지렁이

        public static RunResultData Create(int reachedWave, float surviveTime, int killCount, bool isClear, int earnedDiamond, int earnedGoldInRun, string selectedWormId) // 생성
        {
            RunResultData data = default; // 값 준비
            data.ReachedWave = Mathf.Max(0, reachedWave); // 웨이브
            data.SurviveTime = Mathf.Max(0f, surviveTime); // 시간
            data.KillCount = Mathf.Max(0, killCount); // 처치
            data.IsClear = isClear; // 클리어
            data.EarnedDiamond = Mathf.Max(0, earnedDiamond); // 다이아
            data.CollectedDiamond = data.EarnedDiamond; // 기존 호출 호환
            data.ClearDiamondBonus = 0; // 기존 호출 호환
            data.HasExplicitEarnedDiamond = earnedDiamond > 0; // 0이면 기존 웨이브 공식 사용
            data.EarnedGoldInRun = Mathf.Max(0, earnedGoldInRun); // 골드
            data.SelectedWormId = MetaWormIds.Normalize(selectedWormId); // 지렁이
            return data; // 결과 반환
        }

        public static RunResultData CreateWithExplicitDiamond(int reachedWave, float surviveTime, int killCount, bool isClear, int earnedDiamond, int earnedGoldInRun, string selectedWormId) // 결과창 확정 보상
        {
            RunResultData data = Create(reachedWave, surviveTime, killCount, isClear, earnedDiamond, earnedGoldInRun, selectedWormId);
            data.HasExplicitEarnedDiamond = true; // 0 다이아도 명시 보상으로 취급
            return data;
        }

        public static RunResultData CreateWithDiamondBreakdown(int reachedWave, float surviveTime, int killCount, bool isClear, int collectedDiamond, int clearDiamondBonus, int earnedGoldInRun, string selectedWormId) // 수집/보너스 분리 생성
        {
            int safeCollected = Mathf.Max(0, collectedDiamond); // 수집량
            int safeBonus = Mathf.Max(0, clearDiamondBonus); // 보너스
            RunResultData data = CreateWithExplicitDiamond(reachedWave, surviveTime, killCount, isClear, safeCollected + safeBonus, earnedGoldInRun, selectedWormId);
            data.CollectedDiamond = safeCollected; // 실제 먹은 다이아
            data.ClearDiamondBonus = safeBonus; // 클리어 추가 다이아
            return data;
        }
    }
}
