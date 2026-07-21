using UnityEngine;

namespace TeamProject01.Gameplay
{
    [System.Serializable]
    public struct DamageData // 공격 피해 전달값
    {
        public float Amount; // 피해량
        public DamageType Type; // 피해 종류
        public int SourceSegmentIndex; // 공격 세그먼트 순번
        public Vector3 HitPosition; // 명중 위치
        public GameObject SourceObject; // 공격 오브젝트

        public bool IsValid => Amount > 0f; // 유효 피해

        public static DamageData Create(float amount, DamageType type, int sourceSegmentIndex, Vector3 hitPosition, GameObject sourceObject) // 생성
        {
            DamageData data = default; // 값 준비
            data.Amount = Mathf.Max(0f, amount); // 피해량 보정
            data.Type = type; // 종류 저장
            data.SourceSegmentIndex = sourceSegmentIndex; // 세그먼트 저장
            data.HitPosition = hitPosition; // 위치 저장
            data.SourceObject = sourceObject; // 출처 저장
            return data; // 결과 반환
        }

        public DamageData WithHitPosition(Vector3 hitPosition) // 명중 위치 갱신
        {
            DamageData data = this; // 기존 복사
            data.HitPosition = hitPosition; // 위치 교체
            return data; // 결과 반환
        }

        public DamageData WithAmount(float amount) // 피해량 갱신
        {
            DamageData data = this; // 기존 복사
            data.Amount = Mathf.Max(0f, amount); // 피해량 보정
            return data; // 결과 반환
        }
    }
}
