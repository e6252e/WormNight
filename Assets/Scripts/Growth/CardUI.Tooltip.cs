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
    private void NotifySpawnedCardPointerEnter(SpawnedCardEntry entry, PointerEventData eventData = null)
    {
        if (entry == null || !entry.IsClickable)
        {
            return;
        }

        entry.IsPointerOver = true;
        StoreCardTooltipPointer(entry, eventData);
        SetCardHoverVisual(entry, true);
        TryShowCardHoverTooltip(entry); // 카드 역할별 공통 호버 툴팁
    }

    private void NotifySpawnedCardPointerMove(SpawnedCardEntry entry, PointerEventData eventData)
    {
        if (entry == null || !entry.IsClickable)
        {
            return;
        }

        StoreCardTooltipPointer(entry, eventData);
        if (!UpdateCardHoverTooltipPosition(entry))
        {
            TryShowCardHoverTooltip(entry);
        }
    }

    private void NotifySpawnedCardPointerExit(SpawnedCardEntry entry, PointerEventData eventData = null)
    {
        if (entry == null || !entry.IsClickable)
        {
            return;
        }

        if (IsPointerStillInsideSpawnedCard(entry, eventData))
        {
            return; // 자식 Graphic 사이 이동 중이면 호버 유지
        }

        entry.IsPointerOver = false;
        entry.HasTooltipPointer = false;
        SetCardHoverVisual(entry, false); // 호버 비주얼 복원
        HideCardHoverTooltip(entry, false); // 카드 호버 툴팁 닫기
    }

    private static bool IsPointerStillInsideSpawnedCard(SpawnedCardEntry entry, PointerEventData eventData)
    {
        if (entry == null || entry.RootTransform == null || eventData == null)
        {
            return false;
        }

        Camera eventCamera = eventData.enterEventCamera != null ? eventData.enterEventCamera : eventData.pressEventCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(entry.RootTransform, eventData.position, eventCamera);
    }

    private void SetCardHoverVisual(SpawnedCardEntry entry, bool active)
    {
        if (entry == null || entry.RootTransform == null)
        {
            return;
        }

        if (active == entry.IsHoverVisualActive)
        {
            return;
        }

        entry.IsHoverVisualActive = active;
        BringCardTooltipToFront(entry);
        entry.RootTransform.DOKill();
        entry.RootTransform
            .DOScale(active ? entry.OriginalScale * hoverScale : entry.OriginalScale, 0.15f)
            .SetEase(active ? Ease.OutQuad : Ease.InQuad)
            .SetUpdate(true);

        if (active)
        {
            cardEffect?.OnCardHoverEnter(entry.Root, hoverScale);
        }
        else
        {
            cardEffect?.OnCardHoverExit(entry.Root);
        }
    }

    private static void StoreCardTooltipPointer(SpawnedCardEntry entry, PointerEventData eventData)
    {
        if (entry == null || eventData == null)
        {
            return;
        }

        entry.HasTooltipPointer = true;
        entry.LastTooltipScreenPosition = eventData.position;
        entry.LastTooltipCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
    }

    private void TryShowCardHoverTooltip(SpawnedCardEntry entry)
    {
        if (entry == null || !entry.TooltipReady || !entry.IsPointerOver || !entry.HasTooltipPointer)
        {
            HideCardHoverTooltip(entry, true);
            return;
        }

        ShowCardHoverTooltip(entry); // 위치/크기 계산 완료 후 표시
    }

    private static void CacheCardTooltip(SpawnedCardEntry entry)
    {
        if (entry == null || entry.Root == null)
        {
            return;
        }

        Transform tooltipTransform = FindDescendantByName(entry.Root.transform, CardTooltipRootName);
        if (tooltipTransform == null)
        {
            tooltipTransform = FindDescendantByName(entry.Root.transform, LegacyCardTooltipRootName); // 기존 프리팹 이름 호환
        }

        if (tooltipTransform == null)
        {
            return; // 프리팹에 오브젝트가 없으면 런타임 생성하지 않음
        }

        entry.CardTooltipRoot = tooltipTransform.gameObject;
        entry.CardTooltipCanvasGroup = ResolveCardTooltipCanvasGroup(tooltipTransform);

        Transform textTransform = FindDescendantByName(tooltipTransform, CardTooltipTextName);
        if (textTransform == null)
        {
            textTransform = FindDescendantByName(tooltipTransform, LegacyCardTooltipTextName); // 기존 프리팹 이름 호환
        }

        if (textTransform != null)
        {
            entry.CardTooltipText = textTransform.GetComponent<TMP_Text>();
        }

        if (entry.CardTooltipText == null)
        {
            entry.CardTooltipText = tooltipTransform.GetComponentInChildren<TMP_Text>(true);
        }

        PrepareCardTooltipHidden(entry.CardTooltipRoot, entry.CardTooltipCanvasGroup, entry.CardTooltipText);
    }

    private static CanvasGroup ResolveCardTooltipCanvasGroup(Transform tooltipTransform)
    {
        if (tooltipTransform == null)
        {
            return null;
        }

        CanvasGroup canvasGroup = tooltipTransform.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = tooltipTransform.GetComponentInChildren<CanvasGroup>(true);
        }

        if (canvasGroup == null)
        {
            canvasGroup = tooltipTransform.gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        return canvasGroup;
    }

    private static Transform FindDescendantByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == objectName)
            {
                return children[i];
            }
        }

        return null;
    }

    private void ShowCardHoverTooltip(SpawnedCardEntry entry)
    {
        if (entry == null
            || entry.CardTooltipRoot == null
            || entry.CardTooltipText == null
            || !entry.CanSelect
            || !entry.TooltipReady
            || !entry.HasTooltipPointer
            || !TryBuildCardHoverTooltipText(entry, out string tooltipText))
        {
            HideCardHoverTooltip(entry, true);
            return;
        }

        if (entry.CardTooltipCanvasGroup == null)
        {
            entry.CardTooltipCanvasGroup = ResolveCardTooltipCanvasGroup(entry.CardTooltipRoot.transform);
        }

        PrepareCardTooltipHidden(entry.CardTooltipRoot, entry.CardTooltipCanvasGroup, entry.CardTooltipText);
        entry.CardTooltipText.text = tooltipText;
        ApplyCardTooltipLayout(entry, tooltipText);
        Canvas.ForceUpdateCanvases();
        BringCardTooltipToFront(entry);
        if (!UpdateCardHoverTooltipPosition(entry))
        {
            HideCardHoverTooltip(entry, true);
            return;
        }

        entry.CardTooltipRoot.SetActive(true);
        Canvas.ForceUpdateCanvases();
        UpdateCardHoverTooltipPosition(entry); // 활성화 후 최종 위치 보정

        entry.CardTooltipCanvasGroup.DOKill();
        entry.CardTooltipCanvasGroup.alpha = 0f;
        entry.CardTooltipCanvasGroup.DOFade(1f, CardTooltipFadeSeconds).SetUpdate(true);
    }

    private static void ApplyCardTooltipLayout(SpawnedCardEntry entry, string tooltipText)
    {
        if (entry == null || entry.CardTooltipRoot == null)
        {
            return;
        }

        if (entry.CardTooltipText == null)
        {
            return;
        }

        entry.CardTooltipText.enableAutoSizing = true;
        entry.CardTooltipText.fontSizeMax = CardTooltipFontSizeMax;
        entry.CardTooltipText.fontSizeMin = CardTooltipFontSizeMin;
        entry.CardTooltipText.richText = true;
        entry.CardTooltipText.textWrappingMode = TextWrappingModes.NoWrap;
        entry.CardTooltipText.alignment = TextAlignmentOptions.Center;

        RectTransform tooltipRect = entry.CardTooltipRoot.transform as RectTransform;
        if (tooltipRect == null)
        {
            return;
        }

        Vector2 preferred = entry.CardTooltipText.GetPreferredValues(tooltipText, Mathf.Infinity, Mathf.Infinity);
        float width = Mathf.Clamp(preferred.x + CardTooltipHorizontalPadding, CardTooltipMinWidth, CardTooltipMaxWidth);
        int lineCount = CountDescriptionLines(tooltipText);
        float height = Mathf.Max(CardTooltipMinHeight, lineCount * CardTooltipLineHeight + CardTooltipVerticalPadding);
        tooltipRect.sizeDelta = new Vector2(width, height);
    }

    private static bool UpdateCardHoverTooltipPosition(SpawnedCardEntry entry)
    {
        if (entry == null || entry.CardTooltipRoot == null || entry.RootTransform == null || !entry.HasTooltipPointer)
        {
            return false;
        }

        RectTransform tooltipRect = entry.CardTooltipRoot.transform as RectTransform;
        if (tooltipRect == null)
        {
            return false;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(entry.RootTransform, entry.LastTooltipScreenPosition, entry.LastTooltipCamera, out Vector2 localPoint))
        {
            tooltipRect.anchoredPosition = localPoint + CardTooltipCursorOffset;
            tooltipRect.SetAsLastSibling();
            tooltipRect.localScale = GetInverseTooltipScale(entry.RootTransform.localScale);
            return true;
        }

        return false;
    }

    private static void BringCardTooltipToFront(SpawnedCardEntry entry)
    {
        if (entry == null || entry.RootTransform == null)
        {
            return;
        }

        Transform slotTransform = entry.RootTransform.parent;
        if (slotTransform != null)
        {
            slotTransform.SetAsLastSibling(); // 다른 카드 슬롯보다 위에 렌더
        }

        entry.RootTransform.SetAsLastSibling(); // 슬롯 안에서도 카드가 최상단
        if (entry.CardTooltipRoot != null)
        {
            entry.CardTooltipRoot.transform.SetAsLastSibling(); // 카드 내부에서 툴팁 최상단
        }
    }

    private static Vector3 GetInverseTooltipScale(Vector3 rootScale)
    {
        return new Vector3(
            ResolveInverseTooltipAxisScale(rootScale.x),
            ResolveInverseTooltipAxisScale(rootScale.y),
            1f);
    }

    private static float ResolveInverseTooltipAxisScale(float scale)
    {
        float magnitude = Mathf.Abs(scale);
        if (magnitude < 0.5f)
        {
            return 1f; // 카드 등장 중 scale 0 근처에서 툴팁이 커지는 것 방지
        }

        return Mathf.Clamp(1f / magnitude, 0.5f, 2f);
    }

    private static void HideCardHoverTooltip(SpawnedCardEntry entry, bool instant)
    {
        if (entry == null || entry.CardTooltipRoot == null)
        {
            return;
        }

        if (entry.CardTooltipCanvasGroup == null || instant)
        {
            if (entry.CardTooltipCanvasGroup != null)
            {
                entry.CardTooltipCanvasGroup.DOKill();
                entry.CardTooltipCanvasGroup.alpha = 0f;
                entry.CardTooltipCanvasGroup.blocksRaycasts = false;
                entry.CardTooltipCanvasGroup.interactable = false;
            }

            PrepareCardTooltipHidden(entry.CardTooltipRoot, entry.CardTooltipCanvasGroup, entry.CardTooltipText);
            return;
        }

        entry.CardTooltipCanvasGroup.DOKill();
        entry.CardTooltipCanvasGroup.blocksRaycasts = false;
        entry.CardTooltipCanvasGroup.interactable = false;
        entry.CardTooltipCanvasGroup.DOFade(0f, CardTooltipFadeSeconds)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (entry.CardTooltipRoot != null)
                {
                    PrepareCardTooltipHidden(entry.CardTooltipRoot, entry.CardTooltipCanvasGroup, entry.CardTooltipText);
                }
            });
    }

    private void ForceHideAllCardTooltips()
    {
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            ResetCardTooltipInteraction(spawnedCards[i]);
            HideCardHoverTooltip(spawnedCards[i], true);
        }
    }

    private static void ResetCardTooltipInteraction(SpawnedCardEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        entry.TooltipReady = false;
        entry.IsPointerOver = false;
        entry.HasTooltipPointer = false;
        entry.IsHoverVisualActive = false;
        entry.LastTooltipCamera = null;
        entry.LastTooltipScreenPosition = Vector2.zero;
    }

    private static void PrepareCardTooltipHidden(GameObject cardRoot)
    {
        if (cardRoot == null)
        {
            return;
        }

        Transform tooltipTransform = FindDescendantByName(cardRoot.transform, CardTooltipRootName);
        if (tooltipTransform == null)
        {
            tooltipTransform = FindDescendantByName(cardRoot.transform, LegacyCardTooltipRootName);
        }

        if (tooltipTransform == null)
        {
            return;
        }

        PrepareCardTooltipHidden(
            tooltipTransform.gameObject,
            ResolveCardTooltipCanvasGroup(tooltipTransform),
            tooltipTransform.GetComponentInChildren<TMP_Text>(true));
    }

    private static void PrepareCardTooltipHidden(GameObject tooltipRoot, CanvasGroup canvasGroup, TMP_Text tooltipText)
    {
        if (tooltipRoot == null)
        {
            return;
        }

        if (canvasGroup != null)
        {
            canvasGroup.DOKill();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (tooltipText != null)
        {
            tooltipText.text = string.Empty;
        }

        MoveCardTooltipOffscreen(tooltipRoot);
        tooltipRoot.SetActive(false);
    }

    private static void MoveCardTooltipOffscreen(GameObject tooltipRoot)
    {
        RectTransform tooltipRect = tooltipRoot != null ? tooltipRoot.transform as RectTransform : null;
        if (tooltipRect != null)
        {
            tooltipRect.anchoredPosition = CardTooltipHiddenAnchoredPosition;
            tooltipRect.sizeDelta = CardTooltipHiddenSize;
            tooltipRect.localScale = Vector3.one;
        }
    }

    private bool TryBuildCardHoverTooltipText(SpawnedCardEntry entry, out string text)
    {
        text = string.Empty;
        if (entry == null)
        {
            return false;
        }

        switch (entry.SegmentRole)
        {
            case SegmentCardRole.Candidate:
                return TryBuildSegmentChoiceTooltipText(entry, out text); // 세그먼트 선택 카드
            case SegmentCardRole.AddAction:
                return TryBuildSegmentChoiceTooltipText(entry, out text); // 세그먼트 추가 카드도 보유 목록 표시
            case SegmentCardRole.LevelUpAction:
                return TryBuildSegmentLevelUpTooltipText(entry, out text); // 세그먼트 모델 레벨업 변화
            case SegmentCardRole.EnhanceChoice:
                return TryBuildSegmentEnhancementTooltipText(entry, out text); // 세그먼트 강화 카드
            default:
                return entry.StatUpgrade != null && TryBuildStatUpgradeTooltipText(entry.StatUpgrade, out text); // 공통 강화 카드
        }
    }

    private bool TryBuildStatUpgradeTooltipText(StatUpgrade statUpgrade, out string text)
    {
        text = string.Empty;
        if (statUpgrade == null)
        {
            return false;
        }

        float tierMultiplier = StatUpgrade.GetTierMultiplier(statUpgrade.CurrentTier);
        float damageBonus = statUpgrade.DamageMultiplierBonus * tierMultiplier;
        if (Mathf.Abs(damageBonus) > 0.0001f)
        {
            return TryBuildCommonDamageTooltipText(
                CommonStatSegmentFilter.All,
                damageBonus,
                0f,
                0f,
                "모든 세그먼트의 공격력을 증가시킵니다.",
                out text);
        }

        float meleeDamageBonus = statUpgrade.MeleeDamageMultiplierBonus * tierMultiplier;
        if (Mathf.Abs(meleeDamageBonus) > 0.0001f)
        {
            return TryBuildCommonDamageTooltipText(
                CommonStatSegmentFilter.Melee,
                0f,
                meleeDamageBonus,
                0f,
                "모든 밀리 세그먼트의 공격력을 증가시킵니다.",
                out text);
        }

        float magicDamageBonus = statUpgrade.MagicDamageMultiplierBonus * tierMultiplier;
        if (Mathf.Abs(magicDamageBonus) > 0.0001f)
        {
            return TryBuildCommonDamageTooltipText(
                CommonStatSegmentFilter.Magic,
                0f,
                0f,
                magicDamageBonus,
                "모든 마법 세그먼트의 공격력을 증가시킵니다.",
                out text);
        }

        float cooldownBonus = statUpgrade.AttackSpeedMultiplierBonus * tierMultiplier;
        if (Mathf.Abs(cooldownBonus) > 0.0001f)
        {
            return TryBuildCommonCooldownTooltipText(cooldownBonus, out text);
        }

        float nexusHealthBonus = statUpgrade.NexusHealthBonus * tierMultiplier;
        if (Mathf.Abs(nexusHealthBonus) > 0.0001f)
        {
            return TryBuildSingleStatTooltipText(
                "넥서스의 최대 체력을 증가시킵니다.",
                "넥서스 HP",
                ResolveCurrentNexusMaxHealth(),
                ResolveCurrentNexusMaxHealth() + Mathf.RoundToInt(nexusHealthBonus),
                string.Empty,
                out text);
        }

        float rejoinBonus = statUpgrade.RejoinRangeBonus * tierMultiplier;
        if (Mathf.Abs(rejoinBonus) > 0.0001f)
        {
            float before = ResolveCurrentRejoinRange();
            return TryBuildSingleStatTooltipText(
                "꼬리와 연결이 끊겼을 때 재결합할 수 있는 영역이 증가합니다.",
                "재결합 범위",
                before,
                before + rejoinBonus,
                "m",
                out text);
        }

        float collisionBonus = statUpgrade.CollisionForceBonus * tierMultiplier;
        if (Mathf.Abs(collisionBonus) > 0.0001f)
        {
            float before = CoreStatProvider.Active != null ? CoreStatProvider.Active.CollisionForceBonus : 0f;
            return TryBuildSingleStatPercentTooltipText(
                "플레이어의 충돌 힘을 강화합니다.",
                "충돌힘",
                before,
                before + collisionBonus,
                out text);
        }

        float handlingBonus = statUpgrade.TurnSpeedBonus * tierMultiplier;
        if (Mathf.Abs(handlingBonus) > 0.0001f)
        {
            float before = ResolveCurrentHandling();
            return TryBuildSingleStatTooltipText(
                "플레이어의 좌우 핸들링을 강화합니다.",
                "핸들링",
                before,
                before + handlingBonus,
                string.Empty,
                out text);
        }

        return false;
    }

    private bool TryBuildCommonDamageTooltipText(
        CommonStatSegmentFilter filter,
        float globalDamageBonus,
        float meleeDamageBonus,
        float magicDamageBonus,
        string summary,
        out string text)
    {
        CoreStatProvider core = CoreStatProvider.Active;
        StringBuilder builder = new StringBuilder(256);
        AppendTooltipSummaryLine(builder, summary);
        int appended = 0;
        AppendCommonSegmentStatLines(core, filter, builder, (segmentId, profile) =>
        {
            float before = CalculateCommonTooltipSegmentDamage(core, segmentId, profile, 0f, 0f, 0f);
            float after = CalculateCommonTooltipSegmentDamage(core, segmentId, profile, globalDamageBonus, meleeDamageBonus, magicDamageBonus);
            if (!HasTooltipValueChanged(before, after))
            {
                return false;
            }

            string displayName = ResolveSegmentTooltipDisplayName(core, segmentId, default);
            AppendTooltipLine(builder, $"{displayName} 공격력", FormatTooltipFloat(before, string.Empty), FormatTooltipFloat(after, string.Empty));
            appended++;
            return true;
        });

        if (appended == 0)
        {
            AppendTooltipSummaryLine(builder, "해당 세그먼트 없음");
        }

        text = builder.ToString().TrimEnd();
        return !string.IsNullOrWhiteSpace(text);
    }

    private bool TryBuildCommonCooldownTooltipText(float cooldownBonus, out string text)
    {
        CoreStatProvider core = CoreStatProvider.Active;
        StringBuilder builder = new StringBuilder(256);
        AppendTooltipSummaryLine(builder, "모든 세그먼트의 쿨타임을 감소시킵니다.");
        int appended = 0;
        AppendCommonSegmentStatLines(core, CommonStatSegmentFilter.All, builder, (segmentId, profile) =>
        {
            float before = CalculateCommonTooltipSegmentCooldown(core, segmentId, profile, 0f);
            float after = CalculateCommonTooltipSegmentCooldown(core, segmentId, profile, cooldownBonus);
            if (!HasTooltipValueChanged(before, after))
            {
                return false;
            }

            string displayName = ResolveSegmentTooltipDisplayName(core, segmentId, default);
            AppendTooltipLine(builder, $"{displayName} 쿨타임", FormatTooltipFloat(before, "s"), FormatTooltipFloat(after, "s"));
            appended++;
            return true;
        });

        if (appended == 0)
        {
            AppendTooltipSummaryLine(builder, "해당 세그먼트 없음");
        }

        text = builder.ToString().TrimEnd();
        return !string.IsNullOrWhiteSpace(text);
    }

    private delegate bool AppendCommonSegmentStatLine(string segmentId, SegmentAttackProfile profile);

    private void AppendCommonSegmentStatLines(
        CoreStatProvider core,
        CommonStatSegmentFilter filter,
        StringBuilder builder,
        AppendCommonSegmentStatLine appendLine)
    {
        if (core == null || appendLine == null)
        {
            return;
        }

        List<string> segmentIds = CollectCommonTooltipSegmentIds(core, filter);
        for (int i = 0; i < segmentIds.Count; i++)
        {
            string segmentId = segmentIds[i];
            if (!TryGetSegmentAttackProfile(core, segmentId, out SegmentAttackProfile profile) || profile == null)
            {
                continue;
            }

            appendLine(segmentId, profile);
        }
    }

    private List<string> CollectCommonTooltipSegmentIds(CoreStatProvider core, CommonStatSegmentFilter filter)
    {
        List<string> results = new List<string>(8);
        ConvoyController convoy = ResolveTooltipConvoy(core);
        if (convoy == null)
        {
            return results;
        }

        cardTooltipSegmentCountsById.Clear();
        convoy.CollectAttachedSegmentCounts(cardTooltipSegmentCountsById);
        foreach (string segmentId in cardTooltipSegmentCountsById.Keys)
        {
            if (string.IsNullOrWhiteSpace(segmentId) || !MatchesCommonStatSegmentFilter(core, segmentId, filter))
            {
                continue;
            }

            results.Add(segmentId.Trim());
        }

        results.Sort(System.StringComparer.OrdinalIgnoreCase);
        return results;
    }

    private static bool MatchesCommonStatSegmentFilter(CoreStatProvider core, string segmentId, CommonStatSegmentFilter filter)
    {
        if (core == null || string.IsNullOrWhiteSpace(segmentId))
        {
            return false;
        }

        switch (filter)
        {
            case CommonStatSegmentFilter.Melee:
                return core.IsMeleeWeaponSegment(segmentId);
            case CommonStatSegmentFilter.Magic:
                return core.IsMagicWeaponSegment(segmentId);
            default:
                return true;
        }
    }

    private static float CalculateCommonTooltipSegmentDamage(
        CoreStatProvider core,
        string segmentId,
        SegmentAttackProfile profile,
        float globalDamageBonus,
        float meleeDamageBonus,
        float magicDamageBonus)
    {
        if (core == null || profile == null)
        {
            return 0f;
        }

        WeaponStatBonusData weaponBonus = core.GetWeaponStatBonus(segmentId);
        return CalculateTooltipSegmentDamage(core, segmentId, profile, weaponBonus, globalDamageBonus, meleeDamageBonus, magicDamageBonus);
    }

    private static float CalculateTooltipSegmentDamage(
        CoreStatProvider core,
        string segmentId,
        SegmentAttackProfile profile,
        WeaponStatBonusData weaponBonus,
        float globalDamageBonus = 0f,
        float meleeDamageBonus = 0f,
        float magicDamageBonus = 0f)
    {
        if (core == null || profile == null)
        {
            return 0f;
        }

        SegmentUpgradeData segmentUpgrade = core.GetSegmentUpgrade(segmentId);
        float baseDamage = weaponBonus.ResolveBaseDamage(profile.BaseDamage);
        float commonDamageBonus = CalculateCommonBaseDamageBonus(core, segmentId, profile.BaseDamage, globalDamageBonus, meleeDamageBonus, magicDamageBonus);
        float damage = Mathf.Max(0f, baseDamage) + commonDamageBonus + core.FlatDamageBonus;
        damage = segmentUpgrade.ApplyDamage(damage);
        return damage;
    }

    private static float CalculateCommonBaseDamageBonus(
        CoreStatProvider core,
        string segmentId,
        float profileBaseDamage,
        float globalDamageBonus,
        float meleeDamageBonus,
        float magicDamageBonus)
    {
        if (core == null)
        {
            return 0f;
        }

        float bonusRate = Mathf.Max(0f, core.DamageMultiplier - 1f + globalDamageBonus); // 모든 무기 공격력
        if (core.IsMeleeWeaponSegment(segmentId))
        {
            bonusRate += Mathf.Max(0f, core.MeleeDamageMultiplierBonus + meleeDamageBonus); // 밀리 기초 보너스
        }
        else if (core.IsMagicWeaponSegment(segmentId))
        {
            bonusRate += Mathf.Max(0f, core.MagicDamageMultiplierBonus + magicDamageBonus); // 마법 기초 보너스
        }

        return Mathf.Max(0f, profileBaseDamage) * Mathf.Max(0f, bonusRate);
    }

    private static float CalculateCommonTooltipSegmentCooldown(CoreStatProvider core, string segmentId, SegmentAttackProfile profile, float cooldownBonus)
    {
        if (core == null || profile == null)
        {
            return 0f;
        }

        WeaponStatBonusData weaponBonus = core.GetWeaponStatBonus(segmentId);
        return CalculateTooltipSegmentCooldown(core, segmentId, profile, weaponBonus, cooldownBonus);
    }

    private static float CalculateTooltipSegmentCooldown(
        CoreStatProvider core,
        string segmentId,
        SegmentAttackProfile profile,
        WeaponStatBonusData weaponBonus,
        float cooldownBonus = 0f)
    {
        if (core == null || profile == null)
        {
            return 0f;
        }

        SegmentUpgradeData segmentUpgrade = core.GetSegmentUpgrade(segmentId);
        float cooldown = weaponBonus.ResolveCooldown(profile.Cooldown);
        float attackSpeedMultiplier = Mathf.Max(0.01f, core.AttackSpeedMultiplier + cooldownBonus);
        float coreInterval = Mathf.Max(0.05f, cooldown / attackSpeedMultiplier);
        return segmentUpgrade.ApplyFireInterval(coreInterval);
    }

    private static float CalculateTooltipSegmentSearchRange(
        CoreStatProvider core,
        string segmentId,
        SegmentAttackProfile profile,
        WeaponStatBonusData weaponBonus)
    {
        if (core == null || profile == null)
        {
            return 0f;
        }

        SegmentUpgradeData segmentUpgrade = core.GetSegmentUpgrade(segmentId);
        return segmentUpgrade.ApplyRange(weaponBonus.ResolveSearchRange(profile.SearchRange));
    }

    private static float CalculateTooltipSegmentExplosionRadius(
        CoreStatProvider core,
        string segmentId,
        SegmentAttackProfile profile,
        WeaponStatBonusData weaponBonus)
    {
        if (core == null || profile == null)
        {
            return 0f;
        }

        return weaponBonus.ResolveExplosionRadius(profile.ExplosionRadius);
    }

    private static bool TryBuildSingleStatTooltipText(string summary, string label, float before, float after, string suffix, out string text)
    {
        StringBuilder builder = new StringBuilder(96);
        AppendTooltipSummaryLine(builder, summary);
        AppendTooltipLine(builder, label, FormatTooltipFloat(before, suffix), FormatTooltipFloat(after, suffix));
        text = builder.ToString().TrimEnd();
        return !string.IsNullOrWhiteSpace(text);
    }

    private static bool TryBuildSingleStatPercentTooltipText(string summary, string label, float before, float after, out string text)
    {
        StringBuilder builder = new StringBuilder(96);
        AppendTooltipSummaryLine(builder, summary);
        AppendTooltipLine(builder, label, FormatTooltipPercent(before), FormatTooltipPercent(after));
        text = builder.ToString().TrimEnd();
        return !string.IsNullOrWhiteSpace(text);
    }

    private static void AppendTooltipSummaryLine(StringBuilder builder, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(text.Trim());
    }

    private static int ResolveCurrentNexusMaxHealth()
    {
        return NexusController.Active != null ? Mathf.Max(1, NexusController.Active.MaxHealth) : 0;
    }

    private static float ResolveCurrentRejoinRange()
    {
        CoreStatProvider core = CoreStatProvider.Active;
        ConvoyController convoy = ResolveTooltipConvoy(core);
        float baseRange = convoy != null ? convoy.RejoinAreaRadius : 0f;
        float bonus = core != null ? core.RejoinRangeBonus : 0f;
        return Mathf.Max(0.1f, baseRange + bonus);
    }

    private static float ResolveCurrentHandling()
    {
        CoreStatProvider core = CoreStatProvider.Active;
        ConvoyController convoy = ResolveTooltipConvoy(core);
        float baseHandling = convoy != null ? convoy.TurnSpeed : 0f;
        float bonus = core != null ? core.TurnSpeedBonus : 0f;
        return Mathf.Max(1f, baseHandling + bonus);
    }

    private bool TryBuildSegmentChoiceTooltipText(SpawnedCardEntry entry, out string text)
    {
        text = string.Empty;
        string hoveredSegmentId = !string.IsNullOrWhiteSpace(entry.SegmentId)
            ? entry.SegmentId.Trim()
            : entry.SegmentCatalogEntry.NormalizedId;
        CoreStatProvider core = CoreStatProvider.Active;
        ConvoyController convoy = ResolveTooltipConvoy(core);
        if (convoy == null)
        {
            return false; // 현재 보유 목록 조회 불가
        }

        cardTooltipSegmentCountsById.Clear();
        convoy.CollectAttachedSegmentCounts(cardTooltipSegmentCountsById); // 실제 장착 세그먼트 기준
        bool ownsHoveredSegment = !string.IsNullOrWhiteSpace(hoveredSegmentId)
            && cardTooltipSegmentCountsById.TryGetValue(hoveredSegmentId, out int hoveredCount)
            && hoveredCount > 0;

        List<string> sortedSegmentIds = new List<string>(cardTooltipSegmentCountsById.Keys);
        sortedSegmentIds.Sort(System.StringComparer.OrdinalIgnoreCase);

        StringBuilder builder = new StringBuilder(192);
        for (int i = 0; i < sortedSegmentIds.Count; i++)
        {
            string segmentId = sortedSegmentIds[i];
            string displayName = ResolveSegmentTooltipDisplayName(core, segmentId, entry.SegmentCatalogEntry);
            int count = cardTooltipSegmentCountsById[segmentId];
            bool highlight = string.Equals(segmentId, hoveredSegmentId, System.StringComparison.OrdinalIgnoreCase);
            AppendSegmentChoiceOwnedLine(builder, displayName, count, highlight);
        }

        if (!ownsHoveredSegment && !string.IsNullOrWhiteSpace(hoveredSegmentId))
        {
            string displayName = ResolveSegmentTooltipDisplayName(core, hoveredSegmentId, entry.SegmentCatalogEntry);
            AppendSegmentChoiceNewLine(builder, displayName);
        }

        text = builder.ToString().TrimEnd();
        return !string.IsNullOrWhiteSpace(text);
    }

    private static ConvoyController ResolveTooltipConvoy(CoreStatProvider core)
    {
        if (core != null && core.Convoy != null)
        {
            return core.Convoy; // 코어 연결 우선
        }

        return UnityEngine.Object.FindFirstObjectByType<ConvoyController>(); // 런타임 fallback
    }

    private static string ResolveSegmentTooltipDisplayName(CoreStatProvider core, string segmentId, SegmentCatalogEntry fallbackEntry)
    {
        string normalizedId = string.IsNullOrWhiteSpace(segmentId) ? string.Empty : segmentId.Trim();
        if (fallbackEntry.HasId
            && string.Equals(fallbackEntry.NormalizedId, normalizedId, System.StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(fallbackEntry.DisplayName))
        {
            return fallbackEntry.DisplayName.Trim(); // 현재 카드 데이터 우선
        }

        if (core != null && core.TryFindSegmentEntry(normalizedId, out SegmentCatalogEntry catalogEntry) && !string.IsNullOrWhiteSpace(catalogEntry.DisplayName))
        {
            return catalogEntry.DisplayName.Trim(); // 카탈로그 표시명
        }

        return normalizedId; // 최후 fallback
    }

    private static void AppendSegmentChoiceOwnedLine(StringBuilder builder, string displayName, int count, bool highlight)
    {
        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        if (highlight)
        {
            builder.Append("<color=").Append(CardTooltipValueColor).Append("><b>");
        }

        builder.Append(displayName).Append(" X ").Append(count.ToString(CultureInfo.InvariantCulture));

        if (highlight)
        {
            builder.Append("</b></color>");
        }
    }

    private static void AppendSegmentChoiceNewLine(StringBuilder builder, string displayName)
    {
        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append("<color=")
            .Append(CardTooltipNewColor)
            .Append("><b>(NEW) ")
            .Append(displayName)
            .Append("</b></color>");
    }

    private bool TryBuildSegmentLevelUpTooltipText(SpawnedCardEntry entry, out string text)
    {
        text = string.Empty;
        string segmentId = !string.IsNullOrWhiteSpace(entry?.SegmentId)
            ? entry.SegmentId.Trim()
            : entry?.SegmentCatalogEntry.NormalizedId;
        CoreStatProvider core = CoreStatProvider.Active;
        if (core == null
            || string.IsNullOrWhiteSpace(segmentId)
            || !TryGetSegmentDefinition(core, segmentId, out SegmentDefinition definition)
            || !core.TryGetSegmentModelLevelInfo(segmentId, out int currentLevel, out int maxLevel))
        {
            return false; // 레벨 비교 불가
        }

        int nextLevel = Mathf.Min(currentLevel + 1, Mathf.Max(1, maxLevel));
        if (nextLevel <= currentLevel
            || !TryGetSegmentAttackProfileForLevel(definition, currentLevel, out SegmentAttackProfile beforeProfile)
            || !TryGetSegmentAttackProfileForLevel(definition, nextLevel, out SegmentAttackProfile afterProfile))
        {
            return false; // 이미 MAX 또는 프로필 없음
        }

        WeaponStatBonusData bonus = core.GetWeaponStatBonus(segmentId); // 현재 누적 무기 강화
        StringBuilder builder = new StringBuilder(192);
        AppendLevelUpTooltipFloatLine(builder, "공격력", beforeProfile.BaseDamage, afterProfile.BaseDamage, bonus.ResolveBaseDamage(beforeProfile.BaseDamage), bonus.ResolveBaseDamage(afterProfile.BaseDamage), string.Empty, true);
        AppendLevelUpTooltipFloatLine(builder, "쿨타임", beforeProfile.Cooldown, afterProfile.Cooldown, bonus.ResolveCooldown(beforeProfile.Cooldown), bonus.ResolveCooldown(afterProfile.Cooldown), "s", true);
        AppendLevelUpTooltipFloatLine(builder, "사거리", beforeProfile.SearchRange, afterProfile.SearchRange, bonus.ResolveSearchRange(beforeProfile.SearchRange), bonus.ResolveSearchRange(afterProfile.SearchRange), "m", false);
        AppendLevelUpTooltipFloatLine(builder, "투사체속도", beforeProfile.ProjectileSpeed, afterProfile.ProjectileSpeed, bonus.ResolveProjectileSpeed(beforeProfile.ProjectileSpeed), bonus.ResolveProjectileSpeed(afterProfile.ProjectileSpeed), string.Empty, false);
        AppendLevelUpTooltipIntLine(builder, "발사 수", beforeProfile.ProjectileCount, afterProfile.ProjectileCount, bonus.ResolveProjectileCount(beforeProfile.ProjectileCount), bonus.ResolveProjectileCount(afterProfile.ProjectileCount), false);
        AppendLevelUpTooltipIntLine(builder, "관통 수", beforeProfile.PierceCount, afterProfile.PierceCount, bonus.ResolvePierceCount(beforeProfile.PierceCount), bonus.ResolvePierceCount(afterProfile.PierceCount), false);
        AppendLevelUpTooltipFloatLine(builder, "폭발 반경", beforeProfile.ExplosionRadius, afterProfile.ExplosionRadius, bonus.ResolveExplosionRadius(beforeProfile.ExplosionRadius), bonus.ResolveExplosionRadius(afterProfile.ExplosionRadius), "m", false);
        AppendLevelUpTooltipIntLine(builder, "연쇄 단계", beforeProfile.MaxChainDepth, afterProfile.MaxChainDepth, bonus.ResolveMaxChainDepth(beforeProfile.MaxChainDepth), bonus.ResolveMaxChainDepth(afterProfile.MaxChainDepth), false);
        AppendLevelUpTooltipFloatLine(builder, "연쇄 거리", beforeProfile.ChainRange, afterProfile.ChainRange, bonus.ResolveChainRange(beforeProfile.ChainRange), bonus.ResolveChainRange(afterProfile.ChainRange), "m", false);
        AppendLevelUpTooltipFloatLine(builder, "지속시간", beforeProfile.LaserDuration, afterProfile.LaserDuration, bonus.ResolveLaserDuration(beforeProfile.LaserDuration), bonus.ResolveLaserDuration(afterProfile.LaserDuration), "s", false);
        AppendLevelUpTooltipFloatLine(builder, "틱 간격", beforeProfile.LaserTickInterval, afterProfile.LaserTickInterval, bonus.ResolveLaserTickInterval(beforeProfile.LaserTickInterval), bonus.ResolveLaserTickInterval(afterProfile.LaserTickInterval), "s", false);

        string displayName = ResolveSegmentTooltipDisplayName(core, segmentId, entry.SegmentCatalogEntry);
        string summary = BuildSegmentLevelUpTooltipSummary(displayName, currentLevel, nextLevel, beforeProfile, afterProfile, bonus);
        if (!string.IsNullOrWhiteSpace(summary))
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(summary);
        }

        text = builder.ToString().TrimEnd();
        return !string.IsNullOrWhiteSpace(text);
    }

    private static bool TryGetSegmentDefinition(CoreStatProvider core, string segmentId, out SegmentDefinition definition)
    {
        definition = null;
        return core != null
            && core.SegmentCatalogAsset != null
            && !string.IsNullOrWhiteSpace(segmentId)
            && core.SegmentCatalogAsset.TryFind(segmentId, out definition)
            && definition != null;
    }

    private static bool TryGetSegmentAttackProfileForLevel(SegmentDefinition definition, int level, out SegmentAttackProfile profile)
    {
        profile = null;
        if (definition == null)
        {
            return false;
        }

        if (definition.TryGetLevel(level, out SegmentLevelDefinition levelDef) && levelDef.AttackProfile != null)
        {
            profile = levelDef.AttackProfile;
            return true;
        }

        if (!definition.TryGetSegmentPrefab(level, out GameObject prefab) || prefab == null)
        {
            return false;
        }

        GenericSegmentWeapon weapon = prefab.GetComponentInChildren<GenericSegmentWeapon>(true);
        if (weapon == null || weapon.AttackProfile == null)
        {
            return false;
        }

        profile = weapon.AttackProfile;
        return true;
    }

    private static bool AppendLevelUpTooltipFloatLine(
        StringBuilder builder,
        string label,
        float beforeBase,
        float afterBase,
        float beforeFinal,
        float afterFinal,
        string suffix,
        bool alwaysShow)
    {
        if (!alwaysShow && !HasTooltipValueChanged(beforeBase, afterBase) && !HasTooltipValueChanged(beforeFinal, afterFinal))
        {
            return false;
        }

        AppendTooltipLine(builder, label, FormatLevelUpTooltipValue(beforeBase, beforeFinal, suffix), FormatLevelUpTooltipValue(afterBase, afterFinal, suffix));
        return true;
    }

    private static bool AppendLevelUpTooltipIntLine(
        StringBuilder builder,
        string label,
        int beforeBase,
        int afterBase,
        int beforeFinal,
        int afterFinal,
        bool alwaysShow)
    {
        if (!alwaysShow && beforeBase == afterBase && beforeFinal == afterFinal)
        {
            return false;
        }

        AppendTooltipLine(builder, label, FormatLevelUpTooltipValue(beforeBase, beforeFinal), FormatLevelUpTooltipValue(afterBase, afterFinal));
        return true;
    }

    private static string FormatLevelUpTooltipValue(float baseValue, float finalValue, string suffix)
    {
        string baseText = FormatTooltipFloat(baseValue, suffix);
        float delta = finalValue - baseValue;
        if (Mathf.Abs(delta) <= 0.0001f)
        {
            return baseText;
        }

        string sign = delta > 0f ? "+" : string.Empty;
        return $"{baseText}({sign}{FormatTooltipFloat(delta, suffix)})";
    }

    private static string FormatLevelUpTooltipValue(int baseValue, int finalValue)
    {
        int delta = finalValue - baseValue;
        if (delta == 0)
        {
            return baseValue.ToString(CultureInfo.InvariantCulture);
        }

        string sign = delta > 0 ? "+" : string.Empty;
        return $"{baseValue.ToString(CultureInfo.InvariantCulture)}({sign}{delta.ToString(CultureInfo.InvariantCulture)})";
    }

    private static string BuildSegmentLevelUpTooltipSummary(
        string displayName,
        int currentLevel,
        int nextLevel,
        SegmentAttackProfile beforeProfile,
        SegmentAttackProfile afterProfile,
        WeaponStatBonusData bonus)
    {
        string name = string.IsNullOrWhiteSpace(displayName) ? "세그먼트" : displayName.Trim();
        int beforeProjectileCount = bonus.ResolveProjectileCount(beforeProfile.ProjectileCount);
        int afterProjectileCount = bonus.ResolveProjectileCount(afterProfile.ProjectileCount);
        if (afterProfile.FireProjectilesSequentially && afterProjectileCount > 1)
        {
            return $"{name}이 {afterProjectileCount}발 순차발사합니다";
        }

        if (afterProjectileCount > beforeProjectileCount)
        {
            return $"{name}이 {afterProjectileCount}발 발사합니다";
        }

        if (bonus.ResolveBaseDamage(afterProfile.BaseDamage) > bonus.ResolveBaseDamage(beforeProfile.BaseDamage) + 0.0001f)
        {
            return $"{name}의 화력이 증가합니다";
        }

        if (bonus.ResolveCooldown(afterProfile.Cooldown) < bonus.ResolveCooldown(beforeProfile.Cooldown) - 0.0001f)
        {
            return $"{name}의 공격 주기가 빨라집니다";
        }

        if (bonus.ResolveExplosionRadius(afterProfile.ExplosionRadius) > bonus.ResolveExplosionRadius(beforeProfile.ExplosionRadius) + 0.0001f)
        {
            return $"{name}의 폭발 범위가 넓어집니다";
        }

        return $"{name} Lv.{currentLevel} -> Lv.{nextLevel}";
    }

    private bool TryBuildSegmentEnhancementTooltipText(SpawnedCardEntry entry, out string text)
    {
        text = string.Empty;
        if (entry == null || entry.WeaponDefinition == null)
        {
            return false;
        }

        CoreStatProvider core = CoreStatProvider.Active;
        string segmentId = !string.IsNullOrWhiteSpace(entry.SegmentId)
            ? entry.SegmentId.Trim()
            : entry.WeaponDefinition.NormalizedTargetSegmentId;
        if (core == null || string.IsNullOrWhiteSpace(segmentId) || !TryGetSegmentAttackProfile(core, segmentId, out SegmentAttackProfile profile))
        {
            return false;
        }

        WeaponStatBonusData beforeBonus = core.GetWeaponStatBonus(segmentId);
        WeaponStatBonusData afterBonus = beforeBonus;
        afterBonus.AddDefinition(entry.WeaponDefinition, entry.WeaponEnhancementTier, profile.BaseDamage);
        float beforeDps = EstimateSingleTargetDps(core, segmentId, profile, beforeBonus);
        float afterDps = EstimateSingleTargetDps(core, segmentId, profile, afterBonus);

        StringBuilder builder = new StringBuilder(128);
        bool affectsDps = false;
        affectsDps |= AppendTooltipFloatLine(builder, "공격력", CalculateTooltipSegmentDamage(core, segmentId, profile, beforeBonus), CalculateTooltipSegmentDamage(core, segmentId, profile, afterBonus), string.Empty);
        AppendTooltipFloatLine(builder, "사거리", CalculateTooltipSegmentSearchRange(core, segmentId, profile, beforeBonus), CalculateTooltipSegmentSearchRange(core, segmentId, profile, afterBonus), "m");
        AppendTooltipFloatLine(builder, "투사체속도", beforeBonus.ResolveProjectileSpeed(profile.ProjectileSpeed), afterBonus.ResolveProjectileSpeed(profile.ProjectileSpeed), string.Empty);
        affectsDps |= AppendTooltipIntLine(builder, "발사 수", beforeBonus.ResolveProjectileCount(profile.ProjectileCount), afterBonus.ResolveProjectileCount(profile.ProjectileCount));
        affectsDps |= AppendTooltipFloatLine(builder, "쿨타임", CalculateTooltipSegmentCooldown(core, segmentId, profile, beforeBonus), CalculateTooltipSegmentCooldown(core, segmentId, profile, afterBonus), "s");
        AppendTooltipIntLine(builder, "관통 수", beforeBonus.ResolvePierceCount(profile.PierceCount), afterBonus.ResolvePierceCount(profile.PierceCount));
        AppendTooltipFloatLine(builder, "폭발 반경", CalculateTooltipSegmentExplosionRadius(core, segmentId, profile, beforeBonus), CalculateTooltipSegmentExplosionRadius(core, segmentId, profile, afterBonus), "m");
        AppendTooltipIntLine(builder, "연쇄 단계", beforeBonus.ResolveMaxChainDepth(profile.MaxChainDepth), afterBonus.ResolveMaxChainDepth(profile.MaxChainDepth));
        AppendTooltipFloatLine(builder, "연쇄 거리", beforeBonus.ResolveChainRange(profile.ChainRange), afterBonus.ResolveChainRange(profile.ChainRange), "m");
        AppendTooltipPercentLine(builder, "체인 유지", beforeBonus.ResolveChainDamageFalloff(profile.ChainDamageFalloff), afterBonus.ResolveChainDamageFalloff(profile.ChainDamageFalloff));
        AppendTooltipFloatLine(builder, "부채꼴 각", beforeBonus.ResolveSideConeAngle(profile.SideConeAngle), afterBonus.ResolveSideConeAngle(profile.SideConeAngle), "도");
        affectsDps |= AppendTooltipFloatLine(builder, "지속시간", beforeBonus.ResolveLaserDuration(profile.LaserDuration), afterBonus.ResolveLaserDuration(profile.LaserDuration), "s");
        affectsDps |= AppendTooltipFloatLine(builder, "틱 간격", beforeBonus.ResolveLaserTickInterval(profile.LaserTickInterval), afterBonus.ResolveLaserTickInterval(profile.LaserTickInterval), "s");
        AppendTooltipFloatLine(builder, "구르기 거리", beforeBonus.ResolveLandingRollDistance(profile.LandingRollDistance), afterBonus.ResolveLandingRollDistance(profile.LandingRollDistance), "m");
        AppendTooltipFloatLine(builder, "구르기 시간", beforeBonus.ResolveLandingRollDuration(profile.LandingRollDuration), afterBonus.ResolveLandingRollDuration(profile.LandingRollDuration), "s");
        AppendTooltipPercentLine(builder, "관통 피해", beforeBonus.ResolveSawPierceDamageRatio(profile.SawPierceDamageRatio), afterBonus.ResolveSawPierceDamageRatio(profile.SawPierceDamageRatio));

        if (affectsDps && IsValidDps(beforeDps) && IsValidDps(afterDps) && HasTooltipValueChanged(beforeDps, afterDps))
        {
            AppendTooltipLine(builder, "DPS", FormatCompactDps(beforeDps), FormatCompactDps(afterDps));
        }

        text = builder.ToString().TrimEnd();
        return !string.IsNullOrWhiteSpace(text);
    }

    private static bool AppendTooltipFloatLine(StringBuilder builder, string label, float beforeValue, float afterValue, string suffix)
    {
        if (!HasTooltipValueChanged(beforeValue, afterValue))
        {
            return false;
        }

        AppendTooltipLine(builder, label, FormatTooltipFloat(beforeValue, suffix), FormatTooltipFloat(afterValue, suffix));
        return true;
    }

    private static bool AppendTooltipPercentLine(StringBuilder builder, string label, float beforeValue, float afterValue)
    {
        if (!HasTooltipValueChanged(beforeValue, afterValue))
        {
            return false;
        }

        AppendTooltipLine(builder, label, FormatTooltipPercent(beforeValue), FormatTooltipPercent(afterValue));
        return true;
    }

    private static bool AppendTooltipIntLine(StringBuilder builder, string label, int beforeValue, int afterValue)
    {
        if (beforeValue == afterValue)
        {
            return false;
        }

        AppendTooltipLine(builder, label, beforeValue.ToString(CultureInfo.InvariantCulture), afterValue.ToString(CultureInfo.InvariantCulture));
        return true;
    }

    private static void AppendTooltipLine(StringBuilder builder, string label, string beforeText, string afterText)
    {
        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(label)
            .Append(' ')
            .Append(beforeText)
            .Append(" -> ")
            .Append("<color=")
            .Append(CardTooltipValueColor)
            .Append('>')
            .Append(afterText)
            .Append("</color>");
    }

    private static bool HasTooltipValueChanged(float beforeValue, float afterValue)
    {
        return Mathf.Abs(beforeValue - afterValue) > 0.0001f;
    }

    private static bool IsValidDps(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }

    private static float EstimateSingleTargetDps(CoreStatProvider core, string segmentId, SegmentAttackProfile profile, WeaponStatBonusData bonus)
    {
        if (core == null || profile == null)
        {
            return 0f;
        }

        float cycleTime = EstimateDpsCycleTime(core, segmentId, profile, bonus);
        float castDamage = EstimateSingleTargetCastDamage(core, segmentId, profile, bonus);
        return cycleTime > 0.0001f ? castDamage / cycleTime : 0f;
    }

    private static float EstimateDpsCycleTime(CoreStatProvider core, string segmentId, SegmentAttackProfile profile, WeaponStatBonusData bonus)
    {
        int projectileCount = bonus.ResolveProjectileCount(profile.ProjectileCount);
        float cooldown = CalculateTooltipSegmentCooldown(core, segmentId, profile, bonus);
        if (profile.FireProjectilesSequentially && projectileCount > 1)
        {
            cooldown += Mathf.Max(0f, profile.ProjectileFireDelay) * (projectileCount - 1);
        }

        return Mathf.Max(0.05f, cooldown);
    }

    private static float EstimateSingleTargetCastDamage(CoreStatProvider core, string segmentId, SegmentAttackProfile profile, WeaponStatBonusData bonus)
    {
        int projectileCount = bonus.ResolveProjectileCount(profile.ProjectileCount);
        float damage = CalculateTooltipSegmentDamage(core, segmentId, profile, bonus);
        switch (profile.MoveType)
        {
            case SegmentAttackMoveType.Laser:
                return damage * GetDpsTickCount(bonus.ResolveLaserDuration(profile.LaserDuration), bonus.ResolveLaserTickInterval(profile.LaserTickInterval));
            case SegmentAttackMoveType.ExpandingFlameSphere:
                return damage * projectileCount * GetDpsTickCount(profile.ProjectileLifetime, bonus.ResolveLaserTickInterval(profile.LaserTickInterval));
            case SegmentAttackMoveType.ChainLightning:
                return damage;
            default:
                return damage * projectileCount;
        }
    }

    private static int GetDpsTickCount(float duration, float interval)
    {
        float safeDuration = Mathf.Max(0.05f, duration);
        float safeInterval = Mathf.Max(0.02f, interval);
        return Mathf.Max(1, Mathf.CeilToInt(safeDuration / safeInterval));
    }

    private static string FormatCompactDps(float value)
    {
        return value >= 100f
            ? Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.#", CultureInfo.InvariantCulture);
    }

    private static string FormatTooltipFloat(float value, string suffix)
    {
        string formatted = Mathf.Abs(value) >= 100f
            ? Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.#", CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(suffix) ? formatted : $"{formatted}{suffix}";
    }

    private static string FormatTooltipPercent(float value)
    {
        return $"{(value * 100f).ToString("0.#", CultureInfo.InvariantCulture)}%";
    }
}
