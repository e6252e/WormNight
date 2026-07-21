using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public class EnemySlowZone : MonoBehaviour //디버프 몬스터가 생성하는 슬로우 장판, API
    {
        private static readonly List<EnemySlowZone> ActiveSlowZones = new List<EnemySlowZone>(64); //목록을 검사해서 현재 위치가 장판 안쪽인지 확인한다.

        [Min(0.1f)]
        [SerializeField] private float radius = 3.0f; //슬로우 범위 반경

        [Min(0.1f)]
        [SerializeField] private float lifeTime = 4.0f; //장판 유지 시간

        [Range(0.1f, 1.0f)]
        [SerializeField] private float speedMultiplier = 0.5f; //범위 안에서 적용될 이동속도(0.1은 이속 10%, 1이면 정상 이속)

        private float lifeTimer; //범위 생성 후 지난 시간

        public float Radius //외부에서 장판 범위를 읽기 위한 property
        {
            get
            {
                return radius; //장판 범위를 반환한다.
            }
        }
        public Vector3 Position //외부에서 장판 위치를 읽기 위한 property
        {
            get
            {
                return transform.position;//장판 위치를 반환한다.
            }
        }

        public float SpeedMultiplier //외부에서 슬로우 배율을 읽기 위한 property
        {
            get
            {
                return speedMultiplier; // 슬로우 배율을 반환한다.
            }
        }

        private void Awake()
        {
            Collider zoneCollider = GetComponent<Collider>(); //같은 GameObject에 붙은 Collider을 가져온다.

            if(zoneCollider != null)//Collider가 있다면
            {
                zoneCollider.isTrigger = true; //물체를 막지 않게 Trigger로 설정한다.
            }
        }

        private void OnEnable()
        {
            if (!ActiveSlowZones.Contains(this))//목록에 없다면
            {
                ActiveSlowZones.Add(this); //목록에 등록한다.
            }
        }

        private void Update()
        {
            UpdateLifeTime(); //장판 유지 시간을 갱신한다.
        }

        private void OnDisable()
        {
            ActiveSlowZones.Remove(this); //목록에서 제거한다.
        }

        public void Configure(float radius, float lifeTime, float speedMultiplier)// 장판 생성 후 수치를 설정하는 함수
        {            
            this.radius = Mathf.Max(0.1f, radius); //장판 범위를 설정, 최소값을 0.1로 제한
            this.lifeTime = Mathf.Max(0.1f, lifeTime);//장판 유지 시간 설정, 최소값은 0.1초로 제한
            this.speedMultiplier = Mathf.Clamp(speedMultiplier, 0.1f, 1.0f);//플레이어 이동속도 배율 설정, 0.1 = 10% 1 = 원래 속도

            lifeTimer = 0.0f; // 유지 시간 타이머를 초기화한다.

            float diameter = this.radius * 2.0f; // 반경을 지름으로 바꾼다.
            transform.localScale = new Vector3(diameter, transform.localScale.y, diameter); // 장판 범위 크기를 실제 판정 범위 반경에 맞춘다.
        }

        private void UpdateLifeTime()
        {
            lifeTimer += Time.deltaTime; //지난 시간만큼 장판 유지 시간을 증가시킨다.

            if (lifeTimer >= lifeTime) //유지 시간이 끝나면
            {
                Destroy(gameObject); //제거한다.
            }
        }

        public static float GetSpeedMultiplier(Vector3 targetPosition)// ConvoyController가 호출할 API.
        {   
            float resultMultiplier = 1.0f; // 기본값 정상 속도다.

            for (int i = 0; i < ActiveSlowZones.Count; i++) // 생성된 장판 목록을 순회한다.
            {
                EnemySlowZone slowZone = ActiveSlowZones[i]; // 현재 검사할 장판

                if (slowZone == null) // 이미 삭제된 장판이라면
                {
                    continue; // 건너뛴다.
                }

                Vector3 offset = targetPosition - slowZone.Position; // 장판 중심에서 대상까지의 거리 벡터
                offset.y = 0.0f; //높이는 제거한다.

                float radiusSqr = slowZone.Radius * slowZone.Radius;//반경 제곱

                if (offset.sqrMagnitude > radiusSqr) // 장판 범위 밖이라면
                {
                    continue; //적용하지 않는다.
                }
                                
               resultMultiplier = Mathf.Min(resultMultiplier, slowZone.SpeedMultiplier);// 여러 슬로우 장판이 겹쳐 있으면 더 강한 슬로우를 적용한다.
            }

            return resultMultiplier; // 최종 속도 배율을 반환한다.
        }

    }
}