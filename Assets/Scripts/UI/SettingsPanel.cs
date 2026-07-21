using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    public sealed class SettingsPanel : MonoBehaviour // 타이틀 설정 패널 //안건준 추가 - 0628
    {
        [Header("Panel")]
        [SerializeField] private GameObject panelRoot; // Settings3

        [Header("Volume")]
        [SerializeField] private Slider masterVolumeSlider; // Master Volume 슬라이더 //안건준 추가 - 0628
        [SerializeField] private Slider bgmVolumeSlider; // BGM Volume 슬라이더
        [SerializeField] private Slider sfxVolumeSlider; // SFX Volume 슬라이더
        [SerializeField] private TextMeshProUGUI masterVolumePercentText; // Master % 표시 //안건준 추가 - 0628
        [SerializeField] private TextMeshProUGUI bgmVolumePercentText; // BGM % 표시 (선택)
        [SerializeField] private TextMeshProUGUI sfxVolumePercentText; // SFX % 표시 (선택)

        [Header("Buttons")]
        [SerializeField] private Button closeButton; // CloseButton (우상단 X)
        [SerializeField] private Button quitGameButton; // Quit GameButton — 게임 포기 //안건준 추가 - 0628
        [SerializeField] private Button closeWindowButton; // Close WindowButton — 창 닫기 //안건준 추가 - 0628

        [Header("Button Animation")]
        [SerializeField] private float hoverScale = 1.09f;
        [SerializeField] private float clickScale = 1.2f;
        [SerializeField] private float hoverDuration = 0.15f;
        [SerializeField] private float clickUpSeconds = 0.15f;
        [SerializeField] private float clickDownSeconds = 0.1f;

        [Header("Button SFX")]
        [SerializeField] private AudioClip buttonClickClip; // 패널 버튼 클릭음 — Inspector에서 할당 //안건준 추가 - 0628
        [SerializeField] [Range(0f, 1f)] private float buttonClickVolume = 1f;

        private readonly HashSet<Button> animatedButtons = new HashSet<Button>();
        private readonly Dictionary<Button, bool> hoverStates = new Dictionary<Button, bool>();
        private readonly Dictionary<Button, Vector3> buttonBaseScales = new Dictionary<Button, Vector3>();

        private bool suppressVolumeCallback;

        private void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            ResolveReferences();
            ConfigureRaycastTargets(); // 장식 Image Raycast 끔 — 버튼·슬라이더 클릭 통과 //안건준 추가 - 0628
            ConfigureSliders();
            WireButtons();
            RegisterAnimatedButtons();
            SyncVolumeSlidersFromAudio(); // 씬 슬라이더 기본값(0) 덮어쓰기 //안건준 추가 - 0629
        }

        private void OnEnable()
        {
            SyncVolumeSlidersFromAudio();
        }

        private void OnDisable()
        {
            KillButtonTweens();
        }

        public void Open()
        {
            panelRoot.SetActive(true);
            SyncVolumeSlidersFromAudio();
        }

        public void Close()
        {
            panelRoot.SetActive(false);
        }

        private void ResolveReferences()
        {
            Transform root = transform;

            if (masterVolumeSlider == null)
            {
                masterVolumeSlider = FindSlider(root, "Master Volume");
            }

            if (bgmVolumeSlider == null)
            {
                bgmVolumeSlider = FindSlider(root, "BGM Volume");
            }

            if (sfxVolumeSlider == null)
            {
                sfxVolumeSlider = FindSlider(root, "SFX Volume");
            }

            if (closeButton == null)
            {
                closeButton = FindButton(root, "CloseButton");
            }

            if (quitGameButton == null)
            {
                quitGameButton = FindButton(root, "Quit GameButton");
            }

            if (closeWindowButton == null)
            {
                closeWindowButton = FindButton(root, "Close WindowButton");
            }

            if (masterVolumePercentText == null)
            {
                masterVolumePercentText = FindNamedPercentText(root, "MasterValueText");
            }

            if (bgmVolumePercentText == null)
            {
                bgmVolumePercentText = FindNamedPercentText(root, "BGMValueText");
            }

            if (sfxVolumePercentText == null)
            {
                sfxVolumePercentText = FindNamedPercentText(root, "SFXValueText");
            }

            if (masterVolumePercentText == null && masterVolumeSlider != null)
            {
                masterVolumePercentText = FindPercentText(masterVolumeSlider.transform);
            }

            if (bgmVolumePercentText == null && bgmVolumeSlider != null)
            {
                bgmVolumePercentText = FindPercentText(bgmVolumeSlider.transform);
            }

            if (sfxVolumePercentText == null && sfxVolumeSlider != null)
            {
                sfxVolumePercentText = FindPercentText(sfxVolumeSlider.transform);
            }

            if (closeButton != null)
            {
                closeButton.transform.SetAsLastSibling(); // 닫기 버튼을 맨 앞(클릭 우선)으로 //안건준 추가 - 0628
            }
        }

        private void ConfigureRaycastTargets()
        {
            Transform root = panelRoot != null ? panelRoot.transform : transform;

            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null)
                {
                    continue;
                }

                image.raycastTarget = ShouldReceiveRaycast(image);
            }

            TextMeshProUGUI[] tmpLabels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < tmpLabels.Length; i++)
            {
                if (tmpLabels[i] != null)
                {
                    tmpLabels[i].raycastTarget = false;
                }
            }

            Text[] legacyLabels = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < legacyLabels.Length; i++)
            {
                if (legacyLabels[i] != null)
                {
                    legacyLabels[i].raycastTarget = false;
                }
            }
        }

        private bool ShouldReceiveRaycast(Image image)
        {
            if (IsButtonGraphic(image, closeButton)
                || IsButtonGraphic(image, quitGameButton)
                || IsButtonGraphic(image, closeWindowButton))
            {
                return true;
            }

            if (image.GetComponent<Button>() != null)
            {
                return true;
            }

            if (IsSliderTrackImage(image))
            {
                return true;
            }

            if (IsSliderHandleImage(image))
            {
                return true;
            }

            return false;
        }

        private static bool IsButtonGraphic(Image image, Button button)
        {
            return button != null && image.transform.IsChildOf(button.transform);
        }

        private bool IsSliderTrackImage(Image image)
        {
            if (image == null)
            {
                return false;
            }

            Transform parent = image.transform.parent;
            if (parent == null)
            {
                return false;
            }

            Slider slider = parent.GetComponentInParent<Slider>();
            if (slider == null)
            {
                return false;
            }

            return image.gameObject.name == "Background";
        }

        private bool IsSliderHandleImage(Image image)
        {
            if (image == null)
            {
                return false;
            }

            if (image.gameObject.name != "Handle")
            {
                return false;
            }

            return image.GetComponentInParent<Slider>() != null;
        }

        private static TextMeshProUGUI FindNamedPercentText(Transform root, string objectName)
        {
            Transform target = FindDeep(root, objectName);
            return target != null ? target.GetComponent<TextMeshProUGUI>() : null;
        }

        private void ConfigureSliders()
        {
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.minValue = 0f;
                masterVolumeSlider.maxValue = 1f;
                masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            }

            if (bgmVolumeSlider != null)
            {
                bgmVolumeSlider.minValue = 0f;
                bgmVolumeSlider.maxValue = 1f;
                bgmVolumeSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.minValue = 0f;
                sfxVolumeSlider.maxValue = 1f;
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            }
        }

        private void WireButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HandleCloseClicked);
            }

            if (quitGameButton != null)
            {
                quitGameButton.onClick.AddListener(HandleQuitGameClicked);
            }

            if (closeWindowButton != null)
            {
                closeWindowButton.onClick.AddListener(HandleCloseWindowClicked);
            }
        }

        private void RegisterAnimatedButtons()
        {
            RegisterAnimatedButton(closeButton);
            RegisterAnimatedButton(quitGameButton);
            RegisterAnimatedButton(closeWindowButton);
        }

        private void RegisterAnimatedButton(Button button)
        {
            if (button == null || !animatedButtons.Add(button))
            {
                return;
            }

            buttonBaseScales[button] = button.transform.localScale;

            SettingsPanelButtonHoverBridge bridge = button.GetComponent<SettingsPanelButtonHoverBridge>();
            if (bridge == null)
            {
                bridge = button.gameObject.AddComponent<SettingsPanelButtonHoverBridge>();
            }

            bridge.Initialize(this, button);
        }

        private Vector3 GetButtonBaseScale(Button button)
        {
            if (button != null && buttonBaseScales.TryGetValue(button, out Vector3 baseScale))
            {
                return baseScale;
            }

            return Vector3.one;
        }

        internal void SetHover(Button button, bool active)
        {
            if (button == null || !button.interactable)
            {
                return;
            }

            if (hoverStates.TryGetValue(button, out bool current) && current == active)
            {
                return;
            }

            hoverStates[button] = active;
            RectTransform rt = button.transform as RectTransform;
            if (rt == null)
            {
                return;
            }

            Vector3 baseScale = GetButtonBaseScale(button);
            rt.DOKill();
            rt.DOScale(active ? baseScale * hoverScale : baseScale, hoverDuration)
                .SetEase(active ? Ease.OutQuad : Ease.InQuad)
                .SetUpdate(true);
        }

        private void PlayClickFeedback(Button button)
        {
            PlayButtonClickSfx();
            PlayClickTween(button);
        }

        private void PlayButtonClickSfx()
        {
            if (buttonClickClip != null)
            {
                AudioManager.PlayUiSfxClip(buttonClickClip, buttonClickVolume);
                return;
            }

            AudioManager.EnsureExists()?.PlaySFX(SFXType.ClickButton);
        }

        private void PlayClickTween(Button button)
        {
            if (button == null)
            {
                return;
            }

            RectTransform rt = button.transform as RectTransform;
            if (rt == null)
            {
                return;
            }

            Vector3 baseScale = GetButtonBaseScale(button);
            hoverStates[button] = false;
            rt.DOKill();
            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Append(rt.DOScale(baseScale * clickScale, clickUpSeconds).SetEase(Ease.OutBack));
            sequence.Append(rt.DOScale(baseScale, clickDownSeconds));
        }

        private void KillButtonTweens()
        {
            foreach (Button button in animatedButtons)
            {
                if (button == null)
                {
                    continue;
                }

                RectTransform rt = button.transform as RectTransform;
                if (rt == null)
                {
                    continue;
                }

                rt.DOKill();
                rt.localScale = GetButtonBaseScale(button);
            }
        }

        private void SyncVolumeSlidersFromAudio()
        {
            suppressVolumeCallback = true;

            AudioManager.EnsureVolumePreferencesLoaded(); // 슬라이더 동기화 전 볼륨 선로드 //안건준 추가 - 0629

            float master = AudioManager.DefaultVolume;
            float bgm = AudioManager.DefaultVolume;
            float sfx = AudioManager.DefaultVolume;

            if (AudioManager.Instance != null)
            {
                master = AudioManager.Instance.MasterVolume;
                bgm = AudioManager.Instance.BgmVolume;
                sfx = AudioManager.Instance.SfxVolume;
            }
            else
            {
                master = AudioManager.GlobalMasterVolume;
                bgm = AudioManager.GlobalBgmVolume;
                sfx = AudioManager.GlobalSfxVolume;
            }

            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.SetValueWithoutNotify(master);
            }

            if (bgmVolumeSlider != null)
            {
                bgmVolumeSlider.SetValueWithoutNotify(bgm);
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.SetValueWithoutNotify(sfx);
            }

            UpdateVolumePercentText(masterVolumePercentText, master);
            UpdateVolumePercentText(bgmVolumePercentText, bgm);
            UpdateVolumePercentText(sfxVolumePercentText, sfx);

            suppressVolumeCallback = false;
        }

        private void OnMasterVolumeChanged(float value)
        {
            if (suppressVolumeCallback)
            {
                return;
            }

            float clamped = Mathf.Clamp01(value);
            AudioManager.SetGlobalMasterVolume(clamped);
            UpdateVolumePercentText(masterVolumePercentText, clamped);
        }

        private void OnBgmVolumeChanged(float value)
        {
            if (suppressVolumeCallback)
            {
                return;
            }

            float clamped = Mathf.Clamp01(value);
            AudioManager.SetGlobalBgmVolume(clamped);
            UpdateVolumePercentText(bgmVolumePercentText, clamped);
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (suppressVolumeCallback)
            {
                return;
            }

            float clamped = Mathf.Clamp01(value);
            AudioManager.SetGlobalSfxVolume(clamped);
            UpdateVolumePercentText(sfxVolumePercentText, clamped);
        }

        private void HandleCloseClicked()
        {
            PlayClickFeedback(closeButton);
            Close();
        }

        private void HandleCloseWindowClicked()
        {
            PlayClickFeedback(closeWindowButton);
            Close();
        }

        private void HandleQuitGameClicked()
        {
            PlayClickFeedback(quitGameButton);
            // 게임 포기 기능 — 추후 연결 //안건준 추가 - 0628
        }

        private static void UpdateVolumePercentText(TextMeshProUGUI label, float value)
        {
            if (label == null)
            {
                return;
            }

            label.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }

        private static TextMeshProUGUI FindPercentText(Transform sliderTransform)
        {
            Transform row = sliderTransform.parent;
            if (row == null)
            {
                return null;
            }

            TextMeshProUGUI[] labels = row.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                TextMeshProUGUI label = labels[i];
                if (label == null || label.transform == sliderTransform)
                {
                    continue;
                }

                if (label.text.Contains("%"))
                {
                    return label;
                }
            }

            return null;
        }

        private static Slider FindSlider(Transform root, string objectName)
        {
            Transform target = FindDeep(root, objectName);
            if (target == null)
            {
                return null;
            }

            return target.GetComponent<Slider>() ?? target.GetComponentInChildren<Slider>(true);
        }

        private static Button FindButton(Transform root, string objectName)
        {
            Transform target = FindDeep(root, objectName);
            return target != null ? target.GetComponent<Button>() : null;
        }

        private static Transform FindDeep(Transform root, string objectName)
        {
            if (root.name == objectName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private sealed class SettingsPanelButtonHoverBridge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            private SettingsPanel owner;
            private Button button;

            public void Initialize(SettingsPanel panel, Button target)
            {
                owner = panel;
                button = target;
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                owner?.SetHover(button, true);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                owner?.SetHover(button, false);
            }
        }
    }
}
