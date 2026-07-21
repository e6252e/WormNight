using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TeamProject01.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class SegmentDpsDebugPanel : MonoBehaviour
    {
        private enum MetricKind
        {
            Total,
            Wave
        }

        private sealed class RowView
        {
            public RectTransform Root;
            public TextMeshProUGUI IndexText;
            public TextMeshProUGUI NameText;
            public RectTransform BarRoot;
            public RectTransform FillRect;
            public TextMeshProUGUI DamageText;
            public TextMeshProUGUI DpsText;

            public void SetData(int index, string segmentName, float damage, float dps, float fillRatio, Color fillColor)
            {
                if (Root != null)
                {
                    Root.gameObject.SetActive(true);
                }

                SetText(IndexText, index.ToString("00"));
                SetText(NameText, segmentName);
                SetText(DamageText, FormatDamage(damage));
                SetText(DpsText, $"{FormatDamage(dps)}/s");

                if (FillRect != null)
                {
                    FillRect.anchorMax = new Vector2(Mathf.Clamp01(fillRatio), 1f);
                    Image fillImage = FillRect.GetComponent<Image>();
                    if (fillImage != null)
                    {
                        fillImage.color = fillColor;
                    }
                }
            }

            public void Hide()
            {
                if (Root != null)
                {
                    Root.gameObject.SetActive(false);
                }
            }
        }

        private sealed class PanelView
        {
            public RectTransform Root;
            public RectTransform Title;
            public RectTransform Header;
            public RectTransform Viewport;
            public RectTransform Content;
            public TextMeshProUGUI TitleText;
            public TextMeshProUGUI HeaderText;
        }

        [Header("참조")]
        [SerializeField] private ConvoyController convoyController; // 세그먼트 순서 제공

        [Header("표시")]
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.2f; // UI 갱신 간격
        [SerializeField] private bool showLevelInName = true; // 이름 뒤 Lv 표시
        [SerializeField] private bool syncScrollPosition = true; // TOTAL/WAVE 스크롤 동기화
        [SerializeField] private bool autoFitToCurrentRect = true; // 현재 패널 크기에 내부 내용 맞춤
        [SerializeField] private bool onlyShowInAutoOrbit = true; // 자동궤도 중에만 표시
        [SerializeField] private TMP_FontAsset debugFont; // DPS 미터 전용 폰트

        private readonly List<AttachedSegmentDebugEntry> segmentEntries = new List<AttachedSegmentDebugEntry>(64); // 현재 세그먼트 순서
        private readonly List<RowView> totalRows = new List<RowView>(64); // 누적 패널 행
        private readonly List<RowView> waveRows = new List<RowView>(64); // 웨이브 패널 행

        private RectTransform rootRect; // 루트 Rect
        private readonly PanelView totalPanelView = new PanelView(); // TOTAL 패널 참조
        private readonly PanelView wavePanelView = new PanelView(); // WAVE 패널 참조
        private RectTransform totalContent; // TOTAL 행 부모
        private RectTransform waveContent; // WAVE 행 부모
        private ScrollRect totalScrollRect; // TOTAL 스크롤
        private ScrollRect waveScrollRect; // WAVE 스크롤
        private CanvasGroup visibilityGroup; // 자동궤도 표시 제어
        private float nextRefreshTime; // 다음 갱신 시간
        private bool scrollSyncWired; // 스크롤 이벤트 연결 여부
        private bool isSyncingScroll; // 재귀 동기화 방지
        private bool warnedMissingLayout; // 레이아웃 누락 경고 반복 방지
        private bool lastVisibleState = true; // 직전 표시 상태

        private const string GwangyangFontAssetPath = "Assets/UI/Fonts/Title/GwangyangSunshine_Bold SDF.asset";
        private static readonly Vector2 DefaultGeneratedRootSize = new Vector2(520f, 560f);
        private static readonly Color TotalFillColor = new Color(0.95f, 0.72f, 0.28f, 0.95f);
        private static readonly Color WaveFillColor = new Color(0.32f, 0.78f, 1f, 0.95f);
        private static readonly Color BackgroundColor = new Color(0.015f, 0.018f, 0.024f, 0.82f);
        private static readonly Color PanelColor = new Color(0.025f, 0.032f, 0.042f, 0.86f);
        private static readonly Color BarBackgroundColor = new Color(0.1f, 0.12f, 0.15f, 0.95f);
        private static readonly Color TextColor = new Color(0.92f, 0.96f, 1f, 1f);
        private static readonly Color MutedTextColor = new Color(0.64f, 0.72f, 0.8f, 1f);

        private void OnEnable()
        {
            ResolveReferences();
            bool visible = ShouldShowPanel();
            ApplyPanelVisibility(visible);
            if (!visible)
            {
                return; // 자동궤도 전에는 표시/갱신 보류
            }

            EnsureLayout();
            ApplyResponsiveLayout(); // 사용자 크기에 맞게 내부만 보정
            WireScrollSync();
            Refresh(true);
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return; // 에디터 대기 중 갱신 방지
            }

            bool visible = ShouldShowPanel();
            ApplyPanelVisibility(visible);
            if (!visible)
            {
                return; // 자동궤도 밖에서는 미터 숨김
            }

            if (Time.unscaledTime < nextRefreshTime)
            {
                return; // 갱신 간격 유지
            }

            Refresh(false);
        }

        private void OnDisable()
        {
            UnwireScrollSync();
        }

        [ContextMenu("Rebuild Layout")]
        public void RebuildLayout()
        {
            EnsureRootRect();
            ApplyGeneratedRootSizeIfEmpty(); // 빈 오브젝트에서만 기본 크기 사용
            ClearChildren(transform);
            totalRows.Clear();
            waveRows.Clear();
            totalScrollRect = null;
            waveScrollRect = null;
            totalContent = null;
            waveContent = null;
            BuildStaticLayout();
            WireScrollSync();
        }

        public void EnsureLayout()
        {
            EnsureRootRect();
            CacheLayoutReferences();
            if (totalContent == null || waveContent == null)
            {
                WarnMissingLayoutOnce(); // 런타임에서 사용자 배치를 갈아엎지 않음
            }
        }

        private void Refresh(bool force)
        {
            if (!ShouldShowPanel())
            {
                ApplyPanelVisibility(false);
                return; // 숨김 상태에서는 행 갱신 생략
            }

            ResolveReferences();
            EnsureLayout();
            if (totalContent == null || waveContent == null)
            {
                return; // 정식 배치가 없으면 자동 재생성하지 않음
            }

            WireScrollSync(); // 숨김 상태로 시작한 뒤 켜질 때도 보장
            ApplyResponsiveLayout(); // 현재 RectTransform 크기 기준 재배치
            if (!force)
            {
                nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
            }

            segmentEntries.Clear();
            if (convoyController != null)
            {
                convoyController.CollectAttachedSegmentDebugEntries(segmentEntries); // 현재 연결 순서
            }

            EnsureRows(totalRows, totalContent, segmentEntries.Count, MetricKind.Total);
            EnsureRows(waveRows, waveContent, segmentEntries.Count, MetricKind.Wave);
            ApplyResponsiveLayout(); // 새 행까지 크기/폰트 적용

            float maxTotalDamage = CalculateMaxDamage(MetricKind.Total);
            float maxWaveDamage = CalculateMaxDamage(MetricKind.Wave);
            RefreshRows(totalRows, MetricKind.Total, maxTotalDamage, TotalFillColor);
            RefreshRows(waveRows, MetricKind.Wave, maxWaveDamage, WaveFillColor);
        }

        private bool ShouldShowPanel()
        {
            if (!onlyShowInAutoOrbit)
            {
                return true; // 항상 표시 모드
            }

            ResolveReferences();
            return convoyController != null && convoyController.IsAutoOrbitActive; // 자동궤도 표시
        }

        private void ApplyPanelVisibility(bool visible)
        {
            EnsureVisibilityGroup();
            if (visibilityGroup == null)
            {
                return; // 제어 불가
            }

            visibilityGroup.alpha = visible ? 1f : 0f;
            visibilityGroup.interactable = visible;
            visibilityGroup.blocksRaycasts = visible;
            if (visible && !lastVisibleState)
            {
                nextRefreshTime = 0f; // 다시 켜질 때 즉시 갱신
            }

            lastVisibleState = visible;
        }

        private void EnsureVisibilityGroup()
        {
            if (visibilityGroup != null)
            {
                return; // 이미 있음
            }

            visibilityGroup = GetComponent<CanvasGroup>();
            if (visibilityGroup == null)
            {
                visibilityGroup = gameObject.AddComponent<CanvasGroup>(); // 루트 활성은 유지
            }
        }

        private void RefreshRows(List<RowView> rows, MetricKind metricKind, float maxDamage, Color fillColor)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                RowView row = rows[i];
                if (i >= segmentEntries.Count)
                {
                    row.Hide();
                    continue;
                }

                AttachedSegmentDebugEntry entry = segmentEntries[i];
                SegmentDpsDebugMeter.TryGetSnapshot(entry.DamageMeterKey, out SegmentDpsDebugSnapshot snapshot);
                float damage = metricKind == MetricKind.Total ? snapshot.TotalDamage : snapshot.WaveDamage;
                float dps = metricKind == MetricKind.Total ? snapshot.TotalDps : snapshot.WaveDps;
                float fillRatio = maxDamage > 0f ? damage / maxDamage : 0f;
                row.SetData(entry.DisplayIndex, GetDisplayName(entry), damage, dps, fillRatio, fillColor);
            }
        }

        private float CalculateMaxDamage(MetricKind metricKind)
        {
            float maxDamage = 0f;
            for (int i = 0; i < segmentEntries.Count; i++)
            {
                AttachedSegmentDebugEntry entry = segmentEntries[i];
                SegmentDpsDebugMeter.TryGetSnapshot(entry.DamageMeterKey, out SegmentDpsDebugSnapshot snapshot);
                float damage = metricKind == MetricKind.Total ? snapshot.TotalDamage : snapshot.WaveDamage;
                maxDamage = Mathf.Max(maxDamage, damage);
            }

            return maxDamage;
        }

        private string GetDisplayName(AttachedSegmentDebugEntry entry)
        {
            string displayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? "Segment" : entry.DisplayName.Trim();
            return showLevelInName && entry.SegmentLevel > 1 ? $"{displayName} Lv{entry.SegmentLevel}" : displayName;
        }

        private void ResolveReferences()
        {
            if (convoyController != null)
            {
                return; // 직접 연결 우선
            }

            if (CoreStatProvider.Active != null && CoreStatProvider.Active.Convoy != null)
            {
                convoyController = CoreStatProvider.Active.Convoy; // 코어 연결
                return;
            }

            convoyController = FindFirstObjectByType<ConvoyController>(); // 씬 fallback
        }

        private void EnsureRootRect()
        {
            rootRect = transform as RectTransform;
            if (rootRect == null)
            {
                rootRect = gameObject.AddComponent<RectTransform>();
            }
        }

        private void ApplyGeneratedRootSizeIfEmpty()
        {
            if (rootRect == null)
            {
                return; // RectTransform 없음
            }

            bool hasVisibleSize = Mathf.Abs(rootRect.rect.width) > 1f || Mathf.Abs(rootRect.rect.height) > 1f;
            bool hasManualSize = rootRect.sizeDelta.sqrMagnitude > 1f;
            if (!hasVisibleSize && !hasManualSize)
            {
                rootRect.sizeDelta = DefaultGeneratedRootSize; // 최초 생성 fallback
            }
        }

        private void WarnMissingLayoutOnce()
        {
            if (warnedMissingLayout)
            {
                return; // 반복 방지
            }

            warnedMissingLayout = true;
            Debug.LogWarning("[SegmentDpsDebugPanel] TOTAL/WAVE Content를 찾지 못했습니다. 사용자 배치를 보호하기 위해 런타임 자동 재생성은 하지 않습니다.", this);
        }

        private void BuildStaticLayout()
        {
            Image background = gameObject.GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
            }

            background.color = BackgroundColor;
            background.raycastTarget = false;

            RectTransform totalPanel = CreateMetricPanel(rootRect, "TotalPanel", "TOTAL DPS", MetricKind.Total, -6f);
            RectTransform wavePanel = CreateMetricPanel(rootRect, "WavePanel", "WAVE DPS", MetricKind.Wave, -284f);

            totalContent = FindChildRect(totalPanel, "Content");
            waveContent = FindChildRect(wavePanel, "Content");
            totalScrollRect = totalPanel.GetComponentInChildren<ScrollRect>(true);
            waveScrollRect = wavePanel.GetComponentInChildren<ScrollRect>(true);
            CacheLayoutReferences();
            ApplyResponsiveLayout();
        }

        private RectTransform CreateMetricPanel(RectTransform parent, string objectName, string title, MetricKind metricKind, float anchoredY)
        {
            RectTransform panel = CreateRect(parent, objectName, new Vector2(500f, 270f));
            panel.anchorMin = new Vector2(0.5f, 1f);
            panel.anchorMax = new Vector2(0.5f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = new Vector2(0f, anchoredY);

            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = PanelColor;
            panelImage.raycastTarget = false;

            TextMeshProUGUI titleText = CreateText(panel, "Title", new Vector2(470f, 24f), 15f, TextAlignmentOptions.MidlineLeft);
            titleText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            titleText.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            titleText.text = title;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = metricKind == MetricKind.Total ? TotalFillColor : WaveFillColor;

            TextMeshProUGUI headerText = CreateText(panel, "Header", new Vector2(470f, 18f), 10.5f, TextAlignmentOptions.MidlineLeft);
            headerText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            headerText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            headerText.rectTransform.pivot = new Vector2(0.5f, 1f);
            headerText.rectTransform.anchoredPosition = new Vector2(0f, -35f);
            headerText.text = "NO   SEGMENT                         DAMAGE                         DPS";
            headerText.color = MutedTextColor;

            RectTransform viewport = CreateRect(panel, "Viewport", new Vector2(470f, 210f));
            viewport.anchorMin = new Vector2(0.5f, 1f);
            viewport.anchorMax = new Vector2(0.5f, 1f);
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.anchoredPosition = new Vector2(0f, -56f);

            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.12f);
            viewportImage.raycastTarget = false;
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateRect(viewport, "Content", new Vector2(452f, 210f));
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.offsetMin = new Vector2(0f, content.offsetMin.y);
            content.offsetMax = new Vector2(0f, content.offsetMax.y);

            VerticalLayoutGroup layoutGroup = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.spacing = 3f;
            layoutGroup.padding = new RectOffset(0, 0, 2, 2);

            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scrollRect = panel.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 26f;
            return panel;
        }

        private void CacheLayoutReferences()
        {
            RectTransform totalPanel = FindChildRect(rootRect, "TotalPanel");
            RectTransform wavePanel = FindChildRect(rootRect, "WavePanel");
            CachePanelView(totalPanelView, totalPanel);
            CachePanelView(wavePanelView, wavePanel);
            totalContent = totalPanel != null ? FindChildRect(totalPanel, "Content") : null;
            waveContent = wavePanel != null ? FindChildRect(wavePanel, "Content") : null;
            totalScrollRect = totalPanel != null ? totalPanel.GetComponent<ScrollRect>() : null;
            waveScrollRect = wavePanel != null ? wavePanel.GetComponent<ScrollRect>() : null;
        }

        private static void CachePanelView(PanelView view, RectTransform panelRoot)
        {
            if (view == null)
            {
                return;
            }

            view.Root = panelRoot;
            view.Title = panelRoot != null ? FindChildRect(panelRoot, "Title") : null;
            view.Header = panelRoot != null ? FindChildRect(panelRoot, "Header") : null;
            view.Viewport = panelRoot != null ? FindChildRect(panelRoot, "Viewport") : null;
            view.Content = panelRoot != null ? FindChildRect(panelRoot, "Content") : null;
            view.TitleText = view.Title != null ? view.Title.GetComponent<TextMeshProUGUI>() : null;
            view.HeaderText = view.Header != null ? view.Header.GetComponent<TextMeshProUGUI>() : null;
        }

        private void ApplyResponsiveLayout()
        {
            if (!autoFitToCurrentRect || rootRect == null)
            {
                ApplyFontToChildren();
                return; // 자동 맞춤 비활성
            }

            ApplyPanelLayout(totalPanelView);
            ApplyPanelLayout(wavePanelView);

            float rowWidth = ResolveContentWidth(totalPanelView, wavePanelView);
            float scale = Mathf.Clamp(rowWidth / 452f, 0.55f, 1.05f);
            float rowHeight = Mathf.Clamp(24f * scale, 14f, 24f);
            float rowFontSize = Mathf.Clamp(11.5f * scale, 7.5f, 12f);
            float damageFontSize = Mathf.Clamp(11f * scale, 7.5f, 11.5f);
            float spacing = Mathf.Clamp(5f * scale, 2f, 5f);

            ApplyRowsLayout(totalRows, rowWidth, rowHeight, rowFontSize, damageFontSize, spacing);
            ApplyRowsLayout(waveRows, rowWidth, rowHeight, rowFontSize, damageFontSize, spacing);
            ApplyFontToChildren();
        }

        private void ApplyPanelLayout(PanelView view)
        {
            if (view == null || view.Root == null)
            {
                return;
            }

            float width = Mathf.Max(120f, view.Root.rect.width);
            float height = Mathf.Max(80f, view.Root.rect.height);
            float scale = Mathf.Clamp(width / 500f, 0.55f, 1.05f);
            float inset = Mathf.Clamp(8f * scale, 3f, 8f);
            float titleHeight = Mathf.Clamp(24f * scale, 14f, 24f);
            float headerHeight = Mathf.Clamp(18f * scale, 10f, 18f);
            float headerGap = Mathf.Clamp(2f * scale, 1f, 4f);
            float viewportTop = inset + titleHeight + headerHeight + headerGap * 2f;

            if (view.Title != null)
            {
                StretchTop(view.Title, inset, inset, inset, titleHeight);
                ApplyTextSizing(view.TitleText, Mathf.Clamp(15f * scale, 9f, 15f), Mathf.Clamp(7f * scale, 6f, 10f), TextOverflowModes.Ellipsis);
            }

            if (view.Header != null)
            {
                StretchTop(view.Header, inset, inset, inset + titleHeight + headerGap, headerHeight);
                ApplyTextSizing(view.HeaderText, Mathf.Clamp(10.5f * scale, 6.5f, 10.5f), 5.5f, TextOverflowModes.Ellipsis);
                if (view.HeaderText != null)
                {
                    view.HeaderText.text = width < 330f ? "NO  SEGMENT        DMG       DPS" : "NO   SEGMENT                         DAMAGE                         DPS";
                }
            }

            if (view.Viewport != null)
            {
                view.Viewport.anchorMin = Vector2.zero;
                view.Viewport.anchorMax = Vector2.one;
                view.Viewport.pivot = new Vector2(0.5f, 0.5f);
                view.Viewport.offsetMin = new Vector2(inset, inset);
                view.Viewport.offsetMax = new Vector2(-inset, -Mathf.Min(height - inset, viewportTop));
            }

            if (view.Content != null)
            {
                view.Content.anchorMin = new Vector2(0f, 1f);
                view.Content.anchorMax = new Vector2(1f, 1f);
                view.Content.pivot = new Vector2(0.5f, 1f);
                view.Content.anchoredPosition = Vector2.zero;
                view.Content.offsetMin = new Vector2(0f, view.Content.offsetMin.y);
                view.Content.offsetMax = new Vector2(0f, view.Content.offsetMax.y);
            }
        }

        private static void StretchTop(RectTransform rect, float left, float right, float top, float height)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static float ResolveContentWidth(PanelView first, PanelView second)
        {
            float firstWidth = ResolvePanelContentWidth(first);
            float secondWidth = ResolvePanelContentWidth(second);
            if (firstWidth > 1f && secondWidth > 1f)
            {
                return Mathf.Min(firstWidth, secondWidth); // 더 좁은 패널 기준으로 행 폭 고정
            }

            float resolved = Mathf.Max(firstWidth, secondWidth);
            return resolved > 1f ? resolved : 452f;
        }

        private static float ResolvePanelContentWidth(PanelView view)
        {
            if (view == null)
            {
                return 0f;
            }

            if (view.Root != null && view.Root.rect.width > 1f)
            {
                float width = Mathf.Max(120f, view.Root.rect.width);
                float scale = Mathf.Clamp(width / 500f, 0.55f, 1.05f);
                float inset = Mathf.Clamp(8f * scale, 3f, 8f);
                return Mathf.Max(40f, width - inset * 2f);
            }

            return view.Viewport != null ? view.Viewport.rect.width : 0f;
        }

        private void ApplyRowsLayout(List<RowView> rows, float rowWidth, float rowHeight, float rowFontSize, float damageFontSize, float spacing)
        {
            if (rows == null)
            {
                return;
            }

            float safeWidth = Mathf.Max(120f, rowWidth);
            float horizontalPadding = Mathf.Clamp(safeWidth * 0.015f, 2f, 4f);
            float availableWidth = Mathf.Max(40f, safeWidth - horizontalPadding * 2f - spacing * 3f);
            float indexWidth = Mathf.Clamp(safeWidth * 0.1f, 18f, 30f);
            float dpsWidth = Mathf.Clamp(safeWidth * 0.18f, 42f, 72f);
            float barWidth = Mathf.Clamp(safeWidth * 0.35f, 58f, 198f);
            float nameWidth = Mathf.Max(36f, availableWidth - indexWidth - dpsWidth - barWidth);

            for (int i = 0; i < rows.Count; i++)
            {
                RowView row = rows[i];
                if (row == null || row.Root == null)
                {
                    continue;
                }

                LayoutElement rootLayout = row.Root.GetComponent<LayoutElement>();
                if (rootLayout != null)
                {
                    rootLayout.minWidth = safeWidth;
                    rootLayout.preferredWidth = safeWidth;
                    rootLayout.minHeight = rowHeight;
                    rootLayout.preferredHeight = rowHeight;
                }

                HorizontalLayoutGroup layout = row.Root.GetComponent<HorizontalLayoutGroup>();
                if (layout != null)
                {
                    int padding = Mathf.RoundToInt(horizontalPadding);
                    layout.padding = new RectOffset(padding, padding, 0, 0);
                    layout.spacing = spacing;
                }

                ApplyWidth(row.IndexText, indexWidth);
                ApplyWidth(row.NameText, nameWidth);
                ApplyWidth(row.BarRoot, barWidth);
                ApplyWidth(row.DpsText, dpsWidth);

                if (row.BarRoot != null)
                {
                    row.BarRoot.sizeDelta = new Vector2(barWidth, Mathf.Max(10f, rowHeight - 4f));
                }

                ApplyTextSizing(row.IndexText, rowFontSize, 6f, TextOverflowModes.Ellipsis);
                ApplyTextSizing(row.NameText, rowFontSize, 6f, TextOverflowModes.Ellipsis);
                ApplyTextSizing(row.DamageText, damageFontSize, 6f, TextOverflowModes.Ellipsis);
                ApplyTextSizing(row.DpsText, rowFontSize, 6f, TextOverflowModes.Ellipsis);
            }
        }

        private static void ApplyWidth(Component component, float width)
        {
            if (component == null)
            {
                return;
            }

            RectTransform rect = component.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
            }

            LayoutElement layoutElement = component.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.minWidth = width;
                layoutElement.preferredWidth = width;
            }
        }

        private static void ApplyTextSizing(TextMeshProUGUI text, float maxSize, float minSize, TextOverflowModes overflowMode)
        {
            if (text == null)
            {
                return;
            }

            text.enableAutoSizing = true;
            text.fontSizeMax = Mathf.Max(minSize, maxSize);
            text.fontSizeMin = Mathf.Min(minSize, text.fontSizeMax);
            text.fontSize = text.fontSizeMax;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = overflowMode;
        }

        private void ApplyFontToChildren()
        {
            TMP_FontAsset font = ResolveDebugFont();
            if (font == null)
            {
                return;
            }

            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].font != font)
                {
                    texts[i].font = font; // 광양선샤인 폰트 적용
                }
            }
        }

        private TMP_FontAsset ResolveDebugFont()
        {
            if (debugFont != null)
            {
                return debugFont;
            }

#if UNITY_EDITOR
            debugFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GwangyangFontAssetPath);
            if (debugFont != null)
            {
                return debugFont;
            }
#endif

            return TMP_Settings.defaultFontAsset;
        }

        private void EnsureRows(List<RowView> rows, RectTransform content, int count, MetricKind metricKind)
        {
            if (content == null)
            {
                return; // 레이아웃 없음
            }

            while (rows.Count < count)
            {
                rows.Add(CreateRow(content, $"{metricKind}Row_{rows.Count + 1:00}"));
            }
        }

        private static RowView CreateRow(RectTransform parent, string objectName)
        {
            RectTransform rowRoot = CreateRect(parent, objectName, new Vector2(452f, 24f));
            LayoutElement layoutElement = rowRoot.gameObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = 24f;
            layoutElement.preferredHeight = 24f;

            HorizontalLayoutGroup rowLayout = rowRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.spacing = 5f;
            rowLayout.padding = new RectOffset(4, 4, 1, 1);

            TextMeshProUGUI indexText = CreateRowText(rowRoot, "Index", 30f, 11.5f, TextAlignmentOptions.MidlineRight, MutedTextColor);
            TextMeshProUGUI nameText = CreateRowText(rowRoot, "Name", 128f, 11.5f, TextAlignmentOptions.MidlineLeft, TextColor);

            RectTransform barRoot = CreateRect(rowRoot, "DamageBar", new Vector2(198f, 18f));
            LayoutElement barLayout = barRoot.gameObject.AddComponent<LayoutElement>();
            barLayout.minWidth = 198f;
            barLayout.preferredWidth = 198f;
            Image barBackground = barRoot.gameObject.AddComponent<Image>();
            barBackground.color = BarBackgroundColor;
            barBackground.raycastTarget = false;

            RectTransform fill = CreateRect(barRoot, "Fill", Vector2.zero);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.raycastTarget = false;

            TextMeshProUGUI damageText = CreateText(barRoot, "DamageText", new Vector2(190f, 18f), 11f, TextAlignmentOptions.Center);
            damageText.rectTransform.anchorMin = Vector2.zero;
            damageText.rectTransform.anchorMax = Vector2.one;
            damageText.rectTransform.offsetMin = Vector2.zero;
            damageText.rectTransform.offsetMax = Vector2.zero;
            damageText.fontStyle = FontStyles.Bold;
            damageText.color = Color.white;

            TextMeshProUGUI dpsText = CreateRowText(rowRoot, "Dps", 72f, 11.5f, TextAlignmentOptions.MidlineRight, TextColor);

            return new RowView
            {
                Root = rowRoot,
                IndexText = indexText,
                NameText = nameText,
                BarRoot = barRoot,
                FillRect = fill,
                DamageText = damageText,
                DpsText = dpsText
            };
        }

        private static TextMeshProUGUI CreateRowText(RectTransform parent, string objectName, float width, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            TextMeshProUGUI text = CreateText(parent, objectName, new Vector2(width, 20f), fontSize, alignment);
            LayoutElement layoutElement = text.gameObject.AddComponent<LayoutElement>();
            layoutElement.minWidth = width;
            layoutElement.preferredWidth = width;
            text.color = color;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static TextMeshProUGUI CreateText(RectTransform parent, string objectName, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(parent, objectName, size);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.color = TextColor;
            return text;
        }

        private static RectTransform CreateRect(Transform parent, string objectName, Vector2 size)
        {
            GameObject child = new GameObject(objectName);
            child.transform.SetParent(parent, false);

            RectTransform rect = child.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return rect;
        }

        private static RectTransform FindChildRect(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            Transform direct = parent.Find(childName);
            if (direct != null)
            {
                return direct as RectTransform;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                RectTransform found = FindChildRect(parent.GetChild(i), childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                {
                    Object.Destroy(child.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private void WireScrollSync()
        {
            if (scrollSyncWired || !syncScrollPosition || totalScrollRect == null || waveScrollRect == null)
            {
                return; // 연결 불필요
            }

            totalScrollRect.onValueChanged.AddListener(OnTotalScrollChanged);
            waveScrollRect.onValueChanged.AddListener(OnWaveScrollChanged);
            scrollSyncWired = true;
        }

        private void UnwireScrollSync()
        {
            if (!scrollSyncWired)
            {
                return;
            }

            if (totalScrollRect != null)
            {
                totalScrollRect.onValueChanged.RemoveListener(OnTotalScrollChanged);
            }

            if (waveScrollRect != null)
            {
                waveScrollRect.onValueChanged.RemoveListener(OnWaveScrollChanged);
            }

            scrollSyncWired = false;
        }

        private void OnTotalScrollChanged(Vector2 value)
        {
            SyncScroll(totalScrollRect, waveScrollRect, value);
        }

        private void OnWaveScrollChanged(Vector2 value)
        {
            SyncScroll(waveScrollRect, totalScrollRect, value);
        }

        private void SyncScroll(ScrollRect source, ScrollRect target, Vector2 value)
        {
            if (isSyncingScroll || source == null || target == null)
            {
                return; // 재귀/대상 없음
            }

            isSyncingScroll = true;
            target.verticalNormalizedPosition = value.y;
            isSyncingScroll = false;
        }

        private static string FormatDamage(float value)
        {
            float safeValue = Mathf.Max(0f, value);
            if (safeValue >= 1000000f)
            {
                return $"{safeValue / 1000000f:0.0}M";
            }

            if (safeValue >= 10000f)
            {
                return $"{safeValue / 1000f:0.0}K";
            }

            if (safeValue >= 1000f)
            {
                return $"{safeValue:0}";
            }

            return safeValue.ToString("0.#");
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
