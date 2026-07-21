// ???? ??? - 0624
// ?? ???????? UI ???
// ?? ?????????? ????? ?????? ??? / ????????? ??? ???
// ?? ??????? ??? ??????? + ???/??? ????????
// ?? ???????? AssetDatabase ??AUDIO ??????? wav ??? ??

using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

public class CardSoundSelector : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private CardSoundManager soundManager;
    [SerializeField] private Canvas           targetCanvas;
    [Tooltip("????????? TextMeshProUGUI ?????? (EffectFont ??\n????? ?? ??? ???")]
    [SerializeField] private TextMeshProUGUI  fontSource;
    [SerializeField] private bool showRuntimePanel = false;

    [Header("??? ???????")]
    [SerializeField] private float panelWidth   = 280f;
    [SerializeField] private float panelHeight  = 360f;
    [SerializeField] private float tabHeight    = 28f;
    [Tooltip("???????? ???(CardEffectSelector) ????? ??????????? ?????n" +
             "X = CardEffectSelector panelWidth + gap(8) + ??? offset")]
    [SerializeField] private Vector2 panelOffset = new Vector2(336f, 8f); // ??????? (CardEffectSelector 320px + gap)

    [Header("???/ ?? ???")]
    [SerializeField] private float buttonHeightPx = 20f;
    [SerializeField] private float buttonFontSize = 9f;
    [SerializeField] private float headerFontSize = 9f;
    [SerializeField] private float tabFontSize    = 11f;
    [SerializeField] private float slotFontSize   = 9f;

    [Header("Animation")]
    [SerializeField] private float slideTime      = 0.22f;
    [SerializeField] private float autoCloseDelay = 0.5f;

    // ???? ??? ????????????????????????????????????????????????????????????????????
    private static readonly Color PanelBg      = new Color(0.04f, 0.10f, 0.10f, 0.95f); // ??? ??: ???
    private static readonly Color TabBg        = new Color(0.05f, 0.38f, 0.38f, 1f);   // ??? ?:  ??
    private static readonly Color SlotBg       = new Color(0.12f, 0.12f, 0.17f, 1f);
    private static readonly Color BtnNormal    = new Color(0.15f, 0.15f, 0.20f, 1f);
    private static readonly Color BtnPlaying   = new Color(0.10f, 0.40f, 0.50f, 1f);
    private static readonly Color BtnAppearSel = new Color(0.08f, 0.46f, 0.12f, 1f);
    private static readonly Color BtnSelectSel = new Color(0.50f, 0.35f, 0.00f, 1f);
    private static readonly Color HeaderColor  = new Color(0.70f, 0.85f, 1.00f, 1f);
    private static readonly Color TextColor    = new Color(0.90f, 0.90f, 0.92f, 1f);

    // ???? ??? ?? ??????????????????????????????????????????????????????????
    private enum AssignMode { Appear, Select }
    private AssignMode assignMode = AssignMode.Appear;

    // ???? ???? ??? ??????????????????????????????????????????????????????????
    private RectTransform panelRT;
    private TextMeshProUGUI tabLabel;
    private TextMeshProUGUI appearSlotText;
    private TextMeshProUGUI selectSlotText;
    private bool isExpanded = false;
    private Tweener slideTween;
    private float leaveTimer = 0f;

    // ??? ??? ???? Image (?? ?????
    private readonly Dictionary<string, Image> clipBtnImages = new();
    private AudioClip lastPlayedClip;

    private static readonly string AudioRoot =
        "Assets/ThirdParty/03_LevelSystem/Cute UI _ Interact Sound Effects Pack/AUDIO";

    // PlayerPrefs 키 - 플레이 세션 간 선택 클립 유지
    private const string PrefKeyAppear = "CardSoundSelector_Appear";
    private const string PrefKeySelect = "CardSoundSelector_Select";

    // ??????????????????????????????????????????????????????????????????????????????????
    //  ????
    // ??????????????????????????????????????????????????????????????????????????????????

    private void Start()
    {
        if (soundManager == null)
            soundManager = GetComponent<CardSoundManager>();
        if (soundManager == null)
            soundManager = FindFirstObjectByType<CardSoundManager>();

        if (!showRuntimePanel)
        {
            if (soundManager != null)
                RestoreSavedClips();
            enabled = false;
            return;
        }

        if (targetCanvas == null)
        {
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.name == "Canvas") { targetCanvas = c; break; }
            }
            if (targetCanvas == null)
                targetCanvas = FindFirstObjectByType<Canvas>();
        }

        if (soundManager == null || targetCanvas == null)
        {
            Debug.LogWarning("[CardSoundSelector] SoundManager ??? Canvas ????? ???????");
            return;
        }

        BuildUI();
        RestoreSavedClips(); // PlayerPrefs 에 저장된 클립 복원
    }

    // ??????????????????????????????????????????????????????????????????????????????????
    //  ????????? ??? ???
    // ??????????????????????????????????????????????????????????????????????????????????

    private void ApplyFont(TextMeshProUGUI tmp)
    {
        if (fontSource != null) tmp.font = fontSource.font;
    }

    private static Vector2 GetMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    private void Update()
    {
        if (panelRT == null || !isExpanded) return;

        Vector3[] corners = new Vector3[4];
        panelRT.GetWorldCorners(corners);
        Vector2 mp = GetMousePosition();

        bool inside = mp.x >= corners[0].x && mp.x <= corners[2].x &&
                      mp.y >= corners[0].y && mp.y <= corners[2].y;
        if (!inside)
        {
            leaveTimer += Time.unscaledDeltaTime;
            if (leaveTimer >= autoCloseDelay) Collapse();
        }
        else leaveTimer = 0f;
    }

    // ??????????????????????????????????????????????????????????????????????????????????
    //  UI ??
    // ??????????????????????????????????????????????????????????????????????????????????

    private void BuildUI()
    {
        // ???? ?? ??? (????? ??????????????????????????????????
        GameObject panelGO = new GameObject("SoundSelectorPanel");
        panelRT = panelGO.AddComponent<RectTransform>();
        panelGO.transform.SetParent(targetCanvas.transform, false);
        panelGO.transform.SetAsLastSibling();

        // ??????? ??CardEffectSelector ?? ????? ???
        panelRT.anchorMin        = new Vector2(0f, 0f);
        panelRT.anchorMax        = new Vector2(0f, 0f);
        panelRT.pivot            = new Vector2(0f, 0f);
        panelRT.anchoredPosition = panelOffset;
        panelRT.sizeDelta        = new Vector2(panelWidth, tabHeight);

        Image panelImg = panelGO.AddComponent<Image>();
        panelImg.color = PanelBg;

        Mask panelMask = panelGO.AddComponent<Mask>();
        panelMask.showMaskGraphic = true;

        // ???? ??(??? ??) ????????????????????????????????????????
        BuildTab(panelRT);

        // ???? ??? ??? ??????????????????????????????????????????????????
        BuildSlotHeader(panelRT);

        // ???? ??? ?? ?? ????????????????????????????????????????
        BuildAssignToggle(panelRT);

        // ???? ??????? (????? ????????????????????????????
        BuildSoundList(panelRT);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRT);

        RefreshSlotLabels();
    }

    private void BuildTab(RectTransform parent)
    {
        GameObject tabGO = new GameObject("Tab");
        tabGO.transform.SetParent(parent, false);
        RectTransform tabRT = tabGO.AddComponent<RectTransform>();
        tabRT.anchorMin        = new Vector2(0f, 0f);
        tabRT.anchorMax        = new Vector2(1f, 0f);
        tabRT.pivot            = new Vector2(0.5f, 0f);
        tabRT.anchoredPosition = Vector2.zero;
        tabRT.sizeDelta        = new Vector2(0f, tabHeight);

        Image tabImg = tabGO.AddComponent<Image>();
        tabImg.color = TabBg;

        Button tabBtn = tabGO.AddComponent<Button>();
        tabBtn.targetGraphic = tabImg;
        ColorBlock cb = tabBtn.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        cb.pressedColor     = new Color(0.8f, 0.8f, 0.8f, 1f);
        tabBtn.colors = cb;
        tabBtn.onClick.AddListener(TogglePanel);

        GameObject textGO = new GameObject("Label");
        textGO.transform.SetParent(tabGO.transform, false);
        RectTransform tRT = textGO.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = tRT.offsetMax = Vector2.zero;

        tabLabel = textGO.AddComponent<TextMeshProUGUI>();
        tabLabel.enableWordWrapping = false;
        tabLabel.overflowMode       = TextOverflowModes.Overflow;
        tabLabel.fontSize           = tabFontSize;
        tabLabel.color              = new Color(0.80f, 1.00f, 0.98f, 1f); // ??? ??
        tabLabel.fontStyle          = FontStyles.Bold;
        tabLabel.alignment          = TextAlignmentOptions.Center;
        ApplyFont(tabLabel);
        tabLabel.text = "[ 사운드 ]";
    }

    private float ContentTopY => tabHeight; // ????????????

    // ???: ??? ???/??? ???????? ???
    private void BuildSlotHeader(RectTransform parent)
    {
        float slotH = slotFontSize + 8f;

        appearSlotText = BuildSlotRow(parent, "등장: (없음)",
            new Vector2(0f, ContentTopY + slotH),
            new Vector2(panelWidth, slotH), BtnAppearSel);

        selectSlotText = BuildSlotRow(parent, "선택: (없음)",
            new Vector2(0f, ContentTopY),
            new Vector2(panelWidth, slotH), BtnSelectSel);
    }

    private TextMeshProUGUI BuildSlotRow(RectTransform parent, string label,
        Vector2 anchoredPos, Vector2 size, Color bg)
    {
        GameObject go = new GameObject("Slot_" + label);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot     = new Vector2(0f, 0f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.color = bg * new Color(1,1,1, 0.45f);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        RectTransform tRT = textGO.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(6f, 0f); tRT.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.enableWordWrapping = false;
        tmp.overflowMode       = TextOverflowModes.Overflow;
        tmp.fontSize           = slotFontSize;
        tmp.color              = TextColor;
        tmp.alignment          = TextAlignmentOptions.MidlineLeft;
        ApplyFont(tmp);
        tmp.text = label;
        return tmp;
    }

    // ??? ?? ???? ?? 2??
    private void BuildAssignToggle(RectTransform parent)
    {
        float slotH  = slotFontSize + 8f;
        float toggleY = ContentTopY + slotH * 2f;
        float toggleH = buttonHeightPx + 2f;
        float halfW   = panelWidth * 0.5f;

        BuildToggleBtn(parent, "등장 슬롯", new Rect(0f, toggleY, halfW, toggleH),
            AssignMode.Appear, BtnAppearSel);
        BuildToggleBtn(parent, "선택 슬롯", new Rect(halfW, toggleY, halfW, toggleH),
            AssignMode.Select, BtnSelectSel);
    }

    private void BuildToggleBtn(RectTransform parent, string label, Rect rect,
        AssignMode mode, Color activeColor)
    {
        GameObject go = new GameObject("Toggle_" + mode);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot     = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(rect.x, rect.y);
        rt.sizeDelta        = new Vector2(rect.width, rect.height);

        Image img = go.AddComponent<Image>();
        img.color = assignMode == mode ? activeColor : BtnNormal;

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        AssignMode captured = mode;
        Image capturedImg   = img;
        btn.onClick.AddListener(() =>
        {
            assignMode = captured;
            RefreshAssignToggleColors();
        });

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        RectTransform tRT = textGO.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = tRT.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.enableWordWrapping = false;
        tmp.overflowMode       = TextOverflowModes.Overflow;
        tmp.fontSize           = buttonFontSize;
        tmp.color              = TextColor;
        tmp.alignment          = TextAlignmentOptions.Center;
        ApplyFont(tmp);
        tmp.text = label;
    }

    private readonly List<(Image img, AssignMode mode)> toggleImgs = new();

    private void RefreshAssignToggleColors()
    {
        foreach (var (img, mode) in toggleImgs)
        {
            img.color = assignMode == mode
                ? (mode == AssignMode.Appear ? BtnAppearSel : BtnSelectSel)
                : BtnNormal;
        }
    }

    // ??????? ???????
    private void BuildSoundList(RectTransform parent)
    {
        float slotH    = slotFontSize + 8f;
        float toggleH  = buttonHeightPx + 2f;
        float listTop  = ContentTopY + slotH * 2f + toggleH;
        float listH    = panelHeight - listTop;

        // ScrollRect ?????
        GameObject scrollGO = new GameObject("ScrollArea");
        scrollGO.transform.SetParent(parent, false);
        RectTransform scrollRT = scrollGO.AddComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0f, 0f);
        scrollRT.anchorMax = new Vector2(0f, 0f);
        scrollRT.pivot     = new Vector2(0f, 0f);
        scrollRT.anchoredPosition = new Vector2(0f, listTop);
        scrollRT.sizeDelta = new Vector2(panelWidth, listH);
        scrollGO.AddComponent<Image>().color = new Color(0,0,0,0);

        ScrollRect scroll = scrollGO.AddComponent<ScrollRect>();

        // Viewport
        GameObject vpGO = new GameObject("Viewport");
        vpGO.transform.SetParent(scrollGO.transform, false);
        RectTransform vpRT = vpGO.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = vpRT.offsetMax = Vector2.zero;
        Image vpImg = vpGO.AddComponent<Image>();
        vpImg.color = new Color(1,1,1, 0.01f);
        vpGO.AddComponent<Mask>().showMaskGraphic = false;

        // Content
        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(vpGO.transform, false);
        RectTransform contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot     = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = Vector2.zero;

        VerticalLayoutGroup vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 1f;
        vlg.padding = new RectOffset(3, 3, 3, 3);
        vlg.childAlignment    = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = vpRT; scroll.content = contentRT;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.scrollSensitivity = 35f;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        // ??????? & ?? ???
        var grouped = LoadGroupedClips();
        int number = 1; // ??? ??????????
        foreach (var (category, clips) in grouped)
        {
            AddCategoryHeader(contentRT, category);
            foreach (var clip in clips)
                AddSoundButton(contentRT, clip, number++);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);
    }

    // ???? ????????AUDIO ??? ??AudioClip ?????????? ????
    private List<(string category, List<AudioClip> clips)> LoadGroupedClips()
    {
        var result = new List<(string, List<AudioClip>)>();
#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot });
        var dict = new Dictionary<string, List<AudioClip>>(StringComparer.Ordinal);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) continue;

            // AUDIO/ ??? ???? ????? ?????????
            string rel = path.Replace(AudioRoot + "/", "");
            int slash = rel.IndexOf('/');
            string cat = slash >= 0 ? rel.Substring(0, slash) : "Other";

            if (!dict.ContainsKey(cat))
                dict[cat] = new List<AudioClip>();
            dict[cat].Add(clip);
        }

        var sortedKeys = new List<string>(dict.Keys);
        sortedKeys.Sort(StringComparer.Ordinal);
        foreach (var key in sortedKeys)
            result.Add((key, dict[key]));
#endif
        return result;
    }

    private void AddCategoryHeader(Transform parent, string text)
    {
        GameObject go = new GameObject("Header_" + text);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, headerFontSize + 8f);
        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.28f, 1f);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        RectTransform tRT = textGO.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(5f, 0f); tRT.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.enableWordWrapping = false;
        tmp.overflowMode       = TextOverflowModes.Overflow;
        tmp.fontSize           = headerFontSize;
        tmp.color              = HeaderColor;
        tmp.fontStyle          = FontStyles.Bold;
        tmp.alignment          = TextAlignmentOptions.MidlineLeft;
        ApplyFont(tmp);
        tmp.text = "- " + text;
    }

    private void AddSoundButton(Transform parent, AudioClip clip, int number = 0)
    {
        if (clip == null) return;

        GameObject go = new GameObject("Snd_" + clip.name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, buttonHeightPx);

        Image img = go.AddComponent<Image>();
        img.color = BtnNormal;

        Button btn = go.AddComponent<Button>();
        ColorBlock cb     = btn.colors;
        cb.normalColor    = Color.white;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        cb.pressedColor   = new Color(0.7f, 0.7f, 0.7f, 1f);
        btn.colors = cb;
        btn.targetGraphic = img;

        AudioClip capturedClip = clip;
        Image     capturedImg  = img;
        btn.onClick.AddListener(() => OnClickSound(capturedClip, capturedImg));

        clipBtnImages[clip.name] = img;

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        RectTransform tRT = textGO.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(8f, 0f); tRT.offsetMax = new Vector2(-3f, 0f);

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.enableWordWrapping = false;
        tmp.overflowMode       = TextOverflowModes.Overflow;
        tmp.fontSize           = buttonFontSize;
        tmp.color              = TextColor;
        tmp.alignment          = TextAlignmentOptions.MidlineLeft;
        ApplyFont(tmp);
        string prefix = number > 0 ? $"{number,3}. " : "";
        string displayName = clip.name.Replace('_', ' ');
        tmp.text = $"{prefix}> {displayName}";
    }

    // ??????????????????????????????????????????????????????????????????????????????????
    //  ?? ??? ??
    // ??????????????????????????????????????????????????????????????????????????????????

    private void OnClickSound(AudioClip clip, Image btnImg)
    {
        if (soundManager == null)
        {
            soundManager = FindFirstObjectByType<CardSoundManager>();
            if (soundManager == null) { Debug.LogWarning("[CardSoundSelector] CardSoundManager ???"); return; }
        }
        // ?????
        soundManager.PreviewClip(clip);

        // ??? ??? ?? ??? ??
        if (lastPlayedClip != null &&
            clipBtnImages.TryGetValue(lastPlayedClip.name, out Image prev))
        {
            prev.color = IsAssigned(lastPlayedClip) ? AssignedColor(lastPlayedClip) : BtnNormal;
        }

        lastPlayedClip = clip;
        btnImg.color   = BtnPlaying;

        // 슬롯에 클립 할당 + PlayerPrefs 에 이름 저장 (세션 간 유지)
        if (assignMode == AssignMode.Appear)
        {
            soundManager.CardAppearClip = clip;
            PlayerPrefs.SetString(PrefKeyAppear, clip.name);
        }
        else
        {
            soundManager.CardSelectClip = clip;
            PlayerPrefs.SetString(PrefKeySelect, clip.name);
        }
        PlayerPrefs.Save();

        RefreshSlotLabels();
        RefreshBtnHighlights();
    }

    // PlayerPrefs 에 저장된 클립 이름으로 CardSoundManager 슬롯 복원
    private void RestoreSavedClips()
    {
        if (soundManager == null) return;

        string appearName = PlayerPrefs.GetString(PrefKeyAppear, "");
        string selectName = PlayerPrefs.GetString(PrefKeySelect, "");

        if (string.IsNullOrEmpty(appearName) && string.IsNullOrEmpty(selectName)) return;

#if UNITY_EDITOR
        // LoadGroupedClips() 는 에디터 전용(AssetDatabase) 이므로 에디터에서만 복원
        var allClips = new List<AudioClip>();
        foreach (var (_, clips) in LoadGroupedClips())
            allClips.AddRange(clips);

        if (!string.IsNullOrEmpty(appearName))
        {
            AudioClip found = allClips.Find(c => c.name == appearName);
            if (found != null) soundManager.CardAppearClip = found;
        }
        if (!string.IsNullOrEmpty(selectName))
        {
            AudioClip found = allClips.Find(c => c.name == selectName);
            if (found != null) soundManager.CardSelectClip = found;
        }

        RefreshSlotLabels();
        RefreshBtnHighlights();
#endif
    }

    private bool IsAssigned(AudioClip clip) =>
        clip == soundManager.CardAppearClip || clip == soundManager.CardSelectClip;

    private Color AssignedColor(AudioClip clip)
    {
        if (clip == soundManager.CardAppearClip) return BtnAppearSel;
        if (clip == soundManager.CardSelectClip) return BtnSelectSel;
        return BtnNormal;
    }

    private void RefreshSlotLabels()
    {
        if (appearSlotText != null)
        {
            string name = soundManager.CardAppearClip != null
                ? soundManager.CardAppearClip.name.Replace('_', ' ') : "(없음)";
            appearSlotText.text = "등장: " + name;
        }
        if (selectSlotText != null)
        {
            string name = soundManager.CardSelectClip != null
                ? soundManager.CardSelectClip.name.Replace('_', ' ') : "(없음)";
            selectSlotText.text = "선택: " + name;
        }
    }

    private void RefreshBtnHighlights()
    {
        foreach (var kv in clipBtnImages)
        {
            if (soundManager.CardAppearClip != null && soundManager.CardAppearClip.name == kv.Key)
                kv.Value.color = BtnAppearSel;
            else if (soundManager.CardSelectClip != null && soundManager.CardSelectClip.name == kv.Key)
                kv.Value.color = BtnSelectSel;
            else
                kv.Value.color = BtnNormal;
        }
    }

    // ??????????????????????????????????????????????????????????????????????????????????
    //  ?????? ???????
    // ??????????????????????????????????????????????????????????????????????????????????

    private void TogglePanel() { if (isExpanded) Collapse(); else Expand(); }

    private void Expand()
    {
        if (isExpanded) return;
        isExpanded = true; leaveTimer = 0f;
        slideTween?.Kill();
        slideTween = panelRT
            .DOSizeDelta(new Vector2(panelWidth, tabHeight + panelHeight), slideTime)
            .SetUpdate(true).SetEase(Ease.OutCubic);
        if (tabLabel != null) tabLabel.text = "^ 사운드 ^";
    }

    private void Collapse()
    {
        if (!isExpanded) return;
        isExpanded = false; leaveTimer = 0f;
        slideTween?.Kill();
        slideTween = panelRT
            .DOSizeDelta(new Vector2(panelWidth, tabHeight), slideTime)
            .SetUpdate(true).SetEase(Ease.InCubic);
        if (tabLabel != null) tabLabel.text = "[ 사운드 ]";
    }
}
