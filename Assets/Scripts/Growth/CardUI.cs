using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DG.Tweening;
using TeamProject01.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class CardUI : MonoBehaviour
{
    private const string DescriptionValueToken = "(N)";
    private const string DescriptionValueColor = "#2F6BFF";
    private const string CardTooltipRootName = "CardTooltip";
    private const string CardTooltipTextName = "CardTooltipText";
    private const string LegacyCardTooltipRootName = "DpsTooltip";
    private const string LegacyCardTooltipTextName = "DpsTooltipText";
    private static readonly Vector2 CardTooltipCursorOffset = new Vector2(14f, -14f);
    private const float CardTooltipMinWidth = 96f;
    private const float CardTooltipMaxWidth = 420f;
    private const float CardTooltipHorizontalPadding = 24f;
    private const float CardTooltipMinHeight = 28f;
    private const float CardTooltipLineHeight = 18f;
    private const float CardTooltipVerticalPadding = 8f;
    private const float CardTooltipFontSizeMax = 14f;
    private const float CardTooltipFontSizeMin = 10f;
    private const string CardTooltipValueColor = "#FFD75A";
    private const string CardTooltipNewColor = "#7DFFB2";
    private static readonly Vector2 CardTooltipHiddenAnchoredPosition = new Vector2(-10000f, -10000f);
    private static readonly Vector2 CardTooltipHiddenSize = new Vector2(1f, 1f);
    private const float CardTooltipFadeSeconds = 0.08f;
    private const string CardUiPrefabReferencesResourcePath = "LevelCard/CardUiPrefabReferences";
    private const string TierFrameNormalResourcePath = "LevelCard/TierFrames/CardFrame_Normal";
    private const string TierFrameRareResourcePath = "LevelCard/TierFrames/CardFrame_Rare";
    private const string TierFrameUniqueResourcePath = "LevelCard/TierFrames/CardFrame_Unique";
    private const string AutoSelectInAutoOrbitPrefsKey = "OZ.CardUI.AutoSelectInAutoOrbit";

    [Header("Stat Upgrade")]
    [SerializeField] private GameObject[] statUpgradeCards = System.Array.Empty<GameObject>(); // 스탯 강화 카드 프리팹
    [SerializeField] private StatUpgradeCatalogAsset statUpgradeCatalogAsset; // 공통 강화 카드 데이터 카탈로그

    [Header("Add Segment")]
    // 안건준 수정 - 0623 : 세그먼트 카드 공통 기본 프리팹 (SegmentUpgradeCard 드래그)
    [Header("세그먼트 카드 공통 기본 프리팹")]
    [SerializeField] private GameObject segmentCardBasePrefab; // 세그먼트 카드 공통 프리팹 (SegmentUpgradeCard)
    [Header("세그먼트 선택카드 전용 프리팹")]
    [SerializeField] private GameObject segmentChoiceCardPrefab; // 후보 선택 1단계 전용 프리팹
    [SerializeField] private CardUiPrefabReferences prefabReferences; // Resources 중앙 참조
    // 안건준 추가 - 0623 : 세그먼트 카드 아이콘 크기 조절 (0=원본, -100=절반, +100=두배)
    [Header("세그먼트 카드 아이콘 크기 조절")]
    [Range(-100f, 100f)][SerializeField] private float segmentCardIconSizeOffset = 0f; // 세그먼트 아이콘 크기

    // 안건준 추가 - 0623 : 카드 등급별 VFX 이팩트 (같은 오브젝트의 CardEffect 컴포넌트)
    [SerializeField] private CardEffect cardEffect; // 카드 등급 이팩트 컴포넌트

    // 안건준 추가 - 0624 : 카드 사운드 매니저
    [SerializeField] private CardSoundManager cardSound;

    [Header("세그먼트 강화 카탈로그")]
    [SerializeField] private WeaponCatalogAsset weaponCatalogAsset; // 무기 강화 2단계 카탈로그

    [Header("레어 카드 등장 확률")]
    [Tooltip("레어: 보너스 2배, 노란색 — 스탯 카드·세그먼트 무기 강화 카드 모두 적용")]
    [Range(0f, 100f)][SerializeField] private float rareCardChancePercent = 30f; // 레어 등장 확률(%)
    [Header("유니크 카드 등장 확률")]
    [Tooltip("유니크: 보너스 3배, 초록색 — 스탯 카드·세그먼트 무기 강화 카드 모두 적용")]
    [Range(0f, 100f)][SerializeField] private float uniqueCardChancePercent = 10f; // 유니크 등장 확률(%)

    [Header("스탯 카드 선택 가중치")]
    [Tooltip("직전에 선택한 카드 프리팹이 다음 선택지에 더 자주 나오도록 추가 가중치")]
    [Min(0f)][SerializeField] private float baseCardSpawnWeight = 100f; // 모든 카드 기본 가중치
    [Min(0f)][SerializeField] private float selectedCardWeightBonus = 50f; // 직전 선택 카드 추가 가중치

    private GameObject lastSelectedStatCardPrefab; // 직전 선택한 스탯 카드 프리팹 (다음 뽑기 가중치용)
    private StatUpgradeDefinition lastSelectedStatCardDefinition; // 직전 선택한 스탯 카드 데이터 (다음 뽑기 가중치용)

    [Header("카드 생성 슬롯")]
    [SerializeField] private RectTransform[] cardSlots = System.Array.Empty<RectTransform>(); // 카드 생성 위치
    [Min(1)][SerializeField] private int cardsToSpawn = 3; // 한 번에 생성할 카드 수

    [Header("보상 선택 카드")]
    [Min(0)][SerializeField] private int rewardGoldBaseAmount = 100; // 일반 골드 보상
    [Min(0)][SerializeField] private int rewardExperienceBaseAmount = 200; // 일반 경험치 보상
    [Min(1)][SerializeField] private int rewardSegmentTicketBaseCount = 1; // 일반 세그먼트 선택권 수
    [Min(0)][SerializeField] private int segmentTicketBonusRerollCount = 5; // 선택권 진입 리롤 보너스

    [Header("카드 연출")]
    [SerializeField] private float startYOffset = -80.0f; // 등장 시작 Y 오프셋
    [SerializeField] private float hoverScale = 1.09f; // 마우스 오버 배율

    [Header("카드 선택 후 닫기 (A/B 공통)")]
    [Tooltip("선택 카드만 보여준 뒤 패널을 닫기까지 대기(초). 기존 0.5")]
    [SerializeField] private float selectionCloseHoldSeconds = 0.06f; // 선택 후 홀드
    [Tooltip("선택 카드 강조 트윈 — 커지는 시간")]
    [SerializeField] private float selectionSelectUpSeconds = 0.105f; // 기존 0.2
    [Tooltip("선택 카드 강조 트윈 — 원래 크기 복귀")]
    [SerializeField] private float selectionSelectDownSeconds = 0.07f; // 기존 0.15
    [Tooltip("레벨업 패널 페이드 아웃 (일시정지 해제 직전)")]
    [SerializeField] private float selectionPanelCloseFadeSeconds = 0.12f; // 기존 LevelUpUi 0.25

    [Header("레벨업 패널 감지")]
    [SerializeField] private CanvasGroup levelUpPanelCanvasGroup; // LevelUpPanel Canvas Group

    [Header("레벨업 UI")]
    [SerializeField] private LevelUpUi levelUpUi; // 비워두면 자동 검색

    [Header("세그먼트 선택 후보")]
    [Tooltip("세그먼트 선택 3장 중 보유 Lv3 미만 세그먼트 1장을 먼저 뽑을 확률")]
    [Range(0f, 1f)][SerializeField] private float ownedSegmentChoiceGuaranteeChance = 0.5f; // 보유 Lv3 미만 확정 후보 확률

    [Header("세그먼트 무기 강화선택 조건")]
    [Tooltip("A모드 (체크): 세그먼트 선택 → 선택한 세그먼트의 강화 카드 선택 / B모드 (해제): 보유 세그먼트 강화만 랜덤 3장 (미보유 제외)")]
    [SerializeField] private bool useSegmentSelectWeaponEnhanceFlow = true; // A 기준 — 세그먼트 선택 후 강화
    [Header("A 모드 (체크) 세그먼트 기본 가중치 ")]
    [Min(0f)][SerializeField] private float weaponEnhanceSegmentBaseWeight = 100f; // 보유 0개도 이 가중치로 후보 가능
    [Header("A 모드 (체크) 세그먼트 개수 비례 가중치 증가 ")]
    [Min(0f)][SerializeField] private float weaponEnhanceSegmentWeightPerOwned = 50f; // 세그먼트 개수 비례 가중치

    [Header("디버그 세그먼트 갯수 표기")]
    [SerializeField] private bool logPlayerSegmentCounts = true; // 세그먼트 추가/레벨업 후 현재 구성 출력

    // 건춘추가 - 0621 ======
    [Header("세그먼트 무기 스탯 UI (TMP)")]
    [Tooltip("켜면 아래 Stat Text에 선택한 세그먼트 기본+강화 합산 스탯 표시")]
    [SerializeField] private bool showSegmentWeaponStatUi = true; // 스탯 UI 갱신
    [SerializeField] private TextMeshProUGUI segmentWeaponStatText; // 스탯 표시용 TMP 1개
    [SerializeField] private SegmentWeaponStatViewTarget segmentWeaponStatViewTarget = SegmentWeaponStatViewTarget.Cannon; // 초기 표시 세그먼트

    // 안건준 추가 - 0622 ======
    [Header("자동 카드 선택 (자동궤도 모드 연동)")]
    [Tooltip("켜면 자동궤도(AutoOrbit) 중 카드 선택지가 열릴 때 자동으로 1장을 선택합니다")]
    [SerializeField] private bool autoSelectInAutoOrbit = true; // 자동궤도 중 자동선택 활성화
    [Tooltip("카드가 펼쳐진 뒤 자동선택까지 대기 시간(초)")]
    [Min(0f)]
    [SerializeField] private float autoSelectDelay = 1f; // 자동선택 대기 시간
    private Coroutine autoSelectRoutine; // 자동선택 코루틴 참조
    // 안건준 추가 - 0622 ======

    // 안건준 추가 - 0622 ======
    [Header("세그먼트 리스트 호버 UI")]
    [Tooltip("카드 패널이 열릴 때 함께 활성화되는 트리거 바 (Hierarchy의 Segment List Popup)")]
    [SerializeField] private GameObject segmentListPopup; // 호버 트리거
    [Tooltip("Popup 호버 시 표시되는 세그먼트 목록 (Hierarchy의 Segment List)")]
    [SerializeField] private GameObject segmentList; // 호버 시 표시
    [Tooltip("Segment List 안 Scroll View 텍스트 — 장착 세그먼트 이름 : 개수 표시")]
    [SerializeField] private TextMeshProUGUI segmentListText; // 장착 세그먼트 이름:개수 TMP
    [Tooltip("Segment List > Viewport > Content RectTransform — 스크롤 높이 자동 조정용")]
    [SerializeField] private RectTransform segmentListContent; // 스크롤 Content RT
    // 안건준 추가 - 0622 ======

    [Header("마법책 리롤 UI")]
    [SerializeField] private GameObject rerollUiRoot; // 씬에 배치된 리롤 UI 루트
    [SerializeField] private Button rerollButton; // 정사각형 리롤 버튼
    [SerializeField] private Image rerollButtonImage; // 버튼 배경 이미지
    [SerializeField] private Sprite rerollButtonActiveSprite; // 리롤 가능 상태 이미지
    [SerializeField] private Sprite rerollButtonDisabledSprite; // 리롤 불가 상태 이미지
    [SerializeField] private TextMeshProUGUI rerollCountText; // 남은 리롤 횟수

    //전찬우 수정-0622
    private readonly List<SpawnedCardEntry> spawnedCards = new List<SpawnedCardEntry>(); // 생성된 카드 목록
    private readonly Dictionary<string, int> rerollCountsBySegmentId = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase); // 마법책 개수 집계용
    private readonly Dictionary<string, int> cardTooltipSegmentCountsById = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase); // 카드 툴팁용 보유 세그먼트 집계
    private const string MagicBookRerollSegmentId = "SG55_MagicBook"; // 마법책 세그먼트 ID
    private const int OwnedSegmentChoiceGuaranteeExcludedLevel = 3; // Lv3은 보유 확정 후보에서 제외
    private const int MaxSupportSegmentChoiceCount = 1; // 세그먼트 선택 3장 안 지원형 최대 수
    private bool spawnedForCurrentOpen; // 이번 패널 오픈에서 생성 완료 여부
    private bool isProcessingSelection; // 선택 처리 중
    private bool rerollAllowedForCurrentChoices; // 현재 카드 묶음 리롤 가능 여부
    private int remainingRerollCount; // 이번 카드 선택창 남은 리롤
    private CardPanelMode activePanelMode = CardPanelMode.LevelUp; // 현재 카드 패널 모드
    private int segmentTicketChoicesRemaining; // 선택권으로 남은 세그먼트 선택 횟수
    private int pendingRewardExperience; // 보상 선택 후 닫힘 완료 시 지급
    private int pendingRewardGold; // 보상 선택 후 닫힘 완료 시 지급
    private int pendingRewardSegmentTicketCount; // 보상 선택 후 이어서 열 선택권 수
    private float pendingRewardRareChanceBonusPercent; // 상자 등급이 보상 카드 레어 확률에 더하는 값
    private float pendingRewardUniqueChanceBonusPercent; // 상자 등급이 보상 카드 유니크 확률에 더하는 값
    private static bool loggedWeaponEnhancementInitial; // 무기 강화 초기 디버그 1회
    private LevelUpCardPhase currentSpawnPhase = LevelUpCardPhase.Upgrade; // 이번 레벨업 카드 종류
    private string selectedSegmentWeaponStatId; // 카드 선택으로 갱신되는 디버그 표시 대상
    private CoreStatProvider segmentWeaponStatSubscribedCore; // 스탯 변경 구독 대상
    private Coroutine hideSegmentListCoroutine; // 안건준 추가 - 0622 — 코루틴 참조 (혹시 중복 방지용)
    private CardUiPrefabReferences cachedPrefabReferences; // Resources fallback 캐시
    private Sprite cachedTierFrameNormalSprite; // 일반 등급 카드 프레임
    private Sprite cachedTierFrameRareSprite; // 레어 등급 카드 프레임
    private Sprite cachedTierFrameUniqueSprite; // 유니크 등급 카드 프레임

    public bool AutoSelectInAutoOrbit => autoSelectInAutoOrbit; // HUD 토글 표시용

    private void Awake()
    {
        LoadAutoSelectInAutoOrbitPreference(); // 자동궤도 카드자동 설정 복원
        ResolveManagerReferences(); // 참조 보강
        SetupSegmentListHoverUi(); // 안건준 추가 - 0622 — 호버 브릿지 연결 + 기본 비활성
        SetupRerollUi(); // 마법책 리롤 버튼 연결

        // TMP 줄바꿈 재귀 오류 방지 — 긴 텍스트가 들어가는 TMP에 word wrap 비활성
        if (segmentListText       != null) segmentListText.textWrappingMode         = TextWrappingModes.NoWrap;
        if (segmentWeaponStatText != null) segmentWeaponStatText.textWrappingMode   = TextWrappingModes.NoWrap;
        if (rerollCountText       != null) rerollCountText.textWrappingMode         = TextWrappingModes.NoWrap;

        // 안건준 추가 - 0624 : CardSoundManager 자동 연결 (없으면 자동 생성)
        if (cardSound == null)
            cardSound = GetComponent<CardSoundManager>();
        if (cardSound == null)
            cardSound = FindFirstObjectByType<CardSoundManager>();
        if (cardSound == null)
            cardSound = gameObject.AddComponent<CardSoundManager>();

        // 안건준 추가 - 0623 : 같은 오브젝트에 CardEffect가 있으면 자동 연결
        if (cardEffect == null)
        {
            cardEffect = GetComponent<CardEffect>();
            if (cardEffect != null)
            {
                Debug.Log("[CardUI] CardEffect 자동 연결 완료", this);
            }
        }
    }

    private void Start()
    {
        LogWeaponEnhancementInitialOnce(); // 시작 시 무기 강화 초기값 1회 출력
        TrySubscribeSegmentCountDebug(); // [임시] 세그먼트 추가/제거 시 디버그
        TrySubscribeSegmentWeaponStatDebug(); // 코어 스탯 변경 시 디버그 갱신
        RefreshSegmentWeaponStatUi(); // 건춘추가 - 0621 ====== 세그먼트 스탯 TMP 초기 표시
    }

    private void OnDestroy()
    {
        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveListener(HandleRerollButtonClicked); // 리스너 정리
        }

        UnsubscribeSegmentCountDebug(); // [임시] 구독 해제
        UnsubscribeSegmentWeaponStatDebug(); // 스탯 변경 구독 해제
    }

    // 건춘추가 - 0621 ======
    private void OnValidate() // Inspector에서 세그먼트 변경 시 플레이 중 미리보기
    {
        if (Application.isPlaying)
        {
            RefreshSegmentWeaponStatUi();
            RefreshRerollUi();
        }
    }
    // 건춘추가 - 0621 ======

    private void Update()
    {
        TrySubscribeSegmentCountDebug(); // Convoy 연결 늦을 때 재시도
        TrySubscribeSegmentWeaponStatDebug(); // Core 연결 늦을 때 재시도
        bool panelOpen = IsLevelUpPanelOpen(); // 패널 열림 여부
        if (panelOpen && !spawnedForCurrentOpen)
        {
            BeginRerollForPanelOpen(); // 마법책 개수만큼 이번 선택창 리롤 충전
            SpawnLevelUpCards(); // 현재 레벨 구간에 맞는 카드 생성
            spawnedForCurrentOpen = true;
            ShowSegmentListPopupOnPanelOpen(); // 안건준 추가 - 0622 — 트리거 바만 표시
            return;
        }

        if (panelOpen)
        {
            HandleCardChoiceKeyboardShortcuts(); // 카드 선택 단축키
        }

        if (!panelOpen && spawnedForCurrentOpen)
        {
            CardPanelMode closingMode = activePanelMode; // 수동 닫힘 처리용
            ClearSpawnedCards(); // 패널 닫힘 → 카드 정리
            spawnedForCurrentOpen = false;
            isProcessingSelection = false;
            currentSpawnPhase = LevelUpCardPhase.Upgrade; // 다음 오픈 시 재계산
            remainingRerollCount = 0; // 패널 닫힘 → 리롤 소멸
            rerollAllowedForCurrentChoices = false; // 다음 오픈 전까지 비활성
            RefreshRerollUi(); // 버튼 숨김/비활성 갱신
            HideSegmentListUi(); // 안건준 추가 - 0622 — 팝업·리스트 모두 숨김
            StopAutoSelect(); // 안건준 추가 - 0622 : 패널 닫힐 때 자동선택 코루틴 정리
            if (closingMode == CardPanelMode.LevelUp
                && CoreStatProvider.Active != null
                && CoreStatProvider.Active.IsLevelUpChoicePending)
            {
                CoreStatProvider.Active.CancelLevelUpChoice(); // 선택 없이 닫힘 → 경험치 유지
            }

            if (closingMode != CardPanelMode.LevelUp)
            {
                ResetSpecialCardMode(); // 보상/선택권 수동 닫힘 정리
            }
        }
    }

    public void PlayLevelUpTween()
    {
        TryOpenLevelUpPanel(); // 기존 외부 호출 호환
    }

    public bool CanOpenLevelUpPanel() // 경험치 레벨업 패널 오픈 가능 여부
    {
        ResolveManagerReferences(); // LevelUpUi/CanvasGroup 참조 보강
        if (ResolveLevelUpUi() == null || levelUpPanelCanvasGroup == null)
        {
            return false; // 카드 생성 감지에 필요한 참조 없음
        }

        if (IsLevelUpPanelOpen() || isProcessingSelection)
        {
            return false; // 이미 카드 패널이 열렸거나 선택 처리 중
        }

        if (activePanelMode != CardPanelMode.LevelUp)
        {
            return false; // 보상/선택권 흐름 완료 전에는 레벨업 대기
        }

        return segmentTicketChoicesRemaining <= 0
            && pendingRewardExperience <= 0
            && pendingRewardGold <= 0
            && pendingRewardSegmentTicketCount <= 0; // 특수 카드 잔여 처리 없음
    }

    public bool TryOpenLevelUpPanel() // 경험치 레벨업 패널 오픈
    {
        if (!CanOpenLevelUpPanel())
        {
            return false;
        }

        activePanelMode = CardPanelMode.LevelUp; // 일반 경험치 레벨업 모드
        segmentTicketChoicesRemaining = 0;
        pendingRewardExperience = 0;
        pendingRewardGold = 0;
        pendingRewardSegmentTicketCount = 0;
        ClearRewardChoiceTierChanceBonus();
        spawnedForCurrentOpen = false; // 새 카드 묶음 생성 허용
        isProcessingSelection = false;
        currentSpawnPhase = LevelUpCardPhase.Upgrade;
        remainingRerollCount = 0;
        rerollAllowedForCurrentChoices = false;
        RefreshRerollUi();

        LevelUpUi ui = ResolveLevelUpUi();
        ui.SetUseRewardTitle(false);
        ui.SetUseBackgroundBlur(false);

        // 안건준 추가 - 0622 : 자동궤도 모드이고 자동선택이 켜져 있으면 일시정지 없이 열기
        if (autoSelectInAutoOrbit && IsAutoOrbitActive())
        {
            ui.OpenWithoutPause();
        }
        else
        {
            ui.Open();
        }

        bool opened = IsLevelUpPanelOpen();
        if (!opened)
        {
            Debug.LogWarning("[CardUI] 레벨업 패널 오픈에 실패했습니다. LevelUpUi/CanvasGroup 연결을 확인하세요.", this);
        }

        return opened;
    }

    public void ToggleAutoSelectInAutoOrbit()
    {
        SetAutoSelectInAutoOrbit(!autoSelectInAutoOrbit, true); // HUD 버튼 토글
    }

    public void SetAutoSelectInAutoOrbit(bool enabled)
    {
        SetAutoSelectInAutoOrbit(enabled, true); // 외부 설정 진입점
    }

    public void NotifyAutoOrbitActiveChanged(bool active)
    {
        if (!active)
        {
            StopAutoSelect(); // 자동궤도 종료 즉시 예약 선택 취소
            return;
        }

        TryRestartAutoSelectForCurrentPanel(); // 자동궤도 진입 중 열린 카드가 있으면 재시도
    }

    private void SetAutoSelectInAutoOrbit(bool enabled, bool save)
    {
        if (autoSelectInAutoOrbit == enabled)
        {
            return; // 변경 없음
        }

        autoSelectInAutoOrbit = enabled;
        if (!autoSelectInAutoOrbit)
        {
            StopAutoSelect(); // OFF 즉시 예약 선택 취소
        }
        else
        {
            TryRestartAutoSelectForCurrentPanel(); // 열린 레벨업 카드가 있으면 바로 예약
        }

        if (save)
        {
            PlayerPrefs.SetInt(AutoSelectInAutoOrbitPrefsKey, autoSelectInAutoOrbit ? 1 : 0);
            PlayerPrefs.Save();
        }

    }

    private void LoadAutoSelectInAutoOrbitPreference()
    {
        if (!PlayerPrefs.HasKey(AutoSelectInAutoOrbitPrefsKey))
        {
            return; // Inspector 기본값 유지
        }

        autoSelectInAutoOrbit = PlayerPrefs.GetInt(AutoSelectInAutoOrbitPrefsKey, autoSelectInAutoOrbit ? 1 : 0) != 0;
    }

    public bool OpenRewardChoice() // 상자 등 외부 보상 선택 진입점
    {
        return OpenRewardChoice(0.0f, 0.0f);
    }

    public bool OpenRewardChoice(float rareChanceBonusPercent, float uniqueChanceBonusPercent) // 등급 상자용 보상 선택 진입점
    {
        pendingRewardRareChanceBonusPercent = Mathf.Clamp(rareChanceBonusPercent, 0.0f, 100.0f);
        pendingRewardUniqueChanceBonusPercent = Mathf.Clamp(uniqueChanceBonusPercent, 0.0f, 100.0f);

        bool opened = OpenSpecialCardPanel(CardPanelMode.RewardChoice, 0);
        if (!opened)
        {
            ClearRewardChoiceTierChanceBonus(); // 열기 실패 시 다음 보상에 영향 방지
        }

        return opened;
    }

    public bool OpenSegmentChoiceTicket(int ticketCount) // 월드드랍/보상카드 선택권 진입점
    {
        int safeCount = Mathf.Max(0, ticketCount);
        if (safeCount <= 0)
        {
            return false;
        }

        ClearRewardChoiceTierChanceBonus(); // 선택권 직접 진입은 상자 보너스 없음
        return OpenSpecialCardPanel(CardPanelMode.SegmentTicketChoice, safeCount);
    }

    private bool OpenSpecialCardPanel(CardPanelMode mode, int ticketCount)
    {
        if (IsLevelUpPanelOpen() || isProcessingSelection)
        {
            return false; // 이미 카드 선택 중
        }

        LevelUpUi ui = ResolveLevelUpUi();
        if (ui == null)
        {
            Debug.LogWarning("[CardUI] LevelUpUi가 없어 카드 패널을 열 수 없습니다.", this);
            return false;
        }

        ClearSpawnedCards();
        activePanelMode = mode;
        segmentTicketChoicesRemaining = mode == CardPanelMode.SegmentTicketChoice ? Mathf.Max(1, ticketCount) : 0;
        pendingRewardExperience = 0;
        pendingRewardGold = 0;
        pendingRewardSegmentTicketCount = 0;
        spawnedForCurrentOpen = false;
        isProcessingSelection = false;
        currentSpawnPhase = LevelUpCardPhase.Upgrade;
        ui.SetUseRewardTitle(mode != CardPanelMode.LevelUp); // 보상/선택권은 보상획득 타이틀
        ui.SetUseBackgroundBlur(mode != CardPanelMode.LevelUp); // 보상/선택권은 배경 블러 사용
        ui.Open();
        return true;
    }

    private bool IsAutoOrbitActive()
    {
        TeamProject01.Gameplay.ConvoyController convoy =
            FindFirstObjectByType<TeamProject01.Gameplay.ConvoyController>();
        return convoy != null && convoy.IsAutoOrbitActive;
    }

    private void RefreshSegmentWeaponStatUi() // 선택 세그먼트 스탯 TMP 갱신
    {
        if (!showSegmentWeaponStatUi || segmentWeaponStatText == null)
        {
            return; // UI 비활성 또는 TMP 없음
        }

        CoreStatProvider core = CoreStatProvider.Active;
        string segmentId = ResolveSegmentWeaponStatDebugTargetId();
        segmentWeaponStatText.text = TryBuildSegmentWeaponStatDebugContext(core, segmentId, out SegmentWeaponStatDebugContext context)
            ? FormatSegmentWeaponStatDebugText(context)
            : "Core 없음";
    }

    //전찬우 수정-0622
    public void SelectSegmentWeaponStatDebugContext(string segmentId) // 디버그 UI에서 직접 세그먼트 컨텍스트 선택
    {
        SetSegmentWeaponStatDebugTarget(segmentId);
    }

    public string GetSelectedSegmentWeaponStatDebugContextId() // 디버그 UI 표시용 현재 컨텍스트
    {
        return ResolveSegmentWeaponStatDebugTargetId();
    }

    private void SetSegmentWeaponStatDebugTarget(string segmentId) // 선택 흐름에서 현재 표시 대상 변경
    {
        if (string.IsNullOrWhiteSpace(segmentId))
        {
            return; // 대상 없음
        }

        selectedSegmentWeaponStatId = segmentId.Trim();
        RefreshSegmentWeaponStatUi();
    }

    private string ResolveSegmentWeaponStatDebugTargetId()
    {
        return string.IsNullOrWhiteSpace(selectedSegmentWeaponStatId)
            ? ResolveSegmentWeaponStatViewId(segmentWeaponStatViewTarget)
            : selectedSegmentWeaponStatId.Trim();
    }

    private static string ResolveSegmentWeaponStatViewId(SegmentWeaponStatViewTarget target) // 열거형 → SegmentId
    {
        switch (target)
        {
            case SegmentWeaponStatViewTarget.Missile:
                return "SG02_Missile";
            case SegmentWeaponStatViewTarget.Trebuchet:
                return "SG03_Trebuchet";
            case SegmentWeaponStatViewTarget.SawLauncher:
                return "SG04_SawLauncher";
            case SegmentWeaponStatViewTarget.Flamethrower:
                return "SG05_Flamethrower";
            case SegmentWeaponStatViewTarget.LightningObelisk:
                return "SG20_LightningObelisk";
            case SegmentWeaponStatViewTarget.FireballTower:
                return "SG21_FireballTower";
            default:
                return "SG01_Cannon";
        }
    }

    private bool TryBuildSegmentWeaponStatDebugContext(CoreStatProvider core, string segmentId, out SegmentWeaponStatDebugContext context)
    {
        context = default;
        if (core == null || string.IsNullOrWhiteSpace(segmentId))
        {
            return false; // 조회 불가
        }

        string normalizedId = segmentId.Trim();
        string title = ResolveSegmentStatDisplayTitle(core, normalizedId);
        int level = 1;
        if (core.TryGetSegmentModelLevelInfo(normalizedId, out int currentLevel, out _))
        {
            level = currentLevel;
        }

        WeaponStatBonusData bonus = core.GetWeaponStatBonus(normalizedId);
        TryGetSegmentAttackProfile(core, normalizedId, out SegmentAttackProfile profile);
        SegmentWeaponStatDisplayFlags flags = ResolveSegmentWeaponStatDisplayFlags(normalizedId, profile, bonus);
        context = new SegmentWeaponStatDebugContext(normalizedId, title, level, profile, bonus, flags);
        return true;
    }

    private string FormatSegmentWeaponStatDebugText(SegmentWeaponStatDebugContext context)
    {
        StringBuilder sb = new StringBuilder(384);
        sb.Append('[').Append(context.Title).Append(" Lv").Append(context.Level).Append(']').AppendLine();

        if (!context.HasProfile)
        {
            sb.AppendLine("(프로필 없음)");
            if (!AppendCumulativeBonusLines(sb, context.Bonus))
            {
                sb.AppendLine("강화 없음");
            }

            return sb.ToString().TrimEnd();
        }

        AppendSegmentWeaponStatLines(sb, context.Profile, context.Bonus, context.DisplayFlags);
        return sb.ToString().TrimEnd();
    }

    private SegmentWeaponStatDisplayFlags ResolveSegmentWeaponStatDisplayFlags(string segmentId, SegmentAttackProfile profile, WeaponStatBonusData bonus)
    {
        SegmentWeaponStatDisplayFlags flags = SegmentWeaponStatDisplayFlags.BaseDamage
            | SegmentWeaponStatDisplayFlags.SearchRange
            | SegmentWeaponStatDisplayFlags.Cooldown; // 공통 핵심값

        AddWeaponEnhancementDisplayFlags(segmentId, ref flags);
        AddProfileImportantDisplayFlags(profile, ref flags);
        AddBonusDisplayFlags(bonus, ref flags);
        return flags;
    }

    private void AddWeaponEnhancementDisplayFlags(string segmentId, ref SegmentWeaponStatDisplayFlags flags)
    {
        WeaponCatalogAsset catalog = ResolveWeaponCatalog();
        if (catalog == null
            || string.IsNullOrWhiteSpace(segmentId)
            || !catalog.TryGetEnhancementsForSegment(segmentId, out WeaponDefinition[] enhancements)
            || enhancements == null)
        {
            return; // 카탈로그 없음
        }

        for (int i = 0; i < enhancements.Length; i++)
        {
            AddWeaponDefinitionDisplayFlags(enhancements[i], ref flags);
        }
    }

    private static void AddWeaponDefinitionDisplayFlags(WeaponDefinition definition, ref SegmentWeaponStatDisplayFlags flags)
    {
        if (definition == null)
        {
            return; // 정의 없음
        }

        if (HasAnyTierValue(definition.GetBaseDamage(StatUpgrade.StatCardTier.Normal), definition.GetBaseDamage(StatUpgrade.StatCardTier.Rare), definition.GetBaseDamage(StatUpgrade.StatCardTier.Unique))
            || HasAnyTierValue(definition.GetBaseDamagePercent(StatUpgrade.StatCardTier.Normal), definition.GetBaseDamagePercent(StatUpgrade.StatCardTier.Rare), definition.GetBaseDamagePercent(StatUpgrade.StatCardTier.Unique)))
        {
            flags |= SegmentWeaponStatDisplayFlags.BaseDamage;
        }

        if (HasAnyTierValue(definition.GetProjectileSpeed(StatUpgrade.StatCardTier.Normal), definition.GetProjectileSpeed(StatUpgrade.StatCardTier.Rare), definition.GetProjectileSpeed(StatUpgrade.StatCardTier.Unique))
            || HasAnyTierValue(definition.GetProjectileSpeedPercent(StatUpgrade.StatCardTier.Normal), definition.GetProjectileSpeedPercent(StatUpgrade.StatCardTier.Rare), definition.GetProjectileSpeedPercent(StatUpgrade.StatCardTier.Unique)))
        {
            flags |= SegmentWeaponStatDisplayFlags.ProjectileSpeed;
        }

        if (HasAnyTierValue(definition.GetSearchRange(StatUpgrade.StatCardTier.Normal), definition.GetSearchRange(StatUpgrade.StatCardTier.Rare), definition.GetSearchRange(StatUpgrade.StatCardTier.Unique))
            || HasAnyTierValue(definition.GetSearchRangePercent(StatUpgrade.StatCardTier.Normal), definition.GetSearchRangePercent(StatUpgrade.StatCardTier.Rare), definition.GetSearchRangePercent(StatUpgrade.StatCardTier.Unique)))
        {
            flags |= SegmentWeaponStatDisplayFlags.SearchRange;
        }

        if (HasAnyTierValue(definition.GetMaxChainDepth(StatUpgrade.StatCardTier.Normal), definition.GetMaxChainDepth(StatUpgrade.StatCardTier.Rare), definition.GetMaxChainDepth(StatUpgrade.StatCardTier.Unique)))
        {
            flags |= SegmentWeaponStatDisplayFlags.MaxChainDepth;
        }

        if (HasAnyTierValue(definition.GetChainRange(StatUpgrade.StatCardTier.Normal), definition.GetChainRange(StatUpgrade.StatCardTier.Rare), definition.GetChainRange(StatUpgrade.StatCardTier.Unique))
            || HasAnyTierValue(definition.GetChainRangePercent(StatUpgrade.StatCardTier.Normal), definition.GetChainRangePercent(StatUpgrade.StatCardTier.Rare), definition.GetChainRangePercent(StatUpgrade.StatCardTier.Unique)))
        {
            flags |= SegmentWeaponStatDisplayFlags.ChainRange;
        }

        if (HasAnyTierValue(definition.GetChainDamageFalloff(StatUpgrade.StatCardTier.Normal), definition.GetChainDamageFalloff(StatUpgrade.StatCardTier.Rare), definition.GetChainDamageFalloff(StatUpgrade.StatCardTier.Unique)))
        {
            flags |= SegmentWeaponStatDisplayFlags.ChainDamageFalloff;
        }

        if (HasAnyTierValue(definition.GetProjectileCount(StatUpgrade.StatCardTier.Normal), definition.GetProjectileCount(StatUpgrade.StatCardTier.Rare), definition.GetProjectileCount(StatUpgrade.StatCardTier.Unique)))
        {
            flags |= SegmentWeaponStatDisplayFlags.ProjectileCount;
        }

        if (HasAnyTierValue(definition.GetCooldownReduction(StatUpgrade.StatCardTier.Normal), definition.GetCooldownReduction(StatUpgrade.StatCardTier.Rare), definition.GetCooldownReduction(StatUpgrade.StatCardTier.Unique)))
        {
            flags |= SegmentWeaponStatDisplayFlags.Cooldown;
        }

        if (HasAnyTierValue(definition.GetSideConeAngle(StatUpgrade.StatCardTier.Normal), definition.GetSideConeAngle(StatUpgrade.StatCardTier.Rare), definition.GetSideConeAngle(StatUpgrade.StatCardTier.Unique)))
        {
            flags |= SegmentWeaponStatDisplayFlags.SideConeAngle;
        }

        if (HasAnyTierValue(definition.GetLaserDuration(StatUpgrade.StatCardTier.Normal), definition.GetLaserDuration(StatUpgrade.StatCardTier.Rare), definition.GetLaserDuration(StatUpgrade.StatCardTier.Unique))
            || HasAnyTierValue(definition.GetLaserDurationPercent(StatUpgrade.StatCardTier.Normal), definition.GetLaserDurationPercent(StatUpgrade.StatCardTier.Rare), definition.GetLaserDurationPercent(StatUpgrade.StatCardTier.Unique)))
        {
            flags |= SegmentWeaponStatDisplayFlags.LaserDuration;
        }

        if (HasAnyTierValue(definition.GetLaserTickInterval(StatUpgrade.StatCardTier.Normal), definition.GetLaserTickInterval(StatUpgrade.StatCardTier.Rare), definition.GetLaserTickInterval(StatUpgrade.StatCardTier.Unique)))
        {
            flags |= SegmentWeaponStatDisplayFlags.LaserTickInterval;
        }

        if (HasAnyTierValue(definition.GetLandingRollDistance(StatUpgrade.StatCardTier.Normal), definition.GetLandingRollDistance(StatUpgrade.StatCardTier.Rare), definition.GetLandingRollDistance(StatUpgrade.StatCardTier.Unique))
            || HasAnyTierValue(definition.GetLandingRollDistancePercent(StatUpgrade.StatCardTier.Normal), definition.GetLandingRollDistancePercent(StatUpgrade.StatCardTier.Rare), definition.GetLandingRollDistancePercent(StatUpgrade.StatCardTier.Unique)))
        {
            flags |= SegmentWeaponStatDisplayFlags.LandingRollDistance;
        }

        if (HasAnyTierValue(definition.GetLandingRollDuration(StatUpgrade.StatCardTier.Normal), definition.GetLandingRollDuration(StatUpgrade.StatCardTier.Rare), definition.GetLandingRollDuration(StatUpgrade.StatCardTier.Unique))
            || HasAnyTierValue(definition.GetLandingRollDurationPercent(StatUpgrade.StatCardTier.Normal), definition.GetLandingRollDurationPercent(StatUpgrade.StatCardTier.Rare), definition.GetLandingRollDurationPercent(StatUpgrade.StatCardTier.Unique)))
        {
            flags |= SegmentWeaponStatDisplayFlags.LandingRollDuration;
        }

        if (HasAnyTierValue(definition.GetPierceCount(StatUpgrade.StatCardTier.Normal), definition.GetPierceCount(StatUpgrade.StatCardTier.Rare), definition.GetPierceCount(StatUpgrade.StatCardTier.Unique)))
        {
            flags |= SegmentWeaponStatDisplayFlags.PierceCount;
        }

        if (HasAnyTierValue(definition.GetExplosionRadius(StatUpgrade.StatCardTier.Normal), definition.GetExplosionRadius(StatUpgrade.StatCardTier.Rare), definition.GetExplosionRadius(StatUpgrade.StatCardTier.Unique))
            || HasAnyTierValue(definition.GetExplosionRadiusPercent(StatUpgrade.StatCardTier.Normal), definition.GetExplosionRadiusPercent(StatUpgrade.StatCardTier.Rare), definition.GetExplosionRadiusPercent(StatUpgrade.StatCardTier.Unique)))
        {
            flags |= SegmentWeaponStatDisplayFlags.ExplosionRadius;
        }

        if (HasAnyTierValue(definition.GetSawPierceDamageRatio(StatUpgrade.StatCardTier.Normal), definition.GetSawPierceDamageRatio(StatUpgrade.StatCardTier.Rare), definition.GetSawPierceDamageRatio(StatUpgrade.StatCardTier.Unique)))
        {
            flags |= SegmentWeaponStatDisplayFlags.SawPierceDamageRatio;
        }
    }

    private static void AddProfileImportantDisplayFlags(SegmentAttackProfile profile, ref SegmentWeaponStatDisplayFlags flags)
    {
        if (profile == null)
        {
            return; // 프로필 없음
        }

        if (profile.MoveType != SegmentAttackMoveType.Laser && profile.MoveType != SegmentAttackMoveType.ChainLightning)
        {
            flags |= SegmentWeaponStatDisplayFlags.ProjectileSpeed;
        }

        if (profile.ProjectileCount > 1)
        {
            flags |= SegmentWeaponStatDisplayFlags.ProjectileCount;
        }

        if (profile.ImpactType == SegmentAttackImpactType.PierceDamage || profile.MoveType == SegmentAttackMoveType.PiercingProjectile)
        {
            flags |= SegmentWeaponStatDisplayFlags.PierceCount;
        }

        if (profile.ImpactType == SegmentAttackImpactType.ExplosionArea)
        {
            flags |= SegmentWeaponStatDisplayFlags.ExplosionRadius;
        }

        if (profile.AttackAreaMode == SegmentAttackAreaMode.SideCones)
        {
            flags |= SegmentWeaponStatDisplayFlags.SideConeAngle;
        }

        if (profile.MoveType == SegmentAttackMoveType.ChainLightning)
        {
            flags |= SegmentWeaponStatDisplayFlags.MaxChainDepth
                | SegmentWeaponStatDisplayFlags.ChainRange
                | SegmentWeaponStatDisplayFlags.ChainDamageFalloff;
        }

        if (profile.MoveType == SegmentAttackMoveType.SawBounceProjectile)
        {
            flags |= SegmentWeaponStatDisplayFlags.MaxChainDepth
                | SegmentWeaponStatDisplayFlags.ChainRange
                | SegmentWeaponStatDisplayFlags.SawPierceDamageRatio;
        }

        if (profile.MoveType == SegmentAttackMoveType.Laser || profile.MoveType == SegmentAttackMoveType.ExpandingFlameSphere)
        {
            flags |= SegmentWeaponStatDisplayFlags.LaserDuration
                | SegmentWeaponStatDisplayFlags.LaserTickInterval;
        }

        if (profile.RollAfterArcLanding || profile.LandingRollDistance > 0.0001f)
        {
            flags |= SegmentWeaponStatDisplayFlags.LandingRollDistance
                | SegmentWeaponStatDisplayFlags.LandingRollDuration;
        }
    }

    private static void AddBonusDisplayFlags(WeaponStatBonusData bonus, ref SegmentWeaponStatDisplayFlags flags)
    {
        if (bonus.BaseDamageBonus > 0.0001f || bonus.BaseDamagePercentMultiplier > 0.0001f) flags |= SegmentWeaponStatDisplayFlags.BaseDamage;
        if (bonus.ProjectileSpeedBonus > 0.0001f || bonus.ProjectileSpeedPercentMultiplier > 0.0001f) flags |= SegmentWeaponStatDisplayFlags.ProjectileSpeed;
        if (bonus.SearchRangeBonus > 0.0001f || bonus.SearchRangePercentMultiplier > 0.0001f) flags |= SegmentWeaponStatDisplayFlags.SearchRange;
        if (bonus.MaxChainDepthBonus != 0) flags |= SegmentWeaponStatDisplayFlags.MaxChainDepth;
        if (bonus.ChainRangeBonus > 0.0001f || bonus.ChainRangePercentMultiplier > 0.0001f) flags |= SegmentWeaponStatDisplayFlags.ChainRange;
        if (bonus.ChainDamageFalloffBonus > 0.0001f) flags |= SegmentWeaponStatDisplayFlags.ChainDamageFalloff;
        if (bonus.ProjectileCountBonus != 0) flags |= SegmentWeaponStatDisplayFlags.ProjectileCount;
        if (WeaponStatBonusData.ToReductionDisplayRate(bonus.CooldownReductionMultiplier) > 0.0001f) flags |= SegmentWeaponStatDisplayFlags.Cooldown;
        if (bonus.SideConeAngleBonus > 0.0001f) flags |= SegmentWeaponStatDisplayFlags.SideConeAngle;
        if (bonus.LaserDurationBonus > 0.0001f || bonus.LaserDurationPercentMultiplier > 0.0001f) flags |= SegmentWeaponStatDisplayFlags.LaserDuration;
        if (WeaponStatBonusData.ToReductionDisplayRate(bonus.LaserTickIntervalReductionMultiplier) > 0.0001f) flags |= SegmentWeaponStatDisplayFlags.LaserTickInterval;
        if (bonus.LandingRollDistanceBonus > 0.0001f || bonus.LandingRollDistancePercentMultiplier > 0.0001f) flags |= SegmentWeaponStatDisplayFlags.LandingRollDistance;
        if (bonus.LandingRollDurationBonus > 0.0001f || bonus.LandingRollDurationPercentMultiplier > 0.0001f) flags |= SegmentWeaponStatDisplayFlags.LandingRollDuration;
        if (bonus.PierceCountBonus != 0) flags |= SegmentWeaponStatDisplayFlags.PierceCount;
        if (bonus.ExplosionRadiusBonus > 0.0001f || bonus.ExplosionRadiusPercentMultiplier > 0.0001f) flags |= SegmentWeaponStatDisplayFlags.ExplosionRadius;
        if (bonus.SawPierceDamageRatioBonus > 0.0001f) flags |= SegmentWeaponStatDisplayFlags.SawPierceDamageRatio;
    }

    private static void AppendSegmentWeaponStatLines(StringBuilder sb, SegmentAttackProfile profile, WeaponStatBonusData bonus, SegmentWeaponStatDisplayFlags flags)
    {
        if (Includes(flags, SegmentWeaponStatDisplayFlags.BaseDamage))
        {
            AppendStatLineFloat(sb, "공격력", profile.BaseDamage, bonus.ResolveBaseDamage(profile.BaseDamage), bonus.BaseDamageBonus, WeaponStatBonusData.ToPercentDisplayRate(bonus.BaseDamagePercentMultiplier));
        }

        if (Includes(flags, SegmentWeaponStatDisplayFlags.Cooldown))
        {
            AppendCooldownStatLine(sb, profile.Cooldown, bonus);
        }

        if (Includes(flags, SegmentWeaponStatDisplayFlags.SearchRange))
        {
            AppendStatLineFloat(sb, "사거리", profile.SearchRange, bonus.ResolveSearchRange(profile.SearchRange), bonus.SearchRangeBonus, WeaponStatBonusData.ToPercentDisplayRate(bonus.SearchRangePercentMultiplier));
        }

        if (Includes(flags, SegmentWeaponStatDisplayFlags.ProjectileSpeed))
        {
            AppendStatLineFloat(sb, "투사체속도", profile.ProjectileSpeed, bonus.ResolveProjectileSpeed(profile.ProjectileSpeed), bonus.ProjectileSpeedBonus, WeaponStatBonusData.ToPercentDisplayRate(bonus.ProjectileSpeedPercentMultiplier));
        }

        if (Includes(flags, SegmentWeaponStatDisplayFlags.ProjectileCount))
        {
            AppendStatLineInt(sb, "발사수", profile.ProjectileCount, bonus.ResolveProjectileCount(profile.ProjectileCount), bonus.ProjectileCountBonus);
        }

        if (Includes(flags, SegmentWeaponStatDisplayFlags.PierceCount))
        {
            AppendStatLineInt(sb, "관통", profile.PierceCount, bonus.ResolvePierceCount(profile.PierceCount), bonus.PierceCountBonus);
        }

        if (Includes(flags, SegmentWeaponStatDisplayFlags.ExplosionRadius))
        {
            AppendStatLineFloat(sb, "폭발반경", profile.ExplosionRadius, bonus.ResolveExplosionRadius(profile.ExplosionRadius), bonus.ExplosionRadiusBonus, WeaponStatBonusData.ToPercentDisplayRate(bonus.ExplosionRadiusPercentMultiplier));
        }

        if (Includes(flags, SegmentWeaponStatDisplayFlags.MaxChainDepth))
        {
            AppendStatLineInt(sb, "연쇄단계", profile.MaxChainDepth, bonus.ResolveMaxChainDepth(profile.MaxChainDepth), bonus.MaxChainDepthBonus);
        }

        if (Includes(flags, SegmentWeaponStatDisplayFlags.ChainRange))
        {
            AppendStatLineFloat(sb, "연쇄거리", profile.ChainRange, bonus.ResolveChainRange(profile.ChainRange), bonus.ChainRangeBonus, WeaponStatBonusData.ToPercentDisplayRate(bonus.ChainRangePercentMultiplier));
        }

        if (Includes(flags, SegmentWeaponStatDisplayFlags.ChainDamageFalloff))
        {
            AppendStatLineFloat(sb, "체인감쇠율", profile.ChainDamageFalloff, bonus.ResolveChainDamageFalloff(profile.ChainDamageFalloff), bonus.ChainDamageFalloffBonus);
        }

        if (Includes(flags, SegmentWeaponStatDisplayFlags.SideConeAngle))
        {
            AppendStatLineFloat(sb, "부채꼴각", profile.SideConeAngle, bonus.ResolveSideConeAngle(profile.SideConeAngle), bonus.SideConeAngleBonus);
        }

        if (Includes(flags, SegmentWeaponStatDisplayFlags.LaserDuration))
        {
            AppendStatLineFloat(sb, "레이저지속", profile.LaserDuration, bonus.ResolveLaserDuration(profile.LaserDuration), bonus.LaserDurationBonus, WeaponStatBonusData.ToPercentDisplayRate(bonus.LaserDurationPercentMultiplier));
        }

        if (Includes(flags, SegmentWeaponStatDisplayFlags.LaserTickInterval))
        {
            AppendLaserTickIntervalStatLine(sb, profile.LaserTickInterval, bonus);
        }

        if (Includes(flags, SegmentWeaponStatDisplayFlags.LandingRollDistance))
        {
            AppendStatLineFloat(sb, "굴러거리", profile.LandingRollDistance, bonus.ResolveLandingRollDistance(profile.LandingRollDistance), bonus.LandingRollDistanceBonus, WeaponStatBonusData.ToPercentDisplayRate(bonus.LandingRollDistancePercentMultiplier));
        }

        if (Includes(flags, SegmentWeaponStatDisplayFlags.LandingRollDuration))
        {
            AppendStatLineFloat(sb, "굴러시간", profile.LandingRollDuration, bonus.ResolveLandingRollDuration(profile.LandingRollDuration), bonus.LandingRollDurationBonus, WeaponStatBonusData.ToPercentDisplayRate(bonus.LandingRollDurationPercentMultiplier));
        }

        if (Includes(flags, SegmentWeaponStatDisplayFlags.SawPierceDamageRatio))
        {
            AppendStatLineFloat(sb, "관통피해비율", profile.SawPierceDamageRatio, bonus.ResolveSawPierceDamageRatio(profile.SawPierceDamageRatio), bonus.SawPierceDamageRatioBonus);
        }
    }

    private static void AppendCooldownStatLine(StringBuilder sb, float baseCooldown, WeaponStatBonusData bonus)
    {
        float cooldown = bonus.ResolveCooldown(baseCooldown);
        float cooldownReduction = WeaponStatBonusData.ToReductionDisplayRate(bonus.CooldownReductionMultiplier);
        sb.Append("기준쿨타임: ").Append(cooldown.ToString("0.##")).Append("초");
        if (cooldownReduction > 0.0001f)
        {
            sb.Append(" (쿨-").Append((cooldownReduction * 100f).ToString("0.#")).Append("%)");
        }

        sb.Append(" (실전 ±10%)");
        sb.AppendLine();
    }

    private static void AppendLaserTickIntervalStatLine(StringBuilder sb, float baseTickInterval, WeaponStatBonusData bonus)
    {
        float tickInterval = bonus.ResolveLaserTickInterval(baseTickInterval);
        float tickReduction = WeaponStatBonusData.ToReductionDisplayRate(bonus.LaserTickIntervalReductionMultiplier);
        sb.Append("레이저틱: ").Append(tickInterval.ToString("0.##")).Append('초');
        if (tickReduction > 0.0001f)
        {
            sb.Append(" (틱-").Append((tickReduction * 100f).ToString("0.#")).Append("%)");
        }

        sb.AppendLine();
    }

    private static bool AppendCumulativeBonusLines(StringBuilder sb, WeaponStatBonusData bonus)
    {
        bool appended = false;
        appended |= AppendBonusFloatLine(sb, "공격력", bonus.BaseDamageBonus, WeaponStatBonusData.ToPercentDisplayRate(bonus.BaseDamagePercentMultiplier));
        appended |= AppendBonusFloatLine(sb, "투사체속도", bonus.ProjectileSpeedBonus, WeaponStatBonusData.ToPercentDisplayRate(bonus.ProjectileSpeedPercentMultiplier));
        appended |= AppendBonusFloatLine(sb, "사거리", bonus.SearchRangeBonus, WeaponStatBonusData.ToPercentDisplayRate(bonus.SearchRangePercentMultiplier));
        appended |= AppendBonusReductionLine(sb, "기준쿨타임", "쿨", WeaponStatBonusData.ToReductionDisplayRate(bonus.CooldownReductionMultiplier));
        appended |= AppendBonusIntLine(sb, "발사수", bonus.ProjectileCountBonus);
        appended |= AppendBonusIntLine(sb, "관통", bonus.PierceCountBonus);
        appended |= AppendBonusFloatLine(sb, "폭발반경", bonus.ExplosionRadiusBonus, WeaponStatBonusData.ToPercentDisplayRate(bonus.ExplosionRadiusPercentMultiplier));
        appended |= AppendBonusIntLine(sb, "연쇄단계", bonus.MaxChainDepthBonus);
        appended |= AppendBonusFloatLine(sb, "연쇄거리", bonus.ChainRangeBonus, WeaponStatBonusData.ToPercentDisplayRate(bonus.ChainRangePercentMultiplier));
        appended |= AppendBonusFloatLine(sb, "체인감쇠율", bonus.ChainDamageFalloffBonus);
        appended |= AppendBonusFloatLine(sb, "부채꼴각", bonus.SideConeAngleBonus);
        appended |= AppendBonusFloatLine(sb, "레이저지속", bonus.LaserDurationBonus, WeaponStatBonusData.ToPercentDisplayRate(bonus.LaserDurationPercentMultiplier));
        appended |= AppendBonusReductionLine(sb, "레이저틱", "틱", WeaponStatBonusData.ToReductionDisplayRate(bonus.LaserTickIntervalReductionMultiplier));
        appended |= AppendBonusFloatLine(sb, "굴러거리", bonus.LandingRollDistanceBonus, WeaponStatBonusData.ToPercentDisplayRate(bonus.LandingRollDistancePercentMultiplier));
        appended |= AppendBonusFloatLine(sb, "굴러시간", bonus.LandingRollDurationBonus, WeaponStatBonusData.ToPercentDisplayRate(bonus.LandingRollDurationPercentMultiplier));
        appended |= AppendBonusFloatLine(sb, "관통피해비율", bonus.SawPierceDamageRatioBonus);
        return appended;
    }

    private static bool AppendBonusFloatLine(StringBuilder sb, string label, float flatBonus, float percentBonus = 0f)
    {
        bool hasFlat = Mathf.Abs(flatBonus) > 0.0001f;
        bool hasPercent = percentBonus > 0.0001f;
        if (!hasFlat && !hasPercent)
        {
            return false;
        }

        sb.Append(label).Append(": ");
        if (hasFlat)
        {
            sb.Append('+').Append(flatBonus.ToString("0.##"));
        }

        if (hasFlat && hasPercent)
        {
            sb.Append(", ");
        }

        if (hasPercent)
        {
            sb.Append('+').Append((percentBonus * 100f).ToString("0.#")).Append('%');
        }

        sb.AppendLine();
        return true;
    }

    private static bool AppendBonusIntLine(StringBuilder sb, string label, int bonus)
    {
        if (bonus == 0)
        {
            return false;
        }

        sb.Append(label).Append(": +").Append(bonus).AppendLine();
        return true;
    }

    private static bool AppendBonusReductionLine(StringBuilder sb, string label, string prefix, float reductionRate)
    {
        if (reductionRate <= 0.0001f)
        {
            return false;
        }

        sb.Append(label).Append(": (").Append(prefix).Append('-').Append((reductionRate * 100f).ToString("0.#")).Append("%)").AppendLine();
        return true;
    }

    private static string ResolveSegmentStatDisplayTitle(CoreStatProvider core, string segmentId)
    {
        if (core != null && core.TryFindSegmentEntry(segmentId, out SegmentCatalogEntry catalogEntry))
        {
            if (!string.IsNullOrWhiteSpace(catalogEntry.DisplayName))
            {
                return catalogEntry.DisplayName.Trim();
            }
        }

        return segmentId;
    }

    private static bool HasAnyTierValue(float normal, float rare, float unique)
    {
        return Mathf.Abs(normal) > 0.0001f || Mathf.Abs(rare) > 0.0001f || Mathf.Abs(unique) > 0.0001f;
    }

    private static bool HasAnyTierValue(int normal, int rare, int unique)
    {
        return normal != 0 || rare != 0 || unique != 0;
    }

    private static string BuildTierPrefixedWeaponDescription(WeaponDefinition definition, StatUpgrade.StatCardTier tier)
    {
        if (definition == null)
        {
            return string.Empty;
        }

        string description = string.IsNullOrWhiteSpace(definition.Description)
            ? definition.NormalizedId
            : definition.Description;
        if (string.IsNullOrWhiteSpace(description) || !description.Contains(DescriptionValueToken))
        {
            description = BuildDefaultWeaponDescription(definition, tier);
        }

        description = ApplyUniqueDamageAmplifyWord(description, definition, tier);
        return BuildWeaponDescriptionWithValue(description, definition, tier);
    }

    private static void ApplyStatUpgradeCardPresentation(GameObject root, StatUpgrade statUpgrade, StatUpgrade.StatCardTier tier)
    {
        if (root == null || statUpgrade == null)
        {
            return;
        }

        string title = BuildStatUpgradeCardTitle(statUpgrade);
        string description = BuildTierPrefixedStatDescription(statUpgrade, tier);
        if (!string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(description))
        {
            ApplyStatUpgradeCardTextsDirectly(root, title, description);
            return;
        }

        ApplyTierPrefixToCardDescription(root, tier, isReduction: false);
    }

    private static string BuildStatUpgradeCardTitle(StatUpgrade statUpgrade)
    {
        if (statUpgrade == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(statUpgrade.DisplayName))
        {
            return statUpgrade.DisplayName;
        }

        if (Mathf.Abs(statUpgrade.DamageMultiplierBonus) > 0.0001f) return "모든 무기 공격력 증가";
        if (Mathf.Abs(statUpgrade.MeleeDamageMultiplierBonus) > 0.0001f) return "모든 밀리 공격력 증가";
        if (Mathf.Abs(statUpgrade.MagicDamageMultiplierBonus) > 0.0001f) return "모든 마법 공격력 증가";
        if (Mathf.Abs(statUpgrade.AttackSpeedMultiplierBonus) > 0.0001f) return "쿨타임 감소";
        if (Mathf.Abs(statUpgrade.TurnSpeedBonus) > 0.0001f) return "핸들링 강화";
        if (Mathf.Abs(statUpgrade.CollisionForceBonus) > 0.0001f) return "충돌힘 강화";
        if (Mathf.Abs(statUpgrade.RejoinRangeBonus) > 0.0001f) return "재결합 범위 강화";
        if (Mathf.Abs(statUpgrade.NexusHealthBonus) > 0.0001f) return "넥서스 체력 강화";
        return string.Empty;
    }

    private static string BuildTierPrefixedStatDescription(StatUpgrade statUpgrade, StatUpgrade.StatCardTier tier)
    {
        if (statUpgrade == null)
        {
            return string.Empty;
        }

        string description = statUpgrade.Description;
        if (string.IsNullOrWhiteSpace(description) || !description.Contains(DescriptionValueToken))
        {
            description = BuildDefaultStatDescription(statUpgrade);
        }

        return BuildStatDescriptionWithValue(description, statUpgrade, tier);
    }

    private static void ApplyTierPrefixToCardDescription(GameObject root, StatUpgrade.StatCardTier tier, bool isReduction)
    {
        TMP_Text descText = FindCardDescriptionText(root);
        if (descText == null || string.IsNullOrWhiteSpace(descText.text))
        {
            return;
        }

        descText.richText = true;
        descText.text = BuildTierPrefixedDescription(descText.text, tier, isReduction);
    }

    private static string BuildTierPrefixedDescription(string description, StatUpgrade.StatCardTier tier, bool isReduction)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        return StripTierSymbols(description);
    }

    private static string BuildStatDescriptionWithValue(string description, StatUpgrade statUpgrade, StatUpgrade.StatCardTier tier)
    {
        string body = StripTierSymbols(description);
        if (string.IsNullOrWhiteSpace(body) || !body.Contains(DescriptionValueToken))
        {
            return body;
        }

        List<string> values = BuildStatDescriptionValues(statUpgrade, tier);
        if (values.Count == 0)
        {
            return body.Replace(DescriptionValueToken, string.Empty).Trim();
        }

        string[] lines = SplitDescriptionLines(body);
        int valueIndex = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains(DescriptionValueToken))
            {
                continue;
            }

            string valueText = valueIndex < values.Count ? values[valueIndex] : string.Empty;
            valueIndex++;
            string replacement = string.IsNullOrWhiteSpace(valueText)
                ? string.Empty
                : $"<color={DescriptionValueColor}><b>{valueText}</b></color>";
            lines[i] = lines[i].Replace(DescriptionValueToken, replacement);
        }

        return string.Join("\n", lines).Trim();
    }

    private static string BuildWeaponDescriptionWithValue(string description, WeaponDefinition definition, StatUpgrade.StatCardTier tier)
    {
        string body = StripTierSymbols(description);
        if (string.IsNullOrWhiteSpace(body) || !body.Contains(DescriptionValueToken))
        {
            return body;
        }

        List<string> values = BuildWeaponDescriptionValues(definition, tier);
        if (values.Count == 0)
        {
            return body.Replace(DescriptionValueToken, string.Empty).Trim();
        }

        string[] lines = SplitDescriptionLines(body);
        int valueIndex = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains(DescriptionValueToken))
            {
                continue;
            }

            string valueText = valueIndex < values.Count ? values[valueIndex] : string.Empty;
            valueIndex++;
            string replacement = string.IsNullOrWhiteSpace(valueText)
                ? string.Empty
                : $"<color={DescriptionValueColor}><b>{valueText}</b></color>";
            lines[i] = lines[i].Replace(DescriptionValueToken, replacement);
        }

        return string.Join("\n", lines).Trim();
    }

    private static string BuildDefaultWeaponDescription(WeaponDefinition definition, StatUpgrade.StatCardTier tier)
    {
        if (definition == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        string baseDamageLabel = ShouldUseUniqueDamageAmplifyLabel(definition, tier)
            ? "공격력 (N) 증폭"
            : "공격력 (N) 증가";
        AppendDefaultWeaponDescriptionLine(builder, baseDamageLabel, HasDescriptionFlatOrPercentValue(definition.GetBaseDamage(StatUpgrade.StatCardTier.Normal), definition.GetBaseDamage(StatUpgrade.StatCardTier.Rare), definition.GetBaseDamage(StatUpgrade.StatCardTier.Unique), definition.GetBaseDamagePercent(StatUpgrade.StatCardTier.Normal), definition.GetBaseDamagePercent(StatUpgrade.StatCardTier.Rare), definition.GetBaseDamagePercent(StatUpgrade.StatCardTier.Unique)));
        AppendDefaultWeaponDescriptionLine(builder, "관통피해율 (N) 증가", HasDescriptionTierValue(definition.GetSawPierceDamageRatio(StatUpgrade.StatCardTier.Normal), definition.GetSawPierceDamageRatio(StatUpgrade.StatCardTier.Rare), definition.GetSawPierceDamageRatio(StatUpgrade.StatCardTier.Unique)));
        AppendDefaultWeaponDescriptionLine(builder, "투사체 속도 (N) 증가", HasDescriptionFlatOrPercentValue(definition.GetProjectileSpeed(StatUpgrade.StatCardTier.Normal), definition.GetProjectileSpeed(StatUpgrade.StatCardTier.Rare), definition.GetProjectileSpeed(StatUpgrade.StatCardTier.Unique), definition.GetProjectileSpeedPercent(StatUpgrade.StatCardTier.Normal), definition.GetProjectileSpeedPercent(StatUpgrade.StatCardTier.Rare), definition.GetProjectileSpeedPercent(StatUpgrade.StatCardTier.Unique)));
        AppendDefaultWeaponDescriptionLine(builder, "사거리 (N) 증가", HasDescriptionFlatOrPercentValue(definition.GetSearchRange(StatUpgrade.StatCardTier.Normal), definition.GetSearchRange(StatUpgrade.StatCardTier.Rare), definition.GetSearchRange(StatUpgrade.StatCardTier.Unique), definition.GetSearchRangePercent(StatUpgrade.StatCardTier.Normal), definition.GetSearchRangePercent(StatUpgrade.StatCardTier.Rare), definition.GetSearchRangePercent(StatUpgrade.StatCardTier.Unique)));
        AppendDefaultWeaponDescriptionLine(builder, "연쇄 단계 (N) 증가", HasDescriptionTierValue(definition.GetMaxChainDepth(StatUpgrade.StatCardTier.Normal), definition.GetMaxChainDepth(StatUpgrade.StatCardTier.Rare), definition.GetMaxChainDepth(StatUpgrade.StatCardTier.Unique)));
        AppendDefaultWeaponDescriptionLine(builder, "연쇄 거리 (N) 증가", HasDescriptionFlatOrPercentValue(definition.GetChainRange(StatUpgrade.StatCardTier.Normal), definition.GetChainRange(StatUpgrade.StatCardTier.Rare), definition.GetChainRange(StatUpgrade.StatCardTier.Unique), definition.GetChainRangePercent(StatUpgrade.StatCardTier.Normal), definition.GetChainRangePercent(StatUpgrade.StatCardTier.Rare), definition.GetChainRangePercent(StatUpgrade.StatCardTier.Unique)));
        AppendDefaultWeaponDescriptionLine(builder, "체인 피해 유지율 (N) 증가", HasDescriptionTierValue(definition.GetChainDamageFalloff(StatUpgrade.StatCardTier.Normal), definition.GetChainDamageFalloff(StatUpgrade.StatCardTier.Rare), definition.GetChainDamageFalloff(StatUpgrade.StatCardTier.Unique)));
        AppendDefaultWeaponDescriptionLine(builder, "발사 수 (N) 증가", HasDescriptionTierValue(definition.GetProjectileCount(StatUpgrade.StatCardTier.Normal), definition.GetProjectileCount(StatUpgrade.StatCardTier.Rare), definition.GetProjectileCount(StatUpgrade.StatCardTier.Unique)));
        AppendDefaultWeaponDescriptionLine(builder, "쿨타임 (N) 감소", HasDescriptionTierValue(definition.GetCooldownReduction(StatUpgrade.StatCardTier.Normal), definition.GetCooldownReduction(StatUpgrade.StatCardTier.Rare), definition.GetCooldownReduction(StatUpgrade.StatCardTier.Unique)));
        AppendDefaultWeaponDescriptionLine(builder, "부채꼴 각도 (N) 증가", HasDescriptionTierValue(definition.GetSideConeAngle(StatUpgrade.StatCardTier.Normal), definition.GetSideConeAngle(StatUpgrade.StatCardTier.Rare), definition.GetSideConeAngle(StatUpgrade.StatCardTier.Unique)));
        AppendDefaultWeaponDescriptionLine(builder, "지속시간 (N) 증가", HasDescriptionFlatOrPercentValue(definition.GetLaserDuration(StatUpgrade.StatCardTier.Normal), definition.GetLaserDuration(StatUpgrade.StatCardTier.Rare), definition.GetLaserDuration(StatUpgrade.StatCardTier.Unique), definition.GetLaserDurationPercent(StatUpgrade.StatCardTier.Normal), definition.GetLaserDurationPercent(StatUpgrade.StatCardTier.Rare), definition.GetLaserDurationPercent(StatUpgrade.StatCardTier.Unique)));
        AppendDefaultWeaponDescriptionLine(builder, "틱 간격 (N) 감소", HasDescriptionTierValue(definition.GetLaserTickInterval(StatUpgrade.StatCardTier.Normal), definition.GetLaserTickInterval(StatUpgrade.StatCardTier.Rare), definition.GetLaserTickInterval(StatUpgrade.StatCardTier.Unique)));
        AppendDefaultWeaponDescriptionLine(builder, "구르기 거리 (N) 증가", HasDescriptionFlatOrPercentValue(definition.GetLandingRollDistance(StatUpgrade.StatCardTier.Normal), definition.GetLandingRollDistance(StatUpgrade.StatCardTier.Rare), definition.GetLandingRollDistance(StatUpgrade.StatCardTier.Unique), definition.GetLandingRollDistancePercent(StatUpgrade.StatCardTier.Normal), definition.GetLandingRollDistancePercent(StatUpgrade.StatCardTier.Rare), definition.GetLandingRollDistancePercent(StatUpgrade.StatCardTier.Unique)));
        AppendDefaultWeaponDescriptionLine(builder, "구르기 시간 (N) 증가", HasDescriptionFlatOrPercentValue(definition.GetLandingRollDuration(StatUpgrade.StatCardTier.Normal), definition.GetLandingRollDuration(StatUpgrade.StatCardTier.Rare), definition.GetLandingRollDuration(StatUpgrade.StatCardTier.Unique), definition.GetLandingRollDurationPercent(StatUpgrade.StatCardTier.Normal), definition.GetLandingRollDurationPercent(StatUpgrade.StatCardTier.Rare), definition.GetLandingRollDurationPercent(StatUpgrade.StatCardTier.Unique)));
        AppendDefaultWeaponDescriptionLine(builder, "관통 수 (N) 증가", HasDescriptionTierValue(definition.GetPierceCount(StatUpgrade.StatCardTier.Normal), definition.GetPierceCount(StatUpgrade.StatCardTier.Rare), definition.GetPierceCount(StatUpgrade.StatCardTier.Unique)));
        AppendDefaultWeaponDescriptionLine(builder, "폭발 반경 (N) 증가", HasDescriptionFlatOrPercentValue(definition.GetExplosionRadius(StatUpgrade.StatCardTier.Normal), definition.GetExplosionRadius(StatUpgrade.StatCardTier.Rare), definition.GetExplosionRadius(StatUpgrade.StatCardTier.Unique), definition.GetExplosionRadiusPercent(StatUpgrade.StatCardTier.Normal), definition.GetExplosionRadiusPercent(StatUpgrade.StatCardTier.Rare), definition.GetExplosionRadiusPercent(StatUpgrade.StatCardTier.Unique)));
        string result = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? definition.NormalizedId : result;
    }

    private static string ApplyUniqueDamageAmplifyWord(string description, WeaponDefinition definition, StatUpgrade.StatCardTier tier)
    {
        if (string.IsNullOrWhiteSpace(description) || !ShouldUseUniqueDamageAmplifyLabel(definition, tier))
        {
            return description;
        }

        string result = description;
        result = result.Replace("공격력 (N) 증가", "공격력 (N) 증폭");
        result = result.Replace("피해량 (N) 증가", "피해량 (N) 증폭");
        result = result.Replace("피해 (N) 증가", "피해 (N) 증폭");
        result = result.Replace("데미지 (N) 증가", "데미지 (N) 증폭");
        return result;
    }

    private static bool ShouldUseUniqueDamageAmplifyLabel(WeaponDefinition definition, StatUpgrade.StatCardTier tier)
    {
        return definition != null
            && tier == StatUpgrade.StatCardTier.Unique
            && definition.UsesCurrentBaseDamagePercent(tier)
            && (Mathf.Abs(definition.GetBaseDamage(tier)) > 0.0001f
                || Mathf.Abs(definition.GetBaseDamagePercent(tier)) > 0.0001f);
    }

    private static string BuildDefaultStatDescription(StatUpgrade statUpgrade)
    {
        if (statUpgrade == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        AppendDefaultStatDescriptionLine(builder, "모든 무기 공격력 (N) 증가", statUpgrade.DamageMultiplierBonus);
        AppendDefaultStatDescriptionLine(builder, "모든 밀리 공격력 (N) 증가", statUpgrade.MeleeDamageMultiplierBonus);
        AppendDefaultStatDescriptionLine(builder, "모든 마법 공격력 (N) 증가", statUpgrade.MagicDamageMultiplierBonus);
        AppendDefaultStatDescriptionLine(builder, "모든 세그먼트 쿨타임 (N) 감소", statUpgrade.AttackSpeedMultiplierBonus);
        AppendDefaultStatDescriptionLine(builder, "핸들링 (N) 증가", statUpgrade.TurnSpeedBonus);
        AppendDefaultStatDescriptionLine(builder, "충돌힘 (N) 증가", statUpgrade.CollisionForceBonus);
        AppendDefaultStatDescriptionLine(builder, "재결합 범위 (N) 증가", statUpgrade.RejoinRangeBonus);
        AppendDefaultStatDescriptionLine(builder, "넥서스 최대 체력 (N) 증가", statUpgrade.NexusHealthBonus);
        return builder.ToString().Trim();
    }

    private static void AppendDefaultWeaponDescriptionLine(StringBuilder builder, string line, bool active)
    {
        if (!active)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(line);
    }

    private static void AppendDefaultStatDescriptionLine(StringBuilder builder, string line, float value)
    {
        AppendDefaultWeaponDescriptionLine(builder, line, Mathf.Abs(value) > 0.0001f);
    }

    private static List<string> BuildWeaponDescriptionValues(WeaponDefinition definition, StatUpgrade.StatCardTier tier)
    {
        List<string> values = new List<string>(4);
        AddWeaponDescriptionValue(values, TryFormatFlatOrPercentValue(definition.GetBaseDamage(tier), definition.GetBaseDamagePercent(tier), out string baseDamageValue), baseDamageValue);
        AddWeaponDescriptionValue(values, TryFormatPercentRate(definition.GetSawPierceDamageRatio(tier), out string pierceRatioValue), pierceRatioValue);
        AddWeaponDescriptionValue(values, TryFormatFlatOrPercentValue(definition.GetProjectileSpeed(tier), definition.GetProjectileSpeedPercent(tier), out string projectileSpeedValue), projectileSpeedValue);
        AddWeaponDescriptionValue(values, TryFormatFlatOrPercentValue(definition.GetSearchRange(tier), definition.GetSearchRangePercent(tier), "M", out string searchRangeValue), searchRangeValue);
        AddWeaponDescriptionValue(values, TryFormatIntValue(definition.GetMaxChainDepth(tier), out string chainDepthValue), chainDepthValue);
        AddWeaponDescriptionValue(values, TryFormatFlatOrPercentValue(definition.GetChainRange(tier), definition.GetChainRangePercent(tier), "M", out string chainRangeValue), chainRangeValue);
        AddWeaponDescriptionValue(values, TryFormatPercentRate(definition.GetChainDamageFalloff(tier), out string chainFalloffValue), chainFalloffValue);
        AddWeaponDescriptionValue(values, TryFormatIntValue(definition.GetProjectileCount(tier), out string projectileCountValue), projectileCountValue);
        AddWeaponDescriptionValue(values, TryFormatPercentRate(definition.GetCooldownReduction(tier), out string cooldownValue), cooldownValue);
        AddWeaponDescriptionValue(values, TryFormatFloatValue(definition.GetSideConeAngle(tier), out string sideConeValue), sideConeValue);
        AddWeaponDescriptionValue(values, TryFormatFlatOrPercentValue(definition.GetLaserDuration(tier), definition.GetLaserDurationPercent(tier), out string laserDurationValue), laserDurationValue);
        AddWeaponDescriptionValue(values, TryFormatPercentRate(definition.GetLaserTickInterval(tier), out string laserTickValue), laserTickValue);
        AddWeaponDescriptionValue(values, TryFormatFlatOrPercentValue(definition.GetLandingRollDistance(tier), definition.GetLandingRollDistancePercent(tier), "M", out string rollDistanceValue), rollDistanceValue);
        AddWeaponDescriptionValue(values, TryFormatFlatOrPercentValue(definition.GetLandingRollDuration(tier), definition.GetLandingRollDurationPercent(tier), out string rollDurationValue), rollDurationValue);
        AddWeaponDescriptionValue(values, TryFormatIntValue(definition.GetPierceCount(tier), out string pierceCountValue), pierceCountValue);
        AddWeaponDescriptionValue(values, TryFormatFlatOrPercentValue(definition.GetExplosionRadius(tier), definition.GetExplosionRadiusPercent(tier), "M", out string explosionRadiusValue), explosionRadiusValue);
        return values;
    }

    private static List<string> BuildStatDescriptionValues(StatUpgrade statUpgrade, StatUpgrade.StatCardTier tier)
    {
        List<string> values = new List<string>(4);
        if (statUpgrade == null)
        {
            return values;
        }

        float multiplier = StatUpgrade.GetTierMultiplier(tier);
        AddWeaponDescriptionValue(values, TryFormatPercentRate(statUpgrade.DamageMultiplierBonus * multiplier, out string damageValue), damageValue);
        AddWeaponDescriptionValue(values, TryFormatPercentRate(statUpgrade.MeleeDamageMultiplierBonus * multiplier, out string meleeDamageValue), meleeDamageValue);
        AddWeaponDescriptionValue(values, TryFormatPercentRate(statUpgrade.MagicDamageMultiplierBonus * multiplier, out string magicDamageValue), magicDamageValue);
        AddWeaponDescriptionValue(values, TryFormatPercentRate(statUpgrade.AttackSpeedMultiplierBonus * multiplier, out string attackSpeedValue), attackSpeedValue);
        AddWeaponDescriptionValue(values, TryFormatFloatValue(statUpgrade.TurnSpeedBonus * multiplier, out string turnSpeedValue), turnSpeedValue);
        AddWeaponDescriptionValue(values, TryFormatPercentRate(statUpgrade.CollisionForceBonus * multiplier, out string collisionForceValue), collisionForceValue);
        AddWeaponDescriptionValue(values, TryFormatFloatValue(statUpgrade.RejoinRangeBonus * multiplier, out string rejoinRangeValue), string.IsNullOrWhiteSpace(rejoinRangeValue) ? rejoinRangeValue : $"{rejoinRangeValue}M");
        AddWeaponDescriptionValue(values, TryFormatFloatValue(statUpgrade.NexusHealthBonus * multiplier, out string nexusHealthValue), nexusHealthValue);
        return values;
    }

    private static void AddWeaponDescriptionValue(List<string> values, bool active, string valueText)
    {
        if (active)
        {
            values.Add(valueText);
        }
    }

    private static string[] SplitDescriptionLines(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? System.Array.Empty<string>()
            : text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    private static bool HasDescriptionFlatOrPercentValue(float normal, float rare, float unique, float normalPercent, float rarePercent, float uniquePercent)
    {
        return HasDescriptionTierValue(normal, rare, unique) || HasDescriptionTierValue(normalPercent, rarePercent, uniquePercent);
    }

    private static bool HasDescriptionTierValue(float normal, float rare, float unique)
    {
        return Mathf.Abs(normal) > 0.0001f || Mathf.Abs(rare) > 0.0001f || Mathf.Abs(unique) > 0.0001f;
    }

    private static bool HasDescriptionTierValue(int normal, int rare, int unique)
    {
        return normal != 0 || rare != 0 || unique != 0;
    }

    private static bool TryFormatFlatOrPercentValue(float flatValue, float percentValue, out string valueText)
    {
        return TryFormatFlatOrPercentValue(flatValue, percentValue, string.Empty, out valueText);
    }

    private static bool TryFormatFlatOrPercentValue(float flatValue, float percentValue, string flatSuffix, out string valueText)
    {
        if (TryFormatPercentRate(percentValue, out valueText))
        {
            return true;
        }

        if (!TryFormatFloatValue(flatValue, out valueText))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(flatSuffix))
        {
            valueText += flatSuffix;
        }

        return true;
    }

    private static bool TryFormatFloatValue(float value, out string valueText)
    {
        valueText = string.Empty;
        if (Mathf.Abs(value) <= 0.0001f)
        {
            return false;
        }

        valueText = value.ToString("0.###", CultureInfo.InvariantCulture);
        return true;
    }

    private static bool TryFormatIntValue(int value, out string valueText)
    {
        valueText = string.Empty;
        if (value == 0)
        {
            return false;
        }

        valueText = value.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static bool TryFormatPercentRate(float value, out string valueText)
    {
        valueText = string.Empty;
        if (value <= 0.0001f)
        {
            return false;
        }

        valueText = $"{(value * 100f).ToString("0.#", CultureInfo.InvariantCulture)}%";
        return true;
    }

    private static string StripTierSymbols(string text)
    {
        string trimmed = text.Trim();
        int index = 0;
        while (index < trimmed.Length && (trimmed[index] == '+' || trimmed[index] == '-'))
        {
            index++;
        }

        string body = trimmed;
        if (index > 0 && index < trimmed.Length && char.IsWhiteSpace(trimmed[index]))
        {
            body = trimmed.Substring(index).TrimStart();
        }

        int symbolStart = body.Length;
        while (symbolStart > 0 && (body[symbolStart - 1] == '+' || body[symbolStart - 1] == '-'))
        {
            symbolStart--;
        }

        if (symbolStart < body.Length && (symbolStart == 0 || char.IsWhiteSpace(body[symbolStart - 1])))
        {
            body = body.Substring(0, symbolStart).TrimEnd();
        }

        return body;
    }

    private static TMP_Text FindCardDescriptionText(GameObject root)
    {
        if (root == null)
        {
            return null;
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].gameObject.name == "DescText")
            {
                return texts[i];
            }
        }

        return null;
    }

    private static bool Includes(SegmentWeaponStatDisplayFlags flags, SegmentWeaponStatDisplayFlags target)
    {
        return (flags & target) != 0;
    }

    private static void AppendStatLineFloat(StringBuilder sb, string label, float baseValue, float effectiveValue, float flatBonus, float percentBonus = 0f)
    {
        sb.Append(label).Append(": ").Append(effectiveValue.ToString("0.##"));
        bool hasFlat = Mathf.Abs(flatBonus) > 0.0001f;
        bool hasPercent = percentBonus > 0.0001f;
        if (hasFlat || hasPercent)
        {
            sb.Append(" (");
            if (hasFlat)
            {
                sb.Append('+').Append(flatBonus.ToString("0.##"));
            }

            if (hasFlat && hasPercent)
            {
                sb.Append(", ");
            }

            if (hasPercent)
            {
                sb.Append('+').Append((percentBonus * 100f).ToString("0.#")).Append('%');
            }

            sb.Append(')');
        }
        else if (Mathf.Abs(effectiveValue - baseValue) > 0.0001f)
        {
            sb.Append(" (기본 ").Append(baseValue.ToString("0.##")).Append(')');
        }

        sb.AppendLine();
    }

    private static void AppendStatLineInt(StringBuilder sb, string label, int baseValue, int effectiveValue, int bonusDelta)
    {
        sb.Append(label).Append(": ").Append(effectiveValue);
        if (bonusDelta != 0)
        {
            sb.Append(" (+").Append(bonusDelta).Append(')');
        }
        else if (effectiveValue != baseValue)
        {
            sb.Append(" (기본 ").Append(baseValue).Append(')');
        }

        sb.AppendLine();
    }
    // 건춘추가 - 0621 ======

    //전찬우 수정-0622
    private void TrySubscribeSegmentWeaponStatDebug() // CoreStatProvider 변경 구독
    {
        CoreStatProvider core = CoreStatProvider.Active;
        if (core == null || core == segmentWeaponStatSubscribedCore)
        {
            return; // 코어 없음 / 이미 구독
        }

        UnsubscribeSegmentWeaponStatDebug();
        segmentWeaponStatSubscribedCore = core;
        segmentWeaponStatSubscribedCore.StatsChanged += HandleCoreStatsChangedForWeaponStatDebug;
    }

    private void UnsubscribeSegmentWeaponStatDebug()
    {
        if (segmentWeaponStatSubscribedCore == null)
        {
            return; // 구독 없음
        }

        segmentWeaponStatSubscribedCore.StatsChanged -= HandleCoreStatsChangedForWeaponStatDebug;
        segmentWeaponStatSubscribedCore = null;
    }

    private void HandleCoreStatsChangedForWeaponStatDebug(CoreStatData stats)
    {
        RefreshSegmentWeaponStatUi(); // 리셋·디버그 버튼·외부 변경 반영
    }

    // =============== 세그먼트 구성 디버그 ===============
    private ConvoyController segmentDebugSubscribedConvoy; // 구독 중인 컨보이

    private void TrySubscribeSegmentCountDebug() // Convoy 세그먼트 수 변경 구독
    {
        ConvoyController convoy = CoreStatProvider.Active != null ? CoreStatProvider.Active.Convoy : null; // 현재 컨보이
        if (convoy == null || convoy == segmentDebugSubscribedConvoy)
        {
            return; // 컨보이 없음 / 이미 구독
        }

        UnsubscribeSegmentCountDebug(); // 이전 구독 해제
        segmentDebugSubscribedConvoy = convoy;
        segmentDebugSubscribedConvoy.SegmentCountChanged += HandleConvoySegmentCountChangedForDebug; // 추가/제거 알림
    }

    private void UnsubscribeSegmentCountDebug()
    {
        if (segmentDebugSubscribedConvoy == null)
        {
            return; // 구독 없음
        }

        segmentDebugSubscribedConvoy.SegmentCountChanged -= HandleConvoySegmentCountChangedForDebug;
        segmentDebugSubscribedConvoy = null;
    }

    private void HandleConvoySegmentCountChangedForDebug(int segmentCount) // 세그먼트 수 변경 시
    {
        LogPlayerSegmentCountsDebug($"전체 세그먼트 : {segmentCount} / 각 세그먼트 : "); // CoreTest·카드 UI 공통
        RefreshSegmentWeaponStatUi(); // 건춘추가 - 0621 ====== 레벨업·추가 후 스탯 UI 갱신
        RefreshSegmentListText(); // 안건준 추가 - 0622 — 세그먼트 변경 시 리스트 텍스트 갱신
    }

    private void LogPlayerSegmentCountsDebug(string reason) // ConvoySegments 현재 구성 출력
    {
        if (!logPlayerSegmentCounts)
        {
            return; // 비활성
        }

        CoreStatProvider core = CoreStatProvider.Active; // 현재 코어
        ConvoyController convoy = core != null ? core.Convoy : null; // 플레이어 컨보이
        if (convoy == null)
        {
            Debug.LogWarning($"{reason} | Convoy 없음 — 세그먼트 집계 불가");
            return;
        }

        Dictionary<string, int> countsBySegmentId = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase); // ID별 개수
        convoy.CollectAttachedSegmentCounts(countsBySegmentId); // SG01_Cannon 등 집계
        int total = convoy.GetAttachedSegmentTotalCount(); // 전체 개수 (스타터 포함)

        List<string> sortedSegmentIds = new List<string>(countsBySegmentId.Keys); // 정렬용
        sortedSegmentIds.Sort(System.StringComparer.OrdinalIgnoreCase);

        StringBuilder builder = new StringBuilder(128);        
        builder.Append(reason);
        // builder.Append(" 전체 세그먼트 숫자 : ");
        // builder.Append(total);

        for (int i = 0; i < sortedSegmentIds.Count; i++)
        {
            string segmentId = sortedSegmentIds[i]; // SG01_Cannon / SG02_Missile 등
            builder.Append(" , ");
            builder.Append(segmentId);
            builder.Append(' ');
            builder.Append(countsBySegmentId[segmentId]);
        }

        Debug.Log(builder.ToString());
    }
    // =============== 끝 ===============

    private SpawnedCardEntry CreateSpawnedCard(GameObject prefab, RectTransform slot, GameObject sourcePrefab = null, bool skipStatUpgradeRoll = false) // sourcePrefab: 선택 가중치용 원본 프리팹
    {
        if (prefab == null || slot == null)
        {
            return null;
        }

        GameObject instance = Instantiate(prefab, slot); // 프리팹 생성
        PrepareCardTooltipHidden(instance); // 프리팹 기본 활성 툴팁이 한 프레임 보이는 현상 방지
        RectTransform rectTransform = instance.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogWarning("[CardUI] 카드 프리팹에 RectTransform이 없습니다.", prefab);
            Destroy(instance);
            return null;
        }

        CanvasGroup canvasGroup = instance.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = instance.GetComponentInChildren<CanvasGroup>(true);
        }

        if (canvasGroup == null)
        {
            Debug.LogWarning("[CardUI] 카드 프리팹에 CanvasGroup이 없습니다.", prefab);
            Destroy(instance);
            return null;
        }

        StatUpgrade statUpgrade = instance.GetComponent<StatUpgrade>();
        if (statUpgrade == null)
        {
            statUpgrade = instance.GetComponentInChildren<StatUpgrade>(true);
        }

        SegmentAddCard segmentAddCard = instance.GetComponent<SegmentAddCard>();
        if (segmentAddCard == null)
        {
            segmentAddCard = instance.GetComponentInChildren<SegmentAddCard>(true);
        }

        // 안건준 추가 - 0623 : SegmentUpgradeCard 같이 SegmentAddCard가 없는 커스텀 프리팹도 텍스트 주입 가능하도록 자동 추가
        if (statUpgrade == null && segmentAddCard == null)
        {
            segmentAddCard = instance.AddComponent<SegmentAddCard>();
        }

        if (statUpgrade != null && !skipStatUpgradeRoll)
        {
            statUpgrade.RollSpawnVariant(rareCardChancePercent, uniqueCardChancePercent); // 등급(일반/레어/유니크) + 색상
        }

        ConfigureSpawnedRect(rectTransform, slot); // 프리팹 크기 유지 + 슬롯 중앙 배치

        SpawnedCardEntry entry = new SpawnedCardEntry
        {
            Root = instance,
            RootTransform = rectTransform,
            CanvasGroup = canvasGroup,
            StatUpgrade = statUpgrade,
            SegmentAddCard = segmentAddCard,
            SourcePrefab = sourcePrefab != null ? sourcePrefab : prefab, // null이면 prefab 자체를 추적
            OriginalPosition = rectTransform.anchoredPosition,
            OriginalScale = rectTransform.localScale,
            CanSelect = true // 기본 카드는 선택 가능
        };

        CacheCardTooltip(entry); // 프리팹에 배치된 카드 툴팁 오브젝트 연결
        WireSpawnedCardInput(entry); // 클릭·호버 연결
        return entry;
    }

    private static void ConfigureSpawnedRect(RectTransform cardRect, RectTransform slot)
    {
        Vector2 sizeDelta = cardRect.sizeDelta; // 프리팹 원본 크기
        Vector3 localScale = cardRect.localScale; // 프리팹 원본 스케일
        Vector2 pivot = cardRect.pivot; // 프리팹 피벗

        cardRect.anchorMin = new Vector2(0.5f, 0.5f); // 슬롯 중앙 기준
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = pivot;
        cardRect.sizeDelta = sizeDelta; // stretch 금지 → 프리팹과 동일 크기
        cardRect.anchoredPosition = Vector2.zero; // 슬롯 중심 (프리팹 편집용 -350 등 제거)
        cardRect.localScale = localScale;

        if (slot != null && sizeDelta.sqrMagnitude > 0f)
        {
            slot.sizeDelta = sizeDelta; // 슬롯도 프리팹 크기에 맞춤
        }
    }

    private void WireSpawnedCardInput(SpawnedCardEntry entry)
    {
        Button button = entry.Root.GetComponent<Button>();
        if (button == null)
        {
            button = entry.Root.GetComponentInChildren<Button>(true);
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => NotifySpawnedCardClicked(entry));
        }

        CardInstanceBridge bridge = entry.Root.GetComponent<CardInstanceBridge>();
        if (bridge == null)
        {
            bridge = entry.Root.AddComponent<CardInstanceBridge>();
        }

        bridge.Initialize(this, entry);
        WireSpawnedCardChildInput(entry); // 아이콘/텍스트 자식 히트도 카드 입력으로 연결
    }

    private void WireSpawnedCardChildInput(SpawnedCardEntry entry)
    {
        if (entry == null || entry.Root == null)
        {
            return;
        }

        Graphic[] graphics = entry.Root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null || !graphic.raycastTarget || graphic.gameObject == entry.Root)
            {
                continue;
            }

            if (graphic.GetComponent<Button>() != null)
            {
                continue; // 별도 버튼이면 기존 Button 흐름 유지
            }

            CardChildInputBridge childBridge = graphic.GetComponent<CardChildInputBridge>();
            if (childBridge == null)
            {
                childBridge = graphic.gameObject.AddComponent<CardChildInputBridge>();
            }

            childBridge.Initialize(this, entry);
        }
    }

    private static List<GameObject> BuildPrefabPool(GameObject[] prefabs)
    {
        List<GameObject> pool = new List<GameObject>(prefabs.Length);
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
            {
                pool.Add(prefabs[i]);
            }
        }

        return pool;
    }

    private List<GameObject> PickWeightedStatPrefabs(List<GameObject> pool, int count) // 스탯 카드 가중치 랜덤 선택
    {
        List<WeightedPrefabEntry> remaining = new List<WeightedPrefabEntry>(pool.Count); // 남은 후보+가중치
        for (int i = 0; i < pool.Count; i++)
        {
            GameObject prefab = pool[i]; // 후보 프리팹
            if (prefab == null)
            {
                continue; // null 제외
            }

            float weight = baseCardSpawnWeight; // 기본 가중치
            if (lastSelectedStatCardPrefab != null && prefab == lastSelectedStatCardPrefab)
            {
                weight += selectedCardWeightBonus; // 직전 선택 카드 가중치 증가
            }

            remaining.Add(new WeightedPrefabEntry
            {
                Prefab = prefab, // 후보 프리팹
                Weight = weight // 최종 가중치
            });
        }

        List<GameObject> picked = new List<GameObject>(count); // 선택 결과
        int pickCount = Mathf.Min(count, remaining.Count); // 뽑을 수량
        for (int pickIndex = 0; pickIndex < pickCount; pickIndex++)
        {
            if (!TryPickWeightedPrefab(remaining, out WeightedPrefabEntry selected))
            {
                break; // 더 이상 선택 불가
            }

            picked.Add(selected.Prefab); // 선택된 프리팹 추가
            remaining.Remove(selected); // 중복 방지를 위해 후보에서 제거
        }

        return picked; // 최종 3장(또는 가능한 수)
    }

    private static bool TryPickWeightedPrefab(List<WeightedPrefabEntry> pool, out WeightedPrefabEntry selected) // 가중치 1장 뽑기
    {
        selected = default; // 기본값
        if (pool == null || pool.Count == 0)
        {
            return false; // 후보 없음
        }

        float totalWeight = 0f; // 전체 가중치 합
        for (int i = 0; i < pool.Count; i++)
        {
            totalWeight += pool[i].Weight; // 가중치 누적
        }

        if (totalWeight <= 0f)
        {
            selected = pool[pool.Count - 1]; // fallback: 마지막 후보
            return true;
        }

        float roll = Random.Range(0f, totalWeight); // 0~합계 난수
        float cumulative = 0f; // 누적 구간
        selected = pool[pool.Count - 1]; // fallback
        for (int i = 0; i < pool.Count; i++)
        {
            cumulative += pool[i].Weight; // 구간 확장
            if (roll < cumulative)
            {
                selected = pool[i]; // 해당 구간 당첨
                return true;
            }
        }

        return true; // 부동소수 오차 fallback
    }

    private void RememberSelectedStatCardPrefab(GameObject sourcePrefab) // 직전 선택 카드 저장
    {
        if (sourcePrefab == null)
        {
            return; // 저장할 프리팹 없음
        }

        lastSelectedStatCardPrefab = sourcePrefab; // 다음 SpawnLevelUpCards에서 가중치 적용
        lastSelectedStatCardDefinition = null; // 데이터 에셋 모드와 중복 추적 방지
    }

    private List<StatUpgradeDefinition> PickWeightedStatDefinitions(List<StatUpgradeDefinition> pool, int count) // 데이터 에셋 스탯 카드 가중치 선택
    {
        List<WeightedDefinitionEntry> remaining = new List<WeightedDefinitionEntry>(pool.Count);
        for (int i = 0; i < pool.Count; i++)
        {
            StatUpgradeDefinition definition = pool[i];
            if (definition == null)
            {
                continue;
            }

            float weight = baseCardSpawnWeight;
            if (lastSelectedStatCardDefinition != null && definition == lastSelectedStatCardDefinition)
            {
                weight += selectedCardWeightBonus;
            }

            remaining.Add(new WeightedDefinitionEntry
            {
                Definition = definition,
                Weight = weight
            });
        }

        List<StatUpgradeDefinition> picked = new List<StatUpgradeDefinition>(count);
        int pickCount = Mathf.Min(count, remaining.Count);
        for (int pickIndex = 0; pickIndex < pickCount; pickIndex++)
        {
            if (!TryPickWeightedDefinition(remaining, out WeightedDefinitionEntry selected))
            {
                break;
            }

            picked.Add(selected.Definition);
            remaining.Remove(selected);
        }

        return picked;
    }

    private static bool TryPickWeightedDefinition(List<WeightedDefinitionEntry> pool, out WeightedDefinitionEntry selected)
    {
        selected = default;
        if (pool == null || pool.Count == 0)
        {
            return false;
        }

        float totalWeight = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            totalWeight += pool[i].Weight;
        }

        if (totalWeight <= 0f)
        {
            selected = pool[pool.Count - 1];
            return true;
        }

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        selected = pool[pool.Count - 1];
        for (int i = 0; i < pool.Count; i++)
        {
            cumulative += pool[i].Weight;
            if (roll < cumulative)
            {
                selected = pool[i];
                return true;
            }
        }

        return true;
    }

    private void RememberSelectedStatCardDefinition(StatUpgradeDefinition definition) // 직전 선택 데이터 에셋 저장
    {
        if (definition == null)
        {
            return;
        }

        lastSelectedStatCardDefinition = definition;
        lastSelectedStatCardPrefab = null; // 기존 프리팹 fallback 가중치와 분리
    }

    // 안건준 추가 - 0622 ======
}
