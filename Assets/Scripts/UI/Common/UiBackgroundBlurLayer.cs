using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class UiBackgroundBlurLayer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup rootCanvasGroup; // 전체 표시 제어
    [SerializeField] private RawImage blurBackgroundImage; // 캡처 배경
    [SerializeField] private Image dimOverlayImage; // 약한 딤

    [Header("Blur")]
    [Min(2)][SerializeField] private int blurDownsample = 2; // 캡처 축소 배율
    [Range(0, 6)][SerializeField] private int blurIterations = 4; // 다단 블러 반복
    [Range(0f, 1f)][SerializeField] private float backgroundBrightness = 0.58f; // 캡처 배경 자체 밝기
    [Range(0f, 1f)][SerializeField] private float dimAlpha = 0.38f; // 추가 어둡기
    [Min(0.01f)][SerializeField] private float fadeSeconds = 0.18f; // 열림/닫힘 페이드

    private Texture2D capturedTexture; // 런타임 캡처
    private Coroutine showRoutine; // 표시 루틴
    private Sequence fadeSequence; // 페이드 트윈
    private float EffectiveBackgroundBrightness => Mathf.Clamp(backgroundBrightness, 0.45f, 0.9f);
    private float EffectiveDimAlpha => Mathf.Clamp(Mathf.Max(0.38f, dimAlpha), 0f, 0.65f);

    public bool IsVisible => rootCanvasGroup != null && rootCanvasGroup.alpha > 0.001f;

    private void Awake()
    {
        ResolveReferences();
        if (Application.isPlaying)
        {
            HideImmediate(true);
        }
    }

    private void OnDestroy()
    {
        ClearCapturedTexture();
        fadeSequence?.Kill(false);
    }

    public IEnumerator ShowRoutine()
    {
        gameObject.SetActive(true);
        ResolveReferences();
        PrepareHiddenForCapture();

        yield return new WaitForEndOfFrame();

        CaptureBlurBackground();
        PlayFadeIn();
    }

    public void Show()
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
        }

        showRoutine = StartCoroutine(ShowRoutine());
    }

    public void Hide(float duration)
    {
        ResolveReferences();
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        fadeSequence?.Kill(false);
        float safeDuration = Mathf.Max(0.01f, duration);
        fadeSequence = DOTween.Sequence().SetUpdate(true);
        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.blocksRaycasts = false;
            rootCanvasGroup.interactable = false;
            fadeSequence.Join(rootCanvasGroup.DOFade(0f, safeDuration));
        }

        if (dimOverlayImage != null)
        {
            fadeSequence.Join(dimOverlayImage.DOFade(0f, safeDuration));
        }

        if (blurBackgroundImage != null)
        {
            fadeSequence.Join(blurBackgroundImage.DOFade(0f, safeDuration));
        }

        fadeSequence.OnComplete(() => HideImmediate(true));
    }

    public void HideImmediate(bool deactivate)
    {
        ResolveReferences();
        fadeSequence?.Kill(false);
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 0f;
            rootCanvasGroup.blocksRaycasts = false;
            rootCanvasGroup.interactable = false;
        }

        if (blurBackgroundImage != null)
        {
            blurBackgroundImage.enabled = false;
            blurBackgroundImage.texture = null;
            blurBackgroundImage.color = GetBlurColor(0f);
        }

        if (dimOverlayImage != null)
        {
            dimOverlayImage.color = new Color(0f, 0f, 0f, 0f);
        }

        ClearCapturedTexture();
        if (deactivate)
        {
            gameObject.SetActive(false);
        }
    }

    private void ResolveReferences()
    {
        if (rootCanvasGroup == null)
        {
            rootCanvasGroup = GetComponent<CanvasGroup>();
        }
    }

    private void PrepareHiddenForCapture()
    {
        fadeSequence?.Kill(false);
        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 1f;
            rootCanvasGroup.blocksRaycasts = false;
            rootCanvasGroup.interactable = false;
        }

        if (blurBackgroundImage != null)
        {
            blurBackgroundImage.enabled = false;
            blurBackgroundImage.color = GetBlurColor(0f);
        }

        if (dimOverlayImage != null)
        {
            dimOverlayImage.color = new Color(0f, 0f, 0f, 0f);
        }
    }

    private void CaptureBlurBackground()
    {
        ClearCapturedTexture();
        if (blurBackgroundImage == null)
        {
            return;
        }

        capturedTexture = UiBlurCaptureUtility.CaptureBlurredScreenshot(blurDownsample, blurIterations);
        if (capturedTexture == null)
        {
            return;
        }

        blurBackgroundImage.texture = capturedTexture;
        blurBackgroundImage.enabled = true;
    }

    private void PlayFadeIn()
    {
        fadeSequence?.Kill(false);
        if (blurBackgroundImage != null)
        {
            blurBackgroundImage.color = GetBlurColor(0f);
        }

        if (dimOverlayImage != null)
        {
            dimOverlayImage.color = new Color(0f, 0f, 0f, 0f);
        }

        fadeSequence = DOTween.Sequence().SetUpdate(true);
        if (blurBackgroundImage != null)
        {
            fadeSequence.Join(blurBackgroundImage.DOFade(1f, fadeSeconds));
        }

        if (dimOverlayImage != null)
        {
            fadeSequence.Join(dimOverlayImage.DOFade(EffectiveDimAlpha, fadeSeconds));
        }
    }

    private Color GetBlurColor(float alpha)
    {
        float brightness = EffectiveBackgroundBrightness;
        return new Color(brightness, brightness, brightness, alpha);
    }

    private void ClearCapturedTexture()
    {
        if (capturedTexture == null)
        {
            return;
        }

        Destroy(capturedTexture);
        capturedTexture = null;
    }
}
