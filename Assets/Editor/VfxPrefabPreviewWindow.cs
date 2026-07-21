using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class VfxPrefabPreviewWindow : EditorWindow
{
    private const string WindowTitle = "VFX 프리팹 미리보기";
    private const string MenuPath = "JC Tool/VFX/VFX 프리팹 미리보기";
    private const string FooterBrandText = "JC Soft";
    private const string PreviewInstancePrefix = "[JC VFX Preview] ";
    private const string PreviewFloorName = "[JC VFX Preview Floor]";
    private const string PreviewFloorMaterialName = "[JC VFX Preview Floor Material]";
    private const string LegacyMirrorCameraName = "[JC VFX Preview Mirror Camera]";
    private const string LegacyPreviewCameraName = "[JC VFX Preview Camera]";
    private const string LegacyPreviewLightName = "[JC VFX Preview Light]";
    private const float ControlHeight = 24f;
    private const float FieldLabelWidth = 64f;
    private const float StandardButtonWidth = 88f;
    private const float ShortButtonWidth = 66f;
    private const float PlaybackButtonWidth = 74f;
    private const float ViewButtonWidth = 48f;
    private const float RowGap = 6f;
    private const float SectionGap = 8f;
    private const float HeaderHeight = 62f;
    private const float FooterHeight = 30f;
    private const float FooterBrandWidth = 72f;
    private const float GridHorizontalMargin = 8f;
    private const float CameraSectionWidth = 310f;
    private const float PlaybackSectionMinWidth = 360f;
    private const float ViewPlaybackSectionHeight = 146f;
    private const float MinPreviewSectionHeight = 140f;
    private const float PreviewSectionInnerOffset = 40f;
    private const float TargetSectionHeight = 64f;
    private const float EmptyTargetNoticeHeight = 28f;
    private const float PreviewLayoutSlack = 16f;
    private const float InitialVisiblePreviewTime = 0.08f;
    private const float FirstVisiblePreviewScanMaxTime = 2f;
    private const float FirstVisiblePreviewScanStep = 0.05f;
    private const float MinCameraSize = 1.5f;
    private const float CameraFitPadding = 1.35f;
    private const float FloorPaddingMultiplier = 3.5f;
    private const float MinFloorSize = 4f;
    private const float FloorYOffset = 0.03f;
    private const float OrbitSensitivity = 0.35f;
    private const float CameraBlendDuration = 0.28f;
    private const float AxisOverlaySize = 86f;
    private const float AxisOverlayPadding = 12f;
    private const float AxisOverlayLineLength = 26f;
    private const float MinCameraPitch = -85f;
    private const float MaxCameraPitch = 85f;
    private const double PlayingPreviewTickInterval = 1d / 60d;
    private static readonly Color PreviewBackgroundColor = new Color(0.16f, 0.18f, 0.21f, 1f);
    private static readonly Color PreviewFloorColor = new Color(0.34f, 0.355f, 0.365f, 1f);
    private static readonly Color PreviewAmbientColor = new Color(0.38f, 0.41f, 0.46f, 1f);
    private static readonly Color PreviewKeyLightColor = new Color(1f, 0.96f, 0.9f, 1f);
    private static readonly Color PreviewFillLightColor = new Color(0.58f, 0.7f, 1f, 1f);
    private static readonly Vector2 MinimumWindowSize = new Vector2(700f, 480f);

    private static readonly ViewPreset[] ViewPresets =
    {
        new ViewPreset("앞", new Vector3(0f, 0f, -1f)),
        new ViewPreset("뒤", new Vector3(0f, 0f, 1f)),
        new ViewPreset("좌", new Vector3(-1f, 0f, 0f)),
        new ViewPreset("우", new Vector3(1f, 0f, 0f)),
        new ViewPreset("상", new Vector3(0f, 1f, 0f)),
        new ViewPreset("하", new Vector3(0f, -1f, 0f)),
        new ViewPreset("좌상", new Vector3(-1f, 1f, -1f)),
        new ViewPreset("우상", new Vector3(1f, 1f, -1f)),
        new ViewPreset("좌하", new Vector3(-1f, -1f, -1f)),
        new ViewPreset("우하", new Vector3(1f, -1f, -1f))
    };

    private GameObject selectedPrefab;
    private GameObject previewInstance;
    private GameObject floorInstance;
    private Material floorMaterial;
    private PreviewRenderUtility previewUtility;
    private ParticleSystem[] particleSystems = Array.Empty<ParticleSystem>();
    private ParticleSystem[] rootParticleSystems = Array.Empty<ParticleSystem>();
    private ParticleSystem.Particle[] particleBuffer = Array.Empty<ParticleSystem.Particle>();
    private Renderer[] renderers = Array.Empty<Renderer>();
    private Vector3 previewCenter;
    private float previewRadius = 1f;
    private Vector3 frameCenter;
    private float frameRadius = 1f;
    private float frameBottomY;
    private float cameraYaw = 180f;
    private float cameraPitch;
    private float cameraDistanceScale = 1f;
    private bool cameraBlendActive;
    private float cameraBlendStartYaw;
    private float cameraBlendStartPitch;
    private float cameraBlendStartDistanceScale;
    private float cameraBlendTargetYaw;
    private float cameraBlendTargetPitch;
    private float cameraBlendTargetDistanceScale = 1f;
    private double cameraBlendStartTime;
    private string previewMessage;
    private int viewIndex;
    private int liveParticleCount;
    private float playbackTime;
    private float playbackDuration = 3f;
    private bool isPlaying = true;
    private bool loopPlayback = true;
    private double lastUpdateTime;
    private double lastRepaintTime;

    private GUIStyle headerTitleStyle;
    private GUIStyle headerMetaStyle;
    private GUIStyle sectionStyle;
    private GUIStyle sectionTitleStyle;
    private GUIStyle fieldLabelStyle;
    private GUIStyle compactButtonStyle;
    private GUIStyle noticeStyle;
    private GUIStyle footerStyle;
    private GUIStyle footerBrandStyle;
    private GUIStyle axisLabelStyle;

    [MenuItem(MenuPath)]
    private static void Open()
    {
        VfxPrefabPreviewWindow window = GetWindow<VfxPrefabPreviewWindow>(WindowTitle);
        window.minSize = MinimumWindowSize;
        window.TryUseSelection();
    }

    private void OnEnable()
    {
        minSize = MinimumWindowSize;
        DestroyPreviousPreviewInstances();
        EnsurePreviewUtility();
        EditorApplication.update += TickPreview;
        ResetPreviewClock();
        TryUseSelection();
    }

    private void OnDisable()
    {
        EditorApplication.update -= TickPreview;
        ClearPreviewInstance();
        CleanupPreviewUtility();
    }

    private void OnSelectionChange()
    {
        TryUseSelection();
        Repaint();
    }

    private void OnGUI()
    {
        EnsureStyles();

        DrawHeader();
        DrawSection("대상", DrawPrefabPicker);
        DrawViewPlaybackSections();
        DrawPreviewSection();
        DrawFooter();
    }

    private void DrawHeader()
    {
        Rect rect = GUILayoutUtility.GetRect(10f, HeaderHeight, GUILayout.ExpandWidth(true));
        Rect backgroundRect = new Rect(rect.x + 2f, rect.y + 4f, rect.width - 4f, rect.height - 8f);
        EditorGUI.DrawRect(backgroundRect, new Color(0.105f, 0.12f, 0.145f, 1f));

        GUI.Label(new Rect(backgroundRect.x + 14f, backgroundRect.y + 7f, backgroundRect.width - 28f, 24f), WindowTitle, headerTitleStyle);

        string status = selectedPrefab != null ? selectedPrefab.name : "프리팹 없음";
        GUI.Label(new Rect(backgroundRect.x + 14f, backgroundRect.y + 35f, backgroundRect.width - 28f, 18f), status, headerMetaStyle);
    }

    private void DrawSection(string title, Action drawBody, params GUILayoutOption[] options)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(GridHorizontalMargin);
            DrawSectionContent(title, drawBody, options.Length > 0 ? options : new[] { GUILayout.ExpandWidth(true) });
            GUILayout.Space(GridHorizontalMargin);
        }

        GUILayout.Space(SectionGap);
    }

    private void DrawSectionContent(string title, Action drawBody, params GUILayoutOption[] options)
    {
        using (new EditorGUILayout.VerticalScope(sectionStyle, options))
        {
            EditorGUILayout.LabelField(title, sectionTitleStyle);
            EditorGUILayout.Space(2f);
            drawBody();
        }
    }

    private void DrawViewPlaybackSections()
    {
        float availableWidth = Mathf.Max(0f, position.width - GridHorizontalMargin * 2f - SectionGap);
        float cameraWidth = ResolveCameraSectionWidth(availableWidth);
        float playbackWidth = Mathf.Max(PlaybackSectionMinWidth, availableWidth - cameraWidth);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(GridHorizontalMargin);

            GUILayoutOption[] cameraOptions =
            {
                GUILayout.Width(cameraWidth),
                GUILayout.Height(ViewPlaybackSectionHeight)
            };

            GUILayoutOption[] playbackOptions =
            {
                GUILayout.MinWidth(PlaybackSectionMinWidth),
                GUILayout.Width(playbackWidth),
                GUILayout.Height(ViewPlaybackSectionHeight)
            };

            DrawSectionContent("카메라", DrawViewButtons, cameraOptions);
            GUILayout.Space(SectionGap);
            DrawSectionContent("재생", DrawPlaybackControls, playbackOptions);
            GUILayout.Space(GridHorizontalMargin);
        }

        GUILayout.Space(SectionGap);
    }

    private static float ResolveCameraSectionWidth(float availableWidth)
    {
        float maxCameraWidth = Mathf.Max(0f, availableWidth - PlaybackSectionMinWidth);
        return Mathf.Clamp(CameraSectionWidth, 0f, maxCameraWidth);
    }

    private void DrawPrefabPicker()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("대상", fieldLabelStyle, GUILayout.Width(FieldLabelWidth));

            EditorGUI.BeginChangeCheck();
            GameObject nextPrefab = (GameObject)EditorGUILayout.ObjectField(selectedPrefab, typeof(GameObject), false, GUILayout.Height(ControlHeight));
            if (EditorGUI.EndChangeCheck())
                SetPrefab(IsPrefabAsset(nextPrefab) ? nextPrefab : null);

            GUILayout.Space(RowGap);

            if (GUILayout.Button("선택 사용", compactButtonStyle, GUILayout.Width(StandardButtonWidth), GUILayout.Height(ControlHeight)))
                TryUseSelection();
        }

        if (selectedPrefab == null)
            DrawInlineNotice("프리팹을 선택하거나 드롭하세요.", true);
    }

    private void DrawViewButtons()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("방향", fieldLabelStyle, GUILayout.Width(FieldLabelWidth));
            DrawViewButton(0);
            DrawViewButton(1);
            DrawViewButton(2);
            DrawViewButton(3);
            GUILayout.FlexibleSpace();
        }

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(FieldLabelWidth);
            DrawViewButton(4);
            DrawViewButton(5);
            GUILayout.Space(ViewButtonWidth);
            if (GUILayout.Button("맞춤", compactButtonStyle, GUILayout.Width(ViewButtonWidth), GUILayout.Height(ControlHeight)))
            {
                SetCameraToPreset(viewIndex, true);
                Repaint();
            }

            GUILayout.FlexibleSpace();
        }

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(FieldLabelWidth);
            DrawViewButton(6);
            DrawViewButton(7);
            DrawViewButton(8);
            DrawViewButton(9);
            GUILayout.FlexibleSpace();
        }
    }

    private void DrawPlaybackControls()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("제어", fieldLabelStyle, GUILayout.Width(FieldLabelWidth));

            Color previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = isPlaying ? new Color(1f, 0.78f, 0.45f, 1f) : new Color(0.56f, 0.84f, 0.62f, 1f);
            if (GUILayout.Button(isPlaying ? "정지" : "재생", compactButtonStyle, GUILayout.Width(PlaybackButtonWidth), GUILayout.Height(ControlHeight)))
            {
                isPlaying = !isPlaying;
                ResetPreviewClock();
            }
            GUI.backgroundColor = previousBackground;

            if (GUILayout.Button("처음", compactButtonStyle, GUILayout.Width(PlaybackButtonWidth), GUILayout.Height(ControlHeight)))
            {
                RestartParticlePreview();
                ResetPreviewClock();
                Repaint();
            }

            loopPlayback = GUILayout.Toggle(loopPlayback, "반복", compactButtonStyle, GUILayout.Width(ShortButtonWidth), GUILayout.Height(ControlHeight));
            GUILayout.FlexibleSpace();
            GUILayout.Label("길이", fieldLabelStyle, GUILayout.Width(34f));
            playbackDuration = Mathf.Max(0.1f, EditorGUILayout.FloatField(playbackDuration, GUILayout.Width(56f), GUILayout.Height(ControlHeight)));
            GUILayout.Label("초", fieldLabelStyle, GUILayout.Width(16f));
        }

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("시간", fieldLabelStyle, GUILayout.Width(FieldLabelWidth));
            EditorGUI.BeginChangeCheck();
            playbackTime = EditorGUILayout.Slider(playbackTime, 0f, playbackDuration);
            if (EditorGUI.EndChangeCheck())
            {
                ScrubParticlesToTime(playbackTime);
                ResetPreviewClock();
                Repaint();
            }

            GUILayout.Label(playbackTime.ToString("0.00") + " / " + playbackDuration.ToString("0.00") + "초", footerStyle, GUILayout.Width(112f));
        }
    }

    private void DrawPreviewSection()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(GridHorizontalMargin);
            float previewSectionHeight = ResolvePreviewSectionHeight();
            using (new EditorGUILayout.VerticalScope(sectionStyle, GUILayout.ExpandWidth(true), GUILayout.Height(previewSectionHeight)))
            {
                EditorGUILayout.LabelField("미리보기", sectionTitleStyle);
                float previewHeight = Mathf.Max(1f, previewSectionHeight - PreviewSectionInnerOffset);
                Rect rect = GUILayoutUtility.GetRect(10f, previewHeight, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(rect, new Color(0.08f, 0.085f, 0.095f, 1f));

                if (selectedPrefab == null || previewInstance == null)
                {
                    DrawCenteredLabel(rect, "미리볼 VFX 프리팹을 선택하세요.");
                }
                else
                {
                    HandlePreviewCameraInput(rect);
                    Texture previewTexture = Event.current.type == EventType.Repaint ? RenderPreviewTexture(rect) : null;
                    if (previewTexture != null)
                        GUI.DrawTexture(rect, previewTexture, ScaleMode.ScaleToFit, false);

                    DrawCameraAxisOverlay(rect);

                    if (!string.IsNullOrEmpty(previewMessage))
                        DrawPreviewMessage(rect, previewMessage);
                }
            }
            GUILayout.Space(GridHorizontalMargin);
        }
    }

    private float ResolvePreviewSectionHeight()
    {
        float targetHeight = TargetSectionHeight + (selectedPrefab == null ? EmptyTargetNoticeHeight : 0f);
        float usedHeight = HeaderHeight
            + targetHeight
            + ViewPlaybackSectionHeight
            + FooterHeight
            + SectionGap * 3f
            + PreviewLayoutSlack;
        return Mathf.Max(MinPreviewSectionHeight, position.height - usedHeight);
    }

    private void DrawFooter()
    {
        Rect rect = GUILayoutUtility.GetRect(10f, FooterHeight, GUILayout.ExpandWidth(true));
        Rect statusRect = new Rect(rect.x + 8f, rect.y + 3f, rect.width - 16f, rect.height - 6f);
        EditorGUI.DrawRect(statusRect, new Color(0.12f, 0.13f, 0.145f, 1f));

        string prefabName = selectedPrefab != null ? selectedPrefab.name : "없음";
        string viewName = ViewPresets[Mathf.Clamp(viewIndex, 0, ViewPresets.Length - 1)].Label;
        string text = "프리팹 " + prefabName
            + "   보기 " + viewName
            + "   시간 " + playbackTime.ToString("0.00") + " / " + playbackDuration.ToString("0.00") + "초"
            + "   Renderer " + renderers.Length
            + "   ParticleSystem " + particleSystems.Length
            + "   입자 " + liveParticleCount;
        Rect brandRect = new Rect(statusRect.xMax - FooterBrandWidth - 10f, statusRect.y + 3f, FooterBrandWidth, statusRect.height - 4f);
        Rect textRect = new Rect(statusRect.x + 10f, statusRect.y + 3f, Mathf.Max(1f, brandRect.x - statusRect.x - 18f), statusRect.height - 4f);
        GUI.Label(textRect, text, footerStyle);
        GUI.Label(brandRect, FooterBrandText, footerBrandStyle);
    }

    private void DrawViewButton(int index)
    {
        bool active = viewIndex == index;
        Color previousBackground = GUI.backgroundColor;
        GUI.backgroundColor = active ? new Color(0.52f, 0.72f, 1f, 1f) : Color.white;

        if (GUILayout.Button(ViewPresets[index].Label, compactButtonStyle, GUILayout.Width(ViewButtonWidth), GUILayout.Height(ControlHeight)))
        {
            viewIndex = index;
            SetCameraToPreset(index, true);
            Repaint();
        }

        GUI.backgroundColor = previousBackground;
    }

    private void TryUseSelection()
    {
        GameObject selection = Selection.activeObject as GameObject;
        if (IsPrefabAsset(selection))
            SetPrefab(selection);
    }

    private void SetPrefab(GameObject prefab)
    {
        if (selectedPrefab == prefab && (prefab == null || previewInstance != null))
            return;

        selectedPrefab = prefab;
        playbackTime = 0f;
        previewMessage = string.Empty;
        RebuildPreviewInstance();
        Repaint();
    }

    private void RebuildPreviewInstance()
    {
        ClearPreviewInstance();

        if (selectedPrefab == null)
            return;

        EnsurePreviewUtility();

        previewInstance = Instantiate(selectedPrefab);
        if (previewInstance == null)
        {
            previewMessage = "미리보기 인스턴스를 만들 수 없습니다.";
            return;
        }

        previewInstance.name = PreviewInstancePrefix + selectedPrefab.name;
        previewInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        previewInstance.transform.localScale = Vector3.one;
        previewInstance.SetActive(true);
        SetHideFlagsRecursive(previewInstance, HideFlags.HideAndDontSave);
        previewUtility.AddSingleGO(previewInstance);

        particleSystems = previewInstance.GetComponentsInChildren<ParticleSystem>(true);
        rootParticleSystems = ResolveRootParticleSystems(particleSystems);
        renderers = previewInstance.GetComponentsInChildren<Renderer>(true);
        PrepareParticleSystemsForManualPreview();
        ResolvePlaybackDuration();

        ShowFirstVisibleParticlePreview();
        ResetPreviewClock();
        ResolveBounds();
        RefreshStableFrameBounds();
        RebuildPreviewFloor();
        RefreshPreviewMessage();
        SetCameraToPreset(viewIndex, false);
    }

    private void ResolvePlaybackDuration()
    {
        float duration = 0f;
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            ParticleSystem.MainModule main = particleSystem.main;
            duration = Mathf.Max(duration, main.duration + main.startDelay.constantMax + main.startLifetime.constantMax);
        }

        playbackDuration = Mathf.Clamp(duration > 0f ? duration : playbackDuration, 0.5f, 30f);
    }

    private void PrepareParticleSystemsForManualPreview()
    {
        for (int i = 0; i < rootParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = rootParticleSystems[i];
            if (particleSystem == null)
                continue;

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        RefreshLiveParticleCount();
    }

    private void ResolveBounds()
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        for (int i = 0; i < particleSystems.Length; i++)
            hasBounds |= TryEncapsulateParticles(particleSystems[i], ref bounds, hasBounds);

        if (!hasBounds && previewInstance != null)
            hasBounds = TryResolveTransformBounds(previewInstance.transform, out bounds);

        previewCenter = hasBounds ? bounds.center : Vector3.zero;
        previewRadius = hasBounds ? Mathf.Max(bounds.extents.magnitude, 0.5f) : 1f;
    }

    private void RefreshStableFrameBounds()
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds && previewInstance != null)
            hasBounds = TryResolveTransformBounds(previewInstance.transform, out bounds);

        frameCenter = hasBounds ? bounds.center : previewCenter;
        frameRadius = hasBounds ? Mathf.Max(bounds.extents.magnitude, 0.5f) : Mathf.Max(previewRadius, 1f);
        frameBottomY = hasBounds ? bounds.min.y : 0f;
    }

    private static ParticleSystem[] ResolveRootParticleSystems(ParticleSystem[] systems)
    {
        if (systems == null || systems.Length == 0)
            return Array.Empty<ParticleSystem>();

        int rootCount = 0;
        for (int i = 0; i < systems.Length; i++)
        {
            if (IsRootParticleSystem(systems[i]))
                rootCount++;
        }

        if (rootCount == systems.Length)
            return systems;

        ParticleSystem[] roots = new ParticleSystem[rootCount];
        int index = 0;
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem particleSystem = systems[i];
            if (IsRootParticleSystem(particleSystem))
                roots[index++] = particleSystem;
        }

        return roots;
    }

    private static bool IsRootParticleSystem(ParticleSystem particleSystem)
    {
        if (particleSystem == null)
            return false;

        Transform parent = particleSystem.transform.parent;
        while (parent != null)
        {
            if (parent.GetComponent<ParticleSystem>() != null)
                return false;

            parent = parent.parent;
        }

        return true;
    }

    private void TickPreview()
    {
        if (selectedPrefab == null || previewInstance == null)
        {
            cameraBlendActive = false;
            lastUpdateTime = EditorApplication.timeSinceStartup;
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (now - lastRepaintTime < PlayingPreviewTickInterval)
            return;

        lastRepaintTime = now;

        float deltaTime = Mathf.Clamp((float)(now - lastUpdateTime), 0f, 0.1f);
        bool changed = false;

        if (isPlaying)
        {
            AdvancePlayback(deltaTime);
            changed = true;
        }

        changed |= AdvanceCameraBlend(now);

        lastUpdateTime = now;
        if (changed)
            Repaint();
    }

    private void AdvancePlayback(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        float nextTime = playbackTime + deltaTime;
        if (nextTime > playbackDuration)
        {
            if (loopPlayback)
            {
                float wrappedTime = nextTime % playbackDuration;
                ScrubParticlesToTime(wrappedTime);
                playbackTime = wrappedTime;
                return;
            }

            deltaTime = Mathf.Max(0f, playbackDuration - playbackTime);
            AdvanceParticles(deltaTime);
            playbackTime = playbackDuration;
            isPlaying = false;
            return;
        }

        AdvanceParticles(deltaTime);
        playbackTime = nextTime;
    }

    private void RestartParticlePreview()
    {
        playbackTime = 0f;
        ResetParticleSystems();
        ResolveBounds();
    }

    private void ShowFirstVisibleParticlePreview()
    {
        if (playbackDuration <= 0f)
        {
            RestartParticlePreview();
            return;
        }

        float maxTime = Mathf.Min(playbackDuration, FirstVisiblePreviewScanMaxTime);
        float previewTime = Mathf.Min(InitialVisiblePreviewTime, maxTime);
        bool foundVisibleParticles = false;

        for (float time = InitialVisiblePreviewTime; time <= maxTime; time += FirstVisiblePreviewScanStep)
        {
            ScrubParticlesToTime(time);
            if (CountLiveParticles() > 0)
            {
                previewTime = time;
                foundVisibleParticles = true;
                break;
            }
        }

        if (!foundVisibleParticles)
            ScrubParticlesToTime(previewTime);

        playbackTime = previewTime;
    }

    private void ResetPreviewClock()
    {
        double now = EditorApplication.timeSinceStartup;
        lastUpdateTime = now;
        lastRepaintTime = now;
    }

    private void ScrubParticlesToTime(float time)
    {
        ResetParticleSystems();
        SimulateParticlesFromStart(time);
        RefreshLiveParticleCount();
        ResolveBounds();
    }

    private void ResetParticleSystems()
    {
        for (int i = 0; i < rootParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = rootParticleSystems[i];
            if (particleSystem == null)
                continue;

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        RefreshLiveParticleCount();
    }

    private void AdvanceParticles(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            ResolveBounds();
            return;
        }

        for (int i = 0; i < rootParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = rootParticleSystems[i];
            if (particleSystem == null)
                continue;

            particleSystem.Simulate(deltaTime, true, false, true);
        }

        RefreshLiveParticleCount();
        ResolveBounds();
    }

    private void SimulateParticlesFromStart(float time)
    {
        if (time <= 0f)
            return;

        for (int i = 0; i < rootParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = rootParticleSystems[i];
            if (particleSystem == null)
                continue;

            particleSystem.Simulate(time, true, true, true);
        }
    }

    private void RefreshLiveParticleCount()
    {
        liveParticleCount = CountLiveParticles();
    }

    private int CountLiveParticles()
    {
        int count = 0;
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem != null)
                count += particleSystem.particleCount;
        }

        return count;
    }

    private void SetCameraToPreset(int presetIndex, bool animated)
    {
        Vector3 direction = ViewPresets[Mathf.Clamp(presetIndex, 0, ViewPresets.Length - 1)].Direction.normalized;
        float targetPitch = Mathf.Clamp(Mathf.Asin(direction.y) * Mathf.Rad2Deg, MinCameraPitch, MaxCameraPitch);
        float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        if (animated && previewInstance != null)
        {
            StartCameraBlend(targetYaw, targetPitch, 1f);
            return;
        }

        cameraBlendActive = false;
        cameraPitch = targetPitch;
        cameraYaw = NormalizeAngle(targetYaw);
        cameraDistanceScale = 1f;
        UpdatePreviewCameraTransform();
    }

    private void StartCameraBlend(float targetYaw, float targetPitch, float targetDistanceScale)
    {
        cameraBlendActive = true;
        cameraBlendStartTime = EditorApplication.timeSinceStartup;
        cameraBlendStartYaw = cameraYaw;
        cameraBlendStartPitch = cameraPitch;
        cameraBlendStartDistanceScale = cameraDistanceScale;
        cameraBlendTargetYaw = cameraYaw + Mathf.DeltaAngle(cameraYaw, targetYaw);
        cameraBlendTargetPitch = targetPitch;
        cameraBlendTargetDistanceScale = targetDistanceScale;
    }

    private bool AdvanceCameraBlend(double now)
    {
        if (!cameraBlendActive)
            return false;

        float t = CameraBlendDuration <= 0f ? 1f : Mathf.Clamp01((float)((now - cameraBlendStartTime) / CameraBlendDuration));
        float eased = Mathf.SmoothStep(0f, 1f, t);

        cameraYaw = Mathf.Lerp(cameraBlendStartYaw, cameraBlendTargetYaw, eased);
        cameraPitch = Mathf.Lerp(cameraBlendStartPitch, cameraBlendTargetPitch, eased);
        cameraDistanceScale = Mathf.Lerp(cameraBlendStartDistanceScale, cameraBlendTargetDistanceScale, eased);

        if (t >= 1f)
        {
            cameraBlendActive = false;
            cameraYaw = NormalizeAngle(cameraBlendTargetYaw);
            cameraPitch = cameraBlendTargetPitch;
            cameraDistanceScale = cameraBlendTargetDistanceScale;
        }

        UpdatePreviewCameraTransform();
        return true;
    }

    private void CancelCameraBlend()
    {
        cameraBlendActive = false;
    }

    private void HandlePreviewCameraInput(Rect rect)
    {
        Event current = Event.current;
        if (current == null || !rect.Contains(current.mousePosition))
            return;

        if (current.type == EventType.MouseDown && current.button == 1)
        {
            CancelCameraBlend();
            current.Use();
            return;
        }

        if (current.type == EventType.MouseDrag && current.button == 1)
        {
            CancelCameraBlend();
            cameraYaw -= current.delta.x * OrbitSensitivity;
            cameraPitch = Mathf.Clamp(cameraPitch + current.delta.y * OrbitSensitivity, MinCameraPitch, MaxCameraPitch);
            UpdatePreviewCameraTransform();
            Repaint();
            current.Use();
            return;
        }

        if (current.type == EventType.ScrollWheel)
        {
            CancelCameraBlend();
            cameraDistanceScale = Mathf.Clamp(cameraDistanceScale * (1f + current.delta.y * 0.08f), 0.2f, 6f);
            UpdatePreviewCameraTransform();
            Repaint();
            current.Use();
        }
    }

    private void DrawCameraAxisOverlay(Rect previewRect)
    {
        if (previewUtility == null || previewUtility.camera == null || Event.current.type != EventType.Repaint)
            return;

        Rect boxRect = new Rect(
            previewRect.xMax - AxisOverlaySize - AxisOverlayPadding,
            previewRect.y + AxisOverlayPadding,
            AxisOverlaySize,
            AxisOverlaySize);

        EditorGUI.DrawRect(boxRect, new Color(0.045f, 0.052f, 0.064f, 0.78f));

        Vector2 origin = new Vector2(boxRect.center.x, boxRect.center.y + 6f);
        Vector2 xEnd = ResolveAxisOverlayEnd(origin, Vector3.right);
        Vector2 yEnd = ResolveAxisOverlayEnd(origin, Vector3.up);
        Vector2 zEnd = ResolveAxisOverlayEnd(origin, Vector3.forward);

        Handles.BeginGUI();
        Color previousColor = Handles.color;
        DrawAxisOverlayLine(origin, zEnd, new Color(0.45f, 0.68f, 1f, 1f));
        DrawAxisOverlayLine(origin, xEnd, new Color(1f, 0.42f, 0.38f, 1f));
        DrawAxisOverlayLine(origin, yEnd, new Color(0.46f, 0.92f, 0.52f, 1f));
        Handles.color = new Color(0.93f, 0.95f, 0.98f, 1f);
        Handles.DrawSolidDisc(new Vector3(origin.x, origin.y, 0f), Vector3.forward, 3f);
        Handles.color = previousColor;
        Handles.EndGUI();

        DrawAxisOverlayLabel(xEnd, "X", new Color(1f, 0.48f, 0.45f, 1f));
        DrawAxisOverlayLabel(yEnd, "Y", new Color(0.52f, 1f, 0.58f, 1f));
        DrawAxisOverlayLabel(zEnd, "Z", new Color(0.52f, 0.74f, 1f, 1f));
    }

    private Vector2 ResolveAxisOverlayEnd(Vector2 origin, Vector3 worldAxis)
    {
        Vector3 localAxis = previewUtility.camera.transform.InverseTransformDirection(worldAxis).normalized;
        Vector2 screenDirection = new Vector2(localAxis.x, -localAxis.y);
        float screenMagnitude = screenDirection.magnitude;

        if (screenMagnitude < 0.05f)
            screenDirection = localAxis.z >= 0f ? new Vector2(0.35f, -0.35f) : new Vector2(-0.35f, 0.35f);
        else
            screenDirection /= screenMagnitude;

        float length = Mathf.Lerp(AxisOverlayLineLength * 0.48f, AxisOverlayLineLength, Mathf.Clamp01(screenMagnitude));
        return origin + screenDirection * length;
    }

    private static void DrawAxisOverlayLine(Vector2 origin, Vector2 end, Color color)
    {
        Vector3 originPoint = new Vector3(origin.x, origin.y, 0f);
        Vector3 endPoint = new Vector3(end.x, end.y, 0f);

        Handles.color = new Color(0f, 0f, 0f, 0.42f);
        Handles.DrawAAPolyLine(5f, originPoint, endPoint);
        Handles.color = color;
        Handles.DrawAAPolyLine(3f, originPoint, endPoint);
    }

    private void DrawAxisOverlayLabel(Vector2 position, string label, Color color)
    {
        axisLabelStyle.normal.textColor = color;
        GUI.Label(new Rect(position.x - 10f, position.y - 10f, 20f, 18f), label, axisLabelStyle);
    }

    private Texture RenderPreviewTexture(Rect rect)
    {
        if (previewInstance == null)
            return null;

        EnsurePreviewUtility();
        UpdatePreviewCameraTransform();

        Texture previewTexture = null;
        bool beganPreview = false;
        try
        {
            previewUtility.BeginPreview(rect, GUIStyle.none);
            beganPreview = true;
            previewUtility.Render(true);
            previewTexture = previewUtility.EndPreview();
            previewMessage = BuildPreviewMessage();
        }
        catch (Exception exception)
        {
            if (beganPreview)
            {
                try
                {
                    previewUtility.EndPreview();
                }
                catch
                {
                    // PreviewRenderUtility can be mid-cleanup after a render exception.
                }
            }

            previewMessage = "미리보기 렌더 오류: " + exception.GetType().Name;
        }

        return previewTexture;
    }

    private void EnsurePreviewUtility()
    {
        if (previewUtility != null)
            return;

        previewUtility = new PreviewRenderUtility();
        previewUtility.camera.cameraType = CameraType.Preview;
        previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
        previewUtility.camera.backgroundColor = PreviewBackgroundColor;
        previewUtility.camera.nearClipPlane = 0.01f;
        previewUtility.camera.farClipPlane = 5000f;
        previewUtility.camera.fieldOfView = 45f;
        previewUtility.camera.cullingMask = ~0;

        UniversalAdditionalCameraData cameraData = previewUtility.camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null)
            cameraData = previewUtility.camera.gameObject.AddComponent<UniversalAdditionalCameraData>();

        cameraData.renderPostProcessing = false;
        previewUtility.ambientColor = PreviewAmbientColor;

        if (previewUtility.lights.Length > 0 && previewUtility.lights[0] != null)
            ConfigurePreviewLight(previewUtility.lights[0], 1.65f, Quaternion.Euler(45f, -35f, 0f), PreviewKeyLightColor);

        if (previewUtility.lights.Length > 1 && previewUtility.lights[1] != null)
            ConfigurePreviewLight(previewUtility.lights[1], 0.9f, Quaternion.Euler(335f, 145f, 0f), PreviewFillLightColor);
    }

    private static void ConfigurePreviewLight(Light light, float intensity, Quaternion rotation, Color color)
    {
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.transform.rotation = rotation;
    }

    private void RebuildPreviewFloor()
    {
        ClearPreviewFloor();

        if (previewUtility == null || previewInstance == null)
            return;

        floorInstance = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floorInstance.name = PreviewFloorName;
        floorInstance.transform.SetPositionAndRotation(new Vector3(frameCenter.x, frameBottomY - FloorYOffset, frameCenter.z), Quaternion.identity);

        float floorSize = Mathf.Max(MinFloorSize, frameRadius * FloorPaddingMultiplier);
        float planeScale = floorSize / 10f;
        floorInstance.transform.localScale = new Vector3(planeScale, 1f, planeScale);

        Collider floorCollider = floorInstance.GetComponent<Collider>();
        if (floorCollider != null)
            DestroyImmediate(floorCollider);

        Renderer floorRenderer = floorInstance.GetComponent<Renderer>();
        floorMaterial = CreatePreviewFloorMaterial();
        if (floorRenderer != null)
            floorRenderer.sharedMaterial = floorMaterial;

        SetHideFlagsRecursive(floorInstance, HideFlags.HideAndDontSave);
        previewUtility.AddSingleGO(floorInstance);
    }

    private static Material CreatePreviewFloorMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Hidden/Internal-Colored");

        Material material = new Material(shader)
        {
            name = PreviewFloorMaterialName,
            hideFlags = HideFlags.HideAndDontSave
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", PreviewFloorColor);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", PreviewFloorColor);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.25f);

        return material;
    }

    private void CleanupPreviewUtility()
    {
        ClearPreviewFloor();

        if (previewUtility == null)
            return;

        previewUtility.Cleanup();
        previewUtility = null;
    }

    private void UpdatePreviewCameraTransform()
    {
        if (previewUtility == null)
            return;

        Vector3 direction = ResolveCameraDirection();
        Vector3 center = frameRadius > 0f ? frameCenter : previewCenter;
        float radius = Mathf.Max(frameRadius, 0.5f);
        float distance = Mathf.Max(MinCameraSize, radius * CameraFitPadding / Mathf.Tan(previewUtility.camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
        distance *= cameraDistanceScale;

        previewUtility.camera.transform.position = center + direction * distance;
        previewUtility.camera.transform.rotation = Quaternion.LookRotation(-direction, ResolveCameraUp(direction));
    }

    private Vector3 ResolveCameraDirection()
    {
        float yaw = cameraYaw * Mathf.Deg2Rad;
        float pitch = cameraPitch * Mathf.Deg2Rad;
        float pitchCos = Mathf.Cos(pitch);
        return new Vector3(Mathf.Sin(yaw) * pitchCos, Mathf.Sin(pitch), Mathf.Cos(yaw) * pitchCos).normalized;
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
            angle -= 360f;
        else if (angle < -180f)
            angle += 360f;

        return angle;
    }

    private static Vector3 ResolveCameraUp(Vector3 direction)
    {
        float verticalDot = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up));
        return verticalDot > 0.95f ? Vector3.forward : Vector3.up;
    }

    private void EnsureStyles()
    {
        if (headerTitleStyle != null)
            return;

        headerTitleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 19,
            normal = { textColor = new Color(0.94f, 0.96f, 1f, 1f) }
        };

        headerMetaStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.72f, 0.78f, 0.86f, 1f) }
        };

        sectionStyle = new GUIStyle(EditorStyles.helpBox)
        {
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(10, 10, 8, 10)
        };

        sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = new Color(0.9f, 0.94f, 1f, 1f) }
        };

        fieldLabelStyle = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fixedHeight = ControlHeight,
            normal = { textColor = new Color(0.8f, 0.84f, 0.9f, 1f) }
        };

        compactButtonStyle = new GUIStyle(EditorStyles.miniButton)
        {
            fontStyle = FontStyle.Bold
        };

        noticeStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
        {
            normal = { textColor = new Color(0.82f, 0.88f, 0.96f, 1f) }
        };

        footerStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip,
            normal = { textColor = new Color(0.68f, 0.72f, 0.78f, 1f) }
        };

        footerBrandStyle = new GUIStyle(footerStyle)
        {
            alignment = TextAnchor.MiddleRight,
            fontStyle = FontStyle.Bold
        };
        footerBrandStyle.normal.textColor = new Color(0.78f, 0.86f, 1f, 1f);

        axisLabelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11,
            clipping = TextClipping.Clip
        };
    }

    private void ClearPreviewInstance()
    {
        ClearPreviewFloor();

        if (previewInstance != null)
        {
            DestroyImmediate(previewInstance);
            previewInstance = null;
        }

        particleSystems = Array.Empty<ParticleSystem>();
        rootParticleSystems = Array.Empty<ParticleSystem>();
        renderers = Array.Empty<Renderer>();
        liveParticleCount = 0;
        previewCenter = Vector3.zero;
        previewRadius = 1f;
        frameCenter = Vector3.zero;
        frameRadius = 1f;
        frameBottomY = 0f;
        cameraBlendActive = false;
        previewMessage = string.Empty;
    }

    private void ClearPreviewFloor()
    {
        if (floorInstance != null)
        {
            DestroyImmediate(floorInstance);
            floorInstance = null;
        }

        if (floorMaterial != null)
        {
            DestroyImmediate(floorMaterial);
            floorMaterial = null;
        }
    }

    private void RefreshPreviewMessage()
    {
        previewMessage = BuildPreviewMessage();
    }

    private string BuildPreviewMessage()
    {
        if (previewInstance == null)
            return string.Empty;

        if (renderers.Length == 0 && particleSystems.Length == 0)
            return "Renderer 또는 ParticleSystem이 없습니다.";

        if (particleSystems.Length > 0 && liveParticleCount == 0)
            return "현재 시간에 표시 중인 입자가 없습니다.";

        int activeRendererCount = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                activeRendererCount++;
        }

        return activeRendererCount == 0 ? "켜진 Renderer가 없습니다." : string.Empty;
    }

    private static void DestroyPreviousPreviewInstances()
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject gameObject = objects[i];
            if (gameObject == null)
                continue;

            if (EditorUtility.IsPersistent(gameObject))
                continue;

            if (gameObject.name == LegacyMirrorCameraName
                || gameObject.name == LegacyPreviewCameraName
                || gameObject.name == LegacyPreviewLightName
                || gameObject.name == PreviewFloorName
                || gameObject.name.StartsWith(PreviewInstancePrefix, StringComparison.Ordinal))
                DestroyImmediate(gameObject);
        }
    }

    private static void SetHideFlagsRecursive(GameObject root, HideFlags hideFlags)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null)
                transforms[i].gameObject.hideFlags = hideFlags;
        }
    }

    private static bool TryResolveTransformBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds(root.position, Vector3.one);
        bool hasTransform = false;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            if (transform == null)
                continue;

            if (!hasTransform)
            {
                bounds = new Bounds(transform.position, Vector3.one * 0.5f);
                hasTransform = true;
            }
            else
            {
                bounds.Encapsulate(transform.position);
            }
        }

        return hasTransform;
    }

    private bool TryEncapsulateParticles(ParticleSystem particleSystem, ref Bounds bounds, bool hasBounds)
    {
        if (particleSystem == null)
            return false;

        int particleCount = particleSystem.particleCount;
        if (particleCount <= 0)
            return false;

        if (particleBuffer.Length < particleCount)
            particleBuffer = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(particleCount)];

        int count = particleSystem.GetParticles(particleBuffer);
        ParticleSystem.MainModule main = particleSystem.main;
        bool foundParticle = false;

        for (int i = 0; i < count; i++)
        {
            Vector3 position = particleBuffer[i].position;
            if (main.simulationSpace != ParticleSystemSimulationSpace.World)
                position = particleSystem.transform.TransformPoint(position);

            if (!hasBounds && !foundParticle)
            {
                bounds = new Bounds(position, Vector3.one * 0.25f);
                foundParticle = true;
            }
            else
            {
                bounds.Encapsulate(position);
                foundParticle = true;
            }
        }

        return foundParticle;
    }

    private static bool IsPrefabAsset(GameObject gameObject)
    {
        if (gameObject == null)
            return false;

        if (!AssetDatabase.Contains(gameObject))
            return false;

        PrefabAssetType prefabType = PrefabUtility.GetPrefabAssetType(gameObject);
        return prefabType != PrefabAssetType.NotAPrefab && prefabType != PrefabAssetType.MissingAsset;
    }

    private static void DrawCenteredLabel(Rect rect, string text)
    {
        GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.82f, 0.84f, 0.88f, 1f) }
        };
        GUI.Label(rect, text, style);
    }

    private void DrawInlineNotice(string text, bool warning)
    {
        Rect rect = GUILayoutUtility.GetRect(10f, 28f, GUILayout.ExpandWidth(true));
        Rect noticeRect = new Rect(rect.x, rect.y + 5f, rect.width, rect.height - 6f);
        Color background = warning ? new Color(0.22f, 0.16f, 0.07f, 1f) : new Color(0.08f, 0.13f, 0.16f, 1f);
        EditorGUI.DrawRect(noticeRect, background);
        GUI.Label(new Rect(noticeRect.x + 10f, noticeRect.y + 2f, noticeRect.width - 20f, noticeRect.height - 4f), text, noticeStyle);
    }

    private static void DrawPreviewMessage(Rect rect, string text)
    {
        float width = Mathf.Max(120f, Mathf.Min(rect.width - 24f, 420f));
        Rect messageRect = new Rect(rect.x + 12f, rect.y + 12f, width, 28f);
        EditorGUI.DrawRect(messageRect, new Color(0.05f, 0.05f, 0.055f, 0.85f));

        GUIStyle style = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.95f, 0.88f, 0.62f, 1f) }
        };
        GUI.Label(messageRect, text, style);
    }

    private readonly struct ViewPreset
    {
        public ViewPreset(string label, Vector3 direction)
        {
            Label = label;
            Direction = direction;
        }

        public string Label { get; }
        public Vector3 Direction { get; }
    }
}
