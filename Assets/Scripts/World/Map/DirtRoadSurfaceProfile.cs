using UnityEngine;

namespace TeamProject01.Gameplay
{
    [CreateAssetMenu(menuName = "OZ/World/Dirt Road Surface Profile", fileName = "MP_DirtRoadSurface")]
    public sealed class DirtRoadSurfaceProfile : ScriptableObject // 흙길 지형 생성 설정
    {
        [Header("Shape")]
        [Min(0.15f)] public float CellSize = 0.55f; // 메시 격자 간격
        [Min(0.5f)] public float DefaultRoadWidth = 4f; // 기본 길 폭
        [Min(0.5f)] public float CenterRadius = 3.5f; // 중앙 교차로 반경
        [Min(0f)] public float MidBlendWidth = 1.25f; // 01->02 전환 폭
        [Min(0f)] public float GrassBlendWidth = 1.6f; // 02->03 전환 폭
        [Min(0f)] public float EndpointWidenDistance = 3f; // 브릿지 입구 확장 거리
        [Range(0f, 1f)] public float EndpointWidenRatio = 0.25f; // 브릿지 입구 확장 비율
        public float VisualYOffset = 0.035f; // 바닥 위 표시 높이

        [Header("Height Noise")]
        [Min(0f)] public float HeightAmplitude = 0.16f; // 울퉁불퉁한 높낮이
        [Min(0.001f)] public float HeightNoiseWorldScale = 7f; // 높이 노이즈 크기
        [Range(1, 5)] public int HeightOctaves = 3; // 높이 노이즈 중첩
        [Range(0.1f, 0.9f)] public float HeightPersistence = 0.45f; // 옥타브 세기 감소
        [Min(1f)] public float HeightLacunarity = 2f; // 옥타브 주파수 증가

        [Header("Edge Noise")]
        [Min(0f)] public float EdgeNoiseAmplitude = 0.55f; // 길 가장자리 흔들림
        [Min(0.001f)] public float EdgeNoiseWorldScale = 4.5f; // 가장자리 노이즈 크기

        [Header("Low Height Placeholder")]
        public bool LowHeightUsesDirtTexture = true; // 낮은 곳은 01 텍스처
        [Range(-1f, 0f)] public float LowHeightThreshold = -0.55f; // 웅덩이 후보 깊이
        [Range(0f, 1f)] public float LowHeightDirtBlend = 1f; // 01 보정 강도

        [Header("UV")]
        [Min(0.1f)] public float TextureWorldSize = 4f; // 텍스처 반복 크기

        [Header("Seed")]
        public int Seed = 230623; // 재현용 시드

        private static DirtRoadSurfaceProfile runtimeDefault; // 에셋 없는 1차 테스트용

        public static DirtRoadSurfaceProfile RuntimeDefault // 기본값 프로필
        {
            get
            {
                if (runtimeDefault == null)
                {
                    runtimeDefault = CreateInstance<DirtRoadSurfaceProfile>();
                    runtimeDefault.name = "DirtRoadSurfaceProfile_RuntimeDefault";
                    runtimeDefault.hideFlags = HideFlags.HideAndDontSave;
                }

                return runtimeDefault;
            }
        }
    }
}
