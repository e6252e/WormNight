using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class SegmentProjectileRuntime
    {
        private Vector3 GetEnemyHitPosition(EnemyController enemy) // 몬스터 중심 위치
        {
            float targetAimHeight = profile != null ? profile.TargetAimHeight : 0.45f; // 조준 높이
            return SegmentTargetQuery.GetEnemyHitPosition(enemy, transform.position, targetAimHeight); // 공용 중심 계산
        }

        private void TryApplyHitAt(Vector3 position) // 위치 명중 확인
        {
            Collider[] hits = Physics.OverlapSphere(position, profile.ProjectileHitRadius); // 반경 검색
            for (int i = 0; i < hits.Length; i++)
            {
                EnemyController enemy = hits[i].GetComponentInParent<EnemyController>(); // 몬스터
                if (!SegmentTargetQuery.IsEnemyUsable(enemy) || hitEnemyIds.Contains(enemy.EnemyId))
                {
                    continue; // 대상 아님/중복
                }

                Vector3 hitPosition = GetEnemyHitPosition(enemy); // 명중 위치
                ApplyImpactAt(hitPosition, enemy); // 명중 처리
                if (this == null)
                {
                    return; // 제거됨
                }

                if (profile.MoveType != SegmentAttackMoveType.PiercingProjectile && profile.ImpactType != SegmentAttackImpactType.PierceDamage)
                {
                    return; // 단일 명중
                }
            }
        }

        private void ApplyImpactAt(Vector3 position, EnemyController enemy) // 명중 처리
        {
            if (profile.ImpactType == SegmentAttackImpactType.ExplosionArea)
            {
                ClearAreaTelegraph(); // 폭발 순간 장판 제거
                ApplyExplosion(position); // 범위 피해
                Destroy(gameObject); // 투사체 제거
                return;
            }

            if (SegmentTargetQuery.IsEnemyUsable(enemy))
            {
                bool isPiercingHit = profile.MoveType == SegmentAttackMoveType.PiercingProjectile || profile.ImpactType == SegmentAttackImpactType.PierceDamage; // 관통탄 여부
                DamageData hitDamage = isPiercingHit ? damage.WithAmount(damage.Amount * GetPiercingDamageRatio()) : damage; // 관통 피해 비율
                SegmentMonsterFeedbackKind feedbackKind = isPiercingHit ? SegmentMonsterFeedbackKind.Pierce : SegmentMonsterFeedbackKind.Direct; // 피드백 종류
                SegmentHitResolver.ApplyDamageAndFeedback(enemy, hitDamage, profile, position, transform.position, feedbackKind); // 직접/관통 피해 + 피드백
                PlayHitVfx(position); // 명중 VFX
                PlayProjectileHitSfx(position);
                hitEnemyIds.Add(enemy.EnemyId); // 관통 중복 방지
            }

            if (profile.MoveType == SegmentAttackMoveType.PiercingProjectile || profile.ImpactType == SegmentAttackImpactType.PierceDamage)
            {
                remainingPierces--; // 관통 소모
                if (remainingPierces > 0)
                {
                    return; // 계속 비행
                }
            }

            Destroy(gameObject); // 종료
        }

        private bool TryExplodeOnGroundContact(Vector3 previousPosition, Vector3 currentPosition) // 폭발탄 바닥 충돌
        {
            if (profile == null || profile.ImpactType != SegmentAttackImpactType.ExplosionArea)
            {
                return false; // 폭발탄만 바닥 폭발
            }

            Vector3 movement = currentPosition - previousPosition; // 이동량
            if (movement.sqrMagnitude <= 0.000001f)
            {
                return false; // 이동 없음
            }

            float contactHeight = GetGroundContactHeight(); // 중심 기준 접촉 높이
            float previousClearance = previousPosition.y - GroundService.ProjectToGround(previousPosition, 0f).y; // 이전 바닥 높이 차
            float currentClearance = currentPosition.y - GroundService.ProjectToGround(currentPosition, 0f).y; // 현재 바닥 높이 차
            if (currentClearance > contactHeight || previousClearance <= currentClearance)
            {
                return false; // 아직 바닥에 닿지 않음
            }

            float clearanceDelta = previousClearance - currentClearance; // 통과 깊이
            float contactRatio = previousClearance > contactHeight && clearanceDelta > 0.0001f
                ? Mathf.Clamp01((previousClearance - contactHeight) / clearanceDelta)
                : 1f; // 이미 접촉권이면 현재 위치 기준
            Vector3 contactSample = Vector3.Lerp(previousPosition, currentPosition, contactRatio); // 예상 접촉 위치
            Vector3 groundPoint = GroundService.ProjectToGround(contactSample, 0f); // 실제 바닥점
            ApplyImpactAt(groundPoint, null); // 바닥 폭발
            return true; // 처리 완료
        }

        private float GetGroundContactHeight() // 폭발탄 지면 접촉 여유
        {
            float hitRadius = profile != null ? Mathf.Max(0f, profile.ProjectileHitRadius) : 0f; // 전투 판정 반경
            return Mathf.Clamp(hitRadius * 0.5f, 0.03f, 0.35f); // 너무 일찍/늦게 터지지 않게 제한
        }

        private float GetPiercingDamageRatio() // 일반 관통탄 피해 비율
        {
            return profile != null ? Mathf.Clamp01(profile.PiercingProjectileDamageRatio) : 1f; // 기본 100%
        }

        private void ApplyExplosion(Vector3 position) // 폭발 처리
        {
            ApplyExplosion(position, GetExplosionRadius(), explosionEnemyIds, true); // 강화 반경 폭발
        }

        private void ApplyExplosion(Vector3 position, float radius, List<int> hitIds, bool playVfx) // 반경 피해 처리
        {
            float damageRadius = Mathf.Max(0f, radius); // 반경 보정
            if (damageRadius <= 0f)
            {
                return; // 범위 없음
            }

            if (playVfx)
            {
                PlayExplosionVfx(position, damageRadius); // 폭발 VFX
                PlayProjectileExplosionSfx(position);
            }

            DamageData explosionDamage = DamageData.Create(damage.Amount, DamageType.Explosion, damage.SourceSegmentIndex, position, damage.SourceObject); // 폭발 피해
            Collider[] hits = Physics.OverlapSphere(position, damageRadius); // 범위 검색
            for (int i = 0; i < hits.Length; i++)
            {
                EnemyController enemy = hits[i].GetComponentInParent<EnemyController>(); // 몬스터
                if (!SegmentTargetQuery.IsEnemyUsable(enemy) || hitIds.Contains(enemy.EnemyId))
                {
                    continue; // 대상 아님/중복
                }

                hitIds.Add(enemy.EnemyId); // 중복 방지
                Vector3 hitPosition = GetEnemyHitPosition(enemy); // 명중 위치
                SegmentHitResolver.ApplyDamageAndFeedback(enemy, explosionDamage, profile, hitPosition, position, SegmentMonsterFeedbackKind.Explosion); // 범위 피해 + 피드백
                ApplyExplosionDebuff(enemy); // 감속 등 폭발 부가 효과
            }
        }

        private void ApplyExplosionDebuff(EnemyController enemy) // 폭발 부가 디버프
        {
            if (!SegmentTargetQuery.IsEnemyUsable(enemy)
                || profile == null
                || profile.StatusEffectOnHit != CombatStatusEffectKind.None
                || profile.SlowDuration <= 0f
                || profile.SlowMoveSpeedMultiplier >= 1f)
            {
                return; // 감속 없음
            }

            EnemySupportDebuffState state = EnemySupportDebuffState.GetOrAdd(enemy);
            if (state != null)
            {
                state.ApplyMoveSpeedSlow(profile.SlowMoveSpeedMultiplier, profile.SlowDuration); // 이동속도 감속
            }
        }

        private void ApplyLandingImpactDamage(Vector3 position) // 투석기 돌 착지 순간 작은 범위 피해
        {
            float radius = GetLandingImpactRadius(); // 강화 반영 착지 반경
            ApplyExplosion(position, radius, explosionEnemyIds, true); // 착지 충격파
        }

        private float GetLandingImpactRadius() // 착지 충격 반경
        {
            float radius = profile.LandingImpactRadius > 0f ? profile.LandingImpactRadius : profile.ProjectileHitRadius; // 기본 착지 반경
            if (profile.RollAfterArcLanding)
            {
                radius = Mathf.Max(radius, GetExplosionRadius()); // 투석기 폭발반경 강화 반영
            }

            return radius;
        }

        private void ApplyLandingRollDamage(Vector3 position) // 투석기 돌이 구르는 동안 주는 피해
        {
            float radius = profile.LandingRollDamageRadius > 0f ? profile.LandingRollDamageRadius : profile.ProjectileHitRadius; // 구르기 피해 반경
            if (radius <= 0f)
            {
                return; // 피해 반경 없음
            }

            float damageRatio = profile != null ? Mathf.Clamp01(profile.LandingRollDamageRatio) : 1f; // 구르기 피해 배율
            if (damageRatio <= 0f)
            {
                return; // 구르기 피해 없음
            }

            DamageData rollDamage = DamageData.Create(damage.Amount * damageRatio, DamageType.Projectile, damage.SourceSegmentIndex, position, damage.SourceObject); // 구르기 피해
            Collider[] hits = Physics.OverlapSphere(position, radius); // 돌 주변 검색
            for (int i = 0; i < hits.Length; i++)
            {
                EnemyController enemy = hits[i].GetComponentInParent<EnemyController>(); // 몬스터
                if (!SegmentTargetQuery.IsEnemyUsable(enemy) || hitEnemyIds.Contains(enemy.EnemyId))
                {
                    continue; // 대상 아님/이미 구르기 피해 받음
                }

                hitEnemyIds.Add(enemy.EnemyId); // 구르기 중복 방지
                Vector3 hitPosition = GetEnemyHitPosition(enemy); // 명중 위치
                SegmentHitResolver.ApplyDamageAndFeedback(enemy, rollDamage, profile, hitPosition, position, SegmentMonsterFeedbackKind.Direct); // 구르기 충돌 피해 + 피드백
                PlayHitVfx(hitPosition); // 명중 VFX
                PlayProjectileRollHitSfx(hitPosition);
            }
        }
    }
}
