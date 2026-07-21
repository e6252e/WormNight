using UnityEngine;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyHpBarUI : MonoBehaviour // monster overhead HP bar
    {
        private const int RoundedSpriteSize = 32;
        private const float RoundedSpriteRadius = 8.0f;
        private const float RoundedSpritePixelsPerUnit = 100.0f;

        private static Sprite roundedSprite;

        [Header("Target")]
        [SerializeField] private EnemyHealth health; // HP source

        [Header("UI")]
        [SerializeField] private RectTransform barRoot; // visible bar root
        [SerializeField] private Image backgroundImage; // black rounded background
        [SerializeField] private Image fillImage; // red HP fill
        [SerializeField] private RectTransform segmentLineRoot; // elite segment line root
        [SerializeField] private Image segmentLinePrefab; // elite segment line template

        [Header("Visibility")]
        [SerializeField] private bool hideUntilDamaged = true; // hide by default

        [Min(0.1f)]
        [SerializeField] private float visibleSeconds = 1.35f; // show duration after damage

        [Header("Simple Style")]
        [SerializeField] private bool useRoundedStyle = true; // use rounded 9-sliced sprite
        [SerializeField] private Color backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.95f); // bar back color
        [SerializeField] private Color fillColor = new Color(0.9f, 0.0f, 0.0f, 1.0f); // HP fill color
        [SerializeField] private Color segmentLineColor = new Color(0.0f, 0.0f, 0.0f, 1.0f); // elite split color

        [Header("Size Setting")]
        [Min(1.0f)]
        [SerializeField] private float fixedFillWidth = 40.0f; // base inner fill width

        [Min(0.1f)]
        [SerializeField] private float horizontalSizeMultiplier = 1.0f; // final horizontal scale

        [Min(1.0f)]
        [SerializeField] private float barHeight = 6.0f; // full bar height

        [Header("Segment Setting")]
        [SerializeField] private bool showSegments; // elite only

        [Min(1.0f)]
        [SerializeField] private float hpPerSegment = 10.0f; // HP per elite split

        [Min(1)]
        [SerializeField] private int maxVisibleSegmentCount = 10; // maximum displayed chunks

        [Header("Frame Padding")]
        [Min(0.0f)]
        [SerializeField] private float fillHorizontalPadding = 0.0f; // full HP fill matches background width

        [Min(0.0f)]
        [SerializeField] private float fillVerticalPadding = 0.0f; // full HP fill matches background height

        private EnemyHealth subscribedHealth;
        private RectTransform fillRect;
        private Image cachedBackgroundImage;
        private CanvasGroup barCanvasGroup;
        private float hideTime;
        private bool barVisible;
        private float cachedMaxHp = -1.0f;
        private float cachedFixedFillWidth = -1.0f;
        private float cachedHorizontalSizeMultiplier = -1.0f;
        private float cachedBarHeight = -1.0f;
        private float cachedHpPerSegment = -1.0f;
        private int cachedMaxVisibleSegmentCount = -1;
        private float cachedFillHorizontalPadding = -1.0f;
        private float cachedFillVerticalPadding = -1.0f;
        private bool cachedShowSegments;

        private void Awake()
        {
            ResolveHealth();
            CacheUiReferences();
            ApplySimpleStyle();
            RefreshBarLayoutIfNeeded();
            RefreshFillAmount();
            HideImmediately();
        }

        private void OnEnable()
        {
            ResolveHealth();
            SubscribeToHealth();
            RefreshBarLayoutIfNeeded();
            RefreshFillAmount();

            if (hideUntilDamaged)
            {
                HideImmediately();
            }
            else
            {
                ShowBar();
            }
        }

        private void Start()
        {
            ResolveHealth();
            SubscribeToHealth();
            RefreshBarLayoutIfNeeded();
            RefreshFillAmount();
        }

        private void OnDisable()
        {
            UnsubscribeFromHealth();
        }

        private void Update()
        {
            if (health == null)
            {
                ResolveHealth();
                SubscribeToHealth();
            }

            if (!hideUntilDamaged)
            {
                return;
            }

            if (barVisible && Time.time >= hideTime)
            {
                HideBar();
            }
        }

        private void ResolveHealth()
        {
            if (health == null)
            {
                health = GetComponentInParent<EnemyHealth>();
            }
        }

        private void CacheUiReferences()
        {
            if (barRoot == null)
            {
                barRoot = transform as RectTransform;
            }

            if (backgroundImage == null && barRoot != null)
            {
                backgroundImage = barRoot.GetComponent<Image>();
            }

            cachedBackgroundImage = backgroundImage;
            fillRect = fillImage != null ? fillImage.rectTransform : null;

            if (barRoot != null && barCanvasGroup == null)
            {
                barCanvasGroup = barRoot.GetComponent<CanvasGroup>();

                if (barCanvasGroup == null)
                {
                    barCanvasGroup = barRoot.gameObject.AddComponent<CanvasGroup>();
                }

                barCanvasGroup.interactable = false;
                barCanvasGroup.blocksRaycasts = false;
            }
        }

        private void SubscribeToHealth()
        {
            if (health == null || subscribedHealth == health)
            {
                return;
            }

            UnsubscribeFromHealth();

            subscribedHealth = health;
            subscribedHealth.HealthChanged += HandleHealthChanged;
            subscribedHealth.HpDecreased += HandleHpDecreased;
        }

        private void UnsubscribeFromHealth()
        {
            if (subscribedHealth == null)
            {
                return;
            }

            subscribedHealth.HealthChanged -= HandleHealthChanged;
            subscribedHealth.HpDecreased -= HandleHpDecreased;
            subscribedHealth = null;
        }

        private void HandleHealthChanged(EnemyHealth changedHealth)
        {
            RefreshBarLayoutIfNeeded();
            RefreshFillAmount();

            if (changedHealth != null && changedHealth.IsDead)
            {
                HideBar();
            }
        }

        private void HandleHpDecreased(EnemyHealth changedHealth, float decreasedHp)
        {
            if (changedHealth == null || changedHealth.IsDead || decreasedHp <= 0.0f)
            {
                return;
            }

            RefreshBarLayoutIfNeeded();
            RefreshFillAmount();
            ShowTemporarily();
        }

        private void ApplySimpleStyle()
        {
            CacheUiReferences();

            if (cachedBackgroundImage != null)
            {
                cachedBackgroundImage.color = backgroundColor;
                cachedBackgroundImage.raycastTarget = false;
                ApplyRoundedSprite(cachedBackgroundImage);
            }

            if (fillImage != null)
            {
                fillImage.color = fillColor;
                fillImage.raycastTarget = false;
                fillImage.fillAmount = 1.0f;
                ApplyRoundedSprite(fillImage);
            }

            if (segmentLinePrefab != null)
            {
                segmentLinePrefab.color = segmentLineColor;
                segmentLinePrefab.raycastTarget = false;
                segmentLinePrefab.gameObject.SetActive(false);
            }
        }

        private void ApplyRoundedSprite(Image image)
        {
            if (image == null || !useRoundedStyle)
            {
                return;
            }

            image.sprite = GetRoundedSprite();
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
        }

        private void RefreshBarLayoutIfNeeded()
        {
            float maxHp = health != null ? health.MaxHp : 1.0f;

            if (Mathf.Approximately(cachedMaxHp, maxHp) &&
                Mathf.Approximately(cachedFixedFillWidth, fixedFillWidth) &&
                Mathf.Approximately(cachedHorizontalSizeMultiplier, horizontalSizeMultiplier) &&
                Mathf.Approximately(cachedBarHeight, barHeight) &&
                Mathf.Approximately(cachedHpPerSegment, hpPerSegment) &&
                cachedMaxVisibleSegmentCount == maxVisibleSegmentCount &&
                Mathf.Approximately(cachedFillHorizontalPadding, fillHorizontalPadding) &&
                Mathf.Approximately(cachedFillVerticalPadding, fillVerticalPadding) &&
                cachedShowSegments == showSegments)
            {
                return;
            }

            cachedMaxHp = maxHp;
            cachedFixedFillWidth = fixedFillWidth;
            cachedHorizontalSizeMultiplier = horizontalSizeMultiplier;
            cachedBarHeight = barHeight;
            cachedHpPerSegment = hpPerSegment;
            cachedMaxVisibleSegmentCount = maxVisibleSegmentCount;
            cachedFillHorizontalPadding = fillHorizontalPadding;
            cachedFillVerticalPadding = fillVerticalPadding;
            cachedShowSegments = showSegments;

            float fillWidth = Mathf.Max(1.0f, fixedFillWidth * horizontalSizeMultiplier);
            float fillHeight = Mathf.Max(1.0f, barHeight - fillVerticalPadding * 2.0f);
            float barWidth = fillWidth + fillHorizontalPadding * 2.0f;

            if (barRoot != null)
            {
                barRoot.gameObject.SetActive(true);
                barRoot.sizeDelta = new Vector2(barWidth, barHeight);
            }

            if (fillRect != null)
            {
                fillRect.anchorMin = new Vector2(0.0f, 0.5f);
                fillRect.anchorMax = new Vector2(0.0f, 0.5f);
                fillRect.pivot = new Vector2(0.0f, 0.5f);
                fillRect.anchoredPosition = new Vector2(fillHorizontalPadding, 0.0f);
                fillRect.sizeDelta = new Vector2(fillWidth, fillHeight);
                fillRect.localScale = Vector3.one;
            }

            if (segmentLineRoot != null)
            {
                segmentLineRoot.anchorMin = new Vector2(0.0f, 0.5f);
                segmentLineRoot.anchorMax = new Vector2(0.0f, 0.5f);
                segmentLineRoot.pivot = new Vector2(0.0f, 0.5f);
                segmentLineRoot.anchoredPosition = new Vector2(fillHorizontalPadding, 0.0f);
                segmentLineRoot.sizeDelta = new Vector2(fillWidth, fillHeight);
                segmentLineRoot.localScale = Vector3.one;
                segmentLineRoot.gameObject.SetActive(showSegments);
            }

            RebuildSegmentLines(fillWidth, fillHeight, maxHp);
        }

        private void RefreshFillAmount()
        {
            if (fillRect == null || fillImage == null || health == null)
            {
                return;
            }

            float maxHp = Mathf.Max(1.0f, health.MaxHp);
            float hpRate = Mathf.Clamp01(health.CurrentHp / maxHp);
            float fillWidth = Mathf.Max(1.0f, fixedFillWidth * horizontalSizeMultiplier);
            float fillHeight = Mathf.Max(1.0f, barHeight - fillVerticalPadding * 2.0f);

            fillImage.enabled = hpRate > 0.001f;
            fillImage.fillAmount = 1.0f;
            fillRect.sizeDelta = new Vector2(fillWidth * hpRate, fillHeight);
        }

        private void RebuildSegmentLines(float fillWidth, float fillHeight, float maxHp)
        {
            if (segmentLineRoot == null || segmentLinePrefab == null)
            {
                return;
            }

            for (int i = segmentLineRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(segmentLineRoot.GetChild(i).gameObject);
            }

            segmentLinePrefab.gameObject.SetActive(false);

            if (!showSegments || hpPerSegment <= 0.0f || maxHp <= hpPerSegment)
            {
                return;
            }

            int rawLineCount = Mathf.FloorToInt((maxHp - 0.001f) / hpPerSegment);
            int maxLineCount = Mathf.Max(0, maxVisibleSegmentCount - 1);
            int lineCount = Mathf.Min(rawLineCount, maxLineCount);

            if (lineCount <= 0)
            {
                return;
            }

            bool capped = lineCount < rawLineCount;

            for (int i = 1; i <= lineCount; i++)
            {
                Image line = Instantiate(segmentLinePrefab, segmentLineRoot);
                line.gameObject.SetActive(true);
                line.color = segmentLineColor;

                RectTransform lineRect = line.rectTransform;
                float ratio = capped ? i / (float)(lineCount + 1) : Mathf.Clamp01(i * hpPerSegment / maxHp);
                float x = fillWidth * ratio;

                lineRect.anchorMin = new Vector2(0.0f, 0.5f);
                lineRect.anchorMax = new Vector2(0.0f, 0.5f);
                lineRect.pivot = new Vector2(0.5f, 0.5f);
                lineRect.anchoredPosition = new Vector2(x, 0.0f);
                lineRect.sizeDelta = new Vector2(1.0f, fillHeight);
                lineRect.localScale = Vector3.one;
            }
        }

        private void ShowTemporarily()
        {
            hideTime = Time.time + visibleSeconds;
            ShowBar();
        }

        private void ShowBar()
        {
            barVisible = true;

            if (barRoot != null)
            {
                barRoot.gameObject.SetActive(true);
            }

            SetBarAlpha(1.0f);
        }

        private void HideImmediately()
        {
            hideTime = 0.0f;
            HideBar();
        }

        private void HideBar()
        {
            if (!hideUntilDamaged)
            {
                return;
            }

            barVisible = false;
            SetBarAlpha(0.0f);
        }

        private void SetBarAlpha(float alpha)
        {
            if (barRoot != null && !barRoot.gameObject.activeSelf)
            {
                barRoot.gameObject.SetActive(true);
            }

            if (barCanvasGroup == null)
            {
                CacheUiReferences();
            }

            if (barCanvasGroup != null)
            {
                barCanvasGroup.alpha = alpha;
            }
        }

        private static Sprite GetRoundedSprite()
        {
            if (roundedSprite != null)
            {
                return roundedSprite;
            }

            Texture2D texture = new Texture2D(RoundedSpriteSize, RoundedSpriteSize, TextureFormat.RGBA32, false);
            texture.name = "Generated_EnemyHpBar_RoundedSprite";
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color32 solid = new Color32(255, 255, 255, 255);
            Color32 clear = new Color32(255, 255, 255, 0);

            for (int y = 0; y < RoundedSpriteSize; y++)
            {
                for (int x = 0; x < RoundedSpriteSize; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float cx = Mathf.Clamp(px, RoundedSpriteRadius, RoundedSpriteSize - RoundedSpriteRadius);
                    float cy = Mathf.Clamp(py, RoundedSpriteRadius, RoundedSpriteSize - RoundedSpriteRadius);
                    float dx = px - cx;
                    float dy = py - cy;
                    bool inside = dx * dx + dy * dy <= RoundedSpriteRadius * RoundedSpriteRadius;
                    texture.SetPixel(x, y, inside ? solid : clear);
                }
            }

            texture.Apply(false, true);

            roundedSprite = Sprite.Create(
                texture,
                new Rect(0.0f, 0.0f, RoundedSpriteSize, RoundedSpriteSize),
                new Vector2(0.5f, 0.5f),
                RoundedSpritePixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                new Vector4(RoundedSpriteRadius, RoundedSpriteRadius, RoundedSpriteRadius, RoundedSpriteRadius));
            roundedSprite.name = "Generated_EnemyHpBar_RoundedSprite";
            roundedSprite.hideFlags = HideFlags.HideAndDontSave;

            return roundedSprite;
        }
    }
}
