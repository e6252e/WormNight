using UnityEngine;

namespace TeamProject01.Gameplay
{
    public static class GameSpeedController // 2배속 설정 — timeScale 복원 //안건준 추가 - 0628
    {
        public const string SpeedPrefKey = "Settings.GameSpeed2X";

        public static bool IsDoubleSpeedPreferred()
        {
            return PlayerPrefs.GetInt(SpeedPrefKey, 0) == 1;
        }

        public static float GetDesiredTimeScale()
        {
            if (!IsDoubleSpeedPreferred())
            {
                return 1f;
            }

            ConvoyController convoy = Object.FindFirstObjectByType<ConvoyController>();
            if (convoy == null || !convoy.IsAutoOrbitActive)
            {
                return 1f;
            }

            return 2f;
        }

        public static void ApplyDesiredTimeScale()
        {
            if (Time.timeScale <= 0f)
            {
                return;
            }

            Time.timeScale = GetDesiredTimeScale();
        }
    }
}
