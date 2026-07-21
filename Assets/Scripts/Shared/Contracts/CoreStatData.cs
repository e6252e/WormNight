using UnityEngine;

namespace TeamProject01.Gameplay
{
    [System.Serializable]
    public struct CoreStatData // 코어 → 세그먼트 전달값
    {
        public int Level; // 현재 레벨
        public float FlatDamageBonus; // 기본 공격력 고정 보너스
        public float DamageMultiplier; // 공격력 배율
        public float AttackSpeedMultiplier; // 공격속도 배율
        public float TurnSpeedBonus; // 회전력 보너스
        public float RejoinRangeBonus; // 재결합 범위 보너스
        public float CollisionForceBonus; // 충돌힘 보너스
        public int CurrentExperience; // 현재 레벨 경험치
        public int ExperienceToNextLevel; // 다음 레벨 필요 경험치
        public int TotalExperience; // 누적 경험치
        public int Gold; // 보유 골드
        public int CurrentRunDiamond; // 런 중 획득 다이아

        public float ExperienceRatio => ExperienceToNextLevel <= 0 ? 0f : Mathf.Clamp01((float)CurrentExperience / ExperienceToNextLevel); // 경험치 비율
        public bool CanLevelUp => CurrentExperience >= ExperienceToNextLevel; // 레벨시스템 판단용

        public static CoreStatData Default => new CoreStatData(1, 0f, 1f, 1f, 0f, 0f, 0f, 0, 5, 0, 0); // 기본값

        public CoreStatData(int level, float flatDamageBonus, float damageMultiplier, float attackSpeedMultiplier, float turnSpeedBonus, float rejoinRangeBonus, int currentExperience, int experienceToNextLevel, int totalExperience, int gold) // 생성
            : this(level, flatDamageBonus, damageMultiplier, attackSpeedMultiplier, turnSpeedBonus, rejoinRangeBonus, 0f, currentExperience, experienceToNextLevel, totalExperience, gold) // 기존 호환
        {
        }

        public CoreStatData(int level, float flatDamageBonus, float damageMultiplier, float attackSpeedMultiplier, float turnSpeedBonus, float rejoinRangeBonus, float collisionForceBonus, int currentExperience, int experienceToNextLevel, int totalExperience, int gold) // 생성
            : this(level, flatDamageBonus, damageMultiplier, attackSpeedMultiplier, turnSpeedBonus, rejoinRangeBonus, collisionForceBonus, currentExperience, experienceToNextLevel, totalExperience, gold, 0) // 기존 호환
        {
        }

        public CoreStatData(int level, float flatDamageBonus, float damageMultiplier, float attackSpeedMultiplier, float turnSpeedBonus, float rejoinRangeBonus, float collisionForceBonus, int currentExperience, int experienceToNextLevel, int totalExperience, int gold, int currentRunDiamond) // 생성
        {
            Level = Mathf.Max(1, level); // 레벨 보정
            FlatDamageBonus = Mathf.Max(0f, flatDamageBonus); // 고정 피해 보정
            DamageMultiplier = Mathf.Max(0f, damageMultiplier); // 공격력 보정
            AttackSpeedMultiplier = Mathf.Max(0.01f, attackSpeedMultiplier); // 속도 보정
            TurnSpeedBonus = turnSpeedBonus; // 회전 보너스
            RejoinRangeBonus = Mathf.Max(0f, rejoinRangeBonus); // 범위 보정
            CollisionForceBonus = collisionForceBonus; // 충돌힘 보정
            CurrentExperience = Mathf.Max(0, currentExperience); // 현재 경험치 보정
            ExperienceToNextLevel = Mathf.Max(1, experienceToNextLevel); // 필요 경험치 보정
            TotalExperience = Mathf.Max(0, totalExperience); // 누적 경험치 보정
            Gold = Mathf.Max(0, gold); // 골드 보정
            CurrentRunDiamond = Mathf.Max(0, currentRunDiamond); // 런 다이아 보정
        }

        public float ApplyDamage(float baseDamage) // 데미지 계산
        {
            return (Mathf.Max(0f, baseDamage) + FlatDamageBonus) * DamageMultiplier; // 고정값 + 배율
        }

        public float ApplyFireInterval(float baseInterval) // 공격속도 계산
        {
            return Mathf.Max(0.05f, baseInterval / AttackSpeedMultiplier); // 쿨타임 단축
        }
    }
}
