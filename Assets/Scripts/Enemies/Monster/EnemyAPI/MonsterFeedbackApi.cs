namespace TeamProject01.Gameplay
{
    public static class MonsterFeedbackApi // 전찬우추가-6019(몬스터피드백관련) - 몬스터 피드백 진입점
    {
        public static bool TryApplyFeedback(EnemyController enemy, MonsterFeedbackData feedback) // 전찬우추가-6019(몬스터피드백관련) - 경직/넉백 요청
        {
            if (enemy == null || !feedback.IsValid)
            {
                return false;
            }

            EnemyMovement movement = enemy.GetComponent<EnemyMovement>(); // 전찬우추가-6019(몬스터피드백관련) - 이동 컴포넌트 경계

            if (movement == null)
            {
                return false;
            }

            movement.ApplyMonsterFeedback(feedback); // 전찬우추가-6019(몬스터피드백관련) - 실제 이동 반영
            return true;
        }
    }
}
