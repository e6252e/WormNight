using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    
    
    /// Hp1·Hp3 — 앞에서 깎임 / 뒤에서 가득 대기 (줄 소진 시 교체).
    /// Hp2 — 0.1초 유지 후 따라 깎이는 데미지 잔상.    
    public sealed class BossHpBarUI : MonoBehaviour
    {
        // 싱글톤 — 바인더가 Instance로 UI 접근
        public static BossHpBarUI Instance { get; private set; }
        
        /// Instance가 없을 때 씬에서 BossHpBarUI를 찾아 반환. 비활성 오브젝트 포함.        
        public static bool TryResolveInstance(out BossHpBarUI ui)
        {
            ui = Instance;
            if (ui != null)
            {
                return true;
            }

            // 비활성 HUD도 탐색
            ui = FindFirstObjectByType<BossHpBarUI>(FindObjectsInactive.Include);
            if (ui == null)
            {
                return false;
            }

            // 첫 발견 시 패널 활성화
            if (Instance == null)
            {
                ui.SetHudPanelActive(true);
            }

            ui = Instance ?? ui;
            return ui != null;
        }

        // 바인딩된 보스 체력
        private EnemyHealth health;

        // 피격 시 흔들림 대상 (BossHp RectTransform)
        private RectTransform shakeTarget;
        private Vector2 originalAnchoredPosition;

        [Header("Panel")]
        [SerializeField] private GameObject hudPanelRoot;

        [Header("Fill Layers — Fill Area 자식 Hp1/Hp2/Hp3")]
        [SerializeField] private Image hp1Fill;
        [SerializeField] private Image hp2Fill;
        [SerializeField] private Image hp3Fill;

        [Header("Stock Text")]
        [SerializeField] private TMP_Text hpStockText;

        [Header("Stock Setting")]
        [Min(1)]
        [SerializeField] private int totalStockCount = 50;

        [Header("Automatic Color")]
        [Range(0f, 1f)]
        [SerializeField] private float colorSaturation = 0.88f;

        [Range(0f, 1f)]
        [SerializeField] private float colorBrightness = 1f;

        [Range(0f, 1f)]
        [SerializeField] private float damageHighlightAmount = 0.65f;

        [Tooltip("이 줄 수 이하 — active·대기 모두 빨간색")]
        [Min(1)]
        [SerializeField] private int forceRedStockThreshold = 2;

        [Range(0f, 1f)]
        [SerializeField] private float waitingBrightnessScale = 0.9f;

        [Header("Damage Trail")]
        [Min(0f)]
        [SerializeField] private float damageTrailDelay = 0.1f;

        [Min(0.01f)]
        [SerializeField] private float damageTrailDuration = 0.3f;

        [Header("Hit Shake (DOTween — BossHp)")]
        [Min(0f)]
        [SerializeField] private float shakeDuration = 0.18f;

        [Min(0f)]
        [SerializeField] private float shakeStrength = 14f;

        [Min(1)]
        [SerializeField] private int shakeVibrato = 28;

        [Range(0f, 180f)]
        [SerializeField] private float shakeRandomness = 90f;

        // true: Hp1=앞(깎임), Hp3=뒤(대기) / false: 역할 교체
        private bool frontIsHp1 = true;

        // 한 줄당 HP량
        private float hpPerStock;
        private float previousHp;
        private int previousStockCount;
        private float previousFillAmount;

        private Coroutine damageTrailCoroutine;
        private Tween shakeTween;

        private void Awake()
        {
            Instance = this;
            ResolveShakeTarget();
            ResolveFillReferences();
            SetupFillImages();
            DisableLegacyFillOverlay();

            ClearVisuals();
            SetHudPanelActive(false);
        }

        // 매 프레임 레이어 그리기 순서 갱신 (뒤→Hp2→앞)
        private void LateUpdate()
        {
            if (health == null)
            {
                return;
            }

            ApplyLayerDrawOrder();
        }

        // 체력 변화 감지 → 레이어 갱신, 데미지 잔상, 흔들림
        private void Update()
        {
            if (health == null)
            {
                return;
            }

            hpPerStock = Mathf.Max(1f, health.MaxHp / totalStockCount);

            float currentHp = Mathf.Clamp(health.CurrentHp, 0f, health.MaxHp);
            int currentStockCount = GetCurrentStockCount(currentHp);
            float currentFillAmount = GetCurrentFillAmount(currentHp, currentStockCount);

            bool tookDamage = currentHp < previousHp - 0.001f;
            bool healed = currentHp > previousHp + 0.001f;
            bool stockLineBroken = currentStockCount < previousStockCount && currentStockCount > 0;
            int brokenStockLines = stockLineBroken ? previousStockCount - currentStockCount : 0;

            // 홀수 줄 깨짐 시 Hp1/Hp3 앞·뒤 역할 교체
            if (brokenStockLines > 0 && brokenStockLines % 2 != 0)
            {
                frontIsHp1 = !frontIsHp1;
            }

            if (tookDamage)
            {
                StartDamageTrail(currentFillAmount, currentStockCount, stockLineBroken);
                StartShake();
            }
            else if (healed)
            {
                StopDamageTrail();
                SyncTrailFill(currentFillAmount, currentStockCount);
            }

            RefreshLayers(currentStockCount, currentFillAmount);

            previousHp = currentHp;
            previousStockCount = currentStockCount;
            previousFillAmount = currentFillAmount;
        }

        private void OnDisable()
        {
            StopDamageTrail();
            StopShake();
        }

        private void OnDestroy()
        {
            StopShake();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        //보스 체력 연결 및 HUD 표시.
        public void Bind(EnemyHealth targetHealth)
        {
            SetHudPanelActive(true);
            StopDamageTrail();
            StopShake();

            health = targetHealth;
            frontIsHp1 = true;

            ResolveFillReferences();
            SetupFillImages();

            if (health == null)
            {
                ClearVisuals();
                return;
            }

            hpPerStock = Mathf.Max(1f, health.MaxHp / totalStockCount);

            float currentHp = Mathf.Clamp(health.CurrentHp, 0f, health.MaxHp);
            int currentStockCount = GetCurrentStockCount(currentHp);
            float currentFillAmount = GetCurrentFillAmount(currentHp, currentStockCount);

            previousHp = currentHp;
            previousStockCount = currentStockCount;
            previousFillAmount = currentFillAmount;

            if (shakeTarget != null)
            {
                originalAnchoredPosition = shakeTarget.anchoredPosition;
            }

            ApplyLayerDrawOrder();
            RefreshLayers(currentStockCount, currentFillAmount);
            SyncTrailFill(currentFillAmount, currentStockCount);
        }

        //보스 체력 연결 해제 및 HUD 숨김.
        public void Unbind()
        {
            StopDamageTrail();
            StopShake();

            health = null;
            frontIsHp1 = true;
            previousHp = 0f;
            previousStockCount = 0;
            previousFillAmount = 0f;

            ClearVisuals();
            SetHudPanelActive(false);
        }

        // 현재 앞 레이어(깎이는 바) Image
        private Image GetFrontFill() => frontIsHp1 ? hp1Fill : hp3Fill;

        // 현재 뒤 레이어(대기 바) Image
        private Image GetBackFill() => frontIsHp1 ? hp3Fill : hp1Fill;

        // Hierarchy 순서: 뒤(Hp1/Hp3) → Hp2(잔상) → 앞(Hp1/Hp3)
        private void ApplyLayerDrawOrder()
        {
            Image back = GetBackFill();
            Image front = GetFrontFill();

            if (back != null)
            {
                back.transform.SetAsFirstSibling();
            }

            if (hp2Fill != null)
            {
                hp2Fill.transform.SetSiblingIndex(back != null ? 1 : 0);
            }

            if (front != null)
            {
                front.transform.SetAsLastSibling();
            }
        }

        // 앞=현재 fill·색, 뒤=다음 줄 가득·대기색, 텍스트=×줄 수
        private void RefreshLayers(int currentStockCount, float currentFillAmount)
        {
            if (currentStockCount <= 0)
            {
                SetFillAmount(hp1Fill, 0f);
                SetFillAmount(hp2Fill, 0f);
                SetFillAmount(hp3Fill, 0f);

                if (hpStockText != null)
                {
                    hpStockText.text = string.Empty;
                }

                return;
            }

            Image frontFill = GetFrontFill();
            Image backFill = GetBackFill();
            Color activeColor = GetStockColor(currentStockCount);

            if (frontFill != null)
            {
                frontFill.fillAmount = currentFillAmount;
                frontFill.color = activeColor;
            }

            if (backFill != null)
            {
                if (currentStockCount > 1)
                {
                    backFill.fillAmount = 1f;
                    backFill.color = GetWaitingStockColor(currentStockCount);
                }
                else
                {
                    backFill.fillAmount = 0f;
                }
            }

            if (hpStockText != null)
            {
                hpStockText.text = $"×{currentStockCount}";
            }
        }

        // Hp2 데미지 잔상 시작 — 피격 직후 이전 fill 유지, delay 후 따라 깎임
        private void StartDamageTrail(float targetFillAmount, int currentStockCount, bool stockLineBroken)
        {
            if (hp2Fill == null)
            {
                return;
            }

            StopDamageTrail();

            int trailStock = currentStockCount > 0 ? currentStockCount : Mathf.Max(1, previousStockCount);
            Color trailColor = GetStockColor(trailStock);
            hp2Fill.color = Color.Lerp(trailColor, Color.white, damageHighlightAmount);

            // 줄 깨짐 시 잔상은 한 줄 가득에서 시작
            if (stockLineBroken)
            {
                hp2Fill.fillAmount = 1f;
            }
            else
            {
                hp2Fill.fillAmount = Mathf.Max(hp2Fill.fillAmount, previousFillAmount);
            }

            damageTrailCoroutine = StartCoroutine(DamageTrailRoutine(targetFillAmount));
        }

        // delay 대기 → ease-out으로 Hp2 fillAmount를 목표값까지 보간
        private IEnumerator DamageTrailRoutine(float targetFillAmount)
        {
            float delayElapsed = 0f;
            while (delayElapsed < damageTrailDelay)
            {
                delayElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            float startFillAmount = hp2Fill != null ? hp2Fill.fillAmount : targetFillAmount;
            float elapsed = 0f;

            while (elapsed < damageTrailDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float ratio = Mathf.Clamp01(elapsed / damageTrailDuration);
                float easedRatio = 1f - Mathf.Pow(1f - ratio, 3f);

                if (hp2Fill != null)
                {
                    hp2Fill.fillAmount = Mathf.Lerp(startFillAmount, targetFillAmount, easedRatio);
                }

                yield return null;
            }

            if (hp2Fill != null)
            {
                hp2Fill.fillAmount = targetFillAmount;
            }

            damageTrailCoroutine = null;
        }

        // 회복 시 Hp2를 현재 fill·색과 동기화
        private void SyncTrailFill(float currentFillAmount, int currentStockCount)
        {
            if (hp2Fill == null)
            {
                return;
            }

            Color currentColor = GetStockColor(Mathf.Max(1, currentStockCount));
            hp2Fill.fillAmount = currentFillAmount;
            hp2Fill.color = Color.Lerp(currentColor, Color.white, damageHighlightAmount);
        }

        //mage Filled 설정·Slider 비활성만. RectTransform/크기는 Inspector 값 유지.
        private void SetupFillImages()
        {
            DisableBossHpSlider();
            SyncFillSpritesFromHp2();
        }

        // Slider가 Fill Rect를 덮어쓰지 않도록 비활성화
        private void DisableBossHpSlider()
        {
            Transform bossHp = ResolveBossHpTransform();
            if (bossHp == null)
            {
                return;
            }

            Slider slider = bossHp.GetComponent<Slider>();
            if (slider == null)
            {
                return;
            }

            slider.fillRect = null;
            slider.handleRect = null;
            slider.interactable = false;
            slider.enabled = false;
        }

        // Hp1 부모 체인 또는 이름으로 BossHp Transform 탐색
        private Transform ResolveBossHpTransform()
        {
            if (hp1Fill != null && hp1Fill.transform.parent != null && hp1Fill.transform.parent.parent != null)
            {
                return hp1Fill.transform.parent.parent;
            }

            Transform bossHp = transform.Find("BossHp");
            if (bossHp != null)
            {
                return bossHp;
            }

            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name.Equals("BossHp", System.StringComparison.OrdinalIgnoreCase))
                {
                    return all[i];
                }
            }

            return null;
        }

        //Hp2 Image에 설정된 Source Image를 기준으로 Hp1/Hp3 Filled 설정.
        private void SyncFillSpritesFromHp2()
        {
            if (hp2Fill != null)
            {
                ConfigureFillImage(hp2Fill);
            }

            Sprite referenceSprite = hp2Fill != null && hp2Fill.sprite != null
                ? hp2Fill.sprite
                : GetFallbackFillSprite();

            ApplySharedFillSprite(hp1Fill, referenceSprite);
            ApplySharedFillSprite(hp3Fill, referenceSprite);
        }

        // Hp2와 동일 스프라이트·Filled 설정 적용
        private static void ApplySharedFillSprite(Image image, Sprite referenceSprite)
        {
            if (image == null)
            {
                return;
            }

            if (referenceSprite != null)
            {
                image.sprite = referenceSprite;
            }

            ConfigureFillImage(image);
        }

        // Hp2 스프라이트 미설정 시 Unity 기본 UI 스프라이트 사용
        private static Sprite GetFallbackFillSprite()
        {
            Sprite sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            if (sprite != null)
            {
                return sprite;
            }

            return Resources.GetBuiltinResource<Sprite>("UISprite.psd");
        }

        // DOTween 흔들림 대상 — BossHp RectTransform
        private void ResolveShakeTarget()
        {
            Transform bossHp = transform.Find("BossHp");
            if (bossHp == null)
            {
                Transform[] all = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].name.Equals("BossHp", System.StringComparison.OrdinalIgnoreCase))
                    {
                        bossHp = all[i];
                        break;
                    }
                }
            }

            shakeTarget = bossHp != null ? bossHp as RectTransform : GetComponent<RectTransform>();

            if (shakeTarget != null)
            {
                originalAnchoredPosition = shakeTarget.anchoredPosition;
            }
        }

        // HUD 루트 — 미할당 시 자기 자신
        private GameObject ResolveHudPanelRoot()
        {
            if (hudPanelRoot != null)
            {
                return hudPanelRoot;
            }

            return gameObject;
        }

        // 보스 등장/퇴장 시 HUD 패널 표시·숨김
        private void SetHudPanelActive(bool active)
        {
            GameObject root = ResolveHudPanelRoot();
            if (root != null && root.activeSelf != active)
            {
                root.SetActive(active);
            }
        }

        // Inspector 미연결 시 Hp1/Hp2/Hp3/BossText 자동 탐색
        private void ResolveFillReferences()
        {
            Transform root = transform;
            if (hp1Fill == null)
            {
                hp1Fill = FindFillImage(root, "Hp1");
            }

            if (hp2Fill == null)
            {
                hp2Fill = FindFillImage(root, "Hp2");
            }

            if (hp3Fill == null)
            {
                hp3Fill = FindFillImage(root, "Hp3");
            }

            if (hpStockText == null)
            {
                Transform textTransform = FindDeepChild(root, "BossText");
                if (textTransform != null)
                {
                    hpStockText = textTransform.GetComponentInChildren<TMP_Text>(true);
                }
            }
        }

        // 자식 계층에서 이름으로 Transform 검색
        private static Transform FindDeepChild(Transform root, string objectName)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return all[i];
                }
            }

            return null;
        }

        // 자식 계층에서 이름으로 Image(Fill) 검색
        private static Image FindFillImage(Transform root, string objectName)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    Image image = all[i].GetComponent<Image>();
                    if (image != null)
                    {
                        return image;
                    }
                }
            }

            return null;
        }

        // 예전 Slider Fill 오버레이 비활성화 (Hp1/2/3 외 Fill* Image)
        private void DisableLegacyFillOverlay()
        {
            Transform fillArea = hp1Fill != null ? hp1Fill.transform.parent : null;
            Transform bossHp = fillArea != null ? fillArea.parent : null;
            if (bossHp == null)
            {
                return;
            }

            for (int i = 0; i < bossHp.childCount; i++)
            {
                Transform child = bossHp.GetChild(i);
                if (!child.name.StartsWith("Fill", System.StringComparison.OrdinalIgnoreCase)
                    || child.name.Contains("Area", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Image legacy = child.GetComponent<Image>();
                if (legacy != null && legacy != hp1Fill && legacy != hp2Fill && legacy != hp3Fill)
                {
                    legacy.enabled = false;
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void StopDamageTrail()
        {
            if (damageTrailCoroutine == null)
            {
                return;
            }

            StopCoroutine(damageTrailCoroutine);
            damageTrailCoroutine = null;
        }

        // 피격 시 BossHp 패널 DOTween 흔들림 (unscaledTime)
        private void StartShake()
        {
            if (shakeTarget == null)
            {
                return;
            }

            StopShake();
            shakeTarget.anchoredPosition = originalAnchoredPosition;

            shakeTween = shakeTarget
                .DOShakeAnchorPos(
                    shakeDuration,
                    shakeStrength,
                    shakeVibrato,
                    shakeRandomness,
                    snapping: false,
                    fadeOut: true)
                .SetUpdate(true)
                .OnComplete(ResetShakePosition);
        }

        // 흔들림 종료 후 원래 anchoredPosition 복원
        private void ResetShakePosition()
        {
            shakeTween = null;

            if (shakeTarget != null)
            {
                shakeTarget.anchoredPosition = originalAnchoredPosition;
            }
        }

        private void StopShake()
        {
            if (shakeTween != null && shakeTween.IsActive())
            {
                shakeTween.Kill();
            }

            shakeTween = null;

            if (shakeTarget != null)
            {
                shakeTarget.anchoredPosition = originalAnchoredPosition;
            }
        }

        // 현재 HP를 totalStockCount 줄로 나눈 남은 줄 수
        private int GetCurrentStockCount(float currentHp)
        {
            if (currentHp <= 0f)
            {
                return 0;
            }

            int stockCount = Mathf.CeilToInt(currentHp / hpPerStock);
            return Mathf.Clamp(stockCount, 0, totalStockCount);
        }

        // 현재 줄 안에서의 fillAmount (0~1)
        private float GetCurrentFillAmount(float currentHp, int currentStockCount)
        {
            if (currentStockCount <= 0)
            {
                return 0f;
            }

            float stockStartHp = (currentStockCount - 1) * hpPerStock;
            float stockCapacity = Mathf.Min(hpPerStock, health.MaxHp - stockStartHp);

            if (stockCapacity <= 0f)
            {
                return 0f;
            }

            float hpInsideStock = currentHp - stockStartHp;
            return Mathf.Clamp01(hpInsideStock / stockCapacity);
        }

        // 줄 수에 따른 HP 바 색상 (마지막 N줄은 강제 빨강)
        private Color GetStockColor(int stockCount)
        {
            if (stockCount <= 0 || totalStockCount <= 0)
            {
                return Color.clear;
            }

            if (stockCount <= forceRedStockThreshold)
            {
                return Color.HSVToRGB(0f, colorSaturation, colorBrightness);
            }

            float ratio = GetStockRatio(stockCount);
            float hue = SampleStockHue(ratio);
            return Color.HSVToRGB(hue, colorSaturation, colorBrightness);
        }

        //뒤 대기 줄 — 다음 stock 색 (약간 어둡게).
        private Color GetWaitingStockColor(int activeStockCount)
        {
            int waitingStock = Mathf.Max(1, activeStockCount - 1);
            Color waiting = GetStockColor(waitingStock);

            Color.RGBToHSV(waiting, out float h, out float s, out float v);
            v = Mathf.Clamp01(v * waitingBrightnessScale);
            return Color.HSVToRGB(h, s, v);
        }

        // stockCount를 0~1 비율로 변환 (색상 그라데이션용)
        private float GetStockRatio(int stockCount)
        {
            if (totalStockCount <= 1)
            {
                return 0f;
            }

            return (stockCount - 1f) / (totalStockCount - 1f);
        }

        //보라(1) → 파랑 → 초록 → 노랑 → 빨강(0) 5구간 Hue.
        private static float SampleStockHue(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);

            const float hueRed = 0f;
            const float hueYellow = 0.14f;
            const float hueGreen = 0.33f;
            const float hueBlue = 0.58f;
            const float huePurple = 0.78f;

            if (ratio >= 0.75f)
            {
                return Mathf.Lerp(hueBlue, huePurple, (ratio - 0.75f) / 0.25f);
            }

            if (ratio >= 0.5f)
            {
                return Mathf.Lerp(hueGreen, hueBlue, (ratio - 0.5f) / 0.25f);
            }

            if (ratio >= 0.25f)
            {
                return Mathf.Lerp(hueYellow, hueGreen, (ratio - 0.25f) / 0.25f);
            }

            return Mathf.Lerp(hueRed, hueYellow, ratio / 0.25f);
        }

        // Image를 Horizontal Filled로 설정
        private static void ConfigureFillImage(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillClockwise = true;
            image.preserveAspect = false;
            image.raycastTarget = false;
            image.enabled = true;
        }

        private static void SetFillAmount(Image image, float amount)
        {
            if (image != null)
            {
                image.fillAmount = amount;
            }
        }

        // 바인딩 해제·초기화 시 fill·텍스트 초기화
        private void ClearVisuals()
        {
            SetFillAmount(hp1Fill, 0f);
            SetFillAmount(hp2Fill, 0f);
            SetFillAmount(hp3Fill, 0f);

            if (hpStockText != null)
            {
                hpStockText.text = string.Empty;
            }
        }
    }
}
