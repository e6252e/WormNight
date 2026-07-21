using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    public sealed class HudTooltipManager : MonoBehaviour
    {
        public static HudTooltipManager Instance { get; private set; }
        private const int RoundedBackgroundSize = 32;
        private const float RoundedBackgroundRadius = 7f;
        private const float RoundedBackgroundBorder = 8f;
        private static Sprite roundedBackgroundSprite;

        [SerializeField] private GameObject tooltipPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private TextMeshProUGUI footerText;
        [SerializeField] private TMP_FontAsset tooltipFont;
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Vector2 screenOffset = new Vector2(18f, -18f);
        [SerializeField, Min(180f)] private float panelWidth = 300f;

        private RectTransform tooltipRect;
        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private Vector2 lastPointerPosition;

        private void Awake()
        {
            RegisterInstance(this);
            EnsureRuntimeView();
            HideTooltip();
        }

        private void OnEnable()
        {
            RegisterInstance(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (tooltipPanel == null || !tooltipPanel.activeSelf)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                MoveTo(mouse.position.ReadValue());
            }
            else
            {
                MoveTo(lastPointerPosition);
            }
        }

        public static HudTooltipManager ResolveFor(Transform owner)
        {
            if (Instance != null)
            {
                return Instance;
            }

            Canvas ownerCanvas = owner != null ? owner.GetComponentInParent<Canvas>(true) : null;
            if (ownerCanvas == null)
            {
                ownerCanvas = FindFirstObjectByType<Canvas>();
            }

            if (ownerCanvas == null)
            {
                return null;
            }

            GameObject root = new GameObject("HudTooltipRoot", typeof(RectTransform), typeof(HudTooltipManager));
            root.transform.SetParent(ownerCanvas.transform, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            HudTooltipManager manager = root.GetComponent<HudTooltipManager>();
            manager.canvas = ownerCanvas;
            manager.EnsureRuntimeView();
            manager.HideTooltip();
            return manager;
        }

        public void ShowTooltip(HudTooltipContent content, Vector2 pointerPosition)
        {
            EnsureRuntimeView();
            if (content == null || !content.HasAnyText || tooltipPanel == null)
            {
                HideTooltip();
                return;
            }

            SetText(titleText, content.Title);
            SetText(bodyText, content.Body);
            SetText(footerText, content.Footer);

            tooltipPanel.SetActive(true);
            tooltipPanel.transform.SetAsLastSibling();
            RebuildLayout();
            MoveTo(pointerPosition);
        }

        public void HideTooltip()
        {
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
            }
        }

        public void MoveTo(Vector2 pointerPosition)
        {
            lastPointerPosition = pointerPosition;
            if (tooltipRect == null || tooltipPanel == null || !tooltipPanel.activeSelf)
            {
                return;
            }

            tooltipRect.position = ClampToScreen(pointerPosition + screenOffset);
        }

        private static void RegisterInstance(HudTooltipManager candidate)
        {
            if (candidate == null)
            {
                return;
            }

            Instance = candidate;
        }

        private void EnsureRuntimeView()
        {
            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>(true);
            }

            if (tooltipPanel == null)
            {
                Transform existingPanel = transform.Find("TooltipPanel");
                tooltipPanel = existingPanel != null ? existingPanel.gameObject : CreatePanel();
            }

            tooltipRect = tooltipPanel.GetComponent<RectTransform>();
            if (tooltipRect == null)
            {
                tooltipRect = tooltipPanel.AddComponent<RectTransform>();
            }

            tooltipRect.pivot = new Vector2(0f, 1f);
            tooltipRect.sizeDelta = new Vector2(panelWidth, tooltipRect.sizeDelta.y);

            Image background = tooltipPanel.GetComponent<Image>();
            if (background == null)
            {
                background = tooltipPanel.AddComponent<Image>();
            }

            background.color = new Color(0.03f, 0.035f, 0.045f, 0.96f);
            background.sprite = backgroundSprite != null ? backgroundSprite : GetRoundedBackgroundSprite();
            background.type = Image.Type.Sliced;
            background.raycastTarget = false;

            canvasGroup = tooltipPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = tooltipPanel.AddComponent<CanvasGroup>();
            }

            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            VerticalLayoutGroup layout = tooltipPanel.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = tooltipPanel.AddComponent<VerticalLayoutGroup>();
            }

            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = tooltipPanel.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = tooltipPanel.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            titleText = EnsureText("TitleText", titleText, 17, FontStyles.Bold, new Color(1f, 0.9f, 0.55f, 1f));
            bodyText = EnsureText("BodyText", bodyText, 14, FontStyles.Normal, new Color(0.9f, 0.94f, 1f, 1f));
            footerText = EnsureText("FooterText", footerText, 13, FontStyles.Bold, new Color(0.55f, 0.78f, 1f, 1f));
            ConfigureRaycastBlocking();
        }

        private GameObject CreatePanel()
        {
            GameObject panel = new GameObject("TooltipPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panel.transform.SetParent(transform, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(panelWidth, 120f);
            return panel;
        }

        private static Sprite GetRoundedBackgroundSprite()
        {
            if (roundedBackgroundSprite != null)
            {
                return roundedBackgroundSprite;
            }

            Texture2D texture = new Texture2D(RoundedBackgroundSize, RoundedBackgroundSize, TextureFormat.RGBA32, false)
            {
                name = "HudTooltipRoundedBackgroundTexture",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32[] pixels = new Color32[RoundedBackgroundSize * RoundedBackgroundSize];
            Color32 visible = new Color32(255, 255, 255, 255);
            Color32 transparent = new Color32(255, 255, 255, 0);
            for (int y = 0; y < RoundedBackgroundSize; y++)
            {
                for (int x = 0; x < RoundedBackgroundSize; x++)
                {
                    bool inside = IsInsideRoundedRect(
                        x + 0.5f,
                        y + 0.5f,
                        RoundedBackgroundSize,
                        RoundedBackgroundSize,
                        RoundedBackgroundRadius);
                    pixels[y * RoundedBackgroundSize + x] = inside ? visible : transparent;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            roundedBackgroundSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, RoundedBackgroundSize, RoundedBackgroundSize),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(RoundedBackgroundBorder, RoundedBackgroundBorder, RoundedBackgroundBorder, RoundedBackgroundBorder));
            roundedBackgroundSprite.name = "HudTooltipRoundedBackground_Runtime";
            roundedBackgroundSprite.hideFlags = HideFlags.HideAndDontSave;
            return roundedBackgroundSprite;
        }

        private static bool IsInsideRoundedRect(float x, float y, float width, float height, float radius)
        {
            float closestX = Mathf.Clamp(x, radius, width - radius);
            float closestY = Mathf.Clamp(y, radius, height - radius);
            float dx = x - closestX;
            float dy = y - closestY;
            return dx * dx + dy * dy <= radius * radius;
        }

        private TextMeshProUGUI EnsureText(string childName, TextMeshProUGUI current, int fontSize, FontStyles style, Color color)
        {
            if (current == null)
            {
                Transform child = tooltipPanel.transform.Find(childName);
                current = child != null ? child.GetComponent<TextMeshProUGUI>() : null;
            }

            if (current == null)
            {
                GameObject textObject = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
                textObject.transform.SetParent(tooltipPanel.transform, false);
                current = textObject.GetComponent<TextMeshProUGUI>();
            }

            RemoveLegacyText(current.gameObject);

            current.font = ResolveTooltipFont();
            current.fontSize = fontSize;
            current.fontStyle = style;
            current.color = color;
            current.richText = true;
            current.alignment = TextAlignmentOptions.TopLeft;
            current.textWrappingMode = TextWrappingModes.Normal;
            current.overflowMode = TextOverflowModes.Overflow;
            current.raycastTarget = false;

            LayoutElement layout = current.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = current.gameObject.AddComponent<LayoutElement>();
            }

            layout.preferredWidth = panelWidth - 24f;
            layout.flexibleWidth = 1f;
            return current;
        }

        private TMP_FontAsset ResolveTooltipFont()
        {
            return tooltipFont != null ? tooltipFont : TMP_Settings.defaultFontAsset;
        }

        private static void RemoveLegacyText(GameObject target)
        {
            Text legacyText = target != null ? target.GetComponent<Text>() : null;
            if (legacyText == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(legacyText);
            }
            else
            {
                DestroyImmediate(legacyText);
            }
        }

        private void RebuildLayout()
        {
            if (tooltipRect == null)
            {
                return;
            }

            tooltipRect.sizeDelta = new Vector2(panelWidth, tooltipRect.sizeDelta.y);
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
        }

        private Vector2 ClampToScreen(Vector2 desired)
        {
            float scale = canvas != null ? Mathf.Max(0.001f, canvas.scaleFactor) : 1f;
            float width = tooltipRect != null ? tooltipRect.rect.width * scale : panelWidth;
            float height = tooltipRect != null ? tooltipRect.rect.height * scale : 120f;
            const float margin = 10f;

            float x = Mathf.Clamp(desired.x, margin, Mathf.Max(margin, Screen.width - width - margin));
            float y = Mathf.Clamp(desired.y, Mathf.Min(Screen.height - margin, height + margin), Screen.height - margin);
            return new Vector2(x, y);
        }

        private void ConfigureRaycastBlocking()
        {
            if (tooltipPanel == null)
            {
                return;
            }

            Graphic[] graphics = tooltipPanel.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = false;
            }
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text == null)
            {
                return;
            }

            text.text = value ?? string.Empty;
            text.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
        }
    }
}
