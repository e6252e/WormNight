using UnityEngine;

namespace TeamProject01.Gameplay
{
    [System.Serializable]
    public struct SegmentCatalogEntry // 코어가 보유하는 세그먼트 목록 항목
    {
        public string SegmentId; // 세그먼트 고유 ID
        public string DisplayName; // UI 표시명
        [TextArea(2, 4)] public string Description; // UI 설명
        public GameObject Prefab; // 실제 추가 프리팹
        public bool IsSelectable; // 레벨업 선택지 노출
        public bool IsUpgradeable; // 세그먼트 강화 가능

        public string NormalizedId => string.IsNullOrWhiteSpace(SegmentId) ? string.Empty : SegmentId.Trim(); // 비교용 ID
        public bool HasId => !string.IsNullOrWhiteSpace(SegmentId); // ID 존재
        public bool HasPrefab => Prefab != null; // 프리팹 존재
        public bool IsValid => HasId && HasPrefab; // 추가 가능한 기본 조건
        public bool CanShowAsAddChoice => IsValid && IsSelectable; // 세그먼트 추가 카드 후보
        public bool CanShowAsUpgradeChoice => HasId && IsUpgradeable; // 세그먼트 강화 카드 후보
    }
}
