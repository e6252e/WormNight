using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyReward : MonoBehaviour // 몬스터 보상
    {
        private const int MinimumExperiencePickupAmount = 3; // 몬스터 경험치 픽업 최소값
        private const float DefaultWorldRewardDropChance = 0.5f; // 경험치/골드 월드 픽업 기본 드랍률
        private const int DefaultWorldRewardAmountMultiplier = 2; // 드랍 수를 줄인 만큼 1회 획득량 보정

        [Min(0)]
        [SerializeField] private int experienceReward = MinimumExperiencePickupAmount; // 처치 경험치

        [Min(0)]
        [SerializeField] private int goldReward = 1; // 처치 골드

        [Header("World Reward Drop")]
        [Range(0f, 1f)]
        [SerializeField] private float experienceDropChance = DefaultWorldRewardDropChance; // 경험치 픽업 드랍률
        [Range(0f, 1f)]
        [SerializeField] private float goldDropChance = DefaultWorldRewardDropChance; // 골드 픽업 드랍률
        [Min(1)]
        [SerializeField] private int worldRewardAmountMultiplier = DefaultWorldRewardAmountMultiplier; // 드랍 성공 시 경험치/골드 배율

        [Header("Elite Diamond")]
        [SerializeField] private bool enableEliteDiamondDrop = true; // 엘리트 다이아 드랍
        [Range(0f, 1f)]
        [SerializeField] private float eliteDiamondDropChance = 0.5f; // 엘리트 50%
        [Min(0)]
        [SerializeField] private int eliteDiamondReward = 2; // 엘리트 드랍 다이아

        public void GiveReward(int enemyId, Vector3 position, EnemyGrade grade) // 처치 보상 지급
        {
            int resolvedExperience = ResolveExperienceReward(); // 드랍률 50% / 수치 2배 보정
            int resolvedGold = ResolveGoldReward(); // 드랍률 50% / 수치 2배 보정
            RewardData reward = RewardData.Create(resolvedExperience, resolvedGold, enemyId, position); // 보상 데이터 생성
            RewardDropService.SpawnReward(reward, position); // 전찬우수정-0621 추가: 보상을 즉시 지급하지 않고 월드 경험치/골드 아이템으로 드랍
            TrySpawnEliteDiamondReward(enemyId, position, grade); // 엘리트 다이아 보너스
            // RewardGateway.SubmitReward(reward); // 전찬우수정-0621 삭제: 몬스터 사망 즉시 코어로 보상 지급하던 기존 방식 제거
        }

        private int ResolveExperienceReward() // 경험치 드랍 확률과 배율 적용
        {
            if (experienceReward <= 0 || !RollDrop(experienceDropChance))
            {
                return 0; // 경험치 드랍 실패
            }

            int baseExperience = Mathf.Max(MinimumExperiencePickupAmount, experienceReward); // 기존 1/2 값은 3짜리 픽업으로 보정
            return baseExperience * Mathf.Max(1, worldRewardAmountMultiplier); // 드랍 성공 시 수치 보정
        }

        private int ResolveGoldReward() // 골드 드랍 확률과 배율 적용
        {
            if (goldReward <= 0 || !RollDrop(goldDropChance))
            {
                return 0; // 골드 드랍 실패
            }

            return Mathf.Max(0, goldReward) * Mathf.Max(1, worldRewardAmountMultiplier); // 드랍 성공 시 수치 보정
        }

        private void TrySpawnEliteDiamondReward(int enemyId, Vector3 position, EnemyGrade grade) // 엘리트 다이아 드랍
        {
            if (!enableEliteDiamondDrop || grade != EnemyGrade.Elite || eliteDiamondReward <= 0)
            {
                return; // 조건 미충족
            }

            if (Random.value > Mathf.Clamp01(eliteDiamondDropChance))
            {
                return; // 확률 실패
            }

            RewardDropService.SpawnDiamond(eliteDiamondReward, position, enemyId); // 월드 픽업 생성
        }

        private static bool RollDrop(float chance) // 0~1 확률 판정
        {
            return Random.value <= Mathf.Clamp01(chance);
        }
    }
}
