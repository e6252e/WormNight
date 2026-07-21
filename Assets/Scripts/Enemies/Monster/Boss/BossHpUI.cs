using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    public sealed class BossHpUI : MonoBehaviour // 보스 전용 화면 HP UI
    {
        public static BossHpUI Instance { get; private set; }

        private EnemyHealth health; // 런타임에 생성된 보스의 EnemyHealth
        private RectTransform shakeTarget; // 흔들림을 적용할 BossHpPanel
        private Vector2 originalAnchoredPosition; // BossHpPanel의 원래 UI 위치

        [Header("Gauge")]
        [SerializeField] private Image hpNextFill; // 현재 스톡 뒤에 보일 다음 스톡 색상
        [SerializeField] private Image hpDamageFill; // 피해 전 게이지가 잠시 남는 잔상
        [SerializeField] private Image hpFill; // 실제 현재 HP 게이지
        [SerializeField] private TMP_Text hpStockText; // ×숫자 표시

        [Header("Stock Setting")]
        [Min(1.0f)]
        [SerializeField] private float hpPerStock = 1000.0f; // HP 1000당 스톡 1개

        [Header("Automatic Color")]
        [Range(0.0f, 1.0f)]
        [SerializeField] private float maximumHue = 0.78f; // 최대 스톡의 보라색 계열 Hue

        [Range(0.0f, 1.0f)]
        [SerializeField] private float colorSaturation = 0.85f; // 자동 색상의 채도

        [Range(0.0f, 1.0f)]
        [SerializeField] private float colorBrightness = 1.0f; // 자동 색상의 밝기

        [Range(0.0f, 1.0f)]
        [SerializeField] private float damageHighlightAmount = 0.65f; // 잔상 색상을 흰색 쪽으로 밝힐 정도

        [Header("Damage Trail")]
        [Min(0.0f)]
        [SerializeField] private float damageTrailDelay = 0.1f; // 피해 후 잔상이 움직이기 전 대기 시간

        [Min(0.01f)]
        [SerializeField] private float damageTrailDuration = 0.3f; // 잔상이 실제 HP를 따라가는 시간

        [Header("Hit Shake")]
        [Min(0.0f)]
        [SerializeField] private float shakeDuration = 0.12f; // HP바 흔들림 지속 시간

        [Min(0.0f)]
        [SerializeField] private float shakeStrength = 12.0f; // HP바 흔들림 거리

        private float previousHp; // 직전 프레임의 보스 HP
        private int previousStockCount; // 직전 프레임의 남은 스톡 수
        private float previousFillAmount; // 직전 프레임의 현재 스톡 게이지 비율

        private Coroutine damageTrailCoroutine; // 현재 실행 중인 피해 잔상 Coroutine
        private Coroutine shakeCoroutine; // 현재 실행 중인 흔들림 Coroutine

        private void Awake()
        {
            Instance = this; // 다른 Script가 현재 BossHpUI에 접근할 수 있도록 저장한다.

            shakeTarget = GetComponent<RectTransform>(); // BossHpPanel의 RectTransform을 가져온다.

            if (shakeTarget != null)
            {
                originalAnchoredPosition = shakeTarget.anchoredPosition; // 흔들린 뒤 돌아올 원래 위치를 저장한다.
            }

            ConfigureFillImage(hpNextFill); // 다음 스톡 Image의 Filled 설정을 맞춘다.
            ConfigureFillImage(hpDamageFill); // 피해 잔상 Image의 Filled 설정을 맞춘다.
            ConfigureFillImage(hpFill); // 실제 HP Image의 Filled 설정을 맞춘다.

            ClearVisuals(); // 보스가 연결되기 전에는 게이지와 숫자를 비운다.
            gameObject.SetActive(false); //보스 UI화면을 끈다.
        }

        private void Update()
        {
            if (health == null)
            {
                return; // 연결된 보스가 없으면 갱신하지 않는다.
            }

            float currentHp = Mathf.Clamp(health.CurrentHp, 0.0f, health.MaxHp); // 현재 보스 HP를 가져온다.
            int totalStockCount = GetTotalStockCount(); // 최대 HP 기준 전체 스톡 수를 계산한다.
            int currentStockCount = GetCurrentStockCount(currentHp, totalStockCount); // 현재 남은 스톡 수를 계산한다.
            float currentFillAmount = GetCurrentFillAmount(currentHp, currentStockCount); // 현재 스톡의 게이지 비율을 계산한다.

            if (currentHp < previousHp - 0.001f) // 직전 프레임보다 HP가 감소했다면
            {
                StartDamageFeedback(currentStockCount, currentFillAmount, totalStockCount); // 잔상과 흔들림을 시작한다.
            }
            else if (currentHp > previousHp + 0.001f) // 성장이나 회복으로 HP가 증가했다면
            {
                StopDamageTrail(); // 이전 피해 잔상을 중단한다.
                SyncDamageFill(currentStockCount, currentFillAmount, totalStockCount); // 잔상도 증가한 HP에 즉시 맞춘다.
            }

            RefreshCurrentGauge(currentStockCount, currentFillAmount, totalStockCount); // 실제 게이지와 스톡 숫자를 갱신한다.

            previousHp = currentHp; // 다음 프레임 비교를 위해 현재 HP를 저장한다.
            previousStockCount = currentStockCount; // 다음 프레임 비교를 위해 현재 스톡 수를 저장한다.
            previousFillAmount = currentFillAmount; // 다음 프레임 비교를 위해 현재 게이지 비율을 저장한다.
        }

        private void OnDisable()
        {
            StopDamageTrail(); // UI가 비활성화될 때 피해 잔상을 중단한다.
            StopShake(); // UI가 비활성화될 때 흔들림을 중단하고 위치를 복구한다.
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null; // 제거된 UI 참조가 남지 않도록 정리한다.
            }
        }

        public void Bind(EnemyHealth targetHealth) // 생성된 보스의 EnemyHealth를 연결하는 함수
        {
            gameObject.SetActive(true); //보스 등장시 UI화면을 킨다.

            StopDamageTrail(); // 이전 보스의 잔상 Coroutine을 정리한다.
            StopShake(); // 이전 보스의 흔들림 Coroutine을 정리한다.

            health = targetHealth; // 생성된 보스의 EnemyHealth를 저장한다.

            if (health == null)
            {
                ClearVisuals(); // 전달된 보스가 없다면 UI를 비운다.
                return;
            }

            float currentHp = Mathf.Clamp(health.CurrentHp, 0.0f, health.MaxHp); // 연결된 보스의 현재 HP를 가져온다.
            int totalStockCount = GetTotalStockCount(); // 전체 스톡 수를 계산한다.
            int currentStockCount = GetCurrentStockCount(currentHp, totalStockCount); // 현재 스톡 수를 계산한다.
            float currentFillAmount = GetCurrentFillAmount(currentHp, currentStockCount); // 현재 스톡 게이지 비율을 계산한다.

            previousHp = currentHp; // 최초 연결 시 이전 HP를 현재 HP로 맞춘다.
            previousStockCount = currentStockCount; // 최초 연결 시 이전 스톡을 현재 스톡으로 맞춘다.
            previousFillAmount = currentFillAmount; // 최초 연결 시 이전 게이지 비율을 현재 값으로 맞춘다.

            if (shakeTarget != null)
            {
                originalAnchoredPosition = shakeTarget.anchoredPosition; // 현재 HP바 위치를 흔들림 복귀 위치로 다시 저장한다.
            }

            RefreshCurrentGauge(currentStockCount, currentFillAmount, totalStockCount); // 실제 HP UI를 즉시 표시한다.
            SyncDamageFill(currentStockCount, currentFillAmount, totalStockCount); // 잔상도 현재 HP와 동일하게 맞춘다.
        }

        public void Unbind() // 현재 보스와 UI 연결을 해제하는 함수
        {
            StopDamageTrail(); // 잔상 Coroutine을 중단한다.
            StopShake(); // 흔들림 Coroutine을 중단한다.

            health = null; // 연결된 EnemyHealth 참조를 제거한다.
            previousHp = 0.0f; // 이전 HP 값을 초기화한다.
            previousStockCount = 0; // 이전 스톡 수를 초기화한다.
            previousFillAmount = 0.0f; // 이전 게이지 비율을 초기화한다.

            ClearVisuals(); // 게이지와 숫자를 화면에서 지운다.

            gameObject.SetActive(false); //보스 UI화면을 끈다.
        }

        private void RefreshCurrentGauge(int currentStockCount, float currentFillAmount, int totalStockCount) // 실제 HP 게이지를 갱신하는 함수
        {
            if (hpFill != null)
            {
                hpFill.fillAmount = currentFillAmount; // 실제 현재 스톡의 HP 비율을 적용한다.
                hpFill.color = GetStockColor(currentStockCount, totalStockCount); // 현재 스톡 색상을 자동 적용한다.
            }

            if (hpNextFill != null)
            {
                if (currentStockCount > 1)
                {
                    hpNextFill.fillAmount = 1.0f; // 다음 스톡 색상을 뒤쪽 전체에 표시한다.
                    hpNextFill.color = GetStockColor(currentStockCount - 1, totalStockCount); // 다음 스톡 색상을 자동 적용한다.
                }
                else
                {
                    hpNextFill.fillAmount = 0.0f; // 마지막 스톡에서는 다음 색상을 숨긴다.
                }
            }

            if (hpStockText != null)
            {
                hpStockText.text = $"×{currentStockCount}"; // 현재 남은 스톡 수를 자동 표시한다.
            }
        }

        private void StartDamageFeedback(int currentStockCount, float currentFillAmount, int totalStockCount) // 피해 잔상과 흔들림을 시작하는 함수
        {
            StartDamageTrail(currentStockCount, currentFillAmount, totalStockCount); // 피해 잔상을 시작한다.
            StartShake(); // HP바 전체 흔들림을 시작한다.
        }

        private void StartDamageTrail(int currentStockCount, float currentFillAmount, int totalStockCount) // 피해 잔상 감소를 시작하는 함수
        {
            if (hpDamageFill == null)
            {
                return; // 잔상 Image가 없으면 실행하지 않는다.
            }

            StopDamageTrail(); // 이전 잔상 Coroutine을 중단한다.

            int trailColorStockCount = currentStockCount > 0 ? currentStockCount : Mathf.Max(1, previousStockCount); // 사망 시에는 직전 스톡 색상을 사용한다.
            Color trailColor = GetStockColor(trailColorStockCount, totalStockCount); // 현재 스톡의 기본 색상을 가져온다.

            hpDamageFill.color = Color.Lerp(trailColor, Color.white, damageHighlightAmount); // 기본 색상을 밝게 만들어 잔상 색상으로 사용한다.

            if (currentStockCount < previousStockCount && currentStockCount > 0)
            {
                hpDamageFill.fillAmount = 1.0f; // 스톡이 변경됐다면 새 스톡의 가득 찬 상태에서 잔상을 시작한다.
            }
            else
            {
                hpDamageFill.fillAmount = Mathf.Max(hpDamageFill.fillAmount, previousFillAmount); // 같은 스톡에서는 피해 전 길이를 잔상 시작값으로 사용한다.
            }

            damageTrailCoroutine = StartCoroutine(DamageTrailRoutine(currentFillAmount)); // 실제 게이지 위치까지 잔상을 감소시킨다.
        }

        private IEnumerator DamageTrailRoutine(float targetFillAmount) // 피해 잔상을 지연 후 따라가게 하는 Coroutine
        {
            float delayElapsed = 0.0f; // 지연 시간 경과값

            while (delayElapsed < damageTrailDelay)
            {
                delayElapsed += Time.unscaledDeltaTime; // 게임 배속과 관계없이 실제 시간을 사용한다.
                yield return null;
            }

            float startFillAmount = hpDamageFill.fillAmount; // 현재 잔상 길이를 감소 시작값으로 저장한다.
            float elapsed = 0.0f; // 잔상 감소 경과 시간

            while (elapsed < damageTrailDuration)
            {
                elapsed += Time.unscaledDeltaTime; // 실제 경과 시간을 더한다.

                float ratio = Mathf.Clamp01(elapsed / damageTrailDuration); // 잔상 감소 진행 비율을 계산한다.
                float easedRatio = 1.0f - Mathf.Pow(1.0f - ratio, 3.0f); // 처음 빠르고 마지막에 부드럽게 멈추는 비율을 만든다.

                hpDamageFill.fillAmount = Mathf.Lerp(startFillAmount, targetFillAmount, easedRatio); // 잔상을 실제 HP 게이지 위치까지 이동시킨다.

                yield return null;
            }

            hpDamageFill.fillAmount = targetFillAmount; // 종료 시 실제 게이지와 정확히 일치시킨다.
            damageTrailCoroutine = null; // 잔상 Coroutine이 종료됐다고 기록한다.
        }

        private void SyncDamageFill(int currentStockCount, float currentFillAmount, int totalStockCount) // 잔상 게이지를 실제 HP에 즉시 맞추는 함수
        {
            if (hpDamageFill == null)
            {
                return;
            }

            Color currentColor = GetStockColor(currentStockCount, totalStockCount); // 현재 스톡 색상을 가져온다.

            hpDamageFill.fillAmount = currentFillAmount; // 잔상 길이를 실제 HP 길이와 맞춘다.
            hpDamageFill.color = Color.Lerp(currentColor, Color.white, damageHighlightAmount); // 잔상용 밝은 색상을 적용한다.
        }

        private void StopDamageTrail() // 실행 중인 피해 잔상 Coroutine을 중단하는 함수
        {
            if (damageTrailCoroutine == null)
            {
                return;
            }

            StopCoroutine(damageTrailCoroutine); // 실행 중인 잔상 Coroutine을 중단한다.
            damageTrailCoroutine = null; // Coroutine 참조를 비운다.
        }

        private void StartShake() // HP바 흔들림을 시작하는 함수
        {
            if (shakeTarget == null)
            {
                return;
            }

            StopShake(); // 이전 흔들림을 중단하고 원래 위치로 복구한다.
            shakeCoroutine = StartCoroutine(ShakeRoutine()); // 새로운 흔들림 Coroutine을 시작한다.
        }

        private IEnumerator ShakeRoutine() // BossHpPanel을 짧게 흔드는 Coroutine
        {
            float elapsed = 0.0f; // 흔들림 경과 시간

            while (elapsed < shakeDuration)
            {
                elapsed += Time.unscaledDeltaTime; // 게임 배속과 관계없이 실제 시간을 사용한다.

                float remainingRatio = 1.0f - Mathf.Clamp01(elapsed / shakeDuration); // 끝날수록 흔들림이 약해지는 비율을 계산한다.
                float randomX = Random.Range(-1.0f, 1.0f) * shakeStrength * remainingRatio; // 좌우 흔들림 거리를 계산한다.
                float randomY = Random.Range(-0.35f, 0.35f) * shakeStrength * remainingRatio; // 작은 상하 흔들림 거리를 계산한다.

                shakeTarget.anchoredPosition = originalAnchoredPosition + new Vector2(randomX, randomY); // 원래 위치를 기준으로 HP바를 흔든다.

                yield return null;
            }

            shakeTarget.anchoredPosition = originalAnchoredPosition; // 흔들림이 끝나면 원래 위치로 정확히 복구한다.
            shakeCoroutine = null; // 흔들림 Coroutine이 종료됐다고 기록한다.
        }

        private void StopShake() // 흔들림을 중단하고 원래 위치로 복구하는 함수
        {
            if (shakeCoroutine != null)
            {
                StopCoroutine(shakeCoroutine); // 실행 중인 흔들림 Coroutine을 중단한다.
                shakeCoroutine = null; // Coroutine 참조를 비운다.
            }

            if (shakeTarget != null)
            {
                shakeTarget.anchoredPosition = originalAnchoredPosition; // HP바 위치를 원래 위치로 복구한다.
            }
        }

        private int GetTotalStockCount() // 최대 HP 기준 전체 스톡 수를 계산하는 함수
        {
            return Mathf.Max(1, Mathf.CeilToInt(health.MaxHp / hpPerStock)); // MaxHp를 1000으로 나누고 올림한다.
        }

        private int GetCurrentStockCount(float currentHp, int totalStockCount) // 현재 남은 스톡 수를 계산하는 함수
        {
            if (currentHp <= 0.0f)
            {
                return 0; // HP가 없으면 남은 스톡도 0이다.
            }

            int stockCount = Mathf.CeilToInt(currentHp / hpPerStock); // 현재 HP를 1000으로 나누고 올림한다.

            return Mathf.Clamp(stockCount, 0, totalStockCount); // 계산값을 전체 스톡 범위 안으로 제한한다.
        }

        private float GetCurrentFillAmount(float currentHp, int currentStockCount) // 현재 스톡 안의 HP 비율을 계산하는 함수
        {
            if (currentStockCount <= 0)
            {
                return 0.0f; // 남은 스톡이 없으면 게이지도 비운다.
            }

            float stockStartHp = (currentStockCount - 1) * hpPerStock; // 현재 스톡이 시작되는 누적 HP를 계산한다.
            float stockCapacity = Mathf.Min(hpPerStock, health.MaxHp - stockStartHp); // 현재 스톡의 실제 최대 용량을 계산한다.

            if (stockCapacity <= 0.0f)
            {
                return 0.0f;
            }

            float hpInsideStock = currentHp - stockStartHp; // 현재 스톡 안에 남은 HP를 계산한다.

            return Mathf.Clamp01(hpInsideStock / stockCapacity); // 현재 스톡 HP를 0~1 비율로 반환한다.
        }

        private Color GetStockColor(int stockCount, int totalStockCount) // 스톡에 맞는 색상을 자동 생성하는 함수
        {
            if (stockCount <= 0 || totalStockCount <= 0)
            {
                return Color.clear; // 표시할 스톡이 없으면 투명색을 반환한다.
            }

            if (totalStockCount == 1)
            {
                return Color.HSVToRGB(0.0f, colorSaturation, colorBrightness); // 스톡 하나면 빨간색을 사용한다.
            }

            float stockRatio = (stockCount - 1.0f) / (totalStockCount - 1.0f); // 마지막 스톡부터 최대 스톡까지의 위치 비율을 계산한다.
            float hue = Mathf.Lerp(0.0f, maximumHue, stockRatio); // 빨간색부터 보라색까지 Hue를 계산한다.

            return Color.HSVToRGB(hue, colorSaturation, colorBrightness); // 계산된 자동 색상을 반환한다.
        }

        private void ConfigureFillImage(Image image) // HP Image의 공통 설정을 맞추는 함수
        {
            if (image == null)
            {
                return;
            }

            image.type = Image.Type.Filled; // Filled Image로 설정한다.
            image.fillMethod = Image.FillMethod.Horizontal; // 가로 방향 Fill을 사용한다.
            image.fillOrigin = (int)Image.OriginHorizontal.Left; // 왼쪽부터 HP가 남도록 설정한다.
            image.fillAmount = 1.0f; // 최초 Fill을 가득 채운다.
            image.raycastTarget = false; // UI가 마우스 입력을 막지 않도록 한다.
        }

        private void ClearVisuals() // 보스가 없을 때 UI 내용을 비우는 함수
        {
            if (hpNextFill != null) hpNextFill.fillAmount = 0.0f;
            if (hpDamageFill != null) hpDamageFill.fillAmount = 0.0f;
            if (hpFill != null) hpFill.fillAmount = 0.0f;
            if (hpStockText != null) hpStockText.text = string.Empty;
        }
    }
}