using UnityEngine;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    public sealed class CoreTestBonusChestSpawnButton : MonoBehaviour // CoreTest 보상상자 소환 버튼
    {
        public BonusChestWaveSpawner Spawner; // 보상상자 스포너
        public Button Button; // 클릭 버튼
        public Text Label; // 버튼 글자
        public string LabelOverride = "보상상자\n소환"; // 표시 문구

        private void Awake() // 버튼 연결
        {
            ResolveReferences(); // 참조 보정
            if (Button != null)
            {
                Button.onClick.RemoveListener(SpawnChest); // 중복 방지
                Button.onClick.AddListener(SpawnChest); // 클릭 연결
            }

            RefreshLabel(); // 라벨 표시
        }

        private void OnEnable() // 다시 켜질 때 보정
        {
            ResolveReferences(); // 참조 보정
            RefreshLabel(); // 라벨 표시
        }

        private void OnDestroy() // 연결 해제
        {
            if (Button != null)
            {
                Button.onClick.RemoveListener(SpawnChest); // 제거
            }
        }

        public void SpawnChest() // 보상상자 1개 소환
        {
            ResolveReferences(); // 런타임 누락 보강
            if (Spawner == null)
            {
                Debug.LogWarning("[CoreTest] 보상상자 소환 실패: BonusChestWaveSpawner 없음", this);
                return;
            }

            Spawner.SpawnDebugBonusChestNearNexus(); // 넥서스 주변 1개 랜덤 생성
        }

        private void ResolveReferences() // 참조 자동 연결
        {
            if (Spawner == null)
            {
                Spawner = FindFirstObjectByType<BonusChestWaveSpawner>(); // CoreTest 씬 스포너
            }

            if (Button == null)
            {
                Button = GetComponent<Button>(); // 같은 오브젝트
            }

            if (Label == null && Button != null)
            {
                Label = Button.GetComponentInChildren<Text>(true); // 자식 텍스트
            }
        }

        private void RefreshLabel() // 버튼 글자
        {
            if (Label != null)
            {
                Label.text = string.IsNullOrWhiteSpace(LabelOverride) ? "보상상자\n소환" : LabelOverride.Trim();
            }
        }
    }
}
