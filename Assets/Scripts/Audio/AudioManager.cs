using System;
using System.Collections;
using System.Collections.Generic;
using TeamProject01.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : AudioSingleton<AudioManager>
{
    [Header("AudioSource")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource; //여긴 효과음 추가할꺼 넣으면 됩니다.

    [Header("BGM 리스트")]
    [SerializeField] private BGMClipData[] bgmClips; //인스펙터에서 등록할 BGM

    [Header("BGM변경 페이드 시간")]
    [SerializeField, Min(0f)] private float bgmCrossfadeDuration = 1.2f; // 웨이브 BGM 전환 페이드 시간(초)

    [Header("BGM변경 목록")]    
    [SerializeField] private AudioClip[] stageBgmRotationClips;
    [Header("BGM변경 라운드 단위")]
    [SerializeField, Min(1)] private int stageBgmWavesPerTrack = 10;

    [Header("SFX List")]
    [SerializeField] private SFXClipData[] sfxClips; //인스펙터에서 등록할 효과음

    private static readonly HashSet<SfxVolumeListener> sfxListeners = new HashSet<SfxVolumeListener>();
    private static readonly Dictionary<int, float> sfxBaseVolumes = new Dictionary<int, float>();

    private Dictionary<BGMType, BGMClipData> bgmDictionary;
    private Dictionary<SFXType, SFXClipData> sfxDictionary;

    private BGMClipData currentBGMClip;
    private float masterVolume = 1f;
    private float bgmVolume = 1f;
    private float sfxVolume = 1f;

    private const float WaveBgmBindTimeoutSeconds = 15f;
    private WaveController waveController;
    private Coroutine waveBgmBindCoroutine;

    private AudioSource bgmSourceSecondary; // BGM 크로스페이드용 보조 소스
    private Coroutine bgmCrossfadeCoroutine;

    private int currentStageBgmRotationIndex = -1;
    private readonly BGMClipData stageRotationPlaybackCache = new BGMClipData { type = BGMType.Stage_BGM }; //안건준 추가 - 0630

    public const string BgmVolumePrefKey = "Settings.BGMVolume";
    public const string SfxVolumePrefKey = "Settings.SFXVolume";
    public const string MasterVolumePrefKey = "Settings.MasterVolume"; //안건준 추가 - 0628
    public const float DefaultVolume = 1f; // 저장값 없을 때 기본 볼륨 100% //안건준 추가 - 0629

    public float BgmVolume => bgmVolume;
    public float SfxVolume => sfxVolume;
    public float MasterVolume => masterVolume;

    public event Action<AudioClip> CurrentBgmChanged; //안건준 추가 - 0630: 현재 BGM 변경 알림 (UI 표시용)
    public AudioClip CurrentBgmClip => currentBGMClip != null ? currentBGMClip.clip : null;
    public string CurrentBgmDisplayName => CurrentBgmClip != null ? CurrentBgmClip.name : string.Empty;

    public static float GlobalSfxVolume { get; private set; } = 1f;
    public static float GlobalBgmVolume { get; private set; } = 1f;
    public static float GlobalMasterVolume { get; private set; } = 1f;

    public static AudioManager EnsureExists()
    {
        AudioManager manager = Instance;
        if (manager != null)
        {
            manager.EnsureRuntimeReady();
            manager.TryRecoverClipConfiguration(); // 클립/딕셔너리 유실 시 씬 AudioManager에서 복구 //안건준 수정 - 0629
            return manager;
        }

        manager = FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
        if (manager != null)
        {
            manager.EnsureRuntimeReady();
            return Instance ?? manager; // Awake 전이면 씬 인스턴스 반환 //안건준 수정 - 0629
        }

        Debug.LogWarning("[AudioManager] 씬에 AudioManager가 없어 런타임 생성합니다. TitleScene AudioManager를 사용하는 것을 권장합니다."); //안건준 추가 - 0629
        GameObject go = new GameObject("AudioManager");
        DontDestroyOnLoad(go);
        manager = go.AddComponent<AudioManager>();
        manager.EnsureRuntimeReady();
        return manager;
    }

    //안건준 추가 - 0630: 테스트 씬 직접 실행 시 BGM/SFX 카탈로그 로드 + 재생 + 웨이브 BGM 연동
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapDirectPlaySceneAudio()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (AudioSceneName.GetBGMType(scene.name) == BGMType.None)
        {
            return;
        }

        AudioManager manager = EnsureExists();
        manager.EnsureDirectPlaySceneReady();
    }

    //안건준 추가 - 0630: 타이틀/스테이지/테스트 씬 직접 Play 시 오디오 초기화
    public void EnsureDirectPlaySceneReady()
    {
        EnsureRuntimeReady();
        EnsureVolumePreferencesLoadedInternal();
        PlayBGMForActiveScene();
        BindSceneSfxSources(SceneManager.GetActiveScene());
        TryBeginWaveBgmBinding(SceneManager.GetActiveScene());
    }

    public static void PlayClickButtonSfx() // 타이틀/UI 공통 클릭음 — 복구 후 재생 //안건준 추가 - 0629
    {
        AudioManager manager = EnsureExists();
        if (manager == null)
        {
            Debug.LogWarning("[AudioManager] AudioManager를 찾을 수 없습니다.");
            return;
        }

        if (!manager.TryGetSfxClip(SFXType.ClickButton, out AudioClip clip, out float localVolume))
        {
            Debug.LogWarning("[AudioManager] ClickButton 클립이 없습니다. TitleScene → AudioManager → SFX List를 확인하세요.");
            return;
        }

        float effectiveVolume = manager.GetEffectiveSfxVolume(localVolume);
        if (effectiveVolume <= 0.0001f)
        {
            Debug.LogWarning(
                $"[AudioManager] 클릭음 볼륨이 0입니다. 설정에서 Master/SFX 볼륨을 확인하세요. (Master={manager.masterVolume:F2}, SFX={manager.sfxVolume:F2})");
            return;
        }

        manager.PlaySfxOneShotDirect(clip, localVolume);
    }

    public static void SetGlobalSfxVolume(float volume)
    {
        GlobalSfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(SfxVolumePrefKey, GlobalSfxVolume);
        PlayerPrefs.Save();

        AudioManager manager = EnsureExists();
        manager.sfxVolume = GlobalSfxVolume;
        manager.RefreshAllSfxSources();
    }

    public static void SetGlobalBgmVolume(float volume)
    {
        GlobalBgmVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(BgmVolumePrefKey, GlobalBgmVolume);
        PlayerPrefs.Save();

        AudioManager manager = EnsureExists();
        manager.SetBGMVolume(GlobalBgmVolume);
    }

    public static void SetGlobalMasterVolume(float volume)
    {
        GlobalMasterVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(MasterVolumePrefKey, GlobalMasterVolume);
        PlayerPrefs.Save();

        AudioManager manager = EnsureExists();
        manager.SetMasterVolume(GlobalMasterVolume);
    }

    public static void RegisterSfxListener(SfxVolumeListener listener)
    {
        if (listener == null || !sfxListeners.Add(listener))
        {
            return;
        }

        if (Instance != null)
        {
            Instance.ApplyVolumeToListener(listener);
        }
    }

    public static void UnregisterSfxListener(SfxVolumeListener listener)
    {
        if (listener == null)
        {
            return;
        }

        sfxListeners.Remove(listener);
    }

    public static void NotifySfxSourceReady(AudioSource source, float baseVolume)
    {
        if (source == null)
        {
            return;
        }

        RegisterSfxBaseVolume(source, baseVolume);

        if (Instance != null)
        {
            Instance.ApplySfxVolumeToSource(source);
        }
    }

    public static void RegisterSfxBaseVolume(AudioSource source, float baseVolume)
    {
        if (source == null)
        {
            return;
        }

        sfxBaseVolumes[source.GetInstanceID()] = Mathf.Clamp01(baseVolume);
    }

    public float GetEffectiveSfxVolume(float localVolume = 1f)
    {
        return Mathf.Clamp01(localVolume * sfxVolume * masterVolume);
    }



    private float sfxScanAccumulator;
    private const float SfxScanInterval = 0.25f; // 런타임 생성 AudioSource 탐색 주기 //안건준 추가 - 0628
    private bool volumePreferencesLoaded; // PlayerPrefs 볼륨 로드 완료 여부 //안건준 추가 - 0629

    public static void EnsureVolumePreferencesLoaded() // 설정 UI·SFX 재생 전 볼륨 선로드 //안건준 추가 - 0629
    {
        AudioManager manager = EnsureExists();
        manager?.EnsureVolumePreferencesLoadedInternal();
    }

    protected override void Awake()
    {
        AudioManager survivor = _instance as AudioManager;
        base.Awake();

        if (!IsActiveSingleton)
        {
            survivor?.AbsorbConfiguration(this); // 씬 AudioManager 설정을 DDOL 인스턴스로 이전 //안건준 수정 - 0628
            return;
        }

        EnsureRuntimeReady();
        EnsureVolumePreferencesLoadedInternal(); // Start() 전에도 볼륨 적용 //안건준 추가 - 0629
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    internal void AbsorbConfiguration(AudioManager donor)
    {
        if (donor == null)
        {
            return;
        }

        bool bgmMerged = MergeClipArrayIfNeeded(
            ref bgmClips,
            donor.bgmClips,
            HasAssignedBgmClips(),
            donor.HasAssignedBgmClips());

        bool sfxMerged = MergeClipArrayIfNeeded(
            ref sfxClips,
            donor.sfxClips,
            HasAssignedSfxClips(),
            donor.HasAssignedSfxClips());

        bool stageRotationMerged = MergeAudioClipArrayIfNeeded(
            ref stageBgmRotationClips,
            donor.stageBgmRotationClips,
            HasAssignedStageBgmRotationClips(),
            donor.HasAssignedStageBgmRotationClips());

        if (bgmMerged || sfxMerged || stageRotationMerged || NeedsDictionaryRebuild())
        {
            InitializDictionary(); // 흡수 후 딕셔너리 강제 재구성 //안건준 수정 - 0629
        }
        else if (MergeMissingBgmEntries(donor.bgmClips))
        {
            InitializDictionary();
        }

        EnsureRuntimeReady();
    }

    private void TryRecoverClipConfiguration() // DDOL 인스턴스에 클립이 없을 때 씬 AudioManager에서 복구 //안건준 추가 - 0629
    {
        if (!NeedsWaveBgmRecovery() && !NeedsStageBgmRotationRecovery() && CanPlaySfx(SFXType.ClickButton))
        {
            return;
        }

        AudioManager[] managers = FindObjectsByType<AudioManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        AudioManager bestDonor = null;
        int bestScore = CountAssignedClips(bgmClips) + CountAssignedClips(sfxClips);

        for (int i = 0; i < managers.Length; i++)
        {
            AudioManager candidate = managers[i];
            if (candidate == null || candidate == this)
            {
                continue;
            }

            int candidateScore = CountAssignedClips(candidate.bgmClips)
                + CountAssignedClips(candidate.sfxClips)
                + CountAssignedAudioClips(candidate.stageBgmRotationClips);
            if (candidateScore > bestScore)
            {
                bestDonor = candidate;
                bestScore = candidateScore;
            }
        }

        if (bestDonor != null)
        {
            AbsorbConfiguration(bestDonor);
        }

        TryMergeMissingClipsFromCatalog();
        TryMergeMissingStageRotationFromCatalog();

        if (NeedsDictionaryRebuild())
        {
            InitializDictionary();
        }
    }

    private bool NeedsStageBgmRotationRecovery()
    {
        return !HasStageBgmRotation();
    }

    private bool NeedsWaveBgmRecovery()
    {
        return !HasBgmClipInArray(BGMType.Stage_BGM)
            || !HasBgmClipInArray(BGMType.Boss_BGM)
            || !HasBgmClipInArray(BGMType.EventStage_BGM);
    }

    private bool HasBgmClipInArray(BGMType type)
    {
        if (bgmClips != null)
        {
            for (int i = 0; i < bgmClips.Length; i++)
            {
                BGMClipData entry = bgmClips[i];
                if (entry != null && entry.type == type && entry.clip != null)
                {
                    return true;
                }
            }
        }

        return bgmDictionary != null && bgmDictionary.ContainsKey(type);
    }

    private void EnsureWaveBgmClipsReady()
    {
        TryRecoverClipConfiguration();
        TryMergeMissingClipsFromCatalog();
        TryMergeMissingStageRotationFromCatalog();
        EnsureDictionaries();
    }

    private static bool MergeClipArrayIfNeeded(
        ref BGMClipData[] survivorClips,
        BGMClipData[] donorClips,
        bool survivorHasClips,
        bool donorHasClips)
    {
        if (!donorHasClips)
        {
            return false;
        }

        if (!survivorHasClips || CountAssignedClips(survivorClips) < CountAssignedClips(donorClips))
        {
            survivorClips = donorClips;
            return true;
        }

        return false;
    }

    private static bool MergeClipArrayIfNeeded(
        ref SFXClipData[] survivorClips,
        SFXClipData[] donorClips,
        bool survivorHasClips,
        bool donorHasClips)
    {
        if (!donorHasClips)
        {
            return false;
        }

        if (!survivorHasClips || CountAssignedClips(survivorClips) < CountAssignedClips(donorClips))
        {
            survivorClips = donorClips;
            return true;
        }

        return false;
    }

    private bool NeedsDictionaryRebuild() // Inspector 클립은 있는데 Dictionary만 비어 있는 경우 //안건준 추가 - 0629
    {
        if (bgmDictionary == null || sfxDictionary == null)
        {
            return true;
        }

        if (HasAssignedBgmClips() && bgmDictionary.Count == 0)
        {
            return true;
        }

        if (HasAssignedSfxClips() && !sfxDictionary.ContainsKey(SFXType.ClickButton))
        {
            return true;
        }

        return false;
    }

    private bool CanPlaySfx(SFXType type)
    {
        EnsureRuntimeReady();
        return sfxSource != null && sfxDictionary != null && sfxDictionary.ContainsKey(type);
    }

    public bool TryGetSfxClip(SFXType type, out AudioClip clip, out float localVolume) // SFX 클립 조회 (Dictionary + 배열 fallback) //안건준 추가 - 0629
    {
        clip = null;
        localVolume = 1f;
        EnsureRuntimeReady();
        TryRecoverClipConfiguration();

        if (sfxDictionary != null && sfxDictionary.TryGetValue(type, out SFXClipData clipData) && clipData.clip != null)
        {
            clip = clipData.clip;
            localVolume = clipData.volume;
            return true;
        }

        if (sfxClips == null)
        {
            return false;
        }

        for (int i = 0; i < sfxClips.Length; i++)
        {
            SFXClipData entry = sfxClips[i];
            if (entry == null || entry.type != type || entry.clip == null)
            {
                continue;
            }

            clip = entry.clip;
            localVolume = entry.volume;
            InitializDictionary();
            return true;
        }

        return false;
    }

    public void PlaySfxOneShotDirect(AudioClip clip, float localVolume = 1f) // Dictionary 없이 클립 직접 재생 //안건준 추가 - 0629
    {
        if (clip == null)
        {
            return;
        }

        EnsureRuntimeReady();
        PrepareSfxSourceForUi();
        if (sfxSource == null)
        {
            Debug.LogWarning("[AudioManager] SFX AudioSource가 없습니다.");
            return;
        }

        sfxSource.PlayOneShot(clip, GetEffectiveSfxVolume(localVolume));
    }

    private void PrepareSfxSourceForUi() // UI 효과음용 SFX Source 상태 보정 //안건준 추가 - 0629
    {
        EnsureAudioSources();
        if (sfxSource == null)
        {
            return;
        }

        sfxSource.enabled = true;
        sfxSource.mute = false;
        sfxSource.ignoreListenerPause = true;
        sfxSource.spatialBlend = 0f;
    }

    private void EnsureRuntimeReady()
    {
        EnsureAudioSources();
        TryLoadCatalogFromResourcesIfNeeded();
        EnsureDictionaries();
    }

    //안건준 추가 - 0630: 테스트 씬 직접 실행 — TitleScene AudioManager 없을 때 Resources 카탈로그 적용
    private void TryLoadCatalogFromResourcesIfNeeded()
    {
        if (!HasAssignedBgmClips() && !HasAssignedSfxClips())
        {
            AudioManagerCatalog catalog = Resources.Load<AudioManagerCatalog>("AudioManagerCatalog");
            if (catalog == null)
            {
                Debug.LogWarning("[AudioManager] Resources/AudioManagerCatalog.asset 을 찾을 수 없습니다.");
                return;
            }

            ApplyCatalogConfiguration(catalog);
            return;
        }

        TryMergeMissingClipsFromCatalog();
        TryMergeMissingStageRotationFromCatalog();
    }

    private void TryMergeMissingStageRotationFromCatalog()
    {
        if (HasStageBgmRotation())
        {
            return;
        }

        AudioManagerCatalog catalog = Resources.Load<AudioManagerCatalog>("AudioManagerCatalog");
        if (catalog == null || !HasAssignedStageBgmRotationClips(catalog.stageBgmRotationClips))
        {
            return;
        }

        stageBgmRotationClips = catalog.stageBgmRotationClips;
        if (catalog.stageBgmWavesPerTrack > 0)
        {
            stageBgmWavesPerTrack = catalog.stageBgmWavesPerTrack;
        }
    }

    private static bool HasAssignedStageBgmRotationClips(AudioClip[] clips)
    {
        if (clips == null)
        {
            return false;
        }

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void TryMergeMissingClipsFromCatalog()
    {
        AudioManagerCatalog catalog = Resources.Load<AudioManagerCatalog>("AudioManagerCatalog");
        if (catalog == null)
        {
            return;
        }

        bool bgmMerged = MergeMissingBgmEntries(catalog.bgmClips);
        bool sfxMerged = false;

        if (!HasAssignedSfxClips() && catalog.sfxClips != null && catalog.sfxClips.Length > 0)
        {
            sfxClips = catalog.sfxClips;
            sfxMerged = true;
        }

        if (bgmMerged || sfxMerged)
        {
            InitializDictionary();
        }
    }

    private bool MergeMissingBgmEntries(BGMClipData[] sourceClips)
    {
        if (sourceClips == null || sourceClips.Length == 0)
        {
            return false;
        }

        bool changed = false;

        for (int i = 0; i < sourceClips.Length; i++)
        {
            BGMClipData source = sourceClips[i];
            if (source == null || source.clip == null)
            {
                continue;
            }

            if (TryGetAssignedBgmEntry(source.type, out BGMClipData existing) && existing.clip != null)
            {
                continue;
            }

            bgmClips = UpsertBgmClipEntry(bgmClips, source);
            changed = true;
        }

        return changed;
    }

    private bool TryGetAssignedBgmEntry(BGMType type, out BGMClipData entry)
    {
        entry = null;

        if (bgmClips == null)
        {
            return false;
        }

        for (int i = 0; i < bgmClips.Length; i++)
        {
            BGMClipData candidate = bgmClips[i];
            if (candidate != null && candidate.type == type)
            {
                entry = candidate;
                return true;
            }
        }

        return false;
    }

    private static BGMClipData[] UpsertBgmClipEntry(BGMClipData[] clips, BGMClipData entry)
    {
        if (clips == null || clips.Length == 0)
        {
            return new[] { entry };
        }

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && clips[i].type == entry.type)
            {
                clips[i] = entry;
                return clips;
            }
        }

        BGMClipData[] expanded = new BGMClipData[clips.Length + 1];
        for (int i = 0; i < clips.Length; i++)
        {
            expanded[i] = clips[i];
        }

        expanded[clips.Length] = entry;
        return expanded;
    }

    private void ApplyCatalogConfiguration(AudioManagerCatalog catalog)
    {
        if (catalog == null)
        {
            return;
        }

        bool changed = false;

        if (!HasAssignedBgmClips() && catalog.bgmClips != null && catalog.bgmClips.Length > 0)
        {
            bgmClips = catalog.bgmClips;
            changed = true;
        }
        else
        {
            changed |= MergeMissingBgmEntries(catalog.bgmClips);
        }

        if (!HasAssignedSfxClips() && catalog.sfxClips != null && catalog.sfxClips.Length > 0)
        {
            sfxClips = catalog.sfxClips;
            changed = true;
        }

        if (!HasStageBgmRotation() && HasAssignedStageBgmRotationClips(catalog.stageBgmRotationClips))
        {
            stageBgmRotationClips = catalog.stageBgmRotationClips;
            if (catalog.stageBgmWavesPerTrack > 0)
            {
                stageBgmWavesPerTrack = catalog.stageBgmWavesPerTrack;
            }

            changed = true;
        }

        if (changed)
        {
            InitializDictionary();
        }
    }

    private bool HasAssignedBgmClips()
    {
        return CountAssignedClips(bgmClips) > 0;
    }

    private bool HasAssignedSfxClips()
    {
        return CountAssignedClips(sfxClips) > 0;
    }

    private static int CountAssignedClips(BGMClipData[] clips)
    {
        if (clips == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && clips[i].clip != null)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountAssignedClips(SFXClipData[] clips)
    {
        if (clips == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && clips[i].clip != null)
            {
                count++;
            }
        }

        return count;
    }

    private void EnsureDictionaries()
    {
        bool needsRebuild = bgmDictionary == null
            || sfxDictionary == null
            || (HasAssignedBgmClips() && bgmDictionary.Count == 0)
            || (HasAssignedSfxClips() && sfxDictionary.Count == 0);

        if (needsRebuild)
        {
            InitializDictionary();
        }
    }

    private void EnsureAudioSources()
    {
        if (!IsValidAudioSource(bgmSource))
        {
            bgmSource = FindChildAudioSource("BGM Source");
            if (!IsValidAudioSource(bgmSource))
            {
                bgmSource = CreateChildAudioSource("BGM Source", loop: true);
            }
        }

        if (!IsValidAudioSource(sfxSource))
        {
            sfxSource = FindChildAudioSource("SFX Source");
            if (!IsValidAudioSource(sfxSource))
            {
                sfxSource = CreateChildAudioSource("SFX Source", loop: false);
            }
        }

        if (!IsValidAudioSource(bgmSourceSecondary))
        {
            bgmSourceSecondary = FindChildAudioSource("BGM Source B");
            if (!IsValidAudioSource(bgmSourceSecondary))
            {
                bgmSourceSecondary = CreateChildAudioSource("BGM Source B", loop: true);
            }
        }
    }

    private static bool IsValidAudioSource(AudioSource source)
    {
        return source != null;
    }

    private AudioSource FindChildAudioSource(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<AudioSource>() : null;
    }

    private AudioSource CreateChildAudioSource(string childName, bool loop)
    {
        GameObject sourceObject = new GameObject(childName);
        sourceObject.transform.SetParent(transform, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true; // 게임 오버/일시정지 중 UI 효과음 재생 //안건준 추가 - 0629
        return source;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnsubscribeWaveBgm();
    }

    private void Start()
    {
        EnsureRuntimeReady();
        EnsureVolumePreferencesLoadedInternal();
        PlayBGMForActiveScene();
        BindSceneSfxSources(SceneManager.GetActiveScene());
        TryBeginWaveBgmBinding(SceneManager.GetActiveScene());
    }

    private void Update()
    {
        sfxScanAccumulator += Time.unscaledDeltaTime;
        if (sfxScanAccumulator < SfxScanInterval)
        {
            return;
        }

        sfxScanAccumulator = 0f;
        ScanUnboundSfxSources();
    }

    private void EnsureVolumePreferencesLoadedInternal() // PlayerPrefs → 전역·인스턴스 볼륨 (1회) //안건준 추가 - 0629
    {
        if (volumePreferencesLoaded)
        {
            return;
        }

        LoadVolumePreferences();
        volumePreferencesLoaded = true;
    }

    private void LoadVolumePreferences()
    {
        GlobalMasterVolume = ReadOrInitializeVolumePref(MasterVolumePrefKey, DefaultVolume);
        SetMasterVolume(GlobalMasterVolume);

        GlobalBgmVolume = ReadOrInitializeVolumePref(BgmVolumePrefKey, DefaultVolume);
        SetBGMVolume(GlobalBgmVolume);

        GlobalSfxVolume = ReadOrInitializeVolumePref(SfxVolumePrefKey, DefaultVolume);
        SetSFXVolume(GlobalSfxVolume);

        if (masterVolume <= 0.0001f || sfxVolume <= 0.0001f)
        {
            Debug.LogWarning(
                $"[AudioManager] 효과음/BGM이 꺼져 있습니다. 설정 슬라이더 확인 (Master={masterVolume:F2}, SFX={sfxVolume:F2})"); //안건준 추가 - 0629
        }
    }

    private static float ReadOrInitializeVolumePref(string key, float defaultValue) // 키 없으면 100% 저장 후 반환 //안건준 추가 - 0629
    {
        if (!PlayerPrefs.HasKey(key))
        {
            float initial = Mathf.Clamp01(defaultValue);
            PlayerPrefs.SetFloat(key, initial);
            PlayerPrefs.Save();
            return initial;
        }

        return Mathf.Clamp01(PlayerPrefs.GetFloat(key));
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryRecoverClipConfiguration(); // 타이틀/스테이지 재진입 시 클립 복구 //안건준 수정 - 0629
        EnsureRuntimeReady();
        UnsubscribeWaveBgm();
        PlayBGMForScene(scene.name);
        BindSceneSfxSources(scene);
        TryBeginWaveBgmBinding(scene);
    }

    private void BindSceneSfxSources(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        ScanUnboundSfxSources();
    }

    private void ScanUnboundSfxSources()
    {
        RefreshAllSfxSources();
    }

    private void RefreshAllSfxSources()
    {
        EnsureRuntimeReady();

        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null || ShouldSkipSfxVolumeApply(source))
            {
                continue;
            }

            EnsureSfxListener(source);
            ApplySfxVolumeToSource(source);
        }

        ApplySfxVolumeToListeners();
    }

    private void EnsureSfxListener(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        SfxVolumeListener listener = source.GetComponent<SfxVolumeListener>();
        if (listener != null)
        {
            return;
        }

        float baseVolume = GetOrCaptureBaseVolume(source);
        listener = source.gameObject.AddComponent<SfxVolumeListener>();
        listener.SetBaseVolume(baseVolume);
    }

    private void ApplySfxVolumeToSource(AudioSource source)
    {
        if (source == null || ShouldSkipSfxVolumeApply(source))
        {
            return;
        }

        float baseVolume = GetOrCaptureBaseVolume(source);
        source.volume = Mathf.Clamp01(baseVolume * sfxVolume * masterVolume);
    }

    private float GetOrCaptureBaseVolume(AudioSource source)
    {
        int id = source.GetInstanceID();
        if (sfxBaseVolumes.TryGetValue(id, out float storedBaseVolume))
        {
            return storedBaseVolume;
        }

        SfxVolumeListener listener = source.GetComponent<SfxVolumeListener>();
        float baseVolume = listener != null
            ? listener.BaseVolume
            : ReverseCalculateBaseVolume(source.volume);

        RegisterSfxBaseVolume(source, baseVolume);
        return baseVolume;
    }

    private float ReverseCalculateBaseVolume(float currentVolume)
    {
        float scale = Mathf.Max(sfxVolume * masterVolume, 0.0001f);
        return Mathf.Clamp01(currentVolume / scale);
    }

    private bool ShouldSkipSfxVolumeApply(AudioSource source)
    {
        return IsBgmSource(source) || source == sfxSource;
    }

    private bool IsBgmSource(AudioSource source)
    {
        if (source == null)
        {
            return false;
        }

        if (source == bgmSource || source == bgmSourceSecondary)
        {
            return true;
        }

        return source.gameObject.name == "BGM Source" || source.gameObject.name == "BGM Source B";
    }

    private void ApplySfxVolumeToListeners()
    {
        foreach (SfxVolumeListener listener in sfxListeners)
        {
            if (listener != null)
            {
                ApplyVolumeToListener(listener);
            }
        }
    }

    private void ApplyVolumeToListener(SfxVolumeListener listener)
    {
        listener.ApplyVolume(sfxVolume, masterVolume);
    }

    private void PlayBGMForActiveScene()
    {
        PlayBGMForScene(SceneManager.GetActiveScene().name);
    }

    //안건준 추가 - 0630: 스테이지·테스트 씬에서 WaveController 구독 시작
    private void TryBeginWaveBgmBinding(Scene scene)
    {
        if (!IsStageLikeScene(scene))
        {
            return;
        }

        if (waveController != null || waveBgmBindCoroutine != null)
        {
            return;
        }

        waveBgmBindCoroutine = StartCoroutine(BindWaveBgmRoutine());
    }

    private IEnumerator BindWaveBgmRoutine()
    {
        float elapsed = 0f;

        while (elapsed < WaveBgmBindTimeoutSeconds)
        {
            WaveController controller = FindFirstObjectByType<WaveController>();
            if (controller != null)
            {
                waveController = controller;
                waveController.RunStateChanged += HandleWaveRunStateChanged;
                waveController.CurrentStageChanged += HandleCurrentStageChanged; //안건준 추가 - 0630
                HandleWaveRunStateChanged(waveController.CurrentState);
                waveBgmBindCoroutine = null;
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.LogWarning("[AudioManager] WaveController를 찾지 못해 웨이브 BGM 연동에 실패했습니다.");
        waveBgmBindCoroutine = null;
    }

    private void UnsubscribeWaveBgm()
    {
        if (waveBgmBindCoroutine != null)
        {
            StopCoroutine(waveBgmBindCoroutine);
            waveBgmBindCoroutine = null;
        }

        if (waveController != null)
        {
            waveController.RunStateChanged -= HandleWaveRunStateChanged;
            waveController.CurrentStageChanged -= HandleCurrentStageChanged;
            waveController = null;
        }

        currentStageBgmRotationIndex = -1;
    }

    private void HandleWaveRunStateChanged(WaveController.WaveRunState state)
    {
        switch (state)
        {
            case WaveController.WaveRunState.Boss:
                PlayWaveBgm(BGMType.Boss_BGM);
                break;
            case WaveController.WaveRunState.Special:
                PlayWaveBgm(BGMType.EventStage_BGM);
                break;
            default:
                PlayStageRotationBgmForCurrentStage(forceTransition: false);
                break;
        }
    }

    //안건준 추가 - 0630: 웨이브 번호 변경 시 Normal이면 로테이션 갱신 (보스/특수 웨이브는 RunStateChanged에서 처리)
    private void HandleCurrentStageChanged(int stage)
    {
        if (waveController == null || waveController.CurrentState != WaveController.WaveRunState.Normal)
        {
            return;
        }

        PlayStageRotationBgmForCurrentStage(forceTransition: false);
    }

    //안건준 추가 - 0630: 스테이지 BGM 로테이션 — Normal 웨이브 진입 시에만 재생 (보스/특수 중에는 전환 안 함)
    private void PlayStageRotationBgmForCurrentStage(bool forceTransition)
    {
        EnsureWaveBgmClipsReady();

        if (!HasStageBgmRotation())
        {
            PlayWaveBgm(BGMType.Stage_BGM);
            return;
        }

        if (waveController == null)
        {
            PlayWaveBgm(BGMType.Stage_BGM);
            return;
        }

        int targetIndex = GetStageBgmRotationIndexForStage(waveController.CurrentStage);
        AudioClip clip = GetStageBgmRotationClip(targetIndex);
        if (clip == null)
        {
            PlayWaveBgm(BGMType.Stage_BGM);
            return;
        }

        bool indexChanged = targetIndex != currentStageBgmRotationIndex;
        currentStageBgmRotationIndex = targetIndex;
        PrepareStageRotationPlayback(clip);

        if (!forceTransition && !indexChanged && IsCurrentlyPlayingBgm(stageRotationPlaybackCache))
        {
            return;
        }

        if (!forceTransition && !indexChanged && IsAnyBgmPlaying() && IsPlayingClip(clip))
        {
            return;
        }

        if (bgmCrossfadeDuration > 0.001f && IsAnyBgmPlaying())
        {
            StartBgmCrossfade(stageRotationPlaybackCache);
            return;
        }

        StopBgmCrossfadeCoroutine();
        ApplyBgmImmediate(stageRotationPlaybackCache);
    }

    private bool IsPlayingClip(AudioClip clip)
    {
        if (clip == null || currentBGMClip == null)
        {
            return false;
        }

        return currentBGMClip.clip == clip && IsAnyBgmPlaying();
    }

    private bool HasStageBgmRotation()
    {
        if (stageBgmRotationClips == null)
        {
            return false;
        }

        for (int i = 0; i < stageBgmRotationClips.Length; i++)
        {
            if (stageBgmRotationClips[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasAssignedStageBgmRotationClips()
    {
        return HasStageBgmRotation();
    }

    private int GetStageBgmRotationIndexForStage(int stage)
    {
        int count = stageBgmRotationClips != null ? stageBgmRotationClips.Length : 0;
        if (count <= 0)
        {
            return -1;
        }

        int interval = Mathf.Max(1, stageBgmWavesPerTrack);
        return (Mathf.Max(1, stage) / interval) % count;
    }

    private AudioClip GetStageBgmRotationClip(int index)
    {
        if (stageBgmRotationClips == null || index < 0 || index >= stageBgmRotationClips.Length)
        {
            return null;
        }

        return stageBgmRotationClips[index];
    }

    private void PrepareStageRotationPlayback(AudioClip clip)
    {
        stageRotationPlaybackCache.clip = clip;
        stageRotationPlaybackCache.type = BGMType.Stage_BGM;
        stageRotationPlaybackCache.volume = GetStageBgmRotationLocalVolume();
    }

    private float GetStageBgmRotationLocalVolume()
    {
        EnsureDictionaries();
        if (bgmDictionary != null && bgmDictionary.TryGetValue(BGMType.Stage_BGM, out BGMClipData stageData))
        {
            return stageData.volume;
        }

        return 1f;
    }

    private static bool MergeAudioClipArrayIfNeeded(
        ref AudioClip[] survivorClips,
        AudioClip[] donorClips,
        bool survivorHasClips,
        bool donorHasClips)
    {
        if (!donorHasClips)
        {
            return false;
        }

        if (!survivorHasClips || CountAssignedAudioClips(survivorClips) < CountAssignedAudioClips(donorClips))
        {
            survivorClips = donorClips;
            return true;
        }

        return false;
    }

    private static int CountAssignedAudioClips(AudioClip[] clips)
    {
        if (clips == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    //안건준 추가 - 0630: StageScene·Dev 테스트 씬 여부 — 웨이브 BGM 연동 대상 판별
    private static bool IsStageLikeScene(Scene scene)
    {
        if (!scene.IsValid())
        {
            return false;
        }

        string sceneName = scene.name ?? string.Empty;
        return AudioSceneName.GetBGMType(sceneName) == BGMType.Stage_BGM;
    }

    public void PlayWaveBgm(BGMType type)
    {
        EnsureWaveBgmClipsReady();

        if (HasBgmClip(type))
        {
            PlayBGM(type, crossfade: true);
            return;
        }

        if (type != BGMType.Stage_BGM && HasBgmClip(BGMType.Stage_BGM))
        {
            Debug.LogWarning($"[AudioManager] {type} BGM 클립이 없어 Stage_BGM으로 대체합니다.");
            PlayBGM(BGMType.Stage_BGM, crossfade: true);
        }
    }

    private bool HasBgmClip(BGMType type)
    {
        EnsureRuntimeReady();
        return bgmDictionary != null && bgmDictionary.ContainsKey(type);
    }

    private void PlayBGMForScene(string sceneName)
    {
        BGMType bgmType = AudioSceneName.GetBGMType(sceneName);
        if (bgmType == BGMType.None)
        {
            return;
        }

        PlayBGM(bgmType);
    }

    //배열로 등록한 오디오 데이터를 딕셔너리에 저장
    private void InitializDictionary()
    {
        bgmDictionary = new Dictionary<BGMType, BGMClipData>();
        sfxDictionary = new Dictionary<SFXType, SFXClipData>();

        if (bgmClips != null)
        {
            for (int i = 0; i < bgmClips.Length; i++)
            {
                if (bgmClips[i] == null || bgmClips[i].clip == null)
                {
                    continue;
                }

                if (!bgmDictionary.ContainsKey(bgmClips[i].type))
                {
                    bgmDictionary.Add(bgmClips[i].type, bgmClips[i]);
                }
            }
        }

        if (sfxClips != null)
        {
            for (int i = 0; i < sfxClips.Length; i++)
            {
                if (sfxClips[i] == null || sfxClips[i].clip == null)
                {
                    continue;
                }

                if (!sfxDictionary.ContainsKey(sfxClips[i].type))
                {
                    sfxDictionary.Add(sfxClips[i].type, sfxClips[i]);
                }
            }
        }
    }
    //BGM 재생 — crossfade=true면 웨이브 전환 시 페이드 아웃/인
    public void PlayBGM(BGMType type, bool crossfade = false)
    {
        EnsureRuntimeReady();
        if (bgmSource == null || bgmDictionary == null || !bgmDictionary.ContainsKey(type))
        {
            return;
        }

        BGMClipData clipData = bgmDictionary[type];
        if (IsCurrentlyPlayingBgm(clipData))
        {
            return;
        }

        if (crossfade && bgmCrossfadeDuration > 0.001f && IsAnyBgmPlaying())
        {
            StartBgmCrossfade(clipData);
            return;
        }

        StopBgmCrossfadeCoroutine();
        ApplyBgmImmediate(clipData);
    }

    private float GetEffectiveBgmClipVolume(BGMClipData clipData)
    {
        if (clipData == null)
        {
            return bgmVolume * masterVolume;
        }

        return clipData.volume * bgmVolume * masterVolume;
    }

    private bool IsCurrentlyPlayingBgm(BGMClipData clipData)
    {
        return currentBGMClip != null
            && currentBGMClip.clip == clipData.clip
            && IsAnyBgmPlaying();
    }

    private bool IsAnyBgmPlaying()
    {
        return (bgmSource != null && bgmSource.isPlaying)
            || (bgmSourceSecondary != null && bgmSourceSecondary.isPlaying);
    }

    private void ApplyBgmImmediate(BGMClipData clipData)
    {
        if (bgmSourceSecondary != null)
        {
            bgmSourceSecondary.Stop();
            bgmSourceSecondary.clip = null;
        }

        currentBGMClip = clipData;
        bgmSource.clip = clipData.clip;
        bgmSource.volume = GetEffectiveBgmClipVolume(clipData);
        bgmSource.Play();
        NotifyCurrentBgmChanged();
    }

    private void NotifyCurrentBgmChanged()
    {
        CurrentBgmChanged?.Invoke(CurrentBgmClip);
    }

    private void StartBgmCrossfade(BGMClipData nextClip)
    {
        StopBgmCrossfadeCoroutine();
        bgmCrossfadeCoroutine = StartCoroutine(BgmCrossfadeRoutine(nextClip));
    }

    private void StopBgmCrossfadeCoroutine()
    {
        if (bgmCrossfadeCoroutine == null)
        {
            return;
        }

        StopCoroutine(bgmCrossfadeCoroutine);
        bgmCrossfadeCoroutine = null;
    }

    private IEnumerator BgmCrossfadeRoutine(BGMClipData nextClip)
    {
        EnsureAudioSources();

        AudioSource fadeOutSource = GetActiveBgmSource();
        AudioSource fadeInSource = fadeOutSource == bgmSource ? bgmSourceSecondary : bgmSource;

        float fadeOutStartVolume = fadeOutSource != null ? fadeOutSource.volume : 0f;
        float fadeInTargetVolume = GetEffectiveBgmClipVolume(nextClip);

        if (fadeInSource == null)
        {
            ApplyBgmImmediate(nextClip);
            bgmCrossfadeCoroutine = null;
            yield break;
        }

        fadeInSource.clip = nextClip.clip;
        fadeInSource.volume = 0f;
        fadeInSource.Play();

        currentBGMClip = nextClip;
        NotifyCurrentBgmChanged();

        float elapsed = 0f;
        float duration = bgmCrossfadeDuration;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = t * t * (3f - 2f * t);

            if (fadeOutSource != null && fadeOutSource.isPlaying)
            {
                fadeOutSource.volume = Mathf.Lerp(fadeOutStartVolume, 0f, smoothT);
            }

            fadeInSource.volume = Mathf.Lerp(0f, fadeInTargetVolume, smoothT);
            yield return null;
        }

        if (fadeOutSource != null)
        {
            fadeOutSource.Stop();
            fadeOutSource.clip = null;
        }

        fadeInSource.volume = fadeInTargetVolume;

        if (fadeInSource != bgmSource)
        {
            SwapActiveBgmSources();
        }

        bgmCrossfadeCoroutine = null;
    }

    private AudioSource GetActiveBgmSource()
    {
        if (bgmSource != null && bgmSource.isPlaying)
        {
            return bgmSource;
        }

        if (bgmSourceSecondary != null && bgmSourceSecondary.isPlaying)
        {
            return bgmSourceSecondary;
        }

        return bgmSource;
    }

    private void SwapActiveBgmSources()
    {
        (bgmSource, bgmSourceSecondary) = (bgmSourceSecondary, bgmSource);
    }

    //BGM 정지
    public void StopBGM()
    {
        StopBgmCrossfadeCoroutine();

        if (bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
        }

        if (bgmSourceSecondary != null)
        {
            bgmSourceSecondary.Stop();
            bgmSourceSecondary.clip = null;
        }

        currentBGMClip = null;
        NotifyCurrentBgmChanged();
    }
    //일시정지
    public void PauseBGM()
    {
        if (bgmSource != null)
        {
            bgmSource.Pause();
        }

        if (bgmSourceSecondary != null)
        {
            bgmSourceSecondary.Pause();
        }
    }
    //일시정지된 BGM 다시 재생
    public void ResumeBGM()
    {
        if (bgmSource != null)
        {
            bgmSource.UnPause();
        }

        if (bgmSourceSecondary != null)
        {
            bgmSourceSecondary.UnPause();
        }
    }

    //효과음 재생
    public void PlaySFX(SFXType type)
    {
        EnsureRuntimeReady();
        if (sfxSource == null)
        {
            return;
        }

        if (sfxDictionary == null || !sfxDictionary.ContainsKey(type))
        {
            if (HasAssignedSfxClips())
            {
                InitializDictionary(); // 클립은 있는데 Dictionary만 비어 있을 때 재구성 //안건준 수정 - 0629
            }

            if (sfxDictionary == null || !sfxDictionary.ContainsKey(type))
            {
                TryRecoverClipConfiguration(); // StageScene 등에서 빈 DDOL AudioManager가 생긴 경우 복구 //안건준 수정 - 0629
            }
        }

        if (sfxDictionary == null || !sfxDictionary.ContainsKey(type))
        {
            return;
        }

        sfxSource.ignoreListenerPause = true; // UI 클릭음은 Listener Pause 영향 받지 않게 //안건준 추가 - 0629
        PrepareSfxSourceForUi();
        SFXClipData clipData = sfxDictionary[type];
        float volume = GetEffectiveSfxVolume(clipData.volume);
        sfxSource.PlayOneShot(clipData.clip, volume);
        UpdateBGMVolume();
    }

    // UI 버튼 등 Inspector 클립 직접 재생 — 마스터·효과음 볼륨 반영 //안건준 추가 - 0628
    public void PlayUIClickSfx(AudioClip clip, float localVolume = 1f)
    {
        PlaySfxOneShotDirect(clip, localVolume);
    }

    public static void PlayUiSfxClip(AudioClip clip, float localVolume = 1f)
    {
        AudioManager manager = EnsureExists();
        if (manager == null)
        {
            return;
        }

        manager.PlayUIClickSfx(clip, localVolume);
    }

    //BGM볼륨을 변경
    public void SetBGMVolume(float volume)
    {
        bgmVolume = GlobalBgmVolume = Mathf.Clamp01(volume);
        UpdateBGMVolume();

    }
    //효과음볼륨을 변경
    public void SetSFXVolume(float volume)
    {
        sfxVolume = GlobalSfxVolume = Mathf.Clamp01(volume);
        RefreshAllSfxSources();
    }

    //전체 볼륨을 변경 — BGM·효과음 전부 //안건준 수정 - 0628
    public void SetMasterVolume(float volume)
    {
        masterVolume = GlobalMasterVolume = Mathf.Clamp01(volume);
        UpdateBGMVolume();
        RefreshAllSfxSources();
    }
    //현재 재생중인 BGM의 볼륨을 계산
    private void UpdateBGMVolume()
    {
        if (bgmSource == null)
        {
            return;
        }

        if (bgmCrossfadeCoroutine != null)
        {
            return;
        }

        float volume = currentBGMClip != null
            ? GetEffectiveBgmClipVolume(currentBGMClip)
            : bgmVolume * masterVolume;

        if (bgmSource.isPlaying)
        {
            bgmSource.volume = volume;
        }

        if (bgmSourceSecondary != null && bgmSourceSecondary.isPlaying)
        {
            bgmSourceSecondary.volume = volume;
        }
    }


}
