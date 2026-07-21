using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    public sealed class MeadowTerrainRuntimeGenerator : MonoBehaviour // CoreTest 초원 지형 런타임 생성
    {
        private const string GeneratedRootName = "GeneratedMeadowTerrainRoot";
        private const string GeneratedMeshName = "GeneratedMeadowTerrainMesh";
        private const string GeneratedDemoTerrainName = "GeneratedMeadowDemoTerrain";
        private const string GeneratedDemoTerrainDataName = "GeneratedMeadowDemoTerrainData";
        private const string GeneratedInstancedDetailsName = "GeneratedMeadowInstancedDetails";
        private const string GeneratedClusterContactShadowName = "ClusterContactShadow";
        private const float DefaultClusterContactShadowAlpha = 0.32f;
        private const float DefaultClusterContactShadowPadding = 1.35f;
        private const float DefaultClusterContactShadowMaxSize = 9.5f;
        private const float DefaultClusterContactShadowYOffset = 0.11f;
        private const float MinimumVisibleClusterContactShadowAlpha = 0.22f;
        private const float MinimumVisibleClusterContactShadowYOffset = 0.08f;
        private const float MaximumVisibleClusterContactShadowPadding = 0.25f;
        private const float MaximumVisibleClusterContactShadowSize = 2.4f;
        private const float MinimumVisibleClusterContactShadowSize = 0.35f;
        private const string LoadingCanvasName = "MeadowTerrainLoadingOverlay_Runtime";
#if UNITY_EDITOR
        private const string ProjectTerrainTemplatePath = "Assets/Art/Map/Generated/Terrain_CoreTestMeadowNature_Copy.asset";
        private const string DemoTerrainTemplatePath = "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Materials/Terrain/Terrain Meadow Nature.asset";
        private const string PandazolePrefabFolder = "Assets/ThirdParty/01_Core/Pandazole - Nature Environment Low poly Pack/Pandazole Nature Environment Pack/Prefabs";
#endif

        [Header("Runtime")]
        public bool BuildOnStart = true; // 씬 시작 시 생성
        public bool ShowLoadingOverlay = true; // 생성 중 로딩 표시
        public bool LogGenerationTimings = true; // 생성 단계별 시간 로그
        [Min(0f)] public float MinimumLoadingSeconds = 0.35f; // 너무 빠른 깜빡임 방지
        public string LoadingTitle = "전장 진입 준비"; // 로딩 제목
        public string LoadingStatus = "초원 전장을 준비하는 중..."; // 보조 문구
        public Sprite LoadingBackgroundSprite; // 로딩 배경 이미지

        [Header("Scene")]
        public Material TerrainMaterial; // 01/02/03 블렌드 머티리얼
        public Transform GeneratedParent; // 생성물 부모
        public string LegacyVisualGroundName = "MapVisualGround"; // 기존 비주얼 지형
        public string SourceGroundRendererName = "GroundPlane"; // 판정 평면 렌더
        public string LegacyTerrainObjectName = "Terrain"; // 기존 데모 Terrain
        public bool HideLegacyVisualGround = true; // 구형 비주얼 숨김
        public bool HideSourceGroundRenderer = true; // 판정 평면 표시 숨김
        public bool HideLegacyTerrainObject = true; // 생성 Terrain과 겹치는 데모 Terrain 숨김

        [Header("Map Size")]
        [Min(10f)] public float MapSize = 200f; // 전체 비주얼 크기
        [Min(10f)] public float PlayableSize = 150f; // 플레이 영역 기준
        [Min(0.25f)] public float CellSize = 1f; // 메시 격자
        [Min(0.25f)] public float MaxRuntimeCellSize = 0.4f; // 실제 런타임 최대 격자
        [Min(0.25f)] public float TextureWorldSize = 5f; // 텍스처 반복 크기
        public float VisualYOffset = 0.035f; // 판정면 위 표시 높이

        [Header("Terrain Surface Quality")]
        public bool UseDemoTerrainTemplateSurface = true; // 복사 TerrainData 표면 사용
        public MeadowTerrainSurfaceProfile SurfaceProfile; // 빌드 포함용 Terrain 표면 프로필
        public TerrainData TerrainTemplate; // 프로젝트 소유 데모 TerrainData 복사본
        public bool OverlayGeneratedTrailsOnDemoTerrain = true; // 복사 바닥 위에 생성 흙길만 덮기
        public bool RegenerateDemoTerrainHeights = false; // 데모 heightmap 유지 시 로딩 단축
        public bool ClearDemoDetailsOnTrails = true; // 흙길 위 풀/꽃 제거
        public bool UseInstancedTerrainRendering = false; // Player Build 표면 누락 방지를 위해 기본 Terrain 렌더 사용
        [Min(0f)] public float DemoTerrainEditPadding = 5f; // 길 주변 편집 여유
        public bool UseHighQualityControlMap = true; // Terrain splat map 방식 블렌딩
        [Range(256, 2048)] public int ControlMapResolution = 1024; // 200m 기준 약 0.2m/px
        [Range(0f, 1f)] public float ControlMapBlend = 1f; // vertex color 대비 control map 반영
        [Range(0f, 0.35f)] public float DirtMottleStrength = 0.14f; // 흙길 내부 풀/중간 얼룩
        [Range(0f, 0.35f)] public float GrassMottleStrength = 0.1f; // 풀밭 내부 중간층 얼룩
        [Range(0f, 0.45f)] public float EdgeMottleStrength = 0.22f; // 경계부 유기적 흔들림

        [Header("Height")]
        [Min(0f)] public float PlayableHeightAmplitude = 0.07f; // 내부 작은 굴곡
        [Min(0f)] public float OuterHeightAmplitude = 1.8f; // 외곽 큰 굴곡 후보
        [Min(0f)] public float OuterRimRise = 0.65f; // 외곽 완만한 상승
        [Min(0.001f)] public float HeightNoiseWorldScale = 28f; // 높이 노이즈 크기
        [Range(1, 5)] public int HeightOctaves = 3; // 높이 노이즈 중첩
        [Range(0.1f, 0.9f)] public float HeightPersistence = 0.45f; // 옥타브 감쇠
        [Min(1f)] public float HeightLacunarity = 2f; // 옥타브 주파수

        [Header("Main Trails")]
        [Min(0.1f)] public float MainDirtHalfWidth = 1.9f; // 01 중심 반폭
        [Min(0f)] public float MainMidBlendWidth = 2.2f; // 01->02
        [Min(0f)] public float MainGrassBlendWidth = 2.4f; // 02->03
        [Range(0.25f, 1.5f)] public float MainTrailWidthScale = 0.8f; // 큰 흙길 폭 배율
        [Min(1f)] public float EntryZoneDepth = 10f; // 시작 구역 깊이
        [Min(1f)] public float EntryZoneHalfWidth = 42f; // 시작 구역 폭
        [Min(0f)] public float MainControlJitter = 13f; // 메인 곡률 흔들림

        [Header("Center Clearing")]
        [Min(1f)] public float NexusClearingRadius = 10.5f; // 중앙 원형 흙길
        [Min(0f)] public float NexusClearingBlendWidth = 4f; // 중앙 02 여유
        public string NexusObjectName = "Nexus_Core"; // 넥서스 검색명

        [Header("Noise")]
        [Min(0f)] public float EdgeNoiseAmplitude = 0.55f; // 길 가장자리 흔들림
        [Min(0.001f)] public float EdgeNoiseWorldScale = 5.5f; // 가장자리 노이즈 크기
        public int Seed = 230623; // 재현용 시드

        [Header("Generation")]
        [Range(1, 64)] public int RowsPerFrame = 10; // 로딩 갱신 단위
        [Range(1, 64)] public int TriangleRowsPerFrame = 20; // 삼각형 생성 단위

        [Header("Decorations")]
        public bool SpawnDecorations = true; // 지형 생성 후 자연물 배치
        public bool UseMeadowNatureEditorFallbacks = true; // 인스펙터 미지정 시 에디터에서 Meadow 프리팹 로드
        public bool DisableDecorationColliders = true; // 1차는 비주얼 장식 우선
        [Min(0f)] public float DecorationSurfaceOffset = 0.015f; // 지면 파고듦 방지
        [Range(0f, 1f)] public float SmallDecorationShadowChance = 0.08f; // 작은 식물 그림자 확률
        public bool CastLargeDecorationRealtimeShadows = true; // 나무/돌 실시간 그림자
        public bool UseClusterContactShadows = true; // 작은 장식 군집 접지 그림자
        [Range(0f, 0.55f)] public float ClusterContactShadowAlpha = DefaultClusterContactShadowAlpha; // 접지 그림자 농도
        [Min(0f)] public float ClusterContactShadowPadding = DefaultClusterContactShadowPadding; // 군집 그림자 여백
        [Range(0.5f, 12f)] public float ClusterContactShadowMaxSize = DefaultClusterContactShadowMaxSize; // 군집 그림자 최대 크기
        [Min(0f)] public float ClusterContactShadowYOffset = DefaultClusterContactShadowYOffset; // 지면 겹침 방지
        [Min(0)] public int TreeClusterCount = 24; // 나무 군집 수
        [Min(0)] public int RockClusterCount = 42; // 작은 돌 군집 수
        [Min(0)] public int FlowerPatchCount = 88; // 꽃밭 군집 수
        [Min(0)] public int GrassPatchCount = 120; // 풀/덤불 군집 수
        [Min(0)] public int HeroTreeAccentCount = 5; // 포인트 나무 패턴 수
        [Min(0)] public int NexusOuterAccentCount = 10; // 넥서스 외곽 낮은 장식 수
        [Range(0f, 1f)] public float DetailBackedFlowerAccentRatio = 0.26f; // detail 사용 시 남길 포인트 꽃 비율
        [Range(0f, 1f)] public float DetailBackedGrassAccentRatio = 0.14f; // detail 사용 시 남길 포인트 풀 비율
        [Range(0.75f, 1.8f)] public float DecorationClusterRadiusScale = 1.25f; // 장식 오브젝트 군집 범위
        [Range(1f, 1.8f)] public float DecorationClusterMemberScale = 1.35f; // 장식 오브젝트 군집 내부 수량
        [Min(0f)] public float TreeClusterMinSpacing = 10.5f; // 나무 군집 간격
        [Min(0f)] public float RockClusterMinSpacing = 7f; // 돌 군집 간격
        [Min(0f)] public float FlowerPatchMinSpacing = 4.2f; // 꽃밭 간격
        [Min(0f)] public float GrassPatchMinSpacing = 3.2f; // 풀 군집 간격

        [Header("Terrain Details")]
        public bool UseTerrainDetailsForGrassAndFlowers = true; // 풀/꽃은 지형 밀도 기반 인스턴싱으로 렌더
        [Range(128, 1024)] public int DetailResolution = 512; // detail 후보 샘플 해상도
        [Range(0f, 1.5f)] public float DetailObjectDensity = 1f; // 전체 detail 밀도 보정
        [Min(0f)] public float DetailObjectDistance = 240f; // detail 표시 거리 기준
        [Range(0, 24)] public int GrassDetailDensity = 18; // 일반 잔디 detail 셀 밀도
        [Range(0, 8)] public int FlowerDetailDensity = 3; // 꽃 detail 셀 밀도
        [Range(1, 12)] public int MaxGrassDetailPrototypes = 10; // 풀/잎 식물 종류 수
        [Range(1, 10)] public int MaxFlowerDetailPrototypes = 8; // 꽃 종류 수
        [Min(0)] public int MaxGrassInstances = 120000; // 일반 잔디 중심 인스턴스 상한
        [Min(0)] public int MaxFlowerInstances = 650; // 꽃 인스턴스 상한
        [Range(0f, 1f)] public float PathEdgeFlowerBoost = 0.28f; // 길가 꽃띠 보조
        [Range(0f, 1f)] public float MeadowFlowerChance = 0.12f; // 풀밭 꽃 군집 확률

        [Header("Decoration Prefabs")]
        public GameObject[] TreePrefabs; // 큰 활엽수/침엽수
        public GameObject[] LightTreePrefabs; // 자작나무 등 가벼운 나무
        public GameObject[] BushPrefabs; // 덤불
        public GameObject[] GrassPrefabs; // 풀/잎 식물
        public GameObject[] FlowerGroupPrefabs; // 이미 묶인 꽃 프리팹
        public GameObject[] FlowerAccentPrefabs; // 단일/키 큰 꽃
        public GameObject[] MushroomPrefabs; // 버섯 소품
        public GameObject[] RockGroupPrefabs; // 작은 돌 묶음

        [Header("Decoration Catalog")]
        public MeadowDecorationCatalogAsset DecorationCatalogAsset; // 빌드 포함용 자연물 카탈로그

        [Header("Pandazole Addon Decorations")]
        public bool SpawnPandazoleAddons = true; // Pandazole 팩만 별도 루트로 추가
        public bool UsePandazoleEditorFallbacks = true; // 인스펙터 미지정 시 현재 남은 Pandazole 프리팹 자동 수집
        [Min(0)] public int PandazoleFlowerPocketCount = 22; // Pandazole 꽃 소군집
        [Min(0)] public int PandazoleForestFloorPocketCount = 26; // 버섯/통나무/낮은 풀 소품 군집
        [Min(0)] public int PandazoleCurioPocketCount = 7; // 뼈/해골 희귀 소품 군집
        [Min(0f)] public float PandazoleFlowerPocketMinSpacing = 4.8f; // Pandazole 꽃 간격
        [Min(0f)] public float PandazoleForestFloorMinSpacing = 6.0f; // 숲바닥 소품 간격
        [Min(0f)] public float PandazoleCurioMinSpacing = 10.5f; // 희귀 소품 간격
        [Range(0.25f, 2f)] public float PandazoleScaleMultiplier = 1f; // Pandazole 전체 스케일 보정

        [Header("Pandazole Addon Prefabs")]
        public GameObject[] PandazoleFlowerPrefabs; // Flower_*
        public GameObject[] PandazoleGrassPrefabs; // Grass_*
        public GameObject[] PandazoleFoliagePrefabs; // Foliage_*
        public GameObject[] PandazoleMushroomPrefabs; // Mashroom_*
        public GameObject[] PandazoleLogPrefabs; // Log_*
        public GameObject[] PandazoleBonePrefabs; // Bones_*
        public GameObject[] PandazoleSkullPrefabs; // Skull_*

        private GameObject generatedRoot; // 런타임 생성 루트
        private MeadowLoadingOverlay overlay; // 임시 로딩창
        private Material runtimeMaterial; // 생성 메시 전용 머티리얼
        private Material runtimeContactShadowMaterial; // 군집 접지 그림자 머티리얼
        private Texture2D runtimeContactShadowTexture; // 군집 접지 그림자 알파 텍스처
        private Texture2D runtimeControlTexture; // 런타임 splat control
        private TerrainData runtimeDemoTerrainData; // 데모 TerrainData 런타임 복제본
        private static Material fallbackMaterial; // 머티리얼 미지정 fallback
        private static Mesh contactShadowMesh; // 접지 그림자 공유 메시
        private int contactShadowAttempts; // 접지 그림자 시도 수
        private int contactShadowCreated; // 접지 그림자 생성 수
        private int contactShadowDisabled; // 설정 비활성 스킵 수
        private int contactShadowNullCluster; // null 군집 스킵 수
        private int contactShadowNoBounds; // 포함 렌더러 없음
        private int contactShadowAlphaZero; // 알파 0 스킵 수
        private int contactShadowNoMaterial; // 머티리얼 없음
        private int contactShadowRendererTotal; // 진단용 전체 렌더러
        private int contactShadowRendererIncluded; // 그림자 bounds 포함 렌더러
        private int contactShadowExcludedGrass; // 잔디 제외 렌더러
        private int contactShadowExcludedTree; // 나무 제외 렌더러
        private int contactShadowExcludedSelf; // 그림자 자신 제외
        private int contactShadowSkippedRootless; // 군집 루트 확인 실패
        private int contactShadowSampleLogCount; // 샘플 로그 제한
        private Vector2 contactShadowDiagnosticCenter; // 진단용 지형 중심
        private bool contactShadowDiagnosticDemoTerrain; // 진단용 Terrain 경로 여부

        private static readonly string[] DefaultTreePrefabPaths =
        {
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_tree_bent_01.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_tree_bent_02.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_tree_chestnut_01.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_tree_chestnut_02.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_tree_maple_01 (1) 1.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_tree_maple_01 (1).prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_tree_maple_02 (1).prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_tree_maple_03 (1).prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_tree_oak.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_tree_pine_02.prefab"
        };

        private static readonly string[] DefaultLightTreePrefabPaths =
        {
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_tree_birch_01.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_tree_birch_02.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_tree_birch_03.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_tree_pine_01.prefab"
        };

        private static readonly string[] DefaultBushPrefabPaths =
        {
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_bush_02.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_bush_02 (1).prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_bush_02 (2).prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_bush_large.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_bush_leaves_01.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_bush_leaves_02.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_bush_leaves_03.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_bush_leaves_04.prefab"
        };

        private static readonly string[] DefaultGrassPrefabPaths =
        {
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_grass_01.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_grass_short.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_plant_green_01.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_plant_green_02.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_plant_green_03.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_plant_leaves_01.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_plant_leaves_02.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_ivy_01.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_ivy_02.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_ivy_03.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_shamrocks.prefab"
        };

        private static readonly string[] DefaultFlowerGroupPrefabPaths =
        {
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_flower_group_01.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_flower_group_02.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_flower_group_03.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_flower_group_04.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_flowers_group_05.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_plant_flowers_01.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_plant_flowers_02.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_plant_flowers_03.prefab"
        };

        private static readonly string[] DefaultFlowerAccentPrefabPaths =
        {
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_flower_calendula.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_flower_daffodil.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_flower_lily.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_flower_loosetrifes.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_flower_rose.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_flower_salvia_01.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_flower_salvia_02.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_flower_salvia_03.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_flower_tall_01.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_flower_tall_02.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_flower_tall_03.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_flower_tulip.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_sunflower.prefab"
        };

        private static readonly string[] DefaultRockGroupPrefabPaths =
        {
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_rock_group_01.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_rock_group_02.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_rock_group_03.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_rock_large_01.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_rock_large_02.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_rock_large_03.prefab"
        };

        private static readonly string[] DefaultMushroomPrefabPaths =
        {
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_musrooms_set_01.prefab",
            "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Prefabs/SM_musrooms_set_02.prefab"
        };

        private readonly struct TrailSpline // 산책로 샘플
        {
            public readonly Vector2[] Points;
            public readonly float DirtHalfWidth;
            public readonly float MidBlendWidth;
            public readonly float GrassBlendWidth;

            public TrailSpline(Vector2[] points, float dirtHalfWidth, float midBlendWidth, float grassBlendWidth)
            {
                Points = points;
                DirtHalfWidth = dirtHalfWidth;
                MidBlendWidth = midBlendWidth;
                GrassBlendWidth = grassBlendWidth;
            }
        }

        private void Start() // 씬 시작 처리
        {
            if (BuildOnStart)
            {
                StartCoroutine(BuildRoutine()); // 로딩 UI 포함 생성
            }
        }

        [ContextMenu("Rebuild Meadow Terrain")]
        public void RebuildNow() // 에디터/런타임 수동 재생성
        {
            if (Application.isPlaying)
            {
                StartCoroutine(BuildRoutine()); // 플레이 중
                return;
            }

            BuildImmediate(); // 에디터 즉시 확인
        }

        private void BuildImmediate() // 에디터용 동기 생성
        {
            System.Diagnostics.Stopwatch totalWatch = StartTiming();
            System.Diagnostics.Stopwatch stageWatch = StartTiming();
            PrepareSceneVisuals();
            Transform parent = ResolveGeneratedParent();
            DestroyGeneratedRoot(parent);

            Vector2 center = ResolveNexusCenter();
            List<TrailSpline> trails = CreateTrailNetwork(center);
            LogTiming("BuildImmediate.prepare", stageWatch, "parent/cleanup/trails");
            if (UseDemoTerrainTemplateSurface && TryCreateGeneratedDemoTerrainObject(parent, center, trails))
            {
                LogTiming("BuildImmediate.total", totalWatch, "demo terrain path complete", false);
                return; // 데모 Terrain 경로에서는 메시 생성 스킵
            }

            System.Diagnostics.Stopwatch meshWatch = StartTiming();
            Mesh mesh = CreateMeshImmediate(center, trails);
            LogTiming("BuildImmediate.createMesh", meshWatch, "fallback mesh path");
            CreateGeneratedMeshObject(parent, mesh, center, trails);
            LogTiming("BuildImmediate.total", totalWatch, "fallback mesh path complete", false);
        }

        private IEnumerator BuildRoutine() // 생성 코루틴
        {
            System.Diagnostics.Stopwatch totalWatch = StartTiming();
            System.Diagnostics.Stopwatch stageWatch = StartTiming();
            PrepareSceneVisuals(); // Play 시작 즉시 기존 표시 정리
            LogTiming("BuildRoutine.prepareSceneVisuals", stageWatch);
            float startedAt = Time.unscaledTime;
            if (ShowLoadingOverlay)
            {
                overlay = MeadowLoadingOverlay.Create(LoadingCanvasName, LoadingTitle, LoadingStatus, LoadingBackgroundSprite);
                overlay.SetProgress(0.02f, LoadingStatus);
            }

            yield return null; // 로딩창 먼저 그리기

            overlay?.SetProgress(0.08f, "전장 잔상을 걷어내는 중...");
            yield return null;

            Transform parent = ResolveGeneratedParent();
            LogTiming("BuildRoutine.resolveGeneratedParent", stageWatch);
            DestroyGeneratedRoot(parent);
            LogTiming("BuildRoutine.destroyGeneratedRoot", stageWatch);
            overlay?.SetProgress(0.14f, "초원의 길을 여는 중...");
            yield return null;

            Vector2 center = ResolveNexusCenter();
            List<TrailSpline> trails = CreateTrailNetwork(center);
            LogTiming("BuildRoutine.createTrailNetwork", stageWatch);
            if (UseDemoTerrainTemplateSurface)
            {
                overlay?.SetProgress(0.2f, "초원 표면을 펼치는 중...");
                yield return null;

                if (TryCreateGeneratedDemoTerrainObject(parent, center, trails))
                {
                    LogTiming("BuildRoutine.demoTerrainPath", stageWatch);
                    overlay?.SetProgress(0.96f, "출격 구역을 정리하는 중...");
                    yield return null;

                    while (Time.unscaledTime - startedAt < MinimumLoadingSeconds)
                    {
                        yield return null; // 최소 표시 시간
                    }

                    overlay?.SetProgress(1f, "출격 준비 완료");
                    yield return null;
                    overlay?.Close();
                    overlay = null;
                    LogTiming("BuildRoutine.total", totalWatch, "demo terrain path complete", false);
                    yield break;
                }
            }

            MeshBuildContext context = CreateMeshBuildContext(center, trails);
            LogTiming("BuildRoutine.createMeshContext", stageWatch, $"rows={context.Rows}, columns={context.Columns}");
            overlay?.SetProgress(0.2f, "초원 표면을 펼치는 중...");

            int vertexRows = context.VertexRows;
            for (int z = 0; z < vertexRows; z++)
            {
                FillVertexRow(context, z);
                if (z % Mathf.Max(1, RowsPerFrame) == 0)
                {
                    float progress = Mathf.Lerp(0.2f, 0.72f, z / Mathf.Max(1f, vertexRows - 1f));
                    overlay?.SetProgress(progress, "흙길과 풀숲을 엮는 중...");
                    yield return null;
                }
            }

            LogTiming("BuildRoutine.fillVertices", stageWatch, $"vertexRows={vertexRows}");
            overlay?.SetProgress(0.74f, "전장 밀도를 조율하는 중...");
            int rows = context.Rows;
            for (int z = 0; z < rows; z++)
            {
                FillTriangleRow(context, z);
                if (z % Mathf.Max(1, TriangleRowsPerFrame) == 0)
                {
                    float progress = Mathf.Lerp(0.74f, 0.9f, z / Mathf.Max(1f, rows - 1f));
                    overlay?.SetProgress(progress, "전장 밀도를 조율하는 중...");
                    yield return null;
                }
            }

            LogTiming("BuildRoutine.fillTriangles", stageWatch, $"rows={rows}");
            Mesh mesh = CompleteMesh(context);
            LogTiming("BuildRoutine.completeMesh", stageWatch, $"vertices={context.Vertices.Length}, triangles={context.Triangles.Count / 3}");
            CreateGeneratedMeshObject(parent, mesh, center, trails);
            LogTiming("BuildRoutine.createGeneratedMeshObject", stageWatch);
            overlay?.SetProgress(0.96f, "출격 구역을 정리하는 중...");
            yield return null;

            while (Time.unscaledTime - startedAt < MinimumLoadingSeconds)
            {
                yield return null; // 최소 표시 시간
            }

            overlay?.SetProgress(1f, "출격 준비 완료");
            yield return null;
            overlay?.Close();
            overlay = null;
            LogTiming("BuildRoutine.total", totalWatch, "fallback mesh path complete", false);
        }

        private static System.Diagnostics.Stopwatch StartTiming() // 생성 시간 계측 시작
        {
            return System.Diagnostics.Stopwatch.StartNew();
        }

        private void LogTiming(string stageName, System.Diagnostics.Stopwatch stopwatch, string detail = null, bool restart = true) // 생성 시간 로그
        {
            if (!LogGenerationTimings || stopwatch == null)
            {
                return;
            }

            string detailText = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" | {detail}";
            Debug.Log($"[MeadowTerrainRuntimeGenerator] TIME {stageName}: {stopwatch.Elapsed.TotalMilliseconds:0.0} ms{detailText}", this);
            if (restart)
            {
                stopwatch.Restart();
            }
        }

        private void PrepareSceneVisuals() // 기존 표시 정리
        {
            if (HideLegacyVisualGround && !string.IsNullOrWhiteSpace(LegacyVisualGroundName))
            {
                GameObject legacy = GameObject.Find(LegacyVisualGroundName);
                if (legacy != null)
                {
                    legacy.SetActive(false); // 기존 비주얼 메시 숨김
                }
            }

            if (HideSourceGroundRenderer && !string.IsNullOrWhiteSpace(SourceGroundRendererName))
            {
                SetSourceGroundRendererVisible(true); // 생성 실패 시 대체 바닥 유지
            }

            if (HideLegacyTerrainObject && !string.IsNullOrWhiteSpace(LegacyTerrainObjectName))
            {
                GameObject terrain = GameObject.Find(LegacyTerrainObjectName);
                if (terrain != null && !terrain.name.Equals(GeneratedInstancedDetailsName, StringComparison.Ordinal))
                {
                    terrain.SetActive(false); // 데모 Terrain 잔존물 숨김
                }
            }
        }

        private void HideSourceGroundRendererIfSurfaceReady(string reason) // 생성 표면 준비 후 판정 평면 표시 숨김
        {
            if (!HideSourceGroundRenderer || string.IsNullOrWhiteSpace(SourceGroundRendererName))
            {
                return;
            }

            SetSourceGroundRendererVisible(false);
            if (LogGenerationTimings)
            {
                Debug.Log($"[MeadowTerrainRuntimeGenerator] GroundPlane renderer hidden after {reason}.", this);
            }
        }

        private void SetSourceGroundRendererVisible(bool visible) // GroundPlane Collider는 유지하고 Renderer만 전환
        {
            GameObject source = GameObject.Find(SourceGroundRendererName);
            Renderer renderer = source != null ? source.GetComponent<Renderer>() : null;
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }

        private Transform ResolveGeneratedParent() // 생성물 부모
        {
            if (GeneratedParent != null)
            {
                return GeneratedParent; // 지정 부모
            }

            GameObject source = !string.IsNullOrWhiteSpace(SourceGroundRendererName) ? GameObject.Find(SourceGroundRendererName) : null;
            if (source != null && source.transform.parent != null)
            {
                return source.transform.parent; // 기존 바닥 부모
            }

            return transform; // fallback
        }

        private Vector2 ResolveNexusCenter() // 중앙 기준
        {
            if (!string.IsNullOrWhiteSpace(NexusObjectName))
            {
                GameObject nexus = GameObject.Find(NexusObjectName);
                if (nexus != null)
                {
                    return new Vector2(nexus.transform.position.x, nexus.transform.position.z); // 실제 넥서스
                }
            }

            return new Vector2(transform.position.x, transform.position.z); // fallback
        }

        private List<TrailSpline> CreateTrailNetwork(Vector2 center) // 메인 2축
        {
            System.Random random = new System.Random(Seed);
            float playableHalf = Mathf.Max(5f, PlayableSize * 0.5f);
            float entryDepth = Mathf.Clamp(EntryZoneDepth, 1f, playableHalf);
            float entryHalfWidth = Mathf.Clamp(EntryZoneHalfWidth, 1f, playableHalf);

            Vector2 left = new Vector2(
                center.x - playableHalf + RandomRange(random, 0f, entryDepth),
                center.y + RandomRange(random, -entryHalfWidth, entryHalfWidth));
            Vector2 right = new Vector2(
                center.x + playableHalf - RandomRange(random, 0f, entryDepth),
                center.y + RandomRange(random, -entryHalfWidth, entryHalfWidth));
            Vector2 top = new Vector2(
                center.x + RandomRange(random, -entryHalfWidth, entryHalfWidth),
                center.y + playableHalf - RandomRange(random, 0f, entryDepth));
            Vector2 bottom = new Vector2(
                center.x + RandomRange(random, -entryHalfWidth, entryHalfWidth),
                center.y - playableHalf + RandomRange(random, 0f, entryDepth));

            List<TrailSpline> trails = new List<TrailSpline>();
            TrailSpline horizontal = CreateMainTrail(random, left, center, right, Vector2.up);
            TrailSpline vertical = CreateMainTrail(random, bottom, center, top, Vector2.right);
            trails.Add(horizontal);
            trails.Add(vertical);

            return trails;
        }

        private TrailSpline CreateMainTrail(System.Random random, Vector2 start, Vector2 center, Vector2 end, Vector2 sideAxis) // 주 흙길
        {
            float jitter = Mathf.Max(0f, MainControlJitter);
            Vector2 startControl = Vector2.Lerp(start, center, 0.42f) + sideAxis * RandomRange(random, -jitter, jitter);
            Vector2 endControl = Vector2.Lerp(center, end, 0.58f) + sideAxis * RandomRange(random, -jitter, jitter);
            List<Vector2> controls = new List<Vector2>
            {
                start,
                startControl,
                center,
                endControl,
                end
            };

            float widthScale = GetEffectiveMainTrailWidthScale();
            Vector2[] points = SampleCatmullRom(controls, 10);
            return new TrailSpline(points, MainDirtHalfWidth * widthScale, MainMidBlendWidth * widthScale, MainGrassBlendWidth * widthScale);
        }

        private Mesh CreateMeshImmediate(Vector2 center, List<TrailSpline> trails) // 동기 메시 생성
        {
            MeshBuildContext context = CreateMeshBuildContext(center, trails);
            for (int z = 0; z < context.VertexRows; z++)
            {
                FillVertexRow(context, z);
            }

            for (int z = 0; z < context.Rows; z++)
            {
                FillTriangleRow(context, z);
            }

            return CompleteMesh(context);
        }

        private MeshBuildContext CreateMeshBuildContext(Vector2 center, List<TrailSpline> trails) // 빌드 데이터
        {
            float size = Mathf.Max(10f, MapSize);
            float runtimeMaxCell = Mathf.Max(0.25f, MaxRuntimeCellSize);
            float cell = Mathf.Clamp(CellSize, 0.25f, runtimeMaxCell);
            int columns = Mathf.Max(1, Mathf.CeilToInt(size / cell));
            int rows = Mathf.Max(1, Mathf.CeilToInt(size / cell));
            int vertexColumns = columns + 1;
            int vertexRows = rows + 1;
            int vertexCount = vertexColumns * vertexRows;

            return new MeshBuildContext
            {
                Center = center,
                Trails = trails,
                MinX = center.x - size * 0.5f,
                MinZ = center.y - size * 0.5f,
                MaxX = center.x + size * 0.5f,
                MaxZ = center.y + size * 0.5f,
                CellSize = cell,
                Columns = columns,
                Rows = rows,
                VertexColumns = vertexColumns,
                VertexRows = vertexRows,
                Vertices = new Vector3[vertexCount],
                Uvs = new Vector2[vertexCount],
                Colors = new Color[vertexCount],
                Triangles = new List<int>(columns * rows * 6)
            };
        }

        private void FillVertexRow(MeshBuildContext context, int z) // 버텍스 한 줄
        {
            for (int x = 0; x < context.VertexColumns; x++)
            {
                float worldX = x == context.VertexColumns - 1 ? context.MaxX : context.MinX + x * context.CellSize;
                float worldZ = z == context.VertexRows - 1 ? context.MaxZ : context.MinZ + z * context.CellSize;
                float heightNoise = SampleFractalNoise(worldX, worldZ);
                float outerWeight = CalculateOuterWeight(worldX, worldZ, context.Center);
                float amplitude = Mathf.Lerp(PlayableHeightAmplitude, OuterHeightAmplitude, outerWeight);
                float rimRise = OuterRimRise * outerWeight * outerWeight;
                float worldY = VisualYOffset + heightNoise * amplitude + rimRise;
                int index = z * context.VertexColumns + x;

                context.Vertices[index] = new Vector3(worldX, worldY, worldZ);
                float textureSize = Mathf.Max(0.25f, TextureWorldSize);
                context.Uvs[index] = new Vector2(worldX / textureSize, worldZ / textureSize);
                context.Colors[index] = EvaluateTerrainBlend(new Vector2(worldX, worldZ), context.Center, context.Trails);
            }
        }

        private void FillTriangleRow(MeshBuildContext context, int z) // 삼각형 한 줄
        {
            for (int x = 0; x < context.Columns; x++)
            {
                int a = z * context.VertexColumns + x;
                int b = a + 1;
                int c = a + context.VertexColumns;
                int d = c + 1;
                context.Triangles.Add(a);
                context.Triangles.Add(c);
                context.Triangles.Add(b);
                context.Triangles.Add(b);
                context.Triangles.Add(c);
                context.Triangles.Add(d);
            }
        }

        private static Mesh CompleteMesh(MeshBuildContext context) // Unity Mesh 완성
        {
            Mesh mesh = new Mesh { name = GeneratedMeshName };
            mesh.indexFormat = context.Vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = context.Vertices;
            mesh.uv = context.Uvs;
            mesh.colors = context.Colors;
            mesh.triangles = context.Triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void CreateGeneratedMeshObject(Transform parent, Mesh mesh, Vector2 center, IReadOnlyList<TrailSpline> trails) // 오브젝트 생성
        {
            if (mesh == null)
            {
                Debug.LogWarning("[MeadowTerrainRuntimeGenerator] 생성된 메시가 없습니다.", this);
                return;
            }

            DestroyGeneratedRoot(parent);

            generatedRoot = new GameObject(GeneratedRootName);
            generatedRoot.transform.SetParent(parent, false);
            generatedRoot.transform.localPosition = Vector3.zero;
            generatedRoot.transform.localRotation = Quaternion.identity;
            generatedRoot.transform.localScale = Vector3.one;

            GameObject meshObject = new GameObject(GeneratedMeshName);
            meshObject.transform.SetParent(generatedRoot.transform, false);
            meshObject.transform.localPosition = Vector3.zero;
            meshObject.transform.localRotation = Quaternion.identity;
            meshObject.transform.localScale = Vector3.one;

            MeshFilter filter = meshObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            Texture2D controlTexture = UseHighQualityControlMap ? CreateTerrainControlTexture(center, trails) : null;
            Material terrainMaterial = ResolveTerrainMaterial();
            ConfigureControlMap(terrainMaterial, controlTexture, center);

            MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = terrainMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            HideSourceGroundRendererIfSurfaceReady("mesh fallback"); // 생성 바닥 준비 후 판정 평면 표시 숨김

            if (SpawnDecorations)
            {
                SpawnGeneratedDecorations(generatedRoot.transform, center, trails, false); // 자연물 런타임 배치
            }
        }

        private bool TryCreateGeneratedDemoTerrainObject(Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails) // 데모 Terrain 전용 빠른 생성
        {
            System.Diagnostics.Stopwatch totalWatch = StartTiming();
            System.Diagnostics.Stopwatch stageWatch = StartTiming();
            DestroyGeneratedRoot(parent);
            LogTiming("TryCreateGeneratedDemoTerrainObject.cleanup", stageWatch);

            generatedRoot = new GameObject(GeneratedRootName);
            generatedRoot.transform.SetParent(parent, false);
            generatedRoot.transform.localPosition = Vector3.zero;
            generatedRoot.transform.localRotation = Quaternion.identity;
            generatedRoot.transform.localScale = Vector3.one;
            LogTiming("TryCreateGeneratedDemoTerrainObject.createRoot", stageWatch);

            if (!TryCreateDemoTemplateTerrain(generatedRoot.transform, center, trails))
            {
                DestroyGeneratedRoot(parent);
                LogTiming("TryCreateGeneratedDemoTerrainObject.failed", totalWatch, null, false);
                return false;
            }
            LogTiming("TryCreateGeneratedDemoTerrainObject.createTerrain", stageWatch);

            if (SpawnDecorations)
            {
                System.Diagnostics.Stopwatch decorationWatch = StartTiming();
                SpawnGeneratedDecorations(generatedRoot.transform, center, trails, true); // 풀/꽃은 Terrain detail 사용
                LogTiming("TryCreateGeneratedDemoTerrainObject.decorations", decorationWatch);
            }

            LogTiming("TryCreateGeneratedDemoTerrainObject.total", totalWatch, null, false);
            return true;
        }

        private bool TryCreateDemoTemplateTerrain(Transform root, Vector2 center, IReadOnlyList<TrailSpline> trails) // TerrainData 표면 복제
        {
            System.Diagnostics.Stopwatch totalWatch = StartTiming();
            System.Diagnostics.Stopwatch stageWatch = StartTiming();
            TerrainData template = ResolveDemoTerrainTemplate();
            LogTiming("TryCreateDemoTemplateTerrain.resolveTemplate", stageWatch, template != null ? template.name : "null");
            if (template == null)
            {
                Debug.LogWarning("[MeadowTerrainRuntimeGenerator] 초원 TerrainData 템플릿을 찾지 못해 메시 표면으로 대체합니다.", this);
                return false;
            }

            DestroyRuntimeDemoTerrainData();
            runtimeDemoTerrainData = Instantiate(template);
            runtimeDemoTerrainData.name = GeneratedDemoTerrainDataName;
            runtimeDemoTerrainData.size = new Vector3(Mathf.Max(10f, MapSize), Mathf.Max(1f, OuterHeightAmplitude + OuterRimRise + 1.2f), Mathf.Max(10f, MapSize));
            runtimeDemoTerrainData.treePrototypes = Array.Empty<TreePrototype>();
            runtimeDemoTerrainData.SetTreeInstances(Array.Empty<TreeInstance>(), false);
            ApplyTerrainSurfaceProfile(runtimeDemoTerrainData);
            LogTiming("TryCreateDemoTemplateTerrain.instantiateAndResize", stageWatch, $"heightRes={runtimeDemoTerrainData.heightmapResolution}, alpha={runtimeDemoTerrainData.alphamapWidth}x{runtimeDemoTerrainData.alphamapHeight}, detail={runtimeDemoTerrainData.detailWidth}x{runtimeDemoTerrainData.detailHeight}");
            ApplyGeneratedHeightsToDemoTerrain(runtimeDemoTerrainData, center);
            LogTiming("TryCreateDemoTemplateTerrain.applyHeights", stageWatch);
            if (OverlayGeneratedTrailsOnDemoTerrain)
            {
                ApplyGeneratedTrailAlphamap(runtimeDemoTerrainData, center, trails);
                LogTiming("TryCreateDemoTemplateTerrain.applyAlphamap", stageWatch);
                if (ClearDemoDetailsOnTrails)
                {
                    RemoveDemoDetailsFromTrail(runtimeDemoTerrainData, center, trails);
                    LogTiming("TryCreateDemoTemplateTerrain.clearDetails", stageWatch);
                }
            }

            GameObject terrainObject = Terrain.CreateTerrainGameObject(runtimeDemoTerrainData);
            terrainObject.name = GeneratedDemoTerrainName;
            terrainObject.transform.SetParent(root, false);
            terrainObject.transform.position = new Vector3(center.x - MapSize * 0.5f, VisualYOffset - 0.6f, center.y - MapSize * 0.5f);
            terrainObject.transform.rotation = Quaternion.identity;
            terrainObject.transform.localScale = Vector3.one;

            Terrain terrain = terrainObject.GetComponent<Terrain>();
            if (terrain != null)
            {
                terrain.drawInstanced = UseInstancedTerrainRendering;
                terrain.detailObjectDistance = GetEffectiveDetailObjectDistance();
                terrain.detailObjectDensity = Mathf.Clamp01(GetEffectiveDetailObjectDensity());
                terrain.treeDistance = 0f; // 나무는 담당자 고정 배치/패턴 영역에서 처리
                terrain.shadowCastingMode = ShadowCastingMode.Off;
                terrain.reflectionProbeUsage = ReflectionProbeUsage.Off;
                ApplyTerrainSurfaceProfile(terrain);
                LogTerrainSurfaceSummary("TryCreateDemoTemplateTerrain.surface", runtimeDemoTerrainData, terrain);
                HideSourceGroundRendererIfSurfaceReady("demo terrain"); // Terrain 표면 준비 후 판정 평면 표시 숨김
            }

            TerrainCollider terrainCollider = terrainObject.GetComponent<TerrainCollider>();
            if (terrainCollider != null)
            {
                terrainCollider.enabled = false; // 실제 이동 판정은 GroundPlane Collider 유지
            }

            LogTiming("TryCreateDemoTemplateTerrain.createTerrainObject", stageWatch);
            LogTiming("TryCreateDemoTemplateTerrain.total", totalWatch, null, false);
            return true;
        }

        private TerrainData ResolveDemoTerrainTemplate() // TerrainData 템플릿 로드
        {
            if (SurfaceProfile != null && SurfaceProfile.TerrainTemplate != null)
            {
                return SurfaceProfile.TerrainTemplate;
            }

            if (TerrainTemplate != null)
            {
                return TerrainTemplate;
            }

#if UNITY_EDITOR
            if (UseMeadowNatureEditorFallbacks)
            {
                TerrainData projectCopy = AssetDatabase.LoadAssetAtPath<TerrainData>(ProjectTerrainTemplatePath);
                if (projectCopy != null)
                {
                    return projectCopy;
                }

                return AssetDatabase.LoadAssetAtPath<TerrainData>(DemoTerrainTemplatePath);
            }
#endif

            return null;
        }

        private void ApplyTerrainSurfaceProfile(TerrainData data) // TerrainData 레이어 명시 재연결
        {
            if (data == null || SurfaceProfile == null || !SurfaceProfile.ApplyTerrainLayers)
            {
                return;
            }

            TerrainLayer[] layers = CompactTerrainLayers(SurfaceProfile.TerrainLayers);
            if (layers.Length > 0)
            {
                data.terrainLayers = layers;
            }
        }

        private void ApplyTerrainSurfaceProfile(Terrain terrain) // Terrain 컴포넌트 표면 머티리얼 처리
        {
            if (terrain == null)
            {
                return;
            }

            if (SurfaceProfile == null || !SurfaceProfile.ApplyTerrainMaterial || SurfaceProfile.TerrainMaterial == null)
            {
                terrain.materialTemplate = null; // TerrainLayer 기반 기본 Terrain 머티리얼 사용
                return;
            }

            terrain.materialTemplate = SurfaceProfile.TerrainMaterial;
        }

        private void LogTerrainSurfaceSummary(string stageName, TerrainData data, Terrain terrain) // 빌드 표면 진단 로그
        {
            if (!LogGenerationTimings && (SurfaceProfile == null || !SurfaceProfile.LogSurfaceSummary))
            {
                return;
            }

            if (SurfaceProfile != null && !SurfaceProfile.LogSurfaceSummary)
            {
                return;
            }

            TerrainLayer[] layers = data != null ? data.terrainLayers : null;
            Material material = terrain != null ? terrain.materialTemplate : null;
            string materialName = material != null ? material.name : "DefaultTerrainMaterial";
            string shaderName = material != null && material.shader != null ? material.shader.name : "default";
            Vector3 size = data != null ? data.size : Vector3.zero;
            Debug.Log($"[MeadowTerrainRuntimeGenerator] SURFACE {stageName}: data={(data != null ? data.name : "null")}, size={size.x:0.##}x{size.y:0.##}x{size.z:0.##}, layers={DescribeTerrainLayers(layers)}, material={materialName}, shader={shaderName}", this);
        }

        private static TerrainLayer[] CompactTerrainLayers(TerrainLayer[] layers) // null TerrainLayer 제거
        {
            if (layers == null || layers.Length == 0)
            {
                return Array.Empty<TerrainLayer>();
            }

            List<TerrainLayer> compact = new List<TerrainLayer>(layers.Length);
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] != null)
                {
                    compact.Add(layers[i]);
                }
            }

            return compact.ToArray();
        }

        private static string DescribeTerrainLayers(IReadOnlyList<TerrainLayer> layers) // TerrainLayer/텍스처 로그 요약
        {
            if (layers == null || layers.Count == 0)
            {
                return "0 []";
            }

            int count = Mathf.Min(layers.Count, 8);
            string[] names = new string[count];
            for (int i = 0; i < count; i++)
            {
                TerrainLayer layer = layers[i];
                if (layer == null)
                {
                    names[i] = $"{i}:null";
                    continue;
                }

                string diffuse = layer.diffuseTexture != null ? layer.diffuseTexture.name : "no-diffuse";
                names[i] = $"{i}:{layer.name}/{diffuse}";
            }

            string suffix = layers.Count > count ? ", ..." : string.Empty;
            return $"{layers.Count} [{string.Join(", ", names)}{suffix}]";
        }

        private void ApplyGeneratedHeightsToDemoTerrain(TerrainData data, Vector2 center) // 플레이 영역 저굴곡 유지
        {
            System.Diagnostics.Stopwatch watch = StartTiming();
            int resolution = data.heightmapResolution;
            float[,] heights = new float[resolution, resolution];
            float size = Mathf.Max(10f, MapSize);
            float heightScale = Mathf.Max(1f, data.size.y);
            for (int z = 0; z < resolution; z++)
            {
                float worldZ = center.y - size * 0.5f + size * z / Mathf.Max(1f, resolution - 1f);
                for (int x = 0; x < resolution; x++)
                {
                    float worldX = center.x - size * 0.5f + size * x / Mathf.Max(1f, resolution - 1f);
                    float sampleHeight = SampleTerrainHeight(worldX, worldZ, center);
                    heights[z, x] = Mathf.Clamp01((sampleHeight + 0.6f) / heightScale);
                }
            }

            data.SetHeights(0, 0, heights);
            LogTiming("ApplyGeneratedHeightsToDemoTerrain.total", watch, $"resolution={resolution}");
        }

        private void ApplyGeneratedTrailAlphamap(TerrainData data, Vector2 center, IReadOnlyList<TrailSpline> trails) // 데모 splat 위에 흙길 덮기
        {
            System.Diagnostics.Stopwatch watch = StartTiming();
            int width = data.alphamapWidth;
            int height = data.alphamapHeight;
            int layers = data.alphamapLayers;
            if (layers < 3)
            {
                return;
            }

            TerrainLayerMapping layerMapping = ResolveTerrainLayerMapping(data);
            List<RectInt> editRects = CreateAlphamapEditRects(data, center, trails);
            if (editRects.Count == 0)
            {
                LogTiming("ApplyGeneratedTrailAlphamap.skip", watch, "rectCount=0");
                return;
            }

            float size = Mathf.Max(10f, MapSize);
            int visitedPixels = 0;
            int editedPixels = 0;
            int setAlphamapCalls = 0;
            for (int rectIndex = 0; rectIndex < editRects.Count; rectIndex++)
            {
                RectInt rect = editRects[rectIndex];
                float[,,] maps = data.GetAlphamaps(rect.x, rect.y, rect.width, rect.height);
                bool changed = false;
                for (int z = 0; z < rect.height; z++)
                {
                    float worldZ = AlphaIndexToWorldZ(rect.y + z, height, center, size);
                    for (int x = 0; x < rect.width; x++)
                    {
                        visitedPixels++;
                        float worldX = AlphaIndexToWorldX(rect.x + x, width, center, size);
                        Vector2 point = new Vector2(worldX, worldZ);
                        Color generated = ArtDirectTerrainBlend(EvaluateTerrainBlend(point, center, trails), point);
                        float pathInfluence = Mathf.Clamp01(generated.r + generated.g * 0.72f);
                        if (pathInfluence <= 0.01f)
                        {
                            continue;
                        }

                        editedPixels++;
                        changed = true;
                        float keepDemo = 1f - Smooth01(pathInfluence);
                        for (int layer = 0; layer < layers; layer++)
                        {
                            maps[z, x, layer] *= keepDemo;
                        }

                        maps[z, x, layerMapping.Dirt] = Mathf.Max(maps[z, x, layerMapping.Dirt], generated.r);
                        maps[z, x, layerMapping.DirtGrass] = Mathf.Max(maps[z, x, layerMapping.DirtGrass], generated.g);
                        maps[z, x, layerMapping.Grass] = Mathf.Max(maps[z, x, layerMapping.Grass], generated.b * (1f - generated.r));
                        NormalizeAlphamapPixel(maps, z, x, layers, layerMapping.Grass);
                    }
                }

                if (changed)
                {
                    data.SetAlphamaps(rect.x, rect.y, maps);
                    setAlphamapCalls++;
                }
            }

            LogTiming("ApplyGeneratedTrailAlphamap.total", watch, $"size={width}x{height}, layers={layers}, rects={editRects.Count}, visitedPixels={visitedPixels}, editedPixels={editedPixels}, setAlphamapCalls={setAlphamapCalls}");
        }

        private void RemoveDemoDetailsFromTrail(TerrainData data, Vector2 center, IReadOnlyList<TrailSpline> trails) // 흙길 위 detail 제거
        {
            System.Diagnostics.Stopwatch watch = StartTiming();
            int layerCount = data.detailPrototypes != null ? data.detailPrototypes.Length : 0;
            if (layerCount == 0)
            {
                LogTiming("RemoveDemoDetailsFromTrail.skip", watch, "layerCount=0");
                return;
            }

            List<RectInt> editRects = CreateDetailEditRects(data, center, trails);
            if (editRects.Count == 0)
            {
                LogTiming("RemoveDemoDetailsFromTrail.skip", watch, "rectCount=0");
                return;
            }

            float size = Mathf.Max(10f, MapSize);
            int visitedCells = 0;
            int changedCells = 0;
            int setLayerCalls = 0;
            for (int layer = 0; layer < layerCount; layer++)
            {
                for (int rectIndex = 0; rectIndex < editRects.Count; rectIndex++)
                {
                    RectInt rect = editRects[rectIndex];
                    int[,] values = data.GetDetailLayer(rect.x, rect.y, rect.width, rect.height, layer);
                    bool changed = false;
                    for (int z = 0; z < rect.height; z++)
                    {
                        float worldZ = DetailIndexToWorldZ(rect.y + z, data.detailHeight, center, size);
                        for (int x = 0; x < rect.width; x++)
                        {
                            visitedCells++;
                            if (values[z, x] == 0)
                            {
                                continue;
                            }

                            float worldX = DetailIndexToWorldX(rect.x + x, data.detailWidth, center, size);
                            Color generated = EvaluateTerrainBlend(new Vector2(worldX, worldZ), center, trails);
                            float clear = Mathf.Clamp01(generated.r * 1.2f + generated.g * 0.38f);
                            if (clear <= 0.05f)
                            {
                                continue;
                            }

                            values[z, x] = Mathf.RoundToInt(values[z, x] * (1f - Smooth01(clear)));
                            changedCells++;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        data.SetDetailLayer(rect.x, rect.y, layer, values);
                        setLayerCalls++;
                    }
                }
            }

            LogTiming("RemoveDemoDetailsFromTrail.total", watch, $"layers={layerCount}, rects={editRects.Count}, visitedCells={visitedCells}, changedCells={changedCells}, setLayerCalls={setLayerCalls}");
        }

        private List<RectInt> CreateAlphamapEditRects(TerrainData data, Vector2 center, IReadOnlyList<TrailSpline> trails) // alphamap 편집 범위 축소
        {
            List<RectInt> rects = new List<RectInt>();
            if (data == null)
            {
                return rects;
            }

            float padding = Mathf.Max(0f, DemoTerrainEditPadding) + EdgeNoiseAmplitude + 1.5f;
            if (trails != null)
            {
                for (int i = 0; i < trails.Count; i++)
                {
                    TrailSpline trail = trails[i];
                    if (trail.Points == null || trail.Points.Length == 0)
                    {
                        continue;
                    }

                    float minX = trail.Points[0].x;
                    float maxX = trail.Points[0].x;
                    float minZ = trail.Points[0].y;
                    float maxZ = trail.Points[0].y;
                    for (int pointIndex = 1; pointIndex < trail.Points.Length; pointIndex++)
                    {
                        Vector2 point = trail.Points[pointIndex];
                        minX = Mathf.Min(minX, point.x);
                        maxX = Mathf.Max(maxX, point.x);
                        minZ = Mathf.Min(minZ, point.y);
                        maxZ = Mathf.Max(maxZ, point.y);
                    }

                    float trailPadding = trail.DirtHalfWidth + trail.MidBlendWidth + trail.GrassBlendWidth + padding;
                    AddAlphamapEditRect(rects, data, center, minX - trailPadding, minZ - trailPadding, maxX + trailPadding, maxZ + trailPadding);
                }
            }

            float centerPadding = NexusClearingRadius + NexusClearingBlendWidth + padding;
            AddAlphamapEditRect(rects, data, center, center.x - centerPadding, center.y - centerPadding, center.x + centerPadding, center.y + centerPadding);
            return rects;
        }

        private void AddAlphamapEditRect(List<RectInt> rects, TerrainData data, Vector2 center, float minWorldX, float minWorldZ, float maxWorldX, float maxWorldZ) // world 범위 -> alphamap 범위
        {
            int width = data.alphamapWidth;
            int height = data.alphamapHeight;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            float size = Mathf.Max(10f, MapSize);
            int minX = Mathf.Clamp(WorldXToAlphaIndex(minWorldX, width, center, size), 0, width - 1);
            int maxX = Mathf.Clamp(WorldXToAlphaIndex(maxWorldX, width, center, size), 0, width - 1);
            int minZ = Mathf.Clamp(WorldZToAlphaIndex(minWorldZ, height, center, size), 0, height - 1);
            int maxZ = Mathf.Clamp(WorldZToAlphaIndex(maxWorldZ, height, center, size), 0, height - 1);
            int rectWidth = maxX - minX + 1;
            int rectHeight = maxZ - minZ + 1;
            if (rectWidth <= 0 || rectHeight <= 0)
            {
                return;
            }

            rects.Add(new RectInt(minX, minZ, rectWidth, rectHeight));
        }

        private static int WorldXToAlphaIndex(float worldX, int width, Vector2 center, float size) // world x -> alphamap x
        {
            float normalized = Mathf.InverseLerp(center.x - size * 0.5f, center.x + size * 0.5f, worldX);
            return Mathf.FloorToInt(normalized * Mathf.Max(1, width - 1));
        }

        private static int WorldZToAlphaIndex(float worldZ, int height, Vector2 center, float size) // world z -> alphamap z
        {
            float normalized = Mathf.InverseLerp(center.y - size * 0.5f, center.y + size * 0.5f, worldZ);
            return Mathf.FloorToInt(normalized * Mathf.Max(1, height - 1));
        }

        private static float AlphaIndexToWorldX(int x, int width, Vector2 center, float size) // alphamap x -> world x
        {
            return center.x - size * 0.5f + size * (x + 0.5f) / Mathf.Max(1f, width);
        }

        private static float AlphaIndexToWorldZ(int z, int height, Vector2 center, float size) // alphamap z -> world z
        {
            return center.y - size * 0.5f + size * (z + 0.5f) / Mathf.Max(1f, height);
        }

        private List<RectInt> CreateDetailEditRects(TerrainData data, Vector2 center, IReadOnlyList<TrailSpline> trails) // detail 편집 범위 축소
        {
            List<RectInt> rects = new List<RectInt>();
            if (data == null)
            {
                return rects;
            }

            float padding = Mathf.Max(0f, DemoTerrainEditPadding) + EdgeNoiseAmplitude + 1.5f;
            if (trails != null)
            {
                for (int i = 0; i < trails.Count; i++)
                {
                    TrailSpline trail = trails[i];
                    if (trail.Points == null || trail.Points.Length == 0)
                    {
                        continue;
                    }

                    float minX = trail.Points[0].x;
                    float maxX = trail.Points[0].x;
                    float minZ = trail.Points[0].y;
                    float maxZ = trail.Points[0].y;
                    for (int pointIndex = 1; pointIndex < trail.Points.Length; pointIndex++)
                    {
                        Vector2 point = trail.Points[pointIndex];
                        minX = Mathf.Min(minX, point.x);
                        maxX = Mathf.Max(maxX, point.x);
                        minZ = Mathf.Min(minZ, point.y);
                        maxZ = Mathf.Max(maxZ, point.y);
                    }

                    float trailPadding = trail.DirtHalfWidth + trail.MidBlendWidth + trail.GrassBlendWidth + padding;
                    AddDetailEditRect(rects, data, center, minX - trailPadding, minZ - trailPadding, maxX + trailPadding, maxZ + trailPadding);
                }
            }

            float centerPadding = NexusClearingRadius + NexusClearingBlendWidth + padding;
            AddDetailEditRect(rects, data, center, center.x - centerPadding, center.y - centerPadding, center.x + centerPadding, center.y + centerPadding);
            return rects;
        }

        private void AddDetailEditRect(List<RectInt> rects, TerrainData data, Vector2 center, float minWorldX, float minWorldZ, float maxWorldX, float maxWorldZ) // world 범위 -> detail 범위
        {
            int width = data.detailWidth;
            int height = data.detailHeight;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            float size = Mathf.Max(10f, MapSize);
            int minX = Mathf.Clamp(WorldXToDetailIndex(minWorldX, width, center, size), 0, width - 1);
            int maxX = Mathf.Clamp(WorldXToDetailIndex(maxWorldX, width, center, size), 0, width - 1);
            int minZ = Mathf.Clamp(WorldZToDetailIndex(minWorldZ, height, center, size), 0, height - 1);
            int maxZ = Mathf.Clamp(WorldZToDetailIndex(maxWorldZ, height, center, size), 0, height - 1);
            int rectWidth = maxX - minX + 1;
            int rectHeight = maxZ - minZ + 1;
            if (rectWidth <= 0 || rectHeight <= 0)
            {
                return;
            }

            rects.Add(new RectInt(minX, minZ, rectWidth, rectHeight));
        }

        private static int WorldXToDetailIndex(float worldX, int width, Vector2 center, float size) // world x -> detail x
        {
            float normalized = Mathf.InverseLerp(center.x - size * 0.5f, center.x + size * 0.5f, worldX);
            return Mathf.FloorToInt(normalized * Mathf.Max(1, width - 1));
        }

        private static int WorldZToDetailIndex(float worldZ, int height, Vector2 center, float size) // world z -> detail z
        {
            float normalized = Mathf.InverseLerp(center.y - size * 0.5f, center.y + size * 0.5f, worldZ);
            return Mathf.FloorToInt(normalized * Mathf.Max(1, height - 1));
        }

        private static float DetailIndexToWorldX(int x, int width, Vector2 center, float size) // detail x -> world x
        {
            return center.x - size * 0.5f + size * (x + 0.5f) / Mathf.Max(1f, width);
        }

        private static float DetailIndexToWorldZ(int z, int height, Vector2 center, float size) // detail z -> world z
        {
            return center.y - size * 0.5f + size * (z + 0.5f) / Mathf.Max(1f, height);
        }

        private static TerrainLayerMapping ResolveTerrainLayerMapping(TerrainData data) // 데모 TerrainLayer 이름 기준 매핑
        {
            TerrainLayer[] terrainLayers = data != null ? data.terrainLayers : null;
            int dirt = FindTerrainLayerIndex(terrainLayers, "Dirt Layer", 0);
            int dirtGrass = FindTerrainLayerIndex(terrainLayers, "Grass Dirt Layer", Mathf.Min(1, Mathf.Max(0, (terrainLayers?.Length ?? 1) - 1)));
            int grass = FindTerrainLayerIndex(terrainLayers, "Grass Layer", Mathf.Min(2, Mathf.Max(0, (terrainLayers?.Length ?? 1) - 1)));
            return new TerrainLayerMapping(dirt, dirtGrass, grass);
        }

        private static int FindTerrainLayerIndex(IReadOnlyList<TerrainLayer> terrainLayers, string exactName, int fallback) // TerrainLayer 인덱스 검색
        {
            if (terrainLayers == null || terrainLayers.Count == 0)
            {
                return Mathf.Max(0, fallback);
            }

            for (int i = 0; i < terrainLayers.Count; i++)
            {
                TerrainLayer layer = terrainLayers[i];
                if (layer != null && layer.name.Equals(exactName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return Mathf.Clamp(fallback, 0, terrainLayers.Count - 1);
        }

        private static void NormalizeAlphamapPixel(float[,,] maps, int z, int x, int layers, int fallbackGrassLayer) // splat 가중치 정규화
        {
            float sum = 0f;
            for (int layer = 0; layer < layers; layer++)
            {
                maps[z, x, layer] = Mathf.Max(0f, maps[z, x, layer]);
                sum += maps[z, x, layer];
            }

            if (sum <= 0.0001f)
            {
                for (int layer = 0; layer < layers; layer++)
                {
                    maps[z, x, layer] = 0f;
                }

                maps[z, x, Mathf.Clamp(fallbackGrassLayer, 0, layers - 1)] = 1f;
                return;
            }

            for (int layer = 0; layer < layers; layer++)
            {
                maps[z, x, layer] /= sum;
            }
        }

        private Texture2D CreateTerrainControlTexture(Vector2 center, IReadOnlyList<TrailSpline> trails) // 고해상도 splat map
        {
            DestroyRuntimeControlTexture();

            int resolution = Mathf.Clamp(ControlMapResolution, 256, 2048);
            float size = Mathf.Max(10f, MapSize);
            float minX = center.x - size * 0.5f;
            float minZ = center.y - size * 0.5f;
            float texelSize = size / resolution;
            Color[] pixels = new Color[resolution * resolution];

            for (int z = 0; z < resolution; z++)
            {
                float worldZ = minZ + (z + 0.5f) * texelSize;
                for (int x = 0; x < resolution; x++)
                {
                    float worldX = minX + (x + 0.5f) * texelSize;
                    Vector2 point = new Vector2(worldX, worldZ);
                    Color blend = EvaluateTerrainBlend(point, center, trails);
                    pixels[z * resolution + x] = ArtDirectTerrainBlend(blend, point);
                }
            }

            runtimeControlTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, true)
            {
                name = "MeadowTerrain_Control_Runtime",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 4
            };
            runtimeControlTexture.SetPixels(pixels);
            runtimeControlTexture.Apply(true, false);
            return runtimeControlTexture;
        }

        private Color ArtDirectTerrainBlend(Color blend, Vector2 point) // 데모 Terrain 느낌의 splat 보정
        {
            Vector3 weights = NormalizeTerrainWeights(new Vector3(blend.r, blend.g, blend.b));
            float fine = SampleSurfaceNoise(point.x, point.y, 0.62f, 3.17f);
            float mid = SampleSurfaceNoise(point.x, point.y, 0.18f, 7.43f);
            float broad = SampleSurfaceNoise(point.x, point.y, 0.055f, 13.89f);
            float mottle = Mathf.Clamp01(fine * 0.42f + mid * 0.42f + broad * 0.16f);

            if (weights.x > 0.52f)
            {
                float amount = DirtMottleStrength * weights.x * Smooth01(mottle);
                weights.x -= amount;
                weights.y += amount * 0.72f;
                weights.z += amount * 0.28f;
            }

            if (weights.z > 0.52f)
            {
                float amount = GrassMottleStrength * weights.z * Smooth01(1f - Mathf.Abs(mottle - 0.58f) * 1.7f);
                weights.z -= amount;
                weights.y += amount * 0.82f;
                weights.x += amount * 0.18f;
            }

            float dominant = Mathf.Max(weights.x, Mathf.Max(weights.y, weights.z));
            float edgeAmount = Mathf.Clamp01((1f - dominant) * 2.25f);
            if (edgeAmount > 0.001f)
            {
                float edgeNoise = (SampleSurfaceNoise(point.x, point.y, 0.31f, 29.11f) - 0.5f) * 2f;
                float shift = edgeNoise * EdgeMottleStrength * edgeAmount;
                weights.x = Mathf.Clamp01(weights.x + shift * 0.34f);
                weights.y = Mathf.Clamp01(weights.y + Mathf.Abs(shift) * 0.22f);
                weights.z = Mathf.Clamp01(weights.z - shift * 0.28f);
            }

            weights = NormalizeTerrainWeights(weights);
            return new Color(weights.x, weights.y, weights.z, 1f);
        }

        private float SampleSurfaceNoise(float worldX, float worldZ, float frequency, float salt) // 표면용 노이즈
        {
            float seedX = Seed * 0.0193f + salt * 17.31f;
            float seedZ = Seed * 0.0247f + salt * 31.17f;
            return Mathf.PerlinNoise(worldX * frequency + seedX, worldZ * frequency + seedZ);
        }

        private static Vector3 NormalizeTerrainWeights(Vector3 weights) // RGB splat 정규화
        {
            weights.x = Mathf.Max(0f, weights.x);
            weights.y = Mathf.Max(0f, weights.y);
            weights.z = Mathf.Max(0f, weights.z);
            float sum = weights.x + weights.y + weights.z;
            return sum <= 0.0001f ? new Vector3(0f, 0f, 1f) : weights / sum;
        }

        private void ConfigureControlMap(Material material, Texture2D controlTexture, Vector2 center) // control map 연결
        {
            if (material == null)
            {
                return;
            }

            material.SetVector("_ControlMapCenterSize", new Vector4(center.x, center.y, Mathf.Max(10f, MapSize), 0f));
            if (controlTexture != null)
            {
                material.SetTexture("_ControlTex", controlTexture);
                material.SetFloat("_ControlMapBlend", ControlMapBlend);
            }
            else
            {
                material.SetFloat("_ControlMapBlend", 0f);
            }
        }

        private void SpawnGeneratedDecorations(Transform root, Vector2 center, IReadOnlyList<TrailSpline> trails, bool surfaceAlreadyHasDetails) // 자연물 군집 배치
        {
            DecorationCatalog catalog = ResolveDecorationCatalog();
            if (!catalog.HasAnyPrefab)
            {
                Debug.LogWarning("[MeadowTerrainRuntimeGenerator] 자연물 프리팹을 찾지 못해 장식 배치를 건너뜁니다.", this);
                return;
            }

            Transform decorationRoot = CreateChildRoot(root, "GeneratedMeadowDecorations");
            Transform patternRoot = CreateChildRoot(decorationRoot, "PatternClusters");
            bool terrainDetailsBuilt = surfaceAlreadyHasDetails || (UseTerrainDetailsForGrassAndFlowers && CreateGeneratedDetailTerrain(decorationRoot, center, trails, catalog));
            int flowerPatternCount = terrainDetailsBuilt ? Mathf.RoundToInt(FlowerPatchCount * GetEffectiveDetailBackedFlowerAccentRatio()) : FlowerPatchCount;
            int grassPatternCount = terrainDetailsBuilt ? Mathf.RoundToInt(GrassPatchCount * GetEffectiveDetailBackedGrassAccentRatio()) : GrassPatchCount;
            int rockPatternCount = GetEffectiveRockClusterCount();
            contactShadowDiagnosticCenter = center;
            contactShadowDiagnosticDemoTerrain = surfaceAlreadyHasDetails;
            ResetClusterContactShadowDiagnostics();

            System.Random random = new System.Random(Seed + 9709);
            DecorationPatternContext context = new DecorationPatternContext(
                patternRoot,
                TreeClusterCount + HeroTreeAccentCount,
                rockPatternCount,
                flowerPatternCount,
                grassPatternCount);

            SpawnPatternGroup(context, catalog, center, trails, random, TreeClusterCount, TreeClusterMinSpacing, DecorationZone.Tree, DecorationPatternBand.TreeGrove);
            SpawnPatternGroup(context, catalog, center, trails, random, rockPatternCount, RockClusterMinSpacing, DecorationZone.Rock, DecorationPatternBand.RockGarden);
            SpawnPatternGroup(context, catalog, center, trails, random, HeroTreeAccentCount, TreeClusterMinSpacing * 1.25f, DecorationZone.HeroTree, DecorationPatternBand.Hero);
            SpawnPatternGroup(context, catalog, center, trails, random, NexusOuterAccentCount, 5.2f, DecorationZone.NexusOuter, DecorationPatternBand.NexusOuter);

            SpawnPatternGroup(context, catalog, center, trails, random, flowerPatternCount, FlowerPatchMinSpacing, DecorationZone.FlowerPathEdge, DecorationPatternBand.PathEdge);
            SpawnPatternGroup(context, catalog, center, trails, random, grassPatternCount, GrassPatchMinSpacing, DecorationZone.GrassPatch, DecorationPatternBand.GrassPocket);
            SpawnPandazoleAddonDecorations(decorationRoot, center, trails, random, context);
            LogClusterContactShadowDiagnostics(surfaceAlreadyHasDetails, terrainDetailsBuilt, rockPatternCount, flowerPatternCount, grassPatternCount, context);
        }

        private bool CreateGeneratedDetailTerrain(Transform decorationRoot, Vector2 center, IReadOnlyList<TrailSpline> trails, DecorationCatalog catalog) // 풀/꽃 지형 밀도 인스턴싱
        {
            List<TerrainDetailEntry> entries = CreateTerrainDetailEntries(catalog);
            if (entries.Count == 0)
            {
                Debug.LogWarning("[MeadowTerrainRuntimeGenerator] 풀/꽃 인스턴스 후보 프리팹을 찾지 못했습니다.", this);
                return false;
            }

            List<MeadowInstancedDetailRenderer.DrawBatch> batches = CreateInstancedDetailBatches(entries, center, trails);
            if (batches.Count == 0)
            {
                Debug.LogWarning("[MeadowTerrainRuntimeGenerator] 풀/꽃 인스턴스 배치가 0개라 렌더러 생성을 건너뜁니다.", this);
                return false;
            }

            Transform detailRoot = CreateChildRoot(decorationRoot, "InstancedDetails");
            GameObject rendererObject = new GameObject(GeneratedInstancedDetailsName);
            rendererObject.transform.SetParent(detailRoot, false);
            rendererObject.transform.localPosition = Vector3.zero;
            rendererObject.transform.localRotation = Quaternion.identity;
            rendererObject.transform.localScale = Vector3.one;

            MeadowInstancedDetailRenderer renderer = rendererObject.AddComponent<MeadowInstancedDetailRenderer>();
            renderer.Initialize(batches, GetEffectiveDetailObjectDistance(), new Vector3(center.x, 0f, center.y));
            return true;
        }

        private List<MeadowInstancedDetailRenderer.DrawBatch> CreateInstancedDetailBatches(IReadOnlyList<TerrainDetailEntry> entries, Vector2 center, IReadOnlyList<TrailSpline> trails) // GPU 인스턴스 배치 생성
        {
            List<MeadowInstancedDetailRenderer.DrawBatch> batches = new List<MeadowInstancedDetailRenderer.DrawBatch>();

            for (int i = 0; i < entries.Count; i++)
            {
                TerrainDetailEntry entry = entries[i];
                int totalLimit = entry.Kind == TerrainDetailKind.Grass ? GetEffectiveMaxGrassInstances() : GetEffectiveMaxFlowerInstances();
                float totalWeight = SumDetailEntryWeights(entries, entry.Kind);
                int entryLimit = Mathf.Max(0, Mathf.RoundToInt(totalLimit * entry.InstanceShare / Mathf.Max(0.001f, totalWeight)));
                if (entryLimit <= 0)
                {
                    continue;
                }

                List<Matrix4x4> matrices = CreateInstancedDetailMatrices(entry, i, entryLimit, center, trails);
                if (matrices.Count == 0)
                {
                    continue;
                }

                AddInstancedDetailPrefabBatches(batches, entry.Prefab, matrices);
            }

            return batches;
        }

        private static float SumDetailEntryWeights(IReadOnlyList<TerrainDetailEntry> entries, TerrainDetailKind kind) // 종류별 상한 배분 가중치
        {
            if (entries == null)
            {
                return 0f;
            }

            float total = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Kind == kind)
                {
                    total += Mathf.Max(0.001f, entries[i].InstanceShare);
                }
            }

            return total;
        }

        private List<Matrix4x4> CreateInstancedDetailMatrices(TerrainDetailEntry entry, int prototypeIndex, int maxInstances, Vector2 center, IReadOnlyList<TrailSpline> trails) // 위치/회전/스케일 샘플링
        {
            if (!entry.IsCarpetGrass)
            {
                return CreateClusteredDetailMatrices(entry, prototypeIndex, maxInstances, center, trails);
            }

            List<Matrix4x4> matrices = new List<Matrix4x4>(Mathf.Min(maxInstances, 4096));
            int sampleResolution = Mathf.Clamp(Mathf.RoundToInt(DetailResolution * 0.48f), 128, 288);
            float size = Mathf.Max(10f, MapSize);
            float minX = center.x - size * 0.5f;
            float minZ = center.y - size * 0.5f;
            float cellSize = size / sampleResolution;
            float densityScale = entry.Kind == TerrainDetailKind.Grass ? 0.22f : 0.006f;
            System.Random random = new System.Random(Seed + 14033 + prototypeIndex * 997);
            int acceptedCandidates = 0;

            for (int z = 0; z < sampleResolution; z++)
            {
                for (int x = 0; x < sampleResolution; x++)
                {
                    float worldX = minX + (x + (float)random.NextDouble()) * cellSize;
                    float worldZ = minZ + (z + (float)random.NextDouble()) * cellSize;
                    Vector2 point = new Vector2(worldX, worldZ);
                    float density = EvaluateTerrainDetailDensity(entry, prototypeIndex, point, center, trails);
                    if (density <= 0.001f)
                    {
                        continue;
                    }

                    float breakup = SampleSurfaceNoise(worldX, worldZ, entry.Kind == TerrainDetailKind.Grass ? 1.35f : 0.82f, 503.7f + prototypeIndex * 17.19f);
                    float chance = density * entry.CellDensity * densityScale * entry.DensityMultiplier * GetEffectiveDetailObjectDensity() * Mathf.Lerp(0.55f, 1.25f, breakup);
                    if ((float)random.NextDouble() > Mathf.Clamp01(chance))
                    {
                        continue;
                    }

                    TryAddDetailMatrixCandidate(entry, point, center, random, maxInstances, matrices, ref acceptedCandidates);
                }
            }

            return matrices;
        }

        private List<Matrix4x4> CreateClusteredDetailMatrices(TerrainDetailEntry entry, int prototypeIndex, int maxInstances, Vector2 center, IReadOnlyList<TrailSpline> trails) // 꽃/보조 풀 군집 샘플링
        {
            List<Matrix4x4> matrices = new List<Matrix4x4>(Mathf.Min(maxInstances, 2048));
            if (maxInstances <= 0)
            {
                return matrices;
            }

            int sampleResolution = Mathf.Clamp(Mathf.RoundToInt(DetailResolution * 0.18f), 48, 96);
            float size = Mathf.Max(10f, MapSize);
            float minX = center.x - size * 0.5f;
            float minZ = center.y - size * 0.5f;
            float cellSize = size / sampleResolution;
            float densityScale = entry.Kind == TerrainDetailKind.Flower ? 0.22f : 0.035f;
            System.Random random = new System.Random(Seed + 51437 + prototypeIndex * 1733);
            int acceptedCandidates = 0;

            for (int z = 0; z < sampleResolution; z++)
            {
                for (int x = 0; x < sampleResolution; x++)
                {
                    float worldX = minX + (x + (float)random.NextDouble()) * cellSize;
                    float worldZ = minZ + (z + (float)random.NextDouble()) * cellSize;
                    Vector2 clusterCenter = new Vector2(worldX, worldZ);
                    float density = EvaluateTerrainDetailDensity(entry, prototypeIndex, clusterCenter, center, trails);
                    if (density <= 0.001f)
                    {
                        continue;
                    }

                    float chance = density * entry.CellDensity * densityScale * entry.DensityMultiplier * GetEffectiveDetailObjectDensity();
                    if ((float)random.NextDouble() > Mathf.Clamp01(chance))
                    {
                        continue;
                    }

                    int memberCount = GetDetailClusterMemberCount(entry, random);
                    float clusterRadius = GetDetailClusterRadius(entry, random);
                    for (int memberIndex = 0; memberIndex < memberCount; memberIndex++)
                    {
                        Vector2 point = clusterCenter;
                        if (memberIndex > 0)
                        {
                            point += RandomOffsetInCircle(random, clusterRadius);
                        }

                        if (EvaluateTerrainDetailDensity(entry, prototypeIndex, point, center, trails) <= 0.001f)
                        {
                            continue;
                        }

                        TryAddDetailMatrixCandidate(entry, point, center, random, maxInstances, matrices, ref acceptedCandidates);
                    }
                }
            }

            return matrices;
        }

        private void TryAddDetailMatrixCandidate(TerrainDetailEntry entry, Vector2 point, Vector2 center, System.Random random, int maxInstances, List<Matrix4x4> matrices, ref int acceptedCandidates) // 상한 초과 시 균등 추출
        {
            if (maxInstances <= 0 || matrices == null)
            {
                return;
            }

            float y = SampleTerrainHeight(point.x, point.y, center) + DecorationSurfaceOffset + (entry.Kind == TerrainDetailKind.Grass ? 0.008f : 0.014f);
            Quaternion rotation = Quaternion.Euler(0f, RandomRange(random, 0f, 360f), 0f);
            float width = RandomRange(random, entry.MinWidth, entry.MaxWidth);
            float height = RandomRange(random, entry.MinHeight, entry.MaxHeight);
            Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(point.x, y, point.y), rotation, new Vector3(width, height, width));
            acceptedCandidates++;
            if (matrices.Count < maxInstances)
            {
                matrices.Add(matrix);
                return;
            }

            int replaceIndex = random.Next(acceptedCandidates);
            if (replaceIndex < maxInstances)
            {
                matrices[replaceIndex] = matrix;
            }
        }

        private static int GetDetailClusterMemberCount(TerrainDetailEntry entry, System.Random random) // 군집당 개수
        {
            return entry.Kind == TerrainDetailKind.Flower ? random.Next(4, 10) : random.Next(5, 12);
        }

        private static float GetDetailClusterRadius(TerrainDetailEntry entry, System.Random random) // 군집 반경
        {
            return entry.Kind == TerrainDetailKind.Flower ? RandomRange(random, 0.9f, 2.2f) : RandomRange(random, 0.8f, 2.0f);
        }

        private static Vector2 RandomOffsetInCircle(System.Random random, float radius) // 원형 군집 오프셋
        {
            float angle = RandomRange(random, 0f, Mathf.PI * 2f);
            float distance = Mathf.Sqrt((float)random.NextDouble()) * radius;
            return new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
        }

        private static int CountDetailEntries(IReadOnlyList<TerrainDetailEntry> entries, TerrainDetailKind kind) // 종류별 후보 수
        {
            if (entries == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AddInstancedDetailPrefabBatches(List<MeadowInstancedDetailRenderer.DrawBatch> batches, GameObject prefab, IReadOnlyList<Matrix4x4> rootMatrices) // 프리팹 MeshRenderer를 draw batch로 변환
        {
            if (batches == null || prefab == null || rootMatrices == null || rootMatrices.Count == 0)
            {
                return;
            }

            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                Mesh mesh = filter.sharedMesh;
                if (renderer == null || mesh == null)
                {
                    continue;
                }

                Matrix4x4 localMatrix = CalculatePrefabLocalMatrix(filter.transform, prefab.transform);
                Matrix4x4[] matrices = new Matrix4x4[rootMatrices.Count];
                for (int m = 0; m < rootMatrices.Count; m++)
                {
                    matrices[m] = rootMatrices[m] * localMatrix;
                }

                Material[] materials = renderer.sharedMaterials;
                int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
                for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    Material source = ResolveSubMeshMaterial(materials, subMesh);
                    if (source == null)
                    {
                        continue;
                    }

                    Material material = new Material(source)
                    {
                        name = $"{source.name}_InstancedDetailRuntime",
                        enableInstancing = true
                    };
                    batches.Add(new MeadowInstancedDetailRenderer.DrawBatch(mesh, subMesh, material, matrices, renderer.receiveShadows));
                }
            }
        }

        private static Material ResolveSubMeshMaterial(IReadOnlyList<Material> materials, int subMesh) // submesh material fallback
        {
            if (materials == null || materials.Count == 0)
            {
                return null;
            }

            if (subMesh >= 0 && subMesh < materials.Count && materials[subMesh] != null)
            {
                return materials[subMesh];
            }

            return materials[0];
        }

        private static Matrix4x4 CalculatePrefabLocalMatrix(Transform meshTransform, Transform root) // 프리팹 내부 MeshFilter 로컬 행렬
        {
            if (meshTransform == null || root == null || meshTransform == root)
            {
                return Matrix4x4.identity;
            }

            List<Transform> chain = new List<Transform>();
            Transform current = meshTransform;
            while (current != null && current != root)
            {
                chain.Add(current);
                current = current.parent;
            }

            Matrix4x4 matrix = Matrix4x4.identity;
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                Transform item = chain[i];
                matrix *= Matrix4x4.TRS(item.localPosition, item.localRotation, item.localScale);
            }

            return matrix;
        }

        private List<TerrainDetailEntry> CreateTerrainDetailEntries(DecorationCatalog catalog) // Terrain Detail 후보 구성
        {
            List<TerrainDetailEntry> entries = new List<TerrainDetailEntry>(GetEffectiveGrassPrototypeLimit() + GetEffectiveFlowerPrototypeLimit());
            AddPreferredTerrainDetailEntry(entries, catalog.Grass, "SM_grass_short", TerrainDetailKind.Grass, 0.78f, 1.34f, 0.78f, 1.38f, GetEffectiveGrassDetailDensity(), 8.2f, 78f);
            AddPreferredTerrainDetailEntry(entries, catalog.Grass, "SM_grass_01", TerrainDetailKind.Grass, 0.74f, 1.28f, 0.82f, 1.42f, GetEffectiveGrassDetailDensity(), 7.4f, 68f);
            AddPreferredTerrainDetailEntry(entries, catalog.Grass, "SM_shamrocks", TerrainDetailKind.Grass, 0.5f, 0.86f, 0.52f, 0.9f, GetEffectiveGrassDetailDensity(), 0.18f, 1.4f);
            AddPreferredTerrainDetailEntry(entries, catalog.Grass, "SM_plant_green_03", TerrainDetailKind.Grass, 0.52f, 0.86f, 0.58f, 0.94f, GetEffectiveGrassDetailDensity(), 0.12f, 0.9f);
            AddPreferredTerrainDetailEntry(entries, catalog.Grass, "SM_plant_green_01", TerrainDetailKind.Grass, 0.52f, 0.86f, 0.58f, 0.94f, GetEffectiveGrassDetailDensity(), 0.1f, 0.75f);
            AddPreferredTerrainDetailEntry(entries, catalog.Grass, "SM_plant_leaves_02", TerrainDetailKind.Grass, 0.48f, 0.78f, 0.52f, 0.86f, GetEffectiveGrassDetailDensity(), 0.07f, 0.5f);
            AddPreferredTerrainDetailEntry(entries, catalog.Grass, "SM_plant_leaves_01", TerrainDetailKind.Grass, 0.48f, 0.78f, 0.52f, 0.86f, GetEffectiveGrassDetailDensity(), 0.06f, 0.45f);
            AddPreferredTerrainDetailEntry(entries, catalog.Grass, "SM_plant_green_02", TerrainDetailKind.Grass, 0.48f, 0.78f, 0.52f, 0.86f, GetEffectiveGrassDetailDensity(), 0.045f, 0.35f);
            AddPreferredTerrainDetailEntry(entries, catalog.Grass, "SM_ivy_01", TerrainDetailKind.Grass, 0.45f, 0.72f, 0.48f, 0.78f, GetEffectiveGrassDetailDensity(), 0.025f, 0.18f);
            AddPreferredTerrainDetailEntry(entries, catalog.Grass, "SM_ivy_02", TerrainDetailKind.Grass, 0.45f, 0.72f, 0.48f, 0.78f, GetEffectiveGrassDetailDensity(), 0.02f, 0.14f);

            AddPreferredTerrainDetailEntry(entries, catalog.FlowerAccents, "SM_flower_loosetrifes", TerrainDetailKind.Flower, 0.44f, 0.78f, 0.58f, 0.96f, GetEffectiveFlowerDetailDensity(), 0.13f, 1.5f);
            AddPreferredTerrainDetailEntry(entries, catalog.FlowerAccents, "SM_flower_daffodil", TerrainDetailKind.Flower, 0.42f, 0.74f, 0.58f, 0.94f, GetEffectiveFlowerDetailDensity(), 0.11f, 1.25f);
            AddPreferredTerrainDetailEntry(entries, catalog.FlowerAccents, "SM_flower_tall_02", TerrainDetailKind.Flower, 0.38f, 0.7f, 0.66f, 1.02f, GetEffectiveFlowerDetailDensity(), 0.09f, 1.0f);
            AddPreferredTerrainDetailEntry(entries, catalog.FlowerAccents, "SM_flower_tall_01", TerrainDetailKind.Flower, 0.38f, 0.7f, 0.66f, 1.02f, GetEffectiveFlowerDetailDensity(), 0.07f, 0.8f);
            AddPreferredTerrainDetailEntry(entries, catalog.FlowerAccents, "SM_flower_tall_03", TerrainDetailKind.Flower, 0.38f, 0.7f, 0.66f, 1.02f, GetEffectiveFlowerDetailDensity(), 0.06f, 0.7f);
            AddPreferredTerrainDetailEntry(entries, catalog.FlowerGroups, "SM_plant_flowers_01", TerrainDetailKind.Flower, 0.52f, 0.86f, 0.56f, 0.92f, GetEffectiveFlowerDetailDensity(), 0.06f, 0.7f);
            AddPreferredTerrainDetailEntry(entries, catalog.FlowerGroups, "SM_plant_flowers_03", TerrainDetailKind.Flower, 0.52f, 0.86f, 0.56f, 0.92f, GetEffectiveFlowerDetailDensity(), 0.04f, 0.5f);
            AddPreferredTerrainDetailEntry(entries, catalog.FlowerGroups, "SM_flower_group_01", TerrainDetailKind.Flower, 0.56f, 0.92f, 0.58f, 0.96f, GetEffectiveFlowerDetailDensity(), 0.025f, 0.3f);

            if (CountDetailEntries(entries, TerrainDetailKind.Grass) == 0)
            {
                AddTerrainDetailEntries(entries, catalog.Grass, TerrainDetailKind.Grass, GetEffectiveGrassPrototypeLimit(), 0.66f, 1.14f, 0.7f, 1.24f, GetEffectiveGrassDetailDensity(), 1.2f, 1f);
            }

            if (CountDetailEntries(entries, TerrainDetailKind.Flower) == 0)
            {
                AddTerrainDetailEntries(entries, catalog.FlowerGroups, TerrainDetailKind.Flower, GetEffectiveFlowerPrototypeLimit(), 0.6f, 1.0f, 0.64f, 1.08f, GetEffectiveFlowerDetailDensity(), 0.08f, 0.5f);
                AddTerrainDetailEntries(entries, catalog.FlowerAccents, TerrainDetailKind.Flower, GetEffectiveFlowerPrototypeLimit(), 0.46f, 0.84f, 0.64f, 1.08f, GetEffectiveFlowerDetailDensity(), 0.08f, 0.5f);
            }

            return entries;
        }

        private static void AddPreferredTerrainDetailEntry(List<TerrainDetailEntry> entries, IReadOnlyList<GameObject> prefabs, string prefabName, TerrainDetailKind kind, float minWidth, float maxWidth, float minHeight, float maxHeight, int cellDensity, float densityMultiplier, float instanceShare) // 이름 기반 후보 추가
        {
            if (entries == null || prefabs == null || string.IsNullOrWhiteSpace(prefabName))
            {
                return;
            }

            for (int i = 0; i < prefabs.Count; i++)
            {
                GameObject prefab = prefabs[i];
                if (prefab != null && prefab.name.Equals(prefabName, StringComparison.Ordinal))
                {
                    AddTerrainDetailEntry(entries, prefab, kind, minWidth, maxWidth, minHeight, maxHeight, cellDensity, densityMultiplier, instanceShare);
                    return;
                }
            }
        }

        private static void AddTerrainDetailEntries(List<TerrainDetailEntry> entries, IReadOnlyList<GameObject> prefabs, TerrainDetailKind kind, int maxCount, float minWidth, float maxWidth, float minHeight, float maxHeight, int cellDensity, float densityMultiplier, float instanceShare) // 후보 추가
        {
            if (entries == null || prefabs == null || maxCount <= 0 || cellDensity <= 0)
            {
                return;
            }

            int added = 0;
            for (int i = 0; i < prefabs.Count && added < maxCount; i++)
            {
                GameObject prefab = prefabs[i];
                if (!CanUseAsTerrainDetail(prefab))
                {
                    continue;
                }

                AddTerrainDetailEntry(entries, prefab, kind, minWidth, maxWidth, minHeight, maxHeight, cellDensity, densityMultiplier, instanceShare);
                added++;
            }
        }

        private static void AddTerrainDetailEntry(List<TerrainDetailEntry> entries, GameObject prefab, TerrainDetailKind kind, float minWidth, float maxWidth, float minHeight, float maxHeight, int cellDensity, float densityMultiplier, float instanceShare) // 단일 후보 추가
        {
            if (entries == null || !CanUseAsTerrainDetail(prefab))
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Prefab == prefab)
                {
                    return;
                }
            }

            entries.Add(new TerrainDetailEntry(prefab, kind, minWidth, maxWidth, minHeight, maxHeight, cellDensity, Mathf.Max(0.01f, densityMultiplier), Mathf.Max(0.01f, instanceShare)));
        }

        private static bool CanUseAsTerrainDetail(GameObject prefab) // Terrain Detail Mesh 조건
        {
            return prefab != null &&
                prefab.GetComponentInChildren<MeshFilter>(true) != null &&
                prefab.GetComponentInChildren<MeshRenderer>(true) != null;
        }

        private static bool IsCarpetGrassPrefab(GameObject prefab) // 일반 잔디 카펫 판정
        {
            return prefab != null && prefab.name.StartsWith("SM_grass_", StringComparison.Ordinal);
        }

        private float GetEffectiveDetailObjectDensity() // 인스펙터 0값 방어
        {
            return Mathf.Max(0.7f, DetailObjectDensity);
        }

        private int GetEffectiveGrassDetailDensity() // 데모 비율용 풀 밀도
        {
            return Mathf.Max(GrassDetailDensity, 18);
        }

        private int GetEffectiveFlowerDetailDensity() // 데모 비율용 꽃 밀도
        {
            return Mathf.Max(FlowerDetailDensity, 2);
        }

        private int GetEffectiveGrassPrototypeLimit() // 기존 씬 직렬화값 방어
        {
            return Mathf.Max(MaxGrassDetailPrototypes, 10);
        }

        private int GetEffectiveFlowerPrototypeLimit() // 기존 씬 직렬화값 방어
        {
            return Mathf.Max(MaxFlowerDetailPrototypes, 8);
        }

        private int GetEffectiveMaxGrassInstances() // 데모 잔풀 카펫 밀도 보장
        {
            return Mathf.Max(MaxGrassInstances, 120000);
        }

        private int GetEffectiveMaxFlowerInstances() // 꽃은 군집 위주로 제한
        {
            return MaxFlowerInstances <= 0 ? 0 : Mathf.Min(MaxFlowerInstances, 650);
        }

        private float GetEffectivePathEdgeFlowerBoost() // 오래된 씬 직렬화값 꽃 과다 방어
        {
            return Mathf.Min(PathEdgeFlowerBoost, 0.28f);
        }

        private float GetEffectiveMainTrailWidthScale() // 큰 흙길 전용 폭 배율
        {
            return Mathf.Clamp(MainTrailWidthScale, 0.25f, 1.5f);
        }

        private float GetEffectiveMainBlendOuterWidth() // 길가 장식 판정용 전이 반폭
        {
            return (MainMidBlendWidth + MainGrassBlendWidth) * GetEffectiveMainTrailWidthScale();
        }

        private float GetEffectiveMeadowFlowerChance() // 일반 잔디 우선 비율 보장
        {
            return Mathf.Min(MeadowFlowerChance, 0.12f);
        }

        private float GetEffectiveDetailBackedFlowerAccentRatio() // detail 위 포인트 꽃 과밀 방어
        {
            return Mathf.Clamp(DetailBackedFlowerAccentRatio, 0f, 0.34f);
        }

        private float GetEffectiveDetailBackedGrassAccentRatio() // detail 위 포인트 풀 과밀 방어
        {
            return Mathf.Clamp(DetailBackedGrassAccentRatio, 0f, 0.22f);
        }

        private int GetEffectiveRockClusterCount() // 기존 씬 30개 직렬화값 보정
        {
            if (RockClusterCount == 30)
            {
                return 42;
            }

            return Mathf.Max(0, RockClusterCount);
        }

        private float GetEffectiveDecorationClusterRadiusScale() // Terrain detail 제외 장식 오브젝트 군집 범위
        {
            float scale = DecorationClusterRadiusScale <= 0.001f ? 1.25f : DecorationClusterRadiusScale;
            return Mathf.Clamp(scale, 0.75f, 1.8f);
        }

        private float GetEffectiveClusterRadius(float radius) // 장식 오브젝트 반경 보정
        {
            return Mathf.Max(0f, radius * GetEffectiveDecorationClusterRadiusScale());
        }

        private int GetEffectiveClusterMemberCount(int count) // 장식 오브젝트 수량 보정
        {
            int baseline = Mathf.Max(0, count);
            if (baseline == 0)
            {
                return 0;
            }

            float scale = DecorationClusterMemberScale <= 0.001f ? 1.35f : DecorationClusterMemberScale;
            return Mathf.Max(baseline, Mathf.RoundToInt(baseline * Mathf.Clamp(scale, 1f, 1.8f)));
        }

        private float GetEffectivePandazoleScaleMultiplier() // Pandazole 전체 스케일 보정
        {
            float multiplier = PandazoleScaleMultiplier <= 0.001f ? 1f : PandazoleScaleMultiplier;
            return Mathf.Clamp(multiplier, 0.25f, 2f);
        }

        private float GetEffectiveDetailObjectDistance() // 탑다운 카메라 컬링 방어
        {
            return Mathf.Max(DetailObjectDistance, Mathf.Max(MapSize * 1.2f, PlayableSize * 1.55f));
        }

        private float EvaluateTerrainDetailDensity(TerrainDetailEntry entry, int prototypeIndex, Vector2 point, Vector2 center, IReadOnlyList<TrailSpline> trails) // detail 밀도 계산
        {
            if (!IsInsideMap(point, center, 1f))
            {
                return 0f;
            }

            float centerDistance = Vector2.Distance(point, center);
            float centerFade = Smooth01(Mathf.InverseLerp(NexusClearingRadius + 3.5f, NexusClearingRadius + 11.5f, centerDistance));
            if (centerFade <= 0.001f)
            {
                return 0f;
            }

            float mapHalf = Mathf.Max(5f, MapSize * 0.5f);
            float axisDistance = Mathf.Max(Mathf.Abs(point.x - center.x), Mathf.Abs(point.y - center.y));
            float mapEdgeFade = 1f - Smooth01(Mathf.InverseLerp(mapHalf - 8f, mapHalf - 1f, axisDistance));
            if (mapEdgeFade <= 0.001f)
            {
                return 0f;
            }

            Color blend = ArtDirectTerrainBlend(EvaluateTerrainBlend(point, center, trails), point);
            float nearestTrail = CalculateNearestTrailSignedDistance(point, trails);
            float noDirt = 1f - Smooth01(Mathf.InverseLerp(0.08f, 0.34f, blend.r));
            float grassWeight = Mathf.Clamp01(blend.b + blend.g * 0.5f - blend.r * 0.5f);
            float meadowMask = Smooth01(Mathf.InverseLerp(0.08f, 0.58f, grassWeight));
            float edgeMask = Smooth01(1f - Mathf.Clamp01(Mathf.Abs(nearestTrail - 3.8f) / 5.6f));
            edgeMask *= Smooth01(Mathf.InverseLerp(0.5f, 2.0f, nearestTrail));

            TerrainDetailKind kind = entry.Kind;
            float speciesCluster = SampleSurfaceNoise(point.x, point.y, kind == TerrainDetailKind.Grass ? 0.055f : 0.038f, 137.5f + prototypeIndex * 23.71f);
            float fine = SampleSurfaceNoise(point.x, point.y, kind == TerrainDetailKind.Grass ? 0.24f : 0.18f, 271.4f + prototypeIndex * 11.37f);

            if (entry.IsCarpetGrass)
            {
                float meadow = meadowMask * Mathf.Lerp(0.62f, 1.2f, speciesCluster);
                float pathFray = edgeMask * 0.42f;
                return Mathf.Clamp01((meadow + pathFray) * noDirt * centerFade * mapEdgeFade * Mathf.Lerp(0.68f, 1.18f, fine));
            }

            float clusterMask = CalculateDetailClusterMask(entry, prototypeIndex, point);
            if (clusterMask <= 0.001f)
            {
                return 0f;
            }

            if (kind == TerrainDetailKind.Grass)
            {
                float meadow = meadowMask * clusterMask * Mathf.Lerp(0.62f, 1.08f, fine);
                float pathFray = edgeMask * clusterMask * 0.18f;
                return Mathf.Clamp01((meadow + pathFray) * noDirt * centerFade * mapEdgeFade);
            }

            float flowerChance = GetEffectiveMeadowFlowerChance();
            float meadowCluster = clusterMask * Smooth01(Mathf.InverseLerp(1f - flowerChance, 1f, speciesCluster)) * meadowMask;
            float pathCluster = edgeMask * clusterMask * GetEffectivePathEdgeFlowerBoost() * 0.36f * Smooth01(Mathf.InverseLerp(0.58f, 0.98f, fine));
            float flowerDensity = Mathf.Max(meadowCluster * flowerChance * 0.38f, pathCluster);
            return Mathf.Clamp01(flowerDensity * noDirt * centerFade * mapEdgeFade);
        }

        private float CalculateDetailClusterMask(TerrainDetailEntry entry, int prototypeIndex, Vector2 point) // 같은 종류끼리 뭉치는 군집 마스크
        {
            float largeFrequency = entry.Kind == TerrainDetailKind.Flower ? 0.032f : 0.042f;
            float middleFrequency = entry.Kind == TerrainDetailKind.Flower ? 0.085f : 0.11f;
            float large = SampleSurfaceNoise(point.x, point.y, largeFrequency, 619.1f + prototypeIndex * 37.23f);
            float middle = SampleSurfaceNoise(point.x, point.y, middleFrequency, 911.4f + prototypeIndex * 19.87f);

            if (entry.Kind == TerrainDetailKind.Flower)
            {
                float patch = Smooth01(Mathf.InverseLerp(0.62f, 0.88f, large));
                float core = Smooth01(Mathf.InverseLerp(0.42f, 0.78f, middle));
                return patch * Mathf.Lerp(0.48f, 1f, core);
            }

            float grassPatch = Smooth01(Mathf.InverseLerp(0.5f, 0.8f, large));
            float grassCore = Smooth01(Mathf.InverseLerp(0.38f, 0.74f, middle));
            return grassPatch * Mathf.Lerp(0.55f, 1f, grassCore);
        }

        private float CalculateDecorationCompositionMask(Vector2 point, float broadFrequency, float middleFrequency, float salt) // 큰 장식 덩어리 선호도
        {
            float broad = SampleSurfaceNoise(point.x, point.y, broadFrequency, salt);
            float middle = SampleSurfaceNoise(point.x, point.y, middleFrequency, salt + 211.37f);
            float value = broad * 0.68f + middle * 0.32f;
            return Smooth01(Mathf.InverseLerp(0.32f, 0.82f, value));
        }

        private void SpawnPatternGroup(DecorationPatternContext context, DecorationCatalog catalog, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, int count, float minSpacing, DecorationZone zone, DecorationPatternBand band) // 패턴 묶음 반복
        {
            int targetCount = Mathf.Max(0, count);
            int spawnedCount = 0;
            int attempts = Mathf.Max(targetCount * 3, targetCount + 6);
            for (int i = 0; i < attempts && spawnedCount < targetCount; i++)
            {
                if (!TryFindDecorationCenter(random, center, trails, zone, minSpacing, context.OccupiedCenters, out Vector2 origin))
                {
                    continue;
                }

                DecorationPatternKind pattern = ChoosePatternKind(random, band);
                if (SpawnDecorationPattern(pattern, context, catalog, center, trails, random, origin))
                {
                    RegisterPatternCenter(context, pattern, origin);
                    spawnedCount++;
                }
            }
        }

        private void SpawnPandazoleAddonDecorations(Transform decorationRoot, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, DecorationPatternContext context) // Pandazole 별도 추가 레이어
        {
            if (!SpawnPandazoleAddons)
            {
                return;
            }

            PandazoleDecorationCatalog catalog = ResolvePandazoleDecorationCatalog();
            if (!catalog.HasAnyPrefab)
            {
                return;
            }

            Transform addonRoot = CreateChildRoot(decorationRoot, "PandazoleAddons");
            int flowerCount = SpawnPandazoleAddonGroup(addonRoot, catalog, center, trails, random, context, PandazoleFlowerPocketCount, PandazoleFlowerPocketMinSpacing, DecorationZone.FlowerMeadow, PandazoleAddonPatternBand.FlowerPocket);
            int forestFloorCount = SpawnPandazoleAddonGroup(addonRoot, catalog, center, trails, random, context, PandazoleForestFloorPocketCount, PandazoleForestFloorMinSpacing, DecorationZone.GrassPatch, PandazoleAddonPatternBand.ForestFloor);
            int curioCount = SpawnPandazoleAddonGroup(addonRoot, catalog, center, trails, random, context, PandazoleCurioPocketCount, PandazoleCurioMinSpacing, DecorationZone.Rock, PandazoleAddonPatternBand.Curio);

            if (addonRoot.childCount == 0)
            {
                DestroyGeneratedObject(addonRoot.gameObject);
                return;
            }

            if (LogGenerationTimings)
            {
                Debug.Log(
                    $"[MeadowTerrainRuntimeGenerator] PandazoleAddons spawned flower={flowerCount}, forestFloor={forestFloorCount}, curio={curioCount}, rootChildren={addonRoot.childCount}",
                    this);
            }
        }

        private int SpawnPandazoleAddonGroup(Transform addonRoot, PandazoleDecorationCatalog catalog, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, DecorationPatternContext context, int count, float minSpacing, DecorationZone zone, PandazoleAddonPatternBand band) // Pandazole 패턴 반복
        {
            int targetCount = Mathf.Max(0, count);
            int spawnedCount = 0;
            int attempts = Mathf.Max(targetCount * 4, targetCount + 8);
            for (int i = 0; i < attempts && spawnedCount < targetCount; i++)
            {
                if (!CanSpawnPandazoleBand(catalog, band))
                {
                    break;
                }

                IReadOnlyList<Vector2> usedCenters = context != null ? context.OccupiedCenters : null;
                if (!TryFindDecorationCenter(random, center, trails, zone, minSpacing, usedCenters, out Vector2 origin))
                {
                    continue;
                }

                if (SpawnPandazoleAddonPattern(addonRoot, catalog, center, trails, random, origin, band))
                {
                    context?.OccupiedCenters.Add(origin);
                    spawnedCount++;
                }
            }

            return spawnedCount;
        }

        private bool CanSpawnPandazoleBand(PandazoleDecorationCatalog catalog, PandazoleAddonPatternBand band) // 카테고리 후보 확인
        {
            if (catalog == null)
            {
                return false;
            }

            switch (band)
            {
                case PandazoleAddonPatternBand.FlowerPocket:
                    return catalog.Flowers.Length > 0;

                case PandazoleAddonPatternBand.ForestFloor:
                    return catalog.Mushrooms.Length > 0 || catalog.Logs.Length > 0 || catalog.Foliage.Length > 0 || catalog.Grass.Length > 0;

                case PandazoleAddonPatternBand.Curio:
                    return catalog.Bones.Length > 0 || catalog.Skulls.Length > 0;

                default:
                    return false;
            }
        }

        private bool SpawnPandazoleAddonPattern(Transform addonRoot, PandazoleDecorationCatalog catalog, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin, PandazoleAddonPatternBand band) // Pandazole 패턴 실행
        {
            Transform cluster = CreateChildRoot(addonRoot, $"{addonRoot.childCount:000}_{band}");
            switch (band)
            {
                case PandazoleAddonPatternBand.FlowerPocket:
                    SpawnPandazoleSameGroup(ChoosePrefab(random, catalog.Flowers, null), cluster, center, trails, random, origin, random.Next(5, 12), 0.35f, 3.9f, 0.58f, 1.02f, DecorationPlacementKind.Flower);
                    SpawnPandazoleScatter(catalog.Grass, catalog.Foliage, cluster, center, trails, random, origin, random.Next(3, 7), 1.0f, 4.6f, 0.46f, 0.9f, DecorationPlacementKind.Plant);
                    if (random.NextDouble() < 0.45)
                    {
                        SpawnPandazoleScatter(catalog.Foliage, catalog.Grass, cluster, center, trails, random, origin, random.Next(2, 5), 1.2f, 4.2f, 0.5f, 0.95f, DecorationPlacementKind.Plant);
                    }

                    return FinalizePatternCluster(cluster);

                case PandazoleAddonPatternBand.ForestFloor:
                    if (random.NextDouble() < 0.34)
                    {
                        SpawnPandazoleSingle(ChoosePrefab(random, catalog.Logs, catalog.Mushrooms), cluster, center, trails, random, origin, 0.72f, 1.08f, DecorationPlacementKind.Plant);
                    }

                    SpawnPandazoleScatter(catalog.Mushrooms, catalog.Foliage, cluster, center, trails, random, origin, random.Next(4, 10), 0.4f, 4.4f, 0.54f, 1.0f, DecorationPlacementKind.Plant);
                    SpawnPandazoleScatter(catalog.Foliage, catalog.Grass, cluster, center, trails, random, origin, random.Next(4, 9), 0.9f, 5.2f, 0.52f, 1.05f, DecorationPlacementKind.Plant);
                    if (random.NextDouble() < 0.26)
                    {
                        SpawnPandazoleScatter(catalog.Flowers, null, cluster, center, trails, random, origin, random.Next(2, 5), 1.4f, 4.8f, 0.54f, 0.9f, DecorationPlacementKind.Flower);
                    }

                    return FinalizePatternCluster(cluster);

                case PandazoleAddonPatternBand.Curio:
                    SpawnPandazoleSingle(ChoosePrefab(random, catalog.Bones, catalog.Skulls), cluster, center, trails, random, origin, 0.74f, 1.08f, DecorationPlacementKind.Rock);
                    if (random.NextDouble() < 0.38)
                    {
                        SpawnPandazoleSingle(ChoosePrefab(random, catalog.Skulls, catalog.Bones), cluster, center, trails, random, origin + RandomRingOffset(random, 1.5f, 3.8f), 0.62f, 0.96f, DecorationPlacementKind.Rock);
                    }

                    SpawnPandazoleScatter(catalog.Mushrooms, catalog.Foliage, cluster, center, trails, random, origin, random.Next(2, 6), 1.1f, 4.8f, 0.5f, 0.92f, DecorationPlacementKind.Plant);
                    SpawnPandazoleScatter(catalog.Grass, catalog.Foliage, cluster, center, trails, random, origin, random.Next(2, 5), 1.4f, 5.4f, 0.46f, 0.84f, DecorationPlacementKind.Plant);
                    return FinalizePatternCluster(cluster);

                default:
                    return false;
            }
        }

        private void SpawnPandazoleSameGroup(GameObject prefab, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin, int count, float minRadius, float maxRadius, float minScale, float maxScale, DecorationPlacementKind placementKind) // 같은 Pandazole 프리팹 묶음
        {
            if (prefab == null)
            {
                return;
            }

            int memberCount = GetEffectiveClusterMemberCount(count);
            float effectiveMaxRadius = GetEffectiveClusterRadius(maxRadius);
            for (int i = 0; i < memberCount; i++)
            {
                Vector2 point = origin + RandomRingOffset(random, i == 0 ? 0f : minRadius, effectiveMaxRadius);
                SpawnPandazoleSingle(prefab, parent, center, trails, random, point, minScale, maxScale, placementKind);
            }
        }

        private void SpawnPandazoleScatter(IReadOnlyList<GameObject> primary, IReadOnlyList<GameObject> fallback, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin, int count, float minRadius, float maxRadius, float minScale, float maxScale, DecorationPlacementKind placementKind) // Pandazole 혼합 산포
        {
            int memberCount = GetEffectiveClusterMemberCount(count);
            float effectiveMaxRadius = GetEffectiveClusterRadius(maxRadius);
            for (int i = 0; i < memberCount; i++)
            {
                GameObject prefab = ChoosePrefab(random, primary, fallback);
                if (prefab == null)
                {
                    continue;
                }

                Vector2 point = origin + RandomRingOffset(random, i == 0 ? 0f : minRadius, effectiveMaxRadius);
                SpawnPandazoleSingle(prefab, parent, center, trails, random, point, minScale, maxScale, placementKind);
            }
        }

        private void SpawnPandazoleSingle(GameObject prefab, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 point, float minScale, float maxScale, DecorationPlacementKind placementKind) // Pandazole 단일 배치
        {
            if (prefab == null || !IsDecorationPointAllowed(point, center, trails, placementKind))
            {
                return;
            }

            float scaleMultiplier = GetEffectivePandazoleScaleMultiplier();
            SpawnPrefab(prefab, parent, center, point, minScale * scaleMultiplier, maxScale * scaleMultiplier, random, false, true);
        }

        private bool SpawnDecorationPattern(DecorationPatternKind pattern, DecorationPatternContext context, DecorationCatalog catalog, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin) // 패턴 실행
        {
            Transform cluster = CreateChildRoot(context.PatternRoot, $"{context.NextPatternIndex++:000}_{pattern}");
            switch (pattern)
            {
                case DecorationPatternKind.ThinPathGrassFray:
                    SpawnGrassScatter(catalog, cluster, center, trails, random, origin, random.Next(3, 8), 0.4f, 3.7f, 0.68f, 1.18f, 0.08f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.FlowerSidePocket:
                    SpawnSameFlowerGroup(ChooseFlowerPrefab(random, catalog), cluster, center, trails, random, origin, random.Next(4, 10), 0.4f, 3.8f);
                    SpawnGrassScatter(catalog, cluster, center, trails, random, origin, random.Next(3, 7), 1.0f, 4.5f, 0.68f, 1.2f, 0.05f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.PathBendAccent:
                    SpawnSameFlowerGroup(ChooseFlowerPrefab(random, catalog), cluster, center, trails, random, origin, random.Next(5, 12), 0.6f, 5.6f);
                    SpawnGrassScatter(catalog, cluster, center, trails, random, origin, random.Next(4, 9), 1.2f, 6.4f, 0.7f, 1.25f, 0.08f);
                    SpawnSingleRock(catalog, cluster, center, trails, random, origin + RandomRingOffset(random, 2.4f, 5.4f), 0.72f, 1.05f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.TrampledGrassPatch:
                    SpawnGrassScatter(catalog, cluster, center, trails, random, origin, random.Next(4, 9), 0.4f, 4.2f, 0.52f, 0.92f, 0f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.SmallStonePathMarker:
                    SpawnSingleRock(catalog, cluster, center, trails, random, origin, 0.58f, 0.92f);
                    SpawnGrassScatter(catalog, cluster, center, trails, random, origin, random.Next(2, 5), 0.8f, 3.2f, 0.58f, 1.0f, 0.04f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.SingleColorFlowerDrift:
                    SpawnFlowerDrift(catalog, cluster, center, trails, random, origin, random.Next(8, 18), 7.5f, 2.2f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.PathEntranceBloom:
                    SpawnFlowerDrift(catalog, cluster, center, trails, random, origin, random.Next(10, 22), 9.5f, 2.8f);
                    SpawnGrassScatter(catalog, cluster, center, trails, random, origin, random.Next(6, 12), 1.2f, 6.2f, 0.72f, 1.28f, 0.06f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.OpenGrove:
                    SpawnTreeSet(catalog, cluster, center, trails, random, origin, random.Next(3, 6), 3.2f, 9.0f, 0.86f, 1.18f, 0.34f);
                    SpawnUnderbrush(catalog, cluster, center, trails, random, origin, random.Next(7, 14), 2.0f, 9.2f, 0.18f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.DenseGroveBack:
                    SpawnTreeSet(catalog, cluster, center, trails, random, origin, random.Next(5, 10), 2.6f, 8.2f, 0.92f, 1.28f, 0.24f);
                    SpawnUnderbrush(catalog, cluster, center, trails, random, origin, random.Next(12, 22), 1.6f, 9.5f, 0.08f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.TwinTreeFrame:
                    SpawnTwinTrees(catalog, cluster, center, trails, random, origin);
                    SpawnUnderbrush(catalog, cluster, center, trails, random, origin, random.Next(5, 11), 1.4f, 6.2f, 0.2f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.YoungTreeCluster:
                    SpawnTreeSet(catalog, cluster, center, trails, random, origin, random.Next(4, 8), 1.8f, 6.2f, 0.72f, 1.02f, 0.82f);
                    SpawnGrassScatter(catalog, cluster, center, trails, random, origin, random.Next(6, 12), 2.0f, 7.5f, 0.62f, 1.12f, 0.05f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.OldTreeWithUnderbrush:
                    SpawnPrefab(ChoosePrefab(random, catalog.Trees, catalog.LightTrees), cluster, center, origin, 1.08f, 1.36f, random, true, true);
                    SpawnUnderbrush(catalog, cluster, center, trails, random, origin, random.Next(9, 17), 1.6f, 7.8f, 0.18f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.TreeCrescent:
                    SpawnTreeCrescent(catalog, cluster, center, trails, random, origin);
                    SpawnUnderbrush(catalog, cluster, center, trails, random, origin, random.Next(7, 15), 1.4f, 8.4f, 0.12f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.MossyStoneMini:
                    SpawnSingleRock(catalog, cluster, center, trails, random, origin, 0.72f, 1.08f);
                    SpawnGrassScatter(catalog, cluster, center, trails, random, origin, random.Next(4, 8), 0.9f, 3.8f, 0.62f, 1.14f, 0.04f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.FlowerStonePatch:
                    SpawnSingleRock(catalog, cluster, center, trails, random, origin, 0.76f, 1.12f);
                    SpawnSameFlowerGroup(ChooseFlowerPrefab(random, catalog), cluster, center, trails, random, origin, random.Next(4, 8), 1.0f, 4.8f);
                    SpawnGrassScatter(catalog, cluster, center, trails, random, origin, random.Next(3, 7), 0.9f, 4.2f, 0.64f, 1.14f, 0.06f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.StoneClusterTriangle:
                    SpawnStoneTriangle(catalog, cluster, center, trails, random, origin);
                    SpawnGrassScatter(catalog, cluster, center, trails, random, origin, random.Next(5, 10), 0.8f, 4.6f, 0.62f, 1.12f, 0.04f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.RockAndBushAnchor:
                    SpawnSingleRock(catalog, cluster, center, trails, random, origin, 0.78f, 1.14f);
                    SpawnBushScatter(catalog, cluster, center, trails, random, origin, random.Next(2, 5), 1.1f, 4.6f, 0.74f, 1.15f);
                    SpawnGrassScatter(catalog, cluster, center, trails, random, origin, random.Next(4, 9), 1.0f, 5.2f, 0.62f, 1.1f, 0.05f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.LeafyRockPocket:
                    SpawnLeafyRockPocket(catalog, cluster, center, trails, random, origin);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.MushroomStonePocket:
                    SpawnMushroomStonePocket(catalog, cluster, center, trails, random, origin);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.LowGrassCarpet:
                    SpawnGrassScatter(catalog, cluster, center, trails, random, origin, random.Next(10, 21), 0.4f, 7.2f, 0.52f, 1.18f, 0.03f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.BushPocket:
                    SpawnBushScatter(catalog, cluster, center, trails, random, origin, random.Next(3, 7), 0.7f, 4.2f, 0.72f, 1.18f);
                    SpawnGrassScatter(catalog, cluster, center, trails, random, origin, random.Next(5, 11), 1.2f, 5.8f, 0.58f, 1.12f, 0.05f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.GrassWaveLine:
                    SpawnGrassWave(catalog, cluster, center, trails, random, origin, random.Next(9, 19), 10.0f, 1.8f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.TinyFlowerScatter:
                    SpawnSameFlowerGroup(ChooseFlowerPrefab(random, catalog), cluster, center, trails, random, origin, random.Next(3, 6), 0.6f, 3.0f);
                    SpawnGrassScatter(catalog, cluster, center, trails, random, origin, random.Next(2, 5), 1.1f, 3.8f, 0.58f, 1.0f, 0.03f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.WildflowerPlantTuple:
                    SpawnWildflowerPlantTuple(catalog, cluster, center, trails, random, origin);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.PlantMushroomLine:
                    SpawnPlantMushroomLine(catalog, cluster, center, trails, random, origin);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.NexusOuterGrassHalo:
                    SpawnGrassScatter(catalog, cluster, center, trails, random, origin, random.Next(5, 11), 0.6f, 4.0f, 0.52f, 0.92f, 0.02f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.NexusFlowerAccent:
                    SpawnSameFlowerGroup(ChooseFlowerPrefab(random, catalog), cluster, center, trails, random, origin, random.Next(3, 7), 0.5f, 3.4f);
                    SpawnGrassScatter(catalog, cluster, center, trails, random, origin, random.Next(2, 5), 0.8f, 3.8f, 0.52f, 0.96f, 0.02f);
                    return FinalizePatternCluster(cluster);

                case DecorationPatternKind.NexusStoneCalm:
                    SpawnSingleRock(catalog, cluster, center, trails, random, origin, 0.56f, 0.9f);
                    SpawnGrassScatter(catalog, cluster, center, trails, random, origin, random.Next(2, 5), 0.8f, 3.1f, 0.52f, 0.94f, 0.02f);
                    return FinalizePatternCluster(cluster);

                default:
                    return false;
            }
        }

        private bool FinalizePatternCluster(Transform cluster) // 빈 패턴 제거 + 접지 그림자
        {
            if (cluster == null)
            {
                return false;
            }

            if (cluster.childCount > 0)
            {
                CreateClusterContactShadow(cluster);
                return true;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(cluster.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(cluster.gameObject);
            }

            return false;
        }

        private void CreateClusterContactShadow(Transform cluster) // 작은 군집용 가짜 그림자
        {
            contactShadowAttempts++;
            if (!ShouldUseClusterContactShadows() || cluster == null)
            {
                if (cluster == null)
                {
                    contactShadowNullCluster++;
                }
                else
                {
                    contactShadowDisabled++;
                }

                LogClusterContactShadowSample("skipped-disabled-or-null", cluster, default, Vector3.zero, Vector2.zero, null);
                return;
            }

            ClusterContactShadowResult result = TryCalculateContactShadow(cluster, out Vector3 position, out Vector2 size, out ClusterContactShadowDebugInfo debugInfo);
            AccumulateClusterContactShadowDebug(debugInfo);
            if (result != ClusterContactShadowResult.Success)
            {
                if (result == ClusterContactShadowResult.NoBounds)
                {
                    contactShadowNoBounds++;
                }
                else if (result == ClusterContactShadowResult.AlphaZero)
                {
                    contactShadowAlphaZero++;
                }
                else if (result == ClusterContactShadowResult.NullCluster)
                {
                    contactShadowNullCluster++;
                }

                LogClusterContactShadowSample(result.ToString(), cluster, debugInfo, position, size, null);
                return;
            }

            Material material = ResolveContactShadowMaterial();
            if (material == null)
            {
                contactShadowNoMaterial++;
                LogClusterContactShadowSample("no-material", cluster, debugInfo, position, size, null);
                return;
            }

            GameObject shadow = new GameObject(GeneratedClusterContactShadowName);
            shadow.transform.SetParent(cluster, false);
            shadow.transform.position = position;
            shadow.transform.rotation = Quaternion.identity;
            shadow.transform.localScale = new Vector3(size.x, 1f, size.y);

            MeshFilter filter = shadow.AddComponent<MeshFilter>();
            filter.sharedMesh = GetContactShadowMesh();

            MeshRenderer renderer = shadow.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortingOrder = 20;
            contactShadowCreated++;
            LogClusterContactShadowSample("created", cluster, debugInfo, position, size, material);
        }

        private ClusterContactShadowResult TryCalculateContactShadow(Transform cluster, out Vector3 position, out Vector2 size, out ClusterContactShadowDebugInfo debugInfo) // 군집 bounds -> 타원 그림자
        {
            position = Vector3.zero;
            size = Vector2.zero;
            debugInfo = default;

            if (cluster == null)
            {
                return ClusterContactShadowResult.NullCluster;
            }

            Renderer[] renderers = cluster.GetComponentsInChildren<Renderer>(true);
            debugInfo.RendererCount = renderers.Length;
            Bounds bounds = default;
            bool hasBounds = false;
            float groundY = float.MaxValue;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Transform root = GetDirectClusterChild(renderer.transform, cluster);
                if (root == null)
                {
                    debugInfo.RootlessRendererCount++;
                    continue;
                }

                if (root.name.Equals(GeneratedClusterContactShadowName, StringComparison.Ordinal))
                {
                    debugInfo.SelfRendererCount++;
                    continue;
                }

                if (IsGrassShadowExcluded(root.gameObject))
                {
                    debugInfo.GrassRendererCount++;
                    continue;
                }

                if (IsTreeShadowPrefab(root.gameObject))
                {
                    debugInfo.TreeRendererCount++;
                    continue;
                }

                debugInfo.IncludedRendererCount++;
                if (string.IsNullOrEmpty(debugInfo.FirstIncludedRootName))
                {
                    debugInfo.FirstIncludedRootName = root.name;
                }

                groundY = Mathf.Min(groundY, root.position.y);
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            if (!hasBounds)
            {
                return ClusterContactShadowResult.NoBounds;
            }

            float padding = GetEffectiveClusterContactShadowPadding();
            float maxSize = GetEffectiveClusterContactShadowMaxSize();
            float width = Mathf.Clamp(bounds.size.x + padding, MinimumVisibleClusterContactShadowSize, maxSize);
            float depth = Mathf.Clamp(bounds.size.z + padding, MinimumVisibleClusterContactShadowSize, maxSize);
            float surfaceY = SampleGeneratedSurfaceY(bounds.center.x, bounds.center.z);
            float rootY = groundY < float.MaxValue ? groundY : bounds.min.y;
            float y = Mathf.Max(surfaceY, rootY);
            position = new Vector3(bounds.center.x, y + GetEffectiveClusterContactShadowYOffset(), bounds.center.z);
            size = new Vector2(width, depth);
            return GetEffectiveClusterContactShadowAlpha() > 0.001f ? ClusterContactShadowResult.Success : ClusterContactShadowResult.AlphaZero;
        }

        private static Transform GetDirectClusterChild(Transform item, Transform cluster) // 군집 직계 소품 찾기
        {
            if (item == null || cluster == null)
            {
                return null;
            }

            Transform current = item;
            while (current != null && current.parent != cluster)
            {
                current = current.parent;
            }

            return current;
        }

        private bool ShouldUseClusterContactShadows() // 기존 씬 0 직렬화 방어
        {
            return UseClusterContactShadows || IsClusterContactShadowConfigMissing();
        }

        private bool IsClusterContactShadowConfigMissing() // 새 필드가 0으로 들어온 구버전 씬 보정
        {
            return ClusterContactShadowAlpha <= 0.001f
                && ClusterContactShadowPadding <= 0.001f
                && ClusterContactShadowMaxSize <= 0.001f
                && ClusterContactShadowYOffset <= 0.001f;
        }

        private float GetEffectiveClusterContactShadowAlpha() // 접지 그림자 실효 농도
        {
            float alpha = IsClusterContactShadowConfigMissing() ? DefaultClusterContactShadowAlpha : ClusterContactShadowAlpha;
            return Mathf.Clamp(alpha, MinimumVisibleClusterContactShadowAlpha, 0.55f);
        }

        private float GetEffectiveClusterContactShadowPadding() // 접지 그림자 실효 여백
        {
            float padding = IsClusterContactShadowConfigMissing() ? DefaultClusterContactShadowPadding : ClusterContactShadowPadding;
            return Mathf.Clamp(padding, 0f, MaximumVisibleClusterContactShadowPadding);
        }

        private float GetEffectiveClusterContactShadowMaxSize() // 접지 그림자 실효 최대 크기
        {
            float maxSize = IsClusterContactShadowConfigMissing() ? DefaultClusterContactShadowMaxSize : ClusterContactShadowMaxSize;
            return Mathf.Clamp(maxSize, MinimumVisibleClusterContactShadowSize, MaximumVisibleClusterContactShadowSize);
        }

        private float GetEffectiveClusterContactShadowYOffset() // 접지 그림자 실효 높이
        {
            float yOffset = IsClusterContactShadowConfigMissing() ? DefaultClusterContactShadowYOffset : ClusterContactShadowYOffset;
            return Mathf.Max(MinimumVisibleClusterContactShadowYOffset, yOffset);
        }

        private float SampleGeneratedSurfaceY(float worldX, float worldZ) // 생성 지형 실제 표면 높이
        {
            float sampledY = SampleTerrainHeight(worldX, worldZ, contactShadowDiagnosticCenter);
            return contactShadowDiagnosticDemoTerrain ? sampledY + VisualYOffset : sampledY;
        }

        private void ResetClusterContactShadowDiagnostics() // 접지 그림자 진단 초기화
        {
            contactShadowAttempts = 0;
            contactShadowCreated = 0;
            contactShadowDisabled = 0;
            contactShadowNullCluster = 0;
            contactShadowNoBounds = 0;
            contactShadowAlphaZero = 0;
            contactShadowNoMaterial = 0;
            contactShadowRendererTotal = 0;
            contactShadowRendererIncluded = 0;
            contactShadowExcludedGrass = 0;
            contactShadowExcludedTree = 0;
            contactShadowExcludedSelf = 0;
            contactShadowSkippedRootless = 0;
            contactShadowSampleLogCount = 0;
        }

        private void AccumulateClusterContactShadowDebug(ClusterContactShadowDebugInfo debugInfo) // 렌더러 제외 통계 누적
        {
            contactShadowRendererTotal += debugInfo.RendererCount;
            contactShadowRendererIncluded += debugInfo.IncludedRendererCount;
            contactShadowExcludedGrass += debugInfo.GrassRendererCount;
            contactShadowExcludedTree += debugInfo.TreeRendererCount;
            contactShadowExcludedSelf += debugInfo.SelfRendererCount;
            contactShadowSkippedRootless += debugInfo.RootlessRendererCount;
        }

        private void LogClusterContactShadowSample(string result, Transform cluster, ClusterContactShadowDebugInfo debugInfo, Vector3 position, Vector2 size, Material material) // 접지 그림자 샘플 로그
        {
            if (!LogGenerationTimings || contactShadowSampleLogCount >= 8)
            {
                return;
            }

            contactShadowSampleLogCount++;
            float sampleTerrainY = position != Vector3.zero ? SampleTerrainHeight(position.x, position.z, contactShadowDiagnosticCenter) : 0f;
            float surfaceY = position != Vector3.zero ? SampleGeneratedSurfaceY(position.x, position.z) : 0f;
            string shaderName = material != null && material.shader != null ? material.shader.name : "null";
            string clusterName = cluster != null ? cluster.name : "null";
            int childCount = cluster != null ? cluster.childCount : 0;
            Debug.Log(
                $"[MeadowTerrainRuntimeGenerator] SHADOW sample#{contactShadowSampleLogCount} result={result}, cluster={clusterName}, children={childCount}, " +
                $"renderers={debugInfo.RendererCount}, included={debugInfo.IncludedRendererCount}, excludedGrass={debugInfo.GrassRendererCount}, excludedTree={debugInfo.TreeRendererCount}, " +
                $"firstIncluded={debugInfo.FirstIncludedRootName ?? "none"}, pos={FormatVector3(position)}, size={FormatVector2(size)}, " +
                $"shadowY={position.y:0.###}, sampleTerrainY={sampleTerrainY:0.###}, surfaceY={surfaceY:0.###}, delta={position.y - surfaceY:0.###}, demoTerrainPath={contactShadowDiagnosticDemoTerrain}, " +
                $"alpha={GetEffectiveClusterContactShadowAlpha():0.###}, shader={shaderName}",
                this);
        }

        private void LogClusterContactShadowDiagnostics(bool surfaceAlreadyHasDetails, bool terrainDetailsBuilt, int rockPatternCount, int flowerPatternCount, int grassPatternCount, DecorationPatternContext context) // 접지 그림자 요약 로그
        {
            if (!LogGenerationTimings)
            {
                return;
            }

            int patternCount = context != null ? context.NextPatternIndex : 0;
            string message =
                $"[MeadowTerrainRuntimeGenerator] SHADOW summary attempts={contactShadowAttempts}, created={contactShadowCreated}, disabled={contactShadowDisabled}, " +
                $"nullCluster={contactShadowNullCluster}, noBounds={contactShadowNoBounds}, alphaZero={contactShadowAlphaZero}, noMaterial={contactShadowNoMaterial}, " +
                $"renderers(total={contactShadowRendererTotal}, included={contactShadowRendererIncluded}, grassExcluded={contactShadowExcludedGrass}, treeExcluded={contactShadowExcludedTree}, selfExcluded={contactShadowExcludedSelf}, rootless={contactShadowSkippedRootless}), " +
                $"config(use={UseClusterContactShadows}, missing={IsClusterContactShadowConfigMissing()}, alpha={GetEffectiveClusterContactShadowAlpha():0.###}, padding={GetEffectiveClusterContactShadowPadding():0.###}, max={GetEffectiveClusterContactShadowMaxSize():0.###}, yOffset={GetEffectiveClusterContactShadowYOffset():0.###}), " +
                $"details(surfaceAlreadyHasDetails={surfaceAlreadyHasDetails}, terrainDetailsBuilt={terrainDetailsBuilt}), " +
                $"patternTargets(tree={TreeClusterCount}, hero={HeroTreeAccentCount}, rock={rockPatternCount}, nexus={NexusOuterAccentCount}, flower={flowerPatternCount}, grass={grassPatternCount}), patternAttempts={patternCount}";

            if (contactShadowCreated == 0 && contactShadowAttempts > 0)
            {
                Debug.LogWarning(message, this);
                return;
            }

            Debug.Log(message, this);
        }

        private static string FormatVector2(Vector2 value) // 로그용 Vector2
        {
            return $"({value.x:0.##}, {value.y:0.##})";
        }

        private static string FormatVector3(Vector3 value) // 로그용 Vector3
        {
            return $"({value.x:0.##}, {value.y:0.##}, {value.z:0.##})";
        }

        private void SpawnGrassScatter(DecorationCatalog catalog, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin, int count, float minRadius, float maxRadius, float minScale, float maxScale, float flowerChance) // 낮은 풀 산포
        {
            int memberCount = GetEffectiveClusterMemberCount(count);
            float effectiveMaxRadius = GetEffectiveClusterRadius(maxRadius);
            for (int i = 0; i < memberCount; i++)
            {
                Vector2 point = origin + RandomRingOffset(random, minRadius, effectiveMaxRadius);
                if (!IsDecorationPointAllowed(point, center, trails, DecorationPlacementKind.Plant))
                {
                    continue;
                }

                bool useFlower = random.NextDouble() < flowerChance;
                GameObject prefab = useFlower ? ChooseFlowerPrefab(random, catalog) : ChoosePrefab(random, catalog.Grass, catalog.Bushes);
                bool castShadow = random.NextDouble() < SmallDecorationShadowChance;
                SpawnPrefab(prefab, parent, center, point, minScale, maxScale, random, castShadow, true);
            }
        }

        private void SpawnBushScatter(DecorationCatalog catalog, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin, int count, float minRadius, float maxRadius, float minScale, float maxScale) // 덤불 산포
        {
            int memberCount = GetEffectiveClusterMemberCount(count);
            float effectiveMaxRadius = GetEffectiveClusterRadius(maxRadius);
            for (int i = 0; i < memberCount; i++)
            {
                Vector2 point = origin + RandomRingOffset(random, minRadius, effectiveMaxRadius);
                if (IsDecorationPointAllowed(point, center, trails, DecorationPlacementKind.Plant))
                {
                    SpawnPrefab(ChoosePrefab(random, catalog.Bushes, catalog.Grass), parent, center, point, minScale, maxScale, random, false, true);
                }
            }
        }

        private void SpawnSingleRock(DecorationCatalog catalog, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 point, float minScale, float maxScale) // 작은 돌 1개
        {
            if (IsDecorationPointAllowed(point, center, trails, DecorationPlacementKind.Rock))
            {
                SpawnPrefab(ChoosePrefab(random, catalog.Rocks, null), parent, center, point, minScale, maxScale, random, true, true);
            }
        }

        private void SpawnTreeSet(DecorationCatalog catalog, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin, int count, float minRadius, float maxRadius, float minScale, float maxScale, float lightTreeChance) // 나무 덩어리
        {
            Vector2 tangent = MaybeFlip(random, GetRadialTangent(center, origin)); // 중앙 기준 흐름
            Vector2 radial = NormalizeOrFallback(origin - center, RandomUnitVector(random)); // 바깥 방향
            int memberCount = GetEffectiveClusterMemberCount(count);
            float effectiveMaxRadius = GetEffectiveClusterRadius(maxRadius);
            List<Vector2> localPoints = new List<Vector2>(Mathf.Max(1, memberCount));

            for (int i = 0; i < memberCount; i++)
            {
                Vector2 point = origin;
                if (i > 0)
                {
                    bool found = false;
                    float localSpacing = Mathf.Max(1.2f, minRadius * 0.48f);
                    for (int attempt = 0; attempt < 10; attempt++)
                    {
                        float along = RandomRange(random, -effectiveMaxRadius, effectiveMaxRadius);
                        float outward = RandomRange(random, -effectiveMaxRadius * 0.32f, effectiveMaxRadius * 0.58f);
                        Vector2 candidate = origin + tangent * along + radial * outward;
                        float distance = Vector2.Distance(origin, candidate);
                        if (distance < minRadius || distance > effectiveMaxRadius || !HasMinimumDistance(candidate, localPoints, localSpacing))
                        {
                            continue;
                        }

                        point = candidate;
                        found = true;
                        break;
                    }

                    if (!found)
                    {
                        point = origin + RandomRingOffset(random, minRadius, effectiveMaxRadius);
                    }
                }

                if (!IsDecorationPointAllowed(point, center, trails, DecorationPlacementKind.Tree))
                {
                    continue;
                }

                bool useLightTree = random.NextDouble() < lightTreeChance;
                GameObject prefab = useLightTree ? ChoosePrefab(random, catalog.LightTrees, catalog.Trees) : ChoosePrefab(random, catalog.Trees, catalog.LightTrees);
                SpawnPrefab(prefab, parent, center, point, minScale, maxScale, random, true, true);
                localPoints.Add(point);
            }
        }

        private void SpawnUnderbrush(DecorationCatalog catalog, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin, int count, float minRadius, float maxRadius, float flowerChance) // 나무 하부 식생
        {
            int memberCount = GetEffectiveClusterMemberCount(count);
            float effectiveMaxRadius = GetEffectiveClusterRadius(maxRadius);
            for (int i = 0; i < memberCount; i++)
            {
                Vector2 point = origin + RandomRingOffset(random, minRadius, effectiveMaxRadius);
                if (!IsDecorationPointAllowed(point, center, trails, DecorationPlacementKind.Plant))
                {
                    continue;
                }

                double roll = random.NextDouble();
                GameObject prefab = roll < flowerChance
                    ? ChooseFlowerPrefab(random, catalog)
                    : roll < flowerChance + 0.36 ? ChoosePrefab(random, catalog.Bushes, catalog.Grass) : ChoosePrefab(random, catalog.Grass, catalog.Bushes);
                bool castShadow = roll >= flowerChance && random.NextDouble() < SmallDecorationShadowChance;
                SpawnPrefab(prefab, parent, center, point, 0.64f, 1.22f, random, castShadow, true);
            }
        }

        private void SpawnTwinTrees(DecorationCatalog catalog, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin) // 쌍나무 프레임
        {
            Vector2 direction = MaybeFlip(random, GetRadialTangent(center, origin));
            float spacing = GetEffectiveClusterRadius(RandomRange(random, 4.8f, 7.8f));
            Vector2 a = origin + direction * spacing * 0.5f;
            Vector2 b = origin - direction * spacing * 0.5f;
            if (IsDecorationPointAllowed(a, center, trails, DecorationPlacementKind.Tree))
            {
                SpawnPrefab(ChoosePrefab(random, catalog.Trees, catalog.LightTrees), parent, center, a, 0.9f, 1.2f, random, true, true);
            }

            if (IsDecorationPointAllowed(b, center, trails, DecorationPlacementKind.Tree))
            {
                SpawnPrefab(ChoosePrefab(random, catalog.LightTrees, catalog.Trees), parent, center, b, 0.78f, 1.12f, random, true, true);
            }
        }

        private void SpawnTreeCrescent(DecorationCatalog catalog, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin) // 반달 숲
        {
            Vector2 tangent = MaybeFlip(random, GetRadialTangent(center, origin));
            Vector2 radial = NormalizeOrFallback(origin - center, RandomUnitVector(random));
            int count = GetEffectiveClusterMemberCount(random.Next(4, 8));
            float radiusScale = GetEffectiveDecorationClusterRadiusScale();
            float halfLength = 8.0f * radiusScale;
            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0.5f : i / (float)(count - 1);
                float along = Mathf.Lerp(-halfLength, halfLength, t) + RandomRange(random, -0.9f, 0.9f) * radiusScale;
                float outward = Mathf.Sin(t * Mathf.PI) * RandomRange(random, 1.4f, 3.4f) * radiusScale + RandomRange(random, -0.8f, 0.8f) * radiusScale;
                Vector2 point = origin + tangent * along + radial * outward;
                if (!IsDecorationPointAllowed(point, center, trails, DecorationPlacementKind.Tree))
                {
                    continue;
                }

                bool light = random.NextDouble() < 0.32;
                SpawnPrefab(light ? ChoosePrefab(random, catalog.LightTrees, catalog.Trees) : ChoosePrefab(random, catalog.Trees, catalog.LightTrees), parent, center, point, 0.82f, 1.2f, random, true, true);
            }
        }

        private void SpawnStoneTriangle(DecorationCatalog catalog, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin) // 삼각 돌 군집
        {
            Vector2 direction = GetDecorationFlowDirection(origin, center, trails, random, true);
            Vector2 side = Perpendicular(direction);
            SpawnSingleRock(catalog, parent, center, trails, random, origin, 0.68f, 1.02f);
            int count = GetEffectiveClusterMemberCount(random.Next(2, 4));
            float radiusScale = GetEffectiveDecorationClusterRadiusScale();
            for (int i = 0; i < count; i++)
            {
                float along = RandomRange(random, -2.5f, 2.5f) * radiusScale;
                float lateral = RandomRange(random, 0.8f, 2.4f) * radiusScale * (i % 2 == 0 ? 1f : -1f);
                Vector2 point = origin + direction * along + side * lateral;
                SpawnSingleRock(catalog, parent, center, trails, random, point, 0.54f, 0.92f);
            }
        }

        private void SpawnLeafyRockPocket(DecorationCatalog catalog, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin) // 돌+잎식물 포켓
        {
            SpawnSingleRock(catalog, parent, center, trails, random, origin, 0.74f, 1.16f);
            SpawnSingleRock(catalog, parent, center, trails, random, origin + RandomRingOffset(random, 1.4f, 3.2f), 0.52f, 0.88f);
            SpawnPlantAccentScatter(catalog, parent, center, trails, random, origin, random.Next(5, 10), 0.9f, 4.6f, 0.62f, 1.16f);
            SpawnGrassScatter(catalog, parent, center, trails, random, origin, random.Next(4, 8), 1.4f, 5.2f, 0.5f, 0.92f, 0.02f);
        }

        private void SpawnMushroomStonePocket(DecorationCatalog catalog, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin) // 돌+버섯 포켓
        {
            SpawnSingleRock(catalog, parent, center, trails, random, origin, 0.62f, 1.02f);
            SpawnMushroomScatter(catalog, parent, center, trails, random, origin, random.Next(3, 7), 1.0f, 4.2f, 0.74f, 1.18f);
            SpawnPlantAccentScatter(catalog, parent, center, trails, random, origin, random.Next(3, 6), 1.6f, 4.8f, 0.58f, 1.0f);
            if (random.NextDouble() < 0.45)
            {
                SpawnSameFlowerGroup(ChooseFlowerPrefab(random, catalog), parent, center, trails, random, origin, random.Next(3, 6), 1.4f, 4.4f);
            }
        }

        private void SpawnWildflowerPlantTuple(DecorationCatalog catalog, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin) // 꽃+잎식물 소군집
        {
            SpawnSameFlowerGroup(ChooseFlowerPrefab(random, catalog), parent, center, trails, random, origin, random.Next(4, 8), 0.35f, 3.2f);
            SpawnPlantAccentScatter(catalog, parent, center, trails, random, origin, random.Next(3, 7), 0.9f, 4.0f, 0.62f, 1.1f);
            SpawnGrassScatter(catalog, parent, center, trails, random, origin, random.Next(2, 5), 1.2f, 4.6f, 0.5f, 0.86f, 0.02f);
        }

        private void SpawnPlantMushroomLine(DecorationCatalog catalog, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin) // 잎식물+버섯 흐름선
        {
            Vector2 direction = GetDecorationFlowDirection(origin, center, trails, random, true);
            Vector2 side = Perpendicular(direction);
            int count = GetEffectiveClusterMemberCount(random.Next(5, 10));
            float length = GetEffectiveClusterRadius(RandomRange(random, 5.2f, 8.8f));
            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0.5f : i / (float)(count - 1);
                float along = Mathf.Lerp(-length * 0.5f, length * 0.5f, t) + RandomRange(random, -0.55f, 0.55f);
                float lateral = RandomRange(random, -1.15f, 1.15f);
                Vector2 point = origin + direction * along + side * lateral;
                if (!IsDecorationPointAllowed(point, center, trails, DecorationPlacementKind.Plant))
                {
                    continue;
                }

                bool mushroom = random.NextDouble() < 0.28;
                GameObject prefab = mushroom ? ChoosePrefab(random, catalog.Mushrooms, catalog.Grass) : ChoosePrefab(random, catalog.Grass, catalog.Bushes);
                SpawnPrefab(prefab, parent, center, point, mushroom ? 0.68f : 0.54f, mushroom ? 1.04f : 1.06f, random, random.NextDouble() < SmallDecorationShadowChance, true);
            }
        }

        private void SpawnMushroomScatter(DecorationCatalog catalog, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin, int count, float minRadius, float maxRadius, float minScale, float maxScale) // 버섯 산포
        {
            int memberCount = GetEffectiveClusterMemberCount(count);
            float effectiveMaxRadius = GetEffectiveClusterRadius(maxRadius);
            for (int i = 0; i < memberCount; i++)
            {
                Vector2 point = origin + RandomRingOffset(random, i == 0 ? 0f : minRadius, effectiveMaxRadius);
                if (IsDecorationPointAllowed(point, center, trails, DecorationPlacementKind.Plant))
                {
                    SpawnPrefab(ChoosePrefab(random, catalog.Mushrooms, catalog.Grass), parent, center, point, minScale, maxScale, random, false, true);
                }
            }
        }

        private void SpawnPlantAccentScatter(DecorationCatalog catalog, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin, int count, float minRadius, float maxRadius, float minScale, float maxScale) // 잎식물 악센트 산포
        {
            int memberCount = GetEffectiveClusterMemberCount(count);
            float effectiveMaxRadius = GetEffectiveClusterRadius(maxRadius);
            for (int i = 0; i < memberCount; i++)
            {
                Vector2 point = origin + RandomRingOffset(random, i == 0 ? 0f : minRadius, effectiveMaxRadius);
                if (IsDecorationPointAllowed(point, center, trails, DecorationPlacementKind.Plant))
                {
                    bool castShadow = random.NextDouble() < SmallDecorationShadowChance;
                    SpawnPrefab(ChoosePrefab(random, catalog.Grass, catalog.Bushes), parent, center, point, minScale, maxScale, random, castShadow, true);
                }
            }
        }

        private void SpawnFlowerDrift(DecorationCatalog catalog, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin, int count, float length, float sideJitter) // 같은 꽃 흐름
        {
            GameObject flower = ChooseFlowerPrefab(random, catalog);
            Vector2 direction = GetDecorationFlowDirection(origin, center, trails, random, true);
            Vector2 side = Perpendicular(direction);
            int memberCount = GetEffectiveClusterMemberCount(count);
            float effectiveLength = GetEffectiveClusterRadius(length);
            float effectiveSideJitter = GetEffectiveClusterRadius(sideJitter);
            for (int i = 0; i < memberCount; i++)
            {
                float along = RandomRange(random, -effectiveLength * 0.5f, effectiveLength * 0.5f);
                float lateral = RandomRange(random, -effectiveSideJitter, effectiveSideJitter);
                Vector2 point = origin + direction * along + side * lateral;
                if (IsDecorationPointAllowed(point, center, trails, DecorationPlacementKind.Flower))
                {
                    bool castShadow = random.NextDouble() < SmallDecorationShadowChance;
                    SpawnPrefab(flower, parent, center, point, 0.72f, 1.22f, random, castShadow, true);
                }
            }
        }

        private void SpawnGrassWave(DecorationCatalog catalog, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin, int count, float length, float sideJitter) // 풀 흐름선
        {
            Vector2 direction = GetDecorationFlowDirection(origin, center, trails, random, true);
            Vector2 side = Perpendicular(direction);
            int memberCount = GetEffectiveClusterMemberCount(count);
            float effectiveLength = GetEffectiveClusterRadius(length);
            float effectiveSideJitter = GetEffectiveClusterRadius(sideJitter);
            for (int i = 0; i < memberCount; i++)
            {
                float t = memberCount <= 1 ? 0.5f : i / (float)(memberCount - 1);
                float along = Mathf.Lerp(-effectiveLength * 0.5f, effectiveLength * 0.5f, t) + RandomRange(random, -0.8f, 0.8f);
                float lateral = RandomRange(random, -effectiveSideJitter, effectiveSideJitter);
                Vector2 point = origin + direction * along + side * lateral;
                if (IsDecorationPointAllowed(point, center, trails, DecorationPlacementKind.Plant))
                {
                    bool castShadow = random.NextDouble() < SmallDecorationShadowChance;
                    SpawnPrefab(ChoosePrefab(random, catalog.Grass, catalog.Bushes), parent, center, point, 0.54f, 1.14f, random, castShadow, true);
                }
            }
        }

        private static GameObject ChooseFlowerPrefab(System.Random random, DecorationCatalog catalog) // 꽃 프리팹 선택
        {
            return random.NextDouble() < 0.58
                ? ChoosePrefab(random, catalog.FlowerGroups, catalog.FlowerAccents)
                : ChoosePrefab(random, catalog.FlowerAccents, catalog.FlowerGroups);
        }

        private static Vector2 RandomUnitVector(System.Random random) // 방향 랜덤
        {
            float angle = RandomRange(random, 0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private Vector2 GetDecorationFlowDirection(Vector2 origin, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, bool preferTrail) // 장식 흐름 방향
        {
            if (preferTrail && TryGetNearestTrailFrame(origin, trails, out Vector2 trailDirection, out _, out float distance) && distance <= GetEffectiveMainBlendOuterWidth() + 7.5f)
            {
                return MaybeFlip(random, trailDirection); // 길가 패턴은 길 방향
            }

            return MaybeFlip(random, GetRadialTangent(center, origin)); // 풀밭/숲은 중앙 기준 접선
        }

        private static Vector2 GetRadialTangent(Vector2 center, Vector2 origin) // 중앙 기준 접선
        {
            Vector2 radial = NormalizeOrFallback(origin - center, Vector2.up);
            return Perpendicular(radial);
        }

        private static Vector2 Perpendicular(Vector2 direction) // 2D 수직
        {
            direction = NormalizeOrFallback(direction, Vector2.right);
            return new Vector2(-direction.y, direction.x);
        }

        private static Vector2 MaybeFlip(System.Random random, Vector2 direction) // 방향 반전 랜덤
        {
            return random.NextDouble() < 0.5 ? direction : -direction;
        }

        private static Vector2 NormalizeOrFallback(Vector2 value, Vector2 fallback) // 0벡터 방어
        {
            if (value.sqrMagnitude > 0.0001f)
            {
                return value.normalized;
            }

            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector2.right;
        }

        private static DecorationPatternKind ChoosePatternKind(System.Random random, DecorationPatternBand band) // 밴드별 패턴 가중치
        {
            double roll = random.NextDouble();
            switch (band)
            {
                case DecorationPatternBand.PathEdge:
                    if (roll < 0.22) { return DecorationPatternKind.FlowerSidePocket; }
                    if (roll < 0.38) { return DecorationPatternKind.SingleColorFlowerDrift; }
                    if (roll < 0.54) { return DecorationPatternKind.ThinPathGrassFray; }
                    if (roll < 0.68) { return DecorationPatternKind.SmallStonePathMarker; }
                    if (roll < 0.84) { return DecorationPatternKind.PathBendAccent; }
                    if (roll < 0.94) { return DecorationPatternKind.TrampledGrassPatch; }
                    return DecorationPatternKind.PathEntranceBloom;

                case DecorationPatternBand.TreeGrove:
                    if (roll < 0.24) { return DecorationPatternKind.OpenGrove; }
                    if (roll < 0.44) { return DecorationPatternKind.YoungTreeCluster; }
                    if (roll < 0.62) { return DecorationPatternKind.TwinTreeFrame; }
                    if (roll < 0.78) { return DecorationPatternKind.TreeCrescent; }
                    if (roll < 0.9) { return DecorationPatternKind.OldTreeWithUnderbrush; }
                    return DecorationPatternKind.DenseGroveBack;

                case DecorationPatternBand.RockGarden:
                    if (roll < 0.2) { return DecorationPatternKind.MossyStoneMini; }
                    if (roll < 0.42) { return DecorationPatternKind.FlowerStonePatch; }
                    if (roll < 0.6) { return DecorationPatternKind.RockAndBushAnchor; }
                    if (roll < 0.76) { return DecorationPatternKind.StoneClusterTriangle; }
                    if (roll < 0.9) { return DecorationPatternKind.LeafyRockPocket; }
                    return DecorationPatternKind.MushroomStonePocket;

                case DecorationPatternBand.GrassPocket:
                    if (roll < 0.28) { return DecorationPatternKind.LowGrassCarpet; }
                    if (roll < 0.48) { return DecorationPatternKind.GrassWaveLine; }
                    if (roll < 0.66) { return DecorationPatternKind.BushPocket; }
                    if (roll < 0.8) { return DecorationPatternKind.TinyFlowerScatter; }
                    if (roll < 0.9) { return DecorationPatternKind.WildflowerPlantTuple; }
                    return DecorationPatternKind.PlantMushroomLine;

                case DecorationPatternBand.Hero:
                    return roll < 0.72 ? DecorationPatternKind.OldTreeWithUnderbrush : DecorationPatternKind.TwinTreeFrame;

                case DecorationPatternBand.NexusOuter:
                    if (roll < 0.52) { return DecorationPatternKind.NexusOuterGrassHalo; }
                    if (roll < 0.82) { return DecorationPatternKind.NexusFlowerAccent; }
                    return DecorationPatternKind.NexusStoneCalm;

                default:
                    return DecorationPatternKind.LowGrassCarpet;
            }
        }

        private static void RegisterPatternCenter(DecorationPatternContext context, DecorationPatternKind pattern, Vector2 origin) // 패턴 중심 기록
        {
            context.OccupiedCenters.Add(origin);
            switch (pattern)
            {
                case DecorationPatternKind.OpenGrove:
                case DecorationPatternKind.DenseGroveBack:
                case DecorationPatternKind.TwinTreeFrame:
                case DecorationPatternKind.YoungTreeCluster:
                case DecorationPatternKind.OldTreeWithUnderbrush:
                case DecorationPatternKind.TreeCrescent:
                    context.TreeCenters.Add(origin);
                    break;

                case DecorationPatternKind.MossyStoneMini:
                case DecorationPatternKind.FlowerStonePatch:
                case DecorationPatternKind.StoneClusterTriangle:
                case DecorationPatternKind.RockAndBushAnchor:
                case DecorationPatternKind.LeafyRockPocket:
                case DecorationPatternKind.MushroomStonePocket:
                case DecorationPatternKind.SmallStonePathMarker:
                case DecorationPatternKind.NexusStoneCalm:
                    context.RockCenters.Add(origin);
                    break;

                case DecorationPatternKind.FlowerSidePocket:
                case DecorationPatternKind.SingleColorFlowerDrift:
                case DecorationPatternKind.PathEntranceBloom:
                case DecorationPatternKind.NexusFlowerAccent:
                case DecorationPatternKind.PathBendAccent:
                case DecorationPatternKind.WildflowerPlantTuple:
                    context.FlowerCenters.Add(origin);
                    break;

                default:
                    context.GrassCenters.Add(origin);
                    break;
            }
        }

        private void SpawnClusterFlowers(DecorationCatalog catalog, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin, int count, float minRadius, float maxRadius) // 같은 자리 꽃 보강
        {
            GameObject primary = ChoosePrefab(random, catalog.FlowerAccents, catalog.FlowerGroups);
            SpawnSameFlowerGroup(primary, parent, center, trails, random, origin, Mathf.Max(1, count), minRadius, maxRadius);
        }

        private void SpawnSameFlowerGroup(GameObject prefab, Transform parent, Vector2 center, IReadOnlyList<TrailSpline> trails, System.Random random, Vector2 origin, int count, float minRadius, float maxRadius) // 같은 꽃 최소 묶음
        {
            if (prefab == null)
            {
                return;
            }

            int memberCount = GetEffectiveClusterMemberCount(count);
            float effectiveMaxRadius = GetEffectiveClusterRadius(maxRadius);
            for (int i = 0; i < memberCount; i++)
            {
                Vector2 point = origin + RandomRingOffset(random, i == 0 ? 0f : minRadius, effectiveMaxRadius);
                if (IsDecorationPointAllowed(point, center, trails, DecorationPlacementKind.Flower))
                {
                    bool castShadow = random.NextDouble() < SmallDecorationShadowChance;
                    SpawnPrefab(prefab, parent, center, point, 0.74f, 1.24f, random, castShadow, true);
                }
            }
        }

        private Color EvaluateTerrainBlend(Vector2 point, Vector2 center, IReadOnlyList<TrailSpline> trails) // 01/02/03 가중치
        {
            float bestSigned = CalculateClearingSignedDistance(point, center);
            float bestMid = NexusClearingBlendWidth;
            float bestGrass = NexusClearingBlendWidth;

            for (int i = 0; i < trails.Count; i++)
            {
                TrailSpline trail = trails[i];
                float signed = CalculateTrailSignedDistance(point, trail);
                if (signed < bestSigned)
                {
                    bestSigned = signed;
                    bestMid = trail.MidBlendWidth;
                    bestGrass = trail.GrassBlendWidth;
                }
            }

            return SignedDistanceToBlend(bestSigned, bestMid, bestGrass);
        }

        private float CalculateClearingSignedDistance(Vector2 point, Vector2 center) // 중앙 원형 흙길
        {
            float edge = SampleEdgeNoise(point.x, point.y) * EdgeNoiseAmplitude * 0.45f;
            return Vector2.Distance(point, center) - (NexusClearingRadius + edge);
        }

        private float CalculateTrailSignedDistance(Vector2 point, TrailSpline trail) // 길까지 거리
        {
            float distance = DistanceToPolyline(point, trail.Points);
            float edge = SampleEdgeNoise(point.x, point.y) * EdgeNoiseAmplitude;
            return distance - (trail.DirtHalfWidth + edge);
        }

        private static Color SignedDistanceToBlend(float signedDistance, float midBlendWidth, float grassBlendWidth) // 거리 -> RGB
        {
            float mid = Mathf.Max(0.001f, midBlendWidth);
            float grass = Mathf.Max(0.001f, grassBlendWidth);

            if (signedDistance <= 0f)
            {
                return new Color(1f, 0f, 0f, 1f); // 01 흙길
            }

            if (signedDistance <= mid)
            {
                float t = Mathf.Clamp01(signedDistance / mid);
                return new Color(1f - t, t, 0f, 1f); // 01 -> 02
            }

            float grassT = Mathf.Clamp01((signedDistance - mid) / grass);
            return new Color(0f, 1f - grassT, grassT, 1f); // 02 -> 03
        }

        private bool TryFindDecorationCenter(System.Random random, Vector2 center, IReadOnlyList<TrailSpline> trails, DecorationZone zone, float minSpacing, IReadOnlyList<Vector2> usedCenters, out Vector2 point) // 군집 후보 찾기
        {
            int attempts = zone == DecorationZone.FlowerPathEdge || zone == DecorationZone.NexusOuter ? 780 : 520;
            for (int i = 0; i < attempts; i++)
            {
                point = RandomDecorationPoint(random, center, trails, zone);
                if (!IsDecorationCenterAllowed(point, center, trails, zone))
                {
                    continue;
                }

                if (!HasMinimumDistance(point, usedCenters, minSpacing))
                {
                    continue;
                }

                return true;
            }

            point = center;
            return false;
        }

        private Vector2 RandomDecorationPoint(System.Random random, Vector2 center, IReadOnlyList<TrailSpline> trails, DecorationZone zone) // 카테고리별 후보 샘플
        {
            if (zone == DecorationZone.NexusOuter)
            {
                return center + RandomUnitVector(random) * RandomRange(random, NexusClearingRadius + 6.0f, NexusClearingRadius + 17.0f);
            }

            if (zone == DecorationZone.FlowerPathEdge)
            {
                return RandomPointNearTrail(random, trails, 2.8f, 8.8f);
            }

            if (zone == DecorationZone.Rock && random.NextDouble() < 0.55)
            {
                return RandomPointNearTrail(random, trails, 5.0f, 14.0f);
            }

            float mapHalf = Mathf.Max(5f, MapSize * 0.5f - 4f);
            if (zone == DecorationZone.Tree && random.NextDouble() < 0.72)
            {
                float angle = RandomRange(random, 0f, Mathf.PI * 2f);
                float radius = RandomRange(random, Mathf.Max(28f, PlayableSize * 0.28f), mapHalf);
                return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            if (zone == DecorationZone.HeroTree)
            {
                float angle = RandomRange(random, 0f, Mathf.PI * 2f);
                float radius = RandomRange(random, Mathf.Max(38f, PlayableSize * 0.34f), mapHalf * 0.92f);
                return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            return new Vector2(
                center.x + RandomRange(random, -mapHalf, mapHalf),
                center.y + RandomRange(random, -mapHalf, mapHalf));
        }

        private bool IsDecorationCenterAllowed(Vector2 point, Vector2 center, IReadOnlyList<TrailSpline> trails, DecorationZone zone) // 군집 중심 허용
        {
            if (!IsInsideMap(point, center, 3f))
            {
                return false;
            }

            float centerDistance = Vector2.Distance(point, center);
            Color blend = EvaluateTerrainBlend(point, center, trails);
            float nearestTrail = CalculateNearestTrailSignedDistance(point, trails);
            float outerWeight = CalculateOuterWeight(point.x, point.y, center);
            float groveMask = CalculateDecorationCompositionMask(point, 0.017f, 0.045f, 701.9f);
            float stoneMask = CalculateDecorationCompositionMask(point, 0.026f, 0.071f, 431.6f);
            float lowPlantMask = CalculateDecorationCompositionMask(point, 0.033f, 0.092f, 127.3f);

            switch (zone)
            {
                case DecorationZone.Tree:
                    if (centerDistance < NexusClearingRadius + 18f) { return false; }
                    if (nearestTrail < 5.2f) { return false; }
                    if (blend.r > 0.08f || blend.b < 0.48f) { return false; }
                    if (groveMask < 0.18f && centerDistance < PlayableSize * 0.48f) { return false; }
                    return outerWeight > 0.08f || centerDistance > 34f;

                case DecorationZone.Rock:
                    if (centerDistance < NexusClearingRadius + 12f) { return false; }
                    if (nearestTrail < 3.6f) { return false; }
                    if (stoneMask < 0.12f && nearestTrail > 9.5f) { return false; }
                    return blend.r < 0.14f && blend.b + blend.g * 0.38f > 0.52f;

                case DecorationZone.FlowerPathEdge:
                    if (centerDistance < NexusClearingRadius + 6.5f) { return false; }
                    return blend.r < 0.22f && nearestTrail >= 0.6f && nearestTrail <= GetEffectiveMainBlendOuterWidth() + 4.5f;

                case DecorationZone.FlowerMeadow:
                    if (centerDistance < NexusClearingRadius + 9f) { return false; }
                    if (lowPlantMask < 0.18f) { return false; }
                    return blend.r < 0.1f && blend.b + blend.g * 0.25f > 0.58f;

                case DecorationZone.GrassPatch:
                    if (centerDistance < NexusClearingRadius + 7f) { return false; }
                    if (lowPlantMask < 0.12f) { return false; }
                    return blend.r < 0.18f && blend.b + blend.g * 0.45f > 0.44f;

                case DecorationZone.HeroTree:
                    if (centerDistance < NexusClearingRadius + 24f) { return false; }
                    if (nearestTrail < 6.5f) { return false; }
                    if (groveMask < 0.28f) { return false; }
                    return outerWeight > 0.14f && outerWeight < 0.88f && blend.r < 0.1f && blend.b + blend.g * 0.2f > 0.48f;

                case DecorationZone.NexusOuter:
                    if (centerDistance < NexusClearingRadius + 5f || centerDistance > NexusClearingRadius + 18f) { return false; }
                    return blend.r < 0.34f && blend.b + blend.g * 0.7f > 0.28f;

                default:
                    return false;
            }
        }

        private bool IsDecorationPointAllowed(Vector2 point, Vector2 center, IReadOnlyList<TrailSpline> trails, DecorationPlacementKind kind) // 군집 내부 포인트 허용
        {
            if (!IsInsideMap(point, center, 2f))
            {
                return false;
            }

            float centerDistance = Vector2.Distance(point, center);
            Color blend = EvaluateTerrainBlend(point, center, trails);
            float nearestTrail = CalculateNearestTrailSignedDistance(point, trails);

            switch (kind)
            {
                case DecorationPlacementKind.Tree:
                    if (centerDistance < NexusClearingRadius + 15f) { return false; }
                    return nearestTrail > 4.4f && blend.r < 0.1f && blend.b + blend.g * 0.2f > 0.5f;

                case DecorationPlacementKind.Rock:
                    if (centerDistance < NexusClearingRadius + 10f) { return false; }
                    return nearestTrail > 3f && blend.r < 0.16f && blend.b + blend.g * 0.3f > 0.48f;

                case DecorationPlacementKind.Flower:
                    if (centerDistance < NexusClearingRadius + 4.5f) { return false; }
                    return blend.r < 0.3f && blend.b + blend.g * 0.75f > 0.35f;

                case DecorationPlacementKind.Plant:
                    if (centerDistance < NexusClearingRadius + 3.5f) { return false; }
                    return blend.r < 0.36f && blend.b + blend.g * 0.62f > 0.27f;

                default:
                    return false;
            }
        }

        private GameObject SpawnPrefab(GameObject prefab, Transform parent, Vector2 center, Vector2 point, float minScale, float maxScale, System.Random random, bool castShadow, bool receiveShadow) // 프리팹 1개 배치
        {
            if (prefab == null)
            {
                return null;
            }

            float y = SampleTerrainHeight(point.x, point.y, center) + DecorationSurfaceOffset;
            Vector3 position = new Vector3(point.x, y, point.y);
            Quaternion rotation = Quaternion.Euler(0f, RandomRange(random, 0f, 360f), 0f);
            GameObject instance = Instantiate(prefab, position, rotation, parent);
            instance.name = prefab.name;
            float scale = RandomRange(random, Mathf.Max(0.01f, minScale), Mathf.Max(minScale, maxScale));
            instance.transform.localScale = Vector3.one * scale;
            ConfigureDecorationInstance(instance, castShadow, receiveShadow);
            return instance;
        }

        private void ConfigureDecorationInstance(GameObject instance, bool castShadow, bool receiveShadow) // 렌더/충돌 정리
        {
            if (instance == null)
            {
                return;
            }

            instance.isStatic = true;
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            bool shouldCastShadow = ShouldCastDecorationShadow(instance, castShadow);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = shouldCastShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderers[i].receiveShadows = receiveShadow;
                renderers[i].lightProbeUsage = LightProbeUsage.Off;
                renderers[i].reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            if (!DisableDecorationColliders)
            {
                return;
            }

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false; // 1차 장식은 동선 방해 금지
            }
        }

        private bool ShouldCastDecorationShadow(GameObject instance, bool requestedCastShadow) // 잔디 제외 그림자 정책
        {
            if (instance == null || !CastLargeDecorationRealtimeShadows)
            {
                return false;
            }

            return requestedCastShadow && IsLargeRealtimeShadowPrefab(instance);
        }

        private static bool IsGrassShadowExcluded(GameObject instance) // 기본 잔디 그림자 제외
        {
            return instance != null && instance.name.StartsWith("SM_grass_", StringComparison.Ordinal);
        }

        private static bool IsLargeRealtimeShadowPrefab(GameObject instance) // 실시간 그림자 대상
        {
            return IsTreeShadowPrefab(instance) || IsRockShadowPrefab(instance);
        }

        private static bool IsTreeShadowPrefab(GameObject instance) // 나무 판정
        {
            return instance != null && instance.name.StartsWith("SM_tree_", StringComparison.Ordinal);
        }

        private static bool IsRockShadowPrefab(GameObject instance) // 돌 판정
        {
            return instance != null && instance.name.StartsWith("SM_rock_", StringComparison.Ordinal);
        }

        private Material ResolveContactShadowMaterial() // 접지 그림자 머티리얼
        {
            if (runtimeContactShadowMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Sprites/Default");
                }

                if (shader == null)
                {
                    return null;
                }

                runtimeContactShadowMaterial = new Material(shader) { name = "MeadowClusterContactShadow_Runtime" };
                runtimeContactShadowMaterial.renderQueue = (int)RenderQueue.Transparent + 80;
                runtimeContactShadowMaterial.SetOverrideTag("RenderType", "Transparent");
                if (runtimeContactShadowMaterial.HasProperty("_Surface")) { runtimeContactShadowMaterial.SetFloat("_Surface", 1f); }
                if (runtimeContactShadowMaterial.HasProperty("_Blend")) { runtimeContactShadowMaterial.SetFloat("_Blend", 0f); }
                if (runtimeContactShadowMaterial.HasProperty("_AlphaClip")) { runtimeContactShadowMaterial.SetFloat("_AlphaClip", 0f); }
                if (runtimeContactShadowMaterial.HasProperty("_SrcBlend")) { runtimeContactShadowMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); }
                if (runtimeContactShadowMaterial.HasProperty("_DstBlend")) { runtimeContactShadowMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); }
                if (runtimeContactShadowMaterial.HasProperty("_ZWrite")) { runtimeContactShadowMaterial.SetFloat("_ZWrite", 0f); }
                if (runtimeContactShadowMaterial.HasProperty("_ZTest")) { runtimeContactShadowMaterial.SetFloat("_ZTest", (float)CompareFunction.Always); }
                if (runtimeContactShadowMaterial.HasProperty("_Cull")) { runtimeContactShadowMaterial.SetFloat("_Cull", (float)CullMode.Off); }
                Texture2D shadowTexture = ResolveContactShadowTexture();
                if (runtimeContactShadowMaterial.HasProperty("_BaseMap")) { runtimeContactShadowMaterial.SetTexture("_BaseMap", shadowTexture); }
                if (runtimeContactShadowMaterial.HasProperty("_MainTex")) { runtimeContactShadowMaterial.SetTexture("_MainTex", shadowTexture); }
                runtimeContactShadowMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                runtimeContactShadowMaterial.DisableKeyword("_ALPHATEST_ON");
            }

            Color color = new Color(0f, 0f, 0f, GetEffectiveClusterContactShadowAlpha());
            if (runtimeContactShadowMaterial.HasProperty("_BaseColor")) { runtimeContactShadowMaterial.SetColor("_BaseColor", color); }
            if (runtimeContactShadowMaterial.HasProperty("_Color")) { runtimeContactShadowMaterial.SetColor("_Color", color); }
            if (runtimeContactShadowMaterial.HasProperty("_TintColor")) { runtimeContactShadowMaterial.SetColor("_TintColor", color); }
            return runtimeContactShadowMaterial;
        }

        private Texture2D ResolveContactShadowTexture() // 부드러운 접지 그림자 알파
        {
            if (runtimeContactShadowTexture != null)
            {
                return runtimeContactShadowTexture;
            }

            const int size = 64;
            runtimeContactShadowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "MeadowClusterContactShadowAlpha_Runtime",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(u * u + v * v);
                    float alpha = Smooth01(1f - Mathf.Clamp01(distance));
                    alpha *= alpha;
                    pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
                }
            }

            runtimeContactShadowTexture.SetPixels(pixels);
            runtimeContactShadowTexture.Apply(false, true);
            return runtimeContactShadowTexture;
        }

        private static Mesh GetContactShadowMesh() // 타원형 접지 그림자 메시
        {
            if (contactShadowMesh != null)
            {
                return contactShadowMesh;
            }

            const int segments = 24;
            Vector3[] vertices = new Vector3[segments + 1];
            Vector2[] uvs = new Vector2[segments + 1];
            int[] triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * 0.5f;
                float z = Mathf.Sin(angle) * 0.5f;
                vertices[i + 1] = new Vector3(x, 0f, z);
                uvs[i + 1] = new Vector2(x + 0.5f, z + 0.5f);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i == segments - 1 ? 1 : i + 2;
                triangles[i * 3 + 2] = i + 1;
            }

            contactShadowMesh = new Mesh { name = "MeadowClusterContactShadowMesh" };
            contactShadowMesh.vertices = vertices;
            contactShadowMesh.uv = uvs;
            contactShadowMesh.triangles = triangles;
            contactShadowMesh.RecalculateBounds();
            return contactShadowMesh;
        }

        private float SampleTerrainHeight(float worldX, float worldZ, Vector2 center) // 장식 배치 높이
        {
            float heightNoise = SampleFractalNoise(worldX, worldZ);
            float outerWeight = CalculateOuterWeight(worldX, worldZ, center);
            float amplitude = Mathf.Lerp(PlayableHeightAmplitude, OuterHeightAmplitude, outerWeight);
            float rimRise = OuterRimRise * outerWeight * outerWeight;
            return VisualYOffset + heightNoise * amplitude + rimRise;
        }

        private DecorationCatalog ResolveDecorationCatalog() // 프리팹 묶음 확보
        {
            return new DecorationCatalog(
                ResolveDecorationPrefabs(TreePrefabs, DecorationCatalogAsset != null ? DecorationCatalogAsset.TreePrefabs : null, DefaultTreePrefabPaths),
                ResolveDecorationPrefabs(LightTreePrefabs, DecorationCatalogAsset != null ? DecorationCatalogAsset.LightTreePrefabs : null, DefaultLightTreePrefabPaths),
                ResolveDecorationPrefabs(BushPrefabs, DecorationCatalogAsset != null ? DecorationCatalogAsset.BushPrefabs : null, DefaultBushPrefabPaths),
                ResolveDecorationPrefabs(GrassPrefabs, DecorationCatalogAsset != null ? DecorationCatalogAsset.GrassPrefabs : null, DefaultGrassPrefabPaths),
                ResolveDecorationPrefabs(FlowerGroupPrefabs, DecorationCatalogAsset != null ? DecorationCatalogAsset.FlowerGroupPrefabs : null, DefaultFlowerGroupPrefabPaths),
                ResolveDecorationPrefabs(FlowerAccentPrefabs, DecorationCatalogAsset != null ? DecorationCatalogAsset.FlowerAccentPrefabs : null, DefaultFlowerAccentPrefabPaths),
                ResolveDecorationPrefabs(MushroomPrefabs, DecorationCatalogAsset != null ? DecorationCatalogAsset.MushroomPrefabs : null, DefaultMushroomPrefabPaths),
                ResolveDecorationPrefabs(RockGroupPrefabs, DecorationCatalogAsset != null ? DecorationCatalogAsset.RockGroupPrefabs : null, DefaultRockGroupPrefabPaths));
        }

        private PandazoleDecorationCatalog ResolvePandazoleDecorationCatalog() // Pandazole 전용 프리팹 묶음
        {
            return new PandazoleDecorationCatalog(
                ResolvePandazolePrefabs(PandazoleFlowerPrefabs, DecorationCatalogAsset != null ? DecorationCatalogAsset.PandazoleFlowerPrefabs : null, "Flower_"),
                ResolvePandazolePrefabs(PandazoleGrassPrefabs, DecorationCatalogAsset != null ? DecorationCatalogAsset.PandazoleGrassPrefabs : null, "Grass_"),
                ResolvePandazolePrefabs(PandazoleFoliagePrefabs, DecorationCatalogAsset != null ? DecorationCatalogAsset.PandazoleFoliagePrefabs : null, "Foliage_"),
                ResolvePandazolePrefabs(PandazoleMushroomPrefabs, DecorationCatalogAsset != null ? DecorationCatalogAsset.PandazoleMushroomPrefabs : null, "Mashroom_"),
                ResolvePandazolePrefabs(PandazoleLogPrefabs, DecorationCatalogAsset != null ? DecorationCatalogAsset.PandazoleLogPrefabs : null, "Log_"),
                ResolvePandazolePrefabs(PandazoleBonePrefabs, DecorationCatalogAsset != null ? DecorationCatalogAsset.PandazoleBonePrefabs : null, "Bones_"),
                ResolvePandazolePrefabs(PandazoleSkullPrefabs, DecorationCatalogAsset != null ? DecorationCatalogAsset.PandazoleSkullPrefabs : null, "Skull_"));
        }

        private GameObject[] ResolveDecorationPrefabs(GameObject[] assigned, GameObject[] catalogPrefabs, IReadOnlyList<string> editorFallbackPaths) // 직접 지정 > 카탈로그 > 에디터 fallback
        {
            GameObject[] compact = CompactPrefabs(assigned);
            if (compact.Length > 0)
            {
                return compact;
            }

            compact = CompactPrefabs(catalogPrefabs);
            if (compact.Length > 0)
            {
                return compact;
            }

#if UNITY_EDITOR
            if (!UseMeadowNatureEditorFallbacks || editorFallbackPaths == null)
            {
                return Array.Empty<GameObject>();
            }

            List<GameObject> loaded = new List<GameObject>(editorFallbackPaths.Count);
            for (int i = 0; i < editorFallbackPaths.Count; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(editorFallbackPaths[i]);
                if (prefab != null)
                {
                    loaded.Add(prefab);
                }
            }

            return loaded.ToArray();
#else
            return Array.Empty<GameObject>();
#endif
        }

        private GameObject[] ResolvePandazolePrefabs(GameObject[] assigned, GameObject[] catalogPrefabs, params string[] prefixes) // 직접 지정 > 카탈로그 > 에디터 fallback
        {
            GameObject[] compact = CompactPrefabs(assigned);
            if (compact.Length > 0)
            {
                return compact;
            }

            compact = CompactPrefabs(catalogPrefabs);
            if (compact.Length > 0)
            {
                return compact;
            }

#if UNITY_EDITOR
            if (!UsePandazoleEditorFallbacks || prefixes == null || prefixes.Length == 0)
            {
                return Array.Empty<GameObject>();
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PandazolePrefabFolder });
            List<string> paths = new List<string>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (HasAnyPrefix(fileName, prefixes))
                {
                    paths.Add(path);
                }
            }

            paths.Sort(StringComparer.Ordinal);
            List<GameObject> loaded = new List<GameObject>(paths.Count);
            for (int i = 0; i < paths.Count; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                if (prefab != null)
                {
                    loaded.Add(prefab);
                }
            }

            return loaded.ToArray();
#else
            return Array.Empty<GameObject>();
#endif
        }

        private static bool HasAnyPrefix(string value, IReadOnlyList<string> prefixes) // prefix 필터
        {
            if (string.IsNullOrEmpty(value) || prefixes == null)
            {
                return false;
            }

            for (int i = 0; i < prefixes.Count; i++)
            {
                if (!string.IsNullOrEmpty(prefixes[i]) && value.StartsWith(prefixes[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static GameObject[] CompactPrefabs(GameObject[] source) // null 제거
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<GameObject>();
            }

            List<GameObject> compact = new List<GameObject>(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != null)
                {
                    compact.Add(source[i]);
                }
            }

            return compact.ToArray();
        }

        private static Transform CreateChildRoot(Transform parent, string name) // 계층 루트
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child.transform;
        }

        private static void DestroyGeneratedObject(GameObject target) // 런타임/에디터 공용 제거
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static GameObject ChoosePrefab(System.Random random, IReadOnlyList<GameObject> primary, IReadOnlyList<GameObject> fallback) // 가중 없는 선택
        {
            if (primary != null && primary.Count > 0)
            {
                return primary[random.Next(primary.Count)];
            }

            if (fallback != null && fallback.Count > 0)
            {
                return fallback[random.Next(fallback.Count)];
            }

            return null;
        }

        private static bool HasMinimumDistance(Vector2 point, IReadOnlyList<Vector2> points, float distance) // 군집 간격
        {
            if (points == null || points.Count == 0 || distance <= 0f)
            {
                return true;
            }

            float sqr = distance * distance;
            for (int i = 0; i < points.Count; i++)
            {
                if ((point - points[i]).sqrMagnitude < sqr)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsInsideMap(Vector2 point, Vector2 center, float margin) // 맵 안쪽
        {
            float half = Mathf.Max(5f, MapSize * 0.5f - margin);
            return Mathf.Abs(point.x - center.x) <= half && Mathf.Abs(point.y - center.y) <= half;
        }

        private float CalculateNearestTrailSignedDistance(Vector2 point, IReadOnlyList<TrailSpline> trails) // 가장 가까운 길 가장자리 거리
        {
            if (trails == null || trails.Count == 0)
            {
                return float.MaxValue;
            }

            float best = float.MaxValue;
            for (int i = 0; i < trails.Count; i++)
            {
                float signed = CalculateTrailSignedDistance(point, trails[i]);
                if (signed < best)
                {
                    best = signed;
                }
            }

            return best;
        }

        private static bool TryGetNearestTrailFrame(Vector2 point, IReadOnlyList<TrailSpline> trails, out Vector2 direction, out Vector2 side, out float distance) // 가장 가까운 길 방향
        {
            direction = Vector2.right;
            side = Vector2.up;
            distance = float.MaxValue;

            if (trails == null || trails.Count == 0)
            {
                return false;
            }

            Vector2 bestClosest = point;
            for (int trailIndex = 0; trailIndex < trails.Count; trailIndex++)
            {
                Vector2[] points = trails[trailIndex].Points;
                if (points == null || points.Length < 2)
                {
                    continue;
                }

                for (int i = 0; i < points.Length - 1; i++)
                {
                    Vector2 a = points[i];
                    Vector2 b = points[i + 1];
                    Vector2 segment = b - a;
                    float sqr = segment.sqrMagnitude;
                    if (sqr <= 0.0001f)
                    {
                        continue;
                    }

                    float t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / sqr);
                    Vector2 closest = a + segment * t;
                    float candidateDistance = Vector2.Distance(point, closest);
                    if (candidateDistance >= distance)
                    {
                        continue;
                    }

                    distance = candidateDistance;
                    direction = segment.normalized;
                    bestClosest = closest;
                }
            }

            if (distance == float.MaxValue)
            {
                return false;
            }

            side = Perpendicular(direction);
            if (Vector2.Dot(point - bestClosest, side) < 0f)
            {
                side = -side;
            }

            return true;
        }

        private static Vector2 RandomRingOffset(System.Random random, float minRadius, float maxRadius) // 원형 군집 오프셋
        {
            float angle = RandomRange(random, 0f, Mathf.PI * 2f);
            float min = Mathf.Max(0f, minRadius);
            float max = Mathf.Max(min, maxRadius);
            float radius = Mathf.Sqrt(RandomRange(random, min * min, max * max));
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private static Vector2 RandomPointNearTrail(System.Random random, IReadOnlyList<TrailSpline> trails, float minOffset, float maxOffset) // 길가 후보
        {
            if (trails == null || trails.Count == 0)
            {
                return Vector2.zero;
            }

            TrailSpline trail = trails[random.Next(trails.Count)];
            if (trail.Points == null || trail.Points.Length < 2)
            {
                return trail.Points != null && trail.Points.Length == 1 ? trail.Points[0] : Vector2.zero;
            }

            int index = random.Next(trail.Points.Length - 1);
            Vector2 a = trail.Points[index];
            Vector2 b = trail.Points[index + 1];
            Vector2 direction = b - a;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.right;
            }
            else
            {
                direction.Normalize();
            }

            Vector2 side = new Vector2(-direction.y, direction.x) * (random.NextDouble() < 0.5 ? -1f : 1f);
            Vector2 along = direction * RandomRange(random, -1.6f, 1.6f);
            float offset = RandomRange(random, minOffset, maxOffset);
            return Vector2.Lerp(a, b, (float)random.NextDouble()) + side * offset + along;
        }

        private float CalculateOuterWeight(float worldX, float worldZ, Vector2 center) // 외곽 높이 가중치
        {
            float playableHalf = Mathf.Max(1f, PlayableSize * 0.5f);
            float mapHalf = Mathf.Max(playableHalf + 0.01f, MapSize * 0.5f);
            float axisDistance = Mathf.Max(Mathf.Abs(worldX - center.x), Mathf.Abs(worldZ - center.y));
            float t = Mathf.Clamp01((axisDistance - playableHalf) / Mathf.Max(0.01f, mapHalf - playableHalf));
            return Smooth01(t);
        }

        private float SampleFractalNoise(float worldX, float worldZ) // 높이 fBM
        {
            int octaveCount = Mathf.Clamp(HeightOctaves, 1, 5);
            float frequency = 1f / Mathf.Max(0.001f, HeightNoiseWorldScale);
            float amplitude = 1f;
            float total = 0f;
            float amplitudeTotal = 0f;
            float seedX = Seed * 0.0137f;
            float seedZ = Seed * 0.0173f;

            for (int i = 0; i < octaveCount; i++)
            {
                float sampleX = worldX * frequency + seedX + i * 19.31f;
                float sampleZ = worldZ * frequency + seedZ + i * 41.73f;
                float value = Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f;
                total += value * amplitude;
                amplitudeTotal += amplitude;
                amplitude *= HeightPersistence;
                frequency *= HeightLacunarity;
            }

            return amplitudeTotal <= 0.0001f ? 0f : Mathf.Clamp(total / amplitudeTotal, -1f, 1f);
        }

        private float SampleEdgeNoise(float worldX, float worldZ) // 가장자리 노이즈
        {
            float scale = Mathf.Max(0.001f, EdgeNoiseWorldScale);
            float seedX = Seed * 0.0211f + 79.3f;
            float seedZ = Seed * 0.0317f + 13.7f;
            return Mathf.PerlinNoise(worldX / scale + seedX, worldZ / scale + seedZ) * 2f - 1f;
        }

        private Material ResolveTerrainMaterial() // 머티리얼 보정
        {
            DestroyRuntimeMaterial();

            Material source = TerrainMaterial != null ? TerrainMaterial : ResolveFallbackMaterial();
            runtimeMaterial = new Material(source) { name = $"{source.name}_Runtime" };
            ConfigureTerrainMaterial(runtimeMaterial);
            return runtimeMaterial;
        }

        private static Material ResolveFallbackMaterial() // 기본 머티리얼
        {
            if (fallbackMaterial != null)
            {
                return fallbackMaterial;
            }

            Shader shader = Shader.Find("OZ/Map/Dirt Road Texture Blend");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            fallbackMaterial = new Material(shader) { name = "MeadowTerrain_Fallback_Source" };
            ConfigureTerrainMaterial(fallbackMaterial);
            return fallbackMaterial;
        }

        private static void ConfigureTerrainMaterial(Material material) // 01/02/03 텍스처 연결
        {
            if (material == null)
            {
                return;
            }

            material.SetColor("_Tint01", Color.white);
            material.SetColor("_Tint02", Color.white);
            material.SetColor("_Tint03", Color.white);
            material.SetFloat("_TileSize01", 5f);
            material.SetFloat("_TileSize02", 5f);
            material.SetFloat("_TileSize03", 6f);
            material.SetFloat("_NormalStrength", 0.78f);
            material.SetFloat("_BlendNoiseScale", 0.16f);
            material.SetFloat("_BlendNoiseStrength", 0.16f);
            material.SetFloat("_MacroTintScale", 0.045f);
            material.SetFloat("_MacroTintStrength", 0.055f);
            material.SetFloat("_DetailScale", 38f);
            material.SetFloat("_DetailStrength", 0.035f);
            material.SetFloat("_ShadowStrength", 0.42f);
            material.SetFloat("_AntiTileStrength", 0.18f);
            material.SetFloat("_UvWarpStrength", 0.012f);
            material.SetFloat("_ControlMapBlend", 0f);
            material.SetVector("_ControlMapCenterSize", new Vector4(0f, 0f, 200f, 0f));

#if UNITY_EDITOR
            SetTextureIfFound(material, "_MainTex01", "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Materials/Textures/M_dirt_BaseColor.png");
            SetTextureIfFound(material, "_MainTex02", "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Materials/Textures/M_DirtGrass_BaseMap.png");
            SetTextureIfFound(material, "_MainTex03", "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Materials/Textures/M_grass_BaseMap.png");
            SetTextureIfFound(material, "_NormalTex01", "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Materials/Textures/M_dirt_Normal.png");
            SetTextureIfFound(material, "_NormalTex02", "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Materials/Textures/M_DirtGrass_Normal.png");
            SetTextureIfFound(material, "_NormalTex03", "Assets/ThirdParty/01_Core/STYLIZED Meadow Nature/Materials/Textures/M_grass_Normal.png");
#endif
        }

#if UNITY_EDITOR
        private static void SetTextureIfFound(Material material, string propertyName, string assetPath) // 에디터 테스트용 직접 로드
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture != null)
            {
                material.SetTexture(propertyName, texture);
            }
        }
#endif

        private void DestroyGeneratedRoot(Transform parent) // 이전 생성물 정리
        {
            if (generatedRoot == null)
            {
                Transform existing = parent != null ? parent.Find(GeneratedRootName) : null;
                generatedRoot = existing != null ? existing.gameObject : null;
            }

            if (generatedRoot == null)
            {
                return;
            }

            MeshFilter[] filters = generatedRoot.GetComponentsInChildren<MeshFilter>(true);
            if (Application.isPlaying)
            {
                for (int i = 0; i < filters.Length; i++)
                {
                    if (ShouldDestroyGeneratedMesh(filters[i].sharedMesh))
                    {
                        Destroy(filters[i].sharedMesh);
                    }
                }

                Destroy(generatedRoot);
            }
            else
            {
                for (int i = 0; i < filters.Length; i++)
                {
                    if (ShouldDestroyGeneratedMesh(filters[i].sharedMesh))
                    {
                        DestroyImmediate(filters[i].sharedMesh);
                    }
                }

                DestroyImmediate(generatedRoot);
            }

            generatedRoot = null;
            DestroyRuntimeMaterial();
            DestroyRuntimeContactShadowMaterial();
            DestroyRuntimeContactShadowTexture();
            DestroyRuntimeControlTexture();
            DestroyRuntimeDemoTerrainData();
        }

        private static bool ShouldDestroyGeneratedMesh(Mesh mesh) // 프리팹 원본 메시 보호
        {
            return mesh != null && mesh.name.Equals(GeneratedMeshName, StringComparison.Ordinal);
        }

        private void DestroyRuntimeMaterial() // 생성 머티리얼 정리
        {
            if (runtimeMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(runtimeMaterial);
            }
            else
            {
                DestroyImmediate(runtimeMaterial);
            }

            runtimeMaterial = null;
        }

        private void DestroyRuntimeContactShadowMaterial() // 접지 그림자 머티리얼 정리
        {
            if (runtimeContactShadowMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(runtimeContactShadowMaterial);
            }
            else
            {
                DestroyImmediate(runtimeContactShadowMaterial);
            }

            runtimeContactShadowMaterial = null;
        }

        private void DestroyRuntimeContactShadowTexture() // 접지 그림자 텍스처 정리
        {
            if (runtimeContactShadowTexture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(runtimeContactShadowTexture);
            }
            else
            {
                DestroyImmediate(runtimeContactShadowTexture);
            }

            runtimeContactShadowTexture = null;
        }

        private void DestroyRuntimeControlTexture() // 생성 control map 정리
        {
            if (runtimeControlTexture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(runtimeControlTexture);
            }
            else
            {
                DestroyImmediate(runtimeControlTexture);
            }

            runtimeControlTexture = null;
        }

        private void DestroyRuntimeDemoTerrainData() // 데모 TerrainData 복제본 정리
        {
            if (runtimeDemoTerrainData == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(runtimeDemoTerrainData);
            }
            else
            {
                DestroyImmediate(runtimeDemoTerrainData);
            }

            runtimeDemoTerrainData = null;
        }

        private static Vector2[] SampleCatmullRom(IReadOnlyList<Vector2> controls, int samplesPerSegment) // 곡선 샘플링
        {
            List<Vector2> points = new List<Vector2>();
            int samples = Mathf.Max(1, samplesPerSegment);
            for (int i = 0; i < controls.Count - 1; i++)
            {
                Vector2 p0 = i > 0 ? controls[i - 1] : controls[i];
                Vector2 p1 = controls[i];
                Vector2 p2 = controls[i + 1];
                Vector2 p3 = i + 2 < controls.Count ? controls[i + 2] : p2;

                for (int s = 0; s < samples; s++)
                {
                    float t = s / (float)samples;
                    points.Add(EvaluateCatmullRom(p0, p1, p2, p3, t));
                }
            }

            points.Add(controls[controls.Count - 1]);
            return points.ToArray();
        }

        private static Vector2 EvaluateCatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t) // Catmull-Rom
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1)
                + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static float DistanceToPolyline(Vector2 point, IReadOnlyList<Vector2> points) // 선분 최소 거리
        {
            if (points == null || points.Count == 0)
            {
                return float.MaxValue;
            }

            if (points.Count == 1)
            {
                return Vector2.Distance(point, points[0]);
            }

            float best = float.MaxValue;
            for (int i = 0; i < points.Count - 1; i++)
            {
                float distance = DistanceToSegment(point, points[i], points[i + 1]);
                if (distance < best)
                {
                    best = distance;
                }
            }

            return best;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b) // 선분 거리
        {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq <= 0.0001f)
            {
                return Vector2.Distance(point, a);
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
            return Vector2.Distance(point, a + ab * t);
        }

        private static float RandomRange(System.Random random, float min, float max) // float 랜덤
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        private static float Smooth01(float value) // smoothstep
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        private enum TerrainDetailKind // Terrain Detail 분류
        {
            Grass,
            Flower
        }

        private readonly struct TerrainLayerMapping // 01/02/03 TerrainLayer 실제 인덱스
        {
            public readonly int Dirt;
            public readonly int DirtGrass;
            public readonly int Grass;

            public TerrainLayerMapping(int dirt, int dirtGrass, int grass)
            {
                Dirt = dirt;
                DirtGrass = dirtGrass;
                Grass = grass;
            }
        }

        private readonly struct TerrainDetailEntry // Terrain detail prototype 설정값
        {
            public readonly GameObject Prefab;
            public readonly TerrainDetailKind Kind;
            public readonly float MinWidth;
            public readonly float MaxWidth;
            public readonly float MinHeight;
            public readonly float MaxHeight;
            public readonly int CellDensity;
            public readonly float DensityMultiplier;
            public readonly float InstanceShare;
            public readonly bool IsCarpetGrass;

            public TerrainDetailEntry(GameObject prefab, TerrainDetailKind kind, float minWidth, float maxWidth, float minHeight, float maxHeight, int cellDensity, float densityMultiplier, float instanceShare)
            {
                Prefab = prefab;
                Kind = kind;
                MinWidth = minWidth;
                MaxWidth = maxWidth;
                MinHeight = minHeight;
                MaxHeight = maxHeight;
                CellDensity = cellDensity;
                DensityMultiplier = densityMultiplier;
                InstanceShare = instanceShare;
                IsCarpetGrass = kind == TerrainDetailKind.Grass && IsCarpetGrassPrefab(prefab);
            }
        }

        private enum DecorationZone // 군집 중심 타입
        {
            Tree,
            Rock,
            FlowerPathEdge,
            FlowerMeadow,
            GrassPatch,
            HeroTree,
            NexusOuter
        }

        private enum DecorationPlacementKind // 군집 내부 배치 타입
        {
            Tree,
            Rock,
            Flower,
            Plant
        }

        private enum ClusterContactShadowResult // 접지 그림자 계산 결과
        {
            Success,
            NullCluster,
            NoBounds,
            AlphaZero
        }

        private struct ClusterContactShadowDebugInfo // 접지 그림자 렌더러 진단
        {
            public int RendererCount;
            public int IncludedRendererCount;
            public int GrassRendererCount;
            public int TreeRendererCount;
            public int SelfRendererCount;
            public int RootlessRendererCount;
            public string FirstIncludedRootName;
        }

        private enum DecorationPatternBand // 패턴 선택 그룹
        {
            PathEdge,
            TreeGrove,
            RockGarden,
            GrassPocket,
            Hero,
            NexusOuter
        }

        private enum PandazoleAddonPatternBand // Pandazole 추가 장식 그룹
        {
            FlowerPocket,
            ForestFloor,
            Curio
        }

        private enum DecorationPatternKind // 데모씬 기반 자연물 패턴
        {
            ThinPathGrassFray,
            FlowerSidePocket,
            PathBendAccent,
            TrampledGrassPatch,
            SmallStonePathMarker,
            SingleColorFlowerDrift,
            PathEntranceBloom,
            OpenGrove,
            DenseGroveBack,
            TwinTreeFrame,
            YoungTreeCluster,
            OldTreeWithUnderbrush,
            TreeCrescent,
            MossyStoneMini,
            FlowerStonePatch,
            StoneClusterTriangle,
            RockAndBushAnchor,
            LeafyRockPocket,
            MushroomStonePocket,
            LowGrassCarpet,
            BushPocket,
            GrassWaveLine,
            TinyFlowerScatter,
            WildflowerPlantTuple,
            PlantMushroomLine,
            NexusOuterGrassHalo,
            NexusFlowerAccent,
            NexusStoneCalm
        }

        private sealed class DecorationPatternContext // 패턴 배치 상태
        {
            public readonly Transform PatternRoot;
            public readonly List<Vector2> OccupiedCenters;
            public readonly List<Vector2> TreeCenters;
            public readonly List<Vector2> RockCenters;
            public readonly List<Vector2> FlowerCenters;
            public readonly List<Vector2> GrassCenters;
            public int NextPatternIndex;

            public DecorationPatternContext(Transform patternRoot, int treeCapacity, int rockCapacity, int flowerCapacity, int grassCapacity)
            {
                PatternRoot = patternRoot;
                OccupiedCenters = new List<Vector2>(Mathf.Max(16, treeCapacity + rockCapacity + flowerCapacity + grassCapacity));
                TreeCenters = new List<Vector2>(Mathf.Max(0, treeCapacity));
                RockCenters = new List<Vector2>(Mathf.Max(0, rockCapacity));
                FlowerCenters = new List<Vector2>(Mathf.Max(0, flowerCapacity));
                GrassCenters = new List<Vector2>(Mathf.Max(0, grassCapacity));
                NextPatternIndex = 0;
            }
        }

        private sealed class DecorationCatalog // 자연물 프리팹 묶음
        {
            public readonly GameObject[] Trees;
            public readonly GameObject[] LightTrees;
            public readonly GameObject[] Bushes;
            public readonly GameObject[] Grass;
            public readonly GameObject[] FlowerGroups;
            public readonly GameObject[] FlowerAccents;
            public readonly GameObject[] Mushrooms;
            public readonly GameObject[] Rocks;

            public bool HasAnyPrefab =>
                HasAny(Trees) || HasAny(LightTrees) || HasAny(Bushes) || HasAny(Grass) ||
                HasAny(FlowerGroups) || HasAny(FlowerAccents) || HasAny(Mushrooms) || HasAny(Rocks);

            public DecorationCatalog(GameObject[] trees, GameObject[] lightTrees, GameObject[] bushes, GameObject[] grass, GameObject[] flowerGroups, GameObject[] flowerAccents, GameObject[] mushrooms, GameObject[] rocks)
            {
                Trees = trees ?? Array.Empty<GameObject>();
                LightTrees = lightTrees ?? Array.Empty<GameObject>();
                Bushes = bushes ?? Array.Empty<GameObject>();
                Grass = grass ?? Array.Empty<GameObject>();
                FlowerGroups = flowerGroups ?? Array.Empty<GameObject>();
                FlowerAccents = flowerAccents ?? Array.Empty<GameObject>();
                Mushrooms = mushrooms ?? Array.Empty<GameObject>();
                Rocks = rocks ?? Array.Empty<GameObject>();
            }

            private static bool HasAny(IReadOnlyList<GameObject> prefabs)
            {
                return prefabs != null && prefabs.Count > 0;
            }
        }

        private sealed class PandazoleDecorationCatalog // Pandazole 전용 프리팹 묶음
        {
            public readonly GameObject[] Flowers;
            public readonly GameObject[] Grass;
            public readonly GameObject[] Foliage;
            public readonly GameObject[] Mushrooms;
            public readonly GameObject[] Logs;
            public readonly GameObject[] Bones;
            public readonly GameObject[] Skulls;

            public bool HasAnyPrefab =>
                HasAny(Flowers) || HasAny(Grass) || HasAny(Foliage) || HasAny(Mushrooms) ||
                HasAny(Logs) || HasAny(Bones) || HasAny(Skulls);

            public PandazoleDecorationCatalog(GameObject[] flowers, GameObject[] grass, GameObject[] foliage, GameObject[] mushrooms, GameObject[] logs, GameObject[] bones, GameObject[] skulls)
            {
                Flowers = flowers ?? Array.Empty<GameObject>();
                Grass = grass ?? Array.Empty<GameObject>();
                Foliage = foliage ?? Array.Empty<GameObject>();
                Mushrooms = mushrooms ?? Array.Empty<GameObject>();
                Logs = logs ?? Array.Empty<GameObject>();
                Bones = bones ?? Array.Empty<GameObject>();
                Skulls = skulls ?? Array.Empty<GameObject>();
            }

            private static bool HasAny(IReadOnlyList<GameObject> prefabs)
            {
                return prefabs != null && prefabs.Count > 0;
            }
        }

        private sealed class MeshBuildContext // 메시 생성 임시 데이터
        {
            public Vector2 Center;
            public List<TrailSpline> Trails;
            public float MinX;
            public float MinZ;
            public float MaxX;
            public float MaxZ;
            public float CellSize;
            public int Columns;
            public int Rows;
            public int VertexColumns;
            public int VertexRows;
            public Vector3[] Vertices;
            public Vector2[] Uvs;
            public Color[] Colors;
            public List<int> Triangles;
        }

        private sealed class MeadowLoadingOverlay // 임시 로딩 UI
        {
            private readonly GameObject root;
            private readonly RectTransform fillTransform;
            private readonly Text percentText;
            private readonly Text statusText;

            private MeadowLoadingOverlay(GameObject root, RectTransform fillTransform, Text percentText, Text statusText)
            {
                this.root = root;
                this.fillTransform = fillTransform;
                this.percentText = percentText;
                this.statusText = statusText;
            }

            public static MeadowLoadingOverlay Create(string name, string title, string status, Sprite backgroundSprite) // UI 생성
            {
                GameObject old = GameObject.Find(name);
                if (old != null)
                {
                    UnityEngine.Object.Destroy(old);
                }

                GameObject canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32000;

                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                Image background = CreateImage("LoadingBackground", canvasObject.transform, backgroundSprite == null ? Color.black : Color.white);
                background.sprite = backgroundSprite;
                background.preserveAspect = false;
                RectTransform bgRect = background.rectTransform;
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;

                if (backgroundSprite != null)
                {
                    Image shade = CreateImage("ScreenShade", canvasObject.transform, new Color(0f, 0f, 0f, 0.18f));
                    shade.raycastTarget = false;
                    RectTransform shadeRect = shade.rectTransform;
                    shadeRect.anchorMin = Vector2.zero;
                    shadeRect.anchorMax = Vector2.one;
                    shadeRect.offsetMin = Vector2.zero;
                    shadeRect.offsetMax = Vector2.zero;
                }

                Text titleText = CreateText("Title", canvasObject.transform, title, 42, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.96f, 0.91f, 0.72f, 1f));
                RectTransform titleRect = titleText.rectTransform;
                titleRect.anchorMin = new Vector2(0.5f, 0.5f);
                titleRect.anchorMax = new Vector2(0.5f, 0.5f);
                titleRect.anchoredPosition = new Vector2(0f, -82f);
                titleRect.sizeDelta = new Vector2(760f, 64f);

                Image barBack = CreateImage("ProgressBack", canvasObject.transform, new Color(0.12f, 0.105f, 0.07f, 1f));
                RectTransform barRect = barBack.rectTransform;
                barRect.anchorMin = new Vector2(0.5f, 0.5f);
                barRect.anchorMax = new Vector2(0.5f, 0.5f);
                barRect.anchoredPosition = new Vector2(0f, -146f);
                barRect.sizeDelta = new Vector2(620f, 14f);

                Image fill = CreateImage("ProgressFill", barBack.transform, new Color(0.92f, 0.69f, 0.24f, 1f));
                RectTransform fillRect = fill.rectTransform;
                fillRect.anchorMin = new Vector2(0f, 0f);
                fillRect.anchorMax = new Vector2(0f, 1f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
                fillRect.pivot = new Vector2(0f, 0.5f);

                Text percent = CreateText("Percent", canvasObject.transform, "0%", 20, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.92f, 0.88f, 0.74f, 1f));
                RectTransform percentRect = percent.rectTransform;
                percentRect.anchorMin = new Vector2(0.5f, 0.5f);
                percentRect.anchorMax = new Vector2(0.5f, 0.5f);
                percentRect.anchoredPosition = new Vector2(0f, -176f);
                percentRect.sizeDelta = new Vector2(220f, 30f);

                Text statusText = CreateText("Status", canvasObject.transform, status, 22, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.78f, 0.78f, 0.82f, 1f));
                RectTransform statusRect = statusText.rectTransform;
                statusRect.anchorMin = new Vector2(0.5f, 0.5f);
                statusRect.anchorMax = new Vector2(0.5f, 0.5f);
                statusRect.anchoredPosition = new Vector2(0f, -220f);
                statusRect.sizeDelta = new Vector2(760f, 40f);

                UnityEngine.Object.DontDestroyOnLoad(canvasObject);
                return new MeadowLoadingOverlay(canvasObject, fillRect, percent, statusText);
            }

            public void SetProgress(float progress, string status) // 진행률 갱신
            {
                float value = Mathf.Clamp01(progress);
                fillTransform.anchorMax = new Vector2(value, 1f);
                percentText.text = $"{Mathf.RoundToInt(value * 100f)}%";
                statusText.text = status;
            }

            public void Close() // 제거
            {
                if (root != null)
                {
                    UnityEngine.Object.Destroy(root);
                }
            }

            private static Image CreateImage(string name, Transform parent, Color color) // Image 생성
            {
                GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                child.transform.SetParent(parent, false);
                Image image = child.GetComponent<Image>();
                image.color = color;
                image.raycastTarget = true;
                return image;
            }

            private static Text CreateText(string name, Transform parent, string text, int size, FontStyle style, TextAnchor anchor, Color color) // Text 생성
            {
                GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                child.transform.SetParent(parent, false);
                Text label = child.GetComponent<Text>();
                label.font = ResolveFont();
                label.text = text;
                label.fontSize = size;
                label.fontStyle = style;
                label.alignment = anchor;
                label.color = color;
                label.raycastTarget = false;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                return label;
            }

            private static Font ResolveFont() // 기본 폰트
            {
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font != null)
                {
                    return font;
                }

                Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
                return fonts != null && fonts.Length > 0 ? fonts[0] : null;
            }
        }
    }
}
