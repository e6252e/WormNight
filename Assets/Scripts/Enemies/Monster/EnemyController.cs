using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyController : MonoBehaviour // 몬스터 관리
    {
        private static readonly List<EnemyController> ActiveMonsters = new List<EnemyController>(128); // 타겟 목록
        private static int nextEnemyId; // 몬스터 ID 발급용 번호
        public static event System.Action<EnemyController> DamageKilled; // 플레이어 피해 처치 알림
        public event System.Action<EnemyController> DeathStarted; // 사망 연출 시작 알림

        [SerializeField] private EnemyGrade grade = EnemyGrade.Monster; // 몬스터 등급

        public int EnemyId { get; private set; } // 외부 식별값

        public EnemyGrade Grade // 외부에서 몬스터 등급을 읽기 위한 property
        {
            get
            {
                return grade; // 현재 몬스터 등급을 반환
            }
        }

        private bool dead; //몬스터가 사망 처리되었는지 확인

        private EnemyHealth health; // 체력 처리를 담당하는 EnemyHealth Script Component 참조
        private EnemyReward reward; // 보상 처리를 담당하는 EnemyReward Script Component 참조
        private EnemyDeathAnimatorBridge deathAnimatorBridge; // 조성원추가-0624 - 사망 애니메이션 재생과 지연 제거를 담당하는 Script Component 참조

        public bool IsDead => dead; // 외부 타겟 유효성 확인

        public static int ActiveCount // 현재 활성 몬스터 수
        {
            get
            {
                CleanupActiveList(); // null이거나 죽은 몬스터를 목록에서 정리
                return ActiveMonsters.Count; // 현재 살아있는 몬스터 수를 반환
            }
        }

        private void Awake()
        {
            EnemyId = ++nextEnemyId; // 몬스터마다 고유 ID를 부여

            health = GetComponent<EnemyHealth>(); // 같은 GameObject에 붙은 EnemyHealth Script Component를 찾는다.
            reward = GetComponent<EnemyReward>(); // 같은 GameObject에 붙은 EnemyReward Script Component를 찾는다.
            deathAnimatorBridge = GetComponent<EnemyDeathAnimatorBridge>(); // 조성원추가-0624 - 같은 GameObject에 붙은 사망 애니메이션 연결 Script Component를 찾는다.

            EnemyTags.TryApplyTag(gameObject, grade); // 몬스터 등급에 맞는 Unity Tag를 적용한다.
        }

        private void OnEnable() // 목록 등록
        {
            if (!ActiveMonsters.Contains(this)) // 이미 목록에 등록되어 있지 않다면
            {
                ActiveMonsters.Add(this); // 타겟 등록
            }
        }

        private void OnDisable() // 목록 해제
        {
            ActiveMonsters.Remove(this); // 타겟 제거
        }

        public void Configure(Transform nexus, Material material, float moveSpeed, float stopRadius, float groundHeight, EnemyGrade grade = EnemyGrade.Monster) // 스폰 설정
        {
            this.grade = grade;  // 등급 저장
            EnemyTags.TryApplyTag(gameObject, this.grade); // 태그 적용
        }

        public void ApplyDamage(DamageData damage) // 피해 받기
        {
            if (!damage.IsValid) // 유효하지 않은 피해라면
            {
                return; // 피해 없음
            }

            if (dead) // 이미 사망 처리된 몬스터라면
            {
                return; // 중복 방지
            }

            if (!EnemyShieldRegistry.CanApplyDamage(this, damage)) // 조성원추가-0628 - 보호막 범위 안의 몬스터는 외부 공격 피해를 받지 않게 한다.
            {
                return; // 조성원추가-0628 - 보호막에 막힌 피해는 HP를 깎지 않는다.
            }

            if (health == null) // EnemyHealth가 붙어 있지 않다면
            {
                KillByDamage(); // 체력 계산 없이 즉시 사망 처리한다.
                return; // 더 이상 처리하지 않는다.
            }

            EnemySupportDebuffState supportDebuff = GetComponent<EnemySupportDebuffState>(); // 전찬우추가-0621 - 지원형 디버프 확인
            DamageData resolvedDamage = supportDebuff != null ? supportDebuff.ApplyIncomingDamageBonus(damage) : damage; // 전찬우추가-0621 - 받는 피해 증가 적용

            float hpBeforeDamage = health.CurrentHp; //전찬우추가-0619 - 표시용 피격 전 체력
            health.TakeDamage(resolvedDamage.Amount); // 실제 HP 감소는 EnemyHealth가 담당한다.
            float actualDamage = Mathf.Max(0f, hpBeforeDamage - health.CurrentHp); //전찬우추가-0619 - 실제 감소 체력
            SegmentDpsDebugMeter.RecordDamage(resolvedDamage, actualDamage); // 실제 HP 감소량만 DPS 미터 기록
            DamageFloatingSpawner.SpawnEnemyDamage(resolvedDamage, actualDamage, transform.position); //전찬우추가-0619 - 데미지 숫자 표시

            if (health.IsDead) // HP가 0 이하가 되었다면
            {
                KillByDamage(); // 보상 지급 후 사망 처리한다.
            }
        }

        public void Kill() // 보상 없이 즉시 제거하는 함수
        {
            if (dead) // 이미 사망 처리되었다면
            {
                return; // 중복 방지
            }

            dead = true; // 사망 표시
            DeathStarted?.Invoke(this); // 사망 사운드/연출 보조 시스템에 알린다.

            if (deathAnimatorBridge != null && deathAnimatorBridge.TryBeginDeath()) // 조성원추가-0624 - 사망 애니메이션을 시작할 수 있다면 즉시 제거하지 않는다.
            {
                return; // 조성원추가-0624 - 제거는 EnemyDeathAnimatorBridge의 Death Duration 이후 처리한다.
            }

            RemoveFromBattlefield(); // 조성원수정-0630 - 제거 처리를 함수로 분리해서 나중에 풀링 반환으로 바꾸기 쉽게 한다.
        }

        public void KillByConsumed() // 조성원추가-0630 - 해츨링에게 잡아먹힌 몬스터를 보상 없이 제거한다.
        {
            if (dead) // 조성원추가-0630 - 이미 사망 처리되었다면
            {
                return; // 조성원추가-0630 - 중복 먹힘 처리를 막고 종료한다.
            }

            dead = true; // 조성원추가-0630 - ActiveMonsters 탐색 대상에서 제외되도록 사망 상태로 표시한다.

            RemoveFromBattlefield(); // 조성원추가-0630 - 먹힘은 일반 사망 애니메이션 없이 바로 제거한다.
        }

        private void KillByDamage()  // 피해 사망
        {
            if (dead) // 이미 사망 처리되었다면
            {
                return; // 중복 방지
            }

            if (reward != null) // EnemyReward Script Component가 있다면
            {
                reward.GiveReward(EnemyId, transform.position, grade); // 보상 생성.
            }

            DamageKilled?.Invoke(this); // 런 결과 처치 수 기록
            Kill(); // 공통 제거
        }

        private void RemoveFromBattlefield() // 조성원추가-0630 - 현재는 Destroy로 제거하고, 나중에 풀링 반환으로 교체할 공통 지점
        {
            Destroy(gameObject); // 조성원추가-0630 - 현재 프로젝트 제거 방식 유지
        }

        ////// 전찬우수정-0619 - 태그 검색 대신 ActiveMonsters 기반으로 가까운 몬스터를 찾는다.
        public static bool TryFindNearest(Vector3 origin, float range, out EnemyController target) // 가까운 적 검색
        {
            CleanupActiveList(); //전찬우수정-0619 - 목록 정리

            target = null; // 찾지 못했을 때의 기본값
            float bestDistance = range * range; // 사거리 제곱

            for (int i = 0; i < ActiveMonsters.Count; i++) //전찬우수정-0619 - 활성 몬스터 목록 순회
            {
                TryPickNearest(ActiveMonsters[i], origin, ref bestDistance, ref target); // 최단 갱신
            }

            return target != null; // 대상을 찾았다면 true를 반환한다.
        }

        ////// 전찬우수정-0619 - 조건 검색도 ActiveMonsters 기반으로 통일
        public static bool TryFindNearest(Vector3 origin, float range, System.Func<EnemyController, bool> filter, out EnemyController target) // 조건 포함 가까운 적 검색
        {
            CleanupActiveList(); //전찬우수정-0619 - 목록 정리

            target = null; // 찾지 못했을 때의 기본값
            float bestDistance = range * range; // 사거리 제곱

            for (int i = 0; i < ActiveMonsters.Count; i++) //전찬우수정-0619 - 활성 몬스터 목록 순회
            {
                TryPickNearest(ActiveMonsters[i], origin, filter, ref bestDistance, ref target); // 조건 포함 최단 갱신
            }

            return target != null; // 대상을 찾았다면 true를 반환한다.
        }

        ////// 전찬우추가-0619 - 범위 안 활성 몬스터를 재사용 가능한 목록으로 수집
        public static void CollectActiveInRange(Vector3 origin, float range, List<EnemyController> results, System.Func<EnemyController, bool> filter = null) // 범위 내 몬스터 수집
        {
            if (results == null) // 결과 리스트가 없다면
            {
                return; // 수집 불가
            }

            CleanupActiveList(); //전찬우추가-0619 - 목록 정리
            results.Clear(); //전찬우추가-0619 - 이전 결과 제거

            float rangeSqr = range * range; //전찬우추가-0619 - 사거리 제곱

            for (int i = 0; i < ActiveMonsters.Count; i++) //전찬우추가-0619 - 활성 몬스터 목록 순회
            {
                EnemyController enemy = ActiveMonsters[i]; //전찬우추가-0619 - 후보 몬스터

                if (enemy == null || enemy.dead) // 대상이 없거나 죽었다면
                {
                    continue; // 제외
                }

                if (filter != null && !filter(enemy)) // 추가 조건이 있고 통과하지 못했다면
                {
                    continue; // 제외
                }

                Vector3 offset = enemy.transform.position - origin; // 대상 거리
                offset.y = 0f; // 평면 거리

                if (offset.sqrMagnitude > rangeSqr) // 범위 밖이면
                {
                    continue; // 제외
                }

                results.Add(enemy); //전찬우추가-0619 - 결과 추가
            }
        }

        private static void TryPickNearest(EnemyController enemy, Vector3 origin, ref float bestDistance, ref EnemyController target) // 최단 대상
        {
            if (enemy == null || enemy.dead) // 대상이 없거나 이미 죽었다면
            {
                return; // 대상에서 제외한다.
            }

            Vector3 offset = enemy.transform.position - origin; // 대상 거리
            offset.y = 0f; // 평면 거리

            float distance = offset.sqrMagnitude;  // 제곱 거리

            if (distance > bestDistance) // 현재 최고 대상보다 멀다면
            {
                return; // 갱신하지 않는다.
            }

            bestDistance = distance; // 최단 갱신
            target = enemy;  // 대상 갱신
        }

        ////// 전찬우추가-0619 - 기본 최단 검색에 세그먼트 공격 범위 같은 추가 필터를 적용
        private static void TryPickNearest(EnemyController enemy, Vector3 origin, System.Func<EnemyController, bool> filter, ref float bestDistance, ref EnemyController target) // 조건 포함 최단 대상
        {
            if (enemy == null || enemy.dead) // 대상이 없거나 이미 죽었다면
            {
                return; // 대상에서 제외한다.
            }

            if (filter != null && !filter(enemy)) // 추가 조건이 있고 통과하지 못했다면
            {
                return; // 대상에서 제외한다.
            }

            Vector3 offset = enemy.transform.position - origin; // 대상 거리
            offset.y = 0f; // 평면 거리

            float distance = offset.sqrMagnitude; // 제곱 거리

            if (distance > bestDistance) // 현재 최고 대상보다 멀다면
            {
                return; // 갱신하지 않는다.
            }

            bestDistance = distance; // 최단 갱신
            target = enemy; // 대상 갱신
        }

        private static void CleanupActiveList() // 목록 정리
        {
            ActiveMonsters.RemoveAll(enemy => enemy == null || enemy.dead); // 죽은 대상 제거
        }
    }
}
