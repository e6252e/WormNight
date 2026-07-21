using System.Collections;
using DG.Tweening;
using TeamProject01.Gameplay;
using UnityEngine;
using UnityEngine.UI;

public class LevelUpUi : MonoBehaviour
{
    private const string RewardTitleResourcePath = "UI/Reward/SelectRewardTitle";

    [Header("패널")]
    [SerializeField] private GameObject levelUpPanel;
    [Header("투명도")]
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [Header("타이틀")]
    [SerializeField] private RectTransform titleVisual; // 정식 배치된 타이틀 이미지/텍스트 연출 대상
    [SerializeField] private Sprite levelUpTitleSprite; // 기본 레벨업 타이틀
    [SerializeField] private Sprite rewardTitleSprite; // 보상/선택권 타이틀
    [Header("배경 블러")]
    [SerializeField] private UiBackgroundBlurLayer backgroundBlurLayer; // 보상/선택권 배경 블러
    [Header("디버그")]
    [SerializeField] private bool logCoreStats = true; // 코어 경험치 로그 출력 여부

    private bool isOpen;
    private bool useRewardTitle;
    private bool useBackgroundBlur;
    private Image cachedTitleImage;
    private float previousTimeScale = 1f;
    private bool pausedByPanel; // 패널이 직접 일시정지했는지 //안건준 추가 - 0628
    private bool skipPause; // 안건준 추가 - 0622 : 자동모드일 때 일시정지 스킵 플래그
    private float closeFadeDuration = 0.25f; // Close() 기본 페이드
    private CoreStatProvider subscribedCore; // 구독 중인 코어
    private int lastLoggedExp = -1; // 마지막 로그 경험치
    private int lastLoggedLevel = -1; // 마지막 로그 레벨
    private CoreStatData pendingLogStats; // 디바운스 대기값
    private Coroutine debouncedLogRoutine; // 경험치 변경 묶음 로그
    private Coroutine openBlurRoutine; // 블러 캡처 후 패널 표시 루틴

    private void Reset()
    {
        levelUpPanel = gameObject;
        panelCanvasGroup = GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null)
        {
            panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        Transform title = FindTitleTransform("LevelUpTitleImage") ?? FindTitleTransform("LevelUpText");
        if (title != null)
        {
            titleVisual = title as RectTransform;
        }
    }

    private void Start()
    {
        ResolveTitleVisual(); // 씬에 정식 배치된 타이틀 확인
        CacheTitleSprites(); // 기본/보상 타이틀 준비
        CloseInstant();
        TrySubscribeCore(); // 코어 이벤트 연결
        LogCoreStats(); // 시작 시 1회 출력
    }

    private void OnEnable()
    {
        TrySubscribeCore(); // 활성화 시 코어 연결
    }

    private void OnDisable()
    {
        if (subscribedCore != null)
        {
            subscribedCore.StatsChanged -= OnCoreStatsChanged; // 이벤트 해제
            subscribedCore = null; // 참조 제거
        }

        if (debouncedLogRoutine != null)
        {
            StopCoroutine(debouncedLogRoutine); // 대기 중 로그 취소
            debouncedLogRoutine = null;
        }
    }

    private void TrySubscribeCore()
    {
        if (subscribedCore != null || CoreStatProvider.Active == null)
        {
            return; // 이미 연결 또는 코어 없음
        }

        subscribedCore = CoreStatProvider.Active; // 현재 코어 저장
        subscribedCore.StatsChanged += OnCoreStatsChanged; // 경험치 변경 즉시 로그
    }

    private void OnCoreStatsChanged(CoreStatData stats)
    {
        if (!logCoreStats)
        {
            return; // 로그 비활성
        }

        pendingLogStats = stats; // 최신값 저장
        if (debouncedLogRoutine != null)
        {
            StopCoroutine(debouncedLogRoutine); // 같은 프레임/연속 변경은 1번만
        }

        debouncedLogRoutine = StartCoroutine(LogCoreStatsDebounced()); // 경험치 획득 시 1회 출력
    }

    private IEnumerator LogCoreStatsDebounced()
    {
        yield return null; // 같은 프레임 변경 묶기
        debouncedLogRoutine = null;

        if (!logCoreStats)
        {
            yield break;
        }

        if (pendingLogStats.CurrentExperience == lastLoggedExp && pendingLogStats.Level == lastLoggedLevel)
        {
            yield break; // 변화 없음
        }

        lastLoggedExp = pendingLogStats.CurrentExperience;
        lastLoggedLevel = pendingLogStats.Level;
        LogCoreStats(pendingLogStats); // 경험치/레벨 변경 1회 출력
    }

    private void LogCoreStats()
    {
        CoreStatData stats = CoreStatProvider.GetCurrentOrDefault(); // 코어 없으면 기본값
        lastLoggedExp = stats.CurrentExperience; // 중복 로그 방지용 저장
        lastLoggedLevel = stats.Level; // 중복 로그 방지용 저장
        LogCoreStats(stats); // 공통 로그 출력
    }

    private void LogCoreStats(CoreStatData stats)
    {
        Debug.Log(
            $"[LevelUpUi] Level={$"레벨 : "+stats.Level}, Exp={$"경험치 : "+stats.CurrentExperience+"/"+stats.ExperienceToNextLevel}, CanLevelUp={stats.CanLevelUp}",
            this); // 현재 레벨 / 현재 경험치 / 필요 경험치
    }

    private void OnDestroy()
    {
        if (openBlurRoutine != null)
        {
            StopCoroutine(openBlurRoutine);
            openBlurRoutine = null;
        }

        if (isOpen)
        {
            ResumeGame();
        }

        if (backgroundBlurLayer != null)
        {
            backgroundBlurLayer.HideImmediate(true);
        }
    }

    // 안건준 추가 - 0622 : 자동모드에서 일시정지 없이 패널 열기
    public void OpenWithoutPause()
    {
        skipPause = true;
        Open();
        skipPause = false;
    }

    public bool IsPanelOpen => isOpen; // 닫히는 중(페이드) 포함 열림 상태 //안건준 추가 - 0628

    public bool IsPanelVisible => isOpen
        && panelCanvasGroup != null
        && panelCanvasGroup.blocksRaycasts
        && panelCanvasGroup.interactable
        && panelCanvasGroup.alpha > 0.01f; // 카드 패널 실제 표시 여부 //안건준 추가 - 0628

    public void Open()
    {
        if (panelCanvasGroup == null)
        {
            return;
        }

        if (IsPanelVisible)
        {
            return; // 이미 열려 있음
        }

        isOpen = true;
        bool shouldUseBackgroundBlur = useBackgroundBlur && backgroundBlurLayer != null;
        pausedByPanel = false;
        // 안건준 추가 - 0622 : skipPause 플래그 또는 자동궤도 모드이면 일시정지 스킵
        if (!skipPause && !IsAutoOrbitActive())
        {
            PauseGame();
        }

        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(true);
            SetOverlayPanelActive(!IsAutoOrbitActive() && !shouldUseBackgroundBlur); // 블러 사용 시 기존 딤 중복 방지
        }

        ResolveTitleVisual(); // 정식 타이틀 오브젝트 재확인
        ApplyTitleSprite(); // 레벨업/보상 타이틀 모드 반영

        panelCanvasGroup.DOKill();
        if (shouldUseBackgroundBlur)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.blocksRaycasts = false;
            panelCanvasGroup.interactable = false;
            if (openBlurRoutine != null)
            {
                StopCoroutine(openBlurRoutine);
            }

            openBlurRoutine = StartCoroutine(OpenAfterBackgroundBlurRoutine());
            return;
        }

        ShowPanelImmediate();
    }

    private IEnumerator OpenAfterBackgroundBlurRoutine()
    {
        yield return backgroundBlurLayer.ShowRoutine(); // 패널이 찍히지 않은 화면 캡처 후 블러 표시
        openBlurRoutine = null;
        ShowPanelImmediate();
    }

    private void ShowPanelImmediate()
    {
        // DOFade 가 timeScale=0 에서 충돌하거나 지연되는 문제 → 즉시 표시
        panelCanvasGroup.DOKill();
        panelCanvasGroup.alpha = 1f;
        panelCanvasGroup.blocksRaycasts = true;
        panelCanvasGroup.interactable = true;

        PlayTitleTween();
    }

    public void SetUseRewardTitle(bool useReward) // CardUI 모드별 타이틀 전환
    {
        useRewardTitle = useReward;
        ApplyTitleSprite();
    }

    public void SetUseBackgroundBlur(bool useBlur) // 보상/선택권 모드 배경 블러 전환
    {
        useBackgroundBlur = useBlur;
    }

    // 안건준 추가 - 0622 : Overlay Panel 활성/비활성 (LevelUpPanel 하위에서 이름으로 검색)
    private void SetOverlayPanelActive(bool active)
    {
        if (levelUpPanel == null)
        {
            return;
        }

        Transform overlayTransform = levelUpPanel.transform.Find("Overlay Panel");
        if (overlayTransform != null)
        {
            overlayTransform.gameObject.SetActive(active);
        }
    }

    // 안건준 추가 - 0622 : 자동궤도 활성 여부 확인
    private bool IsAutoOrbitActive()
    {
        TeamProject01.Gameplay.ConvoyController convoy =
            FindFirstObjectByType<TeamProject01.Gameplay.ConvoyController>();
        return convoy != null && convoy.IsAutoOrbitActive;
    }

    private void PauseGame()
    {
        pausedByPanel = true;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : GameSpeedController.GetDesiredTimeScale();
        Time.timeScale = 0f;
    }

    private void ResumeGame()
    {
        if (pausedByPanel)
        {
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            pausedByPanel = false;
            return;
        }

        GameSpeedController.ApplyDesiredTimeScale(); // 자동모드 2배속 유지 //안건준 수정 - 0628
    }

    public void Close()
    {
        Close(closeFadeDuration);
    }

    public void Close(float fadeDuration) // CardUI 선택 후 닫기 — 페이드 시간 지정
    {
        Close(fadeDuration, null);
    }

    // 안건준 추가 - 0622 : 닫힘 완료 후 콜백 — 연속 레벨업 대응
    public void Close(float fadeDuration, System.Action onClosed)
    {
        Close(fadeDuration, onClosed, false);
    }

    public void Close(float fadeDuration, System.Action onClosed, bool keepBackgroundBlur)
    {
        if (panelCanvasGroup == null)
        {
            CloseInstant(keepBackgroundBlur);
            onClosed?.Invoke();
            return;
        }

        if (openBlurRoutine != null)
        {
            StopCoroutine(openBlurRoutine);
            openBlurRoutine = null;
        }

        float duration = Mathf.Max(0.01f, fadeDuration);
        panelCanvasGroup.DOKill();
        if (!keepBackgroundBlur && backgroundBlurLayer != null)
        {
            backgroundBlurLayer.Hide(duration);
        }

        panelCanvasGroup.DOFade(0.0f, duration).SetUpdate(true).OnComplete(() =>
        {
            CloseInstant(keepBackgroundBlur);
            onClosed?.Invoke(); // 완전히 닫힌 후 호출
        });
    }

    private void PlayTitleTween()
    {
        RectTransform target = ResolveTitleVisual();
        if (target == null)
        {
            return;
        }

        target.localScale = Vector3.zero;
        target.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private RectTransform ResolveTitleVisual()
    {
        if (titleVisual != null)
        {
            return titleVisual;
        }

        Transform title = FindTitleTransform("LevelUpTitleImage") ?? FindTitleTransform("LevelUpText");
        titleVisual = title as RectTransform; // 생성하지 않고 기존 오브젝트만 사용
        return titleVisual;
    }

    private void CacheTitleSprites()
    {
        Image image = ResolveTitleImage();
        if (image != null && levelUpTitleSprite == null)
        {
            levelUpTitleSprite = image.sprite; // 씬에 배치된 기본 레벨업 이미지 보관
        }

        if (rewardTitleSprite == null)
        {
            rewardTitleSprite = Resources.Load<Sprite>(RewardTitleResourcePath); // 보상 선택 이미지
        }
    }

    private void ApplyTitleSprite()
    {
        CacheTitleSprites();
        Image image = ResolveTitleImage();
        if (image == null)
        {
            return; // 텍스트 타이틀 씬은 기존 표시 유지
        }

        Sprite target = useRewardTitle ? rewardTitleSprite : levelUpTitleSprite;
        if (target == null)
        {
            return; // 연결 이미지 없음
        }

        image.sprite = target;
        image.color = Color.white;
        image.preserveAspect = true;
    }

    private Image ResolveTitleImage()
    {
        if (cachedTitleImage != null)
        {
            return cachedTitleImage;
        }

        RectTransform target = ResolveTitleVisual();
        cachedTitleImage = target != null ? target.GetComponent<Image>() : null;
        return cachedTitleImage;
    }

    private Transform FindTitleTransform(string objectName)
    {
        RectTransform[] children = GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == objectName)
            {
                return children[i];
            }
        }

        return null;
    }

    private void CloseInstant()
    {
        CloseInstant(false);
    }

    private void CloseInstant(bool keepBackgroundBlur)
    {
        isOpen = false;
        ResumeGame();

        if (levelUpPanel != null && levelUpPanel != gameObject)
        {
            levelUpPanel.SetActive(false);
        }

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0.0f;
            panelCanvasGroup.blocksRaycasts = false;
            panelCanvasGroup.interactable = false;
        }

        if (!keepBackgroundBlur && backgroundBlurLayer != null)
        {
            backgroundBlurLayer.HideImmediate(true);
        }
    }
}
