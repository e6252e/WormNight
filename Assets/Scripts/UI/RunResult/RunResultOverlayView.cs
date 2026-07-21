using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    [ExecuteAlways]
    public sealed class RunResultOverlayView : MonoBehaviour // 게임오버/클리어 결과 팝업
    {
        [Serializable]
        private sealed class PopupLayout // 결과 타입별 전용 레이아웃
        {
            [SerializeField] private RectTransform popupRoot; // 전용 루트
            [SerializeField] private Image panelImage; // 패널 이미지
            [SerializeField] private Button returnTitleButton; // 복귀 버튼
            [SerializeField] private Image returnTitleButtonImage; // 버튼 이미지
            [SerializeField] private Text returnTitleButtonText; // 버튼 텍스트
            [SerializeField] private Text waveLabelText; // 웨이브 라벨
            [SerializeField] private Text waveValueText; // 웨이브 값
            [SerializeField] private Text killLabelText; // 처치 라벨
            [SerializeField] private Text killValueText; // 처치 값
            [SerializeField] private Text diamondLabelText; // 다이아 라벨
            [SerializeField] private Text diamondValueText; // 다이아 값

            public bool HasRoot => popupRoot != null; // 사용 가능 여부
            public RectTransform Root => popupRoot; // 트윈 대상

            public void SetActive(bool active)
            {
                if (popupRoot != null)
                {
                    popupRoot.gameObject.SetActive(active);
                }
            }

            public void SetScale(float scale)
            {
                if (popupRoot != null)
                {
                    popupRoot.localScale = Vector3.one * scale;
                }
            }

            public void SetShowPosition(bool centerOnShow, Vector2 showPosition)
            {
                if (popupRoot != null && centerOnShow)
                {
                    popupRoot.anchoredPosition = showPosition;
                }
            }

            public void ApplyData(RunResultDisplayData data, Sprite panelSprite, Sprite buttonSprite)
            {
                if (panelImage != null && panelSprite != null)
                {
                    panelImage.sprite = panelSprite;
                }

                if (returnTitleButtonImage != null && buttonSprite != null)
                {
                    returnTitleButtonImage.sprite = buttonSprite;
                }

                SetText(waveLabelText, "도달 웨이브");
                SetText(killLabelText, "처치 몬스터");
                SetText(diamondLabelText, "총 지급 다이아");
                SetText(waveValueText, data.ReachedWave.ToString());
                SetText(killValueText, data.KillCount.ToString());
                SetText(diamondValueText, data.DisplayDiamond.ToString());
                SetText(returnTitleButtonText, "타이틀로 돌아가기");
            }

            public void BindReturnButton(Action onReturnTitle)
            {
                if (returnTitleButton == null)
                {
                    return;
                }

                returnTitleButton.onClick.RemoveAllListeners();
                returnTitleButton.onClick.AddListener(() => onReturnTitle?.Invoke());
            }

            public void ApplyEditorPreviewState()
            {
                SetScale(1f);
            }
        }

        [Header("Root")]
        [SerializeField] private CanvasGroup rootCanvasGroup; // 전체 페이드
        [SerializeField] private RawImage blurBackgroundImage; // 캡처 배경
        [SerializeField] private Image dimOverlayImage; // 어두운 오버레이

        [Header("Legacy Single Layout")]
        [SerializeField] private RectTransform popupRoot; // 기존 단일 팝업 루트

        [Header("Panel Sprites")]
        [SerializeField] private Image panelImage; // 결과 패널 이미지
        [SerializeField] private Sprite clearPanelSprite; // 클리어 패널
        [SerializeField] private Sprite gameOverPanelSprite; // 게임오버 패널

        [Header("Button Sprites")]
        [SerializeField] private Button returnTitleButton; // 타이틀 복귀 버튼
        [SerializeField] private Image returnTitleButtonImage; // 버튼 이미지
        [SerializeField] private Sprite clearButtonSprite; // 클리어 버튼
        [SerializeField] private Sprite gameOverButtonSprite; // 게임오버 버튼
        [SerializeField] private Text returnTitleButtonText; // 버튼 텍스트

        [Header("Result Text")]
        [SerializeField] private Text waveLabelText; // 도달 웨이브 라벨
        [SerializeField] private Text waveValueText; // 도달 웨이브 값
        [SerializeField] private Text killLabelText; // 처치 라벨
        [SerializeField] private Text killValueText; // 처치 값
        [SerializeField] private Text diamondLabelText; // 다이아 라벨
        [SerializeField] private Text diamondValueText; // 다이아 값

        [Header("Split Layouts")]
        [SerializeField] private PopupLayout clearLayout = new PopupLayout(); // 클리어 전용 레이아웃
        [SerializeField] private PopupLayout gameOverLayout = new PopupLayout(); // 게임오버 전용 레이아웃
        [SerializeField] private bool showBothLayoutsInEditor = true; // 편집 중 둘 다 표시

        [Header("Animation")]
        [SerializeField] private float openFadeSeconds = 0.18f; // 전체 페이드
        [SerializeField] private float closeFadeSeconds = 0.18f; // 닫힘 페이드
        [SerializeField] private float openScaleSeconds = 0.32f; // 팝업 스케일
        [SerializeField] private float popupStartScale = 0.82f; // 시작 배율
        [SerializeField] private float blurBackgroundBrightness = 0.58f; // 캡처 배경 자체 밝기
        [SerializeField] private float dimAlpha = 0.38f; // 딤 알파
        [SerializeField] private int blurDownsample = 2; // 배경 저해상도 배율
        [Range(0, 6)][SerializeField] private int blurIterations = 4; // 다단 블러 반복
        [SerializeField] private bool centerPopupOnShow = true; // 표시 시 중앙 보정
        [SerializeField] private Vector2 popupShowPosition = Vector2.zero; // 표시 위치

        private Texture2D capturedTexture; // 캡처 텍스처
        private Sequence openSequence; // 등장 연출
        private PopupLayout activeLayout; // 현재 표시 레이아웃
        private bool isClosing; // 중복 클릭 방지
        private float EffectiveBackgroundBrightness => Mathf.Clamp(blurBackgroundBrightness, 0.45f, 0.9f);
        private float EffectiveDimAlpha => Mathf.Clamp(Mathf.Max(0.38f, dimAlpha), 0f, 0.65f);

        private void Awake()
        {
            ResolveReferences(); // 누락 참조 보강
            if (Application.isPlaying)
            {
                HideImmediate(false); // 런타임 시작 시 숨김
            }
        }

        private void OnEnable()
        {
            ApplyEditorPreviewState(); // 에디터에서는 수정 가능 상태 유지
        }

        private void OnValidate()
        {
            ApplyEditorPreviewState(); // 인스펙터 변경 시 흰 배경 방지
        }

        private void OnDestroy()
        {
            ClearCapturedTexture(); // 캡처 해제
            openSequence?.Kill(false); // 트윈 정리
        }

        public IEnumerator ShowRoutine(RunResultDisplayData data, Action onReturnTitle)
        {
            gameObject.SetActive(true); // 코루틴 실행용 활성화
            ResolveReferences();
            isClosing = false;
            PrepareHiddenForCapture(); // 캡처에 팝업이 찍히지 않게 숨김

            yield return new WaitForEndOfFrame(); // 최종 화면 캡처 타이밍

            CaptureBlurBackground(); // 배경 스냅샷
            ApplyData(data); // 텍스트/이미지 적용
            BindReturnButton(onReturnTitle); // 버튼 연결
            PlayOpenTween(); // DOTween 팝업
        }

        public void HideImmediate(bool deactivate)
        {
            ResolveReferences();
            openSequence?.Kill(false);
            isClosing = false;
            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 0f;
                rootCanvasGroup.interactable = false;
                rootCanvasGroup.blocksRaycasts = false;
            }

            if (dimOverlayImage != null)
            {
                dimOverlayImage.color = new Color(0f, 0f, 0f, 0f);
            }

            SetAllPopupScales(popupStartScale);

            if (deactivate)
            {
                gameObject.SetActive(false);
            }
        }

        private void PrepareHiddenForCapture()
        {
            HideImmediate(false);
            if (blurBackgroundImage != null)
            {
                blurBackgroundImage.enabled = false; // 캡처 전 배경 이미지 숨김
                blurBackgroundImage.color = GetBlurColor(0f);
            }
        }

        private void CaptureBlurBackground()
        {
            ClearCapturedTexture();
            if (blurBackgroundImage == null)
            {
                return; // 대상 없음
            }

            capturedTexture = UiBlurCaptureUtility.CaptureBlurredScreenshot(blurDownsample, blurIterations);
            if (capturedTexture == null)
            {
                return; // 캡처 실패
            }

            blurBackgroundImage.texture = capturedTexture;
            blurBackgroundImage.enabled = true;
            blurBackgroundImage.color = GetBlurColor(1f);
        }

        private void ClearCapturedTexture()
        {
            if (capturedTexture == null)
            {
                return; // 해제 대상 없음
            }

            Destroy(capturedTexture);
            capturedTexture = null;
            if (blurBackgroundImage != null)
            {
                blurBackgroundImage.texture = null;
            }
        }

        private void ApplyData(RunResultDisplayData data)
        {
            activeLayout = SelectLayout(data.IsClear);
            ApplyLayoutVisibility(data.IsClear);

            Sprite panelSprite = data.IsClear ? clearPanelSprite : gameOverPanelSprite;
            Sprite buttonSprite = data.IsClear ? clearButtonSprite : gameOverButtonSprite;

            if (activeLayout != null)
            {
                activeLayout.ApplyData(data, panelSprite, buttonSprite);
                return;
            }

            if (panelImage != null)
            {
                panelImage.sprite = panelSprite;
            }

            if (returnTitleButtonImage != null)
            {
                returnTitleButtonImage.sprite = buttonSprite;
            }

            SetText(waveLabelText, "도달 웨이브");
            SetText(killLabelText, "처치 몬스터");
            SetText(diamondLabelText, "총 지급 다이아");
            SetText(waveValueText, data.ReachedWave.ToString());
            SetText(killValueText, data.KillCount.ToString());
            SetText(diamondValueText, data.DisplayDiamond.ToString());
            SetText(returnTitleButtonText, "타이틀로 돌아가기");
        }

        private void BindReturnButton(Action onReturnTitle)
        {
            Action closeAction = () => PlayCloseTween(onReturnTitle);
            if (activeLayout != null)
            {
                activeLayout.BindReturnButton(closeAction);
                return;
            }

            if (returnTitleButton == null)
            {
                return; // 버튼 없음
            }

            returnTitleButton.onClick.RemoveAllListeners();
            returnTitleButton.onClick.AddListener(() => closeAction.Invoke());
        }

        private void PlayOpenTween()
        {
            openSequence?.Kill(false);
            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 0f;
                rootCanvasGroup.interactable = true;
                rootCanvasGroup.blocksRaycasts = true;
            }

            RectTransform tweenRoot = activeLayout != null ? activeLayout.Root : null;
            if (tweenRoot != null)
            {
                activeLayout.SetShowPosition(centerPopupOnShow, popupShowPosition); // 편집 위치와 런타임 위치 분리
                tweenRoot.localScale = Vector3.one * popupStartScale;
            }
            else if (popupRoot != null)
            {
                if (centerPopupOnShow)
                {
                    popupRoot.anchoredPosition = popupShowPosition; // 기존 레이아웃 fallback
                }

                popupRoot.localScale = Vector3.one * popupStartScale;
            }

            if (dimOverlayImage != null)
            {
                dimOverlayImage.color = new Color(0f, 0f, 0f, 0f);
            }

            openSequence = DOTween.Sequence().SetUpdate(true);
            if (rootCanvasGroup != null)
            {
                openSequence.Join(rootCanvasGroup.DOFade(1f, openFadeSeconds));
            }

            if (dimOverlayImage != null)
            {
                openSequence.Join(dimOverlayImage.DOFade(EffectiveDimAlpha, openFadeSeconds));
            }

            if (tweenRoot != null)
            {
                openSequence.Join(tweenRoot.DOScale(Vector3.one, openScaleSeconds).SetEase(Ease.OutBack));
            }
            else if (popupRoot != null)
            {
                openSequence.Join(popupRoot.DOScale(Vector3.one, openScaleSeconds).SetEase(Ease.OutBack));
            }
        }

        private void PlayCloseTween(Action onClosed)
        {
            if (isClosing)
            {
                return;
            }

            isClosing = true;
            openSequence?.Kill(false);
            float duration = Mathf.Max(0.01f, closeFadeSeconds);
            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.interactable = false;
                rootCanvasGroup.blocksRaycasts = false;
            }

            openSequence = DOTween.Sequence().SetUpdate(true);
            if (rootCanvasGroup != null)
            {
                openSequence.Join(rootCanvasGroup.DOFade(0f, duration));
            }

            if (dimOverlayImage != null)
            {
                openSequence.Join(dimOverlayImage.DOFade(0f, duration));
            }

            if (blurBackgroundImage != null)
            {
                openSequence.Join(blurBackgroundImage.DOFade(0f, duration));
            }

            if (rootCanvasGroup == null && dimOverlayImage == null && blurBackgroundImage == null)
            {
                openSequence.AppendInterval(duration);
            }

            openSequence.OnComplete(() =>
            {
                ClearCapturedTexture();
                onClosed?.Invoke();
            });
        }

        private void ResolveReferences()
        {
            if (rootCanvasGroup == null)
            {
                rootCanvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private void ApplyEditorPreviewState()
        {
            if (Application.isPlaying)
            {
                return; // 런타임 상태는 결과창 흐름이 제어
            }

            ResolveReferences();
            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 1f; // 프리팹/씬 편집 시 보이게 유지
                rootCanvasGroup.interactable = false;
                rootCanvasGroup.blocksRaycasts = false;
            }

            if (blurBackgroundImage != null)
            {
                blurBackgroundImage.enabled = false; // 캡처 배경은 런타임 전용
                blurBackgroundImage.raycastTarget = false;
                blurBackgroundImage.color = GetBlurColor(0f);
            }

            if (dimOverlayImage != null)
            {
                dimOverlayImage.raycastTarget = false;
                dimOverlayImage.color = new Color(0f, 0f, 0f, 0f);
            }

            ApplyEditorLayoutPreviewState();
        }

        private Color GetBlurColor(float alpha)
        {
            float brightness = EffectiveBackgroundBrightness;
            return new Color(brightness, brightness, brightness, alpha);
        }

        private PopupLayout SelectLayout(bool isClear)
        {
            PopupLayout candidate = isClear ? clearLayout : gameOverLayout;
            return candidate != null && candidate.HasRoot ? candidate : null;
        }

        private void ApplyLayoutVisibility(bool isClear)
        {
            if (clearLayout != null && clearLayout.HasRoot)
            {
                clearLayout.SetActive(isClear);
            }

            if (gameOverLayout != null && gameOverLayout.HasRoot)
            {
                gameOverLayout.SetActive(!isClear);
            }
        }

        private void SetAllPopupScales(float scale)
        {
            if (clearLayout != null && clearLayout.HasRoot)
            {
                clearLayout.SetScale(scale);
            }

            if (gameOverLayout != null && gameOverLayout.HasRoot)
            {
                gameOverLayout.SetScale(scale);
            }

            if (popupRoot != null)
            {
                popupRoot.localScale = Vector3.one * scale;
            }
        }

        private void ApplyEditorLayoutPreviewState()
        {
            if (clearLayout != null && clearLayout.HasRoot)
            {
                clearLayout.ApplyEditorPreviewState();
                if (showBothLayoutsInEditor)
                {
                    clearLayout.SetActive(true);
                }
            }

            if (gameOverLayout != null && gameOverLayout.HasRoot)
            {
                gameOverLayout.ApplyEditorPreviewState();
                if (showBothLayoutsInEditor)
                {
                    gameOverLayout.SetActive(true);
                }
            }

            if (popupRoot != null && (clearLayout == null || !clearLayout.HasRoot) && (gameOverLayout == null || !gameOverLayout.HasRoot))
            {
                popupRoot.localScale = Vector3.one;
            }
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }
    }
}
