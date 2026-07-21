using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace TeamProject01.Gameplay
{
    public sealed class RunResultController : MonoBehaviour // 런 종료 판정/결과창
    {
        private const string DefaultTitleScenePath = "Assets/Scenes/TitleScene.unity";
        private const string DefaultTitleSceneName = "TitleScene";

        [Header("Rules")]
        [Min(1)] [SerializeField] private int clearStageThreshold = 40; // 이 스테이지 이상에서 넥서스 사망 시 클리어
        [SerializeField] private string titleScenePath = DefaultTitleScenePath; // 복귀 씬

        [Header("Diamond Reward")]
        [Range(0f, 1f)] [SerializeField] private float clearDiamondBonusRate = 0.2f; // 40웨이브 이상 클리어 보너스

        [Header("References")]
        [SerializeField] private NexusController nexus; // 넥서스
        [SerializeField] private WaveController waveController; // 웨이브
        [SerializeField] private CoreStatProvider coreStats; // 골드/런 정보
        [SerializeField] private RunResultOverlayView overlayView; // 결과 팝업

        private bool resultFinalized; // 중복 종료 방지
        private bool subscribedToNexus; // 넥서스 이벤트 구독 상태
        private int killedMonsterCount; // 피해로 처치한 몬스터 수
        private float runStartRealtime; // 생존 시간 기준
        private float previousTimeScale = 1f; // 복귀용 타임스케일
        private Coroutine showRoutine; // 결과창 코루틴

        private void Awake()
        {
            ResolveReferences(); // 씬 참조 보강
            runStartRealtime = Time.unscaledTime; // 시작 시간
        }

        private void OnEnable()
        {
            ResolveReferences();
            TrySubscribeNexus();
            EnemyController.DamageKilled += HandleEnemyDamageKilled; // 처치 수 누적
        }

        private void Start()
        {
            ResolveReferences();
            TrySubscribeNexus(); // Active 등록이 늦을 때 보강
        }

        private void OnDisable()
        {
            UnsubscribeNexus();
            EnemyController.DamageKilled -= HandleEnemyDamageKilled;
        }

        private void HandleEnemyDamageKilled(EnemyController enemy)
        {
            if (resultFinalized)
            {
                return; // 종료 후 카운트 고정
            }

            killedMonsterCount = Mathf.Max(0, killedMonsterCount + 1);
        }

        private void HandleNexusDied(NexusController deadNexus)
        {
            FinalizeRunResult(); // 넥서스 사망 = 결과 판정
        }

        public void FinalizeRunResult()
        {
            if (resultFinalized)
            {
                return; // 중복 방지
            }

            resultFinalized = true;
            ResolveReferences();

            int reachedWave = Mathf.Max(0, waveController != null ? waveController.CurrentStage : 0);
            bool isClear = reachedWave >= clearStageThreshold; // 40 이상이면 클리어
            float surviveTime = Mathf.Max(0f, Time.unscaledTime - runStartRealtime);
            int collectedDiamond = coreStats != null ? Mathf.Max(0, coreStats.CurrentRunDiamond) : 0; // 먹은 다이아
            int clearDiamondBonus = CalculateClearDiamondBonus(collectedDiamond, isClear); // 클리어 추가
            int finalDiamond = collectedDiamond + clearDiamondBonus; // 최종 지급
            int earnedGold = coreStats != null ? Mathf.Max(0, coreStats.CurrentGold) : 0;
            string selectedWormId = RunLoadoutContext.CurrentStartBonus.SelectedWormId;

            RunResultData result = RunResultData.CreateWithDiamondBreakdown(
                reachedWave,
                surviveTime,
                killedMonsterCount,
                isClear,
                collectedDiamond,
                clearDiamondBonus,
                earnedGold,
                selectedWormId); // 메타 적용용 결과

            RunResultContext.SetPendingResult(result); // 타이틀 복귀 후 보상 적용
            SaveData.NotifyRunFinished(); //안건준 추가 - 0629 (런 종료 시 중간 저장 삭제)

            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f; // 월드 정지, UI는 unscaled DOTween 사용

            RunResultDisplayData displayData = new RunResultDisplayData
            {
                IsClear = isClear,
                ReachedWave = reachedWave,
                KillCount = killedMonsterCount,
                CollectedDiamond = collectedDiamond,
                ClearDiamondBonus = clearDiamondBonus,
                DisplayDiamond = finalDiamond
            };

            PlayResultSfx(isClear);
            if (overlayView == null)
            {
                ReturnToTitleScene(); // 팝업 누락 시 결과 저장 후 복귀
                return;
            }

            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
            }

            showRoutine = StartCoroutine(overlayView.ShowRoutine(displayData, ReturnToTitleScene));
        }

        private int CalculateClearDiamondBonus(int collectedDiamond, bool isClear)
        {
            if (!isClear || collectedDiamond <= 0)
            {
                return 0; // 게임오버는 먹은 다이아만
            }

            return Mathf.Max(0, Mathf.FloorToInt(collectedDiamond * Mathf.Clamp01(clearDiamondBonusRate))); // 20%
        }

        private void PlayResultSfx(bool isClear)
        {
            GameplaySfxCue cue = isClear ? GameplaySfxCue.ResultClear : GameplaySfxCue.ResultGameOver;
            Transform root = overlayView != null ? overlayView.transform : transform;
            Vector3 position = root != null ? root.position : Vector3.zero;
            if (root != null && GameplaySfxEmitter.TryPlayAt(root, cue, position, true))
            {
                return;
            }

            GameplaySfxEmitter.TryPlayCatalogAt(cue, position);
        }

        private void ReturnToTitleScene()
        {
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f; // 씬 전환 전 복구
            if (string.IsNullOrWhiteSpace(titleScenePath))
            {
                titleScenePath = DefaultTitleScenePath;
            }

#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(titleScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(DefaultTitleSceneName);
#endif
        }

        private void ResolveReferences()
        {
            if (nexus == null)
            {
                nexus = NexusController.Active != null ? NexusController.Active : FindFirstObjectByType<NexusController>();
            }

            if (waveController == null)
            {
                waveController = FindFirstObjectByType<WaveController>();
            }

            if (coreStats == null)
            {
                coreStats = CoreStatProvider.Active != null ? CoreStatProvider.Active : FindFirstObjectByType<CoreStatProvider>();
            }

            if (overlayView == null)
            {
                overlayView = FindFirstObjectByType<RunResultOverlayView>(FindObjectsInactive.Include);
            }
        }

        private void TrySubscribeNexus()
        {
            if (subscribedToNexus || nexus == null)
            {
                return; // 이미 구독/대상 없음
            }

            nexus.Died += HandleNexusDied;
            subscribedToNexus = true;
        }

        private void UnsubscribeNexus()
        {
            if (!subscribedToNexus || nexus == null)
            {
                subscribedToNexus = false;
                return;
            }

            nexus.Died -= HandleNexusDied;
            subscribedToNexus = false;
        }
    }
}
