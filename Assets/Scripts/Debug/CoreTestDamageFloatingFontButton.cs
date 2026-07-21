using UnityEngine;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    public sealed class CoreTestDamageFloatingFontButton : MonoBehaviour // CoreTest 데미지 폰트 변경 버튼
    {
        public Button Button; // 클릭 버튼
        public Text Label; // 버튼 글자

        private void Awake() // 버튼 연결
        {
            ResolveReferences(); // 참조 보장
            if (Button != null)
            {
                Button.onClick.RemoveListener(CycleFont); // 중복 방지
                Button.onClick.AddListener(CycleFont); // 클릭 연결
            }

            RefreshLabel(); // 라벨 초기화
        }

        private void OnEnable() // 다시 켜질 때 현재 폰트 반영
        {
            ResolveReferences(); // 참조 보장
            RefreshLabel(); // 현재 폰트 표시
        }

        private void OnDestroy() // 연결 해제
        {
            if (Button != null)
            {
                Button.onClick.RemoveListener(CycleFont); // 해제
            }
        }

        public void CycleFont() // 폰트 순환
        {
            string fontName = DamageFloatingSpawner.CycleFontAndSpawnSample(); // 폰트 변경 + 샘플
            RefreshLabel(fontName); // 변경된 폰트명 표시
            Debug.Log($"[CoreTest] 데미지 플로팅 폰트 변경: {fontName}", this); // 확인 로그
        }

        private void ResolveReferences() // 참조 보정
        {
            if (Button == null)
            {
                Button = GetComponent<Button>(); // 같은 오브젝트
            }

            if (Label == null && Button != null)
            {
                Label = Button.GetComponentInChildren<Text>(true); // 자식 텍스트
            }
        }

        public void RefreshLabel() // 현재 폰트명 포함 표시
        {
            RefreshLabel(DamageFloatingSpawner.GetActiveFontDisplayName()); // 활성 폰트 조회
        }

        private void RefreshLabel(string fontName) // 버튼 글자
        {
            if (Label != null)
            {
                Label.text = $"폰트변경\n{fontName}"; // 현재 폰트 표시
            }
        }
    }
}
