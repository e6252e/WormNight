// 안건준 추가 - 0623
// 카드 등급(레어/유니크)에 따라 VFX 이팩트를 카드 UI 위에 표시하는 컴포넌트
// UIParticleSystem 방식: Screen Space Overlay 전용 VFX Canvas 를 런타임에 생성해
// 카드 UI 보다 높은 Sort Order 로 파티클을 렌더링 → 카드 위에 이펙트 표시 가능

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions.FantasyRPG;
using TeamProject01.Gameplay;
using DG.Tweening;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CardEffect : MonoBehaviour
{
    public enum RareEffectKind
    {
        None = 0,
        CardBrush_G, CardBrushLine_G, CardCubeUp_G, CardDissolve_G,
        CardEdgeFlow_G, CardFeather_G, CardFlare_G, CardFlash_G,
        CardFlashAir_G, CardFlowLoop_G, CardLight_Loop_G,
        CardRimLine_G, CardRimStar_G, CardShock_Loop_G,
        CardStar_Loop_G, CardSunLight_Loop_G
    }

    public enum UniqueEffectKind
    {
        None = 0,
        CardBrush_Y, CardBrushLine_Y, CardCubeUp_Y, CardDissolve_Y,
        CardEdgeFlow_Y, CardFeather_Y, CardFlare_Y, CardFlash_Y,
        CardFlashAir_Y, CardFlowLoop_Y, CardLight_Loop_Y,
        CardRimLine_Y, CardRimStar_Y, CardShock_Loop_Y,
        CardStar_Loop_Y, CardSunLight_Loop_Y
    }

    [Header("이팩트 종류 선택")]
    [SerializeField] private RareEffectKind rareEffect   = RareEffectKind.CardLight_Loop_G;
    [SerializeField] private UniqueEffectKind uniqueEffect = UniqueEffectKind.CardLight_Loop_Y;

    [Header("이팩트 프리팹 (자동 등록)")]
    [SerializeField] private GameObject rareEffectPrefab;
    [SerializeField] private GameObject uniqueEffectPrefab;
    [Tooltip("일반 등급 이팩트 (없으면 미적용)")]
    [SerializeField] private GameObject normalEffectPrefab;

    [Header("마스크 설정")]
    [Tooltip("카드 모서리 클리핑 스프라이트 (직접 지정 시 해당 스프라이트 알파로 클리핑)\n" +
             "비워두면 아래 Mask Corner Radius 로 자동 생성")]
    [SerializeField] private Sprite maskSprite;

    [Range(0, 60)]
    [Tooltip("마스크 모서리 반경 (0 = 직사각형, 숫자가 클수록 더 둥글게)")]
    public int maskCornerRadius = 15;

    [Header("이팩트 설정")]
    [Range(0f, 1f)]
    [Tooltip("이팩트 밝기 (0=완전 어둡게, 1=원본)")]
    public float EffectBrightness = 1f;

    [Tooltip("카드 크기 대비 가로 배율")]
    [Min(0.01f)] public float EffectWidth = 1f;

    [Tooltip("카드 크기 대비 세로 배율")]
    [Min(0.01f)] public float EffectHeight = 1f;

    [Range(-100f, 0f)]
    [Tooltip("이팩트 전체 크기 조정 (0 = 카드 기준, -100 = 최소)")]
    public float SizeOffset = 0f;

    [Header("이팩트 크기 오프셋 (픽셀)")]
    [Tooltip("가로 픽셀 추가/감소 (양수=넓어짐, 음수=좁아짐)")]
    [Range(-300f, 300f)] public float WidthOffset = 0f;

    [Tooltip("세로 픽셀 추가/감소 (양수=넓어짐, 음수=좁아짐)")]
    [Range(-300f, 300f)] public float HeightOffset = 0f;

    [Header("이팩트 위치 오프셋 (픽셀)")]
    [Tooltip("X 위치 오프셋 (양수=오른쪽, 음수=왼쪽)")]
    [Range(-300f, 300f)] public float PositionOffsetX = 0f;

    [Tooltip("Y 위치 오프셋 (양수=위, 음수=아래)")]
    [Range(-300f, 300f)] public float PositionOffsetY = 0f;

    [Range(-100f, 100f)]
    [Tooltip("재생 속도 조정 (0 = 기본 속도, +100 = 2배 빠름, -100 = 거의 정지)")]
    public float SpeedOffset = 0f;

    // 카드 루트 → 생성된 VFX 컨테이너 매핑
    private readonly Dictionary<GameObject, GameObject> activeEffects = new();

    // 카드 루트 → 등급 매핑 (열거형 변경 시 즉시 재적용에 필요)
    private readonly Dictionary<GameObject, StatUpgrade.StatCardTier> activeTiers = new();

    // 카드 루트 → 이팩트 컨테이너 기본 크기 (hover 애니메이션 복원용)
    private readonly Dictionary<GameObject, Vector2> activeBaseSizes = new();

    // 모든 프리팹 미리 로드 캐시 (버튼 클릭 시 즉시 교체용)
    private readonly Dictionary<RareEffectKind, GameObject>   rarePrefabCache   = new();
    private readonly Dictionary<UniqueEffectKind, GameObject> uniquePrefabCache = new();

    // 런타임 전용 VFX 캔버스 (카드 캔버스보다 Sort Order 높음)
    private Canvas vfxCanvas;

    // 열거형 변경 감지용 캐시
    private RareEffectKind   lastRareEffect;
    private UniqueEffectKind lastUniqueEffect;

    // 현재 선택 열거형 (읽기 전용 공개)
    public RareEffectKind   CurrentRare   => rareEffect;
    public UniqueEffectKind CurrentUnique => uniqueEffect;

    // ─────────────────────────────────────────────
    //  Unity 이벤트
    // ─────────────────────────────────────────────

    private void Awake()
    {
#if UNITY_EDITOR
        PreloadAllPrefabs(); // 전찬우 수정 -0625: AssetDatabase 프리로드는 에디터 전용
#else
        CacheAssignedPrefabs(); // 전찬우 수정 -0625: 빌드에서는 인스펙터 연결 프리팹만 캐시
#endif
    }

    private void CacheAssignedPrefabs() // 전찬우 수정 -0625: Player Build용 프리팹 캐시
    {
        if (rareEffectPrefab != null)
        {
            rarePrefabCache[rareEffect] = rareEffectPrefab;
        }

        if (uniqueEffectPrefab != null)
        {
            uniquePrefabCache[uniqueEffect] = uniqueEffectPrefab;
        }
    }

    private void Start()
    {
        lastRareEffect   = rareEffect;
        lastUniqueEffect = uniqueEffect;
    }

    private void Update()
    {
#if UNITY_EDITOR
        bool changed = false;
        if (rareEffect != lastRareEffect)
        {
            lastRareEffect = rareEffect;
            UpdateRarePrefab();
            changed = true;
        }
        if (uniqueEffect != lastUniqueEffect)
        {
            lastUniqueEffect = uniqueEffect;
            UpdateUniquePrefab();
            changed = true;
        }
        if (changed) RefreshActiveEffects();
#endif
    }

    // ─────────────────────────────────────────────
    //  공개 Setter (CardEffectSelector 버튼에서 호출)
    // ─────────────────────────────────────────────

    public void SetRareEffect(RareEffectKind kind)
    {
        rareEffect = kind;
        lastRareEffect = kind; // Update 에서 중복 처리 방지
        if (rarePrefabCache.TryGetValue(kind, out GameObject p))
            rareEffectPrefab = p;
        RefreshActiveEffects();
    }

    public void SetUniqueEffect(UniqueEffectKind kind)
    {
        uniqueEffect = kind;
        lastUniqueEffect = kind;
        if (uniquePrefabCache.TryGetValue(kind, out GameObject p))
            uniqueEffectPrefab = p;
        RefreshActiveEffects();
    }

    // ─────────────────────────────────────────────
    //  외부 API
    // ─────────────────────────────────────────────

    // 외부에서 직접 호출할 때는 canvasAlreadyUpdated = false (기본값)
    // RefreshActiveEffects 에서 내부 루프로 호출할 때는 true → 중복 ForceUpdate 방지
    public void ApplyEffect(GameObject cardRoot, StatUpgrade.StatCardTier tier,
                            bool canvasAlreadyUpdated = false)
    {
        if (cardRoot == null) return;

        ClearEffect(cardRoot);

        GameObject prefab = ResolvePrefab(tier);
        if (prefab == null) return;

        if (!canvasAlreadyUpdated) Canvas.ForceUpdateCanvases();

        RectTransform rt = cardRoot.GetComponent<RectTransform>();
        if (rt == null) return;

        Canvas cardCanvas = rt.GetComponentInParent<Canvas>();
        if (cardCanvas == null) return;

        // 카드의 스크린 픽셀 위치·크기 취득
        // Screen Space Overlay 에서 GetWorldCorners() = 스크린 픽셀 좌표
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        Vector3 centerPx = (corners[0] + corners[2]) * 0.5f;
        float pixelW = Mathf.Abs(corners[3].x - corners[0].x);
        float pixelH = Mathf.Abs(corners[1].y - corners[0].y);

        if (pixelW < 1f) pixelW = rt.rect.width  * Mathf.Abs(rt.lossyScale.x);
        if (pixelH < 1f) pixelH = rt.rect.height * Mathf.Abs(rt.lossyScale.y);

        // VFX 전용 캔버스 확보 (카드 캔버스보다 Sort Order 1 높게)
        Canvas targetCanvas = GetOrCreateVfxCanvas(cardCanvas);

        // ── UI 컨테이너 생성 ──────────────────────────
        GameObject container = new GameObject($"VFX_{cardRoot.name}");
        RectTransform containerRT = container.AddComponent<RectTransform>();
        container.transform.SetParent(targetCanvas.transform, false);

        // 카드와 동일한 스크린 위치에 배치
        // Screen Space Overlay: world position = screen pixel position
        containerRT.anchorMin = Vector2.one * 0.5f;
        containerRT.anchorMax = Vector2.one * 0.5f;
        containerRT.pivot     = Vector2.one * 0.5f;

        // 위치 오프셋 적용
        containerRT.position = new Vector3(
            centerPx.x + PositionOffsetX,
            centerPx.y + PositionOffsetY,
            0f);

        // 크기 계산: 배율 × 전체 스케일 + 픽셀 오프셋
        float sizeFactor = Mathf.Max(0.001f, 1f + SizeOffset / 100f);
        float effectW = Mathf.Max(1f, pixelW * EffectWidth  * sizeFactor + WidthOffset);
        float effectH = Mathf.Max(1f, pixelH * EffectHeight * sizeFactor + HeightOffset);
        containerRT.sizeDelta = new Vector2(effectW, effectH);

        // ── Mask 미사용 ────────────────────────────
        // Mask 컴포넌트는 VFX 파티클 머티리얼에 _Stencil 프로퍼티를 추가하려 해
        // "doesn't have _Stencil property" 경고를 매 프레임 발생시키므로 사용 안 함
        // 파티클이 카드 밖으로 벗어나는 경우 SizeOffset / WidthOffset / HeightOffset 으로 조절

        // ── VFX 프리팹 인스턴스화 → 컨테이너 하위 ────
        GameObject vfxGO = Instantiate(prefab, container.transform);
        vfxGO.transform.localPosition = Vector3.zero;
        vfxGO.transform.localRotation = Quaternion.identity;

        // Hierarchy 스케일 → 파티클 방출 범위를 카드 픽셀 크기에 맞춤
        vfxGO.transform.localScale = new Vector3(effectW, effectH, 1f);

        // ── 각 ParticleSystem 에 UIParticleSystem 추가 ──
        // 핵심: GameObject 비활성화 → AddComponent → material 설정 → 활성화
        // AddComponent 직후 Awake 실행 전에 material 을 지정해야 흰색 박스 방지
        foreach (ParticleSystem ps in vfxGO.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.gameObject.GetComponent<UIParticleSystem>() != null) continue;

            // ① 비활성화 → Awake 지연
            bool wasActive = ps.gameObject.activeSelf;
            ps.gameObject.SetActive(false);

            // ② UIParticleSystem 추가 (Awake 는 아직 실행 안 됨)
            var uiPs = ps.gameObject.AddComponent<UIParticleSystem>();

            // ③ VFX 원본 머티리얼 설정 (Awake 실행 전에 지정해야 흰색 박스 방지)
            ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null && psr.sharedMaterial != null)
            {
                uiPs.material = psr.sharedMaterial;
            }
            else
            {
                // 머티리얼 없으면 UIParticleSystem 제거 → 흰색 박스 방지
                Destroy(uiPs);
            }

            // ④ 활성화 → Awake 실행 (material 이 이미 설정된 상태)
            ps.gameObject.SetActive(wasActive);

            // 카드 선택 전까지 무한 반복 재생 + 속도 적용
            // SpeedOffset: 0 = 1.0x, +100 = 2.0x, -100 ≈ 0(정지)
            ParticleSystem.MainModule mainMod = ps.main;
            mainMod.loop = true;
            mainMod.simulationSpeed = Mathf.Max(0.01f, 1f + SpeedOffset / 100f);

            ps.Play(withChildren: false);
        }

        ApplyBrightness(vfxGO, EffectBrightness);

        activeEffects[cardRoot]   = container;
        activeTiers[cardRoot]     = tier;
        activeBaseSizes[cardRoot] = containerRT.sizeDelta;
    }

    public void ClearEffect(GameObject cardRoot)
    {
        if (cardRoot == null) return;
        if (activeEffects.TryGetValue(cardRoot, out GameObject existing) && existing != null)
            Destroy(existing);
        activeEffects.Remove(cardRoot);
        activeTiers.Remove(cardRoot);
        activeBaseSizes.Remove(cardRoot);
    }

    /// <summary>
    /// 모든 이팩트를 즉시 제거. (카드 선택 시 축소 연출 없이 바로 사라짐)
    /// </summary>
    public void FadeAllEffects(float duration = 0.2f)
    {
        ClearAll();
    }

    public void ClearAll()
    {
        foreach (KeyValuePair<GameObject, GameObject> kvp in activeEffects)
        {
            if (kvp.Value != null) Destroy(kvp.Value);
        }
        activeEffects.Clear();
        activeTiers.Clear();
        activeBaseSizes.Clear();
    }

    // ── Hover 연동 ─────────────────────────────
    // CardUI 의 NotifySpawnedCardPointerEnter/Exit 에서 호출
    // scale: 카드의 hoverScale 과 동일한 값 전달 (기본 1.09f)
    // DOSizeDelta 는 VFX GO 자식의 localScale 에 영향을 주지 않으므로
    // 컨테이너 transform 전체를 DOScale → 마스크 + 파티클 전부 같이 커짐
    public void OnCardHoverEnter(GameObject cardRoot, float scale = 1.09f)
    {
        if (!activeEffects.TryGetValue(cardRoot, out GameObject container) || container == null) return;

        container.transform.DOKill();
        container.transform.DOScale(Vector3.one * scale, 0.15f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    public void OnCardHoverExit(GameObject cardRoot)
    {
        if (!activeEffects.TryGetValue(cardRoot, out GameObject container) || container == null) return;

        container.transform.DOKill();
        container.transform.DOScale(Vector3.one, 0.15f)
            .SetEase(Ease.InQuad)
            .SetUpdate(true);
    }

    // 열거형 변경 시 현재 활성 이팩트를 새 프리팹으로 즉시 교체
    private void RefreshActiveEffects()
    {
        // 현재 활성 목록을 복사해서 순회 (ApplyEffect 내부에서 딕셔너리 수정이 일어남)
        var snapshot = new List<KeyValuePair<GameObject, StatUpgrade.StatCardTier>>(activeTiers);
        if (snapshot.Count == 0) return;

        // Canvas.ForceUpdateCanvases 는 카드 수와 관계없이 한 번만 호출
        Canvas.ForceUpdateCanvases();
        foreach (var kv in snapshot)
        {
            if (kv.Key != null)
                ApplyEffect(kv.Key, kv.Value, canvasAlreadyUpdated: true);
        }
    }

    // ─────────────────────────────────────────────
    //  내부 헬퍼
    // ─────────────────────────────────────────────

    private Canvas GetOrCreateVfxCanvas(Canvas cardCanvas)
    {
        // 이미 생성된 캔버스 재사용
        if (vfxCanvas != null) return vfxCanvas;

        // 씬에서 "VFX_Canvas" 검색
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.name == "VFX_Canvas")
            {
                vfxCanvas = c;
                return vfxCanvas;
            }
        }

        // 없으면 런타임 생성
        // Screen Space Overlay + Sort Order = 카드 캔버스 + 1 → 카드 UI 위에 렌더링
        GameObject go = new GameObject("VFX_Canvas");
        vfxCanvas = go.AddComponent<Canvas>();
        vfxCanvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        vfxCanvas.sortingOrder = cardCanvas.sortingOrder + 1;
        // CanvasScaler 없음 → 1 canvas unit = 1 screen pixel (Overlay 기본)

        return vfxCanvas;
    }

    private GameObject ResolvePrefab(StatUpgrade.StatCardTier tier)
    {
        return tier switch
        {
            StatUpgrade.StatCardTier.Unique => uniqueEffectPrefab,
            StatUpgrade.StatCardTier.Rare   => rareEffectPrefab,
            _                               => normalEffectPrefab
        };
    }

    private static void ApplyBrightness(GameObject effect, float brightness)
    {
        if (Mathf.Approximately(brightness, 1f)) return;

        foreach (ParticleSystem ps in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            Color c = main.startColor.color;
            c.r *= brightness; c.g *= brightness; c.b *= brightness;
            main.startColor = c;
        }

        foreach (Light lt in effect.GetComponentsInChildren<Light>(true))
            lt.intensity *= brightness;

        foreach (Renderer r in effect.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material mat in r.materials)
            {
                if (mat.HasProperty("_Color"))
                {
                    Color c = mat.color;
                    c.r *= brightness; c.g *= brightness; c.b *= brightness;
                    mat.color = c;
                }
                if (mat.HasProperty("_BaseColor"))
                {
                    Color c = mat.GetColor("_BaseColor");
                    c.r *= brightness; c.g *= brightness; c.b *= brightness;
                    mat.SetColor("_BaseColor", c);
                }
            }
        }
    }

#if UNITY_EDITOR
    private const string PrefabRoot =
        "Assets/ThirdParty/03_LevelSystem/Game VFX - Card Effects Collection/Game VFX - Card Effects Collection/Prefabs/URP/FX_";

    // 모든 열거형에 해당하는 프리팹을 Awake 시 미리 캐시 (에디터 전용)
    private void PreloadAllPrefabs()
    {
        foreach (RareEffectKind kind in System.Enum.GetValues(typeof(RareEffectKind)))
        {
            if (kind == RareEffectKind.None) continue;
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}{kind}.prefab");
            if (p != null) rarePrefabCache[kind] = p;
        }
        foreach (UniqueEffectKind kind in System.Enum.GetValues(typeof(UniqueEffectKind)))
        {
            if (kind == UniqueEffectKind.None) continue;
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}{kind}.prefab");
            if (p != null) uniquePrefabCache[kind] = p;
        }

        // 인스팩터에서 지정한 초기 프리팹도 캐시에 반영
        if (rareEffectPrefab   != null) rarePrefabCache[rareEffect]     = rareEffectPrefab;
        if (uniqueEffectPrefab != null) uniquePrefabCache[uniqueEffect] = uniqueEffectPrefab;
    }

    private void OnValidate()
    {
        UpdateRarePrefab();
        UpdateUniquePrefab();
    }

    private void UpdateRarePrefab()
    {
        if (rareEffect == RareEffectKind.None) { rareEffectPrefab = null; return; }
        string name = rareEffect.ToString();
        GameObject loaded = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}{name}.prefab");
        if (loaded != null) rareEffectPrefab = loaded;
        else Debug.LogWarning($"[CardEffect] 레어 프리팹 없음: FX_{name}.prefab");
    }

    private void UpdateUniquePrefab()
    {
        if (uniqueEffect == UniqueEffectKind.None) { uniqueEffectPrefab = null; return; }
        string name = uniqueEffect.ToString();
        GameObject loaded = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}{name}.prefab");
        if (loaded != null) uniqueEffectPrefab = loaded;
        else Debug.LogWarning($"[CardEffect] 유니크 프리팹 없음: FX_{name}.prefab");
    }

    [ContextMenu("이팩트 프리팹 재할당 (강제)")]
    private void ForceReassign()
    {
        UpdateRarePrefab();
        UpdateUniquePrefab();
        EditorUtility.SetDirty(this);
        Debug.Log($"[CardEffect] 재할당 완료\n  레어({rareEffect}): {(rareEffectPrefab ? rareEffectPrefab.name : "NULL")}\n  유니크({uniqueEffect}): {(uniqueEffectPrefab ? uniqueEffectPrefab.name : "NULL")}");
    }
#endif
}
