using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyObstacle : MonoBehaviour //생성되는 장애물 관리, API
    {
        private static readonly List<EnemyObstacle> ActiveObstacle = new List<EnemyObstacle>(64); //현재 생성되어있는 장애물 목록

        [Min(1.0f)]
        [SerializeField] private float radius = 1.2f; //위치 보정 장애물 반경

        [Min(1.0f)]
        [SerializeField] private float lifeTime = 8.0f; //장애물이 생성되어 유지되는 시간

        [Min(1.0f)]
        [SerializeField] private float riseHeight = 2.0f; //장애물이 생성되어 땅에서 올라오는 시간

        [Min(0.1f)]
        [SerializeField] private float riseDuration = 0.2f; //장애물이 튀어 나오는데 걸리는 시간

        public float Radius //외부에서 장애물 반경을 읽기 위한 property
        {
            get
            {
                return radius; //장애물 반경 반환한다.
            }
        }

        public Vector3 Position
        {
            get
            {
                return transform.position;
            }
        }

        private float lifeTimer; //장애물이 생성된 뒤 지난 시간
        private float riseTimer; //장애물이 올라온 시간

        private Vector3 startPosition; //장애물이 처음 올라올 위치
        private Vector3 finalPosition; //최종 장애물이 올라올 위치

        private bool risePositionReady; //장애물이 올라올 준비가 되었는지 확인한다.

        private void Awake()
        {
            Collider obstacleCollider = GetComponent<Collider>();//같은 GameObject에 붙은 Collider을 가져온다.

            if (obstacleCollider != null) //Collider가 있다면
            {
                obstacleCollider.isTrigger = true; //물리 충돌로 밀리지 않게 Trigger로 설정한다.
            }

            SetupRisePosition(); //장애물이 올라올 사작 위치와 최종 위치를 설정한다.
        }

        private void OnEnable() //GameObject가 활성화 되었을 떄 호출한다.
        {
            if (!ActiveObstacle.Contains(this)) //장애물 목록에 등록되어 있지 않다면
            {
                ActiveObstacle.Add(this); //현재 장애물을 목록에 등록한다.
            }
        }

        private void Update()
        {
            UpdateRise();
            UpdateLifeTime();
        }

        private void OnDisable() //GameObject가 비활성화 되었을 때 호출한다.
        {
            ActiveObstacle.Remove(this); //장애물 목록에서 제거한다.
        }

        private void SetupRisePosition() //장애물이 올라올 위치를 준비하는 함수
        {
            if (risePositionReady)
            {
                return;
            }

            finalPosition = transform.position; //현재 지정된 위치를 최종 위치로 저장한다.
            startPosition = finalPosition + Vector3.down * riseHeight; //장애물이 올라올 시작부분을 최종 위치 아래쪽으로 한다.

            transform.position = startPosition; //장애물 시작위치를 최종위치 아래에 저장한다.

            riseTimer = 0.0f; //올라올 시간을 초기화한다.
            risePositionReady = true; //자애물이 올라올 준비가 되었다.
        }

        private void UpdateRise() //장애물이 올라올 조건을 처리하는 함수
        {
            if (!risePositionReady) //올라올 준비가 되지 않았다면
            {
                SetupRisePosition(); //장애물 위치를 준비한다.
            }

            if (riseTimer >= riseDuration) //장애물이 올라왔다면
            {
                transform.position = finalPosition; //최종 위치로 저장한다.
                return;
            }

            riseTimer += Time.deltaTime; //올라온 시간만큼 시간을 증감시킨다.

            float progress = Mathf.Clamp01(riseTimer / riseDuration);

            transform.position = Vector3.Lerp(startPosition, finalPosition, progress);
        }

        private void UpdateLifeTime() //장애물이 유지되는 시간 조건을 처리하는 함수
        {
            lifeTimer += Time.deltaTime;//생성된 시간 만큼 시간을 증가 시킨다.

            if (lifeTimer >= lifeTime) //유지시간이 끝났다면
            {
                Destroy(gameObject);//장애물 GameObject를 제거한다.
            }
        }

        public void Configure(float radius, float lifeTime)
        {
            this.radius = Mathf.Max(1.0f, radius);
            this.lifeTime = Mathf.Max(1.0f, lifeTime);

            lifeTimer = 0.0f;
        }

        public static Vector3 ResolvePosition(Vector3 currentPosition, Vector3 desiredPosition, float moverRadius)
        {
            Vector3 resolvedPosition = desiredPosition;

            for (int i = 0; i < ActiveObstacle.Count; i++)
            {
                EnemyObstacle obstacle = ActiveObstacle[i];

                if (obstacle == null)
                {
                    continue;
                }

                Vector3 offset = resolvedPosition - obstacle.Position;
                offset.y = 0.0f;

                float minDistance = moverRadius + obstacle.Radius;
                float minDistanceSqr = minDistance * minDistance;

                if (offset.sqrMagnitude >= minDistanceSqr)
                {
                    continue;
                }

                Vector3 pushDirection;

                if (offset.sqrMagnitude > 0.0001f)
                {
                    pushDirection = offset.normalized;
                }
                else
                {
                    Vector3 fallbackDirection = currentPosition - obstacle.Position;
                    fallbackDirection.y = 0.0f;

                    pushDirection = fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection.normalized : Vector3.forward;
                }

                resolvedPosition = obstacle.Position + pushDirection * minDistance;
                resolvedPosition.y = desiredPosition.y;
            }

            return resolvedPosition;
        }
    }
}