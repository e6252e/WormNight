using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class SegmentCardTagPresenter
{
    private const string TagRootName = "SegmentTagRoot";
    private const float DefaultTagRootY = -118f;
    private const float ChipWidth = 54f;
    private const float ChipHeight = 21f;
    private const float ChipSpacing = 3f;
    private const int ChipSpriteSize = 32;
    private const int ChipCornerRadius = 8;
    private static readonly Regex RichTextTagRegex = new Regex("<[^>]+>", RegexOptions.Compiled);
    private static Sprite roundedChipSprite;

    private static readonly Color MeleeColor = new Color(0.54f, 0.23f, 0.2f, 0.96f);
    private static readonly Color MagicColor = new Color(0.18f, 0.34f, 0.64f, 0.96f);
    private static readonly Color SupportColor = new Color(0.19f, 0.45f, 0.33f, 0.96f);
    private static readonly Color SubTagColor = new Color(0.28f, 0.26f, 0.31f, 0.96f);
    private static readonly Color TagTextColor = Color.white;
    private static readonly Color TagOutlineColor = new Color(1f, 1f, 1f, 0.16f);

    public static string Apply(GameObject cardRoot, string rawDescription, TMP_Text fontSource)
    {
        if (!TrySplitLeadingTags(rawDescription, out List<string> tags, out string bodyDescription))
        {
            SetRootVisible(cardRoot, false);
            return rawDescription;
        }

        RectTransform tagRoot = EnsureTagRoot(cardRoot);
        if (tagRoot == null)
        {
            return bodyDescription;
        }

        tagRoot.gameObject.SetActive(true);
        ConfigureTagRoot(tagRoot);
        RebuildTags(tagRoot, tags, fontSource);
        return bodyDescription;
    }

    private static bool TrySplitLeadingTags(string rawDescription, out List<string> tags, out string bodyDescription)
    {
        tags = new List<string>(4);
        bodyDescription = rawDescription;
        if (string.IsNullOrWhiteSpace(rawDescription))
        {
            return false;
        }

        string plain = RichTextTagRegex.Replace(rawDescription, string.Empty).TrimStart();
        int cursor = 0;
        while (cursor < plain.Length)
        {
            while (cursor < plain.Length && char.IsWhiteSpace(plain[cursor]))
            {
                cursor++;
            }

            if (cursor >= plain.Length || plain[cursor] != '[')
            {
                break;
            }

            int closeIndex = plain.IndexOf(']', cursor + 1);
            if (closeIndex <= cursor + 1)
            {
                break;
            }

            string tag = plain.Substring(cursor + 1, closeIndex - cursor - 1).Trim();
            if (string.IsNullOrWhiteSpace(tag))
            {
                break;
            }

            tags.Add(tag);
            cursor = closeIndex + 1;
        }

        if (tags.Count == 0)
        {
            return false;
        }

        bodyDescription = cursor < plain.Length ? plain.Substring(cursor).TrimStart() : string.Empty;
        return true;
    }

    private static RectTransform EnsureTagRoot(GameObject cardRoot)
    {
        if (cardRoot == null)
        {
            return null;
        }

        Transform found = cardRoot.transform.Find(TagRootName);
        if (found != null)
        {
            return found as RectTransform ?? found.GetComponent<RectTransform>();
        }

        GameObject rootObject = new GameObject(TagRootName, typeof(RectTransform));
        rootObject.layer = cardRoot.layer;
        rootObject.transform.SetParent(cardRoot.transform, false);

        RectTransform rect = rootObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, DefaultTagRootY);
        rect.sizeDelta = new Vector2(250f, 24f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        return rect;
    }

    private static void ConfigureTagRoot(RectTransform tagRoot)
    {
        HorizontalLayoutGroup layout = EnsureComponent<HorizontalLayoutGroup>(tagRoot.gameObject);
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = ChipSpacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    private static void RebuildTags(RectTransform tagRoot, List<string> tags, TMP_Text fontSource)
    {
        for (int i = tagRoot.childCount - 1; i >= 0; i--)
        {
            Object.Destroy(tagRoot.GetChild(i).gameObject);
        }

        for (int i = 0; i < tags.Count; i++)
        {
            CreateTagChip(tagRoot, tags[i], fontSource);
        }
    }

    private static void CreateTagChip(RectTransform parent, string tag, TMP_Text fontSource)
    {
        GameObject chipObject = new GameObject("Tag_" + tag, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(LayoutElement));
        chipObject.layer = parent.gameObject.layer;
        chipObject.transform.SetParent(parent, false);

        RectTransform chipRect = chipObject.GetComponent<RectTransform>();
        chipRect.sizeDelta = new Vector2(ChipWidth, ChipHeight);
        chipRect.localScale = Vector3.one;
        chipRect.localRotation = Quaternion.identity;

        Image background = chipObject.GetComponent<Image>();
        background.sprite = GetRoundedChipSprite();
        background.type = Image.Type.Sliced;
        background.color = ResolveTagColor(tag);
        background.raycastTarget = false;

        Outline outline = chipObject.GetComponent<Outline>();
        outline.effectColor = TagOutlineColor;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        LayoutElement layout = chipObject.GetComponent<LayoutElement>();
        layout.preferredWidth = chipRect.sizeDelta.x;
        layout.preferredHeight = chipRect.sizeDelta.y;
        layout.minWidth = chipRect.sizeDelta.x;
        layout.minHeight = chipRect.sizeDelta.y;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = chipObject.layer;
        textObject.transform.SetParent(chipObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        textRect.localScale = Vector3.one;
        textRect.localRotation = Quaternion.identity;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = tag;
        text.color = TagTextColor;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 11f;
        text.fontStyle = FontStyles.Bold;
        text.enableAutoSizing = true;
        text.fontSizeMin = 8f;
        text.fontSizeMax = 11f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        if (fontSource != null && fontSource.font != null)
        {
            text.font = fontSource.font;
        }
    }

    private static Color ResolveTagColor(string tag)
    {
        if (tag == "밀리")
        {
            return MeleeColor;
        }

        if (tag == "마법")
        {
            return MagicColor;
        }

        if (tag == "지원")
        {
            return SupportColor;
        }

        return SubTagColor;
    }

    private static void SetRootVisible(GameObject cardRoot, bool visible)
    {
        if (cardRoot == null)
        {
            return;
        }

        Transform found = cardRoot.transform.Find(TagRootName);
        if (found != null)
        {
            found.gameObject.SetActive(visible);
        }
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static Sprite GetRoundedChipSprite()
    {
        if (roundedChipSprite != null)
        {
            return roundedChipSprite;
        }

        Texture2D texture = new Texture2D(ChipSpriteSize, ChipSpriteSize, TextureFormat.RGBA32, false)
        {
            name = "SegmentTagChipRoundedSpriteTexture",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float halfSize = ChipSpriteSize * 0.5f;
        float innerHalf = halfSize - ChipCornerRadius;
        Color32[] pixels = new Color32[ChipSpriteSize * ChipSpriteSize];
        for (int y = 0; y < ChipSpriteSize; y++)
        {
            for (int x = 0; x < ChipSpriteSize; x++)
            {
                float px = Mathf.Abs(x + 0.5f - halfSize) - innerHalf;
                float py = Mathf.Abs(y + 0.5f - halfSize) - innerHalf;
                float outsideX = Mathf.Max(0f, px);
                float outsideY = Mathf.Max(0f, py);
                float distance = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY);
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(ChipCornerRadius + 0.5f - distance) * 255f);
                pixels[y * ChipSpriteSize + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        roundedChipSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, ChipSpriteSize, ChipSpriteSize),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(ChipCornerRadius, ChipCornerRadius, ChipCornerRadius, ChipCornerRadius));
        roundedChipSprite.name = "SegmentTagChipRoundedSprite";
        roundedChipSprite.hideFlags = HideFlags.HideAndDontSave;
        return roundedChipSprite;
    }
}
