using TeamProject01.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SegmentAddCard : MonoBehaviour
{
    private static readonly Vector2 CommonCardIconSlotSize = new Vector2(130f, 130f); // 공통카드 Image 슬롯 기준 크기

    [Header("코어 세그먼트 추가")]
    [Min(1)][SerializeField] private int levelDelta = 1; // 선택 시 소비할 레벨 증가량
    ////// 전찬우수정 - 카탈로그 후보/액션 카드에서 세그먼트 ID를 런타임 주입받도록 변경
    // [SerializeField] private string segmentId = "SG01_MachineGun"; // 기존 고정 세그먼트 ID
    [SerializeField] private string segmentId = string.Empty; // 현재 카드가 가리키는 세그먼트 ID
    [Min(1)][SerializeField] private int segmentAddCount = 1; // 추가할 세그먼트 수

    ////// 전찬우추가 - 기존 카드 프리팹 텍스트를 런타임에서 바꾸기 위한 참조
    [SerializeField] private TMP_Text titleText; // 카드 제목
    ////// 전찬우추가 - 기존 카드 프리팹 텍스트를 런타임에서 바꾸기 위한 참조
    [SerializeField] private TMP_Text descriptionText; // 카드 설명

    ////// 전찬우추가 - 없음/불가 카드 클릭 방지용 상태
    public bool IsSelectableChoice { get; private set; } = true; // CardUI가 클릭 가능 여부 판단
    ////// 전찬우추가 - 외부에서 정리된 세그먼트 ID를 조회
    public string SegmentId => string.IsNullOrWhiteSpace(segmentId) ? string.Empty : segmentId.Trim(); // 공백 제거 ID
    ////// 전찬우추가 - 외부에서 레벨 소비량 조회
    public int LevelDelta => Mathf.Max(1, levelDelta); // 최소 1 보정
    ////// 전찬우추가 - 외부에서 추가 수 조회
    public int SegmentAddCount => Mathf.Max(1, segmentAddCount); // 최소 1 보정

    [Header("카드 아이콘")]
    [SerializeField] private Image cardIconImage; // 강화별 아이콘 표시 대상

    private StatUpgrade.StatCardTier weaponEnhancementTier = StatUpgrade.StatCardTier.Normal; // 현재 등급
    private float baseTitleFontSize = -1f; // 제목 자동 축소 기준
    private float baseDescriptionFontSize = -1f; // 설명 줄 수에 따른 폰트 복구 기준

    public StatUpgrade.StatCardTier WeaponEnhancementTier => weaponEnhancementTier; // 외부 등급 조회

    public void ApplyWeaponEnhancementTier(StatUpgrade.StatCardTier tier) // 생성 후 등급 저장
    {
        weaponEnhancementTier = tier; // 등급 저장 (VFX는 CardEffect가 처리)
    }

    public void ApplyCardIcon(Sprite icon, float sizeOffset = 0f) // WeaponDefinition 아이콘 적용
    {
        if (icon == null)
        {
            return; // 아이콘 없음
        }

        Image image = ResolveCardIconImage();
        if (image == null)
        {
            return; // 표시 대상 없음
        }

        Vector2 slotSize = ResolveCommonCardIconSlotSize(image.rectTransform); // 공통카드 기준 슬롯 크기
        image.sprite = icon;
        image.enabled = true;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        if (slotSize.sqrMagnitude <= 0.0001f)
        {
            image.SetNativeSize(); // 슬롯 정보가 없을 때만 fallback
            slotSize = ResolveCommonCardIconSlotSize(image.rectTransform);
        }

        image.rectTransform.sizeDelta = ApplyIconSizeOffset(slotSize, sizeOffset); // 슬롯 기준 크기 조절
    }

    // 슬롯 크기 기준으로 비율 조절 (0=슬롯 원본, -50=절반, 100=두배)
    private static Vector2 ApplyIconSizeOffset(Vector2 slotSize, float offset)
    {
        float positiveOffset = Mathf.Max(0f, Mathf.Clamp(offset, -100f, 100f)); // 과거 축소값(-80)은 공통 크기 이하로 줄이지 않음
        if (Mathf.Approximately(positiveOffset, 0f))
        {
            return slotSize; // 공통카드 크기
        }

        float scale = 1f + positiveOffset / 100f;
        return slotSize * scale;
    }

    private static Vector2 ResolveCommonCardIconSlotSize(RectTransform iconRect)
    {
        if (iconRect == null)
        {
            return CommonCardIconSlotSize; // 안전 fallback
        }

        Vector2 slotSize = iconRect.sizeDelta;
        if (slotSize.sqrMagnitude <= 0.0001f)
        {
            return CommonCardIconSlotSize; // stretch/미설정이면 공통카드 기준
        }

        return new Vector2(
            Mathf.Max(Mathf.Abs(slotSize.x), CommonCardIconSlotSize.x),
            Mathf.Max(Mathf.Abs(slotSize.y), CommonCardIconSlotSize.y)); // 작은 슬롯은 공통 크기로 보정
    }

    ////// 전찬우추가 - 세그먼트 카탈로그 후보 카드로 설정
    public void ConfigureCandidate(SegmentCatalogEntry entry)
    {
        segmentId = entry.NormalizedId; // 카탈로그 ID 저장
        IsSelectableChoice = entry.CanShowAsAddChoice; // 유효 후보만 선택 가능
        string title = string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.NormalizedId : entry.DisplayName; // 표시명 fallback
        string description = string.IsNullOrWhiteSpace(entry.Description) ? $"{entry.NormalizedId} 선택" : entry.Description; // 설명 fallback
        ApplyTexts(title, description); // 화면 문구 갱신
        SetButtonInteractable(IsSelectableChoice); // 버튼 상태 갱신
    }

    ////// 전찬우추가 - 추가/레벨업 2차 선택 카드로 설정
    public void ConfigureAction(string targetSegmentId, string title, string description, bool selectable)
    {
        segmentId = string.IsNullOrWhiteSpace(targetSegmentId) ? string.Empty : targetSegmentId.Trim(); // 대상 ID 저장
        IsSelectableChoice = selectable && !string.IsNullOrWhiteSpace(segmentId); // 대상이 있어야 선택 가능
        ApplyTexts(title, description); // 액션 문구 갱신
        SetButtonInteractable(IsSelectableChoice); // 버튼 상태 갱신
    }

    ////// 전찬우추가 - 후보가 3개보다 적을 때 표시하는 빈 카드
    public void ConfigureEmpty()
    {
        segmentId = string.Empty; // 적용 대상 없음
        IsSelectableChoice = false; // 클릭 불가
        ApplyTexts("없음", "선택 가능한 세그먼트 없음"); // 빈 슬롯 표시
        SetButtonInteractable(false); // 버튼 비활성화
    }

    ////// 무기 강화 2단계 카드 설정 (세그먼트 선택 후 강화 카드 UI)
    public void ConfigureWeaponEnhancement(WeaponDefinition definition, int levelDeltaValue, string overrideDescription = null)
    {
        if (definition == null)
        {
            ConfigureEmpty(); // 데이터 없음
            return;
        }

        levelDelta = Mathf.Max(1, levelDeltaValue); // 소비 레벨
        segmentId = definition.NormalizedTargetSegmentId; // 대상 세그먼트
        IsSelectableChoice = definition.HasAnyStatBonus && !string.IsNullOrWhiteSpace(segmentId); // 유효 강화만 선택
        string title = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.NormalizedId : definition.DisplayName; // 제목
        string description = string.IsNullOrWhiteSpace(overrideDescription)
            ? (string.IsNullOrWhiteSpace(definition.Description) ? definition.NormalizedId : definition.Description)
            : overrideDescription; // 설명
        ApplyTexts(title, description); // 카드 문구
        // 안건준 추가 - 0623 : WeaponDefinition에 지정된 세그먼트 Lv1 아이콘 + 크기 조절 적용
        if (definition.CardIconSprite != null)
        {
            ApplyCardIcon(definition.CardIconSprite, definition.CardIconSizeOffset);
        }
        SetButtonInteractable(IsSelectableChoice); // 버튼 상태
    }

    public GrowthStatData CreateGrowthStatData() // 코어로 보낼 세그먼트 추가 데이터
    {
        ////// 전찬우수정 - 런타임 주입 ID/보정값을 사용
        // return GrowthStatData.CreateAddSegment(levelDelta, segmentId, segmentAddCount); // 기존 고정 ID 기반
        return GrowthStatData.CreateAddSegment(LevelDelta, SegmentId, SegmentAddCount); // 현재 카드 ID 기반
    }

    public bool TryApplyToCore() // 코어에 세그먼트 추가 적용
    {
        GrowthStatData growth = CreateGrowthStatData(); // 적용할 데이터 준비
        if (!growth.HasAnyValue) // 레벨/세그먼트 ID 없음
        {
            return false; // 적용 실패
        }

        return CoreStatProvider.TryApplyGrowth(growth); // 경험치 소비 + 세그먼트 추가
    }

    // =============== [임시] 시작 ===============
    // 세그먼트 추가 없이 레벨/경험치만 반영 (세그먼트 코어 연동 복구 전까지)
    public bool TryApplyLevelOnlyToCore()
    {
        GrowthStatData growth = GrowthStatData.CreateConvoyUpgrade(levelDelta, 0f, 0f, 0f, 0f, 0f);
        return CoreStatProvider.TryApplyGrowth(growth);
    }
    // =============== [임시] 끝 ===============

    ////// 전찬우추가 - 카드 프리팹 안의 TMP 텍스트에 문구 반영
    private void ApplyTexts(string title, string description)
    {
        CacheTextReferences(); // 텍스트 참조 보강
        if (titleText != null)
        {
            ApplySingleLineAutoSize(titleText, ref baseTitleFontSize, title);
            titleText.text = title; // 제목 갱신
        }

        if (descriptionText != null)
        {
            string displayDescription = SegmentCardTagPresenter.Apply(gameObject, description, descriptionText);
            descriptionText.richText = true;
            ApplyDescriptionAutoSize(descriptionText, displayDescription);
            descriptionText.text = displayDescription; // 설명 갱신
        }
    }

    private void ApplyDescriptionAutoSize(TMP_Text target, string description)
    {
        if (target == null)
        {
            return;
        }

        if (baseDescriptionFontSize <= 0f)
        {
            baseDescriptionFontSize = target.fontSize;
        }

        int lineCount = CountDescriptionLines(description);
        float maxSize = lineCount >= 3 ? baseDescriptionFontSize * 0.86f : baseDescriptionFontSize;
        ConfigureAutoSize(target, maxSize, true);
    }

    private void ApplySingleLineAutoSize(TMP_Text target, ref float baseFontSize, string text)
    {
        if (target == null)
        {
            return;
        }

        if (baseFontSize <= 0f)
        {
            baseFontSize = target.fontSize;
        }

        ConfigureAutoSize(target, baseFontSize, false);
    }

    private static void ConfigureAutoSize(TMP_Text target, float maxSize, bool allowWrapping)
    {
        target.enableAutoSizing = true;
        target.fontSizeMax = maxSize;
        target.fontSizeMin = Mathf.Max(8f, maxSize * 0.62f);
        target.fontSize = maxSize;
        target.textWrappingMode = allowWrapping ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
    }

    private static int CountDescriptionLines(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return 0;
        }

        string normalized = description.Replace("\r\n", "\n").Replace('\r', '\n');
        return Mathf.Max(1, normalized.Split('\n').Length);
    }

    ////// 전찬우추가 - 인스펙터 연결 없이 기존 카드 텍스트 2개를 자동 사용
    private void CacheTextReferences()
    {
        if (titleText != null && descriptionText != null)
        {
            return; // 이미 준비됨
        }

        // 안건준 추가 - 0623 : 이름 기반 검색 우선 (SegmentUpgradeCard 프리팹의 Card_Text / DescText 대응)
        if (titleText == null)
        {
            titleText = FindTextByName("Card_Text");
        }

        if (descriptionText == null)
        {
            descriptionText = FindTextByName("DescText");
        }

        // 이름으로 못 찾은 경우 인덱스 fallback
        if (titleText != null && descriptionText != null)
        {
            return;
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true); // 하위 텍스트 검색
        if (titleText == null && texts.Length > 0)
        {
            titleText = texts[0]; // 기존 카드 첫 텍스트
        }

        if (descriptionText == null && texts.Length > 1)
        {
            descriptionText = texts[1]; // 기존 카드 두 번째 텍스트
        }
    }

    // 안건준 추가 - 0623 : 자식 오브젝트 이름으로 TMP_Text 검색
    private TMP_Text FindTextByName(string objectName)
    {
        Transform found = transform.Find(objectName); // 직계 자식 검색
        if (found != null && found.TryGetComponent(out TMP_Text text))
        {
            return text;
        }

        // 직계 자식에 없으면 전체 하위 탐색
        TMP_Text[] all = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].gameObject.name == objectName)
            {
                return all[i];
            }
        }

        return null;
    }

    ////// 전찬우추가 - 없음/불가 카드는 실제 버튼도 막음
    private void SetButtonInteractable(bool interactable)
    {
        Button button = GetComponent<Button>(); // 루트 버튼
        if (button == null)
        {
            button = GetComponentInChildren<Button>(true); // 자식 버튼 fallback
        }

        if (button != null)
        {
            button.interactable = interactable; // 클릭 가능 여부 반영
        }
    }

    private Image ResolveCardIconImage()
    {
        if (cardIconImage != null)
        {
            return cardIconImage;
        }

        // 안건준 추가 - 0623 : SegmentUpgradeCard 프리팹의 "Image" 오브젝트 우선 탐색
        Transform imageTransform = transform.Find("Image");
        if (imageTransform != null && imageTransform.TryGetComponent(out Image namedImage))
        {
            cardIconImage = namedImage;
            return cardIconImage;
        }

        Transform iconTransform = transform.Find("Icon");
        if (iconTransform != null && iconTransform.TryGetComponent(out Image iconImage))
        {
            cardIconImage = iconImage;
            return cardIconImage;
        }

        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image candidate = images[i];
            if (candidate == cardIconImage)
            {
                continue;
            }

            cardIconImage = candidate;
            return cardIconImage;
        }

        return null;
    }

}
