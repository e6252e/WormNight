using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class MinimapController : MonoBehaviour
    {
        private const string BackgroundName = "MinimapBackground";
        private const string MaskName = "MinimapMask";
        private const string MarkerRootName = "MinimapMarkers";
        private const string RingName = "MinimapRing";
        private const string NexusDirectionName = "MinimapNexusDirection";
        private const string NexusDirectionArrowName = "Arrow";
        private const string NexusDirectionDistanceName = "Distance";
        private const string ZoomButtonRootName = "MinimapZoomButtons";
        private const string ZoomInButtonName = "ZoomIn";
        private const string ZoomOutButtonName = "ZoomOut";
        private const string SettingsButtonName = "Settings";
        private const string ZoomValueName = "ZoomValue";
        private const string ButtonVisualName = "Visual";
        private const string ButtonLabelName = "Label";
        private const float MinimapPadding = 8f;

        private static Sprite circleSprite;
        private static Sprite triangleSprite;

        [Header("Layout")]
        [SerializeField] private Sprite circleSpriteAsset;
        [SerializeField] private Sprite frameSpriteAsset;
        [SerializeField, Min(80f)] private float mapSize = 232f;
        [SerializeField] private Vector2 topRightOffset = new Vector2(-22f, -22f);
        [SerializeField] private int sortingOrder = 90;

        [Header("Ring")]
        [SerializeField] private Vector2 ringAnchoredPosition = new Vector2(0.000015258789f, 3.600006f);
        [SerializeField] private Vector2 ringSizeDelta = new Vector2(22.2024f, 22.2024f);

        [Header("World")]
        [SerializeField, Min(5f)] private float worldRadius = 58f;
        [SerializeField] private bool rotateWithCamera = true;
        [SerializeField] private bool alwaysClampNexusToEdge = true;
        [SerializeField, Min(0.02f)] private float updateInterval = 0.08f;

        [Header("Zoom")]
        [SerializeField, Min(5f)] private float minWorldRadius = 24f;
        [SerializeField, Min(5f)] private float maxWorldRadius = 140f;
        [SerializeField, Min(1f)] private float zoomStep = 10f;
        [SerializeField] private Sprite zoomInButtonSpriteAsset;
        [SerializeField] private Sprite zoomInButtonPressedSpriteAsset;
        [SerializeField] private Sprite zoomOutButtonSpriteAsset;
        [SerializeField] private Sprite zoomOutButtonPressedSpriteAsset;
        [SerializeField] private Sprite settingsButtonSpriteAsset;
        [SerializeField] private Sprite settingsButtonPressedSpriteAsset;
        [SerializeField] private Vector2 zoomButtonPressedOffset = new Vector2(1.5f, -1.5f);

        [Header("Markers")]
        [SerializeField, Min(1f)] private float playerMarkerSize = 10f;
        [SerializeField, Min(1f)] private float segmentMarkerSize = 5.5f;
        [SerializeField, Min(1f)] private float nexusMarkerSize = 13f;
        [SerializeField, Min(1f)] private float enemyMarkerSize = 4.6f;
        [SerializeField, Min(1f)] private float rewardMarkerSize = 3.8f;
        [SerializeField, Range(8, 256)] private int maxEnemyMarkers = 160;
        [SerializeField, Range(8, 256)] private int maxRewardMarkers = 160;
        [SerializeField, Range(1, 128)] private int maxSegmentMarkers = 96;

        [Header("Nexus Direction")]
        [SerializeField] private bool showNexusOffscreenIndicator = true;
        [SerializeField, Min(1f)] private float offscreenArrowSize = 18f;
        [SerializeField, Min(0f)] private float offscreenIndicatorInset = 15f;
        [SerializeField, Min(0f)] private float offscreenDistanceInset = 24f;
        [SerializeField, Min(8)] private int offscreenDistanceFontSize = 12;

        private readonly List<EnemyController> enemyBuffer = new List<EnemyController>(160);
        private readonly List<WorldRewardPickup> rewardBuffer = new List<WorldRewardPickup>(160);
        private readonly List<Image> playerMarkers = new List<Image>(128);
        private readonly List<Image> nexusMarkers = new List<Image>(1);
        private readonly List<Image> enemyMarkers = new List<Image>(160);
        private readonly List<Image> rewardMarkers = new List<Image>(160);

        private RectTransform mapRoot;
        private RectTransform markerRoot;
        private Image backgroundImage;
        private RectTransform nexusDirectionRoot;
        private Image nexusDirectionArrow;
        private Text nexusDirectionDistance;
        private RectTransform zoomButtonRoot;
        private Text zoomValueText;
        private ConvoyController convoy;
        private NexusController nexus;
        private Camera mainCamera;
        private float zoomBaseWorldRadius;
        private float nextUpdateTime;

        private void Awake()
        {
            EnsureUi();
            if (Application.isPlaying)
            {
                RefreshReferences(true);
            }
        }

#if UNITY_EDITOR
        public void RebuildStaticUiForEditor()
        {
            EnsureUi();
        }
#endif

        private void OnEnable()
        {
            EnsureUi();
            if (!Application.isPlaying)
            {
                return;
            }

            RefreshReferences(true);
            UpdateMarkers();
        }

        private void LateUpdate()
        {
            if (mapRoot == null || markerRoot == null || nexusDirectionRoot == null || zoomButtonRoot == null)
            {
                EnsureUi();
            }

            if (!Application.isPlaying)
            {
                return;
            }

            if (Time.unscaledTime < nextUpdateTime)
            {
                return;
            }

            nextUpdateTime = Time.unscaledTime + updateInterval;
            RefreshReferences(false);
            UpdateMarkers();
        }

        private void EnsureUi()
        {
            int uiLayer = UiLayer();
            gameObject.layer = uiLayer;

            RectTransform rootRect = GetComponent<RectTransform>();
            if (rootRect == null)
            {
                rootRect = gameObject.AddComponent<RectTransform>();
            }

            if (GetComponentInParent<Canvas>() == null)
            {
                Canvas fallbackCanvas = EnsureComponent<Canvas>(gameObject);
                fallbackCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                fallbackCanvas.overrideSorting = true;
                fallbackCanvas.sortingOrder = sortingOrder;

                CanvasScaler fallbackScaler = EnsureComponent<CanvasScaler>(gameObject);
                fallbackScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                fallbackScaler.referenceResolution = new Vector2(1920f, 1080f);
                fallbackScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                fallbackScaler.matchWidthOrHeight = 0.5f;

                GraphicRaycaster fallbackRaycaster = EnsureComponent<GraphicRaycaster>(gameObject);
                fallbackRaycaster.enabled = false;
            }

            mapRoot = rootRect;
            mapRoot.anchorMin = new Vector2(1f, 1f);
            mapRoot.anchorMax = new Vector2(1f, 1f);
            mapRoot.pivot = new Vector2(1f, 1f);
            mapRoot.anchoredPosition = topRightOffset;
            mapRoot.sizeDelta = new Vector2(mapSize, mapSize);
            mapRoot.localScale = Vector3.one;
            mapRoot.gameObject.layer = uiLayer;

            RectTransform background = EnsureChildRect(mapRoot, BackgroundName);
            Stretch(background);
            backgroundImage = EnsureComponent<Image>(background.gameObject);
            backgroundImage.sprite = ResolveCircleSprite();
            backgroundImage.color = new Color(0.03f, 0.045f, 0.055f, 0.82f);
            backgroundImage.raycastTarget = false;

            RectTransform maskRect = EnsureChildRect(mapRoot, MaskName);
            Stretch(maskRect);
            Image maskImage = EnsureComponent<Image>(maskRect.gameObject);
            maskImage.sprite = ResolveCircleSprite();
            maskImage.color = Color.white;
            maskImage.raycastTarget = false;

            Mask mask = EnsureComponent<Mask>(maskRect.gameObject);
            mask.showMaskGraphic = false;

            markerRoot = EnsureChildRect(maskRect, MarkerRootName);
            Stretch(markerRoot);
            markerRoot.localScale = Vector3.one;

            EnsureRingVisual(mapRoot);
            EnsureNexusDirectionUi(uiLayer);
            EnsureZoomControls(uiLayer);
            SetLayerRecursively(gameObject, uiLayer);
        }

        private void RefreshReferences(bool force)
        {
            if (force || convoy == null)
            {
                convoy = FindFirstObjectByType<ConvoyController>();
            }

            if (force || nexus == null)
            {
                nexus = NexusController.Active != null ? NexusController.Active : FindFirstObjectByType<NexusController>();
            }

            if (force || mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }

        private void UpdateMarkers()
        {
            if (markerRoot == null)
            {
                return;
            }

            Vector3 center = ResolveMapCenter();
            UpdatePlayerAndSegmentMarkers(center);
            UpdateNexusMarker(center);
            UpdateEnemyMarkers(center);
            UpdateRewardMarkers(center);
        }

        private Vector3 ResolveMapCenter()
        {
            if (convoy != null)
            {
                return convoy.transform.position;
            }

            if (nexus != null)
            {
                return nexus.transform.position;
            }

            return Vector3.zero;
        }

        private void UpdatePlayerAndSegmentMarkers(Vector3 center)
        {
            int used = 0;

            if (convoy != null)
            {
                used = ShowMarker(
                    playerMarkers,
                    used,
                    "Player",
                    convoy.transform.position,
                    center,
                    playerMarkerSize,
                    new Color(0.2f, 0.95f, 1f, 1f),
                    false,
                    0f);

                Transform segmentRoot = convoy.SegmentRoot;
                if (segmentRoot != null)
                {
                    int segmentLimit = Mathf.Min(segmentRoot.childCount, maxSegmentMarkers);
                    for (int i = 0; i < segmentLimit; i++)
                    {
                        Transform segment = segmentRoot.GetChild(i);
                        if (segment == null || !segment.gameObject.activeInHierarchy)
                        {
                            continue;
                        }

                        used = ShowMarker(
                            playerMarkers,
                            used,
                            "Segment",
                            segment.position,
                            center,
                            segmentMarkerSize,
                            new Color(0.38f, 1f, 0.66f, 0.92f),
                            false,
                            0f);
                    }
                }
            }

            HideUnused(playerMarkers, used);
        }

        private void UpdateNexusMarker(Vector3 center)
        {
            int used = 0;
            if (nexus != null)
            {
                used = ShowMarker(
                    nexusMarkers,
                    used,
                    "Nexus",
                    nexus.transform.position,
                    center,
                    nexusMarkerSize,
                    new Color(0.25f, 0.72f, 1f, 1f),
                    alwaysClampNexusToEdge,
                    45f);
                UpdateNexusDirectionIndicator(center);
            }
            else
            {
                SetNexusDirectionVisible(false);
            }

            HideUnused(nexusMarkers, used);
        }

        private void UpdateEnemyMarkers(Vector3 center)
        {
            EnemyController.CollectActiveInRange(center, worldRadius, enemyBuffer);
            int count = Mathf.Min(enemyBuffer.Count, maxEnemyMarkers);
            int used = 0;
            for (int i = 0; i < count; i++)
            {
                EnemyController enemy = enemyBuffer[i];
                if (enemy == null)
                {
                    continue;
                }

                Color color = ResolveEnemyColor(enemy);
                used = ShowMarker(enemyMarkers, used, "Enemy", enemy.transform.position, center, enemyMarkerSize, color, false, 0f);
            }

            HideUnused(enemyMarkers, used);
        }

        private void UpdateRewardMarkers(Vector3 center)
        {
            WorldRewardPickup.CollectActiveInRange(center, worldRadius, rewardBuffer);
            int count = Mathf.Min(rewardBuffer.Count, maxRewardMarkers);
            int used = 0;
            for (int i = 0; i < count; i++)
            {
                WorldRewardPickup pickup = rewardBuffer[i];
                if (pickup == null)
                {
                    continue;
                }

                Color color = ResolveRewardMarkerColor(pickup.Kind);
                used = ShowMarker(rewardMarkers, used, "Reward", pickup.transform.position, center, rewardMarkerSize, color, false, 0f);
            }

            HideUnused(rewardMarkers, used);
        }

        private static Color ResolveRewardMarkerColor(RewardPickupKind kind)
        {
            if (kind == RewardPickupKind.Diamond)
            {
                return new Color(0.35f, 0.9f, 1f, 0.96f);
            }

            if (kind == RewardPickupKind.Gold)
            {
                return new Color(1f, 0.78f, 0.14f, 0.96f);
            }

            return kind == RewardPickupKind.SegmentChoiceTicket
                ? new Color(0.42f, 0.82f, 1f, 0.96f)
                : new Color(0.22f, 1f, 0.36f, 0.96f);
        }

        private int ShowMarker(
            List<Image> pool,
            int index,
            string prefix,
            Vector3 worldPosition,
            Vector3 center,
            float size,
            Color color,
            bool clampToEdge,
            float localRotationZ)
        {
            if (index >= pool.Capacity && pool.Capacity > 0)
            {
                return index;
            }

            Image marker = GetMarker(pool, index, prefix);
            RectTransform rect = marker.rectTransform;
            rect.anchoredPosition = WorldToMapPosition(worldPosition, center, clampToEdge);
            rect.sizeDelta = new Vector2(size, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, localRotationZ);
            marker.color = color;
            marker.gameObject.SetActive(true);
            return index + 1;
        }

        private void UpdateNexusDirectionIndicator(Vector3 center)
        {
            if (!showNexusOffscreenIndicator || nexusDirectionRoot == null || nexusDirectionArrow == null || nexusDirectionDistance == null)
            {
                SetNexusDirectionVisible(false);
                return;
            }

            CalculateMapProjection(
                nexus.transform.position,
                center,
                out Vector2 direction,
                out float worldDistance,
                out float pixelRadius,
                out bool isOutside);

            if (!isOutside)
            {
                SetNexusDirectionVisible(false);
                return;
            }

            float arrowRadius = Mathf.Max(1f, pixelRadius - offscreenIndicatorInset);
            Vector2 arrowPosition = direction * arrowRadius;
            RectTransform arrowRect = nexusDirectionArrow.rectTransform;
            arrowRect.anchoredPosition = arrowPosition;
            arrowRect.sizeDelta = new Vector2(offscreenArrowSize, offscreenArrowSize);
            arrowRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);

            RectTransform distanceRect = nexusDirectionDistance.rectTransform;
            distanceRect.anchoredPosition = arrowPosition - direction * offscreenDistanceInset;
            distanceRect.sizeDelta = new Vector2(68f, 20f);
            distanceRect.localRotation = Quaternion.identity;
            nexusDirectionDistance.fontSize = offscreenDistanceFontSize;
            nexusDirectionDistance.text = $"{Mathf.RoundToInt(worldDistance)}m";

            SetNexusDirectionVisible(true);
        }

        private void SetNexusDirectionVisible(bool visible)
        {
            if (nexusDirectionRoot != null && nexusDirectionRoot.gameObject.activeSelf != visible)
            {
                nexusDirectionRoot.gameObject.SetActive(visible);
            }
        }

        private void ZoomIn()
        {
            SetWorldRadius(worldRadius - zoomStep);
        }

        private void ZoomOut()
        {
            SetWorldRadius(worldRadius + zoomStep);
        }

        private void SetWorldRadius(float value)
        {
            float safeMin = Mathf.Max(1f, minWorldRadius);
            float safeMax = Mathf.Max(safeMin, maxWorldRadius);
            worldRadius = Mathf.Clamp(value, safeMin, safeMax);
            UpdateZoomValueLabel();

            if (Application.isPlaying)
            {
                UpdateMarkers();
            }
        }

        private Image GetMarker(List<Image> pool, int index, string prefix)
        {
            while (pool.Count <= index)
            {
                GameObject markerObject = new GameObject($"{prefix}_Marker_{pool.Count:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                markerObject.transform.SetParent(markerRoot, false);
                markerObject.layer = UiLayer();

                Image marker = markerObject.GetComponent<Image>();
                marker.sprite = ResolveCircleSprite();
                marker.raycastTarget = false;
                marker.maskable = true;
                pool.Add(marker);
            }

            return pool[index];
        }

        private void HideUnused(List<Image> pool, int used)
        {
            for (int i = used; i < pool.Count; i++)
            {
                Image marker = pool[i];
                if (marker != null && marker.gameObject.activeSelf)
                {
                    marker.gameObject.SetActive(false);
                }
            }
        }

        private Vector2 WorldToMapPosition(Vector3 worldPosition, Vector3 center, bool clampToEdge)
        {
            Vector2 position = CalculateMapProjection(
                worldPosition,
                center,
                out Vector2 direction,
                out _,
                out float pixelRadius,
                out bool isOutside);

            if (clampToEdge && isOutside)
            {
                position = direction * pixelRadius;
            }

            return position;
        }

        private Vector2 CalculateMapProjection(
            Vector3 worldPosition,
            Vector3 center,
            out Vector2 direction,
            out float worldDistance,
            out float pixelRadius,
            out bool isOutside)
        {
            Vector3 delta = worldPosition - center;
            Vector2 flat = new Vector2(delta.x, delta.z);
            worldDistance = flat.magnitude;

            if (rotateWithCamera && mainCamera != null)
            {
                flat = Rotate(flat, mainCamera.transform.eulerAngles.y);
            }

            float safeWorldRadius = Mathf.Max(1f, worldRadius);
            pixelRadius = Mathf.Max(1f, mapSize * 0.5f - MinimapPadding);
            Vector2 position = flat / safeWorldRadius * pixelRadius;
            direction = position.sqrMagnitude > 0.0001f ? position.normalized : Vector2.up;
            isOutside = worldDistance > safeWorldRadius;
            return position;
        }

        private static Vector2 Rotate(Vector2 value, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(
                value.x * cos - value.y * sin,
                value.x * sin + value.y * cos);
        }

        private static Color ResolveEnemyColor(EnemyController enemy)
        {
            return enemy.Grade switch
            {
                EnemyGrade.Boss => new Color(1f, 0.14f, 0.68f, 1f),
                EnemyGrade.Elite => new Color(1f, 0.42f, 0.12f, 1f),
                _ => new Color(1f, 0.22f, 0.2f, 0.95f)
            };
        }

        private static RectTransform EnsureChildRect(Transform parent, string childName)
        {
            return EnsureChildRect(parent, childName, out _);
        }

        private static RectTransform EnsureChildRect(Transform parent, string childName, out bool created)
        {
            created = false;
            Transform child = parent.Find(childName);
            if (child == null)
            {
                GameObject childObject = new GameObject(childName, typeof(RectTransform));
                childObject.transform.SetParent(parent, false);
                child = childObject.transform;
                created = true;
            }

            RectTransform rect = child as RectTransform;
            if (rect == null)
            {
                rect = child.gameObject.GetComponent<RectTransform>();
            }

            return rect;
        }

        private void EnsureNexusDirectionUi(int uiLayer)
        {
            nexusDirectionRoot = EnsureChildRect(mapRoot, NexusDirectionName);
            Stretch(nexusDirectionRoot);
            nexusDirectionRoot.SetAsLastSibling();

            RectTransform arrowRect = EnsureChildRect(nexusDirectionRoot, NexusDirectionArrowName);
            SetCenterRect(arrowRect, Vector2.zero, new Vector2(offscreenArrowSize, offscreenArrowSize));
            nexusDirectionArrow = EnsureComponent<Image>(arrowRect.gameObject);
            nexusDirectionArrow.sprite = GetTriangleSprite();
            nexusDirectionArrow.color = new Color(0.25f, 0.78f, 1f, 0.96f);
            nexusDirectionArrow.raycastTarget = false;

            RectTransform distanceRect = EnsureChildRect(nexusDirectionRoot, NexusDirectionDistanceName);
            SetCenterRect(distanceRect, Vector2.zero, new Vector2(68f, 20f));
            nexusDirectionDistance = EnsureComponent<Text>(distanceRect.gameObject);
            nexusDirectionDistance.font = ResolveDefaultFont();
            nexusDirectionDistance.fontSize = offscreenDistanceFontSize;
            nexusDirectionDistance.alignment = TextAnchor.MiddleCenter;
            nexusDirectionDistance.color = new Color(0.72f, 0.94f, 1f, 0.98f);
            nexusDirectionDistance.raycastTarget = false;
            nexusDirectionDistance.text = string.Empty;

            SetLayerRecursively(nexusDirectionRoot.gameObject, uiLayer);
            if (!Application.isPlaying)
            {
                SetNexusDirectionVisible(false);
            }
        }

        private void EnsureZoomControls(int uiLayer)
        {
            zoomButtonRoot = EnsureChildRect(mapRoot, ZoomButtonRootName, out bool createdRoot);
            if (createdRoot)
            {
                zoomButtonRoot.anchorMin = new Vector2(1f, 1f);
                zoomButtonRoot.anchorMax = new Vector2(1f, 1f);
                zoomButtonRoot.pivot = new Vector2(1f, 1f);
                zoomButtonRoot.anchoredPosition = new Vector2(-10f, -12f);
                zoomButtonRoot.sizeDelta = new Vector2(30f, 64f);
                zoomButtonRoot.localScale = Vector3.one;
                zoomButtonRoot.localRotation = Quaternion.identity;
            }

            zoomButtonRoot.SetAsLastSibling();

            ConfigureZoomButton(ZoomInButtonName, "+", new Vector2(0f, -15f), ZoomIn);
            ConfigureZoomButton(ZoomOutButtonName, "-", new Vector2(0f, -49f), ZoomOut);
            ConfigureZoomButton(SettingsButtonName, string.Empty, new Vector2(-32f, -15f), null);
            ConfigureZoomValueLabel();
            UpdateZoomValueLabel();
            SetLayerRecursively(zoomButtonRoot.gameObject, uiLayer);
        }

        private void ConfigureZoomButton(string buttonName, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
        {
            RectTransform buttonRect = EnsureChildRect(zoomButtonRoot, buttonName, out bool createdButton);
            if (createdButton)
            {
                buttonRect.anchorMin = new Vector2(0.5f, 1f);
                buttonRect.anchorMax = new Vector2(0.5f, 1f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.anchoredPosition = anchoredPosition;
                buttonRect.sizeDelta = new Vector2(28f, 28f);
                buttonRect.localScale = Vector3.one;
                buttonRect.localRotation = Quaternion.identity;
            }

            Image image = EnsureComponent<Image>(buttonRect.gameObject);
            image.color = Color.clear;
            image.sprite = null;
            image.raycastTarget = true;

            Button button = EnsureComponent<Button>(buttonRect.gameObject);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            if (action != null)
            {
                button.onClick.RemoveListener(action);
                button.onClick.AddListener(action);
            }
            else
            {
                button.onClick.RemoveAllListeners();
            }

            bool zoomIn = buttonName == ZoomInButtonName;
            bool settings = buttonName == SettingsButtonName;
            Sprite normalSprite = settings ? settingsButtonSpriteAsset : zoomIn ? zoomInButtonSpriteAsset : zoomOutButtonSpriteAsset;
            Sprite pressedSprite = settings ? settingsButtonPressedSpriteAsset : zoomIn ? zoomInButtonPressedSpriteAsset : zoomOutButtonPressedSpriteAsset;

            RectTransform visualRect = EnsureChildRect(buttonRect, ButtonVisualName);
            Stretch(visualRect);
            Image visualImage = EnsureComponent<Image>(visualRect.gameObject);
            visualImage.sprite = normalSprite;
            visualImage.color = Color.white;
            visualImage.preserveAspect = true;
            visualImage.raycastTarget = false;
            visualImage.enabled = normalSprite != null;

            MinimapZoomButtonPressVisual pressVisual = EnsureComponent<MinimapZoomButtonPressVisual>(buttonRect.gameObject);
            pressVisual.Configure(visualRect, visualImage, normalSprite, pressedSprite, zoomButtonPressedOffset);

            RectTransform labelRect = EnsureChildRect(buttonRect, ButtonLabelName);
            Stretch(labelRect);
            Text text = EnsureComponent<Text>(labelRect.gameObject);
            text.font = ResolveDefaultFont();
            text.text = label;
            text.fontSize = 20;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.9f, 0.98f, 1f, 1f);
            text.raycastTarget = false;
            labelRect.gameObject.SetActive(normalSprite == null);
        }

        private void ConfigureZoomValueLabel()
        {
            RectTransform valueRect = EnsureChildRect(zoomButtonRoot, ZoomValueName, out bool created);
            if (created)
            {
                valueRect.anchorMin = new Vector2(0.5f, 1f);
                valueRect.anchorMax = new Vector2(0.5f, 1f);
                valueRect.pivot = new Vector2(0.5f, 0.5f);
                valueRect.anchoredPosition = new Vector2(0f, -79f);
                valueRect.sizeDelta = new Vector2(50f, 18f);
                valueRect.localScale = Vector3.one;
                valueRect.localRotation = Quaternion.identity;
            }

            zoomValueText = EnsureComponent<Text>(valueRect.gameObject);
            zoomValueText.font = ResolveDefaultFont();
            zoomValueText.fontSize = 11;
            zoomValueText.fontStyle = FontStyle.Bold;
            zoomValueText.alignment = TextAnchor.MiddleCenter;
            zoomValueText.color = new Color(0.72f, 0.94f, 1f, 0.98f);
            zoomValueText.raycastTarget = false;

            Outline outline = EnsureComponent<Outline>(valueRect.gameObject);
            outline.effectColor = new Color(0f, 0.04f, 0.07f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private void UpdateZoomValueLabel()
        {
            if (zoomValueText == null)
            {
                return;
            }

            if (zoomBaseWorldRadius <= 0.001f)
            {
                zoomBaseWorldRadius = Mathf.Max(1f, worldRadius);
            }

            float multiplier = zoomBaseWorldRadius / Mathf.Max(1f, worldRadius);
            zoomValueText.text = multiplier.ToString("0.0", CultureInfo.InvariantCulture) + "x";
        }

        private static void SetCenterRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private void EnsureRingVisual(Transform parent)
        {
            RectTransform ringRect = EnsureChildRect(parent, RingName, out bool created);
            ApplyRingLayout(ringRect);
            if (created)
            {
                ringRect.SetAsLastSibling();
            }

            Image image = EnsureComponent<Image>(ringRect.gameObject);
            image.enabled = frameSpriteAsset != null;
            image.sprite = frameSpriteAsset;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private void ApplyRingLayout(RectTransform ringRect)
        {
            ringRect.anchorMin = Vector2.zero;
            ringRect.anchorMax = Vector2.one;
            ringRect.pivot = new Vector2(0.5f, 0.5f);
            ringRect.anchoredPosition = ringAnchoredPosition;
            ringRect.sizeDelta = ringSizeDelta;
            ringRect.localScale = Vector3.one;
            ringRect.localRotation = Quaternion.identity;
        }

        private static Font ResolveDefaultFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            foreach (Transform child in target.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static int UiLayer()
        {
            int layer = LayerMask.NameToLayer("UI");
            return layer >= 0 ? layer : 5;
        }

        private Sprite ResolveCircleSprite()
        {
            return circleSpriteAsset != null ? circleSpriteAsset : GetCircleSprite();
        }

        private static Sprite GetCircleSprite()
        {
            if (circleSprite == null)
            {
                circleSprite = CreateCircleSprite("MinimapCircleSprite", 64, 0f, 0.48f);
            }

            return circleSprite;
        }

        private static Sprite GetTriangleSprite()
        {
            if (triangleSprite == null)
            {
                triangleSprite = CreateTriangleSprite("MinimapNexusDirectionTriangleSprite", 48);
            }

            return triangleSprite;
        }

        private static Sprite CreateCircleSprite(string spriteName, int size, float innerRadius, float outerRadius)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = spriteName + "Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float safeInner = Mathf.Clamp01(innerRadius);
            float safeOuter = Mathf.Clamp01(Mathf.Max(outerRadius, safeInner));

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / size;
                    bool inside = distance >= safeInner && distance <= safeOuter;
                    float edgeFade = Mathf.InverseLerp(safeOuter, safeOuter - 0.025f, distance);
                    float innerFade = safeInner <= 0f ? 1f : Mathf.InverseLerp(safeInner, safeInner + 0.025f, distance);
                    float alpha = inside ? Mathf.Clamp01(Mathf.Min(edgeFade, innerFade)) : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = spriteName;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateTriangleSprite(string spriteName, int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = spriteName + "Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < size; y++)
            {
                float normalizedY = y / (float)(size - 1);
                float halfWidth = Mathf.Lerp(0.42f, 0.05f, normalizedY);
                float centerX = (size - 1) * 0.5f;
                float left = centerX - halfWidth * size;
                float right = centerX + halfWidth * size;

                for (int x = 0; x < size; x++)
                {
                    float edgeFade = Mathf.Min(
                        Mathf.InverseLerp(left - 1.5f, left + 1.5f, x),
                        Mathf.InverseLerp(right + 1.5f, right - 1.5f, x));
                    float verticalFade = Mathf.Min(
                        Mathf.InverseLerp(0f, 3f, y),
                        Mathf.InverseLerp(size - 1f, size - 4f, y));
                    float alpha = Mathf.Clamp01(Mathf.Min(edgeFade, verticalFade));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = spriteName;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
