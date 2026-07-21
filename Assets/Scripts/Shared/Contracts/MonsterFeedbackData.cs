using UnityEngine;

namespace TeamProject01.Gameplay
{
    [System.Flags]
    public enum MonsterFeedbackType
    {
        None = 0,
        Knockback = 1 << 0,
        Stagger = 1 << 1
    }

    [System.Serializable]
    public struct MonsterFeedbackData
    {
        public MonsterFeedbackType Type;
        public Vector3 Origin;
        public Vector3 Direction;
        public Vector3 HitPosition;
        public float KnockbackDistance;
        public float KnockbackDuration;
        public float StaggerDuration;
        public int SourceSegmentIndex;
        public DamageType SourceDamageType;
        public GameObject SourceObject;

        public bool HasKnockback => (Type & MonsterFeedbackType.Knockback) != 0 && KnockbackDistance > 0.0f && KnockbackDuration > 0.0f;
        public bool HasStagger => (Type & MonsterFeedbackType.Stagger) != 0 && StaggerDuration > 0.0f;
        public bool IsValid => HasKnockback || HasStagger;

        public static MonsterFeedbackData Create(
            Vector3 origin,
            Vector3 direction,
            Vector3 hitPosition,
            float knockbackDistance,
            float knockbackDuration,
            float staggerDuration,
            int sourceSegmentIndex,
            DamageType sourceDamageType,
            GameObject sourceObject)
        {
            direction.y = 0.0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = hitPosition - origin;
                direction.y = 0.0f;
            }

            if (direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();
            }

            MonsterFeedbackType type = MonsterFeedbackType.None;

            if (knockbackDistance > 0.0f && knockbackDuration > 0.0f)
            {
                type |= MonsterFeedbackType.Knockback;
            }

            if (staggerDuration > 0.0f)
            {
                type |= MonsterFeedbackType.Stagger;
            }

            return new MonsterFeedbackData
            {
                Type = type,
                Origin = origin,
                Direction = direction,
                HitPosition = hitPosition,
                KnockbackDistance = knockbackDistance,
                KnockbackDuration = knockbackDuration,
                StaggerDuration = staggerDuration,
                SourceSegmentIndex = sourceSegmentIndex,
                SourceDamageType = sourceDamageType,
                SourceObject = sourceObject
            };
        }
    }
}
