using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public static class EnemyTags // 몬스터 태그 규격
    {
        public const string Monster = "Monster"; // 일반 몬스터 태그
        public const string Elite = "Elite"; // 엘리트 태그
        public const string Boss = "Boss"; // 보스 태그

        public static readonly string[] TargetTags = { Monster, Elite, Boss }; // 탐색 대상 태그
        private static readonly HashSet<string> WarnedMissingTags = new HashSet<string>(); // 경고 중복 방지

        public static string FromGrade(EnemyGrade grade) // 등급 → 태그
        {
            switch (grade)
            {
                case EnemyGrade.Elite:
                    return Elite; // 엘리트
                case EnemyGrade.Boss:
                    return Boss; // 보스
                default:
                    return Monster; // 일반
            }
        }

        public static bool TryApplyTag(GameObject target, EnemyGrade grade) // 태그 적용
        {
            if (target == null)
            {
                return false; // 대상 없음
            }

            string tagName = FromGrade(grade); // 태그 선택
            try
            {
                target.tag = tagName; // Unity 태그 적용
                return true; // 성공
            }
            catch (UnityException)
            {
                if (WarnedMissingTags.Add(tagName))
                {
                    Debug.LogWarning($"Enemy tag '{tagName}' is not registered.", target); // 태그 누락
                }

                return false; // 실패
            }
        }
    }
}
