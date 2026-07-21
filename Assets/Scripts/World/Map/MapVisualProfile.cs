using UnityEngine;

namespace TeamProject01.Gameplay
{
    [CreateAssetMenu(menuName = "OZ/World/Map Visual Profile", fileName = "MP_VisualGround")]
    public sealed class MapVisualProfile : ScriptableObject // 비주얼 지형 설정
    {
        private const string DefaultResourcePath = "Map/MP_VisualGround";

        [Header("Mesh")]
        [Min(1f)] public float CellSize = 2f; // 격자 간격
        public Vector2 FallbackSize = new Vector2(120f, 120f); // 콜라이더 없을 때 크기
        public float VisualYOffset = 0.025f; // 판정면 위 표시 높이

        [Header("Height Noise")]
        [Min(0f)] public float HeightAmplitude = 0.14f; // 최대 굴곡
        [Min(0.001f)] public float NoiseWorldScale = 24f; // 노이즈 크기
        [Range(1, 5)] public int Octaves = 2; // 노이즈 중첩
        [Range(0.1f, 0.9f)] public float Persistence = 0.45f; // 옥타브 세기 감소
        [Min(1f)] public float Lacunarity = 2f; // 옥타브 주파수 증가
        public int Seed = 230623; // 재현용 시드

        [Header("Flatten")]
        public bool FlattenAroundNexus = true; // 넥서스 주변 평탄화
        public string NexusObjectName = "Nexus_Core"; // 넥서스 검색명
        [Min(0f)] public float NexusFlatRadius = 16f; // 완전 평탄 반경
        [Min(0.01f)] public float NexusBlendDistance = 10f; // 굴곡 복귀 거리

        [Header("Vertex Color")]
        public Color LowColor = new Color(0.19f, 0.24f, 0.18f, 1f); // 낮은 지형
        public Color MidColor = new Color(0.28f, 0.36f, 0.22f, 1f); // 기본 지형
        public Color HighColor = new Color(0.40f, 0.34f, 0.22f, 1f); // 높은 지형

        private static MapVisualProfile cachedDefault; // 정식 기본 에셋

        public static MapVisualProfile Resolve(MapVisualProfile profile) // null 보정
        {
            if (profile != null)
            {
                return profile; // 지정 프로필
            }

            if (cachedDefault == null)
            {
                cachedDefault = Resources.Load<MapVisualProfile>(DefaultResourcePath); // 정식 에셋
            }

            return cachedDefault; // 기본 설정
        }
    }
}
