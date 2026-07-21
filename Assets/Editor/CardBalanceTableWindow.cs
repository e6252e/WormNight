using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TeamProject01.Gameplay;
using UnityEditor;
using UnityEngine;

namespace TeamProject01.EditorTools
{
    public sealed class CardBalanceTableWindow : EditorWindow
    {
        private const string WindowTitle = "강화카드 밸런스 테이블";
        private const string MenuPath = "JC Tool/Balance/강화카드 밸런스 테이블";
        private const string DefaultStatCardFolder = "Assets/Prefabs/LevelCard/Stat Upgrade";
        private const string DefaultStatCatalogPath = "Assets/Resources/LevelCard/StatUpgradeCatalog.asset";
        private const string DefaultWeaponCatalogPath = "Assets/Segments/_Catalog/CardSegment/WeaponEnhancementCatalog.asset";
        private const string DefaultSegmentCatalogPath = "Assets/Segments/_Catalog/SegmentCatalog.asset";
        private const string DefaultWeaponDefinitionFolder = "Assets/Segments/_Weapon";
        private const float RowHeight = 22f;
        private const float SegmentTierValueWidth = 126f;
        private const float SegmentTierNumberWidth = 70f;
        private const float TierModeWidth = 50f;
        private static readonly Color ChangedRowColor = new Color(1f, 0.82f, 0.32f, 0.22f);
        private static readonly string[] PercentModeLabels = { "숫자", "%" };

        private readonly List<BalanceRow> rows = new List<BalanceRow>(128);
        private readonly List<SegmentDraftRow> segmentDraftRows = new List<SegmentDraftRow>(16);
        private readonly Dictionary<string, bool> segmentSectionFoldouts = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private StatUpgradeCatalogAsset statUpgradeCatalog;
        private WeaponCatalogAsset weaponCatalog;
        private SegmentCatalogAsset segmentCatalog;
        private Vector2 scroll;
        private string searchText = string.Empty;
        private bool showDirtyOnly;
        private bool showStatCards = true;
        private bool showWeaponCards = true;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            CardBalanceTableWindow window = GetWindow<CardBalanceTableWindow>(WindowTitle);
            window.minSize = new Vector2(1180f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            if (statUpgradeCatalog == null)
            {
                statUpgradeCatalog = AssetDatabase.LoadAssetAtPath<StatUpgradeCatalogAsset>(DefaultStatCatalogPath);
            }

            if (weaponCatalog == null)
            {
                weaponCatalog = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(DefaultWeaponCatalogPath);
            }

            if (segmentCatalog == null)
            {
                segmentCatalog = AssetDatabase.LoadAssetAtPath<SegmentCatalogAsset>(DefaultSegmentCatalogPath);
            }
            RefreshRows();
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
                    EditorGUI.BeginChangeCheck();
                    statUpgradeCatalog = (StatUpgradeCatalogAsset)EditorGUILayout.ObjectField("공통 카드 카탈로그", statUpgradeCatalog, typeof(StatUpgradeCatalogAsset), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RefreshRows();
                    }

                    DrawCommonTemplateField();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    weaponCatalog = (WeaponCatalogAsset)EditorGUILayout.ObjectField("무기 강화 카탈로그", weaponCatalog, typeof(WeaponCatalogAsset), false);
                    segmentCatalog = (SegmentCatalogAsset)EditorGUILayout.ObjectField("세그먼트 카탈로그", segmentCatalog, typeof(SegmentCatalogAsset), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RefreshRows();
                    }

                    if (GUILayout.Button("새로고침", GUILayout.Width(90f)))
                    {
                        RefreshRows();
                    }

                    GUI.enabled = CountDirtyRows(rows) > 0;
                    if (GUILayout.Button("전체 적용", GUILayout.Width(90f)))
                    {
                        ApplyRows("Apply All Card Balance Rows", CollectDirtyRows(rows));
                    }

                    if (GUILayout.Button("전체 리셋", GUILayout.Width(90f)))
                    {
                        ResetRows("전체 리셋", rows);
                    }

                    GUI.enabled = true;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    searchText = EditorGUILayout.TextField("검색", searchText ?? string.Empty);
                    showDirtyOnly = GUILayout.Toggle(showDirtyOnly, "변경만 보기", EditorStyles.toolbarButton, GUILayout.Width(90f));
                    showStatCards = GUILayout.Toggle(showStatCards, "공통 카드", EditorStyles.toolbarButton, GUILayout.Width(80f));
                    showWeaponCards = GUILayout.Toggle(showWeaponCards, "새그먼트 카드", EditorStyles.toolbarButton, GUILayout.Width(98f));
                }

                EditorGUILayout.HelpBox(
                    "공통카드는 StatUpgradeCatalog의 데이터 에셋을 기준으로 편집하고, UI 위치/양식은 공통 카드 템플릿 프리팹에서 조절합니다.",
                    MessageType.Info);
            }
        }

        private void DrawCommonTemplateField()
        {
            if (statUpgradeCatalog == null)
            {
                GUI.enabled = false;
                EditorGUILayout.ObjectField("공통 카드 템플릿", null, typeof(GameObject), false);
                GUI.enabled = true;
                return;
            }

            EditorGUI.BeginChangeCheck();
            GameObject template = (GameObject)EditorGUILayout.ObjectField("공통 카드 템플릿", statUpgradeCatalog.DefaultCardPrefab, typeof(GameObject), false);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Undo.RecordObject(statUpgradeCatalog, "Set Common Card Template");
            statUpgradeCatalog.DefaultCardPrefab = template;
            EditorUtility.SetDirty(statUpgradeCatalog);
            AssetDatabase.SaveAssets();
            RefreshRows();
        }

        private void DrawSummary()
        {
            EditorGUILayout.LabelField($"표시 행 {CountVisibleRows(rows)}/{rows.Count}개 / 변경 {CountDirtyRows(rows)}개 / 공통 템플릿: {ResolveCommonTemplateName()} / 로그 위치: {GetLogDirectory()}");
        }

        private string ResolveCommonTemplateName()
        {
            if (statUpgradeCatalog == null)
            {
                return "카탈로그 없음";
            }

            return statUpgradeCatalog.DefaultCardPrefab != null ? statUpgradeCatalog.DefaultCardPrefab.name : "없음";
        }

        private void DrawTable()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll, true, true);

            List<BalanceRow> visibleRows = GetVisibleRows();
            List<BalanceRow> commonRows = new List<BalanceRow>();
            List<BalanceRow> segmentRows = new List<BalanceRow>();
            for (int i = 0; i < visibleRows.Count; i++)
            {
                BalanceRow row = visibleRows[i];
                if (row.Kind == CardRowKind.Stat)
                {
                    commonRows.Add(row);
                }
                else
                {
                    segmentRows.Add(row);
                }
            }

            bool drewAny = false;
            if (showStatCards && commonRows.Count > 0)
            {
                drewAny = true;
                DrawCommonSection(commonRows);
            }

            if (showStatCards && showWeaponCards && commonRows.Count > 0 && segmentRows.Count > 0)
            {
                EditorGUILayout.Space(8f);
            }

            if (showWeaponCards)
            {
                List<string> segmentIds = BuildSegmentSectionIds(segmentRows);
                if (segmentIds.Count > 0)
                {
                    drewAny = true;
                    DrawSegmentGroupSection(segmentIds, segmentRows);
                }
            }

            if (!drewAny)
            {
                EditorGUILayout.HelpBox("표시할 강화카드 수치가 없습니다. 검색어, 변경만 보기, 섹션 토글을 확인하세요.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSegmentGroupSection(List<string> segmentIds, List<BalanceRow> segmentRows)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawSegmentGroupTitle(segmentRows);
                for (int i = 0; i < segmentIds.Count; i++)
                {
                    string segmentId = segmentIds[i];
                    List<BalanceRow> sectionRows = CollectSegmentRows(segmentRows, segmentId);
                    DrawSegmentSection(segmentId, sectionRows);
                }
            }
        }

        private void DrawSegmentGroupTitle(List<BalanceRow> segmentRows)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label($"새그먼트 강화카드  ({segmentRows.Count}개 / 신규 {CountVisibleSegmentDraftRows()}개 / 변경 {CountDirtyRows(segmentRows)}개)", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.LabelField("세그먼트별 전용 강화카드 수치입니다. 카드 추가와 능력치 라인 추가는 각 세그먼트 섹션 안에서 처리합니다.", EditorStyles.miniLabel);
        }

        private int CountVisibleSegmentDraftRows()
        {
            int count = 0;
            for (int i = 0; i < segmentDraftRows.Count; i++)
            {
                if (DraftMatchesCurrentFilters(segmentDraftRows[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private void DrawCommonSection(List<BalanceRow> commonRows)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawSectionTitle("공통 강화카드", "세그먼트 종류와 상관없이 적용되는 공통 성장 카드 수치입니다.", commonRows);
                DrawCommonHeader();
                for (int i = 0; i < commonRows.Count; i++)
                {
                    DrawCommonRow(commonRows[i]);
                }
            }
        }

        private void DrawSectionTitle(string title, string description, List<BalanceRow> sectionRows)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label($"{title}  ({sectionRows.Count}개 / 변경 {CountDirtyRows(sectionRows)}개)", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                GUI.enabled = CountDirtyRows(sectionRows) > 0;
                if (GUILayout.Button("섹션 적용", GUILayout.Width(78f)))
                {
                    ApplyRows($"Apply {title}", CollectDirtyRows(sectionRows));
                }

                if (GUILayout.Button("섹션 리셋", GUILayout.Width(78f)))
                {
                    ResetRows($"{title} 리셋", sectionRows);
                }

                GUI.enabled = true;
            }

            EditorGUILayout.LabelField(description, EditorStyles.miniLabel);
        }

        private void DrawSegmentSection(string segmentId, List<BalanceRow> sectionRows)
        {
            string title = GetSegmentSectionTitle(segmentId);
            List<SegmentDraftRow> draftRows = CollectSegmentDraftRows(segmentId);
            int dirtyCount = CountDirtyRows(sectionRows) + draftRows.Count;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool isOpen = GetDictionaryFoldout(segmentSectionFoldouts, segmentId, true);
                    bool nextOpen = EditorGUILayout.Foldout(isOpen, $"{title}  ({sectionRows.Count}개 / 신규 {draftRows.Count}개 / 변경 {dirtyCount}개)", true, EditorStyles.foldoutHeader);
                    SetDictionaryFoldout(segmentSectionFoldouts, segmentId, nextOpen);
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("+ 카드 추가", GUILayout.Width(92f)))
                    {
                        AddSegmentDraftRow(segmentId);
                        SetDictionaryFoldout(segmentSectionFoldouts, segmentId, true);
                    }

                    GUI.enabled = CountDirtyRows(sectionRows) > 0;
                    if (GUILayout.Button("섹션 적용", GUILayout.Width(78f)))
                    {
                        ApplyRows($"Apply {segmentId}", CollectDirtyRows(sectionRows));
                    }

                    if (GUILayout.Button("섹션 리셋", GUILayout.Width(78f)))
                    {
                        ResetRows($"{segmentId} 리셋", sectionRows);
                    }

                    GUI.enabled = true;
                }

                if (!GetDictionaryFoldout(segmentSectionFoldouts, segmentId, true))
                {
                    return;
                }

                if (sectionRows.Count == 0 && draftRows.Count == 0)
                {
                    EditorGUILayout.HelpBox("아직 등록된 강화카드가 없습니다. '+ 카드 추가'로 신규 행을 만든 뒤 오른쪽 적용 버튼으로 생성할 수 있습니다.", MessageType.Info);
                    return;
                }

                DrawSegmentHeader();
                for (int i = 0; i < sectionRows.Count; i++)
                {
                    DrawSegmentRow(sectionRows[i]);
                }

                for (int i = 0; i < draftRows.Count; i++)
                {
                    DrawSegmentDraftRow(draftRows[i]);
                }
            }
        }

        private List<string> BuildSegmentSectionIds(List<BalanceRow> segmentRows)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (segmentCatalog != null && segmentCatalog.Segments != null && string.IsNullOrWhiteSpace(searchText) && !showDirtyOnly)
            {
                for (int i = 0; i < segmentCatalog.Segments.Length; i++)
                {
                    SegmentDefinition definition = segmentCatalog.Segments[i];
                    if (definition == null || !definition.HasId || definition.StarterOnly || !definition.CanUpgradeByLevelChoice)
                    {
                        continue;
                    }

                    ids.Add(definition.UpgradeId);
                }
            }

            for (int i = 0; i < segmentRows.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(segmentRows[i].TargetLabel))
                {
                    ids.Add(segmentRows[i].TargetLabel);
                }
            }

            for (int i = 0; i < segmentDraftRows.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(segmentDraftRows[i].SegmentId))
                {
                    ids.Add(segmentDraftRows[i].SegmentId);
                }
            }

            List<string> result = new List<string>(ids);
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                string needle = searchText.Trim();
                result.RemoveAll(id => !Contains(id, needle)
                    && !Contains(GetSegmentDisplayName(id), needle)
                    && CollectSegmentRows(segmentRows, id).Count == 0
                    && CollectSegmentDraftRows(id).Count == 0);
            }

            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        private static List<BalanceRow> CollectSegmentRows(List<BalanceRow> segmentRows, string segmentId)
        {
            List<BalanceRow> result = new List<BalanceRow>();
            for (int i = 0; i < segmentRows.Count; i++)
            {
                if (string.Equals(segmentRows[i].TargetLabel, segmentId, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(segmentRows[i]);
                }
            }

            return result;
        }

        private List<SegmentDraftRow> CollectSegmentDraftRows(string segmentId)
        {
            List<SegmentDraftRow> result = new List<SegmentDraftRow>();
            for (int i = 0; i < segmentDraftRows.Count; i++)
            {
                if (!DraftMatchesCurrentFilters(segmentDraftRows[i]))
                {
                    continue;
                }

                if (string.Equals(segmentDraftRows[i].SegmentId, segmentId, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(segmentDraftRows[i]);
                }
            }

            return result;
        }

        private bool DraftMatchesCurrentFilters(SegmentDraftRow draft)
        {
            if (draft == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            string needle = searchText.Trim();
            WeaponStatInfo info = GetWeaponStatInfo(draft.StatKind);
            return Contains("신규", needle)
                || Contains(draft.SegmentId, needle)
                || Contains(GetSegmentDisplayName(draft.SegmentId), needle)
                || Contains(draft.CardName, needle)
                || Contains(draft.AssetName, needle)
                || Contains(draft.Description, needle)
                || Contains(info.Label, needle)
                || Contains(info.ModeLabel, needle);
        }

        private void AddSegmentDraftRow(string segmentId)
        {
            WeaponStatInfo info = GetWeaponStatInfo(GetDefaultWeaponStatKindForSegment(segmentId));
            segmentDraftRows.Add(new SegmentDraftRow(segmentId, info, BuildDefaultWeaponCardName(segmentId)));
        }

        private WeaponStatKind GetDefaultWeaponStatKindForSegment(string segmentId)
        {
            WeaponStatKind[] allowedKinds = GetAllowedWeaponStatKindsForSegment(segmentId);
            return allowedKinds.Length > 0 ? allowedKinds[0] : WeaponStatKind.BaseDamage;
        }

        private WeaponStatKind[] GetAllowedWeaponStatKindsForSegment(string segmentId)
        {
            WeaponStatKind[] allKinds = GetWeaponStatKinds();
            HashSet<WeaponStatKind> allowedKinds = new HashSet<WeaponStatKind>();
            bool foundProfile = false;

            if (TryFindSegmentDefinitionForCardSection(segmentId, out SegmentDefinition definition)
                && definition.Levels != null)
            {
                for (int i = 0; i < definition.Levels.Length; i++)
                {
                    SegmentAttackProfile profile = definition.Levels[i].AttackProfile;
                    if (profile == null)
                    {
                        continue;
                    }

                    foundProfile = true;
                    AddAllowedWeaponStatsFromProfile(profile, allowedKinds);
                }
            }

            AddExistingWeaponDefinitionStats(segmentId, allowedKinds);
            if (!foundProfile && allowedKinds.Count == 0)
            {
                return allKinds;
            }

            return FilterWeaponStatKinds(allKinds, allowedKinds);
        }

        private bool TryFindSegmentDefinitionForCardSection(string segmentId, out SegmentDefinition definition)
        {
            definition = null;
            if (segmentCatalog == null || segmentCatalog.Segments == null || string.IsNullOrWhiteSpace(segmentId))
            {
                return false;
            }

            string normalizedId = segmentId.Trim();
            for (int i = 0; i < segmentCatalog.Segments.Length; i++)
            {
                SegmentDefinition candidate = segmentCatalog.Segments[i];
                if (candidate != null && string.Equals(candidate.NormalizedId, normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    definition = candidate;
                    return true;
                }
            }

            for (int i = 0; i < segmentCatalog.Segments.Length; i++)
            {
                SegmentDefinition candidate = segmentCatalog.Segments[i];
                if (candidate != null && string.Equals(candidate.UpgradeId, normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    definition = candidate;
                    return true;
                }
            }

            return false;
        }

        private void AddExistingWeaponDefinitionStats(string segmentId, HashSet<WeaponStatKind> allowedKinds)
        {
            if (weaponCatalog == null
                || allowedKinds == null
                || string.IsNullOrWhiteSpace(segmentId)
                || !weaponCatalog.TryGetEnhancementsForSegment(segmentId, out WeaponDefinition[] definitions)
                || definitions == null)
            {
                return;
            }

            for (int i = 0; i < definitions.Length; i++)
            {
                AddWeaponDefinitionStats(definitions[i], allowedKinds);
            }
        }

        private static void AddAllowedWeaponStatsFromProfile(SegmentAttackProfile profile, HashSet<WeaponStatKind> allowedKinds)
        {
            if (profile == null || allowedKinds == null)
            {
                return;
            }

            allowedKinds.Add(WeaponStatKind.BaseDamage);
            allowedKinds.Add(WeaponStatKind.SearchRange);
            allowedKinds.Add(WeaponStatKind.CooldownReduction);

            bool projectileLike = profile.MoveType != SegmentAttackMoveType.Laser
                && profile.MoveType != SegmentAttackMoveType.ChainLightning;
            if (projectileLike)
            {
                allowedKinds.Add(WeaponStatKind.ProjectileSpeed);
                allowedKinds.Add(WeaponStatKind.ProjectileCount);
            }

            if (profile.ImpactType == SegmentAttackImpactType.PierceDamage
                || profile.MoveType == SegmentAttackMoveType.PiercingProjectile)
            {
                allowedKinds.Add(WeaponStatKind.PierceCount);
            }

            if (profile.ImpactType == SegmentAttackImpactType.ExplosionArea
                || profile.ImpactType == SegmentAttackImpactType.ContinuousDamage
                || profile.MoveType == SegmentAttackMoveType.ExpandingFlameSphere)
            {
                allowedKinds.Add(WeaponStatKind.ExplosionRadius);
            }

            if (profile.AttackAreaMode == SegmentAttackAreaMode.SideCones)
            {
                allowedKinds.Add(WeaponStatKind.SideConeAngle);
            }

            if (profile.MoveType == SegmentAttackMoveType.ChainLightning)
            {
                allowedKinds.Add(WeaponStatKind.MaxChainDepth);
                allowedKinds.Add(WeaponStatKind.ChainRange);
                allowedKinds.Add(WeaponStatKind.ChainDamageFalloff);
            }

            if (profile.MoveType == SegmentAttackMoveType.SawBounceProjectile)
            {
                allowedKinds.Add(WeaponStatKind.MaxChainDepth);
                allowedKinds.Add(WeaponStatKind.ChainRange);
                allowedKinds.Add(WeaponStatKind.SawPierceDamageRatio);
            }

            if (profile.MoveType == SegmentAttackMoveType.Laser
                || profile.MoveType == SegmentAttackMoveType.ExpandingFlameSphere)
            {
                allowedKinds.Add(WeaponStatKind.LaserDuration);
                allowedKinds.Add(WeaponStatKind.LaserTickInterval);
            }

            if (profile.RollAfterArcLanding || profile.LandingRollDistance > 0.0001f)
            {
                allowedKinds.Add(WeaponStatKind.LandingRollDistance);
                allowedKinds.Add(WeaponStatKind.LandingRollDuration);
            }
        }

        private static void AddWeaponDefinitionStats(WeaponDefinition definition, HashSet<WeaponStatKind> allowedKinds)
        {
            if (definition == null || allowedKinds == null)
            {
                return;
            }

            if (HasAny(definition.BaseDamage, definition.BaseDamageRare, definition.BaseDamageUnique)) allowedKinds.Add(WeaponStatKind.BaseDamage);
            if (HasAny(definition.SawPierceDamageRatio, definition.SawPierceDamageRatioRare, definition.SawPierceDamageRatioUnique)) allowedKinds.Add(WeaponStatKind.SawPierceDamageRatio);
            if (HasAny(definition.ProjectileSpeed, definition.ProjectileSpeedRare, definition.ProjectileSpeedUnique)) allowedKinds.Add(WeaponStatKind.ProjectileSpeed);
            if (HasAny(definition.SearchRange, definition.SearchRangeRare, definition.SearchRangeUnique)) allowedKinds.Add(WeaponStatKind.SearchRange);
            if (HasAnyInt(definition.MaxChainDepth, definition.MaxChainDepthRare, definition.MaxChainDepthUnique)) allowedKinds.Add(WeaponStatKind.MaxChainDepth);
            if (HasAny(definition.ChainRange, definition.ChainRangeRare, definition.ChainRangeUnique)) allowedKinds.Add(WeaponStatKind.ChainRange);
            if (HasAny(definition.ChainDamageFalloff, definition.ChainDamageFalloffRare, definition.ChainDamageFalloffUnique)) allowedKinds.Add(WeaponStatKind.ChainDamageFalloff);
            if (HasAnyInt(definition.ProjectileCount, definition.ProjectileCountRare, definition.ProjectileCountUnique)) allowedKinds.Add(WeaponStatKind.ProjectileCount);
            if (HasAny(definition.CooldownReduction, definition.CooldownReductionRare, definition.CooldownReductionUnique)) allowedKinds.Add(WeaponStatKind.CooldownReduction);
            if (HasAny(definition.SideConeAngle, definition.SideConeAngleRare, definition.SideConeAngleUnique)) allowedKinds.Add(WeaponStatKind.SideConeAngle);
            if (HasAny(definition.LaserDuration, definition.LaserDurationRare, definition.LaserDurationUnique)) allowedKinds.Add(WeaponStatKind.LaserDuration);
            if (HasAny(definition.LaserTickInterval, definition.LaserTickIntervalRare, definition.LaserTickIntervalUnique)) allowedKinds.Add(WeaponStatKind.LaserTickInterval);
            if (HasAny(definition.LandingRollDistance, definition.LandingRollDistanceRare, definition.LandingRollDistanceUnique)) allowedKinds.Add(WeaponStatKind.LandingRollDistance);
            if (HasAny(definition.LandingRollDuration, definition.LandingRollDurationRare, definition.LandingRollDurationUnique)) allowedKinds.Add(WeaponStatKind.LandingRollDuration);
            if (HasAnyInt(definition.PierceCount, definition.PierceCountRare, definition.PierceCountUnique)) allowedKinds.Add(WeaponStatKind.PierceCount);
            if (HasAny(definition.ExplosionRadius, definition.ExplosionRadiusRare, definition.ExplosionRadiusUnique)) allowedKinds.Add(WeaponStatKind.ExplosionRadius);
        }

        private static WeaponStatKind[] FilterWeaponStatKinds(WeaponStatKind[] allKinds, HashSet<WeaponStatKind> allowedKinds)
        {
            if (allKinds == null || allKinds.Length == 0 || allowedKinds == null || allowedKinds.Count == 0)
            {
                return GetWeaponStatKinds();
            }

            List<WeaponStatKind> result = new List<WeaponStatKind>();
            for (int i = 0; i < allKinds.Length; i++)
            {
                if (allowedKinds.Contains(allKinds[i]))
                {
                    result.Add(allKinds[i]);
                }
            }

            return result.Count > 0 ? result.ToArray() : GetWeaponStatKinds();
        }

        private string GetSegmentSectionTitle(string segmentId)
        {
            string displayName = GetSegmentDisplayName(segmentId);
            if (string.IsNullOrWhiteSpace(displayName) || string.Equals(displayName, segmentId, StringComparison.OrdinalIgnoreCase))
            {
                return segmentId;
            }

            return $"{segmentId} - {displayName}";
        }

        private string GetSegmentDisplayName(string segmentId)
        {
            if (segmentCatalog != null && !string.IsNullOrWhiteSpace(segmentId) && segmentCatalog.TryFind(segmentId, out SegmentDefinition definition) && definition != null)
            {
                return string.IsNullOrWhiteSpace(definition.DisplayName) ? segmentId : definition.DisplayName;
            }

            return segmentId;
        }

        private static bool GetDictionaryFoldout(Dictionary<string, bool> source, string key, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return defaultValue;
            }

            if (!source.TryGetValue(key, out bool value))
            {
                value = defaultValue;
                source[key] = value;
            }

            return value;
        }

        private static void SetDictionaryFoldout(Dictionary<string, bool> source, string key, bool value)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                source[key] = value;
            }
        }

        private void DrawCommonHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                Header("상태", 54f, "기존 카드 또는 생성 전 신규 행입니다.");
                Header("카드 이름", 150f, "공통 강화카드 표시 이름입니다.");
                Header("데이터에셋", 160f, "수정 대상 StatUpgradeDefinition 에셋입니다. 버튼을 누르면 Project 창에서 선택합니다.");
                Header("설명", 210f, "카드 설명입니다. (N)은 등급 수치로 자동 치환됩니다.");
                Header("능력치", 126f, "이 카드가 올리는 능력치입니다.");
                Header("일반수치", SegmentTierValueWidth, "일반 등급 수치와 수치 방식입니다.");
                Header("레어수치", SegmentTierValueWidth, "레어 등급 수치와 수치 방식입니다.");
                Header("유니크수치", SegmentTierValueWidth, "유니크 등급 수치와 수치 방식입니다.");
                GUILayout.FlexibleSpace();
                Header("작업", 206f, "행 단위 적용, 리셋, Ping, 삭제입니다.");
            }
        }

        private void DrawSegmentHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                Header("상태", 54f, "기존 카드 또는 생성 전 신규 행입니다.");
                Header("카드 이름", 150f, "WeaponDefinition DisplayName 입니다.");
                Header("에셋명", 160f, "생성될 에셋 파일명 또는 기존 에셋입니다.");
                Header("설명", 210f, "능력치 라인별 카드 설명입니다. (N)은 등급 수치로 자동 치환됩니다.");
                Header("능력치", 126f, "이 카드가 올리는 능력치입니다. 편집 중 + 버튼으로 라인을 추가합니다.");
                Header("일반수치", SegmentTierValueWidth, "일반 등급 수치와 숫자/% 방식입니다.");
                Header("레어수치", SegmentTierValueWidth, "레어 등급 수치와 숫자/% 방식입니다.");
                Header("유니크수치", SegmentTierValueWidth, "유니크 등급 수치와 숫자/% 방식입니다.");
                GUILayout.FlexibleSpace();
                Header("작업", 206f, "행 단위 적용, 리셋, Ping, 삭제입니다.");
            }
        }

        private void DrawCommonRow(BalanceRow row)
        {
            Rect rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
            DrawChangedRow(rect, row.IsDirty);

            GUILayout.Label(row.TargetLabel, GUILayout.Width(54f));
            row.DrawCardName(150f);
            DrawPingButton(row.AssetName, row.TargetObject, 160f);
            row.DrawDescription(210f);
            row.DrawStatField(126f, Array.Empty<WeaponStatKind>());

            row.DrawSegmentValues();
            GUILayout.FlexibleSpace();
            DrawRowActions(row);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSegmentRow(BalanceRow row)
        {
            Rect rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(row.SegmentRowHeight));
            DrawChangedRow(rect, row.IsDirty);

            GUILayout.Label("기존", GUILayout.Width(54f));
            row.DrawCardName(150f);
            DrawPingButton(row.AssetName, row.TargetObject, 160f);
            row.DrawDescription(210f);
            row.DrawStatField(126f, GetAllowedWeaponStatKindsForSegment(row.TargetLabel));

            row.DrawSegmentValues();
            GUILayout.FlexibleSpace();
            DrawRowActions(row);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSegmentDraftRow(SegmentDraftRow draft)
        {
            Rect rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
            DrawChangedRow(rect, true);

            WeaponStatKind[] allowedKinds = GetAllowedWeaponStatKindsForSegment(draft.SegmentId);
            if (!ContainsWeaponStatKind(allowedKinds, draft.StatKind))
            {
                WeaponStatInfo oldInfo = GetWeaponStatInfo(draft.StatKind);
                bool replaceDescription = ShouldReplaceDraftDescription(draft.Description, oldInfo);
                draft.StatKind = allowedKinds[0];
                ApplyDraftStatSuggestion(draft, GetWeaponStatInfo(draft.StatKind), replaceDescription);
            }

            WeaponStatKind previousStatKind = draft.StatKind;
            WeaponStatInfo previousInfo = GetWeaponStatInfo(previousStatKind);
            draft.CardName = BuildDefaultWeaponCardName(draft.SegmentId);

            GUILayout.Label("신규", GUILayout.Width(54f));
            GUILayout.Label(draft.CardName, GUILayout.Width(150f));
            draft.AssetName = EditorGUILayout.TextField(draft.AssetName, GUILayout.Width(160f));
            draft.Description = EditorGUILayout.TextField(draft.Description, GUILayout.Width(210f));
            draft.StatKind = DrawWeaponStatPopup(GUIContent.none, draft.StatKind, 126f, allowedKinds);
            WeaponStatInfo info = GetWeaponStatInfo(draft.StatKind);
            if (draft.StatKind != previousStatKind)
            {
                ApplyDraftStatSuggestion(draft, info, ShouldReplaceDraftDescription(draft.Description, previousInfo));
            }

            DrawDraftTierField(draft, info);
            GUILayout.FlexibleSpace();
            DrawDraftActions(draft);

            EditorGUILayout.EndHorizontal();
        }

        private static float DrawDraftNumberField(float value, StatValueKind valueKind, float width)
        {
            if (valueKind == StatValueKind.Int)
            {
                return Mathf.Max(0, EditorGUILayout.IntField(Mathf.RoundToInt(value), GUILayout.Width(width)));
            }

            return Mathf.Max(0f, EditorGUILayout.FloatField(value, GUILayout.Width(width)));
        }

        private static void DrawDraftTierField(SegmentDraftRow draft, WeaponStatInfo info)
        {
            draft.NormalValue = DrawDraftTierValueField(draft.NormalValue, info.ValueKind, info.HasPercentMode, info.ModeLabel, ref draft.NormalUsePercent);
            draft.RareValue = DrawDraftTierValueField(draft.RareValue, info.ValueKind, info.HasPercentMode, info.ModeLabel, ref draft.RareUsePercent);
            draft.UniqueValue = DrawDraftTierValueField(draft.UniqueValue, info.ValueKind, info.HasPercentMode, info.ModeLabel, ref draft.UniqueUsePercent);
        }

        private static float DrawDraftTierValueField(float value, StatValueKind valueKind, bool hasPercentMode, string modeLabel, ref bool usePercent)
        {
            using (new GUILayout.HorizontalScope(GUILayout.Width(SegmentTierValueWidth)))
            {
                float nextValue = DrawDraftNumberField(value, valueKind, SegmentTierNumberWidth);
                if (hasPercentMode)
                {
                    usePercent = DrawPercentModePopup(usePercent, TierModeWidth);
                }
                else
                {
                    usePercent = false;
                    GUILayout.Label(modeLabel, GUILayout.Width(TierModeWidth));
                }

                return nextValue;
            }
        }

        private void DrawDraftActions(SegmentDraftRow draft)
        {
            bool valid = IsDraftReady(draft);
            GUI.enabled = valid;
            if (GUILayout.Button("적용", GUILayout.Width(48f)))
            {
                CreateSegmentWeaponCard(draft);
            }

            GUI.enabled = true;
            if (GUILayout.Button("취소", GUILayout.Width(48f)))
            {
                segmentDraftRows.Remove(draft);
            }
        }

        private bool IsDraftReady(SegmentDraftRow draft)
        {
            return draft != null
                && weaponCatalog != null
                && !string.IsNullOrWhiteSpace(draft.SegmentId)
                && !string.IsNullOrWhiteSpace(draft.CardName)
                && !string.IsNullOrWhiteSpace(draft.AssetName)
                && !string.IsNullOrWhiteSpace(draft.Description);
        }

        private void DrawRowActions(BalanceRow row)
        {
            if (row.IsEditing)
            {
                GUI.enabled = row.IsDirty;
                if (GUILayout.Button("적용", GUILayout.Width(48f)))
                {
                    ApplyRows("Apply Card Balance Row", new List<BalanceRow> { row });
                }

                GUI.enabled = true;
                if (GUILayout.Button("취소", GUILayout.Width(48f)))
                {
                    row.ResetWorking();
                    row.EndEdit();
                }
            }
            else
            {
                GUI.enabled = true;
                if (GUILayout.Button("수정", GUILayout.Width(48f)))
                {
                    row.BeginEdit();
                }
            }

            GUI.enabled = row.TargetObject != null;
            if (GUILayout.Button("Ping", GUILayout.Width(48f)))
            {
                EditorGUIUtility.PingObject(row.TargetObject);
                Selection.activeObject = row.TargetObject;
            }

            GUI.enabled = row.TargetObject != null && !string.IsNullOrWhiteSpace(row.AssetPath);
            if (GUILayout.Button("삭제", GUILayout.Width(48f)))
            {
                DeleteCard(row);
            }

            GUI.enabled = true;
        }

        private void CreateSegmentWeaponCard(SegmentDraftRow draft)
        {
            if (weaponCatalog == null)
            {
                EditorUtility.DisplayDialog("새그먼트 카드 추가", "무기 강화 카탈로그가 없습니다.", "확인");
                return;
            }

            string segmentId = draft?.SegmentId?.Trim();
            if (string.IsNullOrWhiteSpace(segmentId))
            {
                EditorUtility.DisplayDialog("새그먼트 카드 추가", "대상 세그먼트 ID를 입력하세요.", "확인");
                return;
            }

            if (!IsDraftReady(draft))
            {
                EditorUtility.DisplayDialog("새그먼트 카드 추가", "카드 이름, 에셋명, 설명을 모두 입력하세요.", "확인");
                return;
            }

            WeaponStatInfo info = GetWeaponStatInfo(draft.StatKind);
            string safeName = MakeSafeFileName(draft.AssetName, BuildDefaultWeaponAssetName(segmentId, info));
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{DefaultWeaponDefinitionFolder}/{safeName}.asset");
            WeaponDefinition definition = CreateInstance<WeaponDefinition>();
            definition.EnhancementId = Path.GetFileNameWithoutExtension(assetPath);
            definition.DisplayName = BuildDefaultWeaponCardName(segmentId);
            definition.Description = draft.Description.Trim();
            definition.TargetSegmentId = segmentId;

            ApplyWeaponStatValues(definition, info, draft);
            CopySegmentIconSettings(segmentId, definition);

            AssetDatabase.CreateAsset(definition, assetPath);
            AddWeaponDefinitionToCatalog(segmentId, definition);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            segmentDraftRows.Remove(draft);
            RefreshRows();
            EditorGUIUtility.PingObject(definition);
            Selection.activeObject = definition;
            WriteToolLog(
                "Create Segment Card",
                $"Card: {definition.DisplayName}\nAsset: {assetPath}\nSegment: {segmentId}\nStat: {info.Label}\nNormal: {FormatFloat(draft.NormalValue)}\nRare: {FormatFloat(draft.RareValue)}\nUnique: {FormatFloat(draft.UniqueValue)}");
        }

        private void DeleteCard(BalanceRow row)
        {
            if (row == null || row.TargetObject == null || string.IsNullOrWhiteSpace(row.AssetPath))
            {
                return;
            }

            if (row.Kind == CardRowKind.Stat)
            {
                DeleteCommonCard(row);
                return;
            }

            DeleteSegmentWeaponCard(row);
        }

        private void DeleteCommonCard(BalanceRow row)
        {
            if (!EditorUtility.DisplayDialog(
                "공통 카드 삭제",
                $"공통 강화카드 데이터 에셋을 삭제할까요?\n카탈로그 참조 제거 후 에셋을 삭제합니다.\n\n{row.AssetPath}",
                "삭제",
                "취소"))
            {
                return;
            }

            string log = $"Card: {row.CardName}\nAsset: {row.AssetPath}\nStat: {row.StatLabel}";
            RemoveStatDefinitionFromCatalog(row.TargetObject as StatUpgradeDefinition);
            if (!AssetDatabase.DeleteAsset(row.AssetPath))
            {
                EditorUtility.DisplayDialog("공통 카드 삭제", "에셋 삭제에 실패했습니다.", "확인");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshRows();
            WriteToolLog("Delete Common Card", log);
        }

        private void RemoveStatDefinitionFromCatalog(StatUpgradeDefinition definition)
        {
            if (statUpgradeCatalog == null || definition == null || statUpgradeCatalog.Cards == null)
            {
                return;
            }

            List<StatUpgradeDefinition> definitions = new List<StatUpgradeDefinition>(statUpgradeCatalog.Cards);
            if (!definitions.Remove(definition))
            {
                return;
            }

            Undo.RecordObject(statUpgradeCatalog, "Remove Common Card From Catalog");
            statUpgradeCatalog.Cards = definitions.ToArray();
            EditorUtility.SetDirty(statUpgradeCatalog);
        }

        private void DeleteSegmentWeaponCard(BalanceRow row)
        {
            WeaponDefinition definition = row.TargetObject as WeaponDefinition;
            if (definition == null)
            {
                return;
            }

            if (weaponCatalog == null)
            {
                EditorUtility.DisplayDialog("새그먼트 카드 삭제", "무기 강화 카탈로그가 없어 참조 정리를 할 수 없습니다.", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "새그먼트 카드 삭제",
                $"새그먼트 강화카드를 삭제할까요?\n카탈로그 참조 제거 후 에셋을 삭제합니다.\n\n{row.AssetPath}",
                "삭제",
                "취소"))
            {
                return;
            }

            string log = $"Card: {row.CardName}\nAsset: {row.AssetPath}\nSegment: {row.TargetLabel}\nStat: {row.StatLabel}";
            Undo.RecordObject(weaponCatalog, "Delete Weapon Enhancement Card");
            RemoveWeaponDefinitionFromCatalog(definition);
            EditorUtility.SetDirty(weaponCatalog);

            if (!AssetDatabase.DeleteAsset(row.AssetPath))
            {
                EditorUtility.DisplayDialog("새그먼트 카드 삭제", "에셋 삭제에 실패했습니다. 카탈로그 참조는 제거되었습니다.", "확인");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshRows();
            WriteToolLog("Delete Segment Card", log);
        }

        private void RefreshRows()
        {
            rows.Clear();
            CollectStatCardRows();
            CollectWeaponCardRows();
            rows.Sort(CompareRows);
        }

        private void CollectStatCardRows()
        {
            if (CollectStatDefinitionRows())
            {
                return; // 카탈로그/데이터 에셋이 있으면 프리팹 fallback 대신 데이터 행 사용
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { DefaultStatCardFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                StatUpgrade statUpgrade = prefab.GetComponentInChildren<StatUpgrade>(true);
                if (statUpgrade == null)
                {
                    continue;
                }

                AddStatRow(prefab, statUpgrade, path, "무기 공격력", "damageMultiplierBonus", "배율");
                AddStatRow(prefab, statUpgrade, path, "밀리 공격력", "meleeDamageMultiplierBonus", "배율");
                AddStatRow(prefab, statUpgrade, path, "마법 공격력", "magicDamageMultiplierBonus", "배율");
                AddStatRow(prefab, statUpgrade, path, "쿨타임 감소", "attackSpeedMultiplierBonus", "배율");
                AddStatRow(prefab, statUpgrade, path, "핸들링", "turnSpeedBonus", "수치");
                AddStatRow(prefab, statUpgrade, path, "충돌힘", "collisionForceBonus", "배율");
                AddStatRow(prefab, statUpgrade, path, "재결합 범위", "rejoinRangeBonus", "미터");
                AddStatRow(prefab, statUpgrade, path, "넥서스 체력", "nexusHealthBonus", "체력");
            }
        }

        private bool CollectStatDefinitionRows()
        {
            if (statUpgradeCatalog != null)
            {
                if (statUpgradeCatalog.Cards == null)
                {
                    return true; // 카탈로그가 기준이면 비어 있어도 프리팹 fallback 금지
                }

                for (int i = 0; i < statUpgradeCatalog.Cards.Length; i++)
                {
                    StatUpgradeDefinition definition = statUpgradeCatalog.Cards[i];
                    if (definition == null)
                    {
                        continue;
                    }

                    AddStatDefinitionRows(definition, AssetDatabase.GetAssetPath(definition));
                }

                return true;
            }

            string[] guids = AssetDatabase.FindAssets("t:StatUpgradeDefinition");
            bool addedAny = false;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                StatUpgradeDefinition definition = AssetDatabase.LoadAssetAtPath<StatUpgradeDefinition>(path);
                if (definition == null)
                {
                    continue;
                }

                addedAny |= AddStatDefinitionRows(definition, path);
            }

            return addedAny;
        }

        private bool AddStatDefinitionRows(StatUpgradeDefinition definition, string path)
        {
            bool addedAny = false;
            addedAny |= AddStatDefinitionRow(definition, path, "무기 공격력", "DamageMultiplierBonus", "배율");
            addedAny |= AddStatDefinitionRow(definition, path, "밀리 공격력", "MeleeDamageMultiplierBonus", "배율");
            addedAny |= AddStatDefinitionRow(definition, path, "마법 공격력", "MagicDamageMultiplierBonus", "배율");
            addedAny |= AddStatDefinitionRow(definition, path, "쿨타임 감소", "AttackSpeedMultiplierBonus", "배율");
            addedAny |= AddStatDefinitionRow(definition, path, "핸들링", "TurnSpeedBonus", "수치");
            addedAny |= AddStatDefinitionRow(definition, path, "충돌힘", "CollisionForceBonus", "배율");
            addedAny |= AddStatDefinitionRow(definition, path, "재결합 범위", "RejoinRangeBonus", "미터");
            addedAny |= AddStatDefinitionRow(definition, path, "넥서스 체력", "NexusHealthBonus", "체력");
            return addedAny;
        }

        private void AddStatRow(GameObject prefab, StatUpgrade statUpgrade, string path, string statLabel, string propertyName, string modeLabel)
        {
            SerializedObject serialized = new SerializedObject(statUpgrade);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || Mathf.Abs(property.floatValue) <= 0.0001f)
            {
                return;
            }

            rows.Add(new StatBalanceRow(prefab, statUpgrade, path, statLabel, propertyName, property.floatValue, modeLabel));
        }

        private bool AddStatDefinitionRow(StatUpgradeDefinition definition, string path, string statLabel, string propertyName, string modeLabel)
        {
            SerializedObject serialized = new SerializedObject(definition);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || Mathf.Abs(property.floatValue) <= 0.0001f)
            {
                return false;
            }

            rows.Add(new StatDefinitionBalanceRow(definition, path, statLabel, propertyName, property.floatValue, modeLabel));
            return true;
        }

        private void CollectWeaponCardRows()
        {
            if (weaponCatalog == null)
            {
                return;
            }

            HashSet<int> visited = new HashSet<int>();
            AppendWeaponRows("SG01_Cannon", weaponCatalog.CannonEnhancements, visited);
            AppendWeaponRows("SG02_Missile", weaponCatalog.MissileEnhancements, visited);

            if (weaponCatalog.AdditionalSegments == null)
            {
                return;
            }

            for (int i = 0; i < weaponCatalog.AdditionalSegments.Length; i++)
            {
                WeaponSegmentEnhancementGroup group = weaponCatalog.AdditionalSegments[i];
                string groupId = string.IsNullOrWhiteSpace(group.SegmentId) ? $"추가 세그먼트 {i + 1}" : group.SegmentId.Trim();
                AppendWeaponRows(groupId, group.Enhancements, visited);
            }
        }

        private void AppendWeaponRows(string groupLabel, WeaponDefinition[] definitions, HashSet<int> visited)
        {
            if (definitions == null)
            {
                return;
            }

            for (int i = 0; i < definitions.Length; i++)
            {
                WeaponDefinition definition = definitions[i];
                if (definition == null || !visited.Add(definition.GetInstanceID()))
                {
                    continue;
                }

                AddWeaponRows(groupLabel, definition, AssetDatabase.GetAssetPath(definition));
            }
        }

        private void AddWeaponRows(string groupLabel, WeaponDefinition definition, string path)
        {
            if (definition == null)
            {
                return;
            }

            WeaponCardBalanceRow row = new WeaponCardBalanceRow(groupLabel, definition, path);
            if (row.HasVisibleLines)
            {
                rows.Add(row);
            }
        }

        private void AddWeaponFloatRow(
            string groupLabel,
            WeaponDefinition definition,
            string path,
            WeaponStatKind statKind)
        {
            WeaponStatInfo info = GetWeaponStatInfo(statKind);
            if (info.ValueKind != StatValueKind.Float)
            {
                return;
            }

            float normal = GetFloat(definition, info.NormalName);
            float rare = GetFloat(definition, info.RareName);
            float unique = GetFloat(definition, info.UniqueName);
            if (!HasAny(normal, rare, unique))
            {
                return;
            }

            rows.Add(new WeaponFloatBalanceRow(
                groupLabel,
                definition,
                path,
                statKind,
                normal,
                rare,
                unique,
                info));
        }

        private void AddWeaponIntRow(
            string groupLabel,
            WeaponDefinition definition,
            string path,
            WeaponStatKind statKind)
        {
            WeaponStatInfo info = GetWeaponStatInfo(statKind);
            if (info.ValueKind != StatValueKind.Int)
            {
                return;
            }

            int normal = GetInt(definition, info.NormalName);
            int rare = GetInt(definition, info.RareName);
            int unique = GetInt(definition, info.UniqueName);
            if (normal == 0 && rare == 0 && unique == 0)
            {
                return;
            }

            rows.Add(new WeaponIntBalanceRow(groupLabel, definition, path, statKind, normal, rare, unique, info));
        }

        private void ApplyRows(string action, List<BalanceRow> dirtyRows)
        {
            if (dirtyRows == null || dirtyRows.Count == 0)
            {
                return;
            }

            List<RowLogEntry> logEntries = new List<RowLogEntry>(dirtyRows.Count);
            HashSet<UnityEngine.Object> dirtyObjects = new HashSet<UnityEngine.Object>();
            HashSet<GameObject> dirtyPrefabs = new HashSet<GameObject>();

            for (int i = 0; i < dirtyRows.Count; i++)
            {
                BalanceRow row = dirtyRows[i];
                if (!row.IsDirty || row.ApplyTargetObject == null)
                {
                    continue;
                }

                Undo.RecordObject(row.ApplyTargetObject, "Apply Card Balance Table");
                logEntries.Add(RowLogEntry.Create(row));
                row.ApplyWorking();
                row.AcceptWorkingAsOriginal();
                row.EndEdit();
                dirtyObjects.Add(row.ApplyTargetObject);
                if (row.StatPrefab != null)
                {
                    dirtyPrefabs.Add(row.StatPrefab);
                }
            }

            foreach (UnityEngine.Object dirtyObject in dirtyObjects)
            {
                EditorUtility.SetDirty(dirtyObject);
            }

            foreach (GameObject prefab in dirtyPrefabs)
            {
                EditorUtility.SetDirty(prefab);
                PrefabUtility.SavePrefabAsset(prefab);
            }

            AssetDatabase.SaveAssets();
            WriteApplyLog(action, logEntries);
        }

        private void ResetRows(string title, List<BalanceRow> targetRows)
        {
            if (CountDirtyRows(targetRows) == 0)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog(title, "적용하지 않은 변경값을 원본값으로 되돌릴까요?", "리셋", "취소"))
            {
                return;
            }

            for (int i = 0; i < targetRows.Count; i++)
            {
                targetRows[i].ResetWorking();
                targetRows[i].EndEdit();
            }
        }

        private void WriteApplyLog(string action, List<RowLogEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            string directory = GetLogDirectory();
            Directory.CreateDirectory(directory);

            DateTime now = DateTime.Now;
            string fileName = $"CardBalance_{now:yyyy-MM-dd_HH-mm-ss-fff}.log";
            string path = Path.Combine(directory, fileName);

            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine($"[{now:yyyy-MM-dd HH:mm:ss}]");
            builder.AppendLine($"Action: {action}");
            builder.AppendLine($"ChangedRows: {entries.Count}");
            builder.AppendLine();

            for (int i = 0; i < entries.Count; i++)
            {
                RowLogEntry entry = entries[i];
                builder.AppendLine($"Kind: {entry.KindLabel}");
                builder.AppendLine($"Target: {entry.TargetLabel}");
                builder.AppendLine($"Card: {entry.CardName}");
                builder.AppendLine($"Asset: {entry.AssetPath}");
                builder.AppendLine($"Stat: {entry.StatLabel}");
                builder.AppendLine($"Normal: {entry.BeforeNormal} -> {entry.AfterNormal}");
                builder.AppendLine($"Rare: {entry.BeforeRare} -> {entry.AfterRare}");
                builder.AppendLine($"Unique: {entry.BeforeUnique} -> {entry.AfterUnique}");
                if (!string.Equals(entry.BeforeMode, entry.AfterMode, StringComparison.Ordinal))
                {
                    builder.AppendLine($"Mode: {entry.BeforeMode} -> {entry.AfterMode}");
                }
                builder.AppendLine();
            }

            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
            Debug.Log($"[CardBalanceTable] Balance log written: {path}");
        }

        private List<BalanceRow> GetVisibleRows()
        {
            List<BalanceRow> result = new List<BalanceRow>();
            for (int i = 0; i < rows.Count; i++)
            {
                BalanceRow row = rows[i];
                if (ShouldShowRow(row))
                {
                    result.Add(row);
                }
            }

            return result;
        }

        private bool ShouldShowRow(BalanceRow row)
        {
            if (row == null)
            {
                return false;
            }

            if (row.Kind == CardRowKind.Stat && !showStatCards)
            {
                return false;
            }

            if (row.Kind == CardRowKind.Weapon && !showWeaponCards)
            {
                return false;
            }

            if (showDirtyOnly && !row.IsDirty)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            string needle = searchText.Trim();
            return Contains(row.KindLabel, needle)
                || Contains(row.TargetLabel, needle)
                || Contains(row.CardName, needle)
                || Contains(row.AssetName, needle)
                || Contains(row.DescriptionText, needle)
                || Contains(row.StatLabel, needle)
                || Contains(row.ModeLabel, needle);
        }

        private int CountVisibleRows(List<BalanceRow> sourceRows)
        {
            int count = 0;
            for (int i = 0; i < sourceRows.Count; i++)
            {
                if (ShouldShowRow(sourceRows[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountDirtyRows(List<BalanceRow> sourceRows)
        {
            int count = 0;
            for (int i = 0; i < sourceRows.Count; i++)
            {
                if (sourceRows[i].IsDirty)
                {
                    count++;
                }
            }

            return count;
        }

        private static List<BalanceRow> CollectDirtyRows(List<BalanceRow> sourceRows)
        {
            List<BalanceRow> result = new List<BalanceRow>();
            for (int i = 0; i < sourceRows.Count; i++)
            {
                if (sourceRows[i].IsDirty)
                {
                    result.Add(sourceRows[i]);
                }
            }

            return result;
        }

        private static WeaponStatKind DrawWeaponStatPopup(string label, WeaponStatKind current)
        {
            return DrawWeaponStatPopup(new GUIContent(label), current, 0f);
        }

        private static WeaponStatKind DrawWeaponStatPopup(GUIContent label, WeaponStatKind current, float width)
        {
            return DrawWeaponStatPopup(label, current, width, GetWeaponStatKinds());
        }

        private static WeaponStatKind DrawWeaponStatPopup(GUIContent label, WeaponStatKind current, float width, WeaponStatKind[] values)
        {
            if (values == null || values.Length == 0)
            {
                values = GetWeaponStatKinds();
            }

            string[] labels = new string[values.Length];
            int selected = 0;
            for (int i = 0; i < values.Length; i++)
            {
                WeaponStatInfo info = GetWeaponStatInfo(values[i]);
                labels[i] = info.Label;
                if (values[i] == current)
                {
                    selected = i;
                }
            }

            if (width > 0f)
            {
                selected = EditorGUILayout.Popup(label, selected, labels, GUILayout.Width(width));
            }
            else
            {
                selected = EditorGUILayout.Popup(label, selected, labels);
            }

            return values[Mathf.Clamp(selected, 0, values.Length - 1)];
        }

        private static void ApplyWeaponStatValues(WeaponDefinition definition, WeaponStatInfo info, SegmentDraftRow draft)
        {
            if (definition == null || draft == null)
            {
                return;
            }

            float normal = Mathf.Max(0f, draft.NormalValue);
            float rare = Mathf.Max(0f, draft.RareValue);
            float unique = Mathf.Max(0f, draft.UniqueValue);

            if (info.ValueKind == StatValueKind.Int)
            {
                SetInt(definition, info.NormalName, Mathf.RoundToInt(normal));
                SetInt(definition, info.RareName, Mathf.RoundToInt(rare));
                SetInt(definition, info.UniqueName, Mathf.RoundToInt(unique));
            }
            else
            {
                SetFloat(definition, info.NormalName, normal);
                SetFloat(definition, info.RareName, rare);
                SetFloat(definition, info.UniqueName, unique);
            }

            if (info.HasPercentMode)
            {
                SetBool(definition, info.NormalPercentName, draft.NormalUsePercent);
                SetBool(definition, info.RarePercentName, draft.RareUsePercent);
                SetBool(definition, info.UniquePercentName, draft.UniqueUsePercent);
            }
        }

        private void CopySegmentIconSettings(string segmentId, WeaponDefinition definition)
        {
            if (segmentCatalog == null || definition == null || string.IsNullOrWhiteSpace(segmentId))
            {
                return;
            }

            if (!segmentCatalog.TryFind(segmentId, out SegmentDefinition segmentDefinition) || segmentDefinition == null)
            {
                return;
            }

            definition.CardIconSizeOffset = segmentDefinition.CardIconSizeOffset;
            if (segmentDefinition.CardIconSpritesPerLevel == null || segmentDefinition.CardIconSpritesPerLevel.Length == 0)
            {
                return;
            }

            definition.CardIconSpritesPerLevel = (Sprite[])segmentDefinition.CardIconSpritesPerLevel.Clone();
            for (int i = 0; i < definition.CardIconSpritesPerLevel.Length; i++)
            {
                if (definition.CardIconSpritesPerLevel[i] != null)
                {
                    definition.CardIconSprite = definition.CardIconSpritesPerLevel[i];
                    break;
                }
            }
        }

        private void AddWeaponDefinitionToCatalog(string segmentId, WeaponDefinition definition)
        {
            if (weaponCatalog == null || definition == null || string.IsNullOrWhiteSpace(segmentId))
            {
                return;
            }

            string normalized = segmentId.Trim();
            Undo.RecordObject(weaponCatalog, "Add Weapon Enhancement Card");

            if (string.Equals(normalized, "SG01_Cannon", StringComparison.OrdinalIgnoreCase))
            {
                weaponCatalog.CannonEnhancements = AppendUnique(weaponCatalog.CannonEnhancements, definition);
            }
            else if (string.Equals(normalized, "SG02_Missile", StringComparison.OrdinalIgnoreCase))
            {
                weaponCatalog.MissileEnhancements = AppendUnique(weaponCatalog.MissileEnhancements, definition);
            }
            else
            {
                AddWeaponDefinitionToAdditionalSegment(normalized, definition);
            }

            EditorUtility.SetDirty(weaponCatalog);
        }

        private void AddWeaponDefinitionToAdditionalSegment(string segmentId, WeaponDefinition definition)
        {
            WeaponSegmentEnhancementGroup[] groups = weaponCatalog.AdditionalSegments ?? Array.Empty<WeaponSegmentEnhancementGroup>();
            for (int i = 0; i < groups.Length; i++)
            {
                if (string.Equals(groups[i].NormalizedSegmentId, segmentId, StringComparison.OrdinalIgnoreCase))
                {
                    groups[i].SegmentId = segmentId;
                    groups[i].Enhancements = AppendUnique(groups[i].Enhancements, definition);
                    weaponCatalog.AdditionalSegments = groups;
                    return;
                }
            }

            Array.Resize(ref groups, groups.Length + 1);
            groups[groups.Length - 1] = new WeaponSegmentEnhancementGroup
            {
                SegmentId = segmentId,
                Enhancements = new[] { definition }
            };
            weaponCatalog.AdditionalSegments = groups;
        }

        private void RemoveWeaponDefinitionFromCatalog(WeaponDefinition definition)
        {
            if (weaponCatalog == null || definition == null)
            {
                return;
            }

            weaponCatalog.CannonEnhancements = RemoveDefinition(weaponCatalog.CannonEnhancements, definition);
            weaponCatalog.MissileEnhancements = RemoveDefinition(weaponCatalog.MissileEnhancements, definition);

            if (weaponCatalog.AdditionalSegments == null)
            {
                return;
            }

            WeaponSegmentEnhancementGroup[] groups = weaponCatalog.AdditionalSegments;
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i].Enhancements = RemoveDefinition(groups[i].Enhancements, definition);
            }

            weaponCatalog.AdditionalSegments = groups;
        }

        private static WeaponDefinition[] AppendUnique(WeaponDefinition[] source, WeaponDefinition definition)
        {
            if (definition == null)
            {
                return source ?? Array.Empty<WeaponDefinition>();
            }

            source ??= Array.Empty<WeaponDefinition>();
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == definition)
                {
                    return source;
                }
            }

            Array.Resize(ref source, source.Length + 1);
            source[source.Length - 1] = definition;
            return source;
        }

        private static WeaponDefinition[] RemoveDefinition(WeaponDefinition[] source, WeaponDefinition definition)
        {
            if (source == null || source.Length == 0 || definition == null)
            {
                return source ?? Array.Empty<WeaponDefinition>();
            }

            List<WeaponDefinition> result = new List<WeaponDefinition>(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != definition)
                {
                    result.Add(source[i]);
                }
            }

            return result.ToArray();
        }

        private static string MakeSafeFileName(string source, string fallback)
        {
            string value = string.IsNullOrWhiteSpace(source) ? fallback : source.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
            {
                value = value.Replace(invalid[i], '_');
            }

            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string BuildDefaultWeaponAssetName(string segmentId, WeaponStatInfo info)
        {
            string normalizedSegment = BuildWeaponAssetSegmentName(segmentId);
            return MakeSafeFileName($"WE_{normalizedSegment}_{info.AssetName}", "WE_NewWeaponCard");
        }

        private static void ApplyDraftStatSuggestion(SegmentDraftRow draft, WeaponStatInfo info, bool replaceDescription)
        {
            if (draft == null)
            {
                return;
            }

            draft.AssetName = BuildDefaultWeaponAssetName(draft.SegmentId, info);
            if (replaceDescription)
            {
                draft.Description = BuildDefaultWeaponDescriptionLine(info.Kind);
            }
        }

        private static bool ShouldReplaceDraftDescription(string description, WeaponStatInfo previousInfo)
        {
            string trimmed = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return true;
            }

            return string.Equals(trimmed, previousInfo.Label, StringComparison.Ordinal)
                || string.Equals(trimmed, BuildDefaultWeaponDescriptionLine(previousInfo.Kind), StringComparison.Ordinal);
        }

        private string BuildDefaultWeaponCardName(string segmentId)
        {
            string displayName = GetSegmentDisplayName(segmentId);
            if (string.IsNullOrWhiteSpace(displayName) || string.Equals(displayName, segmentId, StringComparison.OrdinalIgnoreCase))
            {
                displayName = BuildWeaponAssetSegmentName(segmentId);
            }

            return $"{displayName.Trim()}강화";
        }

        private static string BuildWeaponAssetSegmentName(string segmentId)
        {
            if (string.IsNullOrWhiteSpace(segmentId))
            {
                return "Segment";
            }

            string normalized = segmentId.Trim();
            int underscoreIndex = normalized.IndexOf('_');
            if (underscoreIndex >= 0 && underscoreIndex < normalized.Length - 1 && IsSegmentNumericPrefix(normalized, underscoreIndex))
            {
                return normalized.Substring(underscoreIndex + 1);
            }

            return normalized;
        }

        private static bool IsSegmentNumericPrefix(string value, int underscoreIndex)
        {
            if (string.IsNullOrWhiteSpace(value) || underscoreIndex < 3 || value[0] != 'S' || value[1] != 'G')
            {
                return false;
            }

            for (int i = 2; i < underscoreIndex; i++)
            {
                if (!char.IsDigit(value[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static void WriteToolLog(string action, string body)
        {
            string directory = GetLogDirectory();
            Directory.CreateDirectory(directory);
            DateTime now = DateTime.Now;
            string path = Path.Combine(directory, $"CardBalance_{now:yyyy-MM-dd_HH-mm-ss-fff}.log");
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"[{now:yyyy-MM-dd HH:mm:ss}]");
            builder.AppendLine($"Action: {action}");
            builder.AppendLine(body ?? string.Empty);
            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
            Debug.Log($"[CardBalanceTable] Balance log written: {path}");
        }

        private static void DrawChangedRow(Rect rect, bool changed)
        {
            if (changed && Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, ChangedRowColor);
            }
        }

        private static void Header(string label, float width, string tooltip)
        {
            GUILayout.Label(new GUIContent(label, tooltip), EditorStyles.toolbarButton, GUILayout.Width(width));
        }

        private static void DrawPingButton(string label, UnityEngine.Object target, float width)
        {
            GUI.enabled = target != null;
            if (GUILayout.Button(string.IsNullOrWhiteSpace(label) ? "None" : label, EditorStyles.objectField, GUILayout.Width(width)))
            {
                EditorGUIUtility.PingObject(target);
                Selection.activeObject = target;
            }
            GUI.enabled = true;
        }

        private static int CompareRows(BalanceRow a, BalanceRow b)
        {
            int kind = a.Kind.CompareTo(b.Kind);
            if (kind != 0)
            {
                return kind;
            }

            int target = string.Compare(a.TargetLabel, b.TargetLabel, StringComparison.OrdinalIgnoreCase);
            if (target != 0)
            {
                return target;
            }

            int card = string.Compare(a.CardName, b.CardName, StringComparison.OrdinalIgnoreCase);
            return card != 0 ? card : string.Compare(a.StatLabel, b.StatLabel, StringComparison.OrdinalIgnoreCase);
        }

        private static bool Contains(string source, string needle)
        {
            return !string.IsNullOrWhiteSpace(source)
                && source.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasAny(float normal, float rare, float unique)
        {
            return Mathf.Abs(normal) > 0.0001f || Mathf.Abs(rare) > 0.0001f || Mathf.Abs(unique) > 0.0001f;
        }

        private static bool HasAnyInt(int normal, int rare, int unique)
        {
            return normal != 0 || rare != 0 || unique != 0;
        }

        private static bool ContainsWeaponStatKind(WeaponStatKind[] values, WeaponStatKind target)
        {
            if (values == null)
            {
                return false;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == target)
                {
                    return true;
                }
            }

            return false;
        }

        private static WeaponStatKind[] EnsureWeaponStatKindOption(WeaponStatKind[] source, WeaponStatKind current)
        {
            if (ContainsWeaponStatKind(source, current))
            {
                return source;
            }

            source ??= Array.Empty<WeaponStatKind>();
            WeaponStatKind[] result = new WeaponStatKind[source.Length + 1];
            result[0] = current;
            Array.Copy(source, 0, result, 1, source.Length);
            return result;
        }

        private static WeaponStatKind[] FilterWeaponStatKindsByValueKind(WeaponStatKind[] source, StatValueKind valueKind)
        {
            source ??= GetWeaponStatKinds();
            List<WeaponStatKind> result = new List<WeaponStatKind>(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                WeaponStatInfo info = GetWeaponStatInfo(source[i]);
                if (info.ValueKind == valueKind)
                {
                    result.Add(source[i]);
                }
            }

            return result.Count > 0 ? result.ToArray() : GetWeaponStatKinds();
        }

        private static bool DrawPercentModePopup(bool current, float width)
        {
            int selected = current ? 1 : 0;
            selected = EditorGUILayout.Popup(selected, PercentModeLabels, GUILayout.Width(width));
            return selected == 1;
        }

        private static string GetPercentModeLabel(bool usePercent)
        {
            return usePercent ? "%" : "숫자";
        }

        private static void DrawReadOnlyTierValue(string valueText, string modeText)
        {
            using (new GUILayout.HorizontalScope(GUILayout.Width(SegmentTierValueWidth)))
            {
                GUILayout.Label(valueText, GUILayout.Width(SegmentTierNumberWidth));
                GUILayout.Label(modeText, GUILayout.Width(TierModeWidth));
            }
        }

        private static float DrawEditableFloatTierValue(float value, bool hasPercentMode, string modeLabel, ref bool usePercent)
        {
            using (new GUILayout.HorizontalScope(GUILayout.Width(SegmentTierValueWidth)))
            {
                float nextValue = EditorGUILayout.FloatField(value, GUILayout.Width(SegmentTierNumberWidth));
                if (hasPercentMode)
                {
                    usePercent = DrawPercentModePopup(usePercent, TierModeWidth);
                }
                else
                {
                    usePercent = false;
                    GUILayout.Label(modeLabel, GUILayout.Width(TierModeWidth));
                }

                return nextValue;
            }
        }

        private static int DrawEditableIntTierValue(int value, string modeLabel)
        {
            using (new GUILayout.HorizontalScope(GUILayout.Width(SegmentTierValueWidth)))
            {
                int nextValue = EditorGUILayout.IntField(value, GUILayout.Width(SegmentTierNumberWidth));
                GUILayout.Label(modeLabel, GUILayout.Width(TierModeWidth));
                return nextValue;
            }
        }

        private static float GetFloat(WeaponDefinition definition, string fieldName)
        {
            return (float)typeof(WeaponDefinition).GetField(fieldName).GetValue(definition);
        }

        private static int GetInt(WeaponDefinition definition, string fieldName)
        {
            return (int)typeof(WeaponDefinition).GetField(fieldName).GetValue(definition);
        }

        private static bool GetBool(WeaponDefinition definition, string fieldName)
        {
            return !string.IsNullOrWhiteSpace(fieldName) && (bool)typeof(WeaponDefinition).GetField(fieldName).GetValue(definition);
        }

        private static void SetFloat(WeaponDefinition definition, string fieldName, float value)
        {
            typeof(WeaponDefinition).GetField(fieldName).SetValue(definition, value);
        }

        private static void SetInt(WeaponDefinition definition, string fieldName, int value)
        {
            typeof(WeaponDefinition).GetField(fieldName).SetValue(definition, value);
        }

        private static void SetBool(WeaponDefinition definition, string fieldName, bool value)
        {
            if (!string.IsNullOrWhiteSpace(fieldName))
            {
                typeof(WeaponDefinition).GetField(fieldName).SetValue(definition, value);
            }
        }

        private static void ClearWeaponStatValues(WeaponDefinition definition, WeaponStatInfo info)
        {
            if (definition == null)
            {
                return;
            }

            if (info.ValueKind == StatValueKind.Int)
            {
                SetInt(definition, info.NormalName, 0);
                SetInt(definition, info.RareName, 0);
                SetInt(definition, info.UniqueName, 0);
            }
            else
            {
                SetFloat(definition, info.NormalName, 0f);
                SetFloat(definition, info.RareName, 0f);
                SetFloat(definition, info.UniqueName, 0f);
            }

            if (info.HasPercentMode)
            {
                SetBool(definition, info.NormalPercentName, false);
                SetBool(definition, info.RarePercentName, false);
                SetBool(definition, info.UniquePercentName, false);
            }
        }

        private static void SetWeaponFloatStatValues(
            WeaponDefinition definition,
            WeaponStatInfo info,
            float normal,
            float rare,
            float unique,
            bool normalUsePercent,
            bool rareUsePercent,
            bool uniqueUsePercent)
        {
            if (definition == null)
            {
                return;
            }

            SetFloat(definition, info.NormalName, Mathf.Max(0f, normal));
            SetFloat(definition, info.RareName, Mathf.Max(0f, rare));
            SetFloat(definition, info.UniqueName, Mathf.Max(0f, unique));
            if (info.HasPercentMode)
            {
                SetBool(definition, info.NormalPercentName, normalUsePercent);
                SetBool(definition, info.RarePercentName, rareUsePercent);
                SetBool(definition, info.UniquePercentName, uniqueUsePercent);
            }
        }

        private static void SetWeaponIntStatValues(WeaponDefinition definition, WeaponStatInfo info, int normal, int rare, int unique)
        {
            if (definition == null)
            {
                return;
            }

            SetInt(definition, info.NormalName, Mathf.Max(0, normal));
            SetInt(definition, info.RareName, Mathf.Max(0, rare));
            SetInt(definition, info.UniqueName, Mathf.Max(0, unique));
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string GetLogDirectory()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string workspaceRoot = Directory.GetParent(projectRoot)?.FullName ?? projectRoot;
            return Path.Combine(workspaceRoot, "OZCodingProject 개인파일", "Logs", "CardBalance");
        }

        private static WeaponStatKind[] GetWeaponStatKinds()
        {
            return new[]
            {
                WeaponStatKind.BaseDamage,
                WeaponStatKind.SawPierceDamageRatio,
                WeaponStatKind.ProjectileSpeed,
                WeaponStatKind.SearchRange,
                WeaponStatKind.MaxChainDepth,
                WeaponStatKind.ChainRange,
                WeaponStatKind.ChainDamageFalloff,
                WeaponStatKind.ProjectileCount,
                WeaponStatKind.CooldownReduction,
                WeaponStatKind.SideConeAngle,
                WeaponStatKind.LaserDuration,
                WeaponStatKind.LaserTickInterval,
                WeaponStatKind.LandingRollDistance,
                WeaponStatKind.LandingRollDuration,
                WeaponStatKind.PierceCount,
                WeaponStatKind.ExplosionRadius
            };
        }

        private static WeaponStatInfo GetWeaponStatInfo(WeaponStatKind kind)
        {
            switch (kind)
            {
                case WeaponStatKind.SawPierceDamageRatio:
                    return WeaponStatInfo.Float(kind, "관통피해율", "PierceDamageRatio", nameof(WeaponDefinition.SawPierceDamageRatio), nameof(WeaponDefinition.SawPierceDamageRatioRare), nameof(WeaponDefinition.SawPierceDamageRatioUnique), "비율");
                case WeaponStatKind.ProjectileSpeed:
                    return WeaponStatInfo.FloatPercent(kind, "투사체속도", "ProjectileSpeed", nameof(WeaponDefinition.ProjectileSpeed), nameof(WeaponDefinition.ProjectileSpeedRare), nameof(WeaponDefinition.ProjectileSpeedUnique), nameof(WeaponDefinition.ProjectileSpeedUsePercent), nameof(WeaponDefinition.ProjectileSpeedUsePercentRare), nameof(WeaponDefinition.ProjectileSpeedUsePercentUnique));
                case WeaponStatKind.SearchRange:
                    return WeaponStatInfo.FloatPercent(kind, "사거리", "SearchRange", nameof(WeaponDefinition.SearchRange), nameof(WeaponDefinition.SearchRangeRare), nameof(WeaponDefinition.SearchRangeUnique), nameof(WeaponDefinition.SearchRangeUsePercent), nameof(WeaponDefinition.SearchRangeUsePercentRare), nameof(WeaponDefinition.SearchRangeUsePercentUnique));
                case WeaponStatKind.MaxChainDepth:
                    return WeaponStatInfo.Int(kind, "연쇄단계", "MaxChainDepth", nameof(WeaponDefinition.MaxChainDepth), nameof(WeaponDefinition.MaxChainDepthRare), nameof(WeaponDefinition.MaxChainDepthUnique));
                case WeaponStatKind.ChainRange:
                    return WeaponStatInfo.FloatPercent(kind, "연쇄거리", "ChainRange", nameof(WeaponDefinition.ChainRange), nameof(WeaponDefinition.ChainRangeRare), nameof(WeaponDefinition.ChainRangeUnique), nameof(WeaponDefinition.ChainRangeUsePercent), nameof(WeaponDefinition.ChainRangeUsePercentRare), nameof(WeaponDefinition.ChainRangeUsePercentUnique));
                case WeaponStatKind.ChainDamageFalloff:
                    return WeaponStatInfo.Float(kind, "체인피해유지", "ChainDamageFalloff", nameof(WeaponDefinition.ChainDamageFalloff), nameof(WeaponDefinition.ChainDamageFalloffRare), nameof(WeaponDefinition.ChainDamageFalloffUnique), "비율");
                case WeaponStatKind.ProjectileCount:
                    return WeaponStatInfo.Int(kind, "발사수", "ProjectileCount", nameof(WeaponDefinition.ProjectileCount), nameof(WeaponDefinition.ProjectileCountRare), nameof(WeaponDefinition.ProjectileCountUnique));
                case WeaponStatKind.CooldownReduction:
                    return WeaponStatInfo.Float(kind, "쿨타임감소", "CooldownReduction", nameof(WeaponDefinition.CooldownReduction), nameof(WeaponDefinition.CooldownReductionRare), nameof(WeaponDefinition.CooldownReductionUnique), "비율");
                case WeaponStatKind.SideConeAngle:
                    return WeaponStatInfo.Float(kind, "부채꼴각도", "SideConeAngle", nameof(WeaponDefinition.SideConeAngle), nameof(WeaponDefinition.SideConeAngleRare), nameof(WeaponDefinition.SideConeAngleUnique), "도");
                case WeaponStatKind.LaserDuration:
                    return WeaponStatInfo.FloatPercent(kind, "지속시간", "LaserDuration", nameof(WeaponDefinition.LaserDuration), nameof(WeaponDefinition.LaserDurationRare), nameof(WeaponDefinition.LaserDurationUnique), nameof(WeaponDefinition.LaserDurationUsePercent), nameof(WeaponDefinition.LaserDurationUsePercentRare), nameof(WeaponDefinition.LaserDurationUsePercentUnique));
                case WeaponStatKind.LaserTickInterval:
                    return WeaponStatInfo.Float(kind, "틱간격감소", "LaserTickInterval", nameof(WeaponDefinition.LaserTickInterval), nameof(WeaponDefinition.LaserTickIntervalRare), nameof(WeaponDefinition.LaserTickIntervalUnique), "비율");
                case WeaponStatKind.LandingRollDistance:
                    return WeaponStatInfo.FloatPercent(kind, "구르기거리", "LandingRollDistance", nameof(WeaponDefinition.LandingRollDistance), nameof(WeaponDefinition.LandingRollDistanceRare), nameof(WeaponDefinition.LandingRollDistanceUnique), nameof(WeaponDefinition.LandingRollDistanceUsePercent), nameof(WeaponDefinition.LandingRollDistanceUsePercentRare), nameof(WeaponDefinition.LandingRollDistanceUsePercentUnique));
                case WeaponStatKind.LandingRollDuration:
                    return WeaponStatInfo.FloatPercent(kind, "구르기시간", "LandingRollDuration", nameof(WeaponDefinition.LandingRollDuration), nameof(WeaponDefinition.LandingRollDurationRare), nameof(WeaponDefinition.LandingRollDurationUnique), nameof(WeaponDefinition.LandingRollDurationUsePercent), nameof(WeaponDefinition.LandingRollDurationUsePercentRare), nameof(WeaponDefinition.LandingRollDurationUsePercentUnique));
                case WeaponStatKind.PierceCount:
                    return WeaponStatInfo.Int(kind, "관통수", "PierceCount", nameof(WeaponDefinition.PierceCount), nameof(WeaponDefinition.PierceCountRare), nameof(WeaponDefinition.PierceCountUnique));
                case WeaponStatKind.ExplosionRadius:
                    return WeaponStatInfo.FloatPercent(kind, "폭발반경", "ExplosionRadius", nameof(WeaponDefinition.ExplosionRadius), nameof(WeaponDefinition.ExplosionRadiusRare), nameof(WeaponDefinition.ExplosionRadiusUnique), nameof(WeaponDefinition.ExplosionRadiusUsePercent), nameof(WeaponDefinition.ExplosionRadiusUsePercentRare), nameof(WeaponDefinition.ExplosionRadiusUsePercentUnique));
                default:
                    return WeaponStatInfo.FloatPercent(WeaponStatKind.BaseDamage, "공격력", "BaseDamage", nameof(WeaponDefinition.BaseDamage), nameof(WeaponDefinition.BaseDamageRare), nameof(WeaponDefinition.BaseDamageUnique), nameof(WeaponDefinition.BaseDamageUsePercent), nameof(WeaponDefinition.BaseDamageUsePercentRare), nameof(WeaponDefinition.BaseDamageUsePercentUnique));
            }
        }

        private static string BuildDefaultWeaponDescriptionLine(WeaponStatKind kind)
        {
            switch (kind)
            {
                case WeaponStatKind.SawPierceDamageRatio:
                    return "관통피해율 (N) 증가";
                case WeaponStatKind.ProjectileSpeed:
                    return "투사체 속도 (N) 증가";
                case WeaponStatKind.SearchRange:
                    return "사거리 (N) 증가";
                case WeaponStatKind.MaxChainDepth:
                    return "연쇄 단계 (N) 증가";
                case WeaponStatKind.ChainRange:
                    return "연쇄 거리 (N) 증가";
                case WeaponStatKind.ChainDamageFalloff:
                    return "체인 피해 유지율 (N) 증가";
                case WeaponStatKind.ProjectileCount:
                    return "발사 수 (N) 증가";
                case WeaponStatKind.CooldownReduction:
                    return "쿨타임 (N) 감소";
                case WeaponStatKind.SideConeAngle:
                    return "부채꼴 각도 (N) 증가";
                case WeaponStatKind.LaserDuration:
                    return "지속시간 (N) 증가";
                case WeaponStatKind.LaserTickInterval:
                    return "틱 간격 (N) 감소";
                case WeaponStatKind.LandingRollDistance:
                    return "구르기 거리 (N) 증가";
                case WeaponStatKind.LandingRollDuration:
                    return "구르기 시간 (N) 증가";
                case WeaponStatKind.PierceCount:
                    return "관통 수 (N) 증가";
                case WeaponStatKind.ExplosionRadius:
                    return "폭발 반경 (N) 증가";
                default:
                    return "공격력 (N) 증가";
            }
        }

        private enum WeaponStatKind
        {
            BaseDamage,
            SawPierceDamageRatio,
            ProjectileSpeed,
            SearchRange,
            MaxChainDepth,
            ChainRange,
            ChainDamageFalloff,
            ProjectileCount,
            CooldownReduction,
            SideConeAngle,
            LaserDuration,
            LaserTickInterval,
            LandingRollDistance,
            LandingRollDuration,
            PierceCount,
            ExplosionRadius
        }

        private enum StatValueKind
        {
            Float,
            Int
        }

        private sealed class SegmentDraftRow
        {
            public SegmentDraftRow(string segmentId, WeaponStatInfo defaultInfo, string cardName)
            {
                SegmentId = string.IsNullOrWhiteSpace(segmentId) ? string.Empty : segmentId.Trim();
                CardName = string.IsNullOrWhiteSpace(cardName) ? "새 강화카드" : cardName.Trim();
                AssetName = BuildDefaultWeaponAssetName(SegmentId, defaultInfo);
                Description = BuildDefaultWeaponDescriptionLine(defaultInfo.Kind);
                StatKind = defaultInfo.Kind;
                NormalValue = 0.1f;
                RareValue = 0.2f;
                UniqueValue = 0.3f;
            }

            public string SegmentId;
            public string CardName;
            public string AssetName;
            public string Description;
            public WeaponStatKind StatKind;
            public float NormalValue;
            public float RareValue;
            public float UniqueValue;
            public bool NormalUsePercent;
            public bool RareUsePercent;
            public bool UniqueUsePercent;
        }

        private readonly struct WeaponStatInfo
        {
            public readonly WeaponStatKind Kind;
            public readonly string Label;
            public readonly string AssetName;
            public readonly string NormalName;
            public readonly string RareName;
            public readonly string UniqueName;
            public readonly string NormalPercentName;
            public readonly string RarePercentName;
            public readonly string UniquePercentName;
            public readonly StatValueKind ValueKind;
            public readonly bool HasPercentMode;
            public readonly string ModeLabel;

            private WeaponStatInfo(
                WeaponStatKind kind,
                string label,
                string assetName,
                string normalName,
                string rareName,
                string uniqueName,
                string normalPercentName,
                string rarePercentName,
                string uniquePercentName,
                StatValueKind valueKind,
                bool hasPercentMode,
                string modeLabel)
            {
                Kind = kind;
                Label = label;
                AssetName = assetName;
                NormalName = normalName;
                RareName = rareName;
                UniqueName = uniqueName;
                NormalPercentName = normalPercentName;
                RarePercentName = rarePercentName;
                UniquePercentName = uniquePercentName;
                ValueKind = valueKind;
                HasPercentMode = hasPercentMode;
                ModeLabel = modeLabel;
            }

            public static WeaponStatInfo Float(WeaponStatKind kind, string label, string assetName, string normalName, string rareName, string uniqueName, string modeLabel)
            {
                return new WeaponStatInfo(kind, label, assetName, normalName, rareName, uniqueName, null, null, null, StatValueKind.Float, false, modeLabel);
            }

            public static WeaponStatInfo FloatPercent(
                WeaponStatKind kind,
                string label,
                string assetName,
                string normalName,
                string rareName,
                string uniqueName,
                string normalPercentName,
                string rarePercentName,
                string uniquePercentName)
            {
                return new WeaponStatInfo(kind, label, assetName, normalName, rareName, uniqueName, normalPercentName, rarePercentName, uniquePercentName, StatValueKind.Float, true, "고정/%");
            }

            public static WeaponStatInfo Int(WeaponStatKind kind, string label, string assetName, string normalName, string rareName, string uniqueName)
            {
                return new WeaponStatInfo(kind, label, assetName, normalName, rareName, uniqueName, null, null, null, StatValueKind.Int, false, "개수");
            }
        }

        private enum CardRowKind
        {
            Stat = 0,
            Weapon = 1
        }

        private abstract class BalanceRow
        {
            private bool isEditing;

            protected BalanceRow(CardRowKind kind, string targetLabel, string cardName, string assetPath, string statLabel)
            {
                Kind = kind;
                TargetLabel = targetLabel;
                CardName = cardName;
                AssetPath = assetPath;
                StatLabel = statLabel;
            }

            public CardRowKind Kind { get; }
            public string KindLabel => Kind == CardRowKind.Stat ? "공통" : "새그먼트";
            public string TargetLabel { get; }
            public virtual string CardName { get; protected set; }
            public string AssetPath { get; }
            public string AssetName => string.IsNullOrWhiteSpace(AssetPath) ? "None" : Path.GetFileNameWithoutExtension(AssetPath);
            public virtual string StatLabel { get; protected set; }
            public virtual string DescriptionText => string.Empty;
            public abstract string ModeLabel { get; }
            public bool IsEditing => isEditing;
            public abstract UnityEngine.Object TargetObject { get; }
            public abstract UnityEngine.Object ApplyTargetObject { get; }
            public virtual GameObject StatPrefab => null;
            public virtual float SegmentRowHeight => RowHeight;
            public abstract bool IsDirty { get; }
            public abstract string BeforeNormalText { get; }
            public abstract string BeforeRareText { get; }
            public abstract string BeforeUniqueText { get; }
            public abstract string AfterNormalText { get; }
            public abstract string AfterRareText { get; }
            public abstract string AfterUniqueText { get; }
            public abstract string BeforeModeText { get; }
            public abstract string AfterModeText { get; }
            public abstract void DrawValues();
            public virtual void DrawCardName(float width)
            {
                GUILayout.Label(CardName, GUILayout.Width(width));
            }

            public virtual void DrawDescription(float width)
            {
                GUILayout.Label(DescriptionText, GUILayout.Width(width));
            }

            public virtual void DrawStatField(float width, WeaponStatKind[] allowedKinds)
            {
                GUILayout.Label(StatLabel, GUILayout.Width(width));
            }

            public virtual void DrawSegmentValues()
            {
                DrawValues();
            }

            public virtual void DrawMode(float width)
            {
                GUILayout.Label(ModeLabel, GUILayout.Width(width));
            }

            public abstract void ResetWorking();
            public abstract void ApplyWorking();
            public abstract void AcceptWorkingAsOriginal();

            public void BeginEdit()
            {
                OnBeginEdit();
                isEditing = true;
            }

            public void EndEdit()
            {
                isEditing = false;
            }

            protected virtual void OnBeginEdit()
            {
            }
        }

        private sealed class StatBalanceRow : BalanceRow
        {
            private const string DefaultCommonCardState = "기존";
            private readonly GameObject prefab;
            private readonly StatUpgrade statUpgrade;
            private readonly string propertyName;
            private readonly string modeLabel;
            private float original;
            private float working;
            private string originalDisplayName;
            private string workingDisplayName;
            private string originalDescription;
            private string workingDescription;
            private string originalCardState;

            public StatBalanceRow(GameObject prefab, StatUpgrade statUpgrade, string assetPath, string statLabel, string propertyName, float value, string modeLabel)
                : base(CardRowKind.Stat, ResolveStatCardState(statUpgrade), ResolveStatCardName(prefab, statUpgrade, statLabel), assetPath, statLabel)
            {
                this.prefab = prefab;
                this.statUpgrade = statUpgrade;
                this.propertyName = propertyName;
                this.modeLabel = modeLabel;
                original = value;
                working = value;
                originalDisplayName = workingDisplayName = ResolveStatCardName(prefab, statUpgrade, statLabel);
                originalDescription = workingDescription = ResolveStatCardDescription(statUpgrade, statLabel);
                originalCardState = ResolveStatCardState(statUpgrade);
            }

            public override string ModeLabel => modeLabel;
            public override string DescriptionText => workingDescription;
            public override UnityEngine.Object TargetObject => prefab;
            public override UnityEngine.Object ApplyTargetObject => statUpgrade;
            public override GameObject StatPrefab => prefab;
            public override bool IsDirty => !Mathf.Approximately(original, working)
                || !string.Equals(originalDisplayName, workingDisplayName, StringComparison.Ordinal)
                || !string.Equals(originalDescription, workingDescription, StringComparison.Ordinal);
            public override string BeforeNormalText => FormatFloat(original);
            public override string BeforeRareText => FormatFloat(original * 2f);
            public override string BeforeUniqueText => FormatFloat(original * 3f);
            public override string AfterNormalText => FormatFloat(working);
            public override string AfterRareText => FormatFloat(working * 2f);
            public override string AfterUniqueText => FormatFloat(working * 3f);
            public override string BeforeModeText => modeLabel;
            public override string AfterModeText => ModeLabel;

            public override void DrawCardName(float width)
            {
                if (IsEditing)
                {
                    workingDisplayName = EditorGUILayout.TextField(workingDisplayName ?? string.Empty, GUILayout.Width(width));
                    CardName = workingDisplayName;
                    return;
                }

                GUILayout.Label(workingDisplayName, GUILayout.Width(width));
            }

            public override void DrawDescription(float width)
            {
                if (IsEditing)
                {
                    workingDescription = EditorGUILayout.TextField(workingDescription ?? string.Empty, GUILayout.Width(width));
                    return;
                }

                GUILayout.Label(workingDescription, GUILayout.Width(width));
            }

            public override void DrawValues()
            {
                DrawSegmentValues();
            }

            public override void DrawSegmentValues()
            {
                if (!IsEditing)
                {
                    DrawReadOnlyTierValue(AfterNormalText, modeLabel);
                    DrawReadOnlyTierValue(AfterRareText, modeLabel);
                    DrawReadOnlyTierValue(AfterUniqueText, modeLabel);
                    return;
                }

                bool unusedPercentMode = false;
                working = DrawEditableFloatTierValue(working, false, modeLabel, ref unusedPercentMode);
                DrawReadOnlyTierValue(FormatFloat(working * 2f), modeLabel);
                DrawReadOnlyTierValue(FormatFloat(working * 3f), modeLabel);
            }

            public override void ResetWorking()
            {
                working = original;
                workingDisplayName = originalDisplayName;
                workingDescription = originalDescription;
                CardName = workingDisplayName;
            }

            public override void ApplyWorking()
            {
                if (statUpgrade == null)
                {
                    return;
                }

                SerializedObject serialized = new SerializedObject(statUpgrade);
                SetStringProperty(serialized, "cardState", originalCardState);
                SetStringProperty(serialized, "displayName", workingDisplayName ?? string.Empty);
                SetStringProperty(serialized, "description", workingDescription ?? string.Empty);
                SerializedProperty property = serialized.FindProperty(propertyName);
                if (property != null)
                {
                    working = Mathf.Max(0f, working);
                    property.floatValue = working;
                }

                serialized.ApplyModifiedProperties();
            }

            public override void AcceptWorkingAsOriginal()
            {
                original = working;
                originalDisplayName = workingDisplayName;
                originalDescription = workingDescription;
                CardName = workingDisplayName;
            }

            private static string ResolveStatCardState(StatUpgrade statUpgrade)
            {
                if (statUpgrade != null && !string.IsNullOrWhiteSpace(statUpgrade.CardState))
                {
                    return statUpgrade.CardState;
                }

                return DefaultCommonCardState;
            }

            private static string ResolveStatCardName(GameObject prefab, StatUpgrade statUpgrade, string statLabel)
            {
                if (statUpgrade != null && !string.IsNullOrWhiteSpace(statUpgrade.DisplayName))
                {
                    return statUpgrade.DisplayName;
                }

                string defaultName = BuildDefaultStatCardName(statLabel);
                if (!string.IsNullOrWhiteSpace(defaultName))
                {
                    return defaultName;
                }

                return prefab != null ? prefab.name : string.Empty;
            }

            private static string ResolveStatCardDescription(StatUpgrade statUpgrade, string statLabel)
            {
                if (statUpgrade != null && !string.IsNullOrWhiteSpace(statUpgrade.Description))
                {
                    return statUpgrade.Description;
                }

                return BuildDefaultStatCardDescription(statLabel);
            }

            private static string BuildDefaultStatCardName(string statLabel)
            {
                switch (statLabel)
                {
                    case "무기 공격력":
                        return "모든 무기 공격력 증가";
                    case "밀리 공격력":
                        return "모든 밀리 공격력 증가";
                    case "마법 공격력":
                        return "모든 마법 공격력 증가";
                    case "쿨타임 감소":
                        return "쿨타임 감소";
                    case "핸들링":
                        return "핸들링 강화";
                    case "충돌힘":
                        return "충돌힘 강화";
                    case "재결합 범위":
                        return "재결합 범위 강화";
                    case "넥서스 체력":
                        return "넥서스 체력 강화";
                    default:
                        return string.IsNullOrWhiteSpace(statLabel) ? string.Empty : $"{statLabel} 강화";
                }
            }

            private static string BuildDefaultStatCardDescription(string statLabel)
            {
                switch (statLabel)
                {
                    case "무기 공격력":
                        return "모든 무기 공격력 (N) 증가";
                    case "밀리 공격력":
                        return "모든 밀리 공격력 (N) 증가";
                    case "마법 공격력":
                        return "모든 마법 공격력 (N) 증가";
                    case "쿨타임 감소":
                        return "모든 세그먼트 쿨타임 (N) 감소";
                    case "핸들링":
                        return "핸들링 (N) 증가";
                    case "충돌힘":
                        return "충돌힘 (N) 증가";
                    case "재결합 범위":
                        return "재결합 범위 (N) 증가";
                    case "넥서스 체력":
                        return "넥서스 최대 체력 (N) 증가";
                    default:
                        return string.IsNullOrWhiteSpace(statLabel) ? string.Empty : $"{statLabel} (N) 증가";
                }
            }

            private static void SetStringProperty(SerializedObject serialized, string propertyName, string value)
            {
                SerializedProperty property = serialized.FindProperty(propertyName);
                if (property != null)
                {
                    property.stringValue = value ?? string.Empty;
                }
            }
        }

        private sealed class StatDefinitionBalanceRow : BalanceRow
        {
            private const string DefaultCommonCardState = "기존";
            private readonly StatUpgradeDefinition definition;
            private readonly string propertyName;
            private readonly string modeLabel;
            private float original;
            private float working;
            private string originalDisplayName;
            private string workingDisplayName;
            private string originalDescription;
            private string workingDescription;
            private string originalCardState;

            public StatDefinitionBalanceRow(StatUpgradeDefinition definition, string assetPath, string statLabel, string propertyName, float value, string modeLabel)
                : base(CardRowKind.Stat, ResolveStatCardState(definition), ResolveStatCardName(definition, statLabel), assetPath, statLabel)
            {
                this.definition = definition;
                this.propertyName = propertyName;
                this.modeLabel = modeLabel;
                original = value;
                working = value;
                originalDisplayName = workingDisplayName = ResolveStatCardName(definition, statLabel);
                originalDescription = workingDescription = ResolveStatCardDescription(definition, statLabel);
                originalCardState = ResolveStatCardState(definition);
            }

            public override string ModeLabel => modeLabel;
            public override string DescriptionText => workingDescription;
            public override UnityEngine.Object TargetObject => definition;
            public override UnityEngine.Object ApplyTargetObject => definition;
            public override bool IsDirty => !Mathf.Approximately(original, working)
                || !string.Equals(originalDisplayName, workingDisplayName, StringComparison.Ordinal)
                || !string.Equals(originalDescription, workingDescription, StringComparison.Ordinal);
            public override string BeforeNormalText => FormatFloat(original);
            public override string BeforeRareText => FormatFloat(original * 2f);
            public override string BeforeUniqueText => FormatFloat(original * 3f);
            public override string AfterNormalText => FormatFloat(working);
            public override string AfterRareText => FormatFloat(working * 2f);
            public override string AfterUniqueText => FormatFloat(working * 3f);
            public override string BeforeModeText => modeLabel;
            public override string AfterModeText => ModeLabel;

            public override void DrawCardName(float width)
            {
                if (IsEditing)
                {
                    workingDisplayName = EditorGUILayout.TextField(workingDisplayName ?? string.Empty, GUILayout.Width(width));
                    CardName = workingDisplayName;
                    return;
                }

                GUILayout.Label(workingDisplayName, GUILayout.Width(width));
            }

            public override void DrawDescription(float width)
            {
                if (IsEditing)
                {
                    workingDescription = EditorGUILayout.TextField(workingDescription ?? string.Empty, GUILayout.Width(width));
                    return;
                }

                GUILayout.Label(workingDescription, GUILayout.Width(width));
            }

            public override void DrawValues()
            {
                DrawSegmentValues();
            }

            public override void DrawSegmentValues()
            {
                if (!IsEditing)
                {
                    DrawReadOnlyTierValue(AfterNormalText, modeLabel);
                    DrawReadOnlyTierValue(AfterRareText, modeLabel);
                    DrawReadOnlyTierValue(AfterUniqueText, modeLabel);
                    return;
                }

                bool unusedPercentMode = false;
                working = DrawEditableFloatTierValue(working, false, modeLabel, ref unusedPercentMode);
                DrawReadOnlyTierValue(FormatFloat(working * 2f), modeLabel);
                DrawReadOnlyTierValue(FormatFloat(working * 3f), modeLabel);
            }

            public override void ResetWorking()
            {
                working = original;
                workingDisplayName = originalDisplayName;
                workingDescription = originalDescription;
                CardName = workingDisplayName;
            }

            public override void ApplyWorking()
            {
                if (definition == null)
                {
                    return;
                }

                SerializedObject serialized = new SerializedObject(definition);
                SetStringProperty(serialized, "CardState", originalCardState);
                SetStringProperty(serialized, "DisplayName", workingDisplayName ?? string.Empty);
                SetStringProperty(serialized, "Description", workingDescription ?? string.Empty);
                SerializedProperty property = serialized.FindProperty(propertyName);
                if (property != null)
                {
                    working = Mathf.Max(0f, working);
                    property.floatValue = working;
                }

                serialized.ApplyModifiedProperties();
            }

            public override void AcceptWorkingAsOriginal()
            {
                original = working;
                originalDisplayName = workingDisplayName;
                originalDescription = workingDescription;
                CardName = workingDisplayName;
            }

            private static string ResolveStatCardState(StatUpgradeDefinition definition)
            {
                if (definition != null && !string.IsNullOrWhiteSpace(definition.ResolvedCardState))
                {
                    return definition.ResolvedCardState;
                }

                return DefaultCommonCardState;
            }

            private static string ResolveStatCardName(StatUpgradeDefinition definition, string statLabel)
            {
                if (definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName))
                {
                    return definition.DisplayName;
                }

                string defaultName = BuildDefaultStatCardName(statLabel);
                if (!string.IsNullOrWhiteSpace(defaultName))
                {
                    return defaultName;
                }

                return definition != null ? definition.name : string.Empty;
            }

            private static string ResolveStatCardDescription(StatUpgradeDefinition definition, string statLabel)
            {
                if (definition != null && !string.IsNullOrWhiteSpace(definition.Description))
                {
                    return definition.Description;
                }

                return BuildDefaultStatCardDescription(statLabel);
            }

            private static string BuildDefaultStatCardName(string statLabel)
            {
                switch (statLabel)
                {
                    case "무기 공격력":
                        return "모든 무기 공격력 증가";
                    case "밀리 공격력":
                        return "모든 밀리 공격력 증가";
                    case "마법 공격력":
                        return "모든 마법 공격력 증가";
                    case "쿨타임 감소":
                        return "쿨타임 감소";
                    case "핸들링":
                        return "핸들링 강화";
                    case "충돌힘":
                        return "충돌힘 강화";
                    case "재결합 범위":
                        return "재결합 범위 강화";
                    case "넥서스 체력":
                        return "넥서스 체력 강화";
                    default:
                        return string.IsNullOrWhiteSpace(statLabel) ? string.Empty : $"{statLabel} 강화";
                }
            }

            private static string BuildDefaultStatCardDescription(string statLabel)
            {
                switch (statLabel)
                {
                    case "무기 공격력":
                        return "모든 무기 공격력 (N) 증가";
                    case "밀리 공격력":
                        return "모든 밀리 공격력 (N) 증가";
                    case "마법 공격력":
                        return "모든 마법 공격력 (N) 증가";
                    case "쿨타임 감소":
                        return "모든 세그먼트 쿨타임 (N) 감소";
                    case "핸들링":
                        return "핸들링 (N) 증가";
                    case "충돌힘":
                        return "충돌힘 (N) 증가";
                    case "재결합 범위":
                        return "재결합 범위 (N) 증가";
                    case "넥서스 체력":
                        return "넥서스 최대 체력 (N) 증가";
                    default:
                        return string.IsNullOrWhiteSpace(statLabel) ? string.Empty : $"{statLabel} (N) 증가";
                }
            }

            private static void SetStringProperty(SerializedObject serialized, string propertyName, string value)
            {
                SerializedProperty property = serialized.FindProperty(propertyName);
                if (property != null)
                {
                    property.stringValue = value ?? string.Empty;
                }
            }
        }

        private sealed class WeaponCardBalanceRow : BalanceRow
        {
            private readonly WeaponDefinition definition;
            private readonly List<WeaponStatLine> statLines = new List<WeaponStatLine>(4);
            private string originalDisplayName;
            private string workingDisplayName;
            private string originalDescription;

            public WeaponCardBalanceRow(string groupLabel, WeaponDefinition definition, string assetPath)
                : base(CardRowKind.Weapon, groupLabel, ResolveDefinitionDisplayName(definition), assetPath, string.Empty)
            {
                this.definition = definition;
                originalDisplayName = workingDisplayName = ResolveDefinitionDisplayName(definition);
                BuildInitialLines();
                originalDescription = BuildCombinedDescription();
                StatLabel = BuildStatSummary();
            }

            public bool HasVisibleLines => CountVisibleLines() > 0;
            public override float SegmentRowHeight => Mathf.Max(RowHeight, (CountVisibleLines() + (IsEditing ? 1 : 0)) * RowHeight);
            public override string ModeLabel => BuildModeSummary();
            public override string DescriptionText => BuildCombinedDescription();
            public override UnityEngine.Object TargetObject => definition;
            public override UnityEngine.Object ApplyTargetObject => definition;
            public override bool IsDirty => !string.Equals(originalDisplayName, workingDisplayName, StringComparison.Ordinal)
                || !string.Equals(originalDescription, BuildCombinedDescription(), StringComparison.Ordinal)
                || HasDirtyStatLine();
            public override string BeforeNormalText => BuildTierText(TierColumn.Normal, useWorking: false);
            public override string BeforeRareText => BuildTierText(TierColumn.Rare, useWorking: false);
            public override string BeforeUniqueText => BuildTierText(TierColumn.Unique, useWorking: false);
            public override string AfterNormalText => BuildTierText(TierColumn.Normal, useWorking: true);
            public override string AfterRareText => BuildTierText(TierColumn.Rare, useWorking: true);
            public override string AfterUniqueText => BuildTierText(TierColumn.Unique, useWorking: true);
            public override string BeforeModeText => BuildModeSummary(useWorking: false);
            public override string AfterModeText => BuildModeSummary();

            public override void DrawCardName(float width)
            {
                if (IsEditing)
                {
                    workingDisplayName = EditorGUILayout.TextField(workingDisplayName ?? string.Empty, GUILayout.Width(width));
                    CardName = workingDisplayName;
                    return;
                }

                GUILayout.Label(workingDisplayName, GUILayout.Width(width));
            }

            public override void DrawDescription(float width)
            {
                int visibleCount = CountVisibleLines();
                GUIStyle labelStyle = visibleCount >= 3 ? EditorStyles.miniLabel : EditorStyles.label;
                using (new GUILayout.VerticalScope(GUILayout.Width(width)))
                {
                    for (int i = 0; i < statLines.Count; i++)
                    {
                        WeaponStatLine line = statLines[i];
                        if (line.IsRemoved)
                        {
                            continue;
                        }

                        if (IsEditing)
                        {
                            line.WorkingDescription = EditorGUILayout.TextField(line.WorkingDescription ?? string.Empty, GUILayout.Width(width));
                        }
                        else
                        {
                            GUILayout.Label(line.WorkingDescription, labelStyle, GUILayout.Width(width), GUILayout.Height(RowHeight));
                        }
                    }
                }
            }

            public override void DrawStatField(float width, WeaponStatKind[] allowedKinds)
            {
                using (new GUILayout.VerticalScope(GUILayout.Width(width)))
                {
                    int visibleCount = CountVisibleLines();
                    for (int i = 0; i < statLines.Count; i++)
                    {
                        WeaponStatLine line = statLines[i];
                        if (line.IsRemoved)
                        {
                            continue;
                        }

                        if (!IsEditing)
                        {
                            GUILayout.Label(GetWeaponStatInfo(line.WorkingKind).Label, GUILayout.Width(width), GUILayout.Height(RowHeight));
                            continue;
                        }

                        using (new GUILayout.HorizontalScope(GUILayout.Width(width)))
                        {
                            WeaponStatKind previous = line.WorkingKind;
                            WeaponStatKind[] selectableKinds = BuildSelectableKinds(line, allowedKinds);
                            line.WorkingKind = DrawWeaponStatPopup(GUIContent.none, line.WorkingKind, Mathf.Max(70f, width - 24f), selectableKinds);
                            if (previous != line.WorkingKind)
                            {
                                line.OnWorkingKindChanged(previous);
                            }

                            GUI.enabled = visibleCount > 1;
                            if (GUILayout.Button("-", GUILayout.Width(20f)))
                            {
                                line.IsRemoved = true;
                                StatLabel = BuildStatSummary();
                            }
                            GUI.enabled = true;
                        }
                    }

                    if (IsEditing)
                    {
                        GUI.enabled = HasAddableKind(allowedKinds);
                        if (GUILayout.Button("+", GUILayout.Width(width)))
                        {
                            AddLine(allowedKinds);
                            StatLabel = BuildStatSummary();
                        }
                        GUI.enabled = true;
                    }
                }
            }

            public override void DrawValues()
            {
                DrawSegmentValues();
            }

            public override void DrawSegmentValues()
            {
                using (new GUILayout.VerticalScope(GUILayout.Width(SegmentTierValueWidth * 3f)))
                {
                    for (int i = 0; i < statLines.Count; i++)
                    {
                        WeaponStatLine line = statLines[i];
                        if (line.IsRemoved)
                        {
                            continue;
                        }

                        using (new GUILayout.HorizontalScope())
                        {
                            line.DrawTierValues(IsEditing);
                        }
                    }
                }
            }

            public override void DrawMode(float width)
            {
                GUILayout.Label(ModeLabel, GUILayout.Width(width));
            }

            public override void ResetWorking()
            {
                workingDisplayName = originalDisplayName;
                for (int i = statLines.Count - 1; i >= 0; i--)
                {
                    if (!statLines[i].HasOriginal)
                    {
                        statLines.RemoveAt(i);
                        continue;
                    }

                    statLines[i].ResetWorking();
                }

                CardName = workingDisplayName;
                StatLabel = BuildStatSummary();
            }

            public override void ApplyWorking()
            {
                if (definition == null)
                {
                    return;
                }

                definition.DisplayName = workingDisplayName ?? string.Empty;
                definition.Description = BuildCombinedDescription();
                WeaponStatKind[] allKinds = GetWeaponStatKinds();
                for (int i = 0; i < allKinds.Length; i++)
                {
                    ClearWeaponStatValues(definition, GetWeaponStatInfo(allKinds[i]));
                }

                for (int i = 0; i < statLines.Count; i++)
                {
                    if (!statLines[i].IsRemoved)
                    {
                        statLines[i].ApplyWorking(definition);
                    }
                }
            }

            public override void AcceptWorkingAsOriginal()
            {
                originalDisplayName = workingDisplayName;
                for (int i = statLines.Count - 1; i >= 0; i--)
                {
                    if (statLines[i].IsRemoved)
                    {
                        statLines.RemoveAt(i);
                        continue;
                    }

                    statLines[i].AcceptWorkingAsOriginal();
                }

                originalDescription = BuildCombinedDescription();
                CardName = workingDisplayName;
                StatLabel = BuildStatSummary();
            }

            protected override void OnBeginEdit()
            {
                workingDisplayName = ResolveDefinitionDisplayName(definition);
                CardName = workingDisplayName;
                StatLabel = BuildStatSummary();
            }

            private void BuildInitialLines()
            {
                WeaponStatKind[] kinds = GetWeaponStatKinds();
                List<WeaponStatKind> activeKinds = new List<WeaponStatKind>(4);
                for (int i = 0; i < kinds.Length; i++)
                {
                    WeaponStatInfo info = GetWeaponStatInfo(kinds[i]);
                    if (HasAnyWeaponStatValue(definition, info))
                    {
                        activeKinds.Add(kinds[i]);
                    }
                }

                string[] descriptionLines = SplitDescriptionLines(definition != null ? definition.Description : string.Empty);
                for (int i = 0; i < activeKinds.Count; i++)
                {
                    WeaponStatKind kind = activeKinds[i];
                    string description = ResolveInitialDescription(descriptionLines, i, activeKinds.Count, kind);
                    statLines.Add(WeaponStatLine.FromDefinition(definition, kind, description));
                }
            }

            private static bool HasAnyWeaponStatValue(WeaponDefinition definition, WeaponStatInfo info)
            {
                if (definition == null)
                {
                    return false;
                }

                if (info.ValueKind == StatValueKind.Int)
                {
                    return GetInt(definition, info.NormalName) != 0
                        || GetInt(definition, info.RareName) != 0
                        || GetInt(definition, info.UniqueName) != 0;
                }

                return HasAny(GetFloat(definition, info.NormalName), GetFloat(definition, info.RareName), GetFloat(definition, info.UniqueName));
            }

            private static string[] SplitDescriptionLines(string description)
            {
                if (string.IsNullOrWhiteSpace(description))
                {
                    return Array.Empty<string>();
                }

                return description.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            }

            private static string ResolveInitialDescription(string[] descriptionLines, int index, int totalCount, WeaponStatKind kind)
            {
                if (descriptionLines != null
                    && descriptionLines.Length == totalCount
                    && index >= 0
                    && index < descriptionLines.Length
                    && !string.IsNullOrWhiteSpace(descriptionLines[index])
                    && descriptionLines[index].Contains("(N)"))
                {
                    return descriptionLines[index].Trim();
                }

                return BuildDefaultWeaponDescriptionLine(kind);
            }

            private int CountVisibleLines()
            {
                int count = 0;
                for (int i = 0; i < statLines.Count; i++)
                {
                    if (!statLines[i].IsRemoved)
                    {
                        count++;
                    }
                }

                return count;
            }

            private bool HasDirtyStatLine()
            {
                for (int i = 0; i < statLines.Count; i++)
                {
                    if (statLines[i].IsDirty)
                    {
                        return true;
                    }
                }

                return false;
            }

            private string BuildCombinedDescription()
            {
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < statLines.Count; i++)
                {
                    WeaponStatLine line = statLines[i];
                    if (line.IsRemoved)
                    {
                        continue;
                    }

                    if (builder.Length > 0)
                    {
                        builder.AppendLine();
                    }

                    builder.Append(string.IsNullOrWhiteSpace(line.WorkingDescription)
                        ? BuildDefaultWeaponDescriptionLine(line.WorkingKind)
                        : line.WorkingDescription.Trim());
                }

                return builder.ToString();
            }

            private string BuildStatSummary()
            {
                int visibleCount = CountVisibleLines();
                if (visibleCount <= 0)
                {
                    return "능력치 없음";
                }

                if (visibleCount == 1)
                {
                    for (int i = 0; i < statLines.Count; i++)
                    {
                        if (!statLines[i].IsRemoved)
                        {
                            return GetWeaponStatInfo(statLines[i].WorkingKind).Label;
                        }
                    }
                }

                return $"{visibleCount}개 능력치";
            }

            private string BuildModeSummary(bool useWorking = true)
            {
                List<string> modes = new List<string>(statLines.Count);
                for (int i = 0; i < statLines.Count; i++)
                {
                    WeaponStatLine line = statLines[i];
                    if (line.IsRemoved)
                    {
                        continue;
                    }

                    modes.Add(useWorking ? line.WorkingModeLabel : line.OriginalModeLabel);
                }

                return string.Join(" / ", modes);
            }

            private string BuildTierText(TierColumn tier, bool useWorking)
            {
                List<string> values = new List<string>(statLines.Count);
                for (int i = 0; i < statLines.Count; i++)
                {
                    WeaponStatLine line = statLines[i];
                    if (line.IsRemoved && useWorking)
                    {
                        continue;
                    }

                    if (!line.HasOriginal && !useWorking)
                    {
                        continue;
                    }

                    values.Add(line.GetTierText(tier, useWorking));
                }

                return string.Join(" / ", values);
            }

            private WeaponStatKind[] BuildSelectableKinds(WeaponStatLine currentLine, WeaponStatKind[] allowedKinds)
            {
                allowedKinds ??= GetWeaponStatKinds();
                List<WeaponStatKind> selectable = new List<WeaponStatKind>(allowedKinds.Length);
                for (int i = 0; i < allowedKinds.Length; i++)
                {
                    WeaponStatKind kind = allowedKinds[i];
                    if (kind == currentLine.WorkingKind || !ContainsVisibleWorkingKind(kind, currentLine))
                    {
                        selectable.Add(kind);
                    }
                }

                if (selectable.Count == 0)
                {
                    selectable.Add(currentLine.WorkingKind);
                }

                return selectable.ToArray();
            }

            private bool ContainsVisibleWorkingKind(WeaponStatKind kind, WeaponStatLine exceptLine = null)
            {
                for (int i = 0; i < statLines.Count; i++)
                {
                    WeaponStatLine line = statLines[i];
                    if (line == exceptLine || line.IsRemoved)
                    {
                        continue;
                    }

                    if (line.WorkingKind == kind)
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool HasAddableKind(WeaponStatKind[] allowedKinds)
            {
                allowedKinds ??= GetWeaponStatKinds();
                for (int i = 0; i < allowedKinds.Length; i++)
                {
                    if (!ContainsVisibleWorkingKind(allowedKinds[i]))
                    {
                        return true;
                    }
                }

                return false;
            }

            private void AddLine(WeaponStatKind[] allowedKinds)
            {
                allowedKinds ??= GetWeaponStatKinds();
                for (int i = 0; i < allowedKinds.Length; i++)
                {
                    WeaponStatKind kind = allowedKinds[i];
                    if (!ContainsVisibleWorkingKind(kind))
                    {
                        statLines.Add(WeaponStatLine.CreateNew(kind));
                        return;
                    }
                }
            }

            private static string ResolveDefinitionDisplayName(WeaponDefinition definition)
            {
                if (definition == null)
                {
                    return string.Empty;
                }

                return string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.name : definition.DisplayName;
            }

            private enum TierColumn
            {
                Normal,
                Rare,
                Unique
            }

            private sealed class WeaponStatLine
            {
                public bool HasOriginal;
                public bool IsRemoved;
                public WeaponStatKind OriginalKind;
                public WeaponStatKind WorkingKind;
                public string OriginalDescription;
                public string WorkingDescription;
                private float originalNormal;
                private float originalRare;
                private float originalUnique;
                private float workingNormal;
                private float workingRare;
                private float workingUnique;
                private bool originalNormalPercent;
                private bool originalRarePercent;
                private bool originalUniquePercent;
                private bool workingNormalPercent;
                private bool workingRarePercent;
                private bool workingUniquePercent;

                private WeaponStatInfo WorkingInfo => GetWeaponStatInfo(WorkingKind);
                private WeaponStatInfo OriginalInfo => GetWeaponStatInfo(OriginalKind);
                public string WorkingModeLabel => WorkingInfo.HasPercentMode ? BuildModeLabel(workingNormalPercent, workingRarePercent, workingUniquePercent) : WorkingInfo.ModeLabel;
                public string OriginalModeLabel => OriginalInfo.HasPercentMode ? BuildModeLabel(originalNormalPercent, originalRarePercent, originalUniquePercent) : OriginalInfo.ModeLabel;
                public bool IsDirty => IsRemoved
                    || !HasOriginal
                    || OriginalKind != WorkingKind
                    || !Mathf.Approximately(originalNormal, workingNormal)
                    || !Mathf.Approximately(originalRare, workingRare)
                    || !Mathf.Approximately(originalUnique, workingUnique)
                    || originalNormalPercent != workingNormalPercent
                    || originalRarePercent != workingRarePercent
                    || originalUniquePercent != workingUniquePercent
                    || !string.Equals(OriginalDescription, WorkingDescription, StringComparison.Ordinal);

                public static WeaponStatLine FromDefinition(WeaponDefinition definition, WeaponStatKind kind, string description)
                {
                    WeaponStatInfo info = GetWeaponStatInfo(kind);
                    WeaponStatLine line = new WeaponStatLine
                    {
                        HasOriginal = true,
                        OriginalKind = kind,
                        WorkingKind = kind,
                        OriginalDescription = description,
                        WorkingDescription = description
                    };

                    if (info.ValueKind == StatValueKind.Int)
                    {
                        line.originalNormal = line.workingNormal = GetInt(definition, info.NormalName);
                        line.originalRare = line.workingRare = GetInt(definition, info.RareName);
                        line.originalUnique = line.workingUnique = GetInt(definition, info.UniqueName);
                    }
                    else
                    {
                        line.originalNormal = line.workingNormal = GetFloat(definition, info.NormalName);
                        line.originalRare = line.workingRare = GetFloat(definition, info.RareName);
                        line.originalUnique = line.workingUnique = GetFloat(definition, info.UniqueName);
                        line.originalNormalPercent = line.workingNormalPercent = GetBool(definition, info.NormalPercentName);
                        line.originalRarePercent = line.workingRarePercent = GetBool(definition, info.RarePercentName);
                        line.originalUniquePercent = line.workingUniquePercent = GetBool(definition, info.UniquePercentName);
                    }

                    return line;
                }

                public static WeaponStatLine CreateNew(WeaponStatKind kind)
                {
                    WeaponStatInfo info = GetWeaponStatInfo(kind);
                    WeaponStatLine line = new WeaponStatLine
                    {
                        HasOriginal = false,
                        OriginalKind = kind,
                        WorkingKind = kind,
                        OriginalDescription = string.Empty,
                        WorkingDescription = BuildDefaultWeaponDescriptionLine(kind)
                    };

                    if (info.ValueKind == StatValueKind.Int)
                    {
                        line.workingNormal = 1f;
                        line.workingRare = 2f;
                        line.workingUnique = 3f;
                    }
                    else
                    {
                        line.workingNormal = 0.1f;
                        line.workingRare = 0.2f;
                        line.workingUnique = 0.3f;
                    }

                    return line;
                }

                public void DrawTierValues(bool isEditing)
                {
                    WeaponStatInfo info = WorkingInfo;
                    if (!isEditing)
                    {
                        DrawReadOnlyTierValue(GetTierText(TierColumn.Normal, useWorking: true), GetModeText(TierColumn.Normal, useWorking: true));
                        DrawReadOnlyTierValue(GetTierText(TierColumn.Rare, useWorking: true), GetModeText(TierColumn.Rare, useWorking: true));
                        DrawReadOnlyTierValue(GetTierText(TierColumn.Unique, useWorking: true), GetModeText(TierColumn.Unique, useWorking: true));
                        return;
                    }

                    if (info.ValueKind == StatValueKind.Int)
                    {
                        workingNormal = DrawEditableIntTierValue(Mathf.RoundToInt(workingNormal), info.ModeLabel);
                        workingRare = DrawEditableIntTierValue(Mathf.RoundToInt(workingRare), info.ModeLabel);
                        workingUnique = DrawEditableIntTierValue(Mathf.RoundToInt(workingUnique), info.ModeLabel);
                        return;
                    }

                    workingNormal = DrawEditableFloatTierValue(workingNormal, info.HasPercentMode, info.ModeLabel, ref workingNormalPercent);
                    workingRare = DrawEditableFloatTierValue(workingRare, info.HasPercentMode, info.ModeLabel, ref workingRarePercent);
                    workingUnique = DrawEditableFloatTierValue(workingUnique, info.HasPercentMode, info.ModeLabel, ref workingUniquePercent);
                }

                public void OnWorkingKindChanged(WeaponStatKind previousKind)
                {
                    WeaponStatInfo info = WorkingInfo;
                    if (info.ValueKind == StatValueKind.Int)
                    {
                        workingNormal = Mathf.RoundToInt(workingNormal);
                        workingRare = Mathf.RoundToInt(workingRare);
                        workingUnique = Mathf.RoundToInt(workingUnique);
                    }

                    if (!info.HasPercentMode)
                    {
                        workingNormalPercent = false;
                        workingRarePercent = false;
                        workingUniquePercent = false;
                    }

                    string previousDefault = BuildDefaultWeaponDescriptionLine(previousKind);
                    if (string.IsNullOrWhiteSpace(WorkingDescription) || string.Equals(WorkingDescription, previousDefault, StringComparison.Ordinal))
                    {
                        WorkingDescription = BuildDefaultWeaponDescriptionLine(WorkingKind);
                    }
                }

                public void ApplyWorking(WeaponDefinition definition)
                {
                    WeaponStatInfo info = WorkingInfo;
                    if (info.ValueKind == StatValueKind.Int)
                    {
                        SetWeaponIntStatValues(definition, info, Mathf.RoundToInt(workingNormal), Mathf.RoundToInt(workingRare), Mathf.RoundToInt(workingUnique));
                        return;
                    }

                    SetWeaponFloatStatValues(definition, info, workingNormal, workingRare, workingUnique, workingNormalPercent, workingRarePercent, workingUniquePercent);
                }

                public void ResetWorking()
                {
                    IsRemoved = false;
                    WorkingKind = OriginalKind;
                    WorkingDescription = OriginalDescription;
                    workingNormal = originalNormal;
                    workingRare = originalRare;
                    workingUnique = originalUnique;
                    workingNormalPercent = originalNormalPercent;
                    workingRarePercent = originalRarePercent;
                    workingUniquePercent = originalUniquePercent;
                }

                public void AcceptWorkingAsOriginal()
                {
                    HasOriginal = true;
                    OriginalKind = WorkingKind;
                    OriginalDescription = WorkingDescription;
                    originalNormal = workingNormal;
                    originalRare = workingRare;
                    originalUnique = workingUnique;
                    originalNormalPercent = workingNormalPercent;
                    originalRarePercent = workingRarePercent;
                    originalUniquePercent = workingUniquePercent;
                    IsRemoved = false;
                }

                public string GetTierText(TierColumn tier, bool useWorking)
                {
                    WeaponStatInfo info = useWorking ? WorkingInfo : OriginalInfo;
                    float value = SelectTierValue(tier, useWorking);
                    if (info.ValueKind == StatValueKind.Int)
                    {
                        return Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);
                    }

                    return FormatFloat(value);
                }

                private string GetModeText(TierColumn tier, bool useWorking)
                {
                    WeaponStatInfo info = useWorking ? WorkingInfo : OriginalInfo;
                    if (!info.HasPercentMode)
                    {
                        return info.ModeLabel;
                    }

                    return GetPercentModeLabel(SelectTierPercent(tier, useWorking));
                }

                private float SelectTierValue(TierColumn tier, bool useWorking)
                {
                    switch (tier)
                    {
                        case TierColumn.Rare:
                            return useWorking ? workingRare : originalRare;
                        case TierColumn.Unique:
                            return useWorking ? workingUnique : originalUnique;
                        default:
                            return useWorking ? workingNormal : originalNormal;
                    }
                }

                private bool SelectTierPercent(TierColumn tier, bool useWorking)
                {
                    switch (tier)
                    {
                        case TierColumn.Rare:
                            return useWorking ? workingRarePercent : originalRarePercent;
                        case TierColumn.Unique:
                            return useWorking ? workingUniquePercent : originalUniquePercent;
                        default:
                            return useWorking ? workingNormalPercent : originalNormalPercent;
                    }
                }
            }
        }

        private sealed class WeaponFloatBalanceRow : BalanceRow
        {
            private readonly WeaponDefinition definition;
            private WeaponStatKind originalStatKind;
            private WeaponStatKind workingStatKind;
            private string originalDisplayName;
            private string workingDisplayName;
            private string originalDescription;
            private string workingDescription;
            private float originalNormal;
            private float originalRare;
            private float originalUnique;
            private float workingNormal;
            private float workingRare;
            private float workingUnique;
            private bool originalNormalPercent;
            private bool originalRarePercent;
            private bool originalUniquePercent;
            private bool workingNormalPercent;
            private bool workingRarePercent;
            private bool workingUniquePercent;

            public WeaponFloatBalanceRow(
                string groupLabel,
                WeaponDefinition definition,
                string assetPath,
                WeaponStatKind statKind,
                float normal,
                float rare,
                float unique,
                WeaponStatInfo info)
                : base(CardRowKind.Weapon, groupLabel, string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.name : definition.DisplayName, assetPath, info.Label)
            {
                this.definition = definition;
                originalStatKind = workingStatKind = statKind;
                originalDisplayName = workingDisplayName = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.name : definition.DisplayName;
                originalDescription = workingDescription = definition.Description ?? string.Empty;
                originalNormal = workingNormal = normal;
                originalRare = workingRare = rare;
                originalUnique = workingUnique = unique;
                originalNormalPercent = workingNormalPercent = GetBool(definition, info.NormalPercentName);
                originalRarePercent = workingRarePercent = GetBool(definition, info.RarePercentName);
                originalUniquePercent = workingUniquePercent = GetBool(definition, info.UniquePercentName);
            }

            private WeaponStatInfo WorkingInfo => GetWeaponStatInfo(workingStatKind);
            private WeaponStatInfo OriginalInfo => GetWeaponStatInfo(originalStatKind);

            public override string ModeLabel => WorkingInfo.HasPercentMode ? BuildModeLabel(workingNormalPercent, workingRarePercent, workingUniquePercent) : WorkingInfo.ModeLabel;
            public override string DescriptionText => workingDescription;
            public override UnityEngine.Object TargetObject => definition;
            public override UnityEngine.Object ApplyTargetObject => definition;
            public override bool IsDirty => !Mathf.Approximately(originalNormal, workingNormal)
                || !Mathf.Approximately(originalRare, workingRare)
                || !Mathf.Approximately(originalUnique, workingUnique)
                || originalNormalPercent != workingNormalPercent
                || originalRarePercent != workingRarePercent
                || originalUniquePercent != workingUniquePercent
                || originalStatKind != workingStatKind
                || !string.Equals(originalDisplayName, workingDisplayName, StringComparison.Ordinal)
                || !string.Equals(originalDescription, workingDescription, StringComparison.Ordinal);
            public override string BeforeNormalText => FormatFloat(originalNormal);
            public override string BeforeRareText => FormatFloat(originalRare);
            public override string BeforeUniqueText => FormatFloat(originalUnique);
            public override string AfterNormalText => FormatFloat(workingNormal);
            public override string AfterRareText => FormatFloat(workingRare);
            public override string AfterUniqueText => FormatFloat(workingUnique);
            public override string BeforeModeText => OriginalInfo.HasPercentMode ? BuildModeLabel(originalNormalPercent, originalRarePercent, originalUniquePercent) : OriginalInfo.ModeLabel;
            public override string AfterModeText => ModeLabel;

            public override void DrawCardName(float width)
            {
                if (IsEditing)
                {
                    workingDisplayName = EditorGUILayout.TextField(workingDisplayName ?? string.Empty, GUILayout.Width(width));
                    CardName = workingDisplayName;
                    return;
                }

                GUILayout.Label(workingDisplayName, GUILayout.Width(width));
            }

            public override void DrawDescription(float width)
            {
                if (IsEditing)
                {
                    workingDescription = EditorGUILayout.TextField(workingDescription ?? string.Empty, GUILayout.Width(width));
                    return;
                }

                GUILayout.Label(workingDescription, GUILayout.Width(width));
            }

            public override void DrawStatField(float width, WeaponStatKind[] allowedKinds)
            {
                if (IsEditing)
                {
                    WeaponStatKind[] floatKinds = FilterWeaponStatKindsByValueKind(allowedKinds, StatValueKind.Float);
                    floatKinds = EnsureWeaponStatKindOption(floatKinds, workingStatKind);
                    WeaponStatKind previous = workingStatKind;
                    workingStatKind = DrawWeaponStatPopup(GUIContent.none, workingStatKind, width, floatKinds);
                    if (previous != workingStatKind && !WorkingInfo.HasPercentMode)
                    {
                        workingNormalPercent = false;
                        workingRarePercent = false;
                        workingUniquePercent = false;
                    }

                    StatLabel = WorkingInfo.Label;
                    return;
                }

                GUILayout.Label(WorkingInfo.Label, GUILayout.Width(width));
            }

            public override void DrawValues()
            {
                DrawSegmentValues();
            }

            public override void DrawSegmentValues()
            {
                if (!IsEditing)
                {
                    WeaponStatInfo info = WorkingInfo;
                    DrawReadOnlyTierValue(AfterNormalText, info.HasPercentMode ? GetPercentModeLabel(workingNormalPercent) : info.ModeLabel);
                    DrawReadOnlyTierValue(AfterRareText, info.HasPercentMode ? GetPercentModeLabel(workingRarePercent) : info.ModeLabel);
                    DrawReadOnlyTierValue(AfterUniqueText, info.HasPercentMode ? GetPercentModeLabel(workingUniquePercent) : info.ModeLabel);
                    return;
                }

                WeaponStatInfo workingInfo = WorkingInfo;
                workingNormal = DrawEditableFloatTierValue(workingNormal, workingInfo.HasPercentMode, workingInfo.ModeLabel, ref workingNormalPercent);
                workingRare = DrawEditableFloatTierValue(workingRare, workingInfo.HasPercentMode, workingInfo.ModeLabel, ref workingRarePercent);
                workingUnique = DrawEditableFloatTierValue(workingUnique, workingInfo.HasPercentMode, workingInfo.ModeLabel, ref workingUniquePercent);
            }

            public override void DrawMode(float width)
            {
                GUILayout.Label(ModeLabel, GUILayout.Width(width));
            }

            public override void ResetWorking()
            {
                workingNormal = originalNormal;
                workingRare = originalRare;
                workingUnique = originalUnique;
                workingNormalPercent = originalNormalPercent;
                workingRarePercent = originalRarePercent;
                workingUniquePercent = originalUniquePercent;
                workingStatKind = originalStatKind;
                workingDisplayName = originalDisplayName;
                workingDescription = originalDescription;
                CardName = workingDisplayName;
                StatLabel = WorkingInfo.Label;
            }

            public override void ApplyWorking()
            {
                if (definition == null)
                {
                    return;
                }

                definition.DisplayName = workingDisplayName ?? string.Empty;
                definition.Description = workingDescription ?? string.Empty;
                if (originalStatKind != workingStatKind)
                {
                    ClearWeaponStatValues(definition, OriginalInfo);
                }

                WeaponStatInfo info = WorkingInfo;
                SetWeaponFloatStatValues(definition, info, workingNormal, workingRare, workingUnique, workingNormalPercent, workingRarePercent, workingUniquePercent);
            }

            public override void AcceptWorkingAsOriginal()
            {
                originalStatKind = workingStatKind;
                originalDisplayName = workingDisplayName;
                originalDescription = workingDescription;
                originalNormal = workingNormal;
                originalRare = workingRare;
                originalUnique = workingUnique;
                originalNormalPercent = workingNormalPercent;
                originalRarePercent = workingRarePercent;
                originalUniquePercent = workingUniquePercent;
                CardName = workingDisplayName;
                StatLabel = WorkingInfo.Label;
            }

            protected override void OnBeginEdit()
            {
                string displayName = ResolveDefinitionDisplayName(definition);
                originalDisplayName = workingDisplayName = displayName;
                originalDescription = workingDescription = definition != null ? definition.Description ?? string.Empty : string.Empty;
                CardName = workingDisplayName;
                StatLabel = WorkingInfo.Label;
            }

            private static string ResolveDefinitionDisplayName(WeaponDefinition definition)
            {
                if (definition == null)
                {
                    return string.Empty;
                }

                return string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.name : definition.DisplayName;
            }
        }

        private sealed class WeaponIntBalanceRow : BalanceRow
        {
            private readonly WeaponDefinition definition;
            private WeaponStatKind originalStatKind;
            private WeaponStatKind workingStatKind;
            private string originalDisplayName;
            private string workingDisplayName;
            private string originalDescription;
            private string workingDescription;
            private int originalNormal;
            private int originalRare;
            private int originalUnique;
            private int workingNormal;
            private int workingRare;
            private int workingUnique;

            public WeaponIntBalanceRow(
                string groupLabel,
                WeaponDefinition definition,
                string assetPath,
                WeaponStatKind statKind,
                int normal,
                int rare,
                int unique,
                WeaponStatInfo info)
                : base(CardRowKind.Weapon, groupLabel, string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.name : definition.DisplayName, assetPath, info.Label)
            {
                this.definition = definition;
                originalStatKind = workingStatKind = statKind;
                originalDisplayName = workingDisplayName = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.name : definition.DisplayName;
                originalDescription = workingDescription = definition.Description ?? string.Empty;
                originalNormal = workingNormal = normal;
                originalRare = workingRare = rare;
                originalUnique = workingUnique = unique;
            }

            private WeaponStatInfo WorkingInfo => GetWeaponStatInfo(workingStatKind);
            private WeaponStatInfo OriginalInfo => GetWeaponStatInfo(originalStatKind);

            public override string ModeLabel => WorkingInfo.ModeLabel;
            public override string DescriptionText => workingDescription;
            public override UnityEngine.Object TargetObject => definition;
            public override UnityEngine.Object ApplyTargetObject => definition;
            public override bool IsDirty => originalNormal != workingNormal
                || originalRare != workingRare
                || originalUnique != workingUnique
                || originalStatKind != workingStatKind
                || !string.Equals(originalDisplayName, workingDisplayName, StringComparison.Ordinal)
                || !string.Equals(originalDescription, workingDescription, StringComparison.Ordinal);
            public override string BeforeNormalText => originalNormal.ToString(CultureInfo.InvariantCulture);
            public override string BeforeRareText => originalRare.ToString(CultureInfo.InvariantCulture);
            public override string BeforeUniqueText => originalUnique.ToString(CultureInfo.InvariantCulture);
            public override string AfterNormalText => workingNormal.ToString(CultureInfo.InvariantCulture);
            public override string AfterRareText => workingRare.ToString(CultureInfo.InvariantCulture);
            public override string AfterUniqueText => workingUnique.ToString(CultureInfo.InvariantCulture);
            public override string BeforeModeText => OriginalInfo.ModeLabel;
            public override string AfterModeText => ModeLabel;

            public override void DrawCardName(float width)
            {
                if (IsEditing)
                {
                    workingDisplayName = EditorGUILayout.TextField(workingDisplayName ?? string.Empty, GUILayout.Width(width));
                    CardName = workingDisplayName;
                    return;
                }

                GUILayout.Label(workingDisplayName, GUILayout.Width(width));
            }

            public override void DrawDescription(float width)
            {
                if (IsEditing)
                {
                    workingDescription = EditorGUILayout.TextField(workingDescription ?? string.Empty, GUILayout.Width(width));
                    return;
                }

                GUILayout.Label(workingDescription, GUILayout.Width(width));
            }

            public override void DrawStatField(float width, WeaponStatKind[] allowedKinds)
            {
                if (IsEditing)
                {
                    WeaponStatKind[] intKinds = FilterWeaponStatKindsByValueKind(allowedKinds, StatValueKind.Int);
                    intKinds = EnsureWeaponStatKindOption(intKinds, workingStatKind);
                    workingStatKind = DrawWeaponStatPopup(GUIContent.none, workingStatKind, width, intKinds);
                    StatLabel = WorkingInfo.Label;
                    return;
                }

                GUILayout.Label(WorkingInfo.Label, GUILayout.Width(width));
            }

            public override void DrawValues()
            {
                DrawSegmentValues();
            }

            public override void DrawSegmentValues()
            {
                if (!IsEditing)
                {
                    DrawReadOnlyTierValue(AfterNormalText, WorkingInfo.ModeLabel);
                    DrawReadOnlyTierValue(AfterRareText, WorkingInfo.ModeLabel);
                    DrawReadOnlyTierValue(AfterUniqueText, WorkingInfo.ModeLabel);
                    return;
                }

                workingNormal = DrawEditableIntTierValue(workingNormal, WorkingInfo.ModeLabel);
                workingRare = DrawEditableIntTierValue(workingRare, WorkingInfo.ModeLabel);
                workingUnique = DrawEditableIntTierValue(workingUnique, WorkingInfo.ModeLabel);
            }

            public override void ResetWorking()
            {
                workingNormal = originalNormal;
                workingRare = originalRare;
                workingUnique = originalUnique;
                workingStatKind = originalStatKind;
                workingDisplayName = originalDisplayName;
                workingDescription = originalDescription;
                CardName = workingDisplayName;
                StatLabel = WorkingInfo.Label;
            }

            public override void ApplyWorking()
            {
                if (definition == null)
                {
                    return;
                }

                definition.DisplayName = workingDisplayName ?? string.Empty;
                definition.Description = workingDescription ?? string.Empty;
                if (originalStatKind != workingStatKind)
                {
                    ClearWeaponStatValues(definition, OriginalInfo);
                }

                SetWeaponIntStatValues(definition, WorkingInfo, workingNormal, workingRare, workingUnique);
            }

            public override void AcceptWorkingAsOriginal()
            {
                originalStatKind = workingStatKind;
                originalDisplayName = workingDisplayName;
                originalDescription = workingDescription;
                originalNormal = workingNormal;
                originalRare = workingRare;
                originalUnique = workingUnique;
                CardName = workingDisplayName;
                StatLabel = WorkingInfo.Label;
            }

            protected override void OnBeginEdit()
            {
                string displayName = ResolveDefinitionDisplayName(definition);
                originalDisplayName = workingDisplayName = displayName;
                originalDescription = workingDescription = definition != null ? definition.Description ?? string.Empty : string.Empty;
                CardName = workingDisplayName;
                StatLabel = WorkingInfo.Label;
            }

            private static string ResolveDefinitionDisplayName(WeaponDefinition definition)
            {
                if (definition == null)
                {
                    return string.Empty;
                }

                return string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.name : definition.DisplayName;
            }
        }

        private sealed class RowLogEntry
        {
            public string KindLabel;
            public string TargetLabel;
            public string CardName;
            public string AssetPath;
            public string StatLabel;
            public string BeforeNormal;
            public string BeforeRare;
            public string BeforeUnique;
            public string BeforeMode;
            public string AfterNormal;
            public string AfterRare;
            public string AfterUnique;
            public string AfterMode;

            public static RowLogEntry Create(BalanceRow row)
            {
                return new RowLogEntry
                {
                    KindLabel = row.KindLabel,
                    TargetLabel = row.TargetLabel,
                    CardName = row.CardName,
                    AssetPath = row.AssetPath,
                    StatLabel = row.StatLabel,
                    BeforeNormal = row.BeforeNormalText,
                    BeforeRare = row.BeforeRareText,
                    BeforeUnique = row.BeforeUniqueText,
                    BeforeMode = row.BeforeModeText,
                    AfterNormal = row.AfterNormalText,
                    AfterRare = row.AfterRareText,
                    AfterUnique = row.AfterUniqueText,
                    AfterMode = row.AfterModeText
                };
            }
        }

        private static string BuildModeLabel(bool normalPercent, bool rarePercent, bool uniquePercent)
        {
            return $"{(normalPercent ? "N%" : "N고정")} / {(rarePercent ? "R%" : "R고정")} / {(uniquePercent ? "U%" : "U고정")}";
        }
    }
}
