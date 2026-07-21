using UnityEngine;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    // CoreTest_StageScene 오른쪽 임시 UI에서 특정 세그먼트를 바로 붙이는 테스트 버튼
    public sealed class CoreTestSegmentAddButton : MonoBehaviour
    {
        public ConvoyController Controller; // 세그먼트를 실제로 붙일 컨보이
        public SegmentDefinition SegmentDefinition; // 추가할 세그먼트 데이터 에셋
        public Button Button; // 클릭을 받을 UI 버튼
        public Text Label; // 버튼에 표시할 텍스트
        public string SegmentId; // 데이터 에셋이 비었을 때 사용할 세그먼트 ID
        public string DisplayName; // 버튼 표시 이름

        private void Awake() // 씬 로드 시 버튼 연결
        {
            ResolveReferences(); // 비어 있는 참조를 씬/자식에서 보강한다.
            if (Button != null)
            {
                Button.onClick.RemoveListener(AddSegment); // 중복 연결을 막는다.
                Button.onClick.AddListener(AddSegment); // 클릭하면 지정 세그먼트를 추가한다.
            }

            RefreshLabel(); // 버튼 텍스트를 고정 표시한다.
        }

        private void OnDestroy() // 오브젝트 제거 시 클릭 연결 해제
        {
            if (Button != null)
            {
                Button.onClick.RemoveListener(AddSegment); // 사라진 오브젝트 호출을 방지한다.
            }
        }

        public void AddSegment() // 버튼 클릭 진입점
        {
            ResolveReferences(); // 런타임 누락 참조를 한 번 더 보강한다.
            if (Controller == null)
            {
                Debug.LogWarning("[CoreTest] 세그먼트 추가 실패: ConvoyController 누락", this); // 씬 연결 누락 알림
                return;
            }

            if (!TryResolvePrefab(out GameObject prefab))
            {
                Debug.LogWarning($"[CoreTest] {GetDisplayName()} 추가 실패: 세그먼트 프리팹 누락", this); // 데이터 누락 알림
                return;
            }

            if (!Controller.TryAddSegment(prefab))
            {
                Debug.LogWarning($"[CoreTest] {GetDisplayName()} 추가 실패: 최대 개수 또는 추가 제한 확인 필요", this); // 규칙상 추가 불가
                return;
            }

            Debug.Log($"[CoreTest] {GetDisplayName()} 세그먼트 추가", this); // 테스트 로그
        }

        private void ResolveReferences() // 필요한 컴포넌트 자동 연결
        {
            if (Controller == null)
            {
                Controller = FindFirstObjectByType<ConvoyController>(); // CoreTest 씬의 플레이어 컨보이 검색
            }

            if (Button == null)
            {
                Button = GetComponent<Button>(); // 같은 오브젝트의 버튼 사용
            }

            if (Label == null && Button != null)
            {
                Label = Button.GetComponentInChildren<Text>(true); // 버튼 자식 텍스트 사용
            }
        }

        private bool TryResolvePrefab(out GameObject prefab) // 현재 레벨에 맞는 프리팹 선택
        {
            prefab = null; // 실패 기본값
            if (SegmentDefinition == null)
            {
                return false; // 데이터 에셋이 없으면 프리팹을 결정할 수 없다.
            }

            string targetId = string.IsNullOrWhiteSpace(SegmentId) ? SegmentDefinition.NormalizedId : SegmentId.Trim(); // 강화/추가 조회 ID
            int currentLevel = Controller != null ? Controller.GetCurrentSegmentLevel(targetId, SegmentDefinition) : 1; // 현재 강화 레벨 반영
            return SegmentDefinition.TryGetSegmentPrefab(currentLevel, out prefab); // 해당 레벨 세그먼트 프리팹 반환
        }

        private void RefreshLabel() // 버튼 글자 갱신
        {
            if (Label == null)
            {
                return; // 표시 대상 없음
            }

            Label.text = $"{GetDisplayName()} 추가"; // 요청한 임시 버튼 표기
        }

        private string GetDisplayName() // 버튼/로그 표시명 결정
        {
            if (!string.IsNullOrWhiteSpace(DisplayName))
            {
                return DisplayName.Trim(); // 씬에서 지정한 이름 우선
            }

            if (SegmentDefinition != null && !string.IsNullOrWhiteSpace(SegmentDefinition.DisplayName))
            {
                return SegmentDefinition.DisplayName.Trim(); // 데이터 에셋 이름 사용
            }

            return string.IsNullOrWhiteSpace(SegmentId) ? "세그먼트" : SegmentId.Trim(); // 최후 fallback
        }
    }
}
