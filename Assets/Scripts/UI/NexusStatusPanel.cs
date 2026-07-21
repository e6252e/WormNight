using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TeamProject01.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class NexusStatusPanel : MonoBehaviour // 넥서스 체력/실드 전용 패널
    {
        private const string ShieldBarName = "Shield";
        private const string HealthBarName = "Health";
        private const string FillName = "Fill";
        private const string TextName = "Text";

        [Header("Binding")]
        [SerializeField] private NexusController nexus; // 표시 대상 넥서스
        [SerializeField] private RectTransform statusRoot; // 패널 루트
        [SerializeField] private Image shieldFillImage; // 실드 Fill
        [SerializeField] private Image healthFillImage; // 체력 Fill
        [SerializeField] private TMP_Text shieldText; // 실드 수치
        [SerializeField] private TMP_Text healthText; // 체력 수치

        [Header("Visual")]
        [SerializeField] private bool autoResolveChildren = true; // 하위 Shield/Health 자동 연결
        [SerializeField] private bool configureVisuals = true; // Fill/Text 표시 속성 보정
        [SerializeField] private Color shieldColor = new Color(0.22f, 0.72f, 1f, 0.92f);
        [SerializeField] private Color healthColor = new Color(0.3f, 0.95f, 0.48f, 0.94f);
        [SerializeField] private Color lowHealthColor = new Color(1f, 0.23f, 0.18f, 0.94f);
        [SerializeField, Range(0.01f, 1f)] private float lowHealthRatio = 0.3f;

        private NexusController subscribedNexus;
        private RectSnapshot shieldFillSnapshot; // 씬에서 맞춘 실드 Fill 크기
        private RectSnapshot healthFillSnapshot; // 씬에서 맞춘 체력 Fill 크기

        private void Awake()
        {
            ResolveReferences();
            CaptureGaugeLayout();
            ConfigureVisuals();
            RestoreGaugeLayout();
        }

        private void OnEnable()
        {
            ResolveReferences();
            CaptureGaugeLayout();
            BindNexusEvents();
            RefreshNow();
        }

        private void OnDisable()
        {
            UnbindNexusEvents();
        }

        private void LateUpdate()
        {
            if (nexus == null)
            {
                ResolveReferences();
                BindNexusEvents();
            }

            RefreshNow();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveReferences();
            ConfigureVisuals();
            RefreshNow();
        }
#endif

        public void RefreshNow()
        {
            RestoreGaugeLayout();

            if (nexus == null)
            {
                SetGaugeRatio(shieldFillImage, 0f);
                SetGaugeRatio(healthFillImage, 0f);
                SetText(shieldText, "실드 0/0");
                SetText(healthText, "체력 0/0");
                return;
            }

            SetGaugeRatio(shieldFillImage, nexus.ShieldRatio);
            SetGaugeRatio(healthFillImage, nexus.HealthRatio);

            if (healthFillImage != null && ShouldTintFill(healthFillImage))
            {
                healthFillImage.color = nexus.HealthRatio <= lowHealthRatio ? lowHealthColor : healthColor;
            }

            SetText(shieldText, $"실드 {nexus.CurrentShield}/{nexus.MaxShield}");
            string healthValue = nexus.IsInvincible
                ? $"체력 {nexus.CurrentHealth}/{nexus.MaxHealth} 무적"
                : $"체력 {nexus.CurrentHealth}/{nexus.MaxHealth}";
            SetText(healthText, healthValue);
        }

        private void ResolveReferences()
        {
            if (statusRoot == null)
            {
                statusRoot = transform as RectTransform;
            }

            if (autoResolveChildren && statusRoot != null)
            {
                shieldFillImage = shieldFillImage != null ? shieldFillImage : FindStatusChild<Image>(ShieldBarName, FillName);
                healthFillImage = healthFillImage != null ? healthFillImage : FindStatusChild<Image>(HealthBarName, FillName);
                shieldText = shieldText != null ? shieldText : FindStatusChild<TMP_Text>(ShieldBarName, TextName);
                healthText = healthText != null ? healthText : FindStatusChild<TMP_Text>(HealthBarName, TextName);
            }

            if (nexus == null)
            {
                nexus = NexusController.Active != null ? NexusController.Active : FindFirstObjectByType<NexusController>();
            }
        }

        private T FindStatusChild<T>(string barName, string childName) where T : Component
        {
            if (statusRoot == null)
            {
                return null;
            }

            Transform child = statusRoot.Find($"{barName}/{childName}");
            if (child != null && child.TryGetComponent(out T directComponent))
            {
                return directComponent;
            }

            Transform bar = statusRoot.Find(barName);
            if (bar == null)
            {
                return null;
            }

            return bar.GetComponentInChildren<T>(true);
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
            subscribedNexus.HealthChanged += OnNexusHealthChanged;
            subscribedNexus.ShieldChanged += OnNexusShieldChanged;
            subscribedNexus.StateChanged += OnNexusStateChanged;
        }

        private void UnbindNexusEvents()
        {
            if (subscribedNexus == null)
            {
                return;
            }

            subscribedNexus.HealthChanged -= OnNexusHealthChanged;
            subscribedNexus.ShieldChanged -= OnNexusShieldChanged;
            subscribedNexus.StateChanged -= OnNexusStateChanged;
            subscribedNexus = null;
        }

        private void OnNexusHealthChanged(int current, int max)
        {
            RefreshNow();
        }

        private void OnNexusShieldChanged(int current, int max)
        {
            RefreshNow();
        }

        private void OnNexusStateChanged(NexusController changedNexus)
        {
            RefreshNow();
        }

        private void ConfigureVisuals()
        {
            if (!configureVisuals)
            {
                return;
            }

            ConfigureFill(shieldFillImage, shieldColor);
            ConfigureFill(healthFillImage, healthColor);
            ConfigureText(shieldText);
            ConfigureText(healthText);
        }

        private void ConfigureFill(Image image, Color color)
        {
            if (image == null)
            {
                return;
            }

            bool hasSprite = image.sprite != null;
            image.color = hasSprite ? Color.white : color;
            image.type = Image.Type.Filled; // 폭을 줄이지 않고 fillAmount로 원본을 자름
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillCenter = true;
            image.preserveAspect = false;

            image.raycastTarget = false;
        }

        private static void ConfigureText(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.92f, 1f, 1f, 1f);
            text.raycastTarget = false;
        }

        private static void SetGaugeRatio(Image image, float amount)
        {
            if (image == null)
            {
                return;
            }

            float ratio = Mathf.Clamp01(amount);
            image.enabled = true;
            image.fillAmount = ratio;
        }

        private void CaptureGaugeLayout() // 에디터에서 맞춘 현재 크기를 런타임 기준으로 사용
        {
            shieldFillSnapshot.Capture(shieldFillImage != null ? shieldFillImage.rectTransform : null);
            healthFillSnapshot.Capture(healthFillImage != null ? healthFillImage.rectTransform : null);
        }

        private void RestoreGaugeLayout() // 다른 갱신이 Fill Rect 크기를 건드려도 현재 씬 크기로 복구
        {
            shieldFillSnapshot.Restore();
            healthFillSnapshot.Restore();
        }

        private static bool ShouldTintFill(Image image)
        {
            return image != null && image.sprite == null;
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        private struct RectSnapshot
        {
            private RectTransform rect;
            private Vector2 anchorMin;
            private Vector2 anchorMax;
            private Vector2 anchoredPosition;
            private Vector2 sizeDelta;
            private Vector2 pivot;
            private bool captured;

            public void Capture(RectTransform source)
            {
                if (source == null)
                {
                    return;
                }

                rect = source;
                anchorMin = source.anchorMin;
                anchorMax = source.anchorMax;
                anchoredPosition = source.anchoredPosition;
                sizeDelta = source.sizeDelta;
                pivot = source.pivot;
                captured = true;
            }

            public void Restore()
            {
                if (!captured || rect == null)
                {
                    return;
                }

                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = sizeDelta;
                rect.pivot = pivot;
            }
        }
    }
}
