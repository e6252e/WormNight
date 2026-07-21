using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class PlayerPickupInteractor : MonoBehaviour // 플레이어 픽업 상호작용
    {
        private const float MinimumRewardMagnetRadius = 4f;
        private const float MinimumRewardPullStrength = 570f;
        private const float MinimumRewardMaxPullSpeed = 252f;

        [SerializeField] private Transform pickupCenter; // 흡수 기준
        [Min(0.1f)]
        [SerializeField] private float rewardMagnetRadius = MinimumRewardMagnetRadius; // 기본 자석 반경
        [Min(0f)]
        [SerializeField] private float rewardPullStrength = MinimumRewardPullStrength; // 기본 흡수 가속
        [Min(0.1f)]
        [SerializeField] private float rewardMaxPullSpeed = MinimumRewardMaxPullSpeed; // 기본 흡수 최대 속도
        [Min(0.05f)]
        [SerializeField] private float rewardCollectDistance = 0.6f; // 획득 거리

        private Transform cachedPickupCenter; // 검색 캐시

        public bool HasActivePickupCandidates => false; // 월드 보상은 카메라 줌 입력을 막지 않는다.

        private void Update()
        {
            Transform center = ResolvePickupCenter();
            if (center == null)
            {
                return;
            }

            WorldRewardPickup.AttractInRange(
                center.position,
                GetEffectiveRewardMagnetRadius(),
                GetEffectiveRewardPullStrength(),
                GetEffectiveRewardMaxPullSpeed(),
                rewardCollectDistance,
                Time.deltaTime);
        }

        private Transform ResolvePickupCenter()
        {
            if (pickupCenter != null && pickupCenter.gameObject.activeInHierarchy)
            {
                return pickupCenter;
            }

            if (cachedPickupCenter != null && cachedPickupCenter.gameObject.activeInHierarchy)
            {
                return cachedPickupCenter;
            }

            if (MonsterInteractionApi.TryGetConvoyTarget(out Transform convoyTarget))
            {
                cachedPickupCenter = convoyTarget;
                return cachedPickupCenter;
            }

            ConvoyController controller = FindFirstObjectByType<ConvoyController>();
            cachedPickupCenter = controller != null ? controller.transform : null;
            return cachedPickupCenter;
        }

        private float GetEffectiveRewardMagnetRadius()
        {
            return Mathf.Max(rewardMagnetRadius, MinimumRewardMagnetRadius);
        }

        private float GetEffectiveRewardPullStrength()
        {
            return Mathf.Max(rewardPullStrength, MinimumRewardPullStrength);
        }

        private float GetEffectiveRewardMaxPullSpeed()
        {
            return Mathf.Max(rewardMaxPullSpeed, MinimumRewardMaxPullSpeed);
        }
    }
}
