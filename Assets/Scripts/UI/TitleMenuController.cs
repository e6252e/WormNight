using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace TeamProject01.Gameplay
{
    public sealed class TitleMenuController : MonoBehaviour // 타이틀 메뉴
    {
        private const string StageScenePath = "Assets/Scenes/StageScene.unity"; // 기본 스테이지 씬 경로 //안건준 추가 - 0628
        private const string StageSceneName = "StageScene"; // 빌드 로드용 씬 이름 //안건준 수정 - 0628
        private const string LegacyCoreTestScenePath = "Assets/Scenes/Dev/CoreTest_StageScene.unity"; // 이전 코어 테스트 씬 (경로 보정용)
        private const string LegacyCoreTestScenePathOld = "Assets/Scenes/Dev/StageScene_CoreTest.unity"; // 더 이전 코어 테스트 씬 (경로 보정용)

        private Button magicWormButton; // 마법형 지렁이 버튼 참조
        private bool mapCardButtonsWired; // 맵 카드 런타임 리스너 중복 방지
        private bool upgradeButtonsWired; // 강화 버튼 런타임 리스너 중복 방지

        [System.Serializable]
        public sealed class TitleMapCardView // 맵 카드 표시 묶음
        {
            public string MapId; // map_01 등
            public Button Button; // 카드 클릭
            public Image PreviewImage; // 썸네일 슬롯
            public Image FrameImage; // 카드 테두리/배경
            public Image EmblemImage; // 문장
            public Image SelectionGlowImage; // 선택 발광
            public GameObject LockedOverlay; // 잠금 딤
            public Text NameText; // 맵 이름
            public Text StateText; // 선택 가능/예정
        }

        [System.Serializable]
        public sealed class TitleUpgradeRowView // 영구 강화 행 묶음
        {
            public MetaUpgradeId UpgradeId; // 실제 강화 ID
            public bool Planned; // 추후 연결 예약칸
            public string PlannedKey; // 예약칸 식별값
            public string PlannedName; // 예약칸 이름
            public Button Button; // 행 클릭
            public Image BackgroundImage; // 행 배경
            public Image IconImage; // 아이콘 슬롯
            public Image SelectionGlowImage; // 선택 발광
            public GameObject PlannedOverlay; // 예정 딤
            public Text NameText; // 행 이름
            public Text StateText; // 상태
            public Image[] LevelPipImages = System.Array.Empty<Image>(); // 5단계 표시
        }

        public MetaProgressionManager Meta; // 메타 데이터
        [Tooltip("Editor Play mode scene path. Core tests use Assets/Scenes/Dev/CoreTest_StageScene.unity; release checks use Assets/Scenes/StageScene.unity.")]
        public string TargetStageScenePath = LegacyCoreTestScenePath; // 게임 시작 시 로드할 스테이지 씬 //안건준 수정 - 0628
        [Min(0)] public int HighestReachedWave; // 레거시 최고 웨이브 이전용
        [Min(0)] public int TemporaryUpgradeBaseCost = 50; // 임시 강화 기본 비용

        [Header("Panels")]
        public GameObject MainMenuPanel; // 메인 메뉴
        public GameObject MapSelectPanel; // 맵 선택
        public GameObject WormSelectPanel; // 지렁이 선택
        public GameObject UpgradePanel; // 업그레이드
        public GameObject SettingsPanel; // 설정

        [Header("Shared UI")]
        public GameObject TitleLogoObject; // 타이틀 로고

        [Header("Preview")]
        public Image SelectedWormPreview; // 지렁이 프리뷰
        public TitleWormPortraitPreview WormPortraitPreview; // 3D 초상화
        public Text SelectedWormNameText; // 지렁이 이름
        public Text SelectedWormBonusText; // 지렁이 보너스
        public Button WormPurchaseButton; // 미리보기 지렁이 구매
        public Button WormSelectButton; // 미리보기 지렁이 선택

        [Header("Status")]
        public Text DiamondText; // 다이아
        public Text HighestWaveText; // 최고 웨이브
        public Text UpgradeSummaryText; // 업그레이드 요약
        public Text StatusText; // 상태 메시지

        [Header("Map Select")]
        public string SelectedMapId = MetaMapIds.Map1; // 선택 맵
        public Text SelectedMapNameText; // 맵 이름
        public Text SelectedMapStateText; // 맵 상태
        public Text SelectedMapDescriptionText; // 맵 설명
        public Image SelectedMapPreview; // 맵 프리뷰
        public Image SelectedMapEmblemImage; // 선택 맵 문장
        public Text SelectedMapRecommendedLevelText; // 추천 레벨
        public Text SelectedMapPowerText; // 권장 전투력
        public Text SelectedMapEnemyTypeText; // 주요 적 유형
        public Text SelectedMapRuleText; // 특수 규칙
        public Text SelectedMapRewardText; // 보상 요약
        public Text SelectedMapRecordText; // 최고 웨이브 기록
        public Text MapDiamondText; // 맵 선택 상단 다이아
        public Text MapHighestWaveText; // 맵 선택 상단 최고 웨이브
        public Button StartSelectedMapButton; // 선택 버튼
        public Text StartSelectedMapButtonText; // 선택 버튼 텍스트
        public TitleMapCardView[] MapCards = System.Array.Empty<TitleMapCardView>(); // 하단 맵 카드들

        [Header("Upgrade Select")]
        public MetaUpgradeId SelectedUpgradeId = MetaUpgradeId.AttackSpeed; // 선택 강화
        public string SelectedPlannedUpgradeKey; // 선택 예약 강화
        public Text UpgradeDiamondText; // 강화 화면 보유 다이아
        public Text UpgradeHighestWaveText; // 강화 화면 최고 웨이브
        public TitleUpgradeRowView[] UpgradeRows = System.Array.Empty<TitleUpgradeRowView>(); // 강화 행들
        public Image UpgradeDetailIconImage; // 상세 아이콘 슬롯
        public Text UpgradeDetailNameText; // 상세 이름
        public Text UpgradeDetailCurrentLevelText; // 현재 레벨
        public Text UpgradeDetailCurrentEffectText; // 현재 효과
        public Text UpgradeDetailNextLevelText; // 다음 레벨
        public Text UpgradeDetailNextEffectText; // 다음 효과
        public Text UpgradeDetailCostText; // 필요 다이아
        public Text UpgradeDetailStatusText; // 상세 상태
        public Button UpgradeConfirmButton; // 강화 버튼
        public Text UpgradeConfirmButtonText; // 강화 버튼 텍스트

        [Header("Debug")]
        [Min(0)] public int DebugDiamondAmount = 1000; // 테스트 지급 다이아
        [Min(0)] public int DebugReachedWave = 20; // 테스트 웨이브
        [Min(0)] public int DebugEarnedDiamond; // 테스트 직접 지급값
        public bool DebugRunClear; // 테스트 클리어 여부
        private string previewWormId; // 현재 미리보기 지렁이

        private void Awake() // 초기 참조
        {
            NormalizeTargetStageScenePath(); // 이전 씬 경로 보정
            if (Meta == null)
            {
                Meta = FindFirstObjectByType<MetaProgressionManager>(); // 씬 메타 검색
            }

            MigrateLegacyHighestReachedWave(); // 이전 타이틀 필드 기록 보존
            ResolvePreviewReferences(); // 프리뷰 참조
            ResolveTitleLogoReference(); // 로고 참조
            WireMapCardButtons(); // 맵 카드 클릭 연결
            WireUpgradeButtons(); // 강화 행 클릭 연결
        }

        private void OnEnable() // 표시 시작
        {
            if (Meta != null)
            {
                Meta.DiamondChanged += OnDiamondChanged; // 다이아 갱신
                Meta.HighestReachedWaveChanged += OnHighestReachedWaveChanged; // 최고 웨이브 갱신
                Meta.SelectedWormChanged += OnSelectedWormChanged; // 지렁이 갱신
                Meta.SelectedMapChanged += OnSelectedMapChanged; // 맵 갱신
                Meta.WormUnlockChanged += OnWormUnlockChanged; // 지렁이 보유 갱신
            }

            ResolveWormSelectionObjects(); // 지렁이 선택 오브젝트 참조
            ResolvePreviewReferences(); // 프리뷰 참조
            ResolveTitleLogoReference(); // 로고 참조
            WireMapCardButtons(); // 씬 오브젝트 리스너 연결
            WireUpgradeButtons(); // 강화 리스너 연결
            SaveData.TryApplyMetaSnapshot(Meta); // 진행 중 저장의 다이아·지렁이 해금 반영 //안건준 추가 - 0629
            TryConsumePendingRunResult(); // 스테이지 결과 보상 반영
            ShowMainMenu(); // 기본 화면
            RefreshAll(); // 즉시 갱신
        }

        private void OnDisable() // 이벤트 해제
        {
            if (Meta != null)
            {
                Meta.DiamondChanged -= OnDiamondChanged; // 다이아 해제
                Meta.HighestReachedWaveChanged -= OnHighestReachedWaveChanged; // 최고 웨이브 해제
                Meta.SelectedWormChanged -= OnSelectedWormChanged; // 지렁이 해제
                Meta.SelectedMapChanged -= OnSelectedMapChanged; // 맵 해제
                Meta.WormUnlockChanged -= OnWormUnlockChanged; // 지렁이 보유 해제
            }
        }

        public void ShowMainMenu() // 메인 표시
        {
            ShowOnly(MainMenuPanel); // 메인만
            RefreshAll(); // 표시 갱신
        }

        public void ShowMapSelect() // 맵 선택 표시
        {
            ShowOnly(MapSelectPanel); // 맵 선택
            SelectMap(Meta != null ? Meta.SelectedMapId : SelectedMapId); // 현재 선택 맵 표시
            RefreshAll(); // 표시 갱신
        }

        public void ShowWormSelect() // 지렁이 선택 표시
        {
            ShowOnly(WormSelectPanel); // 지렁이 선택
            PreviewWorm(Meta != null ? Meta.SelectedWormId : MetaWormIds.Basic); // 현재 선택 프리뷰
            RefreshAll(); // 표시 갱신
            ApplyWormSelectTextColor(); // 지렁이 선택 글자색
        }

        public void ShowUpgrade() // 업그레이드 표시
        {
            ShowOnly(UpgradePanel); // 업그레이드
            RefreshAll(); // 표시 갱신
        }

        public void ShowSettings() // 설정 표시
        {
            ShowOnly(SettingsPanel); // 설정
            RefreshAll(); // 표시 갱신
        }

        public void SelectBasicWorm() // 기본형 미리보기
        {
            PreviewWormChoice(MetaWormIds.Basic); // 카드 클릭은 미리보기만
        }

        public void SelectAttackWorm() // 공격형 미리보기
        {
            PreviewWormChoice(MetaWormIds.Attack); // 카드 클릭은 미리보기만
        }

        public void SelectMobilityWorm() // 이속형 미리보기
        {
            PreviewWormChoice(MetaWormIds.Mobility); // 카드 클릭은 미리보기만
        }

        public void SelectSupportWorm() // 지원형 미리보기
        {
            PreviewWormChoice(MetaWormIds.Support); // 카드 클릭은 미리보기만
        }

        public void SelectMagicWorm() // 마법형 미리보기
        {
            PreviewWormChoice(MetaWormIds.Magic); // 카드 클릭은 미리보기만
        }

        public void SelectDefenseWorm() // 이전 버튼 호환
        {
            SelectSupportWorm(); // 지원형
        }

        public void SelectArmedWorm() // 이전 버튼 호환
        {
            SelectAttackWorm(); // 공격형
        }

        public void SelectChargeWorm() // 이전 버튼 호환
        {
            SelectMobilityWorm(); // 이속형
        }

        private void PreviewWormChoice(string wormId) // 지렁이 카드 미리보기
        {
            PreviewWorm(wormId); // 표시 대상 변경
            RefreshAll(); // 구매/선택 버튼 상태 갱신
        }

        public void PurchasePreviewWorm() // 미리보기 지렁이 구매
        {
            string wormId = GetCurrentPreviewWormId(); // 현재 표시 대상
            if (Meta == null)
            {
                SetStatus("메타 시스템이 없습니다."); // 누락
                return;
            }

            if (Meta.IsWormUnlocked(wormId))
            {
                SetStatus($"{TitleWormCatalog.GetDisplayName(wormId)} 이미 보유 중"); // 이미 보유
                RefreshAll();
                return;
            }

            int cost = Meta.GetWormPrice(wormId); // 가격
            if (!Meta.TryPurchaseWorm(wormId))
            {
                SetStatus($"{TitleWormCatalog.GetDisplayName(wormId)} 구매 불가: 다이아 {cost} 필요"); // 부족
                RefreshAll();
                return;
            }

            SetStatus($"{TitleWormCatalog.GetDisplayName(wormId)} 구매 완료"); // 성공
            RefreshAll();
        }

        public void SelectPreviewWorm() // 미리보기 지렁이 선택
        {
            string wormId = GetCurrentPreviewWormId(); // 현재 표시 대상
            if (Meta == null)
            {
                SetStatus("메타 시스템이 없습니다."); // 누락
                return;
            }

            if (!Meta.IsWormUnlocked(wormId))
            {
                SetStatus($"{TitleWormCatalog.GetDisplayName(wormId)} 먼저 구매 필요"); // 잠금
                RefreshAll();
                return;
            }

            if (Meta.TrySelectWorm(wormId))
            {
                SetStatus($"{TitleWormCatalog.GetDisplayName(wormId)} 선택 완료"); // 성공
            }
            else
            {
                SetStatus($"{TitleWormCatalog.GetDisplayName(wormId)} 선택 실패"); // 예외
            }

            RefreshAll();
        }

        public void StartMap1() // 맵 1 시작
        {
            SelectAndStartMap(MetaMapIds.Map1); // 맵1 시작
        }

        public void StartMap2() // 맵 2
        {
            SelectMap(MetaMapIds.Map2); // 맵2 표시
            SetStatus("맵 2는 업데이트 예정입니다."); // 잠금
        }

        public void StartMap3() // 맵 3
        {
            SelectMap(MetaMapIds.Map3); // 맵3 표시
            SetStatus("맵 3은 업데이트 예정입니다."); // 잠금
        }

        public void StartMap4() // 맵 4
        {
            SelectMap(MetaMapIds.Map4); // 맵4 표시
            SetStatus("맵 4는 업데이트 예정입니다."); // 잠금
        }

        public void StartMap5() // 맵 5
        {
            SelectMap(MetaMapIds.Map5); // 맵5 표시
            SetStatus("맵 5는 업데이트 예정입니다."); // 잠금
        }

        public void SelectMap1() // 맵 1 선택
        {
            SelectMap(MetaMapIds.Map1); // 맵1
        }

        public void SelectMap2() // 맵 2 선택
        {
            SelectMap(MetaMapIds.Map2); // 맵2
        }

        public void SelectMap3() // 맵 3 선택
        {
            SelectMap(MetaMapIds.Map3); // 맵3
        }

        public void SelectMap4() // 맵 4 선택
        {
            SelectMap(MetaMapIds.Map4); // 맵4
        }

        public void SelectMap5() // 맵 5 선택
        {
            SelectMap(MetaMapIds.Map5); // 맵5
        }

        public void SelectMapById(string mapId) // 버튼/카드 공통 선택
        {
            SelectMap(mapId); // ID 기반 선택
        }

        public void StartSelectedMap() // 선택 맵 시작
        {
            SelectAndStartMap(SelectedMapId); // 현재 선택값
        }

        public void UpgradeGoldBonus() // 골드 강화
        {
            Upgrade(MetaUpgradeId.GoldBonus); // 골드
        }

        public void UpgradeDiamondBonus() // 다이아 강화
        {
            Upgrade(MetaUpgradeId.DiamondBonus); // 다이아
        }

        public void UpgradeTurnBonus() // 회전 강화
        {
            Upgrade(MetaUpgradeId.TurnBonus); // 회전
        }

        public void UpgradeCollisionForce() // 충돌 강화
        {
            Upgrade(MetaUpgradeId.CollisionForce); // 충돌
        }

        public void UpgradeBaseAttack() // 공격력 강화
        {
            Upgrade(MetaUpgradeId.BaseAttack); // 공격력
        }

        public void UpgradeAttackSpeed() // 공속 강화
        {
            Upgrade(MetaUpgradeId.AttackSpeed); // 공속
        }

        public void UpgradeNexusMaxHp() // 넥서스 체력 강화
        {
            Upgrade(MetaUpgradeId.NexusMaxHp); // 체력
        }

        public void UpgradeNexusRegen() // 넥서스 회복 강화
        {
            Upgrade(MetaUpgradeId.NexusRegen); // 회복
        }

        public void SelectGoldBonusUpgrade() // 골드 선택
        {
            SelectUpgrade(MetaUpgradeId.GoldBonus); // 골드
        }

        public void SelectDiamondBonusUpgrade() // 다이아 선택
        {
            SelectUpgrade(MetaUpgradeId.DiamondBonus); // 다이아
        }

        public void SelectTurnBonusUpgrade() // 회전 선택
        {
            SelectUpgrade(MetaUpgradeId.TurnBonus); // 회전
        }

        public void SelectCollisionForceUpgrade() // 충돌 선택
        {
            SelectUpgrade(MetaUpgradeId.CollisionForce); // 충돌
        }

        public void SelectBaseAttackUpgrade() // 공격력 선택
        {
            SelectUpgrade(MetaUpgradeId.BaseAttack); // 공격력
        }

        public void SelectAttackSpeedUpgrade() // 공속 선택
        {
            SelectUpgrade(MetaUpgradeId.AttackSpeed); // 공속
        }

        public void SelectNexusMaxHpUpgrade() // 체력 선택
        {
            SelectUpgrade(MetaUpgradeId.NexusMaxHp); // 체력
        }

        public void SelectNexusRegenUpgrade() // 회복 선택
        {
            SelectUpgrade(MetaUpgradeId.NexusRegen); // 회복
        }

        public void ConfirmSelectedUpgrade() // 선택 강화 실행
        {
            if (!string.IsNullOrWhiteSpace(SelectedPlannedUpgradeKey))
            {
                SetStatus($"{TitleUpgradeCatalog.ResolvePlannedName(SelectedPlannedUpgradeKey)}는 추후 적용 예정입니다."); // 예정
                RefreshUpgradePanel(); // 표시 유지
                return;
            }

            Upgrade(SelectedUpgradeId); // 실제 강화
        }

        public void QuitGame() // 종료
        {
            Application.Quit(); // 빌드 종료
            SetStatus("에디터에서는 종료 버튼이 상태만 표시됩니다."); // 에디터 안내
        }

        public void DebugAddDiamond() // 테스트 다이아 지급
        {
            if (Meta == null)
            {
                SetStatus("메타 시스템이 없습니다."); // 누락
                return;
            }

            Meta.AddDiamond(DebugDiamondAmount); // 지급
            SetStatus($"테스트 다이아 +{DebugDiamondAmount}"); // 상태
            RefreshAll(); // 갱신
        }

        public void DebugResetProgress() // 테스트 진행도 초기화
        {
            if (Meta == null)
            {
                SetStatus("메타 시스템이 없습니다."); // 누락
                return;
            }

            Meta.ResetProgress(); // 초기화
            SetStatus("메타 진행도 초기화 완료"); // 상태
            RefreshAll(); // 갱신
        }

        public void DebugSaveProgress() // 테스트 저장
        {
            if (Meta == null)
            {
                SetStatus("메타 시스템이 없습니다."); // 누락
                return;
            }

            Meta.SaveProgress(); // 저장
            SetStatus("메타 저장 완료"); // 상태
            RefreshAll(); // 갱신
        }

        public void DebugLoadProgress() // 테스트 로드
        {
            if (Meta == null)
            {
                SetStatus("메타 시스템이 없습니다."); // 누락
                return;
            }

            bool loaded = Meta.LoadProgressOrDefault(); // 저장 없으면 기본값
            SetStatus(loaded ? "메타 로드 완료" : "저장 데이터 없음: 기본 메타 상태 적용"); // 상태
            RefreshAll(); // 갱신
        }

        public void DebugDeleteSavedProgress() // 테스트 저장 삭제
        {
            if (Meta == null)
            {
                SetStatus("메타 시스템이 없습니다."); // 누락
                return;
            }

            Meta.DeleteSavedProgressAndApplyDefault(); // 저장 삭제 + 기본값
            SetStatus("저장 데이터 삭제 완료. 기본 메타 상태를 적용했습니다."); // 상태
            RefreshAll(); // 갱신
        }

        public void DebugShowMetaSummary() // 테스트 상태 요약
        {
            if (Meta == null)
            {
                SetStatus("메타 시스템이 없습니다."); // 누락
                return;
            }

            SetStatus(Meta.BuildDebugSummary(TemporaryUpgradeBaseCost)); // 요약
            RefreshAll(); // 갱신
        }

        public void DebugApplyRunReward() // 테스트 웨이브 보상
        {
            if (Meta == null)
            {
                SetStatus("메타 시스템이 없습니다."); // 누락
                return;
            }

            RunResultData result = RunResultData.CreateWithExplicitDiamond(DebugReachedWave, 0f, 0, DebugRunClear, DebugEarnedDiamond, 0, Meta.SelectedWormId); // 직접 입력 보상
            int reward = Meta.ApplyRunResult(result); // 보상 적용
            SetStatus($"임시 웨이브 보상 +{reward} 다이아"); // 상태
            RefreshAll(); // 갱신
        }

        private void TryConsumePendingRunResult() // 스테이지 결과 보상 적용
        {
            if (Meta == null || !RunResultContext.TryConsumePendingResult(out RunResultData result))
            {
                return; // 메타 없음/결과 없음
            }

            int reward = Meta.ApplyRunResult(result); // 다이아 지급/저장
            HighestReachedWave = Meta.BestReachedWave; // 메타 기록 동기화
            string resultLabel = result.IsClear ? "게임 클리어" : "게임 오버";
            string bonusText = result.ClearDiamondBonus > 0 ? $" / 보너스 +{result.ClearDiamondBonus}" : string.Empty; // 클리어 보너스
            SetStatus($"{resultLabel} / 도달 웨이브 {result.ReachedWave} / 수집 {result.CollectedDiamond}{bonusText} / 다이아 +{reward}"); // 결과 요약
        }

        private void MigrateLegacyHighestReachedWave() // 이전 컨트롤러 필드 기록 이전
        {
            if (Meta == null || HighestReachedWave <= Meta.BestReachedWave)
            {
                return; // 이전할 기록 없음
            }

            Meta.RegisterReachedWave(HighestReachedWave); // 메타 저장 기록으로 이전
        }

        private void SelectMap(string mapId) // 맵 선택 표시
        {
            SelectedMapId = NormalizeMapId(mapId); // 선택 저장
            if (Meta != null)
            {
                Meta.SelectMap(SelectedMapId); // 메타 동기화/저장
            }

            SetStatus(TitleMapCatalog.IsPlayable(SelectedMapId) ? $"{TitleMapCatalog.GetDisplayName(SelectedMapId)} 선택됨" : $"{TitleMapCatalog.GetDisplayName(SelectedMapId)}는 업데이트 예정입니다."); // 상태
            RefreshAll(); // 갱신
        }

        private void SelectAndStartMap(string mapId) // 맵 선택 후 시작
        {
            if (Meta == null)
            {
                SetStatus("메타 시스템이 없습니다."); // 누락
                return;
            }

            SelectMap(mapId); // 맵 선택
            if (!TitleMapCatalog.IsPlayable(SelectedMapId))
            {
                SetStatus($"{TitleMapCatalog.GetDisplayName(SelectedMapId)}는 업데이트 예정입니다."); // 잠금
                return;
            }

            Meta.SelectMap(SelectedMapId); // 맵 확정/저장
            Meta.PushStartBonusToContext(); // 보너스 준비
            LoadStageScene(); // 스테이지 로드
        }

        private void Upgrade(MetaUpgradeId upgradeId) // 업그레이드 처리
        {
            if (Meta == null)
            {
                SetStatus("메타 시스템이 없습니다."); // 누락
                return;
            }

            SelectedUpgradeId = upgradeId; // 상세 선택 동기화
            SelectedPlannedUpgradeKey = string.Empty; // 예약 선택 해제
            string upgradeName = TitleUpgradeCatalog.GetDisplayName(upgradeId); // 표시명
            int currentLevel = Meta.GetUpgradeLevel(upgradeId); // 현재 단계
            if (Meta.IsUpgradeMaxed(upgradeId))
            {
                SetStatus($"{upgradeName}은 이미 최대 단계입니다."); // 최대
                RefreshAll(); // 갱신
                return;
            }

            int cost = Meta.GetNextUpgradeCost(upgradeId, TemporaryUpgradeBaseCost); // 필요 비용
            if (Meta.OwnedDiamond < cost)
            {
                SetStatus($"{upgradeName} 강화 불가: 다이아 {cost} 필요"); // 부족
                RefreshAll(); // 갱신
                return;
            }

            string beforeEffect = MetaProgressionManager.GetUpgradeEffectText(upgradeId, currentLevel); // 현재 효과
            string afterEffect = MetaProgressionManager.GetUpgradeEffectText(upgradeId, currentLevel + 1); // 다음 효과
            if (Meta.TryUpgrade(upgradeId, TemporaryUpgradeBaseCost))
            {
                SetStatus($"{upgradeName} {currentLevel + 1}/{MetaProgressionManager.MaxUpgradeLevel} 강화 완료 ({beforeEffect} -> {afterEffect})"); // 성공
            }
            else
            {
                SetStatus($"{upgradeName} 강화 실패"); // 예외 실패
            }

            RefreshAll(); // 갱신
        }

        private void SelectUpgrade(MetaUpgradeId upgradeId) // 강화 선택
        {
            SelectedUpgradeId = upgradeId; // 선택 저장
            SelectedPlannedUpgradeKey = string.Empty; // 예약 해제
            SetStatus($"{TitleUpgradeCatalog.GetDisplayName(upgradeId)} 선택됨"); // 상태
            RefreshAll(); // 갱신
        }

        private void SelectPlannedUpgrade(string plannedKey) // 예약 강화 선택
        {
            SelectedPlannedUpgradeKey = string.IsNullOrWhiteSpace(plannedKey) ? "planned_upgrade" : plannedKey; // 키 보정
            SetStatus($"{TitleUpgradeCatalog.ResolvePlannedName(SelectedPlannedUpgradeKey)}는 추후 적용 예정입니다."); // 상태
            RefreshAll(); // 갱신
        }

        private void LoadStageScene() // 스테이지 로드
        {
            NormalizeTargetStageScenePath(); // 직렬화된 이전 값 보정
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(TargetStageScenePath, new LoadSceneParameters(LoadSceneMode.Single)); // 에디터에서 StageScene 경로로 로드 //안건준 수정 - 0628
#else
            SceneManager.LoadScene(StageSceneName); // 빌드에서 StageScene 이름으로 로드 //안건준 수정 - 0628
#endif
        }

        private void NormalizeTargetStageScenePath() // 스테이지 씬 경로 보정
        {
            if (string.IsNullOrWhiteSpace(TargetStageScenePath)
                || TargetStageScenePath == LegacyCoreTestScenePathOld)
            {
                TargetStageScenePath = LegacyCoreTestScenePath; // 이전 테스트 씬 경로를 CoreTest 씬으로 통일 //안건준 수정 - 0628
            }
        }

        private void ShowOnly(GameObject target) // 패널 전환
        {
            ResolveTitleLogoReference(); // 로고 찾기
            SetActive(MainMenuPanel, target == MainMenuPanel); // 메인
            SetActive(MapSelectPanel, target == MapSelectPanel); // 맵
            SetActive(WormSelectPanel, target == WormSelectPanel); // 지렁이
            SetActive(UpgradePanel, target == UpgradePanel); // 업그레이드
            SetActive(SettingsPanel, target == SettingsPanel); // 설정
            SetActive(TitleLogoObject, target != WormSelectPanel && target != MapSelectPanel && target != UpgradePanel); // 전용 화면은 자체 로고 사용
        }

        private void RefreshAll() // 전체 표시 갱신
        {
            if (Meta == null)
            {
                return; // 대상 없음
            }

            ResolveWormSelectionObjects(); // 지렁이 선택 오브젝트 참조
            ResolvePreviewReferences(); // 프리뷰 참조
            string displayWormId = string.IsNullOrWhiteSpace(previewWormId) ? Meta.SelectedWormId : previewWormId; // 표시 대상
            RefreshProgressTexts(); // 보유 정보
            SetText(SelectedWormNameText, TitleWormCatalog.GetDisplayName(displayWormId)); // 이름
            SetText(SelectedWormBonusText, TitleWormCatalog.GetBonusText(displayWormId)); // 효과
            SetText(UpgradeSummaryText, BuildUpgradeSummary()); // 강화 요약
            RefreshUpgradePanel(); // 강화 화면
            RefreshMapPreview(); // 맵 표시
            if (WormPortraitPreview != null)
            {
                WormPortraitPreview.PreviewWorm(displayWormId); // 3D 초상화
            }

            if (SelectedWormPreview != null)
            {
                SelectedWormPreview.color = TitleWormCatalog.GetPreviewColor(displayWormId); // 프리뷰 색
            }

            if (WormSelectPanel != null && WormSelectPanel.activeInHierarchy)
            {
                RefreshWormSelectPanel(displayWormId); // 지렁이 버튼 상태 갱신
                ApplyWormSelectTextColor(); // 버튼 글자색 보정
            }
        }

        private string BuildUpgradeSummary() // 업그레이드 요약
        {
            if (Meta == null)
            {
                return string.Empty; // 없음
            }

            return BuildUpgradeLine(MetaUpgradeId.GoldBonus) + "\n"
                + BuildUpgradeLine(MetaUpgradeId.DiamondBonus) + "\n"
                + BuildUpgradeLine(MetaUpgradeId.TurnBonus) + "\n"
                + BuildUpgradeLine(MetaUpgradeId.CollisionForce) + "\n"
                + BuildUpgradeLine(MetaUpgradeId.BaseAttack) + "\n"
                + BuildUpgradeLine(MetaUpgradeId.AttackSpeed) + "\n"
                + BuildUpgradeLine(MetaUpgradeId.NexusMaxHp) + "\n"
                + BuildUpgradeLine(MetaUpgradeId.NexusRegen); // 요약
        }

        private string BuildUpgradeLine(MetaUpgradeId upgradeId) // 강화 한 줄 요약
        {
            int level = Meta.GetUpgradeLevel(upgradeId); // 현재 단계
            string name = TitleUpgradeCatalog.GetDisplayName(upgradeId); // 이름
            string current = CompactEffectText(MetaProgressionManager.GetUpgradeEffectText(upgradeId, level)); // 현재 효과
            if (Meta.IsUpgradeMaxed(upgradeId))
            {
                return $"{name} {level}/{MetaProgressionManager.MaxUpgradeLevel} {current} MAX"; // 최대
            }

            int cost = Meta.GetNextUpgradeCost(upgradeId, TemporaryUpgradeBaseCost); // 비용
            string next = CompactEffectText(MetaProgressionManager.GetUpgradeEffectText(upgradeId, level + 1)); // 다음 효과
            string costText = Meta.OwnedDiamond >= cost ? $"비용 {cost}" : $"부족 {cost}"; // 구매 상태
            return $"{name} {level}/{MetaProgressionManager.MaxUpgradeLevel} {current}->{next} {costText}"; // 표시
        }

        private void RefreshMapPreview() // 맵 표시 갱신
        {
            string mapId = Meta != null ? NormalizeMapId(Meta.SelectedMapId) : NormalizeMapId(SelectedMapId); // 보정
            SelectedMapId = mapId; // 로드값 동기화
            SetText(SelectedMapNameText, TitleMapCatalog.GetDisplayName(mapId)); // 이름
            SetText(SelectedMapStateText, TitleMapCatalog.GetStateText(mapId)); // 상태
            SetText(SelectedMapDescriptionText, TitleMapCatalog.GetDescription(mapId)); // 설명
            SetText(SelectedMapRecommendedLevelText, TitleMapCatalog.GetRecommendedLevelText(mapId)); // 추천 레벨
            SetText(SelectedMapPowerText, TitleMapCatalog.GetPowerText(mapId)); // 전투력
            SetText(SelectedMapEnemyTypeText, TitleMapCatalog.GetEnemyTypeText(mapId)); // 적 유형
            SetText(SelectedMapRuleText, TitleMapCatalog.GetRuleText(mapId)); // 특수 규칙
            SetText(SelectedMapRewardText, TitleMapCatalog.GetRewardText(mapId)); // 보상
            SetText(SelectedMapRecordText, GetMapRecordText(mapId)); // 기록
            SetText(StartSelectedMapButtonText, TitleMapCatalog.IsPlayable(mapId) ? "선택" : "예정"); // 시작 버튼

            if (SelectedMapPreview != null)
            {
                ApplyMapImageSlotColor(SelectedMapPreview, mapId); // 실제 사진은 원색 유지
            }

            if (SelectedMapEmblemImage != null)
            {
                SelectedMapEmblemImage.color = TitleMapCatalog.GetEmblemColor(mapId); // 문장색
            }

            if (StartSelectedMapButton != null)
            {
                StartSelectedMapButton.interactable = TitleMapCatalog.IsPlayable(mapId); // 잠금 맵 시작 금지
            }

            RefreshMapCardViews(mapId); // 하단 카드 상태 갱신
        }

        private void OnDiamondChanged(int diamond) // 다이아 이벤트
        {
            RefreshAll(); // 갱신
        }

        private void OnHighestReachedWaveChanged(int highestWave) // 최고 웨이브 이벤트
        {
            HighestReachedWave = Mathf.Max(0, highestWave); // 표시 필드 동기화
            RefreshAll(); // 갱신
        }

        private void OnSelectedWormChanged(string wormId) // 지렁이 이벤트
        {
            previewWormId = NormalizeWormId(wormId); // 선택값을 미리보기로
            RefreshAll(); // 갱신
        }

        private void OnWormUnlockChanged(string wormId, bool unlocked) // 지렁이 보유 이벤트
        {
            RefreshAll(); // 구매/보유 상태 갱신
        }

        private void OnSelectedMapChanged(string mapId) // 맵 이벤트
        {
            SelectedMapId = NormalizeMapId(mapId); // 선택 동기화
            RefreshAll(); // 갱신
        }

        private void SetStatus(string message) // 상태 메시지
        {
            SetText(StatusText, message); // 표시
        }

        private void WireMapCardButtons() // 하단 카드 클릭 리스너 연결
        {
            if (mapCardButtonsWired)
            {
                return; // 중복 연결 방지
            }

            mapCardButtonsWired = true; // 1회만
            if (StartSelectedMapButton != null && StartSelectedMapButton.onClick.GetPersistentEventCount() == 0)
            {
                StartSelectedMapButton.onClick.AddListener(StartSelectedMap); // 선택 버튼
            }

            for (int i = 0; MapCards != null && i < MapCards.Length; i++)
            {
                TitleMapCardView card = MapCards[i]; // 카드 묶음
                if (card == null || card.Button == null || string.IsNullOrWhiteSpace(card.MapId))
                {
                    continue; // 연결 불가
                }

                string capturedMapId = NormalizeMapId(card.MapId); // 클로저용 복사
                card.Button.onClick.AddListener(() => SelectMapById(capturedMapId)); // 카드 선택
            }
        }

        private void RefreshProgressTexts() // 보유 정보 표시
        {
            int diamond = Meta != null ? Meta.OwnedDiamond : 0; // 보유 다이아
            int highestWave = ResolveHighestReachedWave(); // 최고 웨이브
            HighestReachedWave = highestWave; // 레거시 표시 필드 동기화
            string diamondText = diamond.ToString(); // 공통 문구
            string highestWaveText = highestWave.ToString(); // 공통 문구
            SetText(DiamondText, diamondText); // 메인 다이아
            SetText(HighestWaveText, highestWaveText); // 메인 기록
            SetText(MapDiamondText, diamondText); // 맵 선택 다이아
            SetText(MapHighestWaveText, highestWaveText); // 맵 선택 기록
            SetText(UpgradeDiamondText, diamondText); // 강화 화면 다이아
            SetText(UpgradeHighestWaveText, highestWaveText); // 강화 화면 기록
        }

        private void WireUpgradeButtons() // 강화 행/버튼 리스너 연결
        {
            if (upgradeButtonsWired)
            {
                return; // 중복 방지
            }

            upgradeButtonsWired = true; // 1회 연결
            if (UpgradeConfirmButton != null && UpgradeConfirmButton.onClick.GetPersistentEventCount() == 0)
            {
                UpgradeConfirmButton.onClick.AddListener(ConfirmSelectedUpgrade); // 강화 버튼
            }

            for (int i = 0; UpgradeRows != null && i < UpgradeRows.Length; i++)
            {
                TitleUpgradeRowView row = UpgradeRows[i]; // 행
                if (row == null || row.Button == null)
                {
                    continue; // 누락
                }

                MetaUpgradeId capturedId = row.UpgradeId; // enum 복사
                bool capturedPlanned = row.Planned; // 예약 여부
                string capturedKey = row.PlannedKey; // 예약 키
                row.Button.onClick.AddListener(() =>
                {
                    if (capturedPlanned)
                    {
                        SelectPlannedUpgrade(capturedKey); // 예약 선택
                    }
                    else
                    {
                        SelectUpgrade(capturedId); // 실제 강화 선택
                    }
                });
            }
        }

        private void RefreshUpgradePanel() // 강화 화면 표시
        {
            WireUpgradeButtons(); // 강화 리스너 연결
            RefreshUpgradeRows(); // 좌측 목록
            RefreshUpgradeDetail(); // 우측 상세
        }

        private void RefreshUpgradeRows() // 강화 목록 표시
        {
            for (int i = 0; UpgradeRows != null && i < UpgradeRows.Length; i++)
            {
                TitleUpgradeRowView row = UpgradeRows[i]; // 행
                if (row == null)
                {
                    continue; // 누락
                }

                string plannedKey = string.IsNullOrWhiteSpace(row.PlannedKey) ? row.PlannedName : row.PlannedKey; // 예약 키
                bool selected = row.Planned
                    ? !string.IsNullOrWhiteSpace(SelectedPlannedUpgradeKey) && SelectedPlannedUpgradeKey == plannedKey
                    : string.IsNullOrWhiteSpace(SelectedPlannedUpgradeKey) && row.UpgradeId == SelectedUpgradeId; // 선택
                int level = row.Planned || Meta == null ? 0 : Meta.GetUpgradeLevel(row.UpgradeId); // 현재 단계
                bool maxed = !row.Planned && Meta != null && Meta.IsUpgradeMaxed(row.UpgradeId); // 최대

                SetText(row.NameText, row.Planned ? TitleUpgradeCatalog.ResolvePlannedName(plannedKey, row.PlannedName) : TitleUpgradeCatalog.GetDisplayName(row.UpgradeId)); // 이름
                SetText(row.StateText, row.Planned ? "예정" : maxed ? "MAX" : $"{level}/{MetaProgressionManager.MaxUpgradeLevel}"); // 상태
                SetActive(row.PlannedOverlay, row.Planned); // 예약 딤
                ApplyUpgradeRowVisual(row, selected, row.Planned, level); // 비주얼
            }
        }

        private void RefreshUpgradeDetail() // 강화 상세 표시
        {
            if (!string.IsNullOrWhiteSpace(SelectedPlannedUpgradeKey))
            {
                RefreshPlannedUpgradeDetail(); // 예약 상세
                return;
            }

            MetaUpgradeId upgradeId = SelectedUpgradeId; // 현재 선택
            string name = TitleUpgradeCatalog.GetDisplayName(upgradeId); // 이름
            int level = Meta != null ? Meta.GetUpgradeLevel(upgradeId) : 0; // 현재 단계
            bool maxed = Meta != null && Meta.IsUpgradeMaxed(upgradeId); // 최대
            int nextLevel = maxed ? level : Mathf.Min(level + 1, MetaProgressionManager.MaxUpgradeLevel); // 다음 단계
            int cost = Meta != null ? Meta.GetNextUpgradeCost(upgradeId, TemporaryUpgradeBaseCost) : 0; // 비용
            bool affordable = Meta != null && !maxed && Meta.OwnedDiamond >= cost; // 구매 가능

            SetText(UpgradeDetailNameText, name); // 이름
            SetText(UpgradeDetailCurrentLevelText, $"{level} / {MetaProgressionManager.MaxUpgradeLevel}"); // 현재 레벨
            SetText(UpgradeDetailCurrentEffectText, MetaProgressionManager.GetUpgradeEffectText(upgradeId, level)); // 현재 효과
            SetText(UpgradeDetailNextLevelText, maxed ? "MAX" : $"{nextLevel} / {MetaProgressionManager.MaxUpgradeLevel}"); // 다음 레벨
            SetText(UpgradeDetailNextEffectText, maxed ? "최대 단계" : MetaProgressionManager.GetUpgradeEffectText(upgradeId, nextLevel)); // 다음 효과
            SetText(UpgradeDetailCostText, maxed ? "-" : cost.ToString()); // 비용
            SetText(UpgradeDetailStatusText, maxed ? "이미 최대 강화입니다." : affordable ? "강화 가능" : "다이아가 부족합니다."); // 상태
            SetText(UpgradeConfirmButtonText, maxed ? "최대" : affordable ? "강화" : "부족"); // 버튼
            if (UpgradeConfirmButton != null)
            {
                UpgradeConfirmButton.interactable = affordable; // 상호작용
                ApplyButtonColor(UpgradeConfirmButton.image, affordable, maxed); // 버튼 색
            }

            ApplyUpgradeIconVisual(UpgradeDetailIconImage, upgradeId, false); // 아이콘
        }

        private void RefreshPlannedUpgradeDetail() // 예약 상세
        {
            string name = TitleUpgradeCatalog.ResolvePlannedName(SelectedPlannedUpgradeKey); // 이름
            SetText(UpgradeDetailNameText, name); // 이름
            SetText(UpgradeDetailCurrentLevelText, "-"); // 현재
            SetText(UpgradeDetailCurrentEffectText, "추후 적용"); // 현재 효과
            SetText(UpgradeDetailNextLevelText, "예정"); // 다음
            SetText(UpgradeDetailNextEffectText, "강화값 적용 구조 협의 후 연결"); // 다음 효과
            SetText(UpgradeDetailCostText, "-"); // 비용
            SetText(UpgradeDetailStatusText, "기능 연결 예정"); // 상태
            SetText(UpgradeConfirmButtonText, "예정"); // 버튼
            if (UpgradeConfirmButton != null)
            {
                UpgradeConfirmButton.interactable = false; // 잠금
                ApplyButtonColor(UpgradeConfirmButton.image, false, false); // 비활성 색
            }

            ApplyUpgradeIconVisual(UpgradeDetailIconImage, MetaUpgradeId.GoldBonus, true); // 예약 아이콘
        }

        private void RefreshMapCardViews(string selectedMapId) // 하단 맵 카드 표시
        {
            for (int i = 0; MapCards != null && i < MapCards.Length; i++)
            {
                TitleMapCardView card = MapCards[i]; // 카드 묶음
                if (card == null)
                {
                    continue; // 누락 방지
                }

                string mapId = NormalizeMapId(card.MapId); // 카드 맵
                bool selected = mapId == selectedMapId; // 선택 여부
                bool playable = TitleMapCatalog.IsPlayable(mapId); // 플레이 가능

                SetText(card.NameText, TitleMapCatalog.GetDisplayName(mapId)); // 이름
                SetText(card.StateText, TitleMapCatalog.GetStateText(mapId)); // 상태
                SetActive(card.LockedOverlay, !playable); // 잠금 딤
                if (card.PreviewImage != null)
                {
                    ApplyMapImageSlotColor(card.PreviewImage, mapId); // 실제 사진은 원색 유지
                }

                if (card.FrameImage != null)
                {
                    card.FrameImage.color = selected
                        ? new Color(0.16f, 0.72f, 1f, 0.95f)
                        : new Color(0.86f, 0.58f, 0.24f, 0.95f); // 선택/일반 테두리
                }

                if (card.EmblemImage != null)
                {
                    card.EmblemImage.color = TitleMapCatalog.GetEmblemColor(mapId); // 문장색
                }

                if (card.SelectionGlowImage != null)
                {
                    card.SelectionGlowImage.enabled = selected; // 선택 발광
                }
            }
        }

        private void ResolveWormSelectionObjects() // 지렁이 선택 오브젝트 참조
        {
            if (WormSelectPanel == null)
            {
                return; // 패널 없음
            }

            SetWormButtonLabel("BasicWormButton", "기본형 지렁이\n시작 무기: 대포"); // 기본형
            SetWormButtonLabel("DefenseWormButton", "지원형 지렁이\n시작 무기: 화염구 / 150 다이아"); // 기존 방어형
            SetWormButtonLabel("ArmedWormButton", "공격형 지렁이\n시작 무기: 미사일 / 200 다이아"); // 기존 무장형
            SetWormButtonLabel("ChargeWormButton", "이속형 지렁이\n시작 무기: 톱날 / 200 다이아"); // 기존 돌격형

            Transform existingMagic = FindWormButtonTransform("MagicWormButton"); // 기존 버튼
            if (magicWormButton == null && existingMagic != null)
            {
                magicWormButton = existingMagic.GetComponent<Button>(); // 참조 저장
            }

            if (magicWormButton != null)
            {
                SetWormButtonLabel("MagicWormButton", "마법형 지렁이\n시작 무기: 전기지직 / 250 다이아"); // 라벨 유지
            }

            ResolveWormActionButtonReferences(); // 정식 하단 구매/선택 버튼 참조
        }

        private void ResolveWormActionButtonReferences() // 지렁이 구매/선택 버튼 참조
        {
            if (WormPurchaseButton == null)
            {
                WormPurchaseButton = FindWormButtonTransform("WormPurchaseButton")?.GetComponent<Button>();
            }

            if (WormSelectButton == null)
            {
                WormSelectButton = FindWormButtonTransform("WormSelectButton")?.GetComponent<Button>();
            }
        }

        private void RefreshWormSelectPanel(string displayWormId) // 지렁이 선택 화면 상태 갱신
        {
            UpdateWormButtonView("BasicWormButton", MetaWormIds.Basic, "기본형 지렁이", "대포");
            UpdateWormButtonView("DefenseWormButton", MetaWormIds.Support, "지원형 지렁이", "화염구");
            UpdateWormButtonView("ArmedWormButton", MetaWormIds.Attack, "공격형 지렁이", "미사일");
            UpdateWormButtonView("ChargeWormButton", MetaWormIds.Mobility, "이속형 지렁이", "톱날");
            UpdateWormButtonView("MagicWormButton", MetaWormIds.Magic, "마법형 지렁이", "전기지직");
            RefreshWormActionButtons(displayWormId);
        }

        private void UpdateWormButtonView(string objectName, string wormId, string displayName, string starterName) // 지렁이 카드 버튼 상태
        {
            Transform transform = FindWormButtonTransform(objectName);
            if (transform == null || Meta == null)
            {
                return;
            }

            string normalized = NormalizeWormId(wormId);
            bool selected = NormalizeWormId(Meta.SelectedWormId) == normalized;
            bool unlocked = Meta.IsWormUnlocked(normalized);
            int price = Meta.GetWormPrice(normalized);
            bool affordable = unlocked || Meta.OwnedDiamond >= price;
            string state = selected
                ? "선택됨"
                : unlocked ? "보유중"
                : affordable ? $"구매 {price} 다이아" : $"부족 {price} 다이아";

            SetWormButtonLabel(objectName, $"{displayName}\n시작 무기: {starterName}\n{state}");

            Image image = transform.GetComponent<Image>();
            if (image != null)
            {
                image.color = selected
                    ? new Color(0.24f, 0.62f, 0.92f, 0.96f)
                    : unlocked ? new Color(0.24f, 0.50f, 0.25f, 0.92f)
                    : affordable ? new Color(0.58f, 0.42f, 0.20f, 0.92f)
                    : new Color(0.30f, 0.28f, 0.26f, 0.82f);
            }

            Button button = transform.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = true;
            }
        }

        private void RefreshWormActionButtons(string displayWormId) // 하단 구매/선택 버튼 상태
        {
            string wormId = NormalizeWormId(displayWormId);
            bool unlocked = Meta != null && Meta.IsWormUnlocked(wormId);
            bool selected = Meta != null && NormalizeWormId(Meta.SelectedWormId) == wormId;
            int price = Meta != null ? Meta.GetWormPrice(wormId) : 0;
            bool affordable = Meta != null && Meta.OwnedDiamond >= price;

            SetWormActionButtonState(
                WormPurchaseButton,
                unlocked ? "보유중" : affordable ? $"구매 {price}" : $"부족 {price}",
                !unlocked && affordable,
                unlocked ? new Color(0.34f, 0.44f, 0.34f, 0.82f) : affordable ? new Color(0.23f, 0.42f, 0.62f, 0.92f) : new Color(0.34f, 0.30f, 0.27f, 0.82f));

            SetWormActionButtonState(
                WormSelectButton,
                selected ? "선택됨" : unlocked ? "선택" : "잠금",
                unlocked && !selected,
                selected ? new Color(0.24f, 0.62f, 0.92f, 0.96f) : unlocked ? new Color(0.23f, 0.48f, 0.20f, 0.92f) : new Color(0.34f, 0.30f, 0.27f, 0.82f));
        }

        private static void SetWormActionButtonState(Button button, string label, bool interactable, Color color) // 하단 버튼 표시
        {
            if (button == null)
            {
                return;
            }

            button.interactable = interactable;
            Image image = button.targetGraphic as Image;
            if (image == null)
            {
                image = button.GetComponent<Image>();
            }

            if (image != null)
            {
                image.color = color;
            }

            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
            }
        }

        private string GetCurrentPreviewWormId() // 현재 미리보기 지렁이
        {
            if (!string.IsNullOrWhiteSpace(previewWormId))
            {
                return NormalizeWormId(previewWormId);
            }

            return Meta != null ? NormalizeWormId(Meta.SelectedWormId) : MetaWormIds.Basic;
        }

        private void PreviewWorm(string wormId) // 지렁이 미리보기
        {
            previewWormId = NormalizeWormId(wormId); // 표시 ID
            ResolvePreviewReferences(); // 프리뷰 참조
            if (WormPortraitPreview != null)
            {
                WormPortraitPreview.PreviewWorm(previewWormId); // 3D 모델 교체
            }

            SetText(SelectedWormNameText, TitleWormCatalog.GetDisplayName(previewWormId)); // 이름 즉시 표시
            SetText(SelectedWormBonusText, TitleWormCatalog.GetBonusText(previewWormId)); // 보너스 즉시 표시
            if (SelectedWormPreview != null)
            {
                SelectedWormPreview.color = TitleWormCatalog.GetPreviewColor(previewWormId); // 색 프리뷰
            }
        }

        private void ResolvePreviewReferences() // 프리뷰 참조 찾기
        {
            if (WormPortraitPreview == null)
            {
                WormPortraitPreview = FindFirstObjectByType<TitleWormPortraitPreview>(); // 씬 검색
            }
        }

        private void ResolveTitleLogoReference() // 로고 참조 찾기
        {
            if (TitleLogoObject == null)
            {
                TitleLogoObject = GameObject.Find("TitleLogo"); // 씬 검색
            }
        }

        private void SetWormButtonLabel(string objectName, string label) // 버튼 라벨 변경
        {
            Transform button = FindWormButtonTransform(objectName); // 버튼 찾기
            if (button == null)
            {
                return; // 없음
            }

            Text text = button.GetComponentInChildren<Text>(true); // 라벨
            if (text != null)
            {
                text.text = label; // 표시
            }
        }

        private Transform FindWormButtonTransform(string objectName) // 버튼 찾기
        {
            if (WormSelectPanel == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null; // 대상 없음
            }

            Transform[] children = WormSelectPanel.GetComponentsInChildren<Transform>(true); // 하위 검색
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i]; // 후보
                if (child != null && child.name == objectName)
                {
                    return child; // 찾음
                }
            }

            return null; // 없음
        }

        private static void SetActive(GameObject target, bool active) // 활성화
        {
            if (target != null)
            {
                target.SetActive(active); // 상태 반영
            }
        }

        private static void SetText(Text target, string value) // 텍스트 설정
        {
            if (target != null)
            {
                target.text = value; // 값 반영
            }
        }

        private void ApplyWormSelectTextColor() // 지렁이 선택 글자색 보정
        {
            if (WormSelectPanel == null)
            {
                return; // 대상 없음
            }

            Text[] texts = WormSelectPanel.GetComponentsInChildren<Text>(true); // 선택 화면 텍스트
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null)
                {
                    texts[i].color = Color.white; // 임시 흰색
                }
            }
        }

        private static string CompactEffectText(string effectText) // 요약용 축약
        {
            return effectText == "효과 없음" ? "없음" : effectText; // 0단계 축약
        }

        private static void ApplyUpgradeRowVisual(TitleUpgradeRowView row, bool selected, bool planned, int level) // 강화 행 비주얼
        {
            if (row == null)
            {
                return; // 대상 없음
            }

            if (row.BackgroundImage != null)
            {
                row.BackgroundImage.type = Image.Type.Simple; // 사각 슬롯
                row.BackgroundImage.color = selected
                    ? new Color(0.78f, 0.92f, 1f, 0.96f)
                    : planned ? new Color(0.54f, 0.48f, 0.40f, 0.84f) : new Color(0.84f, 0.70f, 0.50f, 0.94f); // 선택/예정/일반
            }

            if (row.SelectionGlowImage != null)
            {
                row.SelectionGlowImage.enabled = selected; // 선택 테두리
                row.SelectionGlowImage.color = new Color(0.18f, 0.78f, 1f, 0.86f); // 청색 강조
            }

            ApplyUpgradeIconVisual(row.IconImage, row.UpgradeId, planned); // 아이콘
            for (int i = 0; row.LevelPipImages != null && i < row.LevelPipImages.Length; i++)
            {
                Image pip = row.LevelPipImages[i]; // 단계 점
                if (pip == null)
                {
                    continue; // 누락
                }

                pip.type = Image.Type.Simple; // 다이아 슬롯
                pip.raycastTarget = false; // 입력 통과
                bool filled = !planned && i < Mathf.Clamp(level, 0, MetaProgressionManager.MaxUpgradeLevel); // 채움
                pip.color = planned
                    ? new Color(0.38f, 0.34f, 0.29f, 0.72f)
                    : filled ? new Color(1f, 0.68f, 0.16f, 1f) : new Color(0.49f, 0.43f, 0.35f, 0.98f); // 단계색
            }
        }

        private static void ApplyUpgradeIconVisual(Image image, MetaUpgradeId upgradeId, bool planned) // 강화 아이콘 표시
        {
            if (image == null)
            {
                return; // 대상 없음
            }

            image.enabled = true; // 표시
            image.type = Image.Type.Simple; // 아이콘 슬롯
            image.preserveAspect = true; // 실제 아이콘 비율 유지
            image.raycastTarget = false; // 행 버튼 입력 우선
            image.color = planned ? new Color(0.55f, 0.55f, 0.55f, 0.9f) : image.sprite != null ? Color.white : TitleUpgradeCatalog.GetIconColor(upgradeId); // 스프라이트 교체 대응
        }

        private static void ApplyButtonColor(Image image, bool affordable, bool maxed) // 강화 버튼 색
        {
            if (image == null)
            {
                return; // 대상 없음
            }

            image.type = Image.Type.Simple; // 버튼 슬롯
            image.color = maxed
                ? new Color(0.65f, 0.56f, 0.40f, 0.96f)
                : affordable ? new Color(0.34f, 0.62f, 0.18f, 1f) : new Color(0.38f, 0.33f, 0.27f, 0.88f); // 가능/불가
        }

        private static string NormalizeWormId(string wormId) // 지렁이 ID 보정
        {
            return MetaWormIds.Normalize(wormId); // 공용 보정
        }

        private static string NormalizeMapId(string mapId) // 맵 ID 보정
        {
            return MetaMapIds.Normalize(mapId); // 공용 보정
        }

        private static void ApplyMapImageSlotColor(Image image, string mapId) // 맵 사진 슬롯 색상
        {
            if (image == null)
            {
                return; // 대상 없음
            }

            image.type = Image.Type.Simple; // 실제 맵 사진은 사각 이미지로 표시
            image.preserveAspect = false; // 정해진 슬롯 비율에 맞춰 채움
            image.color = image.sprite != null ? Color.white : TitleMapCatalog.GetPreviewColor(mapId); // 사진 교체 시 원색 유지
        }

        private string GetMapRecordText(string mapId) // 맵 기록
        {
            return TitleMapCatalog.IsPlayable(mapId) ? ResolveHighestReachedWave().ToString() : "-"; // 현재는 맵1 기록만 사용
        }

        private int ResolveHighestReachedWave() // 표시용 최고 웨이브
        {
            return Meta != null ? Meta.BestReachedWave : Mathf.Max(0, HighestReachedWave); // 메타 우선
        }
    }
}
