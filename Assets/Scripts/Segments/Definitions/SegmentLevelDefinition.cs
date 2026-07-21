using UnityEngine;

namespace TeamProject01.Gameplay
{
    [System.Serializable]
    public struct SegmentLevelDefinition // 세그먼트 레벨별 프리팹 묶음
    {
        [Min(1)] public int Level; // 레벨 번호
        public GameObject SegmentPrefab; // 실제 컨보이에 붙는 프리팹
        public GameObject BodyPrefab; // 확인용 몸통 프리팹
        public GameObject HeadPrefab; // 확인용 머리 프리팹
        public SegmentAttackProfile AttackProfile; // 레벨별 공격 방식
        [TextArea(1, 3)] public string Memo; // 팀원 메모

        public bool HasSegmentPrefab => SegmentPrefab != null; // 런타임 프리팹 존재
    }
}
