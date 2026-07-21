using TeamProject01.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

// 타이틀 씬에 배치 권장 — 없으면 런타임 자동 생성 (DontDestroyOnLoad, 전 씬 유지)
// 자동궤도(자동모드) ON일 때만 게임 창을 내려도 백그라운드에서 진행
public sealed class GameManager : AudioSingleton<GameManager>
{
    [SerializeField] private bool logBackgroundStateChanges; // true면 runInBackground 변경 시 콘솔 로그

    private ConvoyController cachedConvoy; // FindObjectByType 호출 줄이기 위한 Convoy 캐시
    private float nextConvoySearchTime; // 다음 Convoy 검색 허용 시각 (unscaledTime)
    private bool lastAutoOrbitActive; // 직전 프레임 자동궤도 상태 — 변경 감지용
    private bool lastRunInBackground; // 직전에 적용한 Application.runInBackground 값

    private const float ConvoySearchInterval = 0.25f; // Convoy 없을 때 재검색 간격(초)

    // 씬에 GameManager가 없을 때 DDOL 인스턴스 보장 (테스트 씬 직접 실행·빌드 첫 씬 대응)
    public static GameManager EnsureExists()
    {
        GameManager manager = Instance;
        if (manager != null)
        {
            return manager;
        }

        manager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        if (manager != null)
        {
            return Instance ?? manager; // Awake 전이면 씬 인스턴스 반환
        }

        GameObject go = new GameObject("GameManager");
        DontDestroyOnLoad(go);
        return go.AddComponent<GameManager>();
    }

    // 타이틀/스테이지/테스트 씬 직접 Play·빌드 시 TitleScene을 거치지 않아도 부트스트랩
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapDirectPlaySceneGameManager()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (AudioSceneName.GetBGMType(scene.name) == BGMType.None)
        {
            return; // BGM 대상이 아닌 씬은 생략 (AudioManager와 동일 기준)
        }

        EnsureExists();
    }

    protected override void Awake()
    {
        base.Awake(); // AudioSingleton — 최초 인스턴스만 DontDestroyOnLoad

        if (!IsActiveSingleton)
        {
            return; // 중복 GameManager는 base.Awake에서 파괴됨
        }

        Application.runInBackground = false; // 기본값: 창 비활성 시 Unity 기본 동작(일시 정지)
        lastRunInBackground = false;
        lastAutoOrbitActive = false;
    }

    private void OnEnable()
    {
        if (!IsActiveSingleton)
        {
            return;
        }

        SceneManager.sceneLoaded += HandleSceneLoaded; // 씬 전환 시 Convoy 캐시·백그라운드 상태 갱신
        RefreshBackgroundRunState(true); // 활성화 직후 한 번 강제 적용
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded; // 구독 해제 — 파괴·비활성 시 누수 방지
    }

    private void Update()
    {
        if (!IsActiveSingleton)
        {
            return;
        }

        RefreshBackgroundRunState(); // 자동궤도 토글·씬 진입 후 상태 변화를 매 프레임 확인
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        cachedConvoy = null; // 이전 씬 Convoy 참조 무효화
        nextConvoySearchTime = 0f; // 새 씬에서 즉시 Convoy 재검색
        RefreshBackgroundRunState(true); // 타이틀↔스테이지 전환 시 runInBackground 재설정
    }

    private void RefreshBackgroundRunState(bool force = false)
    {
        bool autoActive = IsAutoOrbitActive(); // 자동궤도 ON 여부
        bool shouldRunInBackground = autoActive; // 자동모드일 때만 백그라운드 실행 허용

        if (!force && autoActive == lastAutoOrbitActive && shouldRunInBackground == lastRunInBackground)
        {
            return; // 상태 변화 없으면 Application.runInBackground 재설정 생략
        }

        lastAutoOrbitActive = autoActive;
        lastRunInBackground = shouldRunInBackground;
        Application.runInBackground = shouldRunInBackground; // true: 창 내려도 Update·게임 로직 계속

        if (logBackgroundStateChanges)
        {
            Debug.Log(
                $"[GameManager] runInBackground={shouldRunInBackground} (autoOrbit={autoActive}, scene={SceneManager.GetActiveScene().name})",
                this);
        }
    }

    private bool IsAutoOrbitActive()
    {
        ConvoyController convoy = ResolveConvoyController();
        return convoy != null && convoy.IsAutoOrbitActive; // 스테이지 Convoy + HUD 자동궤도 선택 상태
    }

    private ConvoyController ResolveConvoyController()
    {
        if (cachedConvoy != null)
        {
            return cachedConvoy; // 캐시 hit — FindObjectByType 생략
        }

        if (Time.unscaledTime < nextConvoySearchTime)
        {
            return null; // 검색 쿨다운 중 — 타이틀 등 Convoy 없는 씬에서 부하 절감
        }

        nextConvoySearchTime = Time.unscaledTime + ConvoySearchInterval;
        cachedConvoy = FindFirstObjectByType<ConvoyController>(FindObjectsInactive.Include); // 스테이지 플레이어 Convoy
        return cachedConvoy;
    }
}
