using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyGrowthOrbDropper : MonoBehaviour // 몬스터가 죽었을 때 성장 구슬을 드랍관리
    {
        [SerializeField] private EnemyGrowthOrb growthOrbPrefab; // 드랍할 성장 구슬 Prefab

        [Min(1)]
        [SerializeField] private int dropCount = 1; // 죽을 때 생성할 구슬 개수

        [Min(0.0f)]
        [SerializeField] private float dropRandomRadius = 5f; // 구슬이 몬스터 주변에 흩어질 범위

        [SerializeField] private float dropHeightOffset = 3f; // 구슬 생성 위치를 위로 올릴 높이

        private EnemyHealth health; // 같은 GameObject에 붙은 EnemyHealth Script Component 참조

        private bool hasDropped; // 이미 구슬을 드랍했는지 확인하는 값

        private void Awake()
        {
            health = GetComponent<EnemyHealth>(); // 같은 GameObject에 붙은 EnemyHealth Script Component를 찾는다.
        }

        private void Update()
        {
            if (hasDropped) // 이미 구슬을 드랍했다면
            {
                return; // 중복 드랍하지 않는다.
            }

            if (health == null) // EnemyHealth가 없다면
            {
                return; // 죽음 상태를 확인할 수 없으므로 종료한다.
            }

            if (!health.IsDead) // 아직 죽지 않았다면
            {
                return; // 구슬을 드랍하지 않는다.
            }

            DropGrowthOrbs(); // 성장 구슬을 드랍한다.
        }

        private void OnDisable()
        {
            if (hasDropped) // 이미 구슬을 드랍했다면
            {
                return; // 중복 드랍하지 않는다.
            }

            if (health == null) // EnemyHealth가 없다면
            {
                return; // 죽음 상태를 확인할 수 없으므로 종료한다.
            }

            if (!health.IsDead) // 죽어서 비활성화된 것이 아니라면
            {
                return; // 구슬을 드랍하지 않는다.
            }

            DropGrowthOrbs(); // 죽은 상태로 비활성화될 때도 성장 구슬을 드랍한다.
        }

        private void DropGrowthOrbs() // 성장 구슬을 생성하는 함수
        {
            if (growthOrbPrefab == null) // 드랍할 성장 구슬 Prefab이 없다면
            {
                return; // 구슬을 생성하지 않는다.
            }

            hasDropped = true; // 중복 드랍을 막기 위해 드랍 완료 상태로 바꾼다.

            for (int i = 0; i < dropCount; i++) // 설정된 개수만큼 구슬을 생성한다.
            {
                Vector3 dropPosition = GetDropPosition(); // 구슬이 생성될 위치를 계산한다.

                Instantiate(growthOrbPrefab, dropPosition, Quaternion.identity, MonsterRuntimeRoot.GetRootOrFallback(transform.parent)); // 성장 구슬 Prefab을 Monsters 밑에 생성한다.
            }
        }

        private Vector3 GetDropPosition() // 구슬 드랍 위치를 계산하는 함수
        {
            Vector2 randomCircle = Random.insideUnitCircle * dropRandomRadius; // 원형 범위 안에서 랜덤 위치를 구한다.

            Vector3 randomOffset = new Vector3(randomCircle.x, dropHeightOffset, randomCircle.y); // XZ 평면 랜덤 위치와 높이 값을 만든다.

            return transform.position + randomOffset; // 몬스터 위치 기준으로 최종 드랍 위치를 반환한다.
        }
    }
}