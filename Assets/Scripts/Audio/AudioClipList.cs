using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 현재 재생 중인 BGM 이름을 TMP 텍스트로 표시하고, 마스크 영역 안에서 좌측으로 흘러가는 연출을 재생합니다.
/// Image(마스크) 하위 Text (TMP) 구조에 붙여 사용하세요.
/// </summary>
[DisallowMultipleComponent]
public sealed class AudioClipList : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private RectTransform scrollContainer;

    [Header("Scroll")]
    [SerializeField, Min(1f)] private float scrollSpeed = 80f;
    [SerializeField, Min(0f)] private float restartDelay = 0.35f;
    [SerializeField, Min(0f)] private float scrollMargin = 12f;
    [SerializeField] private string emptyText = "BGM -";

    [Header("Mask")]
    [SerializeField] private bool ensureRectMask2D = true;

    private RectTransform labelRect;
    private Tween scrollTween;
    private Coroutine restartScrollCoroutine;
    private AudioManager audioManager;
    private AudioClip lastDisplayedClip;
    private string lastDisplayedText = string.Empty;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindAudioManager();
        RefreshFromAudioManager();
    }

    private void OnDisable()
    {
        UnbindAudioManager();
        StopScroll();
    }

    private void OnDestroy()
    {
        StopScroll();
    }

    private void ResolveReferences()
    {
        if (label == null)
        {
            label = GetComponentInChildren<TMP_Text>(true);
        }

        if (scrollContainer == null)
        {
            scrollContainer = transform as RectTransform;
        }

        if (label != null)
        {
            labelRect = label.rectTransform;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
        }

        if (ensureRectMask2D && scrollContainer != null && scrollContainer.GetComponent<RectMask2D>() == null)
        {
            scrollContainer.gameObject.AddComponent<RectMask2D>();
        }
    }

    private void BindAudioManager()
    {
        audioManager = AudioManager.EnsureExists();
        if (audioManager == null)
        {
            return;
        }

        audioManager.CurrentBgmChanged += HandleCurrentBgmChanged;
    }

    private void UnbindAudioManager()
    {
        if (audioManager == null)
        {
            return;
        }

        audioManager.CurrentBgmChanged -= HandleCurrentBgmChanged;
        audioManager = null;
    }

    private void HandleCurrentBgmChanged(AudioClip clip)
    {
        ApplyDisplay(clip);
    }

    private void RefreshFromAudioManager()
    {
        AudioClip clip = audioManager != null ? audioManager.CurrentBgmClip : null;
        ApplyDisplay(clip);
    }

    private void ApplyDisplay(AudioClip clip)
    {
        if (label == null || labelRect == null || scrollContainer == null)
        {
            return;
        }

        string displayText = clip != null ? clip.name : emptyText;
        if (clip == lastDisplayedClip && displayText == lastDisplayedText)
        {
            return;
        }

        lastDisplayedClip = clip;
        lastDisplayedText = displayText;
        label.text = displayText;

        ScheduleRestartScroll();
    }

    private void ScheduleRestartScroll()
    {
        if (restartScrollCoroutine != null)
        {
            StopCoroutine(restartScrollCoroutine);
        }

        restartScrollCoroutine = StartCoroutine(RestartScrollAfterLayout());
    }

    private IEnumerator RestartScrollAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        RestartScroll();
        restartScrollCoroutine = null;
    }

    private void RestartScroll()
    {
        StopScroll();

        label.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();

        RectTransform moveSpace = labelRect.parent as RectTransform;
        if (moveSpace == null)
        {
            return;
        }

        float textWidth = MeasureTextWidth();
        if (!TryGetMarqueeBounds(moveSpace, textWidth, out float startX, out float endX))
        {
            return;
        }

        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(0f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);

        float posY = labelRect.anchoredPosition.y;
        float travelDistance = startX - endX;
        float duration = travelDistance / scrollSpeed;

        scrollTween = DOTween.Sequence()
            .AppendCallback(() => labelRect.anchoredPosition = new Vector2(startX, posY))
            .Append(labelRect.DOAnchorPosX(endX, duration).SetEase(Ease.Linear))
            .AppendInterval(restartDelay)
            .SetLoops(-1)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private float MeasureTextWidth()
    {
        label.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();

        Vector2 preferred = label.GetPreferredValues(label.text, float.PositiveInfinity, float.PositiveInfinity);
        float width = preferred.x;
        if (width <= 1f)
        {
            width = label.textBounds.size.x;
        }

        return Mathf.Max(1f, width);
    }

    private bool TryGetMarqueeBounds(RectTransform moveSpace, float textWidth, out float startX, out float endX)
    {
        startX = 0f;
        endX = 0f;

        if (scrollContainer == null)
        {
            return false;
        }

        Vector3[] viewportCorners = new Vector3[4];
        scrollContainer.GetWorldCorners(viewportCorners);

        float viewportLeft = moveSpace.InverseTransformPoint(viewportCorners[0]).x;
        float viewportRight = moveSpace.InverseTransformPoint(viewportCorners[3]).x;

        startX = viewportRight + scrollMargin;
        endX = viewportLeft - textWidth - scrollMargin;
        return endX < startX;
    }

    private void StopScroll()
    {
        if (restartScrollCoroutine != null)
        {
            StopCoroutine(restartScrollCoroutine);
            restartScrollCoroutine = null;
        }

        if (scrollTween != null && scrollTween.IsActive())
        {
            scrollTween.Kill();
        }

        scrollTween = null;
    }
}
