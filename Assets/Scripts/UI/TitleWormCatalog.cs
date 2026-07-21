using UnityEngine;

namespace TeamProject01.Gameplay
{
    internal static class TitleWormCatalog // 타이틀 지렁이 표시 데이터
    {
        public static string GetDisplayName(string wormId) // 지렁이 이름
        {
            switch (Normalize(wormId))
            {
                case MetaWormIds.Attack:
                    return "공격형 지렁이";
                case MetaWormIds.Mobility:
                    return "이속형 지렁이";
                case MetaWormIds.Support:
                    return "지원형 지렁이";
                case MetaWormIds.Magic:
                    return "마법형 지렁이";
                default:
                    return "기본형 지렁이";
            }
        }

        public static string GetBonusText(string wormId) // 지렁이 효과
        {
            switch (Normalize(wormId))
            {
                case MetaWormIds.Attack:
                    return "시작 무기: 미사일\n기본 공격력 +1 / 공격속도 +5%";
                case MetaWormIds.Mobility:
                    return "시작 무기: 톱날발사기\n회전력 +10% / 충돌힘 +10%";
                case MetaWormIds.Support:
                    return "시작 무기: 화염구\n넥서스 체력 +15% / 회복 +5";
                case MetaWormIds.Magic:
                    return "시작 무기: 전기지직\n추가 보너스 없음";
                default:
                    return "시작 무기: 대포\n추가 보너스 없음";
            }
        }

        public static Color GetPreviewColor(string wormId) // 프리뷰 색
        {
            switch (Normalize(wormId))
            {
                case MetaWormIds.Attack:
                    return new Color(1f, 0.48f, 0.36f, 1f); // 공격형
                case MetaWormIds.Mobility:
                    return new Color(1f, 0.86f, 0.28f, 1f); // 이속형
                case MetaWormIds.Support:
                    return new Color(0.35f, 0.75f, 1f, 1f); // 지원형
                case MetaWormIds.Magic:
                    return new Color(0.62f, 0.48f, 1f, 1f); // 마법형
                default:
                    return new Color(0.48f, 0.9f, 0.56f, 1f); // 기본형
            }
        }

        private static string Normalize(string wormId) // 지렁이 ID 보정
        {
            return MetaWormIds.Normalize(wormId); // 공용 보정
        }
    }
}
