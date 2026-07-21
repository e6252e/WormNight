using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyProjectile : MonoBehaviour // 원거리 투사체
    {
        private Transform target; // 투사체가 날아갈 목표

        [Min(0)]
        [SerializeField] private int damage = 1; // 이 투사체 Prefab이 가진 기본 피해량

        [Min(0.1f)]
        [SerializeField] private float moveSpeed = 8f; // 투사체 이동 속도

        [Min(0.1f)]
        [SerializeField] private float arcHeight = 3f; // 포물선으로 솟아오를 높이

        [Min(0.1f)]
        [SerializeField] private float lifeTime = 5f; // 투사체 최대 유지 시간

        private Vector3 startPosition; // 투사체가 발사된 시작 위치
        private Vector3 targetPosition; // 투사체가 도착할 목표 위치

        private float lifeTimer; // 투사체가 생성된 뒤 지난 시간
        private float travelTimer; // 투사체가 날아간 시간
        private float travelDuration; // 시작 위치에서 목표 위치까지 도착하는 데 걸릴 시간

        private bool isConfigured; // Configure가 호출되었는지 확인하는 값
        private int finalDamage; // 버프 배율까지 적용된 최종 피해량

        private void Update()
        {
            lifeTimer += Time.deltaTime; // 지난 시간만큼 유지 시간을 증가시킨다.

            if (lifeTimer >= lifeTime) // 유지 시간이 제한 시간을 넘었다면
            {
                Destroy(gameObject); // 투사체를 제거한다.
                return; // 더 이상 처리하지 않는다.
            }

            if (!isConfigured) // 아직 목표 설정이 되지 않았다면
            {
                return; // 이동하지 않는다.
            }

            travelTimer += Time.deltaTime; // 지난 시간만큼 비행 시간을 증가시킨다.

            float progress = travelTimer / travelDuration; // 현재 비행 진행률을 계산한다.
            progress = Mathf.Clamp01(progress); // 진행률이 0보다 작거나 1보다 커지지 않도록 제한한다.

            Vector3 flatPosition = Vector3.Lerp(startPosition, targetPosition, progress); // 시작 위치에서 목표 위치까지 직선 보간 위치를 구한다.

            float height = Mathf.Sin(progress * Mathf.PI) * arcHeight; // 중간에서 가장 높아지는 포물선 높이를 계산한다.

            Vector3 nextPosition = flatPosition + Vector3.up * height; // 평면 이동 위치에 위쪽 높이를 더해 최종 위치를 만든다.

            Vector3 moveDirection = nextPosition - transform.position; // 현재 위치에서 다음 위치로 향하는 방향을 구한다.

            transform.position = nextPosition; // 계산된 포물선 위치를 적용한다.

            if (moveDirection.sqrMagnitude > 0.0001f) // 이동 방향이 충분히 있다면
            {
                transform.rotation = Quaternion.LookRotation(moveDirection.normalized, Vector3.up); // 투사체가 이동 방향을 바라보게 회전한다.
            }

            if (progress >= 1f) // 목표 위치까지 도착했다면
            {
                if (target != null) // 목표가 아직 존재한다면
                {
                    NexusController.TryApplyDamage(target, finalDamage); // 최종 피해량으로 Nexus에 피해를 준다.
                }

                Destroy(gameObject); // 도착 후 투사체를 제거한다.
            }
        }

        public void Configure(Transform target) // EnemyRangedAttack이 투사체 목표만 넣어주는 함수
        {
            Configure(target, 1.0f); // 공격력 버프 배율 없이 기본 피해량으로 설정한다.
        }

        public void Configure(Transform target, float attackPowerMultiplier) // 목표와 공격력 버프 배율을 넣어주는 함수
        {
            this.target = target; // 매개변수 target을 내부 target field에 저장한다.

            attackPowerMultiplier = Mathf.Max(0.0f, attackPowerMultiplier); // 공격력 배율이 음수가 되지 않게 제한한다.
            finalDamage = Mathf.Max(0, Mathf.RoundToInt(damage * attackPowerMultiplier)); // Prefab 기본 damage에 공격력 버프 배율을 적용한다.

            startPosition = transform.position; // 발사 순간의 위치를 시작 위치로 저장한다.

            if (target != null) // 목표가 있다면
            {
                targetPosition = target.position; // 목표의 현재 위치를 도착 위치로 저장한다.
            }
            else // 목표가 없다면
            {
                targetPosition = transform.position; // 자기 위치를 임시 도착 위치로 사용한다.
            }

            Vector3 flatOffset = targetPosition - startPosition; // 시작 위치에서 목표 위치까지의 거리 벡터를 구한다.
            flatOffset.y = 0f; // 비행 시간 계산은 평면 거리 기준으로 한다.

            float distance = flatOffset.magnitude; // 시작 위치에서 목표까지의 평면 거리를 구한다.

            travelDuration = distance / moveSpeed; // 거리와 속도를 이용해 도착 시간을 계산한다.
            travelDuration = Mathf.Max(0.1f, travelDuration); // 너무 짧은 시간이 되지 않도록 최소 시간을 보장한다.

            lifeTimer = 0f; // 유지 시간을 0으로 초기화한다.
            travelTimer = 0f; // 비행 시간을 0으로 초기화한다.
            isConfigured = true; // 투사체 설정이 끝났다고 표시한다.
        }

        public void Configure(Transform target, int unusedDamage) // 기존 호출부 호환용 함수
        {
            Configure(target, 1.0f); // B 방식에서는 외부 damage를 쓰지 않고 Prefab이 가진 damage 값을 사용한다.
        }
    }
}