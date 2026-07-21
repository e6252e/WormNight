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
    private bool rerollButtonHoverActive; // 리롤 버튼 호버 확대 중복 방지

    private void SetupRerollUi()
    {
        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveListener(HandleRerollButtonClicked); // 중복 등록 방지
            rerollButton.onClick.AddListener(HandleRerollButtonClicked); // 씬 배치 버튼 클릭 연결
            if (rerollButtonImage == null)
            {
                rerollButtonImage = rerollButton.targetGraphic as Image;
            }

            if (rerollButtonImage == null)
            {
                rerollButtonImage = rerollButton.GetComponent<Image>();
            }

            SetupRerollButtonHoverBridge();
        }

        RefreshRerollUi(); // 초기 닫힘 상태 반영
    }

    private void SetupRerollButtonHoverBridge()
    {
        if (rerollButton == null)
        {
            return;
        }

        RerollButtonHoverBridge bridge = rerollButton.GetComponent<RerollButtonHoverBridge>();
        if (bridge == null)
        {
            bridge = rerollButton.gameObject.AddComponent<RerollButtonHoverBridge>();
        }

        bridge.Initialize(this);
    }

    internal void NotifyRerollButtonPointerEnter()
    {
        if (rerollButton == null || !rerollButton.interactable)
        {
            return;
        }

        SetRerollButtonHoverVisual(true);
    }

    internal void NotifyRerollButtonPointerExit()
    {
        SetRerollButtonHoverVisual(false);
    }

    private void SetRerollButtonHoverVisual(bool active)
    {
        if (rerollButton == null)
        {
            return;
        }

        if (active == rerollButtonHoverActive)
        {
            return;
        }

        rerollButtonHoverActive = active;
        RectTransform rt = rerollButton.transform as RectTransform;
        if (rt == null)
        {
            return;
        }

        rt.DOKill();
        rt.DOScale(active ? Vector3.one * hoverScale : Vector3.one, 0.15f)
            .SetEase(active ? Ease.OutQuad : Ease.InQuad)
            .SetUpdate(true);
    }

    private void BeginRerollForPanelOpen()
    {
        remainingRerollCount = activePanelMode == CardPanelMode.RewardChoice
            ? 0
            : ResolveMagicBookRerollCount(); // 현재 장착 마법책 수량
        if (activePanelMode == CardPanelMode.SegmentTicketChoice)
        {
            remainingRerollCount += Mathf.Max(0, segmentTicketBonusRerollCount); // 선택권 보너스
        }

        rerollAllowedForCurrentChoices = false; // 카드 생성 전에는 비활성
        RefreshRerollUi();
    }

    private int ResolveMagicBookRerollCount()
    {
        rerollCountsBySegmentId.Clear(); // 이전 집계 제거
        ConvoyController convoy = CoreStatProvider.Active != null ? CoreStatProvider.Active.Convoy : null;
        if (convoy == null)
        {
            return 0; // 컨보이 없음
        }

        convoy.CollectAttachedSegmentCounts(rerollCountsBySegmentId); // 장착 세그먼트 ID별 수량
        return rerollCountsBySegmentId.TryGetValue(MagicBookRerollSegmentId, out int count)
            ? Mathf.Max(0, count)
            : 0;
    }

    private void HandleRerollButtonClicked()
    {
        if (!CanRerollCurrentChoices())
        {
            RefreshRerollUi(); // 클릭 불가 상태 재반영
            return;
        }

        cardSound?.PlayRerollClick();
        PlayRerollButtonClickTween();

        remainingRerollCount = Mathf.Max(0, remainingRerollCount - 1); // 리롤 1회 소비
        StopAutoSelect(); // 재생성 중 자동 선택 중지
        ClearSpawnedCards(); // 현재 후보 제거
        SpawnCardsForCurrentPhase();
        RefreshRerollUi();
    }

    private void PlayRerollButtonClickTween()
    {
        if (rerollButton == null)
        {
            return;
        }

        RectTransform rt = rerollButton.transform as RectTransform;
        if (rt == null)
        {
            return;
        }

        rerollButtonHoverActive = false;
        rt.DOKill();
        Sequence seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(rt.DOScale(Vector3.one * 1.2f, selectionSelectUpSeconds).SetEase(Ease.OutBack));
        seq.Append(rt.DOScale(Vector3.one, selectionSelectDownSeconds));
    }

    private bool CanRerollCurrentChoices()
    {
        return remainingRerollCount > 0
            && activePanelMode != CardPanelMode.RewardChoice
            && rerollAllowedForCurrentChoices
            && !isProcessingSelection
            && IsLevelUpPanelOpen(); // 선택 처리 중/패널 닫힘 방지
    }

    private void RefreshRerollUi()
    {
        bool panelOpen = IsLevelUpPanelOpen(); // CanvasGroup 기준 표시 여부
        if (rerollUiRoot != null)
        {
            rerollUiRoot.SetActive(panelOpen && activePanelMode != CardPanelMode.RewardChoice); // 보상 선택은 리롤 없음
        }

        if (rerollCountText != null)
        {
            rerollCountText.text = $"남은 {remainingRerollCount}"; // 우측 남은 횟수
        }

        if (rerollButton != null)
        {
            bool canReroll = CanRerollCurrentChoices(); // 가능할 때만 클릭
            rerollButton.interactable = canReroll;
            ApplyRerollButtonVisual(canReroll);
            if (!canReroll)
            {
                SetRerollButtonHoverVisual(false); // 비활성 시 호버 스케일 복원
            }
        }
    }

    private void ApplyRerollButtonVisual(bool canReroll)
    {
        if (rerollButtonImage == null)
        {
            return;
        }

        Sprite sprite = canReroll ? rerollButtonActiveSprite : rerollButtonDisabledSprite;
        if (sprite == null)
        {
            return;
        }

        rerollButtonImage.sprite = sprite;
        rerollButtonImage.color = Color.white;
        rerollButtonImage.preserveAspect = true;
    }

    // 안건준 추가 - 0622 : 현재 자동궤도 모드 여부 확인
}
