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
    private static readonly Vector2 CommonCardIconSlotSize = new Vector2(130f, 130f); // 공통카드 Image 슬롯 기준 크기

    // 안건준 추가 - 0623 : 세그먼트 ID + 레벨로 SegmentDefinition 아이콘 스프라이트 조회
    private static Sprite GetSegmentIconSprite(string segmentId, int level)
    {
        SegmentCatalogAsset catalog = CoreStatProvider.Active?.SegmentCatalogAsset;
        if (catalog == null || string.IsNullOrWhiteSpace(segmentId))
        {
            return null; // 카탈로그 없음
        }

        if (!catalog.TryFind(segmentId, out SegmentDefinition def))
        {
            return null; // 정의 없음
        }

        return def.GetIconSpriteForLevel(level); // 레벨별 스프라이트
    }

    // 안건준 추가 - 0623 : 세그먼트 카드 아이콘 크기 조절값 — CardUI 인스펙터값 우선, 없으면 SegmentDefinition 값 사용
    private float GetSegmentIconSizeOffset(string segmentId)
    {
        if (!Mathf.Approximately(segmentCardIconSizeOffset, 0f))
        {
            return segmentCardIconSizeOffset; // CardUI 인스펙터 값 우선
        }

        SegmentCatalogAsset catalog = CoreStatProvider.Active?.SegmentCatalogAsset;
        if (catalog == null || string.IsNullOrWhiteSpace(segmentId))
        {
            return 0f; // 기본값
        }

        return catalog.TryFind(segmentId, out SegmentDefinition def) ? def.CardIconSizeOffset : 0f;
    }

    // 안건준 추가 - 0623 : SegmentUpgradeCard 같은 커스텀 프리팹에 Card_Text / DescText / Image 직접 주입
    private static void ApplyCardTextsDirectly(GameObject root, string title, string desc, Sprite iconSprite = null, float iconSizeOffset = 0f, bool iconSizeAlreadyApplied = false)
    {
        if (root == null)
        {
            return;
        }

        TMPro.TMP_Text[] texts = root.GetComponentsInChildren<TMPro.TMP_Text>(true);
        TMPro.TMP_Text cardText = null;
        TMPro.TMP_Text descText = null;

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].gameObject.name == "Card_Text")
            {
                cardText = texts[i];
            }
            else if (texts[i].gameObject.name == "DescText")
            {
                descText = texts[i];
            }
        }

        if (cardText != null && !string.IsNullOrWhiteSpace(title))
        {
            ApplyDirectSingleLineSizing(cardText, title);
            cardText.text = title; // 세그먼트 이름 (캐논, 미사일 등)
        }

        if (descText != null && !string.IsNullOrWhiteSpace(desc))
        {
            string displayDesc = SegmentCardTagPresenter.Apply(root, desc, descText);
            descText.richText = true;
            ApplyDirectDescriptionSizing(descText, displayDesc);
            descText.text = displayDesc; // WeaponDefinition Description
        }

        // 안건준 추가 - 0623 : "Image" 오브젝트에 세그먼트 Lv1 아이콘 적용
        if (iconSprite != null)
        {
            Transform imageTransform = root.transform.Find("Image");
            if (imageTransform != null && imageTransform.TryGetComponent(out UnityEngine.UI.Image img))
            {
                Vector2 slotSize = ResolveCommonCardIconSlotSize(img.rectTransform); // 공통카드 기준 슬롯 크기
                img.sprite = iconSprite;
                img.overrideSprite = null;
                img.enabled = true;
                img.color = Color.white;
                img.type = UnityEngine.UI.Image.Type.Simple;
                img.preserveAspect = true;
                if (slotSize.sqrMagnitude <= 0.0001f)
                {
                    img.SetNativeSize(); // 슬롯 정보가 없을 때만 fallback
                    slotSize = ResolveCommonCardIconSlotSize(img.rectTransform);
                }

                img.rectTransform.sizeDelta = slotSize; // 원본 PNG 크기 대신 기존 UI 슬롯 크기 사용
                if (!iconSizeAlreadyApplied)
                {
                    img.rectTransform.sizeDelta = ApplyCommonCardIconSizeOffset(slotSize, iconSizeOffset);
                }
            }
            else
            {
                Debug.LogWarning($"[CardUI] 'Image' 자식 오브젝트를 찾지 못했습니다. root={root.name}, 자식 수={root.transform.childCount}");
            }
        }
    }

    private static void ApplyStatUpgradeCardTextsDirectly(GameObject root, string title, string desc)
    {
        if (root == null)
        {
            return;
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text cardText = null;
        TMP_Text descText = null;
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].gameObject.name == "Card_Text")
            {
                cardText = texts[i];
            }
            else if (texts[i].gameObject.name == "DescText")
            {
                descText = texts[i];
            }
        }

        if (cardText != null && !string.IsNullOrWhiteSpace(title))
        {
            ApplyStatCardTextStyle(cardText, 24f); // 공통카드 제목은 한 줄 유지
            cardText.text = title;
        }

        if (descText != null && !string.IsNullOrWhiteSpace(desc))
        {
            ApplyStatCardTextStyle(descText, 20f); // 공통카드 설명은 줄바꿈 대신 축소
            descText.richText = true;
            descText.text = desc;
        }
    }

    private static void ApplyStatUpgradeCardIcon(GameObject root, StatUpgradeDefinition definition)
    {
        if (root == null || definition == null)
        {
            return;
        }

        Transform imageTransform = root.transform.Find("Image");
        if (imageTransform == null || !imageTransform.TryGetComponent(out Image img))
        {
            return;
        }

        Vector2 slotSize = ResolveCommonCardIconSlotSize(img.rectTransform); // 공통카드 기준 슬롯 크기
        Sprite icon = definition.CardIconSprite;
        img.sprite = icon;
        img.overrideSprite = null;
        img.enabled = icon != null;
        img.color = Color.white;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        if (icon == null)
        {
            return;
        }

        if (slotSize.sqrMagnitude <= 0.0001f)
        {
            img.SetNativeSize(); // 슬롯 정보가 없을 때만 fallback
            slotSize = ResolveCommonCardIconSlotSize(img.rectTransform);
        }

        img.rectTransform.sizeDelta = slotSize; // 원본 PNG 크기 대신 기존 UI 크기 사용
        img.rectTransform.sizeDelta = ApplyCommonCardIconSizeOffset(slotSize, definition.CardIconSizeOffset);
    }

    private static Vector2 ResolveCommonCardIconSlotSize(RectTransform iconRect)
    {
        if (iconRect == null)
        {
            return CommonCardIconSlotSize; // 안전 fallback
        }

        Vector2 slotSize = iconRect.sizeDelta;
        if (slotSize.sqrMagnitude <= 0.0001f)
        {
            return CommonCardIconSlotSize; // stretch/미설정이면 공통카드 기준
        }

        return new Vector2(
            Mathf.Max(Mathf.Abs(slotSize.x), CommonCardIconSlotSize.x),
            Mathf.Max(Mathf.Abs(slotSize.y), CommonCardIconSlotSize.y)); // 작은 슬롯은 공통 크기로 보정
    }

    private static Vector2 ApplyCommonCardIconSizeOffset(Vector2 slotSize, float offset)
    {
        float positiveOffset = Mathf.Max(0f, Mathf.Clamp(offset, -100f, 100f)); // 과거 축소값(-80)은 공통 크기 이하로 줄이지 않음
        if (Mathf.Approximately(positiveOffset, 0f))
        {
            return slotSize; // 공통카드 크기
        }

        float scale = 1f + positiveOffset / 100f;
        return slotSize * scale;
    }

    private static void ApplyStatCardTextStyle(TMP_Text text, float maxFontSize)
    {
        if (text == null)
        {
            return;
        }

        float resolvedMax = Mathf.Min(maxFontSize, text.fontSize > 0f ? text.fontSize : maxFontSize);
        resolvedMax = Mathf.Max(8f, resolvedMax);
        text.enableAutoSizing = true;
        text.fontSizeMax = resolvedMax;
        text.fontSizeMin = Mathf.Max(8f, resolvedMax * 0.5f);
        text.fontSize = resolvedMax;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    private static void ApplyDirectDescriptionSizing(TMP_Text descText, string description)
    {
        if (descText == null)
        {
            return;
        }

        float baseSize = ResolveDirectBaseFontSize(descText); // 프리팹 실제 글자 크기 기준
        float maxSize = CountDescriptionLines(description) >= 3 ? baseSize * 0.86f : baseSize;
        ConfigureDirectAutoSize(descText, maxSize, true);
    }

    private static void ApplyDirectSingleLineSizing(TMP_Text text, string value)
    {
        if (text == null)
        {
            return;
        }

        float baseSize = ResolveDirectBaseFontSize(text); // fontSizeMax(72)로 확대되는 문제 방지
        ConfigureDirectAutoSize(text, baseSize, false);
    }

    private static float ResolveDirectBaseFontSize(TMP_Text text)
    {
        if (text == null)
        {
            return 24f; // 안전 fallback
        }

        if (text.fontSize > 0f)
        {
            return text.fontSize; // 프리팹에 배치된 실제 표시 크기
        }

        return text.fontSizeMax > 0f ? Mathf.Min(text.fontSizeMax, 24f) : 24f; // 비정상 값만 보정
    }

    private static void ConfigureDirectAutoSize(TMP_Text text, float maxSize, bool allowWrapping)
    {
        text.enableAutoSizing = true;
        text.fontSizeMax = maxSize;
        text.fontSizeMin = Mathf.Max(8f, maxSize * 0.62f);
        text.fontSize = maxSize;
        text.textWrappingMode = allowWrapping ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
    }

    private static int CountDescriptionLines(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return 0;
        }

        string normalized = description.Replace("\r\n", "\n").Replace('\r', '\n');
        return Mathf.Max(1, normalized.Split('\n').Length);
    }
}
