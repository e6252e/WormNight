using System;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class MetaProgressionManager : MonoBehaviour // 타이틀 메타 성장
    {
        public static MetaProgressionManager Active { get; private set; } // 현재 메타
        public const int MaxUpgradeLevel = 5; // 최대 강화 단계
        private const int SaveVersion = 1; // 저장 버전
        private const int DefaultStartingDiamond = 1000; // 기본 시작 다이아

        [Header("Defaults")]
        [Min(0)] public int StartingDiamond = DefaultStartingDiamond; // 신규 저장 기본 다이아

        [Header("Currency")]
        [Min(0)] public int Diamond; // 보유 다이아

        [Header("Records")]
        [Min(0)] public int HighestReachedWave; // 최고 도달 웨이브

        [Header("Save")]
        public bool LoadOnAwake = true; // 시작 시 로드
        public bool UseDefaultWhenNoSave = true; // 저장 없을 때 기본 진행도 적용
        public bool SaveOnChange = true; // 변경 시 저장
        public string PlayerPrefsKey = "TeamProject01.MetaProgression.v1"; // 저장 키

        [Header("Selection")]
        public string SelectedWormId = MetaWormIds.Basic; // 선택 지렁이
        public string SelectedMapId = MetaMapIds.Map1; // 선택 맵
        public bool AttackWormUnlocked; // 공격형 보유
        public bool MobilityWormUnlocked; // 이속형 보유
        public bool SupportWormUnlocked; // 지원형 보유
        public bool MagicWormUnlocked; // 마법형 보유
        public bool DefenseWormUnlocked; // 이전 지원형
        public bool ArmedWormUnlocked; // 이전 공격형
        public bool ChargeWormUnlocked; // 이전 이속형

        [Header("Worm Prices")]
        [Min(0)] public int AttackWormPrice = 200; // 공격형 가격
        [Min(0)] public int MobilityWormPrice = 200; // 이속형 가격
        [Min(0)] public int SupportWormPrice = 150; // 지원형 가격
        [Min(0)] public int MagicWormPrice = 250; // 마법형 가격

        [Header("Upgrade Levels")]
        [Range(0, MaxUpgradeLevel)] public int GoldBonusLevel; // 골드 보너스
        [Range(0, MaxUpgradeLevel)] public int DiamondBonusLevel; // 다이아 보너스
        [Range(0, MaxUpgradeLevel)] public int TurnBonusLevel; // 회전력
        [Range(0, MaxUpgradeLevel)] public int CollisionForceLevel; // 충돌힘
        [Range(0, MaxUpgradeLevel)] public int BaseAttackLevel; // 기본 공격력
        [Range(0, MaxUpgradeLevel)] public int AttackSpeedLevel; // 공격속도
        [Range(0, MaxUpgradeLevel)] public int NexusMaxHpLevel; // 넥서스 체력
        [Range(0, MaxUpgradeLevel)] public int NexusRegenLevel; // 넥서스 회복

        [Header("Temporary Reward Rules")]
        [Min(1)] public int WaveStepSize = 5; // 웨이브 단계 크기
        [Min(0)] public int DiamondPerWaveStep; // 단계별 다이아
        [Min(0)] public int ClearDiamondBonus; // 클리어 추가 다이아

        public event Action<int> DiamondChanged; // 다이아 변경
        public event Action<int> HighestReachedWaveChanged; // 최고 웨이브 변경
        public event Action<string> SelectedWormChanged; // 지렁이 변경
        public event Action<string> SelectedMapChanged; // 맵 변경
        public event Action<MetaUpgradeId, int> UpgradeLevelChanged; // 강화 단계 변경
        public event Action<string, bool> WormUnlockChanged; // 지렁이 보유 변경

        public int OwnedDiamond => Mathf.Max(0, Diamond); // 영구 보유 다이아 조회
        public int BestReachedWave => Mathf.Max(0, HighestReachedWave); // 최고 웨이브 조회
        public int TotalUpgradeLevel => GetTotalUpgradeLevel(); // 전체 강화 합계

        private void Awake() // 등록
        {
            Active = this; // 현재 인스턴스
            bool loaded = false; // 저장 로드 여부
            if (LoadOnAwake)
            {
                loaded = LoadProgress(); // 저장값 로드
            }

            if (LoadOnAwake && !loaded && UseDefaultWhenNoSave)
            {
                ApplyDefaultProgress(); // 저장 없으면 기본 상태
            }

            NormalizeState(); // 값 보정
        }

        private void OnDestroy() // 해제
        {
            if (Active == this)
            {
                Active = null; // 참조 제거
            }
        }

        public RunStartBonusData BuildStartBonus() // 스테이지 시작 보너스 생성
        {
            RunStartBonusData bonus = RunStartBonusData.Create(SelectedWormId, SelectedMapId); // 기본 정보
            bonus.AddValues(GetWormBonus(SelectedWormId)); // 지렁이 보너스
            bonus.AddValues(GetUpgradeBonus()); // 업그레이드 보너스
            return bonus; // 결과 반환
        }

        public void PushStartBonusToContext() // 타이틀 → 스테이지 준비
        {
            RunLoadoutContext.SetStartBonus(BuildStartBonus()); // 씬 전환 보관
        }

        public bool TrySelectWorm(string wormId) // 지렁이 선택
        {
            string normalized = NormalizeWormId(wormId); // ID 보정
            if (!IsWormUnlocked(normalized))
            {
                return false; // 잠김
            }

            SelectedWormId = normalized; // 선택
            SaveProgressIfNeeded(); // 저장
            SelectedWormChanged?.Invoke(SelectedWormId); // 알림
            return true; // 성공
        }

        public void SelectMap(string mapId) // 맵 선택 저장
        {
            string normalized = NormalizeMapId(mapId); // 선택 맵
            if (SelectedMapId == normalized)
            {
                return; // 같은 맵 중복 저장 방지
            }

            SelectedMapId = normalized; // 선택 맵
            SaveProgressIfNeeded(); // 저장
            SelectedMapChanged?.Invoke(SelectedMapId); // 알림
        }

        public bool TryPurchaseWorm(string wormId) // 지렁이 구매
        {
            string normalized = NormalizeWormId(wormId); // ID 보정
            if (IsWormUnlocked(normalized))
            {
                return true; // 이미 보유
            }

            int price = GetWormPrice(normalized); // 가격
            if (price < 0 || !TrySpendOwnedDiamond(price))
            {
                return false; // 구매 불가
            }

            SetWormUnlocked(normalized, true); // 해금
            SaveProgressIfNeeded(); // 저장
            DiamondChanged?.Invoke(OwnedDiamond); // 다이아 알림
            WormUnlockChanged?.Invoke(normalized, true); // 보유 알림
            return true; // 구매 성공
        }

        public bool TryUpgrade(MetaUpgradeId upgradeId, int baseCost) // 업그레이드 구매
        {
            int level = GetUpgradeLevel(upgradeId); // 현재 단계
            if (level >= MaxUpgradeLevel)
            {
                return false; // 최대 단계
            }

            int cost = CalculateUpgradeCost(baseCost, level); // 다음 비용
            if (!TrySpendOwnedDiamond(cost))
            {
                return false; // 재화 부족
            }

            SetUpgradeLevel(upgradeId, level + 1); // 강화
            SaveProgressIfNeeded(); // 저장
            DiamondChanged?.Invoke(OwnedDiamond); // 다이아 알림
            UpgradeLevelChanged?.Invoke(upgradeId, GetUpgradeLevel(upgradeId)); // 강화 알림
            return true; // 성공
        }

        public int ApplyRunResult(RunResultData result) // 게임 종료 보상 적용
        {
            bool recordChanged = TryRecordHighestReachedWave(result.ReachedWave); // 최고 기록 갱신
            int baseReward = result.HasExplicitEarnedDiamond ? result.EarnedDiamond : CalculateDiamondReward(result.ReachedWave, result.IsClear); // 보상
            int finalReward = result.HasExplicitEarnedDiamond ? baseReward : BuildStartBonus().ApplyDiamondGainBonus(baseReward); // 확정 보상은 재계산 금지
            GrantOwnedDiamond(finalReward); // 획득
            SaveProgressIfNeeded(); // 저장
            if (recordChanged)
            {
                HighestReachedWaveChanged?.Invoke(BestReachedWave); // 기록 알림
            }

            DiamondChanged?.Invoke(OwnedDiamond); // 알림
            return finalReward; // 실제 지급량
        }

        public bool RegisterReachedWave(int reachedWave) // 최고 웨이브 수동 반영
        {
            bool changed = TryRecordHighestReachedWave(reachedWave); // 기록 갱신
            if (!changed)
            {
                return false; // 변화 없음
            }

            SaveProgressIfNeeded(); // 저장
            HighestReachedWaveChanged?.Invoke(BestReachedWave); // 알림
            return true; // 갱신됨
        }

        public void AddDiamond(int amount) // 테스트/보상 다이아 지급
        {
            int safeAmount = Mathf.Max(0, amount); // 지급량 보정
            if (safeAmount <= 0)
            {
                return; // 지급 없음
            }

            GrantOwnedDiamond(safeAmount); // 지급
            SaveProgressIfNeeded(); // 저장
            DiamondChanged?.Invoke(OwnedDiamond); // 알림
        }

        public void ResetProgress() // 진행도 초기화
        {
            ApplyDefaultProgress(); // 기본 메타 상태
            NormalizeState(); // 보정
            SaveProgressIfNeeded(); // 저장
            NotifyAllProgressChanged(); // 전체 알림
        }

        public string BuildDebugSummary(int upgradeBaseCost) // 메타 상태 요약
        {
            NormalizeState(); // 표시 전 보정
            string saveState = HasSavedProgress() ? "저장 있음" : "저장 없음"; // 저장 상태
            string nextCost = GetLowestNextUpgradeCost(upgradeBaseCost); // 다음 비용
            return $"다이아 {OwnedDiamond}\n"
                + $"기본 다이아 {StartingDiamond}\n"
                + $"{saveState}\n"
                + $"최고 웨이브 {BestReachedWave}\n"
                + $"선택 지렁이 {SelectedWormId}\n"
                + $"선택 맵 {SelectedMapId}\n"
                + $"보유 지렁이 {BuildUnlockedWormSummary()}\n"
                + $"업그레이드 {TotalUpgradeLevel}/{MaxUpgradeLevel * 8}\n"
                + $"다음 최저 비용 {nextCost}"; // 요약
        }

        public void SaveProgress() // 메타 저장
        {
            if (string.IsNullOrWhiteSpace(PlayerPrefsKey))
            {
                return; // 키 없음
            }

            NormalizeState(); // 저장 전 보정
            PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(CreateSaveData())); // JSON 저장
            PlayerPrefs.Save(); // 즉시 반영
        }

        public bool LoadProgress() // 메타 로드
        {
            if (!HasSavedProgress())
            {
                return false; // 저장 없음
            }

            string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty); // JSON 조회
            if (string.IsNullOrWhiteSpace(json))
            {
                return false; // 빈 저장
            }

            SaveData data; // 저장값
            try
            {
                data = JsonUtility.FromJson<SaveData>(json); // 역직렬화
            }
            catch (ArgumentException)
            {
                return false; // 깨진 저장
            }

            if (data == null || data.Version <= 0)
            {
                return false; // 잘못된 저장
            }

            ApplySaveData(data); // 값 반영
            NormalizeState(); // 로드 후 보정
            NotifyAllProgressChanged(); // 로드 알림
            return true; // 성공
        }

        public bool LoadProgressOrDefault() // 저장 로드 또는 기본 진행도 적용
        {
            bool loaded = LoadProgress(); // 저장 우선
            if (!loaded)
            {
                ApplyDefaultProgress(); // 저장 없으면 기본값
                NormalizeState(); // 보정
                NotifyAllProgressChanged(); // 기본값 알림
            }

            return loaded; // 저장 로드 여부
        }

        public bool HasSavedProgress() // 저장 존재 여부
        {
            return !string.IsNullOrWhiteSpace(PlayerPrefsKey) && PlayerPrefs.HasKey(PlayerPrefsKey); // 키 확인
        }

        public void DeleteSavedProgress() // 저장 삭제
        {
            if (string.IsNullOrWhiteSpace(PlayerPrefsKey))
            {
                return; // 키 없음
            }

            PlayerPrefs.DeleteKey(PlayerPrefsKey); // 삭제
            PlayerPrefs.Save(); // 반영
        }

        public void DeleteSavedProgressAndApplyDefault() // 저장 삭제 후 런타임 기본값 적용
        {
            DeleteSavedProgress(); // 저장 삭제
            ApplyDefaultProgress(); // 기본 진행도
            NormalizeState(); // 보정
            NotifyAllProgressChanged(); // 표시 갱신
        }

        public int CalculateDiamondReward(int reachedWave, bool isClear) // 웨이브 기반 보상
        {
            int step = Mathf.Max(0, reachedWave) / Mathf.Max(1, WaveStepSize); // 도달 단계
            int reward = step * Mathf.Max(0, DiamondPerWaveStep); // 단계 보상
            if (isClear)
            {
                reward += Mathf.Max(0, ClearDiamondBonus); // 클리어 추가
            }

            return reward; // 결과 반환
        }

        public static int CalculateUpgradeCost(int baseCost, int currentLevel) // x2 비용 공식
        {
            int safeBase = Mathf.Max(0, baseCost); // 기본 비용
            int level = Mathf.Clamp(currentLevel, 0, MaxUpgradeLevel); // 단계 보정
            return safeBase * (1 << level); // 2배 증가
        }

        public bool CanUpgrade(MetaUpgradeId upgradeId, int baseCost) // 강화 가능 여부
        {
            if (IsUpgradeMaxed(upgradeId))
            {
                return false; // 최대 단계
            }

            return OwnedDiamond >= GetNextUpgradeCost(upgradeId, baseCost); // 비용 확인
        }

        public bool IsUpgradeMaxed(MetaUpgradeId upgradeId) // 최대 단계 여부
        {
            return GetUpgradeLevel(upgradeId) >= MaxUpgradeLevel; // 최대 확인
        }

        public int GetNextUpgradeCost(MetaUpgradeId upgradeId, int baseCost) // 다음 강화 비용
        {
            if (IsUpgradeMaxed(upgradeId))
            {
                return 0; // 더 이상 비용 없음
            }

            return CalculateUpgradeCost(baseCost, GetUpgradeLevel(upgradeId)); // 다음 비용
        }

        public static string GetUpgradeDisplayName(MetaUpgradeId upgradeId) // 업그레이드 이름
        {
            switch (upgradeId)
            {
                case MetaUpgradeId.GoldBonus:
                    return "골드 보너스";
                case MetaUpgradeId.DiamondBonus:
                    return "다이아 보너스";
                case MetaUpgradeId.TurnBonus:
                    return "회전력";
                case MetaUpgradeId.CollisionForce:
                    return "충돌힘";
                case MetaUpgradeId.BaseAttack:
                    return "기본 공격력";
                case MetaUpgradeId.AttackSpeed:
                    return "공격속도";
                case MetaUpgradeId.NexusMaxHp:
                    return "넥서스 체력";
                case MetaUpgradeId.NexusRegen:
                    return "넥서스 회복";
                default:
                    return "업그레이드";
            }
        }

        public static string GetUpgradeEffectText(MetaUpgradeId upgradeId, int level) // 단계 효과 문구
        {
            int clamped = Mathf.Clamp(level, 0, MaxUpgradeLevel); // 단계 보정
            if (clamped <= 0)
            {
                return "효과 없음"; // 0단계
            }

            switch (upgradeId)
            {
                case MetaUpgradeId.GoldBonus:
                case MetaUpgradeId.DiamondBonus:
                case MetaUpgradeId.AttackSpeed:
                case MetaUpgradeId.NexusMaxHp:
                    return $"+{clamped * 5}%";
                case MetaUpgradeId.TurnBonus:
                    return $"+{GetTurnBonusPercent(clamped)}%";
                case MetaUpgradeId.CollisionForce:
                    return $"+{GetCollisionForceBonusPercent(clamped)}%";
                case MetaUpgradeId.BaseAttack:
                    return $"+{clamped}";
                case MetaUpgradeId.NexusRegen:
                    return $"+{clamped * 5}/분";
                default:
                    return "효과 없음";
            }
        }

        public bool IsWormUnlocked(string wormId) // 해금 여부
        {
            switch (NormalizeWormId(wormId))
            {
                case MetaWormIds.Basic:
                    return true; // 기본 지급
                case MetaWormIds.Attack:
                    return AttackWormUnlocked || ArmedWormUnlocked; // 공격형
                case MetaWormIds.Mobility:
                    return MobilityWormUnlocked || ChargeWormUnlocked; // 이속형
                case MetaWormIds.Support:
                    return SupportWormUnlocked || DefenseWormUnlocked; // 지원형
                case MetaWormIds.Magic:
                    return MagicWormUnlocked; // 마법형
                default:
                    return false; // 미정
            }
        }

        public int GetWormPrice(string wormId) // 지렁이 가격
        {
            switch (NormalizeWormId(wormId))
            {
                case MetaWormIds.Basic:
                    return 0; // 기본
                case MetaWormIds.Attack:
                    return Mathf.Max(0, AttackWormPrice); // 공격형
                case MetaWormIds.Mobility:
                    return Mathf.Max(0, MobilityWormPrice); // 이속형
                case MetaWormIds.Support:
                    return Mathf.Max(0, SupportWormPrice); // 지원형
                case MetaWormIds.Magic:
                    return Mathf.Max(0, MagicWormPrice); // 마법형
                default:
                    return -1; // 구매 불가
            }
        }

        private void SetWormUnlocked(string wormId, bool unlocked) // 해금 저장
        {
            switch (NormalizeWormId(wormId))
            {
                case MetaWormIds.Attack:
                    AttackWormUnlocked = unlocked; // 공격형
                    ArmedWormUnlocked = unlocked; // 이전 값 동기화
                    break;
                case MetaWormIds.Mobility:
                    MobilityWormUnlocked = unlocked; // 이속형
                    ChargeWormUnlocked = unlocked; // 이전 값 동기화
                    break;
                case MetaWormIds.Support:
                    SupportWormUnlocked = unlocked; // 지원형
                    DefenseWormUnlocked = unlocked; // 이전 값 동기화
                    break;
                case MetaWormIds.Magic:
                    MagicWormUnlocked = unlocked; // 마법형
                    break;
            }
        }

        private void SaveProgressIfNeeded() // 자동 저장
        {
            if (SaveOnChange)
            {
                SaveProgress(); // 저장
            }
        }

        private SaveData CreateSaveData() // 저장 데이터 생성
        {
            return new SaveData
            {
                Version = SaveVersion,
                Diamond = OwnedDiamond,
                HighestReachedWave = BestReachedWave,
                SelectedWormId = NormalizeWormId(SelectedWormId),
                SelectedMapId = NormalizeMapId(SelectedMapId),
                AttackWormUnlocked = AttackWormUnlocked,
                MobilityWormUnlocked = MobilityWormUnlocked,
                SupportWormUnlocked = SupportWormUnlocked,
                MagicWormUnlocked = MagicWormUnlocked,
                DefenseWormUnlocked = SupportWormUnlocked || DefenseWormUnlocked,
                ArmedWormUnlocked = AttackWormUnlocked || ArmedWormUnlocked,
                ChargeWormUnlocked = MobilityWormUnlocked || ChargeWormUnlocked,
                GoldBonusLevel = GoldBonusLevel,
                DiamondBonusLevel = DiamondBonusLevel,
                TurnBonusLevel = TurnBonusLevel,
                CollisionForceLevel = CollisionForceLevel,
                BaseAttackLevel = BaseAttackLevel,
                AttackSpeedLevel = AttackSpeedLevel,
                NexusMaxHpLevel = NexusMaxHpLevel,
                NexusRegenLevel = NexusRegenLevel
            }; // 저장값
        }

        private void ApplySaveData(SaveData data) // 저장 데이터 반영
        {
            Diamond = data.Diamond; // 다이아
            HighestReachedWave = data.HighestReachedWave; // 최고 웨이브
            SelectedWormId = data.SelectedWormId; // 지렁이
            SelectedMapId = data.SelectedMapId; // 맵
            AttackWormUnlocked = data.AttackWormUnlocked; // 공격형
            MobilityWormUnlocked = data.MobilityWormUnlocked; // 이속형
            SupportWormUnlocked = data.SupportWormUnlocked; // 지원형
            MagicWormUnlocked = data.MagicWormUnlocked; // 마법형
            DefenseWormUnlocked = data.DefenseWormUnlocked; // 이전 지원형
            ArmedWormUnlocked = data.ArmedWormUnlocked; // 이전 공격형
            ChargeWormUnlocked = data.ChargeWormUnlocked; // 이전 이속형
            GoldBonusLevel = data.GoldBonusLevel; // 골드
            DiamondBonusLevel = data.DiamondBonusLevel; // 다이아
            TurnBonusLevel = data.TurnBonusLevel; // 회전
            CollisionForceLevel = data.CollisionForceLevel; // 충돌
            BaseAttackLevel = data.BaseAttackLevel; // 공격력
            AttackSpeedLevel = data.AttackSpeedLevel; // 공속
            NexusMaxHpLevel = data.NexusMaxHpLevel; // 체력
            NexusRegenLevel = data.NexusRegenLevel; // 회복
        }

        private void NormalizeState() // 전체 값 보정
        {
            StartingDiamond = Mathf.Max(0, StartingDiamond); // 기본 다이아 보정
            Diamond = OwnedDiamond; // 재화 보정
            HighestReachedWave = BestReachedWave; // 기록 보정
            MigrateLegacyWormUnlocks(); // 이전 저장값 반영
            SelectedWormId = NormalizeWormId(SelectedWormId); // 지렁이 보정
            SelectedMapId = NormalizeMapId(SelectedMapId); // 맵 보정
            if (!IsWormUnlocked(SelectedWormId))
            {
                SelectedWormId = MetaWormIds.Basic; // 잠긴 지렁이 방지
            }

            GoldBonusLevel = Mathf.Clamp(GoldBonusLevel, 0, MaxUpgradeLevel); // 골드
            DiamondBonusLevel = Mathf.Clamp(DiamondBonusLevel, 0, MaxUpgradeLevel); // 다이아
            TurnBonusLevel = Mathf.Clamp(TurnBonusLevel, 0, MaxUpgradeLevel); // 회전
            CollisionForceLevel = Mathf.Clamp(CollisionForceLevel, 0, MaxUpgradeLevel); // 충돌
            BaseAttackLevel = Mathf.Clamp(BaseAttackLevel, 0, MaxUpgradeLevel); // 공격력
            AttackSpeedLevel = Mathf.Clamp(AttackSpeedLevel, 0, MaxUpgradeLevel); // 공속
            NexusMaxHpLevel = Mathf.Clamp(NexusMaxHpLevel, 0, MaxUpgradeLevel); // 체력
            NexusRegenLevel = Mathf.Clamp(NexusRegenLevel, 0, MaxUpgradeLevel); // 회복
        }

        private void ApplyDefaultProgress() // 신규/초기화 기본 메타 상태
        {
            StartingDiamond = Mathf.Max(0, StartingDiamond); // 기본값 보정
            Diamond = StartingDiamond; // 시작 다이아
            HighestReachedWave = 0; // 최고 웨이브
            SelectedWormId = MetaWormIds.Basic; // 기본 지렁이
            SelectedMapId = MetaMapIds.Map1; // 기본 맵
            ResetWormUnlocks(); // 지렁이 보유 초기화
            ResetUpgradeLevels(); // 강화 초기화
        }

        private void ResetWormUnlocks() // 기본형 외 보유 초기화
        {
            AttackWormUnlocked = false; // 공격형
            MobilityWormUnlocked = false; // 이속형
            SupportWormUnlocked = false; // 지원형
            MagicWormUnlocked = false; // 마법형
            DefenseWormUnlocked = false; // 이전 지원형
            ArmedWormUnlocked = false; // 이전 공격형
            ChargeWormUnlocked = false; // 이전 이속형
        }

        private void ResetUpgradeLevels() // 영구 강화 초기화
        {
            GoldBonusLevel = 0; // 골드
            DiamondBonusLevel = 0; // 다이아
            TurnBonusLevel = 0; // 회전
            CollisionForceLevel = 0; // 충돌
            BaseAttackLevel = 0; // 공격력
            AttackSpeedLevel = 0; // 공속
            NexusMaxHpLevel = 0; // 체력
            NexusRegenLevel = 0; // 회복
        }

        private bool TrySpendOwnedDiamond(int amount) // 영구 다이아 지출
        {
            int cost = Mathf.Max(0, amount); // 비용 보정
            if (cost <= 0)
            {
                return true; // 무료
            }

            if (OwnedDiamond < cost)
            {
                return false; // 부족
            }

            Diamond = OwnedDiamond - cost; // 차감
            return true; // 성공
        }

        private void GrantOwnedDiamond(int amount) // 영구 다이아 획득
        {
            int safeAmount = Mathf.Max(0, amount); // 지급량 보정
            if (safeAmount <= 0)
            {
                return; // 변화 없음
            }

            Diamond = OwnedDiamond + safeAmount; // 증가
        }

        private void NotifyAllProgressChanged() // 전체 메타 변경 알림
        {
            DiamondChanged?.Invoke(OwnedDiamond); // 다이아
            HighestReachedWaveChanged?.Invoke(BestReachedWave); // 기록
            SelectedWormChanged?.Invoke(SelectedWormId); // 선택 지렁이
            SelectedMapChanged?.Invoke(SelectedMapId); // 선택 맵
            NotifyAllWormUnlocksChanged(); // 보유 지렁이
            NotifyAllUpgradeLevelsChanged(); // 강화
        }

        private void NotifyAllWormUnlocksChanged() // 지렁이 보유 알림
        {
            WormUnlockChanged?.Invoke(MetaWormIds.Basic, true); // 기본형
            WormUnlockChanged?.Invoke(MetaWormIds.Attack, IsWormUnlocked(MetaWormIds.Attack)); // 공격형
            WormUnlockChanged?.Invoke(MetaWormIds.Mobility, IsWormUnlocked(MetaWormIds.Mobility)); // 이속형
            WormUnlockChanged?.Invoke(MetaWormIds.Support, IsWormUnlocked(MetaWormIds.Support)); // 지원형
            WormUnlockChanged?.Invoke(MetaWormIds.Magic, IsWormUnlocked(MetaWormIds.Magic)); // 마법형
        }

        private void NotifyAllUpgradeLevelsChanged() // 강화 단계 알림
        {
            UpgradeLevelChanged?.Invoke(MetaUpgradeId.GoldBonus, GoldBonusLevel); // 골드
            UpgradeLevelChanged?.Invoke(MetaUpgradeId.DiamondBonus, DiamondBonusLevel); // 다이아
            UpgradeLevelChanged?.Invoke(MetaUpgradeId.TurnBonus, TurnBonusLevel); // 회전
            UpgradeLevelChanged?.Invoke(MetaUpgradeId.CollisionForce, CollisionForceLevel); // 충돌
            UpgradeLevelChanged?.Invoke(MetaUpgradeId.BaseAttack, BaseAttackLevel); // 공격력
            UpgradeLevelChanged?.Invoke(MetaUpgradeId.AttackSpeed, AttackSpeedLevel); // 공속
            UpgradeLevelChanged?.Invoke(MetaUpgradeId.NexusMaxHp, NexusMaxHpLevel); // 체력
            UpgradeLevelChanged?.Invoke(MetaUpgradeId.NexusRegen, NexusRegenLevel); // 회복
        }

        private string BuildUnlockedWormSummary() // 보유 지렁이 요약
        {
            string summary = "기본"; // 기본형
            if (IsWormUnlocked(MetaWormIds.Attack))
            {
                summary += ", 공격"; // 공격형
            }

            if (IsWormUnlocked(MetaWormIds.Mobility))
            {
                summary += ", 이속"; // 이속형
            }

            if (IsWormUnlocked(MetaWormIds.Support))
            {
                summary += ", 지원"; // 지원형
            }

            if (IsWormUnlocked(MetaWormIds.Magic))
            {
                summary += ", 마법"; // 마법형
            }

            return summary; // 결과
        }

        private int GetTotalUpgradeLevel() // 업그레이드 합계
        {
            return GoldBonusLevel
                + DiamondBonusLevel
                + TurnBonusLevel
                + CollisionForceLevel
                + BaseAttackLevel
                + AttackSpeedLevel
                + NexusMaxHpLevel
                + NexusRegenLevel; // 합계
        }

        private string GetLowestNextUpgradeCost(int upgradeBaseCost) // 다음 최저 비용
        {
            int lowest = int.MaxValue; // 후보
            for (int i = 0; i < 8; i++)
            {
                MetaUpgradeId upgradeId = (MetaUpgradeId)i; // ID
                if (IsUpgradeMaxed(upgradeId))
                {
                    continue; // 최대 제외
                }

                lowest = Mathf.Min(lowest, GetNextUpgradeCost(upgradeId, upgradeBaseCost)); // 최저값
            }

            return lowest == int.MaxValue ? "MAX" : lowest.ToString(); // 표시값
        }

        private RunStartBonusData GetWormBonus(string wormId) // 지렁이 보너스
        {
            RunStartBonusData bonus = default; // 값 준비
            switch (NormalizeWormId(wormId))
            {
                case MetaWormIds.Support:
                    bonus.NexusMaxHealthPercentBonus = 0.15f; // 체력 +15%
                    bonus.NexusRegenPerMinuteBonus = 5f; // 분당 회복 +5
                    break;
                case MetaWormIds.Attack:
                    bonus.BaseAttackFlatBonus = 1; // 공격력 +1
                    bonus.AttackSpeedPercentBonus = 0.05f; // 공속 +5%
                    break;
                case MetaWormIds.Mobility:
                    bonus.TurnPercentBonus = 0.10f; // 회전 +10%
                    bonus.CollisionForcePercentBonus = 0.10f; // 충돌힘 +10%
                    break;
            }

            return bonus; // 결과 반환
        }

        private RunStartBonusData GetUpgradeBonus() // 업그레이드 보너스
        {
            RunStartBonusData bonus = default; // 값 준비
            bonus.GoldGainPercentBonus = GetGoldBonus(GoldBonusLevel); // 골드
            bonus.DiamondGainPercentBonus = GetDiamondBonus(DiamondBonusLevel); // 다이아
            bonus.TurnPercentBonus = GetTurnBonus(TurnBonusLevel); // 회전
            bonus.CollisionForcePercentBonus = GetCollisionForceBonus(CollisionForceLevel); // 충돌
            bonus.BaseAttackFlatBonus = Mathf.Clamp(BaseAttackLevel, 0, MaxUpgradeLevel); // 공격력
            bonus.AttackSpeedPercentBonus = GetFiveStepPercent(AttackSpeedLevel); // 공속
            bonus.NexusMaxHealthPercentBonus = GetFiveStepPercent(NexusMaxHpLevel); // 넥서스 체력
            bonus.NexusRegenPerMinuteBonus = Mathf.Clamp(NexusRegenLevel, 0, MaxUpgradeLevel) * 5f; // 분당 회복
            return bonus; // 결과 반환
        }

        public int GetUpgradeLevel(MetaUpgradeId upgradeId) // 단계 조회
        {
            switch (upgradeId)
            {
                case MetaUpgradeId.GoldBonus:
                    return GoldBonusLevel;
                case MetaUpgradeId.DiamondBonus:
                    return DiamondBonusLevel;
                case MetaUpgradeId.TurnBonus:
                    return TurnBonusLevel;
                case MetaUpgradeId.CollisionForce:
                    return CollisionForceLevel;
                case MetaUpgradeId.BaseAttack:
                    return BaseAttackLevel;
                case MetaUpgradeId.AttackSpeed:
                    return AttackSpeedLevel;
                case MetaUpgradeId.NexusMaxHp:
                    return NexusMaxHpLevel;
                case MetaUpgradeId.NexusRegen:
                    return NexusRegenLevel;
                default:
                    return 0;
            }
        }

        private void SetUpgradeLevel(MetaUpgradeId upgradeId, int level) // 단계 저장
        {
            int clamped = Mathf.Clamp(level, 0, MaxUpgradeLevel); // 단계 보정
            switch (upgradeId)
            {
                case MetaUpgradeId.GoldBonus:
                    GoldBonusLevel = clamped;
                    break;
                case MetaUpgradeId.DiamondBonus:
                    DiamondBonusLevel = clamped;
                    break;
                case MetaUpgradeId.TurnBonus:
                    TurnBonusLevel = clamped;
                    break;
                case MetaUpgradeId.CollisionForce:
                    CollisionForceLevel = clamped;
                    break;
                case MetaUpgradeId.BaseAttack:
                    BaseAttackLevel = clamped;
                    break;
                case MetaUpgradeId.AttackSpeed:
                    AttackSpeedLevel = clamped;
                    break;
                case MetaUpgradeId.NexusMaxHp:
                    NexusMaxHpLevel = clamped;
                    break;
                case MetaUpgradeId.NexusRegen:
                    NexusRegenLevel = clamped;
                    break;
            }
        }

        private static string NormalizeWormId(string wormId) // 지렁이 ID 보정
        {
            return MetaWormIds.Normalize(wormId); // 공용 보정
        }

        private void MigrateLegacyWormUnlocks() // 이전 해금값 이전
        {
            SupportWormUnlocked |= DefenseWormUnlocked; // 방어형 → 지원형
            AttackWormUnlocked |= ArmedWormUnlocked; // 무장형 → 공격형
            MobilityWormUnlocked |= ChargeWormUnlocked; // 돌격형 → 이속형
        }

        private bool TryRecordHighestReachedWave(int reachedWave) // 최고 기록 내부 갱신
        {
            int safeWave = Mathf.Max(0, reachedWave); // 음수 방지
            if (safeWave <= HighestReachedWave)
            {
                return false; // 기존 기록 유지
            }

            HighestReachedWave = safeWave; // 새 기록
            return true; // 변경됨
        }

        private static string NormalizeMapId(string mapId) // 맵 ID 보정
        {
            return MetaMapIds.Normalize(mapId); // 공용 보정
        }

        private static float GetFiveStepPercent(int level) // 5/10/15/20/25%
        {
            return Mathf.Clamp(level, 0, MaxUpgradeLevel) * 0.05f; // 단계당 5%
        }

        private static float GetGoldBonus(int level) // 골드 보너스
        {
            return GetFiveStepPercent(level); // 5단계
        }

        private static float GetDiamondBonus(int level) // 다이아 보너스
        {
            return GetFiveStepPercent(level); // 5단계
        }

        private static float GetTurnBonus(int level) // 회전력 보너스
        {
            return GetTurnBonusPercent(level) * 0.01f; // 퍼센트 변환
        }

        private static float GetCollisionForceBonus(int level) // 충돌힘 보너스
        {
            return GetCollisionForceBonusPercent(level) * 0.01f; // 퍼센트 변환
        }

        private static int GetTurnBonusPercent(int level) // 회전력 표시값
        {
            int[] values = { 0, 1, 3, 5, 7, 10 }; // 기획값
            return values[Mathf.Clamp(level, 0, MaxUpgradeLevel)]; // 결과
        }

        private static int GetCollisionForceBonusPercent(int level) // 충돌힘 표시값
        {
            int[] values = { 0, 3, 5, 7, 10, 15 }; // 기획값
            return values[Mathf.Clamp(level, 0, MaxUpgradeLevel)]; // 결과
        }

        [Serializable]
        private sealed class SaveData // PlayerPrefs 저장 데이터
        {
            public int Version; // 버전
            public int Diamond; // 다이아
            public int HighestReachedWave; // 최고 웨이브
            public string SelectedWormId; // 지렁이
            public string SelectedMapId; // 맵
            public bool AttackWormUnlocked; // 공격형
            public bool MobilityWormUnlocked; // 이속형
            public bool SupportWormUnlocked; // 지원형
            public bool MagicWormUnlocked; // 마법형
            public bool DefenseWormUnlocked; // 이전 지원형
            public bool ArmedWormUnlocked; // 이전 공격형
            public bool ChargeWormUnlocked; // 이전 이속형
            public int GoldBonusLevel; // 골드
            public int DiamondBonusLevel; // 다이아
            public int TurnBonusLevel; // 회전
            public int CollisionForceLevel; // 충돌
            public int BaseAttackLevel; // 공격력
            public int AttackSpeedLevel; // 공속
            public int NexusMaxHpLevel; // 체력
            public int NexusRegenLevel; // 회복
        }
    }
}
