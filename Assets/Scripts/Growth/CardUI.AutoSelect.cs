using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TeamProject01.Gameplay;
using UnityEngine;

public partial class CardUI
{
    internal bool IsAutoSelectInProgress => autoSelectRoutine != null;

    private bool pendingAutoSelectSegmentAction;
    private bool pendingAutoSelectCanAdd;
    private bool pendingAutoSelectCanLevelUp;

    internal void ScheduleAutoSelectSegmentAction(bool canAdd, bool canLevelUp)
    {
        pendingAutoSelectSegmentAction = true;
        pendingAutoSelectCanAdd = canAdd;
        pendingAutoSelectCanLevelUp = canLevelUp;
    }

    private void TryStartAutoSelect()
    {
        if (!CanAutoSelectCurrentLevelUpPanel())
        {
            return; // 보상/선택권·수동모드·패널 닫힘은 직접 선택 유지
        }

        StopAutoSelect();
        autoSelectRoutine = StartCoroutine(AutoSelectRoutine());
    }

    private void TryStartAutoSelectSegmentAction(bool canAdd, bool canLevelUp)
    {
        if (!CanAutoSelectCurrentLevelUpPanel())
        {
            return;
        }

        StopAutoSelect();
        autoSelectRoutine = StartCoroutine(AutoSelectSegmentActionRoutine(canAdd, canLevelUp));
    }

    private void TryRestartAutoSelectForCurrentPanel()
    {
        if (!spawnedForCurrentOpen || isProcessingSelection)
        {
            return; // 아직 카드 생성 전이거나 선택 처리 중
        }

        TryStartAutoSelect(); // 현재 열린 카드 묶음 기준 재시작
    }

    private bool CanAutoSelectCurrentLevelUpPanel()
    {
        return activePanelMode == CardPanelMode.LevelUp
            && autoSelectInAutoOrbit
            && IsAutoOrbitActive()
            && IsLevelUpPanelOpen(); // 보상/선택권과 수동 선택은 제외
    }

    private void StopAutoSelect()
    {
        if (autoSelectRoutine != null)
        {
            StopCoroutine(autoSelectRoutine);
            autoSelectRoutine = null;
        }
    }

    private void TryStartAutoSelectAfterSpawn()
    {
        if (activePanelMode != CardPanelMode.LevelUp)
        {
            return;
        }

        if (!autoSelectInAutoOrbit || !IsAutoOrbitActive())
        {
            return;
        }

        if (pendingAutoSelectSegmentAction)
        {
            pendingAutoSelectSegmentAction = false;
            TryStartAutoSelectSegmentAction(pendingAutoSelectCanAdd, pendingAutoSelectCanLevelUp);
            return;
        }

        TryStartAutoSelect();
    }

    private IEnumerator AutoSelectRoutine()
    {
        yield return WaitForClickableCards(3f);
        yield return new WaitForSecondsRealtime(autoSelectDelay);

        if (!TryPickAutoSelectCard(out SpawnedCardEntry picked))
        {
            autoSelectRoutine = null;
            yield break;
        }

        NotifySpawnedCardPointerEnter(picked);
        yield return new WaitForSecondsRealtime(0.2f);
        if (!CanAutoSelectCurrentLevelUpPanel() || isProcessingSelection)
        {
            autoSelectRoutine = null;
            yield break;
        }

        ResetAllCardHoverForAutoSelect();
        yield return null;

        NotifySpawnedCardClicked(picked);
        autoSelectRoutine = null;
    }

    private IEnumerator AutoSelectSegmentActionRoutine(bool canAdd, bool canLevelUp)
    {
        yield return WaitForClickableCards(3f);
        yield return new WaitForSecondsRealtime(autoSelectDelay);

        if (!TryPickAutoSelectSegmentActionCard(canAdd, canLevelUp, out SpawnedCardEntry picked))
        {
            autoSelectRoutine = null;
            yield break;
        }

        NotifySpawnedCardPointerEnter(picked);
        yield return new WaitForSecondsRealtime(0.2f);
        if (!CanAutoSelectCurrentLevelUpPanel() || isProcessingSelection)
        {
            autoSelectRoutine = null;
            yield break;
        }

        ResetAllCardHoverForAutoSelect();
        yield return null;

        NotifySpawnedCardClicked(picked);
        autoSelectRoutine = null;
    }

    private IEnumerator WaitForClickableCards(float timeoutSeconds)
    {
        float deadline = Time.unscaledTime + timeoutSeconds;
        while (Time.unscaledTime < deadline)
        {
            if (isProcessingSelection)
            {
                yield break;
            }

            if (CountClickableSelectableCards() > 0)
            {
                yield break;
            }

            yield return null;
        }
    }

    private int CountClickableSelectableCards()
    {
        if (spawnedCards == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            SpawnedCardEntry card = spawnedCards[i];
            if (card != null && card.CanSelect && card.IsClickable)
            {
                count++;
            }
        }

        return count;
    }

    private bool TryPickAutoSelectCard(out SpawnedCardEntry picked)
    {
        picked = null;
        if (isProcessingSelection || spawnedCards == null || spawnedCards.Count == 0)
        {
            return false;
        }

        List<SpawnedCardEntry> selectable = new List<SpawnedCardEntry>();
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            SpawnedCardEntry card = spawnedCards[i];
            if (card != null && card.CanSelect && card.IsClickable)
            {
                selectable.Add(card);
            }
        }

        if (selectable.Count == 0)
        {
            return false;
        }

        picked = PickHighestTierCard(selectable); // 최고 등급 우선 선택
        return picked != null;
    }

    private bool TryPickAutoSelectSegmentActionCard(bool canAdd, bool canLevelUp, out SpawnedCardEntry picked)
    {
        picked = null;
        if (isProcessingSelection || spawnedCards == null || spawnedCards.Count == 0)
        {
            return false;
        }

        List<SpawnedCardEntry> selectable = new List<SpawnedCardEntry>();
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            SpawnedCardEntry card = spawnedCards[i];
            if (card == null || !card.IsClickable)
            {
                continue;
            }

            bool isAdd = card.SegmentRole == SegmentCardRole.AddAction;
            bool isLevelUp = card.SegmentRole == SegmentCardRole.LevelUpAction;

            if (isAdd && canAdd)
            {
                selectable.Add(card);
            }
            else if (isLevelUp && canLevelUp)
            {
                selectable.Add(card);
            }
            else if (!isAdd && !isLevelUp && card.CanSelect)
            {
                selectable.Add(card);
            }
        }

        if (selectable.Count == 0)
        {
            return false;
        }

        picked = PickHighestTierCard(selectable); // 최고 등급 우선 선택
        return picked != null;
    }

    private void ResetAllCardHoverForAutoSelect()
    {
        if (spawnedCards == null)
        {
            return;
        }

        for (int i = 0; i < spawnedCards.Count; i++)
        {
            SpawnedCardEntry card = spawnedCards[i];
            if (card == null)
            {
                continue;
            }

            card.IsPointerOver = false;
            card.HasTooltipPointer = false;
            card.IsHoverVisualActive = false;
            HideCardHoverTooltip(card, true);

            if (card.RootTransform != null)
            {
                card.RootTransform.DOKill();
                card.RootTransform.localScale = card.OriginalScale;
            }

            if (card.Root != null)
            {
                cardEffect?.OnCardHoverExit(card.Root);
            }
        }
    }

    private StatUpgrade.StatCardTier GetCardTier(SpawnedCardEntry entry)
    {
        if (entry == null)
        {
            return StatUpgrade.StatCardTier.Normal;
        }

        if (entry.SegmentRole == SegmentCardRole.EnhanceChoice)
        {
            return entry.WeaponEnhancementTier;
        }

        if (entry.RewardChoice != RewardChoiceKind.None)
        {
            return entry.RewardTier;
        }

        if (entry.StatUpgrade != null)
        {
            return entry.StatUpgrade.CurrentTier;
        }

        return StatUpgrade.StatCardTier.Normal;
    }

    private SpawnedCardEntry PickHighestTierCard(List<SpawnedCardEntry> candidates)
    {
        StatUpgrade.StatCardTier best = StatUpgrade.StatCardTier.Normal;
        for (int i = 0; i < candidates.Count; i++)
        {
            StatUpgrade.StatCardTier tier = GetCardTier(candidates[i]);
            if (tier > best)
            {
                best = tier;
            }
        }

        List<SpawnedCardEntry> topTier = new List<SpawnedCardEntry>();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (GetCardTier(candidates[i]) == best)
            {
                topTier.Add(candidates[i]);
            }
        }

        return topTier[UnityEngine.Random.Range(0, topTier.Count)];
    }
}
