using System; // 예외 처리·문자열 비교용
using System.Collections.Generic; // List<T> — 지렁이 ID·세그먼트 강화 목록
using Newtonsoft.Json; // C# 객체 ↔ JSON 변환 (Newtonsoft 패키지)
using UnityEngine;
using UnityEngine.SceneManagement; // 씬 이름으로 Stage 여부 판별

namespace TeamProject01.Gameplay
{
    // Newtonsoft JSON 영구 저장 — 타이틀 메타 + 판 중 이어하기
    [DefaultExecutionOrder(-100)] // WaveController.Start()보다 먼저 실행 — 다이아·강화 복원 (웨이브는 복원 안 함)
    public sealed class SaveData : MonoBehaviour // PlayerPrefs JSON 저장/복원 매니저
    {
        private const int SaveVersion = 3; // 저장 포맷 버전 (v3: 타이틀 메타·영구 강화·최고 웨이브 포함)
        private const string DefaultPlayerPrefsKey = "TeamProject01.RunSave.v1"; // Instance 없을 때 fallback 키

        public static SaveData Instance { get; private set; } // 싱글톤 참조

        [Header("Save")]
        [SerializeField] private string playerPrefsKey = DefaultPlayerPrefsKey; // JSON 저장 PlayerPrefs 키
        [SerializeField] private bool persistAcrossScenes = true; // true면 DontDestroyOnLoad — 타이틀↔스테이지 유지
        [SerializeField] private bool autoSaveOnChange = true; // 웨이브·스탯·메타 변경 시 자동 저장
        [SerializeField] private bool loadOnStageStart = true; // Stage Start()에서 저장 데이터 복원
        [SerializeField] private bool clearSaveOnRunEnd = false; // true면 판 종료 시 JSON 전체 삭제 (레거시 옵션)
        [SerializeField] private bool saveOnApplicationPause = true; // 모바일 백그라운드 전환 시 저장
        [SerializeField] private bool saveOnApplicationQuit = true; // 앱/에디터 종료 시 저장

        private RunSavePayload cachedPayload; // 메모리 캐시 — PlayerPrefs 재읽기 최소화
        private bool hasCachedPayload; // cachedPayload 유효 여부
        private bool suppressAutoSave; // 복원 중 StatsChanged/MetaChanged → 저장 루프 방지
        private bool eventsSubscribed; // 스테이지 런타임 이벤트 중복 구독 방지
        private bool metaEventsSubscribed; // MetaProgressionManager 이벤트 구독 여부
        private MetaProgressionManager subscribedMeta; // 현재 구독 중인 Meta 인스턴스
        private WaveController waveController; // 웨이브 번호 조회·기록용
        private CoreStatProvider coreStats; // 런 다이아·세그먼트 강화 조회/복원용

        [Serializable]
        private sealed class RunSavePayload // PlayerPrefs에 Newtonsoft JSON으로 직렬화되는 구조체
        {
            public int Version; // 저장 포맷 버전 (마이그레이션·호환 확인)
            public bool HasActiveRun; // true = 판 중 이어하기 (세그먼트·런 다이아 복원 대상)
            public int CurrentWave = 1; // 저장 당시 웨이브 — 기록용, 게임 시작 시 복원하지 않음
            public string MapId = MetaMapIds.Map1; // 판 당시 맵 (맵 불일치 시 판 데이터 복원 생략)
            public int OwnedDiamond; // 타이틀 보유 다이아
            public int CurrentRunDiamond; // 이번 판 획득 다이아 (HasActiveRun일 때만 복원)
            public string SelectedWormId = MetaWormIds.Basic; // 선택 플레이 캐릭터 ID
            public string SelectedMapId = MetaMapIds.Map1; // 타이틀에서 선택한 맵 ID
            public int HighestReachedWave; // 최고 도달 웨이브 기록
            public List<string> UnlockedWormIds = new List<string>(); // 해금한 지렁이 ID 목록
            public List<SegmentUpgradeData> SegmentUpgrades = new List<SegmentUpgradeData>(); // 판 중 세그먼트 강화 누적
            public int GoldBonusLevel; // 타이틀 영구 강화 — 골드 보너스 단계
            public int DiamondBonusLevel; // 타이틀 영구 강화 — 다이아 보너스 단계
            public int TurnBonusLevel; // 타이틀 영구 강화 — 회전력 단계
            public int CollisionForceLevel; // 타이틀 영구 강화 — 충돌힘 단계
            public int BaseAttackLevel; // 타이틀 영구 강화 — 기본 공격력 단계
            public int AttackSpeedLevel; // 타이틀 영구 강화 — 공격속도 단계
            public int NexusMaxHpLevel; // 타이틀 영구 강화 — 넥서스 체력 단계
            public int NexusRegenLevel; // 타이틀 영구 강화 — 넥서스 회복 단계
            public long SavedAtUnixSeconds; // 저장 시각 (UTC Unix timestamp)
        }

        public static SaveData EnsureExists() // 씬에 SaveData가 없으면 DDOL 오브젝트 생성
        {
            if (Instance != null)
            {
                return Instance; // 이미 싱글톤 있음
            }

            SaveData existing = FindFirstObjectByType<SaveData>(FindObjectsInactive.Include);
            if (existing != null)
            {
                return existing; // 비활성 포함 씬 검색 결과
            }

            GameObject host = new GameObject(nameof(SaveData)); // 런타임 호스트 오브젝트
            DontDestroyOnLoad(host); // 씬 전환 후에도 유지
            return host.AddComponent<SaveData>(); // 컴포넌트 부착 후 Awake 실행
        }

        public static bool HasSavedProfile() // Newtonsoft JSON 저장 파일 존재 여부 (영구 프로필)
        {
            EnsureExists(); // Instance·캐시 준비
            if (Instance != null && Instance.hasCachedPayload && Instance.cachedPayload != null)
            {
                return true; // 메모리 캐시에 데이터 있음
            }

            return TryReadPayloadFromDiskStatic(out RunSavePayload _); // 디스크에서 확인
        }

        public static bool HasSavedRun() // 진행 중인 판 이어하기 저장 여부
        {
            if (Instance != null && Instance.hasCachedPayload && Instance.cachedPayload.HasActiveRun)
            {
                return true; // 캐시에 활성 런 있음
            }

            return TryReadPayloadFromDiskStatic(out RunSavePayload payload) && payload.HasActiveRun;
        }

        private void Awake() // 싱글톤 등록 + PlayerPrefs 선읽기
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); // 중복 SaveData 제거
                return;
            }

            Instance = this; // 전역 참조 등록
            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject); // 타이틀·스테이지 씬 전환 시 파괴 방지
            }

            ReadCacheFromDisk(); // PlayerPrefs → 메모리 캐시
        }

        private void Start() // 참조 해결·이벤트 구독·Stage 복원
        {
            ResolveReferences(); // CoreStatProvider, WaveController 탐색
            TrySubscribeMetaEvents(); // MetaProgressionManager 변경 감지 시작

            if (loadOnStageStart && IsStageScene(SceneManager.GetActiveScene()))
            {
                TryApplyLoadedRun(); // Stage 씬에서만 저장 데이터 복원
            }

            SubscribeRuntimeEvents(); // 웨이브·스탯 변경 감지
            if (autoSaveOnChange && HasSavedProfile())
            {
                SaveCurrentRun(); // 기존 저장이 있으면 최신 상태로 한 번 갱신
            }
        }

        private void Update()
        {
            TrySubscribeMetaEvents(); // Meta Awake 이후 구독 보장 (타이틀 진입 순서 대응)
        }

        private void OnDestroy() // 씬 종료 시 이벤트 해제 + 싱글톤 정리
        {
            UnsubscribeRuntimeEvents();
            UnsubscribeMetaEvents();
            if (Instance == this)
            {
                Instance = null; // 참조 해제
            }
        }

        private void OnApplicationPause(bool paused) // 모바일: 홈 버튼 등 백그라운드 전환
        {
            if (!paused || !saveOnApplicationPause || !autoSaveOnChange)
            {
                return; // 포그라운드 복귀 또는 옵션 꺼짐
            }

            SaveCurrentRun(); // 백그라운드 직전 저장
        }

        private void OnApplicationQuit() // PC/에디터 Play 종료
        {
            if (!saveOnApplicationQuit || !autoSaveOnChange)
            {
                return; // 옵션 꺼짐
            }

            SaveCurrentRun(); // 종료 직전 저장
        }

        public static bool TryApplyMetaSnapshot(MetaProgressionManager meta) // 타이틀 진입: JSON → Meta 영구 복원
        {
            EnsureExists(); // SaveData 인스턴스 보장
            if (meta == null || !TryReadPayloadFromDiskStatic(out RunSavePayload payload))
            {
                return false; // Meta 없음 / 저장 없음
            }

            ApplyMetaFields(meta, payload); // 다이아·지렁이·강화·맵·최고 웨이브 반영
            Debug.Log(
                $"[SaveData] 타이틀 메타 복원 — 다이아 {payload.OwnedDiamond}, 최고 웨이브 {payload.HighestReachedWave}, 지렁이 {payload.UnlockedWormIds?.Count ?? 0}종, 강화 합 {GetTotalUpgradeLevel(payload)}");
            return true; // 복원 성공
        }

        public void SaveCurrentRun() // 현재 상태를 Newtonsoft JSON으로 PlayerPrefs에 저장
        {
            if (suppressAutoSave || string.IsNullOrWhiteSpace(playerPrefsKey))
            {
                return; // 복원 중이거나 키 없음
            }

            ResolveReferences(); // 참조 보강
            RunSavePayload payload = BuildPayloadFromRuntime(); // 런타임 → 저장 구조체
            if (payload == null)
            {
                return; // 수집 실패
            }

            WritePayload(payload); // JSON 직렬화 후 디스크 기록
        }

        public bool TryApplyLoadedRun() // Stage 씬: 저장 데이터 복원 (메타 + HasActiveRun 시 판 데이터)
        {
            if (!TryGetCachedPayload(out RunSavePayload payload))
            {
                return false; // 저장 없음
            }

            suppressAutoSave = true; // 복원 중 자동 저장 차단
            ResolveReferences();

            MetaProgressionManager meta = MetaProgressionManager.Active;
            if (meta != null)
            {
                ApplyMetaFields(meta, payload); // 스테이지에 Meta 있을 때 메타 필드 반영
            }

            bool runRestored = false; // 판 중 데이터 복원 여부
            if (payload.HasActiveRun && IsMapCompatible(payload.MapId))
            {
                if (coreStats != null)
                {
                    coreStats.CurrentRunDiamond = Mathf.Max(0, payload.CurrentRunDiamond); // 런 다이아 복원
                    coreStats.ApplySegmentUpgradeSnapshot(payload.SegmentUpgrades?.ToArray()); // 세그먼트 강화 복원
                }

                runRestored = true;
            }
            else if (payload.HasActiveRun)
            {
                Debug.Log($"[SaveData] 맵 불일치로 판 복원 생략 (저장={payload.MapId}, 현재={ResolveCurrentMapId()})");
            }

            suppressAutoSave = false; // 자동 저장 재개
            Debug.Log(
                $"[SaveData] 복원 — 메타 다이아 {payload.OwnedDiamond}, 판 복원 {(runRestored ? "O" : "X")}, 기록 웨이브 {payload.CurrentWave}(시작 1), 세그먼트 {payload.SegmentUpgrades?.Count ?? 0}개");
            return true; // 메타 복원은 성공으로 처리
        }

        public void ClearSave() // PlayerPrefs JSON 전체 삭제 + 메모리 캐시 초기화
        {
            if (!string.IsNullOrWhiteSpace(playerPrefsKey) && PlayerPrefs.HasKey(playerPrefsKey))
            {
                PlayerPrefs.DeleteKey(playerPrefsKey); // 키 삭제
                PlayerPrefs.Save(); // 즉시 반영
            }

            cachedPayload = null;
            hasCachedPayload = false;
        }

        public void ClearSaveOnRunFinished() // Inspector clearSaveOnRunEnd 옵션에 따른 삭제
        {
            if (!clearSaveOnRunEnd)
            {
                return; // 옵션 꺼짐 — 영구 저장 유지
            }

            ClearSave(); // JSON 전체 삭제
        }

        public static void NotifyRunFinished() // RunResultController — 판 종료 알림
        {
            if (Instance != null)
            {
                if (Instance.clearSaveOnRunEnd)
                {
                    Instance.ClearSave(); // 레거시: 전체 삭제
                }
                else
                {
                    Instance.FinalizeActiveRun(); // 기본: 판 중 데이터만 정리, 메타 유지
                }

                return;
            }

            // Instance 없을 때 (씬 전환 직후) 디스크에서 직접 정리
            if (TryReadPayloadFromDiskStatic(out RunSavePayload payload))
            {
                FinalizePayloadActiveRun(payload);
                WritePayloadStatic(payload);
            }
        }

        private void FinalizeActiveRun() // 판 종료 — HasActiveRun=false, 런 전용 필드 초기화, JSON 유지
        {
            if (!TryGetCachedPayload(out RunSavePayload payload)
                && !TryReadPayloadFromDisk(out payload))
            {
                return; // 저장 없음
            }

            MetaProgressionManager meta = MetaProgressionManager.Active;
            if (meta != null)
            {
                CaptureMetaFields(meta, payload); // 종료 시점 최신 메타 반영
            }

            FinalizePayloadActiveRun(payload); // 판 중 필드만 초기화
            WritePayload(payload); // JSON 갱신 저장
        }

        private static void FinalizePayloadActiveRun(RunSavePayload payload) // 판 중 전용 필드 정리
        {
            if (payload == null)
            {
                return;
            }

            payload.HasActiveRun = false; // 이어하기 종료
            payload.CurrentRunDiamond = 0; // 런 다이아 초기화
            payload.SegmentUpgrades = new List<SegmentUpgradeData>(); // 세그먼트 강화 초기화
            payload.SavedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // 저장 시각 갱신
        }

        private void ReadCacheFromDisk() // Awake: PlayerPrefs → cachedPayload
        {
            hasCachedPayload = TryReadPayloadFromDisk(out RunSavePayload payload);
            if (hasCachedPayload)
            {
                cachedPayload = payload; // 메모리에 보관
            }
        }

        private bool TryGetCachedPayload(out RunSavePayload payload) // 캐시 우선, 없으면 디스크 재읽기
        {
            if (hasCachedPayload && cachedPayload != null)
            {
                payload = cachedPayload; // 메모리 캐시 반환
                return true;
            }

            if (TryReadPayloadFromDisk(out payload))
            {
                cachedPayload = payload; // 디스크 → 캐시 갱신
                hasCachedPayload = true;
                return true;
            }

            payload = null;
            return false; // 저장 없음
        }

        private bool TryReadPayloadFromDisk(out RunSavePayload payload) // 인스턴스 playerPrefsKey로 읽기
        {
            return TryReadPayloadFromDisk(playerPrefsKey, out payload);
        }

        private static bool TryReadPayloadFromDiskStatic(out RunSavePayload payload) // Instance 없을 때 디스크 읽기 (타이틀용)
        {
            string key = Instance != null ? Instance.playerPrefsKey : DefaultPlayerPrefsKey;
            return TryReadPayloadFromDisk(key, out payload);
        }

        private static bool TryReadPayloadFromDisk(string key, out RunSavePayload payload) // PlayerPrefs JSON → RunSavePayload
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(key) || !PlayerPrefs.HasKey(key))
            {
                return false; // 키 없음
            }

            string json = PlayerPrefs.GetString(key, string.Empty); // JSON 문자열 조회
            if (string.IsNullOrWhiteSpace(json))
            {
                return false; // 빈 JSON
            }

            try
            {
                payload = JsonConvert.DeserializeObject<RunSavePayload>(json); // Newtonsoft 역직렬화
                return payload != null && payload.Version > 0; // 유효 버전 확인
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveData] JSON 로드 실패: {exception.Message}");
                return false; // 파싱 실패
            }
        }

        private RunSavePayload BuildPayloadFromRuntime() // WaveController·Meta·CoreStat → 저장 구조체 조립
        {
            ResolveReferences();
            TryGetCachedPayload(out RunSavePayload cached); // 기존 캐시 (메타·판 데이터 유지용)

            bool inActiveStageRun = IsStageScene(SceneManager.GetActiveScene())
                && (waveController != null || coreStats != null); // Stage 씬에서 gameplay 참조 있음

            int wave = waveController != null ? Mathf.Max(1, waveController.CurrentStage) : cached?.CurrentWave ?? 1;

            RunSavePayload payload = new RunSavePayload
            {
                Version = SaveVersion,
                HasActiveRun = inActiveStageRun && (cached?.HasActiveRun ?? true),
                CurrentWave = wave, // 기록용 웨이브 번호
                MapId = ResolveCurrentMapId(),
                SavedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UnlockedWormIds = new List<string>(),
                SegmentUpgrades = new List<SegmentUpgradeData>()
            };

            MetaProgressionManager meta = MetaProgressionManager.Active;
            if (meta != null)
            {
                CaptureMetaFields(meta, payload); // Meta → JSON 필드 수집
            }
            else if (cached != null)
            {
                CopyMetaFields(cached, payload); // Meta 없으면 캐시 메타 유지
            }

            if (inActiveStageRun && coreStats != null)
            {
                payload.HasActiveRun = true; // Stage gameplay 중 — 판 중 저장 활성
                payload.CurrentRunDiamond = Mathf.Max(0, coreStats.CurrentRunDiamond);
                SegmentUpgradeData[] upgrades = coreStats.ExportSegmentUpgradeSnapshot();
                if (upgrades.Length > 0)
                {
                    payload.SegmentUpgrades.AddRange(upgrades);
                }
            }
            else if (cached != null)
            {
                // 타이틀 등 — 판 중 데이터는 캐시에서 유지
                payload.HasActiveRun = cached.HasActiveRun;
                payload.CurrentRunDiamond = cached.CurrentRunDiamond;
                payload.CurrentWave = cached.CurrentWave;
                if (cached.SegmentUpgrades != null && cached.SegmentUpgrades.Count > 0)
                {
                    payload.SegmentUpgrades = new List<SegmentUpgradeData>(cached.SegmentUpgrades);
                }
            }

            return payload;
        }

        private static void CaptureMetaFields(MetaProgressionManager meta, RunSavePayload payload) // MetaProgressionManager → JSON 필드
        {
            if (meta == null || payload == null)
            {
                return;
            }

            payload.OwnedDiamond = Mathf.Max(0, meta.OwnedDiamond);
            payload.SelectedWormId = MetaWormIds.Normalize(meta.SelectedWormId);
            payload.SelectedMapId = MetaMapIds.Normalize(meta.SelectedMapId);
            payload.MapId = payload.SelectedMapId; // MapId와 SelectedMapId 동기화
            payload.HighestReachedWave = Mathf.Max(0, meta.BestReachedWave);
            payload.UnlockedWormIds = CaptureUnlockedWormIds(meta);
            payload.GoldBonusLevel = meta.GoldBonusLevel;
            payload.DiamondBonusLevel = meta.DiamondBonusLevel;
            payload.TurnBonusLevel = meta.TurnBonusLevel;
            payload.CollisionForceLevel = meta.CollisionForceLevel;
            payload.BaseAttackLevel = meta.BaseAttackLevel;
            payload.AttackSpeedLevel = meta.AttackSpeedLevel;
            payload.NexusMaxHpLevel = meta.NexusMaxHpLevel;
            payload.NexusRegenLevel = meta.NexusRegenLevel;
        }

        private static void CopyMetaFields(RunSavePayload source, RunSavePayload target) // 캐시 → 새 payload 메타 복사
        {
            if (source == null || target == null)
            {
                return;
            }

            target.OwnedDiamond = source.OwnedDiamond;
            target.SelectedWormId = source.SelectedWormId;
            target.SelectedMapId = string.IsNullOrWhiteSpace(source.SelectedMapId) ? source.MapId : source.SelectedMapId;
            target.MapId = target.SelectedMapId;
            target.HighestReachedWave = source.HighestReachedWave;
            target.UnlockedWormIds = source.UnlockedWormIds != null
                ? new List<string>(source.UnlockedWormIds)
                : new List<string>();
            target.GoldBonusLevel = source.GoldBonusLevel;
            target.DiamondBonusLevel = source.DiamondBonusLevel;
            target.TurnBonusLevel = source.TurnBonusLevel;
            target.CollisionForceLevel = source.CollisionForceLevel;
            target.BaseAttackLevel = source.BaseAttackLevel;
            target.AttackSpeedLevel = source.AttackSpeedLevel;
            target.NexusMaxHpLevel = source.NexusMaxHpLevel;
            target.NexusRegenLevel = source.NexusRegenLevel;
        }

        private static List<string> CaptureUnlockedWormIds(MetaProgressionManager meta) // 해금된 지렁이 ID 목록 수집
        {
            List<string> unlocked = new List<string>();
            if (meta == null)
            {
                return unlocked;
            }

            TryAddUnlockedWorm(unlocked, meta, MetaWormIds.Basic); // 기본형
            TryAddUnlockedWorm(unlocked, meta, MetaWormIds.Attack); // 공격형
            TryAddUnlockedWorm(unlocked, meta, MetaWormIds.Mobility); // 이속형
            TryAddUnlockedWorm(unlocked, meta, MetaWormIds.Support); // 지원형
            TryAddUnlockedWorm(unlocked, meta, MetaWormIds.Magic); // 마법형
            return unlocked;
        }

        private static void TryAddUnlockedWorm(List<string> unlocked, MetaProgressionManager meta, string wormId) // 해금된 지렁이만 목록 추가
        {
            if (meta.IsWormUnlocked(wormId))
            {
                unlocked.Add(MetaWormIds.Normalize(wormId)); // ID 정규화 후 추가
            }
        }

        private static void ApplyMetaFields(MetaProgressionManager meta, RunSavePayload payload) // JSON → MetaProgressionManager 필드
        {
            if (meta == null || payload == null)
            {
                return;
            }

            meta.Diamond = Mathf.Max(0, payload.OwnedDiamond); // 보유 다이아
            meta.HighestReachedWave = Mathf.Max(0, payload.HighestReachedWave); // 최고 웨이브
            meta.SelectedWormId = MetaWormIds.Normalize(payload.SelectedWormId); // 선택 지렁이
            meta.SelectedMapId = MetaMapIds.Normalize(
                string.IsNullOrWhiteSpace(payload.SelectedMapId) ? payload.MapId : payload.SelectedMapId); // 선택 맵

            meta.AttackWormUnlocked = false; // 해금 bool 초기화 후 아래에서 재설정
            meta.MobilityWormUnlocked = false;
            meta.SupportWormUnlocked = false;
            meta.MagicWormUnlocked = false;
            meta.DefenseWormUnlocked = false; // 레거시 호환 필드
            meta.ArmedWormUnlocked = false;
            meta.ChargeWormUnlocked = false;

            if (payload.UnlockedWormIds != null)
            {
                for (int i = 0; i < payload.UnlockedWormIds.Count; i++)
                {
                    ApplyWormUnlockFlag(meta, payload.UnlockedWormIds[i], true); // 해금 목록 순회 적용
                }
            }

            int maxLevel = MetaProgressionManager.MaxUpgradeLevel;
            meta.GoldBonusLevel = Mathf.Clamp(payload.GoldBonusLevel, 0, maxLevel); // 영구 강화 8종
            meta.DiamondBonusLevel = Mathf.Clamp(payload.DiamondBonusLevel, 0, maxLevel);
            meta.TurnBonusLevel = Mathf.Clamp(payload.TurnBonusLevel, 0, maxLevel);
            meta.CollisionForceLevel = Mathf.Clamp(payload.CollisionForceLevel, 0, maxLevel);
            meta.BaseAttackLevel = Mathf.Clamp(payload.BaseAttackLevel, 0, maxLevel);
            meta.AttackSpeedLevel = Mathf.Clamp(payload.AttackSpeedLevel, 0, maxLevel);
            meta.NexusMaxHpLevel = Mathf.Clamp(payload.NexusMaxHpLevel, 0, maxLevel);
            meta.NexusRegenLevel = Mathf.Clamp(payload.NexusRegenLevel, 0, maxLevel);
        }

        private static void ApplyWormUnlockFlag(MetaProgressionManager meta, string wormId, bool unlocked) // 지렁이 ID별 해금 bool 설정
        {
            if (meta == null || !unlocked)
            {
                return;
            }

            switch (MetaWormIds.Normalize(wormId))
            {
                case MetaWormIds.Attack:
                    meta.AttackWormUnlocked = true;
                    meta.ArmedWormUnlocked = true; // 레거시 필드 동기화
                    break;
                case MetaWormIds.Mobility:
                    meta.MobilityWormUnlocked = true;
                    meta.ChargeWormUnlocked = true;
                    break;
                case MetaWormIds.Support:
                    meta.SupportWormUnlocked = true;
                    meta.DefenseWormUnlocked = true;
                    break;
                case MetaWormIds.Magic:
                    meta.MagicWormUnlocked = true;
                    break;
            }
        }

        private static int GetTotalUpgradeLevel(RunSavePayload payload) // 디버그 로그용 — 영구 강화 단계 합계
        {
            if (payload == null)
            {
                return 0;
            }

            return payload.GoldBonusLevel + payload.DiamondBonusLevel + payload.TurnBonusLevel
                + payload.CollisionForceLevel + payload.BaseAttackLevel + payload.AttackSpeedLevel
                + payload.NexusMaxHpLevel + payload.NexusRegenLevel;
        }

        private void WritePayload(RunSavePayload payload) // RunSavePayload → JSON → PlayerPrefs + 캐시 갱신
        {
            if (payload == null || string.IsNullOrWhiteSpace(playerPrefsKey))
            {
                return;
            }

            WritePayloadToKey(playerPrefsKey, payload);
            cachedPayload = payload;
            hasCachedPayload = true;
        }

        private static void WritePayloadStatic(RunSavePayload payload) // Instance 없을 때 디스크 기록
        {
            string key = Instance != null ? Instance.playerPrefsKey : DefaultPlayerPrefsKey;
            WritePayloadToKey(key, payload);
        }

        private static void WritePayloadToKey(string key, RunSavePayload payload) // Newtonsoft 직렬화 후 PlayerPrefs 저장
        {
            if (payload == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            try
            {
                string json = JsonConvert.SerializeObject(payload); // C# → JSON 문자열
                PlayerPrefs.SetString(key, json);
                PlayerPrefs.Save(); // 즉시 디스크 반영
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveData] JSON 저장 실패: {exception.Message}");
            }
        }

        private void ResolveReferences() // 씬에서 CoreStatProvider, WaveController 자동 탐색
        {
            if (coreStats == null)
            {
                coreStats = CoreStatProvider.Active != null
                    ? CoreStatProvider.Active
                    : FindFirstObjectByType<CoreStatProvider>();
            }

            if (waveController == null)
            {
                waveController = FindFirstObjectByType<WaveController>();
            }
        }

        private void SubscribeRuntimeEvents() // Stage: 웨이브·스탯 변경 → 자동 저장
        {
            if (eventsSubscribed)
            {
                return;
            }

            ResolveReferences();
            if (coreStats != null)
            {
                coreStats.StatsChanged += HandleStatsChanged; // 다이아·세그먼트 강화 변경
            }

            if (waveController != null)
            {
                waveController.CurrentStageChanged += HandleCurrentStageChanged; // 웨이브 진행 (기록용)
            }

            eventsSubscribed = true;
        }

        private void UnsubscribeRuntimeEvents() // OnDestroy 시 Stage 이벤트 해제
        {
            if (!eventsSubscribed)
            {
                return;
            }

            if (coreStats != null)
            {
                coreStats.StatsChanged -= HandleStatsChanged;
            }

            if (waveController != null)
            {
                waveController.CurrentStageChanged -= HandleCurrentStageChanged;
            }

            eventsSubscribed = false;
        }

        private void TrySubscribeMetaEvents() // MetaProgressionManager public 이벤트 구독 — 타이틀 메타 자동 저장
        {
            MetaProgressionManager meta = MetaProgressionManager.Active;
            if (meta == subscribedMeta)
            {
                return; // 이미 같은 인스턴스 구독 중
            }

            UnsubscribeMetaEvents(); // 이전 Meta 구독 해제
            if (meta == null)
            {
                return; // Meta 아직 없음
            }

            meta.DiamondChanged += HandleMetaProgressChanged;
            meta.HighestReachedWaveChanged += HandleMetaProgressChanged;
            meta.SelectedWormChanged += HandleMetaProgressChanged;
            meta.SelectedMapChanged += HandleMetaProgressChanged;
            meta.UpgradeLevelChanged += HandleMetaUpgradeChanged;
            meta.WormUnlockChanged += HandleMetaProgressChanged;
            subscribedMeta = meta;
            metaEventsSubscribed = true;
        }

        private void UnsubscribeMetaEvents() // Meta 이벤트 구독 해제
        {
            if (!metaEventsSubscribed || subscribedMeta == null)
            {
                subscribedMeta = null;
                metaEventsSubscribed = false;
                return;
            }

            subscribedMeta.DiamondChanged -= HandleMetaProgressChanged;
            subscribedMeta.HighestReachedWaveChanged -= HandleMetaProgressChanged;
            subscribedMeta.SelectedWormChanged -= HandleMetaProgressChanged;
            subscribedMeta.SelectedMapChanged -= HandleMetaProgressChanged;
            subscribedMeta.UpgradeLevelChanged -= HandleMetaUpgradeChanged;
            subscribedMeta.WormUnlockChanged -= HandleMetaProgressChanged;
            subscribedMeta = null;
            metaEventsSubscribed = false;
        }

        private void HandleMetaProgressChanged(int _) => HandleMetaProgressChanged(); // int 이벤트 오버로드
        private void HandleMetaProgressChanged(string _) => HandleMetaProgressChanged(); // string 이벤트 오버로드
        private void HandleMetaProgressChanged(string _, bool __) => HandleMetaProgressChanged(); // WormUnlockChanged (string, bool) 오버로드
        private void HandleMetaUpgradeChanged(MetaUpgradeId _, int __) => HandleMetaProgressChanged(); // UpgradeLevelChanged 래퍼

        private void HandleMetaProgressChanged() // 타이틀 메타 변경 시 Newtonsoft JSON 저장
        {
            if (!autoSaveOnChange || suppressAutoSave)
            {
                return;
            }

            SaveCurrentRun();
        }

        private void HandleStatsChanged(CoreStatData stats) // CoreStatProvider 변경 → 저장
        {
            if (!autoSaveOnChange || suppressAutoSave)
            {
                return;
            }

            SaveCurrentRun();
        }

        private void HandleCurrentStageChanged(int stage) // 웨이브 Stage 변경 → 저장 (CurrentWave 기록)
        {
            if (!autoSaveOnChange || suppressAutoSave)
            {
                return;
            }

            SaveCurrentRun();
        }

        private static string ResolveCurrentMapId() // 현재 플레이/선택 맵 ID 조회
        {
            if (RunLoadoutContext.TryGetStartBonus(out RunStartBonusData bonus)
                && !string.IsNullOrWhiteSpace(bonus.SelectedMapId))
            {
                return MetaMapIds.Normalize(bonus.SelectedMapId); // 타이틀 → 스테이지 전달값 우선
            }

            if (MetaProgressionManager.Active != null
                && !string.IsNullOrWhiteSpace(MetaProgressionManager.Active.SelectedMapId))
            {
                return MetaMapIds.Normalize(MetaProgressionManager.Active.SelectedMapId);
            }

            return MetaMapIds.Map1; // 기본 맵
        }

        private static bool IsMapCompatible(string savedMapId) // 저장된 맵과 현재 시작 맵 일치 여부
        {
            if (string.IsNullOrWhiteSpace(savedMapId))
            {
                return true; // 구버전 저장 호환
            }

            return string.Equals(
                MetaMapIds.Normalize(savedMapId),
                ResolveCurrentMapId(),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStageScene(Scene scene) // 씬 이름에 "Stage" 포함 여부 (StageScene, CoreTest_StageScene 등)
        {
            if (!scene.IsValid())
            {
                return false;
            }

            string name = scene.name ?? string.Empty;
            return name.IndexOf("Stage", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
