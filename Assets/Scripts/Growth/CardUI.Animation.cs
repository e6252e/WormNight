using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DG.Tweening;
using TeamProject01.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class CardUI
{
    private void PlaySpawnOpenTween(IReadOnlyList<SpawnedCardEntry> cards)
    {
        if (cards == null || cards.Count == 0)
        {
            return;
        }

        // 카드 패널 등장 사운드 - 레벨업마다 패널이 열릴 때 1회 재생
        cardSound?.PlayCardAppear();

        for (int i = 0; i < cards.Count; i++)
        {
            HideInstant(cards[i]);
        }

        // 이팩트는 카드 오픈 트윈 완료 후 적용 (GetWorldCorners 정확도 보장)
        // 스탯 강화(None), 세그먼트 강화(EnhanceChoice) 카드만 VFX 적용
        if (cardEffect != null)
        {
            const float openDuration = 0.35f;
            const float openInterval = 0.12f;
            for (int i = 0; i < cards.Count; i++)
            {
                SpawnedCardEntry captured = cards[i];
                bool applyVfx = captured.SegmentRole == SegmentCardRole.None
                             || captured.SegmentRole == SegmentCardRole.EnhanceChoice;
                if (!applyVfx) continue;

                float delay = i * openInterval + openDuration;
                DOVirtual.DelayedCall(delay, () =>
                {
                    if (captured?.Root != null)
                        cardEffect.ApplyEffect(captured.Root, GetCardTier(captured));
                }, ignoreTimeScale: true);
            }
        }

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        for (int i = 0; i < cards.Count; i++)
        {
            int index = i;
            sequence.AppendCallback(() => PlayOpenTween(cards[index]));
            sequence.AppendInterval(0.12f);
        }

        sequence.OnComplete(TryStartAutoSelectAfterSpawn); // 등장 연출 후 자동선택 //안건준 수정 - 0628
    }

    private void HideInstant(SpawnedCardEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        entry.IsClickable = false;
        ResetCardTooltipInteraction(entry);
        HideCardHoverTooltip(entry, true);
        entry.RootTransform.DOKill();
        entry.CanvasGroup.DOKill();
        entry.CanvasGroup.alpha = 0f;
        entry.CanvasGroup.blocksRaycasts = false;
        entry.CanvasGroup.interactable = false;
        entry.RootTransform.anchoredPosition = entry.OriginalPosition + new Vector2(0f, startYOffset);
        entry.RootTransform.localScale = Vector3.zero;
    }

    private void PlayOpenTween(SpawnedCardEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        entry.IsClickable = false; // 오픈 트윈 중 툴팁/클릭 차단
        entry.TooltipReady = false;
        entry.CanvasGroup.blocksRaycasts = false;
        entry.CanvasGroup.interactable = entry.CanSelect; // 입력은 막되 Button DisabledColor가 배경 알파를 낮추지 않게 유지

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Join(entry.CanvasGroup.DOFade(1f, 0.25f));
        sequence.Join(entry.RootTransform.DOAnchorPos(entry.OriginalPosition, 0.35f).SetEase(Ease.OutCubic));
        sequence.Join(entry.RootTransform.DOScale(entry.OriginalScale, 0.35f).SetEase(Ease.OutBack));
        sequence.OnComplete(() => FinalizeCardOpen(entry));
    }

    private void FinalizeCardOpen(SpawnedCardEntry entry)
    {
        if (entry == null || entry.Root == null || entry.RootTransform == null || entry.CanvasGroup == null)
        {
            return;
        }

        entry.RootTransform.localScale = entry.OriginalScale; // 툴팁 역스케일 계산 전 안정화
        entry.IsClickable = entry.CanSelect;
        entry.TooltipReady = entry.CanSelect;
        entry.CanvasGroup.blocksRaycasts = entry.CanSelect;
        entry.CanvasGroup.interactable = entry.CanSelect;

        if (!entry.CanSelect)
        {
            HideCardHoverTooltip(entry, true);
            return;
        }

        if (!IsMouseOverCard(entry, out Vector2 screenPosition, out Camera eventCamera))
        {
            return;
        }

        entry.IsPointerOver = true; // 마우스가 이미 카드 위에 있던 연속 레벨업 케이스
        entry.HasTooltipPointer = true;
        entry.LastTooltipScreenPosition = screenPosition;
        entry.LastTooltipCamera = eventCamera;
        SetCardHoverVisual(entry, true);
        TryShowCardHoverTooltip(entry);
    }

    private static bool IsMouseOverCard(SpawnedCardEntry entry, out Vector2 screenPosition, out Camera eventCamera)
    {
        screenPosition = Input.mousePosition;
        eventCamera = ResolveCardEventCamera(entry);
        return entry != null
            && entry.RootTransform != null
            && RectTransformUtility.RectangleContainsScreenPoint(entry.RootTransform, screenPosition, eventCamera);
    }

    private static Camera ResolveCardEventCamera(SpawnedCardEntry entry)
    {
        Canvas canvas = entry?.RootTransform != null ? entry.RootTransform.GetComponentInParent<Canvas>() : null;
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
    }

    private Tween PlaySelectTween(SpawnedCardEntry entry)
    {
        if (entry == null)
        {
            return null;
        }

        entry.IsClickable = false;
        ResetCardTooltipInteraction(entry);
        HideCardHoverTooltip(entry, true);
        entry.RootTransform.DOKill();

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Append(entry.RootTransform.DOScale(entry.OriginalScale * 1.2f, selectionSelectUpSeconds).SetEase(Ease.OutBack));
        sequence.Append(entry.RootTransform.DOScale(entry.OriginalScale, selectionSelectDownSeconds));
        return sequence;
    }

    private Tween PlayHideTween(SpawnedCardEntry entry)
    {
        if (entry == null)
        {
            return null;
        }

        entry.IsClickable = false;
        ResetCardTooltipInteraction(entry);
        HideCardHoverTooltip(entry, true);
        entry.RootTransform.DOKill();
        entry.CanvasGroup.DOKill();

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Join(entry.CanvasGroup.DOFade(0f, 0.2f));
        sequence.Join(entry.RootTransform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack));
        return sequence;
    }
}
