using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.UI.Extensions.FantasyRPG;

namespace TeamProject01.Gameplay
{
    // GoldActionHudRoot — 액션 HUD 구매/강화 SFX (GoldActionHudController 는 수정하지 않음)
    [DefaultExecutionOrder(-100)] // 키 SFX는 컨트롤러 Update(구매/강화)보다 먼저
    public sealed class ActionHudFeature : MonoBehaviour
    {
        private enum SlotButtonSfxKind
        {
            None,
            Purchase,
            Upgrade
        }

        [Header("컨트롤러")]
        [SerializeField] private GoldActionHudController hudController;

        [Header("구매 클립 (Bling05)")]
        [SerializeField] private AudioClip skillPurchaseClip;
        [Range(0f, 1f)] [SerializeField] private float skillPurchaseVolume = 1f;

        [Header("강화 클립 (chimes_magic_bell_ding_1)")]
        [SerializeField] private AudioClip skillUpgradeClip;
        [Range(0f, 1f)] [SerializeField] private float skillUpgradeVolume = 1f;

        [Header("스킬 활성 이팩트")]
        [SerializeField] private GameObject skillReadyVfxPrefab; // FX_CardRimLine_B
        [Header("스킬 활성 이팩트 오브젝트 위치 (UI RectTransform, Canvas 하위)")]
        [SerializeField] private Transform[] skillReadyVfxAnchors = new Transform[5]; // 슬롯 1~5 — VFX_Canvas에 스크린 좌표로 매칭

        [Header("스킬 활성 이팩트 크기·위치")]
        [Tooltip("앵커 가로 대비 배율")]
        [Min(0.01f)] [SerializeField] private float readyVfxWidthScale = 1f;
        [Tooltip("앵커 세로 대비 배율")]
        [Min(0.01f)] [SerializeField] private float readyVfxHeightScale = 1f;
        [Range(-100f, 100f)]
        [Tooltip("전체 크기 보정 (%) — 0=기본, -50=절반")]
        [SerializeField] private float readyVfxSizeOffsetPercent;
        [Tooltip("가로 픽셀 추가 (양수=넓게)")]
        [Range(-300f, 300f)] [SerializeField] private float readyVfxWidthOffsetPx;
        [Tooltip("세로 픽셀 추가 (양수=넓게)")]
        [Range(-300f, 300f)] [SerializeField] private float readyVfxHeightOffsetPx;
        [Tooltip("위치 X 오프셋 (px)")]
        [Range(-300f, 300f)] [SerializeField] private float readyVfxPositionOffsetX;
        [Tooltip("위치 Y 오프셋 (px)")]
        [Range(-300f, 300f)] [SerializeField] private float readyVfxPositionOffsetY;
        [Tooltip("파티클 localScale 추가 배율 (XYZ)")]
        [SerializeField] private Vector3 readyVfxParticleScale = Vector3.one;

        private static readonly int[] ShieldUpgradeCostSteps = { 500, 750, 1200, 1700, 2500 };

        private FieldInfo statesField;
        private FieldInfo purchasedField;
        private FieldInfo upgradeLevelField;
        private FieldInfo repeatPurchaseCountField;
        private FieldInfo cooldownEndsAtField;

        private GameObject[] activeReadyVfxInstances;
        private bool readyVfxLoopRunning;
        private Canvas hudVfxCanvas;

        private bool slotHooksApplied;
        private int slotHookRetryFrames;

        private SlotButtonSfxKind[] pendingClickSfxBySlot;

        private global::LevelUpUi cachedLevelUpUi;

        private UnityEngine.Events.UnityAction[] iconClickSfxActions;
        private UnityEngine.Events.UnityAction[] actionClickSfxActions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureFeatureOnHudControllers()
        {
            GoldActionHudController[] controllers = FindObjectsByType<GoldActionHudController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < controllers.Length; i++)
            {
                GoldActionHudController controller = controllers[i];
                if (controller == null || controller.GetComponent<ActionHudFeature>() != null)
                {
                    continue;
                }

                controller.gameObject.AddComponent<ActionHudFeature>();
            }
        }

        private void Awake()
        {
            EnsureHudController();
            EnsureReflection();
            EnsureClipDefaults();
        }

        private void OnEnable()
        {
            EnsureHudController();
            slotHooksApplied = false;
            slotHookRetryFrames = 30;
            StartCoroutine(HookSlotButtonsNextFrame());
            StartReadyVfxLoop();
        }

        private void OnDisable()
        {
            readyVfxLoopRunning = false;
            ClearAllReadyVfx();
        }

        private void Update()
        {
            HandleKeyboardSfx();
        }

        private void LateUpdate()
        {
            if (hudController == null)
            {
                return;
            }

            if (!slotHooksApplied && slotHookRetryFrames > 0)
            {
                TryApplySlotHooks();
                slotHookRetryFrames--;
            }
        }

        private void EnsureHudController()
        {
            if (hudController != null)
            {
                return;
            }

            hudController = GetComponent<GoldActionHudController>();
            if (hudController == null)
            {
                hudController = GetComponentInChildren<GoldActionHudController>(true);
            }
        }

        private void EnsureReflection()
        {
            if (statesField != null && purchasedField != null)
            {
                return;
            }

            const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type controllerType = typeof(GoldActionHudController);
            statesField = controllerType.GetField("states", instance);

            Type stateType = controllerType.GetNestedType("SkillState", BindingFlags.NonPublic);
            if (stateType == null)
            {
                Debug.LogWarning("[ActionHudFeature] SkillState 타입을 찾지 못했습니다.", this);
                return;
            }

            purchasedField = stateType.GetField("Purchased", instance);
            upgradeLevelField = stateType.GetField("UpgradeLevel", instance);
            repeatPurchaseCountField = stateType.GetField("RepeatPurchaseCount", instance);
            cooldownEndsAtField = stateType.GetField("CooldownEndsAt", instance);
            if (statesField == null || purchasedField == null)
            {
                Debug.LogWarning("[ActionHudFeature] states / Purchased reflection 실패 — SFX 단계 판별 불가.", this);
            }
        }

        private void EnsureClipDefaults()
        {
            if (skillPurchaseClip == null)
            {
                skillPurchaseClip = ResolveCatalogClip(GameplaySfxCue.GoodPickup);
            }

            if (skillUpgradeClip == null)
            {
                skillUpgradeClip = ResolveCatalogClip(GameplaySfxCue.ManaOrbPickup);
            }
        }

        private static AudioClip ResolveCatalogClip(GameplaySfxCue cue)
        {
            GameplaySfxCatalog catalog = Resources.Load<GameplaySfxCatalog>(GameplaySfxCatalog.ResourcePath);
            if (catalog == null || !catalog.TryGetEntry(cue, out GameplaySfxCatalogEntry entry)
                || entry.Clips == null || entry.Clips.Length == 0)
            {
                return null;
            }

            return entry.Clips[0];
        }

        private IEnumerator HookSlotButtonsNextFrame()
        {
            yield return null;
            TryApplySlotHooks();
        }

        private void TryApplySlotHooks()
        {
            if (hudController == null || hudController.Slots == null || hudController.Slots.Length == 0)
            {
                return;
            }

            int slotCount = hudController.Slots.Length;
            EnsurePendingSfxCache(slotCount);
            EnsureClickActionCache(slotCount);

            GoldActionHudSlot[] slots = hudController.Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                HookSlotPointerDown(slots[i], i);
                HookSlotClickSfxAdditive(slots[i], i);
            }

            slotHooksApplied = true;
        }

        private void EnsurePendingSfxCache(int slotCount)
        {
            if (pendingClickSfxBySlot == null || pendingClickSfxBySlot.Length != slotCount)
            {
                pendingClickSfxBySlot = new SlotButtonSfxKind[slotCount];
            }
        }

        private void EnsureClickActionCache(int slotCount)
        {
            if (iconClickSfxActions != null && iconClickSfxActions.Length == slotCount)
            {
                return;
            }

            iconClickSfxActions = new UnityEngine.Events.UnityAction[slotCount];
            actionClickSfxActions = new UnityEngine.Events.UnityAction[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                int capturedIndex = i;
                iconClickSfxActions[i] = () => PlayPendingOrResolvedClickSfx(capturedIndex);
                actionClickSfxActions[i] = () => PlayPendingOrResolvedClickSfx(capturedIndex);
            }
        }

        private void HookSlotPointerDown(GoldActionHudSlot slot, int slotIndex)
        {
            if (slot == null)
            {
                return;
            }

            if (slot.ActionButton != null)
            {
                EnsurePointerDownHandler(slot.ActionButton.gameObject, slotIndex);
            }

            if (slot.IconImage != null)
            {
                EnsurePointerDownHandler(slot.IconImage.gameObject, slotIndex);
            }
        }

        private void EnsurePointerDownHandler(GameObject target, int slotIndex)
        {
            SlotSfxPointerDownHandler handler = target.GetComponent<SlotSfxPointerDownHandler>();
            if (handler == null)
            {
                handler = target.AddComponent<SlotSfxPointerDownHandler>();
            }

            handler.Initialize(this, slotIndex);
        }

        private void HookSlotClickSfxAdditive(GoldActionHudSlot slot, int slotIndex)
        {
            if (slot == null || iconClickSfxActions == null || slotIndex >= iconClickSfxActions.Length)
            {
                return;
            }

            if (slot.IconImage != null)
            {
                Button iconButton = slot.IconImage.GetComponent<Button>();
                if (iconButton != null)
                {
                    iconButton.onClick.RemoveListener(iconClickSfxActions[slotIndex]);
                    iconButton.onClick.AddListener(iconClickSfxActions[slotIndex]);
                }
            }

            if (slot.ActionButton != null)
            {
                slot.ActionButton.onClick.RemoveListener(actionClickSfxActions[slotIndex]);
                slot.ActionButton.onClick.AddListener(actionClickSfxActions[slotIndex]);
            }
        }

        internal void CachePendingSfxForSlot(int slotIndex)
        {
            EnsurePendingSfxCache(hudController != null && hudController.Slots != null ? hudController.Slots.Length : 0);
            if (pendingClickSfxBySlot == null || slotIndex < 0 || slotIndex >= pendingClickSfxBySlot.Length)
            {
                return;
            }

            pendingClickSfxBySlot[slotIndex] = ResolveSfxKindFromSlot(slotIndex);
        }

        private void PlayPendingOrResolvedClickSfx(int slotIndex)
        {
            SlotButtonSfxKind kind = SlotButtonSfxKind.None;
            if (pendingClickSfxBySlot != null && slotIndex >= 0 && slotIndex < pendingClickSfxBySlot.Length
                && pendingClickSfxBySlot[slotIndex] != SlotButtonSfxKind.None)
            {
                kind = pendingClickSfxBySlot[slotIndex];
                pendingClickSfxBySlot[slotIndex] = SlotButtonSfxKind.None;
            }
            else
            {
                kind = ResolveSfxKindFromSlot(slotIndex);
            }

            PlaySfxForKind(kind);
        }

        internal void PlaySlotSfxFromState(int slotIndex)
        {
            PlaySfxForKind(ResolveSfxKindFromSlot(slotIndex));
        }

        private void HandleKeyboardSfx()
        {
            if (hudController == null || IsHudKeyboardInputBlocked())
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (WasDigitKeyPressed(keyboard, 1)) PlayKeyboardSfxForKey(1);
            if (WasDigitKeyPressed(keyboard, 2)) PlayKeyboardSfxForKey(2);
            if (WasDigitKeyPressed(keyboard, 3)) PlayKeyboardSfxForKey(3);
            if (WasDigitKeyPressed(keyboard, 4)) PlayKeyboardSfxForKey(4);
            if (WasDigitKeyPressed(keyboard, 5)) PlayKeyboardSfxForKey(5);
        }

        private static bool WasDigitKeyPressed(Keyboard keyboard, int digit)
        {
            return digit switch
            {
                1 => keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame,
                2 => keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame,
                3 => keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame,
                4 => keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame,
                5 => keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame,
                _ => false
            };
        }

        private void PlayKeyboardSfxForKey(int keyNumber)
        {
            int slotIndex = FindSlotIndexByKeyNumber(keyNumber);
            if (slotIndex < 0)
            {
                return;
            }

            PlaySlotSfxFromState(slotIndex);
        }

        // 1~3: 미활성(미구매)=구매음 / 활성(구매됨)=강화음 · 4~5: 항상 강화음
        private SlotButtonSfxKind ResolveSfxKindFromSlot(int slotIndex)
        {
            if (TryReadPurchased(slotIndex, out GoldActionHudController.SkillDefinition skill, out bool purchased))
            {
                if (!IsSkillUnlocked(skill))
                {
                    return SlotButtonSfxKind.None;
                }

                if (IsPurchaseThenUpgradeSkill(skill))
                {
                    return purchased ? SlotButtonSfxKind.Upgrade : SlotButtonSfxKind.Purchase;
                }

                return SlotButtonSfxKind.Upgrade;
            }

            return ResolveSfxKindFromLabel(GetSlotButtonLabel(slotIndex));
        }

        private static bool IsPurchaseThenUpgradeSkill(GoldActionHudController.SkillDefinition skill)
        {
            return skill != null && skill.KeyNumber >= 1 && skill.KeyNumber <= 3;
        }

        private static SlotButtonSfxKind ResolveSfxKindFromLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return SlotButtonSfxKind.None;
            }

            if (label.StartsWith("구매", StringComparison.Ordinal))
            {
                return SlotButtonSfxKind.Purchase;
            }

            if (label.StartsWith("강화", StringComparison.Ordinal) || label.StartsWith("회복", StringComparison.Ordinal))
            {
                return SlotButtonSfxKind.Upgrade;
            }

            return SlotButtonSfxKind.None;
        }

        private string GetSlotButtonLabel(int slotIndex)
        {
            GoldActionHudSlot slot = GetSlot(slotIndex);
            return slot != null && slot.ActionButtonText != null ? slot.ActionButtonText.text : string.Empty;
        }

        private void PlaySfxForKind(SlotButtonSfxKind kind)
        {
            switch (kind)
            {
                case SlotButtonSfxKind.Purchase:
                    PlaySkillPurchaseSfx();
                    break;
                case SlotButtonSfxKind.Upgrade:
                    PlaySkillUpgradeSfx();
                    break;
            }
        }

        private int FindSlotIndexByKeyNumber(int keyNumber)
        {
            if (hudController == null || hudController.Skills == null)
            {
                return -1;
            }

            GoldActionHudController.SkillDefinition[] skills = hudController.Skills;
            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i] != null && skills[i].KeyNumber == keyNumber)
                {
                    return i;
                }
            }

            return -1;
        }

        private GoldActionHudSlot GetSlot(int slotIndex)
        {
            if (hudController == null || hudController.Slots == null
                || slotIndex < 0 || slotIndex >= hudController.Slots.Length)
            {
                return null;
            }

            return hudController.Slots[slotIndex];
        }

        private bool IsSkillUnlocked(GoldActionHudController.SkillDefinition skill)
        {
            if (skill == null)
            {
                return false;
            }

            CoreStatData stats = hudController.CoreStats != null
                ? hudController.CoreStats.CurrentStats
                : CoreStatProvider.GetCurrentOrDefault();
            return stats.Level >= skill.UnlockLevel;
        }

        private bool IsHudKeyboardInputBlocked()
        {
            if (GameplayInputBlocker.IsGameplayInputBlocked || Time.timeScale <= 0f)
            {
                return true;
            }

            if (cachedLevelUpUi == null)
            {
                cachedLevelUpUi = FindFirstObjectByType<global::LevelUpUi>(FindObjectsInactive.Include);
            }

            return cachedLevelUpUi != null
                   && (cachedLevelUpUi.IsPanelOpen || cachedLevelUpUi.IsPanelVisible);
        }

        private bool TryReadPurchased(
            int slotIndex,
            out GoldActionHudController.SkillDefinition skill,
            out bool purchased)
        {
            if (TryReadSkillState(slotIndex, out skill, out SkillRuntimeState state))
            {
                purchased = state.Purchased;
                return true;
            }

            skill = null;
            purchased = false;
            return false;
        }

        private bool TryReadSkillState(
            int slotIndex,
            out GoldActionHudController.SkillDefinition skill,
            out SkillRuntimeState state)
        {
            skill = null;
            state = default;

            if (hudController == null || hudController.Skills == null
                || slotIndex < 0 || slotIndex >= hudController.Skills.Length)
            {
                return false;
            }

            skill = hudController.Skills[slotIndex];
            if (skill == null || statesField == null || purchasedField == null
                || upgradeLevelField == null || repeatPurchaseCountField == null || cooldownEndsAtField == null)
            {
                return false;
            }

            object statesObject = statesField.GetValue(hudController);
            if (statesObject is not Array states || slotIndex >= states.Length)
            {
                return false;
            }

            object stateObject = states.GetValue(slotIndex);
            if (stateObject == null)
            {
                return false;
            }

            state = new SkillRuntimeState
            {
                Purchased = (bool)purchasedField.GetValue(stateObject),
                UpgradeLevel = (int)upgradeLevelField.GetValue(stateObject),
                RepeatPurchaseCount = (int)repeatPurchaseCountField.GetValue(stateObject),
                CooldownEndsAt = (float)cooldownEndsAtField.GetValue(stateObject)
            };
            return true;
        }

        private struct SkillRuntimeState
        {
            public bool Purchased;
            public int UpgradeLevel;
            public int RepeatPurchaseCount;
            public float CooldownEndsAt;
        }

        // GoldActionHudController.IsIconActive 와 동일 — 스킬 사용(아이콘) 가능 상태
        private bool IsSkillUseReady(int slotIndex)
        {
            if (!TryReadSkillState(slotIndex, out GoldActionHudController.SkillDefinition skill, out SkillRuntimeState state))
            {
                return false;
            }

            CoreStatData stats = hudController.CoreStats != null
                ? hudController.CoreStats.CurrentStats
                : CoreStatProvider.GetCurrentOrDefault();

            bool unlocked = stats.Level >= skill.UnlockLevel;
            bool coolingDown = Time.time < state.CooldownEndsAt;
            if (!unlocked || coolingDown)
            {
                return false;
            }

            if (skill.Kind == GoldActionHudController.GoldActionSkillKind.NexusHeal)
            {
                return stats.Gold >= skill.BaseCost
                       && hudController.NexusHealSkill != null
                       && hudController.NexusHealSkill.CanHeal();
            }

            if (skill.RepeatPurchase)
            {
                if (IsMaxSkillLevel(skill, state))
                {
                    return false;
                }

                if (skill.Kind == GoldActionHudController.GoldActionSkillKind.NexusShieldUpgrade
                    && (hudController.ShieldUpgradeSkill == null || !hudController.ShieldUpgradeSkill.CanUpgradeShieldVisual()))
                {
                    return false;
                }

                return stats.Gold >= GetRepeatPurchaseCost(skill, state);
            }

            if (IsPurchaseThenUpgradeSkill(skill))
            {
                return state.Purchased;
            }

            if (skill.CostOnUse && !skill.RequiresPurchase)
            {
                return stats.Gold >= skill.BaseCost;
            }

            return false;
        }

        private static bool IsMaxSkillLevel(GoldActionHudController.SkillDefinition skill, SkillRuntimeState state)
        {
            int maxLevel = skill.MaxLevel > 0 ? skill.MaxLevel : 5;
            if (skill.RepeatPurchase)
            {
                return state.RepeatPurchaseCount >= maxLevel;
            }

            return state.Purchased && state.UpgradeLevel >= maxLevel;
        }

        private int GetRepeatPurchaseCost(GoldActionHudController.SkillDefinition skill, SkillRuntimeState state)
        {
            int purchaseCount = Mathf.Max(0, state.RepeatPurchaseCount);
            if (skill.Kind == GoldActionHudController.GoldActionSkillKind.NexusShieldUpgrade && ShieldUpgradeCostSteps.Length > 0)
            {
                int index = Mathf.Clamp(purchaseCount, 0, ShieldUpgradeCostSteps.Length - 1);
                return ShieldUpgradeCostSteps[index];
            }

            float growth = Mathf.Pow(Mathf.Max(1f, hudController.ShieldUpgradeCostMultiplier), purchaseCount);
            return Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, skill.BaseCost) * growth));
        }

        private void StartReadyVfxLoop()
        {
            if (readyVfxLoopRunning)
            {
                return;
            }

            readyVfxLoopRunning = true;
            StartCoroutine(RefreshReadyVfxEndOfFrameLoop());
        }

        private IEnumerator RefreshReadyVfxEndOfFrameLoop()
        {
            WaitForEndOfFrame wait = new WaitForEndOfFrame();
            while (readyVfxLoopRunning && isActiveAndEnabled)
            {
                yield return wait;
                RefreshSkillReadyVfx();
            }
        }

        private void RefreshSkillReadyVfx()
        {
            if (skillReadyVfxPrefab == null || hudController == null || hudController.Skills == null)
            {
                ClearAllReadyVfx();
                return;
            }

            Canvas.ForceUpdateCanvases();

            int slotCount = hudController.Skills.Length;
            EnsureReadyVfxInstanceCache(slotCount);

            for (int i = 0; i < slotCount; i++)
            {
                bool shouldShow = IsSkillUseReady(i) && HasReadyVfxAnchor(i);
                if (shouldShow)
                {
                    EnsureReadyVfxPlaying(i);
                    SyncReadyVfxLayout(i);
                }
                else
                {
                    ClearReadyVfx(i);
                }
            }
        }

        private bool HasReadyVfxAnchor(int slotIndex)
        {
            return skillReadyVfxAnchors != null
                   && slotIndex >= 0
                   && slotIndex < skillReadyVfxAnchors.Length
                   && skillReadyVfxAnchors[slotIndex] != null;
        }

        private void EnsureReadyVfxInstanceCache(int slotCount)
        {
            if (activeReadyVfxInstances != null && activeReadyVfxInstances.Length == slotCount)
            {
                return;
            }

            ClearAllReadyVfx();
            activeReadyVfxInstances = new GameObject[slotCount];
        }

        private void EnsureReadyVfxPlaying(int slotIndex)
        {
            if (activeReadyVfxInstances == null || slotIndex < 0 || slotIndex >= activeReadyVfxInstances.Length)
            {
                return;
            }

            if (activeReadyVfxInstances[slotIndex] != null)
            {
                return;
            }

            if (!TryGetAnchorRectTransform(slotIndex, out RectTransform anchorRect, out Canvas referenceCanvas))
            {
                return;
            }

            Canvas targetCanvas = GetOrCreateVfxCanvas(referenceCanvas);

            GameObject container = new GameObject($"VFX_HudSkillReady_{slotIndex + 1}");
            RectTransform containerRect = container.AddComponent<RectTransform>();
            container.transform.SetParent(targetCanvas.transform, false);

            ApplyAnchorLayoutToContainer(containerRect, anchorRect);

            GameObject vfxInstance = Instantiate(skillReadyVfxPrefab, container.transform);
            vfxInstance.name = skillReadyVfxPrefab.name;
            vfxInstance.transform.localPosition = Vector3.zero;
            vfxInstance.transform.localRotation = Quaternion.identity;
            ApplyVfxLocalScale(vfxInstance.transform, containerRect.sizeDelta);

            SetupUiParticleLoop(vfxInstance);
            activeReadyVfxInstances[slotIndex] = container;
        }

        private void SyncReadyVfxLayout(int slotIndex)
        {
            if (activeReadyVfxInstances == null || slotIndex < 0 || slotIndex >= activeReadyVfxInstances.Length)
            {
                return;
            }

            GameObject container = activeReadyVfxInstances[slotIndex];
            if (container == null || !TryGetAnchorRectTransform(slotIndex, out RectTransform anchorRect, out Canvas referenceCanvas))
            {
                return;
            }

            GetOrCreateVfxCanvas(referenceCanvas);

            RectTransform containerRect = container.GetComponent<RectTransform>();
            if (containerRect == null)
            {
                return;
            }

            ApplyAnchorLayoutToContainer(containerRect, anchorRect);

            if (containerRect.childCount > 0)
            {
                ApplyVfxLocalScale(containerRect.GetChild(0), containerRect.sizeDelta);
            }
        }

        private bool TryGetAnchorRectTransform(int slotIndex, out RectTransform anchorRect, out Canvas referenceCanvas)
        {
            anchorRect = null;
            referenceCanvas = null;

            if (!HasReadyVfxAnchor(slotIndex))
            {
                return false;
            }

            Transform anchor = skillReadyVfxAnchors[slotIndex];
            anchorRect = anchor as RectTransform ?? anchor.GetComponent<RectTransform>();
            if (anchorRect == null)
            {
                return false;
            }

            referenceCanvas = anchorRect.GetComponentInParent<Canvas>();
            return referenceCanvas != null;
        }

        // CardEffect 와 동일 — VFX 전용 Overlay 캔버스 (HUD/UI 위에 렌더)
        private Canvas GetOrCreateVfxCanvas(Canvas referenceCanvas)
        {
            if (hudVfxCanvas == null)
            {
                Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                for (int i = 0; i < canvases.Length; i++)
                {
                    Canvas canvas = canvases[i];
                    if (canvas != null && canvas.name == "VFX_Canvas")
                    {
                        hudVfxCanvas = canvas;
                        break;
                    }
                }
            }

            if (hudVfxCanvas == null)
            {
                GameObject canvasObject = new GameObject("VFX_Canvas");
                hudVfxCanvas = canvasObject.AddComponent<Canvas>();
                hudVfxCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            int desiredOrder = referenceCanvas.sortingOrder + 1;
            if (hudVfxCanvas.sortingOrder < desiredOrder)
            {
                hudVfxCanvas.sortingOrder = desiredOrder;
            }

            return hudVfxCanvas;
        }

        private void ApplyAnchorLayoutToContainer(RectTransform containerRect, RectTransform anchorRect)
        {
            Vector3[] corners = new Vector3[4];
            anchorRect.GetWorldCorners(corners);

            Vector3 centerPx = (corners[0] + corners[2]) * 0.5f;
            float pixelW = Mathf.Abs(corners[3].x - corners[0].x);
            float pixelH = Mathf.Abs(corners[1].y - corners[0].y);

            if (pixelW < 1f)
            {
                pixelW = anchorRect.rect.width * Mathf.Abs(anchorRect.lossyScale.x);
            }

            if (pixelH < 1f)
            {
                pixelH = anchorRect.rect.height * Mathf.Abs(anchorRect.lossyScale.y);
            }

            Vector2 effectSize = ComputeReadyVfxSize(pixelW, pixelH);

            containerRect.anchorMin = Vector2.one * 0.5f;
            containerRect.anchorMax = Vector2.one * 0.5f;
            containerRect.pivot = Vector2.one * 0.5f;
            containerRect.position = new Vector3(
                centerPx.x + readyVfxPositionOffsetX,
                centerPx.y + readyVfxPositionOffsetY,
                0f);
            containerRect.sizeDelta = effectSize;
            containerRect.localRotation = Quaternion.identity;
            containerRect.localScale = Vector3.one;
        }

        private Vector2 ComputeReadyVfxSize(float anchorPixelW, float anchorPixelH)
        {
            float sizeFactor = Mathf.Max(0.001f, 1f + readyVfxSizeOffsetPercent / 100f);
            float width = Mathf.Max(1f, anchorPixelW * readyVfxWidthScale * sizeFactor + readyVfxWidthOffsetPx);
            float height = Mathf.Max(1f, anchorPixelH * readyVfxHeightScale * sizeFactor + readyVfxHeightOffsetPx);
            return new Vector2(width, height);
        }

        private void ApplyVfxLocalScale(Transform vfxTransform, Vector2 containerSize)
        {
            vfxTransform.localScale = new Vector3(
                containerSize.x * readyVfxParticleScale.x,
                containerSize.y * readyVfxParticleScale.y,
                readyVfxParticleScale.z);
        }

        private static void SetupUiParticleLoop(GameObject vfxRoot)
        {
            ParticleSystem[] particleSystems = vfxRoot.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps.gameObject.GetComponent<UIParticleSystem>() != null)
                {
                    continue;
                }

                bool wasActive = ps.gameObject.activeSelf;
                ps.gameObject.SetActive(false);

                UIParticleSystem uiPs = ps.gameObject.AddComponent<UIParticleSystem>();
                ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    uiPs.material = renderer.sharedMaterial;
                }
                else
                {
                    Destroy(uiPs);
                    ps.gameObject.SetActive(wasActive);
                    continue;
                }

                ps.gameObject.SetActive(wasActive);
                ParticleSystem.MainModule main = ps.main;
                main.loop = true;
                ps.Play(withChildren: false);
            }
        }

        private void ClearReadyVfx(int slotIndex)
        {
            if (activeReadyVfxInstances == null || slotIndex < 0 || slotIndex >= activeReadyVfxInstances.Length)
            {
                return;
            }

            if (activeReadyVfxInstances[slotIndex] == null)
            {
                return;
            }

            Destroy(activeReadyVfxInstances[slotIndex]);
            activeReadyVfxInstances[slotIndex] = null;
        }

        private void ClearAllReadyVfx()
        {
            if (activeReadyVfxInstances == null)
            {
                return;
            }

            for (int i = 0; i < activeReadyVfxInstances.Length; i++)
            {
                ClearReadyVfx(i);
            }
        }

        private void PlaySkillPurchaseSfx()
        {
            if (skillPurchaseClip == null)
            {
                return;
            }

            AudioManager.PlayUiSfxClip(skillPurchaseClip, skillPurchaseVolume);
        }

        private void PlaySkillUpgradeSfx()
        {
            if (skillUpgradeClip == null)
            {
                return;
            }

            AudioManager.PlayUiSfxClip(skillUpgradeClip, skillUpgradeVolume);
        }

        private sealed class SlotSfxPointerDownHandler : MonoBehaviour, IPointerDownHandler
        {
            private ActionHudFeature owner;
            private int slotIndex;

            public void Initialize(ActionHudFeature feature, int index)
            {
                owner = feature;
                slotIndex = index;
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                owner?.CachePendingSfxForSlot(slotIndex);
            }
        }
    }
}
