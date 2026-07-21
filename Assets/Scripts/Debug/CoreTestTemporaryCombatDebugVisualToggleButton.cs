using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    public sealed class CoreTestTemporaryCombatDebugVisualToggleButton : MonoBehaviour
    {
        public Button Button;
        public Text Label;
        public string EnabledLabel = "임시VFX\nON";
        public string DisabledLabel = "임시VFX\nOFF";

        private void Awake()
        {
            ResolveReferences();
            WireButton();
            RefreshLabel();
        }

        private void OnEnable()
        {
            ResolveReferences();
            WireButton();
            RefreshLabel();
        }

        private void OnDestroy()
        {
            if (Button != null)
            {
                Button.onClick.RemoveListener(ToggleDebugVisuals);
            }
        }

        public void ToggleDebugVisuals()
        {
            bool enabled = RuntimeCombatDebugVisuals.ToggleTemporaryCombatDebugVisuals();
            RefreshLabel();
            Debug.Log($"[CoreTest] 임시 전투 디버그 시각화 {(enabled ? "ON" : "OFF")}", this);
        }

        private void WireButton()
        {
            if (Button == null)
            {
                return;
            }

            Button.onClick.RemoveListener(ToggleDebugVisuals);
            Button.onClick.AddListener(ToggleDebugVisuals);
        }

        private void ResolveReferences()
        {
            if (Button == null)
            {
                Button = GetComponent<Button>();
            }

            if (Label == null && Button != null)
            {
                Label = Button.GetComponentInChildren<Text>(true);
            }
        }

        private void RefreshLabel()
        {
            if (Label == null)
            {
                return;
            }

            Label.text = RuntimeCombatDebugVisuals.TemporaryCombatDebugVisualsEnabled
                ? EnabledLabel
                : DisabledLabel;
        }
    }

    internal static class CoreTestTemporaryCombatDebugVisualToggleBootstrap
    {
        private const string ParentName = "DebugSection_Misc";
        private const string ButtonName = "TemporaryCombatDebugVisualToggleButton";
        private const string TemplateName = "DamageFloatingFontButton";
        private const string EnabledLabel = "임시VFX\nON";
        private const string DisabledLabel = "임시VFX\nOFF";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TryCreateButton();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryCreateButton();
        }

        private static void TryCreateButton()
        {
            GameObject parentObject = GameObject.Find(ParentName);
            if (parentObject == null)
            {
                return;
            }

            Transform parent = parentObject.transform;
            Transform existing = parent.Find(ButtonName);
            if (existing != null)
            {
                ConfigureButton(existing.gameObject);
                return;
            }

            GameObject buttonObject = CreateButton(parent);
            buttonObject.transform.SetAsLastSibling();
            ConfigureButton(buttonObject);
            ResizeMiscSection(parent);
        }

        private static GameObject CreateButton(Transform parent)
        {
            GameObject buttonObject = new GameObject(
                ButtonName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement),
                typeof(CoreTestTemporaryCombatDebugVisualToggleButton));
            buttonObject.layer = parent.gameObject.layer;
            buttonObject.transform.SetParent(parent, false);

            CopyButtonStyle(buttonObject, parent.Find(TemplateName));
            CreateLabel(buttonObject.transform, parent.Find(TemplateName));
            return buttonObject;
        }

        private static void ConfigureButton(GameObject buttonObject)
        {
            Button button = buttonObject.GetComponent<Button>();
            Text label = buttonObject.GetComponentInChildren<Text>(true);
            CoreTestTemporaryCombatDebugVisualToggleButton toggle = buttonObject.GetComponent<CoreTestTemporaryCombatDebugVisualToggleButton>();
            if (toggle == null)
            {
                toggle = buttonObject.AddComponent<CoreTestTemporaryCombatDebugVisualToggleButton>();
            }

            toggle.Button = button;
            toggle.Label = label;
            toggle.EnabledLabel = EnabledLabel;
            toggle.DisabledLabel = DisabledLabel;

            if (label != null)
            {
                label.text = RuntimeCombatDebugVisuals.TemporaryCombatDebugVisualsEnabled ? EnabledLabel : DisabledLabel;
            }

            EnsureButtonLayout(buttonObject);
        }

        private static void CopyButtonStyle(GameObject buttonObject, Transform template)
        {
            Image image = buttonObject.GetComponent<Image>();
            Button button = buttonObject.GetComponent<Button>();
            if (template != null)
            {
                Image templateImage = template.GetComponent<Image>();
                if (templateImage != null && image != null)
                {
                    image.sprite = templateImage.sprite;
                    image.type = templateImage.type;
                    image.color = templateImage.color;
                    image.raycastTarget = templateImage.raycastTarget;
                }

                Button templateButton = template.GetComponent<Button>();
                if (templateButton != null && button != null)
                {
                    button.transition = templateButton.transition;
                    button.colors = templateButton.colors;
                    button.spriteState = templateButton.spriteState;
                    button.animationTriggers = templateButton.animationTriggers;
                    button.interactable = templateButton.interactable;
                }
            }

            if (image != null && image.color.a <= 0f)
            {
                image.color = new Color(0.12f, 0.16f, 0.2f, 0.82f);
            }
        }

        private static void CreateLabel(Transform parent, Transform template)
        {
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.layer = parent.gameObject.layer;
            labelObject.transform.SetParent(parent, false);

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text label = labelObject.GetComponent<Text>();
            Text templateLabel = template != null ? template.GetComponentInChildren<Text>(true) : null;
            if (templateLabel != null)
            {
                label.font = templateLabel.font;
                label.fontSize = templateLabel.fontSize;
                label.fontStyle = templateLabel.fontStyle;
                label.alignment = templateLabel.alignment;
                label.color = templateLabel.color;
                label.raycastTarget = templateLabel.raycastTarget;
            }
            else
            {
                label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                label.fontSize = 12;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = Color.white;
                label.raycastTarget = false;
            }

            label.text = DisabledLabel;
        }

        private static void EnsureButtonLayout(GameObject buttonObject)
        {
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                rect.localScale = Vector3.one;
            }

            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = buttonObject.AddComponent<LayoutElement>();
            }

            layout.minHeight = 23f;
            layout.preferredHeight = 23f;
            layout.flexibleWidth = 1f;
            layout.flexibleHeight = -1f;
        }

        private static void ResizeMiscSection(Transform parent)
        {
            VerticalLayoutGroup layoutGroup = parent.GetComponent<VerticalLayoutGroup>();
            float spacing = layoutGroup != null ? layoutGroup.spacing : 2f;
            float padding = layoutGroup != null ? layoutGroup.padding.top + layoutGroup.padding.bottom : 8f;
            int activeChildren = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).gameObject.activeSelf)
                {
                    activeChildren++;
                }
            }

            float targetHeight = activeChildren * 23f + Mathf.Max(0, activeChildren - 1) * spacing + padding;

            LayoutElement sectionLayout = parent.GetComponent<LayoutElement>();
            if (sectionLayout != null)
            {
                sectionLayout.minHeight = Mathf.Max(sectionLayout.minHeight, targetHeight);
                sectionLayout.preferredHeight = Mathf.Max(sectionLayout.preferredHeight, targetHeight);
            }

            RectTransform sectionRect = parent as RectTransform;
            if (sectionRect != null && sectionRect.sizeDelta.y < targetHeight)
            {
                sectionRect.sizeDelta = new Vector2(sectionRect.sizeDelta.x, targetHeight);
            }
        }
    }
}
