using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class NexusScreenVignetteController : MonoBehaviour
    {
        private const string RootName = "NexusScreenVignette";
        private const string HudCanvasName = "HudCanvas";
        private const string TopName = "Top";
        private const string BottomName = "Bottom";
        private const string LeftName = "Left";
        private const string RightName = "Right";
        private const float HorizontalEdgeHeight = 190f;
        private const float VerticalEdgeWidth = 220f;

        [Header("Binding")]
        [SerializeField] private NexusController nexus;
        [SerializeField] private Image topEdge;
        [SerializeField] private Image bottomEdge;
        [SerializeField] private Image leftEdge;
        [SerializeField] private Image rightEdge;
        [SerializeField] private bool autoResolveChildren = true;

        [Header("Color")]
        [SerializeField] private Color shieldHitColor = new Color(0.22f, 0.62f, 1f, 1f);
        [SerializeField] private Color healthHitColor = new Color(1f, 0.12f, 0.08f, 1f);
        [SerializeField] private Color healColor = new Color(0.18f, 1f, 0.42f, 1f);

        [Header("Intensity")]
        [SerializeField, Range(0f, 1f)] private float shieldHitAlpha = 0.18f;
        [SerializeField, Range(0f, 1f)] private float healthHitAlpha = 0.3f;
        [SerializeField, Range(0f, 1f)] private float healAlpha = 0.22f;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.04f;
        [SerializeField, Min(0f)] private float holdSeconds = 0.04f;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.32f;

        [Header("Generated Gradient")]
        [SerializeField, Min(8)] private int gradientResolution = 64;
        [SerializeField, Range(0.1f, 4f)] private float gradientPower = 1.65f;

        private NexusController subscribedNexus;
        private Coroutine feedbackRoutine;
        private Sprite topSprite;
        private Sprite bottomSprite;
        private Sprite leftSprite;
        private Sprite rightSprite;
        private Texture2D topTexture;
        private Texture2D bottomTexture;
        private Texture2D leftTexture;
        private Texture2D rightTexture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeOverlay()
        {
            if (FindFirstObjectByType<NexusScreenVignetteController>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            Canvas canvas = FindHudCanvas();
            if (canvas == null)
            {
                canvas = CreateFallbackCanvas();
            }

            if (canvas == null)
            {
                return;
            }

            GameObject rootObject = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup));
            rootObject.SetActive(false);
            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.SetParent(canvas.transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootObject.transform.SetAsLastSibling();

            CanvasGroup group = rootObject.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;

            CreateEdge(rootRect, TopName, EdgeDirection.Top);
            CreateEdge(rootRect, BottomName, EdgeDirection.Bottom);
            CreateEdge(rootRect, LeftName, EdgeDirection.Left);
            CreateEdge(rootRect, RightName, EdgeDirection.Right);

            rootObject.AddComponent<NexusScreenVignetteController>();
            rootObject.SetActive(true);
        }

        private static Canvas FindHudCanvas()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas != null && canvas.name == HudCanvasName)
                {
                    return canvas;
                }
            }

            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    return canvas;
                }
            }

            return null;
        }

        private static Canvas CreateFallbackCanvas()
        {
            GameObject canvasObject = new GameObject("NexusScreenVignetteCanvas", typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void CreateEdge(RectTransform root, string edgeName, EdgeDirection direction)
        {
            GameObject edgeObject = new GameObject(edgeName, typeof(RectTransform), typeof(Image));
            RectTransform rect = edgeObject.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            ConfigureEdgeRect(rect, direction);

            Image image = edgeObject.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;
            image.enabled = false;
        }

        private static void ConfigureEdgeRect(RectTransform rect, EdgeDirection direction)
        {
            switch (direction)
            {
                case EdgeDirection.Top:
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(0f, HorizontalEdgeHeight);
                    break;
                case EdgeDirection.Bottom:
                    rect.anchorMin = new Vector2(0f, 0f);
                    rect.anchorMax = new Vector2(1f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(0f, HorizontalEdgeHeight);
                    break;
                case EdgeDirection.Left:
                    rect.anchorMin = new Vector2(0f, 0f);
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(VerticalEdgeWidth, 0f);
                    break;
                case EdgeDirection.Right:
                    rect.anchorMin = new Vector2(1f, 0f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(1f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(VerticalEdgeWidth, 0f);
                    break;
            }
        }

        private void Awake()
        {
            ResolveReferences();
            ConfigureEdges();
            SetAlpha(0f);
        }

        private void OnEnable()
        {
            ResolveReferences();
            ConfigureEdges();
            BindNexusEvents();
            SetAlpha(0f);
        }

        private void OnDisable()
        {
            UnbindNexusEvents();
            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
                feedbackRoutine = null;
            }

            SetAlpha(0f);
        }

        private void OnDestroy()
        {
            DestroyGeneratedAsset(ref topSprite);
            DestroyGeneratedAsset(ref bottomSprite);
            DestroyGeneratedAsset(ref leftSprite);
            DestroyGeneratedAsset(ref rightSprite);
            DestroyGeneratedAsset(ref topTexture);
            DestroyGeneratedAsset(ref bottomTexture);
            DestroyGeneratedAsset(ref leftTexture);
            DestroyGeneratedAsset(ref rightTexture);
        }

        private void Update()
        {
            if (subscribedNexus == null)
            {
                ResolveReferences();
                BindNexusEvents();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveReferences();
            ConfigureEdges();
            SetAlpha(0f);
        }
#endif

        private void ResolveReferences()
        {
            if (nexus == null)
            {
                nexus = NexusController.Active != null ? NexusController.Active : FindFirstObjectByType<NexusController>();
            }

            if (!autoResolveChildren)
            {
                return;
            }

            topEdge = topEdge != null ? topEdge : FindChildImage(TopName);
            bottomEdge = bottomEdge != null ? bottomEdge : FindChildImage(BottomName);
            leftEdge = leftEdge != null ? leftEdge : FindChildImage(LeftName);
            rightEdge = rightEdge != null ? rightEdge : FindChildImage(RightName);
        }

        private Image FindChildImage(string childName)
        {
            Transform child = transform.Find(childName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private void ConfigureEdges()
        {
            ConfigureEdge(topEdge, ref topTexture, ref topSprite, EdgeDirection.Top);
            ConfigureEdge(bottomEdge, ref bottomTexture, ref bottomSprite, EdgeDirection.Bottom);
            ConfigureEdge(leftEdge, ref leftTexture, ref leftSprite, EdgeDirection.Left);
            ConfigureEdge(rightEdge, ref rightTexture, ref rightSprite, EdgeDirection.Right);
        }

        private void ConfigureEdge(Image image, ref Texture2D texture, ref Sprite sprite, EdgeDirection direction)
        {
            if (image == null)
            {
                return;
            }

            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = false;

            if (!Application.isPlaying)
            {
                image.enabled = false;
                return;
            }

            if (texture == null || sprite == null)
            {
                texture = CreateGradientTexture(direction);
                sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = $"NexusScreenVignette_{direction}";
            }

            image.sprite = sprite;
        }

        private Texture2D CreateGradientTexture(EdgeDirection direction)
        {
            int size = Mathf.Max(8, gradientResolution);
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"NexusScreenVignette_{direction}_Texture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = size <= 1 ? 0f : (float)x / (size - 1);
                    float v = size <= 1 ? 0f : (float)y / (size - 1);
                    float edgeAmount = ResolveEdgeAmount(direction, u, v);
                    float alpha = Mathf.Pow(Mathf.Clamp01(edgeAmount), gradientPower);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, true);
            return texture;
        }

        private static float ResolveEdgeAmount(EdgeDirection direction, float u, float v)
        {
            switch (direction)
            {
                case EdgeDirection.Top:
                    return v;
                case EdgeDirection.Bottom:
                    return 1f - v;
                case EdgeDirection.Left:
                    return 1f - u;
                case EdgeDirection.Right:
                    return u;
                default:
                    return 0f;
            }
        }

        private void BindNexusEvents()
        {
            if (subscribedNexus == nexus)
            {
                return;
            }

            UnbindNexusEvents();
            if (nexus == null)
            {
                return;
            }

            subscribedNexus = nexus;
            subscribedNexus.ScreenFeedbackRequested += PlayFeedback;
        }

        private void UnbindNexusEvents()
        {
            if (subscribedNexus == null)
            {
                return;
            }

            subscribedNexus.ScreenFeedbackRequested -= PlayFeedback;
            subscribedNexus = null;
        }

        private void PlayFeedback(NexusScreenFeedbackKind kind, int amount)
        {
            Color color = ResolveColor(kind);
            float targetAlpha = ResolveAlpha(kind);

            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
            }

            feedbackRoutine = StartCoroutine(PlayFeedbackRoutine(color, targetAlpha));
        }

        private Color ResolveColor(NexusScreenFeedbackKind kind)
        {
            switch (kind)
            {
                case NexusScreenFeedbackKind.ShieldHit:
                    return shieldHitColor;
                case NexusScreenFeedbackKind.HealthHit:
                    return healthHitColor;
                case NexusScreenFeedbackKind.Heal:
                    return healColor;
                default:
                    return Color.clear;
            }
        }

        private float ResolveAlpha(NexusScreenFeedbackKind kind)
        {
            switch (kind)
            {
                case NexusScreenFeedbackKind.ShieldHit:
                    return shieldHitAlpha;
                case NexusScreenFeedbackKind.HealthHit:
                    return healthHitAlpha;
                case NexusScreenFeedbackKind.Heal:
                    return healAlpha;
                default:
                    return 0f;
            }
        }

        private IEnumerator PlayFeedbackRoutine(Color color, float targetAlpha)
        {
            SetColor(color);
            yield return FadeAlpha(GetCurrentAlpha(), targetAlpha, fadeInSeconds);

            if (holdSeconds > 0f)
            {
                yield return new WaitForSeconds(holdSeconds);
            }

            yield return FadeAlpha(targetAlpha, 0f, fadeOutSeconds);
            feedbackRoutine = null;
        }

        private float GetCurrentAlpha()
        {
            if (topEdge != null)
            {
                return topEdge.color.a;
            }

            if (bottomEdge != null)
            {
                return bottomEdge.color.a;
            }

            if (leftEdge != null)
            {
                return leftEdge.color.a;
            }

            return rightEdge != null ? rightEdge.color.a : 0f;
        }

        private IEnumerator FadeAlpha(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                SetAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetAlpha(Mathf.Lerp(from, to, Smooth01(t)));
                yield return null;
            }

            SetAlpha(to);
        }

        private static float Smooth01(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private void SetColor(Color color)
        {
            SetEdgeColor(topEdge, color);
            SetEdgeColor(bottomEdge, color);
            SetEdgeColor(leftEdge, color);
            SetEdgeColor(rightEdge, color);
        }

        private void SetAlpha(float alpha)
        {
            SetEdgeAlpha(topEdge, alpha);
            SetEdgeAlpha(bottomEdge, alpha);
            SetEdgeAlpha(leftEdge, alpha);
            SetEdgeAlpha(rightEdge, alpha);
        }

        private static void SetEdgeColor(Image image, Color color)
        {
            if (image == null)
            {
                return;
            }

            float previousAlpha = image.color.a;
            image.color = new Color(color.r, color.g, color.b, previousAlpha);
        }

        private static void SetEdgeAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            Color color = image.color;
            color.a = Mathf.Clamp01(alpha);
            image.color = color;
            image.enabled = color.a > 0.001f;
        }

        private static void DestroyGeneratedAsset<T>(ref T asset) where T : Object
        {
            if (asset == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(asset);
            }
            else
            {
                DestroyImmediate(asset);
            }

            asset = null;
        }

        private enum EdgeDirection
        {
            Top,
            Bottom,
            Left,
            Right
        }
    }
}
