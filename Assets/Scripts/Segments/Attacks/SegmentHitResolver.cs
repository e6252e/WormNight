using UnityEngine;

namespace TeamProject01.Gameplay
{
    public enum SegmentMonsterFeedbackKind
    {
        Direct,
        Explosion,
        Pierce,
        Chain,
        Continuous
    }

    public static class SegmentHitResolver
    {
        public static void ApplyDamageAndFeedback(
            EnemyController enemy,
            DamageData damage,
            SegmentAttackProfile profile,
            Vector3 hitPosition,
            Vector3 feedbackOrigin,
            SegmentMonsterFeedbackKind feedbackKind)
        {
            if (!SegmentTargetQuery.IsEnemyUsable(enemy))
            {
                return;
            }

            DamageData resolvedDamage = damage.WithHitPosition(hitPosition);
            enemy.ApplyDamage(resolvedDamage);

            MonsterFeedbackData feedback = CreateFeedback(enemy, resolvedDamage, profile, hitPosition, feedbackOrigin, feedbackKind);
            MonsterFeedbackApi.TryApplyFeedback(enemy, feedback);
            ApplyStatusEffect(enemy, resolvedDamage, profile, hitPosition);
        }

        private static void ApplyStatusEffect(EnemyController enemy, DamageData damage, SegmentAttackProfile profile, Vector3 hitPosition)
        {
            if (profile == null || profile.StatusEffectOnHit == CombatStatusEffectKind.None)
            {
                return;
            }

            EnemySupportDebuffState state = EnemySupportDebuffState.GetOrAdd(enemy);
            if (state != null)
            {
                state.ApplyStatusEffect(profile.StatusEffectOnHit, damage.SourceSegmentIndex, damage.SourceObject, hitPosition, profile.StatusEffectVfxPrefab);
            }
        }

        private static MonsterFeedbackData CreateFeedback(
            EnemyController enemy,
            DamageData damage,
            SegmentAttackProfile profile,
            Vector3 hitPosition,
            Vector3 feedbackOrigin,
            SegmentMonsterFeedbackKind feedbackKind)
        {
            if (profile == null || !profile.ApplyMonsterFeedback)
            {
                return default;
            }

            float scale = GetFeedbackScale(profile, feedbackKind);

            if (scale <= 0.0f)
            {
                return default;
            }

            float knockbackDistance = profile.MonsterKnockbackDistance * scale;
            float staggerDuration = profile.MonsterStaggerDuration * scale;

            if (feedbackKind == SegmentMonsterFeedbackKind.Chain)
            {
                knockbackDistance = 0.0f;
            }

            Vector3 direction = enemy.transform.position - feedbackOrigin;
            direction.y = 0.0f;

            if (direction.sqrMagnitude <= 0.0001f && damage.SourceObject != null)
            {
                direction = enemy.transform.position - damage.SourceObject.transform.position;
                direction.y = 0.0f;
            }

            return MonsterFeedbackData.Create(
                feedbackOrigin,
                direction,
                hitPosition,
                knockbackDistance,
                profile.MonsterKnockbackDuration,
                staggerDuration,
                damage.SourceSegmentIndex,
                damage.Type,
                damage.SourceObject);
        }

        private static float GetFeedbackScale(SegmentAttackProfile profile, SegmentMonsterFeedbackKind feedbackKind)
        {
            return feedbackKind switch
            {
                SegmentMonsterFeedbackKind.Explosion => profile.MonsterExplosionFeedbackMultiplier,
                SegmentMonsterFeedbackKind.Pierce => profile.MonsterPierceFeedbackMultiplier,
                SegmentMonsterFeedbackKind.Continuous => profile.MonsterContinuousFeedbackMultiplier,
                _ => 1.0f
            };
        }
    }
}
