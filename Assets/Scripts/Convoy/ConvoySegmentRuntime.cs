using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class ConvoySegmentRuntime : MonoBehaviour // 세그먼트 공통 런타임
    {
        private static int nextDamageMeterKey; // DPS 미터용 런타임 식별자

        public GroundCheck GroundCheck; // 바닥 체크
        public SegmentBlocker Blocker; // 몬스터 차단
        public SegmentWeaponBehaviour Weapon; // 세그먼트 무기

        public ConvoyController Owner { get; private set; } // 소유 컨보이
        public int ChainIndex { get; private set; } // 연결 순번
        public bool IsAttached { get; private set; } // 연결 상태
        public int DamageMeterKey => EnsureDamageMeterKey(); // 교체/재정렬에도 추적할 디버그 키
        [Min(1)] public int SegmentLevel = 1; // 현재 레벨

        private int damageMeterKey; // 오브젝트별 DPS 추적 키

        private void Awake() // 참조 준비
        {
            EnsureDamageMeterKey(); // DPS 추적 키 준비
            CacheReferences(); // 자식 참조 수집
        }

        public void Configure(ConvoyController owner, int chainIndex, bool attached) // 체인 연결
        {
            EnsureDamageMeterKey(); // 런타임 생성 직후 키 보장
            Owner = owner; // 소유 저장
            ChainIndex = chainIndex; // 순번 저장
            IsAttached = attached; // 상태 저장
            CacheReferences(); // 참조 갱신

            if (Weapon != null)
            {
                Weapon.Configure(this); // 무기 연결
                Weapon.SetWeaponActive(attached); // 붙은 상태만 작동
            }
        }

        public void SetAttached(bool attached) // 연결 상태 변경
        {
            IsAttached = attached; // 상태 저장
            if (Weapon != null)
            {
                Weapon.SetWeaponActive(attached); // 무기 상태
            }
        }

        public void Tick(float deltaTime) // 세그먼트 갱신
        {
            if (!IsAttached || Weapon == null)
            {
                return; // 작동 안 함
            }

            Weapon.TickWeapon(deltaTime); // 무기 갱신
        }

        public void SetSegmentLevel(int level) // 레벨 지정
        {
            SegmentLevel = Mathf.Max(1, level); // 최소 1
        }

        public void AdoptDamageMeterKeyFrom(ConvoySegmentRuntime source) // 레벨 교체 시 기존 누적값 유지
        {
            damageMeterKey = source != null ? source.DamageMeterKey : EnsureDamageMeterKey(); // 기존 키 계승
        }

        private int EnsureDamageMeterKey() // DPS 미터 키 발급
        {
            if (damageMeterKey <= 0)
            {
                damageMeterKey = ++nextDamageMeterKey; // 세션 내 고유값
            }

            return damageMeterKey;
        }

        private void CacheReferences() // 참조 수집
        {
            if (GroundCheck == null)
            {
                GroundCheck = GetComponentInChildren<GroundCheck>(true); // 바닥 체크
            }

            if (Blocker == null)
            {
                Blocker = GetComponent<SegmentBlocker>(); // 차단 컴포넌트
            }

            if (Weapon == null)
            {
                Weapon = GetComponent<SegmentWeaponBehaviour>(); // 무기 컴포넌트
            }
        }
    }
}
