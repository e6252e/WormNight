using UnityEngine;

namespace TeamProject01.Gameplay
{
    [CreateAssetMenu(menuName = "OZ/World/Dirt Road Network Profile", fileName = "MP_DirtRoadNetwork")]
    public sealed class DirtRoadNetworkProfile : ScriptableObject // 플랫폼/브릿지 선행 생성 설정
    {
        [Header("Guide Platforms")]
        [Min(2)] public int PlatformCount = 5; // 교차로 기준점 개수
        [Min(0.5f)] public float PlatformRadius = 4.5f; // 교차로 기준 반경
        [Min(2f)] public float PlatformSpacing = 15f; // 기준점 간격
        [Range(0f, 80f)] public float MaxTurnAngle = 35f; // 진행 방향 꺾임
        [Min(0f)] public float PositionJitter = 1.25f; // 위치 흔들림
        public bool UseTransformForward = true; // 루트 forward 기준 진행

        [Header("Bridge")]
        [Min(0.5f)] public float BridgeWidth = 3.6f; // 브릿지 폭
        [Min(0.25f)] public float BridgeCellLength = 1f; // 브릿지 메시 분할
        public float BridgeYOffset = 0.045f; // 브릿지 표시 높이

        [Header("Collider")]
        public bool AddBridgeColliders; // 브릿지 판정
        public bool AddRoadColliders; // 흙길 판정

        [Header("Seed")]
        public int Seed = 230623; // 재현용 시드

        private static DirtRoadNetworkProfile runtimeDefault; // 에셋 없는 1차 테스트용

        public static DirtRoadNetworkProfile RuntimeDefault // 기본값 프로필
        {
            get
            {
                if (runtimeDefault == null)
                {
                    runtimeDefault = CreateInstance<DirtRoadNetworkProfile>();
                    runtimeDefault.name = "DirtRoadNetworkProfile_RuntimeDefault";
                    runtimeDefault.hideFlags = HideFlags.HideAndDontSave;
                }

                return runtimeDefault;
            }
        }
    }
}
