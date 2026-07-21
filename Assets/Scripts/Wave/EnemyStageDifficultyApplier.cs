namespace TeamProject01.Gameplay
{
    public static class EnemyStageDifficultyApplier
    {
        public static void Apply(EnemyController enemy, WaveStageDifficulty difficulty, bool applyNexusDamageMultiplier = true)
        {
            if (enemy == null || enemy.Grade == EnemyGrade.Boss)
            {
                return; // 보스는 BossWaveController 쪽 밸런스에서 별도 처리
            }

            enemy.GetComponent<EnemyHealth>()?.ApplyMaxHpMultiplierKeepingRatio(difficulty.HealthMultiplier);
            enemy.GetComponent<EnemyMovement>()?.ApplyMoveSpeedMultiplier(difficulty.MoveSpeedMultiplier);

            if (!applyNexusDamageMultiplier)
            {
                return; // 엘리트처럼 공격력 고정이 필요한 경우 체력/속도만 적용
            }

            enemy.GetComponent<EnemyMeleeAttack>()?.ApplyAttackDamageMultiplier(difficulty.NexusDamageMultiplier);
            enemy.GetComponent<EnemyRangedAttack>()?.ApplyAttackPowerMultiplier(difficulty.NexusDamageMultiplier);
        }
    }
}
