using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class RewardGateway : MonoBehaviour // 몬스터 보상 → 코어 전달 입구
    {
        public static RewardGateway Active { get; private set; } // 현재 입구

        private void Awake() // 등록
        {
            Active = this; // 현재 인스턴스
        }

        private void OnDestroy() // 해제
        {
            if (Active == this)
            {
                Active = null; // 참조 제거
            }
        }

        public bool ReceiveReward(RewardData reward) // 데이터를 받는 곳!! 몬스터 → 코어 보상 입구
        {
            if (!reward.IsValid)
            {
                return false; // 지급 없음
            }

            if (!CoreStatProvider.TryApplyReward(reward)) // 데이터를 보내는 곳!! 보상 입구 → 코어
            {
                Debug.LogWarning("[RewardGateway] CoreStatProvider 없음: 보상 적용 실패", this); // 코어 누락
                return false; // 코어 없음
            }

            return true; // 전달 성공
        }

        public static bool SubmitReward(RewardData reward) // 공통 제출
        {
            if (!reward.IsValid)
            {
                return false; // 지급 없음
            }

            if (Active == null)
            {
                // Debug.LogWarning("[RewardGateway] 보상 입구 없음: GameSystems에 RewardGateway 필요"); // 입구 누락
                // return false; // 직접 전달 금지

                if (CoreStatProvider.TryApplyReward(reward))
                {
                    Debug.LogWarning("[RewardGateway] 보상 입구 없음: CoreStatProvider로 직접 보상 적용", CoreStatProvider.Active);
                    return true;
                }

                Debug.LogWarning("[RewardGateway] 보상 입구 없음: GameSystems에 RewardGateway 또는 CoreStatProvider 필요");
                return false;
            }

            return Active.ReceiveReward(reward); // 보상 입구 경유
        }
    }
}
