using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    public sealed class SettingsPopup : MonoBehaviour // 인게임 설정 팝업 //안건준 추가 - 0628
    {
        private const string SpeedPrefKey = "Settings.GameSpeed2X";
        private const string SpeedTextNormal = "1배속";
        private const string SpeedTextFast = "2배속";

        [Header("Panel")]
        [SerializeField] private GameObject panelRoot; // Settings 또는 Panel

        [Header("Volume")]
        [SerializeField] private Slider masterVolumeSlider; // Master Volume 슬라이더 (선택) //안건준 추가 - 0628
        [SerializeField] private Slider bgmVolumeSlider; // BGM Volume 슬라이더
        [SerializeField] private Slider sfxVolumeSlider; // SFX Volume 슬라이더

        [Header("Buttons")]
        [SerializeField] private Button closeButton; // CloseButton
        [SerializeField] private Button retryButton; // Retry
        [SerializeField] private Button speedButton; // 2X_Speed

        [Header("Auto Mode")]
        [SerializeField] private ConvoyController convoy; // 자동궤도(오토모드) 확인용

        [Header("Speed Button Visual")]
        [SerializeField] private Image speedButtonIcon; // 2X_Speed의 Image. 비우면 Button Target Graphic 자동 사용
        [SerializeField] private TextMeshProUGUI speedText; // SpeedText
        [SerializeField] private Sprite speedIconNormal; // White Right (1배속)
        [SerializeField] private Sprite speedIconFast; // White FastFordward (2배속)
        [SerializeField] private Sprite speedIconDisabled; // 자동모드 OFF — 어두운 화살표 (없으면 Normal + 어두운 색)
        [SerializeField] private Color speedDisabledIconColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color speedDisabledTextColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        [SerializeField] private Color speedEnabledTextColor = Color.white;

        [Header("Icon Button Layout")]
        [SerializeField] private Vector3 iconButtonScale = new Vector3(1f, 4f, 1f); // Retry·2X_Speed Inspector 배율 //안건준 추가 - 0628

        [Header("Button Animation")]
        [SerializeField] private float hoverScale = 1.09f;
        [SerializeField] private float clickScale = 1.2f;
        [SerializeField] private float hoverDuration = 0.15f;
        [SerializeField] private float clickUpSeconds = 0.15f;
        [SerializeField] private float clickDownSeconds = 0.1f;

        private readonly HashSet<Button> animatedButtons = new HashSet<Button>();
        private readonly Dictionary<Button, bool> hoverStates = new Dictionary<Button, bool>();
        private readonly Dictionary<Button, Vector3> buttonBaseScales = new Dictionary<Button, Vector3>();

        private Transform retryIconTransform;
        private Transform speedIconTransform;

        private bool isDoubleSpeed;
        private bool suppressVolumeCallback;
        private bool lastAutoOrbitActive;

        private void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            ResolveReferences();
            ApplyIconButtonLayout();
            ConfigureSliders();
            WireButtons();
            RegisterAnimatedButtons();
            LoadSpeedPreference();
            RefreshSpeedButtonForAutoMode(true);
            SyncVolumeSlidersFromAudio(); // 씬 슬라이더 기본값(0) 덮어쓰기 //안건준 추가 - 0629
        }

        private void Start()
        {
            ApplyIconButtonLayout(); // Layout 재계산 후 Retry·2X_Speed 배율 재적용 //안건준 추가 - 0628
        }

        private void OnEnable()
        {
            ApplyIconButtonLayout();
            SyncVolumeSlidersFromAudio();
            RefreshSpeedButtonForAutoMode(true);
        }

        private void Update()
        {
            bool autoActive = IsAutoOrbitActive();
            if (autoActive != lastAutoOrbitActive)
            {
                RefreshSpeedButtonForAutoMode(true);
            }
        }

        private void OnDisable()
        {
            KillButtonTweens();
        }

        public void Open()
        {
            panelRoot.SetActive(true);
            ApplyIconButtonLayout();
            SyncVolumeSlidersFromAudio();
            RefreshSpeedButtonForAutoMode(true);
        }

        public void Close()
        {
            panelRoot.SetActive(false);
        }

        private void ResolveReferences()
        {
            Transform root = transform;

            if (convoy == null)
            {
                convoy = FindFirstObjectByType<ConvoyController>();
            }

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

            if (retryButton == null)
            {
                retryButton = FindButton(root, "Retry");
            }

            if (speedButton == null)
            {
                speedButton = FindButton(root, "2X_Speed");
            }

            retryIconTransform = retryButton != null ? retryButton.transform : FindDeep(root, "Retry");
            speedIconTransform = speedButton != null ? speedButton.transform : FindDeep(root, "2X_Speed");

            if (speedButton != null)
            {
                if (speedButtonIcon == null)
                {
                    speedButtonIcon = speedButton.targetGraphic as Image;
                }

                if (speedButtonIcon == null)
                {
                    speedButtonIcon = speedButton.GetComponent<Image>();
                }

                if (speedText == null)
                {
                    Transform textTransform = FindDeep(root, "SpeedText");
                    if (textTransform != null)
                    {
                        speedText = textTransform.GetComponent<TextMeshProUGUI>();
                    }
                }
            }
        }

        private void ApplyIconButtonLayout()
        {
            ApplyIconButtonLayout(retryButton, retryIconTransform);
            ApplyIconButtonLayout(speedButton, speedIconTransform);
        }

        private void ApplyIconButtonLayout(Button button, Transform iconTransform)
        {
            if (iconTransform == null)
            {
                return;
            }

            iconTransform.localScale = iconButtonScale;

            if (button != null && IsIconButton(button))
            {
                buttonBaseScales[button] = iconButtonScale;
            }

            Image image = button != null
                ? button.targetGraphic as Image ?? button.GetComponent<Image>()
                : iconTransform.GetComponent<Image>();

            if (image != null)
            {
                image.preserveAspect = false; // Inspector와 동일 — 비율 유지 끔 //안건준 수정 - 0628
            }
        }

        private bool IsIconButton(Button button)
        {
            return button == retryButton || button == speedButton;
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

            if (retryButton != null)
            {
                retryButton.onClick.AddListener(HandleRetryClicked);
            }

            if (speedButton != null)
            {
                speedButton.onClick.AddListener(HandleSpeedClicked);
            }
        }

        private void RegisterAnimatedButtons()
        {
            RegisterAnimatedButton(closeButton);
            RegisterAnimatedButton(retryButton);
            RegisterAnimatedButton(speedButton);
        }

        private void RegisterAnimatedButton(Button button)
        {
            if (button == null || !animatedButtons.Add(button))
            {
                return;
            }

            buttonBaseScales[button] = IsIconButton(button)
                ? iconButtonScale
                : button.transform.localScale;

            SettingsButtonHoverBridge bridge = button.GetComponent<SettingsButtonHoverBridge>();
            if (bridge == null)
            {
                bridge = button.gameObject.AddComponent<SettingsButtonHoverBridge>();
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

            suppressVolumeCallback = false;
        }

        private void OnMasterVolumeChanged(float value)
        {
            if (suppressVolumeCallback)
            {
                return;
            }

            AudioManager.SetGlobalMasterVolume(Mathf.Clamp01(value));
        }

        private void OnBgmVolumeChanged(float value)
        {
            if (suppressVolumeCallback)
            {
                return;
            }

            float clamped = Mathf.Clamp01(value);
            AudioManager.SetGlobalBgmVolume(clamped);
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (suppressVolumeCallback)
            {
                return;
            }

            float clamped = Mathf.Clamp01(value);
            AudioManager.SetGlobalSfxVolume(clamped);
        }

        private void HandleCloseClicked()
        {
            PlayClickFeedback(closeButton);
            Close();
        }

        private void HandleRetryClicked()
        {
            PlayClickFeedback(retryButton);
            Time.timeScale = 1f;
            PlayerPrefs.SetInt(SpeedPrefKey, 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void HandleSpeedClicked()
        {
            if (!IsAutoOrbitActive())
            {
                RefreshSpeedButtonForAutoMode(true);
                return;
            }

            PlayClickFeedback(speedButton);
            isDoubleSpeed = !isDoubleSpeed;
            ApplySpeedState(true);
        }

        private bool IsAutoOrbitActive()
        {
            if (convoy == null)
            {
                convoy = FindFirstObjectByType<ConvoyController>();
            }

            return convoy != null && convoy.IsAutoOrbitActive;
        }

        private void LoadSpeedPreference()
        {
            isDoubleSpeed = PlayerPrefs.GetInt(SpeedPrefKey, 0) == 1;
        }

        private void RefreshSpeedButtonForAutoMode(bool forceVisualRefresh)
        {
            bool autoActive = IsAutoOrbitActive();
            if (!forceVisualRefresh && autoActive == lastAutoOrbitActive)
            {
                return;
            }

            lastAutoOrbitActive = autoActive;

            if (speedButton != null)
            {
                speedButton.interactable = autoActive;
            }

            if (!autoActive)
            {
                if (isDoubleSpeed)
                {
                    isDoubleSpeed = false;
                    PlayerPrefs.SetInt(SpeedPrefKey, 0);
                    PlayerPrefs.Save();
                }

                Time.timeScale = 1f;
                ApplyDisabledSpeedVisual();
                return;
            }

            ApplySpeedState(false);
        }

        private void ApplyDisabledSpeedVisual()
        {
            if (speedButtonIcon != null)
            {
                speedButtonIcon.sprite = speedIconDisabled != null ? speedIconDisabled : speedIconNormal;
                speedButtonIcon.color = speedIconDisabled != null ? Color.white : speedDisabledIconColor;
            }

            if (speedText != null)
            {
                speedText.text = SpeedTextNormal;
                speedText.color = speedDisabledTextColor;
            }
        }

        private void ApplySpeedState(bool savePreference)
        {
            if (!IsAutoOrbitActive())
            {
                ApplyDisabledSpeedVisual();
                return;
            }

            Time.timeScale = isDoubleSpeed ? 2f : 1f;

            if (speedButtonIcon != null)
            {
                speedButtonIcon.color = Color.white;

                if (isDoubleSpeed && speedIconFast != null)
                {
                    speedButtonIcon.sprite = speedIconFast;
                }
                else if (speedIconNormal != null)
                {
                    speedButtonIcon.sprite = speedIconNormal;
                }
            }

            if (speedText != null)
            {
                speedText.color = speedEnabledTextColor;
                speedText.text = isDoubleSpeed ? SpeedTextFast : SpeedTextNormal;
            }

            if (savePreference)
            {
                PlayerPrefs.SetInt(SpeedPrefKey, isDoubleSpeed ? 1 : 0);
                PlayerPrefs.Save();
            }
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
            AudioManager.Instance?.PlaySFX(SFXType.ClickButton);
            PlayClickTween(button);
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
                rt.localScale = IsIconButton(button) ? iconButtonScale : GetButtonBaseScale(button);
            }
        }

        private sealed class SettingsButtonHoverBridge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            private SettingsPopup owner;
            private Button button;

            public void Initialize(SettingsPopup popup, Button target)
            {
                owner = popup;
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
