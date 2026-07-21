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
    public sealed class SegmentBalanceTableWindowV02 : EditorWindow
    {
        private const string WindowTitle = "세그먼트 밸런스 테이블 v0.2";
        private const string MenuPath = "JC Tool/Balance/세그먼트 밸런스 테이블 v0.2";
        private const string DefaultCatalogPath = "Assets/Segments/_Catalog/SegmentCatalog.asset";
        private const float RowHeight = 22f;
        private static readonly Color ChangedCellColor = new Color(1f, 0.82f, 0.32f, 1f);

        private static readonly SegmentAttackMoveType[] MoveTypeValues = (SegmentAttackMoveType[])Enum.GetValues(typeof(SegmentAttackMoveType));
        private static readonly SegmentAttackImpactType[] ImpactTypeValues = (SegmentAttackImpactType[])Enum.GetValues(typeof(SegmentAttackImpactType));
        private static readonly SegmentAttackAreaMode[] AreaModeValues = (SegmentAttackAreaMode[])Enum.GetValues(typeof(SegmentAttackAreaMode));
        private static readonly SegmentSupportAbilityKind[] SupportAbilityKindValues = (SegmentSupportAbilityKind[])Enum.GetValues(typeof(SegmentSupportAbilityKind));
        private static GUIStyle rightAlignedLabel;
        private static GUIStyle rightAlignedHeader;

        private SegmentCatalogAsset catalog;
        private readonly List<RowState> rows = new List<RowState>(64);
        private readonly List<WeaponOption> weaponOptions = new List<WeaponOption>(16);
        private Vector2 scroll;
        private string searchText = string.Empty;
        private bool showDirtyOnly;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            SegmentBalanceTableWindowV02 window = GetWindow<SegmentBalanceTableWindowV02>(WindowTitle);
            window.minSize = new Vector2(1180f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            if (catalog == null)
            {
                catalog = AssetDatabase.LoadAssetAtPath<SegmentCatalogAsset>(DefaultCatalogPath);
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
                    catalog = (SegmentCatalogAsset)EditorGUILayout.ObjectField("세그먼트 카탈로그", catalog, typeof(SegmentCatalogAsset), false);
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
                        ApplyAllDirtyRows();
                    }

                    if (GUILayout.Button("전체 리셋", GUILayout.Width(90f)))
                    {
                        ResetAllRows();
                    }

                    GUI.enabled = true;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    searchText = EditorGUILayout.TextField("검색", searchText ?? string.Empty);
                    showDirtyOnly = GUILayout.Toggle(showDirtyOnly, "변경만 보기", EditorStyles.toolbarButton, GUILayout.Width(90f));
                }

                EditorGUILayout.HelpBox(
                    "v0.2는 모든 세그먼트를 한 화면에 섹션으로 나눠 표시합니다. 공격형은 AttackProfile, 지원형은 프리팹의 SupportSegmentAbility.Profile을 추적해 실제로 쓰는 수치만 전용 컬럼으로 보여줍니다. DPS는 공격형 프로필 원본 기준이며 코어 스탯, 강화 카드, 지원형 버프, 쿨타임 ±10% 랜덤은 제외됩니다.",
                    MessageType.Info);
            }
        }

        private void DrawSummary()
        {
            int visibleGroups = CountVisibleWeaponGroups();
            int visibleRows = CountVisibleRows(rows);
            EditorGUILayout.LabelField($"무기 섹션 {visibleGroups}/{weaponOptions.Count}개 / 표시 행 {visibleRows}/{rows.Count}개 / 변경 {CountDirtyRows(rows)}개 / 로그 위치: {GetLogDirectory()}");
        }

        private void DrawTable()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll, true, true);

            if (catalog == null)
            {
                EditorGUILayout.HelpBox("세그먼트 카탈로그를 찾지 못했습니다.", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            int drawnGroups = 0;
            for (int i = 0; i < weaponOptions.Count; i++)
            {
                WeaponOption option = weaponOptions[i];
                List<RowState> groupRows = GetWeaponRows(option.SegmentId);
                List<RowState> visibleRows = GetVisibleRows(groupRows);
                if (visibleRows.Count == 0)
                {
                    continue;
                }

                DrawWeaponSection(option, groupRows, visibleRows);
                drawnGroups++;
                GUILayout.Space(8f);
            }

            if (drawnGroups == 0)
            {
                EditorGUILayout.HelpBox("표시할 세그먼트가 없습니다. 검색어나 변경만 보기 필터를 확인하세요.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawWeaponSection(WeaponOption option, List<RowState> groupRows, List<RowState> visibleRows)
        {
            List<ColumnId> columns = BuildColumns(groupRows);
            int dirtyCount = CountDirtyRows(groupRows);
            bool showDps = ContainsAttackRows(groupRows);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(option.Label, EditorStyles.boldLabel, GUILayout.Width(280f));
                GUILayout.Label($"행 {visibleRows.Count}/{groupRows.Count}", GUILayout.Width(84f));
                GUILayout.Label($"컬럼 {columns.Count}", GUILayout.Width(70f));
                GUILayout.Label($"변경 {dirtyCount}", GUILayout.Width(70f));
                GUILayout.FlexibleSpace();

                GUI.enabled = dirtyCount > 0;
                if (GUILayout.Button("무기 적용", GUILayout.Width(82f)))
                {
                    ApplyWeaponDirtyRows(option, groupRows);
                }

                if (GUILayout.Button("무기 리셋", GUILayout.Width(82f)))
                {
                    ResetWeaponRows(option, groupRows);
                }

                GUI.enabled = true;
            }

            DrawHeader(columns, showDps);
            for (int i = 0; i < visibleRows.Count; i++)
            {
                DrawRow(visibleRows[i], columns, showDps);
            }
        }

        private void DrawHeader(List<ColumnId> columns, bool showDps)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                Header("ID", 112f, "세그먼트 고유 ID입니다. 강화/카탈로그 조회 기준으로 사용됩니다.");
                Header("이름", 128f, "게임 UI에 표시되는 세그먼트 이름입니다.");
                Header("Lv", 34f, "세그먼트 레벨입니다. 레벨별 프로필이 별도 행으로 표시됩니다.");
                Header("분류", 54f, "공격형은 AttackProfile, 지원형은 SupportSegmentAbility.Profile을 편집합니다.");
                Header("프로필", 174f, "이 행이 실제로 수정하는 프로필 에셋입니다. 클릭하면 Project 창에서 선택됩니다.");

                for (int i = 0; i < columns.Count; i++)
                {
                    ColumnInfo info = GetColumnInfo(columns[i]);
                    if (columns[i] == ColumnId.Spacer)
                    {
                        GUILayout.Space(info.Width);
                        continue;
                    }

                    Header(info.Label, info.Width, GetColumnTooltip(columns[i]));
                }

                GUILayout.FlexibleSpace();

                if (showDps)
                {
                    RightHeader("단일 DPS", 76f, "단위: 피해/초. 현재 공격 프로필 기준 1마리 대상 예상 DPS입니다. 코어 스탯, 강화 카드, 지원형 버프, 쿨타임 랜덤은 제외됩니다.");
                    RightHeader("10마리 DPS", 86f, "단위: 피해/초. 몬스터 10마리가 뭉쳐 있을 때의 예상 DPS입니다. 범위/관통/체인 계열 비교용 추정값입니다.");
                }

                RightHeader("작업", 154f, "행 단위로 변경값 적용, 원본값 리셋, 프로필 Ping을 실행합니다.");
            }
        }

        private void DrawRow(RowState row, List<ColumnId> columns, bool showDps)
        {
            bool hasProfile = row.HasProfile;
            DpsResult dps = row.IsAttackProfile ? DpsCalculator.Calculate(row.Working) : default;
            Rect rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
            if (row.IsDirty)
            {
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, Mathf.Max(position.width, 1500f), RowHeight), new Color(1f, 0.72f, 0.15f, 0.11f));
            }

            Label(row.SegmentId, 112f);
            Label(row.DisplayName, 128f);
            Label(row.Level > 0 ? row.Level.ToString(CultureInfo.InvariantCulture) : "-", 34f);
            Label(row.ProfileKindLabel, 54f);

            if (GUILayout.Button(hasProfile ? row.ProfileAsset.name : "프로필 없음", EditorStyles.linkLabel, GUILayout.Width(174f)))
            {
                PingProfile(row);
            }

            GUI.enabled = hasProfile;
            for (int i = 0; i < columns.Count; i++)
            {
                DrawValueCell(row, columns[i]);
            }

            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            if (showDps)
            {
                RightLabel(row.IsAttackProfile ? FormatFloat(dps.SingleDps) : "-", 76f);
                RightLabel(row.IsAttackProfile ? FormatFloat(dps.Cluster10Dps) : "-", 86f);
            }

            DrawActionCell(row, hasProfile);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawActionCell(RowState row, bool hasProfile)
        {
            using (new EditorGUILayout.HorizontalScope(GUILayout.Width(154f)))
            {
                GUILayout.FlexibleSpace();

                GUI.enabled = hasProfile && row.IsDirty;
                if (GUILayout.Button(new GUIContent("적용", "이 행의 변경값을 실제 프로필/프리팹 컴포넌트 에셋에 저장합니다."), GUILayout.Width(48f)))
                {
                    ApplyRow(row);
                }

                if (GUILayout.Button(new GUIContent("리셋", "아직 적용하지 않은 이 행의 변경값을 원본값으로 되돌립니다."), GUILayout.Width(48f)))
                {
                    row.ResetWorking();
                }

                GUI.enabled = hasProfile;
                if (GUILayout.Button(new GUIContent("Ping", "이 행이 수정하는 프로필 에셋을 Project 창에서 선택합니다."), GUILayout.Width(48f)))
                {
                    PingProfile(row);
                }

                GUI.enabled = true;
            }
        }

        private void DrawValueCell(RowState row, ColumnId column)
        {
            ColumnInfo info = GetColumnInfo(column);
            if (column == ColumnId.Spacer)
            {
                GUILayout.Space(info.Width);
                return;
            }

            switch (column)
            {
                case ColumnId.MoveType:
                    row.Working.MoveType = MoveTypeCell(row.Working.MoveType, row.Original.MoveType, info.Width);
                    break;
                case ColumnId.ImpactType:
                    row.Working.ImpactType = ImpactTypeCell(row.Working.ImpactType, row.Original.ImpactType, info.Width);
                    break;
                case ColumnId.AttackAreaMode:
                    row.Working.AttackAreaMode = AreaModeCell(row.Working.AttackAreaMode, row.Original.AttackAreaMode, info.Width);
                    break;
                case ColumnId.SideConeAngle:
                    row.Working.SideConeAngle = FloatCell(row.Working.SideConeAngle, row.Original.SideConeAngle, info.Width);
                    break;
                case ColumnId.BaseDamage:
                    row.Working.BaseDamage = FloatCell(row.Working.BaseDamage, row.Original.BaseDamage, info.Width);
                    break;
                case ColumnId.Cooldown:
                    row.Working.Cooldown = FloatCell(row.Working.Cooldown, row.Original.Cooldown, info.Width);
                    break;
                case ColumnId.SearchRange:
                    row.Working.SearchRange = FloatCell(row.Working.SearchRange, row.Original.SearchRange, info.Width);
                    break;
                case ColumnId.SupportAbilityKind:
                    row.Working.SupportAbilityKind = SupportAbilityKindCell(row.Working.SupportAbilityKind, row.Original.SupportAbilityKind, info.Width);
                    break;
                case ColumnId.StartsReady:
                    row.Working.StartsReady = BoolCell(row.Working.StartsReady, row.Original.StartsReady, info.Width);
                    break;
                case ColumnId.SupportCooldown:
                    row.Working.SupportCooldown = FloatCell(row.Working.SupportCooldown, row.Original.SupportCooldown, info.Width);
                    break;
                case ColumnId.ActiveDurationSeconds:
                    row.Working.ActiveDurationSeconds = FloatCell(row.Working.ActiveDurationSeconds, row.Original.ActiveDurationSeconds, info.Width);
                    break;
                case ColumnId.EffectDurationSeconds:
                    row.Working.EffectDurationSeconds = FloatCell(row.Working.EffectDurationSeconds, row.Original.EffectDurationSeconds, info.Width);
                    break;
                case ColumnId.SupportRange:
                    row.Working.SupportRange = FloatCell(row.Working.SupportRange, row.Original.SupportRange, info.Width);
                    break;
                case ColumnId.FrontSegmentCount:
                    row.Working.FrontSegmentCount = IntCell(row.Working.FrontSegmentCount, row.Original.FrontSegmentCount, info.Width);
                    break;
                case ColumnId.BackSegmentCount:
                    row.Working.BackSegmentCount = IntCell(row.Working.BackSegmentCount, row.Original.BackSegmentCount, info.Width);
                    break;
                case ColumnId.FinalDamageMultiplier:
                    row.Working.FinalDamageMultiplier = FloatCell(row.Working.FinalDamageMultiplier, row.Original.FinalDamageMultiplier, info.Width);
                    break;
                case ColumnId.FinalAttackSpeedMultiplier:
                    row.Working.FinalAttackSpeedMultiplier = FloatCell(row.Working.FinalAttackSpeedMultiplier, row.Original.FinalAttackSpeedMultiplier, info.Width);
                    break;
                case ColumnId.IncomingDamageMultiplier:
                    row.Working.IncomingDamageMultiplier = FloatCell(row.Working.IncomingDamageMultiplier, row.Original.IncomingDamageMultiplier, info.Width);
                    break;
                case ColumnId.PickupMagnetPullStrength:
                    row.Working.PickupMagnetPullStrength = FloatCell(row.Working.PickupMagnetPullStrength, row.Original.PickupMagnetPullStrength, info.Width);
                    break;
                case ColumnId.PickupMagnetMaxPullSpeed:
                    row.Working.PickupMagnetMaxPullSpeed = FloatCell(row.Working.PickupMagnetMaxPullSpeed, row.Original.PickupMagnetMaxPullSpeed, info.Width);
                    break;
                case ColumnId.PickupMagnetCollectDistance:
                    row.Working.PickupMagnetCollectDistance = FloatCell(row.Working.PickupMagnetCollectDistance, row.Original.PickupMagnetCollectDistance, info.Width);
                    break;
                case ColumnId.HolyWaterSprayAngle:
                    row.Working.HolyWaterSprayAngle = FloatCell(row.Working.HolyWaterSprayAngle, row.Original.HolyWaterSprayAngle, info.Width);
                    break;
                case ColumnId.HolyWaterProjectileCount:
                    row.Working.HolyWaterProjectileCount = IntCell(row.Working.HolyWaterProjectileCount, row.Original.HolyWaterProjectileCount, info.Width);
                    break;
                case ColumnId.HolyWaterProjectileInterval:
                    row.Working.HolyWaterProjectileInterval = FloatCell(row.Working.HolyWaterProjectileInterval, row.Original.HolyWaterProjectileInterval, info.Width);
                    break;
                case ColumnId.HolyWaterProjectileSpeed:
                    row.Working.HolyWaterProjectileSpeed = FloatCell(row.Working.HolyWaterProjectileSpeed, row.Original.HolyWaterProjectileSpeed, info.Width);
                    break;
                case ColumnId.HolyWaterProjectileLifetime:
                    row.Working.HolyWaterProjectileLifetime = FloatCell(row.Working.HolyWaterProjectileLifetime, row.Original.HolyWaterProjectileLifetime, info.Width);
                    break;
                case ColumnId.HolyWaterProjectileStartRadius:
                    row.Working.HolyWaterProjectileStartRadius = FloatCell(row.Working.HolyWaterProjectileStartRadius, row.Original.HolyWaterProjectileStartRadius, info.Width);
                    break;
                case ColumnId.HolyWaterConeLength:
                    row.Working.HolyWaterConeLength = FloatCell(row.Working.HolyWaterConeLength, row.Original.HolyWaterConeLength, info.Width);
                    break;
                case ColumnId.HolyWaterAimTurnSpeed:
                    row.Working.HolyWaterAimTurnSpeed = FloatCell(row.Working.HolyWaterAimTurnSpeed, row.Original.HolyWaterAimTurnSpeed, info.Width);
                    break;
                case ColumnId.HolyWaterFireAngleTolerance:
                    row.Working.HolyWaterFireAngleTolerance = FloatCell(row.Working.HolyWaterFireAngleTolerance, row.Original.HolyWaterFireAngleTolerance, info.Width);
                    break;
                case ColumnId.HolyWaterDebuffTickInterval:
                    row.Working.HolyWaterDebuffTickInterval = FloatCell(row.Working.HolyWaterDebuffTickInterval, row.Original.HolyWaterDebuffTickInterval, info.Width);
                    break;
                case ColumnId.HolyWaterTargetAimHeight:
                    row.Working.HolyWaterTargetAimHeight = FloatCell(row.Working.HolyWaterTargetAimHeight, row.Original.HolyWaterTargetAimHeight, info.Width);
                    break;
                case ColumnId.ProjectileCount:
                    row.Working.ProjectileCount = IntCell(row.Working.ProjectileCount, row.Original.ProjectileCount, info.Width);
                    break;
                case ColumnId.SpreadAngle:
                    row.Working.SpreadAngle = FloatCell(row.Working.SpreadAngle, row.Original.SpreadAngle, info.Width);
                    break;
                case ColumnId.FireProjectilesSequentially:
                    row.Working.FireProjectilesSequentially = BoolCell(row.Working.FireProjectilesSequentially, row.Original.FireProjectilesSequentially, info.Width);
                    break;
                case ColumnId.ProjectileVolleySize:
                    row.Working.ProjectileVolleySize = IntCell(row.Working.ProjectileVolleySize, row.Original.ProjectileVolleySize, info.Width);
                    break;
                case ColumnId.ProjectileFireDelay:
                    row.Working.ProjectileFireDelay = FloatCell(row.Working.ProjectileFireDelay, row.Original.ProjectileFireDelay, info.Width);
                    break;
                case ColumnId.ProjectileSpeed:
                    row.Working.ProjectileSpeed = FloatCell(row.Working.ProjectileSpeed, row.Original.ProjectileSpeed, info.Width);
                    break;
                case ColumnId.ProjectileHitRadius:
                    row.Working.ProjectileHitRadius = FloatCell(row.Working.ProjectileHitRadius, row.Original.ProjectileHitRadius, info.Width);
                    break;
                case ColumnId.ProjectileLifetime:
                    row.Working.ProjectileLifetime = FloatCell(row.Working.ProjectileLifetime, row.Original.ProjectileLifetime, info.Width);
                    break;
                case ColumnId.PierceCount:
                    row.Working.PierceCount = IntCell(row.Working.PierceCount, row.Original.PierceCount, info.Width);
                    break;
                case ColumnId.PiercingProjectileDamageRatio:
                    row.Working.PiercingProjectileDamageRatio = FloatCell(row.Working.PiercingProjectileDamageRatio, row.Original.PiercingProjectileDamageRatio, info.Width);
                    break;
                case ColumnId.ArcHeight:
                    row.Working.ArcHeight = FloatCell(row.Working.ArcHeight, row.Original.ArcHeight, info.Width);
                    break;
                case ColumnId.ExplosionRadius:
                    row.Working.ExplosionRadius = FloatCell(row.Working.ExplosionRadius, row.Original.ExplosionRadius, info.Width);
                    break;
                case ColumnId.LaserDuration:
                    row.Working.LaserDuration = FloatCell(row.Working.LaserDuration, row.Original.LaserDuration, info.Width);
                    break;
                case ColumnId.LaserTickInterval:
                    row.Working.LaserTickInterval = FloatCell(row.Working.LaserTickInterval, row.Original.LaserTickInterval, info.Width);
                    break;
                case ColumnId.ChainRange:
                    row.Working.ChainRange = FloatCell(row.Working.ChainRange, row.Original.ChainRange, info.Width);
                    break;
                case ColumnId.MaxChainDepth:
                    row.Working.MaxChainDepth = IntCell(row.Working.MaxChainDepth, row.Original.MaxChainDepth, info.Width);
                    break;
                case ColumnId.ChainBranchCount:
                    row.Working.ChainBranchCount = IntCell(row.Working.ChainBranchCount, row.Original.ChainBranchCount, info.Width);
                    break;
                case ColumnId.ChainDelay:
                    row.Working.ChainDelay = FloatCell(row.Working.ChainDelay, row.Original.ChainDelay, info.Width);
                    break;
                case ColumnId.ChainDamageFalloff:
                    row.Working.ChainDamageFalloff = FloatCell(row.Working.ChainDamageFalloff, row.Original.ChainDamageFalloff, info.Width);
                    break;
                case ColumnId.SawPierceDamageRatio:
                    row.Working.SawPierceDamageRatio = FloatCell(row.Working.SawPierceDamageRatio, row.Original.SawPierceDamageRatio, info.Width);
                    break;
                case ColumnId.SawTargetMinDistanceRatio:
                    row.Working.SawTargetMinDistanceRatio = FloatCell(row.Working.SawTargetMinDistanceRatio, row.Original.SawTargetMinDistanceRatio, info.Width);
                    break;
                case ColumnId.LandingImpactRadius:
                    row.Working.LandingImpactRadius = FloatCell(row.Working.LandingImpactRadius, row.Original.LandingImpactRadius, info.Width);
                    break;
                case ColumnId.LandingRollDamageRadius:
                    row.Working.LandingRollDamageRadius = FloatCell(row.Working.LandingRollDamageRadius, row.Original.LandingRollDamageRadius, info.Width);
                    break;
                case ColumnId.LandingRollDistance:
                    row.Working.LandingRollDistance = FloatCell(row.Working.LandingRollDistance, row.Original.LandingRollDistance, info.Width);
                    break;
                case ColumnId.LandingRollDuration:
                    row.Working.LandingRollDuration = FloatCell(row.Working.LandingRollDuration, row.Original.LandingRollDuration, info.Width);
                    break;
                default:
                    Label("-", info.Width);
                    break;
            }
        }

        private void RefreshRows()
        {
            rows.Clear();
            weaponOptions.Clear();

            if (catalog == null || catalog.Segments == null)
            {
                return;
            }

            HashSet<string> seenSegments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < catalog.Segments.Length; i++)
            {
                SegmentDefinition definition = catalog.Segments[i];
                if (definition == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(definition.NormalizedId) && seenSegments.Add(definition.NormalizedId))
                {
                    weaponOptions.Add(new WeaponOption(definition.NormalizedId, string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.NormalizedId : definition.DisplayName));
                }

                if (definition.Levels == null || definition.Levels.Length == 0)
                {
                    rows.Add(RowState.CreateMissing(definition, 0));
                    continue;
                }

                for (int levelIndex = 0; levelIndex < definition.Levels.Length; levelIndex++)
                {
                    rows.Add(RowState.Create(definition, definition.Levels[levelIndex]));
                }
            }
        }

        private List<RowState> GetWeaponRows(string segmentId)
        {
            List<RowState> selected = new List<RowState>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].SegmentId, segmentId, StringComparison.OrdinalIgnoreCase))
                {
                    selected.Add(rows[i]);
                }
            }

            return selected;
        }

        private List<RowState> GetVisibleRows(List<RowState> sourceRows)
        {
            List<RowState> visibleRows = new List<RowState>(sourceRows.Count);
            for (int i = 0; i < sourceRows.Count; i++)
            {
                if (ShouldShowRow(sourceRows[i]))
                {
                    visibleRows.Add(sourceRows[i]);
                }
            }

            return visibleRows;
        }

        private List<ColumnId> BuildColumns(List<RowState> sourceRows)
        {
            List<ColumnId> columns = new List<ColumnId>();

            bool hasAttack = false;
            bool hasSupport = false;
            bool usesSideCones = false;
            bool usesProjectile = false;
            bool usesPierce = false;
            bool usesProjectileVolley = false;
            bool usesExplosion = false;
            bool usesArc = false;
            bool usesLaser = false;
            bool usesChain = false;
            bool usesSaw = false;
            bool usesFlame = false;
            bool usesLandingRoll = false;
            bool usesSupportRange = false;
            bool usesSupportSegmentCount = false;
            bool usesFinalDamageMultiplier = false;
            bool usesFinalAttackSpeedMultiplier = false;
            bool usesIncomingDamageMultiplier = false;
            bool usesPickupMagnetRuntime = false;
            bool usesHolyWaterRuntime = false;

            for (int i = 0; i < sourceRows.Count; i++)
            {
                RowState row = sourceRows[i];
                if (!row.HasProfile)
                {
                    continue;
                }

                BalanceValues values = row.Working;
                if (row.IsAttackProfile)
                {
                    hasAttack = true;
                    usesSideCones |= values.AttackAreaMode == SegmentAttackAreaMode.SideCones;
                    usesProjectile |= values.MoveType == SegmentAttackMoveType.StraightProjectile
                        || values.MoveType == SegmentAttackMoveType.PiercingProjectile
                        || values.MoveType == SegmentAttackMoveType.ArcProjectile
                        || values.MoveType == SegmentAttackMoveType.HomingProjectile
                        || values.MoveType == SegmentAttackMoveType.SawBounceProjectile
                        || values.MoveType == SegmentAttackMoveType.ExpandingFlameSphere;
                    usesPierce |= values.MoveType == SegmentAttackMoveType.PiercingProjectile || values.ImpactType == SegmentAttackImpactType.PierceDamage;
                    usesProjectileVolley |= values.FireProjectilesSequentially && values.ProjectileVolleySize > 1;
                    usesExplosion |= values.ImpactType == SegmentAttackImpactType.ExplosionArea || values.MoveType == SegmentAttackMoveType.ExpandingFlameSphere;
                    usesArc |= values.MoveType == SegmentAttackMoveType.ArcProjectile;
                    usesLaser |= values.MoveType == SegmentAttackMoveType.Laser;
                    usesChain |= values.MoveType == SegmentAttackMoveType.ChainLightning;
                    usesSaw |= values.MoveType == SegmentAttackMoveType.SawBounceProjectile;
                    usesFlame |= values.MoveType == SegmentAttackMoveType.ExpandingFlameSphere;
                    usesLandingRoll |= values.RollAfterArcLanding || values.LandingRollDistance > 0f;
                }

                if (row.IsSupportProfile)
                {
                    hasSupport = true;
                    usesSupportRange |= values.SupportAbilityKind == SegmentSupportAbilityKind.PickupMagnet
                        || values.SupportAbilityKind == SegmentSupportAbilityKind.FreezeArea
                        || values.SupportRange > 0f;
                    usesSupportSegmentCount |= values.SupportAbilityKind == SegmentSupportAbilityKind.FinalDamageBuff
                        || values.SupportAbilityKind == SegmentSupportAbilityKind.FinalAttackSpeedBuff
                        || values.FrontSegmentCount > 0
                        || values.BackSegmentCount > 0;
                    usesFinalDamageMultiplier |= values.SupportAbilityKind == SegmentSupportAbilityKind.FinalDamageBuff
                        || !Approximately(values.FinalDamageMultiplier, 1f);
                    usesFinalAttackSpeedMultiplier |= values.SupportAbilityKind == SegmentSupportAbilityKind.FinalAttackSpeedBuff
                        || !Approximately(values.FinalAttackSpeedMultiplier, 1f);
                    usesIncomingDamageMultiplier |= values.SupportAbilityKind == SegmentSupportAbilityKind.HolyWaterVulnerabilitySpray
                        || !Approximately(values.IncomingDamageMultiplier, 1f);
                    usesPickupMagnetRuntime |= values.SupportAbilityKind == SegmentSupportAbilityKind.PickupMagnet;
                    usesHolyWaterRuntime |= values.SupportAbilityKind == SegmentSupportAbilityKind.HolyWaterVulnerabilitySpray;
                }
            }

            if (hasAttack)
            {
                bool hasAttackUniqueColumns = usesSideCones
                    || usesProjectile
                    || usesPierce
                    || usesProjectileVolley
                    || usesArc
                    || usesExplosion
                    || usesLaser
                    || usesChain
                    || usesSaw
                    || usesFlame
                    || usesLandingRoll;

                columns.Add(ColumnId.MoveType);
                columns.Add(ColumnId.ImpactType);
                columns.Add(ColumnId.BaseDamage);
                columns.Add(ColumnId.Cooldown);
                columns.Add(ColumnId.SearchRange);

                if (hasAttackUniqueColumns)
                {
                    columns.Add(ColumnId.Spacer);
                }

                if (usesSideCones)
                {
                    columns.Add(ColumnId.AttackAreaMode);
                    columns.Add(ColumnId.SideConeAngle);
                }

                if (usesProjectile)
                {
                    columns.Add(ColumnId.ProjectileCount);
                    columns.Add(ColumnId.SpreadAngle);
                    columns.Add(ColumnId.FireProjectilesSequentially);
                    if (usesProjectileVolley)
                    {
                        columns.Add(ColumnId.ProjectileVolleySize);
                    }

                    columns.Add(ColumnId.ProjectileFireDelay);
                    columns.Add(ColumnId.ProjectileSpeed);
                    columns.Add(ColumnId.ProjectileHitRadius);
                    columns.Add(ColumnId.ProjectileLifetime);
                }

                if (usesPierce)
                {
                    columns.Add(ColumnId.PierceCount);
                    columns.Add(ColumnId.PiercingProjectileDamageRatio);
                }

                if (usesArc)
                {
                    columns.Add(ColumnId.ArcHeight);
                }

                if (usesExplosion)
                {
                    columns.Add(ColumnId.ExplosionRadius);
                }

                if (usesLaser || usesFlame)
                {
                    columns.Add(ColumnId.LaserDuration);
                    columns.Add(ColumnId.LaserTickInterval);
                }

                if (usesChain || usesSaw)
                {
                    columns.Add(ColumnId.ChainRange);
                    columns.Add(ColumnId.MaxChainDepth);
                }

                if (usesChain)
                {
                    columns.Add(ColumnId.ChainBranchCount);
                    columns.Add(ColumnId.ChainDelay);
                    columns.Add(ColumnId.ChainDamageFalloff);
                }

                if (usesSaw)
                {
                    columns.Add(ColumnId.SawPierceDamageRatio);
                    columns.Add(ColumnId.SawTargetMinDistanceRatio);
                }

                if (usesLandingRoll)
                {
                    columns.Add(ColumnId.LandingImpactRadius);
                    columns.Add(ColumnId.LandingRollDamageRadius);
                    columns.Add(ColumnId.LandingRollDistance);
                    columns.Add(ColumnId.LandingRollDuration);
                }
            }

            if (hasSupport)
            {
                bool hasSupportUniqueColumns = usesSupportRange
                    || usesSupportSegmentCount
                    || usesFinalDamageMultiplier
                    || usesFinalAttackSpeedMultiplier
                    || usesIncomingDamageMultiplier
                    || usesPickupMagnetRuntime
                    || usesHolyWaterRuntime;

                columns.Add(ColumnId.SupportAbilityKind);
                columns.Add(ColumnId.StartsReady);
                columns.Add(ColumnId.SupportCooldown);
                columns.Add(ColumnId.ActiveDurationSeconds);
                columns.Add(ColumnId.EffectDurationSeconds);

                if (hasSupportUniqueColumns)
                {
                    columns.Add(ColumnId.Spacer);
                }

                if (usesSupportRange)
                {
                    columns.Add(ColumnId.SupportRange);
                }

                if (usesSupportSegmentCount)
                {
                    columns.Add(ColumnId.FrontSegmentCount);
                    columns.Add(ColumnId.BackSegmentCount);
                }

                if (usesFinalDamageMultiplier)
                {
                    columns.Add(ColumnId.FinalDamageMultiplier);
                }

                if (usesFinalAttackSpeedMultiplier)
                {
                    columns.Add(ColumnId.FinalAttackSpeedMultiplier);
                }

                if (usesIncomingDamageMultiplier)
                {
                    columns.Add(ColumnId.IncomingDamageMultiplier);
                }

                if (usesPickupMagnetRuntime)
                {
                    columns.Add(ColumnId.PickupMagnetPullStrength);
                    columns.Add(ColumnId.PickupMagnetMaxPullSpeed);
                    columns.Add(ColumnId.PickupMagnetCollectDistance);
                }

                if (usesHolyWaterRuntime)
                {
                    columns.Add(ColumnId.HolyWaterSprayAngle);
                    columns.Add(ColumnId.HolyWaterProjectileLifetime);
                    columns.Add(ColumnId.HolyWaterConeLength);
                    columns.Add(ColumnId.HolyWaterAimTurnSpeed);
                    columns.Add(ColumnId.HolyWaterFireAngleTolerance);
                    columns.Add(ColumnId.HolyWaterDebuffTickInterval);
                    columns.Add(ColumnId.HolyWaterTargetAimHeight);
                }
            }

            return columns;
        }

        private bool ShouldShowRow(RowState row)
        {
            if (showDirtyOnly && !row.IsDirty)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            string query = searchText.Trim();
            return Contains(row.SegmentId, query)
                || Contains(row.DisplayName, query)
                || Contains(row.ProfileKindLabel, query)
                || (row.ProfileAsset != null && Contains(row.ProfileAsset.name, query));
        }

        private void ApplyAllDirtyRows()
        {
            List<RowState> dirtyRows = CollectDirtyRows(rows);
            if (dirtyRows.Count == 0)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("전체 적용", $"모든 무기의 변경 {dirtyRows.Count}개 행을 실제 에셋에 적용할까요?", "적용", "취소"))
            {
                return;
            }

            ApplyRows("Apply All Weapon Tables v0.2", dirtyRows);
        }

        private void ApplyWeaponDirtyRows(WeaponOption option, List<RowState> groupRows)
        {
            List<RowState> dirtyRows = CollectDirtyRows(groupRows);
            if (dirtyRows.Count == 0)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("무기 적용", $"{option.Label} 변경 {dirtyRows.Count}개 행을 실제 에셋에 적용할까요?", "적용", "취소"))
            {
                return;
            }

            ApplyRows("Apply Weapon Section v0.2", dirtyRows);
        }

        private void ApplyRow(RowState row)
        {
            if (!row.HasProfile || !row.IsDirty)
            {
                return;
            }

            ApplyRows("Apply Row v0.2", new List<RowState> { row });
        }

        private void ApplyRows(string action, List<RowState> dirtyRows)
        {
            List<RowLogEntry> entries = new List<RowLogEntry>(dirtyRows.Count);
            for (int i = 0; i < dirtyRows.Count; i++)
            {
                RowState row = dirtyRows[i];
                BalanceValues before = row.Original.Clone();
                DpsResult beforeDps = DpsCalculator.Calculate(before);
                DpsResult afterDps = DpsCalculator.Calculate(row.Working);

                UnityEngine.Object[] targets = row.GetApplyTargets();
                Undo.RecordObjects(targets, "Apply Segment Balance Table v0.2");
                row.Working.ApplyTo(row);
                for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
                {
                    EditorUtility.SetDirty(targets[targetIndex]);
                }
                entries.Add(new RowLogEntry(row, before, row.Working.Clone(), beforeDps, afterDps));
                row.Original = row.Working.Clone();
            }

            AssetDatabase.SaveAssets();
            WriteApplyLog(action, entries);
        }

        private void ResetWeaponRows(WeaponOption option, List<RowState> groupRows)
        {
            if (CountDirtyRows(groupRows) == 0)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("무기 리셋", $"{option.Label}의 적용하지 않은 변경값을 원본값으로 되돌릴까요?", "리셋", "취소"))
            {
                return;
            }

            for (int i = 0; i < groupRows.Count; i++)
            {
                groupRows[i].ResetWorking();
            }
        }

        private void ResetAllRows()
        {
            if (CountDirtyRows(rows) == 0)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("전체 리셋", "모든 무기의 적용하지 않은 변경값을 원본값으로 되돌릴까요?", "리셋", "취소"))
            {
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].ResetWorking();
            }
        }

        private int CountDirtyRows(List<RowState> sourceRows)
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

        private bool ContainsAttackRows(List<RowState> sourceRows)
        {
            for (int i = 0; i < sourceRows.Count; i++)
            {
                if (sourceRows[i].IsAttackProfile)
                {
                    return true;
                }
            }

            return false;
        }

        private int CountVisibleRows(List<RowState> sourceRows)
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

        private int CountVisibleWeaponGroups()
        {
            int count = 0;
            for (int i = 0; i < weaponOptions.Count; i++)
            {
                if (CountVisibleRows(GetWeaponRows(weaponOptions[i].SegmentId)) > 0)
                {
                    count++;
                }
            }

            return count;
        }

        private List<RowState> CollectDirtyRows(List<RowState> sourceRows)
        {
            List<RowState> dirtyRows = new List<RowState>();
            for (int i = 0; i < sourceRows.Count; i++)
            {
                if (sourceRows[i].HasProfile && sourceRows[i].IsDirty)
                {
                    dirtyRows.Add(sourceRows[i]);
                }
            }

            return dirtyRows;
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
            string fileName = $"SegmentBalance_v02_{now:yyyy-MM-dd_HH-mm-ss-fff}.log";
            string path = Path.Combine(directory, fileName);

            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine($"[{now:yyyy-MM-dd HH:mm:ss}]");
            builder.AppendLine($"Action: {action}");
            builder.AppendLine($"ChangedRows: {entries.Count}");
            builder.AppendLine();

            for (int i = 0; i < entries.Count; i++)
            {
                RowLogEntry entry = entries[i];
                builder.AppendLine($"Segment: {entry.Row.SegmentId}");
                builder.AppendLine($"DisplayName: {entry.Row.DisplayName}");
                builder.AppendLine($"Level: {entry.Row.Level}");
                builder.AppendLine($"ProfileKind: {entry.Row.ProfileKindLabel}");
                builder.AppendLine($"Profile: {AssetDatabase.GetAssetPath(entry.Row.ProfileAsset)}");
                AppendDiffs(builder, entry.Before, entry.After);
                if (entry.Row.IsAttackProfile)
                {
                    builder.AppendLine($"SingleDPS: {FormatFloat(entry.BeforeDps.SingleDps)} -> {FormatFloat(entry.AfterDps.SingleDps)}");
                    builder.AppendLine($"Cluster10DPS: {FormatFloat(entry.BeforeDps.Cluster10Dps)} -> {FormatFloat(entry.AfterDps.Cluster10Dps)}");
                }
                builder.AppendLine();
            }

            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
            Debug.Log($"[SegmentBalanceTable v0.2] Balance log written: {path}");
        }

        private static void AppendDiffs(StringBuilder builder, BalanceValues before, BalanceValues after)
        {
            AppendDiff(builder, "프로필 타입", before.ProfileKind, after.ProfileKind);
            AppendDiff(builder, "공격 방식", TranslateMoveType(before.MoveType), TranslateMoveType(after.MoveType));
            AppendDiff(builder, "피해 방식", TranslateImpactType(before.ImpactType), TranslateImpactType(after.ImpactType));
            AppendDiff(builder, "공격 범위", TranslateAreaMode(before.AttackAreaMode), TranslateAreaMode(after.AttackAreaMode));
            AppendDiff(builder, "부채꼴 각도", before.SideConeAngle, after.SideConeAngle);
            AppendDiff(builder, "기본 피해", before.BaseDamage, after.BaseDamage);
            AppendDiff(builder, "쿨타임", before.Cooldown, after.Cooldown);
            AppendDiff(builder, "사거리", before.SearchRange, after.SearchRange);
            AppendDiff(builder, "발사 수", before.ProjectileCount, after.ProjectileCount);
            AppendDiff(builder, "산탄 각도", before.SpreadAngle, after.SpreadAngle);
            AppendDiff(builder, "순차 발사", before.FireProjectilesSequentially, after.FireProjectilesSequentially);
            AppendDiff(builder, "묶음 발사 수", before.ProjectileVolleySize, after.ProjectileVolleySize);
            AppendDiff(builder, "발사 간격", before.ProjectileFireDelay, after.ProjectileFireDelay);
            AppendDiff(builder, "투사체 속도", before.ProjectileSpeed, after.ProjectileSpeed);
            AppendDiff(builder, "명중 반경", before.ProjectileHitRadius, after.ProjectileHitRadius);
            AppendDiff(builder, "투사체 수명", before.ProjectileLifetime, after.ProjectileLifetime);
            AppendDiff(builder, "관통 수", before.PierceCount, after.PierceCount);
            AppendDiff(builder, "관통 피해 비율", before.PiercingProjectileDamageRatio, after.PiercingProjectileDamageRatio);
            AppendDiff(builder, "곡사 높이", before.ArcHeight, after.ArcHeight);
            AppendDiff(builder, "폭발 반경", before.ExplosionRadius, after.ExplosionRadius);
            AppendDiff(builder, "레이저 지속 시간", before.LaserDuration, after.LaserDuration);
            AppendDiff(builder, "틱 간격", before.LaserTickInterval, after.LaserTickInterval);
            AppendDiff(builder, "체인 거리", before.ChainRange, after.ChainRange);
            AppendDiff(builder, "체인 단계", before.MaxChainDepth, after.MaxChainDepth);
            AppendDiff(builder, "체인 분기", before.ChainBranchCount, after.ChainBranchCount);
            AppendDiff(builder, "체인 지연", before.ChainDelay, after.ChainDelay);
            AppendDiff(builder, "체인 피해 유지율", before.ChainDamageFalloff, after.ChainDamageFalloff);
            AppendDiff(builder, "톱날 관통 피해 비율", before.SawPierceDamageRatio, after.SawPierceDamageRatio);
            AppendDiff(builder, "톱날 최소거리 비율", before.SawTargetMinDistanceRatio, after.SawTargetMinDistanceRatio);
            AppendDiff(builder, "착지 충격 반경", before.LandingImpactRadius, after.LandingImpactRadius);
            AppendDiff(builder, "구르기 피해 반경", before.LandingRollDamageRadius, after.LandingRollDamageRadius);
            AppendDiff(builder, "구르기 거리", before.LandingRollDistance, after.LandingRollDistance);
            AppendDiff(builder, "구르기 시간", before.LandingRollDuration, after.LandingRollDuration);
            AppendDiff(builder, "지원 능력", TranslateSupportAbilityKind(before.SupportAbilityKind), TranslateSupportAbilityKind(after.SupportAbilityKind));
            AppendDiff(builder, "시작 즉시 준비", before.StartsReady, after.StartsReady);
            AppendDiff(builder, "지원 쿨타임", before.SupportCooldown, after.SupportCooldown);
            AppendDiff(builder, "발동 시간", before.ActiveDurationSeconds, after.ActiveDurationSeconds);
            AppendDiff(builder, "효과 지속", before.EffectDurationSeconds, after.EffectDurationSeconds);
            AppendDiff(builder, "지원 범위", before.SupportRange, after.SupportRange);
            AppendDiff(builder, "앞 적용 칸", before.FrontSegmentCount, after.FrontSegmentCount);
            AppendDiff(builder, "뒤 적용 칸", before.BackSegmentCount, after.BackSegmentCount);
            AppendDiff(builder, "최종 피해 배율", before.FinalDamageMultiplier, after.FinalDamageMultiplier);
            AppendDiff(builder, "최종 공속 배율", before.FinalAttackSpeedMultiplier, after.FinalAttackSpeedMultiplier);
            AppendDiff(builder, "받는 피해 배율", before.IncomingDamageMultiplier, after.IncomingDamageMultiplier);
            AppendDiff(builder, "자석 끌어당김 힘", before.PickupMagnetPullStrength, after.PickupMagnetPullStrength);
            AppendDiff(builder, "자석 최대 속도", before.PickupMagnetMaxPullSpeed, after.PickupMagnetMaxPullSpeed);
            AppendDiff(builder, "자석 획득 거리", before.PickupMagnetCollectDistance, after.PickupMagnetCollectDistance);
            AppendDiff(builder, "성수 원뿔각", before.HolyWaterSprayAngle, after.HolyWaterSprayAngle);
            AppendDiff(builder, "성수 표시 시간", before.HolyWaterProjectileLifetime, after.HolyWaterProjectileLifetime);
            AppendDiff(builder, "성수 원뿔 길이", before.HolyWaterConeLength, after.HolyWaterConeLength);
            AppendDiff(builder, "성수 조준 속도", before.HolyWaterAimTurnSpeed, after.HolyWaterAimTurnSpeed);
            AppendDiff(builder, "성수 발사 허용각", before.HolyWaterFireAngleTolerance, after.HolyWaterFireAngleTolerance);
            AppendDiff(builder, "성수 디버프 틱", before.HolyWaterDebuffTickInterval, after.HolyWaterDebuffTickInterval);
            AppendDiff(builder, "성수 조준 높이", before.HolyWaterTargetAimHeight, after.HolyWaterTargetAimHeight);
        }

        private static void AppendDiff<T>(StringBuilder builder, string label, T before, T after)
        {
            if (!EqualityComparer<T>.Default.Equals(before, after))
            {
                builder.AppendLine($"{label}: {before} -> {after}");
            }
        }

        private static void AppendDiff(StringBuilder builder, string label, float before, float after)
        {
            if (!Approximately(before, after))
            {
                builder.AppendLine($"{label}: {FormatFloat(before)} -> {FormatFloat(after)}");
            }
        }

        private static void PingProfile(RowState row)
        {
            if (row.ProfileAsset == null)
            {
                return;
            }

            Selection.activeObject = row.ProfileAsset;
            EditorGUIUtility.PingObject(row.ProfileAsset);
        }

        private static ColumnInfo GetColumnInfo(ColumnId id)
        {
            switch (id)
            {
                case ColumnId.MoveType:
                    return new ColumnInfo("공격 방식", 118f);
                case ColumnId.ImpactType:
                    return new ColumnInfo("피해 방식", 108f);
                case ColumnId.AttackAreaMode:
                    return new ColumnInfo("공격 범위", 92f);
                case ColumnId.SideConeAngle:
                    return new ColumnInfo("부채꼴", 58f);
                case ColumnId.BaseDamage:
                    return new ColumnInfo("피해", 58f);
                case ColumnId.Cooldown:
                    return new ColumnInfo("쿨타임", 60f);
                case ColumnId.SearchRange:
                    return new ColumnInfo("사거리", 60f);
                case ColumnId.SupportAbilityKind:
                    return new ColumnInfo("지원 능력", 138f);
                case ColumnId.StartsReady:
                    return new ColumnInfo("즉시", 42f);
                case ColumnId.SupportCooldown:
                    return new ColumnInfo("쿨타임", 58f);
                case ColumnId.ActiveDurationSeconds:
                    return new ColumnInfo("발동", 54f);
                case ColumnId.EffectDurationSeconds:
                    return new ColumnInfo("효과", 54f);
                case ColumnId.SupportRange:
                    return new ColumnInfo("지원범위", 66f);
                case ColumnId.FrontSegmentCount:
                    return new ColumnInfo("앞칸", 48f);
                case ColumnId.BackSegmentCount:
                    return new ColumnInfo("뒤칸", 48f);
                case ColumnId.FinalDamageMultiplier:
                    return new ColumnInfo("피해배율", 66f);
                case ColumnId.FinalAttackSpeedMultiplier:
                    return new ColumnInfo("공속배율", 66f);
                case ColumnId.IncomingDamageMultiplier:
                    return new ColumnInfo("받피배율", 66f);
                case ColumnId.PickupMagnetPullStrength:
                    return new ColumnInfo("자석힘", 58f);
                case ColumnId.PickupMagnetMaxPullSpeed:
                    return new ColumnInfo("자석속도", 66f);
                case ColumnId.PickupMagnetCollectDistance:
                    return new ColumnInfo("획득거리", 66f);
                case ColumnId.HolyWaterSprayAngle:
                    return new ColumnInfo("원뿔각", 58f);
                case ColumnId.HolyWaterProjectileSpeed:
                    return new ColumnInfo("성수속도", 66f);
                case ColumnId.HolyWaterProjectileLifetime:
                    return new ColumnInfo("표시시간", 66f);
                case ColumnId.HolyWaterProjectileStartRadius:
                    return new ColumnInfo("시작반경", 66f);
                case ColumnId.HolyWaterConeLength:
                    return new ColumnInfo("원뿔길이", 66f);
                case ColumnId.HolyWaterAimTurnSpeed:
                    return new ColumnInfo("조준속도", 66f);
                case ColumnId.HolyWaterFireAngleTolerance:
                    return new ColumnInfo("허용각", 58f);
                case ColumnId.HolyWaterDebuffTickInterval:
                    return new ColumnInfo("디버프틱", 66f);
                case ColumnId.HolyWaterTargetAimHeight:
                    return new ColumnInfo("조준높이", 66f);
                case ColumnId.ProjectileCount:
                    return new ColumnInfo("발사 수", 54f);
                case ColumnId.SpreadAngle:
                    return new ColumnInfo("산탄", 54f);
                case ColumnId.FireProjectilesSequentially:
                    return new ColumnInfo("순차", 42f);
                case ColumnId.ProjectileVolleySize:
                    return new ColumnInfo("묶음", 50f);
                case ColumnId.ProjectileFireDelay:
                    return new ColumnInfo("발사간격", 66f);
                case ColumnId.ProjectileSpeed:
                    return new ColumnInfo("속도", 58f);
                case ColumnId.ProjectileHitRadius:
                    return new ColumnInfo("명중반경", 66f);
                case ColumnId.ProjectileLifetime:
                    return new ColumnInfo("수명", 54f);
                case ColumnId.PierceCount:
                    return new ColumnInfo("관통", 50f);
                case ColumnId.PiercingProjectileDamageRatio:
                    return new ColumnInfo("관통피해", 66f);
                case ColumnId.ArcHeight:
                    return new ColumnInfo("곡사높이", 66f);
                case ColumnId.ExplosionRadius:
                    return new ColumnInfo("폭발반경", 66f);
                case ColumnId.LaserDuration:
                    return new ColumnInfo("지속시간", 66f);
                case ColumnId.LaserTickInterval:
                    return new ColumnInfo("틱간격", 58f);
                case ColumnId.ChainRange:
                    return new ColumnInfo("체인거리", 66f);
                case ColumnId.MaxChainDepth:
                    return new ColumnInfo("체인단계", 66f);
                case ColumnId.ChainBranchCount:
                    return new ColumnInfo("체인분기", 66f);
                case ColumnId.ChainDelay:
                    return new ColumnInfo("체인지연", 66f);
                case ColumnId.ChainDamageFalloff:
                    return new ColumnInfo("피해유지", 66f);
                case ColumnId.SawPierceDamageRatio:
                    return new ColumnInfo("톱날관통", 66f);
                case ColumnId.SawTargetMinDistanceRatio:
                    return new ColumnInfo("최소거리", 66f);
                case ColumnId.LandingImpactRadius:
                    return new ColumnInfo("착지반경", 66f);
                case ColumnId.LandingRollDamageRadius:
                    return new ColumnInfo("구름반경", 66f);
                case ColumnId.LandingRollDistance:
                    return new ColumnInfo("구름거리", 66f);
                case ColumnId.LandingRollDuration:
                    return new ColumnInfo("구름시간", 66f);
                case ColumnId.Spacer:
                    return new ColumnInfo(string.Empty, 16f);
                default:
                    return new ColumnInfo(id.ToString(), 60f);
            }
        }

        private static string GetColumnTooltip(ColumnId id)
        {
            switch (id)
            {
                case ColumnId.MoveType:
                    return "공격이 날아가거나 처리되는 방식입니다. 직선, 곡사, 레이저, 체인, 톱날, 화염구 같은 큰 동작 차이를 정합니다.";
                case ColumnId.ImpactType:
                    return "명중 시 피해 처리 방식입니다. 직접 피해, 관통 피해, 폭발 범위, 지속 피해, 체인 피해 계산에 사용됩니다.";
                case ColumnId.AttackAreaMode:
                    return "타겟을 찾는 공격 가능 영역입니다. 전체 원형 또는 좌우 부채꼴 같은 제한을 둡니다.";
                case ColumnId.SideConeAngle:
                    return "단위: 도. 좌우 부채꼴 공격 범위의 각도입니다. 값이 클수록 옆 방향 타겟을 더 넓게 잡습니다.";
                case ColumnId.BaseDamage:
                    return "단위: 피해량. 공격 1회 또는 틱 1회의 기본 피해입니다. 강화 카드와 지원 버프가 적용되기 전 원본 값입니다.";
                case ColumnId.Cooldown:
                    return "단위: 초. 공격 사이 기본 대기 시간입니다. 실제 런타임에서는 약간의 랜덤 쿨타임 흔들림이 추가됩니다.";
                case ColumnId.SearchRange:
                    return "단위: m. 공격 대상 검색 거리입니다. 이 거리 안의 몬스터만 타겟 후보가 됩니다.";
                case ColumnId.SupportAbilityKind:
                    return "지원형 세그먼트의 능력 종류입니다. 피해 버프, 자석, 동결, 공속 버프, 성수 취약 분사를 구분합니다.";
                case ColumnId.StartsReady:
                    return "체인에 붙은 직후 쿨타임 없이 바로 발동 가능한지 여부입니다.";
                case ColumnId.SupportCooldown:
                    return "단위: 초. 지원 능력이 다시 발동되기까지 걸리는 기본 시간입니다.";
                case ColumnId.ActiveDurationSeconds:
                    return "단위: 초. 지원 능력이 발동 상태로 유지되는 시간입니다. 성수 분사는 발사 시퀀스 시간보다 짧아지지 않습니다.";
                case ColumnId.EffectDurationSeconds:
                    return "단위: 초. 동결, 취약 같은 대상에게 남는 효과의 지속 시간입니다.";
                case ColumnId.SupportRange:
                    return "단위: m. 지원 효과가 닿는 거리입니다. 자석, 동결, 성수 취약 분사 같은 범위형 지원에 사용됩니다.";
                case ColumnId.FrontSegmentCount:
                    return "단위: 칸. 지원 세그먼트 앞쪽으로 몇 칸까지 아군 세그먼트에 버프를 줄지 정합니다.";
                case ColumnId.BackSegmentCount:
                    return "단위: 칸. 지원 세그먼트 뒤쪽으로 몇 칸까지 아군 세그먼트에 버프를 줄지 정합니다.";
                case ColumnId.FinalDamageMultiplier:
                    return "단위: 배율. 아군 공격의 최종 피해 배율입니다. 1.3이면 최종 피해가 30% 증가합니다.";
                case ColumnId.FinalAttackSpeedMultiplier:
                    return "단위: 배율. 아군 공격 속도 배율입니다. 1.3이면 쿨타임 소모가 30% 빨라집니다.";
                case ColumnId.IncomingDamageMultiplier:
                    return "단위: 배율. 대상이 받는 피해 배율입니다. 성수 취약 효과에서 1.5면 대상이 피해를 50% 더 받습니다.";
                case ColumnId.PickupMagnetPullStrength:
                    return "단위: m/s^2. 보상 자석이 픽업 아이템을 끌어당기는 가속도 성격의 힘입니다. 값이 클수록 더 강하게 당깁니다.";
                case ColumnId.PickupMagnetMaxPullSpeed:
                    return "단위: m/s. 보상 자석으로 당겨지는 픽업 아이템의 최대 이동 속도입니다.";
                case ColumnId.PickupMagnetCollectDistance:
                    return "단위: m. 픽업 아이템이 이 거리 안에 들어오면 획득 처리됩니다.";
                case ColumnId.HolyWaterSprayAngle:
                    return "단위: 도. 투명 성수 원뿔 범위의 각도입니다. 값이 클수록 넓은 적에게 받피증을 묻힙니다.";
                case ColumnId.HolyWaterProjectileSpeed:
                    return "이전 분사 방식용 필드입니다. 현재 원뿔 범위 표시에는 사용하지 않습니다.";
                case ColumnId.HolyWaterProjectileLifetime:
                    return "단위: 초. 임시 투명 원뿔 범위가 보이는 시간입니다.";
                case ColumnId.HolyWaterProjectileStartRadius:
                    return "이전 분사 방식용 필드입니다. 현재 원뿔은 머즐 위치를 꼭짓점으로 사용합니다.";
                case ColumnId.HolyWaterConeLength:
                    return "단위: m. 0이면 지원 프로필 Range를 원뿔 길이로 사용합니다.";
                case ColumnId.HolyWaterAimTurnSpeed:
                    return "단위: 도/초. 성수발사기 헤드가 적을 향해 회전하는 속도입니다. 0이면 즉시 조준합니다.";
                case ColumnId.HolyWaterFireAngleTolerance:
                    return "단위: 도. 현재 조준 방향과 목표 방향 차이가 이 값 이하일 때 투명 원뿔 범위를 표시합니다.";
                case ColumnId.HolyWaterDebuffTickInterval:
                    return "단위: 초. 투명 원뿔 범위 안의 적에게 받피증 디버프를 다시 거는 간격입니다.";
                case ColumnId.HolyWaterTargetAimHeight:
                    return "단위: m. 콜라이더가 없는 적을 조준할 때 사용하는 목표 높이 보정입니다.";
                case ColumnId.ProjectileCount:
                    return "단위: 개. 공격 1회에 발사되는 투사체 개수입니다.";
                case ColumnId.SpreadAngle:
                    return "단위: 도. 여러 투사체가 퍼지는 각도입니다. 값이 클수록 넓게 산탄됩니다.";
                case ColumnId.FireProjectilesSequentially:
                    return "여러 투사체를 한 번에 쏘지 않고 순차적으로 발사할지 여부입니다.";
                case ColumnId.ProjectileVolleySize:
                    return "단위: 개. 순차 발사일 때 한 번에 묶어서 발사되는 투사체 수입니다. 3이면 3발 세트로 발사합니다.";
                case ColumnId.ProjectileFireDelay:
                    return "단위: 초. 순차 발사일 때 투사체 사이에 들어가는 발사 간격입니다.";
                case ColumnId.ProjectileSpeed:
                    return "단위: m/s. 투사체가 이동하는 속도입니다.";
                case ColumnId.ProjectileHitRadius:
                    return "단위: m. 투사체가 적에게 명중했다고 판단하는 반경입니다.";
                case ColumnId.ProjectileLifetime:
                    return "단위: 초. 투사체가 사라지기 전까지 유지되는 시간입니다.";
                case ColumnId.PierceCount:
                    return "단위: 회. 관통형 공격이 최대 몇 번의 대상 충돌을 처리할지 정합니다.";
                case ColumnId.PiercingProjectileDamageRatio:
                    return "단위: 배율. 일반 관통탄이 적에게 닿았을 때 적용되는 피해 비율입니다. 0.7이면 기본 피해의 70%입니다.";
                case ColumnId.ArcHeight:
                    return "단위: m. 곡사 투사체가 날아오르는 높이입니다.";
                case ColumnId.ExplosionRadius:
                    return "단위: m. 폭발 또는 범위 피해가 적용되는 반경입니다.";
                case ColumnId.LaserDuration:
                    return "단위: 초. 레이저나 지속형 공격이 유지되는 시간입니다.";
                case ColumnId.LaserTickInterval:
                    return "단위: 초. 레이저나 지속형 공격이 피해를 반복 적용하는 간격입니다.";
                case ColumnId.ChainRange:
                    return "단위: m. 체인이나 톱날 연쇄가 다음 대상을 찾는 거리입니다.";
                case ColumnId.MaxChainDepth:
                    return "단위: 단계. 체인이나 톱날 연쇄가 이어지는 최대 단계 수입니다.";
                case ColumnId.ChainBranchCount:
                    return "단위: 개. 체인 번개가 단계마다 동시에 갈라질 수 있는 대상 수입니다.";
                case ColumnId.ChainDelay:
                    return "단위: 초. 체인 번개가 다음 단계로 넘어가기 전 대기 시간입니다.";
                case ColumnId.ChainDamageFalloff:
                    return "단위: 배율. 체인 단계가 깊어질수록 적용되는 피해 유지율입니다. 0.8이면 단계마다 80% 피해입니다.";
                case ColumnId.SawPierceDamageRatio:
                    return "단위: 배율. 톱날이 관통으로 추가 타격할 때 적용되는 피해 비율입니다.";
                case ColumnId.SawTargetMinDistanceRatio:
                    return "단위: 0~1 비율. 톱날이 다음 대상을 고를 때 최소 거리 비율입니다. 너무 가까운 대상 재선택을 줄입니다.";
                case ColumnId.LandingImpactRadius:
                    return "단위: m. 곡사체 착지 순간 충격 또는 폭발 판정 반경입니다.";
                case ColumnId.LandingRollDamageRadius:
                    return "단위: m. 착지 후 구르기 피해가 적용되는 반경입니다.";
                case ColumnId.LandingRollDistance:
                    return "단위: m. 착지 후 투사체가 굴러가는 거리입니다.";
                case ColumnId.LandingRollDuration:
                    return "단위: 초. 착지 후 구르기 동작이 지속되는 시간입니다.";
                default:
                    return string.Empty;
            }
        }

        private static void Header(string text, float width, string tooltip = null)
        {
            GUILayout.Label(new GUIContent(text, tooltip ?? string.Empty), EditorStyles.boldLabel, GUILayout.Width(width));
        }

        private static void RightHeader(string text, float width, string tooltip = null)
        {
            rightAlignedHeader ??= new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleRight
            };

            GUILayout.Label(new GUIContent(text, tooltip ?? string.Empty), rightAlignedHeader, GUILayout.Width(width));
        }

        private static void Label(string text, float width)
        {
            GUILayout.Label(text, GUILayout.Width(width));
        }

        private static void RightLabel(string text, float width)
        {
            rightAlignedLabel ??= new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleRight
            };

            GUILayout.Label(text, rightAlignedLabel, GUILayout.Width(width));
        }

        private static float FloatCell(float value, float original, float width)
        {
            Color previous = GUI.backgroundColor;
            if (!Approximately(value, original))
            {
                GUI.backgroundColor = ChangedCellColor;
            }

            float result = EditorGUILayout.FloatField(value, GUILayout.Width(width));
            GUI.backgroundColor = previous;
            return result;
        }

        private static int IntCell(int value, int original, float width)
        {
            Color previous = GUI.backgroundColor;
            if (value != original)
            {
                GUI.backgroundColor = ChangedCellColor;
            }

            int result = EditorGUILayout.IntField(value, GUILayout.Width(width));
            GUI.backgroundColor = previous;
            return result;
        }

        private static bool BoolCell(bool value, bool original, float width)
        {
            Color previous = GUI.backgroundColor;
            if (value != original)
            {
                GUI.backgroundColor = ChangedCellColor;
            }

            bool result = EditorGUILayout.Toggle(value, GUILayout.Width(width));
            GUI.backgroundColor = previous;
            return result;
        }

        private static SegmentAttackMoveType MoveTypeCell(SegmentAttackMoveType value, SegmentAttackMoveType original, float width)
        {
            Color previous = GUI.backgroundColor;
            if (value != original)
            {
                GUI.backgroundColor = ChangedCellColor;
            }

            int index = Mathf.Max(0, Array.IndexOf(MoveTypeValues, value));
            int resultIndex = EditorGUILayout.Popup(index, BuildMoveTypeLabels(), GUILayout.Width(width));
            GUI.backgroundColor = previous;
            return MoveTypeValues[Mathf.Clamp(resultIndex, 0, MoveTypeValues.Length - 1)];
        }

        private static SegmentAttackImpactType ImpactTypeCell(SegmentAttackImpactType value, SegmentAttackImpactType original, float width)
        {
            Color previous = GUI.backgroundColor;
            if (value != original)
            {
                GUI.backgroundColor = ChangedCellColor;
            }

            int index = Mathf.Max(0, Array.IndexOf(ImpactTypeValues, value));
            int resultIndex = EditorGUILayout.Popup(index, BuildImpactTypeLabels(), GUILayout.Width(width));
            GUI.backgroundColor = previous;
            return ImpactTypeValues[Mathf.Clamp(resultIndex, 0, ImpactTypeValues.Length - 1)];
        }

        private static SegmentAttackAreaMode AreaModeCell(SegmentAttackAreaMode value, SegmentAttackAreaMode original, float width)
        {
            Color previous = GUI.backgroundColor;
            if (value != original)
            {
                GUI.backgroundColor = ChangedCellColor;
            }

            int index = Mathf.Max(0, Array.IndexOf(AreaModeValues, value));
            int resultIndex = EditorGUILayout.Popup(index, BuildAreaModeLabels(), GUILayout.Width(width));
            GUI.backgroundColor = previous;
            return AreaModeValues[Mathf.Clamp(resultIndex, 0, AreaModeValues.Length - 1)];
        }

        private static SegmentSupportAbilityKind SupportAbilityKindCell(SegmentSupportAbilityKind value, SegmentSupportAbilityKind original, float width)
        {
            Color previous = GUI.backgroundColor;
            if (value != original)
            {
                GUI.backgroundColor = ChangedCellColor;
            }

            int index = Mathf.Max(0, Array.IndexOf(SupportAbilityKindValues, value));
            int resultIndex = EditorGUILayout.Popup(index, BuildSupportAbilityKindLabels(), GUILayout.Width(width));
            GUI.backgroundColor = previous;
            return SupportAbilityKindValues[Mathf.Clamp(resultIndex, 0, SupportAbilityKindValues.Length - 1)];
        }

        private static string[] BuildMoveTypeLabels()
        {
            string[] labels = new string[MoveTypeValues.Length];
            for (int i = 0; i < MoveTypeValues.Length; i++)
            {
                labels[i] = TranslateMoveType(MoveTypeValues[i]);
            }

            return labels;
        }

        private static string[] BuildImpactTypeLabels()
        {
            string[] labels = new string[ImpactTypeValues.Length];
            for (int i = 0; i < ImpactTypeValues.Length; i++)
            {
                labels[i] = TranslateImpactType(ImpactTypeValues[i]);
            }

            return labels;
        }

        private static string[] BuildAreaModeLabels()
        {
            string[] labels = new string[AreaModeValues.Length];
            for (int i = 0; i < AreaModeValues.Length; i++)
            {
                labels[i] = TranslateAreaMode(AreaModeValues[i]);
            }

            return labels;
        }

        private static string[] BuildSupportAbilityKindLabels()
        {
            string[] labels = new string[SupportAbilityKindValues.Length];
            for (int i = 0; i < SupportAbilityKindValues.Length; i++)
            {
                labels[i] = TranslateSupportAbilityKind(SupportAbilityKindValues[i]);
            }

            return labels;
        }

        private static string GetLogDirectory()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string workspaceRoot = Directory.GetParent(projectRoot) != null ? Directory.GetParent(projectRoot).FullName : projectRoot;
            return Path.Combine(workspaceRoot, "OZCodingProject 개인파일", "Logs", "SegmentBalance");
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.0001f;
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

        private static string TranslateMoveType(SegmentAttackMoveType moveType)
        {
            switch (moveType)
            {
                case SegmentAttackMoveType.StraightProjectile:
                    return "직선 투사체";
                case SegmentAttackMoveType.PiercingProjectile:
                    return "관통 투사체";
                case SegmentAttackMoveType.ArcProjectile:
                    return "곡사 투사체";
                case SegmentAttackMoveType.HomingProjectile:
                    return "추적 투사체";
                case SegmentAttackMoveType.Laser:
                    return "레이저";
                case SegmentAttackMoveType.ChainLightning:
                    return "체인 번개";
                case SegmentAttackMoveType.SawBounceProjectile:
                    return "톱날 연쇄";
                case SegmentAttackMoveType.ExpandingFlameSphere:
                    return "확장 화염구";
                default:
                    return moveType.ToString();
            }
        }

        private static string TranslateImpactType(SegmentAttackImpactType impactType)
        {
            switch (impactType)
            {
                case SegmentAttackImpactType.DirectDamage:
                    return "직접 피해";
                case SegmentAttackImpactType.PierceDamage:
                    return "관통 피해";
                case SegmentAttackImpactType.ExplosionArea:
                    return "폭발 범위";
                case SegmentAttackImpactType.ContinuousDamage:
                    return "지속 피해";
                case SegmentAttackImpactType.ChainDamage:
                    return "체인 피해";
                default:
                    return impactType.ToString();
            }
        }

        private static string TranslateAreaMode(SegmentAttackAreaMode areaMode)
        {
            switch (areaMode)
            {
                case SegmentAttackAreaMode.FullCircle:
                    return "전체 원형";
                case SegmentAttackAreaMode.SideCones:
                    return "좌우 부채꼴";
                default:
                    return areaMode.ToString();
            }
        }

        private static string TranslateSupportAbilityKind(SegmentSupportAbilityKind abilityKind)
        {
            switch (abilityKind)
            {
                case SegmentSupportAbilityKind.None:
                    return "없음";
                case SegmentSupportAbilityKind.FinalDamageBuff:
                    return "최종 피해 버프";
                case SegmentSupportAbilityKind.PickupMagnet:
                    return "보상 자석";
                case SegmentSupportAbilityKind.FreezeArea:
                    return "범위 동결";
                case SegmentSupportAbilityKind.FinalAttackSpeedBuff:
                    return "최종 공속 버프";
                case SegmentSupportAbilityKind.HolyWaterVulnerabilitySpray:
                    return "성수 취약 분사";
                default:
                    return abilityKind.ToString();
            }
        }

        private readonly struct WeaponOption
        {
            public readonly string SegmentId;
            public readonly string DisplayName;

            public string Label => string.Equals(SegmentId, "전체", StringComparison.OrdinalIgnoreCase)
                ? DisplayName
                : $"{SegmentId} / {DisplayName}";

            public WeaponOption(string segmentId, string displayName)
            {
                SegmentId = segmentId;
                DisplayName = displayName;
            }
        }

        private readonly struct ColumnInfo
        {
            public readonly string Label;
            public readonly float Width;

            public ColumnInfo(string label, float width)
            {
                Label = label;
                Width = width;
            }
        }

        private enum ColumnId
        {
            Spacer,
            MoveType,
            ImpactType,
            AttackAreaMode,
            SideConeAngle,
            BaseDamage,
            Cooldown,
            SearchRange,
            SupportAbilityKind,
            StartsReady,
            SupportCooldown,
            ActiveDurationSeconds,
            EffectDurationSeconds,
            SupportRange,
            FrontSegmentCount,
            BackSegmentCount,
            FinalDamageMultiplier,
            FinalAttackSpeedMultiplier,
            IncomingDamageMultiplier,
            PickupMagnetPullStrength,
            PickupMagnetMaxPullSpeed,
            PickupMagnetCollectDistance,
            HolyWaterSprayAngle,
            HolyWaterProjectileCount,
            HolyWaterProjectileInterval,
            HolyWaterProjectileSpeed,
            HolyWaterProjectileLifetime,
            HolyWaterProjectileStartRadius,
            HolyWaterProjectileEndRadius,
            HolyWaterMuzzleInfluenceStrength,
            HolyWaterConeLength,
            HolyWaterAimTurnSpeed,
            HolyWaterFireAngleTolerance,
            HolyWaterDebuffTickInterval,
            HolyWaterTargetAimHeight,
            ProjectileCount,
            SpreadAngle,
            FireProjectilesSequentially,
            ProjectileVolleySize,
            ProjectileFireDelay,
            ProjectileSpeed,
            ProjectileHitRadius,
            ProjectileLifetime,
            PierceCount,
            PiercingProjectileDamageRatio,
            ArcHeight,
            ExplosionRadius,
            LaserDuration,
            LaserTickInterval,
            ChainRange,
            MaxChainDepth,
            ChainBranchCount,
            ChainDelay,
            ChainDamageFalloff,
            SawPierceDamageRatio,
            SawTargetMinDistanceRatio,
            LandingImpactRadius,
            LandingRollDamageRadius,
            LandingRollDistance,
            LandingRollDuration
        }

        private enum BalanceProfileKind
        {
            None = 0,
            Attack = 1,
            Support = 2
        }

        private sealed class RowState
        {
            public SegmentDefinition Definition;
            public SegmentAttackProfile Profile;
            public SegmentSupportAbilityProfile SupportProfile;
            public SupportSegmentAbility SupportAbility;
            public string SegmentId;
            public string DisplayName;
            public int Level;
            public BalanceValues Original;
            public BalanceValues Working;

            public UnityEngine.Object ProfileAsset => Profile != null ? (UnityEngine.Object)Profile : SupportProfile;
            public bool HasProfile => ProfileAsset != null;
            public bool IsAttackProfile => Profile != null;
            public bool IsSupportProfile => SupportProfile != null;
            public string ProfileKindLabel => IsAttackProfile ? "공격" : (IsSupportProfile ? "지원" : "-");
            public bool IsDirty => HasProfile && !Working.EqualsTo(Original);

            public static RowState Create(SegmentDefinition definition, SegmentLevelDefinition level)
            {
                SegmentAttackProfile profile = level.AttackProfile;
                SupportSegmentAbility supportAbility = profile == null ? ResolveSupportAbility(level.SegmentPrefab) : null;
                SegmentSupportAbilityProfile supportProfile = supportAbility != null ? supportAbility.Profile : null;
                BalanceValues values = profile != null
                    ? BalanceValues.FromAttackProfile(profile)
                    : (supportProfile != null ? BalanceValues.FromSupportProfile(supportProfile, supportAbility) : new BalanceValues());

                return new RowState
                {
                    Definition = definition,
                    Profile = profile,
                    SupportProfile = supportProfile,
                    SupportAbility = supportAbility,
                    SegmentId = definition != null ? definition.NormalizedId : string.Empty,
                    DisplayName = definition != null ? definition.DisplayName : string.Empty,
                    Level = Mathf.Max(0, level.Level),
                    Original = values.Clone(),
                    Working = values.Clone()
                };
            }

            public static RowState CreateMissing(SegmentDefinition definition, int level)
            {
                return new RowState
                {
                    Definition = definition,
                    Profile = null,
                    SegmentId = definition != null ? definition.NormalizedId : string.Empty,
                    DisplayName = definition != null ? definition.DisplayName : string.Empty,
                    Level = level,
                    Original = new BalanceValues(),
                    Working = new BalanceValues()
                };
            }

            public void ResetWorking()
            {
                Working = Original.Clone();
            }

            public UnityEngine.Object[] GetApplyTargets()
            {
                List<UnityEngine.Object> targets = new List<UnityEngine.Object>(2);
                if (ProfileAsset != null)
                {
                    targets.Add(ProfileAsset);
                }

                if (SupportAbility != null)
                {
                    targets.Add(SupportAbility);
                }

                return targets.ToArray();
            }

            private static SupportSegmentAbility ResolveSupportAbility(GameObject segmentPrefab)
            {
                if (segmentPrefab == null)
                {
                    return null;
                }

                return segmentPrefab.GetComponentInChildren<SupportSegmentAbility>(true);
            }
        }

        private sealed class RowLogEntry
        {
            public readonly RowState Row;
            public readonly BalanceValues Before;
            public readonly BalanceValues After;
            public readonly DpsResult BeforeDps;
            public readonly DpsResult AfterDps;

            public RowLogEntry(RowState row, BalanceValues before, BalanceValues after, DpsResult beforeDps, DpsResult afterDps)
            {
                Row = row;
                Before = before;
                After = after;
                BeforeDps = beforeDps;
                AfterDps = afterDps;
            }
        }

        private sealed class BalanceValues
        {
            public BalanceProfileKind ProfileKind;
            public SegmentAttackMoveType MoveType;
            public SegmentAttackImpactType ImpactType;
            public SegmentAttackAreaMode AttackAreaMode;
            public float SideConeAngle;
            public float SearchRange;
            public float BaseDamage;
            public float Cooldown;
            public int ProjectileCount;
            public float SpreadAngle;
            public bool FireProjectilesSequentially;
            public int ProjectileVolleySize;
            public float ProjectileFireDelay;
            public float ProjectileSpeed;
            public float ProjectileHitRadius;
            public float ProjectileLifetime;
            public int PierceCount;
            public float PiercingProjectileDamageRatio;
            public float ArcHeight;
            public float ExplosionRadius;
            public float LaserDuration;
            public float LaserTickInterval;
            public float ChainRange;
            public int MaxChainDepth;
            public int ChainBranchCount;
            public float ChainDelay;
            public float ChainDamageFalloff;
            public float SawPierceDamageRatio;
            public float SawTargetMinDistanceRatio;
            public bool RollAfterArcLanding;
            public float LandingImpactRadius;
            public float LandingRollDamageRadius;
            public float LandingRollDistance;
            public float LandingRollDuration;
            public SegmentSupportAbilityKind SupportAbilityKind;
            public bool StartsReady;
            public float SupportCooldown;
            public float ActiveDurationSeconds;
            public float EffectDurationSeconds;
            public float SupportRange;
            public int FrontSegmentCount;
            public int BackSegmentCount;
            public float FinalDamageMultiplier;
            public float FinalAttackSpeedMultiplier;
            public float IncomingDamageMultiplier;
            public float PickupMagnetPullStrength;
            public float PickupMagnetMaxPullSpeed;
            public float PickupMagnetCollectDistance;
            public float HolyWaterSprayAngle;
            public int HolyWaterProjectileCount;
            public float HolyWaterProjectileInterval;
            public float HolyWaterProjectileSpeed;
            public float HolyWaterProjectileLifetime;
            public float HolyWaterProjectileStartRadius;
            public float HolyWaterProjectileEndRadius;
            public float HolyWaterMuzzleInfluenceStrength;
            public float HolyWaterConeLength;
            public float HolyWaterAimTurnSpeed;
            public float HolyWaterFireAngleTolerance;
            public float HolyWaterDebuffTickInterval;
            public float HolyWaterTargetAimHeight;

            public static BalanceValues FromAttackProfile(SegmentAttackProfile profile)
            {
                return new BalanceValues
                {
                    ProfileKind = BalanceProfileKind.Attack,
                    MoveType = profile.MoveType,
                    ImpactType = profile.ImpactType,
                    AttackAreaMode = profile.AttackAreaMode,
                    SideConeAngle = profile.SideConeAngle,
                    SearchRange = profile.SearchRange,
                    BaseDamage = profile.BaseDamage,
                    Cooldown = profile.Cooldown,
                    ProjectileCount = profile.ProjectileCount,
                    SpreadAngle = profile.SpreadAngle,
                    FireProjectilesSequentially = profile.FireProjectilesSequentially,
                    ProjectileVolleySize = profile.ProjectileVolleySize,
                    ProjectileFireDelay = profile.ProjectileFireDelay,
                    ProjectileSpeed = profile.ProjectileSpeed,
                    ProjectileHitRadius = profile.ProjectileHitRadius,
                    ProjectileLifetime = profile.ProjectileLifetime,
                    PierceCount = profile.PierceCount,
                    PiercingProjectileDamageRatio = profile.PiercingProjectileDamageRatio,
                    ArcHeight = profile.ArcHeight,
                    ExplosionRadius = profile.ExplosionRadius,
                    LaserDuration = profile.LaserDuration,
                    LaserTickInterval = profile.LaserTickInterval,
                    ChainRange = profile.ChainRange,
                    MaxChainDepth = profile.MaxChainDepth,
                    ChainBranchCount = profile.ChainBranchCount,
                    ChainDelay = profile.ChainDelay,
                    ChainDamageFalloff = profile.ChainDamageFalloff,
                    SawPierceDamageRatio = profile.SawPierceDamageRatio,
                    SawTargetMinDistanceRatio = profile.SawTargetMinDistanceRatio,
                    RollAfterArcLanding = profile.RollAfterArcLanding,
                    LandingImpactRadius = profile.LandingImpactRadius,
                    LandingRollDamageRadius = profile.LandingRollDamageRadius,
                    LandingRollDistance = profile.LandingRollDistance,
                    LandingRollDuration = profile.LandingRollDuration
                };
            }

            public static BalanceValues FromSupportProfile(SegmentSupportAbilityProfile profile, SupportSegmentAbility ability)
            {
                BalanceValues values = new BalanceValues
                {
                    ProfileKind = BalanceProfileKind.Support,
                    SupportAbilityKind = profile.AbilityKind,
                    StartsReady = profile.StartsReady,
                    SupportCooldown = profile.Cooldown,
                    ActiveDurationSeconds = profile.ActiveDurationSeconds,
                    EffectDurationSeconds = profile.EffectDurationSeconds,
                    SupportRange = profile.Range,
                    FrontSegmentCount = profile.FrontSegmentCount,
                    BackSegmentCount = profile.BackSegmentCount,
                    FinalDamageMultiplier = profile.FinalDamageMultiplier,
                    FinalAttackSpeedMultiplier = profile.FinalAttackSpeedMultiplier,
                    IncomingDamageMultiplier = profile.IncomingDamageMultiplier
                };

                if (ability != null)
                {
                    values.PickupMagnetPullStrength = ability.PickupMagnetPullStrength;
                    values.PickupMagnetMaxPullSpeed = ability.PickupMagnetMaxPullSpeed;
                    values.PickupMagnetCollectDistance = ability.PickupMagnetCollectDistance;
                    values.HolyWaterSprayAngle = ability.HolyWaterSprayAngle;
                    values.HolyWaterProjectileCount = ability.HolyWaterProjectileCount;
                    values.HolyWaterProjectileInterval = ability.HolyWaterProjectileInterval;
                    values.HolyWaterProjectileSpeed = ability.HolyWaterProjectileSpeed;
                    values.HolyWaterProjectileLifetime = ability.HolyWaterProjectileLifetime;
                    values.HolyWaterProjectileStartRadius = ability.HolyWaterProjectileStartRadius;
                    values.HolyWaterProjectileEndRadius = ability.HolyWaterProjectileEndRadius;
                    values.HolyWaterMuzzleInfluenceStrength = ability.HolyWaterMuzzleInfluenceStrength;
                    values.HolyWaterConeLength = ability.HolyWaterConeLength;
                    values.HolyWaterAimTurnSpeed = ability.HolyWaterAimTurnSpeed;
                    values.HolyWaterFireAngleTolerance = ability.HolyWaterFireAngleTolerance;
                    values.HolyWaterDebuffTickInterval = ability.HolyWaterDebuffTickInterval;
                    values.HolyWaterTargetAimHeight = ability.HolyWaterTargetAimHeight;
                }

                return values;
            }

            public BalanceValues Clone()
            {
                return (BalanceValues)MemberwiseClone();
            }

            public void ApplyTo(RowState row)
            {
                if (row.IsAttackProfile)
                {
                    ApplyTo(row.Profile);
                    return;
                }

                if (row.IsSupportProfile)
                {
                    ApplyTo(row.SupportProfile);
                    ApplyTo(row.SupportAbility);
                }
            }

            private void ApplyTo(SegmentAttackProfile profile)
            {
                profile.MoveType = MoveType;
                profile.ImpactType = ImpactType;
                profile.AttackAreaMode = AttackAreaMode;
                profile.SideConeAngle = Mathf.Clamp(SideConeAngle, 1f, 180f);
                profile.SearchRange = Mathf.Max(0.1f, SearchRange);
                profile.BaseDamage = Mathf.Max(0f, BaseDamage);
                profile.Cooldown = Mathf.Max(0.05f, Cooldown);
                profile.ProjectileCount = Mathf.Max(1, ProjectileCount);
                profile.SpreadAngle = Mathf.Max(0f, SpreadAngle);
                profile.FireProjectilesSequentially = FireProjectilesSequentially;
                profile.ProjectileVolleySize = Mathf.Max(1, ProjectileVolleySize);
                profile.ProjectileFireDelay = Mathf.Max(0f, ProjectileFireDelay);
                profile.ProjectileSpeed = Mathf.Max(0.1f, ProjectileSpeed);
                profile.ProjectileHitRadius = Mathf.Max(0.05f, ProjectileHitRadius);
                profile.ProjectileLifetime = Mathf.Max(0.1f, ProjectileLifetime);
                profile.PierceCount = Mathf.Max(0, PierceCount);
                profile.PiercingProjectileDamageRatio = Mathf.Clamp01(PiercingProjectileDamageRatio);
                profile.ArcHeight = Mathf.Max(0f, ArcHeight);
                profile.ExplosionRadius = Mathf.Max(0.1f, ExplosionRadius);
                profile.LaserDuration = Mathf.Max(0.05f, LaserDuration);
                profile.LaserTickInterval = Mathf.Max(0.02f, LaserTickInterval);
                profile.ChainRange = Mathf.Max(0.1f, ChainRange);
                profile.MaxChainDepth = Mathf.Max(0, MaxChainDepth);
                profile.ChainBranchCount = Mathf.Max(1, ChainBranchCount);
                profile.ChainDelay = Mathf.Max(0f, ChainDelay);
                profile.ChainDamageFalloff = Mathf.Clamp01(ChainDamageFalloff);
                profile.SawPierceDamageRatio = Mathf.Clamp01(SawPierceDamageRatio);
                profile.SawTargetMinDistanceRatio = Mathf.Clamp01(SawTargetMinDistanceRatio);
                profile.LandingImpactRadius = Mathf.Max(0f, LandingImpactRadius);
                profile.LandingRollDamageRadius = Mathf.Max(0f, LandingRollDamageRadius);
                profile.LandingRollDistance = Mathf.Max(0f, LandingRollDistance);
                profile.LandingRollDuration = Mathf.Max(0.01f, LandingRollDuration);
            }

            private void ApplyTo(SegmentSupportAbilityProfile profile)
            {
                profile.AbilityKind = SupportAbilityKind;
                profile.StartsReady = StartsReady;
                profile.Cooldown = Mathf.Max(0f, SupportCooldown);
                profile.ActiveDurationSeconds = Mathf.Max(0f, ActiveDurationSeconds);
                profile.EffectDurationSeconds = Mathf.Max(0f, EffectDurationSeconds);
                profile.Range = Mathf.Max(0f, SupportRange);
                profile.FrontSegmentCount = Mathf.Max(0, FrontSegmentCount);
                profile.BackSegmentCount = Mathf.Max(0, BackSegmentCount);
                profile.FinalDamageMultiplier = Mathf.Max(0f, FinalDamageMultiplier);
                profile.FinalAttackSpeedMultiplier = Mathf.Max(0f, FinalAttackSpeedMultiplier);
                profile.IncomingDamageMultiplier = Mathf.Max(0f, IncomingDamageMultiplier);
            }

            private void ApplyTo(SupportSegmentAbility ability)
            {
                if (ability == null)
                {
                    return;
                }

                ability.PickupMagnetPullStrength = Mathf.Max(0f, PickupMagnetPullStrength);
                ability.PickupMagnetMaxPullSpeed = Mathf.Max(0.1f, PickupMagnetMaxPullSpeed);
                ability.PickupMagnetCollectDistance = Mathf.Max(0.05f, PickupMagnetCollectDistance);
                ability.HolyWaterSprayAngle = Mathf.Clamp(HolyWaterSprayAngle, 1f, 180f);
                ability.HolyWaterProjectileCount = Mathf.Max(1, HolyWaterProjectileCount);
                ability.HolyWaterProjectileInterval = Mathf.Max(0.02f, HolyWaterProjectileInterval);
                ability.HolyWaterProjectileSpeed = Mathf.Max(0.1f, HolyWaterProjectileSpeed);
                ability.HolyWaterProjectileLifetime = Mathf.Max(0.05f, HolyWaterProjectileLifetime);
                ability.HolyWaterProjectileStartRadius = Mathf.Max(0f, HolyWaterProjectileStartRadius);
                ability.HolyWaterProjectileEndRadius = Mathf.Max(0.05f, HolyWaterProjectileEndRadius);
                ability.HolyWaterMuzzleInfluenceStrength = Mathf.Clamp01(HolyWaterMuzzleInfluenceStrength);
                ability.HolyWaterConeLength = Mathf.Max(0f, HolyWaterConeLength);
                ability.HolyWaterAimTurnSpeed = Mathf.Max(0f, HolyWaterAimTurnSpeed);
                ability.HolyWaterFireAngleTolerance = Mathf.Clamp(HolyWaterFireAngleTolerance, 0f, 45f);
                ability.HolyWaterDebuffTickInterval = Mathf.Max(0.05f, HolyWaterDebuffTickInterval);
                ability.HolyWaterTargetAimHeight = Mathf.Max(0f, HolyWaterTargetAimHeight);
            }

            public bool EqualsTo(BalanceValues other)
            {
                if (other == null)
                {
                    return false;
                }

                return ProfileKind == other.ProfileKind
                    && MoveType == other.MoveType
                    && ImpactType == other.ImpactType
                    && AttackAreaMode == other.AttackAreaMode
                    && Approximately(SideConeAngle, other.SideConeAngle)
                    && Approximately(SearchRange, other.SearchRange)
                    && Approximately(BaseDamage, other.BaseDamage)
                    && Approximately(Cooldown, other.Cooldown)
                    && ProjectileCount == other.ProjectileCount
                    && Approximately(SpreadAngle, other.SpreadAngle)
                    && FireProjectilesSequentially == other.FireProjectilesSequentially
                    && ProjectileVolleySize == other.ProjectileVolleySize
                    && Approximately(ProjectileFireDelay, other.ProjectileFireDelay)
                    && Approximately(ProjectileSpeed, other.ProjectileSpeed)
                    && Approximately(ProjectileHitRadius, other.ProjectileHitRadius)
                    && Approximately(ProjectileLifetime, other.ProjectileLifetime)
                    && PierceCount == other.PierceCount
                    && Approximately(PiercingProjectileDamageRatio, other.PiercingProjectileDamageRatio)
                    && Approximately(ArcHeight, other.ArcHeight)
                    && Approximately(ExplosionRadius, other.ExplosionRadius)
                    && Approximately(LaserDuration, other.LaserDuration)
                    && Approximately(LaserTickInterval, other.LaserTickInterval)
                    && Approximately(ChainRange, other.ChainRange)
                    && MaxChainDepth == other.MaxChainDepth
                    && ChainBranchCount == other.ChainBranchCount
                    && Approximately(ChainDelay, other.ChainDelay)
                    && Approximately(ChainDamageFalloff, other.ChainDamageFalloff)
                    && Approximately(SawPierceDamageRatio, other.SawPierceDamageRatio)
                    && Approximately(SawTargetMinDistanceRatio, other.SawTargetMinDistanceRatio)
                    && Approximately(LandingImpactRadius, other.LandingImpactRadius)
                    && Approximately(LandingRollDamageRadius, other.LandingRollDamageRadius)
                    && Approximately(LandingRollDistance, other.LandingRollDistance)
                    && Approximately(LandingRollDuration, other.LandingRollDuration)
                    && SupportAbilityKind == other.SupportAbilityKind
                    && StartsReady == other.StartsReady
                    && Approximately(SupportCooldown, other.SupportCooldown)
                    && Approximately(ActiveDurationSeconds, other.ActiveDurationSeconds)
                    && Approximately(EffectDurationSeconds, other.EffectDurationSeconds)
                    && Approximately(SupportRange, other.SupportRange)
                    && FrontSegmentCount == other.FrontSegmentCount
                    && BackSegmentCount == other.BackSegmentCount
                    && Approximately(FinalDamageMultiplier, other.FinalDamageMultiplier)
                    && Approximately(FinalAttackSpeedMultiplier, other.FinalAttackSpeedMultiplier)
                    && Approximately(IncomingDamageMultiplier, other.IncomingDamageMultiplier)
                    && Approximately(PickupMagnetPullStrength, other.PickupMagnetPullStrength)
                    && Approximately(PickupMagnetMaxPullSpeed, other.PickupMagnetMaxPullSpeed)
                    && Approximately(PickupMagnetCollectDistance, other.PickupMagnetCollectDistance)
                    && Approximately(HolyWaterSprayAngle, other.HolyWaterSprayAngle)
                    && HolyWaterProjectileCount == other.HolyWaterProjectileCount
                    && Approximately(HolyWaterProjectileInterval, other.HolyWaterProjectileInterval)
                    && Approximately(HolyWaterProjectileSpeed, other.HolyWaterProjectileSpeed)
                    && Approximately(HolyWaterProjectileLifetime, other.HolyWaterProjectileLifetime)
                    && Approximately(HolyWaterProjectileStartRadius, other.HolyWaterProjectileStartRadius)
                    && Approximately(HolyWaterProjectileEndRadius, other.HolyWaterProjectileEndRadius)
                    && Approximately(HolyWaterMuzzleInfluenceStrength, other.HolyWaterMuzzleInfluenceStrength)
                    && Approximately(HolyWaterConeLength, other.HolyWaterConeLength)
                    && Approximately(HolyWaterAimTurnSpeed, other.HolyWaterAimTurnSpeed)
                    && Approximately(HolyWaterFireAngleTolerance, other.HolyWaterFireAngleTolerance)
                    && Approximately(HolyWaterDebuffTickInterval, other.HolyWaterDebuffTickInterval)
                    && Approximately(HolyWaterTargetAimHeight, other.HolyWaterTargetAimHeight);
            }
        }

        private readonly struct DpsResult
        {
            public readonly float SingleDps;
            public readonly float Cluster10Dps;

            public DpsResult(float singleDps, float cluster10Dps)
            {
                SingleDps = singleDps;
                Cluster10Dps = cluster10Dps;
            }
        }

        private static class DpsCalculator
        {
            public static DpsResult Calculate(BalanceValues values)
            {
                if (values == null || values.ProfileKind != BalanceProfileKind.Attack)
                {
                    return default;
                }

                float cycleTime = GetCycleTime(values);
                float singleDamage = EstimateSingleCastDamage(values);
                float clusterDamage = EstimateClusterCastDamage(values, 10);
                return new DpsResult(singleDamage / cycleTime, clusterDamage / cycleTime);
            }

            private static float GetCycleTime(BalanceValues values)
            {
                int projectileCount = Mathf.Max(1, values.ProjectileCount);
                float cooldown = Mathf.Max(0.05f, values.Cooldown);
                if (values.FireProjectilesSequentially && projectileCount > 1)
                {
                    int volleySize = Mathf.Clamp(values.ProjectileVolleySize, 1, projectileCount);
                    int volleyCount = Mathf.CeilToInt(projectileCount / (float)volleySize);
                    cooldown += Mathf.Max(0f, values.ProjectileFireDelay) * Mathf.Max(0, volleyCount - 1);
                }

                return Mathf.Max(0.05f, cooldown);
            }

            private static float EstimateSingleCastDamage(BalanceValues values)
            {
                int projectileCount = Mathf.Max(1, values.ProjectileCount);
                float damage = Mathf.Max(0f, values.BaseDamage);

                switch (values.MoveType)
                {
                    case SegmentAttackMoveType.Laser:
                        return damage * GetTickCount(values.LaserDuration, values.LaserTickInterval);
                    case SegmentAttackMoveType.ExpandingFlameSphere:
                        return damage * projectileCount * GetTickCount(values.ProjectileLifetime, values.LaserTickInterval);
                    case SegmentAttackMoveType.ChainLightning:
                        return damage;
                    case SegmentAttackMoveType.PiercingProjectile:
                        return damage * Mathf.Clamp01(values.PiercingProjectileDamageRatio) * projectileCount;
                    default:
                        if (values.ImpactType == SegmentAttackImpactType.PierceDamage)
                        {
                            return damage * Mathf.Clamp01(values.PiercingProjectileDamageRatio) * projectileCount;
                        }

                        return damage * projectileCount;
                }
            }

            private static float EstimateClusterCastDamage(BalanceValues values, int targetCount)
            {
                int clampedTargets = Mathf.Max(1, targetCount);
                int projectileCount = Mathf.Max(1, values.ProjectileCount);
                float damage = Mathf.Max(0f, values.BaseDamage);

                if (values.MoveType == SegmentAttackMoveType.ChainLightning)
                {
                    return EstimateChainDamage(damage, values.MaxChainDepth, values.ChainBranchCount, values.ChainDamageFalloff, clampedTargets);
                }

                if (values.MoveType == SegmentAttackMoveType.Laser)
                {
                    return EstimateSingleCastDamage(values);
                }

                if (values.MoveType == SegmentAttackMoveType.ExpandingFlameSphere)
                {
                    return damage * projectileCount * GetTickCount(values.ProjectileLifetime, values.LaserTickInterval) * clampedTargets;
                }

                if (values.MoveType == SegmentAttackMoveType.SawBounceProjectile)
                {
                    int fullDamageHits = Mathf.Min(clampedTargets, projectileCount * Mathf.Max(1, 1 + values.MaxChainDepth));
                    int remainingTargets = Mathf.Max(0, clampedTargets - fullDamageHits);
                    float pierceDamage = damage * Mathf.Clamp01(values.SawPierceDamageRatio) * remainingTargets;
                    return damage * fullDamageHits + pierceDamage;
                }

                if (values.ImpactType == SegmentAttackImpactType.ExplosionArea)
                {
                    return damage * projectileCount * clampedTargets;
                }

                if (values.MoveType == SegmentAttackMoveType.PiercingProjectile || values.ImpactType == SegmentAttackImpactType.PierceDamage)
                {
                    float pierceRatio = Mathf.Clamp01(values.PiercingProjectileDamageRatio);
                    return damage * pierceRatio * Mathf.Min(clampedTargets, projectileCount * Mathf.Max(1, values.PierceCount));
                }

                return damage * Mathf.Min(clampedTargets, projectileCount);
            }

            private static float EstimateChainDamage(float baseDamage, int maxDepth, int branchCount, float falloff, int targetCount)
            {
                int remainingTargets = Mathf.Max(0, targetCount);
                if (remainingTargets <= 0)
                {
                    return 0f;
                }

                float total = baseDamage;
                remainingTargets--;

                int branches = Mathf.Max(1, branchCount);
                float clampedFalloff = Mathf.Clamp01(falloff);
                int nodesAtDepth = 1;
                for (int depth = 1; depth <= Mathf.Max(0, maxDepth) && remainingTargets > 0; depth++)
                {
                    nodesAtDepth *= branches;
                    int hits = Mathf.Min(remainingTargets, nodesAtDepth);
                    total += baseDamage * Mathf.Pow(clampedFalloff, depth) * hits;
                    remainingTargets -= hits;
                }

                return total;
            }

            private static int GetTickCount(float duration, float interval)
            {
                float safeDuration = Mathf.Max(0.05f, duration);
                float safeInterval = Mathf.Max(0.02f, interval);
                return Mathf.Max(1, Mathf.CeilToInt(safeDuration / safeInterval));
            }
        }
    }
}
