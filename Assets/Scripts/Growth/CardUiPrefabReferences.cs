using UnityEngine;

[CreateAssetMenu(fileName = "CardUiPrefabReferences", menuName = "OZ/Card UI Prefab References")]
public sealed class CardUiPrefabReferences : ScriptableObject
{
    [SerializeField] private GameObject segmentChoiceCardPrefab; // 세그먼트 후보 선택 전용 카드
    [SerializeField] private GameObject rewardChoiceCardPrefab; // 보상 선택 공통 카드
    [SerializeField] private Sprite rewardGoldIconSprite; // 보상 선택 골드 카드 이미지
    [SerializeField] private Sprite rewardExperienceIconSprite; // 보상 선택 경험치 카드 이미지
    [SerializeField] private Sprite rewardSegmentChoiceTicketIconSprite; // 보상 선택권 카드 이미지
    [SerializeField] private Sprite rewardDiamondIconSprite; // 향후 다이아 보상용 예비 이미지
    [SerializeField] private StatUpgradeCatalogAsset statUpgradeCatalog; // 공통 강화 카드 데이터 카탈로그
    [SerializeField] private GameObject[] extraStatUpgradeCards = System.Array.Empty<GameObject>(); // 씬 수정 없이 추가하는 공통 카드

    public GameObject SegmentChoiceCardPrefab => segmentChoiceCardPrefab;
    public GameObject RewardChoiceCardPrefab => rewardChoiceCardPrefab;
    public Sprite RewardGoldIconSprite => rewardGoldIconSprite;
    public Sprite RewardExperienceIconSprite => rewardExperienceIconSprite;
    public Sprite RewardSegmentChoiceTicketIconSprite => rewardSegmentChoiceTicketIconSprite;
    public Sprite RewardDiamondIconSprite => rewardDiamondIconSprite;
    public StatUpgradeCatalogAsset StatUpgradeCatalog => statUpgradeCatalog;
    public GameObject[] ExtraStatUpgradeCards => extraStatUpgradeCards;
}
