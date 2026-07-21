using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class SegmentProjectileRuntime
    {
        private void UpdateSawBounceProjectile() // 톱날 관통 연쇄 이동
        {
            if (!SegmentTargetQuery.IsEnemyUsable(target))
            {
                int lostTargetId = currentSawTargetId; // 사라진 대상 ID
                if (TryFindSawChainTarget(transform.position, lostTargetId, out EnemyController nextTarget))
                {
                    target = nextTarget; // 즉시 새 대상 추적
                    currentSawTargetId = nextTarget.EnemyId; // 새 대상 ID
                }
                else
                {
                    target = null; // 대상 없음
                    UpdateSawLostTargetProjectile(); // 마지막 방향으로 직진
                    return;
                }
            }

            Vector3 targetPosition = GetEnemyHitPosition(target); // 목표 위치
            Vector3 offset = targetPosition - transform.position; // 목표 방향
            float step = GetProjectileSpeed() * Time.deltaTime; // 이번 프레임 이동량
            if (offset.sqrMagnitude <= step * step)
            {
                ReachSawTarget(target, targetPosition); // 목표 명중
                return;
            }

            direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : direction; // 방향 갱신
            transform.position += direction * step; // 목표 방향 이동
            ApplySawFlightRotation(); // 이동 방향 + 회전

            ApplySawPierceHits(transform.position, currentSawTargetId); // 경로 관통 피해
        }

        private void UpdateSawLostTargetProjectile() // 목표가 사라진 톱날 직선 이동
        {
            float step = GetProjectileSpeed() * Time.deltaTime; // 이동량
            transform.position += direction * step; // 현재 방향 유지
            ApplySawFlightRotation(); // 이동 방향 + 회전

            ApplySawPierceHits(transform.position, currentSawTargetId); // 지나가는 적 관통 피해
        }

        private void ReachSawTarget(EnemyController reachedTarget, Vector3 hitPosition) // 톱날 목표 명중
        {
            int reachedTargetId = reachedTarget != null ? reachedTarget.EnemyId : currentSawTargetId; // 명중 전 ID 보관
            ApplySawPierceHits(hitPosition, reachedTargetId); // 도착 지점 주변 관통 피해
            transform.position = hitPosition; // 목표 위치 보정
            ApplySawTargetHit(reachedTarget, hitPosition); // 목표 100% 피해
            if (!TryStartNextSawBounce(hitPosition, reachedTargetId))
            {
                Destroy(gameObject); // 연쇄 종료
            }
        }

        private void ApplySawPierceHits(Vector3 position, int excludedTargetId) // 톱날 경로 관통 피해
        {
            float damageRatio = profile != null ? weaponBonus.ResolveSawPierceDamageRatio(profile.SawPierceDamageRatio) : 0.5f; // 관통 피해 비율
            if (damageRatio <= 0f)
            {
                return; // 관통 피해 없음
            }

            float radius = profile != null ? Mathf.Max(0.01f, profile.ProjectileHitRadius) : 0.1f; // 피해 반경
            DamageData pierceDamage = DamageData.Create(damage.Amount * damageRatio, DamageType.Projectile, damage.SourceSegmentIndex, position, damage.SourceObject); // 50% 피해
            Collider[] hits = Physics.OverlapSphere(position, radius, ~0, QueryTriggerInteraction.Collide); // 현재 위치 주변
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null)
                {
                    continue; // 빈 콜라이더
                }

                EnemyController enemy = hit.GetComponentInParent<EnemyController>(); // 몬스터
                if (!SegmentTargetQuery.IsEnemyUsable(enemy) || enemy.EnemyId == excludedTargetId || hitEnemyIds.Contains(enemy.EnemyId))
                {
                    continue; // 목표/중복 제외
                }

                int enemyId = enemy.EnemyId; // 피해 전 ID 저장
                Vector3 hitPosition = GetEnemyHitPosition(enemy); // 실제 명중 위치
                hitEnemyIds.Add(enemyId); // 같은 구간 중복 방지
                SegmentHitResolver.ApplyDamageAndFeedback(enemy, pierceDamage, profile, hitPosition, position, SegmentMonsterFeedbackKind.Pierce); // 관통 피해 + 피드백
                PlayHitVfx(hitPosition); // 명중 VFX
                PlayProjectileHitSfx(hitPosition);
            }
        }

        private void ApplySawTargetHit(EnemyController enemy, Vector3 position) // 톱날 목표 피해
        {
            if (!SegmentTargetQuery.IsEnemyUsable(enemy))
            {
                return; // 목표 사라짐
            }

            SegmentHitResolver.ApplyDamageAndFeedback(enemy, damage, profile, position, position - direction, SegmentMonsterFeedbackKind.Direct); // 목표 100% 피해 + 피드백
            PlayHitVfx(position); // 명중 VFX
            PlayProjectileHitSfx(position);
        }


        private bool TryStartNextSawBounce(Vector3 origin, int previousTargetId) // 다음 톱날 연쇄 시작
        {
            if (remainingSawBounces <= 0)
            {
                return false; // 남은 연쇄 없음
            }

            remainingSawBounces--; // 연쇄 1회 소모
            hitEnemyIds.Clear(); // 연쇄 후 재타격 가능하게 초기화
            if (!TryFindSawChainTarget(origin, previousTargetId, out EnemyController nextTarget))
            {
                StartRandomSawFlight(); // 대상 없으면 랜덤 이탈
                return true;
            }

            target = nextTarget; // 다음 목표
            currentSawTargetId = nextTarget.EnemyId; // 목표 ID 저장
            Vector3 targetPosition = GetEnemyHitPosition(nextTarget); // 다음 목표 위치
            Vector3 offset = targetPosition - origin; // 방향 계산
            if (offset.sqrMagnitude > 0.0001f)
            {
                direction = offset.normalized; // 다음 이동 방향
            }

            return true;
        }

        private void StartRandomSawFlight() // 톱날 랜덤 이탈 비행
        {
            float angle = Random.Range(0f, 360f); // 수평 랜덤 각도
            direction = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward; // 수평 방향
            target = null; // 목표 없는 직선 비행
            currentSawTargetId = 0; // 제외 대상 없음
        }

        private void ApplySawFlightRotation() // 톱날 비행 회전
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return; // 방향 없음
            }

            sawSpinAngle = Mathf.Repeat(sawSpinAngle + GetSawSpinSpeed() * Time.deltaTime, 360f); // 누적 회전
            Quaternion lookRotation = Quaternion.LookRotation(direction, Vector3.up); // 이동 방향
            transform.rotation = lookRotation * Quaternion.AngleAxis(sawSpinAngle, Vector3.up); // Y축 회전
        }

        private float GetSawSpinSpeed() // 톱날 회전 속도
        {
            return profile != null ? Mathf.Max(0f, profile.SawSpinSpeed) : 1440f; // 기본 빠른 회전
        }

        private bool TryFindSawChainTarget(Vector3 origin, int excludedEnemyId, out EnemyController nextTarget) // 톱날 연쇄 대상 선택
        {
            float range = profile != null ? weaponBonus.ResolveChainRange(profile.ChainRange) : 0.1f; // 연쇄 사거리
            float aimHeight = profile != null ? profile.TargetAimHeight : 0.45f; // 조준 높이
            return SegmentTargetQuery.TryPickMidToLongRandomTarget(origin, range, GetSawTargetMinDistanceRatio(), excludedEnemyId, null, aimHeight, out nextTarget); // 공용 후보 선택
        }

        private float GetSawTargetMinDistanceRatio() // 톱날 중장거리 후보 기준
        {
            return profile != null ? Mathf.Clamp01(profile.SawTargetMinDistanceRatio) : 0.5f; // 기본 절반 이상
        }

    }
}
