using System.Collections;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class GenericSegmentWeapon
    {
        private void FireLaser(EnemyController target, DamageData damage) // 레이저 공격
        {
            if (laserRoutine != null)
            {
                StopCoroutine(laserRoutine); // 이전 지속 피해 정리
            }

            laserRoutine = StartCoroutine(ApplyLaserDamage(target, damage)); // 지속 피해 시작
        }

        private IEnumerator ApplyLaserDamage(EnemyController target, DamageData damage) // 레이저 지속 피해
        {
            float tickInterval = GetEffectiveLaserTickInterval(); // 강화 반영 피해 간격
            float timer = GetEffectiveLaserDuration(); // 지속 시간
            WaitForSeconds wait = new WaitForSeconds(tickInterval); // 피해 간격
            while (timer > 0f)
            {
                if (!IsTargetUsable(target))
                {
                    target = TryFindTarget(out EnemyController nextTarget) ? nextTarget : null; // 죽은 대상이면 즉시 재탐색
                }

                if (IsTargetUsable(target))
                {
                    Vector3 hitPosition = target.transform.position + Vector3.up * AttackProfile.TargetAimHeight; // 명중 위치
                    if (Vector3.Distance(transform.position, target.transform.position) <= GetEffectiveSearchRange() && IsTargetInAttackArea(target)) // 레이저 지속 피해도 공격 범위 형태 유지
                    {
                        SegmentHitResolver.ApplyDamageAndFeedback(target, damage, AttackProfile, hitPosition, transform.position, SegmentMonsterFeedbackKind.Continuous); // 지속 피해 + 피드백
                        PlayHitVfx(hitPosition); // 명중 VFX
                    }
                }

                timer -= tickInterval; // 시간 감소
                yield return wait;
            }

            laserRoutine = null; // 종료
        }
    }
}
