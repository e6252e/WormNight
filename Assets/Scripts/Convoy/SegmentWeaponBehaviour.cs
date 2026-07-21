using UnityEngine;

namespace TeamProject01.Gameplay
{
    public abstract class SegmentWeaponBehaviour : MonoBehaviour // 세그먼트 무기 공통
    {
        private const float CooldownRandomRatio = 0.1f; // cooldown jitter ratio

        public string SegmentId; // 세그먼트 강화 조회 ID

        public ConvoySegmentRuntime Segment { get; private set; } // 소유 세그먼트
        public bool IsWeaponActive { get; private set; } // 작동 여부
        public string EffectiveSegmentId => GetEffectiveSegmentId(); // 실제 강화/교체 ID

        public virtual void Configure(ConvoySegmentRuntime segment) // 세그먼트 연결
        {
            Segment = segment; // 소유 저장
        }

        public virtual void SetWeaponActive(bool active) // 작동 상태
        {
            IsWeaponActive = active; // 상태 저장
        }

        protected SegmentUpgradeData GetUpgrade() // 코어에 누적된 세그먼트 강화값
        {
            return CoreStatProvider.GetSegmentUpgradeOrDefault(GetEffectiveSegmentId()); // ID 기준 조회
        }

        protected static float GetRandomizedCooldown(float cooldown)
        {
            float safeCooldown = Mathf.Max(0f, cooldown);
            return safeCooldown > 0f ? safeCooldown * Random.Range(1f - CooldownRandomRatio, 1f + CooldownRandomRatio) : 0f;
        }

        protected string GetEffectiveSegmentId() // 강화 ID 결정
        {
            if (!string.IsNullOrWhiteSpace(SegmentId))
            {
                return SegmentId.Trim(); // 명시 ID 우선
            }

            string typeName = GetType().Name; // 클래스명 fallback
            return typeName.EndsWith("Weapon", System.StringComparison.Ordinal) ? typeName.Substring(0, typeName.Length - 6) : typeName; // SG01_Cannon
        }

        public abstract void TickWeapon(float deltaTime); // 무기 갱신
    }
}
