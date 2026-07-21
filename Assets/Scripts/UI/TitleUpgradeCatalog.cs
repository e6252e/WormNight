using UnityEngine;

namespace TeamProject01.Gameplay
{
    internal static class TitleUpgradeCatalog // 타이틀 강화 표시 데이터
    {
        public static string GetDisplayName(MetaUpgradeId upgradeId) // 타이틀용 강화 이름
        {
            switch (upgradeId)
            {
                case MetaUpgradeId.GoldBonus:
                    return "골드 보너스";
                case MetaUpgradeId.DiamondBonus:
                    return "다이아 보너스";
                case MetaUpgradeId.TurnBonus:
                    return "회전력 증가";
                case MetaUpgradeId.CollisionForce:
                    return "충돌힘 증가";
                case MetaUpgradeId.BaseAttack:
                    return "기본 공격력 증가";
                case MetaUpgradeId.AttackSpeed:
                    return "기본 공격속도 증가";
                case MetaUpgradeId.NexusMaxHp:
                    return "알 최대체력 증가";
                case MetaUpgradeId.NexusRegen:
                    return "알 분당회복";
                default:
                    return MetaProgressionManager.GetUpgradeDisplayName(upgradeId); // 기본값
            }
        }

        public static string ResolvePlannedName(string plannedKey) // 예정 강화 이름
        {
            return ResolvePlannedName(plannedKey, string.Empty); // 기본
        }

        public static string ResolvePlannedName(string plannedKey, string fallbackName) // 예정 강화 이름
        {
            string key = string.IsNullOrWhiteSpace(plannedKey) ? string.Empty : plannedKey.Trim(); // 키 보정
            switch (key)
            {
                case "planned_rejoin_range":
                    return "재결합 범위 증가";
                case "planned_pickup_range":
                    return "픽업 회수 범위 증가";
                default:
                    return string.IsNullOrWhiteSpace(fallbackName) ? "예정 강화" : fallbackName; // 대체명
            }
        }

        public static Color GetIconColor(MetaUpgradeId upgradeId) // 임시 아이콘 색
        {
            switch (upgradeId)
            {
                case MetaUpgradeId.GoldBonus:
                    return new Color(1f, 0.74f, 0.12f, 1f);
                case MetaUpgradeId.DiamondBonus:
                    return new Color(0.18f, 0.76f, 1f, 1f);
                case MetaUpgradeId.TurnBonus:
                    return new Color(0.78f, 0.78f, 0.74f, 1f);
                case MetaUpgradeId.CollisionForce:
                    return new Color(0.54f, 0.48f, 0.42f, 1f);
                case MetaUpgradeId.BaseAttack:
                    return new Color(0.90f, 0.90f, 0.86f, 1f);
                case MetaUpgradeId.AttackSpeed:
                    return new Color(0.96f, 0.80f, 0.30f, 1f);
                case MetaUpgradeId.NexusMaxHp:
                    return new Color(0.86f, 0.94f, 1f, 1f);
                case MetaUpgradeId.NexusRegen:
                    return new Color(0.46f, 0.95f, 0.32f, 1f);
                default:
                    return Color.white;
            }
        }
    }
}
