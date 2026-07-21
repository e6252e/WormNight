using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using TeamProject01.Gameplay;
using UnityEditor;
using UnityEngine;

namespace TeamProject01.EditorTools
{
    public sealed class GameplaySfxTableWindow : EditorWindow
    {
        private const string WindowTitle = "게임 SFX 테이블";
        private const string MenuPath = "JC Tool/Audio/게임 SFX 테이블";
        private const string CatalogPath = "Assets/Resources/Audio/GameplaySfxCatalog.asset";
        private const string SegmentCatalogPath = "Assets/Segments/_Catalog/SegmentCatalog.asset";
        private const float RowHeight = 24f;
        private static readonly string[] PrefabScanFolders =
        {
            "Assets/Segments",
            "Assets/Prefabs",
            "Assets/Resources",
            "Assets/ThirdParty/01_Core/Animated Fantasy Polygon Chest/Prefab"
        };

        private static readonly string[] CategoryFilterLabels =
        {
            "전체",
            "세그먼트",
            "스타터 세그먼트",
            "런타임 리소스",
            "플레이어/루팅",
            "아이템/루팅",
            "상자",
            "넥서스",
            "스킬/HUD",
            "결과 UI",
            "카탈로그",
            "기타"
        };

        private readonly List<SfxRow> rows = new List<SfxRow>(128);
        private readonly Dictionary<string, SegmentInfo> segmentInfoByPrefabPath = new Dictionary<string, SegmentInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> segmentDisplayNameById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private Vector2 scroll;
        private string searchText = string.Empty;
        private int selectedCategoryIndex;
        private bool showMissingOnly;
        private bool includeDirectAudioSources = true;
        private string lastStatus = "새로고침을 누르면 현재 프로젝트 SFX를 다시 스캔합니다.";
        private AudioClip lastPlayedClip;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            GameplaySfxTableWindow window = GetWindow<GameplaySfxTableWindow>(WindowTitle);
            window.minSize = new Vector2(1280f, 560f);
            window.Show();
        }

        public static void ValidateFromCommandLine()
        {
            GameplaySfxTableWindow window = CreateInstance<GameplaySfxTableWindow>();
            if (window.rows.Count == 0)
            {
                window.RefreshRows();
            }

            int missing = 0;
            for (int i = 0; i < window.rows.Count; i++)
            {
                if (window.rows[i].HasMissingClip)
                {
                    missing++;
                }
            }

            Debug.Log($"[GameplaySfxTable] Rows: {window.rows.Count}, Missing: {missing}");
            DestroyImmediate(window);

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        private void OnEnable()
        {
            RefreshRows();
        }

        private void OnDisable()
        {
            EditorAudioPreview.StopAll();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawSummary();
            DrawTable();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("새로고침", GUILayout.Width(90f), GUILayout.Height(28f)))
                    {
                        RefreshRows();
                    }

                    if (GUILayout.Button("재생 정지", GUILayout.Width(90f), GUILayout.Height(28f)))
                    {
                        EditorAudioPreview.StopAll();
                        lastPlayedClip = null;
                        lastStatus = "미리듣기를 정지했습니다.";
                    }

                    EditorGUI.BeginChangeCheck();
                    bool newIncludeDirectAudioSources = GUILayout.Toggle(includeDirectAudioSources, "직접 AudioSource 포함", EditorStyles.toolbarButton, GUILayout.Width(150f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        includeDirectAudioSources = newIncludeDirectAudioSources;
                        RefreshRows();
                    }
                    showMissingOnly = GUILayout.Toggle(showMissingOnly, "비어있는 행만", EditorStyles.toolbarButton, GUILayout.Width(100f));

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("분류", GUILayout.Width(32f));
                    selectedCategoryIndex = EditorGUILayout.Popup(selectedCategoryIndex, CategoryFilterLabels, GUILayout.Width(140f));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    searchText = EditorGUILayout.TextField("검색", searchText ?? string.Empty);
                    if (GUILayout.Button("검색 초기화", GUILayout.Width(90f)))
                    {
                        searchText = string.Empty;
                    }
                }

                EditorGUILayout.HelpBox(
                    "GameplaySfxEmitter, GameplaySfxCatalog, 직접 AudioSource 클립을 읽기 전용으로 표시합니다. ▶ 버튼은 에디터 미리듣기이며 프리팹/씬을 수정하지 않습니다.",
                    MessageType.Info);
            }
        }

        private void DrawSummary()
        {
            int visible = 0;
            int missing = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].HasMissingClip)
                {
                    missing++;
                }

                if (ShouldShowRow(rows[i]))
                {
                    visible++;
                }
            }

            string playing = lastPlayedClip != null ? " / 마지막 재생: " + lastPlayedClip.name : string.Empty;
            EditorGUILayout.LabelField($"총 {rows.Count}개 / 표시 {visible}개 / 비어있음 {missing}개{playing}");
            EditorGUILayout.LabelField(lastStatus, EditorStyles.miniLabel);
        }

        private void DrawTable()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll, true, true);
            DrawHeader();

            for (int i = 0; i < rows.Count; i++)
            {
                SfxRow row = rows[i];
                if (!ShouldShowRow(row))
                {
                    continue;
                }

                DrawRow(row);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                Header("분류", 105f);
                Header("대상", 170f);
                Header("세그먼트", 140f);
                Header("Lv", 34f);
                Header("종류", 80f);
                Header("슬롯/위치", 190f);
                Header("큐", 130f);
                Header("클립", 300f);
                Header("볼륨", 48f);
                Header("쿨타임", 56f);
                Header("방식", 82f);
                Header("프리팹/에셋 경로", 360f);
                Header("클립 경로", 420f);
                Header("작업", 230f);
            }
        }

        private void DrawRow(SfxRow row)
        {
            Rect rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
            if (row.HasMissingClip)
            {
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, Mathf.Max(position.width, 2200f), RowHeight), new Color(1f, 0.35f, 0.2f, 0.14f));
            }

            Label(row.Category, 105f);
            Label(row.TargetName, 170f);
            Label(row.SegmentLabel, 140f);
            Label(row.LevelLabel, 34f);
            Label(row.SourceKind, 80f);
            Label(row.SlotPath, 190f);
            Label(row.CueLabel, 130f);
            Label(row.ClipSummary, 300f);
            Label(row.VolumeText, 48f);
            Label(row.CooldownText, 56f);
            Label(row.PlaybackText, 82f);
            Label(row.AssetPath, 360f);
            Label(row.ClipPathSummary, 420f);
            DrawActions(row);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawActions(SfxRow row)
        {
            using (new EditorGUI.DisabledScope(!row.HasPlayableClip))
            {
                if (GUILayout.Button("▶", GUILayout.Width(32f)))
                {
                    PlayRandomClip(row);
                }

                int buttonCount = Mathf.Min(3, row.Clips != null ? row.Clips.Length : 0);
                for (int i = 0; i < buttonCount; i++)
                {
                    using (new EditorGUI.DisabledScope(row.Clips[i] == null))
                    {
                        if (GUILayout.Button("▶" + (i + 1).ToString(CultureInfo.InvariantCulture), GUILayout.Width(36f)))
                        {
                            PlayClip(row.Clips[i], row);
                        }
                    }
                }
            }

            if (GUILayout.Button("클립", GUILayout.Width(42f)))
            {
                PingObject(row.FirstClip);
            }

            if (GUILayout.Button("대상", GUILayout.Width(42f)))
            {
                PingObject(row.MainObject);
            }

            if (GUILayout.Button("복사", GUILayout.Width(42f)))
            {
                EditorGUIUtility.systemCopyBuffer = BuildCopyText(row);
                lastStatus = "행 정보를 클립보드에 복사했습니다.";
            }
        }

        private void RefreshRows()
        {
            rows.Clear();
            segmentInfoByPrefabPath.Clear();
            segmentDisplayNameById.Clear();

            BuildSegmentMaps();
            AddCatalogRows();
            AddPrefabRows();

            rows.Sort(CompareRows);
            lastStatus = $"스캔 완료: {DateTime.Now:HH:mm:ss}";
            Repaint();
        }

        private void BuildSegmentMaps()
        {
            SegmentCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<SegmentCatalogAsset>(SegmentCatalogPath);
            if (catalog == null || catalog.Segments == null)
            {
                return;
            }

            for (int i = 0; i < catalog.Segments.Length; i++)
            {
                SegmentDefinition definition = catalog.Segments[i];
                if (definition == null)
                {
                    continue;
                }

                string id = definition.NormalizedId;
                string displayName = string.IsNullOrWhiteSpace(definition.DisplayName) ? id : definition.DisplayName.Trim();
                if (!string.IsNullOrEmpty(id))
                {
                    segmentDisplayNameById[id] = displayName;
                    if (!string.IsNullOrWhiteSpace(definition.UpgradeId))
                    {
                        segmentDisplayNameById[definition.UpgradeId] = displayName;
                    }
                }

                if (definition.Levels == null)
                {
                    continue;
                }

                for (int j = 0; j < definition.Levels.Length; j++)
                {
                    SegmentLevelDefinition level = definition.Levels[j];
                    if (level.SegmentPrefab == null)
                    {
                        continue;
                    }

                    string path = AssetDatabase.GetAssetPath(level.SegmentPrefab);
                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    segmentInfoByPrefabPath[path] = new SegmentInfo(id, displayName, Mathf.Max(0, level.Level));
                }
            }
        }

        private void AddCatalogRows()
        {
            GameplaySfxCatalog catalog = AssetDatabase.LoadAssetAtPath<GameplaySfxCatalog>(CatalogPath);
            if (catalog == null || catalog.Entries == null)
            {
                return;
            }

            for (int i = 0; i < catalog.Entries.Length; i++)
            {
                GameplaySfxCatalogEntry entry = catalog.Entries[i];
                if (entry == null)
                {
                    continue;
                }

                AudioClip[] clips = entry.Clips ?? Array.Empty<AudioClip>();
                rows.Add(new SfxRow
                {
                    Category = "카탈로그",
                    SourceKind = "카탈로그",
                    TargetName = "공용 SFX 카탈로그",
                    SegmentId = string.Empty,
                    SegmentName = string.Empty,
                    Level = 0,
                    SlotPath = "Entries[" + i.ToString(CultureInfo.InvariantCulture) + "]",
                    Cue = entry.Cue,
                    CueLabel = TranslateCue(entry.Cue),
                    Clips = clips,
                    Volume = entry.Volume,
                    Cooldown = entry.Cooldown,
                    Spatial = entry.Spatial,
                    Detached = true,
                    AssetPath = CatalogPath,
                    MainObject = catalog
                });
            }
        }

        private void AddPrefabRows()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", GetExistingPrefabScanFolders());
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                AddEmitterRows(prefab, path);
                if (includeDirectAudioSources)
                {
                    AddDirectAudioSourceRows(prefab, path);
                }
            }
        }

        private static string[] GetExistingPrefabScanFolders()
        {
            List<string> folders = new List<string>(PrefabScanFolders.Length);
            for (int i = 0; i < PrefabScanFolders.Length; i++)
            {
                if (AssetDatabase.IsValidFolder(PrefabScanFolders[i]))
                {
                    folders.Add(PrefabScanFolders[i]);
                }
            }

            return folders.ToArray();
        }

        private void AddEmitterRows(GameObject prefab, string path)
        {
            GameplaySfxEmitter[] emitters = prefab.GetComponentsInChildren<GameplaySfxEmitter>(true);
            if (emitters == null || emitters.Length == 0)
            {
                return;
            }

            SegmentInfo segmentInfo = ResolveSegmentInfo(prefab, path);
            for (int i = 0; i < emitters.Length; i++)
            {
                GameplaySfxEmitter emitter = emitters[i];
                if (emitter == null)
                {
                    continue;
                }

                SerializedObject serialized = new SerializedObject(emitter);
                AudioSource source = GetObjectReference<AudioSource>(serialized, "source");
                if (source == null)
                {
                    source = emitter.GetComponent<AudioSource>();
                }

                bool spatial = source != null && source.spatialBlend > 0.5f;
                bool detached = GetBool(serialized, "forceDetachedPlayback", false);
                float cooldown = GetFloat(serialized, "cooldown", 0f);

                rows.Add(new SfxRow
                {
                    Category = ResolveCategory(path, "Emitter"),
                    SourceKind = "Emitter",
                    TargetName = prefab.name,
                    SegmentId = segmentInfo.Id,
                    SegmentName = segmentInfo.DisplayName,
                    Level = segmentInfo.Level,
                    SlotPath = GetTransformPath(prefab.transform, emitter.transform),
                    Cue = emitter.Cue,
                    CueLabel = TranslateCue(emitter.Cue),
                    Clips = emitter.Clips ?? Array.Empty<AudioClip>(),
                    Volume = emitter.Volume,
                    Cooldown = cooldown,
                    Spatial = spatial,
                    Detached = detached,
                    AssetPath = path,
                    MainObject = prefab
                });
            }
        }

        private void AddDirectAudioSourceRows(GameObject prefab, string path)
        {
            if (!ShouldScanDirectAudioSource(path))
            {
                return;
            }

            AudioSource[] sources = prefab.GetComponentsInChildren<AudioSource>(true);
            if (sources == null || sources.Length == 0)
            {
                return;
            }

            SegmentInfo segmentInfo = ResolveSegmentInfo(prefab, path);
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                if (source == null || source.clip == null || source.GetComponent<GameplaySfxEmitter>() != null)
                {
                    continue;
                }

                rows.Add(new SfxRow
                {
                    Category = ResolveCategory(path, "AudioSource"),
                    SourceKind = "AudioSource",
                    TargetName = prefab.name,
                    SegmentId = segmentInfo.Id,
                    SegmentName = segmentInfo.DisplayName,
                    Level = segmentInfo.Level,
                    SlotPath = GetTransformPath(prefab.transform, source.transform),
                    Cue = GameplaySfxCue.None,
                    CueLabel = "직접 AudioSource",
                    Clips = new[] { source.clip },
                    Volume = source.volume,
                    Cooldown = 0f,
                    Spatial = source.spatialBlend > 0.5f,
                    Detached = false,
                    AssetPath = path,
                    MainObject = prefab
                });
            }
        }

        private bool ShouldShowRow(SfxRow row)
        {
            if (showMissingOnly && !row.HasMissingClip)
            {
                return false;
            }

            if (selectedCategoryIndex > 0 && !string.Equals(row.Category, CategoryFilterLabels[selectedCategoryIndex], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            string query = searchText.Trim();
            return Contains(row.Category, query)
                || Contains(row.SourceKind, query)
                || Contains(row.TargetName, query)
                || Contains(row.SegmentId, query)
                || Contains(row.SegmentName, query)
                || Contains(row.SlotPath, query)
                || Contains(row.CueLabel, query)
                || Contains(row.ClipSummary, query)
                || Contains(row.AssetPath, query)
                || Contains(row.ClipPathSummary, query);
        }

        private void PlayRandomClip(SfxRow row)
        {
            if (row.Clips == null || row.Clips.Length == 0)
            {
                return;
            }

            List<AudioClip> playable = new List<AudioClip>(row.Clips.Length);
            for (int i = 0; i < row.Clips.Length; i++)
            {
                if (row.Clips[i] != null)
                {
                    playable.Add(row.Clips[i]);
                }
            }

            if (playable.Count == 0)
            {
                return;
            }

            int index = UnityEngine.Random.Range(0, playable.Count);
            PlayClip(playable[index], row);
        }

        private void PlayClip(AudioClip clip, SfxRow row)
        {
            if (clip == null)
            {
                return;
            }

            EditorAudioPreview.Play(clip);
            lastPlayedClip = clip;
            lastStatus = $"재생: {clip.name} / {row.TargetName} / {row.SlotPath}";
        }

        private static SegmentInfo ResolveSegmentInfoFromPath(string path, Dictionary<string, string> displayNameById)
        {
            string id = ExtractSegmentIdFromPath(path);
            string displayName = string.Empty;
            if (!string.IsNullOrEmpty(id) && displayNameById.TryGetValue(id, out string foundDisplayName))
            {
                displayName = foundDisplayName;
            }

            return new SegmentInfo(id, displayName, ExtractLevel(path));
        }

        private SegmentInfo ResolveSegmentInfo(GameObject prefab, string path)
        {
            if (segmentInfoByPrefabPath.TryGetValue(path, out SegmentInfo mapped))
            {
                return mapped;
            }

            SegmentInfo parsed = ResolveSegmentInfoFromPath(path, segmentDisplayNameById);
            if (!string.IsNullOrEmpty(parsed.Id))
            {
                return parsed;
            }

            SegmentWeaponBehaviour weapon = prefab.GetComponentInChildren<SegmentWeaponBehaviour>(true);
            if (weapon != null)
            {
                string id = weapon.EffectiveSegmentId;
                string displayName = segmentDisplayNameById.TryGetValue(id, out string foundDisplayName) ? foundDisplayName : string.Empty;
                return new SegmentInfo(id, displayName, ExtractLevel(path));
            }

            return SegmentInfo.Empty;
        }

        private static string ResolveCategory(string path, string sourceKind)
        {
            if (string.Equals(sourceKind, "Catalog", StringComparison.OrdinalIgnoreCase))
            {
                return "카탈로그";
            }

            if (path.StartsWith("Assets/Resources/StarterSegments/", StringComparison.OrdinalIgnoreCase))
            {
                return "런타임 리소스";
            }

            if (path.StartsWith("Assets/Segments/Starter/", StringComparison.OrdinalIgnoreCase))
            {
                return "스타터 세그먼트";
            }

            if (path.StartsWith("Assets/Segments/", StringComparison.OrdinalIgnoreCase))
            {
                return "세그먼트";
            }

            if (path.StartsWith("Assets/Prefabs/Player/", StringComparison.OrdinalIgnoreCase))
            {
                return "플레이어/루팅";
            }

            if (path.StartsWith("Assets/Resources/RewardPickups/", StringComparison.OrdinalIgnoreCase))
            {
                return "아이템/루팅";
            }

            if (path.IndexOf("Chest", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "상자";
            }

            if (path.StartsWith("Assets/Prefabs/Nexus/", StringComparison.OrdinalIgnoreCase))
            {
                return "넥서스";
            }

            if (path.StartsWith("Assets/Prefabs/ActionHud/", StringComparison.OrdinalIgnoreCase)
                || path.IndexOf("GoldAction", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "스킬/HUD";
            }

            if (path.IndexOf("/RunResult/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "결과 UI";
            }

            return "기타";
        }

        private static bool ShouldScanDirectAudioSource(string path)
        {
            return path.StartsWith("Assets/Segments/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Assets/Prefabs/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Assets/Resources/", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractSegmentIdFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace('\\', '/');
            string[] parts = normalized.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.StartsWith("SG", StringComparison.OrdinalIgnoreCase))
                {
                    return TrimLevelSuffix(Path.GetFileNameWithoutExtension(part));
                }
            }

            return string.Empty;
        }

        private static string TrimLevelSuffix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            int index = value.IndexOf("_Lv", StringComparison.OrdinalIgnoreCase);
            return index > 0 ? value.Substring(0, index) : value;
        }

        private static int ExtractLevel(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return 0;
            }

            int index = path.IndexOf("_Lv", StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return 0;
            }

            int start = index + 3;
            int end = start;
            while (end < path.Length && char.IsDigit(path[end]))
            {
                end++;
            }

            if (end <= start)
            {
                return 0;
            }

            return int.TryParse(path.Substring(start, end - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out int level) ? level : 0;
        }

        private static string GetTransformPath(Transform root, Transform target)
        {
            if (root == null || target == null)
            {
                return string.Empty;
            }

            if (root == target)
            {
                return root.name;
            }

            Stack<string> names = new Stack<string>();
            Transform current = target;
            while (current != null)
            {
                names.Push(current.name);
                if (current == root)
                {
                    break;
                }

                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }

        private static string TranslateCue(GameplaySfxCue cue)
        {
            switch (cue)
            {
                case GameplaySfxCue.Fire:
                    return "발사";
                case GameplaySfxCue.FireStart:
                    return "발사 시작";
                case GameplaySfxCue.FireLoop:
                    return "지속 발사";
                case GameplaySfxCue.Hit:
                    return "타격";
                case GameplaySfxCue.Explosion:
                    return "폭발";
                case GameplaySfxCue.RollHit:
                    return "구르기 타격";
                case GameplaySfxCue.Activation:
                    return "활성화/버프";
                case GameplaySfxCue.Pickup:
                    return "아이템 획득";
                case GameplaySfxCue.GoodPickup:
                    return "좋은 아이템 획득";
                case GameplaySfxCue.ManaOrbPickup:
                    return "마력구슬 획득";
                case GameplaySfxCue.Open:
                    return "열기/상자";
                case GameplaySfxCue.ShieldBreak:
                    return "실드 파괴";
                case GameplaySfxCue.ShieldHit:
                    return "실드 피격";
                case GameplaySfxCue.ShieldRegenStart:
                    return "실드 회복 시작";
                case GameplaySfxCue.NexusHeal:
                    return "넥서스 회복";
                case GameplaySfxCue.ResultClear:
                    return "클리어";
                case GameplaySfxCue.ResultGameOver:
                    return "게임오버";
                case GameplaySfxCue.MeteorCast:
                    return "메테오 시작";
                case GameplaySfxCue.HudSkill2:
                    return "HUD 2번 스킬";
                case GameplaySfxCue.HudSkill3LoopA:
                    return "HUD 3번 루프 A";
                case GameplaySfxCue.HudSkill3LoopB:
                    return "HUD 3번 루프 B";
                case GameplaySfxCue.None:
                    return "없음";
                default:
                    return cue.ToString();
            }
        }

        private static float GetFloat(SerializedObject serialized, string propertyName, float fallback)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            return property != null ? property.floatValue : fallback;
        }

        private static bool GetBool(SerializedObject serialized, string propertyName, bool fallback)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            return property != null ? property.boolValue : fallback;
        }

        private static T GetObjectReference<T>(SerializedObject serialized, string propertyName) where T : UnityEngine.Object
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            return property != null ? property.objectReferenceValue as T : null;
        }

        private static void Header(string text, float width)
        {
            GUILayout.Label(text, EditorStyles.boldLabel, GUILayout.Width(width));
        }

        private static void Label(string text, float width)
        {
            GUILayout.Label(text ?? string.Empty, GUILayout.Width(width));
        }

        private static bool Contains(string source, string query)
        {
            return !string.IsNullOrEmpty(source)
                && source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void PingObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        private static string BuildCopyText(SfxRow row)
        {
            return string.Join("\n", new[]
            {
                "분류: " + row.Category,
                "대상: " + row.TargetName,
                "세그먼트: " + row.SegmentLabel,
                "레벨: " + row.LevelLabel,
                "종류: " + row.SourceKind,
                "슬롯/위치: " + row.SlotPath,
                "큐: " + row.CueLabel,
                "클립: " + row.ClipSummary,
                "볼륨: " + row.VolumeText,
                "쿨타임: " + row.CooldownText,
                "방식: " + row.PlaybackText,
                "프리팹/에셋 경로: " + row.AssetPath,
                "클립 경로: " + row.ClipPathSummary
            });
        }

        private static int CompareRows(SfxRow left, SfxRow right)
        {
            int category = string.Compare(left.Category, right.Category, StringComparison.OrdinalIgnoreCase);
            if (category != 0)
            {
                return category;
            }

            int segment = string.Compare(left.SegmentId, right.SegmentId, StringComparison.OrdinalIgnoreCase);
            if (segment != 0)
            {
                return segment;
            }

            int target = string.Compare(left.TargetName, right.TargetName, StringComparison.OrdinalIgnoreCase);
            if (target != 0)
            {
                return target;
            }

            return string.Compare(left.SlotPath, right.SlotPath, StringComparison.OrdinalIgnoreCase);
        }

        private readonly struct SegmentInfo
        {
            public static readonly SegmentInfo Empty = new SegmentInfo(string.Empty, string.Empty, 0);

            public readonly string Id;
            public readonly string DisplayName;
            public readonly int Level;

            public SegmentInfo(string id, string displayName, int level)
            {
                Id = id ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
                Level = Mathf.Max(0, level);
            }
        }

        private sealed class SfxRow
        {
            public string Category;
            public string SourceKind;
            public string TargetName;
            public string SegmentId;
            public string SegmentName;
            public int Level;
            public string SlotPath;
            public GameplaySfxCue Cue;
            public string CueLabel;
            public AudioClip[] Clips;
            public float Volume;
            public float Cooldown;
            public bool Spatial;
            public bool Detached;
            public string AssetPath;
            public UnityEngine.Object MainObject;

            public bool HasMissingClip => Clips == null || Clips.Length == 0 || FirstClip == null;
            public bool HasPlayableClip => FirstClip != null;
            public AudioClip FirstClip
            {
                get
                {
                    if (Clips == null)
                    {
                        return null;
                    }

                    for (int i = 0; i < Clips.Length; i++)
                    {
                        if (Clips[i] != null)
                        {
                            return Clips[i];
                        }
                    }

                    return null;
                }
            }

            public string SegmentLabel
            {
                get
                {
                    if (string.IsNullOrWhiteSpace(SegmentId))
                    {
                        return "-";
                    }

                    return string.IsNullOrWhiteSpace(SegmentName) ? SegmentId : SegmentId + " / " + SegmentName;
                }
            }

            public string LevelLabel => Level > 0 ? Level.ToString(CultureInfo.InvariantCulture) : "-";
            public string VolumeText => FormatFloat(Volume);
            public string CooldownText => Cooldown > 0f ? FormatFloat(Cooldown) : "-";
            public string PlaybackText => (Spatial ? "3D" : "2D") + (Detached ? " / 분리" : string.Empty);

            public string ClipSummary
            {
                get
                {
                    if (Clips == null || Clips.Length == 0)
                    {
                        return "(비어 있음)";
                    }

                    List<string> names = new List<string>(Clips.Length);
                    for (int i = 0; i < Clips.Length; i++)
                    {
                        names.Add(Clips[i] != null ? Clips[i].name : "(Missing)");
                    }

                    return string.Join(", ", names);
                }
            }

            public string ClipPathSummary
            {
                get
                {
                    if (Clips == null || Clips.Length == 0)
                    {
                        return "-";
                    }

                    List<string> paths = new List<string>(Clips.Length);
                    for (int i = 0; i < Clips.Length; i++)
                    {
                        paths.Add(Clips[i] != null ? AssetDatabase.GetAssetPath(Clips[i]) : "(Missing)");
                    }

                    return string.Join(" | ", paths);
                }
            }
        }

        private static class EditorAudioPreview
        {
            private static readonly Type AudioUtilType = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            private static readonly MethodInfo PlayMethod = FindMethod("PlayPreviewClip") ?? FindMethod("PlayClip");
            private static readonly MethodInfo StopAllMethod = FindMethod("StopAllPreviewClips") ?? FindMethod("StopAllClips");

            public static void Play(AudioClip clip)
            {
                if (clip == null || PlayMethod == null)
                {
                    Debug.LogWarning("[GameplaySfxTable] 에디터 오디오 미리듣기 API를 찾지 못했습니다.");
                    return;
                }

                StopAll();
                object[] args = BuildPlayArguments(PlayMethod, clip);
                PlayMethod.Invoke(null, args);
            }

            public static void StopAll()
            {
                if (StopAllMethod == null)
                {
                    return;
                }

                StopAllMethod.Invoke(null, null);
            }

            private static MethodInfo FindMethod(string methodName)
            {
                if (AudioUtilType == null)
                {
                    return null;
                }

                MethodInfo[] methods = AudioUtilType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length > 0 && parameters[0].ParameterType == typeof(AudioClip))
                    {
                        return method;
                    }

                    if (parameters.Length == 0 && methodName.StartsWith("Stop", StringComparison.Ordinal))
                    {
                        return method;
                    }
                }

                return null;
            }

            private static object[] BuildPlayArguments(MethodInfo method, AudioClip clip)
            {
                ParameterInfo[] parameters = method.GetParameters();
                object[] args = new object[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    Type parameterType = parameters[i].ParameterType;
                    if (parameterType == typeof(AudioClip))
                    {
                        args[i] = clip;
                    }
                    else if (parameterType == typeof(int))
                    {
                        args[i] = 0;
                    }
                    else if (parameterType == typeof(bool))
                    {
                        args[i] = false;
                    }
                    else
                    {
                        args[i] = parameterType.IsValueType ? Activator.CreateInstance(parameterType) : null;
                    }
                }

                return args;
            }
        }
    }
}
