using UnityEngine;

namespace TeamProject01.Gameplay
{
    [System.Serializable]
    public struct RewardData // 몬스터 → 보상 입구 → 코어 전달값
    {
        public int Experience; // 경험치
        public int Gold; // 골드
        public int Diamond; // 런 중 획득 다이아
        public int EnemyId; // 몬스터 ID
        public Vector3 Position; // 보상 위치

        public bool IsValid => Experience > 0 || Gold > 0 || Diamond > 0; // 지급 여부

        public static RewardData Create(int experience, int gold, int enemyId, Vector3 position) // 생성
        {
            RewardData data = default; // 값 준비
            data.Experience = Mathf.Max(0, experience); // 경험치 보정
            data.Gold = Mathf.Max(0, gold); // 골드 보정
            data.Diamond = 0; // 기본 다이아 없음
            data.EnemyId = enemyId; // 몬스터 저장
            data.Position = position; // 위치 저장
            return data; // 결과 반환
        }

        public static RewardData CreateDiamond(int diamond, int enemyId, Vector3 position) // 다이아 생성
        {
            RewardData data = default; // 값 준비
            data.Experience = 0; // 경험치 없음
            data.Gold = 0; // 골드 없음
            data.Diamond = Mathf.Max(0, diamond); // 다이아 보정
            data.EnemyId = enemyId; // 몬스터 저장
            data.Position = position; // 위치 저장
            return data; // 결과 반환
        }
    }
}
