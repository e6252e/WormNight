using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ExpBarController : MonoBehaviour // 경험치 바 + 레벨/골드 HUD 연동
    {
        [Header("경험치 바")]
        public Slider ExpSlider;
        [SerializeField] private Image expFillImage;

        [Header("레벨업 UI")]
        public TextMeshProUGUI LevelText;
        public LevelUpUi LevelUpUi;
        public CardUI CardUi;

        [Header("경험치 텍스트")]
        [SerializeField] private TextMeshProUGUI expNumText;

        [Header("골드 텍스트")]
        [SerializeField] private TextMeshProUGUI goldText;

        [Header("보석 텍스트")]
        [SerializeField] private TextMeshProUGUI gemText;

        private CoreStatProvider subscribedCore; // StatsChanged 구독 대상
        private bool levelUpUiOpened; // 레벨업 UI 중복 오픈 방지
        private bool wasLevelUpChoicePending; // 카드 선택 완료 감지용 이전 상태
        private bool cancelingFailedLevelUpOpen; // 오픈 실패 복구 중 재진입 방지

        private void Awake()
        {
            if (ExpSlider == null)
            {
                ExpSlider = GetComponentInChildren<Slider>(true);
            }

            if (ExpSlider != null)
            {
                ExpSlider.minValue = 0f;
                ExpSlider.maxValue = 1f;
                ExpSlider.interactable = false;
            }

            if (expFillImage == null && ExpSlider != null)
            {
                foreach (Image img in ExpSlider.GetComponentsInChildren<Image>(true))
                {
                    if (img.gameObject.name.Contains("Fill") && img.gameObject.name != "Fill Area")
                    {
                        expFillImage = img;
                        break;
                    }
                }
            }

            ConfigureExpFillImage();
        }

        private void ConfigureExpFillImage()
        {
            if (expFillImage == null)
            {
                return;
            }

            expFillImage.type = Image.Type.Filled;
            expFillImage.fillMethod = Image.FillMethod.Horizontal;
            expFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            expFillImage.preserveAspect = false;
        }

        private void OnEnable()
        {
            TrySubscribeCore();
            RefreshFromCore(CoreStatProvider.GetCurrentOrDefault());
            TryProcessLevelUp();
        }

        private void Start()
        {
            TrySubscribeCore();
            RefreshFromCore(CoreStatProvider.GetCurrentOrDefault());
            TryProcessLevelUp();
        }

        private void Update()
        {
            if (subscribedCore == null && TrySubscribeCore())
            {
                RefreshFromCore(CoreStatProvider.GetCurrentOrDefault());
            }

            TryOpenPendingLevelUp(CoreStatProvider.GetCurrentOrDefault()); // UI가 바빴던 레벨업 재시도
        }

        private void OnDisable()
        {
            if (subscribedCore != null)
            {
                subscribedCore.StatsChanged -= OnStatsChanged;
                subscribedCore = null;
            }
        }

        private bool TrySubscribeCore() // CoreStatProvider.StatsChanged 구독
        {
            if (subscribedCore != null || CoreStatProvider.Active == null)
            {
                return false;
            }

            subscribedCore = CoreStatProvider.Active;
            subscribedCore.StatsChanged += OnStatsChanged;
            return true;
        }

        private void OnStatsChanged(CoreStatData stats)
        {
            RefreshFromCore(stats);
            TryOpenPendingLevelUp(stats);
        }

        private void TryOpenPendingLevelUp(CoreStatData stats) // 경험치 충족 시 카드 UI가 열릴 때만 pending 처리
        {
            if (cancelingFailedLevelUpOpen)
            {
                return;
            }

            CoreStatProvider core = CoreStatProvider.Active;
            if (core == null)
            {
                return;
            }

            stats = core.CurrentStats; // 최신 코어 값으로 재확인
            RefreshFromCore(stats);

            bool choicePending = core.IsLevelUpChoicePending;
            bool panelOpen = IsLevelUpPanelOpen();

            if (choicePending && !panelOpen)
            {
                core.CancelLevelUpChoice(); // UI 없이 pending만 남은 stuck 상태 복구
                choicePending = false;
                levelUpUiOpened = false;
            }

            if (wasLevelUpChoicePending && !choicePending)
            {
                levelUpUiOpened = false; // 카드 선택 완료 → 다음 레벨업 허용
            }

            wasLevelUpChoicePending = choicePending;
            if (choicePending)
            {
                return; // 카드 선택 중 — 경험치는 아직 미소비
            }

            if (!stats.CanLevelUp)
            {
                levelUpUiOpened = false;
                return;
            }

            if (levelUpUiOpened)
            {
                return;
            }

            CardUI cardUi = ResolveCardUi();
            if (cardUi == null || !cardUi.CanOpenLevelUpPanel())
            {
                levelUpUiOpened = false; // 보상/선택권 패널이 닫힌 뒤 Update에서 재시도
                return;
            }

            if (!core.TryBeginLevelUpChoice())
            {
                levelUpUiOpened = false; // 레벨 반영 실패 시 재시도 허용
                return;
            }

            if (cardUi.TryOpenLevelUpPanel())
            {
                levelUpUiOpened = true;
                return;
            }

            cancelingFailedLevelUpOpen = true;
            try
            {
                core.CancelLevelUpChoice(); // UI 오픈 실패 시 pending만 남기지 않음
            }
            finally
            {
                cancelingFailedLevelUpOpen = false;
                levelUpUiOpened = false;
            }
        }

        private void TryProcessLevelUp() // 기존 호출 호환
        {
            TryOpenPendingLevelUp(CoreStatProvider.GetCurrentOrDefault());
        }

        private CardUI ResolveCardUi() // 씬 연결 누락 시 런타임 보강
        {
            if (CardUi != null)
            {
                return CardUi;
            }

            CardUi = FindFirstObjectByType<CardUI>();
            return CardUi;
        }

        private bool IsLevelUpPanelOpen()
        {
            return LevelUpUi != null && LevelUpUi.IsPanelOpen;
        }

        private bool IsLevelUpPanelVisible()
        {
            return LevelUpUi != null && LevelUpUi.IsPanelVisible;
        }

        private void RefreshFromCore(CoreStatData stats)
        {
            SetFillRatio(stats.ExperienceRatio);
            SetLevelDisplay(stats.Level);
            SetExpNumDisplay(stats.CurrentExperience, stats.ExperienceToNextLevel);
            SetGoldDisplay(stats.Gold);
            SetGemDisplay(stats.CurrentRunDiamond);
        }

        private void SetFillRatio(float ratio)
        {
            float clamped = Mathf.Clamp01(ratio);

            if (expFillImage != null)
            {
                expFillImage.enabled = true;
                expFillImage.fillAmount = clamped;
                return;
            }

            if (ExpSlider != null)
            {
                ExpSlider.value = clamped;
            }
        }

        private void SetLevelDisplay(int level)
        {
            if (LevelText == null)
            {
                return;
            }

            LevelText.text = $"{Mathf.Max(1, level)}";
        }

        private void SetExpNumDisplay(int current, int max)
        {
            if (expNumText == null)
            {
                return;
            }

            expNumText.text = $"{current}/{max}";
        }

        private void SetGoldDisplay(int gold)
        {
            if (goldText == null)
            {
                return;
            }

            goldText.text = gold.ToString();
        }

        private void SetGemDisplay(int currentRunDiamond)
        {
            if (gemText == null)
            {
                return;
            }

            gemText.text = Mathf.Max(0, currentRunDiamond).ToString();
        }
    }
}
