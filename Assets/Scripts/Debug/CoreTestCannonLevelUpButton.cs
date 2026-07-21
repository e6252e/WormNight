using UnityEngine;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    public sealed class CoreTestCannonLevelUpButton : MonoBehaviour // CoreTest 임시 캐논 레벨업 버튼
    {
        public ConvoyController Controller; // 대상 컨보이
        public SegmentDefinition CannonDefinition; // SG01 정의
        public Button Button; // 클릭 버튼
        public Text Label; // 버튼 글자
        public string CannonSegmentId = "SG01_Cannon"; // 교체 대상 ID
        public string DisplayName = "캐논"; // 표시 이름

        private void Awake() // 연결
        {
            ResolveReferences(); // 누락 보강
            if (Button != null)
            {
                Button.onClick.RemoveListener(LevelUpSegment); // 중복 방지
                Button.onClick.AddListener(LevelUpSegment); // 클릭 연결
            }

            RefreshLabel(); // 초기 글자
        }

        private void OnDestroy() // 정리
        {
            if (Button != null)
            {
                Button.onClick.RemoveListener(LevelUpSegment); // 연결 해제
            }
        }

        public void LevelUpAllCannons() // 버튼 클릭
        {
            LevelUpSegment(); // 기존 호출 호환
        }

        public void LevelUpSegment() // 버튼 클릭
        {
            ResolveReferences(); // 런타임 보강
            if (Controller == null || CannonDefinition == null)
            {
                Debug.LogWarning($"[CoreTest] {GetDisplayName()} 레벨업 실패: ConvoyController 또는 세그먼트 정의 누락", this);
                return;
            }

            int changed = Controller.LevelUpAttachedSegments(CannonSegmentId, CannonDefinition, out int appliedLevel); // 전체 교체
            RefreshLabel(); // 결과 표시
            Debug.Log($"[CoreTest] {GetDisplayName()} Lv.{appliedLevel} 적용, 세그먼트 {changed}개 갱신", this);
        }

        private void ResolveReferences() // 참조 보강
        {
            if (Controller == null)
            {
                Controller = FindFirstObjectByType<ConvoyController>(); // 씬 컨보이
            }

            if (Button == null)
            {
                Button = GetComponent<Button>(); // 같은 오브젝트
            }

            if (Label == null && Button != null)
            {
                Label = Button.GetComponentInChildren<Text>(true); // 버튼 텍스트
            }
        }

        private void RefreshLabel() // 표시 갱신
        {
            if (Label == null)
            {
                return; // 텍스트 없음
            }

            int level = Controller != null ? Controller.GetCurrentSegmentLevel(CannonSegmentId, CannonDefinition) : 1; // 현재 레벨
            int maxLevel = CannonDefinition != null ? Mathf.Max(1, CannonDefinition.MaxLevel) : 3; // 표시 최대
            string displayName = GetDisplayName(); // 버튼 이름
            Label.text = level >= maxLevel ? $"{displayName} Lv.{level} MAX" : $"{displayName} Lv.{level} → Lv.{level + 1}"; // 레벨 표시
        }

        private string GetDisplayName() // 표시명
        {
            if (!string.IsNullOrWhiteSpace(DisplayName))
            {
                return DisplayName.Trim(); // 수동 이름
            }

            if (CannonDefinition != null && !string.IsNullOrWhiteSpace(CannonDefinition.DisplayName))
            {
                return CannonDefinition.DisplayName.Trim(); // 정의 이름
            }

            return string.IsNullOrWhiteSpace(CannonSegmentId) ? "세그먼트" : CannonSegmentId.Trim(); // fallback
        }
    }
}
