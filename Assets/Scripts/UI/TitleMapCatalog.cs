using UnityEngine;

namespace TeamProject01.Gameplay
{
    internal static class TitleMapCatalog // 타이틀 맵 표시 데이터
    {
        public static bool IsPlayable(string mapId) // 플레이 가능 여부
        {
            return Normalize(mapId) == MetaMapIds.Map1; // 현재 맵1만 가능
        }

        public static string GetDisplayName(string mapId) // 맵 이름
        {
            switch (Normalize(mapId))
            {
                case MetaMapIds.Map2:
                    return "숲의 경계";
                case MetaMapIds.Map3:
                    return "바위 고원";
                case MetaMapIds.Map4:
                    return "황혼 늪지";
                case MetaMapIds.Map5:
                    return "빛의 신전";
                default:
                    return "초원 유적";
            }
        }

        public static string GetStateText(string mapId) // 맵 상태
        {
            return IsPlayable(mapId) ? "선택 가능" : "업데이트 예정"; // 상태
        }

        public static string GetDescription(string mapId) // 맵 설명
        {
            switch (Normalize(mapId))
            {
                case MetaMapIds.Map2:
                    return "짙은 숲길과 좁은 진입로가 이어지는 경계 지역입니다.\n빠른 적과 매복형 웨이브가 들어갈 예정입니다.";
                case MetaMapIds.Map3:
                    return "무너진 바위 지형이 많은 고원입니다.\n방어선을 흔드는 돌파형 웨이브가 들어갈 예정입니다.";
                case MetaMapIds.Map4:
                    return "해질녘 안개와 늪지가 깔린 위험 지역입니다.\n감속과 원거리 압박 규칙이 들어갈 예정입니다.";
                case MetaMapIds.Map5:
                    return "폐허가 된 빛의 신전입니다.\n후반 고난도 보스 웨이브가 들어갈 예정입니다.";
                default:
                    return "고대의 유적이 남아 있는 드넓은 초원입니다.\n균형 잡힌 지형으로 초보자에게 추천됩니다.";
            }
        }

        public static string GetRecommendedLevelText(string mapId) // 추천 레벨
        {
            switch (Normalize(mapId))
            {
                case MetaMapIds.Map2:
                    return "추천 레벨 : 6 ~ 12";
                case MetaMapIds.Map3:
                    return "추천 레벨 : 13 ~ 20";
                case MetaMapIds.Map4:
                    return "추천 레벨 : 21 ~ 30";
                case MetaMapIds.Map5:
                    return "추천 레벨 : 31+";
                default:
                    return "추천 레벨 : 1 ~ 5";
            }
        }

        public static string GetPowerText(string mapId) // 권장 전투력
        {
            switch (Normalize(mapId))
            {
                case MetaMapIds.Map2:
                    return "1,800";
                case MetaMapIds.Map3:
                    return "3,200";
                case MetaMapIds.Map4:
                    return "5,000";
                case MetaMapIds.Map5:
                    return "7,500";
                default:
                    return "1,000";
            }
        }

        public static string GetEnemyTypeText(string mapId) // 주요 적 유형
        {
            switch (Normalize(mapId))
            {
                case MetaMapIds.Map2:
                    return "기동형";
                case MetaMapIds.Map3:
                    return "돌파형";
                case MetaMapIds.Map4:
                    return "마법형";
                case MetaMapIds.Map5:
                    return "보스형";
                default:
                    return "균형형";
            }
        }

        public static string GetRuleText(string mapId) // 특수 규칙
        {
            switch (Normalize(mapId))
            {
                case MetaMapIds.Map2:
                    return "숲길 매복";
                case MetaMapIds.Map3:
                    return "낙석 지역";
                case MetaMapIds.Map4:
                    return "늪지 감속";
                case MetaMapIds.Map5:
                    return "정예 강화";
                default:
                    return "없음";
            }
        }

        public static string GetRewardText(string mapId) // 보상 요약
        {
            switch (Normalize(mapId))
            {
                case MetaMapIds.Map2:
                    return "골드 / 다이아 / 숲 문장";
                case MetaMapIds.Map3:
                    return "골드 / 다이아 / 고원 문장";
                case MetaMapIds.Map4:
                    return "골드 / 다이아 / 늪지 문장";
                case MetaMapIds.Map5:
                    return "골드 / 다이아 / 신전 문장";
                default:
                    return "골드 / 다이아 / 초원 문장";
            }
        }

        public static Color GetPreviewColor(string mapId) // 맵 프리뷰 색
        {
            switch (Normalize(mapId))
            {
                case MetaMapIds.Map2:
                    return new Color(0.22f, 0.50f, 0.32f, 1f); // 숲
                case MetaMapIds.Map3:
                    return new Color(0.55f, 0.49f, 0.36f, 1f); // 바위
                case MetaMapIds.Map4:
                    return new Color(0.28f, 0.25f, 0.42f, 1f); // 늪지
                case MetaMapIds.Map5:
                    return new Color(0.58f, 0.72f, 0.78f, 1f); // 신전
                default:
                    return new Color(0.36f, 0.62f, 0.32f, 1f); // 초원
            }
        }

        public static Color GetEmblemColor(string mapId) // 맵 문장색
        {
            switch (Normalize(mapId))
            {
                case MetaMapIds.Map2:
                    return new Color(0.42f, 0.76f, 0.28f, 1f); // 녹색
                case MetaMapIds.Map3:
                    return new Color(0.82f, 0.34f, 0.22f, 1f); // 적갈색
                case MetaMapIds.Map4:
                    return new Color(0.55f, 0.30f, 0.82f, 1f); // 보라
                case MetaMapIds.Map5:
                    return new Color(1.0f, 0.72f, 0.18f, 1f); // 금색
                default:
                    return new Color(0.58f, 0.78f, 0.22f, 1f); // 초원
            }
        }

        private static string Normalize(string mapId) // 맵 ID 보정
        {
            return MetaMapIds.Normalize(mapId); // 공용 보정
        }
    }
}
