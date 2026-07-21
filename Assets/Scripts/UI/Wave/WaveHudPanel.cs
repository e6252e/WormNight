using DG.Tweening;
using TMPro;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    // Display-only controller for WaveHudRoot.
    // It only switches UI groups and updates text from WaveController state.
    public sealed class WaveHudPanel : MonoBehaviour
    {
        private const string DefaultBonusCollectRemainingFormat = "남은 마력구슬 {0}/{1}개";

        private WaveController waveController;
        private BossWaveController bossWaveController;

        private GameObject normalGroup;
        private GameObject bossGroup;
        private GameObject bonusGroup;

        private TMP_Text normalTitleText;
        private TMP_Text normalEnemyCountText;
        private TMP_Text normalTimeText;

        private TMP_Text bossTitleText;
        private GameObject bossBattleMessageObject;
        private GameObject bossRewardMessageObject;

        private TMP_Text bonusTitleText;
        private TMP_Text bonusCollectMessageText;
        private GameObject bonusCollectMessageObject;
        private GameObject bonusRewardMessageObject;
        private TMP_Text bonusTimeText;

        private GameObject bossStageBanner;
        private RectTransform bossStageBannerRect;
        private CanvasGroup bossStageBannerCanvasGroup;
        private Sequence bossStageBannerSequence;

        private GameObject bonusStageBanner;
        private RectTransform bonusStageBannerRect;
        private CanvasGroup bonusStageBannerCanvasGroup;
        private Sequence bonusStageBannerSequence;
        private WaveController.WaveRunState previousState = WaveController.WaveRunState.Normal;

        [Header("Display Text")]
        [SerializeField] private string normalTitleFormat = "WAVE {0}";
        [SerializeField] private string normalEnemyFormat = "{0} Enemies Left";
        [SerializeField] private string bossTitle = "BOSS STAGE";
        [SerializeField] private string bonusTitle = "BONUS STAGE";
        [SerializeField] private string bonusCollectRemainingFormat = DefaultBonusCollectRemainingFormat;
        [SerializeField] private string missingControllerText = "WaveController Missing";

        [Header("Stage Banner Tween")]
        [SerializeField] private float bannerShowDuration = 0.25f;
        [SerializeField] private float bannerStayDuration = 1.2f;
        [SerializeField] private float bannerHideDuration = 0.25f;
        [SerializeField] private float bannerStartScale = 0.75f;
        [SerializeField] private float bannerPopScale = 1.08f;
        [SerializeField] private float bannerEndScale = 1.0f;

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
            HideStageBannersImmediate();
            Refresh();
        }

        private void OnEnable()
        {
            ResolveReferences();
            HideStageBannersImmediate();
            Refresh();
        }

        private void OnDisable()
        {
            KillStageBannerTween(ref bossStageBannerSequence);
            KillStageBannerTween(ref bonusStageBannerSequence);
        }

        private void Update()
        {
            if (waveController == null)
            {
                ResolveReferences();
            }

            Refresh();
        }

        private void ResolveReferences()
        {
            waveController = waveController != null ? waveController : FindFirstObjectByType<WaveController>();
            bossWaveController = bossWaveController != null ? bossWaveController : FindFirstObjectByType<BossWaveController>();

            normalGroup = normalGroup != null ? normalGroup : FindChildObject("NormalGroup");
            bossGroup = bossGroup != null ? bossGroup : FindChildObject("BossGroup");
            bonusGroup = bonusGroup != null ? bonusGroup : FindChildObject("BonusGroup");

            normalTitleText = normalTitleText != null ? normalTitleText : FindGroupText(normalGroup, "TitleText");
            normalEnemyCountText = normalEnemyCountText != null ? normalEnemyCountText : FindGroupText(normalGroup, "EnemyCountText");
            normalTimeText = normalTimeText != null ? normalTimeText : FindGroupText(normalGroup, "TimeText");

            bossTitleText = bossTitleText != null ? bossTitleText : FindGroupText(bossGroup, "TitleText");
            bossBattleMessageObject = bossBattleMessageObject != null ? bossBattleMessageObject : FindGroupObject(bossGroup, "BattleMessageText");
            bossRewardMessageObject = bossRewardMessageObject != null ? bossRewardMessageObject : FindGroupObject(bossGroup, "RewardMessageText");

            bonusTitleText = bonusTitleText != null ? bonusTitleText : FindGroupText(bonusGroup, "TitleText");
            bonusCollectMessageObject = bonusCollectMessageObject != null ? bonusCollectMessageObject : FindGroupObject(bonusGroup, "CollectMessageText");
            bonusCollectMessageText = bonusCollectMessageText != null ? bonusCollectMessageText : GetComponentFromObject<TMP_Text>(bonusCollectMessageObject);
            bonusRewardMessageObject = bonusRewardMessageObject != null ? bonusRewardMessageObject : FindGroupObject(bonusGroup, "RewardMessageText");
            bonusTimeText = bonusTimeText != null ? bonusTimeText : FindGroupText(bonusGroup, "TimeText");

            bossStageBanner = bossStageBanner != null ? bossStageBanner : FindChildObject("BossStageBanner");
            bossStageBannerRect = bossStageBannerRect != null && bossStageBannerRect.gameObject == bossStageBanner
                ? bossStageBannerRect
                : GetComponentFromObject<RectTransform>(bossStageBanner);
            bossStageBannerCanvasGroup = bossStageBannerCanvasGroup != null && bossStageBannerCanvasGroup.gameObject == bossStageBanner
                ? bossStageBannerCanvasGroup
                : GetComponentFromObject<CanvasGroup>(bossStageBanner);

            bonusStageBanner = bonusStageBanner != null ? bonusStageBanner : FindChildObject("BonusStageBanner");
            bonusStageBannerRect = bonusStageBannerRect != null && bonusStageBannerRect.gameObject == bonusStageBanner
                ? bonusStageBannerRect
                : GetComponentFromObject<RectTransform>(bonusStageBanner);
            bonusStageBannerCanvasGroup = bonusStageBannerCanvasGroup != null && bonusStageBannerCanvasGroup.gameObject == bonusStageBanner
                ? bonusStageBannerCanvasGroup
                : GetComponentFromObject<CanvasGroup>(bonusStageBanner);

            if (Application.isPlaying && bossStageBanner != null && bossStageBannerCanvasGroup == null)
            {
                bossStageBannerCanvasGroup = bossStageBanner.AddComponent<CanvasGroup>();
            }

            if (Application.isPlaying && bonusStageBanner != null && bonusStageBannerCanvasGroup == null)
            {
                bonusStageBannerCanvasGroup = bonusStageBanner.AddComponent<CanvasGroup>();
            }
        }

        private void Refresh()
        {
            if (waveController == null)
            {
                ShowNormal();
                SetText(normalTitleText, "WAVE");
                SetText(normalEnemyCountText, missingControllerText);
                SetText(normalTimeText, "00:00");
                previousState = WaveController.WaveRunState.Normal;
                return;
            }

            WaveController.WaveRunState currentState = waveController.CurrentState;

            if (currentState != previousState)
            {
                if (currentState == WaveController.WaveRunState.Boss)
                {
                    PlayBossStageBanner();
                }
                else if (currentState == WaveController.WaveRunState.Special)
                {
                    PlayBonusStageBanner();
                }
            }

            previousState = currentState;

            switch (currentState)
            {
                case WaveController.WaveRunState.Boss:
                    RefreshBoss();
                    break;
                case WaveController.WaveRunState.Special:
                    RefreshBonus();
                    break;
                default:
                    RefreshNormal();
                    break;
            }
        }

        private void PlayBonusStageBanner()
        {
            ResolveReferences();
            PlayStageBanner(bonusStageBanner, bonusStageBannerRect, bonusStageBannerCanvasGroup, ref bonusStageBannerSequence);
        }

        private void PlayBossStageBanner()
        {
            ResolveReferences();
            PlayStageBanner(bossStageBanner, bossStageBannerRect, bossStageBannerCanvasGroup, ref bossStageBannerSequence);
        }

        private void PlayStageBanner(
            GameObject banner,
            RectTransform bannerRect,
            CanvasGroup bannerCanvasGroup,
            ref Sequence bannerSequence)
        {
            if (banner == null || bannerRect == null || bannerCanvasGroup == null)
            {
                return;
            }

            KillStageBannerTween(ref bannerSequence);

            float showDuration = Mathf.Max(0.01f, bannerShowDuration);
            float stayDuration = Mathf.Max(0.0f, bannerStayDuration);
            float hideDuration = Mathf.Max(0.01f, bannerHideDuration);
            float startScale = Mathf.Max(0.01f, bannerStartScale);
            float popScale = Mathf.Max(0.01f, bannerPopScale);
            float endScale = Mathf.Max(0.01f, bannerEndScale);

            banner.SetActive(true);
            bannerCanvasGroup.alpha = 0.0f;
            bannerCanvasGroup.interactable = false;
            bannerCanvasGroup.blocksRaycasts = false;
            bannerRect.localScale = Vector3.one * startScale;

            bannerSequence = DOTween.Sequence()
                .SetUpdate(true)
                .Append(bannerCanvasGroup.DOFade(1.0f, showDuration))
                .Join(bannerRect.DOScale(popScale, showDuration).SetEase(Ease.OutBack))
                .Append(bannerRect.DOScale(endScale, showDuration * 0.35f).SetEase(Ease.OutQuad))
                .AppendInterval(stayDuration)
                .Append(bannerCanvasGroup.DOFade(0.0f, hideDuration))
                .OnComplete(() =>
                {
                    if (banner != null)
                    {
                        banner.SetActive(false);
                    }
                });
        }

        private void HideStageBannersImmediate()
        {
            ResolveReferences();
            HideStageBannerImmediate(bossStageBanner, bossStageBannerRect, bossStageBannerCanvasGroup);
            HideStageBannerImmediate(bonusStageBanner, bonusStageBannerRect, bonusStageBannerCanvasGroup);
        }

        private void HideStageBannerImmediate(
            GameObject banner,
            RectTransform bannerRect,
            CanvasGroup bannerCanvasGroup)
        {
            if (bannerCanvasGroup != null)
            {
                bannerCanvasGroup.alpha = 0.0f;
                bannerCanvasGroup.interactable = false;
                bannerCanvasGroup.blocksRaycasts = false;
            }

            if (bannerRect != null)
            {
                bannerRect.localScale = Vector3.one * Mathf.Max(0.01f, bannerEndScale);
            }

            SetActive(banner, false);
        }

        private static void KillStageBannerTween(ref Sequence bannerSequence)
        {
            if (bannerSequence != null)
            {
                bannerSequence.Kill();
                bannerSequence = null;
            }
        }

        private void RefreshNormal()
        {
            ShowNormal();
            SetText(normalTitleText, string.Format(normalTitleFormat, waveController.CurrentStage));
            SetText(normalEnemyCountText, string.Format(normalEnemyFormat, waveController.CurrentStageRemainingEnemyCount));
            SetText(normalTimeText, FormatTime(waveController.RemainingStageSeconds));
        }

        private void RefreshBoss()
        {
            ShowBoss();
            SetText(bossTitleText, bossTitle);

            bool hasActiveBoss = bossWaveController != null && bossWaveController.HasActiveBoss;
            SetActive(bossBattleMessageObject, hasActiveBoss);
            SetActive(bossRewardMessageObject, !hasActiveBoss);
        }

        private void RefreshBonus()
        {
            ShowBonus();
            SetText(bonusTitleText, bonusTitle);

            ManaOrbCollectSpecialWave manaOrbWave = waveController.CurrentManaOrbCollectSpecialWave;
            bool isRewardStage = manaOrbWave != null && manaOrbWave.IsRewardStageActive;
            bool isCollectStage = !isRewardStage;

            SetActive(bonusCollectMessageObject, isCollectStage);
            SetActive(bonusRewardMessageObject, isRewardStage);
            SetActive(bonusTimeText, isCollectStage);

            if (isCollectStage)
            {
                float seconds = manaOrbWave != null ? manaOrbWave.RemainingCollectSeconds : waveController.RemainingStageSeconds;
                SetText(bonusTimeText, FormatTime(seconds));
                SetText(bonusCollectMessageText, FormatManaOrbRemainingText(manaOrbWave));
            }
        }

        private void ShowNormal()
        {
            SetActive(normalGroup, true);
            SetActive(bossGroup, false);
            SetActive(bonusGroup, false);
        }

        private void ShowBoss()
        {
            SetActive(normalGroup, false);
            SetActive(bossGroup, true);
            SetActive(bonusGroup, false);
        }

        private void ShowBonus()
        {
            SetActive(normalGroup, false);
            SetActive(bossGroup, false);
            SetActive(bonusGroup, true);
        }

        private GameObject FindChildObject(string childName)
        {
            Transform child = FindChild(transform, childName);
            return child != null ? child.gameObject : null;
        }

        private static TMP_Text FindGroupText(GameObject group, string childName)
        {
            GameObject child = FindGroupObject(group, childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static GameObject FindGroupObject(GameObject group, string childName)
        {
            if (group == null)
            {
                return null;
            }

            Transform child = FindChild(group.transform, childName);
            return child != null ? child.gameObject : null;
        }

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == childName)
                {
                    return children[i];
                }
            }

            return null;
        }

        private static T GetComponentFromObject<T>(GameObject target) where T : Component
        {
            return target != null ? target.GetComponent<T>() : null;
        }

        private static string FormatTime(float seconds)
        {
            int totalSeconds = Mathf.CeilToInt(Mathf.Max(0.0f, seconds));
            int minutes = totalSeconds / 60;
            int remainSeconds = totalSeconds % 60;
            return $"{minutes:00}:{remainSeconds:00}";
        }

        private string FormatManaOrbRemainingText(ManaOrbCollectSpecialWave manaOrbWave)
        {
            int total = manaOrbWave != null ? manaOrbWave.SpawnedManaOrbCount : 0;
            int remaining = manaOrbWave != null ? manaOrbWave.RemainingManaOrbCount : 0;
            return string.Format(ResolveManaOrbRemainingFormat(bonusCollectRemainingFormat), remaining, total);
        }

        private static string ResolveManaOrbRemainingFormat(string format)
        {
            if (string.IsNullOrWhiteSpace(format) || IsLegacyManaOrbRemainingFormat(format))
            {
                return DefaultBonusCollectRemainingFormat;
            }

            return format;
        }

        private static bool IsLegacyManaOrbRemainingFormat(string format)
        {
            return format.Contains("마력구슬이") || format.Contains("개 남았습니다");
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null && text.text != value)
            {
                text.text = value;
            }
        }

        private static void SetActive(TMP_Text text, bool active)
        {
            if (text != null)
            {
                SetActive(text.gameObject, active);
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
