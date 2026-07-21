using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamProject01.Gameplay
{
    public sealed class ExpTest : MonoBehaviour // 경험치 게이지 임시 테스트
    {
        public static ExpTest Active { get; private set; } // 현재 테스트

        [Min(1)] public int Level = 1; // 현재 레벨
        [Min(0)] public int CurrentExperience; // 현재 게이지 경험치
        [Min(1)] public int ExperiencePerLevel = 30; // 게이지 MAX
        [Min(1)] public int MaxLevel = 300; // 최대 레벨
        [Min(1)] public int ClickExperienceGain = 10; // 좌클릭당 증가량

        public float FillRatio => ExperiencePerLevel <= 0
            ? 0f
            : Mathf.Clamp01((float)CurrentExperience / ExperiencePerLevel); // 0~1

        public event Action Changed; // UI 갱신 알림
        public event Action LevelUpTriggered; // 레벨업 1회당 알림

        private void Awake() // 등록
        {
            Active = this; // 현재 인스턴스
        }

        private void OnDestroy() // 해제
        {
            if (Active == this)
            {
                Active = null; // 참조 제거
            }
        }

        private void Update() // 입력 테스트
        {
            if (Time.timeScale <= 0f)
            {
                return; // 레벨업 UI 일시정지 중
            }

            Mouse mouse = Mouse.current; // 새 Input System
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                AddExperience(ClickExperienceGain); // 좌클릭 +10
            }
        }

        public void AddExperience(int amount) // 경험치 추가
        {
            if (amount <= 0 || Level >= MaxLevel)
            {
                return; // 더 이상 증가 없음
            }

            CurrentExperience += amount; // 게이지 누적
            ProcessLevelUps(); // 30 도달 시 레벨업
            Changed?.Invoke(); // UI 알림
        }

        private void ProcessLevelUps() // 레벨업 처리
        {
            while (CurrentExperience >= ExperiencePerLevel && Level < MaxLevel)
            {
                CurrentExperience -= ExperiencePerLevel; // 게이지 초과분 이월
                Level++; // 레벨 증가
                Debug.Log($"레벨증가 {Level}", this); // 레벨업 로그
                LevelUpTriggered?.Invoke(); // 레벨업 UI 알림
            }

            if (Level >= MaxLevel)
            {
                Level = MaxLevel; // 상한 고정
                CurrentExperience = Mathf.Min(CurrentExperience, ExperiencePerLevel); // MAX 레벨 게이지 제한
            }
        }
    }
}
