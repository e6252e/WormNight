using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyGrowthOrb : MonoBehaviour //몬스터가 흡수할 성장 구슬
    {
        [Min(0.1f)]
        [SerializeField] private float absorbMoveSpeed = 8.0f; //몬스터에게 빨려 들어가는 이동 속도

        [Min(0.05f)]
        [SerializeField] private float collectDistance = 0.5f; // 몬스터와 구슬의거리 흡수

        [Min(0.1f)]
        [SerializeField] private float lifeTime = 20.0f; //Ground에 구슬이 유지되는 시간

        private EnemyHatchlingGrowth absorbTarget; // 이 구슬을 흡수하려는 몬스터 성장 Script Component

        private float lifeTimer; // 구슬이 생성된 뒤 지난 시간

        private bool isAbsorbing; // 현재 몬스터에게 빨려 들어가는 중인지 저장하는 값

        public bool IsAbsorbing // 외부에서 이 구슬이 이미 흡수 중인지 확인하는 property
        {
            get
            {
                return isAbsorbing; // 현재 흡수 중인지 반환한다.
            }
        }

        private void Update()
        {
            lifeTimer += Time.deltaTime; // 지난 시간만큼 유지 시간을 증가시킨다.

            if (lifeTimer >= lifeTime) // 유지 시간이 끝났다면
            {
                Destroy(gameObject); // 구슬을 제거한다.
                return; // 더 이상 처리하지 않는다.
            }

            if (!isAbsorbing) // 아직 흡수 대상이 없다면
            {
                return; // 움직이지 않고 대기한다.
            }

            if (absorbTarget == null) // 흡수 대상 몬스터가 사라졌다면
            {
                isAbsorbing = false; // 흡수 상태를 해제한다.
                absorbTarget = null; // 흡수 대상 참조를 비운다.
                return; // 이번 프레임 처리를 끝낸다.
            }

            MoveToTarget(); // 흡수 대상 쪽으로 이동한다.
        }

        public void StartAbsorb(EnemyHatchlingGrowth target) // 몬스터가 이 구슬을 흡수 대상으로 지정하는 함수
        {
            if (target == null) // 흡수 대상이 없다면
            {
                return; // 흡수를 시작하지 않는다.
            }

            if (isAbsorbing) // 이미 다른 대상에게 빨려 들어가는 중이라면
            {
                return; // 중복 흡수 명령을 받지 않는다.
            }

            absorbTarget = target; // 흡수 대상 몬스터를 저장한다.
            isAbsorbing = true; // 흡수 이동 상태로 바꾼다.
        }

        private void MoveToTarget() // 구슬을 흡수 대상 쪽으로 이동시키는 함수
        {
            Vector3 targetPosition = absorbTarget.transform.position; // 흡수 대상 몬스터의 현재 위치를 가져온다.

            Vector3 offset = targetPosition - transform.position; // 구슬에서 몬스터까지의 방향과 거리 벡터를 구한다.

            if (offset.sqrMagnitude <= collectDistance * collectDistance) // 흡수 완료 거리 안에 들어왔다면
            {
                absorbTarget.ConsumeGrowthOrb(this); // 몬스터에게 구슬을 먹었다고 알린다.
                Destroy(gameObject); // 먹힌 구슬을 제거한다.
                return; // 이동 처리를 끝낸다.
            }

            Vector3 direction = offset.normalized; // 몬스터 방향을 길이 1짜리 방향으로 만든다.

            transform.position += direction * (absorbMoveSpeed * Time.deltaTime); // 몬스터 쪽으로 구슬을 이동시킨다.
        }
    }
}