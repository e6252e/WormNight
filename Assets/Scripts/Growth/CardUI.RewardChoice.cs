using TeamProject01.Gameplay;
using UnityEngine;
using UnityEngine.UI;

public partial class CardUI
{
    private void SpawnRewardChoiceCards()
    {
        ClearSpawnedCards();
        rerollAllowedForCurrentChoices = false; // 보상 3종 고정 카드는 리롤 없음

        int spawnCount = Mathf.Min(3, cardSlots != null ? cardSlots.Length : 0);
        for (int i = 0; i < spawnCount; i++)
        {
            RewardChoiceKind kind = ResolveRewardChoiceKind(i);
            SpawnedCardEntry entry = CreateRewardChoiceCard(kind, cardSlots[i]);
            if (entry != null)
            {
                spawnedCards.Add(entry);
            }
        }

        PlaySpawnOpenTween(spawnedCards);
    }

    private SpawnedCardEntry CreateRewardChoiceCard(RewardChoiceKind kind, RectTransform slot)
    {
        GameObject template = ResolveRewardChoiceCardTemplate();
        if (template == null || slot == null)
        {
            Debug.LogWarning("[CardUI] 보상 선택 카드 템플릿이 없습니다.", this);
            return null;
        }

        StatUpgrade.StatCardTier tier = RollRewardChoiceTier();
        SpawnedCardEntry entry = CreateSpawnedCard(template, slot, template, skipStatUpgradeRoll: true);
        if (entry == null)
        {
            return null;
        }

        int multiplier = ResolveTierMultiplier(tier);
        int amount = ResolveRewardChoiceAmount(kind, multiplier);
        int ticketCount = ResolveRewardChoiceTicketCount(kind, multiplier);
        entry.StatUpgrade = null; // 공통카드 프리팹 껍데기만 재사용
        entry.StatUpgradeDefinition = null;
        entry.SegmentRole = SegmentCardRole.None;
        entry.RewardChoice = kind;
        entry.RewardTier = tier;
        entry.RewardAmount = amount;
        entry.RewardTicketCount = ticketCount;
        entry.CanSelect = kind != RewardChoiceKind.None;

        ApplyTierCardFrame(entry.Root, tier);
        Sprite icon = ResolveRewardChoiceIcon(kind);
        ApplyCardTextsDirectly(
            entry.Root,
            ResolveRewardChoiceTitle(kind),
            ResolveRewardChoiceDescription(kind, amount, ticketCount),
            icon);
        if (icon == null)
        {
            ClearRewardChoiceCardIcon(entry.Root); // 참조 누락 시 빈 이미지 상태 유지
        }

        return entry;
    }

    private GameObject ResolveRewardChoiceCardTemplate()
    {
        CardUiPrefabReferences references = GetPrefabReferences();
        if (references != null && references.RewardChoiceCardPrefab != null)
        {
            return references.RewardChoiceCardPrefab; // 보상 선택 공통 프리팹
        }

        GameObject template = ResolveStatUpgradeTemplatePrefab(null); // 공통 강화 카드 기본 프리팹
        return template != null ? template : segmentCardBasePrefab;
    }

    private StatUpgrade.StatCardTier RollRewardChoiceTier()
    {
        float rareChance = Mathf.Clamp(rareCardChancePercent + pendingRewardRareChanceBonusPercent, 0.0f, 100.0f);
        float uniqueChance = Mathf.Clamp(uniqueCardChancePercent + pendingRewardUniqueChanceBonusPercent, 0.0f, 100.0f);
        return StatUpgrade.RollTier(rareChance, uniqueChance); // 상자 등급 보너스 반영
    }

    private static RewardChoiceKind ResolveRewardChoiceKind(int index)
    {
        switch (index)
        {
            case 0:
                return RewardChoiceKind.Gold;
            case 1:
                return RewardChoiceKind.Experience;
            case 2:
                return RewardChoiceKind.SegmentChoiceTicket;
            default:
                return RewardChoiceKind.None;
        }
    }

    private int ResolveRewardChoiceAmount(RewardChoiceKind kind, int multiplier)
    {
        switch (kind)
        {
            case RewardChoiceKind.Gold:
                return Mathf.Max(0, rewardGoldBaseAmount) * multiplier;
            case RewardChoiceKind.Experience:
                return Mathf.Max(0, rewardExperienceBaseAmount) * multiplier;
            default:
                return 0;
        }
    }

    private int ResolveRewardChoiceTicketCount(RewardChoiceKind kind, int multiplier)
    {
        return kind == RewardChoiceKind.SegmentChoiceTicket
            ? Mathf.Max(1, rewardSegmentTicketBaseCount) * multiplier
            : 0;
    }

    private static int ResolveTierMultiplier(StatUpgrade.StatCardTier tier)
    {
        return Mathf.Max(1, Mathf.RoundToInt(StatUpgrade.GetTierMultiplier(tier)));
    }

    private static string ResolveRewardChoiceTitle(RewardChoiceKind kind)
    {
        switch (kind)
        {
            case RewardChoiceKind.Gold:
                return "골드";
            case RewardChoiceKind.Experience:
                return "경험치";
            case RewardChoiceKind.SegmentChoiceTicket:
                return "세그먼트선택";
            default:
                return "없음";
        }
    }

    private static string ResolveRewardChoiceDescription(RewardChoiceKind kind, int amount, int ticketCount)
    {
        switch (kind)
        {
            case RewardChoiceKind.Gold:
                return $"골드 +{Mathf.Max(0, amount)}";
            case RewardChoiceKind.Experience:
                return $"경험치 +{Mathf.Max(0, amount)}";
            case RewardChoiceKind.SegmentChoiceTicket:
                return $"세그먼트선택권 x{Mathf.Max(1, ticketCount)}";
            default:
                return "선택 불가";
        }
    }

    private Sprite ResolveRewardChoiceIcon(RewardChoiceKind kind)
    {
        CardUiPrefabReferences references = GetPrefabReferences();
        if (references == null)
        {
            return null;
        }

        switch (kind)
        {
            case RewardChoiceKind.Gold:
                return references.RewardGoldIconSprite;
            case RewardChoiceKind.Experience:
                return references.RewardExperienceIconSprite;
            case RewardChoiceKind.SegmentChoiceTicket:
                return references.RewardSegmentChoiceTicketIconSprite;
            default:
                return null;
        }
    }

    private static void ClearRewardChoiceCardIcon(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Transform imageTransform = root.transform.Find("Image");
        Image image = imageTransform != null ? imageTransform.GetComponent<Image>() : null;
        if (image == null)
        {
            return;
        }

        image.sprite = null;
        image.overrideSprite = null;
        image.enabled = false;
    }

    private void HandleRewardChoiceClicked(SpawnedCardEntry selectedEntry)
    {
        if (selectedEntry == null || selectedEntry.RewardChoice == RewardChoiceKind.None)
        {
            return;
        }

        pendingRewardExperience = 0;
        pendingRewardGold = 0;
        pendingRewardSegmentTicketCount = 0;

        switch (selectedEntry.RewardChoice)
        {
            case RewardChoiceKind.Gold:
                pendingRewardGold = Mathf.Max(0, selectedEntry.RewardAmount);
                break;
            case RewardChoiceKind.Experience:
                pendingRewardExperience = Mathf.Max(0, selectedEntry.RewardAmount);
                break;
            case RewardChoiceKind.SegmentChoiceTicket:
                pendingRewardSegmentTicketCount = Mathf.Max(1, selectedEntry.RewardTicketCount);
                break;
        }

        isProcessingSelection = true;
        PlaySelectionCloseSequence(selectedEntry);
    }

    private void ApplyPendingRewardChoiceAfterClosed()
    {
        int experience = pendingRewardExperience;
        int gold = pendingRewardGold;
        int ticketCount = pendingRewardSegmentTicketCount;

        pendingRewardExperience = 0;
        pendingRewardGold = 0;
        pendingRewardSegmentTicketCount = 0;

        if (ticketCount > 0)
        {
            activePanelMode = CardPanelMode.SegmentTicketChoice;
            segmentTicketChoicesRemaining = Mathf.Max(1, ticketCount);
            if (!OpenActiveCardPanel())
            {
                ResetSpecialCardMode();
            }

            return;
        }

        ResetSpecialCardMode(); // 경험치 지급 전 일반 모드 복귀
        if (experience > 0 || gold > 0)
        {
            RewardData reward = RewardData.Create(experience, gold, 0, ResolveRewardChoiceWorldPosition());
            RewardGateway.SubmitReward(reward);
        }
    }

    private void HandleSegmentTicketChoiceCompletedAfterClose()
    {
        segmentTicketChoicesRemaining = Mathf.Max(0, segmentTicketChoicesRemaining - 1);
        if (segmentTicketChoicesRemaining > 0)
        {
            activePanelMode = CardPanelMode.SegmentTicketChoice;
            if (!OpenActiveCardPanel())
            {
                ResetSpecialCardMode();
            }

            return;
        }

        ResetSpecialCardMode();
    }

    private bool OpenActiveCardPanel()
    {
        LevelUpUi ui = ResolveLevelUpUi();
        if (ui == null)
        {
            Debug.LogWarning("[CardUI] LevelUpUi가 없어 다음 카드 패널을 열 수 없습니다.", this);
            return false;
        }

        spawnedForCurrentOpen = false;
        isProcessingSelection = false;
        currentSpawnPhase = LevelUpCardPhase.Upgrade;
        ui.SetUseRewardTitle(activePanelMode != CardPanelMode.LevelUp);
        ui.SetUseBackgroundBlur(activePanelMode != CardPanelMode.LevelUp);
        ui.Open();
        return true;
    }

    private void ResetSpecialCardMode()
    {
        activePanelMode = CardPanelMode.LevelUp;
        segmentTicketChoicesRemaining = 0;
        pendingRewardExperience = 0;
        pendingRewardGold = 0;
        pendingRewardSegmentTicketCount = 0;
        ClearRewardChoiceTierChanceBonus();
        LevelUpUi ui = ResolveLevelUpUi();
        if (ui != null)
        {
            ui.SetUseRewardTitle(false);
            ui.SetUseBackgroundBlur(false);
        }
    }

    private void ClearRewardChoiceTierChanceBonus()
    {
        pendingRewardRareChanceBonusPercent = 0.0f;
        pendingRewardUniqueChanceBonusPercent = 0.0f;
    }

    private void ApplySegmentChoiceTicketOverrides(SpawnedCardEntry entry)
    {
        if (entry == null || activePanelMode != CardPanelMode.SegmentTicketChoice)
        {
            return;
        }

        entry.LevelDelta = 0; // 선택권은 세그먼트 선택만 제공하고 경험치/레벨은 소비하지 않음
    }

    private static Vector3 ResolveRewardChoiceWorldPosition()
    {
        if (MonsterInteractionApi.TryGetConvoyTarget(out Transform convoyTarget) && convoyTarget != null)
        {
            return convoyTarget.position;
        }

        return Vector3.zero;
    }
}
