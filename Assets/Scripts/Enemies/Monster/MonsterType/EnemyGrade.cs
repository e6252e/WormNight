namespace TeamProject01.Gameplay //몬스터 타입
{
    public enum EnemyGrade // 몬스터 큰 등급
    {
        Monster = 0, // 일반 몬스터
        Elite = 1, // 엘리트 몬스터
        Boss = 2 // 보스 몬스터
    }

    public enum MonsterType // 일반 몬스터 타입
    {
        Melee = 0, // 근거리 일반 몬스터
        Ranged = 1 // 원거리 일반 몬스터
    }

    public enum EliteType // 엘리트 몬스터 타입
    {
        Obstacle = 0, // 방해형 엘리트
        Buff = 1 // 버프형 엘리트
    }

    public enum BossType // 보스 몬스터 타입
    {
        Boss01 = 0 // 1번 보스
    }
}